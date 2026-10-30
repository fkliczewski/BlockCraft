using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

public static class GuiDraw
{
    public static void Panel(Game g, int x, int y, int w, int h)
    {
        var sb = g.Sb;
        sb.Fill(x + 1, y, w - 2, h, 0xFF000000); sb.Fill(x, y + 1, w, h - 2, 0xFF000000);
        sb.Fill(x + 1, y + 1, w - 2, h - 2, 0xFFC6C6C6);
        sb.Fill(x + 1, y + 1, w - 3, 2, 0xFFFFFFFF); sb.Fill(x + 1, y + 1, 2, h - 3, 0xFFFFFFFF);
        sb.Fill(x + 3, y + h - 3, w - 4, 2, 0xFF555555); sb.Fill(x + w - 3, y + 3, 2, h - 4, 0xFF555555);
        sb.Fill(x + 2, y + 2, w - 5, h - 5, 0xFFC6C6C6);
        sb.Fill(x + 3, y + 3, w - 6, h - 6, 0xFFC6C6C6);
    }
    public static void Slot(Game g, int x, int y, int size = 18)
    {
        var sb = g.Sb;
        sb.Fill(x, y, size, size, 0xFF8B8B8B);
        sb.Fill(x, y, size - 1, 1, 0xFF373737); sb.Fill(x, y, 1, size - 1, 0xFF373737);
        sb.Fill(x + 1, y + size - 1, size - 1, 1, 0xFFFFFFFF); sb.Fill(x + size - 1, y + 1, 1, size - 1, 0xFFFFFFFF);
    }
    public static void Tooltip(Game g, string text, int mx, int my)
    {
        var sb = g.Sb; var lines = text.Split('\n');
        int w = lines.Max(l => sb.Measure(l)), h = lines.Length * 10 - 2;
        int x = mx + 12, y = my - 12;
        if (x + w + 4 > g.GuiW) x = mx - w - 16;
        if (y + h + 4 > g.GuiH) y = g.GuiH - h - 4;
        sb.Fill(x - 3, y - 4, w + 6, h + 8, 0xF0100010);
        sb.Fill(x - 4, y - 3, w + 8, h + 6, 0xF0100010);
        sb.Rect(x - 3, y - 3, w + 6, h + 6, 0x505000FF);
        for (int i = 0; i < lines.Length; i++) sb.Text(lines[i], x, y + i * 10, i == 0 ? 0xFFFFFFFF : 0xFFA0A0A0);
    }
    public static string ItemTooltip(ItemStack s, bool adv)
    {
        var it = s.Item; if (it == null) return "?";
        string t = s.Name;
        if (s.Id == I.Potion) foreach (var effect in Potions.Effects(s)) t += "\n" + EffectLogic.Name(effect.Id) + " " + EnchantHelper.Roman(effect.Amplifier + 1) + (EffectLogic.Instant(effect.Id) ? "" : " (" + EffectLogic.Time(effect) + ")");
        if (it.ArmorSlot >= 0) t += "\n§9+" + it.ArmorPoints + " Pancerz";
        if (it.IsTool && it.AttackDamage > 1) t += "\n§9+" + it.AttackDamage + " Obrażenia od ataku";
        if (it.IsFood) t += "\n§7Głód: +" + it.Hunger;
        if (it.MaxDamage > 0) t += $"\n§7Wytrzymałość: {it.MaxDamage - s.Damage} / {it.MaxDamage}";
        if (adv) t += $"\n§8#{s.Id}" + (s.Damage > 0 && it.MaxDamage == 0 ? "/" + s.Damage : "");
        return t;
    }
}

/// <summary>Ekran kontenera: ekwipunek, stół, piec, skrzynia.</summary>
public class ContainerScreen : Screen
{
    protected readonly Container C;
    protected readonly Player P;
    protected int Left, Top;
    private readonly List<int> _drag = new();
    private int _dragBtn = -1; private bool _dragging;
    private long _lastClickTime; private int _lastClickSlot = -1;

    public ContainerScreen(Player p, Container c) { P = p; C = c; }
    public override bool PausesGame => false;
    protected override void Init() { Left = (W - C.Width) / 2; Top = (H - C.Height) / 2; }

    protected virtual void DrawBackground(int mx, int my)
    {
        GuiDraw.Panel(G, Left, Top, C.Width, C.Height);
        foreach (var s in C.Slots) GuiDraw.Slot(G, Left + s.X - 1, Top + s.Y - 1, s.IsOutput && C is not FurnaceContainer ? 18 : 18);
        var sb = G.Sb;
        if (C is PlayerContainer)
        {
            sb.Fill(Left + 26, Top + 8, 52, 70, 0xFF000000);
            sb.Text("Wytwarzanie", Left + 86, Top + 6, 0xFF404040, false);
            DrawArrow(Left + 133, Top + 28, 1f);
            for (int a = 0; a < 4; a++) if (C.Slots[5 + a].Stack == null) DrawArmorGhost(Left + 8, Top + 8 + a * 18, a);
            G.DrawPlayerPreview(Left + 51, Top + 75, 30, Left + 51 - G.MouseGuiX, Top + 25 - G.MouseGuiY);
        }
        else if (C is WorkbenchContainer)
        {
            sb.Text("Wytwarzanie", Left + 28, Top + 6, 0xFF404040, false);
            DrawArrow(Left + 90, Top + 35, 1f);
            GuiDraw.Slot(G, Left + 119, Top + 30, 26);
        }
        else if (C is FurnaceContainer fc)
        {
            sb.TextCentered("Piec", Left + C.Width / 2f, Top + 6, 0xFF404040, false);
            var f = fc.Furnace;
            float burn = f.CurrentItemBurnTime > 0 ? (float)f.BurnTime / f.CurrentItemBurnTime : 0;
            DrawFlame(Left + 57, Top + 37, f.BurnTime > 0 ? burn : 0);
            DrawArrow(Left + 79, Top + 34, f.CookTime / (float)FurnaceTileEntity.CookTotal, true);
            GuiDraw.Slot(G, Left + 111, Top + 30, 26);
        }
        else if (C is ChestContainer cc) sb.Text(cc.Title, Left + 8, Top + 6, 0xFF404040, false);
        if (C is not ChestContainer && C is not PlayerContainer) sb.Text("Ekwipunek", Left + 8, Top + C.Height - 96 + 2, 0xFF404040, false);
        else sb.Text("Ekwipunek", Left + 8, Top + 72, 0xFF404040, false);
    }

    private void DrawArmorGhost(int x, int y, int slot)
    {
        var sb = G.Sb;
        string[] keys = { "item_iron_helmet", "item_iron_chestplate", "item_iron_leggings", "item_iron_boots" };
        sb.DrawLayer(G.Renderer.Terrain, BlockCraft.Render.TextureGen.Layer(keys[slot]), x, y, 16, 16, 0x30000000);
    }

    protected void DrawArrow(int x, int y, float progress, bool furnace = false)
    {
        var sb = G.Sb;
        // strzałka 22x15
        string[] a =
        {
            "..............#.......", "..............##......", "..............###.....", "..............####....", "..............#####...",
            "######################", "#######################", "########################", "#######################", "######################",
            "..............#####...", "..............####....", "..............###.....", "..............##......", "..............#......."
        };
        int pw = (int)(24 * Math.Clamp(progress, 0, 1));
        for (int r = 0; r < a.Length; r++)
            for (int c = 0; c < a[r].Length; c++)
                if (a[r][c] == '#')
                {
                    uint col = furnace ? (c < pw ? 0xFFFFFFFF : 0xFF8B8B8B) : 0xFF8B8B8B;
                    sb.Fill(x + c, y + r, 1, 1, col);
                }
    }

    private void DrawFlame(int x, int y, float f)
    {
        var sb = G.Sb;
        string[] fl =
        {
            ".....##.....", "....###.....", "...####..#..", "...#####.##.", "..##########", ".###########", ".###########", "############",
            "############", "############", "############", ".##########.", "..########..", "...######..."
        };
        int h = fl.Length;
        int lit = (int)(h * f);
        for (int r = 0; r < h; r++)
            for (int c = 0; c < fl[r].Length; c++)
                if (fl[r][c] == '#')
                {
                    bool on = r >= h - lit;
                    uint col = on ? (r < h / 3 ? 0xFF00E0FF : r < 2 * h / 3 ? 0xFF0090FF : 0xFF0040E0) : 0xFF8B8B8B;
                    sb.Fill(x + c, y + r, 1, 1, col);
                }
    }

    protected int SlotAt(int mx, int my)
    {
        for (int i = 0; i < C.Slots.Count; i++)
        {
            var s = C.Slots[i];
            int sx = Left + s.X, sy = Top + s.Y;
            int pad = (s is CraftResultSlot || s is FurnaceOutputSlot) ? 4 : 0;
            if (mx >= sx - 1 - pad && my >= sy - 1 - pad && mx < sx + 17 + pad && my < sy + 17 + pad) return i;
        }
        return -1;
    }

    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        DrawBackground(mx, my);
        if (C is PlayerContainer) EffectPanel.Draw(G, P, Left, Top);
        int hover = SlotAt(mx, my);
        foreach (var s in C.Slots)
            if (s.Stack != null) G.DrawItem(s.Stack, Left + s.X, Top + s.Y);
        if (hover >= 0)
        {
            var s = C.Slots[hover];
            G.Sb.Fill(Left + s.X, Top + s.Y, 16, 16, 0x80FFFFFF);
        }
        foreach (int i in _drag) { var s = C.Slots[i]; G.Sb.Fill(Left + s.X, Top + s.Y, 16, 16, 0x60FFFFFF); }
        base.Render(mx, my, pt);
        if (P.Cursor != null) G.DrawItem(P.Cursor, mx - 8, my - 8);
        else if (hover >= 0 && C.Slots[hover].Stack != null) GuiDraw.Tooltip(G, GuiDraw.ItemTooltip(C.Slots[hover].Stack, G.ShowDebug), mx, my);
    }

    public override void MouseDown(int mx, int my, int btn)
    {
        int slot = SlotAt(mx, my);
        bool outside = mx < Left || my < Top || mx >= Left + C.Width || my >= Top + C.Height;
        bool shift = G.IsShiftDown;
        if (btn == 2) { if (slot >= 0) C.Click(slot, 2, ClickType.Clone, P); return; }
        if (slot < 0) { if (outside) C.Click(-999, btn, ClickType.Pickup, P); return; }
        if (shift) { C.Click(slot, btn, ClickType.QuickMove, P); return; }
        if (btn == 0 && slot == _lastClickSlot && Environment.TickCount64 - _lastClickTime < 250 && P.Cursor != null)
        {
            C.Click(slot, 0, ClickType.PickupAll, P); _lastClickSlot = -1; return;
        }
        _lastClickSlot = slot; _lastClickTime = Environment.TickCount64;
        if (P.Cursor != null && !C.Slots[slot].IsOutput)
        {
            _dragging = true; _dragBtn = btn; _drag.Clear(); _drag.Add(slot);
            return;
        }
        C.Click(slot, btn, ClickType.Pickup, P);
    }

    public override void MouseMove(int mx, int my)
    {
        if (!_dragging) return;
        int slot = SlotAt(mx, my);
        if (slot >= 0 && !_drag.Contains(slot) && !C.Slots[slot].IsOutput && P.Cursor != null)
        {
            var st = C.Slots[slot].Stack;
            if (st == null || st.CanStackWith(P.Cursor)) _drag.Add(slot);
        }
    }

    public override void MouseUp(int mx, int my, int btn)
    {
        if (!_dragging || btn != _dragBtn) return;
        _dragging = false;
        if (_drag.Count == 1) C.Click(_drag[0], btn, ClickType.Pickup, P);
        else if (_drag.Count > 1) C.Distribute(_drag.ToList(), btn, P);
        _drag.Clear();
    }

    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (k == Keys.Escape || k == Keys.E) { G.SetScreen(null); return; }
        int slot = SlotAt(G.MouseGuiX, G.MouseGuiY);
        if (k >= Keys.D1 && k <= Keys.D9 && slot >= 0) { C.Click(slot, k - Keys.D1, ClickType.Swap, P); return; }
        if (k == Keys.Q && slot >= 0) { C.Click(slot, ctrl ? 1 : 0, ClickType.Throw, P); return; }
    }

    public override void Tick()
    {
        if (C is FurnaceContainer fc && P.World.GetTileEntity(fc.Furnace.X, fc.Furnace.Y, fc.Furnace.Z) != fc.Furnace) G.SetScreen(null);
        if (C is WorkbenchContainer wb && P.World.GetBlock(wb.Bx, wb.By, wb.Bz) != B.CraftingTable) G.SetScreen(null);
        if (C is ChestContainer ch && ch.Chest is TileEntity te && P.World.GetTileEntity(te.X, te.Y, te.Z) != te) G.SetScreen(null);
    }

    public override void OnClose() { P.CloseContainer(); }
}

/// <summary>Ekwipunek kreatywny z zakładkami.</summary>
public sealed class CreativeScreen : Screen
{
    private readonly Player _p;
    private static int _tab;
    private float _scroll;
    private List<ItemStack> _items = new();
    private TextField _search;
    private int _left, _top;
    private const int PW = 195, PH = 136;
    private static readonly string[] TabNames = { "Budulce", "Dekoracje", "Redstone", "Różne", "Jedzenie", "Narzędzia", "Walka", "Materiały", "Szukaj", "Ekwipunek przetrwania", "Transport", "Alchemia" };
    private static readonly int[] TabIcons = { B.Bricks, B.Poppy, I.Redstone, I.LavaBucket, I.Apple, I.IronAxe, I.GoldSword, I.Stick, I.Book, B.Chest, B.Rail, I.Potion };
    private static readonly int[] TabOrder = { 0, 1, 2, 10, 3, 8, 4, 5, 6, 11, 7, 9 };
    private readonly PlayerContainer _inv;

    public CreativeScreen(Player p) { _p = p; _inv = (PlayerContainer)p.InventoryContainer; }
    public override bool PausesGame => false;

    protected override void Init()
    {
        _left = (W - PW) / 2; _top = (H - PH) / 2;
        _search = Add(new TextField(_left + 82, _top + 6, 89, 10) { Visible = _tab == 8, Focused = _tab == 8, MaxLength = 30 });
        Refresh();
    }

    private void Refresh()
    {
        if (_tab == 8)
        {
            string q = _search?.Text.ToLowerInvariant() ?? "";
            _items = ItemRegistry.CreativeItems(-1).Where(s => s.Name.ToLowerInvariant().Contains(q) || (s.Item?.Key ?? "").Contains(q)).ToList();
        }
        else if (_tab != 9) _items = ItemRegistry.CreativeItems(_tab);
        _scroll = 0;
    }

    private int Rows => (_items.Count + 8) / 9;
    private int ScrollRow => (int)MathF.Round(_scroll * Math.Max(0, Rows - 5));

    private (int x, int y) TabPos(int t)
    {
        int pos = Array.IndexOf(TabOrder, t);
        return (_left + pos % 6 * 29, pos < 6 ? _top - 28 : _top + PH - 4);
    }

    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        var sb = G.Sb;
        // zakładki za panelem
        for (int t = 0; t < TabNames.Length; t++) if (t != _tab) DrawTab(t, false);
        GuiDraw.Panel(G, _left, _top, PW, PH);
        DrawTab(_tab, true);
        sb.Text(TabNames[_tab], _left + 8, _top + 6, 0xFF404040, false);
        ItemStack hoverStack = null;
        if (_tab != 9)
        {
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 9; c++)
                {
                    int x = _left + 9 + c * 18, y = _top + 18 + r * 18;
                    GuiDraw.Slot(G, x - 1, y - 1);
                    int idx = (ScrollRow + r) * 9 + c;
                    if (idx < _items.Count)
                    {
                        G.DrawItem(_items[idx], x, y, false);
                        if (mx >= x - 1 && my >= y - 1 && mx < x + 17 && my < y + 17) { sb.Fill(x, y, 16, 16, 0x80FFFFFF); hoverStack = _items[idx]; }
                    }
                }
            // pasek przewijania
            sb.Fill(_left + 174, _top + 17, 14, 92, 0xFF8B8B8B);
            sb.Rect(_left + 174, _top + 17, 14, 92, 0xFF373737);
            int sy = _top + 18 + (int)(_scroll * 75);
            bool can = Rows > 5;
            sb.Fill(_left + 175, sy, 12, 15, can ? 0xFFC6C6C6 : 0xFF999999);
            sb.Fill(_left + 175, sy, 11, 1, 0xFFFFFFFF); sb.Fill(_left + 175, sy, 1, 14, 0xFFFFFFFF);
            sb.Fill(_left + 176, sy + 14, 11, 1, 0xFF555555); sb.Fill(_left + 186, sy + 1, 1, 14, 0xFF555555);
        }
        else
        {
            // ekwipunek przetrwania
            for (int i = 0; i < _inv.Slots.Count; i++)
            {
                var (x, y) = InvSlotPos(i);
                if (x < 0) continue;
                GuiDraw.Slot(G, x - 1, y - 1);
                var st = _inv.Slots[i].Stack;
                if (st != null) G.DrawItem(st, x, y);
                if (mx >= x - 1 && my >= y - 1 && mx < x + 17 && my < y + 17) { sb.Fill(x, y, 16, 16, 0x80FFFFFF); hoverStack = st; }
            }
            sb.Fill(_left + 107, _top + 5, 36, 47, 0xFF000000);
            G.DrawPlayerPreview(_left + 125, _top + 48, 20, _left + 125 - mx, _top + 15 - my);
            // kosz
            GuiDraw.Slot(G, _left + 172, _top + 111);
            sb.Text("X", _left + 177, _top + 116, 0xFFC03030, false);
        }
        // hotbar
        for (int c = 0; c < 9; c++)
        {
            int x = _left + 9 + c * 18, y = _top + 112;
            GuiDraw.Slot(G, x - 1, y - 1);
            var st = _p.Inventory.Main[c];
            if (st != null) G.DrawItem(st, x, y);
            if (mx >= x - 1 && my >= y - 1 && mx < x + 17 && my < y + 17) { sb.Fill(x, y, 16, 16, 0x80FFFFFF); hoverStack = st; }
        }
        base.Render(mx, my, pt);
        for (int t = 0; t < TabNames.Length; t++)
        {
            var (tx, ty) = TabPos(t);
            if (mx >= tx && my >= ty && mx < tx + 28 && my < ty + 32 && _p.Cursor == null) GuiDraw.Tooltip(G, TabNames[t], mx, my);
        }
        if (_p.Cursor != null) G.DrawItem(_p.Cursor, mx - 8, my - 8);
        else if (hoverStack != null) GuiDraw.Tooltip(G, GuiDraw.ItemTooltip(hoverStack, G.ShowDebug), mx, my);
    }

    private (int, int) InvSlotPos(int i)
    {
        if (i >= 5 && i <= 8) return (_left + 54 + (i - 5 < 2 ? 0 : 54) , _top + 6 + ((i - 5) % 2) * 27);
        if (i >= _inv.FirstPlayerSlot && i < _inv.FirstPlayerSlot + 27) { int k = i - _inv.FirstPlayerSlot; return (_left + 9 + (k % 9) * 18, _top + 54 + (k / 9) * 18); }
        return (-1, -1);
    }

    private void DrawTab(int t, bool active)
    {
        var (x, y) = TabPos(t);
        G.Sb.DrawPx(G.Gui.Widgets, x, y, active ? 28 : 0, 110, 28, 32);
        G.DrawItem(new ItemStack(TabIcons[t]), x + 6, y + (Array.IndexOf(TabOrder, t) < 6 ? 9 : 7), false);
    }

    public override void MouseDown(int mx, int my, int btn)
    {
        for (int t = 0; t < TabNames.Length; t++)
        {
            var (tx, ty) = TabPos(t);
            if (mx >= tx && my >= ty && mx < tx + 28 && my < ty + 32) { _tab = t; G.PlayUiClick(); _search.Visible = t == 8; _search.Focused = t == 8; Refresh(); return; }
        }
        base.MouseDown(mx, my, btn);
        bool shift = G.IsShiftDown;
        // hotbar
        for (int c = 0; c < 9; c++)
        {
            int x = _left + 9 + c * 18, y = _top + 112;
            if (mx >= x - 1 && my >= y - 1 && mx < x + 17 && my < y + 17)
            {
                if (shift && _p.Cursor == null) { _p.Inventory.Main[c] = null; return; }
                _inv.Click(_inv.FirstPlayerSlot + 27 + c, btn == 2 ? 2 : btn, btn == 2 ? ClickType.Clone : ClickType.Pickup, _p);
                return;
            }
        }
        if (_tab != 9)
        {
            for (int r = 0; r < 5; r++)
                for (int c = 0; c < 9; c++)
                {
                    int x = _left + 9 + c * 18, y = _top + 18 + r * 18;
                    if (mx < x - 1 || my < y - 1 || mx >= x + 17 || my >= y + 17) continue;
                    int idx = (ScrollRow + r) * 9 + c;
                    if (_p.Cursor != null) { if (idx < _items.Count && _p.Cursor.CanStackWith(_items[idx]) && btn == 1) { if (_p.Cursor.Count > 1) _p.Cursor.Count--; else _p.Cursor = null; } else _p.Cursor = null; return; }
                    if (idx >= _items.Count) return;
                    var it = _items[idx];
                    if (shift) { var s = it.CopyWith(it.MaxStack); _p.Inventory.Add(s); return; }
                    _p.Cursor = it.CopyWith(btn == 0 ? it.MaxStack : 1);
                    if (btn == 2) _p.Cursor = it.CopyWith(it.MaxStack);
                    return;
                }
            if (mx >= _left + 174 && mx < _left + 188 && my >= _top + 17 && my < _top + 109) { _dragScroll = true; ScrollTo(my); return; }
        }
        else
        {
            for (int i = 0; i < _inv.Slots.Count; i++)
            {
                var (x, y) = InvSlotPos(i);
                if (x < 0) continue;
                if (mx >= x - 1 && my >= y - 1 && mx < x + 17 && my < y + 17)
                {
                    if (shift && _p.Cursor == null) { _inv.Slots[i].Stack = null; return; }
                    _inv.Click(i, btn, ClickType.Pickup, _p); return;
                }
            }
            if (mx >= _left + 172 && my >= _top + 111 && mx < _left + 190 && my < _top + 129) { if (shift) _p.Inventory.Clear(); _p.Cursor = null; return; }
        }
        bool outside = mx < _left || my < _top || mx >= _left + PW || my >= _top + PH;
        if (outside && _p.Cursor != null) { _p.DropStack(_p.Cursor.CopyWith(btn == 0 ? _p.Cursor.Count : 1), true); if (btn == 0) _p.Cursor = null; else if (--_p.Cursor.Count <= 0) _p.Cursor = null; }
    }
    private bool _dragScroll;
    private void ScrollTo(int my) { _scroll = Math.Clamp((my - _top - 25) / 75f, 0, 1); }
    public override void MouseMove(int mx, int my) { if (_dragScroll) ScrollTo(my); base.MouseMove(mx, my); }
    public override void MouseUp(int mx, int my, int btn) { _dragScroll = false; base.MouseUp(mx, my, btn); }
    public override void Scroll(int mx, int my, float d)
    {
        if (Rows <= 5) return;
        _scroll = Math.Clamp(_scroll - d / (Rows - 5), 0, 1);
    }
    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (_search.Focused && _tab == 8)
        {
            if (k == Keys.Escape) { G.SetScreen(null); return; }
            if (_search.Key(G, k, ctrl, shift)) Refresh();
            return;
        }
        if (k == Keys.Escape || k == Keys.E) { G.SetScreen(null); return; }
    }
    public override void CharTyped(char c)
    {
        if (_tab != 8) { if (c == 't' || c == 'T') { } return; }
        if (_search.Char(G, c)) Refresh();
    }
    public override void OnClose()
    {
        if (_p.Cursor != null) { if (!_p.Inventory.Add(_p.Cursor)) _p.DropStack(_p.Cursor, false); _p.Cursor = null; }
        _p.InventoryContainer.OnClose(_p);
    }
}

public sealed class ChatScreen : Screen
{
    private readonly TextField _f;
    public static readonly List<string> History = new();
    private int _hist = -1;
    private List<string> _completions; private int _compIdx;
    public ChatScreen(string initial) { _f = new TextField(2, 0, 0, 12) { Text = initial, Focused = true, MaxLength = 100 }; }
    public override bool PausesGame => false;
    protected override void Init()
    {
        _f.X = 2; _f.Y = H - 14; _f.W = W - 4; _f.H = 12;
        Widgets.Add(_f);
    }
    public override void Render(int mx, int my, float pt)
    {
        G.Sb.Fill(2, H - 14, W - 4, 12, 0x80000000);
        string t = _f.Text;
        G.Sb.Text(t + ((Environment.TickCount64 / 300) % 2 == 0 ? "_" : ""), 4, H - 12, 0xFFE0E0E0);
        if (_completions != null && _completions.Count > 1)
        {
            int y = H - 26 - Math.Min(10, _completions.Count) * 10;
            for (int i = 0; i < Math.Min(10, _completions.Count); i++)
                G.Sb.Text(_completions[i], 4, y + i * 10, i == _compIdx ? 0xFFFFFF55 : 0xFFA0A0A0);
        }
    }
    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (k == Keys.Escape) { G.SetScreen(null); return; }
        if (k is Keys.Enter or Keys.KeyPadEnter)
        {
            string s = _f.Text.Trim();
            if (s.Length > 0) { History.Add(s); G.SendChat(s); }
            G.SetScreen(null); return;
        }
        if (k == Keys.Up && History.Count > 0) { _hist = _hist < 0 ? History.Count - 1 : Math.Max(0, _hist - 1); _f.Text = History[_hist]; return; }
        if (k == Keys.Down && _hist >= 0) { _hist++; if (_hist >= History.Count) { _hist = -1; _f.Text = ""; } else _f.Text = History[_hist]; return; }
        if (k == Keys.Tab && _f.Text.StartsWith("/"))
        {
            if (_completions == null) { _completions = Commands.Complete(_f.Text, G.World); _compIdx = 0; }
            else _compIdx = (_compIdx + 1) % Math.Max(1, _completions.Count);
            if (_completions.Count > 0) _f.Text = _completions[_compIdx] + (_completions.Count == 1 ? " " : "");
            if (_completions.Count <= 1) _completions = null;
            return;
        }
        _completions = null;
        _f.Key(G, k, ctrl, shift);
    }
    public override void CharTyped(char c) { _completions = null; _f.Char(G, c); }
    public override void MouseDown(int mx, int my, int btn) { }
}
