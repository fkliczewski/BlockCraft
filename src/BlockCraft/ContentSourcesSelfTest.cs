using System.Diagnostics;
using BlockCraft.Core;

namespace BlockCraft;

/// <summary>Źródła paczek: foldery lokalne, nazwy i prefiksy, repozytorium git (lokalne file://, bez sieci).</summary>
public static partial class SelfTest
{
    private static int Git(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false" }.Concat(args)) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode;
    }

    private static void WritePackFolder(string dir, string desc)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pack.mcmeta"), "{\"pack\":{\"pack_format\":1,\"description\":\"" + desc + "\"}}");
    }

    private static void TestContentSources()
    {
        Console.WriteLine("[Źródła paczek]");
        string root = Path.Combine(Path.GetTempPath(), "blockcraft-sources-" + Environment.ProcessId);
        try
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            // źródło z podfolderem resourcepacks/, źródło będące paczką, dwa foldery o tej samej nazwie
            string a = Path.Combine(root, "moje-paczki"), b = Path.Combine(root, "JednaPaczka"), c = Path.Combine(root, "inne", "moje-paczki");
            WritePackFolder(Path.Combine(a, "resourcepacks", "kamien"), "A");
            WritePackFolder(b, "B");
            WritePackFolder(Path.Combine(c, "trawa"), "C");
            ContentSources.Configure(new[] { a, b, c, a, " ", "https://github.com/b-y-t-e/blockcraft-plugins", "https://example.com/x/repo.git#dev" }, Path.Combine(root, "cache"), watch: false);
            var all = ContentSources.All;
            Check(all.Count == 5, "Źródła: powtórzone i puste pominięte", all.Count.ToString());
            Check(all[0].Name == "moje-paczki" && all[2].Name == "moje-paczki-2" && all[1].Name == "jednapaczka", "Nazwy źródeł bez kolizji", string.Join(",", all.Select(s => s.Name)));
            Check(all[3].Remote && all[3].Name == "blockcraft-plugins" && all[3].Branch == null, "Repo GitHub rozpoznane");
            Check(all[4].Remote && all[4].Name == "repo" && all[4].Branch == "dev" && all[4].Url == "https://example.com/x/repo.git", "Gałąź po #");
            var ids = ResourcePacks.Scan().Select(p => p.Id).ToList();
            Check(ids.Contains("moje-paczki:kamien") && ids.Contains("jednapaczka:jednapaczka") && ids.Contains("moje-paczki-2:trawa"), "Paczki ze źródeł z prefiksem nazwy", string.Join(",", ids));
            var kamien = ResourcePacks.Scan().First(p => p.Id == "moje-paczki:kamien");
            Check(kamien.Source == "moje-paczki" && kamien.Name == "kamien" && kamien.Description == "A", "Paczka zna swoje źródło");
            kamien.Dispose();

            if (!ContentSources.GitAvailable) { Console.WriteLine("    (brak programu git - pomijam test repozytorium)"); return; }
            string origin = Path.Combine(root, "origin");
            WritePackFolder(Path.Combine(origin, "resourcepacks", "zgit"), "wersja 1");
            Git(origin, "init", "-q", "-b", "main"); Git(origin, "add", "-A"); Git(origin, "commit", "-q", "-m", "v1");
            string url = "file:///" + origin.Replace(Path.DirectorySeparatorChar, '/');
            ContentSources.Configure(new[] { url }, Path.Combine(root, "cache"), watch: false);
            var src = ContentSources.All[0];
            Check(ContentSources.Sync(src) && File.Exists(Path.Combine(src.Dir, "resourcepacks", "zgit", "pack.mcmeta")), "Git: pierwsze pobranie", src.Status);
            Check(!ContentSources.Sync(src) && src.Status == "aktualne", "Git: bez zmian nic nie pobiera", src.Status);
            WritePackFolder(Path.Combine(origin, "resourcepacks", "zgit"), "wersja 2");
            Git(origin, "commit", "-q", "-am", "v2");
            bool upd = ContentSources.Sync(src);
            Check(upd && File.ReadAllText(Path.Combine(src.Dir, "resourcepacks", "zgit", "pack.mcmeta")).Contains("wersja 2"), "Git: zmiany zaciągnięte", src.Status);
            Check(ResourcePacks.Scan().Any(p => p.Id == src.Name + ":zgit"), "Paczka z repozytorium na liście");
            foreach (var f in Directory.EnumerateFiles(origin, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(origin, true);
            Check(!ContentSources.Sync(src) && src.Status.Contains("ostatnia pobrana wersja") && Directory.Exists(Path.Combine(src.Dir, "resourcepacks", "zgit")), "Git: bez dostępu zostaje ostatnia kopia", src.Status);
        }
        finally
        {
            ContentSources.Configure(Array.Empty<string>(), null, watch: false);
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
            catch { }
        }
    }
}
