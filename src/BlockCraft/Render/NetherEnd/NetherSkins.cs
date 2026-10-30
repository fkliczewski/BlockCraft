namespace BlockCraft.Render;

/// <summary>Proceduralne skórki mobów Netheru i Endu w układzie UV tekstur MC 1.8.</summary>
public static partial class SkinGen
{
    static partial void SkinsNetherEnd()
    {
        Ghast(false); Ghast(true); BlazeSkin(); MagmaCubeSkin(); ZombiePigmanSkin(); WitherSkeletonSkin(); EndermiteSkin();
    }

    private static void Ghast(bool shooting)
    {
        var c = new Canvas(187); var r = c.R;
        uint body = 0xF0F0F0, shade = 0xD8D8D8;
        // macki (UV 0,0) leżą w pustym narożniku układu korpusu
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) c.Set(x, y, V(r, y > 12 ? 0xC8C8C8u : body, .08f));
        c.Box(0, 0, 16, 16, 16, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                // oczy: zamknięte (kreski) albo otwarte przy strzale; usta w dolnej części
                bool eyeRow = shooting ? y >= 5 && y <= 7 : y == 6;
                if (eyeRow && (x >= 3 && x <= 5 || x >= 10 && x <= 12)) return Hx(shooting ? 0x8A0E0Eu : 0x5A5A5Au);
                if (shooting && y == 5 && (x == 4 || x == 11)) return Hx(0x2A0505);
                bool mouth = shooting ? y >= 10 && y <= 13 && x >= 5 && x <= 10 : y == 11 && x >= 6 && x <= 9;
                if (mouth) return Hx(shooting ? 0x3A0A0Au : 0x6A6A6Au);
                if (y >= 8 && y <= 9 && (x == 3 || x == 12)) return V(r, 0xC9C9C9, .05f);
            }
            if (f == 1) return V(r, shade, .08f);
            return V(r, body, .07f);
        });
        Add(shooting ? "ghast_shooting" : "ghast", c);
    }

    private static void BlazeSkin()
    {
        var c = new Canvas(188); var r = c.R;
        uint yellow = 0xFFD326, orange = 0xE8890C, dark = 0xA55400;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 3 && (x is 1 or 2 or 5 or 6)) return Hx(0x2B1A00);
                if (y == 4 && (x is 1 or 2 or 5 or 6)) return Hx(0x5C3300);
                if (y == 6 && x >= 2 && x <= 5) return Hx(dark);
            }
            return V(r, (x + y + f) % 3 == 0 ? orange : yellow, .2f);
        });
        c.Box(0, 16, 2, 8, 2, (f, x, y, w, h) => V(r, y % 3 == 0 ? orange : yellow, .2f));
        Add("blaze", c);
    }

    private static void MagmaCubeSkin()
    {
        var c = new Canvas(189); var r = c.R;
        // plasterki nachodzą na siebie w UV (jak oryginalna tekstura), więc całe pole 64x32 jest skorupą magmy
        for (int y = 0; y < 16; y++) for (int x = 0; x < 64; x++)
        {
            int n = r.Next(10);
            c.Set(x, y, n < 2 ? V(r, 0xFF8A1C, .2f) : n < 4 ? V(r, 0x7A1A0A, .2f) : V(r, 0x3A0E08, .3f));
        }
        for (int y = 16; y < 32; y++) for (int x = 24; x < 64; x++)
        {
            int n = r.Next(10);
            c.Set(x, y, n < 2 ? V(r, 0xFF8A1C, .2f) : n < 4 ? V(r, 0x7A1A0A, .2f) : V(r, 0x3A0E08, .3f));
        }
        // oczy na przodzie plasterków 2 i 3 (UV 24,10 i 24,19: przód w (32, 18) i (32, 27))
        foreach (int ey in new[] { 18, 27 }) { c.Rect(33, ey, 2, 1, Hx(0xFFD24A)); c.Rect(37, ey, 2, 1, Hx(0xFFD24A)); }
        c.Box(0, 16, 4, 4, 4, (f, x, y, w, h) => V(r, (x + y) % 2 == 0 ? 0xFFE35A : 0xFFA21E, .15f));
        Add("magma_cube", c);
    }

    private static void ZombiePigmanSkin()
    {
        var c = new Canvas(190); var r = c.R;
        uint pink = 0xE59A93, pinkD = 0xC97A74, rot = 0x5F8A3C, bone = 0xD8D2C0, cloth = 0x6B4A2A;
        Humanoid(c,
            (f, x, y, w, h) =>
            {
                if (f == 3)
                {
                    if (y == 3 && (x == 1 || x == 2)) return Hx(0x1A1A1A);
                    if (y == 3 && (x == 5 || x == 6)) return V(r, bone);
                    if (y >= 4 && y <= 6 && x >= 2 && x <= 5) return y == 5 && (x == 3 || x == 4) ? Hx(0x6A3530) : V(r, 0xF0B0AA, .08f);
                    if (x >= 5 && y <= 2) return V(r, bone, .1f);
                }
                if (f == 4 && y < 6) return V(r, bone, .12f);
                return V(r, (x * 7 + y * 3) % 11 == 0 ? pinkD : pink, .12f);
            },
            (f, x, y, w, h) =>
            {
                if (y >= 9) return V(r, cloth, .2f);
                if ((f == 3 || f == 5) && x >= 3 && y >= 2 && y <= 7 && y % 2 == 0) return V(r, bone, .1f);
                if ((f == 3 || f == 5) && x >= 3 && y >= 2 && y <= 7) return V(r, rot, .2f);
                return V(r, pink, .12f);
            },
            (f, x, y, w, h) => V(r, y > 7 && (x + y) % 3 == 0 ? rot : pink, .14f),
            (f, x, y, w, h) => V(r, y < 3 ? cloth : (y > 8 && x < 2 ? bone : pink), .14f));
        Add("zombie_pigman", c);
    }

    private static void WitherSkeletonSkin()
    {
        var c = new Canvas(191); var r = c.R;
        uint bone = 0x343434, dark = 0x1A1A1A;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y >= 3 && y <= 4 && (x == 1 || x == 2 || x == 5 || x == 6)) return Hx(0x050505);
                if (y == 5 && (x == 3 || x == 4)) return Hx(0x0C0C0C);
                if (y == 6 && x >= 1 && x <= 6) return (x % 2 == 0) ? Hx(0x0C0C0C) : V(r, bone);
            }
            return V(r, bone, .2f);
        });
        c.Box(16, 16, 8, 12, 4, (f, x, y, w, h) =>
        {
            if (f == 3 || f == 5)
            {
                if (x == 3 || x == 4) return V(r, bone);
                if (y % 3 == 1 && y < 9) return V(r, bone);
                if (y >= 10) return V(r, dark);
                return 0;
            }
            return y % 3 == 1 ? V(r, bone) : 0;
        });
        c.Box(40, 16, 2, 12, 2, (f, x, y, w, h) => V(r, bone, .2f));
        c.Box(0, 16, 2, 12, 2, (f, x, y, w, h) => V(r, bone, .2f));
        Add("wither_skeleton", c);
    }

    private static void EndermiteSkin()
    {
        var c = new Canvas(192); var r = c.R;
        Func<int, int, int, int, int, uint> shell = (f, x, y, w, h) => V(r, (x + y) % 4 == 0 ? 0x2A1F38 : 0x4B3A5C, .25f);
        for (int i = 0; i < 4; i++) c.Box(new[] { 0, 0, 0, 0 }[i], new[] { 0, 5, 14, 18 }[i], new[] { 4, 6, 3, 1 }[i], new[] { 3, 4, 3, 2 }[i], new[] { 2, 5, 1, 1 }[i], shell);
        Add("endermite", c);
    }
}
