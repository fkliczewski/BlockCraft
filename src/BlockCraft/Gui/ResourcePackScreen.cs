using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Render;

namespace BlockCraft.Gui;

/// <summary>Ustawienia -> „Paczki zasobów...” (jak GuiScreenResourcePacks z 1.8): dostępne po lewej, wybrane po prawej.
/// Klik w dostępną dodaje ją na górę wybranych; przy wybranej strzałki na ikonie: usuń (lewa połowa), wyżej/niżej (prawa).</summary>
public sealed class ResourcePackScreen : Screen, IPadLists, IPadHints
{
    private readonly Screen _parent;
    private List<ResourcePack> _available = new(), _selected = new();
    private readonly List<string> _initial;
    private readonly Dictionary<string, Texture2D> _icons = new();
    private float _scrollL, _scrollR;
    private string _signature;
    private int _ticks;
    private bool _changed;
    private const int Row = 36;

    public ResourcePackScreen(Screen parent, List<string> active) { _parent = parent; _initial = active.ToList(); }
    public override bool PausesGame => true;

    protected override void Init()
    {
        if (_signature == null) Rescan(_initial);
        Add(new Button(W / 2 - 154, H - 48, 100, 20, "Folder paczek", OpenFolder));
        Add(new Button(W / 2 - 50, H - 48, 100, 20, () => "Źródła (" + ContentSources.All.Count + ")...", () => G.SetScreen(new PackSourcesScreen(this))));
        Add(new Button(W / 2 + 54, H - 48, 100, 20, "Gotowe", Done));
    }

    private int ListW => Math.Min(200, W / 2 - 8);
    private int LeftX => W / 2 - 4 - ListW;
    private int RightX => W / 2 + 4;
    private int ListTop => 32;
    private int ListBottom => H - 58;
    private const int RowOffset = 2, ItemH = 32;

    /// <summary>Lista plików w resourcepacks/ (nazwa + czas zmiany): odświeżamy tylko, gdy coś się zmieniło.</summary>
    private static string Signature()
    {
        try
        {
            var d = new DirectoryInfo(ResourcePacks.Dir);
            return string.Join("|", d.EnumerateFileSystemInfos().Select(f => f.Name + "@" + f.LastWriteTimeUtc.Ticks).OrderBy(s => s, StringComparer.Ordinal))
                + (File.Exists(Path.Combine(ClientAssets.Root, "client.jar")) ? "|jar" : "")
                + "|v" + ContentSources.Version + "|" + string.Join(",", ContentSources.All.Select(s => s.Name + s.Available));
        }
        catch { return ""; }
    }

    private void Rescan(IEnumerable<string> selectedIds)
    {
        _signature = Signature();
        foreach (var p in _available.Concat(_selected)) p.Dispose();
        foreach (var t in _icons.Values) t.Dispose();
        _icons.Clear();
        var all = ResourcePacks.Scan();
        var byId = all.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        _selected = selectedIds.Where(byId.ContainsKey).Select(id => byId[id]).Distinct().ToList();
        _available = all.Where(p => !_selected.Contains(p)).ToList();
        foreach (var p in all) p.Close();
    }

    public override void Tick()
    {
        if (++_ticks % 40 != 0) return;
        if (Signature() == _signature) return;
        int selectedBefore = _selected.Count;
        Rescan(_selected.Select(p => p.Id).ToList());
        _changed |= _selected.Count != selectedBefore;
        _scrollL = _scrollR = 0;
    }

    private void OpenFolder()
    {
        try { Process.Start(new ProcessStartInfo { FileName = ResourcePacks.Dir, UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("Folder paczek: " + ex.Message); }
    }

    private void Done()
    {
        if (_changed) G.SetResourcePacks(_selected.Select(p => p.Id).ToList());
        G.SetScreen(_parent);
    }
    protected override void Escape() => Done();

    public override void OnClose()
    {
        foreach (var t in _icons.Values) t.Dispose();
        _icons.Clear();
        foreach (var p in _available.Concat(_selected)) p.Dispose();
    }

    private Texture2D Icon(ResourcePack p)
    {
        if (p.Icon == null) return null;
        if (!_icons.TryGetValue(p.Id, out var t)) _icons[p.Id] = t = new Texture2D(p.IconW, p.IconH, p.Icon, true);
        return t;
    }

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Wybierz paczki zasobów", W / 2f, 8, 0xFFFFFFFF);
        sb.TextCentered("Dostępne", LeftX + ListW / 2f, ListTop - 10, 0xFFFFFFFF);
        sb.TextCentered("Wybrane (wyżej = ważniejsza)", RightX + ListW / 2f, ListTop - 10, 0xFFFFFFFF);
        DrawList(_available, LeftX, _scrollL, false, mx, my);
        DrawList(_selected, RightX, _scrollR, true, mx, my);
        sb.TextCentered("§7Wrzuć pliki .zip do folderu paczek - pojawią się tu same.", W / 2f, H - 24, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }

    private void DrawList(List<ResourcePack> list, int x, float scroll, bool selected, int mx, int my)
    {
        var sb = G.Sb;
        sb.Fill(x, ListTop, ListW, ListBottom - ListTop, 0xC0000000);
        if (list.Count == 0)
            sb.TextCentered(selected ? "§7(brak - gra użyje własnych tekstur)" : "§7(brak paczek)", x + ListW / 2f, ListTop + 8, 0xFFFFFFFF);
        for (int i = 0; i < list.Count; i++)
        {
            int y = ListTop + RowOffset + i * Row - (int)scroll;
            if (y < ListTop || y + ItemH > ListBottom) continue;
            var p = list[i];
            bool hover = mx >= x && mx < x + ListW && my >= y && my < y + ItemH;
            if (hover) sb.Fill(x + 1, y - 1, ListW - 2, 34, 0x40FFFFFF);
            var tex = Icon(p);
            if (tex != null) sb.Draw(tex, x + 2, y, 32, 32);
            else sb.Draw(G.Gui.Dirt, x + 2, y, 32, 32, 0, 0, 1, 1, 0xFF808080);
            bool iconHover = hover && mx < x + 34;
            if (iconHover && p.Error == null)
            {
                sb.Fill(x + 2, y, 32, 32, 0xA0000000);
                if (!selected) sb.TextCentered(">", x + 18, y + 12, 0xFFFFFFFF);
                else
                {
                    sb.Text("<", x + 6, y + 12, mx < x + 18 ? 0xFFFFFFA0 : 0xFFFFFFFF);
                    if (i > 0) sb.Text("^", x + 23, y + 4, mx >= x + 18 && my < y + 16 ? 0xFFFFFFA0 : 0xFFFFFFFF);
                    if (i < list.Count - 1) sb.Text("v", x + 23, y + 20, mx >= x + 18 && my >= y + 16 ? 0xFFFFFFA0 : 0xFFFFFFFF);
                }
            }
            int tx = x + 38, tw = ListW - 42;
            sb.Text(Fit(p.Source != null ? p.Name + " §9[" + p.Source + "]" : p.Name, tw), tx, y + 1, 0xFFFFFFFF);
            string desc = p.Error != null ? "§c" + p.Error
                : p.Format != ResourcePacks.Format18 ? "§6Inna wersja MC (format " + p.Format + ")§7 - część może nie działać"
                : p.Description;
            var lines = Wrap(desc ?? "", tw);
            for (int l = 0; l < Math.Min(2, lines.Count); l++) sb.Text(lines[l], tx, y + 12 + l * 10, 0xFF808080);
        }
    }

    private string Fit(string s, int w)
    {
        if (G.Sb.Measure(s) <= w) return s;
        while (s.Length > 1 && G.Sb.Measure(s + "...") > w) s = s[..^1];
        return s + "...";
    }

    private List<string> Wrap(string s, int w)
    {
        var lines = new List<string>();
        foreach (var para in s.Replace("\r", "").Split('\n'))
        {
            string cur = "";
            foreach (var word in para.Split(' '))
            {
                string next = cur.Length == 0 ? word : cur + " " + word;
                if (G.Sb.Measure(next) <= w || cur.Length == 0) cur = next;
                else { lines.Add(cur); cur = word; }
            }
            lines.Add(cur);
        }
        if (lines.Count > 2) lines[1] = Fit(lines[1] + "...", w);
        return lines.Select(l => Fit(l, w)).ToList();
    }

    public override void MouseDown(int mx, int my, int btn)
    {
        // prawy przycisk (X na padzie) na wybranej paczce: wyżej na liście, czyli ważniejsza
        if (btn == 1 && my >= ListTop && my < ListBottom && mx >= RightX && mx < RightX + ListW)
        {
            int i = RowAt(my, _scrollR);
            if (i > 0 && i < _selected.Count) SwapSelected(i - 1, i);
            return;
        }
        if (btn == 0 && my >= ListTop && my < ListBottom)
        {
            bool left = mx >= LeftX && mx < LeftX + ListW, right = mx >= RightX && mx < RightX + ListW;
            if (left || right)
            {
                var list = left ? _available : _selected;
                int x = left ? LeftX : RightX;
                int i = RowAt(my, left ? _scrollL : _scrollR);
                if (i >= 0 && i < list.Count && (right || list[i].Error == null) && (my - ListTop - RowOffset + (int)(left ? _scrollL : _scrollR)) % Row < ItemH)
                {
                    var p = list[i];
                    if (left) { _available.RemoveAt(i); _selected.Insert(0, p); Changed(); }
                    else if (mx < x + 34)
                    {
                        int y = ListTop + RowOffset + i * Row - (int)_scrollR;
                        if (mx < x + 18) { _selected.RemoveAt(i); _available.Add(p); _available.Sort((a, b) => a.Builtin != b.Builtin ? (a.Builtin ? -1 : 1) : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase)); Changed(); }
                        else if (my < y + 16 && i > 0) SwapSelected(i - 1, i);
                        else if (my >= y + 16 && i < _selected.Count - 1) SwapSelected(i, i + 1);
                    }
                    return;
                }
            }
        }
        base.MouseDown(mx, my, btn);
    }

    private int RowAt(int my, float scroll) => (int)((my - ListTop - RowOffset + scroll) / Row);

    private void SwapSelected(int a, int b) { (_selected[a], _selected[b]) = (_selected[b], _selected[a]); Changed(); }

    private void Changed() { _changed = true; G.PlayUiClick(); }

    /// <summary>Pad: na wybranych kursor staje na lewej połowie ikony (A = usuń z wybranych), na dostępnych A = dodaj.</summary>
    public override IEnumerable<(int x, int y)> PadTargets() => this.ListTargets().Concat(base.PadTargets());

    private float MaxScroll(int count) => Math.Max(0, count * Row - (ListBottom - ListTop) + 4);

    public IEnumerable<PadList> PadLists()
    {
        yield return new PadList
        {
            Left = LeftX, Width = ListW, Top = ListTop, Bottom = ListBottom, Offset = RowOffset, RowH = Row, ItemH = ItemH, Count = _available.Count,
            MaxScroll = MaxScroll(_available.Count), GetScroll = () => _scrollL, SetScroll = v => _scrollL = v,
        };
        yield return new PadList
        {
            Left = RightX, Width = ListW, Top = ListTop, Bottom = ListBottom, Offset = RowOffset, RowH = Row, ItemH = ItemH, Count = _selected.Count,
            MaxScroll = MaxScroll(_selected.Count), GetScroll = () => _scrollR, SetScroll = v => _scrollR = v, TargetX = RightX + 9,
        };
    }

    public IEnumerable<(PadButton button, string label)> PadHints() => new[]
    {
        (PadButton.A, "Dodaj / usuń"), (PadButton.X, "Wyżej (ważniejsza)"), (PadButton.B, "Wstecz"),
    };

    public override void Scroll(int mx, int my, float d)
    {
        bool left = mx < W / 2;
        int count = left ? _available.Count : _selected.Count;
        float max = MaxScroll(count);
        if (left) _scrollL = Math.Clamp(_scrollL - d * 20, 0, max);
        else _scrollR = Math.Clamp(_scrollR - d * 20, 0, max);
    }
}
