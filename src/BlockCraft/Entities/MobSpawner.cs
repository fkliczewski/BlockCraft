using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

public sealed class MobSpawner
{
    private readonly World.World _w;
    public MobSpawner(World.World w) { _w = w; }

    private static readonly (Func<World.World, Mob> f, int weight)[] Monsters =
    {
        (w => new Zombie(w), 100), (w => new Skeleton(w), 100), (w => new Creeper(w), 100), (w => new Spider(w), 100), (w => new Enderman(w), 10)
    };
    private static readonly (Func<World.World, Mob> f, int weight)[] Animals =
    {
        (w => new Sheep(w), 12), (w => new Pig(w), 10), (w => new Chicken(w), 10), (w => new Cow(w), 8)
    };

    public static Mob Create(string key, World.World w) => key.ToLowerInvariant() switch
    {
        "zombie" => new Zombie(w), "skeleton" => new Skeleton(w), "creeper" => new Creeper(w), "spider" => new Spider(w),
        "enderman" => new Enderman(w), "cow" => new Cow(w), "pig" => new Pig(w), "sheep" => new Sheep(w), "chicken" => new Chicken(w),
        _ => null
    };
    public static readonly string[] MobKeys = { "zombie", "skeleton", "creeper", "spider", "enderman", "cow", "pig", "sheep", "chicken" };

    private static Func<World.World, Mob> Pick((Func<World.World, Mob> f, int weight)[] list, Rng r)
    {
        int total = list.Sum(x => x.weight);
        int v = r.Next(total);
        foreach (var (f, w) in list) { if (v < w) return f; v -= w; }
        return list[0].f;
    }

    public void Tick()
    {
        if (_w.Players.Count == 0) return;
        // kwalifikujące się chunki
        var chunks = new HashSet<long>();
        foreach (var p in _w.Players)
        {
            int pcx = MathUtil.FloorToInt(p.X) >> 4, pcz = MathUtil.FloorToInt(p.Z) >> 4;
            for (int dx = -7; dx <= 7; dx++) for (int dz = -7; dz <= 7; dz++)
                {
                    var c = _w.GetChunk(pcx + dx, pcz + dz);
                    if (c != null && c.Merged) chunks.Add(MathUtil.ChunkKey(pcx + dx, pcz + dz));
                }
        }
        if (chunks.Count == 0) return;
        int monsters = 0, animals = 0;
        foreach (var e in _w.Entities) { if (e is Monster) monsters++; else if (e is Animal) animals++; }
        if (_w.Difficulty != Difficulty.Peaceful)
        {
            int cap = 70 * chunks.Count / 289;
            if (monsters < cap) SpawnPass(chunks, Monsters, true, cap - monsters);
        }
        if (_w.TotalTime % 400 == 0)
        {
            int cap = 10 * chunks.Count / 289;
            if (animals < cap) SpawnPass(chunks, Animals, false, cap - animals);
        }
    }

    private void SpawnPass(HashSet<long> chunks, (Func<World.World, Mob>, int)[] table, bool hostile, int budget)
    {
        var r = _w.Rand;
        int spawned = 0;
        foreach (long k in chunks)
        {
            if (spawned >= budget) break;
            if (hostile && r.Next(4) != 0) continue; // ograniczenie prób (wydajność)
            int cx = (int)(k >> 32), cz = (int)k;
            var c = _w.GetChunk(cx, cz);
            if (c == null) continue;
            int lx = r.Next(16), lz = r.Next(16);
            int top = c.GetHeight(lx, lz) + 1;
            int y = r.Next(Math.Max(1, top));
            int x = cx * 16 + lx, z = cz * 16 + lz;
            if (Block.Get(_w.GetBlock(x, y, z)).Solid) continue;
            var factory = Pick(table, r);
            int group = 0;
            int px = x, pz = z;
            for (int i = 0; i < 4 && group < 4; i++)
            {
                px += r.Next(6) - r.Next(6); pz += r.Next(6) - r.Next(6);
                if (!CanSpawnAt(px, y, pz, hostile)) continue;
                double nearest = double.MaxValue;
                foreach (var p in _w.Players) nearest = Math.Min(nearest, p.DistanceSq(px + 0.5, y, pz + 0.5));
                if (nearest < 24 * 24) continue;
                double sd = _w.SpawnX - px, sz = _w.SpawnZ - pz;
                if (sd * sd + sz * sz < 24 * 24 && !hostile) continue;
                var m = factory(_w);
                m.SetPositionAndPrev(px + 0.5, y, pz + 0.5);
                m.Yaw = m.PrevYaw = r.NextFloat() * 360f;
                if (!FitsAt(m)) continue;
                if (hostile && !LightOk(px, y, pz)) continue;
                if (!hostile && (_w.GetBlock(px, y - 1, pz) != B.Grass || _w.GetRawLight(px, y, pz) < 9)) continue;
                _w.SpawnEntity(m);
                group++; spawned++;
            }
        }
    }

    private bool CanSpawnAt(int x, int y, int z, bool hostile)
    {
        if (y < 1 || y >= Chunk.Height - 2) return false;
        var below = Block.Get(_w.GetBlock(x, y - 1, z));
        if (!below.Opaque || below.Id == B.Bedrock || below.Id == B.Glass) return false;
        var at = Block.Get(_w.GetBlock(x, y, z)); var above = Block.Get(_w.GetBlock(x, y + 1, z));
        if (at.Solid || at.IsLiquid || above.Solid || above.IsLiquid) return false;
        return true;
    }

    private bool FitsAt(Mob m)
    {
        var bb = m.BoundingBox;
        var tmp = new List<AABB>();
        if (_w.GetCollisionBoxes(bb, tmp).Count > 0) return false;
        if (_w.IsAnyLiquid(bb, false) || _w.IsAnyLiquid(bb, true)) return false;
        return true;
    }

    private bool LightOk(int x, int y, int z)
    {
        var r = _w.Rand;
        if (_w.GetSkyLight(x, y, z) > r.Next(32)) return false;
        int l = _w.GetLight(x, y, z);
        return l <= r.Next(8);
    }

    /// <summary>Zwierzęta przy generowaniu chunka.</summary>
    public void PopulateChunk(Chunk c)
    {
        if (_w.IsRemote || c.FromDisk) return;
        var r = new Rng(_w.Seed ^ (c.Cx * 31L + c.Cz * 7919L) ^ 0x1234);
        if (r.NextFloat() >= 0.1f) return;
        byte bio = c.Biomes[8 * 16 + 8];
        if (bio == Biome.Desert) return;
        var factory = Pick(Animals, r);
        int n = 2 + r.Next(3);
        int bx = r.Next(10) + 3, bz = r.Next(10) + 3;
        for (int i = 0; i < n; i++)
        {
            int lx = Math.Clamp(bx + r.Next(5) - 2, 0, 15), lz = Math.Clamp(bz + r.Next(5) - 2, 0, 15);
            int y = c.TopSolidY(lx, lz);
            if (y <= 0) continue;
            if (c.GetBlock(lx, y, lz) != B.Grass) continue;
            var m = factory(_w);
            m.SetPositionAndPrev(c.Cx * 16 + lx + 0.5, y + 1, c.Cz * 16 + lz + 0.5);
            m.Yaw = m.PrevYaw = r.NextFloat() * 360f;
            _w.SpawnEntity(m);
        }
    }
}
