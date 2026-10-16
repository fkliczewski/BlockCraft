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
}

public enum UseAction { None, Eat, Drink, Bow, Block }

public class Item
{
    public static readonly Item[] ById = new Item[512];
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
    public int ArmorMaterial = -1;    // 0 skóra,1 żelazo,2 złoto,3 diament
    public int CreativeTab = 3;
    public bool InCreative = true;
    public bool Handheld;             // trzymany jak narzędzie (po przekątnej)
    public bool Glint;

    public Item(int id, string key, string name)
    {
        Id = id; Key = key; Name = name;
        ById[id] = this; All.Add(this);
    }

    public bool IsBlock => BlockId >= 0;
    public bool IsFood => Hunger > 0;
    public bool IsTool => Tool != ToolType.None;
    public UseAction Action => IsFood ? UseAction.Eat : Id == I.MilkBucket ? UseAction.Drink : Id == I.Bow ? UseAction.Bow : UseAction.None;
    public int UseDuration => IsFood || Id == I.MilkBucket ? 32 : Id == I.Bow ? 72000 : 0;

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

    public ItemStack(int id, int count = 1, int damage = 0) { Id = id; Count = count; Damage = damage; }
    public Item Item => Item.ById[Id];
    public int MaxStack => Item?.MaxStack ?? 64;
    public ItemStack Copy() => new(Id, Count, Damage);
    public ItemStack CopyWith(int count) => new(Id, count, Damage);
    public bool SameItem(ItemStack o) => o != null && o.Id == Id && (Item?.MaxDamage > 0 || o.Damage == Damage);
    public bool CanStackWith(ItemStack o) => o != null && o.Id == Id && o.Damage == Damage && (Item?.MaxDamage ?? 0) == 0 && MaxStack > 1;
    public string Name => Item?.DisplayName(Damage) ?? "?";
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
