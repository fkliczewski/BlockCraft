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

    /// <summary>--pack-source ŚCIEŻKA|URL (można powtórzyć): źródła tylko na tę sesję.</summary>
    private List<string> _sourceOverride;

    private void ParsePackArgs(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "--resourcepack") (_packOverride ??= new()).Add(args[i + 1]);
            if (args[i] == "--pack-source") (_sourceOverride ??= new()).Add(args[i + 1]);
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
        if (ContentSources.ConsumeChange() && ActivePackIds.Count > 0) _reloadPacks = true;
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
        TextureGen.ApplyPacks(ResourcePacks.Readers);
        if (ResourcePacks.Active.Count > 0) SkinGen.Rebuild(ResourcePacks.ReadImage);
        Sb.ReloadFont();
        // archiwa zamyka OnLoad po utworzeniu GUI i renderera (też czytają z paczek)
    }

    /// <summary>W trakcie gry (na początku klatki): atlas terenu, ikony bloków i tło menu.</summary>
    private void ReloadResourcePacksNow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ResourcePacks.SetActive(ActivePackIds);
        // warstwy animowane (przez poprzednie paczki albo przez grę) mogły zostać na innej klatce niż w atlasie
        var animated = TextureGen.PackAnimations.Keys.Concat(TextureGen.Animations.Keys).Select(TextureGen.Layer).ToList();
        var changed = TextureGen.ApplyPacks(ResourcePacks.Readers);
        Renderer.ReloadTextures(changed.Concat(animated).Distinct());
        Func<string, RawImage> packImages = ResourcePacks.Active.Count > 0 ? ResourcePacks.ReadImage : null;
        SkinGen.Rebuild(packImages);
        Renderer.Entities.ReloadSkins(packImages);
        Icons.Dispose(); Icons = new IconAtlas(Renderer.Entities, Renderer.Terrain);
        Gui.Dispose(); Gui = new GuiTextures(ResourcePacks.ReadImage);
        Sb.ReloadFont();
        // inne mapy kolorów trawy i liści: siatki chunków do przebudowy
        Renderer.ReloadPackAssets();
        if (Biome.LoadColorMaps(ResourcePacks.ReadImage)) MarkAllDirty();
        ResourcePacks.CloseAll();
        Log.Info($"Paczki zasobów przeładowane: {changed.Count} zmienionych tekstur, {sw.ElapsedMilliseconds} ms");
    }
}
