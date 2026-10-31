namespace BlockCraft.World;

/// <summary>
/// Identyfikatory bloków. 0-78 to bloki istniejące przed rozbudową do MC 1.8.
/// Od 79 w górę: wszystkie brakujące bloki MC 1.8, każdy przypisany do modułu (właściciela), który go rejestruje,
/// teksturuje i implementuje. Inne moduły mogą się do nich odwoływać; dopóki właściciel nie zarejestruje bloku,
/// Block.ById[id] wskazuje na powietrze. Warianty jak w MC trzymamy w meta (np. kamień: granit, dioryt, andezyt).
/// Moduły: OW = overworld (biomy, drewna, rośliny, budulce, struktury), RS = redstone i transport,
/// NE = Nether i End, MO = moby, ME = mechaniki itemów i bloki użytkowe.
/// </summary>
public static class B
{
    public const byte Air = 0, Stone = 1, Grass = 2, Dirt = 3, Cobblestone = 4, OakPlanks = 5, BirchPlanks = 6, Bedrock = 7,
        Water = 8, Lava = 9, Sand = 10, Gravel = 11, GoldOre = 12, IronOre = 13, CoalOre = 14, OakLog = 15, BirchLog = 16,
        OakLeaves = 17, BirchLeaves = 18, Glass = 19, LapisOre = 20, LapisBlock = 21, Sandstone = 22, Wool = 23,
        Dandelion = 24, Poppy = 25, TallGrass = 26, DeadBush = 27, Cactus = 28, SugarCane = 29, OakSapling = 30, BirchSapling = 31,
        GoldBlock = 32, IronBlock = 33, DiamondOre = 34, DiamondBlock = 35, CraftingTable = 36, Furnace = 37, FurnaceLit = 38,
        Chest = 39, Torch = 40, RedstoneOre = 41, RedstoneBlock = 42, CoalBlock = 43, Obsidian = 44, MossyCobblestone = 45,
        StoneBricks = 46, Farmland = 47, Wheat = 48, Ladder = 49, OakDoor = 50, Fence = 51, Tnt = 52, RedstoneWire = 53,
        RedstoneTorch = 54, RedstoneTorchOff = 55, Lever = 56, StoneButton = 57, RedstoneLamp = 58, RedstoneLampOn = 59,
        Repeater = 60, RepeaterOn = 61, Piston = 62, StickyPiston = 63, PistonHead = 64, Bed = 65, CobbleSlab = 66,
        OakSlab = 67, StoneSlab = 68, OakStairs = 69, CobbleStairs = 70, FenceGate = 71, Bricks = 72, Bookshelf = 73,
        Glowstone = 74, Snow = 75, Ice = 76, Clay = 77, DoubleStoneSlab = 78;

    // ---- OW: drewna (6 gatunków jak w 1.8), rośliny, budulce, bloki biomów i struktur ----
    // Warianty meta w istniejących blokach (też OW): Stone (0 kamień, 1 granit, 2 polerowany granit, 3 dioryt, 4 pol. dioryt,
    // 5 andezyt, 6 pol. andezyt), Dirt (0, 1 gruboziarnista, 2 bielica), Sand (0, 1 czerwony), Sandstone (0, 1 rzeźbiony, 2 gładki),
    // StoneBricks (0, 1 zamszone, 2 popękane, 3 rzeźbione), Wool (16 kolorów), Poppy (0-8 kwiaty red_flower), TallGrass (1 trawa, 2 paproć).
    public const byte SprucePlanks = 79, JunglePlanks = 80, AcaciaPlanks = 81, DarkOakPlanks = 82,
        SpruceLog = 83, JungleLog = 84, AcaciaLog = 85, DarkOakLog = 86,
        SpruceLeaves = 87, JungleLeaves = 88, AcaciaLeaves = 89, DarkOakLeaves = 90,
        SpruceSapling = 91, JungleSapling = 92, AcaciaSapling = 93, DarkOakSapling = 94,
        BirchSlab = 95, SpruceSlab = 96, JungleSlab = 97, AcaciaSlab = 98, DarkOakSlab = 99,
        BirchStairs = 100, SpruceStairs = 101, JungleStairs = 102, AcaciaStairs = 103, DarkOakStairs = 104,
        SpruceFence = 105, BirchFence = 106, JungleFence = 107, AcaciaFence = 108, DarkOakFence = 109,
        SpruceFenceGate = 110, BirchFenceGate = 111, JungleFenceGate = 112, AcaciaFenceGate = 113, DarkOakFenceGate = 114,
        SpruceDoor = 115, BirchDoor = 116, JungleDoor = 117, AcaciaDoor = 118, DarkOakDoor = 119,
        RedSandstone = 120, RedSandstoneStairs = 121, RedSandstoneSlab = 122, SandstoneStairs = 123, SandstoneSlab = 124,
        Sponge = 125, Cobweb = 126, DoublePlant = 127, BrownMushroom = 128, RedMushroom = 129,
        BrownMushroomBlock = 130, RedMushroomBlock = 131, Mycelium = 132,
        Vine = 133, LilyPad = 134, Cocoa = 135, Pumpkin = 136, JackOLantern = 137, Melon = 138, PumpkinStem = 139, MelonStem = 140,
        Carrots = 141, Potatoes = 142, SnowLayer = 143, PackedIce = 144, HardenedClay = 145, StainedClay = 146,
        StoneBrickStairs = 147, BrickStairs = 148, StoneBrickSlab = 149, BrickSlab = 150, CobbleWall = 151,
        GlassPane = 152, StainedGlass = 153, StainedGlassPane = 154, IronBars = 155, Carpet = 156, HayBale = 157,
        Prismarine = 158, SeaLantern = 159, EmeraldOre = 160, EmeraldBlock = 161;

    // ---- RS: redstone i transport ----
    public const byte IronDoor = 162, Trapdoor = 163, IronTrapdoor = 164, StonePressurePlate = 165, WoodPressurePlate = 166,
        LightWeightedPlate = 167, HeavyWeightedPlate = 168, WoodButton = 169, TripwireHook = 170, Tripwire = 171,
        DaylightSensor = 172, DaylightSensorInverted = 173, Comparator = 174, ComparatorOn = 175, Hopper = 176, Dropper = 177,
        Dispenser = 178, NoteBlock = 179, Rail = 180, PoweredRail = 181, DetectorRail = 182, ActivatorRail = 183,
        TrappedChest = 184, SlimeBlock = 185, PistonExtension = 186, RedstoneOreLit = 222;

    // ---- NE: Nether i End ----
    public const byte Netherrack = 187, SoulSand = 188, NetherBrick = 189, NetherBrickFence = 190, NetherBrickStairs = 191,
        NetherBrickSlab = 192, NetherWart = 193, QuartzOre = 194, QuartzBlock = 195, QuartzStairs = 196, QuartzSlab = 197,
        Portal = 198, Fire = 199, EndStone = 200, EndPortal = 201, EndPortalFrame = 202, DragonEgg = 203;

    // ---- MO: moby ----
    public const byte MonsterEgg = 204, MobSpawner = 205;

    // ---- ME: bloki użytkowe ----
    public const byte EnchantingTable = 206, Anvil = 207, BrewingStand = 208, Cauldron = 209, Beacon = 210, EnderChest = 211,
        Cake = 212, StandingSign = 213, WallSign = 214, StandingBanner = 215, WallBanner = 216, Jukebox = 217, FlowerPot = 218,
        Skull = 219, Barrier = 220, CommandBlock = 221;

    public const int Count = 223;
}
