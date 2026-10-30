using BlockCraft.Core;
using BlockCraft.Items;

namespace BlockCraft.Render;

/// <summary>Tekstury modułu OW: warianty kamienia, drewna, rośliny, budulce, itemy (proceduralnie, 16x16).</summary>
public static partial class TextureGen
{
    /// <summary>Nazwy drewien w kolejności MC (BlockPlanks.EnumType): dąb, świerk, brzoza, dżungla, akacja, ciemny dąb.</summary>
    public static readonly string[] WoodNames = { "oak", "spruce", "birch", "jungle", "acacia", "big_oak" };

    static partial void TexOverworld()
    {
        OwStone();
        OwWood();
        OwPlants();
        OwBuilding();
        OwItems();
    }

    // ---------------- pomocnicze ----------------
    private static uint[] Recolor(uint[] src, uint[] pal)
    {
        var p = New();
        for (int i = 0; i < 256; i++)
        {
            uint c = src[i]; if (A(c) == 0) continue;
            float l = (R(c) * 0.3f + G(c) * 0.59f + Bl(c) * 0.11f) / 255f;
            p[i] = WithA(Pick(pal, l), A(c));
        }
        return p;
    }
    private static uint[] Tint(uint[] src, uint rgb)
    {
        var p = New(); uint t = Hex(rgb);
        for (int i = 0; i < 256; i++) { uint c = src[i]; p[i] = Rgba(R(c) * R(t) / 255, G(c) * G(t) / 255, Bl(c) * Bl(t) / 255, A(c)); }
        return p;
    }
    private static uint[] Copy(string name) => (uint[])_tex[_idxBuilding[name]].Clone();
    private static uint[] Smooth(uint[] pal, int seed, bool border)
    {
        var p = Noisy(seed, pal, 0.3f, 0.9f);
        if (border) for (int i = 0; i < 16; i++) { p[i] = Mul(p[i], 1.12f); p[15 * 16 + i] = Mul(p[15 * 16 + i], 0.8f); p[i * 16] = Mul(p[i * 16], 1.1f); p[i * 16 + 15] = Mul(p[i * 16 + 15], 0.82f); }
        return p;
    }
    private static void Px(uint[] p, int x, int y, uint c) { if (x >= 0 && x < 16 && y >= 0 && y < 16) p[y * 16 + x] = c; }

    // ---------------- kamień, ziemia, piaskowce ----------------
    private static void OwStone()
    {
        Add("stone_granite", Noisy(301, Palette(0x8F5F4C, 0x9A6B56, 0xA77A64, 0xB38773, 0x7D5244, 0xC09684), 0.75f, 1.4f));
        Add("stone_granite_smooth", Smooth(Palette(0x93624F, 0x9D6E59, 0xA77965, 0xAF836E), 302, true));
        Add("stone_diorite", Noisy(303, Palette(0x8C8C8E, 0xA9A9AB, 0xBDBDBD, 0xCFCFCF, 0xE0E0E0, 0x7A7A7C), 0.8f, 1.5f));
        Add("stone_diorite_smooth", Smooth(Palette(0xB0B0B2, 0xBEBEC0, 0xC8C8CA, 0xD2D2D4), 304, true));
        Add("stone_andesite", Noisy(305, Palette(0x737373, 0x7F7F7F, 0x888888, 0x8F8F8F, 0x6A6A6A, 0x9A9A9A), 0.7f, 1.3f));
        Add("stone_andesite_smooth", Smooth(Palette(0x7B7B7D, 0x838385, 0x8B8B8D, 0x939395), 306, true));
        Add("coarse_dirt", Noisy(307, Palette(0x5E4129, 0x6E4D31, 0x7B5A3B, 0x8A6847, 0x4F3622, 0x96745A), 0.8f, 1.5f));
        var pod = Noisy(308, Palette(0x4E3416, 0x5C3E1B, 0x6A4920, 0x7A5428), 0.6f, 1.3f);
        Add("dirt_podzol_top", pod);
        var podSide = Copy("dirt");
        for (int y = 0; y < 4; y++) for (int x = 0; x < 16; x++) if (y < 3 || (x * 7 + 3) % 5 < 3) podSide[y * 16 + x] = pod[y * 16 + x];
        Add("dirt_podzol_side", podSide);
        Add("red_sand", Noisy(309, Palette(0xA1531C, 0xAA5A1F, 0xB46024, 0xBF6A2A, 0xC57532, 0x96491A), 0.7f, 1.3f));
        Add("sandstone_carved", Carved(Copy("sandstone_top"), Hex(0xA6995F), Hex(0xE8DFAF)));
        Add("sandstone_smooth", Smooth(Palette(0xD5CC97, 0xDAD19D, 0xDFD6A4), 310, false));
        uint[] rs = Palette(0x9C4F1B, 0xA9561E, 0xB05D22, 0xBA6428, 0xC26D2E);
        Add("red_sandstone_top", Noisy(311, rs, 0.5f, 1.2f));
        Add("red_sandstone_bottom", Recolor(Copy("sandstone_bottom"), rs));
        Add("red_sandstone_normal", Recolor(Copy("sandstone_side"), rs));
        Add("red_sandstone_carved", Carved(Noisy(312, rs, 0.4f, 1f), Hex(0x7A3D14), Hex(0xC97A3B)));
        Add("red_sandstone_smooth", Smooth(rs, 313, false));
        var sb = Copy("stonebrick");
        var mossy = (uint[])sb.Clone(); var r = new Rng(314); var mn = TileNoise(315, 4);
        for (int i = 0; i < 256; i++) if (mn[i] + r.NextFloat() * 0.3f > 0.75f) mossy[i] = Lerp(mossy[i], Hex(r.NextBool() ? 0x5A7A3Au : 0x48662Eu), 0.8f);
        Add("stonebrick_mossy", mossy);
        var cracked = (uint[])sb.Clone();
        int cx = 4, cy = 0; for (int k = 0; k < 22; k++) { Px(cracked, cx, cy, Hex(0x3E3E3E)); cy++; if (r.Next(3) == 0) cx += r.Next(3) - 1; if (cy > 15) { cy = 3; cx = 11; } }
        Add("stonebrick_cracked", cracked);
        Add("stonebrick_carved", Carved(Stone(316), Hex(0x555555), Hex(0xA0A0A0)));
        Add("emerald_ore", Ore(317, Hex(0xC3FFD8), Hex(0x17DD62), Hex(0x007A1F), 4));
        Add("emerald_block", StorageBlock(Hex(0x2ACB57), Hex(0x8BF5AE), Hex(0x0D8C35), 318, true));
    }

    private static uint[] Carved(uint[] p, uint dark, uint light)
    {
        for (int i = 0; i < 16; i++) { p[i] = light; p[15 * 16 + i] = dark; p[i * 16] = light; p[i * 16 + 15] = dark; }
        for (int y = 4; y <= 11; y++) for (int x = 4; x <= 11; x++)
            {
                bool edge = x == 4 || x == 11 || y == 4 || y == 11;
                bool ring = x == 6 || x == 9 || y == 6 || y == 9;
                if (edge) p[y * 16 + x] = (x == 4 || y == 4) ? dark : light;
                else if (ring && x >= 6 && x <= 9 && y >= 6 && y <= 9) p[y * 16 + x] = dark;
            }
        return p;
    }

    // ---------------- drewna ----------------
    private static void OwWood()
    {
        Add("planks_spruce", Planks(Palette(0x7A5A34, 0x6B4E2C, 0x5A4024, 0x86663D), Hex(0x3F2C17), 23));
        Add("planks_jungle", Planks(Palette(0xA77857, 0x9A6B4C, 0x885C3F, 0xB38464), Hex(0x5C3D27), 24));
        Add("planks_acacia", Planks(Palette(0xB05F33, 0xA3562D, 0x8F4A26, 0xBC6A3B), Hex(0x6A3519), 25));
        Add("planks_big_oak", Planks(Palette(0x4A2F16, 0x422A13, 0x37230F, 0x55371B), Hex(0x24170A), 26));
        Add("log_spruce", LogSide(Palette(0x2A1C0E, 0x35240F, 0x3E2B14, 0x48331A, 0x523B1F), 65));
        Add("log_spruce_top", LogTop(Hex(0x3E2B14), Palette(0x7A5A34, 0x6B4E2C, 0x735330, 0x6B4E2C), 66));
        Add("log_jungle", LogSide(Palette(0x4A3912, 0x56441A, 0x5F4B1E, 0x6B5726, 0x3D2F0E), 67));
        Add("log_jungle_top", LogTop(Hex(0x56441A), Palette(0xA77857, 0x9A6B4C, 0xA07252, 0x9A6B4C), 68));
        Add("log_acacia", LogSide(Palette(0x5A5650, 0x67625A, 0x726C63, 0x7E776D, 0x4E4A45), 69));
        Add("log_acacia_top", LogTop(Hex(0x67625A), Palette(0xB05F33, 0xA3562D, 0xAA5A30, 0xA3562D), 70));
        Add("log_big_oak", LogSide(Palette(0x2B1E0F, 0x342513, 0x3C2B16, 0x45321A, 0x22180B), 71));
        Add("log_big_oak_top", LogTop(Hex(0x342513), Palette(0x4A2F16, 0x422A13, 0x46301A, 0x422A13), 72));
        foreach (var (n, s) in new[] { ("spruce", 73), ("jungle", 74), ("acacia", 75), ("big_oak", 76) })
        {
            Add("leaves_" + n, Leaves(s, true));
            Add("leaves_" + n + "_opaque", Leaves(s, false));
        }
        Add("sapling_spruce", Sapling(Hex(0x2E4A2E), Hex(0x1F351F), Hex(0x456B45)));
        Add("sapling_jungle", Sapling(Hex(0x2F7A14), Hex(0x1F5A0C), Hex(0x4DA02A)));
        Add("sapling_acacia", Sapling(Hex(0x6F8A20), Hex(0x55701A), Hex(0x90AA30)));
        Add("sapling_roofed_oak", Sapling(Hex(0x2C5A12), Hex(0x1C420A), Hex(0x3F7A1E)));
        var doorPal = new Dictionary<string, uint[]>
        {
            ["spruce"] = Palette(0x3F2C17, 0x5A4024, 0x6B4E2C, 0x7A5A34, 0x86663D),
            ["birch"] = Palette(0x9E8C5C, 0xB8A46A, 0xC8B57A, 0xD7C185, 0xE8DCB0),
            ["jungle"] = Palette(0x5C3D27, 0x885C3F, 0x9A6B4C, 0xA77857, 0xB38464),
            ["acacia"] = Palette(0x6A3519, 0x8F4A26, 0xA3562D, 0xB05F33, 0xBC6A3B),
            ["dark_oak"] = Palette(0x24170A, 0x37230F, 0x422A13, 0x4A2F16, 0x55371B),
        };
        foreach (var kv in doorPal)
        {
            var lo = Recolor(Copy("door_wood_lower"), kv.Value); var up = Recolor(Copy("door_wood_upper"), kv.Value);
            if (kv.Key is "spruce" or "jungle" or "acacia")
                for (int y = 3; y < 7; y++) for (int x = 3; x < 13; x++) if (A(up[y * 16 + x]) == 0 && kv.Key == "spruce") up[y * 16 + x] = kv.Value[1];
            Add("door_" + kv.Key + "_lower", lo); Add("door_" + kv.Key + "_upper", up);
            var it = New();
            for (int y = 0; y < 16; y++) for (int x = 4; x < 12; x++)
                {
                    var src = y < 8 ? up : lo; int sy = (y % 8) * 2, sx = (x - 4) * 2;
                    it[y * 16 + x] = src[sy * 16 + sx] != 0 ? src[sy * 16 + sx] : 0;
                }
            Add("item_" + kv.Key + "_door", it);
        }
    }

    // ---------------- rośliny ----------------
    private static uint[] Flower(int seed, uint c1, uint c2, uint center, int shape)
    {
        var p = New(); var r = new Rng(seed);
        uint st = Hex(0x337A1A), st2 = Hex(0x4A9A28);
        for (int y = 8; y < 16; y++) p[y * 16 + 7] = st;
        p[12 * 16 + 6] = st2; p[11 * 16 + 5] = st2; p[13 * 16 + 8] = st2; p[12 * 16 + 9] = st2;
        switch (shape)
        {
            case 0: // kielich (tulipan)
                for (int y = 3; y < 9; y++) for (int x = 5; x < 10; x++) { if (y == 3 && (x == 6 || x == 8)) continue; if (y == 8 && (x == 5 || x == 9)) continue; p[y * 16 + x] = (x + y) % 3 == 0 ? c2 : c1; }
                break;
            case 1: // gwiazda (orchidea, houstonia)
                for (int y = 3; y < 10; y++) for (int x = 4; x < 11; x++)
                    {
                        int dx = Math.Abs(x - 7), dy = Math.Abs(y - 6);
                        if (dx + dy <= 3 && (dx == 0 || dy == 0 || dx + dy <= 2)) p[y * 16 + x] = r.NextBool() ? c1 : c2;
                    }
                p[6 * 16 + 7] = center;
                break;
            case 2: // kula (czosnek)
                for (int y = 2; y < 9; y++) for (int x = 4; x < 11; x++) { float dx = x - 7, dy = y - 5; if (dx * dx + dy * dy < 10) p[y * 16 + x] = (x * 3 + y) % 4 == 0 ? c2 : c1; }
                break;
            default: // stokrotka
                for (int y = 2; y < 11; y++) for (int x = 3; x < 12; x++) { float dx = x - 7, dy = y - 6; float d = dx * dx + dy * dy; if (d < 17 && d > 2.5f && ((x + y) & 1) == 0 || d < 9 && d > 2.5f) p[y * 16 + x] = r.Next(4) == 0 ? c2 : c1; }
                for (int y = 5; y < 8; y++) for (int x = 6; x < 9; x++) p[y * 16 + x] = center;
                break;
        }
        return p;
    }

    private static uint[] Crop(int stage, int maxStage, uint leaf, uint fruit, bool root)
    {
        var p = New(); var r = new Rng(900 + stage * 7 + (int)(fruit & 255));
        int h = 3 + stage * 10 / maxStage;
        int[] xs = { 2, 5, 9, 12 };
        foreach (int bx in xs)
            for (int y = 15; y > 15 - h; y--)
            {
                int x = bx + ((15 - y) % 3 == 2 ? 1 : 0);
                p[y * 16 + x] = r.Next(3) == 0 ? Mul(leaf, 0.8f) : leaf;
                if (y < 14 && r.Next(3) == 0) Px(p, x + (r.NextBool() ? 1 : -1), y, Mul(leaf, 1.15f));
            }
        if (stage == maxStage && root)
            foreach (int bx in xs) { Px(p, bx, 15, fruit); Px(p, bx + 1, 15, Mul(fruit, 0.85f)); Px(p, bx, 14, Mul(fruit, 1.1f)); }
        return p;
    }

    private static void OwPlants()
    {
        Add("flower_blue_orchid", Flower(401, Hex(0x2ABFFD), Hex(0x1A8FD8), Hex(0x8ADFFF), 1));
        Add("flower_allium", Flower(402, Hex(0xB878EC), Hex(0x8F4FC9), Hex(0xE0B8FF), 2));
        Add("flower_houstonia", Flower(403, Hex(0xE8EEF8), Hex(0xC0CCE0), Hex(0xF4E06A), 1));
        Add("flower_tulip_red", Flower(404, Hex(0xE52A1C), Hex(0xB81C12), 0, 0));
        Add("flower_tulip_orange", Flower(405, Hex(0xF07D1F), Hex(0xC85E12), 0, 0));
        Add("flower_tulip_white", Flower(406, Hex(0xF2F2F2), Hex(0xCCD8CC), 0, 0));
        Add("flower_tulip_pink", Flower(407, Hex(0xF2A9D3), Hex(0xD87FB2), 0, 0));
        Add("flower_oxeye_daisy", Flower(408, Hex(0xF4F4F4), Hex(0xD8D8D8), Hex(0xF2CB2A), 3));
        var fern = New(); var fr = new Rng(409);
        for (int k = 0; k < 5; k++)
        {
            float ang = -1.1f + k * 0.55f; float x = 7.5f, y = 15;
            for (int s = 0; s < 13; s++)
            {
                x += MathF.Sin(ang) * 0.9f; y -= MathF.Cos(ang) * 0.9f; ang *= 1.04f;
                Px(fern, (int)x, (int)y, Gray(0.5f + fr.NextFloat() * 0.25f));
                if (s % 2 == 1) { Px(fern, (int)x - 1, (int)y, Gray(0.65f)); Px(fern, (int)x + 1, (int)y, Gray(0.6f)); }
            }
        }
        Add("fern", fern);
        // podwójne rośliny
        var tg = Copy("tallgrass");
        var tgTop = New(); var tgBot = New();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { tgTop[y * 16 + x] = tg[(y / 2 + 8) * 16 + x] != 0 && y > 3 ? tg[(y / 2 + 8) * 16 + x] : (y > 6 ? tg[y * 16 + x] : 0); tgBot[y * 16 + x] = tg[y * 16 + x] != 0 ? tg[y * 16 + x] : tg[((y + 7) & 15) * 16 + x]; }
        Add("double_plant_grass_top", tg); Add("double_plant_grass_bottom", tgBot);
        Add("double_plant_fern_top", Copy("fern")); var fb = Copy("fern"); for (int i = 0; i < 256; i++) if (fb[i] == 0) fb[i] = fern[(i + 128) & 255]; Add("double_plant_fern_bottom", fb);
        var stalk = New(); for (int y = 0; y < 16; y++) { stalk[y * 16 + 7] = Hex(0x3E7A1E); stalk[y * 16 + 8] = Hex(0x2F6316); } for (int k = 0; k < 4; k++) { Px(stalk, 5, 3 + k * 4, Hex(0x4A9A28)); Px(stalk, 10, 5 + k * 4, Hex(0x4A9A28)); Px(stalk, 6, 4 + k * 4, Hex(0x4A9A28)); Px(stalk, 9, 6 + k * 4, Hex(0x4A9A28)); }
        Add("double_plant_sunflower_bottom", stalk);
        var sunTop = New(); for (int y = 8; y < 16; y++) { sunTop[y * 16 + 7] = Hex(0x3E7A1E); sunTop[y * 16 + 8] = Hex(0x2F6316); } Px(sunTop, 6, 10, Hex(0x4A9A28)); Px(sunTop, 9, 12, Hex(0x4A9A28));
        Add("double_plant_sunflower_top", sunTop);
        var sunFront = New();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float dx = x - 7.5f, dy = y - 7.5f, d = MathF.Sqrt(dx * dx + dy * dy); if (d < 4) sunFront[y * 16 + x] = (x + y) % 2 == 0 ? Hex(0x5A3A12) : Hex(0x7A4E1A); else if (d < 7.5f && (MathF.Atan2(dy, dx) * 8 / MathF.PI + 16) % 2 < 1.4f) sunFront[y * 16 + x] = d < 6 ? Hex(0xFFD21C) : Hex(0xF2B807); }
        Add("double_plant_sunflower_front", sunFront);
        var sunBack = New(); for (int i = 0; i < 256; i++) if (sunFront[i] != 0) sunBack[i] = Hex((i & 3) == 0 ? 0x5C8A20u : 0x4A7A18u); Add("double_plant_sunflower_back", sunBack);
        DoubleFlower("syringa", Hex(0xD8A6E0), Hex(0xB57EC2), 410);
        DoubleFlower("rose", Hex(0xD9241C), Hex(0x9E1610), 411);
        DoubleFlower("paeonia", Hex(0xF0C6E8), Hex(0xD49ACB), 412);
        // grzyby
        var mb = New(); for (int y = 9; y < 15; y++) { mb[y * 16 + 7] = Hex(0xD9CDBA); mb[y * 16 + 8] = Hex(0xC4B79F); }
        var mr = (uint[])mb.Clone();
        for (int y = 6; y < 10; y++) for (int x = 4; x < 12; x++) { if (y == 6 && (x < 6 || x > 9)) continue; mb[y * 16 + x] = (x + y) % 3 == 0 ? Hex(0x8C6A4F) : Hex(0x9E7B5C); }
        for (int y = 5; y < 10; y++) for (int x = 4; x < 12; x++) { if (y == 5 && (x < 6 || x > 9)) continue; mr[y * 16 + x] = Hex(0xD3241E); }
        Px(mr, 6, 6, Hex(0xFFFFFF)); Px(mr, 9, 7, Hex(0xFFFFFF)); Px(mr, 5, 8, Hex(0xFFFFFF)); Px(mr, 10, 9, Hex(0xFFFFFF));
        Add("mushroom_brown", mb); Add("mushroom_red", mr);
        Add("mushroom_block_skin_brown", Noisy(413, Palette(0x8A6648, 0x93704F, 0x9C7B58, 0xA58463), 0.4f, 1.1f));
        var redSkin = Noisy(414, Palette(0xB71D18, 0xC4211B, 0xCD2620), 0.3f, 1f); var rr = new Rng(415);
        for (int k = 0; k < 6; k++) { int x = rr.Next(14), y = rr.Next(14); for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 3; dx++) Px(redSkin, x + dx, y + dy, Hex(0xE8E2D6)); }
        Add("mushroom_block_skin_red", redSkin);
        Add("mushroom_block_skin_stem", Noisy(416, Palette(0xC8C0AE, 0xD2CAB8, 0xDCD4C2, 0xE4DDCC), 0.4f, 1.1f));
        Add("mushroom_block_inside", Noisy(417, Palette(0xC9A97A, 0xD4B585, 0xDEC093), 0.5f, 1.1f));
        Add("mycelium_top", Noisy(418, Palette(0x6A5A68, 0x766474, 0x7F6E7C, 0x8C7A89, 0x5E505D, 0x9A8697), 0.75f, 1.5f));
        var mySide = Copy("dirt"); var myTop = _tex[_idxBuilding["mycelium_top"]];
        for (int y = 0; y < 5; y++) for (int x = 0; x < 16; x++) if (y < 2 || (x * 5 + y * 3) % 7 < 7 - y * 2) mySide[y * 16 + x] = myTop[y * 16 + x];
        Add("mycelium_side", mySide);
        var vine = New(); var vr = new Rng(419);
        for (int k = 0; k < 6; k++) { int x = vr.Next(16), y = 0; while (y < 16) { Px(vine, x, y, Gray(0.45f + vr.NextFloat() * 0.2f)); if (vr.Next(3) == 0) { Px(vine, x + 1, y, Gray(0.6f)); Px(vine, x - 1, y + 1, Gray(0.55f)); } y++; if (vr.Next(4) == 0) x = (x + vr.Next(3) + 15) & 15; } }
        Add("vine", vine);
        var lily = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float dx = x - 7.5f, dy = y - 7.5f; double a = Math.Atan2(dy, dx); if (dx * dx + dy * dy < 56 && !(a > 0.15 && a < 0.65)) lily[y * 16 + x] = Gray((x * 7 + y * 3) % 5 == 0 ? 0.55f : 0.7f); }
        Add("waterlily", lily);
        for (int s = 0; s < 3; s++)
        {
            var c = New(); int w = 4 + s * 2, h = 5 + s * 2, x0 = 8 - w / 2;
            for (int y = 1; y < 1 + h; y++) for (int x = x0; x < x0 + w; x++) c[y * 16 + x] = Hex(s == 0 ? 0x8B9A3Cu : s == 1 ? 0xA8762Fu : 0x8F4D1Fu) is var col && (x + y) % 4 == 0 ? Mul(col, 0.8f) : Hex(s == 0 ? 0x8B9A3Cu : s == 1 ? 0xA8762Fu : 0x8F4D1Fu);
            Px(c, 8, 0, Hex(0x5A6B20));
            Add("cocoa_stage_" + s, c);
        }
        var pumpSide = New(); var pr = new Rng(420);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { uint c = (x % 4 == 0) ? Hex(0xC06A0C) : Hex(0xE38A1D); p(pumpSide, x, y, Mul(c, 0.92f + pr.NextFloat() * 0.12f)); }
        Add("pumpkin_side", pumpSide);
        var pumpTop = (uint[])pumpSide.Clone(); for (int y = 6; y < 10; y++) for (int x = 6; x < 10; x++) pumpTop[y * 16 + x] = Hex(0x6B5A1E);
        Add("pumpkin_top", pumpTop);
        var face = (uint[])pumpSide.Clone(); var faceOn = (uint[])pumpSide.Clone();
        string[] fp = { "................", "................", "................", "...##......##...", "..####....####..", "..####....####..", "................", ".......##.......", "................", "..############..", "..############..", "...##..##..##...", "................", "................", "................", "................" };
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) if (fp[y][x] == '#') { face[y * 16 + x] = Hex(0x2A1A05); faceOn[y * 16 + x] = Hex((x + y) % 3 == 0 ? 0xFFF08Au : 0xF2C43A); }
        Add("pumpkin_face_off", face); Add("pumpkin_face_on", faceOn);
        var melSide = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) p(melSide, x, y, ((x + y / 5) % 4 < 2) ? Hex(0x6C9B1E) : Hex(0x8BB82E));
        Add("melon_side", melSide);
        var melTop = Noisy(421, Palette(0x6C9B1E, 0x7AA826, 0x8BB82E), 0.4f, 1f); Add("melon_top", melTop);
        var stem = New(); for (int y = 0; y < 16; y++) { Px(stem, 7, y, Gray(0.7f)); if (y % 4 == 1) Px(stem, 6, y, Gray(0.6f)); if (y % 4 == 3) Px(stem, 8, y, Gray(0.6f)); }
        Add("pumpkin_stem_disconnected", stem);
        var stemC = New(); for (int y = 6; y < 16; y++) Px(stemC, 7, y, Gray(0.7f)); for (int x = 7; x < 16; x++) Px(stemC, x, 6 - (x - 7) / 3, Gray(0.75f));
        Add("pumpkin_stem_connected", stemC);
        for (int s = 0; s < 4; s++)
        {
            Add("carrots_stage_" + s, Crop(s, 3, Hex(0x3E9A28), Hex(0xF08A1D), true));
            Add("potatoes_stage_" + s, Crop(s, 3, Hex(0x4CA834), Hex(0xC9A85A), true));
        }
        Add("web", Web());
    }
    private static void p(uint[] a, int x, int y, uint c) => a[y * 16 + x] = c;

    private static void DoubleFlower(string name, uint c1, uint c2, int seed)
    {
        var bot = New(); var r = new Rng(seed);
        for (int y = 0; y < 16; y++) for (int x = 2; x < 14; x++) if (r.Next(5) < 2 && (x + y) % 2 == 0) bot[y * 16 + x] = r.NextBool() ? Hex(0x3E7A1E) : Hex(0x4A9A28);
        for (int y = 0; y < 16; y++) bot[y * 16 + 7] = Hex(0x2F6316);
        var top = New();
        for (int y = 8; y < 16; y++) top[y * 16 + 7] = Hex(0x2F6316);
        for (int y = 1; y < 12; y++) for (int x = 3; x < 13; x++) { float dx = x - 7.5f, dy = (y - 6) * 1.1f; if (dx * dx + dy * dy < 22 + r.Next(6)) top[y * 16 + x] = r.Next(3) == 0 ? c2 : c1; }
        Add("double_plant_" + name + "_bottom", bot); Add("double_plant_" + name + "_top", top);
    }

    private static uint[] Web()
    {
        var p = New(); uint c = Hex(0xDCDCDC, 220);
        for (int i = 0; i < 16; i++) { Px(p, i, i, c); Px(p, 15 - i, i, c); Px(p, 7, i, c); Px(p, i, 7, c); }
        for (int k = 2; k < 8; k += 2) for (int i = -k; i <= k; i++) { Px(p, 7 + i, 7 - k + Math.Abs(i) / 2, c); Px(p, 7 + i, 7 + k - Math.Abs(i) / 2, c); }
        return p;
    }

    // ---------------- budulce ----------------
    private static void OwBuilding()
    {
        for (int i = 0; i < 16; i++) Add("wool_colored_" + i, Wool(Hex(WoolRgb[i]), 330 + i));
        var hc = Noisy(331, Palette(0x8E5A41, 0x965F44, 0x9A6247, 0xA0664A), 0.25f, 0.8f);
        Add("hardened_clay", hc);
        for (int i = 0; i < 16; i++) Add("hardened_clay_stained_" + i, Noisy(340 + i, Palette(Mul3(ClayRgb[i], 0.94f), ClayRgb[i], Mul3(ClayRgb[i], 1.04f)), 0.25f, 0.8f));
        var glass = Copy("glass");
        for (int i = 0; i < 16; i++)
        {
            var g = New(); uint dc = Hex(Dyes.Rgb[i]);
            for (int k = 0; k < 256; k++) { int x = k & 15, y = k >> 4; bool edge = x == 0 || y == 0 || x == 15 || y == 15; g[k] = edge ? WithA(Mul(dc, 0.85f), 230) : WithA(dc, (x + y) % 9 == 0 ? 170 : 115); }
            Add("glass_" + i, g);
        }
        var paneTop = New(); for (int y = 0; y < 16; y++) for (int x = 7; x < 9; x++) paneTop[y * 16 + x] = Hex(0xD8E8EE, 230);
        Add("glass_pane_top", paneTop);
        var bars = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { if (x % 4 == 1 || x % 4 == 2) bars[y * 16 + x] = x % 4 == 1 ? Hex(0x9A9A9A) : Hex(0x6E6E6E); if (y == 0 || y == 15 || y == 7) bars[y * 16 + x] = Hex(0x8A8A8A); }
        Add("iron_bars", bars);
        var hay = New(); var hr = new Rng(332);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { uint c = hr.Next(3) == 0 ? Hex(0xC8A21C) : hr.NextBool() ? Hex(0xB8921A) : Hex(0xD6B230); if (y == 3 || y == 12) c = Hex(0x7A3E12); p(hay, x, y, c); }
        Add("hay_block_side", hay);
        var hayTop = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float d = MathF.Max(MathF.Abs(x - 7.5f), MathF.Abs(y - 7.5f)); p(hayTop, x, y, ((int)d % 2 == 0) ? Hex(0xC8A21C) : Hex(0xB08A16)); }
        Add("hay_block_top", hayTop);
        Add("ice_packed", Noisy(333, Palette(0x8CB4F5, 0x96BCF8, 0xA0C4FA, 0xACCCFC), 0.3f, 0.9f));
        Add("sponge", Sponge(334, Palette(0xC3C04B, 0xCDCA55, 0xD6D35E, 0xA9A63B)));
        Add("sponge_wet", Sponge(335, Palette(0x9C9A2E, 0xA6A436, 0xB0AE3E, 0x7F7D20)));
        Add("prismarine_rough", Noisy(336, Palette(0x5C9A88, 0x63A493, 0x6CAE9C, 0x4E8A7A, 0x7AB8A6, 0x5A8F96), 0.7f, 1.4f));
        var pb = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { bool seam = y % 8 == 7 || (x + (y / 8) * 8) % 16 == 15; p(pb, x, y, seam ? Hex(0x3E7A6A) : Hex(((x * 3 + y * 5) % 7 == 0) ? 0x7CC0AEu : 0x63AC9A)); }
        Add("prismarine_bricks", pb);
        var pd = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { bool seam = x == 0 || y == 0 || x == 15 || y == 15 || (x == 7 && y > 3 && y < 12) || (y == 7 && x > 3 && x < 12); p(pd, x, y, seam ? Hex(0x203C30) : Hex((x + y) % 5 == 0 ? 0x3A6650u : 0x335B49)); }
        Add("prismarine_dark", pd);
        var sl = New(); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { bool b = x == 0 || y == 0 || x == 15 || y == 15; float d = MathF.Abs(x - 7.5f) + MathF.Abs(y - 7.5f); p(sl, x, y, b ? Hex(0x9DB8B0) : d < 4 ? Hex(0xFFFFFF) : d < 8 ? Hex(0xDCEEE8) : Hex(0xB9D6CC)); }
        Add("sea_lantern", sl);
    }

    private static uint Mul3(uint rgb, float f) => (uint)(Math.Min(255, (int)(((rgb >> 16) & 255) * f)) << 16 | Math.Min(255, (int)(((rgb >> 8) & 255) * f)) << 8 | Math.Min(255, (int)((rgb & 255) * f)));

    private static uint[] Sponge(int seed, uint[] pal)
    {
        var p = Noisy(seed, pal, 0.5f, 1.2f); var r = new Rng(seed + 1);
        for (int k = 0; k < 14; k++) { int x = r.Next(15), y = r.Next(15); p[y * 16 + x] = pal[3]; if (r.NextBool()) p[y * 16 + x + 1] = Mul(pal[3], 0.85f); }
        return p;
    }

    /// <summary>Kolory wełny MC 1.8 (tekstury wool_colored_*), kolejność EnumDyeColor.</summary>
    public static readonly uint[] WoolRgb = { 0xE9ECEC, 0xEA7E35, 0xBE49C9, 0x6B8AC9, 0xC2B51C, 0x41AE38, 0xD08499, 0x404040,
        0x9EA6A6, 0x2E6E89, 0x7E3DB5, 0x2E388D, 0x4F321F, 0x35461B, 0x963430, 0x191616 };
    /// <summary>Kolory barwionej utwardzonej gliny MC 1.8.</summary>
    public static readonly uint[] ClayRgb = { 0xD1B1A1, 0xA15325, 0x95576C, 0x706C8A, 0xBA8523, 0x677534, 0xA14E4E, 0x392A23,
        0x876B62, 0x575B5B, 0x764656, 0x4A3B5B, 0x4D3323, 0x4C532A, 0x8F3D2E, 0x251610 };

    // ---------------- itemy ----------------
    private static void OwItems()
    {
        Add("item_emerald", Sprite(new[] {
            "................", "......oooo......", ".....ollllo.....", "....olllmmmo....", "...olllmmmmmo...", "...ollmmmmmdo...", "..olmmmmmmmddo..",
            "..olmmmmmmmddo..", "..olmmmmmmddddo.", "...ommmmmmddo...", "...ommmmmddo....", "....ommmddo.....", ".....omddo......", "......odo.......", ".......o........", "................" },
            Pal(('o', 0x0B5A22), ('l', 0xC3FFD8), ('m', 0x17DD62), ('d', 0x009A2A))));
        Add("item_snowball", Sprite(new[] {
            "................", "................", "................", ".....oooooo.....", "....owwwwwwo....", "...owwwlwwwso...", "...owwllwwwso...",
            "...owwwwwwsso...", "...owwwwwssso...", "...owwwwsssso...", "....owssssso....", ".....oooooo.....", "................", "................", "................", "................" },
            Pal(('o', 0x9AB4C8), ('w', 0xF4FAFF), ('l', 0xFFFFFF), ('s', 0xD4E4F0))));
        Add("item_carrot", Sprite(new[] {
            "................", ".........gg.g...", "........gGgg....", ".........gGg....", "........ooGg....", ".......oOOo.....", "......oOOoo.....",
            ".....oOOOo......", ".....oOoOo......", "....oOOOo.......", "....oOOo........", "...oOoo.........", "...oOo..........", "..oo............", "................", "................" },
            Pal(('o', 0xB45A0C), ('O', 0xF08A1D), ('g', 0x3E9A28), ('G', 0x5CBF3A))));
        string[] potato = { "................", "................", "................", "......oooo......", ".....oPPPPo.....", "....oPPpPPPo....", "...oPPPPPPPPo...",
            "...oPPPPPpPPo...", "...oPpPPPPPPo...", "...oPPPPPPPo....", "....oPPPpPPo....", ".....oPPPPo.....", "......oooo......", "................", "................", "................" };
        Add("item_potato", Sprite(potato, Pal(('o', 0x7A5A22), ('P', 0xD9B868), ('p', 0xA8853C))));
        Add("item_potato_baked", Sprite(potato, Pal(('o', 0x6B3E0E), ('P', 0xE2A23A), ('p', 0xB06E1E))));
        Add("item_potato_poisonous", Sprite(potato, Pal(('o', 0x4F5A1A), ('P', 0xB9C25A), ('p', 0x7E8A2A))));
        Add("item_melon", Sprite(new[] {
            "................", "................", "................", "................", "..............g.", ".............gg.", "...........rgg..",
            ".........rrrgg..", ".......rrbrrgg..", ".....rrrrrrrg...", "...rrbrrrrbgg...", "..rrrrrrrrgg....", "..gggggggggg....", "................", "................", "................" },
            Pal(('r', 0xD62B1E), ('b', 0x2A1A0A), ('g', 0x6C9B1E))));
        string[] seed = { "................", "................", "................", "................", "................", "......s...s.....", "....s....s......",
            ".......s....s...", "...s.....s......", "......s.....s...", "....s...s.......", ".......s...s....", "................", "................", "................", "................" };
        Add("item_seeds_pumpkin", Sprite(seed, Pal(('s', 0xE3D7A0))));
        Add("item_seeds_melon", Sprite(seed, Pal(('s', 0x2A2010))));
        Add("item_pumpkin_pie", Sprite(new[] {
            "................", "................", "................", "................", "................", "....cccccccc....", "...cOOOOOOOOc...",
            "..cOOoOOOOoOOc..", "..cOOOOOOOOOOc..", "..ccccccccccc...", "..cCCCCCCCCCCc..", "...cCCCCCCCCc...", "....cccccccc....", "................", "................", "................" },
            Pal(('c', 0x8A5418), ('O', 0xE38A1D), ('o', 0xF4C25A), ('C', 0xD9A55A))));
        for (int m = 0; m < 16; m++)
        {
            int color = 15 - m;
            uint c = Dyes.Rgb[color];
            if (m == 3) c = 0x6B3E1E;
            Add("item_dye_" + m, Sprite(new[] {
                "................", "................", "................", "................", ".......oo.......", "......oddo......", ".....odmmdo.....",
                "....odmmmmdo....", "...odmmlmmmdo...", "...odmmmmmmdo...", "...oddmmmmddo...", "....oddddddo....", ".....oooooo.....", "................", "................", "................" },
                new Dictionary<char, uint> { ['o'] = Hex(Mul3(c, 0.45f)), ['d'] = Hex(Mul3(c, 0.75f)), ['m'] = Hex(c), ['l'] = Hex(Mul3(c, 1.3f)) }));
        }
    }
}
