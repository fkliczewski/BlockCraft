namespace BlockCraft.Audio;

public enum MusicType { Menu, Game, Creative, Nether, End, EndBoss, Credits }

/// <summary>Odpowiednik MusicTicker z MC 1.8: losowe opóźnienia zależne od kontekstu, jeden utwór naraz, wyciszanie przy zmianie.</summary>
public static class Music
{
    static MusicType _type = MusicType.Menu;
    static Voice _cur;
    static MusicType _curType;
    static int _timeUntilNext = 100;
    static readonly Random Rnd = new();

    public static MusicType Context => _type;
    public static bool Playing => _cur != null && !_cur.Done;

    public static void Init() { _timeUntilNext = 100; }

    /// <summary>Ustawia kontekst muzyczny (wołane co tick przez grę; tanie).</summary>
    public static void SetContext(MusicType t) { _type = t; }

    static string EventOf(MusicType t) => t switch
    {
        MusicType.Menu => "music.menu", MusicType.Creative => "music.game.creative", MusicType.Nether => "music.game.nether",
        MusicType.End => "music.game.end", MusicType.EndBoss => "music.game.end.dragon", MusicType.Credits => "music.game.end.credits",
        _ => "music.game"
    };
    static (int min, int max) Delay(MusicType t) => t switch
    {
        MusicType.Menu => (20, 600), MusicType.Game => (12000, 24000), MusicType.Creative => (1200, 3600), MusicType.Nether => (1200, 3600),
        MusicType.End => (6000, 24000), MusicType.EndBoss => (0, 0), _ => (int.MaxValue, int.MaxValue)
    };

    /// <summary>Jeden tick gry (20/s).</summary>
    public static void Tick()
    {
        if (!Sound.Enabled || !Sound.BankReady) return;
        var (min, max) = Delay(_type);
        if (_cur != null)
        {
            if (_curType != _type)
            {
                _cur.FadeOut(1.5f); _cur = null;
                _timeUntilNext = Rnd.Next(0, Math.Max(1, min / 2));
            }
            else if (_cur.Done)
            {
                _cur = null;
                _timeUntilNext = Math.Min(Rnd.Next(min, Math.Max(min + 1, max)), _timeUntilNext);
            }
        }
        _timeUntilNext = Math.Min(_timeUntilNext, max);
        if (_cur == null && _timeUntilNext-- <= 0)
        {
            string ev = EventOf(_type);
            if (!Sound.HasEvent(ev)) ev = "music.game";
            _cur = Sound.StartMusic(ev);
            _curType = _type;
            _timeUntilNext = Math.Min(Rnd.Next(min, Math.Max(min + 1, max)), max);
        }
    }

    public static void StopNow() { if (_cur != null) { _cur.FadeOut(0.5f); _cur = null; } }
}
