namespace BlockCraft.World;

/// <summary>Odpowiednik java.util.Random (48-bitowy LCG) - generacja świata korzysta z niego jak MC 1.8.</summary>
public sealed class JRandom
{
    private long _seed;
    private double _nextGaussian; private bool _haveGaussian;
    public JRandom(long seed) { SetSeed(seed); }
    public void SetSeed(long seed) { _seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1); _haveGaussian = false; }
    private int Next(int bits)
    {
        _seed = (_seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1);
        return (int)((ulong)_seed >> (48 - bits));
    }
    public int NextInt() => Next(32);
    public int NextInt(int bound)
    {
        if (bound <= 0) throw new ArgumentOutOfRangeException(nameof(bound));
        if ((bound & -bound) == bound) return (int)((bound * (long)Next(31)) >> 31);
        int bits, val;
        do { bits = Next(31); val = bits % bound; } while (bits - val + (bound - 1) < 0);
        return val;
    }
    public long NextLong() => ((long)Next(32) << 32) + Next(32);
    public bool NextBool() => Next(1) != 0;
    public float NextFloat() => Next(24) / (float)(1 << 24);
    public double NextDouble() => (((long)Next(26) << 27) + Next(27)) * (1.0 / (1L << 53));
    public double NextGaussian()
    {
        if (_haveGaussian) { _haveGaussian = false; return _nextGaussian; }
        double v1, v2, s;
        do { v1 = 2 * NextDouble() - 1; v2 = 2 * NextDouble() - 1; s = v1 * v1 + v2 * v2; } while (s >= 1 || s == 0);
        double m = Math.Sqrt(-2 * Math.Log(s) / s);
        _nextGaussian = v2 * m; _haveGaussian = true;
        return v1 * m;
    }
}

/// <summary>NoiseGeneratorImproved z MC 1.8 (Perlin z tablicą permutacji, populateNoiseArray).</summary>
public sealed class ImprovedNoise
{
    private readonly int[] _p = new int[512];
    public readonly double XCoord, YCoord, ZCoord;
    private static readonly double[] GX = { 1, -1, 1, -1, 1, -1, 1, -1, 0, 0, 0, 0, 1, 0, -1, 0 };
    private static readonly double[] GY = { 1, 1, -1, -1, 0, 0, 0, 0, 1, -1, 1, -1, 1, -1, 1, -1 };
    private static readonly double[] GZ = { 0, 0, 0, 0, 1, 1, -1, -1, 1, 1, -1, -1, 0, 1, 0, -1 };
    private static readonly double[] G2X = { 1, -1, 1, -1, 1, -1, 1, -1, 0, 0, 0, 0, 1, 0, -1, 0 };
    private static readonly double[] G2Z = { 0, 0, 0, 0, 1, 1, -1, -1, 1, 1, -1, -1, 0, 1, 0, -1 };

    public ImprovedNoise(JRandom r)
    {
        XCoord = r.NextDouble() * 256; YCoord = r.NextDouble() * 256; ZCoord = r.NextDouble() * 256;
        for (int i = 0; i < 256; i++) _p[i] = i;
        for (int i = 0; i < 256; i++)
        {
            int j = r.NextInt(256 - i) + i;
            (_p[i], _p[j]) = (_p[j], _p[i]);
            _p[i + 256] = _p[i];
        }
    }

    private static double Lerp(double t, double a, double b) => a + t * (b - a);
    private static double Grad2(int h, double x, double z) { int j = h & 15; return G2X[j] * x + G2Z[j] * z; }
    private static double Grad(int h, double x, double y, double z) { int j = h & 15; return GX[j] * x + GY[j] * y + GZ[j] * z; }
    private static int Floor(double v) { int i = (int)v; return v < i ? i - 1 : i; }
    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    public void Populate(double[] arr, double xo, double yo, double zo, int xs, int ys, int zs, double xsc, double ysc, double zsc, double scale)
    {
        var p = _p;
        if (ys == 1)
        {
            int idx = 0; double inv = 1.0 / scale;
            for (int ix = 0; ix < xs; ix++)
            {
                double x = xo + ix * xsc + XCoord; int fx = Floor(x); int px = fx & 255; x -= fx; double u = Fade(x);
                for (int iz = 0; iz < zs; iz++)
                {
                    double z = zo + iz * zsc + ZCoord; int fz = Floor(z); int pz = fz & 255; z -= fz; double w = Fade(z);
                    int a = p[px], aa = p[a] + pz, b = p[px + 1], ba = p[b] + pz;
                    double l1 = Lerp(u, Grad2(p[aa], x, z), Grad(p[ba], x - 1, 0, z));
                    double l2 = Lerp(u, Grad(p[aa + 1], x, 0, z - 1), Grad(p[ba + 1], x - 1, 0, z - 1));
                    arr[idx++] += Lerp(w, l1, l2) * inv;
                }
            }
            return;
        }
        int i = 0; double inv3 = 1.0 / scale; int last = -1;
        double d1 = 0, d2 = 0, d3 = 0, d4 = 0;
        for (int ix = 0; ix < xs; ix++)
        {
            double x = xo + ix * xsc + XCoord; int fx = Floor(x); int px = fx & 255; x -= fx; double u = Fade(x);
            for (int iz = 0; iz < zs; iz++)
            {
                double z = zo + iz * zsc + ZCoord; int fz = Floor(z); int pz = fz & 255; z -= fz; double w = Fade(z);
                for (int iy = 0; iy < ys; iy++)
                {
                    double y = yo + iy * ysc + YCoord; int fy = Floor(y); int py = fy & 255; y -= fy; double v = Fade(y);
                    if (iy == 0 || py != last)
                    {
                        last = py;
                        int a = p[px] + py, aa = p[a] + pz, ab = p[a + 1] + pz, b = p[px + 1] + py, ba = p[b] + pz, bb = p[b + 1] + pz;
                        d1 = Lerp(u, Grad(p[aa], x, y, z), Grad(p[ba], x - 1, y, z));
                        d2 = Lerp(u, Grad(p[ab], x, y - 1, z), Grad(p[bb], x - 1, y - 1, z));
                        d3 = Lerp(u, Grad(p[aa + 1], x, y, z - 1), Grad(p[ba + 1], x - 1, y, z - 1));
                        d4 = Lerp(u, Grad(p[ab + 1], x, y - 1, z - 1), Grad(p[bb + 1], x - 1, y - 1, z - 1));
                    }
                    double e1 = Lerp(v, d1, d2), e2 = Lerp(v, d3, d4);
                    arr[i++] += Lerp(w, e1, e2) * inv3;
                }
            }
        }
    }
}

/// <summary>NoiseGeneratorOctaves z MC 1.8.</summary>
public sealed class OctavesNoise
{
    private readonly ImprovedNoise[] _o;
    public OctavesNoise(JRandom r, int octaves) { _o = new ImprovedNoise[octaves]; for (int i = 0; i < octaves; i++) _o[i] = new ImprovedNoise(r); }

    public double[] Generate(double[] arr, int x, int y, int z, int xs, int ys, int zs, double xsc, double ysc, double zsc)
    {
        if (arr == null || arr.Length < xs * ys * zs) arr = new double[xs * ys * zs]; else Array.Clear(arr, 0, xs * ys * zs);
        double d3 = 1.0;
        foreach (var o in _o)
        {
            double d0 = x * d3 * xsc, d1 = y * d3 * ysc, d2 = z * d3 * zsc;
            long k = (long)Math.Floor(d0), l = (long)Math.Floor(d2);
            d0 -= k; d2 -= l;
            k %= 16777216L; l %= 16777216L;
            d0 += k; d2 += l;
            o.Populate(arr, d0, d1, d2, xs, ys, zs, xsc * d3, ysc * d3, zsc * d3, d3);
            d3 /= 2.0;
        }
        return arr;
    }

    /// <summary>Wersja 2D (y = 10, ySize = 1).</summary>
    public double[] Generate(double[] arr, int x, int z, int xs, int zs, double xsc, double zsc) => Generate(arr, x, 10, z, xs, 1, zs, xsc, 1.0, zsc);
}

/// <summary>NoiseGeneratorSimplex z MC 1.8.</summary>
public sealed class SimplexNoise
{
    private static readonly int[][] Grad3 = { new[] { 1, 1, 0 }, new[] { -1, 1, 0 }, new[] { 1, -1, 0 }, new[] { -1, -1, 0 }, new[] { 1, 0, 1 }, new[] { -1, 0, 1 },
        new[] { 1, 0, -1 }, new[] { -1, 0, -1 }, new[] { 0, 1, 1 }, new[] { 0, -1, 1 }, new[] { 0, 1, -1 }, new[] { 0, -1, -1 } };
    private static readonly double F2 = 0.5 * (Math.Sqrt(3.0) - 1.0), G2 = (3.0 - Math.Sqrt(3.0)) / 6.0;
    private readonly int[] _p = new int[512];
    public readonly double XO, YO, ZO;

    public SimplexNoise(JRandom r)
    {
        XO = r.NextDouble() * 256; YO = r.NextDouble() * 256; ZO = r.NextDouble() * 256;
        for (int i = 0; i < 256; i++) _p[i] = i;
        for (int i = 0; i < 256; i++)
        {
            int j = r.NextInt(256 - i) + i;
            (_p[i], _p[j]) = (_p[j], _p[i]);
            _p[i + 256] = _p[i];
        }
    }

    private static int FastFloor(double v) => v > 0 ? (int)v : (int)v - 1;
    private static double Dot(int[] g, double x, double y) => g[0] * x + g[1] * y;

    public double Noise(double xin, double yin)
    {
        double s = (xin + yin) * F2;
        int i = FastFloor(xin + s), j = FastFloor(yin + s);
        double t = (i + j) * G2;
        double x0 = xin - (i - t), y0 = yin - (j - t);
        int i1, j1; if (x0 > y0) { i1 = 1; j1 = 0; } else { i1 = 0; j1 = 1; }
        double x1 = x0 - i1 + G2, y1 = y0 - j1 + G2, x2 = x0 - 1.0 + 2.0 * G2, y2 = y0 - 1.0 + 2.0 * G2;
        int ii = i & 255, jj = j & 255;
        int gi0 = _p[ii + _p[jj]] % 12, gi1 = _p[ii + i1 + _p[jj + j1]] % 12, gi2 = _p[ii + 1 + _p[jj + 1]] % 12;
        double n0 = 0, n1 = 0, n2 = 0;
        double t0 = 0.5 - x0 * x0 - y0 * y0; if (t0 >= 0) { t0 *= t0; n0 = t0 * t0 * Dot(Grad3[gi0], x0, y0); }
        double t1 = 0.5 - x1 * x1 - y1 * y1; if (t1 >= 0) { t1 *= t1; n1 = t1 * t1 * Dot(Grad3[gi1], x1, y1); }
        double t2 = 0.5 - x2 * x2 - y2 * y2; if (t2 >= 0) { t2 *= t2; n2 = t2 * t2 * Dot(Grad3[gi2], x2, y2); }
        return 70.0 * (n0 + n1 + n2);
    }

    /// <summary>func_151606_a: dodaje szum do tablicy (indeks: x wewnętrzny).</summary>
    public void Add(double[] arr, double x, double z, int xs, int zs, double xsc, double zsc, double amp)
    {
        int idx = 0;
        for (int j = 0; j < zs; j++)
        {
            double zz = (z + j) * zsc + YO;
            for (int i = 0; i < xs; i++)
            {
                double xx = (x + i) * xsc + XO;
                arr[idx++] += Noise(xx, zz) * amp;
            }
        }
    }
}

/// <summary>NoiseGeneratorPerlin z MC 1.8 (oktawy szumu simplex).</summary>
public sealed class SimplexOctaves
{
    private readonly SimplexNoise[] _o;
    public SimplexOctaves(JRandom r, int octaves) { _o = new SimplexNoise[octaves]; for (int i = 0; i < octaves; i++) _o[i] = new SimplexNoise(r); }

    public double Sample(double x, double z)
    {
        double d0 = 0, d1 = 1;
        foreach (var o in _o) { d0 += o.Noise(x * d1, z * d1) / d1; d1 /= 2; }
        return d0;
    }

    public double[] Generate(double[] arr, double x, double z, int xs, int zs, double xsc, double zsc, double p1, double p2 = 0.5)
    {
        if (arr == null || arr.Length < xs * zs) arr = new double[xs * zs]; else Array.Clear(arr, 0, xs * zs);
        double d1 = 1, d0 = 1;
        foreach (var o in _o) { o.Add(arr, x, z, xs, zs, xsc * d0 * d1, zsc * d0 * d1, 0.55 / d1); d0 *= p1; d1 *= p2; }
        return arr;
    }
}
