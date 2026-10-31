using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>SpawnerAnimals z MC 1.8: cztery kategorie (potwory, zwierzęta, nietoperze, wodne) z list biomów (BiomeSpawns).</summary>
public sealed class MobSpawner
{
    private readonly World.World _w;
    public MobSpawner(World.World w) { _w = w; }

    /// <summary>Tworzy dowolną zarejestrowaną encję (moby i inne, np. /summon minecart).</summary>
    public static Entity Create(string key, World.World w)
    {
        key = key.ToLowerInvariant();
        if (key.StartsWith("minecraft:")) key = key[10..];
        return EntitySerializer.Create(key, w);
    }
    public static IEnumerable<string> MobKeys => Registries.MobKeys;

    private static readonly SpawnKind[] Kinds = { SpawnKind.Monster, SpawnKind.Creature, SpawnKind.Ambient, SpawnKind.Water };

    internal SpawnEntry[] EntriesAt(SpawnKind kind, int x, int y, int z)
    {
        if (kind == SpawnKind.Monster && _w.Dimension == DimensionId.Overworld && _w.Type != WorldType.Superflat
            && BiomeSpawns.ForStructure(_w.Generator.Overworld.StructureMonsterAt(x, y, z)) is { } special) return special;
        return BiomeSpawns.For(_w.GetBiome(x, z), kind);
    }

    internal (HashSet<long> Chunks, int CapChunks) SpawnRegion()
    {
        var chunks = new HashSet<long>(); int count = 0;
        foreach (var p in _w.Players)
        {
            if (p.Spectator) continue;
            int pcx = MathUtil.FloorToInt(p.X) >> 4, pcz = MathUtil.FloorToInt(p.Z) >> 4;
            for (int dx = -8; dx <= 8; dx++) for (int dz = -8; dz <= 8; dz++)
                {
                    long key = MathUtil.ChunkKey(pcx + dx, pcz + dz);
                    if (chunks.Contains(key)) continue;
                    // Jak licznik i w SpawnerAnimals 1.8: pierścień zwiększa cap,
                    // ale nie trafia do zbioru prób. Wspólny pierścień graczy liczy się ponownie.
                    count++;
                    if (Math.Abs(dx) != 8 && Math.Abs(dz) != 8) chunks.Add(key);
                }
        }
        return (chunks, count);
    }

    internal int CountForCap(SpawnKind kind) => _w.Entities.Count(e => !e.Removed
        && !(e is Mob m && (m.PersistenceRequired || !string.IsNullOrEmpty(m.CustomName))) && BiomeSpawns.CountsAs(e, kind));

    internal bool FarEnough(double x, int y, double z)
    {
        if (_w.Players.Any(p => !p.Spectator && p.DistanceSq(x, y, z) < 24 * 24)) return false;
        double dx = _w.SpawnX - x, dy = _w.SpawnY - y, dz = _w.SpawnZ - z;
        return dx * dx + dy * dy + dz * dz >= 24 * 24;
    }

    public void Tick()
    {
        if (_w.IsRemote || _w.Players.Count == 0) return;
        var (region, capChunks) = SpawnRegion();
        // Zasięg próby to 15×15. Nie wymuszaj generacji brakujących chunków.
        var chunks = region.Where(k => _w.GetChunk((int)(k >> 32), (int)k)?.Merged == true).ToHashSet();
        if (chunks.Count == 0) return;
        foreach (var kind in Kinds)
        {
            if (kind == SpawnKind.Monster && _w.Difficulty == Difficulty.Peaceful) continue;
            if (kind == SpawnKind.Creature && _w.TotalTime % 400 != 0) continue; // EnumCreatureType.CREATURE.getAnimal()
            int cap = BiomeSpawns.Cap(kind) * capChunks / 289;
            // MC sprawdza <=, więc grupa może powstać także dokładnie na progu limitu.
            if (CountForCap(kind) <= cap) SpawnPass(chunks, kind);
        }
    }

    private void SpawnPass(HashSet<long> chunks, SpawnKind kind)
    {
        var r = _w.Rand;
        foreach (long k in chunks)
        {
            int cx = (int)(k >> 32), cz = (int)k;
            var c = _w.GetChunk(cx, cz);
            if (c == null) continue;
            int lx = r.Next(16), lz = r.Next(16);
            int top = Math.Min(Chunk.Height, (c.GetHeight(lx, lz) + 1 + 15) & ~15);
            int x = cx * 16 + lx, y = r.Next(Math.Max(1, top)), z = cz * 16 + lz;
            if (Block.Get(_w.GetBlock(x, y, z)).Opaque) continue;
            int inChunk = 0;
            for (int pack = 0; pack < 3; pack++)
            {
                int px = x, pz = z;
                SpawnEntry entry = null;
                for (int i = 0; i < 4; i++)
                {
                    px += r.Next(6) - r.Next(6); pz += r.Next(6) - r.Next(6);
                    if (!_w.IsLoadedAt(px, pz)) continue;
                    if (!FarEnough(px + 0.5, y, pz + 0.5)) continue;
                    var list = EntriesAt(kind, px, y, pz);
                    if (entry == null && (entry = BiomeSpawns.Pick(list, r)) == null) break;
                    // Grupa może przekroczyć ścianę monumentu lub granicę chatki.
                    // SpawnListEntry ma w MC tożsamość obiektu: wiedźma o wadze 1
                    // z chatki nie jest tą samą ofertą co rzadka wiedźma z listy biomu.
                    if (entry == null || !list.Any(e => ReferenceEquals(e, entry))) continue;
                    if (!PlacementOk(px, y, pz, kind == SpawnKind.Water || entry.Key == "guardian")) continue;
                    if (Create(entry.Key, _w) is not Mob m) break;
                    m.SetPositionAndPrev(px + 0.5, y, pz + 0.5);
                    m.Yaw = m.PrevYaw = r.NextFloat() * 360f;
                    if (!m.CanSpawnHere()) continue;
                    // Następna próba w grupie musi już widzieć kolizję z tym mobem.
                    _w.AddEntityNow(m);
                    if (++inChunk >= MaxInChunk(m)) goto nextChunk;
                }
            }
        nextChunk:;
        }
    }

    /// <summary>getMaxSpawnedInChunk: domyślnie 4, wilk 8.</summary>
    private static int MaxInChunk(Mob m) => m is Wolf ? 8 : 4;

    /// <summary>canCreatureTypeSpawnAtLocation: na ziemi (stały blok pod spodem, wolne 2 bloki) albo w wodzie.</summary>
    private bool PlacementOk(int x, int y, int z, bool water)
    {
        if (y < 1 || y >= Chunk.Height - 2) return false;
        var at = Block.Get(_w.GetBlock(x, y, z)); var below = Block.Get(_w.GetBlock(x, y - 1, z)); var above = Block.Get(_w.GetBlock(x, y + 1, z));
        if (water) return at.IsLiquid && at.Id != B.Lava && below.IsLiquid && !above.Opaque;
        if (!below.Opaque || below.Id == B.Bedrock) return false;
        return !at.Opaque && !at.IsLiquid && !above.Opaque && !above.IsLiquid;
    }

    /// <summary>SpawnerAnimals.performWorldGenSpawning: zwierzęta przy generowaniu chunka (szansa 0.1, grupa min-max z listy biomu).</summary>
    public void PopulateChunk(Chunk c)
    {
        if (_w.IsRemote || c.FromDisk) return;
        var r = new Rng(_w.Seed ^ (c.Cx * 31L + c.Cz * 7919L) ^ 0x1234);
        var list = BiomeSpawns.For(c.Biomes[8 * 16 + 8], SpawnKind.Creature);
        if (list.Length == 0) return;
        while (r.NextFloat() < 0.1f)
        {
            var entry = BiomeSpawns.Pick(list, r);
            int n = entry.Min + r.Next(1 + entry.Max - entry.Min);
            int lx = r.Next(16), lz = r.Next(16);
            for (int i = 0; i < n; i++)
            {
                for (int tries = 0; tries < 4; tries++)
                {
                    int y = c.TopSolidY(lx, lz);
                    if (y > 0 && Block.Get(c.GetBlock(lx, y, lz)).Opaque && !Block.Get(c.GetBlock(lx, y + 1, lz)).Opaque && !Block.Get(c.GetBlock(lx, y + 1, lz)).IsLiquid
                        && Create(entry.Key, _w) is Mob m)
                    {
                        m.SetPositionAndPrev(c.Cx * 16 + lx + 0.5, y + 1, c.Cz * 16 + lz + 0.5);
                        m.Yaw = m.PrevYaw = r.NextFloat() * 360f;
                        _w.SpawnEntity(m);
                        break;
                    }
                    lx = Math.Clamp(lx + r.Next(5) - r.Next(5), 0, 15); lz = Math.Clamp(lz + r.Next(5) - r.Next(5), 0, 15);
                }
            }
        }
    }
}
