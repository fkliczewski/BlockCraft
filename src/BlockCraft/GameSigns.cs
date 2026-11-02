using BlockCraft.Core;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft;

/// <summary>Tekst tabliczek (TileEntitySignRenderer): czarne linie na płaszczyźnie deski, do 64 bloków od kamery.</summary>
public sealed partial class Game
{
    private void DrawSignTexts(Vector3d cam, Matrix4 vp)
    {
        if (World == null) return;
        bool blend = GL.IsEnabled(EnableCap.Blend), cull = GL.IsEnabled(EnableCap.CullFace), depth = GL.IsEnabled(EnableCap.DepthTest);
        bool any = false;
        foreach (var s in Signs.Near(World, cam))
        {
            if (s.Lines.All(string.IsNullOrEmpty)) continue;
            int id = World.GetBlock(s.X, s.Y, s.Z); if (!Signs.IsSign(id)) continue;
            var rel = new Vector3((float)(s.X - cam.X), (float)(s.Y - cam.Y), (float)(s.Z - cam.Z));
            Sb.Begin3D(Signs.TextFrame(id, World.GetMeta(s.X, s.Y, s.Z), rel) * vp);
            any = true;
            for (int j = 0; j < 4; j++)
            {
                string line = s.Lines[j];
                if (string.IsNullOrEmpty(line)) continue;
                Sb.Text(line, -Sb.Measure(line) / 2f, j * 10 - 20, 0xFF000000, false);
            }
            Sb.End3D();
        }
        if (!any) return;
        if (blend) GL.Enable(EnableCap.Blend); else GL.Disable(EnableCap.Blend);
        if (cull) GL.Enable(EnableCap.CullFace); else GL.Disable(EnableCap.CullFace);
        if (depth) GL.Enable(EnableCap.DepthTest); else GL.Disable(EnableCap.DepthTest);
    }

    /// <summary>Deska i słupek tabliczek w przebiegu encji (obrót co 22,5 stopnia, czego siatka chunka nie umie).</summary>
    internal static void DrawSignBoards(EntityRenderer r, World.World w, float pt, Vector3d cam)
    {
        int tex = TextureGen.Layer("planks_oak");
        foreach (var s in Signs.Near(w, cam))
        {
            int id = w.GetBlock(s.X, s.Y, s.Z); if (!Signs.IsSign(id)) continue;
            r.Terrain.Sky = w.GetSkyLight(s.X, s.Y, s.Z); r.Terrain.Blk = w.GetBlockLight(s.X, s.Y, s.Z);
            r.Terrain.Color = Vector4.One; r.Terrain.Overlay = Vector4.Zero;
            var rel = new Vector3((float)(s.X - cam.X), (float)(s.Y - cam.Y), (float)(s.Z - cam.Z));
            Signs.Model(id, w.GetMeta(s.X, s.Y, s.Z), rel, r.EmitModelBox, tex);
        }
    }
}
