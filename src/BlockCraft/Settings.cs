using System.Text.Json;
using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft;

public sealed class Settings
{
    public float Fov { get; set; } = 70;
    public int RenderDistance { get; set; } = 8;
    public float Sensitivity { get; set; } = 0.5f;
    public bool InvertMouse { get; set; }
    public int GuiScale { get; set; } = 0; // 0 = auto
    public bool SmoothLighting { get; set; } = true;
    public bool FancyGraphics { get; set; } = true;
    public bool Clouds { get; set; } = true;
    /// <summary>Encje widoczne aż do granicy mgły (wyłaniają się z niej) zamiast znikać wg rozmiaru jak w MC.</summary>
    public bool FarEntities { get; set; } = true;
    public bool ViewBobbing { get; set; } = true;
    public float Brightness { get; set; } = 0.5f;
    public bool VSync { get; set; } = true;
    public int MaxFps { get; set; } = 120;
    public int Difficulty { get; set; } = 2;
    public string PlayerName { get; set; } = "Steve";
    public bool ShowFps { get; set; }
    /// <summary>Skala renderu świata 3D w procentach (25-100); HUD zawsze w pełnej rozdzielczości.</summary>
    public int RenderScale { get; set; } = 100;
    /// <summary>GL_RENDERER, dla którego sprawdzono profil grafiki (przy zmianie karty sprawdzamy jeszcze raz).</summary>
    public string GpuProfileFor { get; set; } = "";
    /// <summary>Głośności kategorii dźwięku (master, music, record, weather, block, hostile, neutral, player, ambient), 0..1.</summary>
    public Dictionary<string, float> SoundVolumes { get; set; } = new();
    /// <summary>Aktywne paczki zasobów (nazwy w resourcepacks/), pierwsza ma pierwszeństwo.</summary>
    public List<string> ResourcePacks { get; set; } = new();
    /// <summary>Dodatkowe źródła paczek: foldery i repozytoria git (URL, opcjonalnie "#gałąź").</summary>
    public List<string> PackSources { get; set; } = new();
    /// <summary>Język nazw przedmiotów z plików .lang MC 1.8 (np. "en_US"); pusty = polskie nazwy gry.</summary>
    public string Language { get; set; } = "";
    /// <summary>Największa rozdzielczość tekstur bloków z paczek (16, 32, 64, 128); większe paczki są zmniejszane.</summary>
    public int MaxPackTextureSize { get; set; } = 64;
    /// <summary>Własny przekaźnik sieci (np. gry.fimson.pl); puste = publiczne przekaźniki Tailscale.</summary>
    public string Relay { get; set; } = "";
    /// <summary>Pad (kontroler): włączony, czułość kamery 0..1, martwa strefa gałek 0..0,5, odwrócona oś Y, wibracje.</summary>
    public bool PadEnabled { get; set; } = true;
    public float PadLookSensitivity { get; set; } = 0.5f;
    public float PadDeadzone { get; set; } = 0.15f;
    public bool PadInvertY { get; set; }
    public bool PadVibration { get; set; } = true;
    /// <summary>Przypisania akcji do przycisków pada różne od domyślnych (np. "Jump": "B").</summary>
    public Dictionary<string, string> PadBindings { get; set; } = new();
    /// <summary>Ikony przycisków: wg pada (Sony = PlayStation), Xbox albo PlayStation.</summary>
    public PadIconStyle PadIconStyle { get; set; }

    private static string PathFile => System.IO.Path.Combine(WorldStorage.Root, "options.json");

    public static Settings Load()
    {
        try { if (File.Exists(PathFile)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathFile)) ?? new Settings(); }
        catch (Exception ex) { Log.Error("options: " + ex.Message); }
        return new Settings();
    }
    public void Save()
    {
        try { File.WriteAllText(PathFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Log.Error("options save: " + ex.Message); }
    }
}
