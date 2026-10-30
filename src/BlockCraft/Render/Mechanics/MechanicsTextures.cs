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
        var liquid = New(); for (int y = 9; y < 14; y++) for (int x = 5; x < 11; x++) liquid[y * 16 + x] = Hex(0xFFFFFF); Add("me_potion_liquid", liquid);
        foreach (var (key, color) in new[] { ("fermented_spider_eye", 0x6E382Fu), ("speckled_melon", 0xECA32Bu), ("golden_carrot", 0xF6C33Bu), ("brewing_stand", 0xA48A55u), ("cauldron", 0x555761u) })
        {
            var px = New(); for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++) if (Math.Abs(x - 8) + Math.Abs(y - 8) < 9) px[y * 16 + x] = Hex(color);
            Add("item_" + key, px);
        }
    }
    private static uint[] MeTile(uint color, int seed)
    { var px = New(); var noise = TileNoise(seed, 8); for (int i = 0; i < 256; i++) px[i] = Mul(Hex(color), .8f + noise[i] * .3f); return px; }
}
