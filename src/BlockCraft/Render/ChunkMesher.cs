using System.Runtime.CompilerServices;
using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Render;

/// <summary>Bufor wierzchołków (28 bajtów na wierzchołek).</summary>
public sealed class VertexBuffer
{
    public const int Stride = 28;
    public byte[] Data = new byte[Stride * 4096];
    public int Count; // wierzchołki
    public int Bytes => Count * Stride;
    public void Clear() => Count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void V(float x, float y, float z, float u, float v, int layer, int flags, float shade, float sky, float blk, uint tint)
    {
        int o = Count * Stride;
        if (o + Stride > Data.Length) Array.Resize(ref Data, Data.Length * 2);
        fixed (byte* p = &Data[o])
        {
            *(float*)p = x; *(float*)(p + 4) = y; *(float*)(p + 8) = z;
            *(ushort*)(p + 12) = (ushort)Math.Clamp((int)(u * 4096f + 32768f), 0, 65535);
            *(ushort*)(p + 14) = (ushort)Math.Clamp((int)(v * 4096f + 32768f), 0, 65535);
            *(ushort*)(p + 16) = (ushort)layer;
            p[18] = (byte)flags;
            p[19] = (byte)Math.Clamp((int)(shade * 255f + 0.5f), 0, 255);
            p[20] = (byte)Math.Clamp((int)(sky * 17f + 0.5f), 0, 255);
            p[21] = (byte)Math.Clamp((int)(blk * 17f + 0.5f), 0, 255);
            p[22] = 0; p[23] = 0;
            *(uint*)(p + 24) = tint;
        }
        Count++;
    }
}

public sealed partial class ChunkMesher
{
    // wyściełane dane 18x18x18
    private const int P = 18;
    private readonly byte[] _id = new byte[P * P * P];
    private readonly byte[] _meta = new byte[P * P * P];
    private readonly byte[] _sky = new byte[P * P * P];
    private readonly byte[] _blk = new byte[P * P * P];
    private readonly uint[] _grass = new uint[P * P], _foliage = new uint[P * P];
    private readonly bool[] _opaque = new bool[256];
    public readonly VertexBuffer Solid = new(), Translucent = new();
    public static bool FancyLeaves = true;
    public static bool SmoothLighting = true;
    private int _ox, _oy, _oz; // początek sekcji w świecie
    private readonly List<ModelBox> _boxes = new();
    private readonly SnapshotAccess _access;
    private static int _leavesOpaqueOak = -1, _leavesOpaqueBirch = -1;

    private static readonly float[] FaceShade = { 0.5f, 1.0f, 0.8f, 0.8f, 0.6f, 0.6f };

    public ChunkMesher() { _access = new SnapshotAccess(this); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Idx(int x, int y, int z) => ((y + 1) * P + (z + 1)) * P + (x + 1);

    private sealed class SnapshotAccess : IBlockAccess
    {
        private readonly ChunkMesher _m;
        public SnapshotAccess(ChunkMesher m) { _m = m; }
        public byte GetBlock(int x, int y, int z)
        {
            int lx = x - _m._ox, ly = y - _m._oy, lz = z - _m._oz;
            if (lx < -1 || lx > 16 || ly < -1 || ly > 16 || lz < -1 || lz > 16) return _m._world?.GetBlock(x, y, z) ?? 0;
            return _m._id[Idx(lx, ly, lz)];
        }
        public int GetMeta(int x, int y, int z)
        {
            int lx = x - _m._ox, ly = y - _m._oy, lz = z - _m._oz;
            if (lx < -1 || lx > 16 || ly < -1 || ly > 16 || lz < -1 || lz > 16) return _m._world?.GetMeta(x, y, z) ?? 0;
            return _m._meta[Idx(lx, ly, lz)];
        }
    }
    private World.World _world;

    private void Snapshot(World.World w, int cx, int sy, int cz)
    {
        _world = w;
        _ox = cx * 16; _oy = sy * 16; _oz = cz * 16;
        var chunks = new Chunk[9];
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) chunks[(dz + 1) * 3 + dx + 1] = w.GetChunk(cx + dx, cz + dz);
        for (int z = -1; z <= 16; z++)
            for (int x = -1; x <= 16; x++)
            {
                int ccx = x < 0 ? 0 : x > 15 ? 2 : 1, ccz = z < 0 ? 0 : z > 15 ? 2 : 1;
                var c = chunks[ccz * 3 + ccx];
                int lx = x & 15, lz = z & 15;
                for (int y = -1; y <= 16; y++)
                {
                    int wy = _oy + y;
                    int i = Idx(x, y, z);
                    if (c == null) { _id[i] = B.Stone; _meta[i] = 0; _sky[i] = 0; _blk[i] = 0; continue; }
                    if (wy < 0) { _id[i] = B.Bedrock; _meta[i] = 0; _sky[i] = 0; _blk[i] = 0; continue; }
                    if (wy >= Chunk.Height) { _id[i] = 0; _meta[i] = 0; _sky[i] = 15; _blk[i] = 0; continue; }
                    int ci = Chunk.Index(lx, wy, lz);
                    _id[i] = c.Blocks[ci]; _meta[i] = c.Meta[ci];
                    byte l = c.Light[ci]; _sky[i] = (byte)(l >> 4); _blk[i] = (byte)(l & 15);
                }
                // kolory biomu (średnia 3x3 liczona niżej)
                byte bio = c != null ? c.Biomes[(lz << 4) | lx] : (byte)0;
                _grass[(z + 1) * P + x + 1] = Biome.GrassColor(bio);
                _foliage[(z + 1) * P + x + 1] = Biome.FoliageColor(bio);
            }
        for (int i = 0; i < 256; i++) { var b = Block.ById[i]; _opaque[i] = b != null && b.Opaque; }
        if (!FancyLeaves) { _opaque[B.OakLeaves] = true; _opaque[B.BirchLeaves] = true; }
    }

    private uint BlendColor(uint[] arr, int x, int z)
    {
        int r = 0, g = 0, b = 0, n = 0;
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int xx = x + dx, zz = z + dz;
                if (xx < -1 || xx > 16 || zz < -1 || zz > 16) continue;
                uint c = arr[(zz + 1) * P + xx + 1];
                r += (int)(c >> 16) & 255; g += (int)(c >> 8) & 255; b += (int)c & 255; n++;
            }
        return (uint)((r / n) | ((g / n) << 8) | ((b / n) << 16) | (255u << 24));
    }

    private static uint Rgb(uint hex) => ((hex >> 16) & 255) | (hex & 0xFF00) | ((hex & 255) << 16) | 0xFF000000;

    private uint TintFor(Block b, int x, int y, int z, int meta)
    {
        switch (b.Tint)
        {
            case TintType.Grass: return BlendColor(_grass, x, z);
            case TintType.Foliage: return BlendColor(_foliage, x, z);
            case TintType.Birch: return Rgb(0x80A755);
            case TintType.RedstoneWire:
                {
                    float f = meta / 15f;
                    float r = f * 0.6f + 0.4f; if (meta == 0) r = 0.3f;
                    float g = Math.Max(0, f * f * 0.7f - 0.5f), bl = Math.Max(0, f * f * 0.6f - 0.7f);
                    return (uint)((int)(r * 255) | ((int)(g * 255) << 8) | ((int)(bl * 255) << 16) | (255 << 24));
                }
        }
        return 0xFFFFFFFF;
    }

    public void Build(World.World w, int cx, int sy, int cz)
    {
        Solid.Clear(); Translucent.Clear();
        Snapshot(w, cx, sy, cz);
        if (_leavesOpaqueOak < 0) { _leavesOpaqueOak = TextureGen.Layer("leaves_oak_opaque"); _leavesOpaqueBirch = TextureGen.Layer("leaves_birch_opaque"); }
        for (int y = 0; y < 16; y++)
            for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                {
                    int i = Idx(x, y, z);
                    byte id = _id[i];
                    if (id == 0) continue;
                    var b = Block.ById[id];
                    int meta = _meta[i];
                    switch (b.Render)
                    {
                        case RenderType.Cube: Cube(b, x, y, z, meta); break;
                        case RenderType.Cross: Cross(b, x, y, z, meta); break;
                        case RenderType.Crops: Crops(b, x, y, z, meta); break;
                        case RenderType.Liquid: Liquid(b, x, y, z, meta); break;
                        case RenderType.Torch: Torch(b, x, y, z, meta); break;
                        case RenderType.Model: Model(b, x, y, z, meta); break;
                        case RenderType.Wire: Wire(b, x, y, z, meta); break;
                        case RenderType.Cactus: Cactus(b, x, y, z, meta); break;
                    }
                }
    }

    // ---------------- sześciany ----------------
    private bool ShouldDrawFace(Block b, int x, int y, int z, int face)
    {
        int ni = Idx(x + Dir.X[face], y + Dir.Y[face], z + Dir.Z[face]);
        byte nid = _id[ni];
        if (_opaque[nid])
        {
            if (nid == B.Piston || nid == B.StickyPiston) return !Block.ById[nid].IsOpaqueCube(_meta[ni]);
            return false;
        }
        if (nid == b.Id && (b.Id == B.Glass || b.Id == B.Ice)) return false;
        if (!FancyLeaves && nid == b.Id && (b.Id == B.OakLeaves || b.Id == B.BirchLeaves)) return false;
        var nb = Block.ById[nid];
        if (nb is SlabBlock && b.Opaque)
        {
            // dolna płyta zasłania spód bloku nad nią
            int nm = _meta[ni];
            if (face == Dir.Down && nm == 1) return false;
            if (face == Dir.Up && nm == 0) return false;
        }
        return true;
    }

    // wektory osi na ścianie (u, v) w kierunku narastania – do AO
    private static readonly int[][] FaceVerts =
    {
        // każda ściana: 4 wierzchołki (x,y,z) w jednostkach bloku, kolejność przeciwnie do wskazówek patrząc z zewnątrz
        new[] { 0,0,1, 0,0,0, 1,0,0, 1,0,1 }, // dół
        new[] { 0,1,0, 0,1,1, 1,1,1, 1,1,0 }, // góra
        new[] { 1,1,0, 1,0,0, 0,0,0, 0,1,0 }, // północ (-Z)
        new[] { 0,1,1, 0,0,1, 1,0,1, 1,1,1 }, // południe (+Z)
        new[] { 0,1,0, 0,0,0, 0,0,1, 0,1,1 }, // zachód (-X)
        new[] { 1,1,1, 1,0,1, 1,0,0, 1,1,0 }, // wschód (+X)
    };
    // UV dla wierzchołków (u,v)
    private static readonly float[][] FaceUV =
    {
        new float[] { 0,1, 0,0, 1,0, 1,1 },
        new float[] { 0,0, 0,1, 1,1, 1,0 },
        new float[] { 0,0, 0,1, 1,1, 1,0 },
        new float[] { 0,0, 0,1, 1,1, 1,0 },
        new float[] { 0,0, 0,1, 1,1, 1,0 },
        new float[] { 0,0, 0,1, 1,1, 1,0 },
    };

    private readonly float[] _vl = new float[4], _vs = new float[4], _vb = new float[4];

    private void Cube(Block b, int x, int y, int z, int meta)
    {
        bool translucent = b.Layer == RenderLayer.Translucent;
        var buf = translucent ? Translucent : Solid;
        uint tint = TintFor(b, x, y, z, meta);
        bool leaves = !FancyLeaves && (b.Id == B.OakLeaves || b.Id == B.BirchLeaves);
        for (int f = 0; f < 6; f++)
        {
            if (!ShouldDrawFace(b, x, y, z, f)) continue;
            int tex = b.GetTexture(f, meta);
            if (leaves) tex = b.Id == B.OakLeaves ? _leavesOpaqueOak : _leavesOpaqueBirch;
            uint t = tint;
            int flags = 0;
            if (b.TintMaskSides && f >= 2) flags = 1;
            if (b.Tint == TintType.Grass && f == Dir.Down) t = 0xFFFFFFFF;
            if (b.Tint == TintType.Grass && f >= 2 && !b.TintMaskSides) { }
            Face(buf, x, y, z, f, tex, flags, t, b.LightEmit > 0);
        }
    }

    private void Face(VertexBuffer buf, int x, int y, int z, int f, int tex, int flags, uint tint, bool emissive)
    {
        int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
        var fv = FaceVerts[f]; var uv = FaceUV[f];
        float shade = FaceShade[f];
        for (int k = 0; k < 4; k++)
        {
            int vx = fv[k * 3], vy = fv[k * 3 + 1], vz = fv[k * 3 + 2];
            if (SmoothLighting)
            {
                // dwie osie styczne do ściany
                int ax, ay, az, bx, by, bz;
                if (f <= 1) { ax = vx == 0 ? -1 : 1; ay = 0; az = 0; bx = 0; by = 0; bz = vz == 0 ? -1 : 1; }
                else if (f <= 3) { ax = vx == 0 ? -1 : 1; ay = 0; az = 0; bx = 0; by = vy == 0 ? -1 : 1; bz = 0; }
                else { ax = 0; ay = vy == 0 ? -1 : 1; az = 0; bx = 0; by = 0; bz = vz == 0 ? -1 : 1; }
                int c0 = Idx(nx, ny, nz);
                int c1 = Idx(nx + ax, ny + ay, nz + az);
                int c2 = Idx(nx + bx, ny + by, nz + bz);
                int c3 = Idx(nx + ax + bx, ny + ay + by, nz + az + bz);
                bool o1 = _opaque[_id[c1]], o2 = _opaque[_id[c2]], o3 = _opaque[_id[c3]] || (o1 && o2);
                float sky = _sky[c0], blk = _blk[c0]; int n = 1;
                if (!o1) { sky += _sky[c1]; blk += _blk[c1]; n++; }
                if (!o2) { sky += _sky[c2]; blk += _blk[c2]; n++; }
                if (!o3) { sky += _sky[c3]; blk += _blk[c3]; n++; }
                _vs[k] = sky / n; _vb[k] = blk / n;
                float ao = (1f + (o1 ? 0.2f : 1f) + (o2 ? 0.2f : 1f) + (o3 ? 0.2f : 1f)) / 4f;
                _vl[k] = ao;
            }
            else
            {
                int c0 = Idx(nx, ny, nz);
                _vs[k] = _sky[c0]; _vb[k] = _blk[c0]; _vl[k] = 1f;
            }
            if (emissive) _vb[k] = 15;
        }
        // przekątna zależna od AO
        bool flip = _vl[0] + _vl[2] < _vl[1] + _vl[3];
        int s = flip ? 1 : 0;
        for (int j = 0; j < 4; j++)
        {
            int k = (j + s) & 3;
            buf.V(x + fv[k * 3], y + fv[k * 3 + 1], z + fv[k * 3 + 2], uv[k * 2], uv[k * 2 + 1], tex, flags, shade * _vl[k], _vs[k], _vb[k], tint);
        }
    }

    // ---------------- rośliny ----------------
    private void Cross(Block b, int x, int y, int z, int meta)
    {
        int tex = b.GetTexture(0, meta);
        uint tint = TintFor(b, x, y, z, meta);
        int i = Idx(x, y, z);
        float sky = _sky[i], blk = _blk[i];
        float ox = 0, oz = 0;
        if (b.Id == B.TallGrass || b.Id == B.Dandelion || b.Id == B.Poppy)
        {
            uint h = MathUtil.HashU(x + _ox, 0, z + _oz);
            ox = ((h & 15) / 15f - 0.5f) * 0.5f; oz = (((h >> 8) & 15) / 15f - 0.5f) * 0.5f;
        }
        float a0 = 0.5f - 0.45f, a1 = 0.5f + 0.45f;
        float h1 = 1f;
        Quad2(Solid, x + a0 + ox, y, z + a0 + oz, x + a1 + ox, y + h1, z + a1 + oz, tex, tint, sky, blk, 1f);
        Quad2(Solid, x + a0 + ox, y, z + a1 + oz, x + a1 + ox, y + h1, z + a0 + oz, tex, tint, sky, blk, 1f);
    }

    /// <summary>Pionowy czworokąt dwustronny z (x0,y0,z0) do (x1,y1,z1).</summary>
    private void Quad2(VertexBuffer buf, float x0, float y0, float z0, float x1, float y1, float z1, int tex, uint tint, float sky, float blk, float shade, float v0 = 0, float v1 = 1)
    {
        buf.V(x0, y1, z0, 0, v0, tex, 0, shade, sky, blk, tint);
        buf.V(x0, y0, z0, 0, v1, tex, 0, shade, sky, blk, tint);
        buf.V(x1, y0, z1, 1, v1, tex, 0, shade, sky, blk, tint);
        buf.V(x1, y1, z1, 1, v0, tex, 0, shade, sky, blk, tint);
        buf.V(x1, y1, z1, 1, v0, tex, 0, shade, sky, blk, tint);
        buf.V(x1, y0, z1, 1, v1, tex, 0, shade, sky, blk, tint);
        buf.V(x0, y0, z0, 0, v1, tex, 0, shade, sky, blk, tint);
        buf.V(x0, y1, z0, 0, v0, tex, 0, shade, sky, blk, tint);
    }

    private void Crops(Block b, int x, int y, int z, int meta)
    {
        int tex = b.GetTexture(0, meta);
        int i = Idx(x, y, z);
        float sky = _sky[i], blk = _blk[i];
        float yo = -1 / 16f;
        foreach (float p in new[] { 0.25f, 0.75f })
        {
            Quad2(Solid, x + p, y + yo, z, x + p, y + 1 + yo, z + 1, tex, 0xFFFFFFFF, sky, blk, 0.9f);
            Quad2(Solid, x, y + yo, z + p, x + 1, y + 1 + yo, z + p, tex, 0xFFFFFFFF, sky, blk, 0.9f);
        }
    }

    // ---------------- płyny ----------------
    private float FluidHeightAt(int x, int y, int z, byte id)
    {
        // wysokość w narożniku (x,z) – średnia 4 komórek
        float sum = 0; int n = 0;
        for (int k = 0; k < 4; k++)
        {
            int cx = x - (k & 1), cz = z - (k >> 1);
            int i = Idx(cx, y, cz);
            if (_id[Idx(cx, y + 1, cz)] == id) return 1f;
            if (_id[i] == id)
            {
                int m = _meta[i];
                float h = FluidBlock.HeightOf(m);
                if (m == 0 || (m & 8) != 0) { sum += h * 10; n += 10; }
                else { sum += h; n++; }
            }
            else if (!Block.ById[_id[i]].Solid) { n++; }
        }
        return n == 0 ? 0 : sum / n;
    }

    private void Liquid(Block b, int x, int y, int z, int meta)
    {
        var fb = (FluidBlock)b;
        var buf = fb.IsLava ? Solid : Translucent;
        int tex = b.Tex[0];
        byte id = b.Id;
        bool above = _id[Idx(x, y + 1, z)] == id;
        float h00 = FluidHeightAt(x, y, z, id), h10 = FluidHeightAt(x + 1, y, z, id), h01 = FluidHeightAt(x, y, z + 1, id), h11 = FluidHeightAt(x + 1, y, z + 1, id);
        if (above) h00 = h10 = h01 = h11 = 1;
        bool emissive = fb.IsLava;
        int ci = Idx(x, y, z);
        float L(int i, bool sky) => emissive && !sky ? 15 : (sky ? _sky[i] : _blk[i]);
        // góra
        if (!above)
        {
            int ui = Idx(x, y + 1, z);
            float sky = Math.Max(_sky[ui], _sky[ci]), blk = emissive ? 15 : Math.Max(_blk[ui], _blk[ci]);
            const float e = 0.001f;
            buf.V(x, y + h00 - e, z, 0, 0, tex, 0, 1f, sky, blk, 0xFFFFFFFF);
            buf.V(x, y + h01 - e, z + 1, 0, 1, tex, 0, 1f, sky, blk, 0xFFFFFFFF);
            buf.V(x + 1, y + h11 - e, z + 1, 1, 1, tex, 0, 1f, sky, blk, 0xFFFFFFFF);
            buf.V(x + 1, y + h10 - e, z, 1, 0, tex, 0, 1f, sky, blk, 0xFFFFFFFF);
        }
        // dół
        {
            int di = Idx(x, y - 1, z);
            byte did = _id[di];
            if (did != id && !_opaque[did])
            {
                float sky = _sky[di], blk = emissive ? 15 : _blk[di];
                buf.V(x, y, z + 1, 0, 1, tex, 0, 0.5f, sky, blk, 0xFFFFFFFF);
                buf.V(x, y, z, 0, 0, tex, 0, 0.5f, sky, blk, 0xFFFFFFFF);
                buf.V(x + 1, y, z, 1, 0, tex, 0, 0.5f, sky, blk, 0xFFFFFFFF);
                buf.V(x + 1, y, z + 1, 1, 1, tex, 0, 0.5f, sky, blk, 0xFFFFFFFF);
            }
        }
        // boki
        for (int f = 2; f < 6; f++)
        {
            int ni = Idx(x + Dir.X[f], y, z + Dir.Z[f]);
            byte nid = _id[ni];
            if (nid == id || _opaque[nid]) continue;
            float sky = _sky[ni], blk = emissive ? 15 : _blk[ni];
            float sh = FaceShade[f];
            float ha, hb; float ax, az, bx, bz;
            switch (f)
            {
                case Dir.North: ax = x + 1; az = z; bx = x; bz = z; ha = h10; hb = h00; break;
                case Dir.South: ax = x; az = z + 1; bx = x + 1; bz = z + 1; ha = h01; hb = h11; break;
                case Dir.West: ax = x; az = z; bx = x; bz = z + 1; ha = h00; hb = h01; break;
                default: ax = x + 1; az = z + 1; bx = x + 1; bz = z; ha = h11; hb = h10; break;
            }
            buf.V(ax, y + ha, az, 0, 1 - ha, tex, 0, sh, sky, blk, 0xFFFFFFFF);
            buf.V(ax, y, az, 0, 1, tex, 0, sh, sky, blk, 0xFFFFFFFF);
            buf.V(bx, y, bz, 1, 1, tex, 0, sh, sky, blk, 0xFFFFFFFF);
            buf.V(bx, y + hb, bz, 1, 1 - hb, tex, 0, sh, sky, blk, 0xFFFFFFFF);
        }
    }

    // ---------------- pochodnie ----------------
    private void Torch(Block b, int x, int y, int z, int meta)
    {
        int tex = b.Tex[0];
        int i = Idx(x, y, z);
        float sky = _sky[i], blk = b.LightEmit > 0 ? 15 : _blk[i];
        float bx = 0.5f, bz = 0.5f, by = 0, lx = 0, lz = 0;
        switch (meta)
        {
            case 1: bx = 0.15f; lx = 0.4f; by = 0.2f; break;
            case 2: bx = 0.85f; lx = -0.4f; by = 0.2f; break;
            case 3: bz = 0.15f; lz = 0.4f; by = 0.2f; break;
            case 4: bz = 0.85f; lz = -0.4f; by = 0.2f; break;
        }
        const float hw = 1 / 16f, h = 10 / 16f;
        // wierzchołek (lx, ly, lz) -> świat z pochyleniem
        void Pt(VertexBuffer buf, float px, float py, float pz, float u, float v, float shade)
        {
            float wx = x + bx + px + lx * py, wy = y + by + py, wz = z + bz + pz + lz * py;
            buf.V(wx, wy, wz, u, v, tex, 0, shade, sky, blk, 0xFFFFFFFF);
        }
        float u0 = 7 / 16f, u1 = 9 / 16f, v0 = 6 / 16f, v1 = 1f;
        // 4 boki
        Pt(Solid, -hw, h, -hw, u1, v0, 0.8f); Pt(Solid, hw, h, -hw, u0, v0, 0.8f); Pt(Solid, hw, 0, -hw, u0, v1, 0.8f); Pt(Solid, -hw, 0, -hw, u1, v1, 0.8f);
        Pt(Solid, hw, h, hw, u1, v0, 0.8f); Pt(Solid, -hw, h, hw, u0, v0, 0.8f); Pt(Solid, -hw, 0, hw, u0, v1, 0.8f); Pt(Solid, hw, 0, hw, u1, v1, 0.8f);
        Pt(Solid, -hw, h, hw, u1, v0, 0.6f); Pt(Solid, -hw, h, -hw, u0, v0, 0.6f); Pt(Solid, -hw, 0, -hw, u0, v1, 0.6f); Pt(Solid, -hw, 0, hw, u1, v1, 0.6f);
        Pt(Solid, hw, h, -hw, u1, v0, 0.6f); Pt(Solid, hw, h, hw, u0, v0, 0.6f); Pt(Solid, hw, 0, hw, u0, v1, 0.6f); Pt(Solid, hw, 0, -hw, u1, v1, 0.6f);
        // góra
        Pt(Solid, -hw, h, -hw, u0, v0, 1f); Pt(Solid, -hw, h, hw, u0, 8 / 16f, 1f); Pt(Solid, hw, h, hw, u1, 8 / 16f, 1f); Pt(Solid, hw, h, -hw, u1, v0, 1f);
        // dół
        Pt(Solid, -hw, 0, hw, u0, 14 / 16f, 0.5f); Pt(Solid, -hw, 0, -hw, u0, 1f, 0.5f); Pt(Solid, hw, 0, -hw, u1, 1f, 0.5f); Pt(Solid, hw, 0, hw, u1, 14 / 16f, 0.5f);
    }

    // ---------------- kaktus ----------------
    private void Cactus(Block b, int x, int y, int z, int meta)
    {
        int i = Idx(x, y, z);
        float sky = _sky[i], blk = _blk[i];
        const float e = 1 / 16f;
        int side = b.Tex[2], top = b.Tex[1], bot = b.Tex[0];
        if (_id[Idx(x, y + 1, z)] != b.Id)
        {
            int ui = Idx(x, y + 1, z);
            Solid.V(x, y + 1, z, 0, 0, top, 0, 1f, _sky[ui], _blk[ui], 0xFFFFFFFF); Solid.V(x, y + 1, z + 1, 0, 1, top, 0, 1f, _sky[ui], _blk[ui], 0xFFFFFFFF);
            Solid.V(x + 1, y + 1, z + 1, 1, 1, top, 0, 1f, _sky[ui], _blk[ui], 0xFFFFFFFF); Solid.V(x + 1, y + 1, z, 1, 0, top, 0, 1f, _sky[ui], _blk[ui], 0xFFFFFFFF);
        }
        if (_id[Idx(x, y - 1, z)] != b.Id && !_opaque[_id[Idx(x, y - 1, z)]])
        {
            Solid.V(x, y, z + 1, 0, 1, bot, 0, 0.5f, sky, blk, 0xFFFFFFFF); Solid.V(x, y, z, 0, 0, bot, 0, 0.5f, sky, blk, 0xFFFFFFFF);
            Solid.V(x + 1, y, z, 1, 0, bot, 0, 0.5f, sky, blk, 0xFFFFFFFF); Solid.V(x + 1, y, z + 1, 1, 1, bot, 0, 0.5f, sky, blk, 0xFFFFFFFF);
        }
        // boki wcięte o 1px
        Solid.V(x + 1, y + 1, z + e, 0, 0, side, 0, 0.8f, sky, blk, 0xFFFFFFFF); Solid.V(x + 1, y, z + e, 0, 1, side, 0, 0.8f, sky, blk, 0xFFFFFFFF);
        Solid.V(x, y, z + e, 1, 1, side, 0, 0.8f, sky, blk, 0xFFFFFFFF); Solid.V(x, y + 1, z + e, 1, 0, side, 0, 0.8f, sky, blk, 0xFFFFFFFF);
        Solid.V(x, y + 1, z + 1 - e, 0, 0, side, 0, 0.8f, sky, blk, 0xFFFFFFFF); Solid.V(x, y, z + 1 - e, 0, 1, side, 0, 0.8f, sky, blk, 0xFFFFFFFF);
        Solid.V(x + 1, y, z + 1 - e, 1, 1, side, 0, 0.8f, sky, blk, 0xFFFFFFFF); Solid.V(x + 1, y + 1, z + 1 - e, 1, 0, side, 0, 0.8f, sky, blk, 0xFFFFFFFF);
        Solid.V(x + e, y + 1, z, 0, 0, side, 0, 0.6f, sky, blk, 0xFFFFFFFF); Solid.V(x + e, y, z, 0, 1, side, 0, 0.6f, sky, blk, 0xFFFFFFFF);
        Solid.V(x + e, y, z + 1, 1, 1, side, 0, 0.6f, sky, blk, 0xFFFFFFFF); Solid.V(x + e, y + 1, z + 1, 1, 0, side, 0, 0.6f, sky, blk, 0xFFFFFFFF);
        Solid.V(x + 1 - e, y + 1, z + 1, 0, 0, side, 0, 0.6f, sky, blk, 0xFFFFFFFF); Solid.V(x + 1 - e, y, z + 1, 0, 1, side, 0, 0.6f, sky, blk, 0xFFFFFFFF);
        Solid.V(x + 1 - e, y, z, 1, 1, side, 0, 0.6f, sky, blk, 0xFFFFFFFF); Solid.V(x + 1 - e, y + 1, z, 1, 0, side, 0, 0.6f, sky, blk, 0xFFFFFFFF);
    }

    // ---------------- przewód ----------------
    private void Wire(Block b, int x, int y, int z, int meta)
    {
        var wb = (RedstoneWireBlock)b;
        int i = Idx(x, y, z);
        float sky = _sky[i], blk = Math.Max(_blk[i], meta > 0 ? 6 : 0);
        uint tint = TintFor(b, x, y, z, meta);
        int wx = x + _ox, wy = y + _oy, wz = z + _oz;
        int mask = RedstoneWireBlock.Connections(_access, wx, wy, wz, out int up);
        bool s = (mask & 1) != 0, wst = (mask & 2) != 0, n = (mask & 4) != 0, e = (mask & 8) != 0;
        const float yy = 1 / 64f;
        bool lineZ = (n || s) && !e && !wst, lineX = (e || wst) && !n && !s;
        if (lineZ)
        {
            int t = wb.LineTex;
            Solid.V(x, y + yy, z, 0, 0, t, 0, 1f, sky, blk, tint); Solid.V(x, y + yy, z + 1, 0, 1, t, 0, 1f, sky, blk, tint);
            Solid.V(x + 1, y + yy, z + 1, 1, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + 1, y + yy, z, 1, 0, t, 0, 1f, sky, blk, tint);
        }
        else if (lineX)
        {
            int t = wb.LineTex;
            Solid.V(x, y + yy, z, 0, 1, t, 0, 1f, sky, blk, tint); Solid.V(x, y + yy, z + 1, 1, 1, t, 0, 1f, sky, blk, tint);
            Solid.V(x + 1, y + yy, z + 1, 1, 0, t, 0, 1f, sky, blk, tint); Solid.V(x + 1, y + yy, z, 0, 0, t, 0, 1f, sky, blk, tint);
        }
        else
        {
            int t = wb.CrossTex;
            bool dot = mask == 0;
            float x0 = wst || dot ? 0 : 5 / 16f, x1 = e || dot ? 1 : 11 / 16f, z0 = n || dot ? 0 : 5 / 16f, z1 = s || dot ? 1 : 11 / 16f;
            if (dot) { x0 = z0 = 3 / 16f; x1 = z1 = 13 / 16f; }
            Solid.V(x + x0, y + yy, z + z0, x0, z0, t, 0, 1f, sky, blk, tint); Solid.V(x + x0, y + yy, z + z1, x0, z1, t, 0, 1f, sky, blk, tint);
            Solid.V(x + x1, y + yy, z + z1, x1, z1, t, 0, 1f, sky, blk, tint); Solid.V(x + x1, y + yy, z + z0, x1, z0, t, 0, 1f, sky, blk, tint);
        }
        // przewód po ścianie w górę
        for (int h = 0; h < 4; h++)
        {
            if ((up & (1 << h)) == 0) continue;
            int t = wb.LineTex; const float o = 0.99f / 16f;
            switch (h)
            {
                case 0: Solid.V(x, y + 1, z + 1 - o, 0, 0, t, 0, 1f, sky, blk, tint); Solid.V(x, y, z + 1 - o, 0, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + 1, y, z + 1 - o, 1, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + 1, y + 1, z + 1 - o, 1, 0, t, 0, 1f, sky, blk, tint); break;
                case 1: Solid.V(x + o, y + 1, z, 0, 0, t, 0, 1f, sky, blk, tint); Solid.V(x + o, y, z, 0, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + o, y, z + 1, 1, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + o, y + 1, z + 1, 1, 0, t, 0, 1f, sky, blk, tint); break;
                case 2: Solid.V(x + 1, y + 1, z + o, 0, 0, t, 0, 1f, sky, blk, tint); Solid.V(x + 1, y, z + o, 0, 1, t, 0, 1f, sky, blk, tint); Solid.V(x, y, z + o, 1, 1, t, 0, 1f, sky, blk, tint); Solid.V(x, y + 1, z + o, 1, 0, t, 0, 1f, sky, blk, tint); break;
                default: Solid.V(x + 1 - o, y + 1, z + 1, 0, 0, t, 0, 1f, sky, blk, tint); Solid.V(x + 1 - o, y, z + 1, 0, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + 1 - o, y, z, 1, 1, t, 0, 1f, sky, blk, tint); Solid.V(x + 1 - o, y + 1, z, 1, 0, t, 0, 1f, sky, blk, tint); break;
            }
        }
    }

    // ---------------- modele z boxów ----------------
    private void Model(Block b, int x, int y, int z, int meta)
    {
        _boxes.Clear();
        b.GetModel(_access, x + _ox, y + _oy, z + _oz, meta, _boxes);
        var buf = b.Layer == RenderLayer.Translucent ? Translucent : Solid;
        uint tint = TintFor(b, x, y, z, meta);
        int ci = Idx(x, y, z);
        foreach (var mb in _boxes) EmitBox(buf, mb, x, y, z, ci, tint, b.LightEmit > 0);
    }

    public void EmitBox(VertexBuffer buf, ModelBox mb, float x, float y, float z, int ci, uint tint, bool emissive)
    {
        float x0 = mb.X0, y0 = mb.Y0, z0 = mb.Z0, x1 = mb.X1, y1 = mb.Y1, z1 = mb.Z1;
        for (int f = 0; f < 6; f++)
        {
            bool flush = f switch { 0 => y0 <= 0, 1 => y1 >= 1, 2 => z0 <= 0, 3 => z1 >= 1, 4 => x0 <= 0, _ => x1 >= 1 };
            // płaskie (zerowej grubości) – tylko ściany prostopadłe
            if (x0 == x1 && f < 4) continue;
            if (z0 == z1 && (f < 2 || f > 3)) continue;
            if (y0 == y1 && f > 1) continue;
            float sky, blk;
            if (ci >= 0)
            {
                int li = flush ? Idx((int)x + Dir.X[f], (int)y + Dir.Y[f], (int)z + Dir.Z[f]) : ci;
                if (flush && !mb.NoCull && _opaque[_id[li]]) continue;
                sky = Math.Max(_sky[li], _sky[ci] - 1); blk = Math.Max(_blk[li], _blk[ci]);
                if (_opaque[_id[li]]) { sky = _sky[ci]; blk = _blk[ci]; }
            }
            else { sky = 15; blk = 15; }
            if (emissive) blk = 15;
            int tex = mb.Tex[f];
            float shade = FaceShade[f];
            // UV
            float u0, v0, u1, v1;
            if (mb.Uv != null) { u0 = mb.Uv[f * 4]; v0 = mb.Uv[f * 4 + 1]; u1 = mb.Uv[f * 4 + 2]; v1 = mb.Uv[f * 4 + 3]; }
            else
            {
                switch (f)
                {
                    case 0: case 1: u0 = x0; u1 = x1; v0 = z0; v1 = z1; break;
                    case 2: u0 = 1 - x1; u1 = 1 - x0; v0 = 1 - y1; v1 = 1 - y0; break;
                    case 3: u0 = x0; u1 = x1; v0 = 1 - y1; v1 = 1 - y0; break;
                    case 4: u0 = z0; u1 = z1; v0 = 1 - y1; v1 = 1 - y0; break;
                    default: u0 = 1 - z1; u1 = 1 - z0; v0 = 1 - y1; v1 = 1 - y0; break;
                }
            }
            int rot = mb.UvRot != null ? mb.UvRot[f] : 0;
            Span<float> uvs = stackalloc float[8];
            // kolejność jak FaceVerts: (u0,v0),(u0,v1),(u1,v1),(u1,v0)
            uvs[0] = u0; uvs[1] = v0; uvs[2] = u0; uvs[3] = v1; uvs[4] = u1; uvs[5] = v1; uvs[6] = u1; uvs[7] = v0;
            var fv = FaceVerts[f];
            for (int k = 0; k < 4; k++)
            {
                float px = fv[k * 3] == 0 ? x0 : x1, py = fv[k * 3 + 1] == 0 ? y0 : y1, pz = fv[k * 3 + 2] == 0 ? z0 : z1;
                int kk = (k + rot) & 3;
                float u = uvs[kk * 2], v = uvs[kk * 2 + 1];
                if (f == 0) { u = uvs[kk * 2]; v = uvs[kk * 2 + 1]; }
                buf.V(x + px, y + py + px * mb.SlopeX + pz * mb.SlopeZ, z + pz, u, v, tex, 0, shade, sky, blk, tint);
            }
            // płaskie: dodaj stronę odwrotną
            if (mb.NoCull && (x0 == x1 || z0 == z1 || y0 == y1))
            {
                for (int k = 3; k >= 0; k--)
                {
                    float px = fv[k * 3] == 0 ? x0 : x1, py = fv[k * 3 + 1] == 0 ? y0 : y1, pz = fv[k * 3 + 2] == 0 ? z0 : z1;
                    int kk = (k + rot) & 3;
                    buf.V(x + px, y + py + px * mb.SlopeX + pz * mb.SlopeZ, z + pz, uvs[kk * 2], uvs[kk * 2 + 1], tex, 0, shade, sky, blk, tint);
                }
            }
        }
    }
}
