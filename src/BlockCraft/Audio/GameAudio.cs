using BlockCraft.Audio;
using BlockCraft.Core;

namespace BlockCraft;

public sealed partial class Game
{
    private int _caveDelay = 400;
    /// <summary>Ambient jaskiń jak w MC: w ciemności (brak światła nieba i blokowego powyżej 3) losowy ambient.cave.cave.</summary>
    private void AmbientTick()
    {
        var p = Player; var w = World;
        if (p == null || w == null || !_sessionStarted || p.Dead) return;
        if (--_caveDelay > 0) return;
        _caveDelay = 100 + w.Rand.Next(500);
        int x = MathUtil.FloorToInt(p.X), y = MathUtil.FloorToInt(p.Y + p.EyeHeight), z = MathUtil.FloorToInt(p.Z);
        if (w.GetSkyLight(x, y, z) > 0 || w.GetBlockLight(x, y, z) > 3) return;
        w.PlaySound("ambient.cave.cave", p.X, p.Y, p.Z, 0.7f, 0.8f + w.Rand.NextFloat() * 0.2f);
    }
    /// <summary>Co tick: pozycja słuchacza i kontekst muzyczny (moduły Nether/End mogą nadpisać przez Music.SetContext po tej metodzie).</summary>
    private void AudioTick()
    {
        var p = Player;
        if (World != null && p != null && _sessionStarted)
        {
            Sound.SetListener(p.X, p.Y + p.EyeHeight, p.Z, p.Yaw);
            if (Music.Context != MusicType.Nether && Music.Context != MusicType.End && Music.Context != MusicType.EndBoss && Music.Context != MusicType.Credits || Music.Context == MusicType.Menu)
                Music.SetContext(p.Creative ? MusicType.Creative : MusicType.Game);
        }
        else if (World == null) { Sound.SetListener(0, 0, 0, 0); Music.SetContext(MusicType.Menu); }
        AmbientTick();
        Music.Tick();
    }
}
