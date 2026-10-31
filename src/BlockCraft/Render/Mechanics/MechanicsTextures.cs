namespace BlockCraft.Render;

public static partial class TextureGen
{
    static partial void TexMechanics()
    {
        Add("me_brewing", MeTile(0x897044, 71)); Add("me_cauldron", MeTile(0x43444A, 72));
        foreach (string key in new[] { "glass_bottle", "potion", "splash_potion", "experience_bottle" })
        {
            var px = New();
            for (int y = 2; y < 15; y++) for (int x = 3; x < 13; x++)
            {
                bool body = y >= 7 && ((x - 7.5) * (x - 7.5) + (y - 10) * (y - 10) < 23);
                if (body || y < 8 && x >= 6 && x <= 9) px[y * 16 + x] = Hex(key == "experience_bottle" ? 0x86AF38u : 0xB1C6D6u, body && x > 4 && x < 11 ? 100 : 255);
                if (y == 2 && x >= 6 && x <= 9) px[y * 16 + x] = Hex(0x93643C);
            }
            Add("item_" + key, px);
        }
        TexMechanicsItems();
        TexEnchanting();
        var liquid = New(); for (int y = 9; y < 14; y++) for (int x = 5; x < 11; x++) liquid[y * 16 + x] = Hex(0xFFFFFF); Add("me_potion_liquid", liquid);
        foreach (var (key, color) in new[] { ("fermented_spider_eye", 0x6E382Fu), ("speckled_melon", 0xECA32Bu), ("golden_carrot", 0xF6C33Bu), ("brewing_stand", 0xA48A55u), ("cauldron", 0x555761u) })
        {
            var px = New(); for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++) if (Math.Abs(x - 8) + Math.Abs(y - 8) < 9) px[y * 16 + x] = Hex(color);
            Add("item_" + key, px);
        }
    }
    private static void TexEnchanting()
    {
        var noise = TileNoise(91, 8); var obs = Get16("obsidian");
        var top = New();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        {
            int i = y * 16 + x; bool edge = x == 0 || y == 0 || x == 15 || y == 15;
            bool corner = (x < 3 || x > 12) && (y < 3 || y > 12);
            uint c = corner ? (x + y) % 2 == 0 ? Hex(0x5DECF5) : Hex(0x2CB5C0) : edge ? Hex(0x1A0A0A)
                : Math.Abs(x - 7.5) < 3 && Math.Abs(y - 7.5) < 3 ? Hex(0x5A1414) : Hex(0x8F2424);
            top[i] = corner || edge ? c : Mul(c, .85f + noise[i] * .25f);
        }
        Add("me_enchant_top", top);
        var side = New();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        {
            int i = y * 16 + x;
            if (y < 8) side[i] = y == 7 && x % 3 == 1 ? Hex(0x5A1414) : Mul(Hex(0x8F2424), .85f + noise[i] * .25f);
            else if (y == 8 && (x is 1 or 2 or 13 or 14)) side[i] = Hex(0x5DECF5);
            else side[i] = obs[i];
        }
        Add("me_enchant_side", side);
    }
    private static void TexMechanicsItems()
    {
        Add("item_fishing_rod", Sprite(new[]
        {
            "..............o.", ".............olo", "............olo.", "...........olo.w", "..........olo..w", ".........olo...w",
            "........olo....w", ".......olo.....w", "......olo......w", ".....olo.......w", "....olo........w", "...olo.........w",
            "..olo..........w", ".olo..........ww", "olo...........w.", ".o..............",
        }, Pal(('o', 0x2E1D0B), ('l', 0x7A5428), ('w', 0xDDDDDD))));
        var book = (uint[])Get16("item_book").Clone();
        for (int i = 0; i < 256; i++) if (A(book[i]) > 0)
        { uint c = book[i]; float l = (R(c) + G(c) + Bl(c)) / 765f; book[i] = Rgba((int)(120 * l + 60), (int)(40 * l + 20), (int)(170 * l + 70), 255); }
        Add("item_enchanted_book", book);
        var potato = Pal(('o', 0x5A3B12), ('d', 0x9B6A26), ('m', 0xC9923C), ('l', 0xE8C06A));
        string[] potatoRows =
        {
            "................", "................", "................", "......oooo......", ".....oddmmo.....", "....odmmmlmo....",
            "...odmmmmmlmo...", "...odmmmmmmmo...", "...oddmmmmmmo...", "....odmmmmmo....", "....oddmmmmo....", ".....oddddo.....",
            "......oooo......", "................", "................", "................",
        };
        Add("item_baked_potato", Sprite(potatoRows, potato));
        Add("item_poisonous_potato", Sprite(potatoRows, Pal(('o', 0x34400F), ('d', 0x6E8526), ('m', 0x9DB43A), ('l', 0xC9D86A))));
        string[] seedRows =
        {
            "................", "................", "................", "....o......o....", "...olo....olo...", "...olo.....o....",
            "....o...o.......", "........olo.....", "..o.....olo..o..", ".olo.....o..olo.", ".olo.........o..", "..o....o........",
            "......olo.......", "......olo.......", ".......o........", "................",
        };
        Add("item_pumpkin_seeds", Sprite(seedRows, Pal(('o', 0x6E5A2A), ('l', 0xE3D9A0))));
        Add("item_melon_seeds", Sprite(seedRows, Pal(('o', 0x1E1A10), ('l', 0x5C4A2A))));
    }
    private static uint[] MeTile(uint color, int seed)
    { var px = New(); var noise = TileNoise(seed, 8); for (int i = 0; i < 256; i++) px[i] = Mul(Hex(color), .8f + noise[i] * .3f); return px; }
}
