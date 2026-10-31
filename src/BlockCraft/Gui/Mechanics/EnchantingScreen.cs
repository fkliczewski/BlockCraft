using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Gui;

/// <summary>GuiEnchantment z 1.8: trzy przyciski z wymaganym poziomem, podpowiedzią zaklęcia i kosztem lazurytu.</summary>
public sealed class EnchantingScreen : ContainerScreen
{
    private readonly EnchantingContainer _ec;
    // EnchantmentNameParts z 1.8 (losowe "galaktyczne" napisy na przyciskach)
    private static readonly string[] Words = ("the elder scrolls klaatu berata niktu xyzzy bless curse light darkness fire air earth water hot dry cold wet ignite snuff embiggen twist shorten stretch fiddle destroy imbue galvanize enchant free limited range of towards inside sphere cube self other ball mental physical grow shrink demon elemental spirit animal creature beast humanoid undead fresh stale ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
    private readonly string[] _names = new string[3];
    private int _namesSeed = int.MinValue;

    public EnchantingScreen(Player p, EnchantingContainer c) : base(p, c) { _ec = c; }

    private static uint Rgb(uint rgb) => BlockCraft.Render.SpriteBatch.Hex(rgb);
    private (int x, int y) ButtonPos(int i) => (Left + 60, Top + 14 + 19 * i);
    private static bool In(int mx, int my, int x, int y, int w, int h) => mx >= x && my >= y && mx < x + w && my < y + h;

    private void EnsureNames()
    {
        if (_namesSeed == P.EnchantmentSeed && _names[0] != null) return;
        _namesSeed = P.EnchantmentSeed;
        var r = new World.JRandom(_namesSeed);
        for (int i = 0; i < 3; i++)
        {
            int n = r.NextInt(2) + 3; string s = "";
            for (int k = 0; k < n; k++) { if (k > 0) s += " "; s += Words[r.NextInt(Words.Length)]; }
            _names[i] = s;
        }
    }

    protected override void DrawBackground(int mx, int my)
    {
        var sb = G.Sb;
        GuiDraw.Panel(G, Left, Top, C.Width, C.Height);
        sb.Text(C.Title, Left + 12, Top + 5, 0xFF404040, false);
        sb.Text("Ekwipunek", Left + 8, Top + C.Height - 96 + 2, 0xFF404040, false);
        foreach (var s in C.Slots) GuiDraw.Slot(G, Left + s.X - 1, Top + s.Y - 1, 18);
        if (C.Slots[1].Stack == null) G.DrawItem(new ItemStack(I.Lapis), Left + 35, Top + 47, false);
        if (C.Slots[1].Stack == null) sb.Fill(Left + 35, Top + 47, 16, 16, 0xA08B8B8B);
        DrawBook(Left + 14, Top + 16);
        EnsureNames();
        var offers = _ec.Offers;
        for (int i = 0; i < 3; i++)
        {
            var (bx, by) = ButtonPos(i);
            int state = _ec.ButtonState(i);
            bool hover = state == 2 && In(mx, my, bx, by, 108, 19);
            sb.Fill(bx, by, 108, 19, Rgb(0x2B2B2B));
            sb.Fill(bx + 1, by + 1, 106, 17, state == 0 ? Rgb(0x8B8B8B) : state == 1 ? Rgb(0x8E8574) : hover ? Rgb(0xE6DAC0) : Rgb(0xCDBE9F));
            if (state == 0) continue;
            // ikona kosztu: kafelek lazurytu z liczbą sztuk
            sb.Fill(bx + 2, by + 2, 15, 15, state == 2 ? Rgb(0x2645A8) : Rgb(0x55504A));
            sb.Text((i + 1).ToString(), bx + 7, by + 6, state == 2 ? 0xFFFFFFFF : Rgb(0x9C9C9C), true);
            string level = offers[i].RequiredLevel.ToString();
            uint lc = state == 2 ? Rgb(0x80FF20) : Rgb(0x407F10);
            sb.Text(level, bx + 106 - sb.Measure(level), by + 6, lc, true);
            string name = _names[i]; int maxW = 104 - 20 - sb.Measure(level) - 2;
            while (name.Length > 0 && sb.Measure(name) > maxW) name = name[..^1];
            sb.Text(name, bx + 20, by + 6, state == 2 ? Rgb(0x685E4A) : Rgb(0x4A4238), false);
        }
    }

    private void DrawBook(int x, int y)
    {
        var sb = G.Sb;
        sb.Fill(x, y, 30, 22, Rgb(0x3B2414)); sb.Fill(x + 2, y + 2, 12, 18, Rgb(0xE9DDC0)); sb.Fill(x + 16, y + 2, 12, 18, Rgb(0xE9DDC0));
        for (int l = 0; l < 5; l++) { sb.Fill(x + 4, y + 5 + l * 3, 8, 1, Rgb(0x8C7C66)); sb.Fill(x + 18, y + 5 + l * 3, 8, 1, Rgb(0x8C7C66)); }
        sb.Fill(x + 14, y + 1, 2, 20, Rgb(0x2A180C));
    }

    public override void Render(int mx, int my, float pt)
    {
        base.Render(mx, my, pt);
        if (P.Cursor != null) return;
        var offers = _ec.Offers;
        for (int i = 0; i < 3; i++)
        {
            var (bx, by) = ButtonPos(i);
            if (!In(mx, my, bx, by, 108, 19) || offers[i].RequiredLevel <= 0 || _ec.Inv.Get(0) == null) continue;
            string t = "";
            if (offers[i].Clue is { } clue) t = "§f§o" + EnchantHelper.Name(clue.Id) + " " + EnchantHelper.Roman(clue.Level) + " . . . ?";
            if (!P.Creative)
            {
                t += "\n";
                if (P.XpLevel < offers[i].RequiredLevel) t += "\n§cWymagany poziom: " + offers[i].RequiredLevel;
                else
                {
                    int n = i + 1;
                    t += "\n" + (_ec.Lapis >= n ? "§7" : "§c") + n + (n == 1 ? " lazuryt" : " lazuryty");
                    t += "\n§7" + n + (n == 1 ? " poziom doświadczenia" : " poziomy doświadczenia");
                }
            }
            GuiDraw.Tooltip(G, t.TrimStart('\n'), mx, my);
        }
    }

    public override void MouseDown(int mx, int my, int btn)
    {
        if (btn == 0)
            for (int i = 0; i < 3; i++)
            {
                var (bx, by) = ButtonPos(i);
                if (!In(mx, my, bx, by, 108, 19)) continue;
                if (_ec.ButtonState(i) == 2)
                {
                    if (P.World.IsRemote) G.Client?.EnchantItem(i);
                    else _ec.Enchant(P, i);
                }
                return;
            }
        base.MouseDown(mx, my, btn);
    }

    public override void Tick() { if (!_ec.StillValid(P)) G.SetScreen(null); }
}
