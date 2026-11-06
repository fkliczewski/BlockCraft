using BlockCraft.Core;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

/// <summary>Co sterowanie padem w menu potrzebuje od gry. Zdarzenia idą tą samą drogą co mysz i klawiatura
/// (odkładane w trakcie ticku), a kursor pada jest kursorem GUI.</summary>
public interface IPadMenuHost
{
    int GuiW { get; }
    int GuiH { get; }
    int CursorX { get; }
    int CursorY { get; }
    /// <summary>Czas w sekundach (powtarzanie krzyżaka).</summary>
    double Seconds { get; }
    /// <summary>Przestawia kursor GUI i wysyła ruch myszy do ekranu.</summary>
    void MoveCursor(int x, int y);
    void MouseButton(int x, int y, int button, bool down);
    void MouseWheel(int x, int y, float step);
    void ShiftClick(int x, int y);
    void ScreenKey(Keys key, bool ctrl);
    void ScreenChar(char c);
    void Defer(Action action);
    /// <summary>Czy Start może wrócić z ekranu do gry (jest świat, a ekran nie jest śmiercią ani ładowaniem).</summary>
    bool CanReturnToGame(Screen s);
    /// <summary>Escape kolejnych ekranów aż do gry.</summary>
    void EscapeToGame();
    void PlayUiClick();
}

/// <summary>Pad w menu i ekranach: lewa gałka prowadzi kursor, krzyżak przeskakuje do przycisków i slotów,
/// A = klik (trzymanie przeciąga), X = prawy klik, Y = szybkie przeniesienie, B = wstecz; przy polach tekstowych
/// klawiatura ekranowa.</summary>
public sealed class PadMenuController
{
    private readonly IPadMenuHost _host;
    private float _cursorX, _cursorY;
    private bool _clickHeld, _rightHeld;
    private float _scroll;
    private readonly Dictionary<int, double> _repeat = new();
    private bool _keyboardOpen;
    /// <summary>Ekran, na którym schowano klawiaturę (Y); Back albo A na polu tekstowym pokazuje ją znowu.</summary>
    private Screen _keyboardDismissedFor;

    public PadMenuController(IPadMenuHost host) { _host = host; }

    public OnScreenKeyboard Keyboard { get; } = new();
    public bool KeyboardOpen => _keyboardOpen;

    /// <summary>Raz na klatkę przy otwartym ekranie.</summary>
    public void Update(Gamepad pad, Screen s, double dt)
    {
        if (s is IPadCapture { CapturesPad: true } capture) { Capture(pad, capture); return; }
        UpdateKeyboardVisibility(s);
        if (_keyboardOpen) { KeyboardInput(pad, s); return; }
        MenuInput(pad, s, dt);
    }

    /// <summary>Gra bez ekranu: schowana klawiatura pokaże się znowu przy następnym polu tekstowym.</summary>
    public void InGame() => _keyboardDismissedFor = null;

    /// <summary>Tryb pada włączony: kursor pada startuje z pozycji kursora GUI.</summary>
    public void Activate() { _cursorX = _host.CursorX; _cursorY = _host.CursorY; }

    /// <summary>Tryb pada wyłączony: klawiatura znika, trzymane kliknięcia są puszczane.</summary>
    public void Deactivate()
    {
        _keyboardOpen = false;
        int mx = _host.CursorX, my = _host.CursorY;
        if (_clickHeld) _host.MouseButton(mx, my, 0, false);
        if (_rightHeld) _host.MouseButton(mx, my, 1, false);
        _clickHeld = _rightHeld = false;
    }

    /// <summary>Zmiana ekranu: kliknięcia menu nie przechodzą dalej; z pada kursor staje na pierwszym przycisku
    /// albo wybranym slocie. Zwraca nową pozycję kursora (null = bez zmiany).</summary>
    public (int x, int y)? OnScreenChange(Screen s, bool padActive)
    {
        _clickHeld = _rightHeld = false;
        if (!padActive || s?.PadStart() is not { } t) return null;
        _cursorX = t.x; _cursorY = t.y;
        return t;
    }

    private void Capture(Gamepad pad, IPadCapture capture)
    {
        _clickHeld = _rightHeld = false;
        int b = pad.AnyPressed();
        if (b >= 0) _host.Defer(() => capture.PadButtonPressed((PadButton)b));
    }

    private void UpdateKeyboardVisibility(Screen s)
    {
        if (!s.WantsText) _keyboardOpen = false;
        else if (!_keyboardOpen && _keyboardDismissedFor != s) { _keyboardOpen = true; Keyboard.Reset(); }
    }

    // ===== menu =====
    private void MenuInput(Gamepad pad, Screen s, double dt)
    {
        MoveCursorWithStick(pad, s, dt);
        JumpOrNudge(pad, s);
        int mx = _host.CursorX, my = _host.CursorY;
        Clicks(pad, s, mx, my);
        if (pad.Pressed(PadButton.Y)) QuickMove(s, mx, my);
        MenuButtons(pad, s);
        Scroll(pad, dt, mx, my);
    }

    /// <summary>Lewa gałka: kursor, wolniejszy nad przyciskiem/slotem, żeby łatwo się na nim zatrzymać.</summary>
    private void MoveCursorWithStick(Gamepad pad, Screen s, double dt)
    {
        var (lx, ly) = pad.LeftStick;
        if (lx == 0 && ly == 0) return;
        float mag = MathF.Sqrt(lx * lx + ly * ly);
        float speed = 70f + 330f * mag * mag;
        if (s.WidgetAt(_host.CursorX, _host.CursorY) != null || NearTarget(s, 6)) speed *= 0.55f;
        MoveCursor(_cursorX + lx * speed * (float)dt, _cursorY + ly * speed * (float)dt);
    }

    /// <summary>Krzyżak: suwak w lewo/prawo albo przeskok do najbliższego celu.</summary>
    private void JumpOrNudge(Gamepad pad, Screen s)
    {
        var (dx, dy) = Direction(pad, false);
        if (dx == 0 && dy == 0) return;
        if (dx == 0 && s is IPadLists lists)
            foreach (var list in lists.PadLists())
                if (list.Move(_host.CursorX, _host.CursorY, dy) is { } row) { MoveCursor(row.x, row.y); return; }
        if (dx != 0 && dy == 0 && s.WidgetAt(_host.CursorX, _host.CursorY) is Slider slider)
        {
            int dir = dx;
            _host.Defer(() => { slider.Nudge(dir); _host.PlayUiClick(); });
        }
        else if (PadNavigation.Next(s.PadTargets(), _host.CursorX, _host.CursorY, dx, dy) is { } t) MoveCursor(t.x, t.y);
    }

    /// <summary>A = lewy klik (trzymanie przeciąga), X = prawy klik.</summary>
    private void Clicks(Gamepad pad, Screen s, int mx, int my)
    {
        if (pad.Pressed(PadButton.A))
        {
            var w = s.WidgetAt(mx, my);
            if (w is TextField) _keyboardDismissedFor = null;
            if (w is not Slider) { _clickHeld = true; _host.MouseButton(mx, my, 0, true); }
        }
        else if (_clickHeld && !pad.Down(PadButton.A)) { _clickHeld = false; _host.MouseButton(mx, my, 0, false); }
        if (pad.Pressed(PadButton.X)) { _rightHeld = true; _host.MouseButton(mx, my, 1, true); }
        else if (_rightHeld && !pad.Down(PadButton.X)) { _rightHeld = false; _host.MouseButton(mx, my, 1, false); }
    }

    /// <summary>Y: szybkie przeniesienie ekranu albo Shift + klik.</summary>
    private void QuickMove(Screen s, int mx, int my)
    {
        if (s is IPadQuickMove quick) _host.Defer(() => quick.PadQuickMove(mx, my));
        else _host.ShiftClick(mx, my);
    }

    /// <summary>B wstecz, Start powrót do gry, Back klawiatura, LB/RB zakładki.</summary>
    private void MenuButtons(Gamepad pad, Screen s)
    {
        if (pad.Pressed(PadButton.B)) _host.ScreenKey(Keys.Escape, false);
        if (pad.Pressed(PadButton.Start) && _host.CanReturnToGame(s)) _host.Defer(_host.EscapeToGame);
        if (pad.Pressed(PadButton.Back) && s.WantsText) _keyboardDismissedFor = null;
        if (s is IPadTabs tabs)
        {
            if (pad.Pressed(PadButton.LeftBumper)) _host.Defer(() => tabs.PadTab(-1));
            if (pad.Pressed(PadButton.RightBumper)) _host.Defer(() => tabs.PadTab(1));
        }
    }

    /// <summary>Prawa gałka przewija listy (kółko myszy).</summary>
    private void Scroll(Gamepad pad, double dt, int mx, int my)
    {
        var (_, ry) = pad.RightStick;
        if (ry == 0) { _scroll = 0; return; }
        _scroll += -ry * (float)dt * 12f;
        while (Math.Abs(_scroll) >= 1)
        {
            float step = Math.Sign(_scroll);
            _scroll -= step;
            _host.MouseWheel(mx, my, step);
        }
    }

    private bool NearTarget(Screen s, int r)
    {
        int cx = _host.CursorX, cy = _host.CursorY;
        foreach (var (x, y) in s.PadTargets()) if (Math.Abs(x - cx) <= r && Math.Abs(y - cy) <= r) return true;
        return false;
    }

    private void MoveCursor(float x, float y)
    {
        _cursorX = Math.Clamp(x, 0, _host.GuiW - 1); _cursorY = Math.Clamp(y, 0, _host.GuiH - 1);
        int nx = (int)_cursorX, ny = (int)_cursorY;
        if (nx != _host.CursorX || ny != _host.CursorY) _host.MoveCursor(nx, ny);
    }

    /// <summary>Wciśnięcie albo powtórzenie przy trzymaniu (krzyżak, gałka w klawiaturze).</summary>
    private bool Repeat(int key, bool down)
    {
        double now = _host.Seconds;
        if (!down) { _repeat.Remove(key); return false; }
        if (!_repeat.TryGetValue(key, out double next)) { _repeat[key] = now + 0.35; return true; }
        if (now < next) return false;
        _repeat[key] = next + 0.09;
        return true;
    }

    /// <summary>Kierunek z krzyżaka i lewej gałki (wychylenie ponad połowę), z powtarzaniem.</summary>
    private (int dx, int dy) Direction(Gamepad pad, bool withStick)
    {
        var (lx, ly) = withStick ? pad.LeftStick : (0f, 0f);
        int dx = 0, dy = 0;
        if (Repeat(0, pad.Down(PadButton.DPadLeft) || lx < -0.5f)) dx = -1;
        if (Repeat(1, pad.Down(PadButton.DPadRight) || lx > 0.5f)) dx = 1;
        if (Repeat(2, pad.Down(PadButton.DPadUp) || ly < -0.5f)) dy = -1;
        if (Repeat(3, pad.Down(PadButton.DPadDown) || ly > 0.5f)) dy = 1;
        return (dx, dy);
    }

    // ===== klawiatura ekranowa =====
    private void KeyboardInput(Gamepad pad, Screen s)
    {
        var (dx, dy) = Direction(pad, true);
        if (dx != 0 || dy != 0) Keyboard.Move(dx, dy);
        if (pad.Pressed(PadButton.A)) KeyboardOutput(s, Keyboard.Press());
        if (pad.Pressed(PadButton.B)) KeyboardOutput(s, new OnScreenKeyboard.Output(OnScreenKeyboard.OutKind.Backspace));
        if (pad.Pressed(PadButton.X)) KeyboardOutput(s, new OnScreenKeyboard.Output(OnScreenKeyboard.OutKind.Char, ' '));
        if (pad.Pressed(PadButton.Y)) KeyboardOutput(s, new OnScreenKeyboard.Output(OnScreenKeyboard.OutKind.Close));
        if (pad.Pressed(PadButton.Start)) KeyboardOutput(s, new OnScreenKeyboard.Output(OnScreenKeyboard.OutKind.Enter));
        if (pad.Pressed(PadButton.LeftTrigger) || pad.Pressed(PadButton.LeftStick)) Keyboard.ToggleShift();
        if (pad.Pressed(PadButton.RightTrigger)) Keyboard.ToggleSymbols();
    }

    private void KeyboardOutput(Screen s, OnScreenKeyboard.Output? o)
    {
        if (o is not { } v) return;
        switch (v.Kind)
        {
            case OnScreenKeyboard.OutKind.Char: _host.ScreenChar(v.Char); break;
            case OnScreenKeyboard.OutKind.Backspace: _host.ScreenKey(Keys.Backspace, false); break;
            case OnScreenKeyboard.OutKind.Enter: _host.ScreenKey(Keys.Enter, false); break;
            case OnScreenKeyboard.OutKind.Paste: _host.ScreenKey(Keys.V, true); break;
            case OnScreenKeyboard.OutKind.Close: _keyboardOpen = false; _keyboardDismissedFor = s; break;
        }
    }
}
