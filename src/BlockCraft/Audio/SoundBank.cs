using System.Text.Json;
using NVorbis;
using BlockCraft.Core;

namespace BlockCraft.Audio;

public sealed class SoundEntry { public string Name; public float Volume = 1f, Pitch = 1f; public bool Stream; public int Weight = 1; }
public sealed class SoundEventDef { public string Category = "master"; public List<SoundEntry> Entries = new(); public int TotalWeight; }

/// <summary>sounds.json MC 1.8 + dekodowanie ogg (cache LRU w pamięci) + zastępcze dźwięki.</summary>
public sealed class SoundBank
{
    public readonly Dictionary<string, SoundEventDef> Events = new(StringComparer.Ordinal);
    readonly Dictionary<string, LinkedListNode<(string key, PcmData pcm)>> _cache = new();
    readonly LinkedList<(string key, PcmData pcm)> _lru = new();
    long _cacheFloats;
    const long MaxCacheFloats = 24_000_000; // ok. 96 MB

    public bool HasAssets => Events.Count > 0;

    public static SoundBank Load()
    {
        var b = new SoundBank();
        try
        {
            string p = AssetStore.PathOf("minecraft/sounds.json");
            if (p == null) return b;
            using var doc = JsonDocument.Parse(File.ReadAllText(p));
            foreach (var ev in doc.RootElement.EnumerateObject())
            {
                var def = new SoundEventDef();
                if (ev.Value.TryGetProperty("category", out var cat)) def.Category = cat.GetString();
                if (ev.Value.TryGetProperty("sounds", out var arr))
                    foreach (var s in arr.EnumerateArray())
                    {
                        var e = new SoundEntry();
                        if (s.ValueKind == JsonValueKind.String) e.Name = s.GetString();
                        else
                        {
                            e.Name = s.GetProperty("name").GetString();
                            if (s.TryGetProperty("volume", out var v)) e.Volume = v.GetSingle();
                            if (s.TryGetProperty("pitch", out var pi)) e.Pitch = pi.GetSingle();
                            if (s.TryGetProperty("stream", out var st)) e.Stream = st.GetBoolean();
                            if (s.TryGetProperty("weight", out var w)) e.Weight = Math.Max(1, w.GetInt32());
                        }
                        def.Entries.Add(e); def.TotalWeight += e.Weight;
                    }
                b.Events[ev.Name] = def;
            }
            Log.Info("SoundBank: " + b.Events.Count + " zdarzeń z sounds.json");
        }
        catch (Exception ex) { Log.Error("SoundBank: " + ex.Message); }
        return b;
    }

    public SoundEntry Pick(SoundEventDef def)
    {
        if (def.Entries.Count == 0) return null;
        int r = Random.Shared.Next(def.TotalWeight);
        foreach (var e in def.Entries) { r -= e.Weight; if (r < 0) return e; }
        return def.Entries[0];
    }

    public static string FileKey(string name) => "minecraft/sounds/" + name + ".ogg";

    /// <summary>Dekoduje (z cache LRU) cały plik. Null gdy brak pliku lub błąd.</summary>
    public PcmData GetPcm(string name)
    {
        string key = FileKey(name);
        if (_cache.TryGetValue(key, out var node)) { _lru.Remove(node); _lru.AddFirst(node); return node.Value.pcm; }
        string path = AssetStore.PathOf(key);
        if (path == null) return null;
        try
        {
            using var r = new VorbisReader(path);
            var list = new List<float[]>();
            int total = 0;
            var tmp = new float[16384 * r.Channels];
            int n;
            while ((n = r.ReadSamples(tmp, 0, tmp.Length)) > 0) { var c = new float[n]; Array.Copy(tmp, c, n); list.Add(c); total += n; }
            var all = new float[total]; int o = 0;
            foreach (var c in list) { Array.Copy(c, 0, all, o, c.Length); o += c.Length; }
            var pcm = new PcmData { Data = all, Channels = r.Channels, Rate = r.SampleRate };
            Put(key, pcm);
            return pcm;
        }
        catch (Exception ex) { Log.Error("SoundBank decode " + name + ": " + ex.Message); return null; }
    }

    public PcmData GetSynth(string ev)
    {
        string key = "synth:" + ev;
        if (_cache.TryGetValue(key, out var node)) { _lru.Remove(node); _lru.AddFirst(node); return node.Value.pcm; }
        var pcm = Synth.Make(ev);
        Put(key, pcm);
        return pcm;
    }

    void Put(string key, PcmData pcm)
    {
        var n = _lru.AddFirst((key, pcm));
        _cache[key] = n; _cacheFloats += pcm.Data.Length;
        while (_cacheFloats > MaxCacheFloats && _lru.Count > 1)
        {
            var last = _lru.Last; _lru.RemoveLast(); _cache.Remove(last.Value.key); _cacheFloats -= last.Value.pcm.Data.Length;
        }
    }

    public VorbisReader OpenStream(string name)
    {
        string path = AssetStore.PathOf(FileKey(name));
        if (path == null) return null;
        try { return new VorbisReader(path); } catch (Exception ex) { Log.Error("SoundBank stream " + name + ": " + ex.Message); return null; }
    }
}
