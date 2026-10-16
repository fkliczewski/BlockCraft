using BlockCraft.Core;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed class Shader : IDisposable
{
    public readonly int Handle;
    private readonly Dictionary<string, int> _loc = new();

    public Shader(string vert, string frag, string name = "")
    {
        int v = Compile(ShaderType.VertexShader, vert, name);
        int f = Compile(ShaderType.FragmentShader, frag, name);
        Handle = GL.CreateProgram();
        GL.AttachShader(Handle, v); GL.AttachShader(Handle, f);
        GL.LinkProgram(Handle);
        GL.GetProgram(Handle, GetProgramParameterName.LinkStatus, out int ok);
        if (ok == 0) throw new Exception($"Shader link ({name}): " + GL.GetProgramInfoLog(Handle));
        GL.DeleteShader(v); GL.DeleteShader(f);
    }

    private static int Compile(ShaderType t, string src, string name)
    {
        int s = GL.CreateShader(t);
        GL.ShaderSource(s, src);
        GL.CompileShader(s);
        GL.GetShader(s, ShaderParameter.CompileStatus, out int ok);
        if (ok == 0) throw new Exception($"Shader compile ({name} {t}): " + GL.GetShaderInfoLog(s));
        return s;
    }

    public void Use() => GL.UseProgram(Handle);
    public int Loc(string n)
    {
        if (!_loc.TryGetValue(n, out int l)) { l = GL.GetUniformLocation(Handle, n); _loc[n] = l; }
        return l;
    }
    public void Set(string n, float v) => GL.Uniform1(Loc(n), v);
    public void Set(string n, int v) => GL.Uniform1(Loc(n), v);
    public void Set(string n, Vector2 v) => GL.Uniform2(Loc(n), v);
    public void Set(string n, Vector3 v) => GL.Uniform3(Loc(n), v);
    public void Set(string n, Vector4 v) => GL.Uniform4(Loc(n), v);
    public void Set(string n, Matrix4 m) => GL.UniformMatrix4(Loc(n), false, ref m);
    public void Dispose() => GL.DeleteProgram(Handle);
}

public sealed class Texture2D : IDisposable
{
    public readonly int Handle, Width, Height;
    public Texture2D(int w, int h, uint[] pixels, bool linear = false)
    {
        Width = w; Height = h;
        Handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        var filter = linear ? (int)TextureMinFilter.Linear : (int)TextureMinFilter.Nearest;
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    }
    public void Update(uint[] pixels)
    {
        GL.BindTexture(TextureTarget.Texture2D, Handle);
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, Width, Height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
    }
    public void Bind(int unit = 0) { GL.ActiveTexture(TextureUnit.Texture0 + unit); GL.BindTexture(TextureTarget.Texture2D, Handle); }
    public void Dispose() => GL.DeleteTexture(Handle);
}

public sealed class TextureArray : IDisposable
{
    public readonly int Handle, Size, Layers;
    private readonly bool _mips;
    public TextureArray(int size, IReadOnlyList<uint[]> layers, bool mipmaps, int levels = 4)
    {
        Size = size; Layers = layers.Count; _mips = mipmaps;
        Handle = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2DArray, Handle);
        int lv = mipmaps ? levels + 1 : 1;
        GL.TexStorage3D(TextureTarget3d.Texture2DArray, lv, SizedInternalFormat.Rgba8, size, size, Math.Max(1, layers.Count));
        for (int i = 0; i < layers.Count; i++)
            GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, i, size, size, 1, PixelFormat.Rgba, PixelType.UnsignedByte, layers[i]);
        if (mipmaps)
        {
            // własne mipmapy z zachowaniem przezroczystości (jak MC)
            for (int i = 0; i < layers.Count; i++)
            {
                var cur = layers[i]; int s = size;
                for (int l = 1; l < lv; l++)
                {
                    var next = Downsample(cur, s);
                    s /= 2;
                    GL.TexSubImage3D(TextureTarget.Texture2DArray, l, 0, 0, i, s, s, 1, PixelFormat.Rgba, PixelType.UnsignedByte, next);
                    cur = next;
                }
            }
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMaxLevel, levels);
        }
        else GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    }

    private static uint[] Downsample(uint[] src, int s)
    {
        int h = s / 2; var dst = new uint[h * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < h; x++)
            {
                int r = 0, g = 0, b = 0, a = 0, n = 0, amax = 0;
                for (int k = 0; k < 4; k++)
                {
                    uint c = src[(y * 2 + (k >> 1)) * s + x * 2 + (k & 1)];
                    int ca = (int)(c >> 24);
                    amax = Math.Max(amax, ca);
                    if (ca == 0) continue;
                    r += (int)(c & 255); g += (int)((c >> 8) & 255); b += (int)((c >> 16) & 255); a += ca; n++;
                }
                if (n == 0) { dst[y * h + x] = 0; continue; }
                int aa = a / 4;
                // zachowaj krycie dla wycięć (liście) – zaokrąglenie jak MC
                if (amax == 255 && n >= 2) aa = 255;
                if (amax < 255) aa = a / n;
                dst[y * h + x] = (uint)((r / n) | ((g / n) << 8) | ((b / n) << 16) | (aa << 24));
            }
        return dst;
    }

    public void UpdateLayer(int layer, uint[] px)
    {
        GL.BindTexture(TextureTarget.Texture2DArray, Handle);
        GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, layer, Size, Size, 1, PixelFormat.Rgba, PixelType.UnsignedByte, px);
        if (_mips)
        {
            var cur = px; int s = Size;
            for (int l = 1; s > 1 && l <= 4; l++)
            {
                var next = Downsample(cur, s); s /= 2;
                GL.TexSubImage3D(TextureTarget.Texture2DArray, l, 0, 0, layer, s, s, 1, PixelFormat.Rgba, PixelType.UnsignedByte, next);
                cur = next;
            }
        }
    }
    public void Bind(int unit = 0) { GL.ActiveTexture(TextureUnit.Texture0 + unit); GL.BindTexture(TextureTarget.Texture2DArray, Handle); }
    public void Dispose() => GL.DeleteTexture(Handle);
}

/// <summary>Bufor wierzchołków z dowolnym formatem.</summary>
public sealed class Mesh : IDisposable
{
    public readonly int Vao, Vbo, Ebo;
    public int IndexCount, VertexCount;
    public readonly int Stride;

    public Mesh(int stride, Action setupAttribs, bool indexed = true)
    {
        Stride = stride;
        Vao = GL.GenVertexArray(); Vbo = GL.GenBuffer();
        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        setupAttribs();
        if (indexed)
        {
            Ebo = SharedQuadIndices.Ebo;
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ebo);
        }
        GL.BindVertexArray(0);
    }

    public unsafe void Upload(byte[] data, int bytes, BufferUsageHint hint = BufferUsageHint.StaticDraw)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        fixed (byte* p = data) GL.BufferData(BufferTarget.ArrayBuffer, bytes, (IntPtr)p, hint);
        VertexCount = bytes / Stride;
        IndexCount = VertexCount / 4 * 6;
        SharedQuadIndices.Ensure(VertexCount / 4);
    }
    public unsafe void Upload(float[] data, int floats, BufferUsageHint hint = BufferUsageHint.StreamDraw)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        fixed (float* p = data) GL.BufferData(BufferTarget.ArrayBuffer, floats * 4, (IntPtr)p, hint);
        VertexCount = floats * 4 / Stride;
        IndexCount = VertexCount / 4 * 6;
        SharedQuadIndices.Ensure(VertexCount / 4);
    }

    public void DrawQuads()
    {
        if (IndexCount == 0) return;
        GL.BindVertexArray(Vao);
        GL.DrawElements(PrimitiveType.Triangles, IndexCount, DrawElementsType.UnsignedInt, 0);
    }
    public void DrawArrays(PrimitiveType t)
    {
        if (VertexCount == 0) return;
        GL.BindVertexArray(Vao);
        GL.DrawArrays(t, 0, VertexCount);
    }
    public void Dispose() { GL.DeleteBuffer(Vbo); GL.DeleteVertexArray(Vao); }
}

public static class SharedQuadIndices
{
    public static int Ebo;
    private static int _quads;
    public static void Init() { Ebo = GL.GenBuffer(); Ensure(65536); }
    public static void Ensure(int quads)
    {
        if (quads <= _quads) return;
        int n = Math.Max(quads, _quads * 2);
        var idx = new uint[n * 6];
        for (uint q = 0; q < n; q++)
        {
            uint v = q * 4; int i = (int)q * 6;
            idx[i] = v; idx[i + 1] = v + 1; idx[i + 2] = v + 2; idx[i + 3] = v + 2; idx[i + 4] = v + 3; idx[i + 5] = v;
        }
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, idx.Length * 4, idx, BufferUsageHint.StaticDraw);
        _quads = n;
    }
}

public sealed class Framebuffer : IDisposable
{
    public int Fbo, ColorTex, DepthRb, Width, Height;
    public Framebuffer(int w, int h) { Resize(w, h); }
    public void Resize(int w, int h)
    {
        Dispose();
        Width = w; Height = h;
        Fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        ColorTex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, ColorTex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorTex, 0);
        DepthRb = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthRb);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, w, h);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, DepthRb);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    public void Dispose()
    {
        if (Fbo != 0) { GL.DeleteFramebuffer(Fbo); GL.DeleteTexture(ColorTex); GL.DeleteRenderbuffer(DepthRb); Fbo = 0; }
    }
}
