using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.Render;

/// <summary>Skórki encji z paczek zasobów 1.8 (textures/entity/...). Modele gry mają UV jak w 1.8, więc tekstura z paczki
/// trafia w te same piksele płótna; poza jej obszarem zostaje skórka wygenerowana przez grę.</summary>
public static partial class SkinGen
{
    /// <summary>Obraz z paczek (ścieżka względem textures/) albo null; ustawiany przez <see cref="Rebuild"/>.</summary>
    private static Func<string, RawImage> _packRead;

    /// <summary>Skórka gry -> plik 1.8 (bez .png) i jego szerokość w 1.8 (64 albo 128 dla konia i golema).</summary>
    public static readonly IReadOnlyDictionary<string, (string File, int VanillaW)> PackFiles = BuildPackFiles();

    private static Dictionary<string, (string, int)> BuildPackFiles()
    {
        var d = new Dictionary<string, (string, int)>();
        void A(string skin, string file, int w = 64) => d[skin] = ("entity/" + file, w);
        void Model(string skin, string path) => d[skin] = (path, 64);
        A("steve", "steve"); A("zombie", "zombie/zombie"); A("skeleton", "skeleton/skeleton"); A("creeper", "creeper/creeper");
        A("spider", "spider/spider"); A("cave_spider", "spider/cave_spider"); A("enderman", "enderman/enderman");
        A("cow", "cow/cow"); A("mooshroom", "cow/mooshroom"); A("pig", "pig/pig"); A("pig_saddle", "pig/pig_saddle");
        A("sheep", "sheep/sheep"); A("sheep_fur", "sheep/sheep_fur"); A("chicken", "chicken");
        A("ghast", "ghast/ghast"); A("ghast_shooting", "ghast/ghast_shooting"); A("blaze", "blaze"); A("magma_cube", "slime/magmacube");
        A("slime", "slime/slime"); A("zombie_pigman", "zombie_pigman"); A("wither_skeleton", "skeleton/wither_skeleton");
        A("endermite", "endermite"); A("silverfish", "silverfish"); A("wither", "wither/wither"); A("wither_invulnerable", "wither/wither_invulnerable");
        A("witch", "witch"); A("guardian", "guardian"); A("elder_guardian", "guardian_elder");
        A("guardian_beam", "guardian_beam"); A("zombie_villager", "zombie/zombie_villager"); A("creeper_charge", "creeper/creeper_armor");
        A("wolf", "wolf/wolf"); A("wolf_tame", "wolf/wolf_tame"); A("wolf_angry", "wolf/wolf_angry"); A("wolf_collar", "wolf/wolf_collar");
        A("ocelot", "cat/ocelot"); A("cat_black", "cat/black"); A("cat_red", "cat/red"); A("cat_siamese", "cat/siamese");
        foreach (var (skin, file) in new[] { ("brown", "brown"), ("white", "white"), ("black", "black"), ("black_white", "white_splotched"), ("gold", "gold"), ("salt", "salt"), ("caerbannog", "caerbannog") })
            A("rabbit_" + skin, "rabbit/" + file);
        A("bat", "bat"); A("squid", "squid"); A("leash_knot", "lead_knot"); A("snow_golem", "snowman");
        A("iron_golem", "iron_golem", 128);
        foreach (var v in new[] { "farmer", "librarian", "priest", "smith", "butcher" }) A("villager_" + v, "villager/" + v);
        foreach (var h in new[] { "white", "creamy", "chestnut", "brown", "black", "gray", "darkbrown" }) A("horse_" + h, "horse/horse_" + h, 128);
        A("horse_donkey", "horse/donkey", 128); A("horse_mule", "horse/mule", 128); A("horse_zombie", "horse/horse_zombie", 128); A("horse_skeleton", "horse/horse_skeleton", 128);
        // HorseMarkings 1.8: 1 white, 2 whitefield, 3 white_dots, 4 black_dots
        string[] marks = { "white", "whitefield", "whitedots", "blackdots" };
        for (int i = 0; i < marks.Length; i++) A("horse_markings_" + (i + 1), "horse/horse_markings_" + marks[i], 128);
        foreach (var m in new[] { "iron", "gold", "diamond" }) A("horse_armor_" + m, "horse/armor/horse_armor_" + m, 128);
        // czaszki (TileEntitySkullRenderer): głowy z tekstur mobów
        Model("armorstand", "entity/armorstand/wood");
        foreach (var mat in new[] { "leather", "iron", "gold", "diamond", "chainmail" })
            foreach (var layer in new[] { "_1", "_2" })
            {
                Model("armor_" + mat + layer, "models/armor/" + mat + "_layer" + layer);
                if (mat == "leather") Model("armor_leather" + layer + "_overlay", "models/armor/leather_layer" + layer + "_overlay");
            }
        A("skull_0", "skeleton/skeleton"); A("skull_1", "skeleton/wither_skeleton"); A("skull_2", "zombie/zombie"); A("skull_3", "steve"); A("skull_4", "creeper/creeper");
        return d;
    }

    private const int PaintingSheetSize = 256, MaxTexSize = 512;

    /// <summary>Warstwy z teksturami paczek w rozmiarze <see cref="TexSize"/>; pozostałe warstwy są skórkami gry.</summary>
    private static readonly Dictionary<int, uint[]> PackLayers = new();

    /// <summary>Tekstura z paczki dla jednej warstwy: obraz pokrywa UnitsW jednostek UV od lewego górnego rogu płótna.</summary>
    private readonly record struct SkinOverlay(int Layer, RawImage Img, int UnitsW);

    /// <summary>Generuje skórki od nowa: najpierw gra (obrazy, zbroje i stojak czytają paczkę przez <see cref="SourceTexture"/>),
    /// potem tekstury z paczek w pełnej rozdzielczości. Kolejność warstw się nie zmienia.</summary>
    public static void Rebuild(Func<string, RawImage> packRead)
    {
        _packRead = packRead;
        _init = false; Skins.Clear(); Index.Clear(); BigSkins.Clear(); PackLayers.Clear(); TexSize = BaseTexSize;
        Ensure();
        if (packRead == null) return;
        var overlays = MobOverlays(packRead).Concat(PaintingOverlays(packRead)).ToList();
        if (overlays.Count == 0) return;
        TexSize = RequiredTexSize(overlays);
        foreach (var o in overlays)
            PackLayers[o.Layer] = Overlay(PackLayers.TryGetValue(o.Layer, out var layer) ? layer : GameLayer(o.Layer), o.Img, CanvasUnits(o), o.UnitsW);
        Log.Info($"Paczki zasobów: {overlays.Count} skórek encji ({TexSize}x{TexSize})");
    }

    private static IEnumerable<SkinOverlay> MobOverlays(Func<string, RawImage> packRead)
    {
        foreach (var (skin, (file, vanillaW)) in PackFiles)
        {
            if (!Index.TryGetValue(skin, out int layer)) continue;
            // 1.8: 64x32, 64x64, 128x128, wiedźma 64x128
            var img = packRead(file + ".png")?.RequireSize(i => i.W % vanillaW == 0 && (i.H == i.W || i.H * 2 == i.W || i.H == i.W * 2),
                $"{vanillaW}x{vanillaW / 2}, {vanillaW}x{vanillaW} albo {vanillaW}x{vanillaW * 2} lub ich wielokrotność");
            if (img != null) yield return new SkinOverlay(layer, img, vanillaW);
        }
    }

    /// <summary>Obrazy wycięte z arkusza painting/paintings_kristoffer_zetterstrand.png w jego rozdzielczości.</summary>
    private static IEnumerable<SkinOverlay> PaintingOverlays(Func<string, RawImage> packRead)
    {
        var sheet = packRead(PaintingSheet)?.RequireSize(IsPaintingSheet, $"{PaintingSheetSize}x{PaintingSheetSize} albo wielokrotność");
        if (sheet == null) yield break;
        int k = sheet.W / PaintingSheetSize;
        foreach (var art in PaintingArt.All)
            yield return new SkinOverlay(Index["painting_" + art.Name], sheet.Crop(art.U * k, art.V * k, art.W * k, art.H * k), art.W);
    }

    private const string PaintingSheet = "painting/paintings_kristoffer_zetterstrand.png";
    private static bool IsPaintingSheet(RawImage i) => i.W == i.H && i.W % PaintingSheetSize == 0;

    /// <summary>Jednostki UV płótna warstwy: 128 dla dużych skórek gry (koń, golem), inaczej 64.</summary>
    private static int CanvasUnits(SkinOverlay o) => BigSkins.ContainsKey(o.Layer) || o.UnitsW == 128 ? 128 : 64;

    /// <summary>Najmniejszy rozmiar warstwy (potęga dwójki, 128..512), w którym żadna tekstura z paczki nie traci pikseli.</summary>
    private static int RequiredTexSize(IEnumerable<SkinOverlay> overlays)
    {
        int needed = overlays.Max(o => o.Img.W * CanvasUnits(o) / o.UnitsW), size = BaseTexSize;
        while (size < needed && size < MaxTexSize) size *= 2;
        if (needed > size) Log.Info($"Paczki zasobów: skórki encji zmniejszone do {size}x{size} (paczka ma {needed})");
        return size;
    }

    /// <summary>Wkleja teksturę 1.8 (vanillaW jednostek szerokości) w lewy górny róg płótna o canvasUnits jednostkach.</summary>
    private static uint[] Overlay(uint[] layer, RawImage img, int canvasUnits, int vanillaW)
    {
        int px = TexSize / canvasUnits; // pikseli warstwy na jednostkę UV
        int w = vanillaW * px, h = img.H * vanillaW * px / img.W;
        var scaled = img.Resize(w, h);
        for (int y = 0; y < Math.Min(h, TexSize); y++) for (int x = 0; x < Math.Min(w, TexSize); x++) layer[y * TexSize + x] = scaled.Px[y * w + x];
        return layer;
    }

    /// <summary>Tekstura dla skórek składanych przez grę (obrazy, zbroje, stojak): paczka, potem client.jar 1.8.
    /// Wynik w rozmiarze 1.8 (w x h); pełną rozdzielczość paczki nakłada potem <see cref="Rebuild"/>.</summary>
    private static uint[] SourceTexture(string path, int w, int h)
    {
        if (_packRead?.Invoke(path)?.RequireSize(i => i.W % w == 0 && i.W * h == i.H * w, $"{w}x{h} albo wielokrotność") is { } img)
            return img.Resize(w, h).Px;
        var px = ClientAssets.TryReadTexture(path, out int iw, out int ih);
        return px != null && iw == w && ih == h ? px : null;
    }
}
