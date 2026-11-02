using BlockCraft.Entities;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed partial class EntityRenderer
{
    private readonly ModelBatch _lightning = new();
    // Geometria RenderLightningBolt 1.8.9: cztery otoczki, pień i dwie gałęzie, Java Random.
    public void RenderLightning(LightningBolt bolt, Vector3 pos)
    {
        if (!bolt.Flashing) return;
        var xs = new double[8]; var zs = new double[8]; double x = 0, z = 0;
        var r = new JRandom(bolt.VertexSeed);
        for (int i = 7; i >= 0; i--) { xs[i] = x; zs[i] = z; x += r.NextInt(11) - 5; z += r.NextInt(11) - 5; }
        var b = _lightning; b.Layer = TextureGen.Layer("weather_lightning"); b.Sky = b.Blk = 15;
        b.Color = new Vector4(.45f, .45f, .5f, .3f);
        for (int layer = 0; layer < 4; layer++)
        {
            r = new JRandom(bolt.VertexSeed);
            for (int branch = 0; branch < 3; branch++)
            {
                int top = branch == 0 ? 7 : 7 - branch, bottom = branch == 0 ? 0 : top - 2;
                double ax = xs[top] - x, az = zs[top] - z;
                for (int i = top; i >= bottom; i--)
                {
                    double bx = ax, bz = az;
                    int spread = branch == 0 ? 11 : 31;
                    ax += r.NextInt(spread) - spread / 2; az += r.NextInt(spread) - spread / 2;
                    double upper = .1 + layer * .2, lower = upper;
                    if (branch == 0) { upper *= i * .1 + 1; lower *= (i - 1) * .1 + 1; }
                    Vector3 Corner(int corner, bool high)
                    {
                        double width = high ? upper : lower;
                        return pos + new Vector3((float)(.5 + (high ? bx : ax) + (corner is 1 or 2 ? width : -width)),
                            (high ? i + 1 : i) * 16, (float)(.5 + (high ? bz : az) + (corner is 2 or 3 ? width : -width)));
                    }
                    for (int side = 0; side < 4; side++)
                        b.Quad(Corner(side, false), Corner(side, true), Corner((side + 1) & 3, true), Corner((side + 1) & 3, false), 0, 0, 1, 1, 1);
                }
            }
        }
        GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        Flush(b, TerrainTex);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); GL.Disable(EnableCap.Blend);
    }
}
