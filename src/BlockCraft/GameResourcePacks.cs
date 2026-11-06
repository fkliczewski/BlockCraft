using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Paczki zasobów 1.8: wybór w ustawieniach (albo --resourcepack na czas sesji) i przeładowanie tekstur bez restartu.</summary>
public sealed partial class Game
{
    /// <summary>--resourcepack NAZWA (można powtórzyć): paczki tylko na tę sesję, bez zapisu w ustawieniach.</summary>
    private List<string> _packOverride;
    private bool _reloadPacks;
    private volatile bool _reloadDownloadedLanguages;
    private bool _clientLanguagesReady;

    /// <summary>--language KOD: język nazw przedmiotów tylko na tę sesję.</summary>
    private string _languageOverride;
    private string ActiveLanguage => _languageOverride ?? Settings.Language;

    /// <summary>--pack-source ŚCIEŻKA|URL (można powtórzyć): źródła tylko na tę sesję.</summary>
    private List<string> _sourceOverride;

    private void ParsePackArgs(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--resourcepack") (_packOverride ??= new()).Add(args[i + 1]);
            if (args[i] == "--pack-source") (_sourceOverride ??= new()).Add(args[i + 1]);
            if (args[i] == "--language") _languageOverride = args[i + 1];
        }
        ContentSources.Configure(ActiveSourceSpecs);
    }

    /// <summary>Źródła paczek: z parametru uruchomienia albo z ustawień.</summary>
    public List<string> ActiveSourceSpecs => _sourceOverride ?? Settings.PackSources ?? new();
    public bool SourcesFromArgs => _sourceOverride != null;

    /// <summary>Zmienia listę źródeł (zapis w ustawieniach, chyba że podano --pack-source) i pobiera repozytoria.</summary>
    public void SetPackSources(List<string> specs)
    {
        if (_sourceOverride != null) _sourceOverride = specs;
        else { Settings.PackSources = specs; Settings.Save(); }
        ContentSources.Configure(specs);
        ContentSources.MarkChanged();
        SyncSourcesInBackground();
    }

    private Task _sourceSync;
    /// <summary>Pobiera repozytoria w tle; gdy coś się zmieniło, paczki przeładują się same (ConsumeChange w klatce).</summary>
    public void SyncSourcesInBackground()
    {
        if (_sourceSync is { IsCompleted: false } || !ContentSources.All.Any(s => s.Remote)) return;
        _sourceSync = Task.Run(async () =>
        {
            try { await ContentSources.SyncAllAsync(); }
            catch (Exception ex) { Log.Error("Źródła paczek: " + ex.Message); }
        });
    }
    public bool SourcesSyncing => _sourceSync is { IsCompleted: false };

    /// <summary>Co klatkę: zmiana w obserwowanym folderze albo pobrane repo -> przeładowanie aktywnych paczek.</summary>
    private void CheckSourceChanges()
    {
        bool clientAssetsFinished = !_clientLanguagesReady && ClientAssets.Done;
        if (_reloadDownloadedLanguages || clientAssetsFinished)
        {
            _reloadDownloadedLanguages = false;
            _clientLanguagesReady = ClientAssets.Done;
            Sound.ReloadBank();
            Lang.Load(ActiveLanguage, ResourcePacks.Readers);
            ResourcePacks.CloseAll();
        }
        if (clientAssetsFinished && ActivePackIds.Count > 0) _reloadPacks = true;
        if (ContentSources.ConsumeChange() && ActivePackIds.Count > 0) _reloadPacks = true;
    }

    /// <summary>Zmienia język nazw przedmiotów ("" = nazwy gry) i zapisuje w ustawieniach.</summary>
    public void SetLanguage(string code)
    {
        if (_languageOverride != null) _languageOverride = code ?? "";
        else { Settings.Language = code ?? ""; Settings.Save(); }
        Lang.Load(ActiveLanguage, ResourcePacks.Readers);
        ResourcePacks.CloseAll();
    }

    /// <summary>Po zmianie ustawień wpływających na tekstury (np. limit rozdzielczości paczek): przeładowanie przed następną klatką.</summary>
    public void RequestPackReload() => _reloadPacks = true;

    /// <summary>Modele bloków z paczek (zbierają potrzebne tekstury), potem atlas z tymi teksturami i przypisanie warstw.</summary>
    private static List<int> ApplyPackTexturesAndModels()
    {
        var sources = ResourcePacks.Active.Select(p => new PackModels.Source(p.Read, p.Builtin)).ToList();
        // jak paczka domyślna w MC: modele 1.8 z client.jar zawsze pod spodem (rodzice typu block/cube_all)
        string jar = Path.Combine(ClientAssets.Root, "client.jar");
        ResourcePack vanilla = null;
        if (sources.Count > 0 && !ResourcePacks.Active.Any(p => p.Builtin) && File.Exists(jar))
        {
            vanilla = new ResourcePack(ResourcePacks.BuiltinId, jar, true);
            sources.Add(new PackModels.Source(vanilla.Read, true));
        }
        try
        {
            var models = PackModels.Load(sources);
            var modelReaders = sources.Select(source => source.Read).ToList();
            var changed = TextureGen.ApplyPacks(ResourcePacks.Readers, models.TexturePaths, modelReaders);
            PackModels.Activate(models, path => Math.Max(0, TextureGen.LayerForPackPath(path)));
            return changed;
        }
        finally { vanilla?.Dispose(); }
    }

    /// <summary>Paczki do użycia: z parametru uruchomienia albo z ustawień.</summary>
    public List<string> ActivePackIds => _packOverride ?? Settings.ResourcePacks ?? new();

    /// <summary>Zmienia listę aktywnych paczek; tekstury przeładują się przed następną klatką.</summary>
    public void SetResourcePacks(List<string> ids)
    {
        if (_packOverride != null) _packOverride = ids;
        else { Settings.ResourcePacks = ids; Settings.Save(); }
        _reloadPacks = true;
    }

    /// <summary>Przy starcie: przed utworzeniem atlasu, więc wystarczy podmienić piksele.</summary>
    private void LoadResourcePacksAtStart()
    {
        ResourcePacks.SetActive(ActivePackIds);
        TextureGen.MaxAtlasSize = Settings.MaxPackTextureSize;
        ApplyPackTexturesAndModels();
        if (ResourcePacks.Active.Count > 0) SkinGen.Rebuild(ResourcePacks.ReadImage);
        Lang.Load(ActiveLanguage, ResourcePacks.Readers);
        Sb.ReloadFont();
        // archiwa zamyka OnLoad po utworzeniu GUI i renderera (też czytają z paczek)
    }

    /// <summary>W trakcie gry (na początku klatki): atlas terenu, ikony bloków i tło menu.</summary>
    private void ReloadResourcePacksNow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ResourcePacks.SetActive(ActivePackIds);
        TextureGen.MaxAtlasSize = Settings.MaxPackTextureSize;
        bool hadModels = PackModels.Any;
        var changed = ApplyPackTexturesAndModels();
        if (hadModels || PackModels.Any) MarkAllDirty(); // geometria bloków z modeli paczek
        Renderer.ReloadTextures();
        Func<string, RawImage> packImages = ResourcePacks.Active.Count > 0 ? ResourcePacks.ReadImage : null;
        SkinGen.Rebuild(packImages);
        Renderer.Entities.ReloadSkins(packImages);
        Icons.Dispose(); Icons = new IconAtlas(Renderer.Entities, Renderer.Terrain);
        Gui.Dispose(); Gui = new GuiTextures(ResourcePacks.ReadImage);
        Sb.ReloadFont();
        // inne mapy kolorów trawy i liści: siatki chunków do przebudowy
        Renderer.ReloadPackAssets();
        if (Biome.LoadColorMaps(ResourcePacks.ReadImage)) MarkAllDirty();
        Sound.ReloadBank();
        Lang.Reload(ResourcePacks.Readers);
        ResourcePacks.CloseAll();
        Log.Info($"Paczki zasobów przeładowane: {changed.Count} zmienionych tekstur, {sw.ElapsedMilliseconds} ms");
    }
}
