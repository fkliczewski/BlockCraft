using BlockCraft.Core;

namespace BlockCraft.World;

public enum WorldType { Default = 0, Superflat = 1, LargeBiomes = 2, Amplified = 3 }

public interface ITreeWriter
{
    int Get(int x, int y, int z);
    void Set(int x, int y, int z, byte id, int meta);
}

public sealed class WorldTreeWriter : ITreeWriter
{
    private readonly World _w;
    public WorldTreeWriter(World w) { _w = w; }
    public int Get(int x, int y, int z) => _w.GetBlock(x, y, z);
    public void Set(int x, int y, int z, byte id, int meta) => _w.SetBlock(x, y, z, id, meta);
}

public static class TreeGen
{
    private static bool CanReplace(int id) => id == B.Air || id == B.OakLeaves || id == B.BirchLeaves || id == B.TallGrass
                                              || id == B.Dandelion || id == B.Poppy || id == B.OakSapling || id == B.BirchSapling || id == B.Snow;

    /// <summary>type: 0 = dąb, 1 = brzoza, 2 = duży dąb. Zwraca false gdy brak miejsca.</summary>
    public static bool Generate(ITreeWriter w, int x, int y, int z, int type, Rng r, bool worldGen)
    {
        if (type == 0 && r.Next(10) == 0 && worldGen) type = 2;
        int ground = w.Get(x, y - 1, z);
        if (ground != B.Grass && ground != B.Dirt) return false;
        if (type == 2) return BigOak(w, x, y, z, r, worldGen);
        int height = type == 1 ? r.Next(3) + 5 : r.Next(3) + 4;
        if (y + height + 1 >= Chunk.Height) return false;
        if (!worldGen)
        {
            for (int yy = y; yy <= y + height + 1; yy++)
            {
                int rad = yy == y ? 0 : yy >= y + height - 1 ? 2 : 1;
                for (int dx = -rad; dx <= rad; dx++) for (int dz = -rad; dz <= rad; dz++)
                        if (!CanReplace(w.Get(x + dx, yy, z + dz)) && !(dx == 0 && dz == 0 && yy == y)) return false;
            }
        }
        byte log = type == 1 ? B.BirchLog : B.OakLog;
        byte leaves = type == 1 ? B.BirchLeaves : B.OakLeaves;
        w.Set(x, y - 1, z, B.Dirt, 0);
        for (int yy = y + height - 3; yy <= y + height; yy++)
        {
            int dy = yy - (y + height);
            int rad = 1 - dy / 2;
            for (int dx = -rad; dx <= rad; dx++)
                for (int dz = -rad; dz <= rad; dz++)
                {
                    if (Math.Abs(dx) == rad && Math.Abs(dz) == rad && (r.Next(2) == 0 || dy == 0)) continue;
                    if (CanReplace(w.Get(x + dx, yy, z + dz)) && w.Get(x + dx, yy, z + dz) == B.Air || w.Get(x + dx, yy, z + dz) == B.TallGrass)
                        w.Set(x + dx, yy, z + dz, leaves, 0);
                }
        }
        for (int i = 0; i < height; i++)
        {
            int id = w.Get(x, y + i, z);
            if (CanReplace(id) || i == 0) w.Set(x, y + i, z, log, 0);
        }
        return true;
    }

    private static bool BigOak(ITreeWriter w, int x, int y, int z, Rng r, bool worldGen)
    {
        int height = r.Next(5) + 8;
        if (y + height + 4 >= Chunk.Height) return false;
        if (!worldGen)
            for (int yy = y + 1; yy < y + height; yy++) if (!CanReplace(w.Get(x, yy, z))) return false;
        w.Set(x, y - 1, z, B.Dirt, 0);
        var nodes = new List<(int, int, int)>();
        nodes.Add((x, y + height, z));
        int branches = 2 + r.Next(3);
        for (int i = 0; i < branches; i++)
        {
            double ang = r.NextDouble() * Math.PI * 2;
            int by = y + (int)(height * (0.45 + r.NextDouble() * 0.4));
            int len = 2 + r.Next(3);
            int ex = x + (int)Math.Round(Math.Cos(ang) * len), ez = z + (int)Math.Round(Math.Sin(ang) * len);
            int ey = by + len / 2 + 1;
            // gałąź z drewna
            int steps = Math.Max(Math.Abs(ex - x), Math.Max(Math.Abs(ez - z), Math.Abs(ey - by)));
            for (int s = 1; s <= steps; s++)
            {
                double t = (double)s / steps;
                int bx = (int)Math.Round(x + (ex - x) * t), bz = (int)Math.Round(z + (ez - z) * t), byy = (int)Math.Round(by + (ey - by) * t);
                int axis = Math.Abs(ex - x) > Math.Abs(ez - z) ? 1 : 2;
                if (Math.Abs(ey - by) >= Math.Max(Math.Abs(ex - x), Math.Abs(ez - z))) axis = 0;
                if (CanReplace(w.Get(bx, byy, bz))) w.Set(bx, byy, bz, B.OakLog, axis);
            }
            nodes.Add((ex, ey, ez));
        }
        foreach (var (nx, ny, nz) in nodes)
        {
            for (int dy = -1; dy <= 2; dy++)
            {
                int rad = dy == -1 || dy == 2 ? 2 : 3;
                for (int dx = -rad; dx <= rad; dx++)
                    for (int dz = -rad; dz <= rad; dz++)
                    {
                        double d = Math.Sqrt(dx * dx + dz * dz);
                        if (d > rad + 0.3 - (r.Next(3) == 0 ? 0.6 : 0)) continue;
                        int id = w.Get(nx + dx, ny + dy, nz + dz);
                        if (id == B.Air || id == B.TallGrass) w.Set(nx + dx, ny + dy, nz + dz, B.OakLeaves, 0);
                    }
            }
        }
        for (int i = 0; i < height; i++)
        {
            int id = w.Get(x, y + i, z);
            if (CanReplace(id) || i == 0) w.Set(x, y + i, z, B.OakLog, 0);
        }
        return true;
    }
}

/// <summary>Publiczne API generatora; algorytmy OW są w World/Overworld.</summary>
public sealed class WorldGenerator
{
    public const int SeaLevel = 63;
    public readonly long Seed;
    public readonly WorldType Type;
    public readonly OwGenerator Overworld;
    public OwBiomeProvider Biomes => Overworld.Biomes;
    public WorldGenerator(long seed, WorldType type) { Seed = seed; Type = type; Overworld = new OwGenerator(seed, type); }
    public byte BiomeAt(int x, int z) => Type == WorldType.Superflat ? Biome.Plains : Biomes.BiomeAt(x, z);
    public double Temperature(int x, int z) => Biome.Get(BiomeAt(x, z)).Temp;
    public double DesertWeight(int x, int z) => Biome.Get(BiomeAt(x, z)).Kind == BiomeKind.Desert ? 1 : 0;
    public Chunk Generate(int cx, int cz)
    {
        if (Type != WorldType.Superflat) return Overworld.Generate(cx, cz);
        var c = new Chunk(cx, cz);
        for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++)
        {
            c.Blocks[Chunk.Index(x, 0, z)] = B.Bedrock;
            c.Blocks[Chunk.Index(x, 1, z)] = B.Dirt;
            c.Blocks[Chunk.Index(x, 2, z)] = B.Dirt;
            c.Blocks[Chunk.Index(x, 3, z)] = B.Grass;
            c.Biomes[z * 16 + x] = Biome.Plains;
        }
        c.RecountSections(); c.RecalcHeightMap();
        return c;
    }
    public (int x, int y, int z) FindSpawn() => Type == WorldType.Superflat ? (0, 4, 0) : Overworld.FindSpawn();
}