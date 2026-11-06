using BlockCraft.Core;
using BlockCraft.Gui;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft;

/// <summary>Pad (zadanie B): w grze gałki = ruch i kamera, przyciski wg przypisań (PadBindings); na ekranach
/// PadMenuController (kursor, krzyżak, klawiatura ekranowa). Akcje idą tą samą drogą co mysz (RunOrDefer).</summary>
public sealed partial class Game : IPadMenuHost
{
    public readonly Gamepad Pad = new();
    private PadBindings _padBinds;
    public PadBindings PadBinds => _padBinds ??= PadBindings.FromSettings(Settings.PadBindings);
    /// <summary>Ostatnio używano pada (kursor GUI z gałki, podpowiedzi przycisków, klawiatura ekranowa).</summary>
    public bool PadActive { get; private set; }
    private PadMenuController _padMenuController;
    private PadMenuController PadMenu => _padMenuController ??= new PadMenuController(this);
    private PadMenuOverlay _padOverlay;
    private static readonly IPadRumble PadVibration = new XInputRumble();
    private readonly PadRumbleTriggers _padRumble = new(PadVibration);
    private bool _padAttackHeld, _padUseHeld, _padShift;
    /// <summary>Zdarzenia myszy tuż po zmianie trybu kursora (GLFW przestawia kursor) nie wyłączają trybu pada.</summary>
    private long _padIgnoreMouseUntil;
    /// <summary>Pozycja myszy zapamiętana w trybie pada; dopiero odejście od niej o kilka pikseli wyłącza tryb.</summary>
    private double _padAnchorX = double.NaN, _padAnchorY = double.NaN;
    private OnScreenKeyboard PadKeyboard => PadMenu.Keyboard;

    private void SetPadActive(bool on)
    {
        if (PadActive == on) return;
        PadActive = on;
        if (!on) { PadMenu.Deactivate(); ReleasePadButtons(); SetMouseGuiFromWindow(MousePosition.X, MousePosition.Y); }
        else { PadMenu.Activate(); _padAnchorX = _padAnchorY = double.NaN; }
        UpdateCursor();
    }

    /// <summary>Ruch myszy wyłącza tryb pada, chyba że to drgnięcie albo przestawienie kursora przez GLFW.</summary>
    private bool PadKeepsMouse(double rawX, double rawY)
    {
        if (!PadActive) return false;
        if (Environment.TickCount64 < _padIgnoreMouseUntil || double.IsNaN(_padAnchorX)) { _padAnchorX = rawX; _padAnchorY = rawY; return true; }
        double dx = rawX - _padAnchorX, dy = rawY - _padAnchorY;
        if (dx * dx + dy * dy < 16) return true;
        SetPadActive(false);
        return false;
    }

    private void ReleasePadButtons()
    {
        int mx = MouseGuiX, my = MouseGuiY;
        if (_padAttackHeld) RunOrDefer(() => MouseUpNow(mx, my, 0));
        if (_padUseHeld) RunOrDefer(() => MouseUpNow(mx, my, 1));
        _padAttackHeld = _padUseHeld = false;
    }

    /// <summary>Raz na klatkę, przed tickami (ControlTick czyta gałkę ruchu i przyciski trzymane).</summary>
    private void PadFrame(double dt)
    {
        Pad.Deadzone = Settings.PadDeadzone;
        Pad.Poll(); // także przy wyłączonym padzie: podgląd stanu w „Kontroler...”
        PadVibration.Update();
        _padHeldSinceScreenChange.RemoveWhere(b => !Pad.Down(b));
        if (!Settings.PadEnabled && !Pad.Injected) { SetPadActive(false); return; }
        if (!IsFocused && !Pad.Injected) return;
        if (Pad.Active) SetPadActive(true);
        if (!PadActive) return;
        if (_screen != null) { PadMenu.Update(Pad, _screen, dt); return; }
        PadMenu.InGame();
        if (World != null && Player != null && _sessionStarted) PadInGame(dt);
    }

    // ===== w grze =====
    private void PadInGame(double dt)
    {
        var p = Player; var b = PadBinds;
        var (rx, ry) = Pad.RightStick;
        if ((rx != 0 || ry != 0) && !p.Sleeping)
        {
            // stopnie na sekundę; kwadrat wychylenia daje precyzję przy małym ruchu gałki
            float speed = 60f + Settings.PadLookSensitivity * 340f;
            p.Yaw += rx * Math.Abs(rx) * speed * (float)dt;
            p.Pitch = Math.Clamp(p.Pitch + ry * Math.Abs(ry) * speed * 0.75f * (float)dt * (Settings.PadInvertY ? -1 : 1), -90f, 90f);
        }
        int mx = MouseGuiX, my = MouseGuiY;
        if (Pad.Pressed(b[PadAction.Attack])) { _padAttackHeld = true; RunOrDefer(() => MouseDownNow(mx, my, 0)); }
        else if (_padAttackHeld && !Pad.Down(b[PadAction.Attack])) { _padAttackHeld = false; RunOrDefer(() => MouseUpNow(mx, my, 0)); }
        if (Pad.Pressed(b[PadAction.Use])) { _padUseHeld = true; RunOrDefer(() => MouseDownNow(mx, my, 1)); }
        else if (_padUseHeld && !Pad.Down(b[PadAction.Use])) { _padUseHeld = false; RunOrDefer(() => MouseUpNow(mx, my, 1)); }
        if (Pad.Pressed(b[PadAction.PickBlock])) RunOrDefer(() => MouseDownNow(mx, my, 2));
        if (Pad.Pressed(b[PadAction.HotbarPrev])) RunOrDefer(() => MouseWheelNow(mx, my, 1));
        if (Pad.Pressed(b[PadAction.HotbarNext])) RunOrDefer(() => MouseWheelNow(mx, my, -1));
        if (Pad.Pressed(b[PadAction.Inventory])) PadGameKey(Keys.E);
        if (Pad.Pressed(b[PadAction.Drop])) PadGameKey(Keys.Q);
        if (Pad.Pressed(b[PadAction.Chat])) PadGameKey(Keys.T);
        if (Pad.Pressed(b[PadAction.Command])) PadGameKey(Keys.Slash);
        if (Pad.Pressed(b[PadAction.Pause])) PadGameKey(Keys.Escape);
        if (Pad.Pressed(b[PadAction.Camera])) PadGameKey(Keys.F5);
    }

    /// <summary>Akcja jak klawisz w grze; bez znaku po nim (klawiatura ekranowa nie ma czego pomijać).</summary>
    private void PadGameKey(Keys k) => RunOrDefer(() => { GameKeyNow(k, false, false); _ignoreNextChar = false; });

    /// <summary>Ruch i przyciski trzymane dla ControlTick (0 i false, gdy pad nie jest używany).</summary>
    private (float forward, float strafe, bool jump, bool sneak, bool sprint) PadMovement()
    {
        if (!PadActive || _screen != null) return (0, 0, false, false, false);
        var (lx, ly) = Pad.LeftStick; var b = PadBinds;
        return (-ly, -lx, PadHeld(b[PadAction.Jump]), PadHeld(b[PadAction.Sneak]), PadHeld(b[PadAction.Sprint]));
    }

    /// <summary>Przyciski trzymane w chwili zmiany ekranu: nie działają, dopóki ich nie puścić (A z „Wróć do gry” nie skacze).</summary>
    private readonly HashSet<PadButton> _padHeldSinceScreenChange = new();

    private bool PadHeld(PadButton b) => Pad.Down(b) && !_padHeldSinceScreenChange.Contains(b);

    /// <summary>Zmiana ekranu: kliknięcia menu nie przechodzą dalej, a trzymane przyciski wymagają ponownego wciśnięcia.</summary>
    private void PadOnScreenChange()
    {
        foreach (var b in Enum.GetValues<PadButton>()) if (Pad.Down(b)) _padHeldSinceScreenChange.Add(b);
    }

    /// <summary>Wibracje przy obrażeniach i zniszczeniu bloku (po ticku świata); podstawiony pad nie wibruje.</summary>
    private void PadRumbleTick()
    {
        if (Player == null) return;
        bool on = PadActive && Settings.PadVibration && Pad.Connected && !Pad.Injected;
        _padRumble.Tick(on, Player.HurtTime, Ctrl, Ctrl?.BlocksBroken ?? 0);
    }

    // ===== ekrany (IPadMenuHost) =====
    int IPadMenuHost.GuiW => GuiW;
    int IPadMenuHost.GuiH => GuiH;
    int IPadMenuHost.CursorX => _mouseGuiX;
    int IPadMenuHost.CursorY => _mouseGuiY;
    double IPadMenuHost.Seconds => _clock.Elapsed.TotalSeconds;

    void IPadMenuHost.MoveCursor(int x, int y)
    {
        _mouseGuiX = x; _mouseGuiY = y;
        RunOrDefer(() => _screen?.MouseMove(x, y));
    }

    void IPadMenuHost.MouseButton(int x, int y, int button, bool down)
    {
        if (down) RunOrDefer(() => MouseDownNow(x, y, button));
        else RunOrDefer(() => MouseUpNow(x, y, button));
    }

    void IPadMenuHost.MouseWheel(int x, int y, float step) => RunOrDefer(() => MouseWheelNow(x, y, step));

    /// <summary>Shift + klik (kontekst odłożonego zdarzenia zapamiętuje Shift).</summary>
    void IPadMenuHost.ShiftClick(int x, int y)
    {
        _padShift = true;
        try
        {
            RunOrDefer(() => MouseDownNow(x, y, 0));
            RunOrDefer(() => MouseUpNow(x, y, 0));
        }
        finally { _padShift = false; }
    }

    void IPadMenuHost.ScreenKey(Keys key, bool ctrl) => RunOrDefer(() => _screen?.KeyDown(key, ctrl, false));
    void IPadMenuHost.ScreenChar(char c) => RunOrDefer(() => _screen?.CharTyped(c));
    void IPadMenuHost.Defer(Action action) => RunOrDefer(action);
    bool IPadMenuHost.CanReturnToGame(Screen s) => World != null && _sessionStarted && s is not DeathScreen && s is not LoadingScreen;

    /// <summary>Start: Escape kolejnych ekranów aż do gry, żeby każdy zapisał i zastosował swoje zmiany (np. ustawienia).</summary>
    void IPadMenuHost.EscapeToGame()
    {
        for (int i = 0; i < 8 && _screen != null; i++)
        {
            var before = _screen;
            before.KeyDown(Keys.Escape, false, false);
            if (_screen == before) return;
        }
    }

    /// <summary>Po otwarciu ekranu z pada kursor staje na pierwszym przycisku albo wybranym slocie.</summary>
    private void PadPlaceCursor(Screen s)
    {
        _padIgnoreMouseUntil = Environment.TickCount64 + 300;
        PadOnScreenChange();
        if (PadMenu.OnScreenChange(s, PadActive) is { } t) { _mouseGuiX = t.x; _mouseGuiY = t.y; }
    }

    private void DrawPadOverlay()
    {
        if (!PadActive || _screen == null) return;
        (_padOverlay ??= new PadMenuOverlay(PadMenu)).Draw(this, _screen);
    }
}
