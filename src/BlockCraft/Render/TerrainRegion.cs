using OpenTK.Graphics.OpenGL4;

namespace BlockCraft.Render;

/// <summary>Wspólny bufor wierzchołków nieprzezroczystego terenu dla regionu 8x8 chunków (cała wysokość).
/// Sekcje dostają w nim kawałki (alokator first-fit z łączeniem wolnych miejsc), a cały region rysuje się
/// jednym glMultiDrawElementsBaseVertex zamiast osobnego draw calla na sekcję (jak regiony w Sodium).
/// Pozycje wierzchołków są względem narożnika regionu, uniform uOffset = narożnik - kamera.</summary>
public sealed class TerrainRegion : IDisposable
{
    public const int Shift = 3, Size = 1 << Shift; // 8 chunków
    public readonly int Rx, Rz;
    public int Vao { get; private set; }
    private int _vbo;
    private int _capacity; // w wierzchołkach
    private readonly List<int> _freeOff = new(), _freeLen = new();
    public int Allocations;
    public long UsedVertices;

    // lista rysowania budowana co klatkę
    public int[] Counts = new int[64], BaseVerts = new int[64];
    public int DrawCount;
    public bool Queued;

    public TerrainRegion(int rx, int rz, int initialVertices = 1 << 15)
    {
        Rx = rx; Rz = rz;
        Vao = GL.GenVertexArray();
        _capacity = initialVertices;
        _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _capacity * VertexBuffer.Stride, IntPtr.Zero, BufferUsageHint.StaticDraw);
        BindAttribs();
        _freeOff.Add(0); _freeLen.Add(_capacity);
    }

    private void BindAttribs()
    {
        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        WorldRenderer.SetupChunkAttribs();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, SharedQuadIndices.Ebo);
        GL.BindVertexArray(0);
    }

    /// <summary>Rezerwuje <paramref name="verts"/> wierzchołków i wgrywa dane; zwraca przesunięcie w wierzchołkach.</summary>
    public unsafe int Upload(byte[] data, int verts)
    {
        int off = Alloc(verts);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        fixed (byte* p = data) GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)((long)off * VertexBuffer.Stride), verts * VertexBuffer.Stride, (IntPtr)p);
        Allocations++; UsedVertices += verts;
        SharedQuadIndices.Ensure(verts / 4);
        return off;
    }

    public void Free(int off, int verts)
    {
        Allocations--; UsedVertices -= verts;
        // wstaw z zachowaniem kolejności i połącz z sąsiadami
        int i = 0;
        while (i < _freeOff.Count && _freeOff[i] < off) i++;
        _freeOff.Insert(i, off); _freeLen.Insert(i, verts);
        if (i + 1 < _freeOff.Count && _freeOff[i] + _freeLen[i] == _freeOff[i + 1])
        { _freeLen[i] += _freeLen[i + 1]; _freeOff.RemoveAt(i + 1); _freeLen.RemoveAt(i + 1); }
        if (i > 0 && _freeOff[i - 1] + _freeLen[i - 1] == _freeOff[i])
        { _freeLen[i - 1] += _freeLen[i]; _freeOff.RemoveAt(i); _freeLen.RemoveAt(i); }
    }

    private int Alloc(int verts)
    {
        while (true)
        {
            for (int i = 0; i < _freeOff.Count; i++)
            {
                if (_freeLen[i] < verts) continue;
                int off = _freeOff[i];
                if (_freeLen[i] == verts) { _freeOff.RemoveAt(i); _freeLen.RemoveAt(i); }
                else { _freeOff[i] += verts; _freeLen[i] -= verts; }
                return off;
            }
            Grow(Math.Max(_capacity * 2, _capacity + verts));
        }
    }

    /// <summary>Nowy, większy bufor; stara zawartość kopiowana po stronie GPU (GL 3.1 CopyBufferSubData).</summary>
    private void Grow(int newCap)
    {
        int nb = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.CopyWriteBuffer, nb);
        GL.BufferData(BufferTarget.CopyWriteBuffer, (IntPtr)((long)newCap * VertexBuffer.Stride), IntPtr.Zero, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.CopyReadBuffer, _vbo);
        GL.CopyBufferSubData(BufferTarget.CopyReadBuffer, BufferTarget.CopyWriteBuffer, IntPtr.Zero, IntPtr.Zero, (IntPtr)((long)_capacity * VertexBuffer.Stride));
        GL.BindBuffer(BufferTarget.CopyReadBuffer, 0); GL.BindBuffer(BufferTarget.CopyWriteBuffer, 0);
        GL.DeleteBuffer(_vbo);
        _vbo = nb;
        // dopisz nowy obszar jako wolny (połącz z ostatnim wolnym, jeśli styka się z końcem)
        int add = newCap - _capacity;
        if (_freeOff.Count > 0 && _freeOff[^1] + _freeLen[^1] == _capacity) _freeLen[^1] += add;
        else { _freeOff.Add(_capacity); _freeLen.Add(add); }
        _capacity = newCap;
        BindAttribs();
    }

    public void AddDraw(int off, int verts)
    {
        if (DrawCount == Counts.Length) { Array.Resize(ref Counts, DrawCount * 2); Array.Resize(ref BaseVerts, DrawCount * 2); }
        Counts[DrawCount] = verts / 4 * 6; BaseVerts[DrawCount] = off; DrawCount++;
    }

    private static IntPtr[] _zeroIndices = new IntPtr[64];
    /// <summary>Rysuje zebraną listę jednym wywołaniem i ją czyści.</summary>
    public void Draw()
    {
        if (DrawCount == 0) return;
        if (_zeroIndices.Length < DrawCount) _zeroIndices = new IntPtr[Counts.Length];
        GL.BindVertexArray(Vao);
        GL.MultiDrawElementsBaseVertex(PrimitiveType.Triangles, Counts, DrawElementsType.UnsignedInt, _zeroIndices, DrawCount, BaseVerts);
        DrawCount = 0;
    }

    public void Dispose()
    {
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (Vao != 0) GL.DeleteVertexArray(Vao);
        _vbo = 0; Vao = 0;
    }
}
