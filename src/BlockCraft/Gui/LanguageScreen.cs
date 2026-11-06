using BlockCraft.Core;

namespace BlockCraft.Gui;

/// <summary>Ustawienia -> „Język...”: nazwy przedmiotów z plików .lang MC 1.8 (zasoby 1.8 i paczki zasobów) albo polskie nazwy gry.</summary>
public sealed class LanguageScreen : Screen, IPadLists
{
    private readonly Screen _parent;
    private List<(string Code, string Name)> _langs;
    private float _scroll;
    private int _languageRevision = -1;
    private const int Row = 14, RowOffset = 3, ItemH = 10;

    public LanguageScreen(Screen parent) { _parent = parent; }
    public override bool PausesGame => true;

    protected override void Init()
    {
        RefreshLanguages();
        Add(new Button(W / 2 - 100, H - 30, 200, 20, "Gotowe", () => G.SetScreen(_parent)));
    }

    private void RefreshLanguages()
    {
        if (_langs == null || _languageRevision != Lang.Revision)
        {
            _languageRevision = Lang.Revision;
            _langs = new List<(string, string)> { ("", "Polski - nazwy gry") };
            _langs.AddRange(Lang.Available(ResourcePacks.Readers));
            ResourcePacks.CloseAll();
        }
        _scroll = Math.Clamp(_scroll, 0, MaxScroll);
    }

    private int ListTop => 32;
    private int ListBottom => H - 40;
    private float MaxScroll => Math.Max(0, _langs.Count * Row - (ListBottom - ListTop) + 6);
    private int RowY(int i) => ListTop + RowOffset + i * Row - (int)_scroll;
    protected override void Escape() => G.SetScreen(_parent);

    public override void Render(int mx, int my, float pt)
    {
        RefreshLanguages();
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Język nazw przedmiotów", W / 2f, 10, 0xFFFFFFFF);
        sb.TextCentered("§7Teksty menu zostają po polsku; języki z zasobów 1.8 i z paczek zasobów", W / 2f, 20, 0xFFFFFFFF);
        sb.Fill(W / 2 - 110, ListTop, 220, ListBottom - ListTop, 0xC0000000);
        if (!BlockCraft.Audio.AssetStore.Done || _langs.Count == 1) sb.TextCentered("§7(pliki języków pobiorą się razem z dźwiękami)", W / 2f, H - 39, 0xFFFFFFFF);
        for (int i = 0; i < _langs.Count; i++)
        {
            int y = RowY(i);
            if (y < ListTop || y + ItemH > ListBottom) continue;
            bool sel = _langs[i].Code == Lang.Code, hover = mx >= W / 2 - 110 && mx < W / 2 + 110 && my >= y - 2 && my < y + Row - 2;
            if (sel) sb.Rect(W / 2 - 108, y - 2, 216, Row, 0xFFFFFFFF);
            sb.TextCentered(_langs[i].Name, W / 2f, y, sel ? 0xFFFFFF80 : hover ? 0xFFFFFFFF : 0xFFC0C0C0);
        }
        base.Render(mx, my, pt);
    }

    public override void MouseDown(int mx, int my, int btn)
    {
        if (btn == 0 && my >= ListTop && my < ListBottom && mx >= W / 2 - 110 && mx < W / 2 + 110)
        {
            int i = (int)((my - ListTop - 1 + _scroll) / Row);
            if (i >= 0 && i < _langs.Count) { G.PlayUiClick(); G.SetLanguage(_langs[i].Code); return; }
        }
        base.MouseDown(mx, my, btn);
    }

    public override void Scroll(int mx, int my, float d)
    {
        _scroll = Math.Clamp(_scroll - d * 20, 0, MaxScroll);
    }

    public override IEnumerable<(int x, int y)> PadTargets() => this.ListTargets().Concat(base.PadTargets());

    public IEnumerable<PadList> PadLists()
    {
        yield return new PadList
        {
            Left = W / 2 - 110, Width = 220, Top = ListTop, Bottom = ListBottom, Offset = RowOffset, RowH = Row, ItemH = ItemH, Count = _langs.Count,
            MaxScroll = MaxScroll, GetScroll = () => _scroll, SetScroll = v => _scroll = v,
        };
    }

    /// <summary>Pad: kursor na wybranym języku.</summary>
    public override (int x, int y)? PadStart()
    {
        int i = Math.Max(0, _langs.FindIndex(l => l.Code == Lang.Code));
        _scroll = Math.Clamp(i * Row - (ListBottom - ListTop) / 2f, 0, MaxScroll);
        return (W / 2, RowY(i) + ItemH / 2);
    }
}
