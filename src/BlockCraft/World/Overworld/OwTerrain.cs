using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>Surowy teren chunka przed dekoracją (ChunkPrimer z MC): bloki, meta i biomy w indeksach Chunk.Index.</summary>
public sealed class ProtoChunk
{
    public readonly byte[] Blocks = new byte[Chunk.Volume];
    public readonly byte[] Meta = new byte[Chunk.Volume];
    public readonly byte[] Biomes = new byte[256];
    public readonly Dictionary<int, OwTilePlan> Tiles = new();
    public readonly List<OwEntityPlan> Entities = new();
    public byte Get(int x, int y, int z) => Blocks[Chunk.Index(x, y, z)];
    public void Set(int x, int y, int z, byte id, int meta = 0) { int i = Chunk.Index(x, y, z); Blocks[i] = id; Meta[i] = (byte)meta; }
}

/// <summary>
/// Teren jak ChunkProviderGenerate w MC 1.8 z domyślnymi ChunkProviderSettings: szum 3D 5x33x5 z wysokościami biomów
/// wygładzonymi polem parabolicznym, interpolacja trójliniowa, potem bloki powierzchni biomów (genTerrainBlocks).
/// </summary>
public sealed class OwTerrain
{
    // ChunkProviderSettings.Factory (wartości domyślne)
    private const double CoordinateScale = 684.412, HeightScale = 684.412, UpperLimitScale = 512, LowerLimitScale = 512,
        DepthNoiseScaleX = 200, DepthNoiseScaleZ = 200, DepthNoiseScaleExponent = 0.5, MainNoiseScaleX = 80, MainNoiseScaleY = 160,
        MainNoiseScaleZ = 80, BaseSize = 8.5, StretchY = 12;
    private const float BiomeDepthWeight = 1, BiomeDepthOffset = 0, BiomeScaleWeight = 1, BiomeScaleOffset = 0;
    public const int SeaLevel = 63;

    private readonly long _seed;
    private readonly bool _amplified;
    public readonly OwBiomeProvider Biomes;
    private readonly OctavesNoise _minLimit, _maxLimit, _main, _depth;
    private readonly SimplexOctaves _stone;
    private readonly float[] _parabolic = new float[25];
    private readonly MesaBands _mesa;

    public OwTerrain(long seed, bool amplified, bool largeBiomes)
    {
        _seed = seed; _amplified = amplified;
        Biomes = new OwBiomeProvider(seed, largeBiomes);
        var r = new JRandom(seed);
        _minLimit = new OctavesNoise(r, 16);
        _maxLimit = new OctavesNoise(r, 16);
        _main = new OctavesNoise(r, 8);
        _stone = new SimplexOctaves(r, 4);
        _ = new OctavesNoise(r, 10); // noiseGen5 (nieużywany, ale zużywa losowanie)
        _depth = new OctavesNoise(r, 16);
        for (int i = -2; i <= 2; i++)
            for (int j = -2; j <= 2; j++)
                _parabolic[i + 2 + (j + 2) * 5] = 10f / MathF.Sqrt(i * i + j * j + 0.2f);
        _mesa = new MesaBands(seed);
    }

    /// <summary>provideChunk bez struktur: teren, powierzchnia. Jaskinie i wąwozy dokłada generator.</summary>
    public ProtoChunk Generate(int cx, int cz)
    {
        var p = new ProtoChunk();
        var rand = new JRandom(cx * 341873128712L + cz * 132897987541L);
        SetBlocksInChunk(cx, cz, p);
        var bio = Biomes.BlockBiomes(cx * 16, cz * 16, 16, 16);
        for (int i = 0; i < 256; i++) p.Biomes[((i >> 4) << 4) | (i & 15)] = (byte)(Biome.Exists(bio[i]) ? bio[i] : Biome.Ocean);
        ReplaceBlocksForBiome(cx, cz, p, rand);
        return p;
    }

    // ---------------- szum terenu ----------------
    private void SetBlocksInChunk(int cx, int cz, ProtoChunk p)
    {
        var gen = Biomes.GenBiomes(cx * 4 - 2, cz * 4 - 2, 10, 10);
        var d = Density(cx * 4, 0, cz * 4, gen);
        for (int i = 0; i < 4; i++)
        {
            int j = i * 5, k = (i + 1) * 5;
            for (int l = 0; l < 4; l++)
            {
                int i1 = (j + l) * 33, j1 = (j + l + 1) * 33, k1 = (k + l) * 33, l1 = (k + l + 1) * 33;
                for (int i2 = 0; i2 < 32; i2++)
                {
                    const double d0 = 0.125;
                    double d1 = d[i1 + i2], d2 = d[j1 + i2], d3 = d[k1 + i2], d4 = d[l1 + i2];
                    double d5 = (d[i1 + i2 + 1] - d1) * d0, d6 = (d[j1 + i2 + 1] - d2) * d0, d7 = (d[k1 + i2 + 1] - d3) * d0, d8 = (d[l1 + i2 + 1] - d4) * d0;
                    for (int j2 = 0; j2 < 8; j2++)
                    {
                        const double d9 = 0.25;
                        double d10 = d1, d11 = d2, d12 = (d3 - d1) * d9, d13 = (d4 - d2) * d9;
                        int y = i2 * 8 + j2;
                        for (int k2 = 0; k2 < 4; k2++)
                        {
                            const double d14 = 0.25;
                            double d16 = (d11 - d10) * d14, v = d10 - d16;
                            for (int l2 = 0; l2 < 4; l2++)
                            {
                                if ((v += d16) > 0.0) p.Set(i * 4 + k2, y, l * 4 + l2, B.Stone);
                                else if (y < SeaLevel) p.Set(i * 4 + k2, y, l * 4 + l2, B.Water);
                            }
                            d10 += d12; d11 += d13;
                        }
                        d1 += d5; d2 += d6; d3 += d7; d4 += d8;
                    }
                }
            }
        }
    }

    /// <summary>func_147423_a: gęstość 5x33x5 (indeks (x*5+z)*33+y).</summary>
    private double[] Density(int x, int y, int z, int[] gen)
    {
        var depth = _depth.Generate(null, x, z, 5, 5, DepthNoiseScaleX, DepthNoiseScaleZ);
        var main = _main.Generate(null, x, y, z, 5, 33, 5, CoordinateScale / MainNoiseScaleX, HeightScale / MainNoiseScaleY, CoordinateScale / MainNoiseScaleZ);
        var min = _minLimit.Generate(null, x, y, z, 5, 33, 5, CoordinateScale, HeightScale, CoordinateScale);
        var max = _maxLimit.Generate(null, x, y, z, 5, 33, 5, CoordinateScale, HeightScale, CoordinateScale);
        var o = new double[825];
        int i = 0, j = 0;
        for (int k = 0; k < 5; k++)
            for (int l = 0; l < 5; l++)
            {
                float f2 = 0, f3 = 0, f4 = 0;
                var center = Biome.Get(gen[k + 2 + (l + 2) * 10]);
                for (int j1 = -2; j1 <= 2; j1++)
                    for (int k1 = -2; k1 <= 2; k1++)
                    {
                        var b1 = Biome.Get(gen[k + j1 + 2 + (l + k1 + 2) * 10]);
                        float f5 = BiomeDepthOffset + b1.MinHeight * BiomeDepthWeight;
                        float f6 = BiomeScaleOffset + b1.MaxHeight * BiomeScaleWeight;
                        if (_amplified && f5 > 0f) { f5 = 1f + f5 * 2f; f6 = 1f + f6 * 4f; }
                        float f7 = _parabolic[j1 + 2 + (k1 + 2) * 5] / (f5 + 2f);
                        if (b1.MinHeight > center.MinHeight) f7 /= 2f;
                        f2 += f6 * f7; f3 += f5 * f7; f4 += f7;
                    }
                f2 /= f4; f3 /= f4;
                f2 = f2 * 0.9f + 0.1f;
                f3 = (f3 * 4f - 1f) / 8f;
                double d7 = depth[j] / 8000.0;
                if (d7 < 0) d7 = -d7 * 0.3;
                d7 = d7 * 3.0 - 2.0;
                if (d7 < 0)
                {
                    d7 /= 2.0;
                    if (d7 < -1.0) d7 = -1.0;
                    d7 /= 1.4; d7 /= 2.0;
                }
                else
                {
                    if (d7 > 1.0) d7 = 1.0;
                    d7 /= 8.0;
                }
                j++;
                double d8 = f3, d9 = f2;
                d8 += d7 * 0.2;
                d8 = d8 * BaseSize / 8.0;
                double d0 = BaseSize + d8 * 4.0;
                for (int l1 = 0; l1 < 33; l1++)
                {
                    double d1 = (l1 - d0) * StretchY * 128.0 / 256.0 / d9;
                    if (d1 < 0) d1 *= 4.0;
                    double d2 = min[i] / LowerLimitScale, d3 = max[i] / UpperLimitScale, d4 = (main[i] / 10.0 + 1.0) / 2.0;
                    double d5 = (d4 < 0 ? d2 : d4 > 1 ? d3 : d2 + (d3 - d2) * d4) - d1;
                    if (l1 > 29)
                    {
                        double d6 = (l1 - 29) / 3.0f;
                        d5 = d5 * (1.0 - d6) + -10.0 * d6;
                    }
                    o[i++] = d5;
                }
            }
        return o;
    }

    // ---------------- bloki powierzchni ----------------
    private void ReplaceBlocksForBiome(int cx, int cz, ProtoChunk p, JRandom rand)
    {
        var noise = _stone.Generate(null, cx * 16, cz * 16, 16, 16, 0.0625, 0.0625, 1.0);
        // jak w MC: pętla po (i, j) zapisuje kolumnę (x = j, z = i), a do biomu podaje współrzędne (X + i, Z + j)
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
            {
                int lx = j, lz = i;
                var b = Biome.Get(p.Biomes[(lz << 4) | lx]);
                GenTerrainBlocks(b, rand, p, lx, lz, cx * 16 + i, cz * 16 + j, noise[j + i * 16]);
            }
    }

    private void GenTerrainBlocks(BiomeDef b, JRandom rand, ProtoChunk p, int lx, int lz, int x, int z, double noise)
    {
        byte top = b.Top, filler = b.Filler; int topM = b.TopMeta, fillM = b.FillerMeta;
        switch (b.Kind)
        {
            case BiomeKind.Hills:
                top = B.Grass; filler = B.Dirt; topM = fillM = 0;
                if ((noise < -1.0 || noise > 2.0) && b.Variant == 2) { top = B.Gravel; filler = B.Gravel; }
                else if (noise > 1.0 && b.Variant != 1) { top = B.Stone; filler = B.Stone; }
                break;
            case BiomeKind.Taiga when b.Variant is 1 or 2:
                top = B.Grass; filler = B.Dirt; topM = fillM = 0;
                if (noise > 1.75) { top = B.Dirt; topM = 1; }
                else if (noise > -0.95) { top = B.Dirt; topM = 2; }
                break;
            case BiomeKind.Savanna when b.IsMutation:
                top = B.Grass; filler = B.Dirt; topM = fillM = 0;
                if (noise > 1.75) { top = B.Stone; filler = B.Stone; }
                else if (noise > -0.5) { top = B.Dirt; topM = 1; }
                break;
            case BiomeKind.Swamp:
                {
                    double d0 = Biome.GrassColorNoise(x * 0.25, z * 0.25);
                    if (d0 > 0.0)
                        for (int k = 255; k >= 0; k--)
                            if (p.Get(lx, k, lz) != B.Air)
                            {
                                if (k == 62 && p.Get(lx, k, lz) != B.Water)
                                {
                                    p.Set(lx, k, lz, B.Water);
                                    if (d0 < 0.12) p.Set(lx, k + 1, lz, B.LilyPad);
                                }
                                break;
                            }
                    break;
                }
            case BiomeKind.Mesa:
                MesaTerrain(b, rand, p, lx, lz, x, z, noise);
                return;
        }
        GenerateBiomeTerrain(b, rand, p, lx, lz, x, z, noise, top, topM, filler, fillM);
    }

    /// <summary>BiomeGenBase.generateBiomeTerrain.</summary>
    private static void GenerateBiomeTerrain(BiomeDef b, JRandom rand, ProtoChunk p, int lx, int lz, int x, int z, double noise,
        byte top, int topM, byte filler, int fillM)
    {
        int st = top, stM = topM; int fl = filler, flM = fillM; // st == -1 oznacza null
        int i = -1;
        int j = (int)(noise / 3.0 + 3.0 + rand.NextDouble() * 0.25);
        for (int y = 255; y >= 0; y--)
        {
            if (y <= rand.NextInt(5)) { p.Set(lx, y, lz, B.Bedrock); continue; }
            byte cur = p.Get(lx, y, lz);
            if (cur == B.Air) { i = -1; continue; }
            if (cur != B.Stone) continue;
            if (i == -1)
            {
                if (j <= 0) { st = -1; fl = B.Stone; flM = 0; }
                else if (y >= SeaLevel - 4 && y <= SeaLevel + 1) { st = top; stM = topM; fl = filler; flM = fillM; }
                if (y < SeaLevel && (st == -1 || st == B.Air))
                {
                    if (Biome.TemperatureAt(b.Id, x, y, z) < 0.15f) { st = B.Ice; stM = 0; } else { st = B.Water; stM = 0; }
                }
                i = j;
                if (y >= SeaLevel - 1) p.Set(lx, y, lz, (byte)(st < 0 ? B.Air : st), stM);
                else if (y < SeaLevel - 7 - j) { st = -1; fl = B.Stone; flM = 0; p.Set(lx, y, lz, B.Gravel); }
                else p.Set(lx, y, lz, (byte)fl, flM);
            }
            else if (i > 0)
            {
                i--;
                p.Set(lx, y, lz, (byte)fl, flM);
                if (i == 0 && fl == B.Sand)
                {
                    i = rand.NextInt(4) + Math.Max(0, y - SeaLevel);
                    fl = flM == 1 ? B.RedSandstone : B.Sandstone; flM = 0;
                }
            }
        }
    }

    /// <summary>BiomeGenMesa.genTerrainBlocks: pasy barwionej gliny, iglice w wariancie Bryce, trawa na zalesionym płaskowyżu.</summary>
    private void MesaTerrain(BiomeDef b, JRandom rand, ProtoChunk p, int lx, int lz, int x, int z, double noise)
    {
        bool bryce = (b.Variant & 1) != 0, forest = (b.Variant & 2) != 0;
        double d4 = 0.0;
        if (bryce)
        {
            int i = (x & -16) + (z & 15), j = (z & -16) + (x & 15);
            double d0 = Math.Min(Math.Abs(noise), _mesa.Pillar.Sample(i * 0.25, j * 0.25));
            if (d0 > 0.0)
            {
                const double d1 = 0.001953125;
                double d2 = Math.Abs(_mesa.PillarRoof.Sample(i * d1, j * d1));
                d4 = d0 * d0 * 2.5;
                double d3 = Math.Ceiling(d2 * 50.0) + 14.0;
                if (d4 > d3) d4 = d3;
                d4 += 64.0;
            }
        }
        int fl = B.StainedClay, flM = 0; bool stNull = false;
        int k = (int)(noise / 3.0 + 3.0 + rand.NextDouble() * 0.25);
        bool flag1 = Math.Cos(noise / 3.0 * Math.PI) > 0.0;
        int l = -1; bool flag2 = false;
        for (int y = 255; y >= 0; y--)
        {
            if (p.Get(lx, y, lz) == B.Air && y < (int)d4) p.Set(lx, y, lz, B.Stone);
            if (y <= rand.NextInt(5)) { p.Set(lx, y, lz, B.Bedrock); continue; }
            byte cur = p.Get(lx, y, lz);
            if (cur == B.Air) { l = -1; continue; }
            if (cur != B.Stone) continue;
            if (l == -1)
            {
                flag2 = false;
                if (k <= 0) { stNull = true; fl = B.Stone; flM = 0; }
                else if (y >= SeaLevel - 4 && y <= SeaLevel + 1) { stNull = false; fl = b.Filler; flM = 0; }
                l = k + Math.Max(0, y - SeaLevel);
                if (y >= SeaLevel - 1)
                {
                    if (forest && y > 86 + k * 2)
                    {
                        if (flag1) p.Set(lx, y, lz, B.Dirt, 1); else p.Set(lx, y, lz, B.Grass);
                    }
                    else if (y > 66 + k)
                    {
                        if (y >= 64 && y <= 127)
                        {
                            if (flag1) p.Set(lx, y, lz, B.HardenedClay);
                            else { var (id, m) = _mesa.Band(x, y); p.Set(lx, y, lz, id, m); }
                        }
                        else p.Set(lx, y, lz, B.StainedClay, Items.Dyes.Orange);
                    }
                    else { p.Set(lx, y, lz, b.Top, b.TopMeta); flag2 = true; }
                }
                else
                {
                    p.Set(lx, y, lz, (byte)fl, flM);
                    if (fl == B.StainedClay) p.Set(lx, y, lz, B.StainedClay, Items.Dyes.Orange);
                }
                _ = stNull;
            }
            else if (l > 0)
            {
                l--;
                if (flag2) p.Set(lx, y, lz, B.StainedClay, Items.Dyes.Orange);
                else { var (id, m) = _mesa.Band(x, y); p.Set(lx, y, lz, id, m); }
            }
        }
    }

    /// <summary>Kolumna terenu (do szukania spawnu): najwyższy blok nad poziomem morza bez dekoracji.</summary>
    public (int y, byte id) GroundAboveSeaLevel(ProtoChunk p, int lx, int lz)
    {
        int y = SeaLevel;
        while (y + 1 < Chunk.Height && p.Get(lx, y + 1, lz) != B.Air) y++;
        return (y, p.Get(lx, y, lz));
    }
}

/// <summary>Pasy gliny Mesy (BiomeGenMesa.func_150619_a) i szumy iglic Bryce.</summary>
public sealed class MesaBands
{
    private readonly (byte id, int meta)[] _bands = new (byte, int)[64];
    private readonly SimplexOctaves _bandNoise;
    public readonly SimplexOctaves Pillar, PillarRoof;

    public MesaBands(long seed)
    {
        var r2 = new JRandom(seed);
        Pillar = new SimplexOctaves(r2, 4);
        PillarRoof = new SimplexOctaves(r2, 1);
        Array.Fill(_bands, (B.HardenedClay, 0));
        var r = new JRandom(seed);
        _bandNoise = new SimplexOctaves(r, 1);
        for (int l1 = 0; l1 < 64; l1++)
        {
            l1 += r.NextInt(5) + 1;
            if (l1 < 64) _bands[l1] = (B.StainedClay, Items.Dyes.Orange);
        }
        void Stripe(int color, int minLen)
        {
            int n = r.NextInt(4) + 2;
            for (int i = 0; i < n; i++)
            {
                int len = r.NextInt(3) + minLen, start = r.NextInt(64);
                for (int j = 0; start + j < 64 && j < len; j++) _bands[start + j] = (B.StainedClay, color);
            }
        }
        Stripe(Items.Dyes.Yellow, 1);
        Stripe(Items.Dyes.Brown, 2);
        Stripe(Items.Dyes.Red, 1);
        int k3 = r.NextInt(3) + 3, j4 = 0;
        for (int l4 = 0; l4 < k3; l4++)
        {
            j4 += r.NextInt(16) + 4;
            for (int k1 = 0; j4 + k1 < 64 && k1 < 1; k1++)
            {
                _bands[j4 + k1] = (B.StainedClay, Items.Dyes.White);
                if (j4 + k1 > 1 && r.NextBool()) _bands[j4 + k1 - 1] = (B.StainedClay, Items.Dyes.LightGray);
                if (j4 + k1 < 63 && r.NextBool()) _bands[j4 + k1 + 1] = (B.StainedClay, Items.Dyes.LightGray);
            }
        }
    }

    /// <summary>func_180629_a (jak w MC szum bierze x dwa razy).</summary>
    public (byte id, int meta) Band(int x, int y)
    {
        int i = (int)Math.Floor(_bandNoise.Sample(x / 512.0, x / 512.0) * 2.0 + 0.5); // Math.round z Javy
        return _bands[(y + i + 64) % 64];
    }
}
