using BlockCraft.Core;
using BlockCraft.Gui;
using OpenTK.Windowing.Common;

namespace BlockCraft;

/// <summary>Wybór karty graficznej w laptopach z dwiema kartami (Core/GpuSwitch.cs).</summary>
public sealed partial class Game
{
    /// <summary>GL_RENDERER karty, na której działa gra.</summary>
    public string GlRenderer { get; private set; } = "";
    private List<GpuSwitch.Adapter> _gpuAdapters;
    public IReadOnlyList<GpuSwitch.Adapter> GpuAdapters => _gpuAdapters ??= GpuSwitch.Adapters();

    /// <summary>Karta, na którą można przełączyć grę z Ustawień: mocniejsza albo (z osobnej) zintegrowana; null = brak wyboru.</summary>
    public GpuSwitch.Adapter GpuPerformanceAlternative => GpuSwitch.BetterThan(GlRenderer, GpuAdapters);

    /// <summary>Oszczędniejsza karta dostępna niezależnie od propozycji mocniejszej karty.</summary>
    public GpuSwitch.Adapter GpuPowerSavingAlternative =>
        Render.GpuProfile.Detect(GlRenderer) == Render.GpuProfile.Kind.Dedicated
            ? GpuSwitch.IntegratedOther(GlRenderer, GpuAdapters) : null;

    /// <summary>VSync przez DWM zamiast sterownika (laptop hybrydowy, GpuSwitch.CrossAdapter); ustawiane w ApplySettings.</summary>
    private bool _dwmSync, _dwmSyncLogged;
    private List<string> _displayAdapters;
    private IntPtr _syncMonitor;

    /// <summary>VSync: zwykle przez sterownik (interwał 1). Gdy karta renderująca nie ma ekranu, sterownik OpenGL potrafi
    /// interwał zignorować (obraz się rwie), więc interwał 0 i czekanie na złożenie obrazu przez Windows po każdej klatce.</summary>
    private void ApplyVSync()
    {
        bool vs = Settings.VSync && _fpsOverride == 0;
        _syncMonitor = GpuSwitch.WindowMonitor(Location.X, Location.Y, Size.X, Size.Y);
        string displayAdapter = GpuSwitch.MonitorAdapter(_syncMonitor);
        _dwmSync = vs && displayAdapter != "" && !GpuSwitch.SameCard(displayAdapter, GlRenderer);
        VSync = vs && !_dwmSync ? VSyncMode.On : VSyncMode.Off;
        if (_dwmSync && !_dwmSyncLogged) { _dwmSyncLogged = true; Log.Info("VSync: karta bez ekranu (laptop hybrydowy), synchronizacja przez DWM"); }
    }

    /// <summary>Po SwapBuffers: czekanie na odświeżenie ekranu (tylko przy _dwmSync); błąd DWM = powrót do VSync sterownika.</summary>
    private void DwmSyncAfterSwap()
    {
        if (_syncMonitor != GpuSwitch.WindowMonitor(Location.X, Location.Y, Size.X, Size.Y)) ApplyVSync();
        if (!_dwmSync || GpuSwitch.DwmFlush()) return;
        _dwmSync = false; VSync = VSyncMode.On;
        Log.Info("VSync: DWM niedostępny, VSync sterownika");
    }

    /// <summary>Po starcie (bez trybów testowych): pytanie o mocniejszą kartę albo wyjaśnienie, że restart jej nie włączył.</summary>
    private void CheckGpuAtStart()
    {
        Log.Info("GPU: ekrany na: " + string.Join(", ", _displayAdapters ??= GpuSwitch.DisplayAdapters()));
        foreach (var a in GpuAdapters) Log.Info($"GPU: {a.Name} (0x{a.VendorId:X4}, {a.DedicatedMemory >> 20} MB{(a.Discrete ? ", osobna" : "")})");
        if (_args.Contains(GpuSwitch.RestartArg))
        {
            bool dedicated = Render.GpuProfile.Detect(GlRenderer) == Render.GpuProfile.Kind.Dedicated;
            bool switchFailed = Settings.GpuTarget.Length > 0
                ? !GpuSwitch.SameCard(Settings.GpuTarget, GlRenderer)
                : Settings.GpuChoice switch
            {
                "high" => !dedicated,
                "saving" => dedicated,
                _ => false,
            };
            if (switchFailed)
            {
                Log.Info("GPU: po restarcie nie osiągnięto wybranego trybu: " + GlRenderer);
                SetScreen(new GpuChoiceScreen(_screen, GlRenderer, null, failed: true));
            }
            return;
        }
        var better = GpuSwitch.BetterThan(GlRenderer, GpuAdapters);
        if (better == null) return;
        int pref = GpuSwitch.Preference();
        if (pref != GpuSwitch.PrefNone || Settings.GpuChoice == "keep") return; // wybór gracza albo ustawienie Windows
        SetScreen(new GpuChoiceScreen(_screen, GlRenderer, better));
    }

    /// <summary>Zapisuje preferencję Windows dla gry i uruchamia ją ponownie (bieżące okno się zamyka).</summary>
    public bool RestartForGpu { get; private set; }

    public bool SwitchGpu(int pref)
    {
        if (!GpuSwitch.SetPreference(pref)) return false;
        RestartForGpu = true;
        Close();
        return true;
    }
}
