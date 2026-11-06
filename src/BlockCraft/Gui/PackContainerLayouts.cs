using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.Gui;

/// <summary>Układ tła okna z paczki zasobów (gui/container/NAZWA.png, współrzędne jak w 1.8) dla typu kontenera.</summary>
public abstract class PackContainerLayout
{
    /// <summary>Nazwa pliku w gui/container/ bez rozszerzenia.</summary>
    public abstract string Texture { get; }

    public virtual void DrawFrame(SpriteBatch sb, Texture2D bg, Container c, int left, int top) =>
        sb.DrawPx(bg, left, top, 0, 0, c.Width, c.Height);

    /// <summary>Elementy zależne od stanu kontenera (np. postęp pieca); domyślnie brak.</summary>
    public virtual void DrawProgress(SpriteBatch sb, Texture2D bg, Container c, int left, int top) { }

    /// <summary>Wysokość napisu "Ekwipunek" nad ekwipunkiem gracza na tym tle.</summary>
    public virtual int InventoryLabelY(Container c) => DefaultInventoryLabelY(c);

    public static int DefaultInventoryLabelY(Container c) => c.Height - 96 + 2;

    /// <summary>Wszystkie układy okien jak w 1.8, według nazwy pliku (Container.PackWindow).</summary>
    public static readonly IReadOnlyDictionary<string, PackContainerLayout> ByTexture = new PackContainerLayout[]
    {
        new InventoryPackLayout(), new PlainPackLayout("crafting_table"), new FurnacePackLayout(), new ChestPackLayout(),
    }.ToDictionary(l => l.Texture);

    /// <summary>Układ okna kontenera; null = tło rysowane przez grę.</summary>
    public static PackContainerLayout For(Container c) =>
        c.PackWindow == null ? null : ByTexture.GetValueOrDefault(c.PackWindow);
}

/// <summary>Tło o tym samym układzie co okno gry, rysowane w całości.</summary>
public sealed class PlainPackLayout : PackContainerLayout
{
    public PlainPackLayout(string texture) { Texture = texture; }
    public override string Texture { get; }
}

/// <summary>inventory.png z 1.8 ma wytwarzanie od (88,26) i wynik w (144,36); u nas sloty są o (10,-8) dalej,
/// więc ten fragment tła jest przesuwany, a odsłonięte miejsce zakrywane gładkim pasem tła spod wytwarzania.</summary>
public sealed class InventoryPackLayout : PackContainerLayout
{
    private const int CraftX = 86, CraftY = 24, CraftW = 76, CraftH = 38, PlainY = 62, PlainH = 19, ShiftX = 10, ShiftY = -8;
    public override string Texture => "inventory";

    public override void DrawFrame(SpriteBatch sb, Texture2D bg, Container c, int left, int top)
    {
        base.DrawFrame(sb, bg, c, left, top);
        for (int y = CraftY; y < CraftY + CraftH; y += PlainH)
            sb.DrawPx(bg, left + CraftX, top + y, CraftX, PlainY, CraftW, Math.Min(PlainH, CraftY + CraftH - y));
        sb.DrawPx(bg, left + CraftX + ShiftX, top + CraftY + ShiftY, CraftX, CraftY, CraftW, CraftH);
    }
}

/// <summary>generic_54.png z 1.8: nagłówek i rzędy skrzyni od góry, ekwipunek gracza (96 px) od y=126.</summary>
public sealed class ChestPackLayout : PackContainerLayout
{
    private const int HeaderH = 17, RowsBaseH = 114, PlayerY = 126, PlayerH = 96;
    public override string Texture => "generic_54";
    public override int InventoryLabelY(Container c) => c.Height - 96;

    public override void DrawFrame(SpriteBatch sb, Texture2D bg, Container c, int left, int top)
    {
        int rowsBottom = c.Height - RowsBaseH + HeaderH;
        sb.DrawPx(bg, left, top, 0, 0, c.Width, rowsBottom);
        sb.DrawPx(bg, left, top + rowsBottom, 0, PlayerY, c.Width, PlayerH);
    }
}

/// <summary>furnace.png z 1.8 jak GuiFurnace: płomień 14x13 w (176,0) maleje od góry wraz z paliwem,
/// strzałka 24x16 w (176,14) rośnie w prawo wraz z przepalaniem.</summary>
public sealed class FurnacePackLayout : PackContainerLayout
{
    private const int SpriteX = 176, FlameW = 14, FlameH = 13, FlameX = 56, FlameY = 36, ArrowY = 14, ArrowW = 24, ArrowH = 16, ArrowX = 79, ArrowTop = 34;
    public override string Texture => "furnace";

    public override void DrawProgress(SpriteBatch sb, Texture2D bg, Container c, int left, int top)
    {
        if (c is not IFurnaceProgressSource { Progress: var f }) return;
        if (f.IsBurning)
        {
            int flame = (int)(f.BurnFraction * FlameH);
            sb.DrawPx(bg, left + FlameX, top + FlameY + FlameH - 1 - flame, SpriteX, FlameH - 1 - flame, FlameW, flame + 1);
        }
        int arrow = (int)(f.CookFraction * ArrowW);
        if (arrow > 0) sb.DrawPx(bg, left + ArrowX, top + ArrowTop, SpriteX, ArrowY, arrow + 1, ArrowH);
    }
}
