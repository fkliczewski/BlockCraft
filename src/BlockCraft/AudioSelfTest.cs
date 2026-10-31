using BlockCraft.Audio;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestAudioMix()
    {
        Console.WriteLine("[Audio: nakładanie, limiter i czyszczenie]");
        Voice Tone(float left = 1f, float right = .5f) => new()
        {
            Data = Enumerable.Range(0, 4096).SelectMany(_ => new[] { left, right }).ToArray(),
            Channels = 2, Frames = 4096, Loop = true
        };
        var mix = new Mixer(); var buffer = new float[4096];
        for (int i = 0; i < Mixer.MaxVoices; i++) mix.Add(Tone());
        mix.Read(buffer, 0, buffer.Length);
        Check(buffer.All(s => float.IsFinite(s) && Math.Abs(s) <= Mixer.PeakCeiling + .000001),
            "32 głośne źródła nie przesterowują miksu");
        Check(Math.Abs(buffer[0] - Mixer.PeakCeiling) < .000001 && Math.Abs(buffer[1] / buffer[0] - .5f) < .000001,
            "Limiter zachowuje proporcję stereo zamiast obcinać kanały");
        mix.ForEach(v => v.Done = true); mix.Add(Tone(.2f, .1f));
        var release = new float[44100 * 2]; mix.Read(release, 0, release.Length);
        Check(release[0] < .01f && release[^2] > .099f && release.All(s => Math.Abs(s) <= .100001f),
            "Po głośnym efekcie limiter płynnie wraca do normalnego poziomu");
        mix.Clear(); mix.Add(Tone(.2f, .1f)); mix.Read(buffer, 0, buffer.Length);
        Check(Math.Abs(buffer[0] - .1f) < .000001 && Math.Abs(buffer[1] - .05f) < .000001,
            "Cichy pojedynczy dźwięk ma zapas 6 dB bez kompresji");
        mix.CategoryVolume = _ => 0; mix.Read(buffer, 0, buffer.Length);
        Check(buffer.All(s => s == 0), "Wyciszenie kategorii daje cyfrową ciszę");

        var events = new Mixer();
        Voice Event(string key = "dig.stone", double x = 0) { var v = Tone(); v.Positional = true; v.EventKey = key; v.X = x; return v; }
        for (int i = 0; i < 20; i++) events.Add(Event());
        Check(events.Count == 1 && events.SuppressedDuplicates == 19, "Duplikaty jednego zdarzenia z tego samego miejsca nie sumują się");
        events.Read(buffer, 0, buffer.Length); events.Add(Event());
        Check(events.Count == 2, "Nowe uderzenie w kolejnym ticku pozostaje słyszalne");
        events.Read(buffer, 0, buffer.Length); events.Add(Event());
        Check(events.Count == 2, "Jedno źródło nie odkłada wielu kopii długiego efektu");
        events.Add(Event(x: 3)); events.Add(Event("dig.wood"));
        Check(events.Count == 4, "Różne źródła i różne efekty mogą grać równocześnie");
        events.Add(Event("note.harp")); events.Add(Event("note.harp")); events.Add(Event("note.harp"));
        Check(events.Count == 7, "Limiter duplikatów nie usuwa nut bloku muzycznego");
        var music = Tone(); music.IsMusic = true; events.Add(music);
        events.Clear(includeMusic: false);
        Check(events.Count == 1 && !music.Done, "Zmiana świata usuwa odgłosy i pozostawia muzykę");
        events.Stop(); Check(events.Count == 0 && music.Done, "Zamknięcie audio usuwa wszystkie głosy");

        var whole = new Mixer(); var split = new Mixer();
        for (int i = 0; i < 8; i++) { whole.Add(Tone()); split.Add(Tone()); }
        var a = new float[2048]; var b = new float[2050]; b[0] = b[^1] = 123;
        whole.Read(a, 0, 2048);
        for (int i = 0; i < 4; i++) split.Read(b, 1 + i * 512, 512);
        Check(a.SequenceEqual(b.Skip(1).Take(2048)) && b[0] == 123 && b[^1] == 123,
            "Limiter działa identycznie niezależnie od podziału i przesunięcia bufora");
    }
}
