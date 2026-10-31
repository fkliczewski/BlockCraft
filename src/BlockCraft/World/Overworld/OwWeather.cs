using System.Text.Json;
using BlockCraft.Net;

namespace BlockCraft.World;

/// <summary>WorldInfo i World.updateWeather z 1.8: osobne zegary deszczu, burzy i wymuszonej pogody.</summary>
public sealed class WeatherData
{
    public bool Raining { get; set; }
    public bool Thundering { get; set; }
    public int RainTime { get; set; }
    public int ThunderTime { get; set; }
    public int CleanTime { get; set; }
}

public sealed partial class World
{
    public WeatherData Weather { get; private set; } = new();
    public float RainStrength { get; private set; }
    public float ThunderStrength { get; private set; }
    private float _prevRainStrength, _prevThunderStrength;
    public float RainAt(float pt) => _prevRainStrength + (RainStrength - _prevRainStrength) * pt;
    public float ThunderAt(float pt) => (_prevThunderStrength + (ThunderStrength - _prevThunderStrength) * pt) * RainAt(pt);
    public bool IsRaining => Dimension == DimensionId.Overworld && RainStrength > .2f;

    public void RestoreWeather(WeatherData data)
    {
        // Kopia: dalsze tykanie świata nie zmienia migawki zapisu.
        Weather = data == null ? new() : new()
        {
            Raining = data.Raining, Thundering = data.Thundering,
            RainTime = Math.Max(0, data.RainTime), ThunderTime = Math.Max(0, data.ThunderTime), CleanTime = Math.Max(0, data.CleanTime)
        };
        _prevRainStrength = RainStrength = Weather.Raining ? 1 : 0;
        _prevThunderStrength = ThunderStrength = Weather.Thundering ? 1 : 0;
    }

    public bool IsRainingAt(int x, int y, int z)
    {
        if (!IsRaining || !IsLoadedAt(x, z) || !CanSeeSky(x, y, z)) return false;
        int biome = GetBiome(x, z);
        return Biome.Get(biome).CanRain && !Biome.IsSnowyAt(biome, x, y, z);
    }

    public void SetWeather(string kind, int ticks)
    {
        if (ticks < 1) throw new ArgumentOutOfRangeException(nameof(ticks));
        if (kind is not ("clear" or "rain" or "thunder")) throw new ArgumentException("/weather <clear|rain|thunder> [sekundy]");
        Weather.CleanTime = kind == "clear" ? ticks : 0;
        Weather.RainTime = Weather.ThunderTime = kind == "clear" ? 0 : ticks;
        Weather.Raining = kind != "clear";
        Weather.Thundering = kind == "thunder";
    }

    private void TickWeather()
    {
        if (Dimension != DimensionId.Overworld) return;
        if (!IsRemote)
        {
            if (Weather.CleanTime > 0)
            {
                Weather.CleanTime--;
                Weather.ThunderTime = Weather.Thundering ? 1 : 2;
                Weather.RainTime = Weather.Raining ? 1 : 2;
            }
            if (Weather.ThunderTime <= 0) Weather.ThunderTime = Weather.Thundering ? Rand.Next(12000) + 3600 : Rand.Next(168000) + 12000;
            else if (--Weather.ThunderTime == 0) Weather.Thundering = !Weather.Thundering;
            if (Weather.RainTime <= 0) Weather.RainTime = Weather.Raining ? Rand.Next(12000) + 12000 : Rand.Next(168000) + 12000;
            else if (--Weather.RainTime == 0) Weather.Raining = !Weather.Raining;
        }
        _prevRainStrength = RainStrength; _prevThunderStrength = ThunderStrength;
        RainStrength = Math.Clamp(RainStrength + (Weather.Raining ? .01f : -.01f), 0, 1);
        ThunderStrength = Math.Clamp(ThunderStrength + (Weather.Thundering ? .01f : -.01f), 0, 1);
    }

    private void ResetWeatherAfterSleep()
    {
        Weather.RainTime = Weather.ThunderTime = 0;
        Weather.Raining = Weather.Thundering = false;
    }
}

public static class OwWeather
{
    public static void Register()
    {
        WorldHooks.IsRainingAt = (w, x, y, z) => w.IsRainingAt(x, y, z);
        // Klient otrzymuje cele; sam wygładza przejście co tick, bez sterowania zegarami hosta.
        NetHooks.WorldState["ow.weather"] = (
            w => JsonSerializer.SerializeToUtf8Bytes(w.Weather),
            (w, bytes) =>
            {
                var data = JsonSerializer.Deserialize<WeatherData>(bytes);
                if (data == null) return;
                w.Weather.Raining = data.Raining; w.Weather.Thundering = data.Thundering;
                w.Weather.RainTime = data.RainTime; w.Weather.ThunderTime = data.ThunderTime; w.Weather.CleanTime = data.CleanTime;
            });
    }
}
