using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Core;

/// <summary>Przyciski pada w kolejności GLFW (układ Xbox; 8BitDo w trybie X-input i pady PlayStation mapuje GLFW/SDL),
/// plus spusty jako przyciski (wciśnięte powyżej połowy).</summary>
public enum PadButton
{
    A, B, X, Y, LeftBumper, RightBumper, Back, Start, Guide, LeftStick, RightStick, DPadUp, DPadRight, DPadDown, DPadLeft,
    LeftTrigger, RightTrigger,
}

/// <summary>Układ oznaczeń przycisków na ekranie: Xbox (A/B/X/Y, LB/RT...) albo PlayStation (krzyżyk/kółko/kwadrat/trójkąt, L1/R2...).</summary>
public enum PadLayout { Xbox, PlayStation }

/// <summary>Ustawienie ikon przycisków: wg pada (wykryty układ), zawsze Xbox albo zawsze PlayStation.</summary>
public enum PadIconStyle { Auto, Xbox, PlayStation }

/// <summary>Stan pada w jednej klatce: przyciski i osie (gałki -1..1, Y w dół dodatni jak w GLFW; spusty 0..1).</summary>
public sealed class PadState
{
    public const int ButtonCount = 17;
    public readonly bool[] Buttons = new bool[ButtonCount];
    public float LX, LY, RX, RY, LT, RT;

    public bool this[PadButton b] { get => Buttons[(int)b]; set => Buttons[(int)b] = value; }

    public PadState Clone()
    {
        var s = new PadState { LX = LX, LY = LY, RX = RX, RY = RY, LT = LT, RT = RT };
        Buttons.CopyTo(s.Buttons, 0);
        return s;
    }
}

/// <summary>Pad (pierwszy podłączony gamepad GLFW): odczyt raz na klatkę, zbocza przycisków, martwa strefa gałek.
/// Inject podstawia stan do testów i podglądu.</summary>
public sealed class Gamepad
{
    public PadState Current { get; private set; } = new();
    public PadState Previous { get; private set; } = new();
    public bool Connected { get; private set; }
    public string Name { get; private set; } = "";
    /// <summary>Martwa strefa gałek (0..0,5), promieniowa.</summary>
    public float Deadzone = 0.15f;
    private PadState _injected;
    private int _jid = -1;
    private bool _mappingsLoaded;

    private string _injectedName = "Pad testowy";

    /// <summary>Podstawia stan pada zamiast GLFW (null = z powrotem prawdziwy pad); name = nazwa pada (np. do ikon PlayStation).</summary>
    public void Inject(PadState s, string name = null)
    {
        _injected = s?.Clone();
        if (name != null) _injectedName = name;
    }

    /// <summary>Układ przycisków wg nazwy pada: Sony (DualShock, DualSense) = PlayStation, reszta = Xbox.</summary>
    public PadLayout Layout => DetectLayout(Name);

    public static PadLayout DetectLayout(string name)
    {
        if (string.IsNullOrEmpty(name)) return PadLayout.Xbox;
        foreach (var key in new[] { "PS3", "PS4", "PS5", "DualShock", "DualSense", "PlayStation", "Sony" })
            if (name.Contains(key, StringComparison.OrdinalIgnoreCase)) return PadLayout.PlayStation;
        return PadLayout.Xbox;
    }
    public bool Injected => _injected != null;

    /// <summary>Odczyt stanu (wątek główny, raz na klatkę).</summary>
    public void Poll()
    {
        Previous = Current;
        if (_injected != null) { Current = _injected.Clone(); Connected = true; Name = _injectedName; return; }
        Current = ReadGlfw();
    }

    /// <summary>Błąd odczytu zgłoszony do logu; kolejne takie same błędy nie zaśmiecają go w każdej klatce.</summary>
    private bool _readErrorLogged;

    private unsafe PadState ReadGlfw()
    {
        var s = new PadState();
        try
        {
            if (!_mappingsLoaded) { _mappingsLoaded = true; LoadExtraMappings(); }
            if (_jid < 0 || !GLFW.JoystickIsGamepad(_jid))
            {
                _jid = -1;
                for (int j = 0; j <= 15; j++) if (GLFW.JoystickIsGamepad(j)) { _jid = j; break; }
                if (_jid < 0) { if (Connected) Log.Info("Pad: odłączony"); Connected = false; Name = ""; return s; }
                Name = GLFW.GetGamepadName(_jid) ?? GLFW.GetJoystickName(_jid) ?? "Pad";
                Log.Info("Pad: " + Name);
            }
            Connected = true;
            if (!GLFW.GetGamepadState(_jid, out var st)) return s;
            for (int i = 0; i < 15; i++) s.Buttons[i] = st.Buttons[i] != 0;
            s.LX = st.Axes[0]; s.LY = st.Axes[1]; s.RX = st.Axes[2]; s.RY = st.Axes[3];
            s.LT = (st.Axes[4] + 1) / 2; s.RT = (st.Axes[5] + 1) / 2;
            s[PadButton.LeftTrigger] = s.LT > 0.5f; s[PadButton.RightTrigger] = s.RT > 0.5f;
        }
        catch (Exception ex)
        {
            if (!_readErrorLogged) Log.Error("Pad: " + ex.Message);
            _readErrorLogged = true; Connected = false; // _jid zostaje: ponowne „wykrycie” logowałoby nazwę co klatkę
            return s;
        }
        _readErrorLogged = false;
        return s;
    }

    /// <summary>gamecontrollerdb.txt obok gry (mapowania SDL dla padów, których GLFW nie zna).</summary>
    private static void LoadExtraMappings()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "gamecontrollerdb.txt");
        if (!File.Exists(path)) return;
        try { if (GLFW.UpdateGamepadMappings(File.ReadAllText(path))) Log.Info("Pad: wczytano gamecontrollerdb.txt"); }
        catch (Exception ex) { Log.Error("Pad: gamecontrollerdb.txt: " + ex.Message); }
    }

    public bool Down(PadButton b) => Current[b];
    public bool Pressed(PadButton b) => Current[b] && !Previous[b];
    public bool Released(PadButton b) => !Current[b] && Previous[b];

    /// <summary>Pierwszy przycisk wciśnięty w tej klatce (-1 = żaden).</summary>
    public int AnyPressed()
    {
        for (int i = 0; i < PadState.ButtonCount; i++) if (Current.Buttons[i] && !Previous.Buttons[i]) return i;
        return -1;
    }

    public (float x, float y) LeftStick => ApplyDeadzone(Current.LX, Current.LY, Deadzone);
    public (float x, float y) RightStick => ApplyDeadzone(Current.RX, Current.RY, Deadzone);

    /// <summary>Czy pad został w tej klatce użyty: zmiana stanu (wciśnięcie przycisku, spust albo gałka wychodzące
    /// z spoczynku), a nie sam stan — dryfująca gałka ani trzymany przycisk nie odbierają sterowania myszy.</summary>
    public bool Active => Connected && (AnyPressed() >= 0 || Engaged(Current) && !Engaged(Previous));

    private bool Engaged(PadState s) =>
        s.LT > 0.3f || s.RT > 0.3f || StickEngaged(s.LX, s.LY) || StickEngaged(s.RX, s.RY);

    private bool StickEngaged(float x, float y) => ApplyDeadzone(x, y, Deadzone) != (0f, 0f);

    /// <summary>Promieniowa martwa strefa z przeskalowaniem: długość poniżej dz = 0, potem liniowo 0..1 (kierunek bez zmian).</summary>
    public static (float x, float y) ApplyDeadzone(float x, float y, float dz)
    {
        float len = MathF.Sqrt(x * x + y * y);
        if (len <= dz || len < 1e-6f) return (0, 0);
        float scaled = Math.Min(1f, (len - dz) / (1f - dz));
        return (x / len * scaled, y / len * scaled);
    }
}
