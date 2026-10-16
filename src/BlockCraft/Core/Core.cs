using System.Runtime.CompilerServices;

namespace BlockCraft.Core;

public static class MathUtil
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorToInt(double v) { int i = (int)v; return v < i ? i - 1 : i; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FloorToInt(float v) { int i = (int)v; return v < i ? i - 1 : i; }
    public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
    public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
    public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    public static float WrapDegrees(float d) { d %= 360f; if (d >= 180f) d -= 360f; if (d < -180f) d += 360f; return d; }
    public static float LerpAngle(float a, float b, float t) => a + WrapDegrees(b - a) * t;
    public static long ChunkKey(int cx, int cz) => ((long)cx << 32) | (uint)cz;
    public static int Hash(int x, int y, int z, long seed)
    {
        long h = seed ^ (x * 0x27d4eb2dL) ^ (y * 0x165667b1L) ^ (z * 0x9E3779B97F4A7C15UL.GetHashCode());
        h ^= h >> 15; h *= 0x2c1b3c6dL; h ^= h >> 12; h *= 0x297a2d39L; h ^= h >> 15;
        return (int)h;
    }
    public static uint HashU(int x, int y, int z)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + z * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }
}

/// <summary>Szybki deterministyczny generator (SplitMix64 / xorshift)</summary>
public sealed class Rng
{
    private ulong _s;
    public Rng(long seed) { SetSeed(seed); }
    public void SetSeed(long seed) { _s = (ulong)seed ^ 0x9E3779B97F4A7C15UL; if (_s == 0) _s = 1; NextULong(); }
    public ulong NextULong()
    {
        ulong z = (_s += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
    public int NextInt() => (int)(NextULong() >> 33);
    public int Next(int bound) => bound <= 0 ? 0 : (int)((NextULong() >> 33) % (ulong)bound);
    public int Next(int min, int maxExclusive) => min + Next(maxExclusive - min);
    public float NextFloat() => (NextULong() >> 40) * (1f / (1 << 24));
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
    public bool NextBool() => (NextULong() & 1) == 1;
    public double NextGaussian()
    {
        double u1 = 1.0 - NextDouble(), u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
    public long NextLong() => (long)NextULong();
}

public sealed class PerlinNoise
{
    private readonly int[] _p = new int[512];
    private readonly double _ox, _oy, _oz;

    public PerlinNoise(Rng r)
    {
        _ox = r.NextDouble() * 256; _oy = r.NextDouble() * 256; _oz = r.NextDouble() * 256;
        for (int i = 0; i < 256; i++) _p[i] = i;
        for (int i = 0; i < 256; i++) { int j = r.Next(256 - i) + i; (_p[i], _p[j]) = (_p[j], _p[i]); _p[i + 256] = _p[i]; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Grad(int h, double x, double y, double z)
    {
        h &= 15;
        double u = h < 8 ? x : y, v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    public double Noise(double x, double y, double z)
    {
        x += _ox; y += _oy; z += _oz;
        int X = MathUtil.FloorToInt(x), Y = MathUtil.FloorToInt(y), Z = MathUtil.FloorToInt(z);
        x -= X; y -= Y; z -= Z; X &= 255; Y &= 255; Z &= 255;
        double u = Fade(x), v = Fade(y), w = Fade(z);
        int[] p = _p;
        int A = p[X] + Y, AA = p[A] + Z, AB = p[A + 1] + Z, B = p[X + 1] + Y, BA = p[B] + Z, BB = p[B + 1] + Z;
        return Lerp(w, Lerp(v, Lerp(u, Grad(p[AA], x, y, z), Grad(p[BA], x - 1, y, z)),
                               Lerp(u, Grad(p[AB], x, y - 1, z), Grad(p[BB], x - 1, y - 1, z))),
                       Lerp(v, Lerp(u, Grad(p[AA + 1], x, y, z - 1), Grad(p[BA + 1], x - 1, y, z - 1)),
                               Lerp(u, Grad(p[AB + 1], x, y - 1, z - 1), Grad(p[BB + 1], x - 1, y - 1, z - 1))));
    }
    public double Noise(double x, double z) => Noise(x, 0.0, z);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Lerp(double t, double a, double b) => a + t * (b - a);
}

public sealed class OctaveNoise
{
    private readonly PerlinNoise[] _o;
    public OctaveNoise(Rng r, int octaves) { _o = new PerlinNoise[octaves]; for (int i = 0; i < octaves; i++) _o[i] = new PerlinNoise(r); }
    /// <summary>Suma oktaw znormalizowana do ok. [-1,1]</summary>
    public double Sample(double x, double y, double z)
    {
        double sum = 0, amp = 1, freq = 1, norm = 0;
        for (int i = 0; i < _o.Length; i++) { sum += _o[i].Noise(x * freq, y * freq, z * freq) * amp; norm += amp; amp *= 0.5; freq *= 2; }
        return sum / norm;
    }
    public double Sample(double x, double z) => Sample(x, 0.5, z);
}

public struct AABB
{
    public double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
    public AABB(double x0, double y0, double z0, double x1, double y1, double z1) { MinX = x0; MinY = y0; MinZ = z0; MaxX = x1; MaxY = y1; MaxZ = z1; }
    public AABB Offset(double x, double y, double z) => new(MinX + x, MinY + y, MinZ + z, MaxX + x, MaxY + y, MaxZ + z);
    public AABB Expand(double x, double y, double z)
    {
        var b = this;
        if (x < 0) b.MinX += x; else b.MaxX += x;
        if (y < 0) b.MinY += y; else b.MaxY += y;
        if (z < 0) b.MinZ += z; else b.MaxZ += z;
        return b;
    }
    public AABB Grow(double d) => new(MinX - d, MinY - d, MinZ - d, MaxX + d, MaxY + d, MaxZ + d);
    public AABB Grow(double x, double y, double z) => new(MinX - x, MinY - y, MinZ - z, MaxX + x, MaxY + y, MaxZ + z);
    public bool Intersects(in AABB o) => o.MaxX > MinX && o.MinX < MaxX && o.MaxY > MinY && o.MinY < MaxY && o.MaxZ > MinZ && o.MinZ < MaxZ;
    public bool Contains(double x, double y, double z) => x >= MinX && x < MaxX && y >= MinY && y < MaxY && z >= MinZ && z < MaxZ;
    public double CenterX => (MinX + MaxX) * 0.5;
    public double CenterY => (MinY + MaxY) * 0.5;
    public double CenterZ => (MinZ + MaxZ) * 0.5;

    public double ClipX(in AABB o, double dx)
    {
        if (o.MaxY > MinY && o.MinY < MaxY && o.MaxZ > MinZ && o.MinZ < MaxZ)
        {
            if (dx > 0 && o.MaxX <= MinX) { double d = MinX - o.MaxX; if (d < dx) dx = d; }
            else if (dx < 0 && o.MinX >= MaxX) { double d = MaxX - o.MinX; if (d > dx) dx = d; }
        }
        return dx;
    }
    public double ClipY(in AABB o, double dy)
    {
        if (o.MaxX > MinX && o.MinX < MaxX && o.MaxZ > MinZ && o.MinZ < MaxZ)
        {
            if (dy > 0 && o.MaxY <= MinY) { double d = MinY - o.MaxY; if (d < dy) dy = d; }
            else if (dy < 0 && o.MinY >= MaxY) { double d = MaxY - o.MinY; if (d > dy) dy = d; }
        }
        return dy;
    }
    public double ClipZ(in AABB o, double dz)
    {
        if (o.MaxX > MinX && o.MinX < MaxX && o.MaxY > MinY && o.MinY < MaxY)
        {
            if (dz > 0 && o.MaxZ <= MinZ) { double d = MinZ - o.MaxZ; if (d < dz) dz = d; }
            else if (dz < 0 && o.MinZ >= MaxZ) { double d = MaxZ - o.MinZ; if (d > dz) dz = d; }
        }
        return dz;
    }

    /// <summary>Przecięcie promienia z boxem; zwraca t i ścianę (0..5) lub false.</summary>
    public bool RayIntersect(double ox, double oy, double oz, double dx, double dy, double dz, out double tHit, out int face)
    {
        double tmin = double.NegativeInfinity, tmax = double.PositiveInfinity; face = -1; tHit = 0;
        int fmin = -1;
        if (!Slab(ox, dx, MinX, MaxX, ref tmin, ref tmax, ref fmin, 4, 5)) return false;
        if (!Slab(oy, dy, MinY, MaxY, ref tmin, ref tmax, ref fmin, 0, 1)) return false;
        if (!Slab(oz, dz, MinZ, MaxZ, ref tmin, ref tmax, ref fmin, 2, 3)) return false;
        if (tmax < 0 || tmin > tmax) return false;
        tHit = tmin < 0 ? 0 : tmin; face = fmin;
        return true;
    }
    // face ids: 0=down(-Y),1=up(+Y),2=north(-Z),3=south(+Z),4=west(-X),5=east(+X)
    private static bool Slab(double o, double d, double mn, double mx, ref double tmin, ref double tmax, ref int fmin, int negFace, int posFace)
    {
        if (Math.Abs(d) < 1e-12) return o >= mn && o <= mx;
        double t1 = (mn - o) / d, t2 = (mx - o) / d;
        int f1 = negFace;
        if (t1 > t2) { (t1, t2) = (t2, t1); f1 = posFace; }
        if (t1 > tmin) { tmin = t1; fmin = f1; }
        if (t2 < tmax) tmax = t2;
        return tmin <= tmax;
    }
}

/// <summary>Kierunki ścian: 0=dół,1=góra,2=północ(-Z),3=południe(+Z),4=zachód(-X),5=wschód(+X)</summary>
public static class Dir
{
    public const int Down = 0, Up = 1, North = 2, South = 3, West = 4, East = 5;
    public static readonly int[] X = { 0, 0, 0, 0, -1, 1 };
    public static readonly int[] Y = { -1, 1, 0, 0, 0, 0 };
    public static readonly int[] Z = { 0, 0, -1, 1, 0, 0 };
    public static readonly int[] Opp = { 1, 0, 3, 2, 5, 4 };
    // poziome: 0=S(+Z),1=W(-X),2=N(-Z),3=E(+X) (jak w MC)
    public static readonly int[] HorizX = { 0, -1, 0, 1 };
    public static readonly int[] HorizZ = { 1, 0, -1, 0 };
    public static int HorizToFace(int h) => h switch { 0 => South, 1 => West, 2 => North, _ => East };
    public static int FaceToHoriz(int f) => f switch { South => 0, West => 1, North => 2, East => 3, _ => 0 };
    public static int HorizFromYaw(float yaw) => MathUtil.FloorToInt(yaw * 4f / 360f + 0.5f) & 3;
}

public static class Log
{
    private static readonly object Lock = new();
    public static string FilePath = Path.Combine(AppContext.BaseDirectory, "blockcraft.log");
    public static void Info(string msg) => Write("INFO", msg);
    public static void Error(string msg) => Write("ERROR", msg);
    private static void Write(string lvl, string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] [{lvl}] {msg}";
        lock (Lock)
        {
            try { File.AppendAllText(FilePath, line + Environment.NewLine); } catch { }
            try { Console.WriteLine(line); } catch { }
        }
    }
}
