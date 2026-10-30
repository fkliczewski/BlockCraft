using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public static partial class Registries
{
    static partial void RegisterVillagers18() { Mob("villager",w=>new Villager(w)); }
}

/// <summary>MerchantRecipe z 1.8: do dwóch przedmiotów za jeden, 7 użyć na start.</summary>
public sealed class MerchantRecipe
{
    public ItemStack Buy1, Buy2, Sell;
    public int Uses, MaxUses = 7;
    public bool RewardsExp = true;
    public MerchantRecipe(ItemStack buy1, ItemStack buy2, ItemStack sell) { Buy1 = buy1; Buy2 = buy2; Sell = sell; }
    public bool Disabled => Uses >= MaxUses;
    public void Save(BinaryWriter b) { b.WriteStack(Buy1); b.WriteStack(Buy2); b.WriteStack(Sell); b.Write(Uses); b.Write(MaxUses); b.Write(RewardsExp); }
    public static MerchantRecipe Load(BinaryReader b) => new(b.ReadStack(), b.ReadStack(), b.ReadStack()) { Uses = b.ReadInt32(), MaxUses = b.ReadInt32(), RewardsExp = b.ReadBoolean() };
}

/// <summary>Kariery i oferty jak EntityVillager.DEFAULT_TRADE_LIST_MAP w 1.8.</summary>
public static class VillagerTrades
{
    public delegate void Trade(Rng r, List<MerchantRecipe> list);
    public sealed record Career(string Name, Trade[][] Levels);

    private static int Price(Rng r, int a, int b) => a >= b ? a : a + r.Next(b - a + 1);
    /// <summary>EmeraldForItems: n przedmiotów za 1 szmaragd.</summary>
    private static Trade Emerald(int id, int a, int b, int meta = 0) => (r, l) => l.Add(new(new ItemStack(id, Price(r, a, b), meta), null, new ItemStack(I.Emerald)));
    /// <summary>ListItemForEmeralds: cena ujemna = tyle sztuk za 1 szmaragd.</summary>
    private static Trade Item(int id, int a, int b, int meta = 0) => (r, l) =>
    {
        int p = Price(r, a, b);
        l.Add(p < 0 ? new(new ItemStack(I.Emerald), null, new ItemStack(id, -p, meta)) : new(new ItemStack(I.Emerald, p), null, new ItemStack(id, 1, meta)));
    };
    /// <summary>ItemAndEmeraldToItem.</summary>
    private static Trade Swap(int id, int n, int to, int a, int b) => (r, l) => l.Add(new(new ItemStack(id, n), new ItemStack(I.Emerald), new ItemStack(to, Price(r, a, b))));
    /// <summary>ListEnchantedItemForEmeralds: zaklęcie na poziomie 5-19.</summary>
    private static Trade Enchanted(int id, int a, int b) => (r, l) =>
    {
        var s = new ItemStack(id); RandomEnchant(r, s, 5 + r.Next(15));
        l.Add(new(new ItemStack(I.Emerald, Price(r, a, b)), null, s));
    };
    /// <summary>ListEnchantedBookForEmeralds: cena 2..5+10*poziom + 3*poziom.</summary>
    private static void Book(Rng r, List<MerchantRecipe> l)
    {
        var all = Enum.GetValues<Ench>();
        var e = all[r.Next(all.Length)];
        int lvl = 1 + r.Next(EnchantHelper.MaxLevel(e));
        var book = new ItemStack(I.EnchantedBook); EnchantHelper.Set(book, e, lvl, "StoredEnchantments");
        int cost = Math.Min(64, 2 + r.Next(5 + lvl * 10) + 3 * lvl);
        l.Add(new(new ItemStack(I.Book), new ItemStack(I.Emerald, cost), book));
    }

    /// <summary>Uproszczone EnchantmentHelper.addRandomEnchantment: zaklęcia pasujące do przedmiotu, poziom z mocy.</summary>
    public static void RandomEnchant(Rng r, ItemStack s, int power)
    {
        var it = s.Item; if (it == null) return;
        List<Ench> pool = new() { Ench.Unbreaking };
        if (it.ArmorSlot >= 0)
        {
            pool.AddRange(new[] { Ench.Protection, Ench.FireProtection, Ench.BlastProtection, Ench.ProjectileProtection });
            if (it.ArmorSlot == 0) pool.AddRange(new[] { Ench.Respiration, Ench.AquaAffinity });
            if (it.ArmorSlot == 1) pool.Add(Ench.Thorns);
            if (it.ArmorSlot == 3) pool.AddRange(new[] { Ench.FeatherFalling, Ench.DepthStrider });
        }
        else if (it.Tool == ToolType.Sword) pool.AddRange(new[] { Ench.Sharpness, Ench.Smite, Ench.BaneOfArthropods, Ench.Knockback, Ench.FireAspect, Ench.Looting });
        else if (it.Id == I.FishingRod) pool.AddRange(new[] { Ench.LuckOfTheSea, Ench.Lure });
        else if (it.Id == I.Bow) pool.AddRange(new[] { Ench.Power, Ench.Punch, Ench.Flame, Ench.Infinity });
        else pool.AddRange(new[] { Ench.Efficiency, Ench.SilkTouch, Ench.Fortune });
        if (it.Tool == ToolType.Axe) pool.AddRange(new[] { Ench.Sharpness, Ench.Smite, Ench.BaneOfArthropods });
        int count = 1; for (int p = power; r.Next(50) <= p && count < 3; p /= 2) count++;
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            var e = pool[r.Next(pool.Count)]; pool.Remove(e);
            if (e is Ench.SilkTouch) pool.Remove(Ench.Fortune); if (e is Ench.Fortune) pool.Remove(Ench.SilkTouch);
            if (e is Ench.Sharpness or Ench.Smite or Ench.BaneOfArthropods) pool.RemoveAll(x => x is Ench.Sharpness or Ench.Smite or Ench.BaneOfArthropods);
            if (e is Ench.Protection or Ench.FireProtection or Ench.BlastProtection or Ench.ProjectileProtection) pool.RemoveAll(x => x is Ench.Protection or Ench.FireProtection or Ench.BlastProtection or Ench.ProjectileProtection);
            int max = EnchantHelper.MaxLevel(e);
            EnchantHelper.Set(s, e, Math.Clamp(1 + power * max / 30 + r.Next(2) - (i > 0 ? 1 : 0), 1, max));
        }
    }

    private static Trade[] L(params Trade[] t) => t;
    private static Trade[] Wools()
    {
        var t = new Trade[16];
        for (int c = 0; c < 16; c++) t[c] = Item(B.Wool, 1, 2, c);
        return t;
    }

    /// <summary>[zawód][kariera] - kolejność i ceny z 1.8.</summary>
    public static readonly Career[][] Professions =
    {
        new[]
        {
            new Career("Rolnik", new[] {
                L(Emerald(I.Wheat,18,22), Emerald(I.Potato,15,19), Emerald(I.Carrot,15,19), Item(I.Bread,-4,-2)),
                L(Emerald(B.Pumpkin,8,13), Item(I.PumpkinPie,-3,-2)),
                L(Emerald(B.Melon,7,12), Item(I.Apple,-5,-7)),
                L(Item(I.Cookie,-6,-10), Item(I.CakeItem,1,1)) }),
            new Career("Rybak", new[] {
                L(Emerald(I.String,15,20), Emerald(I.Coal,16,24), Swap(I.RawFish,6,I.CookedFish,6,6)),
                L(Enchanted(I.FishingRod,7,8)) }),
            new Career("Pasterz", new[] {
                L(Emerald(B.Wool,16,22), Item(I.Shears,3,4)),
                Wools() }),
            new Career("Łuczarz", new[] {
                L(Emerald(I.String,15,20), Item(I.Arrow,-12,-8)),
                L(Item(I.Bow,2,3), Swap(B.Gravel,10,I.Flint,6,10)) }),
        },
        new[]
        {
            new Career("Bibliotekarz", new[] {
                L(Emerald(I.Paper,24,36), Book),
                L(Emerald(I.Book,8,10), Item(I.Compass,10,12), Item(B.Bookshelf,3,4)),
                L(Emerald(I.WrittenBook,2,2), Item(I.Clock,10,12), Item(B.Glass,-5,-3)),
                L(Book), L(Book),
                L(Item(I.NameTag,20,22)) }),
        },
        new[]
        {
            new Career("Kapłan", new[] {
                L(Emerald(I.RottenFlesh,36,40), Emerald(I.GoldIngot,8,10)),
                L(Item(I.Redstone,-4,-1), Item(I.Lapis,-2,-1)),
                L(Item(I.EyeOfEnder,7,11), Item(B.Glowstone,-3,-1)),
                L(Item(I.ExpBottle,3,11)) }),
        },
        new[]
        {
            new Career("Płatnerz", new[] {
                L(Emerald(I.Coal,16,24), Item(I.IronHelmet,4,6)),
                L(Emerald(I.IronIngot,7,9), Item(I.IronChest,10,14)),
                L(Emerald(I.Diamond,3,4), Enchanted(I.DiamondChest,16,19)),
                L(Item(I.ChainBoots,5,7), Item(I.ChainLegs,9,11), Item(I.ChainHelmet,5,7), Item(I.ChainChest,11,15)) }),
            new Career("Zbrojmistrz", new[] {
                L(Emerald(I.Coal,16,24), Item(I.IronAxe,6,8)),
                L(Emerald(I.IronIngot,7,9), Enchanted(I.IronSword,9,10)),
                L(Emerald(I.Diamond,3,4), Enchanted(I.DiamondSword,12,15), Enchanted(I.DiamondAxe,9,12)) }),
            new Career("Narzędziowiec", new[] {
                L(Emerald(I.Coal,16,24), Enchanted(I.IronShovel,5,7)),
                L(Emerald(I.IronIngot,7,9), Enchanted(I.IronPickaxe,9,11)),
                L(Emerald(I.Diamond,3,4), Enchanted(I.DiamondPickaxe,12,15)) }),
        },
        new[]
        {
            new Career("Rzeźnik", new[] {
                L(Emerald(I.RawPorkchop,14,18), Emerald(I.RawChicken,14,18)),
                L(Emerald(I.Coal,16,24), Item(I.CookedPorkchop,-7,-5), Item(I.CookedChicken,-8,-6)) }),
            new Career("Kaletnik", new[] {
                L(Emerald(I.Leather,9,12), Item(I.LeatherLegs,2,4)),
                L(Enchanted(I.LeatherChest,7,12)),
                L(Item(I.Saddle,8,10)) }),
        },
    };
    public static readonly string[] ProfessionNames = { "Rolnik", "Bibliotekarz", "Kapłan", "Kowal", "Rzeźnik" };
}

/// <summary>EntityVillager z 1.8: zawody, kariery, handel, rozmnażanie wg drzwi wioski, ucieczka przed zombie.</summary>
public sealed class Villager : Animal
{
    public int Profession, Career, CareerLevel;
    public readonly List<MerchantRecipe> Recipes = new();
    public readonly SimpleInventory Food = new(8);
    public Player Customer;
    public bool WillingToMate;
    private int _restock = -1, _headShake, _pickup;
    private bool _needsRefresh;
    private static readonly Dictionary<long, (int tick, int doors)> DoorCache = new();

    public Villager(World.World w) : this(w, w.Rand.Next(5)) { }
    public Villager(World.World w, int profession) : base(w) { Width = .6f; Height = 1.8f; MaxHealth = Health = 20; MoveSpeed = .3; Profession = Math.Clamp(profession, 0, 4); }
    public override string TypeKey => "villager";
    public override string DisplayName => Career > 0 ? VillagerTrades.Professions[Profession][Career - 1].Name : VillagerTrades.ProfessionNames[Profession];
    public override bool Persistent => true;
    public override bool CanOpenDoors => true;
    public override bool CanBeLeashed => false;
    public override string LivingSound => Customer != null ? "mob.villager.haggle" : "mob.villager.idle";
    public override string HurtSound => "mob.villager.hit";
    public override string DeathSound => "mob.villager.death";
    public override float EyeHeight => IsBaby ? Height * .85f * .5f : 1.62f;
    public bool HeadShaking => _headShake > 0;
    public override bool IsBreedingItem(ItemStack s) => false;
    public override bool CanMateWith(Animal a) => a is Villager v && v != this && v.WillingToMate && WillingToMate;
    public override Animal CreateChild() => new Villager(World);

    public override bool Interact(Player p)
    {
        var s = p.Inventory.Selected;
        if (s?.Id == I.SpawnEgg) return false;
        if (s?.Id == I.NameTag) return false;
        if (!Dead && Customer == null && !IsBaby && !p.Sneaking)
        {
            if (World.IsRemote) return true;
            if (Recipes.Count == 0) PopulateTrades();
            if (Recipes.Count == 0) { _headShake = 40; World.PlaySound("mob.villager.no", X, Y, Z); return true; }
            Customer = p; Nav.Stop();
            p.OpenEntityContainer(new MerchantContainer(p.Inventory, this));
            return true;
        }
        if (IsBaby) { _headShake = 40; World.PlaySound("mob.villager.no", X, Y, Z); return true; }
        return base.Interact(p);
    }

    /// <summary>populateBuyingList: losuje karierę i dodaje oferty następnego poziomu.</summary>
    public void PopulateTrades()
    {
        var careers = VillagerTrades.Professions[Profession];
        if (Career == 0) { Career = World.Rand.Next(careers.Length) + 1; CareerLevel = 1; }
        else CareerLevel++;
        var levels = careers[Career - 1].Levels;
        if (CareerLevel - 1 < levels.Length) foreach (var t in levels[CareerLevel - 1]) t(World.Rand, Recipes);
    }

    /// <summary>useRecipe: dźwięk, XP i szansa na odblokowanie kolejnych ofert.</summary>
    public void UseRecipe(MerchantRecipe r)
    {
        r.Uses++;
        LivingSoundTimer = -LivingSoundInterval;
        World.PlaySound("mob.villager.yes", X, Y, Z);
        if (r.Uses == 1 || World.Rand.Next(5) == 0)
        {
            _restock = 40; _needsRefresh = true; WillingToMate = true;
            if (r.RewardsExp) World.SpawnXp(X, Y + .5, Z, 3 + World.Rand.Next(4));
        }
    }

    protected override void Think()
    {
        if (_headShake > 0) _headShake--;
        if (Customer != null)
        {
            if (Customer.Dead || Customer.Removed || Customer.OpenContainer is not MerchantContainer mc || mc.Villager != this || DistanceSq(Customer) > 64) Customer = null;
            else { Nav.Stop(); LookTarget = Customer; LookTime = 10; return; }
        }
        if (_restock > 0 && --_restock == 0 && _needsRefresh)
        {
            foreach (var r in Recipes) if (r.Disabled) r.MaxUses += World.Rand.Next(6) + World.Rand.Next(6) + 2;
            PopulateTrades(); _needsRefresh = false;
            AddEffect(Effect.Regeneration, 200, 0);
        }
        // EntityAIAvoidEntity(zombie, 8, 0.6, 0.6)
        var zombie = World.GetEntitiesInBox(BoundingBox.Grow(8, 3, 8), this).OfType<Zombie>().FirstOrDefault(z => !z.Dead);
        if (zombie != null) { if (Age % 10 == 0 || !Nav.HasPath) Nav.MoveTo(X + (X - zombie.X) * 2, Y, Z + (Z - zombie.Z) * 2, MoveSpeed * 2); return; }
        PickupFood();
        if (!IsBaby && Profession == 0 && Career == 1 && HarvestFarmland()) return;
        if (!IsBaby && WillingToMate && TryMate()) return;
        if (IsBaby && World.Rand.Next(200) == 0) { var other = World.GetEntitiesInBox(BoundingBox.Grow(6, 3, 6), this).OfType<Villager>().FirstOrDefault(v => v.IsBaby); if (other != null) Nav.MoveTo(other.X, other.Y, other.Z, MoveSpeed * 1.5); }
        TrySpawnGolem();
        if (World.Rand.Next(1000) == 0 && GoIndoors()) return;
        LookAtPlayersRandomly();
        Wander(MoveSpeed * .6 / .5, 120);
    }

    private void PickupFood()
    {
        if (++_pickup % 10 != 0) return;
        foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(1, 0, 1), this).OfType<ItemEntity>())
        {
            if (e.Removed || e.PickupDelay > 0 || e.Stack?.Id is not (I.Bread or I.Potato or I.Carrot or I.Wheat or I.Seeds)) continue;
            for (int i = 0; i < Food.Size && e.Stack.Count > 0; i++)
            {
                var s = Food.Get(i);
                if (s == null) { Food.Set(i, e.Stack.Copy()); e.Stack.Count = 0; }
                else if (s.CanStackWith(e.Stack)) { int n = Math.Min(e.Stack.Count, 64 - s.Count); s.Count += n; e.Stack.Count -= n; }
            }
            if (e.Stack.Count <= 0) e.Removed = true;
        }
        // farmer robi chleb z pszenicy
        if (Career == 1 && Profession == 0) { int wheat = Count(I.Wheat); if (wheat >= 3) { Take(I.Wheat, 3); Give(new ItemStack(I.Bread)); } }
    }
    private int Count(int id) { int n = 0; for (int i = 0; i < Food.Size; i++) if (Food.Get(i)?.Id == id) n += Food.Get(i).Count; return n; }
    private void Take(int id, int n) { for (int i = 0; i < Food.Size && n > 0; i++) if (Food.Get(i) is { } s && s.Id == id) { int k = Math.Min(n, s.Count); s.Count -= k; n -= k; if (s.Count <= 0) Food.Set(i, null); } }
    private void Give(ItemStack st) { for (int i = 0; i < Food.Size; i++) { var s = Food.Get(i); if (s == null) { Food.Set(i, st); return; } if (s.CanStackWith(st) && s.Count < 64) { s.Count++; return; } } }
    /// <summary>hasEnoughFoodToBreed: 3 chleby albo 12 marchwi/ziemniaków.</summary>
    public bool HasFoodToBreed => Count(I.Bread) >= 3 || Count(I.Carrot) >= 12 || Count(I.Potato) >= 12;
    private void EatForBreeding() { if (Count(I.Bread) >= 3) Take(I.Bread, 3); else if (Count(I.Carrot) >= 12) Take(I.Carrot, 12); else Take(I.Potato, 12); }

    /// <summary>Liczba drzwi wioski w promieniu 32 bloków (Village.getNumVillageDoors), z pamięcią podręczną.</summary>
    public int VillageDoors()
    {
        int cx = MathUtil.FloorToInt(X) >> 4, cz = MathUtil.FloorToInt(Z) >> 4;
        long key = ((long)cx << 32) ^ (uint)cz;
        if (DoorCache.TryGetValue(key, out var c) && Age - c.tick is >= 0 and < 400) return c.doors;
        int x0 = MathUtil.FloorToInt(X), y0 = MathUtil.FloorToInt(Y), z0 = MathUtil.FloorToInt(Z), doors = 0;
        for (int dx = -32; dx <= 32; dx++) for (int dz = -32; dz <= 32; dz++) for (int dy = -4; dy <= 4; dy++)
        {
            int id = World.GetBlock(x0 + dx, y0 + dy, z0 + dz);
            if (PathFinder.IsWoodDoor(id) && World.GetBlock(x0 + dx, y0 + dy - 1, z0 + dz) != id) doors++;
        }
        DoorCache[key] = (Age, doors);
        return doors;
    }

    /// <summary>EntityAIVillagerMate: limit 35% liczby drzwi, obaj chętni i najedzeni.</summary>
    private bool TryMate()
    {
        if (GrowingAge != 0 || World.Rand.Next(500) != 0 && Mate == null) return false;
        if (Mate == null)
        {
            int villagers = World.GetEntitiesInBox(BoundingBox.Grow(32, 8, 32), null).Count(e => e is Villager);
            if (villagers >= (int)(VillageDoors() * .35)) { WillingToMate = false; return false; }
            Mate = World.GetEntitiesInBox(BoundingBox.Grow(8, 3, 8), this).OfType<Villager>().FirstOrDefault(v => CanMateWith(v) && v.GrowingAge == 0 && !v.IsBaby && v.HasFoodToBreed);
            if (Mate == null || !HasFoodToBreed) { Mate = null; return false; }
            MateTicks = 300;
        }
        var m = (Villager)Mate;
        if (m.Dead || --MateTicks <= 0) { Mate = null; return false; }
        LookTarget = m; LookTime = 10;
        if (DistanceSq(m) > 2.25) { if (Age % 10 == 0) Nav.MoveTo(m.X, m.Y, m.Z, MoveSpeed * .5 / .5); return true; }
        Nav.Stop();
        if (World.Rand.Next(35) == 0) World.SpawnParticles(ParticleKind.Heart, X, Y + Height + .5, Z, 1);
        if (MateTicks % 50 == 0 && HasFoodToBreed && m.HasFoodToBreed)
        {
            EatForBreeding(); m.EatForBreeding();
            var child = new Villager(World); child.GrowingAge = -24000; child.SetPositionAndPrev(X, Y, Z); World.SpawnEntity(child);
            GrowingAge = m.GrowingAge = 6000; WillingToMate = m.WillingToMate = false; Mate = null; m.Mate = null;
            World.SpawnParticles(ParticleKind.Heart, X, Y + Height, Z, 7);
        }
        return true;
    }

    /// <summary>EntityAIHarvestFarmland: rolnik zbiera dojrzałe uprawy i sieje z ekwipunku.</summary>
    private bool HarvestFarmland()
    {
        if (World.Rand.Next(20) != 0 && !Nav.HasPath) return false;
        int x0 = MathUtil.FloorToInt(X), y0 = MathUtil.FloorToInt(Y), z0 = MathUtil.FloorToInt(Z);
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
        {
            int x = x0 + dx, z = z0 + dz, id = World.GetBlock(x, y0, z);
            if ((id == B.Wheat || id == B.Carrots || id == B.Potatoes) && World.GetMeta(x, y0, z) >= 7) { World.BreakBlock(x, y0, z, true); return true; }
            if (id == B.Air && World.GetBlock(x, y0 - 1, z) == B.Farmland)
            {
                int seed = Count(I.Seeds) > 0 ? I.Seeds : Count(I.Carrot) > 0 ? I.Carrot : Count(I.Potato) > 0 ? I.Potato : 0;
                if (seed != 0) { Take(seed, 1); World.SetBlock(x, y0, z, seed == I.Seeds ? B.Wheat : seed == I.Carrot ? B.Carrots : B.Potatoes); return true; }
            }
        }
        if (Nav.HasPath) return true;
        for (int i = 0; i < 20; i++)
        {
            int x = x0 + World.Rand.Next(17) - 8, z = z0 + World.Rand.Next(17) - 8, y = y0 + World.Rand.Next(3) - 1, id = World.GetBlock(x, y, z);
            bool ripe = (id == B.Wheat || id == B.Carrots || id == B.Potatoes) && World.GetMeta(x, y, z) >= 7;
            bool empty = id == B.Air && World.GetBlock(x, y - 1, z) == B.Farmland && (Count(I.Seeds) + Count(I.Carrot) + Count(I.Potato)) > 0;
            if ((ripe || empty) && Nav.MoveTo(x, y, z, MoveSpeed * .6 / .5)) return true;
        }
        return false;
    }

    /// <summary>EntityAIMoveIndoors w uproszczeniu: w nocy idzie do najbliższych drzwi.</summary>
    private bool GoIndoors()
    {
        long t = World.DayTime % 24000;
        if (t is < 13000 or > 23000) return false;
        int x0 = MathUtil.FloorToInt(X), y0 = MathUtil.FloorToInt(Y), z0 = MathUtil.FloorToInt(Z);
        for (int r = 2; r <= 16; r += 2) for (int dx = -r; dx <= r; dx++) for (int dz = -r; dz <= r; dz++)
            if (PathFinder.IsWoodDoor(World.GetBlock(x0 + dx, y0, z0 + dz)) && Nav.MoveTo(x0 + dx, y0, z0 + dz, MoveSpeed * .6 / .5)) return true;
        return false;
    }

    /// <summary>Village.tick: golem w wiosce z co najmniej 10 wieśniakami i 21 drzwiami (szansa 1/7000 na tick wioski).</summary>
    private void TrySpawnGolem()
    {
        if (World.Rand.Next(7000) != 0) return;
        var near = World.GetEntitiesInBox(BoundingBox.Grow(32, 8, 32), null);
        int villagers = near.Count(e => e is Villager);
        if (villagers < 10 || near.Count(e => e is IronGolem) >= villagers / 10 || VillageDoors() < 21) return;
        if (World.Rand.Next(villagers) != 0) return;
        for (int i = 0; i < 10; i++)
        {
            var g = new IronGolem(World);
            g.SetPositionAndPrev(MathUtil.FloorToInt(X) + World.Rand.Next(16) - 8 + .5, MathUtil.FloorToInt(Y) + World.Rand.Next(6) - 3, MathUtil.FloorToInt(Z) + World.Rand.Next(16) - 8 + .5);
            if (g.HasSpawnSpace() && Block.Get(World.GetBlock(MathUtil.FloorToInt(g.X), MathUtil.FloorToInt(g.Y) - 1, MathUtil.FloorToInt(g.Z))).Opaque) { World.SpawnEntity(g); return; }
        }
    }

    public override void OnStruckByLightning(Entity bolt)
    {
        if (World.IsRemote || Dead) return;
        var witch = new Witch(World); witch.SetPositionAndPrev(X, Y, Z); witch.Yaw = Yaw; witch.CustomName = CustomName; witch.PersistenceRequired = PersistenceRequired;
        World.SpawnEntity(witch); Removed = true;
    }

    public override void OnDeath(DamageSource src)
    {
        if (Dead) return;
        if (src.Attacker is Player p) Customer = null;
        base.OnDeath(src);
    }

    public override void Save(BinaryWriter b)
    {
        base.Save(b);
        b.Write(Profession); b.Write(Career); b.Write(CareerLevel); b.Write(WillingToMate);
        b.Write(Recipes.Count); foreach (var r in Recipes) r.Save(b);
        for (int i = 0; i < 8; i++) b.WriteStack(Food.Get(i));
    }
    public override void Load(BinaryReader b)
    {
        base.Load(b);
        Profession = Math.Clamp(b.ReadInt32(), 0, 4); Career = b.ReadInt32(); CareerLevel = b.ReadInt32(); WillingToMate = b.ReadBoolean();
        Recipes.Clear(); int n = b.ReadInt32(); for (int i = 0; i < n; i++) Recipes.Add(MerchantRecipe.Load(b));
        for (int i = 0; i < 8; i++) Food.Slots[i] = b.ReadStack();
    }
    public override int NetData => base.NetData | (Profession << 1) | (_headShake > 0 ? 16 : 0);
    public override void SetNetData(int d) { base.SetNetData(d); Profession = (d >> 1) & 7; _headShake = (d & 16) != 0 ? Math.Max(_headShake, 2) : 0; }
}

/// <summary>InventoryMerchant + ContainerMerchant: 2 sloty zapłaty, wynik i wybór oferty.</summary>
public sealed class MerchantContainer : Container
{
    public readonly Villager Villager;
    public readonly SimpleInventory Inv = new(3);
    public int Index;
    public MerchantRecipe Current => Index >= 0 && Index < Villager.Recipes.Count ? Villager.Recipes[Index] : null;

    private sealed class ResultSlot : Slot
    {
        private readonly MerchantContainer _c;
        public ResultSlot(MerchantContainer c) : base(c.Inv, 2, 120, 53) { _c = c; IsOutput = true; }
        public override void OnTake(Player p, ItemStack taken) { if (_c.Current is { } r && taken != null && taken.Count < r.Sell.Count && taken.Id == r.Sell.Id) taken.Count = r.Sell.Count; _c.OnTrade(); }
    }
    public MerchantContainer(PlayerInventory inv, Villager v)
    {
        Villager = v; Title = v.CustomName ?? v.DisplayName;
        Inv.OnChanged = Refresh;
        Slots.Add(new Slot(Inv, 0, 36, 53)); Slots.Add(new Slot(Inv, 1, 62, 53)); Slots.Add(new ResultSlot(this));
        AddPlayerSlots(inv, 84, 142);
        Index = Math.Max(0, v.Recipes.FindIndex(r => !r.Disabled));
    }
    public void SetIndex(int i) { Index = Math.Clamp(i, 0, Math.Max(0, Villager.Recipes.Count - 1)); Refresh(); }

    private static bool Pays(ItemStack have, ItemStack need) => need == null ? true : have != null && have.Id == need.Id && have.Damage == need.Damage && have.Count >= need.Count;

    /// <summary>Znajduje ofertę pasującą do zapłaty (najpierw wybraną, jak getRecipesFor w 1.8).</summary>
    private MerchantRecipe Match(out bool swapped)
    {
        swapped = false;
        ItemStack a = Inv.Get(0), b = Inv.Get(1);
        if (a == null) { a = b; b = null; swapped = a != null; }
        if (a == null) return null;
        bool Fits(MerchantRecipe r) => !r.Disabled && Pays(a, r.Buy1) && (r.Buy2 == null || Pays(b, r.Buy2));
        var cur = Current;
        if (cur != null && Fits(cur)) return cur;
        return Villager.Recipes.FirstOrDefault(Fits);
    }
    private bool _busy;
    public void Refresh()
    {
        if (_busy) return; _busy = true;
        var r = Match(out _);
        Inv.Set(2, r?.Sell.Copy());
        _busy = false;
    }
    public void OnTrade()
    {
        var r = Match(out bool swapped);
        if (r == null) return;
        int ia = swapped ? 1 : 0, ib = swapped ? 0 : 1;
        Pay(ia, r.Buy1); if (r.Buy2 != null) Pay(ib, r.Buy2);
        Villager.UseRecipe(r);
        Refresh();
    }
    private void Pay(int slot, ItemStack need)
    {
        var s = Inv.Get(slot); if (s == null) return;
        s.Count -= need.Count; Inv.Set(slot, s.Count <= 0 ? null : s);
    }
    protected override bool TransferFromPlayer(ItemStack s) => MergeStack(s, 0, 2, false);
    public override void TransferStack(Player p, int idx)
    {
        if (idx == 2)
        {
            for (int guard = 0; guard < 64; guard++)
            {
                var res = Inv.Get(2); if (res == null) break;
                var copy = res.Copy();
                if (!MergeStack(copy, FirstPlayerSlot, FirstPlayerSlot + 36, true)) break;
                if (copy.Count > 0) p.DropStack(copy, false);
                Inv.Set(2, null); OnTrade();
            }
            return;
        }
        base.TransferStack(p, idx);
    }
    public override void OnClose(Player p)
    {
        base.OnClose(p);
        for (int i = 0; i < 2; i++) if (Inv.Get(i) is { } s) { Inv.Set(i, null); if (!p.Inventory.Add(s)) p.DropStack(s, false); }
        Inv.Set(2, null);
        if (Villager.Customer == p) Villager.Customer = null;
    }
    public bool StillValid(Player p) => !Villager.Dead && !Villager.Removed && Villager.DistanceSq(p) < 64;
}
