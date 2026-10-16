using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Renderer 2D (GUI): tekstury 2D, tablice tekstur, prostokąty i tekst pikselowy.</summary>
public sealed class SpriteBatch : IDisposable
{
    private const int MaxQuads = 8192;
    private readonly float[] _v = new float[MaxQuads * 4 * 9];
    private int _quads;
    private readonly Mesh _mesh;
    private readonly Shader _shader;
    private int _curTex = -1; private bool _curArray;
    public Texture2D White;
    public Texture2D FontTex;
    private readonly Dictionary<char, (int x, int y, int w, int start)> _glyphs = new();
    private Matrix4 _proj;
    private readonly Stack<Matrix3> _xf = new();
    private Matrix3 _cur = Matrix3.Identity;
    private bool _hasXf;

    private const string Vert = @"#version 330 core
layout(location=0) in vec2 aPos; layout(location=1) in vec2 aUV; layout(location=2) in vec4 aCol; layout(location=3) in float aLayer;
uniform mat4 uProj; out vec2 vUV; out vec4 vCol; out float vLayer;
void main(){ gl_Position = uProj * vec4(aPos,0.0,1.0); vUV=aUV; vCol=aCol; vLayer=aLayer; }";
    private const string Frag = @"#version 330 core
in vec2 vUV; in vec4 vCol; in float vLayer; uniform sampler2D uTex; uniform sampler2DArray uArr; uniform int uMode; out vec4 o;
void main(){ vec4 t = uMode==1 ? texture(uArr, vec3(vUV, vLayer)) : texture(uTex, vUV); o = t*vCol; if(o.a<0.004) discard; }";

    public SpriteBatch()
    {
        _shader = new Shader(Vert, Frag, "sprite");
        _mesh = new Mesh(36, () =>
        {
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 36, 0);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 36, 8);
            GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, 36, 16);
            GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, 36, 32);
        });
        White = new Texture2D(1, 1, new[] { 0xFFFFFFFFu });
        BuildFont();
    }

    private void BuildFont()
    {
        var chars = FontData.Glyphs.Keys.ToList();
        int cols = 32, rows = (chars.Count + cols - 1) / cols;
        int w = cols * FontData.CellW, h = rows * FontData.CellH;
        var px = new uint[w * h];
        for (int i = 0; i < chars.Count; i++)
        {
            var (g, start) = FontData.Glyphs[chars[i]];
            int cx = (i % cols) * FontData.CellW, cy = (i / cols) * FontData.CellH;
            int gw = g.Max(r => r.Length);
            for (int r = 0; r < g.Length; r++)
                for (int c = 0; c < g[r].Length; c++)
                    if (g[r][c] == '#')
                    {
                        int yy = cy + FontData.Top + start + r;
                        if (yy >= cy && yy < cy + FontData.CellH) px[yy * w + cx + c] = 0xFFFFFFFF;
                    }
            _glyphs[chars[i]] = (cx, cy, gw, start);
        }
        FontTex = new Texture2D(w, h, px);
    }

    public void Begin(int width, int height, float scale)
    {
        _proj = Matrix4.CreateOrthographicOffCenter(0, width / scale, height / scale, 0, -1, 1);
        _quads = 0; _curTex = -1;
        _xf.Clear(); _cur = Matrix3.Identity; _hasXf = false;
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.Blend);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
    }

    public void PushTransform(float tx, float ty, float rotDeg, float scale)
    {
        _xf.Push(_cur);
        float r = MathHelper.DegreesToRadians(rotDeg);
        float c = MathF.Cos(r) * scale, s = MathF.Sin(r) * scale;
        // [x', y'] = M * [x, y, 1]
        var m = new Matrix3(c, -s, tx, s, c, ty, 0, 0, 1);
        _cur = Mul(_cur, m);
        _hasXf = true;
    }
    public void PopTransform() { Flush(); _cur = _xf.Count > 0 ? _xf.Pop() : Matrix3.Identity; _hasXf = _xf.Count > 0; }
    private static Matrix3 Mul(Matrix3 a, Matrix3 b)
    {
        var r = new Matrix3();
        for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) { float s = 0; for (int k = 0; k < 3; k++) s += a[i, k] * b[k, j]; r[i, j] = s; }
        return r;
    }

    private void SetTex(int handle, bool array)
    {
        if (handle == _curTex && array == _curArray) return;
        Flush();
        _curTex = handle; _curArray = array;
    }

    private void Vtx(int i, float x, float y, float u, float v, uint col, float layer)
    {
        if (_hasXf) { float nx = _cur.M11 * x + _cur.M12 * y + _cur.M13; float ny = _cur.M21 * x + _cur.M22 * y + _cur.M23; x = nx; y = ny; }
        int o = i * 9;
        _v[o] = x; _v[o + 1] = y; _v[o + 2] = u; _v[o + 3] = v;
        _v[o + 4] = (col & 255) / 255f; _v[o + 5] = ((col >> 8) & 255) / 255f; _v[o + 6] = ((col >> 16) & 255) / 255f; _v[o + 7] = (col >> 24) / 255f;
        _v[o + 8] = layer;
    }

    private void Quad(float x, float y, float w, float h, float u0, float v0, float u1, float v1, uint c0, uint c1, float layer)
    {
        if (_quads >= MaxQuads) Flush();
        int b = _quads * 4;
        Vtx(b, x, y, u0, v0, c0, layer);
        Vtx(b + 1, x + w, y, u1, v0, c0, layer);
        Vtx(b + 2, x + w, y + h, u1, v1, c1, layer);
        Vtx(b + 3, x, y + h, u0, v1, c1, layer);
        _quads++;
    }

    public void Draw(Texture2D t, float x, float y, float w, float h, float u0 = 0, float v0 = 0, float u1 = 1, float v1 = 1, uint color = 0xFFFFFFFF)
    {
        SetTex(t.Handle, false);
        Quad(x, y, w, h, u0, v0, u1, v1, color, color, 0);
    }
    /// <summary>Rysuj fragment tekstury w pikselach (jak MC drawTexturedModalRect).</summary>
    public void DrawPx(Texture2D t, float x, float y, int sx, int sy, int sw, int sh, uint color = 0xFFFFFFFF, float scale = 1)
        => Draw(t, x, y, sw * scale, sh * scale, (float)sx / t.Width, (float)sy / t.Height, (float)(sx + sw) / t.Width, (float)(sy + sh) / t.Height, color);

    public void DrawLayer(TextureArray arr, int layer, float x, float y, float w, float h, uint color = 0xFFFFFFFF, float u0 = 0, float v0 = 0, float u1 = 1, float v1 = 1)
    {
        SetTex(arr.Handle, true);
        Quad(x, y, w, h, u0, v0, u1, v1, color, color, layer);
    }

    public void Fill(float x, float y, float w, float h, uint color)
    {
        SetTex(White.Handle, false);
        Quad(x, y, w, h, 0, 0, 1, 1, color, color, 0);
    }
    public void Gradient(float x, float y, float w, float h, uint top, uint bottom)
    {
        SetTex(White.Handle, false);
        Quad(x, y, w, h, 0, 0, 1, 1, top, bottom, 0);
    }
    public void Rect(float x, float y, float w, float h, uint color, float t = 1)
    {
        Fill(x, y, w, t, color); Fill(x, y + h - t, w, t, color); Fill(x, y, t, h, color); Fill(x + w - t, y, t, h, color);
    }

    public static uint Col(int r, int g, int b, int a = 255) => (uint)(r | (g << 8) | (b << 16) | (a << 24));
    public static uint Hex(uint rgb, int a = 255) => Col((int)(rgb >> 16) & 255, (int)(rgb >> 8) & 255, (int)rgb & 255, a);
    public static uint Alpha(uint c, float a) => (c & 0x00FFFFFF) | ((uint)Math.Clamp((int)((c >> 24) * a), 0, 255) << 24);

    private static readonly uint[] Codes =
    {
        0x000000, 0x0000AA, 0x00AA00, 0x00AAAA, 0xAA0000, 0xAA00AA, 0xFFAA00, 0xAAAAAA,
        0x555555, 0x5555FF, 0x55FF55, 0x55FFFF, 0xFF5555, 0xFF55FF, 0xFFFF55, 0xFFFFFF
    };

    public int Measure(string s, float scale = 1)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int w = 0, max = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '§' && i + 1 < s.Length) { i++; continue; }
            if (c == '\n') { max = Math.Max(max, w); w = 0; continue; }
            w += CharW(c) + 1;
        }
        return (int)(Math.Max(max, w) * scale);
    }
    private int CharW(char c) => _glyphs.TryGetValue(c, out var g) ? (c == ' ' ? 3 : g.w) : (_glyphs.TryGetValue(char.ToLowerInvariant(c), out var g2) ? g2.w : 5);

    public void Text(string s, float x, float y, uint color = 0xFFFFFFFF, bool shadow = true, float scale = 1)
    {
        if (string.IsNullOrEmpty(s)) return;
        if (shadow) TextRaw(s, x + scale, y + scale, color, true, scale);
        TextRaw(s, x, y, color, false, scale);
    }
    public void TextCentered(string s, float cx, float y, uint color = 0xFFFFFFFF, bool shadow = true, float scale = 1)
        => Text(s, cx - Measure(s, scale) / 2f, y, color, shadow, scale);

    private void TextRaw(string s, float x, float y, uint color, bool isShadow, float scale)
    {
        SetTex(FontTex.Handle, false);
        float cx = x; uint col = color;
        float a = (color >> 24) / 255f;
        if (isShadow) col = Shadow(color);
        float tw = FontTex.Width, th = FontTex.Height;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '§' && i + 1 < s.Length)
            {
                char k = char.ToLowerInvariant(s[++i]);
                int idx = "0123456789abcdef".IndexOf(k);
                uint nc = idx >= 0 ? (Hex(Codes[idx], (int)(a * 255))) : color;
                col = isShadow ? Shadow(nc) : nc;
                continue;
            }
            if (c == '\n') { cx = x; y += 10 * scale; continue; }
            if (!_glyphs.TryGetValue(c, out var g) && !_glyphs.TryGetValue(char.ToLowerInvariant(c), out g)) { g = _glyphs['?']; }
            if (c != ' ')
                Quad(cx, y - FontData.Top * scale, FontData.CellW * scale, FontData.CellH * scale,
                    g.x / tw, g.y / th, (g.x + FontData.CellW) / tw, (g.y + FontData.CellH) / th, col, col, 0);
            cx += (CharW(c) + 1) * scale;
        }
    }
    private static uint Shadow(uint c) => Col((int)(c & 255) / 4, (int)((c >> 8) & 255) / 4, (int)((c >> 16) & 255) / 4, (int)(c >> 24));

    public void Flush()
    {
        if (_quads == 0) return;
        _mesh.Upload(_v, _quads * 4 * 9);
        _shader.Use();
        _shader.Set("uProj", _proj);
        _shader.Set("uMode", _curArray ? 1 : 0);
        _shader.Set("uTex", 0); _shader.Set("uArr", 1);
        if (_curArray) { GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture2DArray, _curTex); }
        else { GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, _curTex); }
        _mesh.DrawQuads();
        _quads = 0;
    }

    public void End() => Flush();

    public void Dispose() { _mesh.Dispose(); _shader.Dispose(); White.Dispose(); FontTex.Dispose(); }
}
