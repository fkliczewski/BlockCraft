using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>Klasa biomu jak getBiomeClass() w MC 1.8 (BiomeGenBase.isEqualTo porównuje klasy, wersje "M" mają klasę bazy).</summary>
public enum BiomeKind { Ocean, Plains, Desert, Hills, Forest, Taiga, Swamp, River, Hell, Sky, Snow, Mushroom, Beach, Jungle, StoneBeach, Savanna, Mesa }

/// <summary>Kategoria temperatury (BiomeGenBase.TempCategory).</summary>
public enum TempCategory { Ocean, Cold, Medium, Warm }

/// <summary>Definicja biomu MC 1.8 (BiomeGenBase): wysokości, temperatura, bloki powierzchni i ustawienia dekoratora.</summary>
public sealed class BiomeDef
{
    public int Id;
    public string Name, McName;
    public BiomeKind Kind;
    public float Temp = 0.5f, Rain = 0.5f, MinHeight = 0.1f, MaxHeight = 0.2f;
    public bool Snow, NoRain;
    public byte Top = B.Grass, Filler = B.Dirt;
    public byte TopMeta, FillerMeta;
    public uint WaterColor = 0xFFFFFF;
    /// <summary>Wariant klasy (Forest: 0 zwykły, 1 kwiecisty, 2 brzozowy, 3 mroczny; Taiga: 0, 1 mega, 2 mega świerkowa;
    /// Hills: 0 zwykłe, 1 krawędź/plus, 2 M; Mesa: bit 1 bryce, bit 2 las; Snow: 1 kolce lodowe; Plains: 1 słoneczniki).</summary>
    public int Variant;
    /// <summary>ID biomu bazowego dla wersji zmutowanych (id+128), inaczej -1.</summary>
    public int Base = -1;
    // BiomeDecorator
    public int Trees, Flowers = 2, Grass = 1, DeadBush, Mushrooms, Reeds, Cacti, SandPatches = 3, GravelPatches = 1, Clay = 1, BigMushrooms, Lilies;
    public bool Lakes = true;

    public bool IsMutation => Base >= 0;
    public TempCategory TempCategory => Kind == BiomeKind.Ocean ? TempCategory.Ocean : Temp < 0.2f ? TempCategory.Cold : Temp < 1.0f ? TempCategory.Medium : TempCategory.Warm;
    public bool HighHumidity => Rain > 0.85f;
    public bool CanRain => !Snow && !NoRain;
}

/// <summary>Biomy MC 1.8 z ID jak w grze (Chunk.Biomes przechowuje ID). Kolory trawy/liści jak ColorizerGrass/ColorizerFoliage.</summary>
public static class Biome
{
    public const byte Ocean = 0, Plains = 1, Desert = 2, ExtremeHills = 3, Forest = 4, Taiga = 5, Swampland = 6, River = 7, Hell = 8, Sky = 9,
        FrozenOcean = 10, FrozenRiver = 11, IcePlains = 12, IceMountains = 13, MushroomIsland = 14, MushroomIslandShore = 15, Beach = 16,
        DesertHills = 17, ForestHills = 18, TaigaHills = 19, ExtremeHillsEdge = 20, Jungle = 21, JungleHills = 22, JungleEdge = 23,
        DeepOcean = 24, StoneBeach = 25, ColdBeach = 26, BirchForest = 27, BirchForestHills = 28, RoofedForest = 29, ColdTaiga = 30,
        ColdTaigaHills = 31, MegaTaiga = 32, MegaTaigaHills = 33, ExtremeHillsPlus = 34, Savanna = 35, SavannaPlateau = 36, Mesa = 37,
        MesaPlateauF = 38, MesaPlateau = 39;
    public const int Mutation = 128;
    public const byte SunflowerPlains = 129, DesertM = 130, ExtremeHillsM = 131, FlowerForest = 132, TaigaM = 133, SwamplandM = 134,
        IcePlainsSpikes = 140, JungleM = 149, JungleEdgeM = 151, BirchForestM = 155, BirchForestHillsM = 156, RoofedForestM = 157,
        ColdTaigaM = 158, MegaSpruceTaiga = 160, MegaSpruceTaigaHills = 161, ExtremeHillsPlusM = 162, SavannaM = 163,
        SavannaPlateauM = 164, MesaBryce = 165, MesaPlateauFM = 166, MesaPlateauM = 167;

    public static readonly BiomeDef[] ById = new BiomeDef[256];

    // BiomeGenBase.Height
    private static readonly (float min, float max) HDefault = (0.1f, 0.2f), HShallowWaters = (-0.5f, 0f), HOceans = (-1f, 0.1f),
        HDeepOceans = (-1.8f, 0.1f), HLowPlains = (0.125f, 0.05f), HMidPlains = (0.2f, 0.2f), HLowHills = (0.45f, 0.3f),
        HHighPlateaus = (1.5f, 0.025f), HMidHills = (1f, 0.5f), HShores = (0f, 0.025f), HRockyWaters = (0.1f, 0.8f),
        HLowIslands = (0.2f, 0.3f), HPartiallySubmerged = (-0.2f, 0.1f);

    static Biome()
    {
        Def(Ocean, "Ocean", "Ocean", BiomeKind.Ocean, 0.5f, 0.5f, HOceans);
        Def(Plains, "Równiny", "Plains", BiomeKind.Plains, 0.8f, 0.4f, HLowPlains, d => { d.Trees = 0; d.Flowers = 4; d.Grass = 10; });
        Def(Desert, "Pustynia", "Desert", BiomeKind.Desert, 2f, 0f, HLowPlains, DesertDeco);
        Def(ExtremeHills, "Góry", "Extreme Hills", BiomeKind.Hills, 0.2f, 0.3f, HMidHills, d => d.Trees = 0);
        Def(Forest, "Las", "Forest", BiomeKind.Forest, 0.7f, 0.8f, HDefault, d => { d.Trees = 10; d.Grass = 2; });
        Def(Taiga, "Tajga", "Taiga", BiomeKind.Taiga, 0.25f, 0.8f, HMidPlains, d => { d.Trees = 10; d.Grass = 1; d.Mushrooms = 1; });
        Def(Swampland, "Bagno", "Swampland", BiomeKind.Swamp, 0.8f, 0.9f, HPartiallySubmerged, SwampDeco).WaterColor = 0xE0FFAE;
        Def(River, "Rzeka", "River", BiomeKind.River, 0.5f, 0.5f, HShallowWaters);
        Def(Hell, "Nether", "Hell", BiomeKind.Hell, 2f, 0f, HDefault, d => d.NoRain = true);
        Def(Sky, "Kres", "The End", BiomeKind.Sky, 0.5f, 0.5f, HDefault, d => d.NoRain = true);
        Def(FrozenOcean, "Zamarznięty ocean", "FrozenOcean", BiomeKind.Ocean, 0f, 0.5f, HOceans, d => d.Snow = true);
        Def(FrozenRiver, "Zamarznięta rzeka", "FrozenRiver", BiomeKind.River, 0f, 0.5f, HShallowWaters, d => d.Snow = true);
        Def(IcePlains, "Lodowa równina", "Ice Plains", BiomeKind.Snow, 0f, 0.5f, HLowPlains, d => d.Snow = true);
        Def(IceMountains, "Lodowe góry", "Ice Mountains", BiomeKind.Snow, 0f, 0.5f, HLowHills, d => d.Snow = true);
        Def(MushroomIsland, "Grzybowa wyspa", "MushroomIsland", BiomeKind.Mushroom, 0.9f, 1f, HLowIslands, MushroomDeco);
        Def(MushroomIslandShore, "Brzeg grzybowej wyspy", "MushroomIslandShore", BiomeKind.Mushroom, 0.9f, 1f, HShores, MushroomDeco);
        Def(Beach, "Plaża", "Beach", BiomeKind.Beach, 0.8f, 0.4f, HShores, BeachDeco);
        Def(DesertHills, "Pustynne wzgórza", "DesertHills", BiomeKind.Desert, 2f, 0f, HLowHills, DesertDeco);
        Def(ForestHills, "Zalesione wzgórza", "ForestHills", BiomeKind.Forest, 0.7f, 0.8f, HLowHills, d => { d.Trees = 10; d.Grass = 2; });
        Def(TaigaHills, "Wzgórza tajgi", "TaigaHills", BiomeKind.Taiga, 0.25f, 0.8f, HLowHills, d => { d.Trees = 10; d.Grass = 1; d.Mushrooms = 1; });
        Def(ExtremeHillsEdge, "Skraj gór", "Extreme Hills Edge", BiomeKind.Hills, 0.2f, 0.3f, (HMidHills.min * 0.8f, HMidHills.max * 0.6f), d => { d.Trees = 0; d.Variant = 1; });
        Def(Jungle, "Dżungla", "Jungle", BiomeKind.Jungle, 0.95f, 0.9f, HDefault, d => { d.Trees = 50; d.Grass = 25; d.Flowers = 4; });
        Def(JungleHills, "Wzgórza dżungli", "JungleHills", BiomeKind.Jungle, 0.95f, 0.9f, HLowHills, d => { d.Trees = 50; d.Grass = 25; d.Flowers = 4; });
        Def(JungleEdge, "Skraj dżungli", "JungleEdge", BiomeKind.Jungle, 0.95f, 0.8f, HDefault, d => { d.Trees = 2; d.Grass = 25; d.Flowers = 4; d.Variant = 1; });
        Def(DeepOcean, "Głęboki ocean", "Deep Ocean", BiomeKind.Ocean, 0.5f, 0.5f, HDeepOceans);
        Def(StoneBeach, "Kamienna plaża", "Stone Beach", BiomeKind.StoneBeach, 0.2f, 0.3f, HRockyWaters, d => { BeachDeco(d); d.Top = B.Stone; d.Filler = B.Stone; });
        Def(ColdBeach, "Zimna plaża", "Cold Beach", BiomeKind.Beach, 0.05f, 0.3f, HShores, d => { BeachDeco(d); d.Snow = true; });
        Def(BirchForest, "Las brzozowy", "Birch Forest", BiomeKind.Forest, 0.6f, 0.6f, HDefault, d => { d.Trees = 10; d.Grass = 2; d.Variant = 2; });
        Def(BirchForestHills, "Wzgórza lasu brzozowego", "Birch Forest Hills", BiomeKind.Forest, 0.6f, 0.6f, HLowHills, d => { d.Trees = 10; d.Grass = 2; d.Variant = 2; });
        Def(RoofedForest, "Mroczny las", "Roofed Forest", BiomeKind.Forest, 0.7f, 0.8f, HDefault, d => { d.Trees = -999; d.Grass = 2; d.Variant = 3; });
        Def(ColdTaiga, "Zimna tajga", "Cold Taiga", BiomeKind.Taiga, -0.5f, 0.4f, HMidPlains, d => { d.Trees = 10; d.Grass = 1; d.Mushrooms = 1; d.Snow = true; });
        Def(ColdTaigaHills, "Wzgórza zimnej tajgi", "Cold Taiga Hills", BiomeKind.Taiga, -0.5f, 0.4f, HLowHills, d => { d.Trees = 10; d.Grass = 1; d.Mushrooms = 1; d.Snow = true; });
        Def(MegaTaiga, "Wielka tajga", "Mega Taiga", BiomeKind.Taiga, 0.3f, 0.8f, HMidPlains, MegaTaigaDeco);
        Def(MegaTaigaHills, "Wzgórza wielkiej tajgi", "Mega Taiga Hills", BiomeKind.Taiga, 0.3f, 0.8f, HLowHills, MegaTaigaDeco);
        Def(ExtremeHillsPlus, "Góry+", "Extreme Hills+", BiomeKind.Hills, 0.2f, 0.3f, HMidHills, d => { d.Trees = 3; d.Variant = 1; });
        Def(Savanna, "Sawanna", "Savanna", BiomeKind.Savanna, 1.2f, 0f, HLowPlains, SavannaDeco);
        Def(SavannaPlateau, "Płaskowyż sawanny", "Savanna Plateau", BiomeKind.Savanna, 1f, 0f, HHighPlateaus, SavannaDeco);
        Def(Mesa, "Płaskowyż Mesa", "Mesa", BiomeKind.Mesa, 2f, 0f, HDefault, MesaDeco);
        Def(MesaPlateauF, "Zalesiony płaskowyż Mesa", "Mesa Plateau F", BiomeKind.Mesa, 2f, 0f, HHighPlateaus, d => { MesaDeco(d); d.Trees = 5; d.Variant = 2; });
        Def(MesaPlateau, "Płaskowyż Mesa", "Mesa Plateau", BiomeKind.Mesa, 2f, 0f, HHighPlateaus, MesaDeco);

        // mutacje (BiomeGenBase.createMutation), domyślnie BiomeGenMutated: wysokość (min + 0.1, max + 0.2)
        Mut(Plains, "Równiny słonecznikowe", "Sunflower Plains", d => { d.MinHeight -= 0.1f; d.MaxHeight -= 0.2f; d.Variant = 1; });
        Mut(Desert, "Pustynia M", "Desert M");
        Mut(ExtremeHills, "Góry M", "Extreme Hills M", d => { d.MinHeight -= 0.1f; d.MaxHeight -= 0.2f; d.Variant = 2; });
        Mut(Forest, "Las kwiecisty", "Flower Forest", d => { d.MinHeight -= 0.1f; d.Trees = 6; d.Flowers = 100; d.Grass = 1; d.Variant = 1; });
        Mut(Taiga, "Tajga M", "Taiga M");
        Mut(Swampland, "Bagno M", "Swampland M");
        Mut(IcePlains, "Lodowe kolce", "Ice Plains Spikes", d => { d.MinHeight += 0.2f; d.MaxHeight += 0.2f; d.Variant = 1; d.Top = B.Snow; });
        Mut(Jungle, "Dżungla M", "Jungle M");
        Mut(JungleEdge, "Skraj dżungli M", "JungleEdge M");
        Mut(BirchForest, "Las brzozowy M", "Birch Forest M", d => d.Variant = 4);
        Mut(BirchForestHills, "Wzgórza lasu brzozowego M", "Birch Forest Hills M", d => d.Variant = 4);
        Mut(RoofedForest, "Mroczny las M", "Roofed Forest M");
        Mut(ColdTaiga, "Zimna tajga M", "Cold Taiga M");
        Mut(MegaTaiga, "Wielka tajga świerkowa", "Mega Spruce Taiga", d => { d.MinHeight -= 0.1f; d.MaxHeight -= 0.2f; d.Temp = 0.25f; d.Variant = 2; });
        Mut(MegaTaigaHills, "Wzgórza wielkiej tajgi świerkowej", "Redwood Taiga Hills M", d => d.Variant = 2);
        Mut(ExtremeHillsPlus, "Góry+ M", "Extreme Hills+ M", d => { d.MinHeight -= 0.1f; d.MaxHeight -= 0.2f; d.Variant = 2; });
        Mut(Savanna, "Sawanna M", "Savanna M", d => { d.MinHeight = 0.125f * 0.5f + 0.3f; d.MaxHeight = 0.05f * 0.5f + 1.2f; d.Temp = (1.2f + 1f) * 0.5f; d.Variant = 1; });
        Mut(SavannaPlateau, "Płaskowyż sawanny M", "Savanna Plateau M", d => { d.MinHeight = 1.5f * 0.5f + 0.3f; d.MaxHeight = 0.025f * 0.5f + 1.2f; d.Temp = 1f; d.Variant = 1; });
        Mut(Mesa, "Mesa (Bryce)", "Mesa (Bryce)", d => { d.MinHeight -= 0.1f; d.MaxHeight -= 0.2f; d.Variant = 1; });
        Mut(MesaPlateauF, "Zalesiony płaskowyż Mesa M", "Mesa Plateau F M", d => { (d.MinHeight, d.MaxHeight) = HLowHills; });
        Mut(MesaPlateau, "Płaskowyż Mesa M", "Mesa Plateau M", d => { (d.MinHeight, d.MaxHeight) = HLowHills; });
    }

    private static void DesertDeco(BiomeDef d) { d.Top = B.Sand; d.Filler = B.Sand; d.Trees = -999; d.DeadBush = 2; d.Reeds = 50; d.Cacti = 10; d.NoRain = true; }
    private static void SwampDeco(BiomeDef d) { d.Trees = 2; d.Flowers = 1; d.DeadBush = 1; d.Mushrooms = 8; d.Reeds = 10; d.Clay = 1; d.Lilies = 4; d.SandPatches = 0; d.GravelPatches = 0; d.Grass = 5; }
    private static void MushroomDeco(BiomeDef d) { d.Trees = -100; d.Flowers = -100; d.Grass = -100; d.Mushrooms = 1; d.BigMushrooms = 1; d.Top = B.Mycelium; }
    private static void BeachDeco(BiomeDef d) { d.Top = B.Sand; d.Filler = B.Sand; d.Trees = -999; d.DeadBush = 0; d.Reeds = 0; d.Cacti = 0; }
    private static void MegaTaigaDeco(BiomeDef d) { d.Trees = 10; d.Grass = 7; d.DeadBush = 1; d.Mushrooms = 3; d.Variant = 1; }
    private static void SavannaDeco(BiomeDef d) { d.Trees = 1; d.Flowers = 4; d.Grass = 20; d.NoRain = true; }
    private static void MesaDeco(BiomeDef d)
    {
        d.Top = B.Sand; d.TopMeta = 1; d.Filler = B.StainedClay; d.FillerMeta = 1;
        d.Trees = -999; d.DeadBush = 20; d.Reeds = 3; d.Cacti = 5; d.Flowers = 0; d.NoRain = true;
    }

    private static BiomeDef Def(int id, string name, string mc, BiomeKind kind, float temp, float rain, (float min, float max) h, Action<BiomeDef> cfg = null)
    {
        var d = new BiomeDef { Id = id, Name = name, McName = mc, Kind = kind, Temp = temp, Rain = rain, MinHeight = h.min, MaxHeight = h.max };
        cfg?.Invoke(d);
        ById[id] = d;
        return d;
    }

    private static void Mut(int baseId, string name, string mc, Action<BiomeDef> cfg = null)
    {
        var b = ById[baseId];
        var d = new BiomeDef
        {
            Id = baseId + Mutation, Name = name, McName = mc, Kind = b.Kind, Temp = b.Temp, Rain = b.Rain, Snow = b.Snow, NoRain = b.NoRain,
            MinHeight = b.MinHeight + 0.1f, MaxHeight = b.MaxHeight + 0.2f, Top = b.Top, Filler = b.Filler, TopMeta = b.TopMeta, FillerMeta = b.FillerMeta,
            WaterColor = b.WaterColor, Variant = b.Variant, Base = baseId, Trees = b.Trees, Flowers = b.Flowers, Grass = b.Grass, DeadBush = b.DeadBush,
            Mushrooms = b.Mushrooms, Reeds = b.Reeds, Cacti = b.Cacti, SandPatches = b.SandPatches, GravelPatches = b.GravelPatches, Clay = b.Clay,
            BigMushrooms = b.BigMushrooms, Lilies = b.Lilies, Lakes = b.Lakes
        };
        cfg?.Invoke(d);
        ById[d.Id] = d;
    }

    public static BiomeDef Get(int id) => (uint)id < 256 && ById[id] != null ? ById[id] : ById[Plains];
    public static bool Exists(int id) => (uint)id < 256 && ById[id] != null;
    public static string Name(int b) => Get(b).Name;
    public static bool IsOceanic(int id) => id == Ocean || id == DeepOcean || id == FrozenOcean;
    /// <summary>BiomeGenBase.isEqualTo: ta sama klasa biomu (mutacje liczą się jak baza).</summary>
    public static bool SameClass(int a, int b) => a == b || Exists(a) && Exists(b) && Get(a).Kind == Get(b).Kind;

    // ---------------- temperatura z wysokością (BiomeGenBase.getFloatTemperature) ----------------
    private static readonly SimplexOctaves TempNoise = new(new JRandom(1234L), 1);
    private static readonly SimplexOctaves GrassNoise = new(new JRandom(2345L), 1);

    /// <summary>Szum GRASS_COLOR_NOISE z MC (kwiaty na równinach, bagna, kwiecisty las).</summary>
    public static double GrassColorNoise(double x, double z) => GrassNoise.Sample(x, z);

    public static float TemperatureAt(int biome, int x, int y, int z)
    {
        var d = Get(biome);
        if (y > 64)
        {
            float n = (float)(TempNoise.Sample(x * 1.0 / 8.0, z * 1.0 / 8.0) * 4.0);
            return d.Temp - (n + y - 64f) * 0.05f / 30f;
        }
        return d.Temp;
    }

    /// <summary>Czy w tym miejscu pada śnieg zamiast deszczu (temperatura &lt; 0.15).</summary>
    public static bool IsSnowyAt(int biome, int x, int y, int z) => TemperatureAt(biome, x, y, z) < 0.15f;

    // ---------------- kolory ----------------
    private static uint Tri(float temp, float rain, uint cold, uint dry, uint wet)
    {
        temp = Math.Clamp(temp, 0f, 1f); rain = Math.Clamp(rain, 0f, 1f) * temp;
        float w0 = 1f - temp, w2 = rain, w1 = temp - rain;
        int Ch(int s) => (int)Math.Clamp(((cold >> s) & 255) * w0 + ((dry >> s) & 255) * w1 + ((wet >> s) & 255) * w2 + 0.5f, 0, 255);
        return (uint)((Ch(16) << 16) | (Ch(8) << 8) | Ch(0));
    }

    /// <summary>Kolor trawy (ColorizerGrass): tabela dla biomów z wiki MC 1.8, reszta z interpolacji mapy kolorów.</summary>
    public static uint GrassColor(int b)
    {
        var d = Get(b);
        if (d.Kind == BiomeKind.Swamp) return 0x6A7039;
        if (d.Kind == BiomeKind.Mesa) return 0x90814D;
        uint c = Tri(d.Temp, d.Rain, 0x80B497, 0xBFB755, 0x47CD33);
        int baseId = d.Base >= 0 ? d.Base : d.Id;
        c = baseId switch
        {
            Plains or Beach => 0x91BD59, Desert or DesertHills or Savanna or SavannaPlateau => 0xBFB755, ExtremeHills or ExtremeHillsEdge or ExtremeHillsPlus or StoneBeach => 0x8AB689,
            Forest or ForestHills => 0x79C05A, Taiga or TaigaHills => 0x86B783, Ocean or River or DeepOcean => 0x8EB971,
            FrozenOcean or FrozenRiver or IcePlains or IceMountains or ColdTaiga or ColdTaigaHills => 0x80B497, MushroomIsland or MushroomIslandShore => 0x55C93F,
            Jungle or JungleHills => 0x59C93C, JungleEdge => 0x64C73F, ColdBeach => 0x83B593, BirchForest or BirchForestHills => 0x88BB67,
            MegaTaiga or MegaTaigaHills => 0x86B87F, _ => c
        };
        if (baseId == RoofedForest) c = (((c & 0xFEFEFE) + 0x28340A) >> 1);
        return c;
    }

    /// <summary>Kolor trawy w punkcie (bagno: dwa odcienie z szumu jak BiomeGenSwamp).</summary>
    public static uint GrassColorAt(int b, int x, int z)
    {
        if (Get(b).Kind == BiomeKind.Swamp) return GrassColorNoise(x * 0.0225, z * 0.0225) < -0.1 ? 0x4C763Cu : 0x6A7039u;
        return GrassColor(b);
    }

    public static uint FoliageColor(int b)
    {
        var d = Get(b);
        if (d.Kind == BiomeKind.Swamp) return 0x6A7039;
        if (d.Kind == BiomeKind.Mesa) return 0x9E814D;
        int baseId = d.Base >= 0 ? d.Base : d.Id;
        return baseId switch
        {
            Plains or Beach => 0x77AB2F, Desert or DesertHills or Savanna or SavannaPlateau => 0xAEA42A, ExtremeHills or ExtremeHillsEdge or ExtremeHillsPlus or StoneBeach => 0x6DA36B,
            Forest or ForestHills or RoofedForest => 0x59AE30, Taiga or TaigaHills => 0x68A464, Ocean or River or DeepOcean => 0x71A74D,
            FrozenOcean or FrozenRiver or IcePlains or IceMountains or ColdTaiga or ColdTaigaHills => 0x60A17B, MushroomIsland or MushroomIslandShore => 0x2BBB0F,
            Jungle or JungleHills => 0x30BB0B, JungleEdge => 0x3EB80F, ColdBeach => 0x64A278, BirchForest or BirchForestHills => 0x6BA941,
            _ => Tri(d.Temp, d.Rain, 0x60A17B, 0xAEA42A, 0x1ABF00)
        };
    }

    public static uint WaterColor(int b) => Get(b).WaterColor;
}
