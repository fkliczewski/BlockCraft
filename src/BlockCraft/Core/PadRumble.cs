using System.Runtime.InteropServices;

namespace BlockCraft.Core;

/// <summary>Wibracje pada (osobno od odczytu stanu: inna platforma podstawia własną implementację).</summary>
public interface IPadRumble
{
    /// <summary>Wibracja o sile 0..1 przez ms milisekund.</summary>
    void Rumble(float strength, int ms);
    /// <summary>Wyłącza wibrację po czasie (wołane co klatkę).</summary>
    void Update();
}

/// <summary>Wibracje przez XInput (Xbox 360/One, 8BitDo w trybie X-input; GLFW ich nie obsługuje), na wszystkich padach
/// XInput — w grze jest jeden gracz. Brak biblioteki wyłącza wibracje bez błędów.</summary>
public sealed class XInputRumble : IPadRumble
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XInputVibration { public ushort Left, Right; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern int XInputSetState(int user, ref XInputVibration v);
    private static bool _xinputMissing;
    private long _rumbleUntil;

    public void Rumble(float strength, int ms)
    {
        _rumbleUntil = Environment.TickCount64 + ms;
        SetVibration(strength);
    }

    public void Update()
    {
        if (_rumbleUntil != 0 && Environment.TickCount64 >= _rumbleUntil) { _rumbleUntil = 0; SetVibration(0); }
    }

    private static void SetVibration(float strength)
    {
        if (_xinputMissing || !OperatingSystem.IsWindows()) return;
        float s = Math.Clamp(strength, 0, 1);
        var v = new XInputVibration { Left = (ushort)(s * 65535), Right = (ushort)(s * 40000) };
        try { for (int i = 0; i < 4; i++) XInputSetState(i, ref v); }
        catch (Exception) { _xinputMissing = true; }
    }
}

/// <summary>Kiedy wibrować: przy obrażeniach i zniszczeniu bloku (liczniki z kolejnych ticków).</summary>
public sealed class PadRumbleTriggers
{
    private readonly IPadRumble _rumble;
    private int _prevHurt, _prevBroken;
    private object _prevBreaker;

    public PadRumbleTriggers(IPadRumble rumble) { _rumble = rumble; }

    /// <summary>Po ticku świata. breaker = właściciel licznika bloków (nowy kontroler — wymiar, świat — zaczyna od zera,
    /// więc zmiana licznika przy zmianie właściciela to nie zniszczony blok).</summary>
    public void Tick(bool enabled, int hurtTime, object breaker, int blocksBroken)
    {
        if (enabled && hurtTime > _prevHurt) _rumble.Rumble(0.7f, 250);
        if (enabled && breaker != null && breaker == _prevBreaker && blocksBroken != _prevBroken) _rumble.Rumble(0.25f, 70);
        _prevHurt = hurtTime; _prevBroken = blocksBroken; _prevBreaker = breaker;
    }
}
