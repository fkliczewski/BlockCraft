namespace BlockCraft;

/// <summary>Profile grafiki: każdy rozpoznaje się po zastosowaniu, ręczna zmiana daje Własną, osobiste opcje zostają.</summary>
public static partial class SelfTest
{
    private static void TestGraphicsQuality()
    {
        Console.WriteLine("[Profile grafiki]");
        foreach (var p in GraphicsQuality.Choices)
        {
            var s = new Settings { Brightness = 0.3f, Fov = 90, MaxFps = 60, VSync = false };
            GraphicsQuality.Apply(s, p);
            Check(GraphicsQuality.Match(s) == p, "Profil " + GraphicsQuality.Name(p) + " rozpoznany po zastosowaniu", GraphicsQuality.Name(GraphicsQuality.Match(s)));
            Check(s.Brightness == 0.3f && s.Fov == 90 && s.MaxFps == 60 && !s.VSync, "Profil " + GraphicsQuality.Name(p) + " nie zmienia jasności, FOV, FPS i V-sync");
        }
        var c = new Settings();
        GraphicsQuality.Apply(c, QualityProfile.Ultra);
        c.Clouds = false;
        Check(GraphicsQuality.Match(c) == QualityProfile.Custom, "Ręczna zmiana opcji = Własna");
        GraphicsQuality.Apply(c, GraphicsQuality.Next(QualityProfile.Custom));
        Check(GraphicsQuality.Match(c) == QualityProfile.Classic, "Z Własnej następny jest Klasyczna");
        Check(GraphicsQuality.Next(QualityProfile.Ultra) == QualityProfile.Light, "Po Ultra wraca Lekka");
        var light = new Settings(); GraphicsQuality.Apply(light, QualityProfile.Light);
        var ultra = new Settings(); GraphicsQuality.Apply(ultra, QualityProfile.Ultra);
        Check(light.RenderDistance < ultra.RenderDistance && light.RenderScale < ultra.RenderScale && !light.Clouds, "Lekka lżejsza niż Ultra");
    }
}
