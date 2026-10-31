using BlockCraft.Items;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Izometryczne ikony bloków renderowane raz do tablicy tekstur 64x64.</summary>
public sealed class IconAtlas : IDisposable
{
    public const int Size = 64;
    public TextureArray Array;
    private readonly Dictionary<int, int> _layer = new(); // klucz: id | meta<<16

    public bool TryGet(ItemStack s, out int layer)
    {
        int key = s.Id | (HasVariants(s.Id) ? s.Damage << 16 : 0);
        return _layer.TryGetValue(key, out layer);
    }

    public IconAtlas(EntityRenderer er, TextureArray terrain)
    {
        var keys = new List<(int id, int meta)>();
        foreach (var it in Item.All)
        {
            if (!EntityRenderer.IsBlockModel(it)) continue;
            foreach (int m in it.GetVariants()) keys.Add((it.Id, m));
        }
        var empty = new List<uint[]>();
        for (int i = 0; i < Math.Max(1, keys.Count); i++) empty.Add(new uint[Size * Size]);
        Array = new TextureArray(Size, empty, false);
        var fbo = new Framebuffer(Size, Size);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo.Fbo);
        GL.Viewport(0, 0, Size, Size);
        GL.Enable(EnableCap.DepthTest); GL.DepthFunc(DepthFunction.Lequal);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.Blend);
        var proj = Matrix4.CreateOrthographicOffCenter(-1, 1, -1, 1, -10, 10);
        er.Shader.Use(); er.Shader.Set("uViewProj", proj);
        er.Shader.Set("uFogColor", Vector3.Zero); er.Shader.Set("uFogStart", 1000f); er.Shader.Set("uFogEnd", 2000f); er.Shader.Set("uFogMode", 0);
        er.Shader.Set("uSun", 1f); er.Shader.Set("uGamma", 0f);
        var b = er.Terrain;
        b.Sky = 15; b.Blk = 0; b.Color = Vector4.One; b.Overlay = Vector4.Zero;
        for (int i = 0; i < keys.Count; i++)
        {
            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            // MC: rotate X 30, Y 225(45), skala ~0.625
            var m = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(45)) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(30)) * Matrix4.CreateScale(1.12f);
            b.FlatShade = true;
            er.RenderBlockAt(keys[i].id, keys[i].meta, m);
            // ręczne cieniowanie ścian: góra jasna, boki ciemniejsze (jak GUI MC)
            ShadeFaces(b);
            b.FlatShade = false;
            er.Flush(b, terrain);
            Array.Bind(0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, 0, 0, Size, Size);
            _layer[keys[i].id | (HasVariants(keys[i].id) ? keys[i].meta << 16 : 0)] = i;
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        fbo.Dispose();
    }

    private static bool HasVariants(int id) { var it = Item.ById[id]; return it != null && it.GetVariants().Length > 1; }

    /// <summary>Ustawia jasność wg normalnej (obliczonej z pozycji wierzchołków).</summary>
    private static void ShadeFaces(ModelBatch b)
    {
        for (int q = 0; q < b.Verts / 4; q++)
        {
            int o = q * 4 * ModelBatch.F;
            var a = new Vector3(b.D[o], b.D[o + 1], b.D[o + 2]);
            var bb = new Vector3(b.D[o + ModelBatch.F], b.D[o + ModelBatch.F + 1], b.D[o + ModelBatch.F + 2]);
            var d = new Vector3(b.D[o + 3 * ModelBatch.F], b.D[o + 3 * ModelBatch.F + 1], b.D[o + 3 * ModelBatch.F + 2]);
            var n = Vector3.Cross(bb - a, d - a);
            if (n.LengthSquared < 1e-12f) continue;
            n.Normalize();
            float s = n.Y > 0.5f ? 1f : n.X > 0.1f ? 0.8f : n.X < -0.1f ? 0.6f : 0.7f;
            if (n.Y < -0.5f) s = 0.5f;
            for (int k = 0; k < 4; k++) b.D[o + k * ModelBatch.F + 6] = s;
        }
    }

    public void Dispose() => Array.Dispose();
}
