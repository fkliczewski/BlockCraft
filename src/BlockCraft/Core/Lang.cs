using System.IO.Compression;
using System.Text;
using BlockCraft.Audio;

namespace BlockCraft.Core;

/// <summary>Pliki językowe MC 1.8 (assets/minecraft/lang/xx_XX.lang): z client.jar (en_US), z pobranych zasobów 1.8
/// (pozostałe języki) i z paczek zasobów. Jak w 1.8 najpierw en_US, potem wybrany język; w każdym kroku paczki od najmniej ważnej.
/// Bez wybranego języka gra używa własnych polskich nazw.</summary>
public static class Lang
{
    private static Dictionary<string, string> _table;
    private static string _code = "";
    private static IReadOnlyList<Func<string, byte[]>> _packs = Array.Empty<Func<string, byte[]>>();

    /// <summary>Kod wybranego języka ("" = nazwy gry).</summary>
    public static int Revision { get; private set; }
    public static string Code => _code;
    public static bool Active => _table != null;
    public static string Get(string key) => _table != null && _table.TryGetValue(key, out var v) ? v : null;

    /// <summary>Wczytuje język (kod pusty = nazwy gry) z podanych paczek (pierwsza ma pierwszeństwo).</summary>
    public static void Load(string code, IReadOnlyList<Func<string, byte[]>> packs, Func<string, byte[]> official = null)
    {
        Revision++;
        _code = code ?? ""; _packs = packs ?? Array.Empty<Func<string, byte[]>>();
        if (_code.Length == 0) { _table = null; return; }
        official ??= OfficialFile;
        var t = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var lang in _code == "en_US" ? new[] { "en_US" } : new[] { "en_US", _code })
        {
            Merge(t, SafeRead(official, lang));
            for (int i = _packs.Count - 1; i >= 0; i--) Merge(t, SafeRead(_packs[i], "assets/minecraft/lang/" + lang + ".lang"));
        }
        _table = t;
        Log.Info($"Język: {_code}, {t.Count} tekstów");
    }

    /// <summary>Ponownie z tym samym językiem (np. po pobraniu zasobów albo zmianie paczek).</summary>
    public static void Reload(IReadOnlyList<Func<string, byte[]>> packs) => Load(_code, packs);

    private static byte[] SafeRead(Func<string, byte[]> read, string path)
    {
        try { return read(path); } catch (Exception ex) { Log.Error("Język: " + path + ": " + ex.Message); return null; }
    }

    /// <summary>Plik .lang 1.8: linie klucz=wartość, # komentarz.</summary>
    public static void Merge(Dictionary<string, string> t, byte[] data)
    {
        if (data == null) return;
        foreach (var raw in Encoding.UTF8.GetString(data).Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length == 0 || line[0] == '#') continue;
            int eq = line.IndexOf('=');
            if (eq > 0) t[line[..eq]] = line[(eq + 1)..];
        }
    }

    /// <summary>Oficjalny plik języka: en_US z client.jar, pozostałe z pobranych zasobów 1.8 (null = brak).</summary>
    public static byte[] OfficialFile(string code)
    {
        try
        {
            if (code == "en_US")
            {
                string jar = Path.Combine(ClientAssets.Root, "client.jar");
                if (!File.Exists(jar)) return null;
                using var zip = ZipFile.OpenRead(jar);
                var e = zip.GetEntry("assets/minecraft/lang/en_US.lang");
                if (e == null) return null;
                using var s = e.Open(); using var ms = new MemoryStream(); s.CopyTo(ms); return ms.ToArray();
            }
            string p = AssetStore.PathOf("minecraft/lang/" + code + ".lang");
            return p == null ? null : File.ReadAllBytes(p);
        }
        catch (Exception ex) { Log.Error("Język " + code + ": " + ex.Message); return null; }
    }

    /// <summary>Dostępne języki: kod i nazwa ("Polski (Polska)") z oficjalnych zasobów i z paczek.</summary>
    public static List<(string Code, string Name)> Available(IReadOnlyList<Func<string, byte[]>> packs,
        Func<IEnumerable<string>> officialCodes = null, Func<string, byte[]> official = null)
    {
        officialCodes ??= OfficialCodes;
        official ??= OfficialFile;
        var codes = new SortedSet<string>(officialCodes()) { "en_US" };
        var declarations = new Dictionary<string, (string Name, string Region)>();
        foreach (var read in (packs ?? Array.Empty<Func<string, byte[]>>()).Reverse())
            foreach (var language in PackLanguages(read))
            {
                codes.Add(language.Code);
                declarations[language.Code] = (language.Name, language.Region);
            }
        var list = new List<(string, string)>();
        foreach (var code in codes)
        {
            var t = new Dictionary<string, string>();
            Merge(t, SafeRead(official, code));
            foreach (var read in (packs ?? Array.Empty<Func<string, byte[]>>()).Reverse()) Merge(t, SafeRead(read, "assets/minecraft/lang/" + code + ".lang"));
            if (t.Count == 0 && !declarations.ContainsKey(code)) continue;
            string name = t.GetValueOrDefault("language.name", code), region = t.GetValueOrDefault("language.region", "");
            if (declarations.TryGetValue(code, out var declaration))
            {
                name = declaration.Name ?? name;
                region = declaration.Region ?? region;
            }
            list.Add((code, region.Length > 0 ? $"{name} ({region})" : name));
        }
        return list.OrderBy(l => l.Item2, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static IEnumerable<string> OfficialCodes() => AssetStore.Keys
        .Where(key => key.StartsWith("minecraft/lang/", StringComparison.Ordinal) && key.EndsWith(".lang", StringComparison.Ordinal))
        .Select(key => key["minecraft/lang/".Length..^".lang".Length]);

    /// <summary>Języki dodane przez paczkę: sekcja "language" w pack.mcmeta (jak w 1.8).</summary>
    private static IEnumerable<(string Code, string Name, string Region)> PackLanguages(Func<string, byte[]> read)
    {
        var meta = SafeRead(read, "pack.mcmeta");
        if (meta == null) yield break;
        System.Text.Json.JsonDocument doc;
        try { doc = System.Text.Json.JsonDocument.Parse(meta, new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = System.Text.Json.JsonCommentHandling.Skip }); }
        catch { yield break; }
        using (doc)
        {
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("language", out var langs) && langs.ValueKind == System.Text.Json.JsonValueKind.Object)
                foreach (var language in langs.EnumerateObject())
                {
                    var value = language.Value;
                    string name = null, region = null;
                    if (value.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        if (value.TryGetProperty("name", out var n) && n.ValueKind == System.Text.Json.JsonValueKind.String) name = n.GetString();
                        if (value.TryGetProperty("region", out var r) && r.ValueKind == System.Text.Json.JsonValueKind.String) region = r.GetString();
                    }
                    yield return (language.Name, name, region);
                }
        }
    }
}
