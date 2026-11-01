using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Batch wierzchołków dla encji: pos3 uv2 layer1 shade1 light2 color4 overlay4 = 17 floatów.</summary>
public sealed class ModelBatch
{
    public const int F = 17;
    public float[] D = new float[F * 4 * 4096];
    public int Verts;
    public float Sky = 15, Blk = 15;
    public Vector4 Color = Vector4.One, Overlay = Vector4.Zero;
    public int Layer;
    private static readonly Vector3 L0 = Vector3.Normalize(new Vector3(0.2f, 1f, -0.7f)), L1 = Vector3.Normalize(new Vector3(-0.2f, 1f, 0.7f));
    public bool FlatShade;

    public void Clear() => Verts = 0;

    public void V(Vector3 p, float u, float v, float shade)
    {
        int o = Verts * F;
        if (o + F > D.Length) Array.Resize(ref D, D.Length * 2);
        D[o] = p.X; D[o + 1] = p.Y; D[o + 2] = p.Z; D[o + 3] = u; D[o + 4] = v; D[o + 5] = Layer; D[o + 6] = shade;
        D[o + 7] = Sky; D[o + 8] = Blk;
        D[o + 9] = Color.X; D[o + 10] = Color.Y; D[o + 11] = Color.Z; D[o + 12] = Color.W;
        D[o + 13] = Overlay.X; D[o + 14] = Overlay.Y; D[o + 15] = Overlay.Z; D[o + 16] = Overlay.W;
        Verts++;
    }

    public float ShadeFor(Vector3 n)
    {
        if (FlatShade) return 1f;
        float s = 0.4f + 0.6f * (Math.Max(0, Vector3.Dot(n, L0)) + Math.Max(0, Vector3.Dot(n, L1)));
        return Math.Min(1f, s);
    }

    /// <summary>Czworokąt (a,b,c,d) w kolejności CCW, uv (u0,v0)-(u1,v1).</summary>
    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float v0, float u1, float v1, float shade)
    {
        V(a, u0, v0, shade); V(b, u0, v1, shade); V(c, u1, v1, shade); V(d, u1, v0, shade);
    }
}

public sealed partial class EntityRenderer : IDisposable
{
    public readonly Shader Shader;
    private readonly Mesh _mesh;
    public readonly ModelBatch Skin = new(), Terrain = new();
    public TextureArray SkinTex;
    public TextureArray TerrainTex;

    // End: światło przycięte do 1 przed pow(1-c), inaczej przy świetle 15 pow z ujemną podstawą daje NaN (czarne)
    public const string LightmapGlsl = @"
uniform float uSun; uniform float uGamma; uniform int uDimension;
float tbl(float l){ float f1 = 1.0 - clamp(l,0.0,15.0)/15.0; float a = uDimension == -1 ? 0.1 : 0.0; return (1.0 - f1)/(f1*3.0 + 1.0)*(1.0-a)+a; }
vec3 lightmap(float sky, float blk){
  float f = uSun; float f1 = f*0.95+0.05;
  float f2 = tbl(sky)*f1; float f3 = tbl(blk)*1.5;
  vec3 c = vec3(f2*(f*0.65+0.35), f2*(f*0.65+0.35), f2) + vec3(f3, f3*((f3*0.6+0.4)*0.6+0.4), f3*(f3*f3*0.6+0.4));
  c = clamp(c*0.96+0.03, 0.0, 1.0);
  if (uDimension == 1) c = clamp(vec3(0.22,0.28,0.25) + vec3(f3*0.75), 0.0, 1.0);
  vec3 g = 1.0 - pow(1.0 - c, vec3(4.0)); c = mix(c, g, uGamma);
  return clamp(c*0.96+0.03, 0.0, 1.0);
}";

    private const string Vert = @"#version 330 core
layout(location=0) in vec3 aPos; layout(location=1) in vec2 aUV; layout(location=2) in float aLayer; layout(location=3) in float aShade;
layout(location=4) in vec2 aLight; layout(location=5) in vec4 aCol; layout(location=6) in vec4 aOver;
uniform mat4 uViewProj; out vec3 vUV; out float vShade; out vec2 vLight; out vec4 vCol; out vec4 vOver; out float vDist;
void main(){ gl_Position = uViewProj*vec4(aPos,1.0); vUV=vec3(aUV,aLayer); vShade=aShade; vLight=aLight; vCol=aCol; vOver=aOver; vDist=length(aPos.xz); }";
    private static readonly string Frag = @"#version 330 core
in vec3 vUV; in float vShade; in vec2 vLight; in vec4 vCol; in vec4 vOver; in float vDist;
uniform sampler2DArray uTex; uniform vec3 uFogColor; uniform float uFogStart; uniform float uFogEnd; uniform float uFogDensity; uniform int uFogMode;
out vec4 o;" + LightmapGlsl + @"
void main(){ vec4 t = texture(uTex, vUV); if(t.a < 0.1) discard; t *= vCol;
 vec3 c = t.rgb * vShade * lightmap(vLight.x, vLight.y); c = mix(c, vOver.rgb, vOver.a);
 float fog = uFogMode==1 ? 1.0-exp(-uFogDensity*vDist) : clamp((vDist-uFogStart)/(uFogEnd-uFogStart),0.0,1.0);
 o = vec4(mix(c, uFogColor, fog), t.a); }";

    public EntityRenderer()
    {
        Shader = new Shader(Vert, Frag, "entity");
        _mesh = new Mesh(ModelBatch.F * 4, () =>
        {
            int s = ModelBatch.F * 4;
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, s, 0);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, s, 12);
            GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 1, VertexAttribPointerType.Float, false, s, 20);
            GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, s, 24);
            GL.EnableVertexAttribArray(4); GL.VertexAttribPointer(4, 2, VertexAttribPointerType.Float, false, s, 28);
            GL.EnableVertexAttribArray(5); GL.VertexAttribPointer(5, 4, VertexAttribPointerType.Float, false, s, 36);
            GL.EnableVertexAttribArray(6); GL.VertexAttribPointer(6, 4, VertexAttribPointerType.Float, false, s, 52);
        });
        SkinGen.Ensure();
        SkinTex = new TextureArray(SkinGen.TexSize, SkinGen.Layers(), false);
    }

    public void Flush(ModelBatch b, TextureArray tex)
    {
        if (b.Verts == 0) return;
        _mesh.Upload(b.D, b.Verts * ModelBatch.F);
        tex.Bind(0);
        Shader.Set("uTex", 0);
        _mesh.DrawQuads();
        b.Clear();
    }

    // ================= modele =================
    private static ModelPart P(float x, float y, float z) => new(x, y, z);

    private readonly ModelPart _head = P(0, 0, 0).Box(0, 0, -4, -8, -4, 8, 8, 8), _body = P(0, 0, 0).Box(16, 16, -4, 0, -2, 8, 12, 4);
    private readonly ModelPart _rArm = P(-5, 2, 0).Box(40, 16, -3, -2, -2, 4, 12, 4), _lArm = P(5, 2, 0).Box(40, 16, -1, -2, -2, 4, 12, 4, 0, true);
    private readonly ModelPart _rLeg = P(-1.9f, 12, 0).Box(0, 16, -2, 0, -2, 4, 12, 4), _lLeg = P(1.9f, 12, 0).Box(0, 16, -2, 0, -2, 4, 12, 4, 0, true);
    private readonly ModelPart _sRArm = P(-5, 2, 0).Box(40, 16, -1, -2, -1, 2, 12, 2), _sLArm = P(5, 2, 0).Box(40, 16, -1, -2, -1, 2, 12, 2, 0, true);
    private readonly ModelPart _sRLeg = P(-2, 12, 0).Box(0, 16, -1, 0, -1, 2, 12, 2), _sLLeg = P(2, 12, 0).Box(0, 16, -1, 0, -1, 2, 12, 2, 0, true);
    private readonly ModelPart _cHead = P(0, 6, 0).Box(0, 0, -4, -8, -4, 8, 8, 8), _cBody = P(0, 6, 0).Box(16, 16, -4, 0, -2, 8, 12, 4);
    private readonly ModelPart[] _cLegs = { P(-2, 18, 4).Box(0, 16, -2, 0, -2, 4, 6, 4), P(2, 18, 4).Box(0, 16, -2, 0, -2, 4, 6, 4), P(-2, 18, -4).Box(0, 16, -2, 0, -2, 4, 6, 4), P(2, 18, -4).Box(0, 16, -2, 0, -2, 4, 6, 4) };
    private readonly ModelPart _spHead = P(0, 15, -3).Box(32, 4, -4, -4, -8, 8, 8, 8), _spNeck = P(0, 15, 0).Box(0, 0, -3, -3, -3, 6, 6, 6), _spBody = P(0, 15, 9).Box(0, 12, -5, -4, -6, 10, 8, 12);
    private readonly ModelPart[] _spLegs = new ModelPart[8];
    private readonly ModelPart _eHead = P(0, -14, 0).Box(0, 0, -4, -8, -4, 8, 8, 8), _eBody = P(0, -14, 0).Box(32, 16, -4, 0, -2, 8, 12, 4);
    private readonly ModelPart _eRArm = P(-3, -12, 0).Box(56, 0, -1, -2, -1, 2, 30, 2), _eLArm = P(5, -12, 0).Box(56, 0, -1, -2, -1, 2, 30, 2, 0, true);
    private readonly ModelPart _eRLeg = P(-2, -5, 0).Box(56, 0, -1, 0, -1, 2, 30, 2), _eLLeg = P(2, -5, 0).Box(56, 0, -1, 0, -1, 2, 30, 2, 0, true);
    private readonly ModelPart _cowHead = P(0, 4, -8).Box(0, 0, -4, -4, -6, 8, 8, 6).Box(22, 0, -5, -5, -4, 1, 3, 1).Box(22, 0, 4, -5, -4, 1, 3, 1);
    private readonly ModelPart _cowBody = P(0, 5, 2).Box(18, 4, -6, -10, -7, 12, 18, 10).Box(52, 0, -2, 2, -8, 4, 6, 1);
    private readonly ModelPart[] _cowLegs = { P(-4, 12, 7).Box(0, 16, -2, 0, -2, 4, 12, 4), P(4, 12, 7).Box(0, 16, -2, 0, -2, 4, 12, 4), P(-4, 12, -6).Box(0, 16, -2, 0, -2, 4, 12, 4), P(4, 12, -6).Box(0, 16, -2, 0, -2, 4, 12, 4) };
    private readonly ModelPart _pigHead = P(0, 12, -6).Box(0, 0, -4, -4, -8, 8, 8, 8).Box(16, 16, -2, 0, -9, 4, 3, 1);
    private readonly ModelPart _pigBody = P(0, 11, 2).Box(28, 8, -5, -10, -7, 10, 16, 8);
    private readonly ModelPart[] _pigLegs = { P(-3, 18, 7).Box(0, 16, -2, 0, -2, 4, 6, 4), P(3, 18, 7).Box(0, 16, -2, 0, -2, 4, 6, 4), P(-3, 18, -5).Box(0, 16, -2, 0, -2, 4, 6, 4), P(3, 18, -5).Box(0, 16, -2, 0, -2, 4, 6, 4) };
    private readonly ModelPart _shHead = P(0, 6, -8).Box(0, 0, -3, -4, -6, 6, 6, 8), _shBody = P(0, 5, 2).Box(28, 8, -4, -10, -7, 8, 16, 6);
    private readonly ModelPart[] _shLegs = { P(-3, 12, 7).Box(0, 16, -2, 0, -2, 4, 12, 4), P(3, 12, 7).Box(0, 16, -2, 0, -2, 4, 12, 4), P(-3, 12, -5).Box(0, 16, -2, 0, -2, 4, 12, 4), P(3, 12, -5).Box(0, 16, -2, 0, -2, 4, 12, 4) };
    private readonly ModelPart _fHead = P(0, 6, -8).Box(0, 0, -3, -4, -4, 6, 6, 6, 0.6f), _fBody = P(0, 5, 2).Box(28, 8, -4, -10, -7, 8, 16, 6, 1.75f);
    private readonly ModelPart[] _fLegs = { P(-3, 12, 7).Box(0, 16, -2, 0, -2, 4, 6, 4, 0.5f), P(3, 12, 7).Box(0, 16, -2, 0, -2, 4, 6, 4, 0.5f), P(-3, 12, -5).Box(0, 16, -2, 0, -2, 4, 6, 4, 0.5f), P(3, 12, -5).Box(0, 16, -2, 0, -2, 4, 6, 4, 0.5f) };
    private readonly ModelPart _chHead = P(0, 15, -4).Box(0, 0, -2, -6, -2, 4, 6, 3).Box(14, 0, -2, -4, -4, 4, 2, 2).Box(14, 4, -1, -2, -3, 2, 2, 2);
    private readonly ModelPart _chBody = P(0, 16, 0).Box(0, 9, -3, -4, -3, 6, 8, 6);
    private readonly ModelPart _chRLeg = P(-2, 19, 1).Box(26, 0, -1, 0, -3, 3, 5, 3), _chLLeg = P(1, 19, 1).Box(26, 0, -1, 0, -3, 3, 5, 3);
    private readonly ModelPart _chRWing = P(-4, 13, 0).Box(24, 13, 0, 0, -3, 1, 4, 6), _chLWing = P(4, 13, 0).Box(24, 13, -1, 0, -3, 1, 4, 6);

    private void InitSpider()
    {
        if (_spLegs[0] != null) return;
        float[] zs = { 2, 2, 1, 1, 0, 0, -1, -1 };
        for (int i = 0; i < 8; i++)
        {
            bool right = i % 2 == 0;
            _spLegs[i] = P(right ? -4 : 4, 15, zs[i]).Box(18, 0, right ? -15 : -1, -1, -1, 16, 2, 2);
        }
    }

    // ================= rysowanie części =================
    private static Matrix4 PartMatrix(ModelPart p, Matrix4 parent)
    {
        var m = Matrix4.Identity;
        if (p.RX != 0) m *= Matrix4.CreateRotationX(p.RX);
        if (p.RY != 0) m *= Matrix4.CreateRotationY(p.RY);
        if (p.RZ != 0) m *= Matrix4.CreateRotationZ(p.RZ);
        m *= Matrix4.CreateTranslation(p.PX / 16f, p.PY / 16f, p.PZ / 16f);
        return m * parent;
    }

    public void RenderPart(ModelBatch b, ModelPart p, Matrix4 parent, int texW = 64, int texH = 64)
    {
        if (!p.Visible) return;
        var m = PartMatrix(p, parent);
        foreach (var box in p.Boxes) RenderBox(b, box, m, texW, texH);
        foreach (var c in p.Children) RenderPart(b, c, m, texW, texH);
    }

    private static Vector3 T(Matrix4 m, float x, float y, float z) => Vector3.TransformPosition(new Vector3(x, y, z), m);

    private void RenderBox(ModelBatch b, BoxDef bx, Matrix4 m, int tw, int th)
    {
        float i = bx.Inflate;
        float x0 = (bx.X - i) / 16f, y0 = (bx.Y - i) / 16f, z0 = (bx.Z - i) / 16f;
        float x1 = (bx.X + bx.W + i) / 16f, y1 = (bx.Y + bx.H + i) / 16f, z1 = (bx.Z + bx.D + i) / 16f;
        if (bx.Mirror) (x0, x1) = (x1, x0);
        int u = bx.U, v = bx.V; float w = bx.W, h = bx.H, d = bx.D;
        Vector3 p000 = T(m, x0, y0, z0), p100 = T(m, x1, y0, z0), p010 = T(m, x0, y1, z0), p110 = T(m, x1, y1, z0);
        Vector3 p001 = T(m, x0, y0, z1), p101 = T(m, x1, y0, z1), p011 = T(m, x0, y1, z1), p111 = T(m, x1, y1, z1);
        float U(float px) => px / tw; float Vv(float py) => py / th;
        void Face(Vector3 a, Vector3 bb, Vector3 c, Vector3 dd, float fu, float fv, float fw, float fh)
        {
            var n = Vector3.Cross(bb - a, dd - a);
            if (n.LengthSquared > 1e-12f) n.Normalize();
            float s = b.ShadeFor(n);
            float u0 = U(fu), v0 = Vv(fv), u1 = U(fu + fw), v1 = Vv(fv + fh);
            if (bx.Mirror) (u0, u1) = (u1, u0);
            // a: (u0,v0) b: (u0,v1) c: (u1,v1) d: (u1,v0)
            b.V(a, u0, v0, s); b.V(bb, u0, v1, s); b.V(c, u1, v1, s); b.V(dd, u1, v0, s);
        }
        // y w dół w modelu: y0 = góra
        Face(p000, p010, p110, p100, u + d, v + d, w, h);                     // przód (-Z)
        Face(p101, p111, p011, p001, u + d + w + d, v + d, w, h);             // tył (+Z)
        Face(p001, p011, p010, p000, u, v + d, d, h);                         // prawa (-X)
        Face(p100, p110, p111, p101, u + d + w, v + d, d, h);                 // lewa (+X)
        Face(p001, p000, p100, p101, u + d, v, w, d);                         // góra (y0)
        Face(p010, p011, p111, p110, u + d + w, v, w, d);                     // dół (y1)
    }

    /// <summary>Macierz bazowa encji: T(0,-1.5) S(-s,-s,s) RotY(180-yaw) T(pos).</summary>
    public static Matrix4 EntityRoot(Vector3 pos, float bodyYaw, float scale = 1f)
        => Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-scale, -scale, scale)
           * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180f - bodyYaw)) * Matrix4.CreateTranslation(pos);

    // ================= encje =================
    public void RenderEntity(Entity e, float pt, Vector3 cam, World.World w, bool firstPersonSelf)
    {
        var pos = new Vector3((float)(e.IX(pt) - cam.X), (float)(e.IY(pt) - cam.Y), (float)(e.IZ(pt) - cam.Z));
        int bx = MathUtil.FloorToInt(e.IX(pt)), by = MathUtil.FloorToInt(e.IY(pt) + e.Height * 0.5), bz = MathUtil.FloorToInt(e.IZ(pt));
        float sky = w.GetSkyLight(bx, by, bz), blk = w.GetBlockLight(bx, by, bz);
        if (e.FireTicks > 0) blk = 15;
        Skin.Sky = Terrain.Sky = sky; Skin.Blk = Terrain.Blk = blk;
        Skin.Color = Terrain.Color = Vector4.One; Skin.Overlay = Terrain.Overlay = Vector4.Zero;
        if (FindCustom(Custom, e.GetType()) is { } custom) { custom(this, e, pt, pos, cam); return; }
        switch (e)
        {
            case LivingEntity le: RenderLiving(le, pt, pos); if (le is Mob { LeashHolder: not null } lm) RenderLeash(lm, pt, pos, cam); break;
            case ItemEntity ie: RenderItemEntity(ie, pt, pos); break;
            case XpOrbEntity xp: RenderBillboardSprite(TextureGen.Layer("particle_crit"), pos + new Vector3(0, 0.25f, 0), 0.3f, new Vector4(0.6f, 1f, 0.3f, 1f), cam); break;
            case FallingBlockEntity fb: RenderBlockAt(fb.BlockId, fb.Meta, Matrix4.CreateTranslation(pos + new Vector3(0, 0.5f, 0))); break;
            case TntEntity tnt:
                {
                    float f = 1f;
                    if (tnt.Fuse < 10) { f = 1f + (1f - Math.Max(0, tnt.Fuse - pt + 1) / 10f) * 0.3f; }
                    if ((tnt.Fuse / 5) % 2 == 0) Terrain.Overlay = new Vector4(1, 1, 1, 0.6f);
                    Terrain.Blk = 15;
                    RenderBlockAt(B.Tnt, 0, Matrix4.CreateScale(f) * Matrix4.CreateTranslation(pos + new Vector3(0, 0.5f, 0)));
                    Terrain.Overlay = Vector4.Zero;
                    break;
                }
            case ArrowEntity ar: RenderArrow(ar, pt, pos); break;
            case ThrownEntity th: RenderBillboardSprite(TextureGen.Layer(th.Kind == 0 ? "item_ender_pearl" : "item_egg"), pos + new Vector3(0, 0.1f, 0), 0.25f, Vector4.One, cam); break;
        }
    }

    private Vector3 _camRight = Vector3.UnitX, _camUp = Vector3.UnitY;
    public void SetCameraAxes(Vector3 right, Vector3 up) { _camRight = right; _camUp = up; }

    public void RenderBillboardSprite(int layer, Vector3 center, float size, Vector4 color, Vector3 cam)
    {
        Terrain.Layer = layer; Terrain.Color = color;
        var r = _camRight * size * 0.5f; var u = _camUp * size * 0.5f;
        Terrain.Quad(center - r + u, center - r - u, center + r - u, center + r + u, 0, 0, 1, 1, 1f);
        Terrain.Color = Vector4.One;
    }

    private void RenderArrow(ArrowEntity a, float pt, Vector3 pos)
    {
        float yaw = MathUtil.LerpAngle(a.PrevYaw, a.Yaw, pt), pitch = MathHelper.Lerp(a.PrevPitch, a.Pitch, pt);
        var m = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-45))
                * Matrix4.CreateScale(0.7f) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-pitch)) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(yaw)) * Matrix4.CreateTranslation(pos);
        // strzała: płaski sprite w dwóch płaszczyznach
        Terrain.Layer = TextureGen.Layer("item_arrow");
        var m2 = Matrix4.CreateRotationX(MathHelper.PiOver2) * m;
        foreach (var mm in new[] { m, m2 })
        {
            Vector3 a0 = T(mm, -0.5f, 0.5f, 0), b0 = T(mm, -0.5f, -0.5f, 0), c0 = T(mm, 0.5f, -0.5f, 0), d0 = T(mm, 0.5f, 0.5f, 0);
            Terrain.Quad(a0, b0, c0, d0, 0, 0, 1, 1, 0.9f);
            Terrain.Quad(d0, c0, b0, a0, 1, 0, 0, 1, 0.9f);
        }
    }

    private void RenderItemEntity(ItemEntity ie, float pt, Vector3 pos)
    {
        float age = ie.Age + pt;
        float bob = MathF.Sin(age / 10f + ie.Bob) * 0.1f + 0.1f;
        float rot = (age / 20f + ie.Bob) * MathHelper.RadiansToDegrees(1);
        var it = ie.Stack.Item;
        if (it == null) return;
        bool block = IsBlockModel(it);
        float scale = block ? 0.25f : 0.5f;
        int copies = ie.Stack.Count > 20 ? 3 : ie.Stack.Count > 1 ? 2 : 1;
        for (int c = 0; c < copies; c++)
        {
            var off = c == 0 ? Vector3.Zero : new Vector3((c * 0.37f % 0.2f) - 0.1f, c * 0.06f, ((c * 0.71f) % 0.2f) - 0.1f);
            var m = Matrix4.CreateScale(scale) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(rot)) * Matrix4.CreateTranslation(pos + off + new Vector3(0, bob + scale * 0.5f, 0));
            RenderItem(ie.Stack, m);
        }
    }

    public static bool IsBlockModel(Item it)
    {
        if (it.Id >= 256 || it.Sprite > 0 || it.SpriteFn != null) return false;
        var b = Block.ById[it.BlockId];
        return b.Render is RenderType.Cube or RenderType.Model or RenderType.Cactus;
    }

    /// <summary>Renderuje przedmiot wycentrowany w (0,0,0) o rozmiarze 1 w macierzy m.</summary>
    public void RenderItem(ItemStack s, Matrix4 m)
    {
        var it = s.Item;
        if (it == null) return;
        if (IsBlockModel(it)) RenderBlockAt(it.BlockId, s.Damage, m);
        else RenderSpriteExtruded(it.GetSprite(s), m);
    }

    private sealed class NullAccess : IBlockAccess { public byte GetBlock(int x, int y, int z) => 0; public int GetMeta(int x, int y, int z) => 0; }
    private static readonly NullAccess Null = new();
    private readonly List<ModelBox> _mb = new();

    public void RenderBlockAt(int id, int meta, Matrix4 m)
    {
        var b = Block.Get(id);
        _mb.Clear();
        if (b.Render == RenderType.Model)
        {
            int mm = id == B.Chest || id == B.Furnace ? 0 : meta;
            if (b is DoorBlock) mm = 0;
            b.GetModel(Null, 0, 0, 0, mm, _mb);
            if (b is LadderBlock) { _mb.Clear(); }
        }
        if (_mb.Count == 0)
        {
            var t = new int[6];
            int fm = id == B.Furnace || id == B.FurnaceLit || id == B.Chest ? 0 : meta;
            for (int f = 0; f < 6; f++) t[f] = b.GetTexture(f, fm);
            if (id == B.Grass) { t[1] = TextureGen.Layer("grass_top_item"); for (int f = 2; f < 6; f++) t[f] = TextureGen.Layer("grass_side_item"); }
            if (b.Render == RenderType.Cactus) { _mb.Add(new ModelBox(1 / 16f, 0, 1 / 16f, 15 / 16f, 1, 15 / 16f, t) { Uv = Full() }); }
            else _mb.Add(new ModelBox(0, 0, 0, 1, 1, 1, t));
        }
        var col = Terrain.Color;
        if (b.Tint == TintType.Foliage) Terrain.Color = HexColor(0x59AE30);
        else if (b.Tint == TintType.Birch) Terrain.Color = HexColor(0x80A755);
        var off = Matrix4.CreateTranslation(-0.5f, -0.5f, -0.5f) * m;
        foreach (var box in _mb) EmitModelBox(box, off);
        Terrain.Color = col;
    }

    private static float[] Full() { var u = new float[24]; for (int f = 0; f < 6; f++) { u[f * 4 + 2] = 1; u[f * 4 + 3] = 1; } return u; }
    public static Vector4 HexColor(uint c) => new(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, 1);

    internal void EmitModelBox(ModelBox mb, Matrix4 m)
    {
        float x0 = mb.X0, y0 = mb.Y0, z0 = mb.Z0, x1 = mb.X1, y1 = mb.Y1, z1 = mb.Z1;
        void Face(int f, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u0, float v0, float u1, float v1)
        {
            if (mb.Uv != null) { u0 = mb.Uv[f * 4]; v0 = mb.Uv[f * 4 + 1]; u1 = mb.Uv[f * 4 + 2]; v1 = mb.Uv[f * 4 + 3]; }
            Terrain.Layer = mb.Tex[f];
            var pa = T(m, a.X, a.Y + a.X * mb.SlopeX + a.Z * mb.SlopeZ, a.Z); var pb = T(m, b.X, b.Y + b.X * mb.SlopeX + b.Z * mb.SlopeZ, b.Z);
            var pc = T(m, c.X, c.Y + c.X * mb.SlopeX + c.Z * mb.SlopeZ, c.Z); var pd = T(m, d.X, d.Y + d.X * mb.SlopeX + d.Z * mb.SlopeZ, d.Z);
            var n = Vector3.Cross(pb - pa, pd - pa); if (n.LengthSquared > 1e-12f) n.Normalize();
            float s = Terrain.ShadeFor(n);
            Terrain.Quad(pa, pb, pc, pd, u0, v0, u1, v1, s);
        }
        // patrząc z zewnątrz CCW: a(lewy-góra) b(lewy-dół) c(prawy-dół) d(prawy-góra)
        Face(2, new(x1, y1, z0), new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), 1 - x1, 1 - y1, 1 - x0, 1 - y0); // N
        Face(3, new(x0, y1, z1), new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), x0, 1 - y1, x1, 1 - y0);         // S
        Face(4, new(x0, y1, z0), new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), z0, 1 - y1, z1, 1 - y0);         // W
        Face(5, new(x1, y1, z1), new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), 1 - z1, 1 - y1, 1 - z0, 1 - y0); // E
        Face(1, new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), x0, z0, x1, z1);                 // góra
        Face(0, new(x0, y0, z1), new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), x0, 1 - z1, x1, 1 - z0);         // dół
    }

    // ---- wyciągnięty sprite (przedmioty 3D jak w MC) ----
    private readonly Dictionary<int, List<(int x, int y, int dir)>> _edges = new();
    private List<(int x, int y, int dir)> Edges(int layer)
    {
        if (_edges.TryGetValue(layer, out var l)) return l;
        l = new();
        var px = TextureGen.Textures[Math.Clamp(layer, 0, TextureGen.Count - 1)];
        bool Op(int x, int y) => x >= 0 && x < 16 && y >= 0 && y < 16 && (px[y * 16 + x] >> 24) > 25;
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                if (!Op(x, y)) continue;
                if (!Op(x - 1, y)) l.Add((x, y, 0));
                if (!Op(x + 1, y)) l.Add((x, y, 1));
                if (!Op(x, y - 1)) l.Add((x, y, 2));
                if (!Op(x, y + 1)) l.Add((x, y, 3));
            }
        _edges[layer] = l;
        return l;
    }

    public void RenderSpriteExtruded(int layer, Matrix4 m)
    {
        Terrain.Layer = layer;
        const float t = 1 / 32f;
        Vector3 P(float x, float y, float z) => T(m, x, y, z);
        // przód i tył
        var n1 = Vector3.TransformNormal(Vector3.UnitZ, m); n1.Normalize();
        float sf = Terrain.ShadeFor(n1), sb = Terrain.ShadeFor(-n1);
        Terrain.Quad(P(-0.5f, 0.5f, t), P(-0.5f, -0.5f, t), P(0.5f, -0.5f, t), P(0.5f, 0.5f, t), 0, 0, 1, 1, sf);
        Terrain.Quad(P(0.5f, 0.5f, -t), P(0.5f, -0.5f, -t), P(-0.5f, -0.5f, -t), P(-0.5f, 0.5f, -t), 1, 0, 0, 1, sb);
        var nx = Vector3.TransformNormal(Vector3.UnitX, m); nx.Normalize();
        var ny = Vector3.TransformNormal(Vector3.UnitY, m); ny.Normalize();
        foreach (var (x, y, dir) in Edges(layer))
        {
            float x0 = x / 16f - 0.5f, x1 = (x + 1) / 16f - 0.5f, y0 = 0.5f - (y + 1) / 16f, y1 = 0.5f - y / 16f;
            float u0 = (x + 0.1f) / 16f, u1 = (x + 0.9f) / 16f, v0 = (y + 0.1f) / 16f, v1 = (y + 0.9f) / 16f;
            switch (dir)
            {
                case 0: Terrain.Quad(P(x0, y1, -t), P(x0, y0, -t), P(x0, y0, t), P(x0, y1, t), u0, v0, u1, v1, Terrain.ShadeFor(-nx)); break;
                case 1: Terrain.Quad(P(x1, y1, t), P(x1, y0, t), P(x1, y0, -t), P(x1, y1, -t), u0, v0, u1, v1, Terrain.ShadeFor(nx)); break;
                case 2: Terrain.Quad(P(x0, y1, -t), P(x0, y1, t), P(x1, y1, t), P(x1, y1, -t), u0, v0, u1, v1, Terrain.ShadeFor(ny)); break;
                default: Terrain.Quad(P(x0, y0, t), P(x0, y0, -t), P(x1, y0, -t), P(x1, y0, t), u0, v0, u1, v1, Terrain.ShadeFor(-ny)); break;
            }
        }
    }

    // ================= animacje mobów =================
    private void Biped(LivingEntity e, float pt, float limb, float amt, float age, float headYaw, float headPitch, ModelPart head, ModelPart body, ModelPart ra, ModelPart la, ModelPart rl, ModelPart ll, bool zombieArms, float armW)
    {
        head.Reset(0, 0, 0); body.Reset(0, 0, 0);
        ra.Reset(-5, 2, 0); la.Reset(5, 2, 0); rl.Reset(armW < 4 ? -2 : -1.9f, 12, 0); ll.Reset(armW < 4 ? 2 : 1.9f, 12, 0);
        head.RY = MathHelper.DegreesToRadians(headYaw); head.RX = MathHelper.DegreesToRadians(headPitch);
        ra.RX = MathF.Cos(limb * 0.6662f + MathF.PI) * 2f * amt * 0.5f;
        la.RX = MathF.Cos(limb * 0.6662f) * 2f * amt * 0.5f;
        rl.RX = MathF.Cos(limb * 0.6662f) * 1.4f * amt;
        ll.RX = MathF.Cos(limb * 0.6662f + MathF.PI) * 1.4f * amt;
        if (e is Player { RidingMob: not null } || e is Mob { Vehicle: not null }) // ModelBiped.isRiding
        { ra.RX -= MathF.PI / 5f; la.RX -= MathF.PI / 5f; rl.RX = ll.RX = -MathF.PI * 2f / 5f; rl.RY = MathF.PI / 10f; ll.RY = -MathF.PI / 10f; }
        if (e.HeldItem != null || e is Player p && p.Inventory.Selected != null) ra.RX = ra.RX * 0.5f - MathF.PI / 10f;
        float swing = e.PrevSwingProgress + (e.SwingProgress - e.PrevSwingProgress) * pt;
        if (swing > 0)
        {
            body.RY = MathF.Sin(MathF.Sqrt(swing) * MathF.PI * 2f) * 0.2f;
            ra.PZ = MathF.Sin(body.RY) * 5f; ra.PX = -MathF.Cos(body.RY) * 5f;
            la.PZ = -MathF.Sin(body.RY) * 5f; la.PX = MathF.Cos(body.RY) * 5f;
            ra.RY += body.RY; la.RY += body.RY; la.RX += body.RY;
            float f = 1f - swing; f *= f; f *= f; f = 1f - f;
            float f2 = MathF.Sin(f * MathF.PI), f3 = MathF.Sin(swing * MathF.PI) * -(head.RX - 0.7f) * 0.75f;
            ra.RX -= f2 * 1.2f + f3; ra.RY += body.RY * 2f; ra.RZ += MathF.Sin(swing * MathF.PI) * -0.4f;
        }
        if (e.Sneaking)
        {
            body.RX = 0.5f; ra.RX += 0.4f; la.RX += 0.4f; rl.PZ = 4f; ll.PZ = 4f; rl.PY = 9f; ll.PY = 9f; head.PY = 1f;
        }
        ra.RZ += MathF.Cos(age * 0.09f) * 0.05f + 0.05f; la.RZ -= MathF.Cos(age * 0.09f) * 0.05f + 0.05f;
        ra.RX += MathF.Sin(age * 0.067f) * 0.05f; la.RX -= MathF.Sin(age * 0.067f) * 0.05f;
        if (zombieArms)
        {
            ra.RY = -(0.1f - (1 - MathF.Sin(swing * MathF.PI)) * 0.6f) * 0; la.RY = 0;
            ra.RX = -MathF.PI / 2f - MathF.Sin(swing * MathF.PI) * 1.2f; la.RX = -MathF.PI / 2f;
            ra.RZ = MathF.Cos(age * 0.09f) * 0.05f + 0.05f; la.RZ = -ra.RZ;
        }
    }

    private static void Quad4(ModelPart[] legs, float limb, float amt)
    {
        legs[0].RX = MathF.Cos(limb * 0.6662f) * 1.4f * amt;
        legs[1].RX = MathF.Cos(limb * 0.6662f + MathF.PI) * 1.4f * amt;
        legs[2].RX = MathF.Cos(limb * 0.6662f + MathF.PI) * 1.4f * amt;
        legs[3].RX = MathF.Cos(limb * 0.6662f) * 1.4f * amt;
    }

    // ---- rejestr rendererów modułów (zamiast dopisywania gałęzi do switchy) ----
    public delegate void CustomEntityRender(EntityRenderer r, Entity e, float pt, Vector3 pos, Vector3 cam);
    /// <summary>Pozycja i animacja moba w chwili renderu (Root ma już obrót ciała i przewrócenie po śmierci).</summary>
    public struct LivingPose { public float Pt, BodyYaw, HeadYaw, Pitch, Limb, LimbAmt, Age; public Vector3 Pos; public Matrix4 Root; public bool ForGui; }
    public delegate void CustomLivingRender(EntityRenderer r, LivingEntity e, LivingPose pose);
    /// <summary>Renderery encji nie-żywych (wagoniki, łódki, ramki...). Klucz: typ encji (dziedziczony też przez podklasy).</summary>
    public static readonly Dictionary<Type, CustomEntityRender> Custom = new();
    /// <summary>Renderery modeli mobów. Wołane z RenderLiving po ustawieniu oświetlenia, nakładki obrażeń i Root.</summary>
    public static readonly Dictionary<Type, CustomLivingRender> CustomLiving = new();
    private static T FindCustom<T>(Dictionary<Type, T> d, Type t) where T : class
    {
        if (d.Count == 0) return null;
        for (var x = t; x != null && x != typeof(object); x = x.BaseType) if (d.TryGetValue(x, out var f)) return f;
        return null;
    }

    public void RenderLiving(LivingEntity e, float pt, Vector3 pos, bool forGui = false)
    {
        if (e.HasEffect(Effect.Invisibility) && !forGui) return;
        float bodyYaw = MathUtil.LerpAngle(e.PrevBodyYaw, e.BodyYaw, pt);
        float headYawAbs = MathUtil.LerpAngle(e.PrevYaw, e.Yaw, pt);
        float headYaw = MathUtil.WrapDegrees(headYawAbs - bodyYaw);
        float pitch = MathHelper.Lerp(e.PrevPitch, e.Pitch, pt);
        float amt = MathHelper.Lerp(e.PrevLimbSwingAmount, e.LimbSwingAmount, pt);
        float limb = e.LimbSwing - e.LimbSwingAmount * (1 - pt);
        if (amt > 1) amt = 1;
        float age = e.Age + pt;
        Skin.Overlay = Vector4.Zero;
        if (e.HurtTime > 0 || e.DeathTime > 0) Skin.Overlay = new Vector4(1f, 0f, 0f, 0.3f);
        var root = EntityRoot(pos, bodyYaw, 1f);
        if (e.DeathTime > 0)
        {
            float f = ((e.DeathTime + pt - 1) / 20f * 1.6f);
            f = MathF.Sqrt(Math.Max(0, f)); if (f > 1) f = 1;
            // obrót na bok
            root = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-1, -1, 1)
                   * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(f * 90f)) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180 - bodyYaw)) * Matrix4.CreateTranslation(pos);
        }
        if (FindCustom(CustomLiving, e.GetType()) is { } cl)
        {
            cl(this, e, new LivingPose { Pt = pt, BodyYaw = bodyYaw, HeadYaw = headYaw, Pitch = pitch, Limb = limb, LimbAmt = amt, Age = age, Pos = pos, Root = root, ForGui = forGui });
            return;
        }
        switch (e)
        {
            case Player pl:
                {
                    if (pl.Sleeping)
                    {
                        root = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-1, -1, 1) * Matrix4.CreateRotationX(-MathHelper.PiOver2)
                               * Matrix4.CreateTranslation(0, 0.2f, 0) * Matrix4.CreateTranslation(pos);
                    }
                    else root = Matrix4.CreateScale(0.9375f) * root;
                    Skin.Layer = SkinGen.Get("steve");
                    Biped(e, pt, limb, amt, age, headYaw, pitch, _head, _body, _rArm, _lArm, _rLeg, _lLeg, false, 4);
                    if (pl.ItemInUse?.Id == I.Bow) { _rArm.RY = -0.1f + _head.RY; _lArm.RY = 0.1f + _head.RY + 0.4f; _rArm.RX = -MathF.PI / 2f + _head.RX; _lArm.RX = -MathF.PI / 2f + _head.RX; }
                    foreach (var part in new[] { _head, _body, _rArm, _lArm, _rLeg, _lLeg }) RenderPart(Skin, part, root);
                    RenderArmorLayers(pl.Inventory.Armor, _head, _body, _rArm, _lArm, _rLeg, _lLeg, root);
                    var held = pl.Inventory.Selected;
                    if (held != null) RenderHeld(held, _rArm, root);
                    break;
                }
            case Zombie:
                Skin.Layer = SkinGen.Get("zombie");
                Biped(e, pt, limb, amt, age, headYaw, pitch, _head, _body, _rArm, _lArm, _rLeg, _lLeg, true, 4);
                foreach (var part in new[] { _head, _body, _rArm, _lArm, _rLeg, _lLeg }) RenderPart(Skin, part, root);
                break;
            case Skeleton sk:
                Skin.Layer = SkinGen.Get("skeleton");
                Biped(e, pt, limb, amt, age, headYaw, pitch, _head, _body, _sRArm, _sLArm, _sRLeg, _sLLeg, false, 2);
                if (sk.Aiming) { _sRArm.RY = -0.1f + _head.RY; _sLArm.RY = 0.1f + _head.RY + 0.4f; _sRArm.RX = -MathF.PI / 2f + _head.RX; _sLArm.RX = -MathF.PI / 2f + _head.RX; }
                foreach (var part in new[] { _head, _body, _sRArm, _sLArm, _sRLeg, _sLLeg }) RenderPart(Skin, part, root);
                RenderHeld(new ItemStack(I.Bow), _sRArm, root);
                break;
            case Creeper cr:
                {
                    Skin.Layer = SkinGen.Get("creeper");
                    float fuse = (cr.PrevFuse + (cr.Fuse - cr.PrevFuse) * pt) / (Creeper.MaxFuse - 2);
                    var croot = root;
                    if (fuse > 0)
                    {
                        float f1 = 1f + MathF.Sin(fuse * 100f) * fuse * 0.01f;
                        fuse = Math.Clamp(fuse, 0, 1); fuse *= fuse; fuse *= fuse;
                        float sxz = (1f + fuse * 0.4f) * f1, sy = (1f + fuse * 0.1f) / f1;
                        croot = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-sxz, -sy, sxz) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180 - bodyYaw)) * Matrix4.CreateTranslation(pos);
                        if ((int)(fuse * 10f) % 2 != 0 || cr.Fuse > 20 && (cr.Fuse / 2) % 2 == 0) Skin.Overlay = new Vector4(1, 1, 1, Math.Clamp(fuse * 0.2f + 0.3f, 0, 0.8f));
                    }
                    _cHead.RY = MathHelper.DegreesToRadians(headYaw); _cHead.RX = MathHelper.DegreesToRadians(pitch);
                    Quad4(_cLegs, limb, amt);
                    RenderPart(Skin, _cHead, croot); RenderPart(Skin, _cBody, croot);
                    foreach (var l in _cLegs) RenderPart(Skin, l, croot);
                    break;
                }
            case Spider:
                {
                    InitSpider();
                    Skin.Layer = SkinGen.Get("spider");
                    _spHead.RY = MathHelper.DegreesToRadians(headYaw); _spHead.RX = MathHelper.DegreesToRadians(pitch);
                    float f = MathF.PI / 4f;
                    float[] rz = { -f, f, -f * 0.74f, f * 0.74f, -f * 0.74f, f * 0.74f, -f, f };
                    float f2 = MathF.PI / 8f;
                    float[] ry = { f2 * 2, -f2 * 2, f2, -f2, -f2, f2, -f2 * 2, f2 * 2 };
                    float s = limb * 0.6662f;
                    float[] w3 = { -(MathF.Cos(s * 2) * 0.4f) * amt, -(MathF.Cos(s * 2 + MathF.PI) * 0.4f) * amt, -(MathF.Cos(s * 2 + MathF.PI / 2) * 0.4f) * amt, -(MathF.Cos(s * 2 + MathF.PI * 1.5f) * 0.4f) * amt };
                    float[] w7 = { MathF.Abs(MathF.Sin(s) * 0.4f) * amt, MathF.Abs(MathF.Sin(s + MathF.PI) * 0.4f) * amt, MathF.Abs(MathF.Sin(s + MathF.PI / 2) * 0.4f) * amt, MathF.Abs(MathF.Sin(s + MathF.PI * 1.5f) * 0.4f) * amt };
                    for (int i = 0; i < 8; i++)
                    {
                        int k = i / 2; float sg = i % 2 == 0 ? 1 : -1;
                        _spLegs[i].RZ = rz[i] + sg * w7[k];
                        _spLegs[i].RY = ry[i] + sg * w3[k];
                    }
                    RenderPart(Skin, _spHead, root); RenderPart(Skin, _spNeck, root); RenderPart(Skin, _spBody, root);
                    foreach (var l in _spLegs) RenderPart(Skin, l, root);
                    break;
                }
            case Enderman en:
                {
                    Skin.Layer = SkinGen.Get("enderman");
                    _eHead.Reset(0, -14, 0); _eBody.Reset(0, -14, 0);
                    _eRArm.Reset(-5, -12, 0); _eLArm.Reset(5, -12, 0); _eRLeg.Reset(-2, -5, 0); _eLLeg.Reset(2, -5, 0);
                    _eHead.RY = MathHelper.DegreesToRadians(headYaw); _eHead.RX = MathHelper.DegreesToRadians(pitch);
                    _eRArm.RX = MathF.Cos(limb * 0.6662f + MathF.PI) * amt * 0.5f; _eLArm.RX = MathF.Cos(limb * 0.6662f) * amt * 0.5f;
                    _eRLeg.RX = MathF.Cos(limb * 0.6662f) * 0.7f * amt; _eLLeg.RX = MathF.Cos(limb * 0.6662f + MathF.PI) * 0.7f * amt;
                    _eRArm.RX = Math.Clamp(_eRArm.RX, -0.4f, 0.4f); _eLArm.RX = Math.Clamp(_eLArm.RX, -0.4f, 0.4f);
                    _eRLeg.RX = Math.Clamp(_eRLeg.RX, -0.4f, 0.4f); _eLLeg.RX = Math.Clamp(_eLLeg.RX, -0.4f, 0.4f);
                    if (en.Angry) _eHead.PY -= 5;
                    _eRArm.RZ = 0.05f; _eLArm.RZ = -0.05f;
                    foreach (var part in new[] { _eHead, _eBody, _eRArm, _eLArm, _eRLeg, _eLLeg }) RenderPart(Skin, part, root);
                    break;
                }
            case Animal an:
                {
                    float sc = an.IsBaby ? 0.5f : 1f;
                    var aroot = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-sc, -sc, sc) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180 - bodyYaw)) * Matrix4.CreateTranslation(pos);
                    if (e.DeathTime > 0) aroot = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-sc, -sc, sc) * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Math.Min(1, MathF.Sqrt((e.DeathTime + pt) / 20f * 1.6f)) * 90f)) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180 - bodyYaw)) * Matrix4.CreateTranslation(pos);
                    float hy = MathHelper.DegreesToRadians(headYaw), hp = MathHelper.DegreesToRadians(pitch);
                    if (an is Cow)
                    {
                        Skin.Layer = SkinGen.Get("cow");
                        _cowHead.RY = hy; _cowHead.RX = hp; _cowBody.RX = MathF.PI / 2; Quad4(_cowLegs, limb, amt);
                        RenderPart(Skin, _cowHead, aroot); RenderPart(Skin, _cowBody, aroot); foreach (var l in _cowLegs) RenderPart(Skin, l, aroot);
                    }
                    else if (an is Pig)
                    {
                        Skin.Layer = SkinGen.Get("pig");
                        _pigHead.RY = hy; _pigHead.RX = hp; _pigBody.RX = MathF.PI / 2; Quad4(_pigLegs, limb, amt);
                        RenderPart(Skin, _pigHead, aroot); RenderPart(Skin, _pigBody, aroot); foreach (var l in _pigLegs) RenderPart(Skin, l, aroot);
                    }
                    else if (an is Sheep sh)
                    {
                        Skin.Layer = SkinGen.Get("sheep");
                        float eat = sh.EatAnim > 0 ? 1 : 0;
                        _shHead.RY = hy; _shHead.RX = hp + eat * 0.9f; _shHead.PY = 6 + eat * 9; _shBody.RX = MathF.PI / 2; Quad4(_shLegs, limb, amt);
                        RenderPart(Skin, _shHead, aroot); RenderPart(Skin, _shBody, aroot); foreach (var l in _shLegs) RenderPart(Skin, l, aroot);
                        if (!sh.Sheared)
                        {
                            uint[] wc = Items.Dyes.Rgb;
                            Skin.Layer = SkinGen.Get("sheep_fur"); Skin.Color = HexColor(wc[sh.Color & 15]);
                            if (sh.Color == 0) Skin.Color = Vector4.One;
                            _fHead.RY = _shHead.RY; _fHead.RX = _shHead.RX; _fHead.PY = _shHead.PY; _fBody.RX = MathF.PI / 2;
                            for (int i = 0; i < 4; i++) _fLegs[i].RX = _shLegs[i].RX;
                            RenderPart(Skin, _fHead, aroot); RenderPart(Skin, _fBody, aroot); foreach (var l in _fLegs) RenderPart(Skin, l, aroot);
                            Skin.Color = Vector4.One;
                        }
                    }
                    else if (an is Chicken ch)
                    {
                        Skin.Layer = SkinGen.Get("chicken");
                        _chHead.RY = hy; _chHead.RX = hp; _chBody.RX = MathF.PI / 2;
                        _chRLeg.RX = MathF.Cos(limb * 0.6662f) * 1.4f * amt; _chLLeg.RX = MathF.Cos(limb * 0.6662f + MathF.PI) * 1.4f * amt;
                        float wing = (MathF.Sin(MathHelper.Lerp(ch.PrevWingRotation, ch.WingRotation, pt)) + 1f) * (ch.OnGround ? 0.1f : 1f);
                        _chRWing.RZ = wing; _chLWing.RZ = -wing;
                        foreach (var part in new[] { _chHead, _chBody, _chRLeg, _chLLeg, _chRWing, _chLWing }) RenderPart(Skin, part, aroot);
                    }
                    break;
                }
        }
        Skin.Overlay = Vector4.Zero;
    }

    private void RenderHeld(ItemStack s, ModelPart arm, Matrix4 root)
    {
        // pozycja dłoni: koniec ramienia
        var m = Matrix4.Identity;
        if (arm.RX != 0) m *= Matrix4.CreateRotationX(arm.RX);
        if (arm.RY != 0) m *= Matrix4.CreateRotationY(arm.RY);
        if (arm.RZ != 0) m *= Matrix4.CreateRotationZ(arm.RZ);
        m *= Matrix4.CreateTranslation(arm.PX / 16f, arm.PY / 16f, arm.PZ / 16f);
        var handM = Matrix4.CreateTranslation(-1f / 16f, 10f / 16f, -1f / 16f) * m * root;
        var it = s.Item; if (it == null) return;
        Matrix4 im;
        if (IsBlockModel(it))
            im = Matrix4.CreateScale(0.375f) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(45)) * Matrix4.CreateRotationX(MathHelper.DegreesToRadians(-20)) * Matrix4.CreateTranslation(0, 0.1f, -0.1f) * handM;
        else
            im = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(180)) * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-60))
                 * Matrix4.CreateScale(0.7f) * Matrix4.CreateTranslation(0, 0.25f, -0.15f) * handM;
        var c = Terrain.Sky; Terrain.Sky = Skin.Sky; Terrain.Blk = Skin.Blk;
        RenderItem(s, im);
    }

    // ================= ręka w pierwszej osobie =================
    /// <summary>Buduje rękę/przedmiot w przestrzeni kamery (MC 1.8 ItemRenderer).</summary>
    public void RenderFirstPerson(Player p, float pt, float equip, ItemStack held, float sky, float blk)
    {
        float swing = p.PrevSwingProgress + (p.SwingProgress - p.PrevSwingProgress) * pt;
        Skin.Sky = Terrain.Sky = sky; Skin.Blk = Terrain.Blk = blk;
        Skin.Overlay = Terrain.Overlay = Vector4.Zero; Skin.Color = Terrain.Color = Vector4.One;
        var gs = new GlStack();
        if (held == null)
        {
            float f = -0.3f * MathF.Sin(MathF.Sqrt(swing) * MathF.PI);
            float f1 = 0.4f * MathF.Sin(MathF.Sqrt(swing) * MathF.PI * 2f);
            float f2 = -0.4f * MathF.Sin(swing * MathF.PI);
            gs.Translate(f, f1, f2);
            gs.Translate(0.64000005f, -0.6f, -0.71999997f);
            gs.Translate(0, equip * -0.6f, 0);
            gs.Rotate(45, 0, 1, 0);
            float f3 = MathF.Sin(swing * swing * MathF.PI), f4 = MathF.Sin(MathF.Sqrt(swing) * MathF.PI);
            gs.Rotate(f4 * 70f, 0, 1, 0);
            gs.Rotate(f3 * -20f, 0, 0, 1);
            gs.Translate(-1f, 3.6f, 3.5f);
            gs.Rotate(120f, 0, 0, 1);
            gs.Rotate(200f, 1, 0, 0);
            gs.Rotate(-135f, 0, 1, 0);
            gs.Translate(5.6f, 0, 0);
            Skin.Layer = SkinGen.Get("steve");
            _rArm.Reset(-5, 2, 0);
            RenderPart(Skin, _rArm, gs.M);
            return;
        }
        var it = held.Item;
        if (it?.Id == I.FilledMap) { RenderFirstPersonMap(p, pt, equip, swing, held); return; }
        bool using_ = p.ItemInUse != null && p.ItemInUse == held;
        if (using_ && (it.Action == UseAction.Eat || it.Action == UseAction.Drink))
        {
            float left = p.ItemInUseCount - pt + 1f;
            float f1 = left / it.UseDuration;
            float f2 = MathF.Abs(MathF.Cos(left / 4f * MathF.PI) * 0.1f);
            if (f1 >= 0.8f) f2 = 0;
            gs.Translate(0, f2, 0);
            float f3 = 1f - MathF.Pow(f1, 27f);
            gs.Translate(f3 * 0.6f, f3 * -0.5f, 0);
            gs.Rotate(f3 * 90f, 0, 1, 0);
            gs.Rotate(f3 * 10f, 1, 0, 0);
            gs.Rotate(f3 * 30f, 0, 0, 1);
            swing = 0;
        }
        else
        {
            float f = -0.4f * MathF.Sin(MathF.Sqrt(swing) * MathF.PI);
            float f1 = 0.2f * MathF.Sin(MathF.Sqrt(swing) * MathF.PI * 2f);
            float f2 = -0.2f * MathF.Sin(swing * MathF.PI);
            gs.Translate(f, f1, f2);
        }
        // transformFirstPersonItem
        gs.Translate(0.56f, -0.52f, -0.71999997f);
        gs.Translate(0, equip * -0.6f, 0);
        gs.Rotate(45, 0, 1, 0);
        float s1 = MathF.Sin(swing * swing * MathF.PI), s2 = MathF.Sin(MathF.Sqrt(swing) * MathF.PI);
        gs.Rotate(s1 * -20f, 0, 1, 0);
        gs.Rotate(s2 * -20f, 0, 0, 1);
        gs.Rotate(s2 * -80f, 1, 0, 0);
        gs.Scale(0.4f, 0.4f, 0.4f);
        if (using_ && it.Id == I.Bow)
        {
            gs.Rotate(-18f, 0, 0, 1); gs.Rotate(-12f, 0, 1, 0); gs.Rotate(-8f, 1, 0, 0);
            gs.Translate(-0.9f, 0.2f, 0.0f);
            float pull = p.BowCharge;
            gs.Translate(0, 0, -pull * 0.1f);
        }
        if (IsBlockModel(it))
        {
            gs.Translate(0.1f, 0.1f, 0); gs.Rotate(45f, 0, 1, 0);
            gs.Scale(0.55f, 0.55f, 0.55f);
            RenderBlockAt(it.BlockId, held.Damage, gs.M);
        }
        else
        {
            gs.Translate(0, 4 / 16f, 2 / 16f);
            gs.Rotate(-135f, 0, 1, 0);
            gs.Rotate(25f, 0, 0, 1);
            gs.Scale(1.05f, 1.05f, 1.05f);
            RenderSpriteExtruded(it.GetSprite(held), gs.M);
        }
    }

    public void Dispose() { _mesh.Dispose(); Shader.Dispose(); SkinTex.Dispose(); }
}

/// <summary>Stos macierzy z semantyką OpenGL (M = M * X).</summary>
public sealed class GlStack
{
    public Matrix4 M = Matrix4.Identity;
    public void Translate(float x, float y, float z) => M = Matrix4.CreateTranslation(x, y, z) * M;
    public void Rotate(float deg, float x, float y, float z) => M = Matrix4.CreateFromAxisAngle(new Vector3(x, y, z), MathHelper.DegreesToRadians(deg)) * M;
    public void Scale(float x, float y, float z) => M = Matrix4.CreateScale(x, y, z) * M;
}
