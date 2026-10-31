using BlockCraft.Audio;
using BlockCraft.Core;

namespace BlockCraft;

/// <summary>
/// API dźwięku dla całej gry. Nazwy zdarzeń jak w sounds.json MC 1.8 (lista w Audio/SoundEvents.cs).
/// Wątek gry wywołuje Play/PlayUi/PlayRecord; mieszanie odbywa się w wątku audio.
/// </summary>
public static class Sound
{
    static readonly Mixer Mix = new();
    static SoundBank _bank = new();
    static Settings _settings;
    static volatile bool _started;
    static readonly object BankLock = new();

    public static bool Enabled => _started && Mix.Running;
    public static int ActiveVoices => Mix.Count;

    /// <summary>Start systemu: otwiera urządzenie, uruchamia pobieranie assetów w tle.</summary>
    public static void Init(Settings settings)
    {
        _settings = settings;
        Mix.CategoryVolume = CategoryVolume;
        _started = Mix.Start();
        AssetStore.Finished = ReloadBank;
        AssetStore.StartAsync();
        Music.Init();
    }

    public static void Shutdown() { _started = false; Mix.Stop(); }

    static void ReloadBank()
    {
        var b = SoundBank.Load();
        lock (BankLock) _bank = b;
        Log.Info("Sound: bank przeładowany, zdarzeń " + b.Events.Count);
    }

    /// <summary>Głośność kategorii 0..1 (master, music, record, weather, block, hostile, neutral, player, ambient).</summary>
    public static float CategoryVolume(string cat)
    {
        if (_settings?.SoundVolumes != null && _settings.SoundVolumes.TryGetValue(cat, out var v)) return v;
        return 1f;
    }
    public static void SetCategoryVolume(string cat, float v)
    {
        if (_settings == null) return;
        _settings.SoundVolumes[cat] = Math.Clamp(v, 0f, 1f);
    }

    /// <summary>Pozycja i kierunek słuchacza (yaw w stopniach, konwencja MC: 0 = +Z).</summary>
    public static void SetListener(double x, double y, double z, float yawDeg)
    {
        Mix.LX = (float)x; Mix.LY = (float)y; Mix.LZ = (float)z; Mix.LYaw = yawDeg;
    }

    /// <summary>Dźwięk w świecie (tłumienie liniowe do 16 * max(1, vol) bloków).</summary>
    public static void Play(string ev, double x, double y, double z, float vol = 1f, float pitch = 1f)
    {
        if (!Enabled || string.IsNullOrEmpty(ev)) return;
        try { Start(SoundEvents.Resolve(ref ev, ref vol, ref pitch), ev, x, y, z, vol, pitch, true); }
        catch (Exception ex) { Log.Error("Sound.Play " + ev + ": " + ex.Message); }
    }

    /// <summary>Dźwięk interfejsu, bez pozycji.</summary>
    public static void PlayUi(string ev, float vol = 1f, float pitch = 1f)
    {
        if (!Enabled || string.IsNullOrEmpty(ev)) return;
        try { Start(SoundEvents.Resolve(ref ev, ref vol, ref pitch), ev, 0, 0, 0, vol, pitch, false); }
        catch (Exception ex) { Log.Error("Sound.PlayUi " + ev + ": " + ex.Message); }
    }

    static Voice Start(bool resolved, string ev, double x, double y, double z, float vol, float pitch, bool positional, bool forceStream = false, float attOverride = 0)
    {
        SoundBank bank; lock (BankLock) bank = _bank;
        bank.Events.TryGetValue(ev, out var def);
        var v = new Voice { Positional = positional, X = x, Y = y, Z = z };
        float att = 16f * Math.Max(1f, vol);
        v.AttDist = attOverride > 0 ? attOverride : att;
        float entryVol = 1f, entryPitch = 1f;
        PcmData pcm = null; bool stream = false; string name = null;
        if (def != null)
        {
            v.Category = def.Category;
            var e = bank.Pick(def);
            if (e != null) { entryVol = e.Volume; entryPitch = e.Pitch; stream = e.Stream || forceStream; name = e.Name; }
        }
        else if (bank.HasAssets) return null; // nieznane zdarzenie przy załadowanych assetach: cisza
        v.Volume = Math.Clamp(vol, 0f, 1f) * entryVol;
        float p = Math.Clamp(pitch * entryPitch, 0.5f, 2f);
        if (name != null && stream)
        {
            var r = bank.OpenStream(name);
            if (r != null) { v.Reader = r; v.Channels = r.Channels; v.Rate = r.SampleRate; }
            else return null;
        }
        else
        {
            if (name != null) pcm = bank.GetPcm(name);
            if (pcm == null) pcm = bank.GetSynth(ev);
            v.Data = pcm.Data; v.Channels = pcm.Channels; v.Rate = pcm.Rate; v.Frames = pcm.Frames;
        }
        v.Step = p * v.Rate / 44100.0;
        Mix.Add(v);
        return v;
    }

    // ---- płyty (szafa grająca) ----
    static string Key(double x, double y, double z) => (int)Math.Floor(x) + "," + (int)Math.Floor(y) + "," + (int)Math.Floor(z);

    /// <summary>Odtwarza płytę (np. "cat" albo "records.cat") w pozycji szafy; głośność 4 = zasięg 64 bloki jak w MC.</summary>
    public static void PlayRecord(string name, double x, double y, double z)
    {
        if (!Enabled || string.IsNullOrEmpty(name)) return;
        StopRecord(x, y, z);
        try
        {
            string ev = name.StartsWith("records.") ? name : "records." + name;
            float vol = 4f, pitch = 1f;
            var v = Start(true, ev, x + 0.5, y + 0.5, z + 0.5, vol, pitch, true, true);
            if (v != null) { v.Tag = Key(x, y, z); v.Category = "record"; }
        }
        catch (Exception ex) { Log.Error("Sound.PlayRecord: " + ex.Message); }
    }

    public static void StopRecord(double x, double y, double z)
    {
        string k = Key(x, y, z);
        Mix.ForEach(v => { if (v.Tag == k) v.Done = true; });
    }

    // ---- muzyka (używane przez Music) ----
    internal static Voice StartMusic(string ev)
    {
        if (!Enabled) return null;
        try
        {
            float vol = 1f, pitch = 1f; string e = ev;
            var v = Start(true, e, 0, 0, 0, vol, pitch, false, true);
            if (v != null) { v.IsMusic = true; v.Category = "music"; }
            return v;
        }
        catch (Exception ex) { Log.Error("Sound.StartMusic: " + ex.Message); return null; }
    }

    internal static bool HasEvent(string ev) { lock (BankLock) return _bank.Events.ContainsKey(ev); }
    internal static bool BankReady { get { lock (BankLock) return _bank.HasAssets; } }

    /// <summary>Wstrzymanie wszystkiego (np. wyjście do menu): kasuje głosy nie-muzyczne.</summary>
    public static void StopAll() => Mix.ForEach(v => v.Done = true);
}
