using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>Oświetlenie: światło nieba + bloków (BFS), jak w MC.</summary>
public sealed class LightEngine
{
    private readonly World _w;
    private int[] _q = new int[1 << 16]; // pakowane: x,y,z,level w 4 intach
    private int[] _rq = new int[1 << 16];
    private static readonly byte[] Opacity = new byte[256];
    private static readonly byte[] Emit = new byte[256];
    private static bool _tablesInit;

    public LightEngine(World w) { _w = w; InitTables(); }

    public static void InitTables()
    {
        if (_tablesInit) return;
        for (int i = 0; i < 256; i++) { var b = Block.ById[i]; if (b == null) continue; Opacity[i] = (byte)b.LightOpacity; Emit[i] = (byte)b.LightEmit; }
        _tablesInit = true;
    }

    // ---------- lokalna inicjalizacja chunka (wątek roboczy) ----------
    public static void InitChunkLocal(Chunk c, bool hasNoSky = false)
    {
        InitTables();
        var light = c.Light; var blocks = c.Blocks;
        Array.Clear(light);
        var q = new Queue<int>(4096);
        // niebo: kolumny
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                int level = hasNoSky ? 0 : 15;
                for (int y = Chunk.Height - 1; y >= 0; y--)
                {
                    int i = Chunk.Index(x, y, z);
                    int op = Opacity[blocks[i]];
                    if (op > 0) { level -= op; if (level < 0) level = 0; }
                    light[i] = (byte)(level << 4);
                    if (level == 0) break;
                }
            }
        // ziarna do rozprzestrzeniania w bok
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                int maxN = 0;
                for (int h = 0; h < 4; h++)
                {
                    int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
                    if (nx < 0 || nx > 15 || nz < 0 || nz > 15) continue;
                    maxN = Math.Max(maxN, c.HeightMap[(nz << 4) | nx]);
                }
                int top = Math.Min(Chunk.Height - 1, maxN);
                for (int y = top; y >= 0; y--)
                {
                    int i = Chunk.Index(x, y, z);
                    if ((light[i] >> 4) > 1) q.Enqueue(i);
                }
            }
        SpreadLocal(c, q, true);
        // światło bloków
        for (int i = 0; i < Chunk.Volume; i++)
        {
            int e = Emit[blocks[i]];
            if (e > 0) { light[i] = (byte)((light[i] & 0xF0) | e); q.Enqueue(i); }
        }
        SpreadLocal(c, q, false);
        c.Lit = true;
    }

    private static void SpreadLocal(Chunk c, Queue<int> q, bool sky)
    {
        var light = c.Light; var blocks = c.Blocks;
        while (q.Count > 0)
        {
            int i = q.Dequeue();
            int x = i & 15, z = (i >> 4) & 15, y = i >> 8;
            int lvl = sky ? light[i] >> 4 : light[i] & 15;
            if (lvl <= 1) continue;
            for (int f = 0; f < 6; f++)
            {
                int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
                if (nx < 0 || nx > 15 || nz < 0 || nz > 15 || ny < 0 || ny >= Chunk.Height) continue;
                int ni = Chunk.Index(nx, ny, nz);
                int op = Opacity[blocks[ni]];
                if (op >= 15) continue;
                int nl = lvl - Math.Max(1, op);
                int cur = sky ? light[ni] >> 4 : light[ni] & 15;
                if (cur < nl)
                {
                    light[ni] = sky ? (byte)((light[ni] & 0x0F) | (nl << 4)) : (byte)((light[ni] & 0xF0) | nl);
                    q.Enqueue(ni);
                }
            }
        }
    }

    // ---------- operacje w przestrzeni świata ----------
    private int GetL(int x, int y, int z, bool sky, out Chunk c)
    {
        c = _w.GetChunkIfLit(x >> 4, z >> 4);
        if (c == null) return -1;
        if (y < 0) return 0;
        if (y >= Chunk.Height) return sky ? 15 : 0;
        byte v = c.Light[Chunk.Index(x & 15, y, z & 15)];
        return sky ? v >> 4 : v & 15;
    }
    private void SetL(Chunk c, int x, int y, int z, bool sky, int lvl)
    {
        int i = Chunk.Index(x & 15, y, z & 15);
        byte v = c.Light[i];
        c.Light[i] = sky ? (byte)((v & 0x0F) | (lvl << 4)) : (byte)((v & 0xF0) | lvl);
        _w.MarkLightDirty(c, x, y, z);
    }

    private int _qh, _qt, _rh, _rt;
    private void Push(int x, int y, int z, int l)
    {
        if (_qt + 4 > _q.Length) { if (_qh > 0) { Array.Copy(_q, _qh, _q, 0, _qt - _qh); _qt -= _qh; _qh = 0; } if (_qt + 4 > _q.Length) Array.Resize(ref _q, _q.Length * 2); }
        _q[_qt++] = x; _q[_qt++] = y; _q[_qt++] = z; _q[_qt++] = l;
    }
    private void PushR(int x, int y, int z, int l)
    {
        if (_rt + 4 > _rq.Length) { if (_rh > 0) { Array.Copy(_rq, _rh, _rq, 0, _rt - _rh); _rt -= _rh; _rh = 0; } if (_rt + 4 > _rq.Length) Array.Resize(ref _rq, _rq.Length * 2); }
        _rq[_rt++] = x; _rq[_rt++] = y; _rq[_rt++] = z; _rq[_rt++] = l;
    }

    private void RunAdd(bool sky)
    {
        while (_qh < _qt)
        {
            int x = _q[_qh++], y = _q[_qh++], z = _q[_qh++]; _qh++;
            int lvl = GetL(x, y, z, sky, out _);
            if (lvl <= 1) continue;
            for (int f = 0; f < 6; f++)
            {
                int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
                if (ny < 0 || ny >= Chunk.Height) continue;
                int cur = GetL(nx, ny, nz, sky, out var c);
                if (cur < 0) continue;
                int op = Opacity[c.Blocks[Chunk.Index(nx & 15, ny, nz & 15)]];
                if (op >= 15) continue;
                int nl = (sky && f == Dir.Down && lvl == 15 && op == 0) ? 15 : lvl - Math.Max(1, op);
                if (cur < nl) { SetL(c, nx, ny, nz, sky, nl); Push(nx, ny, nz, nl); }
            }
        }
        _qh = _qt = 0;
    }

    private void RunRemove(bool sky)
    {
        while (_rh < _rt)
        {
            int x = _rq[_rh++], y = _rq[_rh++], z = _rq[_rh++], lvl = _rq[_rh++];
            for (int f = 0; f < 6; f++)
            {
                int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
                if (ny < 0 || ny >= Chunk.Height) continue;
                int nl = GetL(nx, ny, nz, sky, out var c);
                if (nl <= 0) continue;
                bool downSky = sky && f == Dir.Down && lvl == 15 && nl == 15;
                if (nl < lvl || downSky)
                {
                    SetL(c, nx, ny, nz, sky, 0);
                    PushR(nx, ny, nz, nl);
                }
                else Push(nx, ny, nz, nl);
            }
        }
        _rh = _rt = 0;
    }

    /// <summary>Połącz światło chunka z załadowanymi sąsiadami (wątek główny).</summary>
    public void MergeChunk(Chunk c)
    {
        int bx = c.Cx * 16, bz = c.Cz * 16;
        for (int pass = 0; pass < 2; pass++)
        {
            bool sky = pass == 0;
            for (int h = 0; h < 4; h++)
            {
                var n = _w.GetChunkIfLit(c.Cx + Dir.HorizX[h], c.Cz + Dir.HorizZ[h]);
                if (n == null) continue;
                for (int k = 0; k < 16; k++)
                {
                    int ax, az, nx, nz;
                    switch (h)
                    {
                        case 0: ax = k; az = 15; nx = k; nz = 0; break;      // +Z
                        case 1: ax = 0; az = k; nx = 15; nz = k; break;      // -X
                        case 2: ax = k; az = 0; nx = k; nz = 15; break;      // -Z
                        default: ax = 15; az = k; nx = 0; nz = k; break;     // +X
                    }
                    for (int y = 0; y < Chunk.Height; y++)
                    {
                        int ia = Chunk.Index(ax, y, az), ib = Chunk.Index(nx, y, nz);
                        int la = sky ? c.Light[ia] >> 4 : c.Light[ia] & 15;
                        int lb = sky ? n.Light[ib] >> 4 : n.Light[ib] & 15;
                        if (la > lb + 1) Push(bx + ax, y, bz + az, la);
                        else if (lb > la + 1) Push(n.Cx * 16 + nx, y, n.Cz * 16 + nz, lb);
                    }
                }
            }
            RunAdd(sky);
        }
        c.Merged = true;
    }

    /// <summary>Aktualizacja po zmianie bloku.</summary>
    public void OnBlockChanged(int x, int y, int z, Block oldB, Block newB)
    {
        if (_w.GetChunkIfLit(x >> 4, z >> 4) == null) return;
        if (oldB.LightOpacity == newB.LightOpacity && oldB.LightEmit == newB.LightEmit) return;
        UpdateChannel(x, y, z, false, newB);
        UpdateChannel(x, y, z, true, newB);
    }

    private void UpdateChannel(int x, int y, int z, bool sky, Block newB)
    {
        if (sky && _w.Provider.HasNoSky) return;
        int old = GetL(x, y, z, sky, out var c);
        if (old < 0) return;
        int emit = sky ? 0 : newB.LightEmit;
        // usuwanie
        if (old > 0)
        {
            SetL(c, x, y, z, sky, 0);
            PushR(x, y, z, old);
            RunRemove(sky);
        }
        // źródło
        if (sky)
        {
            // jeśli nad blokiem jest pełne niebo i blok przepuszcza
            int above = GetL(x, y + 1, z, true, out _);
            if (newB.LightOpacity < 15)
            {
                int l = above == 15 && newB.LightOpacity == 0 ? 15 : Math.Max(0, (above < 0 ? 0 : above) - Math.Max(1, newB.LightOpacity));
                if (y + 1 >= Chunk.Height) l = 15;
                if (l > GetL(x, y, z, true, out _)) { SetL(c, x, y, z, true, l); }
                Push(x, y, z, l);
            }
        }
        else if (emit > 0)
        {
            if (emit > GetL(x, y, z, false, out _)) SetL(c, x, y, z, false, emit);
            Push(x, y, z, emit);
        }
        // sąsiedzi wlewają światło z powrotem
        if (newB.LightOpacity < 15)
            for (int f = 0; f < 6; f++)
            {
                int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
                if (ny < 0 || ny >= Chunk.Height) continue;
                int nl = GetL(nx, ny, nz, sky, out _);
                if (nl > 0) Push(nx, ny, nz, nl);
            }
        RunAdd(sky);
    }
}
