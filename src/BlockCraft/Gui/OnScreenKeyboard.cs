namespace BlockCraft.Gui;

/// <summary>Klawiatura ekranowa do pisania padem: litery z polskimi znakami, cyfry, strona symboli, Shift (jednorazowy,
/// dwa razy = stały), Backspace, spacja, wklejanie, Enter. Wybór krzyżakiem/gałką, A wpisuje.
/// Wpisy idą do ekranu jak z klawiatury (CharTyped i KeyDown), więc działa z każdym polem tekstowym.</summary>
public sealed class OnScreenKeyboard
{
    public enum Special { None, Shift, Symbols, Space, Backspace, Paste, Enter, Close }

    public sealed record Key(string Label, char Char, Special Special = Special.None, int Width = 1);

    /// <summary>Co klawiatura wysyła do ekranu.</summary>
    public enum OutKind { Char, Backspace, Enter, Paste, Close }
    public readonly record struct Output(OutKind Kind, char Char = '\0');

    private static Key[] Chars(string s) => s.Select(c => new Key(c.ToString(), c)).ToArray();

    private static readonly Key[] SpecialRow =
    {
        // spacja na środku (pod środkiem liter)
        new("Shift", '\0', Special.Shift, 2), new("#+=", '\0', Special.Symbols, 2), new("Wklej", '\0', Special.Paste, 2),
        new("Spacja", ' ', Special.Space, 3), new("<-", '\0', Special.Backspace), new("Enter", '\0', Special.Enter, 2),
        new("Ukryj", '\0', Special.Close, 2),
    };

    private static readonly Key[][] LetterPage =
    {
        Chars("1234567890"), Chars("qwertyuiop"), Chars("asdfghjkl-"), Chars("zxcvbnm,.?"), Chars("ąćęłńóśźż!"), SpecialRow,
    };

    private static readonly Key[][] SymbolPage =
    {
        Chars("1234567890"), Chars("!@#$%^&*()"), Chars("-_=+[]{};:"), Chars("'\"<>/\\|~`,"), Chars(".,?!:;()"), SpecialRow,
    };

    public bool Symbols { get; private set; }
    /// <summary>0 = małe litery, 1 = jedna wielka (po znaku wraca), 2 = stałe wielkie.</summary>
    public int ShiftState { get; private set; }
    public int Row { get; private set; } = 1;
    public int Col { get; private set; }

    public Key[][] Page => Symbols ? SymbolPage : LetterPage;
    public Key Selected => Page[Row][Col];

    /// <summary>Etykieta klawisza z uwzględnieniem Shift.</summary>
    public string LabelOf(Key k) => k.Special == Special.None && ShiftState > 0 ? k.Label.ToUpperInvariant() : k.Label;

    public void ToggleShift() => ShiftState = (ShiftState + 1) % 3;
    public void ToggleSymbols() { Symbols = !Symbols; Row = Math.Min(Row, Page.Length - 1); Col = Math.Min(Col, Page[Row].Length - 1); }

    public void Reset() { Symbols = false; ShiftState = 0; Row = 1; Col = 0; }

    /// <summary>Przesunięcie wyboru; w pionie do klawisza najbliższego w poziomie (rzędy mają różne szerokości).</summary>
    public void Move(int dx, int dy)
    {
        if (dx != 0)
        {
            int n = Page[Row].Length;
            Col = ((Col + dx) % n + n) % n;
        }
        if (dy != 0)
        {
            float center = KeyCenter(Page[Row], Col);
            Row = ((Row + dy) % Page.Length + Page.Length) % Page.Length;
            var row = Page[Row];
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < row.Length; i++) { float d = Math.Abs(KeyCenter(row, i) - center); if (d < bd) { bd = d; best = i; } }
            Col = best;
        }
    }

    /// <summary>Środek klawisza w jednostkach szerokości (rząd wyśrodkowany).</summary>
    private static float KeyCenter(Key[] row, int i)
    {
        int total = row.Sum(k => k.Width), start = 0;
        for (int j = 0; j < i; j++) start += row[j].Width;
        return start + row[i].Width / 2f - total / 2f;
    }

    /// <summary>Naciśnięcie wybranego klawisza.</summary>
    public Output? Press() => Press(Selected);

    public Output? Press(Key k)
    {
        switch (k.Special)
        {
            case Special.Shift: ToggleShift(); return null;
            case Special.Symbols: ToggleSymbols(); return null;
            case Special.Space: return new Output(OutKind.Char, ' ');
            case Special.Backspace: return new Output(OutKind.Backspace);
            case Special.Paste: return new Output(OutKind.Paste);
            case Special.Enter: return new Output(OutKind.Enter);
            case Special.Close: return new Output(OutKind.Close);
        }
        char c = ShiftState > 0 ? char.ToUpperInvariant(k.Char) : k.Char;
        if (ShiftState == 1 && char.IsLetter(k.Char)) ShiftState = 0;
        return new Output(OutKind.Char, c);
    }

    /// <summary>Wybiera (bez naciskania) klawisz o danej etykiecie małymi literami; do testów.</summary>
    public bool Select(string label)
    {
        for (int r = 0; r < Page.Length; r++)
            for (int c = 0; c < Page[r].Length; c++)
                if (Page[r][c].Label == label) { Row = r; Col = c; return true; }
        return false;
    }

    // ===== rysowanie i mysz =====
    public const int KeyW = 18, KeyH = 16, Gap = 2;
    private int _x0, _y0;

    public int Height => Page.Length * (KeyH + Gap) + 6;
    private int PanelWidth => Page.Max(r => r.Sum(k => k.Width)) * (KeyW + Gap) + 10;

    private (int x, int y, int w) KeyRect(int r, int c)
    {
        var row = Page[r];
        int unit = KeyW + Gap, total = row.Sum(k => k.Width) * unit - Gap, x = _x0 - total / 2;
        for (int j = 0; j < c; j++) x += row[j].Width * unit;
        return (x, _y0 + r * (KeyH + Gap), row[c].Width * unit - Gap);
    }

    /// <summary>Rysuje klawiaturę wyśrodkowaną w poziomie; top = true u góry ekranu (gdy pole jest na dole).</summary>
    public void Draw(Game g, int screenW, int screenH, bool top)
    {
        var sb = g.Sb;
        int width = PanelWidth;
        _x0 = screenW / 2;
        int panelY = top ? 2 : screenH - Height - 2;
        _y0 = panelY + 4;
        sb.Fill(_x0 - width / 2f, panelY, width, Height, 0xD0101010);
        sb.Rect(_x0 - width / 2f, panelY, width, Height, 0xFF808080);
        for (int r = 0; r < Page.Length; r++)
            for (int c = 0; c < Page[r].Length; c++)
            {
                var k = Page[r][c];
                var (x, y, w) = KeyRect(r, c);
                bool sel = r == Row && c == Col;
                bool lit = k.Special == Special.Shift && ShiftState > 0 || k.Special == Special.Symbols && Symbols;
                uint bg = sel ? 0xFFE0E0E0 : lit ? 0xFF406040 : 0xFF404040;
                sb.Fill(x, y, w, KeyH, bg);
                string label = k.Special == Special.Shift && ShiftState == 2 ? "SHIFT" : LabelOf(k);
                sb.TextCentered(label, x + w / 2f, y + (KeyH - 8) / 2f, sel ? 0xFF000000 : 0xFFFFFFFF, !sel);
            }
    }
}
