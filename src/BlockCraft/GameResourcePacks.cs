using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.Render;

namespace BlockCraft;

/// <summary>Paczki zasobów 1.8: wybór w ustawieniach (albo --resourcepack na czas sesji) i przeładowanie tekstur bez restartu.</summary>
public sealed partial class Game
{
    /// <summary>--resourcepack NAZWA (można powtórzyć): paczki tylko na tę sesję, bez zapisu w ustawieniach.</summary>
    private List<string> _packOverride;
    private bool _reloadPacks;

    private void ParsePackArgs(string[] args)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "--resourcepack") (_packOverride ??= new()).Add(args[i + 1]);
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
        ResourcePacks.CloseAll();
    }

    /// <summary>W trakcie gry (na początku klatki): atlas terenu, ikony bloków i tło menu.</summary>
    private void ReloadResourcePacksNow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        ResourcePacks.SetActive(ActivePackIds);
        // warstwy animowane (przez poprzednie paczki albo przez grę) mogły zostać na innej klatce niż w atlasie
        var animated = TextureGen.PackAnimations.Keys.Concat(TextureGen.Animations.Keys).Select(TextureGen.Layer).ToList();
        var changed = TextureGen.ApplyPacks(ResourcePacks.Readers);
        ResourcePacks.CloseAll();
        Renderer.ReloadTextures(changed.Concat(animated).Distinct());
        Icons.Dispose(); Icons = new IconAtlas(Renderer.Entities, Renderer.Terrain);
        Gui.Dispose(); Gui = new GuiTextures();
        Log.Info($"Paczki zasobów przeładowane: {changed.Count} zmienionych tekstur, {sw.ElapsedMilliseconds} ms");
    }
}
