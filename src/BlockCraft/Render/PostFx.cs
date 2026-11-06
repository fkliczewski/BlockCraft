using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Bufor HDR świata (RGBA16F + tekstura głębi) i przebieg końcowy: tonemapping ACES, color grading, FXAA.
/// Świat rysuje się do bufora (także w zmniejszonej skali renderu), przebieg końcowy rysuje go na ekran
/// w pełnej rozdzielczości; GUI rysuje się potem bez efektów. Plan: docs/GRAFIKA_PLAN.md.</summary>
public sealed class PostFx : IDisposable
{
    public int Fbo, ColorTex, DepthTex, Width, Height;
    private readonly Shader _final;
    private readonly Mesh _quad;

    private const string QuadVert = @"#version 330 core
layout(location=0) in vec2 aPos; out vec2 vUV;
void main(){ vUV = aPos*0.5+0.5; gl_Position = vec4(aPos,0.0,1.0); }";

    // ACES: przybliżenie Narkowicza na luminancji koloru liniowego (świat liczy światło w gamma, więc pow 2.2 w obie strony);
    // kolor skalowany przez stosunek luminancji, więc odcień zostaje (ACES na kanałach osobno szarzył niebo),
    // a kanał powyżej 1 skaluje cały kolor zamiast go obcinać (obcięty niebieski robił z nieba turkus);
    // wynik dzielony przez wartość dla bieli, żeby biel z ekspozycją 1 została bielą (obraz LDR się nie przyciemnia).
    // Grading: vibrance mocniej podbija kolory blade niż już nasycone.
    // FXAA: uproszczona wersja 3.11 (Lottes) na wartościach po tonemappingu; bez krawędzi nie rozmywa tekstur.
    // Bez polskich znaków w kodzie GLSL: część sterowników nie przyjmuje znaków spoza ASCII nawet w komentarzach.
    private const string FinalFrag = @"#version 330 core
in vec2 vUV; out vec4 o;
uniform sampler2D uScene; uniform vec2 uTexel;
uniform int uTonemap; uniform int uFxaa; uniform float uExposure; uniform float uWhite;
uniform float uSaturation; uniform float uContrast; uniform vec3 uTint; uniform float uVignette;
vec3 aces(vec3 x){ return clamp((x*(2.51*x+0.03))/(x*(2.43*x+0.59)+0.14), 0.0, 1.0); }
vec3 grade(vec3 c){
  c = max(c, 0.0);
  if (uTonemap == 1) {
    vec3 lin = pow(c, vec3(2.2))*uExposure;
    float y = dot(lin, vec3(0.2126,0.7152,0.0722));
    float t = aces(vec3(y)).x/uWhite;
    lin *= t/max(y, 1e-4);
    lin /= max(1.0, max(lin.r, max(lin.g, lin.b)));
    c = pow(lin, vec3(1.0/2.2));
  }
  c = clamp(c, 0.0, 1.0);
  c = clamp((c-0.5)*uContrast+0.5, 0.0, 1.0);
  float l = dot(c, vec3(0.2126,0.7152,0.0722));
  float s = max(c.r, max(c.g, c.b)) - min(c.r, min(c.g, c.b));
  c = mix(vec3(l), c, 1.0 + (uSaturation-1.0)*(1.0-s));
  return clamp(c*uTint, 0.0, 1.0);
}
vec3 tap(vec2 uv){ return grade(texture(uScene, uv).rgb); }
float luma(vec3 c){ return dot(c, vec3(0.299,0.587,0.114)); }
vec3 fxaa(vec2 uv){
  vec3 nw = tap(uv+vec2(-1.0,-1.0)*uTexel), ne = tap(uv+vec2(1.0,-1.0)*uTexel);
  vec3 sw = tap(uv+vec2(-1.0,1.0)*uTexel), se = tap(uv+vec2(1.0,1.0)*uTexel), m = tap(uv);
  float lNW = luma(nw), lNE = luma(ne), lSW = luma(sw), lSE = luma(se), lM = luma(m);
  float lMin = min(lM, min(min(lNW,lNE), min(lSW,lSE))), lMax = max(lM, max(max(lNW,lNE), max(lSW,lSE)));
  if (lMax - lMin < max(0.0312, lMax*0.125)) return m;
  vec2 dir = vec2(-((lNW+lNE)-(lSW+lSE)), (lNW+lSW)-(lNE+lSE));
  float red = max((lNW+lNE+lSW+lSE)*0.03125, 1.0/128.0);
  float inv = 1.0/(min(abs(dir.x), abs(dir.y))+red);
  dir = clamp(dir*inv, vec2(-8.0), vec2(8.0))*uTexel;
  vec3 a = 0.5*(tap(uv+dir*(1.0/3.0-0.5)) + tap(uv+dir*(2.0/3.0-0.5)));
  vec3 b = a*0.5 + 0.25*(tap(uv-dir*0.5) + tap(uv+dir*0.5));
  float lB = luma(b);
  return (lB < lMin || lB > lMax) ? a : b;
}
void main(){
  vec3 c = uFxaa == 1 ? fxaa(vUV) : tap(vUV);
  vec2 d = vUV-0.5; c *= 1.0 - uVignette*dot(d, d)*2.0;
  o = vec4(c, 1.0);
}";

    public PostFx()
    {
        try
        {
            _final = new Shader(QuadVert, FinalFrag, "postfx");
            _quad = new Mesh(8, () => { GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, 0); });
            _quad.Upload(new float[] { -1, 1, -1, -1, 1, -1, 1, 1 }, 8, BufferUsageHint.StaticDraw);
        }
        catch
        {
            _quad?.Dispose();
            _final?.Dispose();
            throw;
        }
    }

    /// <summary>Podpina bufor HDR o rozmiarze (w,h) (tworzy lub zmienia rozmiar) i ustawia viewport.</summary>
    public void Bind(int w, int h)
    {
        if (Fbo == 0 || Width != w || Height != h) Create(w, h);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        GL.Viewport(0, 0, w, h);
    }

    private void Create(int w, int h)
    {
        Release();
        Width = w; Height = h;
        Fbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Fbo);
        ColorTex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, ColorTex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, w, h, 0, PixelFormat.Rgba, PixelType.HalfFloat, IntPtr.Zero);
        SetSampling(TextureMinFilter.Linear, TextureMagFilter.Linear);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorTex, 0);
        // głębia jako tekstura: następne etapy (mgła, wolumetria, woda) czytają ją w przebiegach ekranowych
        DepthTex = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, DepthTex);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Depth24Stencil8, w, h, 0, PixelFormat.DepthStencil, PixelType.UnsignedInt248, IntPtr.Zero);
        SetSampling(TextureMinFilter.Nearest, TextureMagFilter.Nearest);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, TextureTarget.Texture2D, DepthTex, 0);
        var st = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (st != FramebufferErrorCode.FramebufferComplete) throw new Exception("PostFx: bufor HDR niekompletny: " + st);
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private static void SetSampling(TextureMinFilter min, TextureMagFilter mag)
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)min);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)mag);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    /// <summary>Przebieg końcowy: bufor HDR → ekran (fw×fh) z tonemappingiem, gradingiem i FXAA wg ustawień.</summary>
    public void Present(int fw, int fh, Settings s)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, fw, fh);
        GL.Disable(EnableCap.DepthTest); GL.Disable(EnableCap.Blend); GL.Disable(EnableCap.CullFace);
        _final.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, ColorTex);
        _final.Set("uScene", 0);
        _final.Set("uTexel", new Vector2(1f / Width, 1f / Height));
        _final.Set("uTonemap", s.Tonemapping ? 1 : 0);
        _final.Set("uFxaa", s.Fxaa ? 1 : 0);
        const float exposure = 1.0f;
        _final.Set("uExposure", exposure);
        _final.Set("uWhite", Aces(exposure));
        bool vivid = s.VividColors;
        _final.Set("uSaturation", vivid ? 1.25f : 1f);
        _final.Set("uContrast", vivid ? 1.06f : 1f);
        _final.Set("uTint", vivid ? new Vector3(1.02f, 1.0f, 0.985f) : Vector3.One); // lekko ciepłe światło
        _final.Set("uVignette", vivid ? 0.35f : 0f);
        _quad.DrawQuads();
        GL.BindTexture(TextureTarget.Texture2D, 0);
    }

    private static float Aces(float x) => Math.Clamp(x * (2.51f * x + 0.03f) / (x * (2.43f * x + 0.59f) + 0.14f), 0f, 1f);

    private void Release()
    {
        if (Fbo == 0) return;
        GL.DeleteFramebuffer(Fbo); GL.DeleteTexture(ColorTex); GL.DeleteTexture(DepthTex);
        Fbo = ColorTex = DepthTex = 0;
    }

    public void Dispose() { Release(); _final.Dispose(); _quad.Dispose(); }
}
