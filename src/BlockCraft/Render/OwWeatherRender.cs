using BlockCraft.Core;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using StbImageSharp;

namespace BlockCraft.Render;

public sealed partial class WorldRenderer
{
    public int RainColumns { get; private set; }
    public int SnowColumns { get; private set; }
    private TextureArray _weatherTextures;
    private bool _weatherNative, _weatherNativeFailed;
    public bool WeatherNativeTextures => _weatherNative;
    public int WeatherTextureSize => _weatherTextures?.Size ?? 0;

    private void EnsureWeatherTextures()
    {
        if (ClientAssets.Ready && !_weatherNative && !_weatherNativeFailed)
        {
            try
            {
                var images = new[] { "rain", "snow" }.Select(n =>
                {
                    using var stream = File.OpenRead(ClientAssets.WeatherPath(n));
                    return ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                }).ToArray();
                int size = images.Max(i => Math.Max(i.Width, i.Height));
                if (size > 512 || images.Any(i => i.Width < 1 || i.Height < 1)) throw new IOException("nieprawidłowy rozmiar tekstury pogody");
                var pixels = images.Select(i =>
                {
                    var result = new uint[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        int src = ((y * i.Height / size) * i.Width + x * i.Width / size) * 4;
                        result[y * size + x] = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(i.Data.AsSpan(src, 4));
                    }
                    return result;
                }).ToArray();
                var texture = new TextureArray(size, pixels, false);
                _weatherTextures?.Dispose(); _weatherTextures = texture; _weatherNative = true;
                Log.Info($"Pogoda: oryginalne tekstury 1.8 ({string.Join(",", images.Select(i => i.Width + "x" + i.Height))}), tablica {size}x{size}");
            }
            catch (Exception ex) { _weatherNativeFailed = true; Log.Error("Pogoda: tekstury zastępcze, " + ex.Message); }
        }
        _weatherTextures ??= new TextureArray(16, new[] { TextureGen.Get("weather_rain"), TextureGen.Get("weather_snow") }, false);
    }

    private void RenderWeather(Vector3d cam, Matrix4 vp, float pt)
    {
        RainColumns = SnowColumns = 0;
        var w = World; float strength = w.RainAt(pt);
        if (w.Dimension != DimensionId.Overworld || strength <= 0) return;
        EnsureWeatherTextures();
        const int radius = 10;
        int cx = MathUtil.FloorToInt(cam.X), cy = MathUtil.FloorToInt(cam.Y), cz = MathUtil.FloorToInt(cam.Z);
        var b = Entities.Terrain;
        GL.Disable(EnableCap.CullFace); GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); GL.DepthMask(false);
        Entities.Shader.Use(); Entities.Shader.Set("uViewProj", vp); SetFog(Entities.Shader);
        for (int z = cz - radius; z <= cz + radius; z++)
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (!w.IsLoadedAt(x, z)) continue;
                int surface = w.GetHeight(x, z), low = Math.Max(cy - radius, surface), high = Math.Max(cy + radius, surface);
                if (low == high) continue;
                var biome = Biome.Get(w.GetBiome(x, z));
                if (!biome.Snow && !biome.CanRain) continue;
                bool snow = Biome.IsSnowyAt(biome.Id, x, Math.Max(cy, surface), z);
                double dx = x + .5 - cam.X, dz = z + .5 - cam.Z;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                double ox = x - cx, oz = z - cz, axis = Math.Sqrt(ox * ox + oz * oz);
                if (axis < .05) continue;
                float rx = (float)(-oz / axis * .5), rz = (float)(ox / axis * .5);
                float px = (float)dx, pz = (float)dz;
                float y0 = (float)(low - cam.Y), y1 = (float)(high - cam.Y);
                int hash = unchecked(x * x * 3121 + x * 45238971 ^ z * z * 418711 + z * 13761);
                var random = new JRandom(hash); int tick = unchecked((int)w.TotalTime);
                float time = tick + pt, u = 0, v;
                if (snow)
                {
                    u = (float)(random.NextDouble() + time * .01 * (float)random.NextGaussian());
                    v = ((tick & 511) + pt) / 512f + (float)(random.NextDouble() + time * (float)random.NextGaussian() * .001);
                }
                else
                {
                    int offset = unchecked(tick + x * x * 3121 + x * 45238971 + z * z * 418711 + z * 13761);
                    v = (float)(((offset & 31) + pt) / 32.0 * (3 + random.NextDouble()));
                }
                b.Layer = snow ? 1 : 0;
                b.Sky = w.GetSkyLight(x, Math.Max(cy, surface), z); b.Blk = w.GetBlockLight(x, Math.Max(cy, surface), z);
                if (snow) { b.Sky = (b.Sky * 3 + 15) / 4; b.Blk = (b.Blk * 3 + 15) / 4; }
                b.Color = new Vector4(1, 1, 1, Math.Clamp((1 - distance * distance / (radius * radius)) * (snow ? .3f : .5f) + .5f, 0, 1) * strength);
                b.Overlay = Vector4.Zero;
                b.Quad(new(px - rx, y1, pz - rz), new(px - rx, y0, pz - rz),
                    new(px + rx, y0, pz + rz), new(px + rx, y1, pz + rz),
                    u, high / 4f + v, 1 + u, low / 4f + v, 1);
                if (snow) SnowColumns++; else RainColumns++;
            }
        Entities.Flush(b, _weatherTextures); b.Color = Vector4.One;
        GL.DepthMask(true);
    }
}
