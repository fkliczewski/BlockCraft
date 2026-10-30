using System.Collections.Concurrent;
using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>
/// Stos warstw biomów jak GenLayer w MC 1.8 (te same ziarna, kolejność i arytmetyka long), więc ziarno daje
/// taki sam układ biomów jak w grze. Warstwy trzymają stan losowania, dlatego każdy wątek ma własny stos (OwBiomeProvider).
/// </summary>
public abstract class GenLayer
{
    private const long Mul = 6364136223846793005L, Add = 1442695040888963407L;
    private long _worldGenSeed, _chunkSeed;
    protected GenLayer Parent;
    protected long BaseSeed;

    protected GenLayer(long seed)
    {
        unchecked
        {
            BaseSeed = seed;
            BaseSeed *= BaseSeed * Mul + Add; BaseSeed += seed;
            BaseSeed *= BaseSeed * Mul + Add; BaseSeed += seed;
            BaseSeed *= BaseSeed * Mul + Add; BaseSeed += seed;
        }
    }

    public virtual void InitWorldGenSeed(long seed)
    {
        unchecked
        {
            _worldGenSeed = seed;
            Parent?.InitWorldGenSeed(seed);
            _worldGenSeed *= _worldGenSeed * Mul + Add; _worldGenSeed += BaseSeed;
            _worldGenSeed *= _worldGenSeed * Mul + Add; _worldGenSeed += BaseSeed;
            _worldGenSeed *= _worldGenSeed * Mul + Add; _worldGenSeed += BaseSeed;
        }
    }

    protected void InitChunkSeed(long x, long z)
    {
        unchecked
        {
            _chunkSeed = _worldGenSeed;
            _chunkSeed *= _chunkSeed * Mul + Add; _chunkSeed += x;
            _chunkSeed *= _chunkSeed * Mul + Add; _chunkSeed += z;
            _chunkSeed *= _chunkSeed * Mul + Add; _chunkSeed += x;
            _chunkSeed *= _chunkSeed * Mul + Add; _chunkSeed += z;
        }
    }

    protected int NextInt(int bound)
    {
        unchecked
        {
            int i = (int)((_chunkSeed >> 24) % bound);
            if (i < 0) i += bound;
            _chunkSeed *= _chunkSeed * Mul + Add;
            _chunkSeed += _worldGenSeed;
            return i;
        }
    }

    public abstract int[] GetInts(int x, int z, int w, int h);

    protected int SelectRandom(int a, int b) => NextInt(2) == 0 ? a : b;
    protected int SelectRandom(int a, int b, int c, int d) => NextInt(4) switch { 0 => a, 1 => b, 2 => c, _ => d };

    protected virtual int SelectModeOrRandom(int a, int b, int c, int d)
        => b == c && c == d ? b : a == b && a == c ? a : a == b && a == d ? a : a == c && a == d ? a : a == b && c != d ? a
        : a == c && b != d ? a : a == d && b != c ? a : b == c && a != d ? b : b == d && a != c ? b : c == d && a != b ? c
        : SelectRandom(a, b, c, d);

    /// <summary>biomesEqualOrMesaPlateau (celowo niesymetryczne jak w MC).</summary>
    public static bool EqualOrMesaPlateau(int a, int b)
    {
        if (a == b) return true;
        if (a == Biome.MesaPlateauF || a == Biome.MesaPlateau) return b == Biome.MesaPlateauF || b == Biome.MesaPlateau;
        return Biome.Exists(a) && Biome.Exists(b) && Biome.Get(a).Kind == Biome.Get(b).Kind;
    }
    public static bool IsOceanic(int id) => id == Biome.Ocean || id == Biome.DeepOcean || id == Biome.FrozenOcean;

    /// <summary>GenLayer.initializeAllBiomeGenerators: [0] biomy w skali 1:4 (generacja terenu), [1] biomy blokowe (Voronoi).</summary>
    public static GenLayer[] Build(long seed, bool largeBiomes)
    {
        GenLayer g = new LayerIsland(1);
        g = new LayerZoom(2000, g, true);
        g = new LayerAddIsland(1, g);
        g = new LayerZoom(2001, g);
        g = new LayerAddIsland(2, g);
        g = new LayerAddIsland(50, g);
        g = new LayerAddIsland(70, g);
        g = new LayerRemoveTooMuchOcean(2, g);
        g = new LayerAddSnow(2, g);
        g = new LayerAddIsland(3, g);
        g = new LayerEdge(2, g, 0);
        g = new LayerEdge(2, g, 1);
        g = new LayerEdge(3, g, 2);
        g = new LayerZoom(2002, g);
        g = new LayerZoom(2003, g);
        g = new LayerAddIsland(4, g);
        g = new LayerAddMushroomIsland(5, g);
        g = new LayerDeepOcean(4, g);
        var g4 = LayerZoom.Magnify(1000, g, 0);
        int biomeSize = largeBiomes ? 6 : 4, riverSize = 4;
        var riverInit = new LayerRiverInit(100, LayerZoom.Magnify(1000, g4, 0));
        var biome = new LayerBiome(200, g4);
        var biomeEdge = new LayerBiomeEdge(1000, LayerZoom.Magnify(1000, biome, 2));
        GenLayer hills = new LayerHills(1000, biomeEdge, LayerZoom.Magnify(1000, riverInit, 2));
        var river5 = LayerZoom.Magnify(1000, LayerZoom.Magnify(1000, riverInit, 2), riverSize);
        var riverSmooth = new LayerSmooth(1000, new LayerRiver(1, river5));
        hills = new LayerRareBiome(1001, hills);
        for (int k = 0; k < biomeSize; k++)
        {
            hills = new LayerZoom(1000 + k, hills);
            if (k == 0) hills = new LayerAddIsland(3, hills);
            if (k == 1 || biomeSize == 1) hills = new LayerShore(1000, hills);
        }
        var mix = new LayerRiverMix(100, new LayerSmooth(1000, hills), riverSmooth);
        var voronoi = new LayerVoronoiZoom(10, mix);
        mix.InitWorldGenSeed(seed);
        voronoi.InitWorldGenSeed(seed);
        return new GenLayer[] { mix, voronoi };
    }
}

sealed class LayerIsland : GenLayer
{
    public LayerIsland(long s) : base(s) { }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++) { InitChunkSeed(x + j, z + i); o[j + i * w] = NextInt(10) == 0 ? 1 : 0; }
        if (x > -w && x <= 0 && z > -h && z <= 0) o[-x + -z * w] = 1;
        return o;
    }
}

sealed class LayerZoom : GenLayer
{
    private readonly bool _fuzzy;
    public LayerZoom(long s, GenLayer p, bool fuzzy = false) : base(s) { Parent = p; _fuzzy = fuzzy; }
    protected override int SelectModeOrRandom(int a, int b, int c, int d) => _fuzzy ? SelectRandom(a, b, c, d) : base.SelectModeOrRandom(a, b, c, d);
    public static GenLayer Magnify(long seed, GenLayer l, int times)
    {
        for (int i = 0; i < times; i++) l = new LayerZoom(seed + i, l);
        return l;
    }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        int px = x >> 1, pz = z >> 1, pw = (w >> 1) + 2, ph = (h >> 1) + 2;
        var a = Parent.GetInts(px, pz, pw, ph);
        int ow = (pw - 1) << 1, oh = (ph - 1) << 1;
        var t = new int[ow * oh];
        for (int k1 = 0; k1 < ph - 1; k1++)
        {
            int l1 = (k1 << 1) * ow;
            int i2 = 0;
            int j2 = a[i2 + (k1 + 0) * pw];
            for (int k2 = a[i2 + (k1 + 1) * pw]; i2 < pw - 1; i2++)
            {
                InitChunkSeed((i2 + px) << 1, (k1 + pz) << 1);
                int l2 = a[i2 + 1 + (k1 + 0) * pw];
                int i3 = a[i2 + 1 + (k1 + 1) * pw];
                t[l1] = j2;
                t[l1++ + ow] = SelectRandom(j2, k2);
                t[l1] = SelectRandom(j2, l2);
                t[l1++ + ow] = SelectModeOrRandom(j2, l2, k2, i3);
                j2 = l2; k2 = i3;
            }
        }
        var o = new int[w * h];
        for (int j3 = 0; j3 < h; j3++) Array.Copy(t, (j3 + (z & 1)) * ow + (x & 1), o, j3 * w, w);
        return o;
    }
}

sealed class LayerAddIsland : GenLayer
{
    public LayerAddIsland(long s, GenLayer p) : base(s) { Parent = p; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        int k = w + 2;
        var a = Parent.GetInts(x - 1, z - 1, k, h + 2);
        var o = new int[w * h];
        for (int i1 = 0; i1 < h; i1++)
            for (int j1 = 0; j1 < w; j1++)
            {
                int nw = a[j1 + (i1 + 0) * k], ne = a[j1 + 2 + (i1 + 0) * k], sw = a[j1 + (i1 + 2) * k], se = a[j1 + 2 + (i1 + 2) * k];
                int c = a[j1 + 1 + (i1 + 1) * k];
                InitChunkSeed(j1 + x, i1 + z);
                if (c != 0 || nw == 0 && ne == 0 && sw == 0 && se == 0)
                {
                    if (c > 0 && (nw == 0 || ne == 0 || sw == 0 || se == 0))
                        o[j1 + i1 * w] = NextInt(5) == 0 ? (c == 4 ? 4 : 0) : c;
                    else o[j1 + i1 * w] = c;
                }
                else
                {
                    int n = 1, r = 1;
                    if (nw != 0 && NextInt(n++) == 0) r = nw;
                    if (ne != 0 && NextInt(n++) == 0) r = ne;
                    if (sw != 0 && NextInt(n++) == 0) r = sw;
                    if (se != 0 && NextInt(n++) == 0) r = se;
                    o[j1 + i1 * w] = NextInt(3) == 0 ? r : r == 4 ? 4 : 0;
                }
            }
        return o;
    }
}

/// <summary>Bazowa warstwa z sąsiedztwem 3x3 (parent.getInts(x-1, z-1, w+2, h+2)).</summary>
abstract class CrossLayer : GenLayer
{
    protected CrossLayer(long s, GenLayer p) : base(s) { Parent = p; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        int k = w + 2;
        var a = Parent.GetInts(x - 1, z - 1, k, h + 2);
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++)
            {
                int c = a[j + 1 + (i + 1) * k];
                int n = a[j + 1 + i * k], e = a[j + 2 + (i + 1) * k], wv = a[j + (i + 1) * k], s = a[j + 1 + (i + 2) * k];
                o[j + i * w] = Cell(x + j, z + i, c, n, e, wv, s);
            }
        return o;
    }
    protected abstract int Cell(int x, int z, int c, int n, int e, int w, int s);
}

sealed class LayerRemoveTooMuchOcean : CrossLayer
{
    public LayerRemoveTooMuchOcean(long s, GenLayer p) : base(s, p) { }
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        InitChunkSeed(x, z);
        return c == 0 && n == 0 && e == 0 && w == 0 && s == 0 && NextInt(2) == 0 ? 1 : c;
    }
}

sealed class LayerAddSnow : CrossLayer
{
    public LayerAddSnow(long s, GenLayer p) : base(s, p) { }
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        InitChunkSeed(x, z);
        if (c == 0) return 0;
        int l = NextInt(6);
        return l == 0 ? 4 : l <= 1 ? 3 : 1;
    }
}

/// <summary>GenLayerEdge: tryb 0 COOL_WARM, 1 HEAT_ICE, 2 SPECIAL.</summary>
sealed class LayerEdge : CrossLayer
{
    private readonly int _mode;
    public LayerEdge(long s, GenLayer p, int mode) : base(s, p) { _mode = mode; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        if (_mode != 2) return base.GetInts(x, z, w, h);
        var a = Parent.GetInts(x, z, w, h);
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++)
            {
                InitChunkSeed(j + x, i + z);
                int k = a[j + i * w];
                if (k != 0 && NextInt(13) == 0) k |= ((1 + NextInt(15)) << 8) & 3840;
                o[j + i * w] = k;
            }
        return o;
    }
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        InitChunkSeed(x, z);
        if (_mode == 0 && c == 1 && (n == 3 || e == 3 || w == 3 || s == 3 || n == 4 || e == 4 || w == 4 || s == 4)) return 2;
        if (_mode == 1 && c == 4 && (n == 2 || e == 2 || w == 2 || s == 2 || n == 1 || e == 1 || w == 1 || s == 1)) return 3;
        return c;
    }
}

sealed class LayerAddMushroomIsland : GenLayer
{
    public LayerAddMushroomIsland(long s, GenLayer p) : base(s) { Parent = p; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        int k = w + 2;
        var a = Parent.GetInts(x - 1, z - 1, k, h + 2);
        var o = new int[w * h];
        for (int i1 = 0; i1 < h; i1++)
            for (int j1 = 0; j1 < w; j1++)
            {
                int nw = a[j1 + i1 * k], ne = a[j1 + 2 + i1 * k], sw = a[j1 + (i1 + 2) * k], se = a[j1 + 2 + (i1 + 2) * k], c = a[j1 + 1 + (i1 + 1) * k];
                InitChunkSeed(j1 + x, i1 + z);
                o[j1 + i1 * w] = c == 0 && nw == 0 && ne == 0 && sw == 0 && se == 0 && NextInt(100) == 0 ? Biome.MushroomIsland : c;
            }
        return o;
    }
}

sealed class LayerDeepOcean : CrossLayer
{
    public LayerDeepOcean(long s, GenLayer p) : base(s, p) { }
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        int l = (n == 0 ? 1 : 0) + (e == 0 ? 1 : 0) + (w == 0 ? 1 : 0) + (s == 0 ? 1 : 0);
        return c == 0 && l > 3 ? Biome.DeepOcean : c;
    }
}

sealed class LayerRiverInit : GenLayer
{
    public LayerRiverInit(long s, GenLayer p) : base(s) { Parent = p; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        var a = Parent.GetInts(x, z, w, h);
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++) { InitChunkSeed(j + x, i + z); o[j + i * w] = a[j + i * w] > 0 ? NextInt(299999) + 2 : 0; }
        return o;
    }
}

sealed class LayerBiome : GenLayer
{
    private static readonly int[] Warm = { Biome.Desert, Biome.Desert, Biome.Desert, Biome.Savanna, Biome.Savanna, Biome.Plains };
    private static readonly int[] Medium = { Biome.Forest, Biome.RoofedForest, Biome.ExtremeHills, Biome.Plains, Biome.BirchForest, Biome.Swampland };
    private static readonly int[] Cold = { Biome.Forest, Biome.ExtremeHills, Biome.Taiga, Biome.Plains };
    private static readonly int[] Ice = { Biome.IcePlains, Biome.IcePlains, Biome.IcePlains, Biome.ColdTaiga };
    public LayerBiome(long s, GenLayer p) : base(s) { Parent = p; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        var a = Parent.GetInts(x, z, w, h);
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++)
            {
                InitChunkSeed(j + x, i + z);
                int k = a[j + i * w];
                int l = (k & 3840) >> 8;
                k &= -3841;
                int r;
                if (IsOceanic(k)) r = k;
                else if (k == Biome.MushroomIsland) r = k;
                else if (k == 1) r = l > 0 ? (NextInt(3) == 0 ? Biome.MesaPlateau : Biome.MesaPlateauF) : Warm[NextInt(Warm.Length)];
                else if (k == 2) r = l > 0 ? Biome.Jungle : Medium[NextInt(Medium.Length)];
                else if (k == 3) r = l > 0 ? Biome.MegaTaiga : Cold[NextInt(Cold.Length)];
                else if (k == 4) r = Ice[NextInt(Ice.Length)];
                else r = Biome.MushroomIsland;
                o[j + i * w] = r;
            }
        return o;
    }
}

sealed class LayerBiomeEdge : GenLayer
{
    public LayerBiomeEdge(long s, GenLayer p) : base(s) { Parent = p; }
    private static bool CanBeNeighbors(int a, int b)
    {
        if (EqualOrMesaPlateau(a, b)) return true;
        if (!Biome.Exists(a) || !Biome.Exists(b)) return false;
        var ta = Biome.Get(a).TempCategory; var tb = Biome.Get(b).TempCategory;
        return ta == tb || ta == TempCategory.Medium || tb == TempCategory.Medium;
    }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        int k0 = w + 2;
        var a = Parent.GetInts(x - 1, z - 1, k0, h + 2);
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++)
            {
                InitChunkSeed(j + x, i + z);
                int k = a[j + 1 + (i + 1) * k0];
                int n = a[j + 1 + i * k0], e = a[j + 2 + (i + 1) * k0], wv = a[j + (i + 1) * k0], s = a[j + 1 + (i + 2) * k0];
                int r;
                if (EqualOrMesaPlateau(k, Biome.ExtremeHills))
                    r = CanBeNeighbors(n, Biome.ExtremeHills) && CanBeNeighbors(e, Biome.ExtremeHills) && CanBeNeighbors(wv, Biome.ExtremeHills) && CanBeNeighbors(s, Biome.ExtremeHills) ? k : Biome.ExtremeHillsEdge;
                else if (k == Biome.MesaPlateauF || k == Biome.MesaPlateau || k == Biome.MegaTaiga)
                {
                    int rep = k == Biome.MegaTaiga ? Biome.Taiga : Biome.Mesa;
                    r = EqualOrMesaPlateau(n, k) && EqualOrMesaPlateau(e, k) && EqualOrMesaPlateau(wv, k) && EqualOrMesaPlateau(s, k) ? k : rep;
                }
                else if (k == Biome.Desert)
                    r = n != Biome.IcePlains && e != Biome.IcePlains && wv != Biome.IcePlains && s != Biome.IcePlains ? k : Biome.ExtremeHillsPlus;
                else if (k == Biome.Swampland)
                {
                    bool bad(int v) => v == Biome.Desert || v == Biome.ColdTaiga || v == Biome.IcePlains;
                    if (bad(n) || bad(e) || bad(wv) || bad(s)) r = Biome.Plains;
                    else r = n != Biome.Jungle && e != Biome.Jungle && wv != Biome.Jungle && s != Biome.Jungle ? k : Biome.JungleEdge;
                }
                else r = k;
                o[j + i * w] = r;
            }
        return o;
    }
}

sealed class LayerHills : GenLayer
{
    private readonly GenLayer _river;
    // jak w MC: initWorldGenSeed nie schodzi do warstwy rzek (jej dodatkowe zoomy mają ziarno świata 0)
    public LayerHills(long s, GenLayer p, GenLayer river) : base(s) { Parent = p; _river = river; }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        int k0 = w + 2;
        var a = Parent.GetInts(x - 1, z - 1, k0, h + 2);
        var rv = _river.GetInts(x - 1, z - 1, k0, h + 2);
        var o = new int[w * h];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < w; j++)
            {
                InitChunkSeed(j + x, i + z);
                int k = a[j + 1 + (i + 1) * k0];
                int l = rv[j + 1 + (i + 1) * k0];
                bool flag = (l - 2) % 29 == 0;
                if (k != 0 && l >= 2 && (l - 2) % 29 == 1 && k < 128)
                {
                    o[j + i * w] = Biome.Exists(k + 128) ? k + 128 : k;
                    continue;
                }
                if (NextInt(3) != 0 && !flag) { o[j + i * w] = k; continue; }
                int i1 = k;
                if (k == Biome.Desert) i1 = Biome.DesertHills;
                else if (k == Biome.Forest) i1 = Biome.ForestHills;
                else if (k == Biome.BirchForest) i1 = Biome.BirchForestHills;
                else if (k == Biome.RoofedForest) i1 = Biome.Plains;
                else if (k == Biome.Taiga) i1 = Biome.TaigaHills;
                else if (k == Biome.MegaTaiga) i1 = Biome.MegaTaigaHills;
                else if (k == Biome.ColdTaiga) i1 = Biome.ColdTaigaHills;
                else if (k == Biome.Plains) i1 = NextInt(3) == 0 ? Biome.ForestHills : Biome.Forest;
                else if (k == Biome.IcePlains) i1 = Biome.IceMountains;
                else if (k == Biome.Jungle) i1 = Biome.JungleHills;
                else if (k == Biome.Ocean) i1 = Biome.DeepOcean;
                else if (k == Biome.ExtremeHills) i1 = Biome.ExtremeHillsPlus;
                else if (k == Biome.Savanna) i1 = Biome.SavannaPlateau;
                else if (EqualOrMesaPlateau(k, Biome.MesaPlateauF)) i1 = Biome.Mesa;
                else if (k == Biome.DeepOcean && NextInt(3) == 0) i1 = NextInt(2) == 0 ? Biome.Plains : Biome.Forest;
                if (flag && i1 != k) i1 = Biome.Exists(i1 + 128) ? i1 + 128 : k;
                if (i1 == k) { o[j + i * w] = k; continue; }
                int n = a[j + 1 + i * k0], e = a[j + 2 + (i + 1) * k0], wv = a[j + (i + 1) * k0], s = a[j + 1 + (i + 2) * k0];
                int cnt = (EqualOrMesaPlateau(n, k) ? 1 : 0) + (EqualOrMesaPlateau(e, k) ? 1 : 0) + (EqualOrMesaPlateau(wv, k) ? 1 : 0) + (EqualOrMesaPlateau(s, k) ? 1 : 0);
                o[j + i * w] = cnt >= 3 ? i1 : k;
            }
        return o;
    }
}

sealed class LayerRareBiome : CrossLayer
{
    public LayerRareBiome(long s, GenLayer p) : base(s, p) { }
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        InitChunkSeed(x, z);
        return NextInt(57) == 0 && c == Biome.Plains ? Biome.SunflowerPlains : c;
    }
}

sealed class LayerShore : CrossLayer
{
    public LayerShore(long s, GenLayer p) : base(s, p) { }
    private static bool JungleCompatible(int id)
        => Biome.Exists(id) && Biome.Get(id).Kind == BiomeKind.Jungle || id == Biome.JungleEdge || id == Biome.Jungle || id == Biome.JungleHills
           || id == Biome.Forest || id == Biome.Taiga || IsOceanic(id);
    private static bool IsMesa(int id) => Biome.Exists(id) && Biome.Get(id).Kind == BiomeKind.Mesa;
    protected override int Cell(int x, int z, int k, int n, int e, int w, int s)
    {
        InitChunkSeed(x, z);
        var b = Biome.Exists(k) ? Biome.Get(k) : null;
        bool anyOcean = IsOceanic(n) || IsOceanic(e) || IsOceanic(w) || IsOceanic(s);
        if (k == Biome.MushroomIsland)
            return n != Biome.Ocean && e != Biome.Ocean && w != Biome.Ocean && s != Biome.Ocean ? k : Biome.MushroomIslandShore;
        if (b != null && b.Kind == BiomeKind.Jungle)
        {
            if (JungleCompatible(n) && JungleCompatible(e) && JungleCompatible(w) && JungleCompatible(s)) return anyOcean ? Biome.Beach : k;
            return Biome.JungleEdge;
        }
        if (k == Biome.ExtremeHills || k == Biome.ExtremeHillsPlus || k == Biome.ExtremeHillsEdge)
            return IsOceanic(k) || !anyOcean ? k : Biome.StoneBeach;
        if (b != null && b.Snow)
            return IsOceanic(k) || !anyOcean ? k : Biome.ColdBeach;
        if (k == Biome.Mesa || k == Biome.MesaPlateauF)
        {
            if (anyOcean) return k;
            return IsMesa(n) && IsMesa(e) && IsMesa(w) && IsMesa(s) ? k : Biome.Desert;
        }
        if (k == Biome.Ocean || k == Biome.DeepOcean || k == Biome.River || k == Biome.Swampland) return k;
        return anyOcean ? Biome.Beach : k;
    }
}

sealed class LayerRiver : CrossLayer
{
    public LayerRiver(long s, GenLayer p) : base(s, p) { }
    private static int F(int i) => i >= 2 ? 2 + (i & 1) : i;
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        int k2 = F(c), k1 = F(w), l1 = F(e), i2 = F(n), j2 = F(s);
        return k2 == k1 && k2 == i2 && k2 == l1 && k2 == j2 ? -1 : Biome.River;
    }
}

sealed class LayerSmooth : CrossLayer
{
    public LayerSmooth(long s, GenLayer p) : base(s, p) { }
    protected override int Cell(int x, int z, int c, int n, int e, int w, int s)
    {
        if (w == e && n == s)
        {
            InitChunkSeed(x, z);
            return NextInt(2) == 0 ? w : n;
        }
        if (w == e) c = w;
        if (n == s) c = n;
        return c;
    }
}

sealed class LayerRiverMix : GenLayer
{
    private readonly GenLayer _biome, _river;
    public LayerRiverMix(long s, GenLayer biome, GenLayer river) : base(s) { _biome = biome; _river = river; }
    public override void InitWorldGenSeed(long seed)
    {
        _biome.InitWorldGenSeed(seed);
        _river.InitWorldGenSeed(seed);
        base.InitWorldGenSeed(seed);
    }
    public override int[] GetInts(int x, int z, int w, int h)
    {
        var a = _biome.GetInts(x, z, w, h);
        var r = _river.GetInts(x, z, w, h);
        var o = new int[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int b = a[i];
            if (b != Biome.Ocean && b != Biome.DeepOcean && r[i] == Biome.River)
                o[i] = b == Biome.IcePlains ? Biome.FrozenRiver : b != Biome.MushroomIsland && b != Biome.MushroomIslandShore ? r[i] & 255 : Biome.MushroomIslandShore;
            else o[i] = b;
        }
        return o;
    }
}

sealed class LayerVoronoiZoom : GenLayer
{
    public LayerVoronoiZoom(long s, GenLayer p) : base(s) { Parent = p; }
    private double R() => (NextInt(1024) / 1024.0 - 0.5) * 3.6;
    public override int[] GetInts(int x, int z, int w, int h)
    {
        x -= 2; z -= 2;
        // Uwzględnij przesunięcie w komórce także dla prostokątów innych niż pełny chunk.
        int px = x >> 2, pz = z >> 2, pw = ((w + (x & 3) + 3) >> 2) + 1, ph = ((h + (z & 3) + 3) >> 2) + 1;
        var a = Parent.GetInts(px, pz, pw, ph);
        int ow = (pw - 1) << 2, oh = (ph - 1) << 2;
        var t = new int[ow * oh];
        for (int k1 = 0; k1 < ph - 1; k1++)
        {
            int l1 = 0;
            int i2 = a[l1 + (k1 + 0) * pw];
            for (int j2 = a[l1 + (k1 + 1) * pw]; l1 < pw - 1; l1++)
            {
                InitChunkSeed((l1 + px) << 2, (k1 + pz) << 2);
                double d1 = R(), d2 = R();
                InitChunkSeed((l1 + px + 1) << 2, (k1 + pz) << 2);
                double d3 = R() + 4.0, d4 = R();
                InitChunkSeed((l1 + px) << 2, (k1 + pz + 1) << 2);
                double d5 = R(), d6 = R() + 4.0;
                InitChunkSeed((l1 + px + 1) << 2, (k1 + pz + 1) << 2);
                double d7 = R() + 4.0, d8 = R() + 4.0;
                int k2 = a[l1 + 1 + (k1 + 0) * pw] & 255;
                int l2 = a[l1 + 1 + (k1 + 1) * pw] & 255;
                for (int i3 = 0; i3 < 4; i3++)
                {
                    int j3 = ((k1 << 2) + i3) * ow + (l1 << 2);
                    for (int k3 = 0; k3 < 4; k3++)
                    {
                        double d9 = (i3 - d2) * (i3 - d2) + (k3 - d1) * (k3 - d1);
                        double d10 = (i3 - d4) * (i3 - d4) + (k3 - d3) * (k3 - d3);
                        double d11 = (i3 - d6) * (i3 - d6) + (k3 - d5) * (k3 - d5);
                        double d12 = (i3 - d8) * (i3 - d8) + (k3 - d7) * (k3 - d7);
                        if (d9 < d10 && d9 < d11 && d9 < d12) t[j3++] = i2;
                        else if (d10 < d9 && d10 < d11 && d10 < d12) t[j3++] = k2;
                        else if (d11 < d9 && d11 < d10 && d11 < d12) t[j3++] = j2;
                        else t[j3++] = l2;
                    }
                }
                i2 = k2; j2 = l2;
            }
        }
        var o = new int[w * h];
        for (int l3 = 0; l3 < h; l3++) Array.Copy(t, (l3 + (z & 3)) * ow + (x & 3), o, l3 * w, w);
        return o;
    }
}

/// <summary>WorldChunkManager 1.8: biomy do generacji (1:4), biomy blokowe z cache, wyszukiwanie biomów dla struktur.</summary>
public sealed class OwBiomeProvider
{
    private readonly long _seed; private readonly bool _large;
    private readonly ThreadLocal<GenLayer[]> _layers;
    private readonly ConcurrentDictionary<long, byte[]> _cache = new();

    public OwBiomeProvider(long seed, bool largeBiomes)
    {
        _seed = seed; _large = largeBiomes;
        _layers = new ThreadLocal<GenLayer[]>(() => GenLayer.Build(_seed, _large));
    }

    /// <summary>getBiomesForGeneration: biomy w skali 1:4 (x, z w jednostkach 4 bloków).</summary>
    public int[] GenBiomes(int x, int z, int w, int h) => _layers.Value[0].GetInts(x, z, w, h);
    /// <summary>Biomy blokowe obszaru (Voronoi).</summary>
    public int[] BlockBiomes(int x, int z, int w, int h) => _layers.Value[1].GetInts(x, z, w, h);

    /// <summary>Biomy chunka 16x16 (z cache, jak BiomeCache).</summary>
    public byte[] ChunkBiomes(int cx, int cz)
    {
        long key = MathUtil.ChunkKey(cx, cz);
        if (_cache.TryGetValue(key, out var b)) return b;
        var ints = BlockBiomes(cx << 4, cz << 4, 16, 16);
        b = new byte[256];
        for (int i = 0; i < 256; i++) b[i] = (byte)(Biome.Exists(ints[i]) ? ints[i] : Biome.Ocean);
        if (_cache.Count > 8192) _cache.Clear();
        _cache[key] = b;
        return b;
    }
    public byte BiomeAt(int x, int z) => ChunkBiomes(x >> 4, z >> 4)[((z & 15) << 4) | (x & 15)];

    /// <summary>areBiomesViable: czy wszystkie biomy (1:4) w promieniu są na liście.</summary>
    public bool AreBiomesViable(int x, int z, int radius, ICollection<int> allowed)
    {
        int i = (x - radius) >> 2, j = (z - radius) >> 2, k = (x + radius) >> 2, l = (z + radius) >> 2;
        int w = k - i + 1, h = l - j + 1;
        var a = GenBiomes(i, j, w, h);
        for (int n = 0; n < w * h; n++) if (!allowed.Contains(a[n])) return false;
        return true;
    }

    /// <summary>findBiomePosition: losowy punkt (1:4) z dozwolonym biomem albo null.</summary>
    public (int x, int z)? FindBiomePosition(int x, int z, int range, ICollection<int> biomes, JRandom r)
    {
        int i = (x - range) >> 2, j = (z - range) >> 2, k = (x + range) >> 2, l = (z + range) >> 2;
        int w = k - i + 1, h = l - j + 1;
        var a = GenBiomes(i, j, w, h);
        (int, int)? pos = null; int found = 0;
        for (int n = 0; n < w * h; n++)
        {
            int px = (i + n % w) << 2, pz = (j + n / w) << 2;
            if (biomes.Contains(a[n]) && (pos == null || r.NextInt(found + 1) == 0)) { pos = (px, pz); found++; }
        }
        return pos;
    }
}
