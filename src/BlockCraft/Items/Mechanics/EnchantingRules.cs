using BlockCraft.World;

namespace BlockCraft.Items;

public readonly record struct EnchantmentRoll(Ench Id, int Level);
public sealed record EnchantOffer(int RequiredLevel, int Lapis, IReadOnlyList<EnchantmentRoll> Enchantments, EnchantmentRoll? Clue);

/// <summary>Reguły EnchantmentHelper/ContainerEnchantment z Java 1.8, niezależne od GUI.</summary>
public static partial class EnchantingRules
{
    private sealed record Rule(Ench Id, int Weight, int Start, int Step, int Spread, bool BaseMax = false);
    private static readonly Rule[] Rules =
    {
        new(Ench.Protection,10,1,11,20), new(Ench.FireProtection,5,10,8,12), new(Ench.FeatherFalling,5,5,6,10),
        new(Ench.BlastProtection,2,5,8,12), new(Ench.ProjectileProtection,5,3,6,15), new(Ench.Respiration,2,10,10,30),
        new(Ench.AquaAffinity,2,1,0,40), new(Ench.Thorns,1,10,20,50,true), new(Ench.DepthStrider,2,10,10,15),
        new(Ench.Sharpness,10,1,11,20), new(Ench.Smite,5,5,8,20), new(Ench.BaneOfArthropods,5,5,8,20),
        new(Ench.Knockback,5,5,20,50,true), new(Ench.FireAspect,2,10,20,50,true), new(Ench.Looting,2,15,9,50,true),
        new(Ench.Efficiency,10,1,10,50,true), new(Ench.SilkTouch,1,15,0,50,true), new(Ench.Unbreaking,5,5,8,50,true),
        new(Ench.Fortune,2,15,9,50,true), new(Ench.Power,10,1,10,15), new(Ench.Punch,2,12,20,25),
        new(Ench.Flame,2,20,0,30), new(Ench.Infinity,1,20,0,30),
        new(Ench.LuckOfTheSea,2,15,9,50,true), new(Ench.Lure,2,15,9,50,true)
    };

    public static int MinPower(Ench id, int level)
    { var r = Rules.First(r => r.Id == id); return r.Start + (level - 1) * r.Step; }
    public static int MaxPower(Ench id, int level)
    {
        var r = Rules.First(r => r.Id == id);
        return r.BaseMax ? 1 + level * 10 + r.Spread : MinPower(id, level) + r.Spread;
    }
    public static int Weight(Ench id) => Rules.First(r => r.Id == id).Weight;

    public static int Enchantability(ItemStack stack)
    {
        if (stack == null) return 0;
        int id = stack.Id; var item = stack.Item;
        if (id is I.Book or I.Bow or I.FishingRod) return 1;
        if (item == null) return 0;
        if (item.ArmorSlot >= 0) return item.ArmorMaterial switch { 0 => 15, 1 => 9, 2 => 25, 3 => 10, 4 => 12, _ => 0 };
        if (item.Tool is not (ToolType.Pickaxe or ToolType.Axe or ToolType.Shovel or ToolType.Sword)) return 0;
        return id switch
        {
            >= I.WoodPickaxe and <= I.WoodSword => 15,
            >= I.StonePickaxe and <= I.StoneSword => 5,
            >= I.IronPickaxe and <= I.IronSword => 14,
            >= I.GoldPickaxe and <= I.GoldSword => 22,
            >= I.DiamondPickaxe and <= I.DiamondSword => 10,
            _ => 0
        };
    }

    public static bool CanUseTable(ItemStack stack) => stack is { Count: 1 } && Enchantability(stack) > 0 && !EnchantHelper.IsEnchanted(stack);

    public static bool CanApply(Ench id, ItemStack stack, bool table = true)
    {
        if (stack == null) return false;
        if (stack.Id is I.Book or I.EnchantedBook) return true;
        var item = stack.Item; if (item == null) return false;
        bool armor = item.ArmorSlot >= 0;
        if ((int)id <= 4) return armor && (id != Ench.FeatherFalling || item.ArmorSlot == 3);
        if (id is Ench.Respiration or Ench.AquaAffinity) return armor && item.ArmorSlot == 0;
        if (id == Ench.DepthStrider) return armor && item.ArmorSlot == 3;
        if (id == Ench.Thorns) return armor && (!table || item.ArmorSlot == 1);
        if ((int)id is >= 16 and <= 21)
            return item.Tool == ToolType.Sword || !table && item.Tool == ToolType.Axe && id is Ench.Sharpness or Ench.Smite or Ench.BaneOfArthropods;
        if (id is Ench.Efficiency or Ench.SilkTouch or Ench.Fortune)
            return item.Tool is ToolType.Pickaxe or ToolType.Axe or ToolType.Shovel || !table && stack.Id == I.Shears && id != Ench.Fortune;
        if (id == Ench.Unbreaking) return item.MaxDamage > 0;
        if ((int)id is >= 48 and <= 51) return stack.Id == I.Bow;
        return id is Ench.LuckOfTheSea or Ench.Lure && stack.Id == I.FishingRod;
    }

    /// <summary>Enchantment.canApplyTogether z 1.8: owner (już na przedmiocie) ocenia kandydata. Relacja nie jest symetryczna:
    /// Grabież/Szczęście/Morska fortuna odrzucają Jedwabny dotyk, a Jedwabny dotyk odrzuca tylko Szczęście.</summary>
    public static bool Compatible(Ench owner, Ench candidate)
    {
        if (owner == candidate) return false;
        if ((int)owner is >= 0 and <= 4)
            return (int)candidate is >= 0 and <= 4 ? owner == Ench.FeatherFalling || candidate == Ench.FeatherFalling : true;
        if ((int)owner is >= 16 and <= 18) return (int)candidate is not (>= 16 and <= 18);
        if (owner == Ench.SilkTouch) return candidate != Ench.Fortune;
        if (owner is Ench.Looting or Ench.Fortune or Ench.LuckOfTheSea) return candidate != Ench.SilkTouch;
        return true;
    }

    public static int LevelCost(JRandom random, ItemStack stack, int slot, int bookshelves)
    {
        if ((uint)slot > 2) throw new ArgumentOutOfRangeException(nameof(slot));
        if (Enchantability(stack) == 0) return 0;
        int power = Math.Clamp(bookshelves, 0, 15);
        int value = random.NextInt(8) + 1 + (power >> 1) + random.NextInt(power + 1);
        return slot switch { 0 => Math.Max(value / 3, 1), 1 => value * 2 / 3 + 1, _ => Math.Max(value, power * 2) };
    }

    public static List<EnchantmentRoll> BuildList(JRandom random, ItemStack stack, int power)
    {
        int value = Enchantability(stack); var result = new List<EnchantmentRoll>();
        if (value <= 0) return result;
        value /= 2;
        value = 1 + random.NextInt((value >> 1) + 1) + random.NextInt((value >> 1) + 1);
        int boosted = value + power;
        float variance = (random.NextFloat() + random.NextFloat() - 1) * .15f;
        boosted = Math.Max(1, (int)(boosted * (1 + variance) + .5f));
        var candidates = new List<EnchantmentRoll>();
        foreach (var rule in Rules)
        {
            if (!CanApply(rule.Id, stack)) continue;
            for (int level = EnchantHelper.MaxLevel(rule.Id); level >= 1; level--)
                if (boosted >= MinPower(rule.Id, level) && boosted <= MaxPower(rule.Id, level))
                { candidates.Add(new(rule.Id, level)); break; }
        }
        // Java 8 HashMap<Integer,...> (domyślna pojemność, kubełki w kolejności indeksów).
        int capacity = 16; while (candidates.Count > capacity * 3 / 4) capacity *= 2;
        candidates = candidates.OrderBy(c => (int)c.Id & (capacity - 1)).ThenBy(c => (int)c.Id).ToList();
        if (candidates.Count == 0) return result;
        EnchantmentRoll Pick()
        {
            int n = random.NextInt(candidates.Sum(c => Weight(c.Id)));
            foreach (var c in candidates) { n -= Weight(c.Id); if (n < 0) return c; }
            throw new InvalidOperationException();
        }
        result.Add(Pick());
        for (int remaining = boosted; random.NextInt(50) <= remaining; remaining >>= 1)
        {
            candidates.RemoveAll(c => result.Any(e => !Compatible(e.Id, c.Id)));
            if (candidates.Count > 0) result.Add(Pick());
        }
        return result;
    }

    public static EnchantOffer[] Offers(ItemStack stack, int xpSeed, int bookshelves)
    {
        var random = new JRandom(xpSeed); var costs = new int[3];
        if (CanUseTable(stack)) for (int i = 0; i < 3; i++)
        { costs[i] = LevelCost(random, stack, i, bookshelves); if (costs[i] < i + 1) costs[i] = 0; }
        var offers = new EnchantOffer[3];
        for (int i = 0; i < 3; i++)
        {
            random.SetSeed(unchecked(xpSeed + i));
            var list = costs[i] > 0 ? BuildList(random, stack, costs[i]) : new List<EnchantmentRoll>();
            if (stack?.Id == I.Book && list.Count > 1) list.RemoveAt(random.NextInt(list.Count));
            EnchantmentRoll? clue = list.Count > 0 ? list[random.NextInt(list.Count)] : null;
            offers[i] = new(costs[i], i + 1, list.AsReadOnly(), clue);
        }
        return offers;
    }

    public static int BookshelfPower(World.World world, int x, int y, int z)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
        {
            if (dx == 0 && dz == 0 || world.GetBlock(x + dx, y, z + dz) != B.Air || world.GetBlock(x + dx, y + 1, z + dz) != B.Air) continue;
            void Shelf(int sx, int sz) { for (int dy = 0; dy <= 1; dy++) if (world.GetBlock(x + sx, y + dy, z + sz) == B.Bookshelf) count++; }
            Shelf(dx * 2, dz * 2);
            if (dx != 0 && dz != 0) { Shelf(dx * 2, dz); Shelf(dx, dz * 2); }
        }
        return Math.Min(15, count);
    }
}
