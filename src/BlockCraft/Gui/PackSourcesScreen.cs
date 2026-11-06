using BlockCraft.Core;

namespace BlockCraft.Gui;

/// <summary>Paczki zasobów -> „Źródła...”: dodatkowe foldery i repozytoria git, z których gra bierze paczki
/// (a w przyszłości mody). Repozytoria pobierają się przy starcie gry i po „Odśwież teraz”.</summary>
public sealed class PackSourcesScreen : Screen
{
    private readonly Screen _parent;
    private List<string> _specs;
    private TextField _input;
    private float _scroll;
    private const int Row = 24;

    public PackSourcesScreen(Screen parent) { _parent = parent; }
    public override bool PausesGame => true;

    protected override void Init()
    {
        _specs ??= G.ActiveSourceSpecs.ToList();
        _input = Add(new TextField(W / 2 - 154, H - 76, 254, 20) { MaxLength = 400, Placeholder = "folder albo https://github.com/... (#gałąź)", Focused = true, Text = _typed ?? "" });
        _input.OnEnter = AddSource;
        Add(new Button(W / 2 + 104, H - 76, 50, 20, "Dodaj", AddSource));
        Add(new Button(W / 2 - 154, H - 48, 150, 20, () => G.SourcesSyncing ? "Pobieranie..." : "Odśwież teraz", () => G.SyncSourcesInBackground()));
        Add(new Button(W / 2 + 4, H - 48, 150, 20, "Gotowe", () => G.SetScreen(_parent)));
        int y = ListTop + 2 - (int)_scroll;
        for (int i = 0; i < _specs.Count; i++, y += Row)
        {
            if (y < ListTop || y + 20 > ListBottom) continue;
            int idx = i;
            Add(new Button(W / 2 + 104, y, 50, 20, "Usuń", () => { _specs.RemoveAt(idx); Apply(); }));
        }
    }

    private int ListTop => 40;
    private int ListBottom => H - 84;

    private void AddSource()
    {
        string s = _input.Text.Trim().Trim('"');
        if (s.Length == 0) return;
        if (!ContentSources.IsRemote(s) && !Directory.Exists(s)) { _error = "Nie ma takiego folderu: " + s; return; }
        if (!_specs.Contains(s, StringComparer.OrdinalIgnoreCase)) _specs.Add(s);
        _input.Text = ""; _error = null;
        Apply();
    }

    private string _error, _typed;

    private void Apply()
    {
        G.SetPackSources(_specs.ToList());
        G.SetScreen(this);
    }

    protected override void Escape() => G.SetScreen(_parent);

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Źródła paczek", W / 2f, 10, 0xFFFFFFFF);
        sb.TextCentered(G.SourcesFromArgs ? "§6Źródła z parametru --pack-source (zmiany tylko na tę sesję)" : "§7Foldery i repozytoria git; repozytoria pobierają się przy starcie gry",
            W / 2f, 24, 0xFFFFFFFF);
        sb.Fill(W / 2 - 158, ListTop - 2, 316, ListBottom - ListTop + 4, 0xC0000000);
        var all = ContentSources.All;
        if (_specs.Count == 0) sb.TextCentered("§7(brak - paczki tylko z folderu resourcepacks)", W / 2f, ListTop + 8, 0xFFFFFFFF);
        int y = ListTop + 2 - (int)_scroll;
        for (int i = 0; i < _specs.Count; i++, y += Row)
        {
            if (y < ListTop || y + 20 > ListBottom) continue;
            var src = all.FirstOrDefault(s => string.Equals(s.Spec, _specs[i], StringComparison.OrdinalIgnoreCase));
            string kind = src == null ? "" : src.Remote ? "§9git§r " : "§2folder§r ";
            sb.Text(kind + Fit(_specs[i], 250 - sb.Measure(kind)), W / 2 - 154, y + 1, 0xFFFFFFFF);
            string status = src == null ? "" : (src.Name + ": " + src.Status);
            uint col = status.Contains("błąd") || status.Contains("nie udało") || status.Contains("brak") ? 0xFFFF8080 : 0xFF909090;
            sb.Text(Fit(status, 254), W / 2 - 154, y + 11, col);
        }
        if (_error != null) sb.TextCentered(_error, W / 2f, H - 90, 0xFFFF6060);
        base.Render(mx, my, pt);
    }

    private string Fit(string s, int w)
    {
        if (G.Sb.Measure(s) <= w) return s;
        while (s.Length > 1 && G.Sb.Measure("..." + s) > w) s = s[1..];
        return "..." + s;
    }

    public override void Scroll(int mx, int my, float d)
    {
        float max = Math.Max(0, _specs.Count * Row - (ListBottom - ListTop) + 4);
        _scroll = Math.Clamp(_scroll - d * 20, 0, max);
        _typed = _input.Text;
        G.SetScreen(this);
    }
}
