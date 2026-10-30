using BlockCraft.World;

namespace BlockCraft.Items;

public static partial class Recipes
{
    static partial void RecipesRedstone()
    {
        // Zastąp dwie uproszczone receptury fundamentu recepturami 1.8.
        All.RemoveAll(r => r.Result.Id is B.RedstoneLamp or B.StickyPiston);
        Shaped(S(B.RedstoneLamp), " R ", "RGR", " R ", 'R', I.Redstone, 'G', B.Glowstone);
        Shaped(S(B.StickyPiston), "S", "P", 'S', I.Slimeball, 'P', B.Piston);

        Shaped(S(I.IronDoorItem, 3), "II", "II", "II", 'I', I.IronIngot);
        Shaped(S(B.Trapdoor, 2), "PPP", "PPP", 'P', AnyPlanks);
        Shaped(S(B.IronTrapdoor), "II", "II", 'I', I.IronIngot);
        Shaped(S(B.StonePressurePlate), "SS", 'S', B.Stone);
        Shaped(S(B.WoodPressurePlate), "PP", 'P', AnyPlanks);
        Shaped(S(B.LightWeightedPlate), "GG", 'G', I.GoldIngot);
        Shaped(S(B.HeavyWeightedPlate), "II", 'I', I.IronIngot);
        Shapeless(S(B.WoodButton), AnyPlanks);
        Shaped(S(B.TripwireHook, 2), "I", "S", "P", 'I', I.IronIngot, 'S', I.Stick, 'P', AnyPlanks);
        Shaped(S(B.DaylightSensor), "GGG", "QQQ", "SSS", 'G', B.Glass, 'Q', I.Quartz, 'S', AnyWoodSlab);
        Shaped(S(I.ComparatorItem), " T ", "TQT", "SSS", 'T', B.RedstoneTorch, 'Q', I.Quartz, 'S', B.Stone);
        Shaped(S(B.Hopper), "I I", "ICI", " I ", 'I', I.IronIngot, 'C', B.Chest);
        Shaped(S(B.Dropper), "CCC", "C C", "CRC", 'C', B.Cobblestone, 'R', I.Redstone);
        Shaped(S(B.Dispenser), "CCC", "CBC", "CRC", 'C', B.Cobblestone, 'B', I.Bow, 'R', I.Redstone);
        Shaped(S(B.NoteBlock), "PPP", "PRP", "PPP", 'P', AnyPlanks, 'R', I.Redstone);
        Shapeless(S(B.TrappedChest), B.Chest, B.TripwireHook);
        Shaped(S(B.SlimeBlock), "SSS", "SSS", "SSS", 'S', I.Slimeball);
        Shapeless(S(I.Slimeball, 9), B.SlimeBlock);
        Shaped(S(B.Rail, 16), "I I", "ISI", "I I", 'I', I.IronIngot, 'S', I.Stick);
        Shaped(S(B.PoweredRail, 6), "G G", "GSG", "GRG", 'G', I.GoldIngot, 'S', I.Stick, 'R', I.Redstone);
        Shaped(S(B.DetectorRail, 6), "I I", "IPI", "IRI", 'I', I.IronIngot, 'P', B.StonePressurePlate, 'R', I.Redstone);
        Shaped(S(B.ActivatorRail, 6), "ISI", "ITI", "ISI", 'I', I.IronIngot, 'S', I.Stick, 'T', B.RedstoneTorch);
        Shaped(S(I.Minecart), "I I", "III", 'I', I.IronIngot);
        Shapeless(S(I.ChestMinecart), I.Minecart, B.Chest);
        Shapeless(S(I.FurnaceMinecart), I.Minecart, B.Furnace);
        Shapeless(S(I.TntMinecart), I.Minecart, B.Tnt);
        Shapeless(S(I.HopperMinecart), I.Minecart, B.Hopper);
    }
}
