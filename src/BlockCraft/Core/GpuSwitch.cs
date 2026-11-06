using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BlockCraft.Core;

/// <summary>Wybór karty graficznej w laptopach z dwiema kartami (zintegrowana + RTX/Radeon RX). Windows daje grom OpenGL
/// domyślnie kartę oszczędną; gra C# nie może wyeksportować znacznika NvOptimusEnablement jak gry C++, więc zapisuje
/// dla swojego pliku .exe to samo ustawienie co Ustawienia Windows → System → Ekran → Grafika („Wysoka wydajność”)
/// i uruchamia się ponownie (karty nie da się zmienić w działającym procesie). Tylko Windows.</summary>
public static class GpuSwitch
{
    public sealed record Adapter(string Name, uint VendorId, ulong DedicatedMemory)
    {
        // Pami?? zarezerwowana przez firmware nie oznacza osobnej karty (np. Radeon 780M).
        public int PerformanceRank { get; init; } = int.MaxValue;
        public int PowerSavingRank { get; init; } = int.MaxValue;

        public bool Discrete => VendorId == 0x10DE ||
            (VendorId is 0x1002 or 0x8086 && Render.GpuProfile.Detect(Name) == Render.GpuProfile.Kind.Dedicated);
    }

    /// <summary>Preferencja Windows dla pliku .exe: 0 brak (decyduje system), 1 oszczędzanie energii, 2 wysoka wydajność.</summary>
    public const int PrefNone = 0, PrefPowerSaving = 1, PrefHighPerformance = 2;
    private const string PrefKey = @"Software\Microsoft\DirectX\UserGpuPreferences";
    /// <summary>Argument restartu po zmianie karty: drugi raz nie przełączamy (bez pętli restartów).</summary>
    public const string RestartArg = "--gpu-switched";

    private static string ExePath
    {
        get
        {
            string processPath = Environment.ProcessPath ?? "";
            if (!Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                return processPath;
            // Preferencja ma dotyczy? apphosta gry, nigdy wsp?lnego hosta dotnet.
            string assemblyPath = typeof(GpuSwitch).Assembly.Location;
            string gamePath = Path.ChangeExtension(assemblyPath, ".exe");
            return File.Exists(gamePath) ? gamePath : "";
        }
    }

    /// <summary>Karty widziane przez DXGI (bez programowych i Microsoft Basic Render). Pusta lista poza Windows albo przy błędzie.</summary>
    public static List<Adapter> Adapters()
    {
        var list = new List<Adapter>();
        if (!OperatingSystem.IsWindows()) return list;
        try { EnumDxgi(list); ReadGpuPreferenceRanks(list); } catch (Exception ex) { Log.Error("GPU: lista kart: " + ex.Message); }
        return list;
    }

    /// <summary>Mocniejsza karta niż ta, na której działa gra (rozpoznana po nazwie z GL_RENDERER), albo null.</summary>
    public static Adapter BetterThan(string glRenderer, IReadOnlyList<Adapter> adapters)
    {
        var preferred = PreferredAdapter(adapters, PrefHighPerformance);
        return preferred != null && preferred.Discrete && !SameCard(preferred.Name, glRenderer)
            ? preferred : null;
    }

    /// <summary>Karta preferowana przez system do oszcz?dzania energii, je?eli jest inna ni? obecna.</summary>
    public static Adapter IntegratedOther(string glRenderer, IReadOnlyList<Adapter> adapters)
    {
        var preferred = PreferredAdapter(adapters, PrefPowerSaving);
        return preferred != null && !SameCard(preferred.Name, glRenderer) ? preferred : null;
    }

    public static int PreferenceFor(Adapter target, IReadOnlyList<Adapter> adapters) =>
        PreferredAdapter(adapters, PrefHighPerformance) == target ? PrefHighPerformance : PrefPowerSaving;

    private static Adapter PreferredAdapter(IReadOnlyList<Adapter> adapters, int preference)
    {
        // DXGI zna preferencje systemu tak?e dla kart o identycznej pojemno?ci VRAM.
        // Starszy Windows: typ i pami?? s? jedynie przybli?eniem; remis nie usuwa wyboru.
        return preference == PrefHighPerformance
            ? adapters.OrderBy(a => a.PerformanceRank).ThenByDescending(a => a.Discrete)
                .ThenByDescending(a => a.DedicatedMemory).FirstOrDefault()
            : adapters.Reverse().OrderBy(a => a.PowerSavingRank).ThenBy(a => a.Discrete)
                .ThenBy(a => a.DedicatedMemory).FirstOrDefault();
    }

    /// <summary>Porównuje całe modele GPU, pomijając znane dopiski sterownika OpenGL i formatowanie nazw.</summary>
    public static bool SameCard(string adapterName, string glRenderer)
    {
        string adapterModel = NormalizeCardName(adapterName);
        string rendererModel = NormalizeCardName(glRenderer);
        return adapterModel.Length > 0 && adapterModel == rendererModel;
    }

    private static string NormalizeCardName(string name)
    {
        string model = (name ?? "").Trim();
        foreach (string suffix in new[] { "/PCIe/SSE2", "/SSE2" })
        {
            if (!model.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            model = model[..^suffix.Length];
            break;
        }
        return new string(model.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    public static int Preference()
    {
        if (!OperatingSystem.IsWindows() || ExePath.Length == 0) return PrefNone;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(PrefKey);
            return ParsePreference(k?.GetValue(ExePath) as string);
        }
        catch { return PrefNone; }
    }

    /// <summary>"GpuPreference=2;" → 2 (wartość może mieć też inne pola, np. "SwapEffectUpgradeEnable=1;").</summary>
    public static int ParsePreference(string v)
    {
        if (string.IsNullOrEmpty(v)) return PrefNone;
        foreach (var part in v.Split(';'))
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && kv[0].Trim() == "GpuPreference" && int.TryParse(kv[1], out int p)) return p;
        }
        return PrefNone;
    }

    /// <summary>Zapisuje preferencję Windows dla pliku gry; inne pola wartości zostają.</summary>
    public static bool SetPreference(int pref)
    {
        if (!OperatingSystem.IsWindows() || ExePath.Length == 0) return false;
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(PrefKey);
            string old = k.GetValue(ExePath) as string ?? "";
            var parts = old.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.TrimStart().StartsWith("GpuPreference=")).ToList();
            parts.Insert(0, "GpuPreference=" + pref);
            k.SetValue(ExePath, string.Join(";", parts) + ";");
            Log.Info($"GPU: preferencja Windows dla {ExePath} = {pref}");
            return true;
        }
        catch (Exception ex) { Log.Error("GPU: zapis preferencji: " + ex.Message); return false; }
    }

    /// <summary>Uruchamia grę ponownie z tymi samymi argumentami (plus RestartArg); wywołujący zamyka bieżące okno.</summary>
    public static bool Restart(IEnumerable<string> args)
    {
        try
        {
            if (ExePath.Length == 0) return false;
            var psi = new ProcessStartInfo(ExePath) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };
            foreach (var a in args.Where(a => a != RestartArg)) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add(RestartArg);
            using var process = Process.Start(psi);
            return process != null;
        }
        catch (Exception ex) { Log.Error("GPU: restart: " + ex.Message); return false; }
    }

    /// <summary>Gra renderuje na karcie bez ekranu, a ekran ma inna karta (laptop hybrydowy: RTX → kopia do zintegrowanej).
    /// Wtedy sterowniki OpenGL często ignorują VSync i obraz się rwie.
    /// Ekrany z EnumDisplayDevices (fizyczne podłączenie): DXGI w procesie z preferencją „Wysoka wydajność” pokazuje
    /// ekrany przy RTX, choć obraz i tak idzie przez zintegrowaną.</summary>
    public static bool CrossAdapter(string glRenderer, IReadOnlyList<string> displayAdapters) =>
        displayAdapters.Count > 0 && !displayAdapters.Any(d => SameCard(d, glRenderer));

    /// <summary>Nazwy kart, do których są podłączone ekrany pulpitu (np. "AMD Radeon(TM) 610M").</summary>
    public static List<string> DisplayAdapters()
    {
        var list = new List<string>();
        if (!OperatingSystem.IsWindows()) return list;
        try
        {
            var dd = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
            for (uint i = 0; EnumDisplayDevices(null, i, ref dd, 0); i++, dd.cb = Marshal.SizeOf<DisplayDevice>())
                if ((dd.StateFlags & 1) != 0 && !list.Contains(dd.DeviceString)) list.Add(dd.DeviceString); // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
        }
        catch (Exception ex) { Log.Error("GPU: lista ekranów: " + ex.Message); }
        return list;
    }

    public static IntPtr WindowMonitor(int x, int y, int width, int height)
    {
        if (!OperatingSystem.IsWindows()) return IntPtr.Zero;
        var bounds = new MonitorRect { Left = x, Top = y, Right = x + width, Bottom = y + height };
        return MonitorFromRect(ref bounds, 2);
    }

    public static string MonitorAdapter(IntPtr monitor)
    {
        if (monitor == IntPtr.Zero) return "";
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return "";
        var device = new DisplayDevice { cb = Marshal.SizeOf<DisplayDevice>() };
        for (uint i = 0; EnumDisplayDevices(null, i, ref device, 0); i++, device.cb = Marshal.SizeOf<DisplayDevice>())
            if (device.DeviceName == info.DeviceName) return device.DeviceString;
        return "";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfo
    {
        public int Size;
        public MonitorRect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
    }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref MonitorRect rect, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplayDevicesW")]
    private static extern bool EnumDisplayDevices(string device, uint devNum, ref DisplayDevice dd, uint flags);

    /// <summary>Czeka na najbliższe złożenie obrazu przez Windows (DWM), czyli na odświeżenie ekranu. false = niedostępne.</summary>
    public static bool DwmFlush() => OperatingSystem.IsWindows() && DwmFlushNative() >= 0;
    [DllImport("dwmapi.dll", EntryPoint = "DwmFlush")] private static extern int DwmFlushNative();

    // ---------------- DXGI (COM przez tablice funkcji, bez dodatkowych bibliotek) ----------------
    [DllImport("dxgi.dll")] private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct AdapterDesc1
    {
        public fixed char Description[128];
        public uint VendorId, DeviceId, SubSysId, Revision;
        public nuint DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public uint LuidLow; public int LuidHigh;
        public uint Flags;
    }

    private static unsafe void EnumDxgi(List<Adapter> list)
    {
        var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387"); // IDXGIFactory1
        if (CreateDXGIFactory1(ref iid, out var factory) < 0 || factory == IntPtr.Zero) return;
        try
        {
            var fvt = *(IntPtr**)factory;
            // IDXGIFactory1::EnumAdapters1 = 12 (IUnknown 3 + IDXGIObject 4 + IDXGIFactory 5)
            var enumAdapters1 = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)fvt[12];
            for (uint i = 0; i < 16; i++)
            {
                IntPtr adapter;
                if (enumAdapters1(factory, i, &adapter) < 0) break; // DXGI_ERROR_NOT_FOUND: koniec listy
                try
                {
                    var avt = *(IntPtr**)adapter;
                    // IDXGIAdapter1::GetDesc1 = 10 (IUnknown 3 + IDXGIObject 4 + IDXGIAdapter 3)
                    var getDesc1 = (delegate* unmanaged[Stdcall]<IntPtr, AdapterDesc1*, int>)avt[10];
                    AdapterDesc1 d;
                    if (getDesc1(adapter, &d) < 0) continue;
                    if ((d.Flags & 2) != 0 || d.VendorId == 0x1414) continue; // programowa / Microsoft Basic Render
                    string name = new string(d.Description).TrimEnd('\0');
                    list.Add(new Adapter(name, d.VendorId, d.DedicatedVideoMemory));
                }
                finally { Release(adapter); }
            }
        }
        finally { Release(factory); }
    }

    /// <summary>Kolejno?? Windows odpowiada trybom zapisywanym w UserGpuPreferences.</summary>
    private static unsafe void ReadGpuPreferenceRanks(List<Adapter> adapters)
    {
        var factoryId = new Guid("c1b6694f-ff09-44a9-b03c-77900a0a1d17"); // IDXGIFactory6
        if (CreateDXGIFactory1(ref factoryId, out var factory) < 0 || factory == IntPtr.Zero) return;
        try
        {
            var enumerate = (delegate* unmanaged[Stdcall]<IntPtr, uint, int, Guid*, IntPtr*, int>)(*(IntPtr**)factory)[29];
            var adapterId = new Guid("29038f61-3839-4626-91fd-086879011a05"); // IDXGIAdapter1
            foreach (int preference in new[] { PrefHighPerformance, PrefPowerSaving })
            {
                for (uint rank = 0; rank < 16; rank++)
                {
                    IntPtr adapter;
                    if (enumerate(factory, rank, preference, &adapterId, &adapter) < 0) break;
                    try
                    {
                        var describe = (delegate* unmanaged[Stdcall]<IntPtr, AdapterDesc1*, int>)(*(IntPtr**)adapter)[10];
                        AdapterDesc1 description;
                        if (describe(adapter, &description) < 0) continue;
                        string name = new string(description.Description).TrimEnd('\0');
                        int index = adapters.FindIndex(a => SameCard(a.Name, name));
                        if (index < 0) continue;
                        adapters[index] = preference == PrefHighPerformance
                            ? adapters[index] with { PerformanceRank = (int)rank }
                            : adapters[index] with { PowerSavingRank = (int)rank };
                    }
                    finally { Release(adapter); }
                }
            }
        }
        finally { Release(factory); }
    }

    private static unsafe void Release(IntPtr com)
    {
        if (com == IntPtr.Zero) return;
        ((delegate* unmanaged[Stdcall]<IntPtr, uint>)(*(IntPtr**)com)[2])(com);
    }
}
