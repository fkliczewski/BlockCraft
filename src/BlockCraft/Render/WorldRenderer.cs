using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed class SectionMesh
{
    public Mesh Solid, Trans;
    public int Version;      // wersja żądana
    public int BuiltVersion = -1;
}

public sealed class ChunkRender
{
    public readonly SectionMesh[] S = new SectionMesh[Chunk.Sections];
    public ChunkRender() { for (int i = 0; i < S.Length; i++) S[i] = new SectionMesh(); }
}

public struct Particle
{
    public Vector3 Pos, Prev, Vel;
    public int Age, Life, Layer;
    public float U0, V0, U1, V1, Size, Gravity;
    public Vector4 Color;
    public bool Collide, Emissive, Grow;
}

public sealed partial class WorldRenderer : IWorldListener, IDisposable
{
    public World.World World;
    public TextureArray Terrain;
    private readonly Shader _chunk, _sky, _cloudSh, _line;
    public readonly EntityRenderer Entities;
    private readonly Dictionary<long, ChunkRender> _renders = new();
    private readonly object _jobLock = new();
    private readonly List<(int cx, int sy, int cz, int ver)> _jobs = new();
    private readonly HashSet<(int, int, int)> _jobSet = new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<(int cx, int sy, int cz, int ver, byte[] solid, int sb, byte[] trans, int tb)> _done = new();
    private readonly Thread[] _workers;
    private volatile bool _running = true;
    private readonly ChunkMesher _mainMesher = new();
    private Vector3d _camPos; private volatile int _camCx, _camCz;
    private readonly Mesh _skyQuad, _sunMesh, _starMesh, _lineMesh;
    private Mesh _cloudMesh;
    private Texture2D _sunTex, _moonTex;
    public readonly List<Particle> Particles = new();
    public Action<string, double, double, double, float, float> SoundHook;
    public int RenderedSections, TotalSections;
    public int RenderDistance = 8;
    public bool CloudsEnabled = true;
    public float Gamma = 0.5f;
    private int _waterLayer, _lavaLayer;

    public WorldRenderer()
    {
        ItemRegistry.Init();
        Terrain = new TextureArray(16, TextureGen.Textures, true, 4);
        Entities = new EntityRenderer { TerrainTex = Terrain };
        _waterLayer = TextureGen.Layer("water_still"); _lavaLayer = TextureGen.Layer("lava_still");
        _chunk = new Shader(ChunkVert, ChunkFrag, "chunk");
        _sky = new Shader(SkyVert, SkyFrag, "sky");
        _cloudSh = new Shader(CloudVert, CloudFrag, "cloud");
        _line = new Shader(LineVert, LineFrag, "line");
        _skyQuad = new Mesh(8, () => { GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 8, 0); });
        _skyQuad.Upload(new float[] { -1, 1, -1, -1, 1, -1, 1, 1 }, 8, BufferUsageHint.StaticDraw);
        _sunMesh = new Mesh(20, () =>
        {
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 20, 0);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 20, 12);
        });
        _starMesh = new Mesh(12, () => { GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, 0); });
        _lineMesh = new Mesh(12, () => { GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 12, 0); }, false);
        BuildStars();
        BuildCelestialTextures();
        BuildClouds();
        int n = Math.Clamp(Environment.ProcessorCount / 2, 1, 3);
        _workers = new Thread[n];
        for (int i = 0; i < n; i++) { _workers[i] = new Thread(WorkerLoop) { IsBackground = true, Name = "Mesher" + i, Priority = ThreadPriority.BelowNormal }; _workers[i].Start(); }
    }

    public void SetWorld(World.World w)
    {
        if (World != null) World.Listeners.Remove(this);
        World = w;
        lock (_jobLock) { _jobs.Clear(); _jobSet.Clear(); }
        while (_done.TryDequeue(out _)) { }
        foreach (var r in _renders.Values) foreach (var s in r.S) { s.Solid?.Dispose(); s.Trans?.Dispose(); }
        _renders.Clear();
        Particles.Clear();
        if (w != null) w.Listeners.Add(this);
    }

    // ================= shadery =================
    private const string ChunkVert = @"#version 330 core
layout(location=0) in vec3 aPos; layout(location=1) in vec2 aUV; layout(location=2) in float aLayer; layout(location=3) in vec2 aFS;
layout(location=4) in vec2 aLight; layout(location=5) in vec4 aTint;
uniform mat4 uViewProj; uniform vec3 uOffset;
out vec3 vUV; out vec2 vLight; out float vShade; out vec4 vTint; out float vMask; out float vDist;
void main(){ vec3 p = aPos + uOffset; gl_Position = uViewProj*vec4(p,1.0);
 vUV = vec3((aUV - 32768.0) / 4096.0, aLayer); vLight = aLight*15.0; vShade = aFS.y/255.0; vTint = aTint; vMask = mod(aFS.x, 2.0); vDist = length(p.xz); }";
    private static readonly string ChunkFrag = @"#version 330 core
in vec3 vUV; in vec2 vLight; in float vShade; in vec4 vTint; in float vMask; in float vDist;
uniform sampler2DArray uTex; uniform float uAlphaCut; uniform vec3 uFogColor; uniform float uFogStart; uniform float uFogEnd; uniform float uFogDensity; uniform int uFogMode;
out vec4 o;" + EntityRenderer.LightmapGlsl + @"
void main(){ vec4 t = texture(uTex, vUV); if (t.a < uAlphaCut) discard;
 vec3 tint = vTint.rgb; if (vMask > 0.5) tint = t.a < 0.99 ? vTint.rgb : vec3(1.0);
 vec3 c = t.rgb * tint * vShade * lightmap(vLight.x, vLight.y);
 float fog = uFogMode==1 ? 1.0-exp(-uFogDensity*vDist) : clamp((vDist-uFogStart)/(uFogEnd-uFogStart),0.0,1.0);
 float a = uAlphaCut > 0.01 ? 1.0 : t.a;
 o = vec4(mix(c, uFogColor, fog), a); }";
    private const string SkyVert = @"#version 330 core
layout(location=0) in vec2 aPos; uniform mat4 uInvViewProj; out vec3 vDir;
void main(){ gl_Position = vec4(aPos, 0.999, 1.0); vec4 p = uInvViewProj * vec4(aPos, 1.0, 1.0); vDir = p.xyz / p.w; }";
    private const string SkyFrag = @"#version 330 core
in vec3 vDir; uniform vec3 uSky; uniform vec3 uFog; uniform vec4 uSunset; uniform vec3 uSunDir; out vec4 o;
void main(){ vec3 d = normalize(vDir);
 float h = d.y;
 vec3 c = mix(uFog, uSky, smoothstep(-0.02, 0.35, h));
 if (h < -0.05) c = mix(c, uSky*0.2 + uFog*0.3, smoothstep(-0.05, -0.4, h));
 if (uSunset.a > 0.0) { vec2 sd = normalize(uSunDir.xz + vec2(0.0001)); float t = max(0.0, dot(normalize(d.xz+vec2(0.0001)), sd));
   float band = (1.0 - smoothstep(0.0, 0.45, abs(h - 0.05))) * pow(t, 2.0); c = mix(c, uSunset.rgb, band * uSunset.a); }
 o = vec4(c, 1.0); }";
    private const string CloudVert = @"#version 330 core
layout(location=0) in vec3 aPos; layout(location=1) in float aShade; uniform mat4 uViewProj; uniform vec3 uOffset; out float vShade; out float vDist;
void main(){ vec3 p = aPos + uOffset; gl_Position = uViewProj*vec4(p,1.0); vShade=aShade; vDist=length(p.xz); }";
    private const string CloudFrag = @"#version 330 core
in float vShade; in float vDist; uniform vec3 uColor; uniform vec3 uFogColor; uniform float uFogEnd; out vec4 o;
void main(){ float fog = clamp((vDist - uFogEnd*0.6)/(uFogEnd*1.6 - uFogEnd*0.6), 0.0, 1.0); o = vec4(mix(uColor*vShade, uFogColor, fog), 0.8*(1.0-fog*0.9)); }";
    private const string LineVert = @"#version 330 core
layout(location=0) in vec3 aPos; uniform mat4 uViewProj; void main(){ gl_Position = uViewProj*vec4(aPos,1.0); }";
    private const string LineFrag = @"#version 330 core
uniform vec4 uColor; out vec4 o; void main(){ o = uColor; }";
    private const string SunVert = @"#version 330 core
layout(location=0) in vec3 aPos; layout(location=1) in vec2 aUV; uniform mat4 uViewProj; out vec2 vUV;
void main(){ gl_Position = uViewProj*vec4(aPos,1.0); vUV=aUV; }";
    private const string SunFrag = @"#version 330 core
in vec2 vUV; uniform sampler2D uTex; uniform vec4 uColor; out vec4 o; void main(){ o = texture(uTex, vUV)*uColor; }";
    private Shader _sunSh;

    // ================= niebo =================
    private void BuildStars()
    {
        var r = new Rng(10842);
        var v = new List<float>();
        for (int i = 0; i < 1500; i++)
        {
            double x = r.NextFloat() * 2 - 1, y = r.NextFloat() * 2 - 1, z = r.NextFloat() * 2 - 1;
            double s = 0.15 + r.NextFloat() * 0.1;
            double d = x * x + y * y + z * z;
            if (d >= 1 || d <= 0.01) continue;
            d = 1 / Math.Sqrt(d); x *= d; y *= d; z *= d;
            var c = new Vector3((float)x, (float)y, (float)z) * 100;
            var n = Vector3.Normalize(c);
            var t1 = Vector3.Normalize(Vector3.Cross(n, Math.Abs(n.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
            var t2 = Vector3.Cross(n, t1);
            float ang = r.NextFloat() * MathF.PI * 2;
            var a1 = (t1 * MathF.Cos(ang) + t2 * MathF.Sin(ang)) * (float)s * 1.4f; var a2 = Vector3.Cross(n, a1);
            foreach (var p in new[] { c - a1 - a2, c - a1 + a2, c + a1 + a2, c + a1 - a2 }) { v.Add(p.X); v.Add(p.Y); v.Add(p.Z); }
        }
        _starMesh.Upload(v.ToArray(), v.Count, BufferUsageHint.StaticDraw);
    }

    private void BuildCelestialTextures()
    {
        var sun = new uint[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                int dx = Math.Max(Math.Abs(x - 16) - 0, 0), dy = Math.Abs(y - 16);
                int d = Math.Max(Math.Abs(x * 2 - 31), Math.Abs(y * 2 - 31));
                uint c = 0;
                if (d < 16) c = TextureGen.Rgba(255, 255, 220);
                else if (d < 20) c = TextureGen.Rgba(255, 240, 140);
                else if (d < 26) c = TextureGen.Rgba(200, 160, 60, 90);
                sun[y * 32 + x] = c;
            }
        _sunTex = new Texture2D(32, 32, sun);
        var moon = new uint[32 * 32]; var r = new Rng(3);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                int d = Math.Max(Math.Abs(x * 2 - 31), Math.Abs(y * 2 - 31));
                if (d < 18)
                {
                    int g = 200 + r.Next(30);
                    if ((x - 12) * (x - 12) + (y - 13) * (y - 13) < 10 || (x - 19) * (x - 19) + (y - 20) * (y - 20) < 6 || (x - 18) * (x - 18) + (y - 11) * (y - 11) < 4) g -= 50;
                    moon[y * 32 + x] = TextureGen.Rgba(g, g, g + 10);
                }
            }
        _moonTex = new Texture2D(32, 32, moon);
        _sunSh = new Shader(SunVert, SunFrag, "sun");
    }

    private const float CloudY = 108.33f;
    private const int CloudCells = 128, CloudCell = 12;
    private void BuildClouds()
    {
        // wzór chmur 128x128 komórek, kafelkowalny
        var r = new Rng(1234);
        var grid = new bool[CloudCells * CloudCells];
        var n1 = new float[CloudCells * CloudCells];
        var octs = new[] { (16, 0.6f), (8, 0.3f), (4, 0.1f) };
        foreach (var (cells, w) in octs)
        {
            var g = new float[cells * cells]; for (int i = 0; i < g.Length; i++) g[i] = r.NextFloat();
            float cs = (float)CloudCells / cells;
            for (int y = 0; y < CloudCells; y++) for (int x = 0; x < CloudCells; x++)
                {
                    float fx = x / cs, fy = y / cs; int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0;
                    tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                    int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells; x0 %= cells; y0 %= cells;
                    float a = g[y0 * cells + x0], b = g[y0 * cells + x1], c = g[y1 * cells + x0], d = g[y1 * cells + x1];
                    n1[y * CloudCells + x] += ((a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty) * w;
                }
        }
        for (int i = 0; i < grid.Length; i++) grid[i] = n1[i] + r.NextFloat() * 0.08f > 0.6f;
        bool G(int x, int y) => grid[((y % CloudCells + CloudCells) % CloudCells) * CloudCells + ((x % CloudCells + CloudCells) % CloudCells)];
        var v = new List<float>();
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float s) { foreach (var p in new[] { a, b, c, d }) { v.Add(p.X); v.Add(p.Y); v.Add(p.Z); v.Add(s); } }
        const float h = 4;
        for (int cy = 0; cy < CloudCells; cy++)
            for (int cx = 0; cx < CloudCells; cx++)
            {
                if (!G(cx, cy)) continue;
                float x0 = cx * CloudCell, z0 = cy * CloudCell, x1 = x0 + CloudCell, z1 = z0 + CloudCell;
                Q(new(x0, h, z0), new(x0, h, z1), new(x1, h, z1), new(x1, h, z0), 1f);
                Q(new(x0, 0, z1), new(x0, 0, z0), new(x1, 0, z0), new(x1, 0, z1), 0.7f);
                if (!G(cx, cy - 1)) Q(new(x1, h, z0), new(x1, 0, z0), new(x0, 0, z0), new(x0, h, z0), 0.85f);
                if (!G(cx, cy + 1)) Q(new(x0, h, z1), new(x0, 0, z1), new(x1, 0, z1), new(x1, h, z1), 0.85f);
                if (!G(cx - 1, cy)) Q(new(x0, h, z0), new(x0, 0, z0), new(x0, 0, z1), new(x0, h, z1), 0.9f);
                if (!G(cx + 1, cy)) Q(new(x1, h, z1), new(x1, 0, z1), new(x1, 0, z0), new(x1, h, z0), 0.9f);
            }
        _cloudMesh = new Mesh(16, () =>
        {
            GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 16, 0);
            GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 1, VertexAttribPointerType.Float, false, 16, 12);
        });
        _cloudMesh.Upload(v.ToArray(), v.Count, BufferUsageHint.StaticDraw);
    }

    // ================= meshowanie =================
    private void WorkerLoop()
    {
        var mesher = new ChunkMesher();
        while (_running)
        {
            (int cx, int sy, int cz, int ver) job = default; bool got = false;
            lock (_jobLock)
            {
                if (_jobs.Count > 0)
                {
                    int best = -1; long bd = long.MaxValue;
                    int ccx = _camCx, ccz = _camCz;
                    for (int i = 0; i < _jobs.Count; i++)
                    {
                        long d = (long)(_jobs[i].cx - ccx) * (_jobs[i].cx - ccx) + (long)(_jobs[i].cz - ccz) * (_jobs[i].cz - ccz);
                        if (d < bd) { bd = d; best = i; }
                    }
                    job = _jobs[best]; _jobs[best] = _jobs[^1]; _jobs.RemoveAt(_jobs.Count - 1);
                    _jobSet.Remove((job.cx, job.sy, job.cz));
                    got = true;
                }
            }
            if (!got) { Thread.Sleep(3); continue; }
            var w = World;
            if (w == null) continue;
            try
            {
                mesher.Build(w, job.cx, job.sy, job.cz);
                var s = mesher.Solid.Count > 0 ? mesher.Solid.Data[..mesher.Solid.Bytes] : null;
                var t = mesher.Translucent.Count > 0 ? mesher.Translucent.Data[..mesher.Translucent.Bytes] : null;
                _done.Enqueue((job.cx, job.sy, job.cz, job.ver, s, s?.Length ?? 0, t, t?.Length ?? 0));
            }
            catch (Exception ex) { Log.Error("Mesher: " + ex.Message); }
        }
    }

    private static Mesh NewChunkMesh() => new(VertexBuffer.Stride, () =>
    {
        int s = VertexBuffer.Stride;
        GL.EnableVertexAttribArray(0); GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, s, 0);
        GL.EnableVertexAttribArray(1); GL.VertexAttribPointer(1, 2, VertexAttribPointerType.UnsignedShort, false, s, 12);
        GL.EnableVertexAttribArray(2); GL.VertexAttribPointer(2, 1, VertexAttribPointerType.UnsignedShort, false, s, 16);
        GL.EnableVertexAttribArray(3); GL.VertexAttribPointer(3, 2, VertexAttribPointerType.UnsignedByte, false, s, 18);
        GL.EnableVertexAttribArray(4); GL.VertexAttribPointer(4, 2, VertexAttribPointerType.UnsignedByte, true, s, 20);
        GL.EnableVertexAttribArray(5); GL.VertexAttribPointer(5, 4, VertexAttribPointerType.UnsignedByte, true, s, 24);
    });

    private void Upload(SectionMesh sm, byte[] solid, int sb, byte[] trans, int tb)
    {
        if (sb > 0) { sm.Solid ??= NewChunkMesh(); sm.Solid.Upload(solid, sb); } else if (sm.Solid != null) { sm.Solid.Dispose(); sm.Solid = null; }
        if (tb > 0) { sm.Trans ??= NewChunkMesh(); sm.Trans.Upload(trans, tb); } else if (sm.Trans != null) { sm.Trans.Dispose(); sm.Trans = null; }
    }

    private bool NeighborsReady(Chunk c)
    {
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
            {
                var n = World.GetChunk(c.Cx + dx, c.Cz + dz);
                if (n == null || !n.Merged) return false;
            }
        return true;
    }

    /// <summary>Planowanie mesh-y; wywoływane co klatkę.</summary>
    public void Update(Vector3d cam)
    {
        if (World == null) return;
        _camPos = cam; _camCx = MathUtil.FloorToInt(cam.X) >> 4; _camCz = MathUtil.FloorToInt(cam.Z) >> 4;
        // usuń rendery z niezaładowanych chunków
        if (_renders.Count > 0)
        {
            List<long> rem = null;
            foreach (var k in _renders.Keys) if (!World.Chunks.ContainsKey(k)) (rem ??= new()).Add(k);
            if (rem != null) foreach (var k in rem) { foreach (var s in _renders[k].S) { s.Solid?.Dispose(); s.Trans?.Dispose(); } _renders.Remove(k); }
        }
        var urgent = new List<(int, int, int, int, double)>();
        foreach (var c in World.Chunks.Values)
        {
            if (c.DirtySections == 0) continue;
            int dxc = c.Cx - _camCx, dzc = c.Cz - _camCz;
            if (Math.Abs(dxc) > RenderDistance + 1 || Math.Abs(dzc) > RenderDistance + 1) continue;
            if (!c.Merged || !NeighborsReady(c)) continue;
            long key = MathUtil.ChunkKey(c.Cx, c.Cz);
            if (!_renders.TryGetValue(key, out var cr)) _renders[key] = cr = new ChunkRender();
            int dirty = c.DirtySections; c.DirtySections = 0;
            for (int sy = 0; sy < Chunk.Sections; sy++)
            {
                if ((dirty & (1 << sy)) == 0) continue;
                var sm = cr.S[sy];
                sm.Version++;
                if (c.SectionCounts[sy] == 0 && (sy == 0 || c.SectionCounts[sy - 1] == 0) && (sy == Chunk.Sections - 1 || c.SectionCounts[sy + 1] == 0) && sm.Solid == null && sm.Trans == null)
                { sm.BuiltVersion = sm.Version; continue; }
                double dist = Math.Abs(dxc) + Math.Abs(dzc) + Math.Abs(sy * 16 + 8 - cam.Y) / 16.0;
                if (dist <= 1.5 && sm.BuiltVersion >= 0) urgent.Add((c.Cx, sy, c.Cz, sm.Version, dist));
                else
                    lock (_jobLock)
                    {
                        if (_jobSet.Add((c.Cx, sy, c.Cz))) _jobs.Add((c.Cx, sy, c.Cz, sm.Version));
                        else { for (int i = 0; i < _jobs.Count; i++) if (_jobs[i].cx == c.Cx && _jobs[i].sy == sy && _jobs[i].cz == c.Cz) { _jobs[i] = (c.Cx, sy, c.Cz, sm.Version); break; } }
                    }
            }
        }
        // pilne: budowane od razu (edycja bloków przy graczu)
        foreach (var (cx, sy, cz, ver, _) in urgent)
        {
            _mainMesher.Build(World, cx, sy, cz);
            var sm = _renders[MathUtil.ChunkKey(cx, cz)].S[sy];
            Upload(sm, _mainMesher.Solid.Data, _mainMesher.Solid.Bytes, _mainMesher.Translucent.Data, _mainMesher.Translucent.Bytes);
            sm.BuiltVersion = ver;
        }
        // wyniki z wątków
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 4 && _done.TryDequeue(out var d))
        {
            if (!_renders.TryGetValue(MathUtil.ChunkKey(d.cx, d.cz), out var cr)) continue;
            var sm = cr.S[d.sy];
            if (d.ver < sm.BuiltVersion) continue;
            Upload(sm, d.solid, d.sb, d.trans, d.tb);
            sm.BuiltVersion = d.ver;
        }
    }

    public int PendingMeshes { get { lock (_jobLock) return _jobs.Count + _done.Count; } }

    // ================= frustum =================
    private readonly Vector4[] _planes = new Vector4[6];
    private void ExtractFrustum(Matrix4 m)
    {
        var c0 = new Vector4(m.M11, m.M21, m.M31, m.M41); var c1 = new Vector4(m.M12, m.M22, m.M32, m.M42);
        var c2 = new Vector4(m.M13, m.M23, m.M33, m.M43); var c3 = new Vector4(m.M14, m.M24, m.M34, m.M44);
        _planes[0] = c3 + c0; _planes[1] = c3 - c0; _planes[2] = c3 + c1; _planes[3] = c3 - c1; _planes[4] = c3 + c2; _planes[5] = c3 - c2;
    }
    private bool InFrustum(Vector3 min, Vector3 max)
    {
        foreach (var p in _planes)
        {
            float x = p.X >= 0 ? max.X : min.X, y = p.Y >= 0 ? max.Y : min.Y, z = p.Z >= 0 ? max.Z : min.Z;
            if (p.X * x + p.Y * y + p.Z * z + p.W < 0) return false;
        }
        return true;
    }

    // ================= stan kamery / mgły =================
    public Vector3 FogColor, SkyColor;
    public float SunBrightness = 1f;
    public float FogStart, FogEnd, FogDensity; public int FogMode;

    public void ComputeAtmosphere(Vector3d cam, Vector3 look, float pt, int headFluid)
    {
        var w = World;
        float angle = w.CelestialAngle(pt);
        float f1 = Math.Clamp(MathF.Cos(angle * MathF.PI * 2f) * 2f + 0.5f, 0, 1);
        byte bio = w.GetBiome(MathUtil.FloorToInt(cam.X), MathUtil.FloorToInt(cam.Z));
        var baseSky = bio == Biome.Desert ? new Vector3(0.43f, 0.69f, 1f) : new Vector3(0.48f, 0.66f, 1f);
        SkyColor = baseSky * f1;
        var fog = new Vector3(0.7529412f * (f1 * 0.94f + 0.06f), 0.84705883f * (f1 * 0.94f + 0.06f), 1f * (f1 * 0.91f + 0.09f));
        float rd = 1f - MathF.Pow(0.25f + 0.75f * RenderDistance / 32f, 0.25f);
        fog += (SkyColor - fog) * rd;
        // wschód/zachód
        var ss = SunsetColor(angle);
        if (ss.W > 0)
        {
            float sunX = -MathF.Sin(angle * MathF.PI * 2f) > 0 ? 1 : -1;
            float d = Math.Max(0, look.X * sunX);
            fog += (ss.Xyz - fog) * d * ss.W;
        }
        float sb = 1f - Math.Clamp(1f - (MathF.Cos(angle * MathF.PI * 2f) * 2f + 0.2f), 0, 1);
        SunBrightness = sb * 0.8f + 0.2f;
        FogColor = fog; FogMode = 0;
        FogEnd = RenderDistance * 16; FogStart = FogEnd * 0.75f;
        if (headFluid == 1) { FogColor = new Vector3(0.02f, 0.02f, 0.2f) + SkyColor * 0.1f; FogMode = 1; FogDensity = 0.08f; }
        else if (headFluid == 2) { FogColor = new Vector3(0.6f, 0.1f, 0f); FogMode = 1; FogDensity = 2f; }
        // pod ziemią ciemniej (void fog)
        if (cam.Y < 40 && headFluid == 0)
        {
            float f = (float)Math.Clamp((40 - cam.Y) / 40.0, 0, 1) * 0.6f;
            int sky = w.GetSkyLight(MathUtil.FloorToInt(cam.X), MathUtil.FloorToInt(cam.Y), MathUtil.FloorToInt(cam.Z));
            f *= 1f - sky / 15f;
            FogColor *= 1f - f;
        }
    }

    public static Vector4 SunsetColor(float angle)
    {
        float f2 = MathF.Cos(angle * MathF.PI * 2f);
        if (f2 >= -0.4f && f2 <= 0.4f)
        {
            float f5 = f2 / 0.4f * 0.5f + 0.5f;
            float f6 = 1f - (1f - MathF.Sin(f5 * MathF.PI)) * 0.99f; f6 *= f6;
            return new Vector4(f5 * 0.3f + 0.7f, f5 * f5 * 0.7f + 0.2f, 0.2f, f6);
        }
        return Vector4.Zero;
    }

    private void SetFog(Shader s)
    {
        s.Set("uFogColor", FogColor); s.Set("uFogStart", FogStart); s.Set("uFogEnd", FogEnd); s.Set("uFogDensity", FogDensity); s.Set("uFogMode", FogMode);
        s.Set("uSun", SunBrightness); s.Set("uGamma", Gamma);
    }

    // ================= rysowanie =================
    public void Render(Vector3d cam, Matrix4 view, Matrix4 proj, float pt, Entity viewer, bool thirdPerson, World.World.HitResult? sel, int breakStage)
    {
        var w = World;
        var vp = view * proj;
        ExtractFrustum(vp);
        var look = -new Vector3(view.M13, view.M23, view.M33);
        _camRightUp(view);

        // --- niebo ---
        GL.Disable(EnableCap.DepthTest); GL.DepthMask(false); GL.Disable(EnableCap.CullFace);
        float angle = w.CelestialAngle(pt);
        var sunDir = new Vector3(-MathF.Sin(angle * MathF.PI * 2f), MathF.Cos(angle * MathF.PI * 2f), 0);
        if (FogMode == 0)
        {
            _sky.Use();
            var inv = Matrix4.Invert(vp);
            _sky.Set("uInvViewProj", inv);
            _sky.Set("uSky", SkyColor); _sky.Set("uFog", FogColor);
            _sky.Set("uSunset", SunsetColor(angle)); _sky.Set("uSunDir", sunDir);
            _skyQuad.DrawQuads();
            // gwiazdy
            float starB = 1f - (MathF.Cos(angle * MathF.PI * 2f) * 2f + 0.25f); starB = Math.Clamp(starB, 0, 1); starB = starB * starB * 0.5f;
            GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            var rot = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(-90)) * Matrix4.CreateRotationX(angle * MathF.PI * 2f);
            rot = Matrix4.CreateRotationZ(angle * MathF.PI * 2f);
            if (starB > 0)
            {
                _line.Use(); _line.Set("uViewProj", rot * view.ClearTranslation() * proj); _line.Set("uColor", new Vector4(starB, starB, starB, starB));
                _starMesh.DrawQuads();
            }
            // słońce i księżyc
            _sunSh.Use();
            var skyVp = view.ClearTranslation() * proj;
            DrawCelestial(_sunTex, sunDir * 100, 30, skyVp, new Vector4(1, 1, 1, 1));
            DrawCelestial(_moonTex, -sunDir * 100, 20, skyVp, new Vector4(1, 1, 1, 1));
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }
        GL.DepthMask(true);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);

        // --- teren nieprzezroczysty ---
        GL.Enable(EnableCap.CullFace); GL.CullFace(CullFaceMode.Back); GL.FrontFace(FrontFaceDirection.Ccw);
        GL.Disable(EnableCap.Blend);
        _chunk.Use();
        _chunk.Set("uViewProj", vp); _chunk.Set("uTex", 0); _chunk.Set("uAlphaCut", 0.5f);
        SetFog(_chunk);
        Terrain.Bind(0);
        RenderedSections = 0; TotalSections = 0;
        var visible = new List<(SectionMesh sm, Vector3 off, float d)>();
        float maxD = (RenderDistance + 1) * 16;
        foreach (var kv in _renders)
        {
            int cx = (int)(kv.Key >> 32), cz = (int)kv.Key;
            double ox = cx * 16 - cam.X, oz = cz * 16 - cam.Z;
            if (Math.Abs(ox + 8) > maxD + 8 || Math.Abs(oz + 8) > maxD + 8) continue;
            for (int sy = 0; sy < Chunk.Sections; sy++)
            {
                var sm = kv.Value.S[sy];
                if (sm.Solid == null && sm.Trans == null) continue;
                TotalSections++;
                var off = new Vector3((float)ox, (float)(sy * 16 - cam.Y), (float)oz);
                if (!InFrustum(off, off + new Vector3(16, 16, 16))) continue;
                RenderedSections++;
                visible.Add((sm, off, (off + new Vector3(8)).LengthSquared));
                if (sm.Solid != null) { _chunk.Set("uOffset", off); sm.Solid.DrawQuads(); }
            }
        }

        // --- encje ---
        GL.Disable(EnableCap.CullFace);
        var er = Entities;
        er.Shader.Use(); er.Shader.Set("uViewProj", vp); SetFog(er.Shader);
        var camf = new Vector3((float)cam.X, (float)cam.Y, (float)cam.Z);
        foreach (var e in w.Entities)
        {
            if (e.Removed) continue;
            if (e == viewer && !thirdPerson) continue;
            if (e is Player pp && pp.Spectator) continue;
            if (e.DistanceSq(cam.X, cam.Y, cam.Z) > maxD * maxD) continue;
            var bb = e.BoundingBox.Grow(0.5);
            if (!InFrustum(new Vector3((float)(bb.MinX - cam.X), (float)(bb.MinY - cam.Y), (float)(bb.MinZ - cam.Z)), new Vector3((float)(bb.MaxX - cam.X), (float)(bb.MaxY - cam.Y), (float)(bb.MaxZ - cam.Z)))) continue;
            er.RenderEntity(e, pt, cam_(cam), w, false);
        }
        er.Flush(er.Skin, er.SkinTex);
        er.Flush(er.Terrain, Terrain);

        // --- cząsteczki ---
        RenderParticles(pt, cam);
        er.Flush(er.Terrain, Terrain);

        // --- pęknięcia ---
        if (sel.HasValue && breakStage >= 0)
        {
            var h = sel.Value;
            var boxes = new List<AABB>();
            Block.Get(w.GetBlock(h.X, h.Y, h.Z)).GetSelectionBoxes(w, h.X, h.Y, h.Z, w.GetMeta(h.X, h.Y, h.Z), boxes);
            GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.DstColor, BlendingFactor.SrcColor);
            GL.Enable(EnableCap.PolygonOffsetFill); GL.PolygonOffset(-3f, -3f); GL.DepthMask(false);
            er.Terrain.Sky = 15; er.Terrain.Blk = 15; er.Terrain.Color = Vector4.One; er.Terrain.Overlay = Vector4.Zero;
            er.Terrain.FlatShade = true;
            int layer = TextureGen.Layer("destroy_" + Math.Clamp(breakStage, 0, 9));
            foreach (var b in boxes)
            {
                var bb = b.Offset(h.X - cam.X, h.Y - cam.Y, h.Z - cam.Z).Grow(0.002);
                CrackBox(er.Terrain, bb, layer);
            }
            er.Shader.Set("uSun", 1f);
            er.Flush(er.Terrain, Terrain);
            er.Terrain.FlatShade = false;
            GL.Disable(EnableCap.PolygonOffsetFill); GL.DepthMask(true);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        // --- przezroczyste (woda) ---
        GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        _chunk.Use(); _chunk.Set("uAlphaCut", 0.0f); SetFog(_chunk);
        Terrain.Bind(0);
        visible.Sort((a, b) => b.d.CompareTo(a.d));
        foreach (var (sm, off, _) in visible)
            if (sm.Trans != null) { _chunk.Set("uOffset", off); sm.Trans.DrawQuads(); }

        // --- chmury ---
        if (CloudsEnabled && FogMode == 0) RenderClouds(cam, vp, pt);

        // --- obrys zaznaczenia ---
        if (sel.HasValue && sel.Value.Hit)
        {
            var h = sel.Value;
            var boxes = new List<AABB>();
            var bl = Block.Get(w.GetBlock(h.X, h.Y, h.Z));
            bl.GetSelectionBoxes(w, h.X, h.Y, h.Z, w.GetMeta(h.X, h.Y, h.Z), boxes);
            var v = new List<float>();
            foreach (var b in boxes)
            {
                var bb = b.Offset(h.X - cam.X, h.Y - cam.Y, h.Z - cam.Z).Grow(0.002);
                float x0 = (float)bb.MinX, y0 = (float)bb.MinY, z0 = (float)bb.MinZ, x1 = (float)bb.MaxX, y1 = (float)bb.MaxY, z1 = (float)bb.MaxZ;
                void L(float a, float b2, float c, float d, float e, float f) { v.Add(a); v.Add(b2); v.Add(c); v.Add(d); v.Add(e); v.Add(f); }
                L(x0, y0, z0, x1, y0, z0); L(x1, y0, z0, x1, y0, z1); L(x1, y0, z1, x0, y0, z1); L(x0, y0, z1, x0, y0, z0);
                L(x0, y1, z0, x1, y1, z0); L(x1, y1, z0, x1, y1, z1); L(x1, y1, z1, x0, y1, z1); L(x0, y1, z1, x0, y1, z0);
                L(x0, y0, z0, x0, y1, z0); L(x1, y0, z0, x1, y1, z0); L(x1, y0, z1, x1, y1, z1); L(x0, y0, z1, x0, y1, z1);
            }
            _lineMesh.Upload(v.ToArray(), v.Count);
            _line.Use(); _line.Set("uViewProj", vp); _line.Set("uColor", new Vector4(0, 0, 0, 0.45f));
            try { GL.LineWidth(2f); } catch { }
            _lineMesh.DrawArrays(PrimitiveType.Lines);
        }
        GL.Enable(EnableCap.CullFace);
    }

    private static Vector3 cam_(Vector3d c) => new((float)c.X, (float)c.Y, (float)c.Z);
    private void _camRightUp(Matrix4 view)
    {
        var right = new Vector3(view.M11, view.M21, view.M31); var up = new Vector3(view.M12, view.M22, view.M32);
        Entities.SetCameraAxes(right, up);
        _right = right; _up = up;
    }
    private Vector3 _right, _up;

    private void CrackBox(ModelBatch b, AABB bb, int layer)
    {
        b.Layer = layer;
        float x0 = (float)bb.MinX, y0 = (float)bb.MinY, z0 = (float)bb.MinZ, x1 = (float)bb.MaxX, y1 = (float)bb.MaxY, z1 = (float)bb.MaxZ;
        b.Quad(new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), new(x1, y1, z0), 0, 0, 1, 1, 1);
        b.Quad(new(x0, y0, z1), new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), 0, 0, 1, 1, 1);
        b.Quad(new(x1, y1, z0), new(x1, y0, z0), new(x0, y0, z0), new(x0, y1, z0), 0, 0, 1, 1, 1);
        b.Quad(new(x0, y1, z1), new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), 0, 0, 1, 1, 1);
        b.Quad(new(x0, y1, z0), new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), 0, 0, 1, 1, 1);
        b.Quad(new(x1, y1, z1), new(x1, y0, z1), new(x1, y0, z0), new(x1, y1, z0), 0, 0, 1, 1, 1);
    }

    private void DrawCelestial(Texture2D tex, Vector3 c, float size, Matrix4 vp, Vector4 col)
    {
        var n = Vector3.Normalize(c);
        var t1 = Vector3.Normalize(Vector3.Cross(n, Vector3.UnitZ)); var t2 = Vector3.Cross(n, t1);
        t1 *= size / 2; t2 *= size / 2;
        var pts = new[] { c - t1 - t2, c - t1 + t2, c + t1 + t2, c + t1 - t2 };
        float[] uv = { 0, 0, 0, 1, 1, 1, 1, 0 };
        var v = new float[20];
        for (int i = 0; i < 4; i++) { v[i * 5] = pts[i].X; v[i * 5 + 1] = pts[i].Y; v[i * 5 + 2] = pts[i].Z; v[i * 5 + 3] = uv[i * 2]; v[i * 5 + 4] = uv[i * 2 + 1]; }
        _sunMesh.Upload(v, 20);
        _sunSh.Set("uViewProj", vp); _sunSh.Set("uColor", col); _sunSh.Set("uTex", 0);
        tex.Bind(0);
        _sunMesh.DrawQuads();
    }

    private void RenderClouds(Vector3d cam, Matrix4 vp, float pt)
    {
        double scroll = (World.TotalTime + pt) * 0.03;
        double tile = CloudCells * CloudCell;
        double cx = cam.X + scroll, cz = cam.Z + 3.96;
        double baseX = Math.Floor(cx / tile) * tile, baseZ = Math.Floor(cz / tile) * tile;
        float f1 = Math.Clamp(MathF.Cos(World.CelestialAngle(pt) * MathF.PI * 2f) * 2f + 0.5f, 0, 1);
        var col = new Vector3(f1 * 0.9f + 0.1f, f1 * 0.9f + 0.1f, f1 * 0.85f + 0.15f);
        _cloudSh.Use(); _cloudSh.Set("uViewProj", vp); _cloudSh.Set("uColor", col); _cloudSh.Set("uFogColor", FogColor); _cloudSh.Set("uFogEnd", FogEnd);
        GL.Enable(EnableCap.CullFace);
        for (int pass = 0; pass < 2; pass++)
        {
            if (pass == 0) GL.ColorMask(false, false, false, false); else GL.ColorMask(true, true, true, true);
            for (int ix = -1; ix <= 0; ix++)
                for (int iz = -1; iz <= 0; iz++)
                {
                    double tx = baseX + ix * tile + (cx - baseX > tile / 2 ? tile : 0);
                    double tz = baseZ + iz * tile + (cz - baseZ > tile / 2 ? tile : 0);
                    var off = new Vector3((float)(tx - cx), (float)(CloudY - cam.Y), (float)(tz - cz));
                    _cloudSh.Set("uOffset", off);
                    _cloudMesh.DrawQuads();
                }
        }
        GL.ColorMask(true, true, true, true);
    }

    // ================= animowane tekstury =================
    private int _animTick;
    public void TickAnimations()
    {
        _animTick++;
        if (_animTick % 2 != 0) return;
        if (TextureGen.Animations.TryGetValue("water_still", out var wf)) Terrain.UpdateLayer(_waterLayer, wf[(_animTick / 2) % wf.Count]);
        if (_animTick % 4 == 0 && TextureGen.Animations.TryGetValue("lava_still", out var lf)) Terrain.UpdateLayer(_lavaLayer, lf[(_animTick / 4) % lf.Count]);
    }

    // ================= cząsteczki =================
    private readonly Rng _pr = new(5);
    public void TickParticles()
    {
        var w = World;
        for (int i = Particles.Count - 1; i >= 0; i--)
        {
            var p = Particles[i];
            p.Prev = p.Pos;
            p.Age++;
            if (p.Age >= p.Life) { Particles.RemoveAt(i); continue; }
            p.Vel.Y -= p.Gravity;
            var np = p.Pos + p.Vel;
            if (p.Collide && w != null)
            {
                int bx = MathUtil.FloorToInt(np.X), by = MathUtil.FloorToInt(np.Y), bz = MathUtil.FloorToInt(np.Z);
                var b = Block.Get(w.GetBlock(bx, by, bz));
                if (b.Solid && b.Opaque)
                {
                    if (MathUtil.FloorToInt(p.Pos.Y) != by) { np.Y = p.Pos.Y; p.Vel.Y = 0; p.Vel.X *= 0.7f; p.Vel.Z *= 0.7f; }
                    else { np = p.Pos; p.Vel *= 0.3f; }
                }
            }
            p.Pos = np;
            p.Vel *= 0.98f;
            Particles[i] = p;
        }
        if (Particles.Count > 3000) Particles.RemoveRange(0, Particles.Count - 3000);
    }

    private void RenderParticles(float pt, Vector3d cam)
    {
        var b = Entities.Terrain;
        var w = World;
        foreach (var p in Particles)
        {
            var pos = Vector3.Lerp(p.Prev, p.Pos, pt);
            var rel = new Vector3((float)(pos.X - cam.X), (float)(pos.Y - cam.Y), (float)(pos.Z - cam.Z));
            if (rel.LengthSquared > 4096) continue;
            int bx = MathUtil.FloorToInt(pos.X), by = MathUtil.FloorToInt(pos.Y), bz = MathUtil.FloorToInt(pos.Z);
            b.Sky = p.Emissive ? 15 : w.GetSkyLight(bx, by, bz); b.Blk = p.Emissive ? 15 : w.GetBlockLight(bx, by, bz);
            b.Layer = p.Layer; b.Color = p.Color; b.Overlay = Vector4.Zero;
            float size = p.Size;
            if (p.Grow) size *= 1f + (p.Age + pt) / p.Life;
            var r = _right * size * 0.5f; var u = _up * size * 0.5f;
            b.Quad(rel - r + u, rel - r - u, rel + r - u, rel + r + u, p.U0, p.V0, p.U1, p.V1, 1f);
        }
        b.Color = Vector4.One;
    }

    public void AddParticle(Vector3 pos, Vector3 vel, int layer, float size, int life, float gravity, Vector4 color, bool collide = true, bool emissive = false, float u0 = 0, float v0 = 0, float u1 = 1, float v1 = 1, bool grow = false)
    {
        if (Particles.Count > 4000) return;
        Particles.Add(new Particle { Pos = pos, Prev = pos, Vel = vel, Layer = layer, Size = size, Life = life, Gravity = gravity, Color = color, Collide = collide, Emissive = emissive, U0 = u0, V0 = v0, U1 = u1, V1 = v1, Grow = grow });
    }

    public void BlockCrackParticle(int x, int y, int z, int face)
    {
        var b = Block.Get(World.GetBlock(x, y, z));
        if (b.Id == 0) return;
        float px = x + _pr.NextFloat() * 0.8f + 0.1f, py = y + _pr.NextFloat() * 0.8f + 0.1f, pz = z + _pr.NextFloat() * 0.8f + 0.1f;
        if (face == 0) py = y - 0.1f; if (face == 1) py = y + 1.1f; if (face == 2) pz = z - 0.1f; if (face == 3) pz = z + 1.1f; if (face == 4) px = x - 0.1f; if (face == 5) px = x + 1.1f;
        BlockParticle(new Vector3(px, py, pz), b, World.GetMeta(x, y, z), new Vector3(0, 0.05f, 0) * 0.2f, 0.6f);
    }

    private void BlockParticle(Vector3 pos, Block b, int meta, Vector3 vel, float sizeMul)
    {
        int tex = b.GetTexture(_pr.Next(6), meta);
        if (b.Id == B.Grass) tex = TextureGen.Layer("dirt");
        float u = _pr.Next(12) / 16f, v = _pr.Next(12) / 16f;
        var col = Vector4.One;
        if (b.Tint == TintType.Grass) col = EntityRenderer.HexColor(0x79C05A);
        if (b.Tint == TintType.Foliage) col = EntityRenderer.HexColor(0x59AE30);
        if (b.Tint == TintType.Birch) col = EntityRenderer.HexColor(0x80A755);
        col *= 0.6f + 0.4f * _pr.NextFloat(); col.W = 1;
        AddParticle(pos, vel, tex, 0.1f * sizeMul + _pr.NextFloat() * 0.1f, 4 + _pr.Next(20), 0.04f, col, true, false, u, v, u + 4 / 16f, v + 4 / 16f);
    }

    // ================= IWorldListener =================
    public void OnBlockChanged(int x, int y, int z, byte id, int meta) { }
    public void OnEntityAdded(Entity e) { }
    public void OnEntityRemoved(Entity e) { }
    public void PlaySound(string name, double x, double y, double z, float volume, float pitch) => SoundHook?.Invoke(name, x, y, z, volume, pitch);

    public void SpawnParticles(ParticleKind kind, double x, double y, double z, int count, int data)
    {
        var p = new Vector3((float)x, (float)y, (float)z);
        var r = _pr;
        Vector3 Rnd(float s) => new((r.NextFloat() - 0.5f) * s, (r.NextFloat() - 0.5f) * s, (r.NextFloat() - 0.5f) * s);
        switch (kind)
        {
            case ParticleKind.BlockBreak:
                if (data < 0)
                {
                    var it = Item.ById[-data];
                    int layer = it != null ? it.GetSprite(new ItemStack(-data)) : 0;
                    for (int i = 0; i < count; i++)
                    {
                        float u = r.Next(12) / 16f, v = r.Next(12) / 16f;
                        AddParticle(p + Rnd(0.3f), new Vector3((r.NextFloat() - 0.5f) * 0.1f, 0.1f + r.NextFloat() * 0.05f, (r.NextFloat() - 0.5f) * 0.1f), layer, 0.12f, 10 + r.Next(10), 0.04f, Vector4.One, true, false, u, v, u + 4 / 16f, v + 4 / 16f);
                    }
                }
                else
                {
                    var b = Block.Get(data & 255);
                    int meta = data >> 8;
                    int bx = MathUtil.FloorToInt(x), by = MathUtil.FloorToInt(y), bz = MathUtil.FloorToInt(z);
                    for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) for (int k = 0; k < 4; k++)
                            {
                                if (r.Next(2) == 0) continue;
                                var pos = new Vector3(bx + (i + 0.5f) / 4f, by + (j + 0.5f) / 4f, bz + (k + 0.5f) / 4f);
                                var vel = (pos - new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f)) * 0.3f + new Vector3(0, 0.08f, 0);
                                BlockParticle(pos, b, meta, vel * 0.6f, 1f);
                            }
                }
                break;
            case ParticleKind.Smoke:
            case ParticleKind.LargeSmoke:
                for (int i = 0; i < count; i++)
                    AddParticle(p + Rnd(0.3f), new Vector3(0, 0.02f + r.NextFloat() * 0.02f, 0) + Rnd(0.02f), TextureGen.Layer("particle_smoke_" + r.Next(4)), kind == ParticleKind.LargeSmoke ? 0.5f : 0.2f, 20 + r.Next(20), -0.002f, new Vector4(0.3f, 0.3f, 0.3f, 0.8f), false, false, 0, 0, 1, 1, true);
                break;
            case ParticleKind.Explosion:
                for (int i = 0; i < 16; i++)
                    AddParticle(p + Rnd(4f), Rnd(0.1f), TextureGen.Layer("particle_explosion_" + r.Next(4)), 1.5f + r.NextFloat() * 1.5f, 6 + r.Next(6), 0, new Vector4(0.9f, 0.9f, 0.9f, 1), false, true);
                for (int i = 0; i < 30; i++)
                    AddParticle(p + Rnd(3f), Rnd(0.3f), TextureGen.Layer("particle_smoke_" + r.Next(8)), 0.6f, 20 + r.Next(30), -0.003f, new Vector4(0.25f, 0.25f, 0.25f, 0.9f), false, false, 0, 0, 1, 1, true);
                break;
            case ParticleKind.Flame:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(0.05f), new Vector3(0, 0.005f, 0), TextureGen.Layer("particle_flame"), 0.1f, 10 + r.Next(10), 0, Vector4.One, false, true);
                break;
            case ParticleKind.Splash:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(1f), new Vector3((r.NextFloat() - 0.5f) * 0.2f, 0.15f + r.NextFloat() * 0.1f, (r.NextFloat() - 0.5f) * 0.2f), TextureGen.Layer("particle_splash"), 0.1f, 10 + r.Next(10), 0.04f, Vector4.One);
                break;
            case ParticleKind.Bubble:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(0.5f), new Vector3(0, 0.05f, 0) + Rnd(0.02f), TextureGen.Layer("particle_bubble"), 0.1f, 15 + r.Next(10), -0.01f, Vector4.One, false);
                break;
            case ParticleKind.Heart:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(0.8f), new Vector3(0, 0.03f, 0), TextureGen.Layer("particle_heart"), 0.25f, 20, -0.002f, Vector4.One, false, true);
                break;
            case ParticleKind.Angry:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(0.8f), new Vector3(0, 0.03f, 0), TextureGen.Layer("particle_angry"), 0.25f, 20, 0, Vector4.One, false, true);
                break;
            case ParticleKind.Portal:
                for (int i = 0; i < Math.Min(count, 40); i++) AddParticle(p + Rnd(1f), Rnd(0.2f), TextureGen.Layer("particle_portal"), 0.12f, 20 + r.Next(20), 0, new Vector4(0.8f, 0.4f, 1f, 1), false, true);
                break;
            case ParticleKind.Crit:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(0.6f), Rnd(0.4f), TextureGen.Layer("particle_crit"), 0.15f, 10 + r.Next(5), 0.02f, new Vector4(0.9f, 0.9f, 0.6f, 1), false, true);
                break;
            case ParticleKind.Poof:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(1f), Rnd(0.1f) + new Vector3(0, 0.02f, 0), TextureGen.Layer("particle_smoke_" + r.Next(8)), 0.3f, 15 + r.Next(10), 0, new Vector4(0.9f, 0.9f, 0.9f, 1), false, false, 0, 0, 1, 1, true);
                break;
            case ParticleKind.RedDust:
                for (int i = 0; i < count; i++) AddParticle(p + Rnd(0.3f), Vector3.Zero, TextureGen.Layer("particle_white"), 0.08f, 10, 0, new Vector4(1, 0.1f, 0.1f, 1), false, true);
                break;
        }
    }

    public void Dispose()
    {
        _running = false;
        foreach (var t in _workers) t.Join(500);
        SetWorld(null);
        _chunk.Dispose(); _sky.Dispose(); _cloudSh.Dispose(); _line.Dispose(); _sunSh?.Dispose();
        _skyQuad.Dispose(); _sunMesh.Dispose(); _starMesh.Dispose(); _lineMesh.Dispose(); _cloudMesh?.Dispose();
        Entities.Dispose(); Terrain.Dispose(); _sunTex.Dispose(); _moonTex.Dispose();
    }
}
