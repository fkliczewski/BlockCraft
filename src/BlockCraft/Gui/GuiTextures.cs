using BlockCraft.Core;
using BlockCraft.Render;

namespace BlockCraft.Gui;

/// <summary>Proceduralne tekstury GUI w stylu MC (widgets.png / icons.png).</summary>
public sealed class GuiTextures : IDisposable
{
    public Texture2D Widgets, Icons, Logo, Dirt;
    public const int BtnY = 46; // y przycisków: 46 wyłączony, 66 normalny, 86 podświetlony
    public static readonly (int x, int y) Hotbar = (0, 0), Sel = (0, 22);
    // ikony (9x9): serca
    public const int HeartEmpty = 16, HeartFull = 52, HeartHalf = 61, HeartBlinkEmpty = 25;
    public static int LogoW, LogoH;

    private static uint C(uint hex, int a = 255) => TextureGen.Hex(hex, a);

    public GuiTextures()
    {
        Widgets = new Texture2D(256, 256, MakeWidgets());
        Icons = new Texture2D(256, 256, MakeIcons());
        Logo = MakeLogo();
        var d = TextureGen.Get("dirt");
        var dirt = new uint[256];
        for (int i = 0; i < 256; i++) dirt[i] = d[i];
        Dirt = new Texture2D(16, 16, dirt);
    }

    private static void Fill(uint[] p, int x, int y, int w, int h, uint c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) if (x + i < 256 && y + j < 256) p[(y + j) * 256 + x + i] = c; }

    private static uint[] MakeWidgets()
    {
        var p = new uint[256 * 256];
        // hotbar 182x22
        Fill(p, 0, 0, 182, 22, C(0x000000, 0));
        for (int s = 0; s < 9; s++)
        {
            int x = 1 + s * 20;
            Fill(p, x, 1, 20, 20, C(0x8B8B8B));
            Fill(p, x + 1, 2, 18, 18, C(0x1E1E1E, 140));
            Fill(p, x, 1, 20, 1, C(0x8B8B8B)); Fill(p, x, 20, 20, 1, C(0x8B8B8B));
            Fill(p, x + 1, 2, 18, 1, C(0x5A5A5A)); Fill(p, x + 1, 2, 1, 18, C(0x5A5A5A));
        }
        Fill(p, 0, 0, 182, 1, C(0x000000, 200)); Fill(p, 0, 21, 182, 1, C(0x000000, 200)); Fill(p, 0, 0, 1, 22, C(0x000000, 200)); Fill(p, 181, 0, 1, 22, C(0x000000, 200));
        for (int s = 1; s < 9; s++) Fill(p, s * 20, 1, 1, 20, C(0x5A5A5A));
        // zaznaczenie 24x24
        Fill(p, 0, 22, 24, 24, 0);
        Fill(p, 0, 22, 24, 2, C(0xFFFFFF)); Fill(p, 0, 44, 24, 2, C(0xFFFFFF)); Fill(p, 0, 22, 2, 24, C(0xFFFFFF)); Fill(p, 22, 22, 2, 24, C(0xFFFFFF));
        Fill(p, 0, 22, 24, 1, C(0x000000)); Fill(p, 0, 45, 24, 1, C(0x000000)); Fill(p, 0, 22, 1, 24, C(0x000000)); Fill(p, 23, 22, 1, 24, C(0x000000));
        Fill(p, 2, 24, 20, 1, C(0xA0A0A0)); Fill(p, 2, 43, 20, 1, C(0xA0A0A0)); Fill(p, 2, 24, 1, 20, C(0xA0A0A0)); Fill(p, 21, 24, 1, 20, C(0xA0A0A0));
        // przyciski
        var r = new Rng(9);
        for (int st = 0; st < 3; st++)
        {
            int y0 = BtnY + st * 20;
            uint baseC = st == 0 ? C(0x2C2C2C) : st == 1 ? C(0x6F6F6F) : C(0x7E88BF);
            uint hi = st == 0 ? C(0x3A3A3A) : st == 1 ? C(0xAAAAAA) : C(0xBCC4F0);
            uint lo = st == 0 ? C(0x1E1E1E) : st == 1 ? C(0x555555) : C(0x5A62A0);
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 200; x++)
                {
                    uint c = TextureGen.Mul(baseC, 0.92f + r.NextFloat() * 0.14f);
                    if (y == 1 || x == 1) c = hi;
                    if (y >= 17 && y <= 18 || x == 198) c = lo;
                    if (y == 0 || y == 19 || x == 0 || x == 199) c = st == 2 ? C(0xFFFFFF) : C(0x000000);
                    p[(y0 + y) * 256 + x] = c;
                }
        }
        // zakładki kreatywne 28x32: nieaktywna (0,110), aktywna (28,110)
        for (int t = 0; t < 2; t++)
        {
            int x0 = t * 28, y0 = 110;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 28; x++)
                {
                    uint c = t == 1 ? C(0xC6C6C6) : C(0x9E9E9E);
                    if (x == 0 || y == 0 || x == 27) c = C(0x000000);
                    else if (x == 1 || y == 1) c = C(0xFFFFFF);
                    else if (x == 26) c = C(0x555555);
                    if (y == 31 && t == 0) c = C(0x000000);
                    if ((x == 0 || x == 27) && y < 2) c = 0;
                    p[(y0 + y) * 256 + x0 + x] = c;
                }
        }
        return p;
    }

    private static void Sprite9(uint[] p, int ox, int oy, string[] rows, Dictionary<char, uint> pal)
    {
        for (int y = 0; y < rows.Length; y++) for (int x = 0; x < rows[y].Length; x++)
                if (pal.TryGetValue(rows[y][x], out var c)) p[(oy + y) * 256 + ox + x] = c;
    }

    private static uint[] MakeIcons()
    {
        var p = new uint[256 * 256];
        // celownik 15x15 w (0,0)
        for (int i = 0; i < 15; i++) { if (i != 7) { p[7 * 256 + i] = C(0xFFFFFF); p[i * 256 + 7] = C(0xFFFFFF); } }
        p[7 * 256 + 7] = C(0xFFFFFF);
        string[] heart =
        {
            ".##.##...", "#aa#bb#..", "#abbbbb#.", "#bbbbbb#.", ".#bbbbb#.", "..#bbb#..", "...#b#...", "....#....", "........."
        };
        string[] heartF =
        {
            ".##.##...", "#wr#rr#..", "#wrrrrr#.", "#rrrrrr#.", ".#rrrrd#.", "..#rrd#..", "...#d#...", "....#....", "........."
        };
        string[] heartH =
        {
            ".##......", "#wr#.....", "#wrr.....", "#rrr.....", ".#rr.....", "..#r.....", "...#.....", ".........", "........."
        };
        var hp = new Dictionary<char, uint> { ['#'] = C(0x000000), ['a'] = C(0x2A2A2A), ['b'] = C(0x2A0A0A), ['w'] = C(0xFFC8C8), ['r'] = C(0xE81E1E), ['d'] = C(0xA00000) };
        Sprite9(p, 16, 0, heart, hp);                          // pojemnik
        var hpBlink = new Dictionary<char, uint>(hp) { ['#'] = C(0xFFFFFF) };
        Sprite9(p, 25, 0, heart, hpBlink);                     // pojemnik migający
        Sprite9(p, 52, 0, heartF, hp);                         // pełne
        Sprite9(p, 61, 0, heartH, hp);                         // pół
        var hpW = new Dictionary<char, uint>(hp) { ['r'] = C(0xFFFFFF), ['w'] = C(0xFFFFFF), ['d'] = C(0xC8C8C8) };
        Sprite9(p, 70, 0, heartF, hpW); Sprite9(p, 79, 0, heartH, hpW);
        // głód (9x9) w (16,27)
        string[] food =
        {
            "......##.", ".....#ww#", "....#wo#.", "..##oo#..", ".#bbbb#..", "#bmbbb#..", "#bbbbm#..", ".#bbb#...", "..###...."
        };
        string[] foodC =
        {
            "......##.", ".....#..#", "....#..#.", "..##..#..", ".#....#..", "#.....#..", "#.....#..", ".#...#...", "..###...."
        };
        var fp = new Dictionary<char, uint> { ['#'] = C(0x2A1A0A), ['w'] = C(0xF4F4EA), ['o'] = C(0xC8C8B4), ['b'] = C(0xB5651D), ['m'] = C(0x8A4A10), ['.'] = 0 };
        var fpc = new Dictionary<char, uint> { ['#'] = C(0x000000), ['.'] = 0 };
        Sprite9(p, 16, 27, foodC, fpc);
        // wnętrze pojemnika ciemne
        for (int y = 0; y < 9; y++) for (int x = 0; x < 9; x++) if (p[(27 + y) * 256 + 16 + x] == 0 && IsInside(foodC, x, y)) p[(27 + y) * 256 + 16 + x] = C(0x2A2A2A, 200);
        Sprite9(p, 52, 27, food, fp);
        // pół: lewa połowa
        Sprite9(p, 61, 27, food.Select(s => s[..5] + "....").ToArray(), fp);
        var fpH = new Dictionary<char, uint>(fp) { ['b'] = C(0x8AB01D), ['m'] = C(0x5A7A10) };
        Sprite9(p, 88, 27, food, fpH); Sprite9(p, 97, 27, food.Select(s => s[..5] + "....").ToArray(), fpH);
        // zbroja (9x9) w (16,9): pusta, (25,9) pół, (34,9) pełna
        string[] armor = { ".###.###.", "#aaa#aaa#", "#aaaaaaa#", "#aaaaaaa#", ".#aaaaa#.", ".#aaaaa#.", ".#aaaaa#.", "..#####..", "........." };
        var ap = new Dictionary<char, uint> { ['#'] = C(0x1E1E1E), ['a'] = C(0xC6C6C6) };
        var apE = new Dictionary<char, uint> { ['#'] = C(0x1E1E1E), ['a'] = C(0x3A3A3A, 180) };
        Sprite9(p, 16, 9, armor, apE);
        Sprite9(p, 34, 9, armor, ap);
        Sprite9(p, 25, 9, armor, apE);
        Sprite9(p, 25, 9, armor.Select(s => s[..4] + ".....").ToArray(), ap);
        // bąbelek w (16,18), pęknięty (25,18)
        string[] bub = { "..###....", ".#www#...", "#wbbbb#..", "#wbbbb#..", "#bbbbb#..", ".#bbb#...", "..###....", ".........", "........." };
        Sprite9(p, 16, 18, bub, new() { ['#'] = C(0x2050D0), ['w'] = C(0xFFFFFF), ['b'] = C(0x80B0FF) });
        string[] bub2 = { "..#.#....", ".#...#...", "#.....#..", ".........", "#.....#..", ".#...#...", "..#.#....", ".........", "........." };
        Sprite9(p, 25, 18, bub2, new() { ['#'] = C(0x80B0FF) });
        // pasek XP 182x5 w (0,64) pusty, (0,69) pełny
        for (int x = 0; x < 182; x++) for (int y = 0; y < 5; y++)
            {
                bool edge = y == 0 || y == 4 || x == 0 || x == 181;
                p[(64 + y) * 256 + x] = edge ? C(0x000000) : C(0x1E3A0A, 220);
                p[(69 + y) * 256 + x] = edge ? C(0x000000, 0) : (y == 1 ? C(0xB0FF60) : C(0x80FF20));
                if (!edge && x % 20 == 0) p[(64 + y) * 256 + x] = C(0x000000);
            }
        return p;
    }

    private static bool IsInside(string[] rows, int x, int y)
    {
        int l = rows[y].IndexOf('#'), r = rows[y].LastIndexOf('#');
        return l >= 0 && x > l && x < r;
    }

    private static readonly Dictionary<char, string[]> BigLetters = new()
    {
        ['B'] = new[] { "####.", "#...#", "####.", "#...#", "#...#", "####." },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#####" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", ".####" },
        ['K'] = new[] { "#...#", "#..#.", "###..", "#..#.", "#...#", "#...#" },
        ['R'] = new[] { "####.", "#...#", "####.", "#..#.", "#...#", "#...#" },
        ['A'] = new[] { ".###.", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['F'] = new[] { "#####", "#....", "####.", "#....", "#....", "#...." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#.." },
    };

    private static Texture2D MakeLogo()
    {
        const string text = "BLOCKCRAFT";
        const int px = 6, depth = 4;
        int cols = text.Length * 6 - 1;
        int w = cols * px + depth + 2, h = 6 * px + depth + 2;
        LogoW = w; LogoH = h;
        var grid = new bool[cols, 6];
        for (int i = 0; i < text.Length; i++)
        {
            var L = BigLetters[text[i]];
            for (int y = 0; y < 6; y++) for (int x = 0; x < 5; x++) if (L[y][x] == '#') grid[i * 6 + x, y] = true;
        }
        var p = new uint[w * h];
        var stone = TextureGen.Get("stone"); var r = new Rng(4);
        // ekstruzja (cień 3D)
        for (int gy = 0; gy < 6; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                if (!grid[gx, gy]) continue;
                for (int d = 1; d <= depth; d++)
                    for (int y = 0; y < px; y++) for (int x = 0; x < px; x++)
                        {
                            int X = gx * px + x + d, Y = gy * px + y + d;
                            float shade = 0.28f - d * 0.02f;
                            p[Y * w + X] = TextureGen.Mul(stone[((Y) & 15) * 16 + (X & 15)], shade);
                        }
            }
        // przód
        for (int gy = 0; gy < 6; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                if (!grid[gx, gy]) continue;
                for (int y = 0; y < px; y++) for (int x = 0; x < px; x++)
                    {
                        int X = gx * px + x, Y = gy * px + y;
                        uint c = stone[(Y & 15) * 16 + (X & 15)];
                        float f = 1.15f;
                        if (y == 0 && (gy == 0 || !grid[gx, gy - 1])) f = 1.45f;
                        if (x == 0 && (gx == 0 || !grid[gx - 1, gy])) f = 1.3f;
                        if (y == px - 1 && (gy == 5 || !grid[gx, gy + 1])) f = 0.75f;
                        if (x == px - 1 && (gx == cols - 1 || !grid[gx + 1, gy])) f = 0.85f;
                        p[Y * w + X] = TextureGen.Mul(c, f);
                    }
            }
        // czarny kontur
        var outP = (uint[])p.Clone();
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                if (p[y * w + x] != 0) continue;
                bool n = false;
                for (int dy = -1; dy <= 1 && !n; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < w && yy < h && p[yy * w + xx] != 0) { n = true; break; }
                    }
                if (n) outP[y * w + x] = C(0x000000, 220);
            }
        return new Texture2D(w, h, outP);
    }

    public void Dispose() { Widgets.Dispose(); Icons.Dispose(); Logo.Dispose(); Dirt.Dispose(); }
}
