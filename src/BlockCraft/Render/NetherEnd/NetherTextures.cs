using BlockCraft.Core;

namespace BlockCraft.Render;

public static partial class TextureGen
{
    static partial void TexNetherEnd()
    {
        uint[] Rock(int seed, uint color, float amount)
        {
            var noise = TileNoise(seed, 8); var p = New(); var r = new Rng(seed);
            for (int i = 0; i < 256; i++) p[i] = Mul(Hex(color), 1 - amount + amount * (noise[i] + r.NextFloat() * 0.6f));
            return p;
        }
        var rack = Rock(187, 0x854238, .7f); Add("netherrack", rack);
        var soul = Rock(188, 0x66503B, .6f);
        for (int y = 2; y < 14; y += 6) for (int x = 2; x < 15; x += 7)
        { soul[y * 16 + x] = Hex(0x291F18); if (x + 2 < 16) soul[y * 16 + x + 2] = Hex(0x291F18); soul[(y + 2) * 16 + x + 1] = Hex(0x33281E); }
        Add("soul_sand", soul);
        var brick = Rock(189, 0x402029, .4f);
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            if (y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0) brick[y * 16 + x] = Hex(0x1D1015);
        Add("nether_brick", brick);
        var ore = (uint[])rack.Clone(); var rng = new Rng(194);
        for (int j = 0; j < 16; j++) { int x = rng.Next(14), y = rng.Next(14); for (int k = 0; k < 3; k++) ore[(y + k) * 16 + x + (k & 1)] = Hex(k == 1 ? 0xF4E9D5u : 0xBFAE99u); }
        Add("quartz_ore", ore);
        Add("quartz_block", Rock(195, 0xEAE5DA, .13f));
        var pillar = Rock(196, 0xEAE5DA, .12f); var chisel = (uint[])pillar.Clone();
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        { if (x % 4 == 1) pillar[y * 16 + x] = Hex(0xC3BFB3); if (x == 3 || x == 12 || y == 3 || y == 12) chisel[y * 16 + x] = Hex(0xA9A497); }
        Add("quartz_pillar", pillar); Add("quartz_chiseled", chisel);
        Add("end_stone", Rock(200, 0xDFE4A5, .35f)); Add("end_sky", Rock(201, 0x302139, .8f));
        Add("end_portal_frame", Rock(202, 0x648477, .5f)); Add("dragon_egg", Rock(203, 0x24172E, .8f));
        for (int stage = 0; stage < 3; stage++)
        {
            var p = New();
            for (int y = 12 - stage * 3; y < 16; y++) for (int x = 1; x < 15; x++)
                if (x % 4 != 0 && (y > 13 || (x + y) % 3 != 0)) p[y * 16 + x] = Hex((x + y) % 2 == 0 ? 0xA52E35u : 0x642328u);
            Add("nether_wart_" + stage, p);
        }
        foreach (var (name, color) in new (string, uint)[] { ("nether_wart",0xA52E35), ("quartz",0xEEE8DB), ("netherbrick",0x5C2932), ("glowstone_dust",0xEBD073), ("blaze_rod",0xFFCB32), ("blaze_powder",0xEFA827), ("ghast_tear",0xDBE9EA), ("magma_cream",0xD17F28), ("ender_eye",0x54987B), ("fire_charge",0xE48827), ("nether_star",0xE3EADA) })
        {
            var p = New();
            for (int y = 2; y < 14; y++) for (int x = 2; x < 14; x++)
            {
                bool inside = name == "blaze_rod" ? Math.Abs(x + y - 15) < 3 : name == "nether_star" ? Math.Abs(x - 8) + Math.Abs(y - 8) < 7 : (x - 8) * (x - 8) + (y - 8) * (y - 8) < 35;
                if (inside) p[y * 16 + x] = Mul(Hex(color), .65f + (15 - x - y) * .025f + rng.NextFloat() * .35f);
            }
            if (name == "ender_eye") for (int y = 5; y <= 10; y++) for (int x = 7; x <= 8; x++) p[y * 16 + x] = Hex(0x12232C);
            Add("item_" + name, p);
        }
        foreach (string key in new[] { "portal", "end_portal", "fire" })
        {
            var frames = new List<uint[]>();
            for (int t = 0; t < 32; t++)
            {
                var p = New();
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    double wave = (Math.Sin(x * .7 + Math.Sin(y * .6 + t * Math.PI / 16) * 2 + t * Math.PI / 16) + 1) * .5;
                    p[y * 16 + x] = key == "portal" ? Rgba(70 + (int)(wave * 90), 20, 130 + (int)(wave * 110), 190)
                        : key == "end_portal" ? Rgba((int)(wave * 15), 12 + (int)(wave * 30), 25 + (int)(wave * 30))
                        : Rgba(255, 70 + (int)(wave * 180), (int)(wave * 40), y > (1 - wave) * 13 ? 255 : 0);
                }
                frames.Add(p);
            }
            Add(key, frames[0]); Animations[key] = frames;
        }
    }
}
