using BlockCraft.Audio;

namespace BlockCraft;

public sealed partial class Game
{
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
        Music.Tick();
    }
}
