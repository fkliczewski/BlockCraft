namespace BlockCraft;

/// <summary>Profil grafiki: jeden wybór ustawia jakość świata, zasięg, rozdzielczość i efekty (presety z GraphicsPresets).
/// Profil nie jest zapisywany osobno — wynika z ustawień; po ręcznej zmianie pojedynczej opcji jest „Własna”.</summary>
public enum QualityProfile { Light, Classic, Nice, Cacy, Ultra, Custom }

public static class GraphicsQuality
{
    public static readonly QualityProfile[] Choices = { QualityProfile.Light, QualityProfile.Classic, QualityProfile.Nice, QualityProfile.Cacy, QualityProfile.Ultra };

    public static string Name(QualityProfile p) => p switch
    {
        QualityProfile.Light => "Lekka",
        QualityProfile.Classic => "Klasyczna",
        QualityProfile.Nice => "Ładna",
        QualityProfile.Cacy => "Cacy",
        QualityProfile.Ultra => "Ultra",
        _ => "Własna",
    };

    public static string Description(QualityProfile p) => p switch
    {
        QualityProfile.Light => "Dla laptopów i słabych kart: mniejszy zasięg i rozdzielczość świata, bez chmur.",
        QualityProfile.Classic => "Wygląd jak w Minecrafcie 1.8, bez dodatkowych efektów.",
        QualityProfile.Nice => "Żywsze kolory, wygładzone krawędzie i falujące rośliny; zasięg 10 chunków.",
        QualityProfile.Cacy => "Jak Ładna, z zasięgiem 12 chunków.",
        QualityProfile.Ultra => "Wszystkie efekty, zasięg 16 chunków i tekstury paczek do 128x; dla mocnych kart.",
        _ => "Zmienione pojedyncze opcje; wszystkie są w „Własne ustawienia...”.",
    };

    /// <summary>Ustawia wszystko, co należy do profilu (Własna nic nie zmienia). Jasność, pole widzenia, FPS i V-sync zostają.</summary>
    public static void Apply(Settings s, QualityProfile p)
    {
        if (p == QualityProfile.Custom) return;
        if (p == QualityProfile.Light)
        {
            bool vsync = s.VSync;
            Render.GpuProfile.ApplyLaptop(s, Render.GpuProfile.Kind.Integrated);
            s.VSync = vsync;
            return;
        }
        s.SmoothLighting = true;
        s.FancyGraphics = true;
        s.Clouds = true;
        s.FarEntities = true;
        s.RenderScale = 100;
        (s.RenderDistance, s.MaxPackTextureSize, var fx) = p switch
        {
            QualityProfile.Classic => (8, 64, GraphicsPreset.Classic),
            QualityProfile.Nice => (10, 64, GraphicsPreset.Nice),
            QualityProfile.Cacy => (12, 64, GraphicsPreset.Cacy),
            _ => (16, 128, GraphicsPreset.Ultra),
        };
        GraphicsPresets.Apply(s, fx);
    }

    /// <summary>Profil zgodny z bieżącymi ustawieniami albo Własna.</summary>
    public static QualityProfile Match(Settings s)
    {
        foreach (var p in Choices)
        {
            var t = new Settings { GraphicsPreset = s.GraphicsPreset };
            Apply(t, p);
            if (t.SmoothLighting == s.SmoothLighting && t.FancyGraphics == s.FancyGraphics && t.Clouds == s.Clouds && t.FarEntities == s.FarEntities && t.RenderScale == s.RenderScale
                && t.RenderDistance == s.RenderDistance && t.MaxPackTextureSize == s.MaxPackTextureSize
                && SameEffects(t, s))
                return p;
        }
        return QualityProfile.Custom;
    }

    private static bool SameEffects(Settings a, Settings b) =>
        a.Tonemapping == b.Tonemapping && a.VividColors == b.VividColors && a.Fxaa == b.Fxaa && a.WavingPlants == b.WavingPlants;

    /// <summary>Następny profil z listy (z Własnej: Klasyczna).</summary>
    public static QualityProfile Next(QualityProfile p) =>
        p == QualityProfile.Custom ? QualityProfile.Classic : Choices[(Array.IndexOf(Choices, p) + 1) % Choices.Length];
}
