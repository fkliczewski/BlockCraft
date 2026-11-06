using BlockCraft.Core;

namespace BlockCraft.Render;

/// <summary>Czcionka Unicode MC 1.8 dla znaków spoza czcionki gry (cyrylica, greka, litery z akcentami, CJK...):
/// strony font/unicode_page_XX.png (16x16 znaków po 16 px; paczki HD większe) i font/glyph_sizes.bin (bajt na znak:
/// starsze 4 bity = pierwsza kolumna, młodsze = ostatnia, w jednostkach 1/16 komórki). Pliki z paczek zasobów,
/// a bez nich z client.jar; strony wczytywane przy pierwszym użyciu.</summary>
public sealed class UnicodeFont : IDisposable
{
    public const string SizesPath = "assets/minecraft/font/glyph_sizes.bin";
    public static string PagePath(int page) => $"assets/minecraft/textures/font/unicode_page_{page:x2}.png";

    /// <summary>Znak na stronie: obszar tekstury (piksele) i wymiary w pikselach GUI.</summary>
    public readonly record struct Glyph(Texture2D Tex, float U0, float V0, float U1, float V1, float Width, int Advance);

    private readonly Func<string, byte[]> _read;
    private readonly byte[] _sizes;
    private readonly Dictionary<int, (Texture2D tex, int size)> _pages = new();
    private readonly HashSet<int> _missing = new();

    public UnicodeFont(Func<string, byte[]> read)
    {
        _read = read;
        var sizes = Safe(SizesPath);
        _sizes = sizes is { Length: 65536 } ? sizes : null;
        if (_sizes == null) Log.Info("Czcionka Unicode: brak glyph_sizes.bin (znaki spoza czcionki gry jako ?)");
    }

    public bool Available => _sizes != null;

    private byte[] Safe(string path)
    {
        try { return _read(path); }
        catch (Exception ex) { Log.Error("Czcionka Unicode: " + path + ": " + ex.Message); return null; }
    }

    /// <summary>Kolumny znaku (start, koniec+1) w jednostkach 1/16 komórki; null = brak znaku.</summary>
    public static (int start, int end)? Columns(byte size) => size == 0 ? null : (size >> 4, (size & 15) + 1);

    /// <summary>Szerokość jak FontRenderer.getCharWidth dla znaku Unicode (bez odstępu 1 px):
    /// gdy młodsze 4 bity > 7, 1.8 liczy całą komórkę (kolumny 0..15).</summary>
    public static int CharWidth(byte size) => Columns(size) is not { } c ? 0
        : (size & 15) > 7 ? 16 / 2 : (c.end - c.start) / 2;

    /// <summary>Szerokość znaku w pikselach GUI (bez odstępu); null = brak znaku.</summary>
    public int? Width(char c) => _sizes == null || _sizes[c] == 0 ? null : CharWidth(_sizes[c]);

    public bool TryGet(char c, out Glyph g)
    {
        g = default;
        if (_sizes == null || Columns(_sizes[c]) is not { } col) return false;
        int page = c >> 8;
        if (!_pages.TryGetValue(page, out var p))
        {
            if (_missing.Contains(page) || LoadPage(page) is not { } loaded) { _missing.Add(page); return false; }
            _pages[page] = p = loaded;
        }
        float k = p.size / 256f, cell = p.size / 16f;
        float x0 = (c & 15) * cell + col.start * k, y0 = ((c & 255) >> 4) * cell;
        float tw = (col.end - col.start) * k - 0.02f * k, th = cell - 0.02f * k;
        g = new Glyph(p.tex, x0 / p.size, y0 / p.size, (x0 + tw) / p.size, (y0 + th) / p.size,
            (col.end - col.start) / 2f - 0.01f, CharWidth(_sizes[c]) + 1);
        return true;
    }

    private (Texture2D, int)? LoadPage(int page)
    {
        var data = Safe(PagePath(page));
        var img = data == null ? null : RawImage.Decode(data, "font/unicode_page_" + page.ToString("x2"));
        if (img == null) return null;
        if (img.W != img.H || img.W < 256 || img.W % 256 != 0) { Log.Error($"Czcionka Unicode: unicode_page_{page:x2}.png ma {img.W}x{img.H}, oczekiwano kwadratu 256 lub większego"); return null; }
        return (new Texture2D(img.W, img.H, img.Px), img.W);
    }

    public void Dispose()
    {
        foreach (var (tex, _) in _pages.Values) tex.Dispose();
        _pages.Clear();
    }
}
