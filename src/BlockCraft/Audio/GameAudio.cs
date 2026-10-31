using BlockCraft.Audio;
using BlockCraft.Core;

namespace BlockCraft;

public sealed partial class Game
{
    private int _caveDelay = 400;
    private float _rainVolume;
    private void WeatherAudioTick()
    {
        var w = World; var p = Player;
        if (!_sessionStarted || w == null || p == null || p.Dead)
        { _rainVolume = 0; Sound.SetRain(0); return; }
        float target = 0;
        if (w.IsRaining)
        {
            int cx = MathUtil.FloorToInt(p.X), cz = MathUtil.FloorToInt(p.Z);
            // Deszcz z otoczenia słychać także pod dachem, ale nie głęboko w jaskini.
            for (int dx = -6; dx <= 6; dx += 2)
                for (int dz = -6; dz <= 6; dz += 2)
                {
                    int x = cx + dx, z = cz + dz, y = w.GetHeight(x, z);
                    if (Math.Abs(y - (p.Y + p.EyeHeight)) > 8 || !w.IsRainingAt(x, y, z)) continue;
                    float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                    target = Math.Max(target, w.RainStrength * .2f * (1 - distance / 12));
                }
            if (!w.CanSeeSky(cx, MathUtil.FloorToInt(p.Y + p.EyeHeight), cz)) target *= .5f;
        }
        _rainVolume += (target - _rainVolume) * .15f;
        Sound.SetRain(_rainVolume);
    }
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
        WeatherAudioTick();
        Music.Tick();
    }
}
