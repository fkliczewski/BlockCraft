using BlockCraft.Core;

namespace BlockCraft.Render;

/// <summary>Proceduralnie generowane tekstury 16x16 w stylu Minecraft (bloki, przedmioty, cząsteczki).</summary>
public static partial class TextureGen
{
    public const int S = 16;
    private static readonly object Lock = new();
    private static Dictionary<string, int> _index;
    private static List<uint[]> _tex;
    public static readonly Dictionary<string, List<uint[]>> Animations = new();
    private static readonly HashSet<string> Missing = new();

    public static IReadOnlyList<uint[]> Textures { get { Ensure(); return _tex; } }
    public static int Count { get { Ensure(); return _tex.Count; } }
    public static bool Has(string name) { Ensure(); return _index.ContainsKey(name); }

    public static int Layer(string name)
    {
        Ensure();
        if (_index.TryGetValue(name, out int i)) return i;
        lock (Missing) if (Missing.Add(name)) Log.Error("Brak tekstury: " + name);
        return 0;
    }

    public static uint[] Get(string name) { Ensure(); return _tex[Layer(name)]; }

    private static void Ensure()
    {
        if (_index != null) return;
        lock (Lock)
        {
            if (_index != null) return;
            _tex = new List<uint[]>(); var idx = new Dictionary<string, int>();
            _idxBuilding = idx;
            Add("missing", Missing16());
            GenerateBlocks();
            GenerateItems();
            GenerateParticles();
            ModuleTextures();
            _index = idx;
        }
    }
    private static Dictionary<string, int> _idxBuilding;

    private static void Add(string name, uint[] px)
    {
        _idxBuilding[name] = _tex.Count;
        _tex.Add(px);
    }

    // ---------- narzędzia kolorów ----------
    public static uint Rgba(int r, int g, int b, int a = 255)
        => (uint)(Math.Clamp(r, 0, 255) | (Math.Clamp(g, 0, 255) << 8) | (Math.Clamp(b, 0, 255) << 16) | (Math.Clamp(a, 0, 255) << 24));
    public static uint Hex(uint rgb, int a = 255) => Rgba((int)(rgb >> 16) & 255, (int)(rgb >> 8) & 255, (int)rgb & 255, a);
    public static int R(uint c) => (int)(c & 255);
    public static int G(uint c) => (int)((c >> 8) & 255);
    public static int Bl(uint c) => (int)((c >> 16) & 255);
    public static int A(uint c) => (int)(c >> 24);
    public static uint Mul(uint c, float f) => Rgba((int)(R(c) * f), (int)(G(c) * f), (int)(Bl(c) * f), A(c));
    public static uint WithA(uint c, int a) => (c & 0x00FFFFFF) | ((uint)a << 24);
    public static uint Lerp(uint a, uint b, float t) => Rgba((int)(R(a) + (R(b) - R(a)) * t), (int)(G(a) + (G(b) - G(a)) * t), (int)(Bl(a) + (Bl(b) - Bl(a)) * t), (int)(A(a) + (A(b) - A(a)) * t));
    public static uint Gray(float v, int a = 255) { int g = (int)(v * 255); return Rgba(g, g, g, a); }

    private static uint[] New(uint fill = 0) { var p = new uint[S * S]; if (fill != 0) Array.Fill(p, fill); return p; }
    private static uint[] Missing16()
    {
        var p = New();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) p[y * 16 + x] = ((x / 8 + y / 8) & 1) == 0 ? Hex(0xF800F8) : Hex(0x000000);
        return p;
    }

    /// <summary>Kafelkowalny szum wartości 16x16 (okres = liczba komórek).</summary>
    public static float[] TileNoise(int seed, int cells)
    {
        var r = new Rng(seed);
        var grid = new float[cells * cells];
        for (int i = 0; i < grid.Length; i++) grid[i] = r.NextFloat();
        var res = new float[256];
        float cs = 16f / cells;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float fx = x / cs, fy = y / cs;
                int x0 = (int)fx, y0 = (int)fy;
                float tx = fx - x0, ty = fy - y0;
                tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells; x0 %= cells; y0 %= cells;
                float a = grid[y0 * cells + x0], b = grid[y0 * cells + x1], c = grid[y1 * cells + x0], d = grid[y1 * cells + x1];
                res[y * 16 + x] = (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
            }
        return res;
    }

    public static float[] Fbm(int seed, float wRand = 0.35f)
    {
        var a = TileNoise(seed, 4); var b = TileNoise(seed + 1, 8); var r = new Rng(seed + 7);
        var res = new float[256];
        for (int i = 0; i < 256; i++) res[i] = a[i] * 0.45f + b[i] * (0.55f - wRand) + r.NextFloat() * wRand;
        return res;
    }

    private static uint Pick(uint[] pal, float v)
    {
        int i = (int)(Math.Clamp(v, 0, 0.9999f) * pal.Length);
        return pal[i];
    }

    private static uint[] Palette(params uint[] hex) => hex.Select(h => Hex(h)).ToArray();

    private static uint[] Noisy(int seed, uint[] pal, float randW = 0.35f, float contrast = 1.3f)
    {
        var n = Fbm(seed, randW); var p = New();
        for (int i = 0; i < 256; i++) p[i] = Pick(pal, (n[i] - 0.5f) * contrast + 0.5f);
        return p;
    }

    // ---------- bloki ----------
    private static uint[] Stone(int seed = 1)
    {
        var pal = Palette(0x686868, 0x737373, 0x7D7D7D, 0x7D7D7D, 0x878787, 0x919191);
        var p = Noisy(seed, pal, 0.4f, 1.6f);
        var r = new Rng(seed + 99);
        // drobne ciemne smugi
        for (int k = 0; k < 4; k++)
        {
            int x = r.Next(16), y = r.Next(16), len = 2 + r.Next(3);
            for (int i = 0; i < len; i++) p[y * 16 + ((x + i) & 15)] = Hex(0x6A6A6A);
        }
        return p;
    }

    private static uint[] Dirt(int seed = 2)
    {
        var pal = Palette(0x5E4129, 0x6F4C32, 0x79553A, 0x866043, 0x866043, 0x8F6849, 0x9B724E);
        var p = Noisy(seed, pal, 0.6f, 1.5f);
        var r = new Rng(seed + 5);
        for (int k = 0; k < 6; k++) p[r.Next(256)] = Hex(0xB9855C);
        for (int k = 0; k < 8; k++) p[r.Next(256)] = Hex(0x593D29);
        return p;
    }

    private static uint[] GrassTop(int seed = 3)
    {
        var n = Fbm(seed, 0.55f); var p = New();
        float[] lv = { 0.62f, 0.70f, 0.77f, 0.84f, 0.92f };
        for (int i = 0; i < 256; i++) p[i] = Gray(lv[(int)(Math.Clamp((n[i] - 0.5f) * 1.6f + 0.5f, 0, 0.999f) * lv.Length)]);
        return p;
    }

    private static uint[] GrassSide()
    {
        var p = Dirt(4);
        var top = GrassTop(5);
        var r = new Rng(77);
        for (int x = 0; x < 16; x++)
        {
            int depth = 3 + (r.Next(3) == 0 ? 1 : 0) + (r.Next(5) == 0 ? 1 : 0);
            if (x % 5 == 2 && r.NextBool()) depth += 1;
            for (int y = 0; y < depth; y++) p[y * 16 + x] = WithA(top[y * 16 + x], 160);
        }
        return p;
    }

    private static uint[] Cobble(int seed, bool mossy)
    {
        var r = new Rng(seed);
        int n = 10;
        var pts = new (float x, float y, float shade)[n];
        for (int i = 0; i < n; i++) pts[i] = (r.NextFloat() * 16, r.NextFloat() * 16, 0.8f + r.NextFloat() * 0.35f);
        var p = New();
        var moss = Fbm(seed + 3, 0.3f);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d1 = 999, d2 = 999; int best = 0; float bdx = 0, bdy = 0;
                for (int i = 0; i < n; i++)
                    for (int ox = -16; ox <= 16; ox += 16)
                        for (int oy = -16; oy <= 16; oy += 16)
                        {
                            float dx = x + 0.5f - (pts[i].x + ox), dy = y + 0.5f - (pts[i].y + oy);
                            float d = MathF.Sqrt(dx * dx + dy * dy);
                            if (d < d1) { d2 = d1; d1 = d; best = i; bdx = dx; bdy = dy; }
                            else if (d < d2) d2 = d;
                        }
                uint c;
                if (d2 - d1 < 1.1f) c = (d2 - d1 < 0.5f) ? Hex(0x3C3C3C) : Hex(0x525252);
                else
                {
                    float shade = pts[best].shade;
                    float hl = -(bdx + bdy) * 0.03f;
                    float v = Math.Clamp(shade + hl + (r.NextFloat() - 0.5f) * 0.12f, 0.55f, 1.25f);
                    c = Mul(Hex(0x8A8A8A), v);
                }
                if (mossy && moss[y * 16 + x] > 0.58f) c = (d2 - d1 < 1.1f) ? Hex(0x3E5A2A) : Lerp(Hex(0x5C7F3A), Hex(0x4A6A2E), r.NextFloat());
                p[y * 16 + x] = c;
            }
        return p;
    }

    private static uint[] Planks(uint[] pal, uint seam, int seed)
    {
        // pal: jasny, średni, ciemny, bardzo jasny
        var p = New(); var r = new Rng(seed);
        var grain = TileNoise(seed, 16);
        int[] seams = { 3, 12, 7, 15 };
        for (int b = 0; b < 4; b++)
        {
            int sx = (seams[b] + r.Next(2)) & 15;
            for (int y = b * 4; y < b * 4 + 4; y++)
                for (int x = 0; x < 16; x++)
                {
                    float g = grain[(y / 2 * 16 + x) & 255] + (r.NextFloat() - 0.5f) * 0.25f;
                    uint c = g < 0.3f ? pal[2] : g < 0.62f ? pal[1] : g < 0.9f ? pal[0] : pal[3];
                    if (y == b * 4 + 3) c = seam;
                    else if (x == sx) c = Mul(seam, 1.15f);
                    p[y * 16 + x] = c;
                }
        }
        return p;
    }

    private static uint[] LogSide(uint[] pal, int seed)
    {
        var p = New(); var r = new Rng(seed);
        var col = new float[16];
        for (int x = 0; x < 16; x++) col[x] = r.NextFloat();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float v = col[x] * 0.6f + r.NextFloat() * 0.25f + (MathF.Sin((y + x * 3) * 0.7f) * 0.5f + 0.5f) * 0.15f;
                if (r.Next(14) == 0) col[x] = r.NextFloat();
                p[y * 16 + x] = Pick(pal, v);
            }
        return p;
    }

    private static uint[] LogTop(uint bark, uint[] rings, int seed)
    {
        var p = New(); var r = new Rng(seed);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float dx = x - 7.5f, dy = y - 7.5f;
                float d = MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) * 0.75f + MathF.Sqrt(dx * dx + dy * dy) * 0.25f;
                uint c;
                if (x == 0 || y == 0 || x == 15 || y == 15) c = Mul(bark, 0.85f + r.NextFloat() * 0.3f);
                else
                {
                    int ring = (int)(d * 0.9f + r.NextFloat() * 0.4f);
                    c = rings[ring % rings.Length];
                    c = Mul(c, 0.94f + r.NextFloat() * 0.1f);
                }
                p[y * 16 + x] = c;
            }
        return p;
    }

    private static uint[] Leaves(int seed, bool transparent)
    {
        var n = Fbm(seed, 0.5f); var r = new Rng(seed + 3);
        var holes = TileNoise(seed + 9, 8);
        var p = New();
        float[] lv = { 0.36f, 0.45f, 0.53f, 0.62f, 0.72f };
        for (int i = 0; i < 256; i++)
        {
            bool hole = holes[i] + r.NextFloat() * 0.35f > 0.92f;
            uint c = Gray(lv[(int)(Math.Clamp((n[i] - 0.5f) * 1.7f + 0.5f, 0, 0.999f) * lv.Length)]);
            if (hole) c = transparent ? 0 : Gray(0.22f);
            p[i] = c;
        }
        return p;
    }

    private static uint[] Ore(int seed, uint light, uint mid, uint dark, int clusters = 5)
    {
        var p = Stone(seed);
        var r = new Rng(seed * 13 + 1);
        for (int k = 0; k < clusters; k++)
        {
            int cx = r.Next(14) + 1, cy = r.Next(14) + 1;
            int shape = r.Next(3);
            var pts = new List<(int, int)>();
            pts.Add((cx, cy)); pts.Add((cx + 1, cy)); pts.Add((cx, cy + 1));
            if (shape >= 1) pts.Add((cx + 1, cy + 1));
            if (shape == 2) { pts.Add((cx - 1, cy)); pts.Add((cx, cy - 1)); }
            foreach (var (x, y) in pts)
            {
                int sx = (x + 1) & 15, sy = (y + 1) & 15;
                if (!pts.Contains((x + 1, y + 1))) p[sy * 16 + sx] = Mul(dark, 0.8f);
            }
            foreach (var (x, y) in pts) p[(y & 15) * 16 + (x & 15)] = mid;
            p[(cy & 15) * 16 + (cx & 15)] = light;
            if (r.NextBool()) p[((cy + 1) & 15) * 16 + ((cx + 1) & 15)] = dark;
        }
        return p;
    }

    private static uint[] StorageBlock(uint baseC, uint light, uint dark, int seed, bool pattern)
    {
        var p = New(baseC); var r = new Rng(seed);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                uint c = Mul(baseC, 0.95f + r.NextFloat() * 0.1f);
                if (x == 0 || y == 0) c = light;
                if (x == 15 || y == 15) c = dark;
                if (pattern)
                {
                    if ((x == 1 || y == 1) && x < 15 && y < 15) c = Lerp(light, baseC, 0.5f);
                    if ((x == 14 || y == 14) && x > 0 && y > 0) c = Lerp(dark, baseC, 0.5f);
                    if ((x + y) % 7 == 0 && x > 2 && y > 2 && x < 13 && y < 13 && r.Next(3) == 0) c = light;
                }
                p[y * 16 + x] = c;
            }
        return p;
    }

    private static uint[] Wool(uint c, int seed)
    {
        var p = New(); var r = new Rng(seed);
        var n = TileNoise(seed, 8);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float v = 0.88f + n[y * 16 + x] * 0.12f + (((x + y * 2) % 4 == 0) ? -0.05f : 0) + (r.NextFloat() - 0.5f) * 0.06f;
                p[y * 16 + x] = Mul(c, v);
            }
        return p;
    }

    private static void GenerateBlocks()
    {
        Add("stone", Stone());
        Add("dirt", Dirt());
        Add("grass_top", GrassTop());
        Add("grass_side", GrassSide());
        { var gs = GrassSide(); var tc = Hex(0x79C05A); for (int i = 0; i < 256; i++) if (A(gs[i]) < 250) gs[i] = Rgba(R(gs[i]) * R(tc) / 255, G(gs[i]) * G(tc) / 255, Bl(gs[i]) * Bl(tc) / 255); Add("grass_side_item", gs); var gt = GrassTop(); for (int i = 0; i < 256; i++) gt[i] = Rgba(R(gt[i]) * R(tc) / 255, G(gt[i]) * G(tc) / 255, Bl(gt[i]) * Bl(tc) / 255); Add("grass_top_item", gt); }
        Add("cobblestone", Cobble(11, false));
        Add("cobblestone_mossy", Cobble(11, true));
        Add("planks_oak", Planks(Palette(0xB08F5A, 0x9C7F4E, 0x86683E, 0xBC9862), Hex(0x6B5331), 21));
        Add("planks_birch", Planks(Palette(0xD7C185, 0xC8B57A, 0xB8A46A, 0xE0CC90), Hex(0x9E8C5C), 22));
        Add("bedrock", Noisy(31, Palette(0x1E1E1E, 0x333333, 0x4A4A4A, 0x575757, 0x6E6E6E, 0x858585), 0.6f, 1.8f));
        Add("sand", Noisy(41, Palette(0xC9BE88, 0xD1C893, 0xDBD3A0, 0xDBD3A0, 0xE3DBB0, 0xEBE4C0), 0.7f, 1.3f));
        Add("gravel", Gravel());
        Add("gold_ore", Ore(51, Hex(0xFFFF8F), Hex(0xFCEE4B), Hex(0xBA9116)));
        Add("iron_ore", Ore(52, Hex(0xE8CBB4), Hex(0xD8AF93), Hex(0x9C7B66)));
        Add("coal_ore", Ore(53, Hex(0x4A4A4A), Hex(0x2E2E2E), Hex(0x1A1A1A), 6));
        Add("diamond_ore", Ore(54, Hex(0xD5FFF6), Hex(0x5DECF5), Hex(0x1AA5A9), 4));
        Add("redstone_ore", Ore(55, Hex(0xFF6A6A), Hex(0xFF0000), Hex(0x8F0000), 6));
        Add("lapis_ore", Ore(56, Hex(0x3A6BDB), Hex(0x1F48B0), Hex(0x0F2470), 5));
        Add("log_oak", LogSide(Palette(0x382A14, 0x4F3A1C, 0x5E4A26, 0x6B5126, 0x7A5F33), 61));
        Add("log_oak_top", LogTop(Hex(0x5E4A26), Palette(0xB4905A, 0x9C7A48, 0xA88552, 0x9C7A48), 62));
        Add("log_birch", BirchSide());
        Add("log_birch_top", LogTop(Hex(0xD8D7D2), Palette(0xD3C38E, 0xC1AE77, 0xCDBB84, 0xC1AE77), 64));
        Add("leaves_oak", Leaves(71, true));
        Add("leaves_birch", Leaves(72, true));
        Add("leaves_oak_opaque", Leaves(71, false));
        Add("leaves_birch_opaque", Leaves(72, false));
        Add("glass", Glass());
        Add("lapis_block", StorageBlock(Hex(0x1D46A8), Hex(0x3B66D0), Hex(0x10286A), 81, true));
        Add("sandstone_top", Noisy(91, Palette(0xD6CD9A, 0xDCD3A2, 0xE0D8A9, 0xDCD3A2), 0.5f, 1.2f));
        Add("sandstone_bottom", SandstoneBottom());
        Add("sandstone_side", SandstoneSide());
        uint[] woolCols = { 0xE9ECEC, 0x8E8E86, 0x3E4447, 0x141519, 0x724728, 0xED8DAC };
        for (int i = 0; i < woolCols.Length; i++) Add("wool_" + i, Wool(Hex(woolCols[i]), 100 + i));
        Add("flower_dandelion", Dandelion());
        Add("flower_poppy", Poppy());
        Add("tallgrass", TallGrass());
        Add("deadbush", DeadBush());
        Add("cactus_side", CactusSide());
        Add("cactus_top", CactusTop(false));
        Add("cactus_bottom", CactusTop(true));
        Add("reeds", Reeds());
        Add("sapling_oak", Sapling(Hex(0x3C7A1E), Hex(0x2C5A12), Hex(0x55A02C)));
        Add("sapling_birch", Sapling(Hex(0x6E9A3E), Hex(0x557A2C), Hex(0x8CBA55)));
        Add("gold_block", StorageBlock(Hex(0xF9D849), Hex(0xFFF68C), Hex(0xC9891A), 111, true));
        Add("iron_block", StorageBlock(Hex(0xDADADA), Hex(0xF4F4F4), Hex(0xA0A0A0), 112, true));
        Add("diamond_block", StorageBlock(Hex(0x61DBD5), Hex(0xC9FFF8), Hex(0x2AA7A1), 113, true));
        Add("redstone_block", RedstoneBlock());
        Add("coal_block", StorageBlock(Hex(0x1C1C1C), Hex(0x333333), Hex(0x0E0E0E), 115, false));
        Add("obsidian", Noisy(121, Palette(0x0A0910, 0x110F19, 0x14121D, 0x1B1729, 0x241B35, 0x3B2A54), 0.7f, 1.4f));
        Add("stonebrick", StoneBrick());
        Add("brick", Bricks());
        Add("crafting_table_top", CraftingTop());
        Add("crafting_table_side", CraftingSide(false));
        Add("crafting_table_front", CraftingSide(true));
        Add("furnace_side", FurnaceSide());
        Add("furnace_top", FurnaceTop());
        Add("furnace_front_off", FurnaceFront(false));
        Add("furnace_front_on", FurnaceFront(true));
        Add("chest_top", ChestTex(0));
        Add("chest_side", ChestTex(1));
        Add("chest_front", ChestTex(2));
        Add("torch", Torch(Hex(0xFFD84A), Hex(0xFFFFB0), Hex(0xFF9C1A)));
        Add("redstone_torch_on", Torch(Hex(0xFF2A2A), Hex(0xFF9A9A), Hex(0xB20000)));
        Add("redstone_torch_off", Torch(Hex(0x6A1E1E), Hex(0x7A2A2A), Hex(0x4A1010)));
        Add("farmland_dry", Farmland(false));
        Add("farmland_wet", Farmland(true));
        for (int i = 0; i < 8; i++) Add("wheat_" + i, Wheat(i));
        Add("ladder", Ladder());
        Add("door_wood_lower", Door(false));
        Add("door_wood_upper", Door(true));
        Add("tnt_side", TntSide());
        Add("tnt_top", TntTop(true));
        Add("tnt_bottom", TntTop(false));
        Add("redstone_dust_cross", RedDust(true));
        Add("redstone_dust_line", RedDust(false));
        Add("lever", LeverTex());
        Add("stone_slab_top", SlabTop());
        Add("stone_slab_side", SlabSide());
        Add("redstone_lamp_off", Lamp(false));
        Add("redstone_lamp_on", Lamp(true));
        Add("repeater_off", Repeater(false));
        Add("repeater_on", Repeater(true));
        Add("piston_top", PistonTop(false));
        Add("piston_top_sticky", PistonTop(true));
        Add("piston_side", PistonSide());
        Add("piston_bottom", PistonBottom());
        Add("piston_inner", PistonInner());
        AddBed();
        Add("bookshelf", Bookshelf());
        Add("glowstone", Noisy(141, Palette(0x8A5A26, 0xCC8C3F, 0xE8B055, 0xFFDB72, 0xFFF5B3), 0.6f, 1.6f));
        Add("snow", Noisy(142, Palette(0xE6F0F0, 0xEEF8F8, 0xF4FCFC, 0xFFFFFF), 0.5f, 1.0f));
        var ice = Noisy(143, Palette(0x6E9CF0, 0x7DADFF, 0x8DBAFF, 0xA5C8FF), 0.4f, 1.0f);
        for (int i = 0; i < 256; i++) ice[i] = WithA(ice[i], 170);
        Add("ice", ice);
        Add("clay", Noisy(144, Palette(0x9298A5, 0x9AA0AD, 0xA0A6B3, 0xA8AEBB), 0.5f, 1.2f));
        for (int i = 0; i < 10; i++) Add("destroy_" + i, Destroy(i));
        // animowane: woda i lawa (klatka 0 w atlasie, reszta w Animations)
        var water = new List<uint[]>(); var lava = new List<uint[]>();
        for (int f = 0; f < 32; f++) { water.Add(WaterFrame(f)); lava.Add(LavaFrame(f)); }
        Animations["water_still"] = water; Animations["lava_still"] = lava;
        Add("water_still", water[0]);
        Add("lava_still", lava[0]);
    }

    private static uint[] Gravel()
    {
        var r = new Rng(43);
        int n = 22;
        var pts = new (float x, float y, uint c)[n];
        var cols = Palette(0xA9A5A4, 0x6E6967, 0x8E7E78, 0x5D5856, 0x9A9594, 0x7F7C7B, 0xB5B0AE);
        for (int i = 0; i < n; i++) pts[i] = (r.NextFloat() * 16, r.NextFloat() * 16, cols[r.Next(cols.Length)]);
        var p = New();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d1 = 999, d2 = 999; int best = 0;
                for (int i = 0; i < n; i++)
                    for (int ox = -16; ox <= 16; ox += 16)
                        for (int oy = -16; oy <= 16; oy += 16)
                        {
                            float dx = x + 0.5f - (pts[i].x + ox), dy = y + 0.5f - (pts[i].y + oy);
                            float d = dx * dx + dy * dy;
                            if (d < d1) { d2 = d1; d1 = d; best = i; } else if (d < d2) d2 = d;
                        }
                uint c = pts[best].c;
                if (MathF.Sqrt(d2) - MathF.Sqrt(d1) < 0.6f) c = Mul(c, 0.7f);
                p[y * 16 + x] = Mul(c, 0.93f + r.NextFloat() * 0.12f);
            }
        return p;
    }

    private static uint[] BirchSide()
    {
        var p = Noisy(63, Palette(0xC9C8C3, 0xD3D2CD, 0xD8D7D2, 0xDEDDD8, 0xE6E5E0), 0.5f, 1.2f);
        var r = new Rng(631);
        for (int k = 0; k < 9; k++)
        {
            int x = r.Next(16), y = r.Next(16), len = 2 + r.Next(4);
            uint c = r.Next(3) == 0 ? Hex(0x4A4A48) : Hex(0x1E1E1E);
            for (int i = 0; i < len; i++) p[y * 16 + ((x + i) & 15)] = c;
            if (r.NextBool()) p[((y + 1) & 15) * 16 + ((x + 1) & 15)] = Hex(0x5A5A56);
        }
        return p;
    }

    private static uint[] Glass()
    {
        var p = New();
        uint edge = Hex(0xDAEEF0), edgeD = Hex(0xA7C9D1), hl = Hex(0xFFFFFF, 220);
        for (int i = 0; i < 16; i++) { p[i] = edge; p[i * 16] = edge; p[15 * 16 + i] = edgeD; p[i * 16 + 15] = edgeD; }
        int[][] streaks = { new[] { 3, 4 }, new[] { 4, 3 }, new[] { 5, 2 }, new[] { 10, 9 }, new[] { 11, 8 }, new[] { 12, 7 }, new[] { 4, 5 } };
        foreach (var s in streaks) p[s[1] * 16 + s[0]] = hl;
        return p;
    }

    private static uint[] SandstoneSide()
    {
        var p = New(); var r = new Rng(92); var n = TileNoise(93, 8);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                uint c;
                if (y < 3) c = y == 2 ? Hex(0xCFC48C) : Lerp(Hex(0xE0D7A6), Hex(0xD8CF9C), r.NextFloat());
                else if (y > 12) c = y == 13 ? Hex(0xB8AA70) : Lerp(Hex(0xC9BC82), Hex(0xBFB179), r.NextFloat());
                else
                {
                    float band = MathF.Sin(y * 1.3f + n[y * 16 + x] * 3f) * 0.5f + 0.5f;
                    c = Lerp(Hex(0xCBBE84), Hex(0xDDD4A2), band * 0.8f + r.NextFloat() * 0.2f);
                }
                p[y * 16 + x] = c;
            }
        return p;
    }
    private static uint[] SandstoneBottom()
    {
        var p = Noisy(94, Palette(0xCFC48E, 0xD6CD9A, 0xDCD3A2, 0xD6CD9A), 0.5f, 1.2f);
        var r = new Rng(95);
        for (int k = 0; k < 3; k++) { int x = r.Next(14), y = r.Next(14); p[y * 16 + x] = Hex(0xB3A56D); p[(y + 1) * 16 + x + 1] = Hex(0xB3A56D); }
        return p;
    }

    private static uint[] Dandelion()
    {
        var p = New();
        uint st = Hex(0x337A1A), st2 = Hex(0x4A9A28), y1 = Hex(0xFFEC4F), y2 = Hex(0xF5C21C), y3 = Hex(0xFFF7A8);
        for (int y = 10; y < 16; y++) p[y * 16 + 7] = st;
        p[12 * 16 + 8] = st2; p[13 * 16 + 9] = st2; p[11 * 16 + 6] = st2;
        int[,] fl = { { 6, 6 }, { 7, 6 }, { 8, 6 }, { 6, 7 }, { 7, 7 }, { 8, 7 }, { 6, 8 }, { 7, 8 }, { 8, 8 }, { 7, 5 }, { 5, 7 }, { 9, 7 }, { 7, 9 } };
        for (int i = 0; i < fl.GetLength(0); i++) p[fl[i, 1] * 16 + fl[i, 0]] = (i % 3 == 0) ? y2 : y1;
        p[6 * 16 + 7] = y3; p[7 * 16 + 6] = y3;
        return p;
    }

    private static uint[] Poppy()
    {
        var p = New();
        uint st = Hex(0x337A1A), st2 = Hex(0x4A9A28), r1 = Hex(0xED302C), r2 = Hex(0xBE1C1C), r3 = Hex(0xFF6F5E), c = Hex(0x2B0A0A);
        for (int y = 9; y < 16; y++) p[y * 16 + 7] = st;
        p[12 * 16 + 6] = st2; p[11 * 16 + 5] = st2; p[13 * 16 + 8] = st2; p[12 * 16 + 9] = st2;
        for (int y = 4; y < 9; y++)
            for (int x = 5; x < 10; x++)
            {
                if ((x == 5 || x == 9) && (y == 4 || y == 8)) continue;
                p[y * 16 + x] = (x + y) % 3 == 0 ? r2 : r1;
            }
        p[5 * 16 + 6] = r3; p[4 * 16 + 7] = r3;
        p[6 * 16 + 7] = c; p[6 * 16 + 8] = Hex(0x5A1010);
        return p;
    }

    private static uint[] TallGrass()
    {
        var p = New(); var r = new Rng(201);
        int[] xs = { 1, 3, 4, 6, 7, 9, 10, 12, 13, 14 };
        foreach (int bx in xs)
        {
            int h = 5 + r.Next(10);
            int x = bx; float drift = (r.NextFloat() - 0.5f) * 0.4f; float fx = bx;
            for (int y = 15; y > 15 - h && y >= 0; y--)
            {
                fx += drift; x = Math.Clamp((int)MathF.Round(fx), 0, 15);
                p[y * 16 + x] = Gray(0.55f + r.NextFloat() * 0.3f + (15 - y) * 0.01f);
            }
        }
        return p;
    }

    private static uint[] DeadBush()
    {
        var p = New();
        uint a = Hex(0x6B4E2A), b = Hex(0x8A6436), c = Hex(0x4F391D);
        void L(int x0, int y0, int x1, int y1, uint col)
        {
            int n = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
            for (int i = 0; i <= n; i++) { int x = x0 + (x1 - x0) * i / Math.Max(1, n), y = y0 + (y1 - y0) * i / Math.Max(1, n); p[y * 16 + x] = col; }
        }
        L(8, 15, 8, 9, a); L(8, 11, 4, 6, b); L(8, 10, 12, 5, a); L(5, 8, 2, 7, c); L(11, 7, 14, 8, c); L(8, 9, 7, 3, b); L(6, 7, 5, 3, c); L(10, 6, 11, 2, b);
        return p;
    }

    private static uint[] CactusSide()
    {
        var p = New(); var r = new Rng(211);
        uint d = Hex(0x0D6418), m = Hex(0x107A1C), l = Hex(0x15921F);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                uint c = (x % 4 == 1) ? l : (x % 4 == 3 ? d : m);
                c = Mul(c, 0.92f + r.NextFloat() * 0.12f);
                p[y * 16 + x] = c;
            }
        for (int k = 0; k < 10; k++)
        {
            int x = (r.Next(4) * 4 + 1) & 15, y = r.Next(16);
            p[y * 16 + x] = Hex(0xDADAB0); p[((y + 1) & 15) * 16 + x] = Hex(0x1B1B1B);
        }
        for (int y = 0; y < 16; y++) { p[y * 16] = Mul(p[y * 16], 0.8f); p[y * 16 + 15] = Mul(p[y * 16 + 15], 0.8f); }
        return p;
    }
    private static uint[] CactusTop(bool bottom)
    {
        var p = New(); var r = new Rng(bottom ? 213 : 212);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = MathF.Max(MathF.Abs(x - 7.5f), MathF.Abs(y - 7.5f));
                uint c = d > 6.5f ? Hex(0x0D6418) : ((int)d % 2 == 0 ? Hex(0x15921F) : Hex(0x107A1C));
                if (!bottom && d < 1.5f) c = Hex(0x2DA537);
                p[y * 16 + x] = Mul(c, 0.92f + r.NextFloat() * 0.12f);
            }
        return p;
    }

    private static uint[] Reeds()
    {
        var p = New(); var r = new Rng(221);
        int[] cols = { 3, 8, 12 };
        foreach (int cx in cols)
        {
            int off = r.Next(4);
            for (int y = 0; y < 16; y++)
            {
                bool node = (y + off) % 5 == 0;
                p[y * 16 + cx] = node ? Hex(0x6B8F3D) : Hex(0x94C065);
                p[y * 16 + cx + 1] = node ? Hex(0x5E7F34) : Hex(0xAACC77);
                if (node && r.NextBool()) p[y * 16 + cx + 2] = Hex(0x7FA855);
            }
        }
        return p;
    }

    private static uint[] Sapling(uint leaf, uint dark, uint light)
    {
        var p = New(); var r = new Rng((int)leaf);
        for (int y = 9; y < 16; y++) p[y * 16 + 7] = Hex(0x5E4A26);
        p[12 * 16 + 8] = Hex(0x6B5126);
        for (int y = 1; y < 11; y++)
            for (int x = 2; x < 14; x++)
            {
                float dx = x - 7.5f, dy = (y - 5.5f) * 1.2f;
                if (dx * dx + dy * dy < 22 + r.Next(10) - 5)
                {
                    float v = r.NextFloat();
                    p[y * 16 + x] = v < 0.3f ? dark : v < 0.8f ? leaf : light;
                }
            }
        return p;
    }

    private static uint[] RedstoneBlock()
    {
        var p = New(); var r = new Rng(114);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                uint c = Hex(0xAB1A09);
                if ((x + y) % 4 == 0) c = Hex(0xD9260F);
                if ((x - y + 16) % 4 == 2) c = Hex(0x7F1006);
                if (x == 0 || y == 0) c = Hex(0xE33A1F);
                if (x == 15 || y == 15) c = Hex(0x620B04);
                p[y * 16 + x] = Mul(c, 0.94f + r.NextFloat() * 0.1f);
            }
        return p;
    }

    private static uint[] StoneBrick()
    {
        var p = Stone(131);
        for (int i = 0; i < 256; i++) p[i] = Mul(p[i], 0.98f);
        uint mort = Hex(0x4F4F4F), hl = Hex(0x9A9A9A);
        for (int x = 0; x < 16; x++) { p[7 * 16 + x] = mort; p[15 * 16 + x] = mort; p[0 * 16 + x] = hl; p[8 * 16 + x] = hl; }
        for (int y = 0; y < 8; y++) { p[y * 16 + 15] = mort; if (y > 0 && y < 7) p[y * 16 + 0] = hl; }
        for (int y = 8; y < 16; y++) { p[y * 16 + 7] = mort; if (y > 8 && y < 15) p[y * 16 + 8] = hl; }
        return p;
    }

    private static uint[] Bricks()
    {
        var p = New(); var r = new Rng(132);
        uint mort = Hex(0xA49A8D);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int row = y / 4; int off = (row % 2) * 4;
                bool m = y % 4 == 3 || (x + off) % 8 == 7;
                uint c = m ? Mul(mort, 0.9f + r.NextFloat() * 0.15f) : Mul(Hex(0x96604B), 0.85f + r.NextFloat() * 0.25f);
                if (!m && y % 4 == 0) c = Mul(c, 1.1f);
                p[y * 16 + x] = c;
            }
        return p;
    }

    private static uint[] CraftingTop()
    {
        var p = Planks(Palette(0xB08F5A, 0x9C7F4E, 0x86683E, 0xBC9862), Hex(0x6B5331), 141);
        uint dark = Hex(0x4F3A1C), light = Hex(0xC8A26A);
        for (int i = 0; i < 16; i++) { p[i] = dark; p[15 * 16 + i] = dark; p[i * 16] = dark; p[i * 16 + 15] = dark; }
        for (int i = 1; i < 15; i++) { p[1 * 16 + i] = light; p[i * 16 + 1] = light; }
        for (int i = 2; i < 14; i++) { p[5 * 16 + i] = dark; p[10 * 16 + i] = dark; p[i * 16 + 5] = dark; p[i * 16 + 10] = dark; }
        return p;
    }

    private static uint[] CraftingSide(bool front)
    {
        var p = Planks(Palette(0xA8865A, 0x94774C, 0x7E623C, 0xB4905E), Hex(0x5E4A2C), front ? 143 : 142);
        uint dark = Hex(0x4F3A1C);
        for (int i = 0; i < 16; i++) { p[i] = dark; p[16 + i] = Hex(0x6B5331); p[2 * 16 + i] = Hex(0xB08F5A); }
        uint metal = Hex(0x8A8A8A), metalD = Hex(0x5A5A5A), handle = Hex(0x6B4E2A);
        if (!front)
        {
            // młotek
            for (int y = 5; y < 13; y++) p[y * 16 + 4] = handle;
            for (int x = 2; x < 7; x++) { p[4 * 16 + x] = metal; p[5 * 16 + x] = metalD; }
            // piła
            for (int y = 4; y < 13; y++) { p[y * 16 + 10] = metal; p[y * 16 + 11] = metal; if (y % 2 == 0) p[y * 16 + 12] = metalD; }
            for (int y = 12; y < 15; y++) { p[y * 16 + 10] = handle; p[y * 16 + 11] = handle; }
        }
        else
        {
            // nożyce/obcęgi
            for (int y = 5; y < 13; y++) { p[y * 16 + 5] = metal; p[y * 16 + 10] = metal; }
            for (int x = 5; x < 11; x++) p[8 * 16 + x] = metalD;
            for (int y = 11; y < 14; y++) { p[y * 16 + 4] = handle; p[y * 16 + 11] = handle; }
            p[4 * 16 + 6] = metal; p[4 * 16 + 9] = metal;
        }
        return p;
    }

    private static uint[] FurnaceSide()
    {
        var p = Noisy(151, Palette(0x5F5F5F, 0x686868, 0x737373, 0x7D7D7D, 0x878787), 0.5f, 1.3f);
        for (int i = 0; i < 16; i++) { p[i] = Hex(0x4E4E4E); p[15 * 16 + i] = Hex(0x4E4E4E); }
        return p;
    }
    private static uint[] FurnaceTop()
    {
        var p = Noisy(152, Palette(0x6A6A6A, 0x737373, 0x7D7D7D, 0x868686), 0.5f, 1.1f);
        for (int i = 0; i < 16; i++) { p[i] = Hex(0x585858); p[15 * 16 + i] = Hex(0x585858); p[i * 16] = Hex(0x585858); p[i * 16 + 15] = Hex(0x585858); }
        for (int i = 2; i < 14; i++) { p[2 * 16 + i] = Hex(0x666666); p[13 * 16 + i] = Hex(0x666666); }
        return p;
    }
    private static uint[] FurnaceFront(bool lit)
    {
        var p = FurnaceSide();
        var r = new Rng(153);
        // szczelina u góry
        for (int x = 4; x < 12; x++) { p[3 * 16 + x] = Hex(0x2A2A2A); p[4 * 16 + x] = Hex(0x3A3A3A); }
        // otwór paleniska
        for (int y = 8; y < 14; y++)
            for (int x = 3; x < 13; x++)
            {
                bool edge = y == 8 || x == 3 || x == 12 || y == 13;
                uint c = edge ? Hex(0x484848) : Hex(0x1A1A1A);
                if (!edge && lit)
                {
                    float h = (13 - y) / 5f + r.NextFloat() * 0.3f;
                    c = h > 0.9f ? Hex(0x3A1A0A) : h > 0.6f ? Hex(0xE25822) : h > 0.3f ? Hex(0xFF9C1A) : Hex(0xFFD23A);
                }
                p[y * 16 + x] = c;
            }
        if (!lit) for (int x = 4; x < 12; x += 2) p[12 * 16 + x] = Hex(0x333333);
        return p;
    }

    private static uint[] ChestTex(int kind)
    {
        var p = New(); var r = new Rng(160 + kind);
        uint a = Hex(0xB67C33), b = Hex(0x9F6B26), c = Hex(0x8A5A1E), dark = Hex(0x3D2913);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float v = r.NextFloat();
                uint col = v < 0.3f ? c : v < 0.75f ? b : a;
                if (kind == 0 ? (y % 4 == 3) : (y % 5 == 4 && y < 14)) col = Mul(col, 0.85f);
                if (x == 0 || y == 0 || x == 15 || y == 15) col = dark;
                if (kind > 0 && y == 5) col = dark;
                p[y * 16 + x] = col;
            }
        if (kind == 2)
        {
            for (int y = 3; y < 8; y++) for (int x = 7; x < 9; x++) p[y * 16 + x] = Hex(0xC7C7C7);
            p[3 * 16 + 7] = Hex(0xEDEDED); p[7 * 16 + 8] = Hex(0x7A7A7A); p[7 * 16 + 7] = Hex(0x8F8F8F);
        }
        return p;
    }

    private static uint[] Torch(uint head, uint headLight, uint headDark)
    {
        var p = New();
        uint s1 = Hex(0x8F6A3B), s2 = Hex(0x6B4E2A);
        for (int y = 8; y < 16; y++) { p[y * 16 + 7] = s1; p[y * 16 + 8] = s2; }
        p[6 * 16 + 7] = headLight; p[6 * 16 + 8] = head;
        p[7 * 16 + 7] = head; p[7 * 16 + 8] = headDark;
        p[5 * 16 + 7] = WithA(head, 200);
        return p;
    }

    private static uint[] Farmland(bool wet)
    {
        var p = Dirt(171);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float f = wet ? 0.62f : 0.95f;
                if (y % 4 == 0 || y % 4 == 3) f *= 0.82f;
                if (x == 0 || x == 15 || y == 0 || y == 15) f *= 0.85f;
                p[y * 16 + x] = Mul(p[y * 16 + x], f);
            }
        return p;
    }

    private static uint[] Wheat(int stage)
    {
        var p = New(); var r = new Rng(180 + stage);
        float t = stage / 7f;
        uint stalk = Lerp(Hex(0x2C8F1E), Hex(0xB89A3A), t * t);
        uint stalkL = Lerp(Hex(0x4CB43A), Hex(0xD8C062), t * t);
        int h = 2 + stage * 2;
        int[] xs = { 1, 4, 6, 9, 11, 14 };
        foreach (var x in xs)
        {
            int hh = Math.Min(15, h + r.Next(2));
            for (int y = 15; y > 15 - hh; y--) p[y * 16 + x] = r.NextBool() ? stalk : stalkL;
            if (stage == 7)
                for (int y = 15 - hh; y < 15 - hh + 5 && y >= 0; y++)
                {
                    if (y < 0) continue;
                    p[y * 16 + x] = Hex(0xD8C062);
                    if (x + 1 < 16) p[y * 16 + x + 1] = (y % 2 == 0) ? Hex(0x9C7F2A) : Hex(0xC9AC4E);
                }
        }
        return p;
    }

    private static uint[] Ladder()
    {
        var p = New();
        uint l = Hex(0x8F6A3B), d = Hex(0x5E4A26), m = Hex(0x6B5126);
        for (int y = 0; y < 16; y++) { p[y * 16 + 2] = l; p[y * 16 + 3] = d; p[y * 16 + 12] = l; p[y * 16 + 13] = d; }
        for (int k = 0; k < 4; k++)
        {
            int y = k * 4 + 1;
            for (int x = 4; x < 12; x++) { p[y * 16 + x] = l; p[(y + 1) * 16 + x] = m; }
        }
        return p;
    }

    private static uint[] Door(bool upper)
    {
        var p = New(); var r = new Rng(upper ? 191 : 190);
        uint frame = Hex(0x6B5331), panel = Hex(0x9C7F4E), panelL = Hex(0xB08F5A), panelD = Hex(0x86683E);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                uint c = r.NextFloat() < 0.5f ? panel : (r.NextBool() ? panelL : panelD);
                if (x == 0 || x == 15 || (upper && y == 0) || (!upper && y == 15)) c = frame;
                if (x == 7 || x == 8) c = Mul(c, 0.9f);
                p[y * 16 + x] = c;
            }
        if (upper)
        {
            for (int y = 2; y < 8; y++) for (int x = 2; x < 6; x++) p[y * 16 + x] = 0;
            for (int y = 2; y < 8; y++) for (int x = 10; x < 14; x++) p[y * 16 + x] = 0;
            for (int y = 10; y < 15; y++) { p[y * 16 + 2] = frame; p[y * 16 + 13] = frame; }
        }
        else
        {
            for (int y = 2; y < 14; y++) { p[y * 16 + 3] = frame; p[y * 16 + 12] = frame; }
            for (int x = 3; x < 13; x++) { p[2 * 16 + x] = frame; p[13 * 16 + x] = frame; p[8 * 16 + x] = frame; }
            p[1 * 16 + 12] = Hex(0x5A5A5A); p[1 * 16 + 13] = Hex(0x3A3A3A);
        }
        return p;
    }

    private static uint[] TntSide()
    {
        var p = New(); var r = new Rng(201);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                uint c;
                if (y >= 4 && y < 12) c = Hex(0xE8E8E8);
                else c = (x % 4 == 0) ? Hex(0xA3311B) : Mul(Hex(0xDB441A), 0.92f + r.NextFloat() * 0.1f);
                p[y * 16 + x] = c;
            }
        // "TNT"
        string[] txt = { "###.#..#.###", ".#..##.#..#.", ".#..#.##..#.", ".#..#..#..#." };
        for (int y = 0; y < 4; y++) for (int x = 0; x < 12; x++) if (txt[y][x] == '#') p[(6 + y) * 16 + 2 + x] = Hex(0x111111);
        return p;
    }
    private static uint[] TntTop(bool top)
    {
        var p = New(); var r = new Rng(top ? 202 : 203);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int cx = x % 4, cy = y % 4;
                bool ring = cx == 0 || cy == 0;
                p[y * 16 + x] = ring ? Hex(0x7A2012) : Mul(Hex(0xDB441A), 0.9f + r.NextFloat() * 0.15f);
            }
        if (top) { p[7 * 16 + 7] = Hex(0x404040); p[7 * 16 + 8] = Hex(0x505050); p[8 * 16 + 7] = Hex(0x303030); p[8 * 16 + 8] = Hex(0x404040); }
        return p;
    }

    private static uint[] RedDust(bool cross)
    {
        var p = New(); var r = new Rng(cross ? 211 : 212);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool on;
                if (cross) on = (Math.Abs(x - 7.5f) < 2.5f) || (Math.Abs(y - 7.5f) < 2.5f) || (Math.Abs(x - 7.5f) < 4 && Math.Abs(y - 7.5f) < 4);
                else on = Math.Abs(x - 7.5f) < 2.5f;
                if (on && r.Next(5) != 0) p[y * 16 + x] = Gray(0.7f + r.NextFloat() * 0.3f);
            }
        return p;
    }

    private static uint[] LeverTex()
    {
        var p = New();
        for (int y = 6; y < 16; y++) { p[y * 16 + 7] = Hex(0x8F6A3B); p[y * 16 + 8] = Hex(0x6B4E2A); }
        return p;
    }

    private static uint[] SlabTop()
    {
        var p = Noisy(221, Palette(0x9E9E9E, 0xA5A5A5, 0xACACAC, 0xB2B2B2), 0.5f, 1.0f);
        for (int i = 0; i < 16; i++) { p[i] = Hex(0x8C8C8C); p[15 * 16 + i] = Hex(0x8C8C8C); p[i * 16] = Hex(0x8C8C8C); p[i * 16 + 15] = Hex(0x8C8C8C); }
        return p;
    }
    private static uint[] SlabSide()
    {
        var p = Noisy(222, Palette(0x9A9A9A, 0xA0A0A0, 0xA8A8A8), 0.5f, 1.0f);
        for (int x = 0; x < 16; x++) { p[7 * 16 + x] = Hex(0x7E7E7E); p[15 * 16 + x] = Hex(0x7E7E7E); p[x] = Hex(0xB8B8B8); p[8 * 16 + x] = Hex(0xB8B8B8); }
        return p;
    }

    private static uint[] Lamp(bool on)
    {
        var p = New(); var r = new Rng(on ? 231 : 230);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool frame = x == 0 || y == 0 || x == 15 || y == 15 || x == 5 || x == 10 || y == 5 || y == 10;
                uint c = on ? (frame ? Hex(0xB46F33) : Lerp(Hex(0xF0C27A), Hex(0xFFE7A8), r.NextFloat()))
                            : (frame ? Hex(0x3A2412) : Lerp(Hex(0x4A2E19), Hex(0x6B4A2A), r.NextFloat()));
                p[y * 16 + x] = c;
            }
        return p;
    }

    private static uint[] Repeater(bool on)
    {
        var p = SlabTop();
        uint rd = on ? Hex(0xFF2A1A) : Hex(0x5A1010);
        for (int y = 2; y < 14; y++) p[y * 16 + 7] = rd;
        for (int y = 2; y < 14; y++) p[y * 16 + 8] = Mul(rd, 0.8f);
        return p;
    }

    private static uint[] PistonTop(bool sticky)
    {
        var p = Planks(Palette(0xB89361, 0xA6835A, 0x8E6E46, 0xC69E68), Hex(0x6B5331), 241);
        for (int i = 0; i < 16; i++) { p[i] = Hex(0x7A7A7A); p[15 * 16 + i] = Hex(0x7A7A7A); p[i * 16] = Hex(0x7A7A7A); p[i * 16 + 15] = Hex(0x7A7A7A); }
        if (sticky)
        {
            var r = new Rng(242);
            for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++) if (r.Next(5) != 0) p[y * 16 + x] = Lerp(Hex(0x6BBE5A), Hex(0x8FD67E), r.NextFloat());
        }
        return p;
    }
    private static uint[] PistonSide()
    {
        var p = Cobble(243, false);
        var pl = Planks(Palette(0xB89361, 0xA6835A, 0x8E6E46, 0xC69E68), Hex(0x6B5331), 244);
        for (int y = 0; y < 4; y++) for (int x = 0; x < 16; x++) p[y * 16 + x] = pl[y * 16 + x];
        for (int y = 4; y < 16; y++) { p[y * 16 + 7] = Hex(0xC0C0C0); p[y * 16 + 8] = Hex(0x9A9A9A); }
        return p;
    }
    private static uint[] PistonBottom()
    {
        var p = Cobble(245, false);
        for (int y = 5; y < 11; y++) for (int x = 5; x < 11; x++) p[y * 16 + x] = (x == 5 || y == 5) ? Hex(0xD0D0D0) : (x == 10 || y == 10) ? Hex(0x8A8A8A) : Hex(0xB0B0B0);
        return p;
    }
    private static uint[] PistonInner()
    {
        var p = Cobble(246, false);
        for (int y = 6; y < 10; y++) for (int x = 6; x < 10; x++) p[y * 16 + x] = Hex(0x2A2A2A);
        return p;
    }

    private static void AddBed()
    {
        uint red = Hex(0x9E2626), redL = Hex(0xB83333), redD = Hex(0x7A1A1A), white = Hex(0xE6E6E6), whiteD = Hex(0xC4C4C4), wood = Hex(0x8F6A3B), woodD = Hex(0x5E4A26);
        var r = new Rng(250);
        uint[] Top(bool head)
        {
            var p = New();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    uint c = (r.Next(4) == 0) ? redL : red;
                    if (x == 0 || x == 15) c = redD;
                    if (head && y < 7 && x > 1 && x < 14) c = (y == 0 || y == 6) ? whiteD : white;
                    p[y * 16 + x] = c;
                }
            return p;
        }
        uint[] Side(bool head)
        {
            var p = New();
            for (int y = 7; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    uint c = y < 10 ? ((r.Next(4) == 0) ? redL : red) : y < 13 ? wood : 0;
                    if (y >= 13 && ((head && x < 3) || (!head && x > 12))) c = woodD;
                    if (y == 9) c = redD;
                    p[y * 16 + x] = c;
                }
            if (head) for (int x = 10; x < 16; x++) { p[7 * 16 + x] = white; p[8 * 16 + x] = whiteD; }
            return p;
        }
        uint[] End(bool head)
        {
            var p = New();
            for (int y = 7; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    uint c = y < 10 ? red : y < 13 ? wood : ((x < 3 || x > 12) ? woodD : 0);
                    if (head && y < 9 && x > 1 && x < 14) c = white;
                    p[y * 16 + x] = c;
                }
            return p;
        }
        Add("bed_head_top", Top(true)); Add("bed_feet_top", Top(false));
        Add("bed_head_side", Side(true)); Add("bed_feet_side", Side(false));
        Add("bed_head_end", End(true)); Add("bed_feet_end", End(false));
    }

    private static uint[] Bookshelf()
    {
        var p = Planks(Palette(0xB08F5A, 0x9C7F4E, 0x86683E, 0xBC9862), Hex(0x6B5331), 261);
        var r = new Rng(262);
        uint[] cols = Palette(0x7A2222, 0x2E4A7A, 0x2E6A2E, 0x8A6A2A, 0x5A2A6A, 0x6A3A1A, 0xA8A8A8);
        for (int shelf = 0; shelf < 2; shelf++)
        {
            int y0 = shelf == 0 ? 1 : 9;
            int x = 1;
            while (x < 15)
            {
                int w = 1 + r.Next(2); uint c = cols[r.Next(cols.Length)]; int h = 5 + r.Next(2);
                for (int dx = 0; dx < w && x + dx < 15; dx++)
                    for (int y = y0 + (6 - h); y < y0 + 6; y++) p[y * 16 + x + dx] = Mul(c, dx == 0 ? 1.1f : 0.9f);
                x += w;
            }
            for (int xx = 0; xx < 16; xx++) p[(y0 + 6) * 16 + xx] = Hex(0x6B5331);
        }
        return p;
    }

    private static uint[] Destroy(int stage)
    {
        var p = New(); var r = new Rng(300);
        int segs = (stage + 1) * 5;
        var heads = new List<(float x, float y, float a)>();
        for (int i = 0; i < 5; i++) heads.Add((7.5f, 7.5f, i * 1.25f + r.NextFloat()));
        int drawn = 0;
        while (drawn < segs)
        {
            for (int h = 0; h < heads.Count && drawn < segs; h++)
            {
                var (x, y, a) = heads[h];
                a += (r.NextFloat() - 0.5f) * 1.2f;
                x += MathF.Cos(a) * 1.1f; y += MathF.Sin(a) * 1.1f;
                int ix = ((int)x % 16 + 16) % 16, iy = ((int)y % 16 + 16) % 16;
                p[iy * 16 + ix] = Rgba(0, 0, 0, 170);
                heads[h] = (x, y, a); drawn++;
                if (r.Next(8) == 0 && heads.Count < 12) heads.Add((x, y, a + 1.2f));
            }
        }
        return p;
    }

    private static uint[] WaterFrame(int f)
    {
        var p = New();
        float t = f / 32f * MathF.PI * 2;
        var n1 = TileNoise(401, 4); var n2 = TileNoise(402, 8);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int sx = (x + (int)(f * 0.5f)) & 15, sy = (y + f / 4) & 15;
                float v = n1[sy * 16 + sx] * 0.6f + n2[((y - f / 2) & 15) * 16 + ((x + f / 3) & 15)] * 0.4f;
                v += MathF.Sin(t + (x + y) * 0.4f) * 0.08f;
                uint c = Lerp(Hex(0x2A4CC8), Hex(0x4A7EEA), Math.Clamp(v, 0, 1));
                p[y * 16 + x] = WithA(c, 175);
            }
        return p;
    }

    private static uint[] LavaFrame(int f)
    {
        var p = New();
        var n1 = TileNoise(411, 4); var n2 = TileNoise(412, 8);
        float t = f / 32f * MathF.PI * 2;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int sx = (x + f / 3) & 15, sy = (y + f / 5) & 15;
                float v = n1[sy * 16 + sx] * 0.55f + n2[((y + f / 2) & 15) * 16 + ((x - f / 4) & 15)] * 0.45f + MathF.Sin(t + x * 0.5f) * 0.07f;
                uint c = v < 0.35f ? Hex(0xB8340B) : v < 0.5f ? Hex(0xD45A12) : v < 0.68f ? Hex(0xFC9B1E) : Hex(0xFFD25F);
                p[y * 16 + x] = c;
            }
        return p;
    }

    // ---------- cząsteczki ----------
    private static void GenerateParticles()
    {
        for (int i = 0; i < 8; i++)
        {
            var p = New(); float rad = 7.5f - i * 0.8f;
            var r = new Rng(500 + i);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    float dx = x - 7.5f, dy = y - 7.5f;
                    if (dx * dx + dy * dy < rad * rad && r.Next(6) != 0) p[y * 16 + x] = Gray(0.75f + r.NextFloat() * 0.25f);
                }
            Add("particle_smoke_" + i, p);
        }
        {
            var p = New();
            string[] heart = { "................", "................", "...####..####...", "..#aaaa##aaaa#..", ".#aaabaaaaaaaa#.", ".#aabaaaaaaaaa#.", ".#aaaaaaaaaaaa#.", ".#aaaaaaaaaaaa#.", "..#aaaaaaaaaa#..", "...#aaaaaaaa#...", "....#aaaaaa#....", ".....#aaaa#.....", "......#aa#......", ".......##.......", "................", "................" };
            Stamp(p, heart, new() { ['#'] = Hex(0x5A0000), ['a'] = Hex(0xE81E1E), ['b'] = Hex(0xFFA0A0) });
            Add("particle_heart", p);
        }
        {
            var p = New();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    float d = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                    if (d < 7 && d > 5) p[y * 16 + x] = Hex(0xB0D8FF); else if (d <= 5) p[y * 16 + x] = Hex(0x5080FF, 90);
                }
            p[4 * 16 + 5] = Hex(0xFFFFFF); p[5 * 16 + 4] = Hex(0xFFFFFF);
            Add("particle_bubble", p);
        }
        {
            var p = New();
            for (int i = 0; i < 16; i++) { p[7 * 16 + i] = Hex(0xFFFFFF); p[i * 16 + 7] = Hex(0xFFFFFF); }
            for (int i = 3; i < 13; i++) { p[i * 16 + i] = Hex(0xCCCCCC); p[i * 16 + 15 - i] = Hex(0xCCCCCC); }
            Add("particle_crit", p);
        }
        {
            var p = New(); var r = new Rng(510);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    float d = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                    if (d < 6) p[y * 16 + x] = d < 3 ? Hex(0xFFF4B0) : Lerp(Hex(0xFFB030), Hex(0xFF6010), (d - 3) / 3f);
                }
            Add("particle_flame", p);
        }
        {
            var p = New(); var r = new Rng(511);
            for (int k = 0; k < 30; k++) p[r.Next(256)] = Lerp(Hex(0xB040FF), Hex(0xE0A0FF), r.NextFloat());
            for (int y = 5; y < 11; y++) for (int x = 5; x < 11; x++) p[y * 16 + x] = Hex(0xC060FF);
            Add("particle_portal", p);
        }
        {
            var p = New();
            for (int y = 4; y < 12; y++) for (int x = 4; x < 12; x++) p[y * 16 + x] = Hex(0xFFFFFF);
            Add("particle_white", p);
        }
        for (int i = 0; i < 4; i++)
        {
            var p = New(); var r = new Rng(520 + i);
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    float d = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f));
                    if (d < 7.5f - i && r.Next(4) != 0) p[y * 16 + x] = Gray(0.55f + r.NextFloat() * 0.4f);
                }
            Add("particle_explosion_" + i, p);
        }
        {
            var p = New();
            for (int y = 6; y < 10; y++) for (int x = 6; x < 10; x++) p[y * 16 + x] = Hex(0x8FB8FF);
            p[6 * 16 + 6] = Hex(0xFFFFFF);
            Add("particle_splash", p);
        }
        {
            var p = New();
            string[] cloud = { "................", "......##........", ".....#aa#.###...", "....#aaaa#aaa#..", "..##aaaaaaaaaa#.", ".#aaaaaaaaaaaa#.", ".#aaaaaaaaaaaa#.", "..##aaaaaaaa##..", "....########....", "....#a#..#a#....", ".....#....#.....", "................", "................", "................", "................", "................" };
            Stamp(p, cloud, new() { ['#'] = Hex(0x202020), ['a'] = Hex(0x4A4A4A) });
            Add("particle_angry", p);
        }
    }

    public static void Stamp(uint[] p, string[] rows, Dictionary<char, uint> pal, int ox = 0, int oy = 0)
    {
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                char ch = rows[y][x];
                if (ch == '.' || ch == ' ') continue;
                if (!pal.TryGetValue(ch, out uint c)) continue;
                int px = x + ox, py = y + oy;
                if (px < 0 || px > 15 || py < 0 || py > 15) continue;
                p[py * 16 + px] = c;
            }
    }
}
