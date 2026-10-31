using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

public static partial class ItemRegistry
{
    private static bool _init;
    public static readonly string[] MaterialNames = { "Drewniany", "Kamienny", "Żelazny", "Złoty", "Diamentowy" };

    private static Item Simple(int id, string key, string name, int tab, int stack = 64)
    {
        var it = new Item(id, key, name) { CreativeTab = tab, MaxStack = stack, Sprite = TextureGen.Layer("item_" + key) };
        return it;
    }

    private static Item BlockItem(int id, string key, string name, int block, int tab)
    {
        var it = new Item(id, key, name) { CreativeTab = tab, BlockId = block, Sprite = TextureGen.Layer("item_" + key) };
        return it;
    }

    public static void Init()
    {
        if (_init) return; _init = true;
        Blocks.Init();
        // przedmioty-bloki
        for (int i = 1; i < 256; i++)
        {
            var b = Block.ById[i];
            if (b == null || b.Id == 0 && i != 0) continue;
            if (b.IsLiquid) continue;
            var it = new Item(i, b.Key, b.Name) { BlockId = i, CreativeTab = b.CreativeTab, InCreative = b.InCreative };
            if (b.Render is RenderType.Cross or RenderType.Torch or RenderType.Crops || b.Id is B.Ladder or B.Lever)
                it.Sprite = b.Tex[0];
            if (b.Id == B.RedstoneWire || b.Id == B.Wheat || b.Id == B.SugarCane || b.Id == B.Bed || b.Id == B.OakDoor || b.Id == B.Repeater || b.Id == B.RepeaterOn) it.InCreative = false;
        }

        const int Mats = 7, Tools = 5, Combat = 6, Food = 4, Misc = 3, Red = 2, Deco = 1;
        Simple(I.Stick, "stick", "Patyk", Mats).Handheld = true;
        Simple(I.Coal, "coal", "Węgiel", Mats);
        Simple(I.Charcoal, "charcoal", "Węgiel drzewny", Mats);
        Simple(I.IronIngot, "iron_ingot", "Sztabka żelaza", Mats);
        Simple(I.GoldIngot, "gold_ingot", "Sztabka złota", Mats);
        Simple(I.Diamond, "diamond", "Diament", Mats);
        Simple(I.Redstone, "redstone", "Czerwony proszek", Red).BlockId = B.RedstoneWire;
        Simple(I.Lapis, "lapis_lazuli", "Lazuryt", Mats);
        Simple(I.Flint, "flint", "Krzemień", Mats);

        string[] toolKeys = { "pickaxe", "axe", "shovel", "sword", "hoe" };
        string[] toolNames = { "kilof", "siekiera", "łopata", "miecz", "motyka" };
        string[] matKeys = { "wooden", "stone", "iron", "golden", "diamond" };
        int[] matLevel = { 0, 1, 2, 0, 3 };
        float[] matSpeed = { 2, 4, 6, 12, 8 };
        int[] matDur = { 59, 131, 250, 32, 1561 };
        float[] matDmg = { 0, 1, 2, 0, 3 };
        int[] baseIds = { I.WoodPickaxe, I.StonePickaxe, I.IronPickaxe, I.GoldPickaxe, I.DiamondPickaxe };
        ToolType[] types = { ToolType.Pickaxe, ToolType.Axe, ToolType.Shovel, ToolType.Sword, ToolType.Hoe };
        float[] typeDmg = { 2, 3, 1, 4, 0 };
        for (int m = 0; m < 5; m++)
            for (int t = 0; t < 5; t++)
            {
                string name = toolNames[t];
                string adj = MaterialNames[m];
                // dopasowanie rodzaju gramatycznego
                string fullName = t switch
                {
                    0 or 3 => adj + " " + name,                           // kilof, miecz (m)
                    _ => adj[..^1] + "a " + name                           // siekiera, łopata, motyka (ż)
                };
                if (t is 1 or 2 or 4) fullName = AdjF(adj) + " " + name;
                var it = Simple(baseIds[m] + t, matKeys[m] + "_" + toolKeys[t], fullName, t == 3 ? Combat : Tools, 1);
                it.Tool = types[t]; it.ToolLevel = matLevel[m]; it.ToolSpeed = t == 3 || t == 4 ? 1 : matSpeed[m];
                it.MaxDamage = matDur[m]; it.Handheld = true;
                it.AttackDamage = t == 4 ? 1 : typeDmg[t] + matDmg[m] + 1;
            }
        var shears = Simple(I.Shears, "shears", "Nożyce", Tools, 1); shears.Tool = ToolType.Shears; shears.MaxDamage = 238; shears.ToolSpeed = 1;
        Simple(I.Bucket, "bucket", "Wiadro", Misc, 16);
        Simple(I.WaterBucket, "water_bucket", "Wiadro wody", Misc, 1);
        Simple(I.LavaBucket, "lava_bucket", "Wiadro lawy", Misc, 1);
        Simple(I.MilkBucket, "milk_bucket", "Wiadro mleka", Misc, 1);
        var fs = Simple(I.FlintAndSteel, "flint_and_steel", "Krzesiwo", Tools, 1); fs.MaxDamage = 64;
        var bow = Simple(I.Bow, "bow", "Łuk", Combat, 1); bow.MaxDamage = 384; bow.Handheld = true;
        Simple(I.Arrow, "arrow", "Strzała", Combat);

        void FoodItem(int id, string key, string name, int hunger, float sat, int stack = 64)
        { var f = Simple(id, key, name, Food, stack); f.Hunger = hunger; f.SaturationMod = sat; }
        FoodItem(I.Apple, "apple", "Jabłko", 4, 0.3f);
        FoodItem(I.Bread, "bread", "Chleb", 5, 0.6f);
        FoodItem(I.RawPorkchop, "porkchop", "Surowy schab", 3, 0.3f);
        FoodItem(I.CookedPorkchop, "cooked_porkchop", "Pieczony schab", 8, 0.8f);
        FoodItem(I.RawBeef, "beef", "Surowa wołowina", 3, 0.3f);
        FoodItem(I.Steak, "cooked_beef", "Stek", 8, 0.8f);
        FoodItem(I.RawChicken, "chicken", "Surowy kurczak", 2, 0.3f);
        FoodItem(I.CookedChicken, "cooked_chicken", "Pieczony kurczak", 6, 0.6f);
        FoodItem(I.RawMutton, "mutton", "Surowa baranina", 2, 0.3f);
        FoodItem(I.CookedMutton, "cooked_mutton", "Pieczona baranina", 6, 0.8f);
        FoodItem(I.RottenFlesh, "rotten_flesh", "Zgniłe mięso", 4, 0.1f);
        FoodItem(I.GoldenApple, "golden_apple", "Złote jabłko", 4, 1.2f);
        ById(I.GoldenApple).Glint = false;
        FoodItem(I.Cookie, "cookie", "Ciastko", 2, 0.1f);
        FoodItem(I.MushroomStew, "mushroom_stew", "Zupa grzybowa", 6, 0.6f, 1);
        ById(I.MushroomStew).InCreative = false;
        Simple(I.Bowl, "bowl", "Miska", Mats).InCreative = false;

        Simple(I.Wheat, "wheat", "Pszenica", Mats);
        Simple(I.Seeds, "wheat_seeds", "Nasiona", Mats);
        Simple(I.Bone, "bone", "Kość", Mats).Handheld = true;
        Simple(I.BoneMeal, "bone_meal", "Mączka kostna", Mats);
        Simple(I.String, "string", "Nić", Mats);
        Simple(I.Feather, "feather", "Pióro", Mats);
        Simple(I.Gunpowder, "gunpowder", "Proch", Mats);
        Simple(I.Leather, "leather", "Skóra", Mats);
        Simple(I.Egg, "egg", "Jajko", Mats, 16);
        Simple(I.SpiderEye, "spider_eye", "Oko pająka", Mats);
        Simple(I.EnderPearl, "ender_pearl", "Perła Kresu", Misc, 16);
        Simple(I.SugarCane, "reeds", "Trzcina cukrowa", Mats).BlockId = B.SugarCane;
        Simple(I.Paper, "paper", "Papier", Mats);
        Simple(I.ClayBall, "clay_ball", "Kulka gliny", Mats);
        Simple(I.Brick, "brick", "Cegła", Mats);
        Simple(I.Book, "book", "Książka", Mats);
        Simple(I.Sugar, "sugar", "Cukier", Mats);
        Simple(I.GoldNugget, "gold_nugget", "Samorodek złota", Mats);

        string[] armorKeys = { "helmet", "chestplate", "leggings", "boots" };
        string[] armorNames = { "hełm", "napierśnik", "spodnie", "buty" };
        string[] amKeys = { "leather", "iron", "golden", "diamond" };
        string[] amNames = { "Skórzany", "Żelazny", "Złoty", "Diamentowy" };
        int[][] points = { new[] { 1, 3, 2, 1 }, new[] { 2, 6, 5, 2 }, new[] { 2, 5, 3, 1 }, new[] { 3, 8, 6, 3 } };
        int[] mult = { 5, 15, 7, 33 };
        int[] baseDur = { 11, 16, 15, 13 };
        int[] firstId = { I.LeatherHelmet, I.IronHelmet, I.GoldHelmet, I.DiamondHelmet };
        for (int m = 0; m < 4; m++)
            for (int s = 0; s < 4; s++)
            {
                string adj = amNames[m];
                string nm = s switch { 2 or 3 => AdjPl(adj) + " " + armorNames[s], _ => adj + " " + armorNames[s] };
                var a = Simple(firstId[m] + s, amKeys[m] + "_" + armorKeys[s], nm, Combat, 1);
                a.ArmorSlot = s; a.ArmorPoints = points[m][s]; a.ArmorMaterial = m; a.MaxDamage = baseDur[s] * mult[m];
            }

        var door = Simple(I.Door, "wooden_door", "Drewniane drzwi", Red, 64); door.BlockId = B.OakDoor;
        var bed = Simple(I.Bed, "bed", "Łóżko", Deco, 1); bed.BlockId = B.Bed;
        var rep = Simple(I.RepeaterItem, "repeater", "Przekaźnik", Red); rep.BlockId = B.Repeater;

        ModuleItems();
    }

    public static Item ById(int id) => Item.ById[id];

    private static string AdjF(string adj) => adj switch
    {
        "Drewniany" => "Drewniana", "Kamienny" => "Kamienna", "Żelazny" => "Żelazna", "Złoty" => "Złota", "Diamentowy" => "Diamentowa", _ => adj
    };
    private static string AdjPl(string adj) => adj switch
    {
        "Skórzany" => "Skórzane", "Żelazny" => "Żelazne", "Złoty" => "Złote", "Diamentowy" => "Diamentowe", _ => adj
    };

    /// <summary>Wszystkie przedmioty dla zakładki kreatywnej.</summary>
    public static List<ItemStack> CreativeItems(int tab)
    {
        var list = new List<ItemStack>();
        foreach (var it in Item.All)
        {
            if (!it.InCreative) continue;
            if (tab >= 0 && it.CreativeTab != tab) continue;
            foreach (int m in it.GetVariants()) list.Add(new ItemStack(it.Id, 1, m));
        }
        return list;
    }
}
