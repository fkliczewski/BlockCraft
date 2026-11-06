using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using BlockCraft.World;

namespace BlockCraft.Core;

/// <summary>Jedno źródło paczek (a w przyszłości modów): folder lokalny albo repozytorium git.</summary>
public sealed class ContentSource
{
    /// <summary>Tak jak wpisano: ścieżka, URL repo (opcjonalnie "#gałąź").</summary>
    public string Spec { get; init; }
    /// <summary>Krótka nazwa, prefiks identyfikatorów paczek ("blockcraft-plugins:moja-paczka").</summary>
    public string Name { get; init; }
    public bool Remote { get; init; }
    public string Url { get; init; }
    public string Branch { get; init; }
    /// <summary>Katalog z zawartością: folder lokalny albo kopia repo w %APPDATA%\.blockcraft\sources.</summary>
    public string Dir { get; init; }
    /// <summary>Stan do pokazania w grze ("aktualne", "pobieranie...", opis błędu).</summary>
    public volatile string Status = "";
    public bool Available => Directory.Exists(Dir);

    /// <summary>Foldery, w których leżą paczki zasobów: &lt;źródło&gt;/resourcepacks, a gdy go nie ma — samo źródło.</summary>
    public string PacksDir
    {
        get
        {
            var rp = Path.Combine(Dir, "resourcepacks");
            return Directory.Exists(rp) ? rp : Dir;
        }
    }
}

/// <summary>Źródła paczek z ustawień (PackSources) albo z --pack-source. Repozytoria git są pobierane w tle przy starcie
/// (git, a bez gita archiwum zip z GitHuba); bez sieci zostaje ostatnia pobrana wersja. Foldery lokalne są obserwowane:
/// po zapisaniu pliku gra przeładowuje paczki.</summary>
public static class ContentSources
{
    private static List<ContentSource> _all = new();
    private static readonly List<FileSystemWatcher> _watchers = new();
    private static long _changedAt; // Stopwatch.GetTimestamp ostatniej zmiany (0 = brak)
    private static int _version;
    private static readonly object _lock = new();

    public static IReadOnlyList<ContentSource> All => _all;
    /// <summary>Rośnie przy każdej zmianie zawartości źródeł (synchronizacja, zapis pliku) — do odświeżania list w GUI.</summary>
    public static int Version => _version;
    public static string CacheRoot => Path.Combine(WorldStorage.Root, "sources");

    public static bool IsRemote(string spec) =>
        spec.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || spec.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || spec.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || spec.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
        || spec.EndsWith(".git", StringComparison.OrdinalIgnoreCase);

    /// <summary>Ustawia listę źródeł (bez pobierania). Puste i powtórzone wpisy są pomijane.</summary>
    public static void Configure(IEnumerable<string> specs, string cacheRoot = null, bool watch = true)
    {
        cacheRoot ??= CacheRoot;
        var list = new List<ContentSource>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "" };
        foreach (var raw in specs ?? Enumerable.Empty<string>())
        {
            string spec = raw?.Trim();
            if (string.IsNullOrEmpty(spec) || list.Any(s => string.Equals(s.Spec, spec, StringComparison.OrdinalIgnoreCase))) continue;
            bool remote = IsRemote(spec);
            string url = spec, branch = null;
            if (remote && spec.LastIndexOf('#') is int h and > 0) { url = spec[..h]; branch = spec[(h + 1)..]; }
            string baseName = Slug(remote ? url.TrimEnd('/') : Path.GetFullPath(spec).TrimEnd('\\', '/'));
            string name = baseName;
            for (int i = 2; !names.Add(name); i++) name = baseName + "-" + i;
            list.Add(new ContentSource
            {
                Spec = spec, Name = name, Remote = remote, Url = url, Branch = branch,
                Dir = remote ? Path.Combine(cacheRoot, name) : Path.GetFullPath(spec),
                Status = remote ? (Directory.Exists(Path.Combine(cacheRoot, name)) ? "ostatnia pobrana wersja" : "jeszcze nie pobrane") : Directory.Exists(spec) ? "folder lokalny" : "brak folderu",
            });
        }
        lock (_lock) _all = list;
        if (watch) Watch();
    }

    /// <summary>Nazwa z ostatniego członu ścieżki/URL: litery, cyfry, '-', '_' i '.'.</summary>
    private static string Slug(string s)
    {
        string last = s.Split('/', '\\', ':').LastOrDefault(p => p.Length > 0) ?? "zrodlo";
        if (last.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) last = last[..^4];
        var chars = last.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? char.ToLowerInvariant(c) : '-').ToArray();
        var r = new string(chars).Trim('-', '.');
        return r.Length == 0 ? "zrodlo" : r;
    }

    // ================= obserwowanie folderów lokalnych =================

    private static void Watch()
    {
        lock (_watchers)
        {
            foreach (var w in _watchers) w.Dispose();
            _watchers.Clear();
            var dirs = new List<string> { ResourcePacks.Dir };
            dirs.AddRange(_all.Where(s => !s.Remote && s.Available).Select(s => s.Dir));
            foreach (var d in dirs)
            {
                try
                {
                    var w = new FileSystemWatcher(d) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
                    FileSystemEventHandler h = (_, e) => { if (!e.FullPath.Contains(Path.DirectorySeparatorChar + ".git")) MarkChanged(); };
                    w.Changed += h; w.Created += h; w.Deleted += h; w.Renamed += (_, e) => MarkChanged();
                    w.EnableRaisingEvents = true;
                    _watchers.Add(w);
                }
                catch (Exception ex) { Log.Error("Obserwowanie " + d + ": " + ex.Message); }
            }
        }
    }

    public static void MarkChanged()
    {
        Interlocked.Increment(ref _version);
        Interlocked.Exchange(ref _changedAt, Stopwatch.GetTimestamp());
    }

    /// <summary>True raz po serii zmian, gdy od ostatniej minęło pół sekundy (zapis pliku to kilka zdarzeń naraz).</summary>
    public static bool ConsumeChange()
    {
        long t = Interlocked.Read(ref _changedAt);
        if (t == 0 || Stopwatch.GetElapsedTime(t).TotalMilliseconds < 500) return false;
        return Interlocked.CompareExchange(ref _changedAt, 0, t) == t;
    }

    // ================= pobieranie repozytoriów =================

    private static bool? _gitAvailable;
    public static bool GitAvailable => _gitAvailable ??= RunGit(null, TimeSpan.FromSeconds(10), "--version").code == 0;

    /// <summary>Pobiera zmiany ze wszystkich repozytoriów. Zwraca true, gdy zawartość któregoś się zmieniła.</summary>
    public static async Task<bool> SyncAllAsync(CancellationToken ct = default)
    {
        bool any = false;
        foreach (var s in _all.Where(s => s.Remote).ToList())
        {
            ct.ThrowIfCancellationRequested();
            try { any |= await Task.Run(() => Sync(s), ct); }
            catch (Exception ex) { s.Status = "błąd: " + ex.Message; Log.Error("Źródło " + s.Name + ": " + ex.Message); }
        }
        if (any) MarkChanged();
        return any;
    }

    /// <summary>Jedno repozytorium: git (klon płytki, potem fetch + reset do zdalnego stanu) albo zip z GitHuba.</summary>
    public static bool Sync(ContentSource s)
    {
        s.Status = "pobieranie...";
        Directory.CreateDirectory(Path.GetDirectoryName(s.Dir));
        bool changed = GitAvailable ? SyncGit(s) : SyncGithubZip(s);
        Log.Info($"Źródło {s.Name}: {s.Status}");
        return changed;
    }

    private static bool SyncGit(ContentSource s)
    {
        var timeout = TimeSpan.FromSeconds(60);
        if (!Directory.Exists(Path.Combine(s.Dir, ".git")))
        {
            string tmp = s.Dir + ".pobieranie";
            TryDelete(tmp);
            var args = new List<string> { "clone", "--depth", "1" };
            if (s.Branch != null) { args.Add("--branch"); args.Add(s.Branch); }
            args.Add(s.Url); args.Add(tmp);
            var (code, err) = RunGit(null, timeout, args.ToArray());
            if (code != 0) { TryDelete(tmp); s.Status = Directory.Exists(s.Dir) ? "bez połączenia - ostatnia pobrana wersja" : "nie udało się pobrać: " + FirstLine(err); return false; }
            TryDelete(s.Dir);
            Directory.Move(tmp, s.Dir);
            s.Status = "pobrane";
            return true;
        }
        string before = Head(s.Dir);
        var fetch = RunGit(s.Dir, timeout, "fetch", "--depth", "1", "origin", s.Branch ?? "HEAD");
        if (fetch.code != 0) { s.Status = "bez połączenia - ostatnia pobrana wersja (" + FirstLine(fetch.err) + ")"; return false; }
        var reset = RunGit(s.Dir, timeout, "reset", "--hard", "FETCH_HEAD");
        if (reset.code != 0) { s.Status = "błąd aktualizacji: " + FirstLine(reset.err); return false; }
        RunGit(s.Dir, timeout, "clean", "-fdq");
        string after = Head(s.Dir);
        s.Status = before == after ? "aktualne" : "zaktualizowane";
        return before != after;
    }

    private static string Head(string dir)
    {
        var (code, outp) = RunGit(dir, TimeSpan.FromSeconds(10), true, "rev-parse", "HEAD");
        return code == 0 ? outp.Trim() : "";
    }

    /// <summary>Bez gita: https://github.com/WŁAŚCICIEL/REPO -> archiwum zip (ETag: nie pobieramy, gdy nic się nie zmieniło).</summary>
    private static bool SyncGithubZip(ContentSource s)
    {
        var m = System.Text.RegularExpressions.Regex.Match(s.Url, @"github\.com[/:]([^/]+)/([^/#]+?)(\.git)?/?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) { s.Status = "brak programu git (bez gita obsługiwane są tylko repozytoria GitHub)"; return false; }
        string zipUrl = $"https://github.com/{m.Groups[1].Value}/{m.Groups[2].Value}/archive/{(s.Branch != null ? "refs/heads/" + s.Branch : "HEAD")}.zip";
        string etagFile = Path.Combine(s.Dir, ".blockcraft-etag");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        using var req = new HttpRequestMessage(HttpMethod.Get, zipUrl);
        if (File.Exists(etagFile)) req.Headers.TryAddWithoutValidation("If-None-Match", File.ReadAllText(etagFile));
        HttpResponseMessage resp;
        try { resp = http.Send(req); }
        catch (Exception ex) { s.Status = Directory.Exists(s.Dir) ? "bez połączenia - ostatnia pobrana wersja" : "nie udało się pobrać: " + ex.Message; return false; }
        using (resp)
        {
            if (resp.StatusCode == HttpStatusCode.NotModified) { s.Status = "aktualne"; return false; }
            if (!resp.IsSuccessStatusCode) { s.Status = "nie udało się pobrać: HTTP " + (int)resp.StatusCode; return false; }
            string tmp = s.Dir + ".pobieranie";
            TryDelete(tmp);
            using (var zip = new ZipArchive(resp.Content.ReadAsStream()))
                foreach (var e in zip.Entries)
                {
                    int slash = e.FullName.IndexOf('/');
                    string rel = slash >= 0 ? e.FullName[(slash + 1)..] : "";
                    if (rel.Length == 0 || rel.Contains("..")) continue;
                    string dst = Path.Combine(tmp, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (rel.EndsWith('/')) { Directory.CreateDirectory(dst); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    e.ExtractToFile(dst, true);
                }
            Directory.CreateDirectory(tmp);
            if (resp.Headers.ETag != null) File.WriteAllText(Path.Combine(tmp, ".blockcraft-etag"), resp.Headers.ETag.ToString());
            TryDelete(s.Dir);
            Directory.Move(tmp, s.Dir);
            s.Status = "pobrane (zip)";
            return true;
        }
    }

    private static (int code, string err) RunGit(string dir, TimeSpan timeout, params string[] args) => RunGit(dir, timeout, false, args);

    /// <summary>git bez okna i bez pytania o hasło; stdout (gdy wantOut) albo stderr w wyniku.</summary>
    private static (int code, string text) RunGit(string dir, TimeSpan timeout, bool wantOut, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            if (dir != null) psi.WorkingDirectory = dir;
            foreach (var a in args) psi.ArgumentList.Add(a);
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            psi.Environment["GCM_INTERACTIVE"] = "never";
            using var p = Process.Start(psi);
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit((int)timeout.TotalMilliseconds)) { try { p.Kill(true); } catch { } return (-1, "przekroczono czas"); }
            p.WaitForExit();
            return (p.ExitCode, wantOut ? outTask.Result : errTask.Result);
        }
        catch (Exception ex) { return (-1, ex.Message); }
    }

    private static string FirstLine(string s) => (s ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("Cloning")) ?? "";

    private static void TryDelete(string dir)
    {
        if (!Directory.Exists(dir)) return;
        // pliki w .git bywają tylko do odczytu
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
        Directory.Delete(dir, true);
    }
}
