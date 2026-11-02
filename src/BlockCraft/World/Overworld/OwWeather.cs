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
            foreach (var c in Chunks.Values)
            {
                if (!c.Merged || !Players.Any(p => Math.Abs((int)Math.Floor(p.X / 16) - c.Cx) <= 7
                    && Math.Abs((int)Math.Floor(p.Z / 16) - c.Cz) <= 7)) continue;
                if (IsRaining && ThunderAt(1) > .9f && Rand.Next(100000) == 0)
                    TryNaturalLightning(c.Cx * 16 + Rand.Next(16), c.Cz * 16 + Rand.Next(16));
                if (Rand.Next(16) == 0) UpdateWeatherSurface(c.Cx * 16 + Rand.Next(16), c.Cz * 16 + Rand.Next(16));
            }
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

    /// <summary>Jedna próba powierzchniowa WorldServer: zamarzanie brzegu wody i pojedyncza warstwa śniegu.</summary>
    internal void UpdateWeatherSurface(int x, int z)
    {
        if (IsRemote || Dimension != DimensionId.Overworld || !IsLoadedAt(x, z)) return;
        int y = GetHeight(x, z);
        if (y < 1 || y >= Chunk.Height || !Biome.IsSnowyAt(GetBiome(x, z), x, y, z)) return;
        if (GetBlockLight(x, y - 1, z) < 10 && GetBlock(x, y - 1, z) == B.Water && GetMeta(x, y - 1, z) == 0
            && (GetBlock(x - 1, y - 1, z) != B.Water || GetBlock(x + 1, y - 1, z) != B.Water
                || GetBlock(x, y - 1, z - 1) != B.Water || GetBlock(x, y - 1, z + 1) != B.Water))
            SetBlock(x, y - 1, z, B.Ice);
        if (Weather.Raining && GetBlockLight(x, y, z) < 10 && GetBlock(x, y, z) == B.Air
            && GetBlock(x, y - 1, z) is not (B.Ice or B.PackedIce)
            && Block.Get(B.SnowLayer).CanPlaceAt(this, x, y, z)) SetBlock(x, y, z, B.SnowLayer);
    }

    internal bool TryNaturalLightning(int x, int z)
    {
        if (IsRemote || Dimension != DimensionId.Overworld || !IsRaining || ThunderAt(1) <= .9f || !IsLoadedAt(x, z)) return false;
        int y = GetHeight(x, z);
        var targets = GetEntitiesInBox(new BlockCraft.Core.AABB(x - 3, y - 3, z - 3, x + 4, Chunk.Height, z + 4))
            .OfType<BlockCraft.Entities.LivingEntity>().Where(e => !e.Dead && CanSeeSky((int)Math.Floor(e.X), (int)Math.Floor(e.Y), (int)Math.Floor(e.Z))).ToArray();
        if (targets.Length > 0)
        {
            var target = targets[Rand.Next(targets.Length)];
            x = (int)Math.Floor(target.X); y = (int)Math.Floor(target.Y); z = (int)Math.Floor(target.Z);
        }
        if (!IsRainingAt(x, y, z)) return false;
        var bolt = new BlockCraft.Entities.LightningBolt(this); bolt.SetPositionAndPrev(x, y, z); SpawnEntity(bolt);
        return true;
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
