using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.Render;

namespace BlockCraft.Gui;

/// <summary>
/// GuiStats 1.8: zakładki Ogólne / Bloki / Przedmioty / Moby. Bloki: wytworzone, użyte, wykopane; przedmioty: zniszczone,
/// wytworzone, użyte (sortowanie kliknięciem w nagłówek). W sieci statystyki przychodzą od hosta (prośba przy otwarciu).
/// </summary>
public sealed class StatsScreen : Screen
{
    public enum Tab { General, Blocks, Items, Mobs }
    private readonly Screen _parent;
    public Tab Current { get; private set; } = Tab.General;
    private int _scroll, _sortColumn = -1, _sortDir = 1;
    private bool _waiting;
    private Button _blocks, _items, _mobs;
    public static int Received;
    private int _receivedAtOpen;

    public StatsScreen(Screen parent) { _parent = parent; }

    protected override void Init()
    {
        if (G.Client != null && !_waiting) { _waiting = true; _receivedAtOpen = Received; Bl2Net.RequestStatistics(G.Client); }
        Add(new Button(W / 2 + 4, H - 28, 150, 20, "Gotowe", () => G.SetScreen(_parent)));
        Add(new Button(W / 2 - 160, H - 52, 80, 20, "Ogólne", () => Select(Tab.General)));
        _blocks = Add(new Button(W / 2 - 80, H - 52, 80, 20, "Bloki", () => Select(Tab.Blocks)));
        _items = Add(new Button(W / 2, H - 52, 80, 20, "Przedmioty", () => Select(Tab.Items)));
        _mobs = Add(new Button(W / 2 + 80, H - 52, 80, 20, "Moby", () => Select(Tab.Mobs)));
    }

    public void Select(Tab t) { Current = t; _scroll = 0; _sortColumn = -1; }
    protected override void Escape() => G.SetScreen(_parent);
    private Dictionary<string, int> Stats => G.Player?.Statistics ?? new();
    private bool Loading => _waiting && Received == _receivedAtOpen;

    // ---------- dane zakładek ----------
    public static readonly string[] BlockCols = { Bl2Stats.Craft, Bl2Stats.Use, Bl2Stats.Mine };
    public static readonly string[] ItemCols = { Bl2Stats.Break, Bl2Stats.Craft, Bl2Stats.Use };
    public static readonly string[] BlockHeads = { "Wytworzono", "Użyto", "Wykopano" };
    public static readonly string[] ItemHeads = { "Zniszczono", "Wytworzono", "Użyto" };

    /// <summary>Wiersze listy bloków albo przedmiotów: tylko pozycje z niezerową statystyką (jak GuiStats.StatsBlock/StatsItem).</summary>
    public static List<int> Rows(Dictionary<string, int> stats, bool blocks, int sortColumn = -1, int sortDir = 1)
    {
        var cols = blocks ? BlockCols : ItemCols;
        var ids = new List<int>();
        foreach (var it in Item.All)
        {
            if (blocks != it.Id < 256) continue;
            if (cols.Any(c => stats.GetValueOrDefault(c + it.Id) > 0)) ids.Add(it.Id);
        }
        ids.Sort();
        if (sortColumn >= 0) ids = ids.OrderBy(id => -sortDir * stats.GetValueOrDefault(cols[sortColumn] + id)).ThenBy(id => id).ToList();
        return ids;
    }

    public static List<SpawnEggs.Egg> MobRows(Dictionary<string, int> stats)
        => SpawnEggs.All.Where(e => stats.GetValueOrDefault(Bl2Stats.KillEntity + e.Key) > 0 || stats.GetValueOrDefault(Bl2Stats.KilledBy + e.Key) > 0).ToList();

    public override void Scroll(int mx, int my, float delta) => _scroll = Math.Max(0, _scroll - (int)(delta * 18));

    public override void MouseDown(int mx, int my, int btn)
    {
        if ((Current is Tab.Blocks or Tab.Items) && my >= 32 && my < 50)
        {
            int left = W / 2 - 110;
            for (int c = 0; c < 3; c++)
            {
                int cx = left + 115 + c * 50;
                if (mx >= cx - 45 && mx < cx + 2)
                {
                    if (_sortColumn == c) { if (_sortDir == 1) _sortDir = -1; else _sortColumn = -1; }
                    else { _sortColumn = c; _sortDir = 1; }
                    G.PlayUiClick();
                    return;
                }
            }
        }
        base.MouseDown(mx, my, btn);
    }

    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        var sb = G.Sb; var stats = Stats;
        sb.TextCentered("Statystyki", W / 2f, 20, 0xFFFFFFFF);
        _blocks.Enabled = Rows(stats, true).Count > 0; _items.Enabled = Rows(stats, false).Count > 0; _mobs.Enabled = MobRows(stats).Count > 0;
        if (Loading) { sb.TextCentered("Pobieranie statystyk...", W / 2f, H / 2f, 0xFFFFFFFF); base.Render(mx, my, pt); return; }
        int top = 32, bottom = H - 64, left = W / 2 - 110;
        sb.Fill(0, top, W, bottom - top, 0x60000000);
        switch (Current)
        {
            case Tab.General:
                {
                    int y = top + 4 - _scroll, i = 0;
                    _scroll = Math.Min(_scroll, Math.Max(0, Bl2Stats.General.Length * 10 - (bottom - top) + 8));
                    foreach (var d in Bl2Stats.General)
                    {
                        if (y >= top && y + 10 <= bottom)
                        {
                            uint col = i % 2 == 0 ? 0xFFFFFFFF : 0xFF909090;
                            sb.Text(d.Name, left + 2, y + 1, col);
                            string v = Bl2Stats.Format(d.Format, stats.GetValueOrDefault(d.Key));
                            sb.Text(v, left + 215 - sb.Measure(v), y + 1, col);
                        }
                        y += 10; i++;
                    }
                    break;
                }
            case Tab.Blocks:
            case Tab.Items:
                {
                    bool blocks = Current == Tab.Blocks;
                    var cols = blocks ? BlockCols : ItemCols; var heads = blocks ? BlockHeads : ItemHeads;
                    for (int c = 0; c < 3; c++)
                    {
                        string h = heads[c] + (_sortColumn == c ? (_sortDir == 1 ? " v" : " ^") : "");
                        sb.Text(h, left + 115 + c * 50 - sb.Measure(h), top + 5, _sortColumn == c ? 0xFFFFFF80 : 0xFFFFFFFF);
                    }
                    var rows = Rows(stats, blocks, _sortColumn, _sortDir);
                    _scroll = Math.Min(_scroll, Math.Max(0, rows.Count * 20 - (bottom - top - 20) + 4));
                    int y = top + 20 - _scroll, i = 0;
                    foreach (int id in rows)
                    {
                        if (y >= top + 18 && y + 20 <= bottom)
                        {
                            bool odd = i % 2 == 1;
                            GuiDraw.Slot(G, left, y, 18);
                            G.DrawItem(new ItemStack(id), left + 1, y + 1, false);
                            for (int c = 0; c < 3; c++)
                            {
                                string v = Bl2Stats.Format(StatFormat.Simple, stats.GetValueOrDefault(cols[c] + id));
                                sb.Text(v, left + 115 + c * 50 - sb.Measure(v), y + 5, odd ? 0xFF909090 : 0xFFFFFFFF);
                            }
                            if (mx >= left && mx < left + 18 && my >= y && my < y + 18) sb.Text(new ItemStack(id).Name, mx + 12, my - 12, 0xFFFFFFFF);
                        }
                        y += 20; i++;
                    }
                    break;
                }
            case Tab.Mobs:
                {
                    var rows = MobRows(stats);
                    _scroll = Math.Min(_scroll, Math.Max(0, rows.Count * 36 - (bottom - top) + 4));
                    int y = top + 4 - _scroll;
                    foreach (var e in rows)
                    {
                        if (y >= top && y + 36 <= bottom)
                        {
                            int kills = stats.GetValueOrDefault(Bl2Stats.KillEntity + e.Key), deaths = stats.GetValueOrDefault(Bl2Stats.KilledBy + e.Key);
                            sb.Text(e.Name, left + 2, y + 1, 0xFFFFFFFF);
                            sb.Text(kills > 0 ? $"Zabito: {kills}" : "Nigdy nie zabito", left + 4, y + 11, kills > 0 ? 0xFFE0E0E0 : 0xFF909090);
                            sb.Text(deaths > 0 ? $"Zabił cię {deaths} {(deaths == 1 ? "raz" : "razy")}" : "Nigdy cię nie zabił", left + 4, y + 21, deaths > 0 ? 0xFFE0E0E0 : 0xFF909090);
                        }
                        y += 36;
                    }
                    break;
                }
        }
        base.Render(mx, my, pt);
    }
}
