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
    public bool ViewBobbing { get; set; } = true;
    public float Brightness { get; set; } = 0.5f;
    public bool VSync { get; set; } = true;
    public int MaxFps { get; set; } = 120;
    public int Difficulty { get; set; } = 2;
    public string PlayerName { get; set; } = "Steve";
    public bool ShowFps { get; set; }

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
