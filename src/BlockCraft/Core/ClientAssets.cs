using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace BlockCraft.Core;

/// <summary>Oryginalne tekstury środowiska 1.8 z oficjalnego klienta; wyłącznie lokalny cache.</summary>
public static class ClientAssets
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockCraft", "assets", "client-1.8");
    public static string WeatherPath(string name) => Path.Combine(Root, name + ".png");
    public static volatile bool Ready, Done;
    private static int _started;

    public static void StartAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            try { await Load(); Ready = true; Log.Info("ClientAssets: tekstury opadów 1.8 gotowe"); }
            catch (Exception ex) { Log.Error("ClientAssets: tekstury zastępcze, " + ex.Message); }
            finally { Done = true; }
        });
    }

    private static async Task Load()
    {
        Directory.CreateDirectory(Root);
        if (new[] { "rain", "snow" }.All(n => File.Exists(WeatherPath(n)))) return;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        string metadata = Path.Combine(Root, "version.json");
        if (!File.Exists(metadata))
        {
            using var manifest = JsonDocument.Parse(await http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json"));
            var version = manifest.RootElement.GetProperty("versions").EnumerateArray().First(v => v.GetProperty("id").GetString() == "1.8");
            string json = await http.GetStringAsync(version.GetProperty("url").GetString());
            await AtomicWrite(metadata, System.Text.Encoding.UTF8.GetBytes(json));
        }
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(metadata));
        var client = doc.RootElement.GetProperty("downloads").GetProperty("client");
        string sha = client.GetProperty("sha1").GetString(), jar = Path.Combine(Root, "client.jar");
        async Task<bool> ValidJar()
        {
            if (!File.Exists(jar)) return false;
            using var stream = File.OpenRead(jar);
            return Convert.ToHexString(await SHA1.HashDataAsync(stream)).Equals(sha, StringComparison.OrdinalIgnoreCase);
        }
        if (!await ValidJar())
        {
            byte[] data = await http.GetByteArrayAsync(client.GetProperty("url").GetString());
            if (!Convert.ToHexString(SHA1.HashData(data)).Equals(sha, StringComparison.OrdinalIgnoreCase)) throw new IOException("niezgodny SHA1 klienta");
            await AtomicWrite(jar, data);
        }
        using var zip = ZipFile.OpenRead(jar);
        foreach (string name in new[] { "rain", "snow" })
        {
            var entry = zip.GetEntry("assets/minecraft/textures/environment/" + name + ".png") ?? throw new IOException("brak " + name);
            using var input = entry.Open(); using var bytes = new MemoryStream(); await input.CopyToAsync(bytes);
            await AtomicWrite(WeatherPath(name), bytes.ToArray());
        }
    }

    /// <summary>Synchronicznie czyta obraz RGBA z pobranego wcześniej client.jar (np. "painting/paintings_kristoffer_zetterstrand.png").
    /// Null, gdy klienta jeszcze nie ma (pierwsze uruchomienie): wtedy wołający rysuje teksturę zastępczą.</summary>
    public static uint[] TryReadTexture(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            string jar = Path.Combine(Root, "client.jar");
            if (!File.Exists(jar)) return null;
            using var zip = ZipFile.OpenRead(jar);
            var entry = zip.GetEntry("assets/minecraft/textures/" + path);
            if (entry == null) return null;
            using var input = entry.Open(); using var bytes = new MemoryStream(); input.CopyTo(bytes); bytes.Position = 0;
            var img = StbImageSharp.ImageResult.FromStream(bytes, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            width = img.Width; height = img.Height;
            var px = new uint[width * height];
            Buffer.BlockCopy(img.Data, 0, px, 0, Math.Min(img.Data.Length, px.Length * 4));
            return px;
        }
        catch (Exception ex) { Log.Error("ClientAssets: " + path + ": " + ex.Message); return null; }
    }

    private static async Task AtomicWrite(string path, byte[] bytes)
    {
        // Host i gość mogą pobierać równocześnie; nie współdzielą pliku częściowego.
        string part = path + "." + Guid.NewGuid().ToString("N") + ".part";
        try { await File.WriteAllBytesAsync(part, bytes); File.Move(part, path, true); }
        finally { if (File.Exists(part)) File.Delete(part); }
    }
}
