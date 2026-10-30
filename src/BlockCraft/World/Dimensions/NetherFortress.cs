using System.Collections.Concurrent;
using BlockCraft.Core;
using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>java.util.Random (LCG 48-bit), potrzebny tam, gdzie MC 1.8 losuje położenie struktur z ziarna świata.</summary>
public sealed class JavaRandom
{
    private long _seed;
    public JavaRandom(long seed) { SetSeed(seed); }
    public void SetSeed(long seed) => _seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1);
    private int Next(int bits) { _seed = (_seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1); return (int)((ulong)_seed >> (48 - bits)); }
    public int NextInt() => Next(32);
    public int NextInt(int bound)
    {
        if ((bound & -bound) == bound) return (int)((bound * (long)Next(31)) >> 31);
        int bits, val;
        do { bits = Next(31); val = bits % bound; } while (bits - val + (bound - 1) < 0);
        return val;
    }
    public float NextFloat() => Next(24) / (float)(1 << 24);
}

/// <summary>
/// Forteca Netheru (MapGenNetherBridge 1.8): położenie z ziarna jak w canSpawnStructureAtCoords (region 16x16 chunków, 1/3 szans,
/// chunk startu 4..11 w regionie). Układ: skrzyżowania mostów, mosty 5x19 na filarach, pomosty z płomykowym spawnerem,
/// zamknięte korytarze ze skrzyniami i sala z brodawką na piasku dusz. Rysowanie per chunk, bez zależności od sąsiednich chunków.
/// </summary>
public static class NetherFortress
{
    private enum Kind { Crossing, Bridge, Throne, Corridor, StalkRoom }
    private sealed record Piece(Kind Kind, int X0, int Y0, int Z0, int X1, int Y1, int Z1, int Dir, int Seed, bool Chest)
    {
        public bool Intersects(Piece o) => X0 <= o.X1 && X1 >= o.X0 && Z0 <= o.Z1 && Z1 >= o.Z0 && Y0 <= o.Y1 && Y1 >= o.Y0;
        public bool Contains(int x, int y, int z) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1 && z >= Z0 && z <= Z1;
    }
    private sealed record Layout(int StartX, int StartZ, Piece[] Pieces, int X0, int Z0, int X1, int Z1);

    private static readonly ConcurrentDictionary<(long seed, int rx, int rz), Layout> Cache = new();
    private static readonly int[] DX = { 0, -1, 0, 1 }, DZ = { 1, 0, -1, 0 };

    static NetherFortress()
    {
        DimensionSpawner.InFortress = (w, x, y, z) => w.Dimension == DimensionId.Nether && IsInside(w.Seed, x, y, z);
        WorldHooks.Locators["Fortress"] = (w, x, z) =>
        {
            if (w.Dimension != DimensionId.Nether) return null;
            (int x, int y, int z)? best = null; double bd = double.MaxValue;
            int rx = (x >> 4) >> 4, rz = (z >> 4) >> 4;
            for (int i = -3; i <= 3; i++) for (int j = -3; j <= 3; j++)
                if (Get(w.Seed, rx + i, rz + j) is { } l)
                {
                    double d = (double)(l.StartX - x) * (l.StartX - x) + (double)(l.StartZ - z) * (l.StartZ - z);
                    if (d < bd) { bd = d; best = (l.StartX, 64, l.StartZ); }
                }
            return best;
        };
    }

    public static void Init() { }

    /// <summary>MC 1.8 MapGenNetherBridge.canSpawnStructureAtCoords.</summary>
    public static bool IsStartChunk(long seed, int cx, int cz)
    {
        int i = cx >> 4, j = cz >> 4;
        var r = new JavaRandom((long)(i ^ (j << 4)) ^ seed);
        r.NextInt();
        if (r.NextInt(3) != 0) return false;
        if (cx != (i << 4) + 4 + r.NextInt(8)) return false;
        return cz == (j << 4) + 4 + r.NextInt(8);
    }

    private static Layout Get(long seed, int rx, int rz) => Cache.GetOrAdd((seed, rx, rz), k =>
    {
        var r = new JavaRandom((long)(k.rx ^ (k.rz << 4)) ^ k.seed);
        r.NextInt();
        if (r.NextInt(3) != 0) return null;
        int cx = (k.rx << 4) + 4 + r.NextInt(8), cz = (k.rz << 4) + 4 + r.NextInt(8);
        return Build(k.seed, cx, cz);
    });

    public static bool IsInside(long seed, int x, int y, int z)
    {
        int rx = (x >> 4) >> 4, rz = (z >> 4) >> 4;
        for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
        {
            var l = Get(seed, rx + i, rz + j);
            if (l == null || x < l.X0 || x > l.X1 || z < l.Z0 || z > l.Z1) continue;
            foreach (var p in l.Pieces) if (p.Contains(x, y, z)) return true;
        }
        return false;
    }

    private static Layout Build(long seed, int cx, int cz)
    {
        var r = new JavaRandom(seed ^ ((long)cx * 341873128712L + (long)cz * 132897987541L));
        int sx = (cx << 4) + 2, sz = (cz << 4) + 2;
        int baseY = 48 + r.NextInt(12);
        var pieces = new List<Piece>();
        var queue = new Queue<(int ex, int ez, int dir, int depth)>();
        var start = Make(Kind.Crossing, sx, baseY, sz - 9, 0, r);
        pieces.Add(start);
        for (int d = 0; d < 4; d++) queue.Enqueue((sx + DX[d] * 10, sz + DZ[d] * 10, d, 1));

        bool TryAdd(Piece p)
        {
            if (Math.Abs((p.X0 + p.X1) / 2 - sx) > 112 || Math.Abs((p.Z0 + p.Z1) / 2 - sz) > 112) return false;
            foreach (var o in pieces) if (o.Intersects(p)) return false;
            pieces.Add(p); return true;
        }

        while (queue.Count > 0 && pieces.Count < 60)
        {
            var (ex, ez, dir, depth) = queue.Dequeue();
            int n = 1 + r.NextInt(2);
            bool ok = true;
            for (int i = 0; i < n && ok; i++)
            {
                var b = Make(Kind.Bridge, ex, baseY, ez, dir, r);
                if (!(ok = TryAdd(b))) break;
                ex += DX[dir] * 19; ez += DZ[dir] * 19;
            }
            if (!ok) continue;
            float roll = r.NextFloat();
            if (depth < 3 && roll < .4f)
            {
                var c = Make(Kind.Crossing, ex, baseY, ez, dir, r);
                if (TryAdd(c))
                {
                    int ccx = ex + DX[dir] * 9, ccz = ez + DZ[dir] * 9;
                    for (int d = 0; d < 4; d++) if (d != (dir + 2) % 4) queue.Enqueue((ccx + DX[d] * 10, ccz + DZ[d] * 10, d, depth + 1));
                }
            }
            else if (roll < .65f) TryAdd(Make(Kind.Throne, ex, baseY + 3, ez, dir, r));
            else
            {
                int k = 2 + r.NextInt(3);
                for (int i = 0; i < k; i++)
                {
                    var c = Make(Kind.Corridor, ex, baseY + 4, ez, dir, r, i > 0 && r.NextInt(3) == 0);
                    if (!TryAdd(c)) { ok = false; break; }
                    ex += DX[dir] * 5; ez += DZ[dir] * 5;
                }
                if (ok && r.NextInt(2) == 0) TryAdd(Make(Kind.StalkRoom, ex, baseY + 4, ez, dir, r));
            }
        }
        var arr = pieces.ToArray();
        return new Layout(sx, sz, arr, arr.Min(p => p.X0), arr.Min(p => p.Z0), arr.Max(p => p.X1), arr.Max(p => p.Z1));
    }

    /// <summary>Element o szerokości w i długości l wchodzący środkiem krawędzi (ex, ez) w kierunku dir.</summary>
    private static Piece Make(Kind kind, int ex, int y, int ez, int dir, JavaRandom r, bool chest = false)
    {
        var (w, h, l) = kind switch
        {
            Kind.Crossing => (19, 10, 19), Kind.Bridge => (5, 10, 19), Kind.Throne => (7, 8, 9),
            Kind.Corridor => (5, 7, 5), _ => (13, 9, 13)
        };
        int x0, z0, x1, z1;
        switch (dir)
        {
            case 0: x0 = ex - w / 2; x1 = x0 + w - 1; z0 = ez; z1 = ez + l - 1; break;
            case 2: x0 = ex - w / 2; x1 = x0 + w - 1; z1 = ez; z0 = ez - l + 1; break;
            case 3: z0 = ez - w / 2; z1 = z0 + w - 1; x0 = ex; x1 = ex + l - 1; break;
            default: z0 = ez - w / 2; z1 = z0 + w - 1; x1 = ex; x0 = ex - l + 1; break;
        }
        return new Piece(kind, x0, y, z0, x1, y + h - 1, z1, dir, r.NextInt(), chest);
    }

    /// <summary>Wołane przez generator Netheru po wygenerowaniu terenu chunka.</summary>
    public static void Generate(long seed, Chunk c)
    {
        int rx = c.Cx >> 4, rz = c.Cz >> 4;
        int cx0 = c.Cx * 16, cz0 = c.Cz * 16;
        for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++)
        {
            var l = Get(seed, rx + i, rz + j);
            if (l == null || l.X1 < cx0 || l.X0 > cx0 + 15 || l.Z1 < cz0 || l.Z0 > cz0 + 15) continue;
            foreach (var p in l.Pieces)
                if (p.X1 >= cx0 && p.X0 <= cx0 + 15 && p.Z1 >= cz0 && p.Z0 <= cz0 + 15) Draw(p, c);
        }
    }

    private readonly struct Pen
    {
        private readonly Piece _p; private readonly Chunk _c;
        public Pen(Piece p, Chunk c) { _p = p; _c = c; }
        private (int x, int z) World(int lx, int lz) => _p.Dir switch
        {
            0 => (_p.X0 + lx, _p.Z0 + lz),
            2 => (_p.X0 + lx, _p.Z1 - lz),
            3 => (_p.X0 + lz, _p.Z0 + lx),
            _ => (_p.X1 - lz, _p.Z0 + lx)
        };
        private bool Local(int lx, int ly, int lz, out int x, out int y, out int z)
        {
            (x, z) = World(lx, lz); y = _p.Y0 + ly;
            x -= _c.Cx * 16; z -= _c.Cz * 16;
            return x is >= 0 and < 16 && z is >= 0 and < 16 && (uint)y < Chunk.Height;
        }
        public void Set(int lx, int ly, int lz, byte id, int meta = 0) { if (Local(lx, ly, lz, out int x, out int y, out int z)) _c.SetBlockRaw(x, y, z, id, (byte)meta); }
        public void Fill(int ax, int ay, int az, int bx, int by, int bz, byte id, int meta = 0)
        { for (int x = ax; x <= bx; x++) for (int y = ay; y <= by; y++) for (int z = az; z <= bz; z++) Set(x, y, z, id, meta); }
        /// <summary>replaceAirAndLiquidDownwards: filar z cegieł od ly w dół do pierwszego bloku stałego.</summary>
        public void Pillar(int lx, int ly, int lz)
        {
            if (!Local(lx, ly, lz, out int x, out int y, out int z)) return;
            for (; y > 1; y--)
            {
                byte b = _c.GetBlock(x, y, z);
                if (b != B.Air && b != B.Lava && b != B.Fire) break;
                _c.SetBlockRaw(x, y, z, B.NetherBrick, 0);
            }
        }
        public void Chest(int lx, int ly, int lz, int seed)
        {
            if (!Local(lx, ly, lz, out int x, out int y, out int z)) return;
            _c.SetBlockRaw(x, y, z, B.Chest, 0);
            var te = new ChestTileEntity { X = _c.Cx * 16 + x, Y = y, Z = _c.Cz * 16 + z };
            FillLoot(te, new Rng(seed));
            _c.TileEntities[Chunk.Index(x, y, z)] = te;
        }
        public void Spawner(int lx, int ly, int lz, string mob)
        {
            if (!Local(lx, ly, lz, out int x, out int y, out int z) || Block.ById[B.MobSpawner] is not { } sb || sb.Id != B.MobSpawner) return;
            _c.SetBlockRaw(x, y, z, B.MobSpawner, 0);
            // typ tile entity spawnera należy do modułu MO (TypeId 50-59); szukamy go bez zależności kompilacyjnej
            foreach (var (id, f) in TileEntity.Types)
            {
                if (id < 50 || id > 59) continue;
                var te = f();
                if (!te.GetType().Name.Contains("Spawner")) continue;
                te.X = _c.Cx * 16 + x; te.Y = y; te.Z = _c.Cz * 16 + z;
                te.SetData("EntityId", mob);
                _c.TileEntities[Chunk.Index(x, y, z)] = te;
                break;
            }
        }
    }

    private static void Draw(Piece p, Chunk c)
    {
        var g = new Pen(p, c);
        const byte brick = B.NetherBrick, fence = B.NetherBrickFence;
        switch (p.Kind)
        {
            case Kind.Bridge:
                g.Fill(0, 3, 0, 4, 4, 18, brick);
                g.Fill(1, 5, 0, 3, 9, 18, B.Air);
                g.Fill(0, 5, 0, 0, 5, 18, brick); g.Fill(4, 5, 0, 4, 5, 18, brick);
                g.Fill(0, 6, 0, 0, 9, 18, B.Air); g.Fill(4, 6, 0, 4, 9, 18, B.Air);
                for (int z = 1; z <= 17; z += 2) { g.Set(0, 6, z, fence); g.Set(4, 6, z, fence); }
                g.Fill(0, 2, 0, 4, 2, 5, brick); g.Fill(0, 2, 13, 4, 2, 18, brick);
                g.Fill(0, 0, 0, 4, 1, 3, brick); g.Fill(0, 0, 15, 4, 1, 18, brick);
                for (int x = 0; x <= 4; x++) for (int z = 0; z <= 2; z++) { g.Pillar(x, -1, z); g.Pillar(x, -1, 18 - z); }
                break;
            case Kind.Crossing:
                g.Fill(7, 3, 0, 11, 4, 18, brick); g.Fill(0, 3, 7, 18, 4, 11, brick);
                g.Fill(8, 5, 0, 10, 9, 18, B.Air); g.Fill(0, 5, 8, 18, 9, 10, B.Air);
                g.Fill(7, 5, 0, 7, 5, 7, brick); g.Fill(7, 5, 11, 7, 5, 18, brick);
                g.Fill(11, 5, 0, 11, 5, 7, brick); g.Fill(11, 5, 11, 11, 5, 18, brick);
                g.Fill(0, 5, 7, 7, 5, 7, brick); g.Fill(11, 5, 7, 18, 5, 7, brick);
                g.Fill(0, 5, 11, 7, 5, 11, brick); g.Fill(11, 5, 11, 18, 5, 11, brick);
                for (int i = 1; i <= 17; i += 2) if (i < 7 || i > 11)
                { g.Set(7, 6, i, fence); g.Set(11, 6, i, fence); g.Set(i, 6, 7, fence); g.Set(i, 6, 11, fence); }
                g.Fill(7, 2, 7, 11, 2, 11, brick);
                for (int x = 7; x <= 11; x++) for (int z = 7; z <= 11; z++) g.Pillar(x, 1, z);
                for (int a = 7; a <= 11; a++) for (int b = 0; b <= 2; b++)
                { g.Pillar(a, 2, b); g.Pillar(a, 2, 18 - b); g.Pillar(b, 2, a); g.Pillar(18 - b, 2, a); }
                break;
            case Kind.Throne:
                // pomost z spawnerem płomyków (MC: StructureNetherBridgePieces.Throne)
                g.Fill(0, 2, 0, 6, 7, 8, B.Air);
                g.Fill(1, 0, 0, 5, 1, 8, brick);
                g.Fill(1, 2, 2, 5, 2, 8, brick); g.Fill(1, 3, 4, 5, 3, 8, brick);
                for (int z = 0; z <= 8; z++)
                {
                    int top = z >= 4 ? 4 : z >= 2 ? 3 : 2;
                    g.Set(1, top, z, fence); g.Set(5, top, z, fence);
                }
                g.Fill(2, 4, 8, 4, 4, 8, fence);
                g.Spawner(3, 4, 6, "blaze");
                for (int x = 1; x <= 5; x++) for (int z = 0; z <= 8; z++) g.Pillar(x, -1, z);
                break;
            case Kind.Corridor:
                g.Fill(0, 0, 0, 4, 0, 4, brick);
                g.Fill(1, 1, 0, 3, 4, 4, B.Air);
                g.Fill(0, 1, 0, 0, 4, 4, brick); g.Fill(4, 1, 0, 4, 4, 4, brick);
                g.Fill(0, 5, 0, 4, 5, 4, brick);
                g.Set(0, 3, 1, fence); g.Set(0, 3, 3, fence); g.Set(4, 3, 1, fence); g.Set(4, 3, 3, fence);
                if (p.Chest) g.Chest(3, 1, 2, p.Seed);
                for (int x = 0; x <= 4; x++) for (int z = 0; z <= 4; z++) g.Pillar(x, -1, z);
                break;
            case Kind.StalkRoom:
                // sala z brodawką netherową (MC: CastleStalkRoom, uproszczona)
                g.Fill(0, 0, 0, 12, 0, 12, brick);
                g.Fill(1, 1, 1, 11, 7, 11, B.Air);
                g.Fill(0, 1, 0, 12, 7, 0, brick); g.Fill(0, 1, 12, 12, 7, 12, brick);
                g.Fill(0, 1, 0, 0, 7, 12, brick); g.Fill(12, 1, 0, 12, 7, 12, brick);
                g.Fill(0, 8, 0, 12, 8, 12, brick);
                g.Fill(5, 1, 0, 7, 3, 0, B.Air);
                for (int i = 2; i <= 10; i += 2) { g.Set(0, 4, i, fence); g.Set(0, 5, i, fence); g.Set(12, 4, i, fence); g.Set(12, 5, i, fence); }
                g.Fill(2, 1, 3, 4, 1, 10, B.SoulSand); g.Fill(8, 1, 3, 10, 1, 10, B.SoulSand);
                g.Fill(2, 2, 3, 4, 2, 10, B.NetherWart); g.Fill(8, 2, 3, 10, 2, 10, B.NetherWart);
                g.Fill(5, 1, 6, 7, 1, 6, B.NetherBrickStairs);
                for (int x = 0; x <= 12; x++) for (int z = 0; z <= 12; z++) g.Pillar(x, -1, z);
                break;
        }
    }

    private static readonly (int id, int min, int max, int weight)[] Loot =
    {
        (I.Diamond, 1, 3, 5), (I.IronIngot, 1, 5, 5), (I.GoldIngot, 1, 3, 15), (I.GoldSword, 1, 1, 5), (I.GoldChest, 1, 1, 5),
        (I.FlintAndSteel, 1, 1, 5), (I.NetherWartItem, 3, 7, 5), (I.Saddle, 1, 1, 10), (I.GoldHorseArmor, 1, 1, 8),
        (I.IronHorseArmor, 1, 1, 5), (I.DiamondHorseArmor, 1, 1, 3), (B.Obsidian, 2, 4, 2)
    };

    /// <summary>Łup skrzyni fortecy (netherBridge z 1.8): 2-5 losowań z wagami.</summary>
    private static void FillLoot(ChestTileEntity te, Rng r)
    {
        var table = Loot.Where(e => e.id < 256 || Item.ById[e.id] != null).ToArray();
        int total = table.Sum(e => e.weight), n = 2 + r.Next(4);
        for (int i = 0; i < n; i++)
        {
            int v = r.Next(total);
            foreach (var e in table)
            {
                if ((v -= e.weight) >= 0) continue;
                te.Slots[r.Next(27)] = new ItemStack(e.id, e.min + r.Next(e.max - e.min + 1));
                break;
            }
        }
    }
}
