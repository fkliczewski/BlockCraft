using BlockCraft.Core;

namespace BlockCraft.Gui;

/// <summary>Ekran, który sam odbiera przyciski pada (np. przypisywanie przycisków).</summary>
public interface IPadCapture
{
    bool CapturesPad { get; }
    void PadButtonPressed(PadButton b);
}

/// <summary>Ekran z zakładkami przełączanymi LB/RB (np. ekwipunek kreatywny).</summary>
public interface IPadTabs
{
    void PadTab(int dir);
}

/// <summary>Ekran z własnym szybkim przeniesieniem (Y); bez tego pad wysyła Shift+klik.</summary>
public interface IPadQuickMove
{
    void PadQuickMove(int mx, int my);
}

/// <summary>Ekran z własnymi podpowiedziami przycisków pada na dole ekranu.</summary>
public interface IPadHints
{
    IEnumerable<(PadButton button, string label)> PadHints();
}
