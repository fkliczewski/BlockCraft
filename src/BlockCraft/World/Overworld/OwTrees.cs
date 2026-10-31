using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>Dostęp do bloków dla generatorów (świat albo chunk w trakcie generacji).</summary>
public interface IGenAccess
{
    int Get(int x, int y, int z);
    int GetMeta(int x, int y, int z);
    void Set(int x, int y, int z, int id, int meta);
}

public sealed class WorldGenAccess : IGenAccess
{
    private readonly World _w;
    public WorldGenAccess(World w) { _w = w; }
    public int Get(int x, int y, int z) => _w.GetBlock(x, y, z);
    public int GetMeta(int x, int y, int z) => _w.GetMeta(x, y, z);
    public void Set(int x, int y, int z, int id, int meta) => _w.SetBlock(x, y, z, (byte)id, meta, false);
}

/// <summary>Generatory drzew i wielkich grzybów jak w MC 1.8 (WorldGenTrees, WorldGenBigTree, WorldGenForest, WorldGenTaiga1/2,
/// WorldGenMegaPineTree, WorldGenMegaJungle, WorldGenSavannaTree, WorldGenCanopyTree, WorldGenSwamp, WorldGenShrub, WorldGenBigMushroom).</summary>
public static class OwTrees
{
    public const int Oak = 0, Spruce = 1, Birch = 2, Jungle = 3, Acacia = 4, DarkOak = 5;
    private static readonly byte[] Logs = { B.OakLog, B.SpruceLog, B.BirchLog, B.JungleLog, B.AcaciaLog, B.DarkOakLog };
    private static readonly byte[] Leaves = { B.OakLeaves, B.SpruceLeaves, B.BirchLeaves, B.JungleLeaves, B.AcaciaLeaves, B.DarkOakLeaves };
    private static readonly byte[] Saplings = { B.OakSapling, B.SpruceSapling, B.BirchSapling, B.JungleSapling, B.AcaciaSapling, B.DarkOakSapling };

    // ---------------- materiały ----------------
    public static bool IsAir(int id) => id == B.Air;
    public static bool IsLeaves(int id) => Block.ById[id] is LeavesBlock;
    public static bool AirOrLeaves(int id) => id == B.Air || IsLeaves(id);
    /// <summary>WorldGenAbstractTree.func_150523_a: powietrze, liście, trawa, ziemia, kłody, sadzonki, pnącza.</summary>
    public static bool Replaceable(int id) => id == B.Air || IsLeaves(id) || id == B.Grass || id == B.Dirt || Block.ById[id] is LogBlock || Block.ById[id] is SaplingBlock || id == B.Vine;
    private static bool FullBlock(int id) => Block.Get(id).Opaque;
    private static bool Ground(int id) => id == B.Grass || id == B.Dirt || id == B.Farmland;

    // ---------------- sadzonki (BlockSapling.generateTree) ----------------
    public static bool GrowSapling(World w, int x, int y, int z, int type, JRandom r)
    {
        var g = new WorldGenAccess(w);
        byte sap = Saplings[type];
        bool Is(int px, int pz) => w.GetBlock(px, y, pz) == sap;
        bool Is2x2(int px, int pz) => Is(px, pz) && Is(px + 1, pz) && Is(px, pz + 1) && Is(px + 1, pz + 1);
        int ox = 0, oz = 0; bool big = false;
        if (type is Spruce or Jungle or DarkOak)
            for (int i = 0; i >= -1 && !big; i--) for (int j = 0; j >= -1 && !big; j--) if (Is2x2(x + i, z + j)) { ox = i; oz = j; big = true; }
        if (type == DarkOak && !big) return false;
        Func<bool> gen;
        int gx = x + ox, gz = z + oz;
        switch (type)
        {
            case Spruce: gen = big ? () => MegaPine(g, r, gx, y, gz, r.NextBool(), false) : () => Taiga2(g, r, gx, y, gz); break;
            case Birch: gen = () => BirchTree(g, r, gx, y, gz, false); break;
            case Jungle: gen = big ? () => MegaJungle(g, r, gx, y, gz) : () => SmallTree(g, r, gx, y, gz, 4 + r.NextInt(7), Jungle, Jungle, false); break;
            case Acacia: gen = () => Savanna(g, r, gx, y, gz); break;
            case DarkOak: gen = () => Canopy(g, r, gx, y, gz); break;
            default: gen = r.NextInt(10) == 0 ? () => BigTree(g, r, gx, y, gz) : () => SmallTree(g, r, gx, y, gz, 4, Oak, Oak, false); break;
        }
        var saved = new List<(int, int, int)>();
        if (big) { for (int i = 0; i <= 1; i++) for (int j = 0; j <= 1; j++) { saved.Add((gx + i, gz + j, w.GetMeta(gx + i, y, gz + j))); w.SetBlock(gx + i, y, gz + j, B.Air, 0, false); } }
        else { saved.Add((x, z, w.GetMeta(x, y, z))); w.SetBlock(x, y, z, B.Air, 0, false); }
        if (gen()) return true;
        foreach (var (sx, sz, sm) in saved) w.SetBlock(sx, y, sz, sap, sm, false);
        return false;
    }

    private static void Vine(IGenAccess g, int x, int y, int z, int bit) => g.Set(x, y, z, B.Vine, bit);
    private static void VineDown(IGenAccess g, int x, int y, int z, int bit)
    {
        Vine(g, x, y, z, bit);
        for (int i = 4; i > 0 && IsAir(g.Get(x, --y, z)); i--) Vine(g, x, y, z, bit);
    }

    // ---------------- WorldGenTrees ----------------
    public static bool SmallTree(IGenAccess g, JRandom r, int x, int y, int z, int minHeight, int log, int leaves, bool vines)
    {
        int h = r.NextInt(3) + minHeight;
        if (y < 1 || y + h + 1 > 256) return false;
        for (int j = y; j <= y + 1 + h; j++)
        {
            int k = j == y ? 0 : j >= y + 1 + h - 2 ? 2 : 1;
            for (int l = x - k; l <= x + k; l++) for (int m = z - k; m <= z + k; m++)
                    if (j < 0 || j >= 256 || !Replaceable(g.Get(l, j, m))) return false;
        }
        if (!Ground(g.Get(x, y - 1, z)) || y >= 256 - h - 1) return false;
        g.Set(x, y - 1, z, B.Dirt, 0);
        byte lg = Logs[log], lv = Leaves[leaves];
        for (int i3 = y - 3 + h; i3 <= y + h; i3++)
        {
            int i4 = i3 - (y + h), j1 = 1 - i4 / 2;
            for (int k1 = x - j1; k1 <= x + j1; k1++)
                for (int i2 = z - j1; i2 <= z + j1; i2++)
                    if (Math.Abs(k1 - x) != j1 || Math.Abs(i2 - z) != j1 || r.NextInt(2) != 0 && i4 != 0)
                    {
                        int id = g.Get(k1, i3, i2);
                        if (AirOrLeaves(id) || id == B.Vine) g.Set(k1, i3, i2, lv, 0);
                    }
        }
        for (int j3 = 0; j3 < h; j3++)
        {
            int id = g.Get(x, y + j3, z);
            if (!(AirOrLeaves(id) || id == B.Vine)) continue;
            g.Set(x, y + j3, z, lg, 0);
            if (vines && j3 > 0)
            {
                if (r.NextInt(3) > 0 && IsAir(g.Get(x - 1, y + j3, z))) Vine(g, x - 1, y + j3, z, 8);
                if (r.NextInt(3) > 0 && IsAir(g.Get(x + 1, y + j3, z))) Vine(g, x + 1, y + j3, z, 2);
                if (r.NextInt(3) > 0 && IsAir(g.Get(x, y + j3, z - 1))) Vine(g, x, y + j3, z - 1, 1);
                if (r.NextInt(3) > 0 && IsAir(g.Get(x, y + j3, z + 1))) Vine(g, x, y + j3, z + 1, 4);
            }
        }
        if (vines)
        {
            for (int k3 = y - 3 + h; k3 <= y + h; k3++)
            {
                int j4 = k3 - (y + h), k4 = 2 - j4 / 2;
                for (int l4 = x - k4; l4 <= x + k4; l4++) for (int i5 = z - k4; i5 <= z + k4; i5++)
                    {
                        if (!IsLeaves(g.Get(l4, k3, i5))) continue;
                        if (r.NextInt(4) == 0 && IsAir(g.Get(l4 - 1, k3, i5))) VineDown(g, l4 - 1, k3, i5, 8);
                        if (r.NextInt(4) == 0 && IsAir(g.Get(l4 + 1, k3, i5))) VineDown(g, l4 + 1, k3, i5, 2);
                        if (r.NextInt(4) == 0 && IsAir(g.Get(l4, k3, i5 - 1))) VineDown(g, l4, k3, i5 - 1, 1);
                        if (r.NextInt(4) == 0 && IsAir(g.Get(l4, k3, i5 + 1))) VineDown(g, l4, k3, i5 + 1, 4);
                    }
            }
            if (r.NextInt(5) == 0 && h > 5)
                for (int l3 = 0; l3 < 2; l3++)
                    for (int f = 0; f < 4; f++) // HORIZONTALS: S, W, N, E
                        if (r.NextInt(4 - l3) == 0)
                        {
                            int age = r.NextInt(3);
                            int px = x - Dir.HorizX[f], pz = z - Dir.HorizZ[f];
                            g.Set(px, y + h - 5 + l3, pz, B.Cocoa, f | (age << 2));
                        }
        }
        return true;
    }

    // ---------------- WorldGenForest (brzoza) ----------------
    public static bool BirchTree(IGenAccess g, JRandom r, int x, int y, int z, bool tall)
    {
        int h = r.NextInt(3) + 5;
        if (tall) h += r.NextInt(7);
        if (y < 1 || y + h + 1 > 256) return false;
        for (int j = y; j <= y + 1 + h; j++)
        {
            int k = j == y ? 0 : j >= y + 1 + h - 2 ? 2 : 1;
            for (int l = x - k; l <= x + k; l++) for (int m = z - k; m <= z + k; m++)
                    if (j < 0 || j >= 256 || !Replaceable(g.Get(l, j, m))) return false;
        }
        if (!Ground(g.Get(x, y - 1, z)) || y >= 256 - h - 1) return false;
        g.Set(x, y - 1, z, B.Dirt, 0);
        for (int i2 = y - 3 + h; i2 <= y + h; i2++)
        {
            int k2 = i2 - (y + h), l2 = 1 - k2 / 2;
            for (int i3 = x - l2; i3 <= x + l2; i3++) for (int k3 = z - l2; k3 <= z + l2; k3++)
                    if (Math.Abs(i3 - x) != l2 || Math.Abs(k3 - z) != l2 || r.NextInt(2) != 0 && k2 != 0)
                        if (AirOrLeaves(g.Get(i3, i2, k3))) g.Set(i3, i2, k3, B.BirchLeaves, 0);
        }
        for (int j2 = 0; j2 < h; j2++) if (AirOrLeaves(g.Get(x, y + j2, z))) g.Set(x, y + j2, z, B.BirchLog, 0);
        return true;
    }

    // ---------------- WorldGenTaiga2 (świerk) ----------------
    public static bool Taiga2(IGenAccess g, JRandom r, int x, int y, int z)
    {
        int h = r.NextInt(4) + 6, j = 1 + r.NextInt(2), k = h - j, l = 2 + r.NextInt(2);
        if (y < 1 || y + h + 1 > 256) return false;
        for (int i1 = y; i1 <= y + 1 + h; i1++)
        {
            int j1 = i1 - y < j ? 0 : l;
            for (int a = x - j1; a <= x + j1; a++) for (int b = z - j1; b <= z + j1; b++)
                    if (i1 < 0 || i1 >= 256 || !AirOrLeaves(g.Get(a, i1, b))) return false;
        }
        if (!Ground(g.Get(x, y - 1, z)) || y >= 256 - h - 1) return false;
        g.Set(x, y - 1, z, B.Dirt, 0);
        int i3 = r.NextInt(2), j3 = 1, k3 = 0;
        for (int l3 = 0; l3 <= k; l3++)
        {
            int j4 = y + h - l3;
            for (int i2 = x - i3; i2 <= x + i3; i2++) for (int k2 = z - i3; k2 <= z + i3; k2++)
                    if ((Math.Abs(i2 - x) != i3 || Math.Abs(k2 - z) != i3 || i3 <= 0) && !FullBlock(g.Get(i2, j4, k2)))
                        g.Set(i2, j4, k2, B.SpruceLeaves, 0);
            if (i3 >= j3) { i3 = k3; k3 = 1; j3++; if (j3 > l) j3 = l; }
            else i3++;
        }
        int i4 = r.NextInt(3);
        for (int k4 = 0; k4 < h - i4; k4++) if (AirOrLeaves(g.Get(x, y + k4, z))) g.Set(x, y + k4, z, B.SpruceLog, 0);
        return true;
    }

    // ---------------- WorldGenTaiga1 (sosna) ----------------
    public static bool Taiga1(IGenAccess g, JRandom r, int x, int y, int z)
    {
        int h = r.NextInt(5) + 7, j = h - r.NextInt(2) - 3, k = h - j, l = 1 + r.NextInt(k + 1);
        if (y < 1 || y + h + 1 > 256) return false;
        for (int i1 = y; i1 <= y + 1 + h; i1++)
        {
            int j1 = i1 - y < j ? 0 : l;
            for (int a = x - j1; a <= x + j1; a++) for (int b = z - j1; b <= z + j1; b++)
                    if (i1 < 0 || i1 >= 256 || !AirOrLeaves(g.Get(a, i1, b))) return false;
        }
        if (!Ground(g.Get(x, y - 1, z)) || y >= 256 - h - 1) return false;
        g.Set(x, y - 1, z, B.Dirt, 0);
        int k2 = 0;
        for (int l2 = y + h; l2 >= y + j; l2--)
        {
            for (int a = x - k2; a <= x + k2; a++) for (int b = z - k2; b <= z + k2; b++)
                    if ((Math.Abs(a - x) != k2 || Math.Abs(b - z) != k2 || k2 <= 0) && !FullBlock(g.Get(a, l2, b))) g.Set(a, l2, b, B.SpruceLeaves, 0);
            if (k2 >= 1 && l2 == y + j + 1) k2--; else if (k2 < l) k2++;
        }
        for (int i3 = 0; i3 < h - 1; i3++) if (AirOrLeaves(g.Get(x, y + i3, z))) g.Set(x, y + i3, z, B.SpruceLog, 0);
        return true;
    }

    // ---------------- WorldGenHugeTrees ----------------
    private static int HugeHeight(JRandom r, int baseH, int extra) { int i = r.NextInt(3) + baseH; if (extra > 1) i += r.NextInt(extra); return i; }
    private static bool HugeSpace(IGenAccess g, int x, int y, int z, int h)
    {
        if (y < 1 || y + h + 1 > 256) return false;
        for (int i = 0; i <= 1 + h; i++)
        {
            int j = i == 0 ? 1 : 2;
            for (int k = -j; k <= j; k++) for (int l = -j; l <= j; l++)
                    if (y + i < 0 || y + i >= 256 || !Replaceable(g.Get(x + k, y + i, z + l))) return false;
        }
        return true;
    }
    private static bool HugeGround(IGenAccess g, int x, int y, int z)
    {
        int b = g.Get(x, y - 1, z);
        if (!(b == B.Grass || b == B.Dirt) || y < 2) return false;
        g.Set(x, y - 1, z, B.Dirt, 0); g.Set(x + 1, y - 1, z, B.Dirt, 0); g.Set(x, y - 1, z + 1, B.Dirt, 0); g.Set(x + 1, y - 1, z + 1, B.Dirt, 0);
        return true;
    }
    /// <summary>func_175925_a: warstwa liści dla pnia 2x2.</summary>
    private static void LeafLayer2(IGenAccess g, int x, int y, int z, int r, byte leaves)
    {
        int rr = r * r;
        for (int j = -r; j <= r + 1; j++) for (int k = -r; k <= r + 1; k++)
            {
                int l = j - 1, m = k - 1;
                if (j * j + k * k <= rr || l * l + m * m <= rr || j * j + m * m <= rr || l * l + k * k <= rr)
                    if (AirOrLeaves(g.Get(x + j, y, z + k))) g.Set(x + j, y, z + k, leaves, 0);
            }
    }
    private static void LeafLayer(IGenAccess g, int x, int y, int z, int r, byte leaves)
    {
        int rr = r * r;
        for (int j = -r; j <= r; j++) for (int k = -r; k <= r; k++)
                if (j * j + k * k <= rr && AirOrLeaves(g.Get(x + j, y, z + k))) g.Set(x + j, y, z + k, leaves, 0);
    }

    /// <summary>WorldGenMegaPineTree: useBaseHeight = true daje wyższą koronę (świerk), false sosnę.</summary>
    public static bool MegaPine(IGenAccess g, JRandom r, int x, int y, int z, bool useBaseHeight, bool podzol)
    {
        int h = HugeHeight(r, 13, 15);
        if (!HugeSpace(g, x, y, z, h) || !HugeGround(g, x, y, z)) return false;
        int top = y + h;
        int crown = r.NextInt(5) + (useBaseHeight ? 13 : 3), prev = 0;
        for (int k = top - crown; k <= top; k++)
        {
            int l = top - k;
            int i1 = (int)Math.Floor(l / (float)crown * 3.5f);
            LeafLayer2(g, x, k, z, i1 + (l > 0 && i1 == prev && (k & 1) == 0 ? 1 : 0), B.SpruceLeaves);
            prev = i1;
        }
        for (int j = 0; j < h; j++)
        {
            if (AirOrLeaves(g.Get(x, y + j, z))) g.Set(x, y + j, z, B.SpruceLog, 0);
            if (j < h - 1)
            {
                if (AirOrLeaves(g.Get(x + 1, y + j, z))) g.Set(x + 1, y + j, z, B.SpruceLog, 0);
                if (AirOrLeaves(g.Get(x + 1, y + j, z + 1))) g.Set(x + 1, y + j, z + 1, B.SpruceLog, 0);
                if (AirOrLeaves(g.Get(x, y + j, z + 1))) g.Set(x, y + j, z + 1, B.SpruceLog, 0);
            }
        }
        if (podzol)
        {
            void Spot(int px, int pz) { for (int i = -2; i <= 2; i++) for (int j = -2; j <= 2; j++) if (Math.Abs(i) != 2 || Math.Abs(j) != 2) Podzol(g, px + i, y, pz + j); }
            Spot(x - 1, z - 1); Spot(x + 2, z - 1); Spot(x - 1, z + 2); Spot(x + 2, z + 2);
            for (int i = 0; i < 5; i++)
            {
                int j = r.NextInt(64), k = j % 8, l = j / 8;
                if (k == 0 || k == 7 || l == 0 || l == 7) Spot(x - 3 + k, z - 3 + l);
            }
        }
        return true;
    }
    private static void Podzol(IGenAccess g, int x, int y, int z)
    {
        for (int i = 2; i >= -3; i--)
        {
            int b = g.Get(x, y + i, z);
            if (b == B.Grass || b == B.Dirt) { g.Set(x, y + i, z, B.Dirt, 2); break; }
            if (b != B.Air && i < 0) break;
        }
    }

    /// <summary>WorldGenMegaJungle (10 + do 20 bloków).</summary>
    public static bool MegaJungle(IGenAccess g, JRandom r, int x, int y, int z)
    {
        int h = HugeHeight(r, 10, 20);
        if (!HugeSpace(g, x, y, z, h) || !HugeGround(g, x, y, z)) return false;
        for (int j = -2; j <= 0; j++) LeafLayer2(g, x, y + h + j, z, 3 - j, B.JungleLeaves);
        for (int j = y + h - 2 - r.NextInt(4); j > y + h / 2; j -= 2 + r.NextInt(4))
        {
            float f = r.NextFloat() * MathF.PI * 2f;
            int k = x + (int)(0.5f + MathF.Cos(f) * 4f), l = z + (int)(0.5f + MathF.Sin(f) * 4f);
            for (int i1 = 0; i1 < 5; i1++)
            {
                k = x + (int)(1.5f + MathF.Cos(f) * i1); l = z + (int)(1.5f + MathF.Sin(f) * i1);
                g.Set(k, j - 3 + i1 / 2, l, B.JungleLog, 0);
            }
            int j2 = 1 + r.NextInt(2);
            for (int k1 = j - j2; k1 <= j; k1++) LeafLayer(g, k, k1, l, 1 - (k1 - j), B.JungleLeaves);
        }
        void PV(int px, int py, int pz, int bit) { if (r.NextInt(3) > 0 && IsAir(g.Get(px, py, pz))) Vine(g, px, py, pz, bit); }
        for (int i2 = 0; i2 < h; i2++)
        {
            int yy = y + i2;
            if (AirOrLeaves(g.Get(x, yy, z))) { g.Set(x, yy, z, B.JungleLog, 0); if (i2 > 0) { PV(x - 1, yy, z, 8); PV(x, yy, z - 1, 1); } }
            if (i2 < h - 1)
            {
                if (AirOrLeaves(g.Get(x + 1, yy, z))) { g.Set(x + 1, yy, z, B.JungleLog, 0); if (i2 > 0) { PV(x + 2, yy, z, 2); PV(x + 1, yy, z - 1, 1); } }
                if (AirOrLeaves(g.Get(x + 1, yy, z + 1))) { g.Set(x + 1, yy, z + 1, B.JungleLog, 0); if (i2 > 0) { PV(x + 2, yy, z + 1, 2); PV(x + 1, yy, z + 2, 4); } }
                if (AirOrLeaves(g.Get(x, yy, z + 1))) { g.Set(x, yy, z + 1, B.JungleLog, 0); if (i2 > 0) { PV(x - 1, yy, z + 1, 8); PV(x, yy, z + 2, 4); } }
            }
        }
        return true;
    }

    // ---------------- WorldGenSavannaTree (akacja) ----------------
    public static bool Savanna(IGenAccess g, JRandom r, int x, int y, int z)
    {
        int h = r.NextInt(3) + r.NextInt(3) + 5;
        if (y < 1 || y + h + 1 > 256) return false;
        for (int j = y; j <= y + 1 + h; j++)
        {
            int k = j == y ? 0 : j >= y + 1 + h - 2 ? 2 : 1;
            for (int l = x - k; l <= x + k; l++) for (int m = z - k; m <= z + k; m++)
                    if (j < 0 || j >= 256 || !Replaceable(g.Get(l, j, m))) return false;
        }
        int gb = g.Get(x, y - 1, z);
        if (!(gb == B.Grass || gb == B.Dirt) || y >= 256 - h - 1) return false;
        g.Set(x, y - 1, z, B.Dirt, 0);
        void Leaf(int px, int py, int pz) { if (AirOrLeaves(g.Get(px, py, pz))) g.Set(px, py, pz, B.AcaciaLeaves, 0); }
        int f = r.NextInt(4), k2 = h - r.NextInt(4) - 1, l2 = 3 - r.NextInt(3);
        int cx = x, cz = z, top = 0;
        for (int l1 = 0; l1 < h; l1++)
        {
            int yy = y + l1;
            if (l1 >= k2 && l2 > 0) { cx += Dir.HorizX[f]; cz += Dir.HorizZ[f]; l2--; }
            if (AirOrLeaves(g.Get(cx, yy, cz))) { g.Set(cx, yy, cz, B.AcaciaLog, 0); top = yy; }
        }
        for (int a = -3; a <= 3; a++) for (int b = -3; b <= 3; b++) if (Math.Abs(a) != 3 || Math.Abs(b) != 3) Leaf(cx + a, top, cz + b);
        for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) Leaf(cx + a, top + 1, cz + b);
        Leaf(cx + 2, top + 1, cz); Leaf(cx - 2, top + 1, cz); Leaf(cx, top + 1, cz + 2); Leaf(cx, top + 1, cz - 2);
        cx = x; cz = z;
        int f1 = r.NextInt(4);
        if (f1 != f)
        {
            int l3 = k2 - r.NextInt(2) - 1, k4 = 1 + r.NextInt(3); top = 0;
            for (int l4 = l3; l4 < h && k4 > 0; k4--)
            {
                if (l4 >= 1)
                {
                    int yy = y + l4; cx += Dir.HorizX[f1]; cz += Dir.HorizZ[f1];
                    if (AirOrLeaves(g.Get(cx, yy, cz))) { g.Set(cx, yy, cz, B.AcaciaLog, 0); top = yy; }
                }
                l4++;
            }
            if (top > 0)
            {
                for (int a = -2; a <= 2; a++) for (int b = -2; b <= 2; b++) if (Math.Abs(a) != 2 || Math.Abs(b) != 2) Leaf(cx + a, top, cz + b);
                for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) Leaf(cx + a, top + 1, cz + b);
            }
        }
        return true;
    }

    // ---------------- WorldGenCanopyTree (ciemny dąb) ----------------
    public static bool Canopy(IGenAccess g, JRandom r, int x, int y, int z)
    {
        int h = r.NextInt(3) + r.NextInt(2) + 6;
        if (y < 1 || y + h + 1 >= 256) return false;
        int gb = g.Get(x, y - 1, z);
        if (gb != B.Grass && gb != B.Dirt) return false;
        for (int l = 0; l <= h + 1; l++)
        {
            int i1 = l == 0 ? 0 : l >= h - 1 ? 2 : 1;
            for (int a = -i1; a <= i1; a++) for (int b = -i1; b <= i1; b++) if (!Replaceable(g.Get(x + a, y + l, z + b))) return false;
        }
        g.Set(x, y - 1, z, B.Dirt, 0); g.Set(x + 1, y - 1, z, B.Dirt, 0); g.Set(x, y - 1, z + 1, B.Dirt, 0); g.Set(x + 1, y - 1, z + 1, B.Dirt, 0);
        void Leaf(int px, int py, int pz) { if (IsAir(g.Get(px, py, pz))) g.Set(px, py, pz, B.DarkOakLeaves, 0); }
        void Log(int px, int py, int pz) { if (AirOrLeaves(g.Get(px, py, pz))) g.Set(px, py, pz, B.DarkOakLog, 0); }
        int f = r.NextInt(4), i1b = h - r.NextInt(4), j1 = 2 - r.NextInt(3);
        int k1 = x, l1 = z, i2 = y + h - 1;
        for (int j2 = 0; j2 < h; j2++)
        {
            if (j2 >= i1b && j1 > 0) { k1 += Dir.HorizX[f]; l1 += Dir.HorizZ[f]; j1--; }
            int k2 = y + j2;
            if (AirOrLeaves(g.Get(k1, k2, l1))) { g.Set(k1, k2, l1, B.DarkOakLog, 0); g.Set(k1 + 1, k2, l1, B.DarkOakLog, 0); g.Set(k1, k2, l1 + 1, B.DarkOakLog, 0); g.Set(k1 + 1, k2, l1 + 1, B.DarkOakLog, 0); }
        }
        for (int i3 = -2; i3 <= 0; i3++)
            for (int l3 = -2; l3 <= 0; l3++)
            {
                int k4 = -1;
                Leaf(k1 + i3, i2 + k4, l1 + l3); Leaf(1 + k1 - i3, i2 + k4, l1 + l3); Leaf(k1 + i3, i2 + k4, 1 + l1 - l3); Leaf(1 + k1 - i3, i2 + k4, 1 + l1 - l3);
                if ((i3 > -2 || l3 > -1) && (i3 != -1 || l3 != -2))
                {
                    k4 = 1;
                    Leaf(k1 + i3, i2 + k4, l1 + l3); Leaf(1 + k1 - i3, i2 + k4, l1 + l3); Leaf(k1 + i3, i2 + k4, 1 + l1 - l3); Leaf(1 + k1 - i3, i2 + k4, 1 + l1 - l3);
                }
            }
        if (r.NextBool()) { Leaf(k1, i2 + 2, l1); Leaf(k1 + 1, i2 + 2, l1); Leaf(k1 + 1, i2 + 2, l1 + 1); Leaf(k1, i2 + 2, l1 + 1); }
        for (int j3 = -3; j3 <= 4; j3++)
            for (int i4 = -3; i4 <= 4; i4++)
                if ((j3 != -3 || i4 != -3) && (j3 != -3 || i4 != 4) && (j3 != 4 || i4 != -3) && (j3 != 4 || i4 != 4) && (Math.Abs(j3) < 3 || Math.Abs(i4) < 3))
                    Leaf(k1 + j3, i2, l1 + i4);
        for (int k3 = -1; k3 <= 2; k3++)
            for (int j4 = -1; j4 <= 2; j4++)
                if ((k3 < 0 || k3 > 1 || j4 < 0 || j4 > 1) && r.NextInt(3) <= 0)
                {
                    int l4 = r.NextInt(3) + 2;
                    for (int i5 = 0; i5 < l4; i5++) Log(x + k3, i2 - i5 - 1, z + j4);
                    for (int j5 = -1; j5 <= 1; j5++) for (int l2 = -1; l2 <= 1; l2++) Leaf(k1 + k3 + j5, i2, l1 + j4 + l2);
                    for (int k5 = -2; k5 <= 2; k5++) for (int l5 = -2; l5 <= 2; l5++) if (Math.Abs(k5) != 2 || Math.Abs(l5) != 2) Leaf(k1 + k3 + k5, i2 - 1, l1 + j4 + l5);
                }
        return true;
    }

    // ---------------- WorldGenSwamp ----------------
    public static bool Swamp(IGenAccess g, JRandom r, int x, int y, int z)
    {
        int h = r.NextInt(4) + 5;
        while (y > 1 && g.Get(x, y - 1, z) == B.Water) y--;
        if (y < 1 || y + h + 1 > 256) return false;
        for (int j = y; j <= y + 1 + h; j++)
        {
            int k = j == y ? 0 : j >= y + 1 + h - 2 ? 3 : 1;
            for (int l = x - k; l <= x + k; l++) for (int m = z - k; m <= z + k; m++)
                {
                    if (j < 0 || j >= 256) return false;
                    int b = g.Get(l, j, m);
                    if (!AirOrLeaves(b)) { if (b != B.Water) return false; if (j > y) return false; }
                }
        }
        if (!(g.Get(x, y - 1, z) is B.Grass or B.Dirt) || y >= 256 - h - 1) return false;
        g.Set(x, y - 1, z, B.Dirt, 0);
        for (int i2 = y - 3 + h; i2 <= y + h; i2++)
        {
            int l2 = i2 - (y + h), j3 = 2 - l2 / 2;
            for (int a = x - j3; a <= x + j3; a++) for (int b = z - j3; b <= z + j3; b++)
                    if ((Math.Abs(a - x) != j3 || Math.Abs(b - z) != j3 || r.NextInt(2) != 0 && l2 != 0) && !FullBlock(g.Get(a, i2, b)))
                        g.Set(a, i2, b, B.OakLeaves, 0);
        }
        for (int i3 = 0; i3 < h; i3++)
        {
            int b = g.Get(x, y + i3, z);
            if (AirOrLeaves(b) || b == B.Water) g.Set(x, y + i3, z, B.OakLog, 0);
        }
        for (int k3 = y - 3 + h; k3 <= y + h; k3++)
        {
            int i4 = k3 - (y + h), j4 = 2 - i4 / 2;
            for (int a = x - j4; a <= x + j4; a++) for (int b = z - j4; b <= z + j4; b++)
                {
                    if (!IsLeaves(g.Get(a, k3, b))) continue;
                    if (r.NextInt(4) == 0 && IsAir(g.Get(a - 1, k3, b))) VineDown(g, a - 1, k3, b, 8);
                    if (r.NextInt(4) == 0 && IsAir(g.Get(a + 1, k3, b))) VineDown(g, a + 1, k3, b, 2);
                    if (r.NextInt(4) == 0 && IsAir(g.Get(a, k3, b - 1))) VineDown(g, a, k3, b - 1, 1);
                    if (r.NextInt(4) == 0 && IsAir(g.Get(a, k3, b + 1))) VineDown(g, a, k3, b + 1, 4);
                }
        }
        return true;
    }

    // ---------------- WorldGenShrub (krzak w dżungli) ----------------
    public static bool Shrub(IGenAccess g, JRandom r, int x, int y, int z)
    {
        while (AirOrLeaves(g.Get(x, y, z)) && y > 0) y--;
        int b1 = g.Get(x, y, z);
        if (b1 != B.Dirt && b1 != B.Grass) return true;
        y++;
        g.Set(x, y, z, B.JungleLog, 0);
        for (int i = y; i <= y + 2; i++)
        {
            int k = 2 - (i - y);
            for (int l = x - k; l <= x + k; l++) for (int j1 = z - k; j1 <= z + k; j1++)
                    if ((Math.Abs(l - x) != k || Math.Abs(j1 - z) != k || r.NextInt(2) != 0) && !FullBlock(g.Get(l, i, j1)))
                        g.Set(l, i, j1, B.OakLeaves, 0);
        }
        return true;
    }

    // ---------------- WorldGenBigTree (duży dąb) ----------------
    public static bool BigTree(IGenAccess g, JRandom rand, int bx, int by, int bz)
    {
        var r = new JRandom(rand.NextLong());
        int heightLimit = 5 + r.NextInt(12);
        const int leafDist = 4;
        int down = g.Get(bx, by - 1, bz);
        if (down != B.Dirt && down != B.Grass && down != B.Farmland) return false;
        int line = CheckLine(g, bx, by, bz, bx, by + heightLimit - 1, bz);
        if (line != -1) { if (line < 6) return false; heightLimit = line; }
        int height = (int)(heightLimit * 0.618);
        if (height >= heightLimit) height = heightLimit - 1;
        int per = (int)(1.382 + Math.Pow(1.0 * heightLimit / 13.0, 2.0));
        if (per < 1) per = 1;
        int topY = by + height;
        int k = heightLimit - leafDist;
        var nodes = new List<(int x, int y, int z, int baseY)> { (bx, by + k, bz, topY) };
        float LayerSize(int yy)
        {
            if (yy < heightLimit * 0.3f) return -1f;
            float f = heightLimit / 2f, f1 = f - yy, f2 = MathF.Sqrt(f * f - f1 * f1);
            if (f1 == 0) f2 = f; else if (Math.Abs(f1) >= f) return 0f;
            return f2 * 0.5f;
        }
        for (; k >= 0; k--)
        {
            float f = LayerSize(k);
            if (f < 0) continue;
            for (int l = 0; l < per; l++)
            {
                double d0 = 1.0 * f * (r.NextFloat() + 0.328);
                double d1 = r.NextFloat() * 2.0f * Math.PI;
                int px = (int)Math.Floor(bx + d0 * Math.Sin(d1) + 0.5), py = by + k - 1, pz = (int)Math.Floor(bz + d0 * Math.Cos(d1) + 0.5);
                if (CheckLine(g, px, py, pz, px, py + leafDist, pz) != -1) continue;
                int dx = bx - px, dz = bz - pz;
                double d4 = py - Math.Sqrt(dx * dx + dz * dz) * 0.381;
                int k1 = d4 > topY ? topY : (int)d4;
                if (CheckLine(g, bx, k1, bz, px, py, pz) == -1) nodes.Add((px, py, pz, k1));
            }
        }
        foreach (var n in nodes)
            for (int i = 0; i < leafDist; i++)
            {
                float rad = i >= 0 && i < leafDist ? (i != 0 && i != leafDist - 1 ? 3f : 2f) : -1f;
                int ri = (int)(rad + 0.618);
                for (int a = -ri; a <= ri; a++) for (int b = -ri; b <= ri; b++)
                        if (Math.Pow(Math.Abs(a) + 0.5, 2) + Math.Pow(Math.Abs(b) + 0.5, 2) <= rad * rad && AirOrLeaves(g.Get(n.x + a, n.y + i, n.z + b)))
                            g.Set(n.x + a, n.y + i, n.z + b, B.OakLeaves, 0);
            }
        Limb(g, bx, by, bz, bx, by + height, bz);
        foreach (var n in nodes)
            if (!(n.x == bx && n.baseY == n.y && n.z == bz) && n.baseY - by >= heightLimit * 0.2)
                Limb(g, bx, n.baseY, bz, n.x, n.y, n.z);
        return true;
    }
    private static int CheckLine(IGenAccess g, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        int dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
        int n = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
        if (n == 0) return -1;
        float fx = dx / (float)n, fy = dy / (float)n, fz = dz / (float)n;
        for (int j = 0; j <= n; j++)
        {
            int px = (int)Math.Floor(x0 + (0.5f + j * fx)), py = (int)Math.Floor(y0 + (0.5f + j * fy)), pz = (int)Math.Floor(z0 + (0.5f + j * fz));
            if (!Replaceable(g.Get(px, py, pz))) return j;
        }
        return -1;
    }
    private static void Limb(IGenAccess g, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        int dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
        int n = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
        float fx = n == 0 ? 0 : dx / (float)n, fy = n == 0 ? 0 : dy / (float)n, fz = n == 0 ? 0 : dz / (float)n;
        for (int j = 0; j <= n; j++)
        {
            int px = (int)Math.Floor(x0 + (0.5f + j * fx)), py = (int)Math.Floor(y0 + (0.5f + j * fy)), pz = (int)Math.Floor(z0 + (0.5f + j * fz));
            int ax = Math.Abs(px - x0), az = Math.Abs(pz - z0), m = Math.Max(ax, az);
            int axis = m > 0 ? (ax == m ? 1 : 2) : 0;
            g.Set(px, py, pz, B.OakLog, axis);
        }
    }

    // ---------------- WorldGenBigMushroom ----------------
    /// <summary>type: 0 brązowy, 1 czerwony, -1 losowy.</summary>
    public static bool HugeMushroom(IGenAccess g, JRandom r, int x, int y, int z, int type)
    {
        if (type < 0) type = r.NextBool() ? 0 : 1;
        byte block = type == 0 ? B.BrownMushroomBlock : B.RedMushroomBlock;
        int h = r.NextInt(3) + 4;
        if (y < 1 || y + h + 1 >= 256) return false;
        for (int k = y; k <= y + 1 + h; k++)
        {
            int l = k <= y + 3 ? 0 : 3;
            for (int a = x - l; a <= x + l; a++) for (int b = z - l; b <= z + l; b++)
                    if (k < 0 || k >= 256 || !AirOrLeaves(g.Get(a, k, b))) return false;
        }
        int gb = g.Get(x, y - 1, z);
        if (gb != B.Dirt && gb != B.Grass && gb != B.Mycelium) return false;
        int k2 = y + h; if (type == 1) k2 = y + h - 3;
        for (int l2 = k2; l2 <= y + h; l2++)
        {
            int j3 = 1; if (l2 < y + h) j3++; if (type == 0) j3 = 3;
            int k3 = x - j3, l3 = x + j3, j1 = z - j3, k1 = z + j3;
            for (int l1 = k3; l1 <= l3; l1++)
                for (int i2 = j1; i2 <= k1; i2++)
                {
                    int t = 5;
                    if (l1 == k3) t--; else if (l1 == l3) t++;
                    if (i2 == j1) t -= 3; else if (i2 == k1) t += 3;
                    if (type == 0 || l2 < y + h)
                    {
                        if ((l1 == k3 || l1 == l3) && (i2 == j1 || i2 == k1)) continue;
                        if (l1 == x - (j3 - 1) && i2 == j1) t = 1;
                        if (l1 == k3 && i2 == z - (j3 - 1)) t = 1;
                        if (l1 == x + (j3 - 1) && i2 == j1) t = 3;
                        if (l1 == l3 && i2 == z - (j3 - 1)) t = 3;
                        if (l1 == x - (j3 - 1) && i2 == k1) t = 7;
                        if (l1 == k3 && i2 == z + (j3 - 1)) t = 7;
                        if (l1 == x + (j3 - 1) && i2 == k1) t = 9;
                        if (l1 == l3 && i2 == z + (j3 - 1)) t = 9;
                    }
                    if (t == 5 && l2 < y + h) t = 0;
                    if (t != 0 && !FullBlock(g.Get(l1, l2, i2))) g.Set(l1, l2, i2, block, t);
                }
        }
        for (int i3 = 0; i3 < h; i3++) if (!FullBlock(g.Get(x, y + i3, z))) g.Set(x, y + i3, z, block, 10);
        return true;
    }
}
