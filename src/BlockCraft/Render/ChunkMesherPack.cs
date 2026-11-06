using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Render;

/// <summary>Bloki z modelem z paczki zasobów (PackModels): ściany modelu JSON z tym samym światłem, AO i cieniowaniem co sześciany.</summary>
public sealed partial class ChunkMesher
{
    /// <summary>Rysuje blok modelem z paczki; false = brak modelu (zwykła geometria gry).</summary>
    private bool PackModel(Block b, int x, int y, int z, int meta)
    {
        if (!PackModels.Any) return false;
        bool snowy = _id[Idx(x, y + 1, z)] is B.Snow or B.SnowLayer;
        var model = PackModels.Get(b.Id, meta, x + _ox, y + _oy, z + _oz, snowy);
        if (model == null) return false;
        var (offsetX, offsetZ) = b.Render == RenderType.Cross ? CrossOffset(b, x, z) : (0f, 0f);
        var buf = b.Layer == RenderLayer.Translucent ? Translucent : Solid;
        uint tint = TintFor(b, x, y, z, meta);
        bool emissive = b.LightEmit > 0;
        int flags = !FancyLeaves && b is LeavesBlock ? 2 : 0;
        foreach (var q in model.Quads)
        {
            if (q.CullFace >= 0 && !ShouldDrawFace(b, x, y, z, q.CullFace)) continue;
            // ściana na krawędzi bloku bierze światło od sąsiada (z AO), wewnętrzna — z własnej komórki
            bool onEdge = q.CullFace >= 0 || IsOnBlockFace(q, q.Normal);
            int f = q.Normal;
            float shade = q.Shade ? FaceShade[f] : 1f;
            SamplePackFaceLight(x, y, z, q, onEdge, model.AmbientOcclusion);
            if (emissive) Array.Fill(_vb, 15f);
            uint t = q.Tint ? tint : 0xFFFFFFFF;
            int start = FaceVertexStart();
            for (int j = 0; j < 4; j++)
            {
                int k = (j + start) & 3;
                buf.V(x + q.P[k].X + offsetX, y + q.P[k].Y, z + q.P[k].Z + offsetZ, q.Uv[k].X, q.Uv[k].Y, q.Layer, flags, shade * _vl[k], _vs[k], _vb[k], t);
            }
        }
        return true;
    }

    /// <summary>Światło ściany wewnątrz bloku: komórka bloku, a gdy jest pełna (np. filar z modelu w miejscu kamienia,
    /// który ma światło 0) — sąsiad w kierunku ściany albo najjaśniejszy przezroczysty sąsiad.</summary>
    private (float sky, float blk) InnerLight(int x, int y, int z, int f)
    {
        int self = Idx(x, y, z);
        if (!_opaque[_id[self]]) return (_sky[self], _blk[self]);
        int n = Idx(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f]);
        if (!_opaque[_id[n]]) return (_sky[n], _blk[n]);
        float sky = 0, blk = 0;
        for (int d = 0; d < 6; d++)
        {
            int c = Idx(x + Dir.X[d], y + Dir.Y[d], z + Dir.Z[d]);
            if (_opaque[_id[c]]) continue;
            sky = Math.Max(sky, _sky[c]); blk = Math.Max(blk, _blk[c]);
        }
        return (sky, blk);
    }

    /// <summary>Czy ściana leży na ścianie bloku zgodnej z normalną (np. y = 1 dla góry).</summary>
    private static bool IsOnBlockFace(PackModels.Quad q, int f)
    {
        const float e = 1e-4f;
        foreach (var p in q.P)
        {
            float c = f switch { 0 => p.Y, 1 => 1 - p.Y, 2 => p.Z, 3 => 1 - p.Z, 4 => p.X, _ => 1 - p.X };
            if (c > e) return false;
        }
        return true;
    }

    /// <summary>Światło i AO ściany modelu: wspólne dla wnętrza, a na krawędzi próbkowane przy najbliższych narożnikach bloku.</summary>
    private void SamplePackFaceLight(int x, int y, int z, PackModels.Quad quad, bool onEdge, bool ambientOcclusion)
    {
        int f = quad.Normal;
        int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
        int c0 = Idx(nx, ny, nz);
        // sąsiad pełny (ściana bez cullface przy ścianie bloku): światło z własnej komórki
        if (!onEdge || _opaque[_id[c0]])
        {
            var (sky, blockLight) = InnerLight(x, y, z, f);
            Array.Fill(_vs, sky);
            Array.Fill(_vb, blockLight);
            Array.Fill(_vl, 1f);
            return;
        }
        for (int k = 0; k < 4; k++)
        {
            var p = quad.P[k];
            SampleFaceVertexLight(x, y, z, f, p.X < .5f ? 0 : 1, p.Y < .5f ? 0 : 1, p.Z < .5f ? 0 : 1, k, ambientOcclusion);
        }
    }

    private int FaceVertexStart() => _vl[0] + _vl[2] < _vl[1] + _vl[3] ? 1 : 0;

    private void SampleFaceVertexLight(int x, int y, int z, int f, int vx, int vy, int vz, int k, bool ambientOcclusion)
    {
        int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
        int c0 = Idx(nx, ny, nz);
        if (!SmoothLighting) { _vs[k] = _sky[c0]; _vb[k] = _blk[c0]; _vl[k] = 1f; return; }
        int ax, ay, az, bx, by, bz;
        if (f <= 1) { ax = vx == 0 ? -1 : 1; ay = 0; az = 0; bx = 0; by = 0; bz = vz == 0 ? -1 : 1; }
        else if (f <= 3) { ax = vx == 0 ? -1 : 1; ay = 0; az = 0; bx = 0; by = vy == 0 ? -1 : 1; bz = 0; }
        else { ax = 0; ay = vy == 0 ? -1 : 1; az = 0; bx = 0; by = 0; bz = vz == 0 ? -1 : 1; }
        int c1 = Idx(nx + ax, ny + ay, nz + az), c2 = Idx(nx + bx, ny + by, nz + bz), c3 = Idx(nx + ax + bx, ny + ay + by, nz + az + bz);
        bool o1 = _opaque[_id[c1]], o2 = _opaque[_id[c2]], o3 = _opaque[_id[c3]] || (o1 && o2);
        float sky = _sky[c0], blk = _blk[c0]; int n = 1;
        if (!o1) { sky += _sky[c1]; blk += _blk[c1]; n++; }
        if (!o2) { sky += _sky[c2]; blk += _blk[c2]; n++; }
        if (!o3) { sky += _sky[c3]; blk += _blk[c3]; n++; }
        _vs[k] = sky / n; _vb[k] = blk / n;
        _vl[k] = ambientOcclusion ? (1f + (o1 ? .2f : 1f) + (o2 ? .2f : 1f) + (o3 ? .2f : 1f)) / 4f : 1f;
    }
}
