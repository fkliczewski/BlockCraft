using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Render;

internal static class PackModelStates
{
    public readonly record struct State(string File, Dictionary<string, string> Props);

    /// <summary>Bloki z wariantami w meta, które w 1.8 mają osobne pliki blockstates.</summary>
    private static readonly Dictionary<string, string[]> FileByMeta = new()
    {
        ["stone"] = new[] { "stone", "granite", "smooth_granite", "diorite", "smooth_diorite", "andesite", "smooth_andesite" },
        ["dirt"] = new[] { "dirt", "coarse_dirt", "podzol" },
        ["sand"] = new[] { "sand", "red_sand" },
        ["sandstone"] = new[] { "sandstone", "chiseled_sandstone", "smooth_sandstone" },
        ["red_sandstone"] = new[] { "red_sandstone", "chiseled_red_sandstone", "smooth_red_sandstone" },
        ["stonebrick"] = new[] { "stonebrick", "mossy_stonebrick", "cracked_stonebrick", "chiseled_stonebrick" },
        ["prismarine"] = new[] { "prismarine", "prismarine_bricks", "dark_prismarine" },
        ["quartz_block"] = new[] { "quartz_block", "chiseled_quartz_block", "quartz_column" },
        ["red_flower"] = new[] { "poppy", "blue_orchid", "allium", "houstonia", "red_tulip", "orange_tulip", "white_tulip", "pink_tulip", "oxeye_daisy" },
        ["tallgrass"] = new[] { "tall_grass", "tall_grass", "fern" },
        ["monster_egg"] = new[] { "stone_monster_egg", "cobblestone_monster_egg", "stone_brick_monster_egg", "mossy_brick_monster_egg", "cracked_brick_monster_egg", "chiseled_brick_monster_egg" },
    };
    private static readonly Dictionary<string, string> FileRename = new() { ["yellow_flower"] = "dandelion", ["deadbush"] = "dead_bush" };

    public static State? For(Block b, int meta, bool snowy)
    {
        var props = new Dictionary<string, string>();
        string key = b.Key, file = key;
        bool hasProperties = TryPopulateProperties(b, meta, props);
        if (FileByMeta.TryGetValue(key, out var files))
        {
            int variant = key == "quartz_block" && meta is 3 or 4 ? 2 : meta;
            if (variant >= files.Length) return null;
            file = files[variant];
        }
        else if (b is ColoredBlock || key is "wool" or "stained_glass" or "stained_hardened_clay")
            file = TextureGen.DyeColorNames[meta & 15] + "_" + key;
        else if (FileRename.TryGetValue(key, out var renamed)) file = renamed;
        else if (!hasProperties) return null;

        if (key == "quartz_block" && meta >= 2) props["axis"] = meta == 3 ? "x" : meta == 4 ? "z" : "y";
        if (key is "grass" or "mycelium" || file == "podzol") props["snowy"] = snowy ? "true" : "false";
        return new State(file, props);
    }

    /// <summary>Znaczenie meta jako właściwości 1.8 dla obsługiwanych bloków; false = nie wiemy (geometria gry).</summary>
    private static bool TryPopulateProperties(Block b, int meta, Dictionary<string, string> props)
    {
        switch (b)
        {
            case SaplingBlock: props["stage"] = meta == 0 ? "0" : "1"; return true;
            case SugarCaneBlock: return true;
            case LeavesBlock: return true;
            case LogBlock: props["axis"] = (meta & 3) switch { 0 => "y", 1 => "x", 2 => "z", _ => "none" }; return true;
            case HayBlock: props["axis"] = (meta & 3) switch { 1 => "x", 2 => "z", _ => "y" }; return true; // jak kłody: 0 y, 1 x, 2 z
            case FurnaceBlock or PumpkinBlock: props["facing"] = PackModelDirections.Names[Dir.HorizToFace(meta & 3)]; return true;
            case DispenserBlock: props["facing"] = PackModelDirections.Names[DispenserBlock.Facing(meta) % 6]; return true;
            case PistonBlock: props["facing"] = PackModelDirections.Names[PistonBlock.Facing(meta) % 6]; props["extended"] = (meta & 8) != 0 ? "true" : "false"; return true;
        }
        if (b.Key == "sponge") { props["wet"] = meta == 1 ? "true" : "false"; return true; }
        return meta == 0;
    }
}
