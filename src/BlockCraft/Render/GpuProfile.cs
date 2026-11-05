namespace BlockCraft.Render;

/// <summary>Rozpoznanie słabej karty po GL_RENDERER i ustawienia "laptop" (zintegrowana grafika, sterownik programowy).</summary>
public static class GpuProfile
{
    public enum Kind { Dedicated, Integrated, Software }

    public static Kind Detect(string renderer)
    {
        var r = (renderer ?? "").ToLowerInvariant();
        if (r.Contains("llvmpipe") || r.Contains("softpipe") || r.Contains("swiftshader") || r.Contains("gdi generic") || r.Contains("basic render"))
            return Kind.Software;
        if (r.Contains("intel"))
        {
            // Arc A-series to osobne karty; "Arc(TM) Graphics" bez numeru to zintegrowana (Meteor/Lunar Lake)
            bool arcDedicated = r.Contains("arc") && System.Text.RegularExpressions.Regex.IsMatch(r, @"\b[ab]\d{3}");
            return arcDedicated ? Kind.Dedicated : Kind.Integrated;
        }
        if (r.Contains("radeon") || r.Contains("amd"))
        {
            // APU: "AMD Radeon(TM) Graphics", "Radeon Vega 8 Graphics", "Radeon 780M"
            if (r.Contains("vega") && !r.Contains("vega 56") && !r.Contains("vega 64")) return Kind.Integrated;
            if (System.Text.RegularExpressions.Regex.IsMatch(r, @"radeon(\(tm\))? graphics")) return Kind.Integrated;
            if (System.Text.RegularExpressions.Regex.IsMatch(r, @"\b\d{3}m\b")) return Kind.Integrated;
            return Kind.Dedicated;
        }
        if (r.Contains("mali") || r.Contains("adreno") || r.Contains("powervr") || r.Contains("apple m")) return Kind.Integrated;
        return Kind.Dedicated;
    }

    /// <summary>Ustawienia dla słabego GPU. Mechanika gry się nie zmienia, tylko obraz.</summary>
    public static void ApplyLaptop(Settings s, Kind k)
    {
        s.FancyGraphics = false;                      // szybkie liście (jak "Fast" w MC)
        s.Clouds = false;
        s.FarEntities = false;
        s.RenderDistance = k == Kind.Software ? 4 : 6;
        s.RenderScale = k == Kind.Software ? 50 : 75;
        s.VSync = true;
    }
}
