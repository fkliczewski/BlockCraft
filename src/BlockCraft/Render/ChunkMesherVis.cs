using System.Runtime.CompilerServices;

namespace BlockCraft.Render;

/// <summary>VisGraph z MC 1.8: które ściany sekcji łączą się ze sobą przez bloki, które nie są pełnymi
/// nieprzezroczystymi sześcianami. Bit (a * 6 + b) = z ściany a widać ścianę b. Kolejność ścian jak EnumFacing:
/// 0 dół, 1 góra, 2 północ (-Z), 3 południe (+Z), 4 zachód (-X), 5 wschód (+X).</summary>
public sealed partial class ChunkMesher
{
    public const ulong VisAll = (1UL << 36) - 1;
    /// <summary>Łączność ścian ostatnio zbudowanej sekcji.</summary>
    public ulong Vis;
    private readonly ulong[] _seen = new ulong[64];
    private readonly short[] _stack = new short[4096];

    private void ComputeVisibility()
    {
        int opaque = 0;
        for (int y = 0; y < 16; y++)
            for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                    if (_opaque[_id[Idx(x, y, z)]]) opaque++;
        // VisGraph.computeVisibility: mniej niż 256 pełnych bloków = wszystko widać, same pełne = nic
        if (opaque < 256) { Vis = VisAll; return; }
        if (opaque == 4096) { Vis = 0; return; }
        Vis = 0;
        Array.Clear(_seen);
        for (int y = 0; y < 16; y++)
            for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                {
                    int c = x | z << 4 | y << 8;
                    if ((_seen[c >> 6] & (1UL << c)) != 0 || _opaque[_id[Idx(x, y, z)]]) continue;
                    // przechodzimy tylko od komórek na brzegu, wnętrza bez kontaktu z brzegiem nie zmieniają wyniku,
                    // ale i tak trzeba je oznaczyć, więc zalewamy każdą składową
                    int faces = Flood(c);
                    for (int a = 0; a < 6; a++)
                        if ((faces & (1 << a)) != 0)
                            for (int b = 0; b < 6; b++)
                                if ((faces & (1 << b)) != 0) Vis |= 1UL << (a * 6 + b);
                }
    }

    private int Flood(int start)
    {
        int sp = 0, faces = 0;
        _stack[sp++] = (short)start; _seen[start >> 6] |= 1UL << start;
        while (sp > 0)
        {
            int c = _stack[--sp];
            int x = c & 15, z = (c >> 4) & 15, y = c >> 8;
            if (y == 0) faces |= 1; else Push(c - 256, ref sp);
            if (y == 15) faces |= 2; else Push(c + 256, ref sp);
            if (z == 0) faces |= 4; else Push(c - 16, ref sp);
            if (z == 15) faces |= 8; else Push(c + 16, ref sp);
            if (x == 0) faces |= 16; else Push(c - 1, ref sp);
            if (x == 15) faces |= 32; else Push(c + 1, ref sp);
        }
        return faces;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Push(int c, ref int sp)
    {
        if ((_seen[c >> 6] & (1UL << c)) != 0) return;
        _seen[c >> 6] |= 1UL << c;
        if (_opaque[_id[Idx(c & 15, c >> 8, (c >> 4) & 15)]]) return;
        _stack[sp++] = (short)c;
    }

    public static bool VisConnects(ulong vis, int from, int to) => (vis & (1UL << (from * 6 + to))) != 0;
}
