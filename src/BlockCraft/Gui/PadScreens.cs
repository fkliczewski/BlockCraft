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

/// <summary>Ekran z przewijanymi listami (światy, języki, paczki): krzyżak chodzi po wierszach i przewija listę.</summary>
public interface IPadLists
{
    IEnumerable<PadList> PadLists();
}

public static class PadListsExtensions
{
    /// <summary>Cele krzyżaka na listach ekranu (w pełni widoczne wiersze).</summary>
    public static IEnumerable<(int x, int y)> ListTargets(this IPadLists screen) => screen.PadLists().SelectMany(l => l.Targets());
}

/// <summary>Pionowa lista do nawigacji padem: wiersz i-ty zaczyna się w Top + Offset + i * RowH - przewinięcie
/// i ma wysokość ItemH; w pełni widoczne wiersze są celami krzyżaka, a przeskok poza widoczne przewija listę.</summary>
public sealed class PadList
{
    public int Left, Width, Top, Bottom, Offset, RowH, ItemH, Count;
    /// <summary>Gdzie w wierszu staje kursor (-1 = środek listy).</summary>
    public int TargetX = -1;
    public float MaxScroll;
    public Func<float> GetScroll;
    public Action<float> SetScroll;

    private int RowY(int i) => Top + Offset + i * RowH - (int)GetScroll();
    private bool Visible(int i) => RowY(i) >= Top && RowY(i) + ItemH <= Bottom;

    private int X => TargetX >= 0 ? TargetX : Left + Width / 2;
    public bool Contains(int mx, int my) => my >= Top && my < Bottom && mx >= Left && mx < Left + Width;

    /// <summary>Środki w pełni widocznych wierszy.</summary>
    public IEnumerable<(int x, int y)> Targets()
    {
        for (int i = 0; i < Count; i++) if (Visible(i)) yield return (X, RowY(i) + ItemH / 2);
    }

    /// <summary>Krok o dy wierszy od wiersza pod kursorem: przewija listę, żeby nowy wiersz był widoczny, i zwraca jego środek.
    /// Null = kursor poza listą albo koniec listy (krzyżak przechodzi wtedy do przycisków).</summary>
    public (int x, int y)? Move(int mx, int my, int dy)
    {
        if (dy == 0 || Count == 0 || !Contains(mx, my)) return null;
        int i = (int)Math.Floor((my - Top - Offset + GetScroll()) / (float)RowH);
        int next = Math.Clamp(i, 0, Count - 1) + dy;
        if (i < 0 || i >= Count || next < 0 || next >= Count) return null;
        float scroll = GetScroll();
        int y = Top + Offset + next * RowH;
        if (y - scroll < Top) scroll = y - Top;
        else if (y + ItemH - scroll > Bottom) scroll = y + ItemH - Bottom;
        SetScroll(Math.Clamp(scroll, 0, Math.Max(0, MaxScroll)));
        return (X, RowY(next) + ItemH / 2);
    }
}
