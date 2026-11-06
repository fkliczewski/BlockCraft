using BlockCraft.Core;

namespace BlockCraft.Gui;

/// <summary>Przeskok kursora krzyżakiem: najbliższy cel w danym kierunku (przyciski, sloty, zakładki).</summary>
public static class PadNavigation
{
    /// <summary>Cel w kierunku (dx, dy) od (fx, fy): odległość wzdłuż kierunku plus podwójne odchylenie w bok;
    /// cele bardziej z boku niż z przodu są pomijane. Null = nic w tym kierunku.</summary>
    public static (int x, int y)? Next(IEnumerable<(int x, int y)> targets, int fx, int fy, int dx, int dy)
    {
        (int x, int y)? best = null;
        float bestScore = float.MaxValue;
        foreach (var t in targets)
        {
            int vx = t.x - fx, vy = t.y - fy;
            float along = vx * dx + vy * dy, side = Math.Abs(vx * dy - vy * dx);
            if (along <= 3 || side > along * 3) continue;
            float score = along + side * 2;
            if (score < bestScore) { bestScore = score; best = t; }
        }
        return best;
    }
}

/// <summary>Ikony przycisków pada (układ Xbox: kolorowe A/B/X/Y), podpowiedzi na dole ekranu i kursor GUI.</summary>
public static class PadIcons
{
    /// <summary>Kolor ikony (format SpriteBatch: 0xAABBGGRR): A zielony, B czerwony, X niebieski, Y żółty.</summary>
    public static uint Color(PadButton b) => b switch
    {
        PadButton.A => 0xFF3CA03C, PadButton.B => 0xFF3232C8, PadButton.X => 0xFFC86432, PadButton.Y => 0xFF1EA0C8, _ => 0xFF505050,
    };

    public static string Short(Game g, PadButton b) => PadBindings.ShortName(b, g.PadLayout);

    /// <summary>Kształty przycisków PlayStation 7x7 (krzyżyk, kółko, kwadrat, trójkąt) i ich kolory (0xAABBGGRR).</summary>
    private static readonly Dictionary<PadButton, (string[] mask, uint color)> PlayStationShapes = new()
    {
        [PadButton.A] = (new[] { "X.....X", ".X...X.", "..X.X..", "...X...", "..X.X..", ".X...X.", "X.....X" }, 0xFFE8B27C),
        [PadButton.B] = (new[] { "..XXX..", ".X...X.", "X.....X", "X.....X", "X.....X", ".X...X.", "..XXX.." }, 0xFF6666FF),
        [PadButton.X] = (new[] { ".......", ".XXXXX.", ".X...X.", ".X...X.", ".X...X.", ".XXXXX.", "......." }, 0xFFF869FF),
        [PadButton.Y] = (new[] { ".......", "...X...", "..X.X..", "..X.X..", ".X...X.", ".XXXXX.", "......." }, 0xFFA0E240),
    };

    /// <summary>Ikona przycisku (podświetlona, gdy wciśnięty); zwraca szerokość.</summary>
    public static int Draw(Game g, PadButton b, int x, int y, bool lit = false)
    {
        var sb = g.Sb;
        if (g.PadLayout == PadLayout.PlayStation && PlayStationShapes.TryGetValue(b, out var shape))
        {
            uint bg = lit ? 0xFFFFFFFF : 0xFF303030;
            sb.Fill(x + 1, y, 7, 9, bg);
            sb.Fill(x, y + 1, 9, 7, bg);
            for (int r = 0; r < 7; r++)
                for (int col = 0; col < 7; col++)
                    if (shape.mask[r][col] == 'X') sb.Fill(x + 1 + col, y + 1 + r, 1, 1, lit ? 0xFF000000 : shape.color);
            return 9;
        }
        string label = Short(g, b);
        int w = Math.Max(9, sb.Measure(label) + 4);
        uint c = Color(b);
        if (lit) c = 0xFFFFFFFF;
        // zaokrąglone rogi: bez narożnych pikseli
        sb.Fill(x + 1, y, w - 2, 9, c);
        sb.Fill(x, y + 1, w, 7, c);
        sb.Text(label, x + (w - sb.Measure(label)) / 2f + 0.5f, y + 1, lit ? 0xFF000000 : 0xFFFFFFFF, false);
        return w;
    }

    /// <summary>Pasek podpowiedzi „[A] Wybierz [B] Wstecz” przez całą szerokość ekranu (zasłania napisy pod spodem).</summary>
    public static void Hints(Game g, int x, int y, params (PadButton b, string text)[] hints)
    {
        var sb = g.Sb;
        sb.Fill(0, y - 2, g.GuiW, 13, 0xE0101010);
        foreach (var (b, text) in hints)
        {
            x += Draw(g, b, x, y) + 3;
            sb.Text(text, x, y + 1, 0xFFE0E0E0);
            x += sb.Measure(text) + 8;
        }
    }

    /// <summary>Kursor GUI w trybie pada (systemowy jest wtedy ukryty).</summary>
    public static void Cursor(Game g, int x, int y)
    {
        var sb = g.Sb;
        // trójkątna strzałka: czarny obrys o piksel większy, biały środek
        for (int i = 0; i < 10; i++) sb.Fill(x - 1, y - 1 + i, Math.Min(i, 7) + 2, 1, 0xFF000000);
        for (int i = 0; i < 8; i++) sb.Fill(x, y + i, Math.Max(1, Math.Min(i, 6)), 1, 0xFFFFFFFF);
    }
}
