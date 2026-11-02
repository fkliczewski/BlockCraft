using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Działanie zaklęć w rozgrywce (EnchantEffects) według wzorów MC 1.8.</summary>
public static partial class SelfTest
{
    private static ItemStack Ench1(int id, Ench e, int lvl) { var s = new ItemStack(id); EnchantHelper.Set(s, e, lvl); return s; }

    private static void TestEnchantEffects()
    {
        var w = MakeWorld(7, WorldType.Superflat, 1);

        // Premie obrażeń (getModifierForCreature)
        Check(Math.Abs(EnchantEffects.DamageBonus(Ench1(I.DiamondSword, Ench.Sharpness, 5), EnchantEffects.Creature.Undefined) - 6.25f) < 1e-4,
            "Zaklęcia: Ostrość V = +6,25");
        var smite = Ench1(I.DiamondSword, Ench.Smite, 5);
        Check(EnchantEffects.DamageBonus(smite, EnchantEffects.Creature.Undead) == 12.5f && EnchantEffects.DamageBonus(smite, EnchantEffects.Creature.Arthropod) == 0,
            "Zaklęcia: Pogromca V = +12,5 tylko na nieumarłych");
        Check(EnchantEffects.DamageBonus(Ench1(I.DiamondSword, Ench.BaneOfArthropods, 5), EnchantEffects.Creature.Arthropod) == 12.5f,
            "Zaklęcia: Zmora stawonogów V = +12,5 na stawonogach");
        Check(EnchantEffects.CreatureOf(new Zombie(w)) == EnchantEffects.Creature.Undead && EnchantEffects.CreatureOf(new Spider(w)) == EnchantEffects.Creature.Arthropod,
            "Zaklęcia: zombie nieumarły, pająk stawonóg");

        // Ochrony (EnchantmentProtection.calcModifierDamage)
        var gen = DamageSource.Mob(null);
        Check(EnchantEffects.ProtectionModifier(Ench.Protection, 4, gen) == 5
              && EnchantEffects.ProtectionModifier(Ench.FeatherFalling, 4, DamageSource.Fall) == 18
              && EnchantEffects.ProtectionModifier(Ench.FireProtection, 4, DamageSource.Lava) == 9
              && EnchantEffects.ProtectionModifier(Ench.BlastProtection, 4, DamageSource.Explosion(null)) == 11
              && EnchantEffects.ProtectionModifier(Ench.ProjectileProtection, 4, DamageSource.Arrow(null, null)) == 11
              && EnchantEffects.ProtectionModifier(Ench.FeatherFalling, 4, gen) == 0
              && EnchantEffects.ProtectionModifier(Ench.Protection, 4, DamageSource.Void) == 0,
            "Zaklęcia: modyfikatory ochron IV = 5/18/9/11/11, poza typem 0, pustka 0");

        var p = new Player(w); p.SetPositionAndPrev(8, 5, 8);
        for (int i = 0; i < 4; i++) p.Inventory.Armor[i] = Ench1(I.DiamondHelmet + i, Ench.Protection, 4);
        int min = 99, max = -1; var r = new Rng(1);
        for (int i = 0; i < 2000; i++) { int k = EnchantEffects.ProtectionEpf(p, gen, r); min = Math.Min(min, k); max = Math.Max(max, k); }
        Check(min == 10 && max == 20, "Zaklęcia: 4x Ochrona IV = EPF 20, losowo 10-20", $"{min}-{max}");
        EnchantHelper.Set(p.Inventory.Armor[3], Ench.FeatherFalling, 4);
        min = 99; max = -1;
        for (int i = 0; i < 2000; i++) { int k = EnchantEffects.ProtectionEpf(p, DamageSource.Fall, r); min = Math.Min(min, k); max = Math.Max(max, k); }
        Check(min == 13 && max == 25, "Zaklęcia: suma ochron ograniczona do 25 (upadek 13-25)", $"{min}-{max}");

        // Ochrona przed ogniem skraca podpalenie
        for (int i = 0; i < 4; i++) p.Inventory.Armor[i] = null;
        p.Inventory.Armor[0] = Ench1(I.IronHelmet, Ench.FireProtection, 4);
        p.FireTicks = 0; p.SetFire(8);
        Check(p.FireTicks == 64, "Zaklęcia: Ochrona przed ogniem IV skraca 8 s ognia do 64 ticków", p.FireTicks.ToString());

        // Wydajność i niezniszczalność
        var eff = Ench1(I.DiamondPickaxe, Ench.Efficiency, 5);
        Check(EnchantEffects.Efficiency(p, eff, 8f) == 34f && EnchantEffects.Efficiency(p, eff, 1f) == 1f,
            "Zaklęcia: Wydajność V dodaje 26 tylko przy właściwym narzędziu");
        var rnd = new Random(5); int tool = 0, armor = 0;
        var ub = Ench1(I.DiamondPickaxe, Ench.Unbreaking, 3); var ubA = Ench1(I.DiamondHelmet, Ench.Unbreaking, 3);
        for (int i = 0; i < 20000; i++) { tool += EnchantEffects.Unbreaking(ub, 1, rnd); armor += EnchantEffects.Unbreaking(ubA, 1, rnd); }
        Check(Math.Abs(tool / 20000.0 - .25) < .02 && Math.Abs(armor / 20000.0 - .7) < .02,
            "Zaklęcia: Niezniszczalność III - narzędzie 25%, zbroja 70% zużycia", $"{tool / 200.0:0.0}% / {armor / 200.0:0.0}%");

        // Jedwabny dotyk i szczęście
        int bx = 4, by = 3, bz = 4;
        List<ItemEntity> Drops() => w.Entities.OfType<ItemEntity>().ToList();
        void Clear() { foreach (var e in Drops()) e.Kill(); w.Tick(); }
        Clear();
        w.SetBlock(bx, by, bz, B.Stone); w.BreakBlock(bx, by, bz, true, p, Ench1(I.DiamondPickaxe, Ench.SilkTouch, 1)); w.Tick();
        Check(Drops().Any(e => e.Stack.Id == B.Stone), "Zaklęcia: Jedwabny dotyk - kamień daje kamień");
        Clear();
        w.SetBlock(bx, by, bz, B.DiamondOre); w.BreakBlock(bx, by, bz, true, p, Ench1(I.DiamondPickaxe, Ench.SilkTouch, 1)); w.Tick();
        Check(Drops().Any(e => e.Stack.Id == B.DiamondOre), "Zaklęcia: Jedwabny dotyk - ruda diamentu daje rudę");
        Clear();
        int dia = 0; var fort = Ench1(I.DiamondPickaxe, Ench.Fortune, 3);
        for (int i = 0; i < 400; i++) { var l = new List<ItemStack>(); Block.ById[B.DiamondOre].GetDrops(w, bx, by, bz, 0, w.Rand, fort, l); dia += l.Sum(s => s.Count); }
        Check(Math.Abs(dia / 400.0 - 2.2) < .2, "Zaklęcia: Szczęście III - średnio 2,2 diamentu", (dia / 400.0).ToString("0.00"));
        bool allFlint = true;
        for (int i = 0; i < 50; i++) { var l = new List<ItemStack>(); Block.ById[B.Gravel].GetDrops(w, bx, by, bz, 0, w.Rand, fort, l); allFlint &= l.Count == 1 && l[0].Id == I.Flint; }
        Check(allFlint, "Zaklęcia: Szczęście III - żwir zawsze daje krzemień");

        // Atak wręcz: Pogromca, Zmora stawonogów (spowolnienie IV), Zaklęty ogień
        float Hit(LivingEntity t, ItemStack held)
        {
            t.SetPositionAndPrev(9, 4, 8); w.AddEntityNow(t);
            p.Inventory.Selected = held; p.OnGround = true; p.FallDistance = 0; p.Sprinting = false;
            float h0 = t.Health; p.AttackEntity(t); return h0 - t.Health;
        }
        float plain = Hit(new Zombie(w), new ItemStack(I.IronSword)), smote = Hit(new Zombie(w), Ench1(I.IronSword, Ench.Smite, 1));
        Check(Math.Abs(smote - plain - 2.5f * 23 / 25f) < .01f, "Zaklęcia: Pogromca I dodaje 2,5 (po pancerzu zombie)", $"{plain} / {smote}");
        var sp = new Spider(w); Hit(sp, Ench1(I.IronSword, Ench.BaneOfArthropods, 1));
        Check(sp.HasEffect(Effect.Slowness) && sp.EffectLevel(Effect.Slowness) == 3, "Zaklęcia: Zmora stawonogów daje spowolnienie IV");
        var z3 = new Zombie(w); Hit(z3, Ench1(I.IronSword, Ench.FireAspect, 2));
        Check(z3.FireTicks == 160, "Zaklęcia: Zaklęty ogień II podpala na 8 s", z3.FireTicks.ToString());
    }
}
