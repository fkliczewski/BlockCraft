using System.Collections.Concurrent;
using BlockCraft.Core;

namespace BlockCraft.World;

public enum WorldType { Default = 0, Superflat = 1 }

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

public sealed class WorldGenerator
{
    public const int SeaLevel = 62; // najwyższy blok wody
    public readonly long Seed;
    public readonly WorldType Type;
    private readonly OctaveNoise _terrain3D, _hills, _continental, _temp, _forest, _detail, _surface, _beach;
    private readonly ConcurrentDictionary<long, (byte[] top, byte[] topBlock, byte[] biome)> _protoCache = new();

    public WorldGenerator(long seed, WorldType type)
    {
        Seed = seed; Type = type;
        var r = new Rng(seed);
        _terrain3D = new OctaveNoise(r, 4);
        _hills = new OctaveNoise(r, 4);
        _continental = new OctaveNoise(r, 3);
        _temp = new OctaveNoise(r, 3);
        _forest = new OctaveNoise(r, 2);
        _detail = new OctaveNoise(r, 2);
        _surface = new OctaveNoise(r, 3);
        _beach = new OctaveNoise(r, 2);
    }

    public double Temperature(int x, int z) => _temp.Sample(x / 420.0, z / 420.0) * 1.6 + _detail.Sample(x / 40.0, 3.3, z / 40.0) * 0.04;
    public double DesertWeight(int x, int z) { double t = Temperature(x, z); return SmoothStep(0.12, 0.30, t); }
    public byte BiomeAt(int x, int z)
    {
        if (Type == WorldType.Superflat) return Biome.Plains;
        if (DesertWeight(x, z) > 0.5) return Biome.Desert;
        return _forest.Sample(x / 160.0, 7.7, z / 160.0) < -0.12 ? Biome.Plains : Biome.Forest;
    }
    private static double SmoothStep(double a, double b, double v) { double t = Math.Clamp((v - a) / (b - a), 0, 1); return t * t * (3 - 2 * t); }

    private void ColumnParams(int x, int z, out double height, out double noiseAmp)
    {
        double dw = DesertWeight(x, z);
        double c = _continental.Sample(x / 600.0, z / 600.0) * 1.5;
        double h = _hills.Sample(x / 200.0, z / 200.0) * 1.4;
        double baseH = 67 + c * 12;
        if (c < -0.35) baseH -= (-0.35 - c) * 30; // oceany / jeziora
        double fh = baseH + h * 10 + Math.Pow(Math.Max(0, h), 2) * 40;
        double dh = 65 + c * 4 + h * 5;
        if (c < -0.35) dh -= (-0.35 - c) * 24;
        height = Math.Clamp(fh * (1 - dw) + dh * dw, 30, 112);
        noiseAmp = 9 * (1 - dw) + 3.5 * dw + Math.Max(0, h) * 6 * (1 - dw);
    }

    private double[] ComputeDensity(int cx, int cz)
    {
        // siatka 5x17x5 (co 4 w poziomie, co 8 w pionie)
        var d = new double[5 * 17 * 5];
        for (int gx = 0; gx < 5; gx++)
            for (int gz = 0; gz < 5; gz++)
            {
                int wx = cx * 16 + gx * 4, wz = cz * 16 + gz * 4;
                ColumnParams(wx, wz, out double height, out double amp);
                for (int gy = 0; gy < 17; gy++)
                {
                    int wy = gy * 8;
                    double n = _terrain3D.Sample(wx / 56.0, wy / 40.0, wz / 56.0);
                    double val = (height - wy) + n * amp * 2.2;
                    if (wy <= 4) val += 50;
                    if (wy >= 124) val -= 50;
                    d[(gx * 5 + gz) * 17 + gy] = val;
                }
            }
        return d;
    }

    private void FillTerrain(int cx, int cz, byte[] blocks)
    {
        var d = ComputeDensity(cx, cz);
        for (int gx = 0; gx < 4; gx++)
            for (int gz = 0; gz < 4; gz++)
                for (int gy = 0; gy < 16; gy++)
                {
                    double d000 = d[(gx * 5 + gz) * 17 + gy], d001 = d[(gx * 5 + gz + 1) * 17 + gy];
                    double d100 = d[((gx + 1) * 5 + gz) * 17 + gy], d101 = d[((gx + 1) * 5 + gz + 1) * 17 + gy];
                    double d010 = d[(gx * 5 + gz) * 17 + gy + 1], d011 = d[(gx * 5 + gz + 1) * 17 + gy + 1];
                    double d110 = d[((gx + 1) * 5 + gz) * 17 + gy + 1], d111 = d[((gx + 1) * 5 + gz + 1) * 17 + gy + 1];
                    for (int ly = 0; ly < 8; ly++)
                    {
                        double ty = ly / 8.0;
                        double a00 = d000 + (d010 - d000) * ty, a01 = d001 + (d011 - d001) * ty;
                        double a10 = d100 + (d110 - d100) * ty, a11 = d101 + (d111 - d101) * ty;
                        int y = gy * 8 + ly;
                        for (int lx = 0; lx < 4; lx++)
                        {
                            double tx = lx / 4.0;
                            double b0 = a00 + (a10 - a00) * tx, b1 = a01 + (a11 - a01) * tx;
                            for (int lz = 0; lz < 4; lz++)
                            {
                                double v = b0 + (b1 - b0) * (lz / 4.0);
                                int x = gx * 4 + lx, z = gz * 4 + lz;
                                byte id = v > 0 ? B.Stone : (y <= SeaLevel ? B.Water : B.Air);
                                blocks[Chunk.Index(x, y, z)] = id;
                            }
                        }
                    }
                }
    }

    private (byte[] top, byte[] topBlock, byte[] biome) Proto(int cx, int cz)
    {
        long key = MathUtil.ChunkKey(cx, cz);
        if (_protoCache.TryGetValue(key, out var p)) return p;
        var blocks = new byte[Chunk.Volume];
        FillTerrain(cx, cz, blocks);
        var top = new byte[256]; var tb = new byte[256]; var bio = new byte[256];
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                int y = Chunk.Height - 1;
                while (y > 0 && blocks[Chunk.Index(x, y, z)] == B.Air) y--;
                top[z * 16 + x] = (byte)y;
                tb[z * 16 + x] = blocks[Chunk.Index(x, y, z)];
                bio[z * 16 + x] = BiomeAt(cx * 16 + x, cz * 16 + z);
            }
        p = (top, tb, bio);
        if (_protoCache.Count > 4000) _protoCache.Clear();
        _protoCache[key] = p;
        return p;
    }

    public Chunk Generate(int cx, int cz)
    {
        var c = new Chunk(cx, cz);
        if (Type == WorldType.Superflat) { GenerateFlat(c); return c; }
        var blocks = c.Blocks;
        FillTerrain(cx, cz, blocks);
        var rng = new Rng(Seed ^ (cx * 341873128712L + cz * 132897987541L));
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                c.Biomes[z * 16 + x] = BiomeAt(cx * 16 + x, cz * 16 + z);

        // powierzchnia
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                int wx = cx * 16 + x, wz = cz * 16 + z;
                byte bio = c.Biomes[z * 16 + x];
                double sn = _surface.Sample(wx / 16.0, 0.3, wz / 16.0);
                int depthMax = (int)(3 + sn * 2 + rng.NextDouble());
                bool beach = _beach.Sample(wx / 32.0, 1.7, wz / 32.0) > -0.1;
                bool gravelBeach = _beach.Sample(wx / 32.0, 9.1, wz / 32.0) > 0.45;
                int depth = -1;
                byte top = bio == Biome.Desert ? B.Sand : B.Grass, filler = bio == Biome.Desert ? B.Sand : B.Dirt;
                for (int y = Chunk.Height - 1; y >= 0; y--)
                {
                    int i = Chunk.Index(x, y, z);
                    byte id = blocks[i];
                    if (id == B.Air) { depth = -1; continue; }
                    if (id == B.Water) { depth = -1; continue; }
                    if (id != B.Stone) continue;
                    if (depth == -1)
                    {
                        // pierwszy blok stały od góry
                        byte t = top, f = filler;
                        if (depthMax <= 0) { t = B.Air; f = B.Stone; }
                        if (y >= SeaLevel - 3 && y <= SeaLevel + 1 && bio != Biome.Desert)
                        {
                            if (gravelBeach) { t = B.Gravel; f = B.Gravel; }
                            else if (beach) { t = B.Sand; f = B.Sand; }
                        }
                        if (y < SeaLevel && t == B.Grass) { t = B.Dirt; }
                        if (y < SeaLevel - 1 && bio != Biome.Desert && (sn > 0.25)) { t = B.Gravel; f = B.Dirt; }
                        depth = depthMax;
                        blocks[i] = y >= SeaLevel ? (t == B.Air ? B.Stone : t) : (t == B.Air ? B.Stone : t);
                        if (blocks[i] == B.Grass && y + 1 < Chunk.Height && blocks[Chunk.Index(x, y + 1, z)] == B.Water) blocks[i] = B.Dirt;
                        filler = f;
                    }
                    else if (depth > 0)
                    {
                        depth--;
                        blocks[i] = filler;
                        if (depth == 0 && filler == B.Sand) { depth = rng.Next(4) + 2; filler = B.Sandstone; }
                    }
                }
                // skała macierzysta
                blocks[Chunk.Index(x, 0, z)] = B.Bedrock;
                for (int y = 1; y < 5; y++) if (rng.Next(5) >= y) blocks[Chunk.Index(x, y, z)] = B.Bedrock;
            }

        CarveCaves(c);
        GenerateOres(c);
        Decorate(c, rng);
        c.RecountSections();
        c.RecalcHeightMap();
        return c;
    }

    private void GenerateFlat(Chunk c)
    {
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                c.Blocks[Chunk.Index(x, 0, z)] = B.Bedrock;
                c.Blocks[Chunk.Index(x, 1, z)] = B.Dirt;
                c.Blocks[Chunk.Index(x, 2, z)] = B.Dirt;
                c.Blocks[Chunk.Index(x, 3, z)] = B.Grass;
                c.Biomes[z * 16 + x] = Biome.Plains;
            }
        c.RecountSections();
        c.RecalcHeightMap();
    }

    // ---------------- Jaskinie (robaki jak MapGenCaves) ----------------
    private void CarveCaves(Chunk c)
    {
        const int range = 8;
        for (int dx = -range; dx <= range; dx++)
            for (int dz = -range; dz <= range; dz++)
            {
                int sx = c.Cx + dx, sz = c.Cz + dz;
                var r = new Rng(Seed * 31 + sx * 341873128712L + sz * 132897987541L + 77);
                int count = r.Next(r.Next(r.Next(15) + 1) + 1);
                if (r.Next(7) != 0) count = 0;
                for (int i = 0; i < count; i++)
                {
                    double x = sx * 16 + r.Next(16), y = r.Next(r.Next(110) + 8), z = sz * 16 + r.Next(16);
                    int tunnels = 1;
                    if (r.Next(4) == 0)
                    {
                        Tunnel(c, r.NextLong(), x, y, z, 1 + r.NextFloat() * 6, 0, 0, -1, -1, 0.5);
                        tunnels += r.Next(4);
                    }
                    for (int t = 0; t < tunnels; t++)
                    {
                        float yaw = r.NextFloat() * MathF.PI * 2, pitch = (r.NextFloat() - 0.5f) * 2f / 8f;
                        float width = r.NextFloat() * 2 + r.NextFloat();
                        if (r.Next(10) == 0) width *= r.NextFloat() * r.NextFloat() * 3 + 1;
                        Tunnel(c, r.NextLong(), x, y, z, width, yaw, pitch, 0, 0, 1.0);
                    }
                }
            }
    }

    private void Tunnel(Chunk c, long seed, double x, double y, double z, float width, float yaw, float pitch, int step, int maxSteps, double yScale)
    {
        double cxw = c.Cx * 16 + 8, czw = c.Cz * 16 + 8;
        float yawV = 0, pitchV = 0;
        var r = new Rng(seed);
        if (maxSteps <= 0) { int rr = 8 * 16 - 16; maxSteps = rr - r.Next(rr / 4); }
        bool room = false;
        if (step == -1) { step = maxSteps / 2; room = true; }
        int branch = r.Next(Math.Max(1, maxSteps / 2)) + maxSteps / 4;
        bool steep = r.Next(6) == 0;
        for (; step < maxSteps; step++)
        {
            double radH = 1.5 + Math.Sin(step * Math.PI / maxSteps) * width;
            double radV = radH * yScale;
            float cp = MathF.Cos(pitch), sp = MathF.Sin(pitch);
            x += MathF.Cos(yaw) * cp; y += sp; z += MathF.Sin(yaw) * cp;
            pitch *= steep ? 0.92f : 0.7f;
            pitch += pitchV * 0.1f; yaw += yawV * 0.1f;
            pitchV *= 0.9f; yawV *= 0.75f;
            pitchV += (r.NextFloat() - r.NextFloat()) * r.NextFloat() * 2f;
            yawV += (r.NextFloat() - r.NextFloat()) * r.NextFloat() * 4f;
            if (!room && step == branch && width > 1f && maxSteps > 0)
            {
                Tunnel(c, r.NextLong(), x, y, z, r.NextFloat() * 0.5f + 0.5f, yaw - MathF.PI / 2, pitch / 3f, step, maxSteps, 1.0);
                Tunnel(c, r.NextLong(), x, y, z, r.NextFloat() * 0.5f + 0.5f, yaw + MathF.PI / 2, pitch / 3f, step, maxSteps, 1.0);
                return;
            }
            if (!room && r.Next(4) == 0) continue;
            double ddx = x - cxw, ddz = z - czw, rem = maxSteps - step, maxR = width + 2 + 16;
            if (ddx * ddx + ddz * ddz - rem * rem > maxR * maxR) return;
            if (x < cxw - 16 - radH * 2 || z < czw - 16 - radH * 2 || x > cxw + 16 + radH * 2 || z > czw + 16 + radH * 2) continue;
            Carve(c, x, y, z, radH, radV);
            if (room) break;
        }
    }

    private void Carve(Chunk c, double x, double y, double z, double rh, double rv)
    {
        int bx0 = c.Cx * 16, bz0 = c.Cz * 16;
        int x0 = Math.Max(0, MathUtil.FloorToInt(x - rh) - bx0 - 1), x1 = Math.Min(16, MathUtil.FloorToInt(x + rh) - bx0 + 1);
        int z0 = Math.Max(0, MathUtil.FloorToInt(z - rh) - bz0 - 1), z1 = Math.Min(16, MathUtil.FloorToInt(z + rh) - bz0 + 1);
        int y0 = Math.Max(1, MathUtil.FloorToInt(y - rv) - 1), y1 = Math.Min(Chunk.Height - 8, MathUtil.FloorToInt(y + rv) + 1);
        if (x0 >= x1 || z0 >= z1 || y0 >= y1) return;
        // brak wody w pobliżu
        for (int lx = x0; lx < x1; lx++)
            for (int lz = z0; lz < z1; lz++)
                for (int ly = y1 + 1; ly >= y0 - 1; ly--)
                {
                    if (ly < 0 || ly >= Chunk.Height) continue;
                    byte id = c.Blocks[Chunk.Index(lx, ly, lz)];
                    if (id == B.Water) return;
                    if (ly != y0 - 1 && lx != x0 && lx != x1 - 1 && lz != z0 && lz != z1 - 1) ly = y0;
                }
        for (int lx = x0; lx < x1; lx++)
        {
            double dx = (lx + bx0 + 0.5 - x) / rh;
            for (int lz = z0; lz < z1; lz++)
            {
                double dz = (lz + bz0 + 0.5 - z) / rh;
                if (dx * dx + dz * dz >= 1) continue;
                bool hitGrass = false;
                for (int ly = y1 - 1; ly >= y0; ly--)
                {
                    double dy = (ly + 0.5 - y) / rv;
                    if (dy > -0.7 && dx * dx + dy * dy + dz * dz < 1)
                    {
                        int i = Chunk.Index(lx, ly, lz);
                        byte id = c.Blocks[i];
                        if (id == B.Grass) hitGrass = true;
                        if (id == B.Stone || id == B.Dirt || id == B.Grass || id == B.Sand || id == B.Sandstone || id == B.Gravel)
                        {
                            if (id == B.Sand && ly + 1 < Chunk.Height && c.Blocks[Chunk.Index(lx, ly + 1, lz)] == B.Water) continue;
                            c.Blocks[i] = ly < 11 ? B.Lava : B.Air;
                            if (hitGrass && ly > 0 && c.Blocks[Chunk.Index(lx, ly - 1, lz)] == B.Dirt) c.Blocks[Chunk.Index(lx, ly - 1, lz)] = B.Grass;
                        }
                    }
                }
            }
        }
    }

    // ---------------- Rudy ----------------
    private void GenerateOres(Chunk c)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int sx = c.Cx + dx, sz = c.Cz + dz;
                var r = new Rng(Seed + sx * 1234567L + sz * 7654321L + 999);
                Veins(c, r, sx, sz, B.Dirt, 32, 8, 0, 128);
                Veins(c, r, sx, sz, B.Gravel, 32, 6, 0, 128);
                Veins(c, r, sx, sz, B.CoalOre, 17, 20, 0, 128);
                Veins(c, r, sx, sz, B.IronOre, 9, 20, 0, 64);
                Veins(c, r, sx, sz, B.GoldOre, 9, 2, 0, 32);
                Veins(c, r, sx, sz, B.RedstoneOre, 8, 8, 0, 16);
                Veins(c, r, sx, sz, B.DiamondOre, 8, 1, 0, 16);
                // lazuryt – rozkład trójkątny wokół y=16
                for (int i = 0; i < 1; i++)
                {
                    int x = sx * 16 + r.Next(16), z = sz * 16 + r.Next(16), y = r.Next(16) + r.Next(16);
                    Vein(c, r, x, y, z, B.LapisOre, 7);
                }
            }
    }

    private void Veins(Chunk c, Rng r, int sx, int sz, byte ore, int size, int count, int minY, int maxY)
    {
        for (int i = 0; i < count; i++)
        {
            int x = sx * 16 + r.Next(16), y = r.Next(maxY - minY) + minY, z = sz * 16 + r.Next(16);
            Vein(c, r, x, y, z, ore, size);
        }
    }

    private void Vein(Chunk c, Rng r, int px, int py, int pz, byte ore, int n)
    {
        float f = r.NextFloat() * MathF.PI;
        double d0 = px + MathF.Sin(f) * n / 8f, d1 = px - MathF.Sin(f) * n / 8f;
        double d2 = pz + MathF.Cos(f) * n / 8f, d3 = pz - MathF.Cos(f) * n / 8f;
        double d4 = py + r.Next(3) - 2, d5 = py + r.Next(3) - 2;
        int bx0 = c.Cx * 16, bz0 = c.Cz * 16;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            double cx = d0 + (d1 - d0) * t, cy = d4 + (d5 - d4) * t, cz = d2 + (d3 - d2) * t;
            double d9 = r.NextDouble() * n / 16.0;
            double rad = ((MathF.Sin(MathF.PI * t) + 1f) * d9 + 1.0) / 2.0;
            int x0 = MathUtil.FloorToInt(cx - rad), x1 = MathUtil.FloorToInt(cx + rad);
            int y0 = MathUtil.FloorToInt(cy - rad), y1 = MathUtil.FloorToInt(cy + rad);
            int z0 = MathUtil.FloorToInt(cz - rad), z1 = MathUtil.FloorToInt(cz + rad);
            if (x1 < bx0 || x0 > bx0 + 15 || z1 < bz0 || z0 > bz0 + 15) continue;
            for (int x = x0; x <= x1; x++)
            {
                double ex = (x + 0.5 - cx) / rad; if (ex * ex >= 1) continue;
                if (x < bx0 || x > bx0 + 15) continue;
                for (int y = y0; y <= y1; y++)
                {
                    double ey = (y + 0.5 - cy) / rad; if (ex * ex + ey * ey >= 1) continue;
                    if (y < 1 || y >= Chunk.Height) continue;
                    for (int z = z0; z <= z1; z++)
                    {
                        double ez = (z + 0.5 - cz) / rad;
                        if (ex * ex + ey * ey + ez * ez >= 1) continue;
                        if (z < bz0 || z > bz0 + 15) continue;
                        int idx = Chunk.Index(x - bx0, y, z - bz0);
                        if (c.Blocks[idx] == B.Stone) c.Blocks[idx] = ore;
                    }
                }
            }
        }
    }

    // ---------------- Dekoracje ----------------
    private sealed class ChunkWriter : ITreeWriter
    {
        private readonly Chunk _c;
        public ChunkWriter(Chunk c) { _c = c; }
        public int Get(int x, int y, int z)
        {
            int lx = x - _c.Cx * 16, lz = z - _c.Cz * 16;
            if (y < 0 || y >= Chunk.Height) return B.Stone;
            if (lx < 0 || lx > 15 || lz < 0 || lz > 15) return OutsideGround(x, y, z);
            return _c.Blocks[Chunk.Index(lx, y, lz)];
        }
        public Func<int, int, int, int> OutsideGround;
        public void Set(int x, int y, int z, byte id, int meta)
        {
            int lx = x - _c.Cx * 16, lz = z - _c.Cz * 16;
            if (lx < 0 || lx > 15 || lz < 0 || lz > 15 || y < 0 || y >= Chunk.Height) return;
            int i = Chunk.Index(lx, y, lz);
            byte old = _c.Blocks[i];
            if (id == B.OakLeaves || id == B.BirchLeaves) { if (old != B.Air && old != B.TallGrass && old != B.Dandelion && old != B.Poppy) return; }
            _c.Blocks[i] = id; _c.Meta[i] = (byte)meta;
        }
    }

    private void Decorate(Chunk c, Rng rng)
    {
        var writer = new ChunkWriter(c);
        // drzewa z sąsiednich chunków (deterministycznie)
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                int sx = c.Cx + dx, sz = c.Cz + dz;
                var proto = Proto(sx, sz);
                writer.OutsideGround = (x, y, z) =>
                {
                    // dla pni sąsiadów – baza drzewa: zakładamy trawę pod pniem
                    return B.Air;
                };
                var r = new Rng(Seed ^ (sx * 98765431L + sz * 1234577L) ^ 0x5DEECE66DL);
                int bx = sx * 16 + 8, bz = sz * 16 + 8;
                byte bio = BiomeAt(bx, bz);
                double fd = _forest.Sample(bx / 160.0, 7.7, bz / 160.0);
                int trees;
                if (bio == Biome.Desert) trees = 0;
                else if (bio == Biome.Plains) trees = r.Next(10) == 0 ? 1 : 0;
                else trees = (int)(3 + Math.Clamp(fd + 0.12, 0, 1) * 9) + r.Next(3) - 1;
                for (int t = 0; t < trees; t++)
                {
                    int lx = r.Next(16), lz = r.Next(16);
                    int ty = proto.top[lz * 16 + lx];
                    byte tb = proto.topBlock[lz * 16 + lx];
                    int type = r.Next(5) == 0 ? 1 : 0;
                    long treeSeed = r.NextLong();
                    if (proto.biome[lz * 16 + lx] == Biome.Desert || ty < SeaLevel) continue;
                    if (tb != B.Stone) continue;
                    int wx = sx * 16 + lx, wz = sz * 16 + lz;
                    // pomiń jeśli pień w sąsiednim chunku i za daleko
                    if (wx < c.Cx * 16 - 4 || wx > c.Cx * 16 + 19 || wz < c.Cz * 16 - 4 || wz > c.Cz * 16 + 19) continue;
                    var tr = new Rng(treeSeed);
                    // grunt: w naszym chunku sprawdzamy realnie
                    bool own = dx == 0 && dz == 0;
                    if (own)
                    {
                        int g = c.Blocks[Chunk.Index(lx, ty, lz)];
                        if (g != B.Grass && g != B.Dirt) continue;
                        if (c.Blocks[Chunk.Index(lx, ty + 1, lz)] != B.Air) continue;
                    }
                    var gw = new GroundWriter(writer, wx, ty, wz);
                    TreeGen.Generate(gw, wx, ty + 1, wz, type, tr, true);
                }
            }

        int cxw = c.Cx * 16, czw = c.Cz * 16;
        // trawa, kwiaty, kaktusy, krzaki, trzcina
        for (int i = 0; i < 64; i++)
        {
            int lx = rng.Next(16), lz = rng.Next(16);
            byte bio = c.Biomes[lz * 16 + lx];
            int y = c.TopSolidY(lx, lz);
            if (y <= 0 || y >= Chunk.Height - 4) continue;
            int ground = c.Blocks[Chunk.Index(lx, y, lz)];
            int above = c.Blocks[Chunk.Index(lx, y + 1, lz)];
            if (above != B.Air) continue;
            if (bio != Biome.Desert && ground == B.Grass)
            {
                int roll = rng.Next(100);
                int grassChance = bio == Biome.Plains ? 70 : 55;
                if (roll < grassChance) c.Blocks[Chunk.Index(lx, y + 1, lz)] = B.TallGrass;
            }
        }
        // płaty kwiatów
        int patches = rng.Next(3);
        for (int p = 0; p < patches; p++)
        {
            int px = rng.Next(16), pz = rng.Next(16);
            byte flower = rng.Next(3) == 0 ? B.Dandelion : B.Poppy;
            for (int i = 0; i < 12; i++)
            {
                int lx = px + rng.Next(5) - 2, lz = pz + rng.Next(5) - 2;
                if (lx < 0 || lx > 15 || lz < 0 || lz > 15) continue;
                int y = c.TopSolidY(lx, lz);
                if (y <= 0 || y >= Chunk.Height - 2) continue;
                if (c.Blocks[Chunk.Index(lx, y, lz)] == B.Grass && c.Blocks[Chunk.Index(lx, y + 1, lz)] is B.Air or B.TallGrass)
                    c.Blocks[Chunk.Index(lx, y + 1, lz)] = flower;
            }
        }
        // pustynia
        for (int i = 0; i < 10; i++)
        {
            int lx = rng.Next(16), lz = rng.Next(16);
            if (c.Biomes[lz * 16 + lx] != Biome.Desert) continue;
            int y = c.TopSolidY(lx, lz);
            if (y <= SeaLevel - 1 || y >= Chunk.Height - 4) continue;
            if (c.Blocks[Chunk.Index(lx, y, lz)] != B.Sand || c.Blocks[Chunk.Index(lx, y + 1, lz)] != B.Air) continue;
            if (i < 3 && lx > 0 && lx < 15 && lz > 0 && lz < 15)
            {
                bool free = true;
                for (int h = 0; h < 4; h++) if (c.Blocks[Chunk.Index(lx + Dir.HorizX[h], y + 1, lz + Dir.HorizZ[h])] != B.Air) free = false;
                if (free)
                {
                    int hgt = 1 + rng.Next(rng.Next(3) + 1);
                    for (int k = 1; k <= hgt; k++) c.Blocks[Chunk.Index(lx, y + k, lz)] = B.Cactus;
                }
            }
            else if (i < 6) c.Blocks[Chunk.Index(lx, y + 1, lz)] = B.DeadBush;
        }
        // trzcina przy wodzie
        for (int i = 0; i < 20; i++)
        {
            int lx = rng.Next(14) + 1, lz = rng.Next(14) + 1;
            int y = c.TopSolidY(lx, lz);
            if (y != SeaLevel) continue;
            int g = c.Blocks[Chunk.Index(lx, y, lz)];
            if (g != B.Grass && g != B.Sand && g != B.Dirt) continue;
            if (c.Blocks[Chunk.Index(lx, y + 1, lz)] != B.Air) continue;
            bool water = false;
            for (int h = 0; h < 4; h++) if (c.Blocks[Chunk.Index(lx + Dir.HorizX[h], y, lz + Dir.HorizZ[h])] == B.Water) water = true;
            if (!water) continue;
            int hgt = 2 + rng.Next(rng.Next(3) + 1);
            for (int k = 1; k <= hgt && y + k < Chunk.Height; k++) c.Blocks[Chunk.Index(lx, y + k, lz)] = B.SugarCane;
        }
        // glina pod wodą
        if (rng.Next(3) == 0)
        {
            int lx = rng.Next(16), lz = rng.Next(16);
            int y = c.TopSolidY(lx, lz);
            if (y > 0 && y < SeaLevel && c.Blocks[Chunk.Index(lx, y + 1, lz)] == B.Water)
            {
                int rad = 1 + rng.Next(2);
                for (int dx = -rad; dx <= rad; dx++) for (int dz = -rad; dz <= rad; dz++) for (int dy = -1; dy <= 0; dy++)
                        {
                            int x = lx + dx, z = lz + dz;
                            if (x < 0 || x > 15 || z < 0 || z > 15 || dx * dx + dz * dz > rad * rad) continue;
                            int idx = Chunk.Index(x, y + dy, z);
                            if (c.Blocks[idx] is B.Dirt or B.Sand or B.Gravel) c.Blocks[idx] = B.Clay;
                        }
            }
        }
    }

    /// <summary>Writer który traktuje (x,y-1,z) pnia jako trawę (dla drzew z sąsiednich chunków).</summary>
    private sealed class GroundWriter : ITreeWriter
    {
        private readonly ITreeWriter _w; private readonly int _x, _y, _z;
        public GroundWriter(ITreeWriter w, int x, int y, int z) { _w = w; _x = x; _y = y; _z = z; }
        public int Get(int x, int y, int z) => x == _x && y == _y && z == _z ? B.Grass : _w.Get(x, y, z);
        public void Set(int x, int y, int z, byte id, int meta) => _w.Set(x, y, z, id, meta);
    }

    /// <summary>Znajdź dobry punkt spawnu w pobliżu (0,0).</summary>
    public (int x, int y, int z) FindSpawn()
    {
        if (Type == WorldType.Superflat) return (0, 4, 0);
        var r = new Rng(Seed);
        for (int attempt = 0; attempt < 2000; attempt++)
        {
            int x = attempt == 0 ? 0 : r.Next(attempt * 2 + 1) - attempt, z = attempt == 0 ? 0 : r.Next(attempt * 2 + 1) - attempt;
            int cx = x >> 4, cz = z >> 4;
            var p = Proto(cx, cz);
            int lx = x & 15, lz = z & 15;
            int y = p.top[lz * 16 + lx];
            if (y >= SeaLevel && p.topBlock[lz * 16 + lx] == B.Stone) return (x, y + 1, z);
        }
        return (0, 100, 0);
    }
}
