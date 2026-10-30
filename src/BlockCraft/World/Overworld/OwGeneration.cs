using System.Collections.Concurrent;
using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>Etap bazowy jest niezmienny. Populacja zapisuje całe cechy, również poza chunkiem źródłowym.
/// Złożenie ma stałą kolejność, niezależną od kolejności i wątku żądań ChunkManagera.</summary>
public sealed partial class OwGenerator
{
    public readonly long Seed;
    public readonly OwTerrain Terrain;
    public OwBiomeProvider Biomes => Terrain.Biomes;
    private readonly ConcurrentDictionary<long, Lazy<ProtoChunk>> _base = new();
    private readonly ConcurrentDictionary<long, Lazy<OwGenRegion>> _population = new();

    public OwGenerator(long seed, WorldType type)
    {
        Seed = seed;
        Terrain = new OwTerrain(seed, type == WorldType.Amplified, type == WorldType.LargeBiomes);
    }

    public ProtoChunk Base(int cx, int cz)
    {
        long key = MathUtil.ChunkKey(cx, cz);
        var p = _base.GetOrAdd(key, _ => new Lazy<ProtoChunk>(() =>
        {
            var raw = Terrain.Generate(cx, cz);
            OwCaves.Generate(Seed, cx, cz, raw);
            GenerateStructures(cx, cz, raw);
            return raw;
        })).Value;
        if (_base.Count > 512) _base.TryRemove(_base.Keys.First(), out _);
        return p;
    }

    partial void GenerateStructures(int cx, int cz, ProtoChunk p);
    partial void PopulateStructures(OwGenRegion g, JRandom r, int x, int z);

    public JRandom PopulationRandom(int cx, int cz)
    {
        var r = new JRandom(Seed);
        long a = r.NextLong() / 2 * 2 + 1, b = r.NextLong() / 2 * 2 + 1;
        r.SetSeed(unchecked((cx * a + cz * b) ^ Seed));
        return r;
    }

    private OwGenRegion Population(int cx, int cz)
    {
        long key = MathUtil.ChunkKey(cx, cz);
        var result = _population.GetOrAdd(key, _ => new Lazy<OwGenRegion>(() =>
        {
            var g = new OwGenRegion(this);
            int x = cx * 16, z = cz * 16;
            var r = PopulationRandom(cx, cz);
            var b = Biome.Get(Biomes.BiomeAt(x + 16, z + 16));
            if (b.Id != Biome.Desert && b.Id != Biome.DesertHills && r.NextInt(4) == 0)
                OwFeatures.Lake(g, r, x + r.NextInt(16) + 8, r.NextInt(256), z + r.NextInt(16) + 8, B.Water);
            if (r.NextInt(8) == 0)
            {
                int lx = x + r.NextInt(16) + 8, ly = r.NextInt(r.NextInt(248) + 8), lz = z + r.NextInt(16) + 8;
                if (ly < 63 || r.NextInt(10) == 0) OwFeatures.Lake(g, r, lx, ly, lz, B.Lava);
            }
            PopulateStructures(g, r, x, z);
            OwDecorator.Decorate(g, r, b, x, z);
            for (int dx = 8; dx < 24; dx++)
                for (int dz = 8; dz < 24; dz++)
                {
                    int wx = x + dx, wz = z + dz, y = g.Height(wx, wz);
                    if (!Biome.IsSnowyAt(Biomes.BiomeAt(wx, wz), wx, y, wz)) continue;
                    if (g.Get(wx, y - 1, wz) == B.Water && g.GetMeta(wx, y - 1, wz) == 0) g.Set(wx, y - 1, wz, B.Ice, 0);
                    if (g.Get(wx, y, wz) == B.Air && g.Solid(wx, y - 1, wz) && g.Get(wx, y - 1, wz) != B.Ice) g.Set(wx, y, wz, B.SnowLayer, 0);
                }
            g.ReleaseReadCache();
            return g;
        })).Value;
        if (_population.Count > 256) _population.TryRemove(_population.Keys.First(), out _);
        return result;
    }

    public Chunk Generate(int cx, int cz)
    {
        var c = new Chunk(cx, cz);
        var raw = Base(cx, cz);
        Array.Copy(raw.Blocks, c.Blocks, Chunk.Volume);
        Array.Copy(raw.Meta, c.Meta, Chunk.Volume);
        Array.Copy(raw.Biomes, c.Biomes, 256);
        // Duże żyły kamieni mogą przekroczyć drugi dodatni chunk od źródła.
        for (int sx = cx - 2; sx <= cx + 1; sx++)
            for (int sz = cz - 2; sz <= cz + 1; sz++) Population(sx, sz).Apply(c);
        c.RecountSections(); c.RecalcHeightMap();
        return c;
    }

    public (int x, int y, int z) FindSpawn()
    {
        var r = new JRandom(Seed);
        int[] allowed = { Biome.Forest, Biome.Plains, Biome.Taiga, Biome.TaigaHills, Biome.ForestHills, Biome.Jungle, Biome.JungleHills };
        var near = Biomes.FindBiomePosition(0, 0, 256, allowed, r) ?? (0, 0);
        int x = near.x, z = near.z;
        for (int i = 0; i < 1000; i++)
        {
            var p = Base(x >> 4, z >> 4);
            int y = 255;
            while (y > 0 && p.Get(x & 15, y, z & 15) == B.Air) y--;
            if (y >= 63 && p.Get(x & 15, y, z & 15) == B.Grass) return (x, y + 1, z);
            x += r.NextInt(64) - r.NextInt(64); z += r.NextInt(64) - r.NextInt(64);
        }
        var fallback = Base(0, 0); int fy = 255;
        while (fy > 0 && fallback.Get(0, fy, 0) == B.Air) fy--;
        return (0, fy + 1, 0);
    }
}

/// <summary>Transakcja populacji: odczyt terenu i własnych zapisów cechy, bez modyfikowania bazowego cache.</summary>
public sealed class OwGenRegion : IGenAccess
{
    public readonly OwGenerator Generator;
    private readonly Dictionary<long, ProtoChunk> _read = new();
    private readonly Dictionary<long, Dictionary<int, (byte id, byte meta)>> _writes = new();
    public OwGenRegion(OwGenerator generator) { Generator = generator; }
    private ProtoChunk Read(int x, int z)
    {
        long key = MathUtil.ChunkKey(x >> 4, z >> 4);
        if (!_read.TryGetValue(key, out var p)) _read[key] = p = Generator.Base(x >> 4, z >> 4);
        return p;
    }
    public int Get(int x, int y, int z)
    {
        if ((uint)y >= 256) return B.Air;
        if (_writes.TryGetValue(MathUtil.ChunkKey(x >> 4, z >> 4), out var edits) && edits.TryGetValue(Chunk.Index(x & 15, y, z & 15), out var s)) return s.id;
        return Read(x, z).Get(x & 15, y, z & 15);
    }
    public int GetMeta(int x, int y, int z)
    {
        if ((uint)y >= 256) return 0;
        if (_writes.TryGetValue(MathUtil.ChunkKey(x >> 4, z >> 4), out var edits) && edits.TryGetValue(Chunk.Index(x & 15, y, z & 15), out var s)) return s.meta;
        return Read(x, z).Meta[Chunk.Index(x & 15, y, z & 15)];
    }
    public void Set(int x, int y, int z, int id, int meta = 0)
    {
        if ((uint)y >= 256) return;
        long key = MathUtil.ChunkKey(x >> 4, z >> 4);
        if (!_writes.TryGetValue(key, out var edits)) _writes[key] = edits = new();
        edits[Chunk.Index(x & 15, y, z & 15)] = ((byte)id, (byte)meta);
    }
    public bool Solid(int x, int y, int z) => Block.Get(Get(x, y, z)).Solid;
    public int Height(int x, int z)
    {
        int y = 255;
        while (y >= 0 && Block.Get(Get(x, y, z)).LightOpacity == 0) y--;
        return y + 1;
    }
    public int TopSolid(int x, int z)
    {
        int y = 255;
        while (y >= 0)
        {
            var b = Block.Get(Get(x, y, z));
            if (b.Solid && b is not LeavesBlock) return y + 1;
            y--;
        }
        return 0;
    }
    public void ReleaseReadCache() => _read.Clear();
    public void Apply(Chunk c)
    {
        if (!_writes.TryGetValue(MathUtil.ChunkKey(c.Cx, c.Cz), out var edits)) return;
        foreach (var (index, state) in edits) { c.Blocks[index] = state.id; c.Meta[index] = state.meta; }
    }
}
