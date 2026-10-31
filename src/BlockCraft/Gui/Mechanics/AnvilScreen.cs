using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

/// <summary>GuiRepair z 1.8: pole nazwy (30 znaków), dwa wejścia, wynik i koszt w poziomach ("Zbyt drogie!" od 40).</summary>
public sealed class AnvilScreen : ContainerScreen
{
    private readonly AnvilContainer _ac;
    private readonly TextField _name = new(0, 0, 103, 12) { MaxLength = AnvilRules.MaxNameLength, Focused = true };
    private ItemStack _lastLeft;

    public AnvilScreen(Player p, AnvilContainer c) : base(p, c) { _ac = c; }

    protected override void Init()
    {
        base.Init();
        _name.X = Left + 62; _name.Y = Top + 24;
    }

    private static uint Rgb(uint rgb) => BlockCraft.Render.SpriteBatch.Hex(rgb);

    /// <summary>GuiRepair.sendSlotContents(slot 0): pole przyjmuje nazwę nowego przedmiotu.</summary>
    private void SyncLeft()
    {
        var left = _ac.Inv.Get(0);
        if (ReferenceEquals(left, _lastLeft) || left != null && _lastLeft != null && left.Id == _lastLeft.Id && left.Damage == _lastLeft.Damage && Nbt.Same(left.Tag, _lastLeft.Tag)) { _lastLeft = left; return; }
        _lastLeft = left;
        _name.Text = left == null ? "" : left.Name;
        if (left != null) Rename();
    }

    /// <summary>GuiRepair.renameItem: nazwa domyślna nieprzemianowanego przedmiotu liczy się jako pusta.</summary>
    private void Rename()
    {
        string s = _name.Text;
        var left = _ac.Inv.Get(0);
        if (left != null && !AnvilRules.HasCustomName(left) && s == left.Name) s = "";
        _ac.SetName(s);
        if (P.World.IsRemote) G.Client?.AnvilName(s);
    }

    protected override void DrawBackground(int mx, int my)
    {
        SyncLeft();
        var sb = G.Sb;
        GuiDraw.Panel(G, Left, Top, C.Width, C.Height);
        sb.Text(C.Title, Left + 60, Top + 6, 0xFF404040, false);
        sb.Text("Ekwipunek", Left + 8, Top + C.Height - 96 + 2, 0xFF404040, false);
        foreach (var s in C.Slots) GuiDraw.Slot(G, Left + s.X - 1, Top + s.Y - 1, 18);
        // młotek w lewym górnym rogu jak w anvil.png
        sb.Fill(Left + 18, Top + 12, 22, 7, Rgb(0x4A4A4A)); sb.Fill(Left + 27, Top + 19, 4, 12, Rgb(0x6B4A2B));
        // plus i strzałka
        sb.Fill(Left + 53, Top + 51, 13, 3, Rgb(0x8B8B8B)); sb.Fill(Left + 58, Top + 46, 3, 13, Rgb(0x8B8B8B));
        DrawArrow(Left + 102, Top + 48, 1f);
        bool enabled = _ac.Inv.Get(0) != null;
        if (!enabled || _ac.Output.Get(0) == null && _ac.Inv.Get(1) != null)
        {
            // przekreślona strzałka (brak wyniku)
            for (int i = 0; i < 15; i++) { sb.Fill(Left + 106 + i, Top + 48 + i, 2, 1, Rgb(0xC04040)); sb.Fill(Left + 120 - i, Top + 48 + i, 2, 1, Rgb(0xC04040)); }
        }
        sb.Fill(_name.X - 2, _name.Y - 2, _name.W + 2, _name.H + 2, enabled ? 0xFF000000 : Rgb(0x2B2B2B));
        string t = _name.Text;
        while (t.Length > 0 && sb.Measure(t) > _name.W - 6) t = t[1..];
        sb.Text(t, _name.X + 2, _name.Y + 2, enabled ? Rgb(0xE0E0E0) : Rgb(0x707070), true);
        if (enabled && Environment.TickCount64 / 300 % 2 == 0) sb.Text("_", _name.X + 2 + sb.Measure(t), _name.Y + 2, Rgb(0xE0E0E0), true);
        // koszt (drawGuiContainerForegroundLayer)
        int cost = _ac.MaximumCost;
        if (cost > 0)
        {
            uint col = Rgb(0x80FF20); bool show = true;
            string s = "Koszt zaklęcia: " + cost; // container.repair.cost (pl_PL)
            if (cost >= 40 && !P.Creative) { s = "Zbyt drogie!"; col = Rgb(0xFF6060); }
            else if (_ac.Output.Get(0) == null) show = false;
            else if (!C.Slots[2].CanTake(P)) col = Rgb(0xFF6060);
            if (show)
            {
                int w = sb.Measure(s), x = Left + C.Width - 8 - w - 2;
                sb.Fill(x - 2, Top + 65, w + 6, 12, 0x4F000000);
                sb.Text(s, x, Top + 67, col, true);
            }
        }
    }

    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (k == Keys.Escape) { G.SetScreen(null); return; }
        if (_ac.Inv.Get(0) != null && _name.Key(G, k, ctrl, shift)) { Rename(); return; }
        if (k == Keys.E) return; // E pisze w polu nazwy jak w 1.8, nie zamyka okna
        base.KeyDown(k, ctrl, shift);
    }

    public override void CharTyped(char c)
    {
        if (_ac.Inv.Get(0) == null || c == '§') return;
        if (_name.Char(G, c)) Rename();
    }

    public override void Tick() { if (!_ac.StillValid(P)) G.SetScreen(null); }
}
