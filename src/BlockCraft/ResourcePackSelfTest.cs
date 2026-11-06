using System.IO.Compression;
using BlockCraft.Core;
using BlockCraft.Render;

namespace BlockCraft;

/// <summary>Paczki zasobów 1.8: odczyt zip/folderu, kolejność, przypisania tekstur, animacje .mcmeta, powrót do tekstur gry.</summary>
public static partial class SelfTest
{
    private static byte[] Png(int w, int h, Func<int, int, uint> px)
    {
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                uint c = px(x, y); int o = (y * w + x) * 4;
                data[o] = (byte)c; data[o + 1] = (byte)(c >> 8); data[o + 2] = (byte)(c >> 16); data[o + 3] = (byte)(c >> 24);
            }
        using var ms = new MemoryStream();
        new StbImageWriteSharp.ImageWriter().WritePng(data, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, ms);
        return ms.ToArray();
    }

    private static void WriteZip(string path, Dictionary<string, byte[]> files)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, data) in files) { using var s = zip.CreateEntry(name).Open(); s.Write(data); }
    }

    private static byte[] Utf8(string s) => System.Text.Encoding.UTF8.GetBytes(s);

    /// <summary>Czy .mcmeta dla paska frameCount klatek jest odrzucany (tekstura zostaje generowana).</summary>
    private static bool Rejected(byte[] mcmeta, int frameCount)
    {
        try { TextureGen.AnimationSequence(mcmeta, frameCount); return false; }
        catch (System.Text.Json.JsonException) { return true; }
    }

    private static void TestResourcePacks()
    {
        Console.WriteLine("[Paczki zasobów]");
        // .mcmeta: frametime, lista klatek, {index,time}, brak pliku
        Check(TextureGen.AnimationSequence(Utf8("{\"animation\":{\"frametime\":2,\"frames\":[0,{\"index\":2,\"time\":3}]}}"), 4) is [(0, 2), (2, 3)], "mcmeta: frametime i klatki {index,time}");
        Check(TextureGen.AnimationSequence(null, 1) == null, "Obraz 16x16 bez mcmeta jest statyczny");
        Check(TextureGen.AnimationSequence(Utf8("{\"animation\":{\"frametime\":3,\"frames\":[]}}"), 2) is [(0, 3), (1, 3)], "mcmeta: pusta lista frames = wszystkie klatki");
        Check(Rejected(null, 3), "Pasek bez mcmeta odrzucony");
        Check(Rejected(Utf8("{}"), 2), "Pasek z mcmeta bez sekcji animation odrzucony");
        Check(Rejected(Utf8("{\"animation\":{\"frames\":[7,1]}}"), 2), "mcmeta: klatka spoza paska odrzuca teksturę");
        Check(Rejected(Utf8("{zepsuty"), 2), "Uszkodzony mcmeta odrzuca teksturę paczki");

        // HD 32x32 -> 16x16: kolory uśrednione, alfa wycinana zostaje ostra
        var hd = new uint[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) hd[y * 32 + x] = x < 16 ? 0xFF0000FF : (y % 2 == 0 ? 0xFF00FF00u : 0u);
        var s16 = TextureGen.ScaleTo16(hd, 32);
        Check(s16[0] == 0xFF0000FF, "HD 32x: kolor bloku 2x2", s16[0].ToString("X8"));
        Check(s16[15] >> 24 == 255 && (s16[15] & 0xFF00) == 0xFF00, "HD 32x: połowa pikseli wyciętych = piksel pełny", s16[15].ToString("X8"));
        var strip = TextureGen.DecodePackImage(Png(16, 48, (x, y) => 0xFF000000 | (uint)(y / 16 * 40)), Utf8("{\"animation\":{}}"), "test");
        Check(strip != null && strip.Frames.Count == 3 && strip.Anim != null && (strip.Frames[2][0] & 0xFF) == 80, "Pasek 16x48 = 3 klatki animacji");
        Check(TextureGen.DecodePackImage(Png(16, 20, (x, y) => 0xFFFFFFFF), null, "test") == null, "Zły rozmiar tekstury odrzucony");

        // przypisania wskazują na istniejące tekstury gry
        var rules = TextureGen.PackRules;
        var unknown = rules.Alias.Keys.Concat(rules.Transform.Keys).Concat(rules.Custom.Keys).Concat(rules.Skip).Where(n => !TextureGen.Has(n)).ToList();
        Check(unknown.Count == 0, "Tabela przypisań: same znane tekstury", string.Join(", ", unknown.Take(10)));

        var original = TextureGen.Textures.Select(t => (uint[])t.Clone()).ToList();
        string dir = Path.Combine(Path.GetTempPath(), "blockcraft-packs-" + Environment.ProcessId);
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            const string T = TextureGen.TexturesRoot;
            byte[] Solid(uint c) => Png(16, 16, (x, y) => c);
            WriteZip(Path.Combine(dir, "a.zip"), new()
            {
                ["pack.mcmeta"] = Utf8("{\"pack\":{\"pack_format\":1,\"description\":\"Paczka A\"}}"),
                ["pack.png"] = Png(8, 8, (x, y) => 0xFF00FF00),
                [T + "blocks/stone.png"] = Solid(0xFF0000FF),
                [T + "blocks/torch_on.png"] = Solid(0xFF00FFFF),
                [T + "blocks/grass_side.png"] = Solid(0xFF204060),
                [T + "blocks/grass_side_overlay.png"] = Png(16, 16, (x, y) => y < 4 ? 0xFF909090 : 0),
                [T + "items/clock.png"] = Png(16, 16 * 64, (x, y) => 0xFF000000 | (uint)(y / 16)),
                [T + "items/clock.png.mcmeta"] = Utf8("{\"animation\":{}}"),
                [T + "blocks/lava_still.png"] = Png(16, 32, (x, y) => 0xFF0000AA),
                [T + "blocks/water_still.png"] = Png(16, 32, (x, y) => y < 16 ? 0xFFFF0000 : 0xFF00FF00),
                [T + "blocks/water_still.png.mcmeta"] = Utf8("{\"animation\":{\"frametime\":3}}"),
                [T + "blocks/rail_normal_turned.png"] = Png(16, 16, (x, y) => x == 0 && y == 0 ? 0xFFFFFFFF : 0),
            });
            WriteZip(Path.Combine(dir, "b.zip"), new()
            {
                ["b/pack.mcmeta"] = Utf8("{\"pack\":{\"pack_format\":2,\"description\":{\"text\":\"Paczka \",\"extra\":[{\"text\":\"B\"}]}}}"),
                [ "b/" + T + "blocks/stone.png"] = Solid(0xFFFF0000),
                [ "b/" + T + "blocks/dirt.png"] = Solid(0xFF123456),
                [ "b/" + T + "blocks/lava_still.png.mcmeta"] = Utf8("{\"animation\":{\"frametime\":2}}"),
            });
            WriteZip(Path.Combine(dir, "zepsuta.zip"), new() { ["readme.txt"] = Utf8("to nie paczka") });
            File.WriteAllBytes(Path.Combine(dir, "smieci.zip"), Utf8("to nie zip"));
            var folder = Path.Combine(dir, "folder");
            Directory.CreateDirectory(Path.Combine(folder, "assets/minecraft/textures/blocks"));
            File.WriteAllBytes(Path.Combine(folder, "pack.mcmeta"), Utf8("{\"pack\":{\"pack_format\":1,\"description\":\"Folder\"}}"));
            File.WriteAllBytes(Path.Combine(folder, "assets/minecraft/textures/blocks/sand.png"), Solid(0xFF00AA00));

            var scan = ResourcePacks.Scan(dir).ToDictionary(p => p.Id);
            Check(scan.Count == 5, "Skanowanie: zipy i folder", string.Join(",", scan.Keys));
            Check(scan["a.zip"].Error == null && scan["a.zip"].Description == "Paczka A" && scan["a.zip"].Format == 1 && scan["a.zip"].IconW == 8, "pack.mcmeta i pack.png", scan["a.zip"].Description);
            Check(scan["b.zip"].Error == null && scan["b.zip"].Description == "Paczka B" && scan["b.zip"].Format == 2, "Paczka w podfolderze zipa i opis jako komponent JSON", scan["b.zip"].Description + " " + scan["b.zip"].Error);
            Check(scan["zepsuta.zip"].Error != null && scan["smieci.zip"].Error != null, "Zip bez pack.mcmeta i uszkodzony plik oznaczone jako błędne");
            Check(scan["folder"].Error == null && scan["folder"].Name == "folder", "Paczka jako folder");
            foreach (var p in scan.Values) p.Dispose();

            // kolejność: A nad B (kamień z A, ziemia z B)
            ResourcePacks.SetActive(new[] { "a.zip", "b.zip", "zepsuta.zip", "brak.zip", "folder" }, dir);
            Check(ResourcePacks.Active.Select(p => p.Id).SequenceEqual(new[] { "a.zip", "b.zip", "folder" }), "Aktywne: pominięte błędne i brakujące", string.Join(",", ResourcePacks.Active.Select(p => p.Id)));
            var changed = TextureGen.ApplyPacks(ResourcePacks.Readers);
            ResourcePacks.CloseAll();
            Check(TextureGen.Get("stone")[0] == 0xFF0000FF, "Wyższa paczka wygrywa (kamień z A)", TextureGen.Get("stone")[0].ToString("X8"));
            Check(TextureGen.Get("dirt")[0] == 0xFF123456, "Plik tylko w niższej paczce (ziemia z B)");
            Check(TextureGen.Get("sand")[0] == 0xFF00AA00, "Tekstura z paczki-folderu");
            Check(TextureGen.Get("torch")[0] == 0xFF00FFFF, "Przypisanie nazwy: torch -> torch_on");
            Check((TextureGen.Get("item_clock_5")[0] & 0xFF) == 5 && (TextureGen.Get("item_clock_63")[0] & 0xFF) == 63, "Zegar: klatki z paska clock.png");
            Check(!TextureGen.PackAnimations.ContainsKey("item_clock"), "Zegar nie animuje się z czasem");
            var gs = TextureGen.Get("grass_side");
            Check(gs[0] >> 24 == 250 && gs[15 * 16] == 0xFF204060, "Bok trawy: nakładka oznaczona do barwienia, reszta z grass_side", gs[0].ToString("X8"));
            Check(TextureGen.Get("grass_side_item")[0] >> 24 == 255, "Bok trawy w ekwipunku: zabarwiony, nieprzezroczysty");
            Check(TextureGen.PackAnimations.TryGetValue("water_still", out var wa) && wa.Steps is [(0, 3), (1, 3)] && TextureGen.Get("water_still")[0] == 0xFFFF0000, "Woda: animacja z mcmeta (frametime 3)");
            Check(!TextureGen.PackOverridden.Contains("lava_still") && !TextureGen.PackAnimations.ContainsKey("lava_still"), "Pasek z A nie bierze .mcmeta z niższej paczki B");
            Check(TextureGen.Get("rail_turn_6")[0] == 0xFFFFFFFF && TextureGen.Get("rail_turn_8")[255] == 0xFFFFFFFF, "Zakręt szyn obracany wg meta");
            Check(TextureGen.PackOverridden.Contains("stone") && !TextureGen.PackOverridden.Contains("cobblestone"), "Bez pliku w paczce zostaje tekstura gry");
            Check(changed.Contains(TextureGen.Layer("stone")) && !changed.Contains(TextureGen.Layer("cobblestone")), "Lista zmienionych warstw");

            // wyłączenie paczek: wszystko jak przed
            ResourcePacks.SetActive(Array.Empty<string>(), dir);
            var back = TextureGen.ApplyPacks((Func<string, byte[]>)null);
            bool same = TextureGen.Textures.Select((t, i) => t.AsSpan().SequenceEqual(original[i])).All(b => b);
            Check(same && TextureGen.PackAnimations.Count == 0 && back.Contains(TextureGen.Layer("stone")), "Wyłączenie paczek przywraca tekstury gry");
        }
        finally
        {
            ResourcePacks.SetActive(Array.Empty<string>(), dir);
            TextureGen.ApplyPacks((Func<string, byte[]>)null);
            try { Directory.Delete(dir, true); } catch { }
        }

        // oryginalny klient 1.8 (gdy pobrany): wszystkie przypisania istnieją
        string jar = Path.Combine(ClientAssets.Root, "client.jar");
        if (!File.Exists(jar)) { Console.WriteLine("    (brak client.jar - pomijam test przypisań do 1.8)"); return; }
        using (var zip = ZipFile.OpenRead(jar))
        {
            var missing = rules.Alias.Values.Distinct().Where(p => zip.GetEntry(TextureGen.TexturesRoot + p + ".png") == null).ToList();
            Check(missing.Count == 0, "Przypisania wskazują pliki istniejące w 1.8", string.Join(", ", missing.Take(10)));
        }
        var pack = new ResourcePack(ResourcePacks.BuiltinId, jar, true);
        try
        {
            TextureGen.ApplyPacks(pack.Read);
            int n = TextureGen.PackOverridden.Count;
            Check(n > 560, "Oryginalne tekstury 1.8: pokrycie", n + " z " + TextureGen.Count);
            Check(TextureGen.PackAnimations.ContainsKey("water_still") && TextureGen.PackAnimations.ContainsKey("lava_still") && TextureGen.PackAnimations.ContainsKey("portal") && TextureGen.PackAnimations.ContainsKey("fire"),
                "Oryginalne animacje: woda, lawa, portal, ogień", string.Join(",", TextureGen.PackAnimations.Keys));
        }
        finally { pack.Dispose(); TextureGen.ApplyPacks((Func<string, byte[]>)null); }
    }
}
