using BlockCraft.Core;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed partial class WorldRenderer
{
    public int RainColumns { get; private set; }
    public int SnowColumns { get; private set; }

    private void RenderWeather(Vector3d cam, Matrix4 vp, float pt)
    {
        RainColumns = SnowColumns = 0;
        var w = World; float strength = w.RainAt(pt);
        if (w.Dimension != DimensionId.Overworld || strength <= 0) return;
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
                if (distance > radius || distance < .05f) continue;
                float rx = (float)(-dz / distance * .5), rz = (float)(dx / distance * .5);
                float px = (float)dx, pz = (float)dz;
                float y0 = (float)(low - cam.Y), y1 = (float)(high - cam.Y);
                int hash = unchecked(x * x * 3121 + x * 45238971 ^ z * z * 418711 + z * 13761);
                float phase = (hash & 1023) / 1024f;
                float time = (w.TotalTime % 100000 + pt);
                float v = phase + time * (snow ? .003f : .035f);
                float drift = snow ? MathF.Sin(time * .01f + phase * 6.28f) * .2f : 0;
                b.Layer = TextureGen.Layer(snow ? "weather_snow" : "weather_rain");
                b.Sky = w.GetSkyLight(x, Math.Max(cy, surface), z); b.Blk = w.GetBlockLight(x, Math.Max(cy, surface), z);
                b.Color = new Vector4(1, 1, 1, (1 - distance * distance / (radius * radius)) * (snow ? .6f : .4f) * strength);
                b.Overlay = Vector4.Zero;
                b.Quad(new(px - rx + drift, y1, pz - rz), new(px - rx + drift, y0, pz - rz),
                    new(px + rx + drift, y0, pz + rz), new(px + rx + drift, y1, pz + rz),
                    0, high / 4f + v, 1, low / 4f + v, 1);
                if (snow) SnowColumns++; else RainColumns++;
            }
        Entities.Flush(b, Terrain); b.Color = Vector4.One;
        GL.DepthMask(true);
    }
}
