using BlockCraft.World;

namespace BlockCraft.Render;

/// <summary>Falowanie roślin i wody (docs/GRAFIKA_PLAN.md, etap 2): rodzaj falowania zapisany w bitach od 2 flag wierzchołka,
/// ruch liczy shader chunków (wyłączony = obraz jak dotąd). Przesunięcie zależy tylko od pozycji wierzchołka w świecie,
/// więc wspólne wierzchołki sąsiednich bloków ruszają się razem i nie powstają szczeliny.</summary>
public sealed partial class ChunkMesher
{
    public const int WaveNone = 0, WaveLeaves = 1, WavePlant = 2, WaveWater = 3, WaveVine = 4;

    private static byte[] _waveKind;

    /// <summary>Wsp?lny wierzcho?ek li?ci pozostaje nieruchomy, je?li dotyka innego bloku.
    /// Sprawdzamy wszystkie bloki wok?? punktu, wi?c s?siednie li?cie maj? identyczne mocowanie,
    /// r?wnie? na granicach sekcji. Nie ods?aniamy ?cian usuni?tych przez mesher.</summary>
    private bool IsLeafVertexPinned(float x, float y, float z)
    {
        int minX = (int)Math.Ceiling(x) - 1, maxX = (int)Math.Floor(x);
        int minY = (int)Math.Ceiling(y) - 1, maxY = (int)Math.Floor(y);
        int minZ = (int)Math.Ceiling(z) - 1, maxZ = (int)Math.Floor(z);
        for (int by = minY; by <= maxY; by++)
            for (int bz = minZ; bz <= maxZ; bz++)
                for (int bx = minX; bx <= maxX; bx++)
                {
                    byte id = _access.GetBlock(bx + _ox, by + _oy, bz + _oz);
                    if (id != 0 && WaveKindOf(id) != WaveLeaves) return true;
                }
        return false;
    }

    /// <summary>Wsp?lna kraw?d? pn?czy ma jedno mocowanie, niezale?nie od bloku emituj?cego wierzcho?ek.</summary>
    private bool IsVineVertexPinned(float x, float y, float z)
    {
        int belowY = (int)Math.Round(y) - 1;
        for (int bz = (int)Math.Ceiling(z) - 1; bz <= (int)Math.Floor(z); bz++)
            for (int bx = (int)Math.Ceiling(x) - 1; bx <= (int)Math.Floor(x); bx++)
            {
                byte below = _access.GetBlock(bx + _ox, belowY + _oy, bz + _oz);
                if (below != 0 && WaveKindOf(below) != WaveVine) return true;
            }
        return false;
    }

    /// <summary>Rodzaj falowania bloku (Wave*).</summary>
    public static int WaveKindOf(byte id) => (_waveKind ??= BuildWaveKinds())[id];

    private static byte[] BuildWaveKinds()
    {
        var k = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            var b = Block.ById[i];
            if (b == null) continue;
            if (b is LeavesBlock) k[i] = WaveLeaves;
            else if (i == B.Vine) k[i] = WaveVine;
            else if (i == B.Water) k[i] = WaveWater;
            // pajęczyna, ogień, grzyby i trzcina (słupek z kilku bloków) stoją w miejscu
            else if (b.Render is RenderType.Cross or RenderType.Crops && i is not (B.Cobweb or B.Fire or B.BrownMushroom or B.RedMushroom or B.SugarCane))
                k[i] = WavePlant;
        }
        return k;
    }

    /// <summary>Ustawia falowanie dla wierzchołków bloku (x,y,z): roślina ma nieruchomy spód, górna połowa
    /// podwójnej rośliny rusza się cała (jej spód to wierzch dolnej połowy).</summary>
    private void SetWave(byte id, int x, int y, int z, int meta)
    {
        int k = WaveKindOf(id);
        // Pn?cza pod sufitem pozostaj? przy poziomej powierzchni mocowania.
        if (k == WaveVine && meta == 0) k = WaveNone;
        float baseY = k == WavePlant && !(id == B.DoublePlant && (meta & 8) != 0) ? y : float.NegativeInfinity;
        Solid.Wave = Translucent.Wave = k;
        Solid.WaveBaseY = Translucent.WaveBaseY = baseY;
    }
}
