using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Zapalone TNT rysowane instancjami: jedna siatka sześcianu (ta sama co RenderBlockAt), na instancję 24 bajty
/// (pozycja względem kamery, skala lontu, błysk, światło nieba). Przy 100 000 TNT to jeden draw call zamiast
/// 2,4 mln wierzchołków przeliczanych na CPU i wysyłanych co klatkę.</summary>
public sealed class TntInstancer : IDisposable
{
    private const int InstFloats = 6;
    private readonly Shader _sh;
    private readonly int _vao, _vbo, _ibo;
    private readonly int _cubeVerts;
    private float[] _inst = new float[InstFloats * 1024];
    private int _count;

    private const string Vert = @"#version 330 core
layout(location=0) in vec3 aPos; layout(location=1) in vec2 aUV; layout(location=2) in float aLayer; layout(location=3) in float aShade;
layout(location=7) in vec4 iPosScale; layout(location=8) in vec2 iFlashSky;
uniform mat4 uViewProj; out vec3 vUV; out float vShade; out vec2 vLight; out float vFlash; out float vDist;
void main(){ vec3 p = aPos * iPosScale.w + iPosScale.xyz + vec3(0.0, 0.5, 0.0);
 gl_Position = uViewProj*vec4(p,1.0); vUV=vec3(aUV,aLayer); vShade=aShade; vLight=vec2(iFlashSky.y, 15.0); vFlash=iFlashSky.x; vDist=length(p.xz); }";
    private static readonly string Frag = @"#version 330 core
in vec3 vUV; in float vShade; in vec2 vLight; in float vFlash; in float vDist;
uniform sampler2DArray uTex; uniform vec3 uFogColor; uniform float uFogStart; uniform float uFogEnd; uniform float uFogDensity; uniform int uFogMode;
out vec4 o;" + EntityRenderer.LightmapGlsl + @"
void main(){ vec4 t = texture(uTex, vUV); if(t.a < 0.1) discard;
 vec3 c = t.rgb * vShade * lightmap(vLight.x, vLight.y); c = mix(c, vec3(1.0), 0.6 * vFlash);
 float fog = uFogMode==1 ? 1.0-exp(-uFogDensity*vDist) : clamp((vDist-uFogStart)/(uFogEnd-uFogStart),0.0,1.0);
 o = vec4(mix(c, uFogColor, fog), t.a); }";

    public Shader Shader => _sh;
    public int Count => _count;

    /// <summary>er: renderer encji, z którego bierzemy siatkę bloku TNT (RenderBlockAt w środku 0,0,0).</summary>
    public TntInstancer(EntityRenderer er)
    {
        _sh = new Shader(Vert, Frag, "tnt-inst");
        var b = er.Terrain;
        int start = b.Verts;
        er.RenderBlockAt(B.Tnt, 0, Matrix4.Identity);
        _cubeVerts = b.Verts - start;
        var cube = new float[_cubeVerts * ModelBatch.F];
        Array.Copy(b.D, start * ModelBatch.F, cube, 0, cube.Length);
        b.Verts = start;

        _vao = GL.GenVertexArray(); _vbo = GL.GenBuffer(); _ibo = GL.GenBuffer();
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, cube.Length * 4, cube, BufferUsageHint.StaticDraw);
        int s = ModelBatch.F * 4;
        GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, s, 0);
        GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, s, 12);
        GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, s, 20);
        GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, s, 24);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _ibo);
        GL.BufferData(BufferTarget.ArrayBuffer, _inst.Length * 4, IntPtr.Zero, BufferUsageHint.StreamDraw);
        int si = InstFloats * 4;
        GL.EnableVertexAttribArray(7); GL.VertexAttribPointer(7, 4, VertexAttribPointerType.Float, false, si, 0); GL.VertexAttribDivisor(7, 1);
        GL.EnableVertexAttribArray(8); GL.VertexAttribPointer(8, 2, VertexAttribPointerType.Float, false, si, 16); GL.VertexAttribDivisor(8, 1);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, SharedQuadIndices.Ebo);
        GL.BindVertexArray(0);
    }

    public void Clear() => _count = 0;

    public void Add(Vector3 pos, float scale, bool flash, float sky)
    {
        if ((_count + 1) * InstFloats > _inst.Length) Array.Resize(ref _inst, _inst.Length * 2);
        int o = _count * InstFloats;
        _inst[o] = pos.X; _inst[o + 1] = pos.Y; _inst[o + 2] = pos.Z; _inst[o + 3] = scale;
        _inst[o + 4] = flash ? 1f : 0f; _inst[o + 5] = sky;
        _count++;
    }

    /// <summary>Rysuje zebrane instancje; shader ma już ustawione uViewProj, mgłę i lightmapę (przez WorldRenderer).</summary>
    public unsafe void Draw(TextureArray terrain)
    {
        if (_count == 0) return;
        GL.BindBuffer(BufferTarget.ArrayBuffer, _ibo);
        int bytes = _count * InstFloats * 4;
        GL.BufferData(BufferTarget.ArrayBuffer, Math.Max(bytes, _inst.Length * 4), IntPtr.Zero, BufferUsageHint.StreamDraw); // osierocenie bufora
        fixed (float* p = _inst) GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, (IntPtr)p);
        terrain.Bind(0); _sh.Set("uTex", 0);
        GL.BindVertexArray(_vao);
        SharedQuadIndices.Ensure(_cubeVerts / 4);
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, SharedQuadIndices.Ebo);
        GL.DrawElementsInstanced(PrimitiveType.Triangles, _cubeVerts / 4 * 6, DrawElementsType.UnsignedInt, IntPtr.Zero, _count);
        GL.BindVertexArray(0);
        _count = 0;
    }

    public void Dispose() { GL.DeleteBuffer(_vbo); GL.DeleteBuffer(_ibo); GL.DeleteVertexArray(_vao); _sh.Dispose(); }
}
