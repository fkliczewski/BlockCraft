using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

public static class I
{
    public const int Stick = 256, Coal = 257, Charcoal = 258, IronIngot = 259, GoldIngot = 260, Diamond = 261, Redstone = 262, Lapis = 263, Flint = 264,
        WoodPickaxe = 270, WoodAxe = 271, WoodShovel = 272, WoodSword = 273, WoodHoe = 274,
        StonePickaxe = 275, StoneAxe = 276, StoneShovel = 277, StoneSword = 278, StoneHoe = 279,
        IronPickaxe = 280, IronAxe = 281, IronShovel = 282, IronSword = 283, IronHoe = 284,
        GoldPickaxe = 285, GoldAxe = 286, GoldShovel = 287, GoldSword = 288, GoldHoe = 289,
        DiamondPickaxe = 290, DiamondAxe = 291, DiamondShovel = 292, DiamondSword = 293, DiamondHoe = 294,
        Shears = 295, Bucket = 296, WaterBucket = 297, LavaBucket = 298, MilkBucket = 299, FlintAndSteel = 300, Bow = 301, Arrow = 302,
        Apple = 310, Bread = 311, RawPorkchop = 312, CookedPorkchop = 313, RawBeef = 314, Steak = 315, RawChicken = 316, CookedChicken = 317,
        RawMutton = 318, CookedMutton = 319, RottenFlesh = 320, GoldenApple = 321, Cookie = 322,
        Wheat = 330, Seeds = 331, Bone = 332, BoneMeal = 333, String = 334, Feather = 335, Gunpowder = 336, Leather = 337, Egg = 338,
        SpiderEye = 339, EnderPearl = 340, SugarCane = 341, Paper = 342, ClayBall = 343, Brick = 344, Book = 345, Sugar = 346, GoldNugget = 347,
        LeatherHelmet = 350, LeatherChest = 351, LeatherLegs = 352, LeatherBoots = 353,
        IronHelmet = 354, IronChest = 355, IronLegs = 356, IronBoots = 357,
        GoldHelmet = 358, GoldChest = 359, GoldLegs = 360, GoldBoots = 361,
        DiamondHelmet = 362, DiamondChest = 363, DiamondLegs = 364, DiamondBoots = 365,
        Door = 370, Bed = 371, RepeaterItem = 372, Bowl = 373, MushroomStew = 374;

    // ---- OW: overworld (rośliny, uprawy, drzwi z nowych drewien, barwniki) ----
    // Dye: meta jak w MC (0 worek z atramentem, 1 czerwony, 2 zielony, 3 ziarna kakaowe, 5 fioletowy, ... 14 pomarańczowy);
    // meta 4 i 15 to istniejące Lapis i BoneMeal - używaj Dyes.ColorOf(stack) do sprawdzania koloru.
    public const int Emerald = 400, Snowball = 401, Carrot = 402, Potato = 403, BakedPotato = 404, PoisonousPotato = 405,
        MelonSlice = 406, PumpkinSeeds = 407, MelonSeeds = 408, PumpkinPie = 409, SpruceDoorItem = 411, BirchDoorItem = 412,
        JungleDoorItem = 413, AcaciaDoorItem = 414, DarkOakDoorItem = 415, Dye = 416;

    // ---- MO: moby ----
    public const int CarrotOnAStick = 424, DiamondHorseArmor = 425, NameTag = 426, Saddle = 427, IronHorseArmor = 428, GoldHorseArmor = 429,
        PrismarineShard = 430, PrismarineCrystals = 431, Slimeball = 432, RawRabbit = 433, CookedRabbit = 434, RabbitStew = 435,
        RabbitHide = 436, RabbitFoot = 437, SpawnEgg = 438, Lead = 439;

    // ---- RS: redstone i transport ----
    public const int IronDoorItem = 440, ComparatorItem = 441, Minecart = 442, ChestMinecart = 443, FurnaceMinecart = 444,
        TntMinecart = 445, HopperMinecart = 446;

    // ---- NE: Nether i End ----
    public const int NetherWartItem = 450, Quartz = 451, NetherBrickItem = 452, GlowstoneDust = 453, BlazeRod = 454, BlazePowder = 455,
        GhastTear = 456, MagmaCream = 457, EyeOfEnder = 458, FireCharge = 459, NetherStar = 460;

    // ---- ME: mechaniki itemów ----
    public const int FishingRod = 470, RawFish = 471, CookedFish = 472, Compass = 473, Clock = 474, EmptyMap = 475, FilledMap = 476,
        GlassBottle = 477, Potion = 478, ExpBottle = 479, FermentedSpiderEye = 480, GlisteringMelon = 481, GoldenCarrot = 482,
        BrewingStandItem = 483, CauldronItem = 484, EnchantedBook = 485, WritableBook = 486, WrittenBook = 487, SignItem = 488,
        Painting = 489, ItemFrame = 490, FlowerPotItem = 491, SkullItem = 492, ArmorStand = 493, Banner = 494, Boat = 495,
        FireworkRocket = 496, FireworkStar = 497, CakeItem = 498, ChainHelmet = 499, ChainChest = 500, ChainLegs = 501, ChainBoots = 502,
        Record13 = 503, RecordCat = 504, RecordBlocks = 505, RecordChirp = 506, RecordFar = 507, RecordMall = 508, RecordMellohi = 509,
        RecordStal = 510, RecordStrad = 511, RecordWard = 512, Record11 = 513, RecordWait = 514;
}

public enum UseAction { None, Eat, Drink, Bow, Block }

public class Item
{
    public static readonly Item[] ById = new Item[1024];
    public static readonly List<Item> All = new();

    public int Id;
    public string Key;
    public string Name;
    public int MaxStack = 64;
    public int MaxDamage;
    public int Sprite = -1;           // warstwa tekstury 16x16 (dla itemów nie-blokowych)
    public int BlockId = -1;          // blok stawiany
    public ToolType Tool = ToolType.None;
    public int ToolLevel = -1;
    public float ToolSpeed = 1f;
    public float AttackDamage = 1f;
    public int Hunger; public float SaturationMod;
    public int ArmorSlot = -1;        // 0=hełm,1=napierśnik,2=spodnie,3=buty
    public int ArmorPoints;
    public int ArmorMaterial = -1;    // 0 skóra,1 żelazo,2 złoto,3 diament,4 kolczuga
    public int CreativeTab = 3;
    public bool InCreative = true;
    public bool Handheld;             // trzymany jak narzędzie (po przekątnej)
    public bool Glint;
    /// <summary>Warianty meta pokazywane w kreatywnym (null = tylko 0). Dla bloków brane z Block.Variants.</summary>
    public int[] Variants;
    /// <summary>Sprite zależny od stacka (np. mikstury, barwniki). Null = Sprite.</summary>
    public Func<ItemStack, int> SpriteFn;
    /// <summary>Kolor (tint) warstwy 0 zależny od stacka (np. skórzana zbroja). Null = biały.</summary>
    public Func<ItemStack, uint> ColorFn;
    /// <summary>Druga warstwa (np. płyn mikstury, kropki jajka spawnu): (warstwa tekstury, kolor ARGB).</summary>
    public Func<ItemStack, (int layer, uint color)> OverlayFn;
    /// <summary>Własna nazwa zależna od stacka (np. mikstury, zaklęta książka).</summary>
    public Func<ItemStack, string> NameFn;
    /// <summary>Własne użycie PPM na bloku: (gracz, stack, x, y, z, ściana, hx, hy, hz). True = obsłużone, domyślna logika się nie wykona.</summary>
    public Func<Player, ItemStack, int, int, int, int, double, double, double, bool> UseOnBlock;
    /// <summary>Własne użycie PPM w powietrze (też gdy UseOnBlock nie obsłużył). True = obsłużone.</summary>
    public Func<Player, ItemStack, bool> UseInAir;
    /// <summary>Wołane po zjedzeniu (efekty jedzenia modułów, np. trujący ziemniak).</summary>
    public Action<Player> OnEaten;

    public Item(int id, string key, string name)
    {
        Id = id; Key = key; Name = name;
        ById[id] = this; All.Add(this);
    }

    public bool IsBlock => BlockId >= 0;
    public bool IsFood => Hunger > 0;
    public bool IsTool => Tool != ToolType.None;
    public UseAction Action => IsFood ? UseAction.Eat : Id is I.MilkBucket or I.Potion ? UseAction.Drink : Id == I.Bow ? UseAction.Bow : UseAction.None;
    public int UseDuration => IsFood || Id is I.MilkBucket or I.Potion ? 32 : Id == I.Bow ? 72000 : 0;

    public int GetSprite(ItemStack s)
    {
        if (SpriteFn != null) return SpriteFn(s);
        if (Sprite > 0) return Sprite;
        return IsBlock ? Block.Get(BlockId).GetTexture(0, s?.Damage ?? 0) : 0;
    }
    public uint GetColor(ItemStack s, int pass)
    {
        if (ColorFn != null) return ColorFn(s);
        var b = IsBlock ? Block.Get(BlockId) : null;
        return b != null && b.Tint is TintType.Grass or TintType.Foliage ? 0xFF5AC079 : 0xFFFFFFFF;
    }
    public int[] GetVariants() => Variants ?? (IsBlock ? Block.Get(BlockId).Variants : null) ?? new[] { 0 };

    public string DisplayName(int meta) => IsBlock && Block.ById[BlockId] != null ? Block.ById[BlockId].DisplayName(meta) : Name;

    public static Item Get(string key)
    {
        key = key.ToLowerInvariant();
        if (key.StartsWith("minecraft:")) key = key[10..];
        foreach (var it in All) if (it.Key == key) return it;
        if (int.TryParse(key, out int id) && id >= 0 && id < ById.Length) return ById[id];
        return null;
    }
}

public sealed class ItemStack
{
    public int Id;
    public int Count;
    public int Damage;

    /// <summary>Dodatkowe dane jak NBT w MC (enchanty, nazwa, mikstury...). Null = brak.</summary>
    public Nbt Tag;

    public ItemStack(int id, int count = 1, int damage = 0) { Id = id; Count = count; Damage = damage; }
    public Item Item => Item.ById[Id];
    public int MaxStack => Item?.MaxStack ?? 64;
    public ItemStack Copy() => new(Id, Count, Damage) { Tag = Tag?.Copy() };
    public ItemStack CopyWith(int count) => new(Id, count, Damage) { Tag = Tag?.Copy() };
    public bool SameItem(ItemStack o) => o != null && o.Id == Id && (Item?.MaxDamage > 0 || o.Damage == Damage);
    public bool CanStackWith(ItemStack o) => o != null && o.Id == Id && o.Damage == Damage && (Item?.MaxDamage ?? 0) == 0 && MaxStack > 1 && Nbt.Same(Tag, o.Tag);
    public Nbt GetOrCreateTag() => Tag ??= new Nbt();
    /// <summary>Własna nazwa (kowadło, plakietka) z Tag display/Name.</summary>
    public string CustomName => Tag?.GetCompound("display")?.GetString("Name");
    public string Name => CustomName ?? Item?.NameFn?.Invoke(this) ?? Item?.DisplayName(Damage) ?? "?";
    public override string ToString() => $"{Count}x {Name}";

    /// <summary>Zwraca true gdy przedmiot się zepsuł.</summary>
    public bool DamageItem(int amount)
    {
        var it = Item;
        if (it == null || it.MaxDamage <= 0) return false;
        Damage += amount;
        if (Damage >= it.MaxDamage) { Count--; Damage = 0; return true; }
        return false;
    }
}
