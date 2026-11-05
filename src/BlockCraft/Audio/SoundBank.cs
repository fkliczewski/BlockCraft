using System.Text.Json;
using NVorbis;
using BlockCraft.Core;

namespace BlockCraft.Audio;

public sealed class SoundEntry { public string Name; public float Volume = 1f, Pitch = 1f; public bool Stream, IsEvent; public int Weight = 1; }
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

    /// <summary>Paczki zasobów (pierwsza ma pierwszeństwo): ścieżka w paczce -> bajty albo null.</summary>
    private IReadOnlyList<Func<string, byte[]>> _packs = Array.Empty<Func<string, byte[]>>();
    private readonly Dictionary<string, bool> _fileAvailability = new(StringComparer.Ordinal);
    private IReadOnlyList<Func<string, bool>> _packFileLookups;
    public const string PackSoundsJson = "assets/minecraft/sounds.json";

    /// <summary>sounds.json z pobranych zasobów 1.8, potem z paczek od najmniej ważnej: zdarzenie z "replace": true
    /// zastępuje dotychczasowe, inaczej jego dźwięki są dopisywane (jak SoundHandler 1.8).</summary>
    public static SoundBank Load(IReadOnlyList<Func<string, byte[]>> packs = null, IReadOnlyList<string> domains = null,
        IReadOnlyList<Func<string, bool>> packFileLookups = null)
    {
        var b = new SoundBank { _packs = packs ?? Array.Empty<Func<string, byte[]>>(), _packFileLookups = packFileLookups };
        try
        {
            string p = AssetStore.PathOf("minecraft/sounds.json");
            if (p != null) b.Merge(File.ReadAllBytes(p), "zasoby 1.8");
        }
        catch (Exception ex) { Log.Error("SoundBank: " + ex.Message); }
        int fromPacks = 0;
        for (int i = b._packs.Count - 1; i >= 0; i--)
        {
            foreach (string domain in domains ?? new[] { "minecraft" })
            {
                byte[] json;
                try { json = b._packs[i]("assets/" + domain + "/sounds.json"); }
                catch (Exception ex) { Log.Error("SoundBank: paczka: " + ex.Message); continue; }
                if (json != null) fromPacks += b.Merge(json, "paczka zasobów " + domain, domain);
            }
        }
        Log.Info("SoundBank: " + b.Events.Count + " zdarzeń z sounds.json" + (fromPacks > 0 ? $" ({fromPacks} z paczek zasobów)" : ""));
        b._fileAvailability.Clear();
        b._packFileLookups = null;
        return b;
    }

    /// <summary>Dokłada zdarzenia z jednego sounds.json; zwraca ich liczbę.</summary>
    private int Merge(byte[] json, string source, string domain = "minecraft")
    {
        int n = 0;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            foreach (var ev in doc.RootElement.EnumerateObject())
            {
                if (ev.Value.ValueKind != JsonValueKind.Object) continue;
                string eventName = domain == "minecraft" ? ev.Name : domain + ":" + ev.Name;
                bool replace = ev.Value.TryGetProperty("replace", out var rp) && rp.ValueKind == JsonValueKind.True;
                if (replace || !Events.TryGetValue(eventName, out var def))
                {
                    Events[eventName] = def = new SoundEventDef();
                    if (ev.Value.TryGetProperty("category", out var cat) && cat.ValueKind == JsonValueKind.String) def.Category = cat.GetString();
                }
                if (ev.Value.TryGetProperty("sounds", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var s in arr.EnumerateArray())
                    {
                        var e = new SoundEntry();
                        if (s.ValueKind == JsonValueKind.String) e.Name = s.GetString();
                        else if (s.ValueKind == JsonValueKind.Object && s.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String)
                        {
                            e.Name = nm.GetString();
                            if (s.TryGetProperty("type", out var type))
                            {
                                if (type.ValueKind != JsonValueKind.String || type.GetString() is not ("file" or "event")) continue;
                                e.IsEvent = type.GetString() == "event";
                            }
                            if (s.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out float vf) && float.IsFinite(vf)) e.Volume = vf;
                            if (s.TryGetProperty("pitch", out var pi) && pi.ValueKind == JsonValueKind.Number && pi.TryGetSingle(out float pf) && float.IsFinite(pf)) e.Pitch = pf;
                            if (s.TryGetProperty("stream", out var st) && st.ValueKind is JsonValueKind.True or JsonValueKind.False) e.Stream = st.GetBoolean();
                            if (s.TryGetProperty("weight", out var w) && w.ValueKind == JsonValueKind.Number && w.TryGetInt32(out int wi)) e.Weight = Math.Max(1, wi);
                        }
                        else continue;
                        e.Name = ResolveName(e.Name, domain);
                        if (!e.IsEvent && _packs.Count > 0 && !FileExists(e.Name)) continue;
                        def.Entries.Add(e); def.TotalWeight += e.Weight;
                    }
                n++;
            }
        }
        catch (Exception ex) { Log.Error("SoundBank (" + source + "): " + ex.Message); }
        return n;
    }

    private static string ResolveName(string name, string domain)
    {
        string qualified = name.Contains(':') ? name : domain + ":" + name;
        return qualified.StartsWith("minecraft:", StringComparison.Ordinal) ? qualified["minecraft:".Length..] : qualified;
    }

    private bool FileExists(string name)
    {
        if (_fileAvailability.TryGetValue(name, out bool exists)) return exists;
        try
        {
            if (_packFileLookups != null)
            {
                string key = FileKey(name);
                exists = _packFileLookups.Any(contains => contains("assets/" + key)) || AssetStore.PathOf(key) != null;
            }
            else
            {
                using var file = OpenFile(name);
                exists = file != null;
            }
        }
        catch (Exception ex)
        {
            Log.Error("SoundBank: " + name + ": " + ex.Message);
            exists = false;
        }
        _fileAvailability[name] = exists;
        return exists;
    }

    /// <summary>Plik dźwięku: z pierwszej paczki, która go ma, inaczej z pobranych zasobów 1.8. Null = brak.</summary>
    private Stream OpenFile(string name)
    {
        foreach (var read in _packs)
        {
            byte[] data;
            try { data = read("assets/" + FileKey(name)); }
            catch (Exception ex) { Log.Error("SoundBank: paczka " + FileKey(name) + ": " + ex.Message); continue; }
            if (data != null) return new MemoryStream(data, false);
        }
        string path = AssetStore.PathOf(FileKey(name));
        return path == null ? null : File.OpenRead(path);
    }

    private sealed class SelectionContext
    {
        public readonly HashSet<SoundEventDef> Path = new();
        public readonly Dictionary<(SoundEventDef Definition, int Depth), long> Weights = new();
        public int RemainingWork = 100_000;
        public int Cycles;
    }

    public SoundEntry Pick(SoundEventDef def)
    {
        var context = new SelectionContext();
        var selected = Pick(def, context);
        return context.RemainingWork > 0 ? selected : null;
    }

    private static long AddWeight(long total, long weight) => total > long.MaxValue - weight ? long.MaxValue : total + weight;

    private long EntryWeight(SoundEntry entry, SelectionContext context) => entry.IsEvent
        ? Events.TryGetValue(entry.Name, out var target) ? EventWeight(target, context) : 0
        : entry.Weight;

    private long EventWeight(SoundEventDef definition, SelectionContext context)
    {
        if (context.Path.Count >= 64) return 0;
        if (context.Path.Contains(definition)) { context.Cycles++; return 0; }
        var key = (definition, context.Path.Count);
        if (context.Weights.TryGetValue(key, out long cached)) return cached;
        context.Path.Add(definition);
        int cycles = context.Cycles;
        long total = 0;
        try
        {
            foreach (var entry in definition.Entries)
            {
                if (--context.RemainingWork <= 0) return 0;
                total = AddWeight(total, EntryWeight(entry, context));
            }
            // A cyclic graph depends on the current path; do not cache that result.
            if (cycles == context.Cycles) context.Weights[key] = total;
            return total;
        }
        finally { context.Path.Remove(definition); }
    }

    private SoundEntry Pick(SoundEventDef definition, SelectionContext context)
    {
        long total = EventWeight(definition, context);
        if (total == 0 || context.RemainingWork <= 0 || !context.Path.Add(definition)) return null;
        try
        {
            long remaining = Random.Shared.NextInt64(total);
            foreach (var entry in definition.Entries)
            {
                if (--context.RemainingWork <= 0) return null;
                remaining -= EntryWeight(entry, context);
                if (remaining >= 0) continue;
                return entry.IsEvent ? Pick(Events[entry.Name], context) : entry;
            }
            return null;
        }
        finally { context.Path.Remove(definition); }
    }

    public static string FileKey(string name)
    {
        int separator = name.IndexOf(':');
        string domain = separator > 0 ? name[..separator] : "minecraft";
        string path = separator >= 0 ? name[(separator + 1)..] : name;
        return domain + "/sounds/" + path + ".ogg";
    }

    /// <summary>Dekoduje (z cache LRU) cały plik. Null gdy brak pliku lub błąd.</summary>
    public PcmData GetPcm(string name)
    {
        string key = FileKey(name);
        if (_cache.TryGetValue(key, out var node)) { _lru.Remove(node); _lru.AddFirst(node); return node.Value.pcm; }
        try
        {
            var file = OpenFile(name);
            if (file == null) return null;
            using var r = new VorbisReader(file, true);
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
        try { return OpenFile(name) is { } file ? new VorbisReader(file, true) : null; }
        catch (Exception ex) { Log.Error("SoundBank stream " + name + ": " + ex.Message); return null; }
    }
}
