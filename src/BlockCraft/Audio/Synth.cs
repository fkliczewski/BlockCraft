namespace BlockCraft.Audio;

/// <summary>Zastępcze, proceduralne dźwięki (gdy brak assetów Mojang). Mono, 44.1 kHz.</summary>
public static class Synth
{
    const int Rate = 44100;

    public static PcmData Make(string ev)
    {
        int h = 17; foreach (char c in ev) h = h * 31 + c;
        var rnd = new Random(h);
        string e = ev.ToLowerInvariant();
        float dur; float freq; float noise; float decay;
        if (e.StartsWith("dig.") || e.StartsWith("step.")) { dur = e.StartsWith("step.") ? 0.09f : 0.16f; freq = e.Contains("stone") || e.Contains("gravel") ? 140 : e.Contains("wood") ? 200 : 90; noise = 0.85f; decay = 22; }
        else if (e.StartsWith("random.explode") || e.Contains("explode")) { dur = 0.9f; freq = 50; noise = 0.9f; decay = 4; }
        else if (e.Contains("click") || e.Contains("button") || e.Contains("pop")) { dur = 0.05f; freq = 1400; noise = 0.1f; decay = 60; }
        else if (e.StartsWith("mob.") || e.StartsWith("damage.") || e.Contains("hurt")) { dur = 0.3f; freq = 120 + (h & 127) * 2; noise = 0.3f; decay = 7; }
        else if (e.StartsWith("liquid.") || e.StartsWith("game.neutral.swim") || e.Contains("splash")) { dur = 0.3f; freq = 300; noise = 0.95f; decay = 9; }
        else if (e.Contains("levelup") || e.Contains("orb")) { dur = 0.25f; freq = 900 + (h & 255); noise = 0f; decay = 8; }
        else { dur = 0.15f; freq = 300 + (h & 511); noise = 0.4f; decay = 14; }
        int n = (int)(dur * Rate);
        var d = new float[n];
        float lp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = MathF.Exp(-decay * t);
            float nz = (float)(rnd.NextDouble() * 2 - 1);
            lp += (nz - lp) * 0.35f;
            float tone = MathF.Sin(2 * MathF.PI * freq * t);
            d[i] = (lp * noise + tone * (1 - noise)) * env * 0.5f;
        }
        return new PcmData { Data = d, Channels = 1, Rate = Rate };
    }
}
