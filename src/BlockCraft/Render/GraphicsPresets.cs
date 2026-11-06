namespace BlockCraft;

/// <summary>Presety efektów graficznych. Klasyczna = dotychczasowy render bez efektów (słabe komputery).</summary>
public enum GraphicsPreset { Classic, Nice, Cacy, Ultra, Custom }

public static class GraphicsPresets
{
    public static string Name(GraphicsPreset p) => p switch
    {
        GraphicsPreset.Classic => "Klasyczna",
        GraphicsPreset.Nice => "Ładna",
        GraphicsPreset.Cacy => "Cacy",
        GraphicsPreset.Ultra => "Ultra",
        _ => "Własna",
    };

    /// <summary>Ustawia efekty presetu (Własna nic nie zmienia). Kolejne etapy planu dopisują tu swoje efekty.</summary>
    public static void Apply(Settings s, GraphicsPreset p)
    {
        s.GraphicsPreset = p;
        if (p == GraphicsPreset.Custom) return;
        bool on = p != GraphicsPreset.Classic;
        s.Tonemapping = on;
        s.VividColors = on;
        s.Fxaa = on;
        s.WavingPlants = on;
    }

    /// <summary>Preset zgodny z bieżącymi efektami albo Własna (po ręcznej zmianie pojedynczego efektu).</summary>
    public static GraphicsPreset Match(Settings s)
    {
        // bieżący preset ma pierwszeństwo (presety mogą mieć te same efekty, dopóki kolejne etapy ich nie rozróżnią)
        if (s.GraphicsPreset != GraphicsPreset.Custom && Same(s, s.GraphicsPreset)) return s.GraphicsPreset;
        foreach (var p in new[] { GraphicsPreset.Classic, GraphicsPreset.Nice, GraphicsPreset.Cacy, GraphicsPreset.Ultra })
            if (Same(s, p)) return p;
        return GraphicsPreset.Custom;
    }

    private static bool Same(Settings s, GraphicsPreset p)
    {
        var t = new Settings();
        Apply(t, p);
        return t.Tonemapping == s.Tonemapping && t.VividColors == s.VividColors && t.Fxaa == s.Fxaa && t.WavingPlants == s.WavingPlants;
    }

    /// <summary>Czy świat idzie przez bufor HDR i przebieg końcowy (włączony któryś efekt ekranowy; falowanie działa bez niego).</summary>
    public static bool PostFxOn(Settings s) => s.Tonemapping || s.VividColors || s.Fxaa;
}
