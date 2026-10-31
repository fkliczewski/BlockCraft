using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>Dane tile entity powstają w generatorze, instancja dopiero przy wydaniu chunka z cache.</summary>
public sealed class OwTilePlan
{
    public int BlockId, TypeId;
    public string EntityId;
    public ItemStack[] Slots;
    public void Apply(Chunk c, int index)
    {
        if (c.Blocks[index] != BlockId) return;
        TileEntity te = null;
        if (TypeId >= 0) te = TileEntity.Create(TypeId);
        else if (BlockId == B.Dispenser)
        {
            // RS wybiera swój TypeId z zakresu 10-29. Nie zakładamy konkretnego numeru.
            foreach (var factory in TileEntity.Types.Values)
            {
                var candidate = factory();
                if (candidate is IInventory && candidate.GetType().Name.Contains("Dispenser", StringComparison.OrdinalIgnoreCase)) { te = candidate; break; }
            }
        }
        if (te == null) return;
        te.X = c.Cx * 16 + (index & 15); te.Y = index >> 8; te.Z = c.Cz * 16 + ((index >> 4) & 15);
        if (EntityId != null) te.SetData("EntityId", EntityId);
        if (te is IInventory inv && Slots != null)
            for (int i = 0; i < Math.Min(inv.Size, Slots.Length); i++) inv.Set(i, Slots[i]?.Copy());
        c.TileEntities[index] = te;
    }
}

public static class OwLoot
{
    public readonly record struct Entry(int Id, int Min, int Max, int Weight, int Meta = 0);
    private static Entry E(int id, int min, int max, int weight, int meta = 0) => new(id, min, max, weight, meta);
    public static readonly Entry[] Dungeon =
    {
        E(I.Saddle,1,1,10), E(I.IronIngot,1,4,10), E(I.Bread,1,1,10), E(I.Wheat,1,4,10), E(I.Gunpowder,1,4,10), E(I.String,1,4,10),
        E(I.Bucket,1,1,10), E(I.GoldenApple,1,1,1), E(I.Redstone,1,4,10), E(I.Record13,1,1,4), E(I.RecordCat,1,1,4), E(I.NameTag,1,1,10),
        E(I.GoldHorseArmor,1,1,2), E(I.IronHorseArmor,1,1,5), E(I.DiamondHorseArmor,1,1,1), E(I.EnchantedBook,1,1,1)
    };
    public static readonly Entry[] DesertTemple =
    {
        E(I.Diamond,1,3,3), E(I.IronIngot,1,5,10), E(I.GoldIngot,2,7,15), E(I.Emerald,1,3,2), E(I.Bone,4,6,20), E(I.RottenFlesh,3,7,16),
        E(I.Saddle,1,1,3), E(I.IronHorseArmor,1,1,1), E(I.GoldHorseArmor,1,1,1), E(I.DiamondHorseArmor,1,1,1), E(I.EnchantedBook,1,1,1)
    };
    public static readonly Entry[] JungleTemple =
    {
        E(I.Diamond,1,3,3), E(I.IronIngot,1,5,10), E(I.GoldIngot,2,7,15), E(I.Emerald,1,3,2), E(I.Bone,4,6,20), E(I.RottenFlesh,3,7,16),
        E(I.Saddle,1,1,3), E(I.IronHorseArmor,1,1,1), E(I.GoldHorseArmor,1,1,1), E(I.DiamondHorseArmor,1,1,1), E(I.EnchantedBook,1,1,1)
    };
    public static readonly Entry[] Mineshaft =
    {
        E(I.IronIngot,1,5,10), E(I.GoldIngot,1,3,5), E(I.Redstone,4,9,5), E(I.Lapis,4,9,5), E(I.Diamond,1,2,3), E(I.Coal,3,8,10),
        E(I.Bread,1,3,15), E(I.IronPickaxe,1,1,1), E(B.Rail,4,8,1), E(I.MelonSeeds,2,4,10), E(I.PumpkinSeeds,2,4,10), E(I.Saddle,1,1,3),
        E(I.IronHorseArmor,1,1,1), E(I.EnchantedBook,1,1,1)
    };
    public static readonly Entry[] Blacksmith =
    {
        E(I.Diamond,1,3,3), E(I.IronIngot,1,5,10), E(I.GoldIngot,1,3,5), E(I.Bread,1,3,15), E(I.Apple,1,3,15), E(I.IronPickaxe,1,1,5),
        E(I.IronSword,1,1,5), E(I.IronChest,1,1,5), E(I.IronHelmet,1,1,5), E(I.IronLegs,1,1,5), E(I.IronBoots,1,1,5), E(B.Obsidian,3,7,5),
        E(B.OakSapling,3,7,5), E(I.Saddle,1,1,3), E(I.IronHorseArmor,1,1,1), E(I.GoldHorseArmor,1,1,1), E(I.DiamondHorseArmor,1,1,1)
    };
    public static readonly Entry[] Stronghold =
    {
        E(I.EnderPearl,1,1,10), E(I.Diamond,1,3,3), E(I.IronIngot,1,5,10), E(I.GoldIngot,1,3,5), E(I.Redstone,4,9,5), E(I.Bread,1,3,15),
        E(I.Apple,1,3,15), E(I.IronPickaxe,1,1,5), E(I.IronSword,1,1,5), E(I.IronChest,1,1,5), E(I.IronHelmet,1,1,5), E(I.IronLegs,1,1,5),
        E(I.IronBoots,1,1,5), E(I.GoldenApple,1,1,1), E(I.Saddle,1,1,1), E(I.IronHorseArmor,1,1,1), E(I.GoldHorseArmor,1,1,1), E(I.DiamondHorseArmor,1,1,1), E(I.EnchantedBook,1,1,1)
    };
    public static readonly Entry[] Library = { E(I.Book,1,3,20), E(I.Paper,2,7,20), E(I.EmptyMap,1,1,1), E(I.Compass,1,1,1), E(I.EnchantedBook,1,5,2) };

    public static ItemStack[] Generate(JRandom r, Entry[] entries, int rolls, int size = 27, Ench? preparedEnchantment = null, int preparedLevel = 0)
    {
        var slots = new ItemStack[size]; int weight = entries.Sum(e => e.Weight);
        // EnchantedBook.getRandom jest losowane raz przy budowie tabeli, przed wyborem łupów.
        var enchantments = Enum.GetValues<Ench>();
        bool books = entries.Any(e => e.Id == I.EnchantedBook);
        Ench enchantment = preparedEnchantment ?? (books ? enchantments[r.NextInt(enchantments.Length)] : 0);
        int enchantmentLevel = preparedEnchantment != null ? preparedLevel : books ? 1 + r.NextInt(EnchantHelper.MaxLevel(enchantment)) : 0;
        for (int i = 0; i < rolls; i++)
        {
            int pick = r.NextInt(weight); Entry e = entries[0];
            foreach (var entry in entries) { pick -= entry.Weight; if (pick < 0) { e = entry; break; } }
            int amount = e.Min + r.NextInt(e.Max - e.Min + 1);
            var stack = new ItemStack(e.Id, amount, e.Meta);
            if (e.Id == I.EnchantedBook) EnchantHelper.Set(stack, enchantment, enchantmentLevel, "StoredEnchantments");
            int maxStack = e.Id == I.EnchantedBook ? 1 : Item.ById[e.Id]?.MaxStack ?? 64;
            if (maxStack >= amount) slots[r.NextInt(size)] = stack;
            else for (int j = 0; j < amount; j++) { var single = stack.Copy(); single.Count = 1; slots[r.NextInt(size)] = single; }
        }
        return slots;
    }

    public static void Chest(OwGenRegion g, JRandom r, int x, int y, int z, Entry[] table, int rolls, int facing = 2)
    {
        g.Set(x, y, z, B.Chest, facing);
        g.Tile(x, y, z, new OwTilePlan { BlockId = B.Chest, TypeId = 2, Slots = Generate(r, table, rolls) });
    }
}
