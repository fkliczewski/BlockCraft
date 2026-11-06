using System.Numerics;
using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Render;

/// <summary>Modele bloków z paczek zasobów 1.8: blockstates/*.json (warianty, obroty, wagi) i models/block/*.json
/// (parent, textures, elements z obrotem, faces z uv, cullface, tintindex, rotation). Używane tylko dla bloków pełnych
/// i roślin (krzyżyk), gdy któryś plik łańcucha pochodzi z paczki innej niż wbudowana „Minecraft 1.8 (oryginalne)” —
/// sama paczka 1.8 nie zmienia geometrii gry.</summary>
public static class PackModels
{
    /// <summary>Ściana gotowa do rysowania: 4 wierzchołki w jednostkach bloku (0..1), UV 0..1, warstwa atlasu.</summary>
    public sealed class Quad
    {
        public readonly Vector3[] P = new Vector3[4];
        public readonly Vector2[] Uv = new Vector2[4];
        public string Texture;
        public int Layer;
        /// <summary>Kierunek (Dir) zasłaniania przez sąsiada, -1 = zawsze rysowana.</summary>
        public int CullFace = -1;
        /// <summary>Kierunek najbliższy normalnej (do cieniowania ścian jak w grze).</summary>
        public int Normal;
        public bool Tint, Shade = true;
    }

    /// <summary>Jeden wariant modelu (blockstate może losować kilka z wagami).</summary>
    public sealed class Baked
    {
        public readonly List<Quad> Quads = new();
        public int Weight = 1;
        public bool AmbientOcclusion = true;
    }

    /// <summary>Warianty modelu z sumą wag używaną do losowania.</summary>
    internal sealed class Variants
    {
        public readonly Baked[] Models;
        public readonly long TotalWeight;

        public Variants(Baked[] models)
        {
            Models = models;
            foreach (var model in models) TotalWeight += model.Weight;
        }
    }

    public sealed class Loaded
    {
        internal readonly Variants[][][] Models;
        internal readonly int Count;
        public IReadOnlySet<string> TexturePaths { get; }

        internal Loaded(Variants[][][] models, HashSet<string> texturePaths, int count)
        {
            Models = models;
            TexturePaths = texturePaths;
            Count = count;
        }
    }

    /// <summary>[id bloku][meta][snowy] -> warianty; snowy = 1 oznacza pokrycie śniegiem. Null = geometria gry.</summary>
    private static Variants[][][] _models = new Variants[256][][];
    public static bool Any { get; private set; }
    public static int Count { get; private set; }

    /// <summary>Wybrany wariant dla bloku w pozycji (losowanie wagowe stałe dla pozycji jak getPositionRandom).</summary>
    public static Baked Get(int id, int meta, int x, int y, int z, bool snowy)
    {
        var byMeta = _models[id];
        if (byMeta == null) return null;
        var bySnow = byMeta[meta & 15];
        if (bySnow == null) return null;
        var variants = bySnow[snowy && bySnow[1] != null ? 1 : 0];
        var list = variants?.Models;
        if (list == null || list.Length == 0) return null;
        if (list.Length == 1) return list[0];
        long seed = (x * 3129871L) ^ (z * 116129781L) ^ y;
        seed = seed * seed * 42317861L + seed * 11L;
        long r = ((seed >> 16) & 0x7FFFFFFF) % variants.TotalWeight;
        foreach (var m in list) { r -= m.Weight; if (r < 0) return m; }
        return list[0];
    }

    /// <summary>Paczka: odczyt pliku i czy to wbudowana paczka 1.8 (jej pliki nie włączają modeli).</summary>
    public readonly record struct Source(Func<string, byte[]> Read, bool Builtin);

    public static Loaded Load(IReadOnlyList<Source> packs) => PackModelLoader.Load(packs);

    /// <summary>Przypisuje warstwy atlasu teksturom (po nałożeniu paczek) i podmienia modele używane przez mesher.</summary>
    public static void Activate(Loaded loaded, Func<string, int> layerOf)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(layerOf);
        var next = loaded.Models;
        foreach (var q in next.Where(m => m != null).SelectMany(m => m).Where(m => m != null).SelectMany(m => m).Where(m => m != null)
                     .SelectMany(m => m.Models).SelectMany(m => m.Quads))
            q.Layer = layerOf(q.Texture);
        _models = next;
        Count = loaded.Count;
        Any = Count > 0;
        if (Any) Log.Info($"Paczki zasobów: modele bloków z paczek dla {Count} stanów bloków");
    }

}
