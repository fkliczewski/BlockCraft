using BlockCraft.Core;

namespace BlockCraft.Gui;

/// <summary>Rysowanie trybu pada na ekranach: klawiatura ekranowa, kursor i pasek podpowiedzi przycisków.</summary>
public sealed class PadMenuOverlay
{
    private const int FieldH = 20;
    private static readonly (PadButton, string)[] BasicHints = { (PadButton.A, "Wybierz"), (PadButton.B, "Wstecz") };
    private static readonly (PadButton, string)[] KeyboardHints =
    {
        (PadButton.A, "Wpisz"), (PadButton.B, "Usuń"), (PadButton.X, "Spacja"), (PadButton.Y, "Ukryj"),
        (PadButton.Start, "Enter"), (PadButton.LeftTrigger, "Shift"), (PadButton.RightTrigger, "Symbole"),
    };
    private readonly PadMenuController _menu;

    public PadMenuOverlay(PadMenuController menu) { _menu = menu; }

    public void Draw(Game g, Screen s)
    {
        if (_menu.KeyboardOpen) { DrawKeyboard(g, s); return; }
        if (s is IPadCapture { CapturesPad: true }) return;
        PadIcons.Hints(g, 4, MenuHintY(g, s), Hints(s).ToArray());
        PadIcons.Cursor(g, g.MouseGuiX, g.MouseGuiY);
    }

    private static IEnumerable<(PadButton button, string label)> Hints(Screen s) =>
        s is IPadHints own ? own.PadHints() : s.WantsText ? BasicHints.Append((PadButton.Back, "Klawiatura")) : BasicHints;

    private void DrawKeyboard(Game g, Screen s)
    {
        var osk = _menu.Keyboard;
        bool top = KeyboardOnTop(g, osk, s.TextY);
        osk.Draw(g, g.GuiW, g.GuiH, top);
        PadIcons.Hints(g, 4, KeyboardHintY(g, osk, top, s.TextY), KeyboardHints);
    }

    /// <summary>Klawiatura u góry, jeśli pole mieści się pod nią, a nie mieści się nad klawiaturą na dole (wtedy strona przeciwna polu).</summary>
    private static bool KeyboardOnTop(Game g, OnScreenKeyboard osk, int fieldY)
    {
        bool fitsBelowKeyboard = fieldY >= osk.Height + 4 && fieldY + FieldH <= g.GuiH - 11;
        bool fitsAboveKeyboard = fieldY + FieldH <= g.GuiH - osk.Height - 15;
        if (fitsBelowKeyboard != fitsAboveKeyboard) return fitsBelowKeyboard;
        return fieldY > g.GuiH / 2;
    }

    /// <summary>Pasek podpowiedzi przy klawiaturze: nad nią, gdy jest na dole; przy klawiaturze u góry na dole ekranu,
    /// chyba że tam jest pole tekstowe (czat) — wtedy tuż pod klawiaturą.</summary>
    private static int KeyboardHintY(Game g, OnScreenKeyboard osk, bool top, int fieldY)
    {
        if (!top) return g.GuiH - osk.Height - 15;
        return FieldUnderBottomBar(g, fieldY) ? osk.Height + 6 : g.GuiH - 11;
    }

    private static bool FieldUnderBottomBar(Game g, int fieldY) => fieldY + FieldH > g.GuiH - 13;

    /// <summary>Pasek podpowiedzi bez klawiatury: na dole, a gdy tam jest pole tekstowe (czat) — u góry ekranu.</summary>
    private static int MenuHintY(Game g, Screen s) => s.WantsText && FieldUnderBottomBar(g, s.TextY) ? 2 : g.GuiH - 11;
}
