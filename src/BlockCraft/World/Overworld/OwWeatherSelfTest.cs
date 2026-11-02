using BlockCraft.Entities;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestOwWeather()
    {
        var w = MakeWorld(19, WorldType.Superflat, 0);
        w.Rules.DoMobSpawning = false; w.Rules.DoDaylightCycle = false; w.DayTime = 6000;
        w.Weather.RainTime = w.Weather.ThunderTime = 1;
        w.Tick();
        Check(w.Weather.Raining && w.Weather.Thundering && Math.Abs(w.RainStrength - .01f) < .0001f,
            "Pogoda: wygaśnięcie zegarów włącza deszcz i burzę, płynne narastanie");
        w.Tick();
        Check(w.Weather.RainTime is >= 12000 and < 24000 && w.Weather.ThunderTime is >= 3600 and < 15600,
            "Pogoda: okresy opadów i burzy z MC 1.8");
        Ticks(w, 100);
        Check(w.RainStrength == 1 && w.ThunderStrength == 1 && w.DayTime == 6000 && w.SkyLightSubtracted == 5,
            "Pogoda działa bez cyklu dnia i przyciemnia światło podczas burzy");
        Check(w.IsRainingAt(1, 4, 1) && WorldHooks.IsRainingAt(w, 1, 4, 1), "Pogoda: deszcz pod otwartym niebem i hak ognia/kotła");
        var burning = new Cow(w); burning.SetPositionAndPrev(8, 4, 8); burning.FireTicks = 100;
        burning.Tick();
        Check(burning.FireTicks == 0, "Deszcz gasi płonącą encję");
        w.SetBlock(1, 7, 1, B.Stone);
        Check(!w.IsRainingAt(1, 4, 1) && !w.IsRainingAt(32, 4, 32), "Deszcz nie przenika dachu ani niewczytanego terenu");
        var c = w.GetChunk(0, 0);
        c.Biomes[0] = (byte)Biome.Desert; c.Biomes[1] = (byte)Biome.IcePlains;
        Check(!w.IsRainingAt(0, 4, 0) && !w.IsRainingAt(1, 4, 0), "Brak deszczu na pustyni i w miejscu opadów śniegu");
        w.SetWeather("clear", 200);
        Ticks(w, 101);
        Check(!w.IsRaining && !w.Weather.Raining && !w.Weather.Thundering && w.Weather.CleanTime == 99 && w.SkyLightSubtracted == 0,
            "Pogoda: clear wygasza opady i wstrzymuje ich powrót");
        var p = SpawnPlayer(w, 8, 4, 8); p.IsLocalOp = true;
        var cmd = new Commands(w);
        Check(cmd.Execute(p, "/weather thunder 42") && w.Weather.RainTime == 840 && w.Weather.ThunderTime == 840
            && w.Weather.Thundering && w.Weather.Raining, "/weather thunder ustawia czas w sekundach");
        Check(!cmd.Execute(p, "/weather rain 0") && !cmd.Execute(p, "/weather rain 1000001") && !cmd.Execute(p, "/weather snow"),
            "/weather odrzuca błędne czasy i nieznane typy");
        string dir = Path.Combine(Path.GetTempPath(), "blockcraft-weather-" + Guid.NewGuid().ToString("N"));
        try
        {
            new WorldStorage(dir).SaveLevel(new LevelData { Weather = w.Weather });
            var copy = MakeWorld(19, WorldType.Superflat, 0);
            copy.RestoreWeather(WorldStorage.LoadLevel(dir).Weather);
            Check(copy.Weather.RainTime == 840 && copy.Weather.ThunderTime == 840 && copy.IsRaining && copy.ThunderStrength == 1,
                "Zapis level.json odtwarza zegary i aktywne opady");
            copy.Tick();
            Check(w.Weather.RainTime == 840 && copy.Weather.RainTime == 839, "Odtworzona pogoda nie współdzieli migawki zapisu");
            copy.RestoreWeather(null);
            Check(!copy.IsRaining && copy.Weather.RainTime == 0, "Starszy zapis bez pogody rozpoczyna przy dobrej pogodzie");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        var remote = new World.World(19, WorldType.Superflat, true);
        var hook = NetHooks.WorldState["ow.weather"];
        hook.Read(remote, hook.Write(w)); Ticks(remote, 100);
        Check(remote.RainStrength > .99f && remote.ThunderStrength > .99f && remote.Weather.RainTime == 840,
            "Sieć: stan pogody hosta steruje opadami klienta bez lokalnego odmierzania zegarów");
        var nether = new World.World(19, WorldType.Default, false, DimensionId.Nether);
        nether.SetWeather("thunder", 200); Ticks(nether, 100);
        Check(!nether.IsRaining && nether.RainStrength == 0 && nether.Weather.RainTime == 200, "Nether nie ma opadów ani cyklu pogody");
        TestOwWeatherSurface();
    }

    private static void TestOwWeatherSurface()
    {
        var w = MakeWorld(21, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        foreach (var c in w.Chunks.Values) Array.Fill(c.Biomes, (byte)Biome.IcePlains);
        w.RestoreWeather(new WeatherData { Raining = true, RainTime = 1000 });
        w.UpdateWeatherSurface(8, 8); w.UpdateWeatherSurface(8, 8);
        Check(w.GetBlock(8, 4, 8) == B.SnowLayer && w.GetMeta(8, 4, 8) == 0 && w.GetBlock(8, 5, 8) == B.Air,
            "Opady odkładają jedną warstwę śniegu bez samoczynnego piętrzenia");
        w.SetBlock(10, 7, 10, B.Stone); w.UpdateWeatherSurface(10, 10);
        Check(w.GetBlock(10, 8, 10) == B.SnowLayer && w.GetBlock(10, 4, 10) == B.Air, "Śnieg odkłada się na dachu, nie pod nim");
        for (int x = 1; x <= 3; x++) for (int z = 1; z <= 3; z++) w.SetBlock(x, 3, z, B.Water);
        w.UpdateWeatherSurface(2, 2);
        Check(w.GetBlock(2, 3, 2) == B.Water, "Woda otoczona wodą nie zamarza przed brzegiem");
        w.UpdateWeatherSurface(1, 2);
        Check(w.GetBlock(1, 3, 2) == B.Ice && w.GetBlock(1, 4, 2) == B.Air, "Zamarza brzeg źródła wody, śnieg nie pokrywa lodu");
        w.UpdateWeatherSurface(2, 2);
        Check(w.GetBlock(2, 3, 2) == B.Ice, "Zamarzanie rozchodzi się od brzegu");
        w.SetBlock(5, 3, 5, B.Water, 1); w.UpdateWeatherSurface(5, 5);
        Check(w.GetBlock(5, 3, 5) == B.Water, "Płynąca woda nie zamarza");
        w.SetWeather("clear", 1000); w.UpdateWeatherSurface(9, 9);
        w.SetBlock(6, 3, 6, B.Water); w.UpdateWeatherSurface(6, 6);
        Check(w.GetBlock(9, 4, 9) == B.Air && w.GetBlock(6, 3, 6) == B.Ice, "Przy dobrej pogodzie woda zamarza, ale nie przybywa śniegu");
        foreach (var c in w.Chunks.Values) Array.Fill(c.Biomes, (byte)Biome.Plains);
        w.RestoreWeather(new WeatherData { Raining = true, RainTime = 1000 });
        w.SetBlock(12, 3, 12, B.Farmland); Block.Get(B.Farmland).OnRandomTick(w, 12, 3, 12, w.Rand);
        Check(w.GetMeta(12, 3, 12) == 7, "Deszcz nawadnia pole uprawne bez źródła wody");
        w.SetBlock(12, 6, 12, B.Stone); Block.Get(B.Farmland).OnRandomTick(w, 12, 3, 12, w.Rand);
        Check(w.GetMeta(12, 3, 12) == 6, "Dach chroni pole przed nawadnianiem deszczem");
    }
}
