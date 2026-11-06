using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.Render;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

/// <summary>
/// GuiScreenBook 1.8: książka 192x192, strona 116 pikseli szerokości, do 50 stron po 256 znaków i 128 pikseli wysokości,
/// podpisywanie (tytuł do 16 znaków), czytanie napisanej książki. Zapis przy zamknięciu (sendBookToServer).
/// </summary>
public sealed class BookScreen : Screen
{
    public const int BookW = 192, BookH = 192, PageW = 116, PageMaxHeight = 128, LineH = 9;
    private readonly Player _p;
    private readonly bool _editable;
    private readonly List<string> _pages;
    private readonly string _author;
    private int _page, _ticks;
    private bool _signing, _modified, _sent;
    private string _title = "";
    private Button _next, _prev, _done, _sign, _finalize, _cancel;
    public override bool PausesGame => false;

    public BookScreen(Player p, ItemStack book, bool editable)
    {
        _p = p; _editable = editable && book?.Id == I.WritableBook;
        _pages = Books.Pages(book);
        if (_pages.Count == 0) _pages.Add("");
        _author = Books.Author(book) ?? p?.Name ?? "";
    }

    public IReadOnlyList<string> Pages => _pages;
    public int Page => _page;
    public bool Signing => _signing;
    public override bool WantsText => _editable || _signing;
    /// <summary>Wiersz z kursorem (tytuł albo ostatnia linia strony): klawiatura ekranowa staje po drugiej stronie.</summary>
    public override int TextY => _signing ? TitleTop : PageTop + (Wrap(_pages[_page] + "_").Count() - 1) * LineH;
    public string TitleText => _title;
    private const int BookTop = 2, PageTop = BookTop + 32, TitleTop = BookTop + 48;

    protected override void Init()
    {
        int left = (W - BookW) / 2, top = BookTop;
        if (_editable)
        {
            _sign = Add(new Button(W / 2 - 100, top + BookH + 4, 98, 20, "Podpisz", () => { _signing = true; Refresh(); }));
            _done = Add(new Button(W / 2 + 2, top + BookH + 4, 98, 20, "Gotowe", () => { Send(false); G.SetScreen(null); }));
            _finalize = Add(new Button(W / 2 - 100, top + BookH + 4, 98, 20, "Podpisz i zamknij", () => { if (Send(true)) G.SetScreen(null); }));
            _cancel = Add(new Button(W / 2 + 2, top + BookH + 4, 98, 20, "Anuluj", () => { _signing = false; Refresh(); }));
        }
        else _done = Add(new Button(W / 2 - 100, top + BookH + 4, 200, 20, "Gotowe", () => G.SetScreen(null)));
        _next = Add(new Button(left + 120, top + 154, 23, 13, ">", NextPage));
        _prev = Add(new Button(left + 38, top + 154, 23, 13, "<", PrevPage));
        Refresh();
    }

    private void Refresh()
    {
        if (_next == null) return;
        _next.Visible = !_signing && (_page < _pages.Count - 1 || _editable && _pages.Count < Books.MaxPages);
        _prev.Visible = !_signing && _page > 0;
        if (_editable)
        {
            _sign.Visible = _done.Visible = !_signing;
            _finalize.Visible = _cancel.Visible = _signing;
            _finalize.Enabled = Books.FilterTitle(_title).Length > 0;
        }
    }

    public void NextPage()
    {
        if (_page < _pages.Count - 1) _page++;
        else if (_editable && _pages.Count < Books.MaxPages) { _pages.Add(""); _page++; _modified = true; }
        Refresh();
    }
    public void PrevPage() { if (_page > 0) _page--; Refresh(); }

    /// <summary>sendBookToServer: tylko książka z piórem, gdy coś zmieniono albo podpisujemy.</summary>
    public bool Send(bool sign)
    {
        if (!_editable || _sent || !_modified && !sign) return false;
        if (sign && Books.FilterTitle(_title).Length == 0) return false;
        _sent = true;
        if (G?.Client != null) { Bl2Net.SendEditBook(G.Client, _pages, sign, _title); return true; }
        return Books.Apply(_p, _pages, sign, _title);
    }

    public override void OnClose() => Send(false);
    public override void Tick() => _ticks++;

    /// <summary>Wstawianie tekstu z limitem 256 znaków i 128 pikseli wysokości strony (pageInsertIntoCurrent).</summary>
    public void Type(string s)
    {
        if (!_editable) return;
        if (_signing)
        {
            foreach (char c in s) if (c >= ' ' && c != '§' && c != 127 && _title.Length < Books.MaxTitle) _title += c;
            Refresh(); return;
        }
        string cur = _pages[_page], next = cur + s;
        if (next.Length < Books.MaxPageChars && Wrap(next).Count * LineH <= PageMaxHeight) { _pages[_page] = next; _modified = true; }
    }

    public void Backspace()
    {
        if (_signing) { if (_title.Length > 0) _title = _title[..^1]; Refresh(); return; }
        if (_editable && _pages[_page].Length > 0) { _pages[_page] = _pages[_page][..^1]; _modified = true; }
    }

    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (k == Keys.Escape) { G.SetScreen(null); return; }
        if (k == Keys.Backspace) { Backspace(); return; }
        if (k is Keys.Enter or Keys.KeyPadEnter)
        {
            if (_signing) { if (Send(true)) G.SetScreen(null); }
            else Type("\n");
            return;
        }
        if (ctrl && k == Keys.V && _editable)
        {
            try { var clip = G.ClipboardString; if (!string.IsNullOrEmpty(clip)) Type(Books.FilterPage(clip)); } catch { }
            return;
        }
        if (!_editable && k == Keys.Right) { NextPage(); return; }
        if (!_editable && k == Keys.Left) { PrevPage(); return; }
        base.KeyDown(k, ctrl, shift);
    }

    public override void CharTyped(char c)
    {
        if (c < ' ' || c == '§' || c == 127) return;
        Type(c.ToString());
    }

    /// <summary>FontRenderer.listFormattedStringToWidth: zawijanie po słowach do szerokości strony, \n łamie linię.</summary>
    public List<string> Wrap(string text)
    {
        var lines = new List<string>();
        foreach (var para in (text ?? "").Split('\n'))
        {
            string line = "";
            foreach (var word in para.Split(' '))
            {
                string cand = line.Length == 0 ? word : line + " " + word;
                if (Measure(cand) <= PageW) { line = cand; continue; }
                if (line.Length > 0) lines.Add(line);
                line = word;
                while (Measure(line) > PageW)
                {
                    int n = line.Length - 1;
                    while (n > 1 && Measure(line[..n]) > PageW) n--;
                    lines.Add(line[..n]); line = line[n..];
                }
            }
            lines.Add(line);
        }
        return lines;
    }
    private float Measure(string s) => G?.Sb != null ? G.Sb.Measure(s) : s.Length * 6;

    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        var sb = G.Sb;
        int left = (W - BookW) / 2, top = BookTop;
        // okładka i strona (gui/book.png)
        sb.Fill(left + 18, top, BookW - 36, BookH - 10, SpriteBatch.Hex(0x6B3A14));
        sb.Fill(left + 22, top + 4, BookW - 44, BookH - 18, SpriteBatch.Hex(0xF2EAD0));
        sb.Fill(left + 22, top + 4, 6, BookH - 18, SpriteBatch.Hex(0xD9CBA3));
        uint black = 0xFF000000;
        if (_signing)
        {
            bool cursor = _ticks / 6 % 2 == 0;
            string head = "Wprowadź tytuł książki:";
            sb.Text(head, left + 36 + (PageW - Measure(head)) / 2, top + 32, black, false);
            string t = _title + (cursor ? "_" : "");
            sb.Text(t, left + 36 + (PageW - Measure(_title + "_")) / 2, TitleTop, black, false);
            string by = "autor: " + _author;
            sb.Text(by, left + 36 + (PageW - Measure(by)) / 2, top + 58, 0xFF555555, false);
            int y = top + 80;
            foreach (var line in Wrap("Uwaga! Po podpisaniu książki nie będzie można jej edytować.")) { sb.Text(line, left + 36, y, black, false); y += LineH; }
        }
        else
        {
            string ind = $"Strona {_page + 1} z {_pages.Count}";
            sb.Text(ind, left - Measure(ind) + BookW - 44, top + 16, black, false);
            string text = _pages[_page];
            if (_editable && _ticks / 6 % 2 == 0) text += "_";
            int y = PageTop;
            foreach (var line in Wrap(text)) { sb.Text(line, left + 36, y, black, false); y += LineH; }
        }
        base.Render(mx, my, pt);
    }
}
