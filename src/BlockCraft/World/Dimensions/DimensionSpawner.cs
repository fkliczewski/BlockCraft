using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

/// <summary>Spawn potworów w Netherze i Endzie (SpawnerAnimals z MC 1.8 z listami biomów Hell i Sky oraz fortecy).</summary>
public static class DimensionSpawner
{
    private sealed record Entry(Func<World, Mob> Create, int Weight, bool NeedsDark);

    // BiomeGenHell: ghast 50, zombie pigman 100, magma cube 1 (grupy po 4)
    private static readonly Entry[] Hell =
    {
        new(w => new Ghast(w), 50, false), new(w => new ZombiePigman(w), 100, false), new(w => new MagmaCube(w), 1, false)
    };
    // MapGenNetherBridge.getSpawnList: blaze 10, zombie pigman 5, szkielet 10 (w Netherze 80% witherowy), magma cube 3
    private static readonly Entry[] Fortress =
    {
        new(w => new Blaze(w), 10, false), new(w => new ZombiePigman(w), 5, false),
        new(w => w.Rand.Next(5) > 0 ? new WitherSkeleton(w) : new Skeleton(w), 10, true), new(w => new MagmaCube(w), 3, false)
    };
    // BiomeGenEnd: enderman 10
    private static readonly Entry[] Sky = { new(w => new Enderman(w), 10, true) };

    /// <summary>Hak fortecy: czy punkt leży w obrysie fortecy Netheru (ustawia generator fortecy).</summary>
    public static Func<World, int, int, int, bool> InFortress;

    public static void Tick(World w)
    {
        if (w.Players.Count == 0 || w.Difficulty == Difficulty.Peaceful || w.Dimension == DimensionId.Overworld) return;
        var chunks = new HashSet<long>();
        foreach (var p in w.Players)
        {
            if (p.Spectator) continue;
            int pcx = MathUtil.FloorToInt(p.X) >> 4, pcz = MathUtil.FloorToInt(p.Z) >> 4;
            for (int dx = -7; dx <= 7; dx++) for (int dz = -7; dz <= 7; dz++)
            { var c = w.GetChunk(pcx + dx, pcz + dz); if (c != null && c.Merged) chunks.Add(MathUtil.ChunkKey(pcx + dx, pcz + dz)); }
        }
        if (chunks.Count == 0) return;
        int monsters = w.Entities.Count(e => e is Monster);
        int cap = 70 * chunks.Count / 289;
        if (monsters >= cap) return;
        var r = w.Rand;
        foreach (long k in chunks)
        {
            if (r.Next(4) != 0) continue; // jak w MobSpawner: ograniczenie prób na tick
            int cx = (int)(k >> 32), cz = (int)k;
            var c = w.GetChunk(cx, cz);
            if (c == null) continue;
            int lx = r.Next(16), lz = r.Next(16);
            int top = Math.Min(Chunk.Height, ((c.GetHeight(lx, lz) + 1 + 15) & ~15) + 16);
            int x = cx * 16 + lx, y = r.Next(Math.Max(1, top)), z = cz * 16 + lz;
            if (Block.Get(w.GetBlock(x, y, z)).Opaque) continue;
            for (int group = 0; group < 3; group++)
            {
                int px = x, pz = z;
                Entry entry = null;
                for (int i = 0; i < 4; i++)
                {
                    px += r.Next(6) - r.Next(6); pz += r.Next(6) - r.Next(6);
                    if (!CanSpawnAt(w, px, y, pz)) continue;
                    double nearest = double.MaxValue;
                    foreach (var p in w.Players) nearest = Math.Min(nearest, p.DistanceSq(px + .5, y, pz + .5));
                    if (nearest < 24 * 24) continue;
                    entry ??= Pick(w.Dimension == DimensionId.End ? Sky : InFortress?.Invoke(w, px, y, pz) == true ? Fortress : Hell, r);
                    var m = entry.Create(w);
                    m.SetPositionAndPrev(px + .5, y, pz + .5);
                    m.Yaw = m.PrevYaw = r.NextFloat() * 360;
                    if (!CanMobSpawn(w, m, entry, px, y, pz)) continue;
                    w.SpawnEntity(m);
                    if (++monsters >= cap) return;
                }
            }
        }
    }

    private static Entry Pick(Entry[] list, Rng r)
    {
        int v = r.Next(list.Sum(e => e.Weight));
        foreach (var e in list) { if (v < e.Weight) return e; v -= e.Weight; }
        return list[0];
    }

    private static bool CanSpawnAt(World w, int x, int y, int z)
    {
        if (y < 1 || y >= Chunk.Height - 2) return false;
        var below = Block.Get(w.GetBlock(x, y - 1, z));
        if (!below.Opaque || below.Id == B.Bedrock) return false;
        var at = Block.Get(w.GetBlock(x, y, z)); var above = Block.Get(w.GetBlock(x, y + 1, z));
        return !at.Solid && !at.IsLiquid && !above.Solid && !above.IsLiquid;
    }

    private static bool CanMobSpawn(World w, Mob m, Entry e, int x, int y, int z)
    {
        // EntityGhast.getCanSpawnHere: dodatkowo 1/20
        if (m is Ghast && w.Rand.Next(20) != 0) return false;
        if (e.NeedsDark && (w.GetSkyLight(x, y, z) > w.Rand.Next(32) || w.GetLight(x, y, z) > w.Rand.Next(8))) return false;
        var tmp = new List<AABB>();
        if (w.GetCollisionBoxes(m.BoundingBox, tmp).Count > 0) return false;
        return !w.IsAnyLiquid(m.BoundingBox, false) && !w.IsAnyLiquid(m.BoundingBox, true);
    }
}
