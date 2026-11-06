namespace BlockCraft.Render;

/// <summary>Nieprzezroczysty mesh sekcji pogrupowany w 7 kubełków po kierunku ściany (jak w Sodium):
/// 0 nieosiowe, 1 +Y, 2 -Y, 3 +X, 4 -X, 5 +Z, 6 -Z. Start[b]..Start[b+1] to wierzchołki kubełka b.
/// Plane[b-1] to skrajna płaszczyzna kubełka w układzie sekcji (dla + najmniejsza, dla - największa),
/// więc kubełek jest na pewno tyłem do kamery, gdy kamera leży po złej stronie tej płaszczyzny.</summary>
public sealed class SortedSolid
{
    public byte[] Data;
    public int Verts;
    /// <summary>Maska kierunków zawierających co najmniej jeden ruchomy wierzchołek.</summary>
    public int WavingMask;
    public readonly int[] Start = new int[8];
    public readonly float[] Plane = new float[6];
}

public static class FacingSort
{
    public const int Buckets = 7;
    [ThreadStatic] private static byte[] _bucket;

    /// <summary>Sortuje kwadraty (po 4 wierzchołki) stabilnie według kierunku normalnej z kolejności wierzchołków
    /// (CCW = przód). Kolejność kwadratów w kubełku zostaje, więc współpłaszczyznowe nakładki rysują się jak dawniej.</summary>
    public static unsafe SortedSolid Sort(byte[] src, int verts)
    {
        const int S = VertexBuffer.Stride;
        int quads = verts / 4;
        var res = new SortedSolid { Data = new byte[verts * S], Verts = verts };
        var bucket = _bucket;
        if (bucket == null || bucket.Length < quads) _bucket = bucket = new byte[Math.Max(quads, 1024)];
        Span<int> count = stackalloc int[Buckets];
        Span<float> plane = stackalloc float[6];
        for (int i = 0; i < 6; i++) plane[i] = (i & 1) == 0 ? float.MaxValue : float.MinValue; // + : min, - : max
        fixed (byte* p = src)
        {
            for (int q = 0; q < quads; q++)
            {
                float* v0 = (float*)(p + (q * 4) * S), v1 = (float*)(p + (q * 4 + 1) * S), v2 = (float*)(p + (q * 4 + 2) * S), v3 = (float*)(p + (q * 4 + 3) * S);
                float ax = v1[0] - v0[0], ay = v1[1] - v0[1], az = v1[2] - v0[2];
                float bx = v2[0] - v0[0], by = v2[1] - v0[1], bz = v2[2] - v0[2];
                float nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
                float anx = Math.Abs(nx), any = Math.Abs(ny), anz = Math.Abs(nz);
                int b = 0;
                const float eps = 1e-5f;
                if (anx > eps && any <= anx * 1e-4f && anz <= anx * 1e-4f && Math.Abs(v3[0] - v0[0]) < 1e-4f)
                {
                    b = nx > 0 ? 3 : 4; Track(plane, b, v0[0]);
                }
                else if (any > eps && anx <= any * 1e-4f && anz <= any * 1e-4f && Math.Abs(v3[1] - v0[1]) < 1e-4f)
                {
                    b = ny > 0 ? 1 : 2; Track(plane, b, v0[1]);
                }
                else if (anz > eps && anx <= anz * 1e-4f && any <= anz * 1e-4f && Math.Abs(v3[2] - v0[2]) < 1e-4f)
                {
                    b = nz > 0 ? 5 : 6; Track(plane, b, v0[2]);
                }
                // Flagi od bitu 2 opisują falowanie; sprawdzamy też częściowo zakotwiczone ściany.
                for (int vertex = 0; vertex < 4; vertex++)
                    if ((p[(q * 4 + vertex) * S + 18] >> 2) != 0)
                    {
                        res.WavingMask |= 1 << b;
                        break;
                    }
                bucket[q] = (byte)b; count[b]++;
            }
            int acc = 0;
            for (int b = 0; b < Buckets; b++) { res.Start[b] = acc; acc += count[b] * 4; }
            res.Start[Buckets] = acc;
            Span<int> pos = stackalloc int[Buckets];
            for (int b = 0; b < Buckets; b++) pos[b] = res.Start[b];
            fixed (byte* d = res.Data)
                for (int q = 0; q < quads; q++)
                {
                    int b = bucket[q];
                    Buffer.MemoryCopy(p + q * 4 * S, d + pos[b] * S, 4 * S, 4 * S);
                    pos[b] += 4;
                }
        }
        for (int i = 0; i < 6; i++) res.Plane[i] = plane[i];
        return res;
    }

    private static void Track(Span<float> plane, int b, float c)
    {
        int i = b - 1;
        if ((i & 1) == 0) { if (c < plane[i]) plane[i] = c; }
        else if (c > plane[i]) plane[i] = c;
    }

    /// <summary>Maska widocznych kubełków dla kamery w układzie sekcji.</summary>
    public static int VisibleMask(float[] plane, float lx, float ly, float lz)
    {
        int m = 1;
        if (ly > plane[0]) m |= 1 << 1;
        if (ly < plane[1]) m |= 1 << 2;
        if (lx > plane[2]) m |= 1 << 3;
        if (lx < plane[3]) m |= 1 << 4;
        if (lz > plane[4]) m |= 1 << 5;
        if (lz < plane[5]) m |= 1 << 6;
        return m;
    }
}
