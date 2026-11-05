using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BlockCraft.World;

namespace BlockCraft.Core;

/// <summary>Paczka zasobów MC 1.8: plik .zip albo folder z pack.mcmeta i assets/minecraft/...
/// Ścieżki zasobów jak w MC, np. "assets/minecraft/textures/blocks/stone.png".</summary>
public sealed class ResourcePack : IDisposable
{
    /// <summary>Nazwa pliku albo folderu w resourcepacks/ (dla wbudowanej: <see cref="ResourcePacks.BuiltinId"/>).</summary>
    public readonly string Id;
    public readonly string Path;
    public readonly bool Builtin;
    public string Name => Builtin ? "Minecraft 1.8 (oryginalne)" : _zip ? System.IO.Path.GetFileNameWithoutExtension(Path) : System.IO.Path.GetFileName(Path);
    /// <summary>Nazwa źródła (folder albo repozytorium z ustawień), null = resourcepacks/ albo wbudowana.</summary>
    public readonly string Source;
    public string Description { get; private set; } = "";
    public int Format { get; private set; } = -1;
    public uint[] Icon { get; private set; }
    public int IconW { get; private set; }
    public int IconH { get; private set; }
    /// <summary>Czemu paczki nie da się użyć (null = w porządku).</summary>
    public string Error { get; private set; }

    private readonly bool _zip;
    private readonly object _lock = new();
    private ZipArchive _arc;
    private Dictionary<string, ZipArchiveEntry> _entries;
    /// <summary>Zip z paczką w podfolderze (częsty błąd przy pakowaniu): prefiks ścieżek wewnątrz archiwum.</summary>
    private string _prefix = "";

    public ResourcePack(string id, string path, bool builtin = false, string source = null)
    {
        Id = id; Path = path; Builtin = builtin; Source = source;
        _zip = File.Exists(path);
        try { ReadInfo(); }
        catch (Exception ex) { Error = "uszkodzona paczka (" + ex.Message + ")"; Close(); }
    }

    private void ReadInfo()
    {
        if (_zip) OpenZip();
        byte[] meta = Read("pack.mcmeta");
        if (meta == null && !Builtin) { Error = "brak pliku pack.mcmeta"; return; }
        if (meta != null)
        {
            using var doc = JsonDocument.Parse(meta, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (!TryReadPackSection(doc.RootElement, out var pack, out int format))
            {
                if (!Builtin) { Error = "nieprawidłowy pack.mcmeta (brak sekcji pack z liczbowym pack_format)"; Close(); return; }
            }
            else
            {
                Format = format;
                if (pack.TryGetProperty("description", out var d)) Description = TextOf(d);
            }
        }
        if (Builtin) { Format = 1; Description = "Tekstury z pobranego klienta 1.8"; }
        byte[] png = Read("pack.png");
        if (png != null)
        {
            try
            {
                var img = StbImageSharp.ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                var px = new uint[img.Width * img.Height];
                Buffer.BlockCopy(img.Data, 0, px, 0, px.Length * 4);
                Icon = px; IconW = img.Width; IconH = img.Height;
            }
            catch (Exception ex) { Log.Error("Paczka " + Id + ": pack.png: " + ex.Message); }
        }
        Close();
    }

    /// <summary>Sekcja "pack" z liczbowym "pack_format" – bez nich pack.mcmeta jest nieprawidłowy.</summary>
    private static bool TryReadPackSection(JsonElement root, out JsonElement pack, out int format)
    {
        format = -1; pack = default;
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("pack", out pack) && pack.ValueKind == JsonValueKind.Object
            && pack.TryGetProperty("pack_format", out var f) && f.ValueKind == JsonValueKind.Number && f.TryGetInt32(out format);
    }

    /// <summary>Opis z pack.mcmeta: zwykły tekst albo komponent JSON ({"text":..., "extra":[...]}).</summary>
    private static string TextOf(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.String: return e.GetString();
            case JsonValueKind.Array: return string.Concat(e.EnumerateArray().Select(TextOf));
            case JsonValueKind.Object:
                var sb = new StringBuilder();
                if (e.TryGetProperty("text", out var t)) sb.Append(TextOf(t));
                if (e.TryGetProperty("extra", out var x)) sb.Append(TextOf(x));
                return sb.ToString();
            default: return "";
        }
    }

    private void OpenZip()
    {
        if (_arc != null) return;
        _arc = ZipFile.OpenRead(Path);
        _entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        foreach (var e in _arc.Entries) if (!string.IsNullOrEmpty(e.Name)) _entries[e.FullName.Replace('\\', '/')] = e;
        _prefix = "";
        if (!Builtin && !_entries.ContainsKey("pack.mcmeta"))
        {
            // jedyny najpłycej położony podfolder (dowolnej głębokości) z pack.mcmeta w środku
            var inner = _entries.Keys.Where(k => k.EndsWith("/pack.mcmeta")).GroupBy(k => k.Count(c => c == '/')).OrderBy(g => g.Key).FirstOrDefault()?.ToList();
            if (inner?.Count == 1) _prefix = inner[0][..^"pack.mcmeta".Length];
        }
    }

    /// <summary>Zawartość pliku albo null, gdy go nie ma.</summary>
    public byte[] Read(string rel)
    {
        if (Error != null) return null;
        lock (_lock)
        {
            try
            {
                if (_zip)
                {
                    OpenZip();
                    if (!_entries.TryGetValue(_prefix + rel, out var e)) return null;
                    using var s = e.Open(); using var ms = new MemoryStream((int)Math.Max(0, Math.Min(e.Length, int.MaxValue)));
                    s.CopyTo(ms); return ms.ToArray();
                }
                if (rel.Contains("..")) return null;
                string full = System.IO.Path.Combine(Path, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                return File.Exists(full) ? File.ReadAllBytes(full) : null;
            }
            catch (Exception ex) { Log.Error("Paczka " + Id + ": " + rel + ": " + ex.Message); return null; }
        }
    }

    /// <summary>Zwalnia uchwyt archiwum (Windows blokuje otwarty plik); kolejny odczyt otworzy je ponownie.</summary>
    public void Close()
    {
        lock (_lock) { _arc?.Dispose(); _arc = null; _entries = null; }
    }

    public void Dispose() => Close();
}

/// <summary>Lista paczek zasobów: dostępne w folderze resourcepacks/ i aktywne (kolejność: pierwsza ma pierwszeństwo).</summary>
public static class ResourcePacks
{
    public const string BuiltinId = "minecraft-1.8";
    /// <summary>pack_format paczek dla MC 1.8.</summary>
    public const int Format18 = 1;

    public static string Dir
    {
        get { var d = Path.Combine(WorldStorage.Root, "resourcepacks"); Directory.CreateDirectory(d); return d; }
    }

    private static List<ResourcePack> _active = new();
    public static IReadOnlyList<ResourcePack> Active => _active;

    /// <summary>Dostępne paczki: wbudowana (gdy klient 1.8 jest pobrany), pliki .zip i foldery z pack.mcmeta
    /// z resourcepacks/ oraz ze źródeł (<see cref="ContentSources"/>, identyfikator "źródło:nazwa").
    /// Z podanym dir: tylko ten folder (testy).</summary>
    public static List<ResourcePack> Scan(string dir = null)
    {
        var list = new List<ResourcePack>();
        if (dir != null) { ScanDir(list, dir, null); return list; }
        string jar = Path.Combine(ClientAssets.Root, "client.jar");
        if (File.Exists(jar)) list.Add(new ResourcePack(BuiltinId, jar, true));
        ScanDir(list, Dir, null);
        foreach (var s in ContentSources.All)
        {
            if (!s.Available) continue;
            // źródło, które samo jest paczką (pack.mcmeta w głównym folderze)
            if (File.Exists(Path.Combine(s.Dir, "pack.mcmeta"))) AddUnique(list, s.Name + ":" + s.Name, s.Dir, s.Name);
            else ScanDir(list, s.PacksDir, s.Name);
        }
        return list;
    }

    private static void ScanDir(List<ResourcePack> list, string dir, string source)
    {
        string prefix = source == null ? "" : source + ":";
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*.zip").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                AddUnique(list, prefix + Path.GetFileName(f), f, source);
            foreach (var d in Directory.EnumerateDirectories(dir).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                if (File.Exists(Path.Combine(d, "pack.mcmeta"))) AddUnique(list, prefix + Path.GetFileName(d), d, source);
        }
        catch (Exception ex) { Log.Error("Paczki zasobów (" + dir + "): " + ex.Message); }
    }

    /// <summary>Dodaje paczkę, chyba że jej nazwa jest już zajęta (np. folder o nazwie wbudowanej paczki 1.8).</summary>
    private static void AddUnique(List<ResourcePack> list, string id, string path, string source)
    {
        if (list.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))) { Log.Info("Paczka zasobów pominięta: nazwa " + id + " jest już zajęta"); return; }
        list.Add(new ResourcePack(id, path, false, source));
    }

    /// <summary>Ustawia aktywne paczki wg identyfikatorów (nieznane i uszkodzone pomija).</summary>
    public static void SetActive(IEnumerable<string> ids, string dir = null)
    {
        foreach (var p in _active) p.Dispose();
        var avail = Scan(dir).ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var list = new List<ResourcePack>();
        foreach (var id in ids ?? Enumerable.Empty<string>())
        {
            if (avail.TryGetValue(id, out var p) && p.Error == null && !list.Contains(p)) list.Add(p);
            else Log.Info("Paczka zasobów pominięta: " + id + (p?.Error != null ? " (" + p.Error + ")" : " (nie znaleziono)"));
        }
        foreach (var p in avail.Values) if (!list.Contains(p)) p.Dispose();
        _active = list;
        if (list.Count > 0) Log.Info("Paczki zasobów: " + string.Join(", ", list.Select(p => p.Id)));
    }

    /// <summary>Plik z pierwszej aktywnej paczki, która go ma (null = zasób wbudowany w grę).</summary>
    public static byte[] Read(string rel)
    {
        foreach (var p in _active) { var b = p.Read(rel); if (b != null) return b; }
        return null;
    }

    /// <summary>Odczyt z każdej aktywnej paczki osobno, w kolejności pierwszeństwa.</summary>
    public static IReadOnlyList<Func<string, byte[]>> Readers => _active.Select(p => (Func<string, byte[]>)p.Read).ToList();

    public static void CloseAll() { foreach (var p in _active) p.Close(); }

    /// <summary>Obraz z pierwszej aktywnej paczki ("gui/widgets.png" względem assets/minecraft/textures/), null = brak.</summary>
    public static RawImage ReadImage(string texturePath)
    {
        var data = Read(Render.TextureGen.TexturesRoot + texturePath);
        return data == null ? null : RawImage.Decode(data, texturePath);
    }
}

/// <summary>Obraz RGBA (piksel = uint jak w TextureGen: R w najmłodszym bajcie).</summary>
public sealed class RawImage
{
    public int W, H;
    public uint[] Px;
    public string Name = "";

    public static RawImage Decode(byte[] png, string name = "")
    {
        try
        {
            var img = StbImageSharp.ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            var px = new uint[img.Width * img.Height];
            Buffer.BlockCopy(img.Data, 0, px, 0, Math.Min(img.Data.Length, px.Length * 4));
            return new RawImage { W = img.Width, H = img.Height, Px = px, Name = name };
        }
        catch (Exception ex) { Log.Error("Paczka zasobów: " + name + ": " + ex.Message); return null; }
    }

    /// <summary>Ten obraz, gdy ma wymiary spełniające warunek; inaczej błąd w logu i null (element jak bez paczki).</summary>
    public RawImage RequireSize(Func<RawImage, bool> valid, string expected)
    {
        if (valid(this)) return this;
        Log.Error($"Paczka zasobów: {Name}: złe wymiary {W}x{H}, oczekiwano {expected}");
        return null;
    }

    public uint At(int x, int y) => x >= 0 && y >= 0 && x < W && y < H ? Px[y * W + x] : 0;

    /// <summary>Wycinek (x,y,w,h) w pikselach tego obrazu.</summary>
    public RawImage Crop(int x, int y, int w, int h)
    {
        var r = new RawImage { W = w, H = h, Px = new uint[w * h] };
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) r.Px[j * w + i] = At(x + i, y + j);
        return r;
    }

    /// <summary>Powiększenie bez wygładzania o całkowity współczynnik.</summary>
    public RawImage Upscale(int k) => k <= 1 ? this : Resize(W * k, H * k);

    /// <summary>Skalowanie do dowolnego rozmiaru metodą najbliższego piksela.</summary>
    public RawImage Resize(int w, int h)
    {
        if (w == W && h == H) return this;
        var r = new RawImage { W = w, H = h, Px = new uint[w * h] };
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) r.Px[y * w + x] = Px[(y * H / h) * W + x * W / w];
        return r;
    }

    /// <summary>Kopiuje prostokąt z src w to samo miejsce (oba obrazy w tej samej skali).</summary>
    public void Paste(RawImage src, int x, int y, int w, int h)
    {
        for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
            if (x + i < W && y + j < H) Px[(y + j) * W + x + i] = src.At(x + i, y + j);
    }
}
