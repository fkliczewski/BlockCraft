using System.Security.Cryptography;
using System.Text.Json;
using BlockCraft.Core;

namespace BlockCraft.Audio;

/// <summary>
/// Pobieranie assetów dźwiękowych Mojang jak launcher (version_manifest_v2 -> wersja 1.8 -> assetIndex -> obiekty).
/// Cache w %LOCALAPPDATA%\BlockCraft\assets (indexes/ + objects/xx/hash). Pliki audio nigdy nie trafiają do repo.
/// </summary>
public static class AssetStore
{
    const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
    const string ObjectsUrl = "https://resources.download.minecraft.net/";
    const string Version = "1.8";

    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockCraft", "assets");

    /// <summary>Klucz zasobu (np. minecraft/sounds/dig/stone1.ogg) -> hash SHA1.</summary>
    static readonly Dictionary<string, string> Index = new(StringComparer.Ordinal);
    static readonly Dictionary<string, long> Sizes = new(StringComparer.Ordinal);
    static readonly object Lock = new();

    public static volatile string Status = "";
    public static volatile bool Done, Failed;
    public static volatile int Total, Completed;
    public static float Progress => Total <= 0 ? (Done ? 1f : 0f) : Completed / (float)Total;
    /// <summary>Wywoływane (z wątku pobierania) po zakończeniu - bank dźwięków może się przeładować.</summary>
    public static Action Finished;

    static string IndexPath => Path.Combine(Root, "indexes", Version + ".json");
    public static string ObjectPath(string hash) => Path.Combine(Root, "objects", hash.Substring(0, 2), hash);

    public static string PathOf(string key)
    {
        lock (Lock) return Index.TryGetValue(key, out var h) && File.Exists(ObjectPath(h)) ? ObjectPath(h) : null;
    }

    static bool Wanted(string key) => key.StartsWith("minecraft/sounds", StringComparison.Ordinal) || key.StartsWith("minecraft/music", StringComparison.Ordinal) || key.StartsWith("minecraft/records", StringComparison.Ordinal);

    static void LoadIndex(string json)
    {
        using var doc = JsonDocument.Parse(json);
        lock (Lock)
        {
            Index.Clear(); Sizes.Clear();
            foreach (var o in doc.RootElement.GetProperty("objects").EnumerateObject())
            {
                if (!Wanted(o.Name)) continue;
                Index[o.Name] = o.Value.GetProperty("hash").GetString();
                Sizes[o.Name] = o.Value.GetProperty("size").GetInt64();
            }
        }
    }

    /// <summary>Uruchamia pobieranie w tle. Nigdy nie rzuca wyjątku.</summary>
    public static void StartAsync()
    {
        ClientAssets.StartAsync();
        var t = new Thread(() =>
        {
            try { Run().GetAwaiter().GetResult(); }
            catch (Exception ex) { Failed = true; Status = "Dźwięki: błąd pobierania (" + ex.Message + ")"; Log.Error("AssetStore: " + ex); }
            finally { Done = true; try { Finished?.Invoke(); } catch (Exception ex) { Log.Error("AssetStore.Finished: " + ex.Message); } }
        }) { IsBackground = true, Name = "AssetStore" };
        t.Start();
    }

    static int _completed;

    static async Task Run()
    {
        Directory.CreateDirectory(Path.Combine(Root, "indexes"));
        using var http = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 8 }) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BlockCraft/1.0");

        // 1. indeks: z cache albo z sieci
        bool haveIndex = false;
        if (File.Exists(IndexPath))
        {
            try { LoadIndex(File.ReadAllText(IndexPath)); haveIndex = Index.Count > 0; } catch { }
        }
        if (haveIndex && AllPresent())
        {
            Status = "Dźwięki: gotowe (" + Index.Count + " plików)";
            Log.Info("AssetStore: cache kompletny, " + Index.Count + " plików");
            return;
        }
        try
        {
            Status = "Dźwięki: pobieranie listy wersji...";
            var manifest = await http.GetStringAsync(ManifestUrl);
            string verUrl = null;
            using (var md = JsonDocument.Parse(manifest))
                foreach (var v in md.RootElement.GetProperty("versions").EnumerateArray())
                    if (v.GetProperty("id").GetString() == Version) { verUrl = v.GetProperty("url").GetString(); break; }
            if (verUrl == null) throw new Exception("brak wersji " + Version);
            var verJson = await http.GetStringAsync(verUrl);
            string idxUrl;
            using (var vd = JsonDocument.Parse(verJson)) idxUrl = vd.RootElement.GetProperty("assetIndex").GetProperty("url").GetString();
            var idxJson = await http.GetStringAsync(idxUrl);
            LoadIndex(idxJson);
            File.WriteAllText(IndexPath, idxJson);
        }
        catch (Exception ex)
        {
            if (!haveIndex) throw;
            Log.Error("AssetStore: indeks z sieci niedostępny, używam cache: " + ex.Message);
        }

        // 2. brakujące obiekty
        List<(string key, string hash, long size)> todo;
        lock (Lock) todo = Index.Where(kv => !File.Exists(ObjectPath(kv.Value))).Select(kv => (kv.Key, kv.Value, Sizes[kv.Key])).ToList();
        Total = todo.Count; Completed = 0; _completed = 0;
        Log.Info($"AssetStore: do pobrania {todo.Count} plików, {todo.Sum(t => t.size) / 1024 / 1024} MB");
        int failed = 0;
        var sem = new SemaphoreSlim(8);
        var tasks = todo.Select(async t =>
        {
            await sem.WaitAsync();
            try
            {
                if (!await Download(http, t.hash)) Interlocked.Increment(ref failed);
            }
            finally { sem.Release(); int c = Interlocked.Increment(ref _completed); Completed = c; Status = $"Dźwięki: pobieranie {c}/{Total}"; }
        }).ToList();
        await Task.WhenAll(tasks);
        Status = failed == 0 ? "Dźwięki: gotowe" : $"Dźwięki: brak {failed} plików (zastępcze)";
        Log.Info("AssetStore: koniec, błędów " + failed);
    }

    static bool AllPresent()
    {
        lock (Lock) return Index.Values.All(h => File.Exists(ObjectPath(h)));
    }

    static async Task<bool> Download(HttpClient http, string hash)
    {
        string path = ObjectPath(hash), part = path + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                long have = File.Exists(part) ? new FileInfo(part).Length : 0;
                using var req = new HttpRequestMessage(HttpMethod.Get, ObjectsUrl + hash.Substring(0, 2) + "/" + hash);
                if (have > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(have, null);
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (resp.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable) { File.Delete(part); continue; }
                if (!resp.IsSuccessStatusCode) throw new Exception("HTTP " + (int)resp.StatusCode);
                bool append = resp.StatusCode == System.Net.HttpStatusCode.PartialContent;
                using (var fs = new FileStream(part, append ? FileMode.Append : FileMode.Create, FileAccess.Write))
                    await resp.Content.CopyToAsync(fs);
                string sha;
                using (var fs = File.OpenRead(part)) sha = Convert.ToHexString(await SHA1.HashDataAsync(fs)).ToLowerInvariant();
                if (sha != hash) { File.Delete(part); continue; }
                File.Move(part, path, true);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt == 2) Log.Error("AssetStore " + hash + ": " + ex.Message);
                await Task.Delay(500);
            }
        }
        return false;
    }
}
