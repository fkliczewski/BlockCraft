using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Kowadło: wartości policzone ręcznie według ContainerRepair/SlotRepair/EntityFallingBlock z 1.8.</summary>
public static partial class SelfTest
{
    private static ItemStack Book(params (Ench e, int l)[] list)
    {
        var b = new ItemStack(I.EnchantedBook);
        foreach (var (e, l) in list) EnchantingRules.AddStored(b, new EnchantmentRoll(e, l));
        return b;
    }

    private static void TestAnvil()
    {
        var sword = new ItemStack(I.DiamondSword);
        var r = AnvilRules.Compute(sword, null, "Ostrze", false);
        Check(r.Output?.CustomName == "Ostrze" && r.Cost == 1 && AnvilRules.RepairCost(r.Output) == 1, "Kowadło: zmiana nazwy kosztuje 1, RepairCost 1", $"{r.Cost}");
        Check(AnvilRules.Compute(sword, null, sword.Name, false).Output == null, "Kowadło: ta sama nazwa bez zmian = brak wyniku");
        var named = r.Output;
        var cleared = AnvilRules.Compute(named, null, "", false);
        Check(cleared.Output != null && cleared.Output.CustomName == null && cleared.Cost == 2, "Kowadło: usunięcie nazwy (1 + RepairCost 1)", $"{cleared.Cost}");

        var pick = new ItemStack(I.DiamondPickaxe) { Damage = 1000 };
        r = AnvilRules.Compute(pick, new ItemStack(I.Diamond, 5), "", false);
        Check(r.Output?.Damage == 0 && r.Cost == 3 && r.MaterialCost == 3, "Kowadło: naprawa 3 diamentami po 25% (1000 -> 0)", $"{r.Output?.Damage} {r.Cost} {r.MaterialCost}");
        Check(AnvilRules.Compute(new ItemStack(I.DiamondPickaxe), new ItemStack(I.Diamond), "", false).Output == null, "Kowadło: nieuszkodzone narzędzie nie przyjmuje materiału");
        Check(AnvilRules.Compute(new ItemStack(I.IronHelmet) { Damage = 50 }, new ItemStack(I.IronIngot), "", false).Output?.Damage == 9
              && AnvilRules.Compute(new ItemStack(I.ChainHelmet) { Damage = 50 }, new ItemStack(I.IronIngot), "", false).Output != null,
            "Kowadło: zbroja żelazna i kolczuga naprawiane sztabką");

        int max = Item.ById[I.IronSword].MaxDamage;
        r = AnvilRules.Compute(new ItemStack(I.IronSword) { Damage = max - 50 }, new ItemStack(I.IronSword) { Damage = max - 50 }, "", false);
        Check(r.Output?.Damage == max - (100 + max * 12 / 100) && r.Cost == 2, "Kowadło: łączenie dwóch mieczy (+12% premii, koszt 2)", $"{r.Output?.Damage} {r.Cost}");

        var s3 = new ItemStack(I.DiamondSword); EnchantHelper.Set(s3, Ench.Sharpness, 3);
        r = AnvilRules.Compute(s3, Book((Ench.Sharpness, 3)), "", false);
        Check(EnchantHelper.GetLevel(r.Output, Ench.Sharpness) == 4 && r.Cost == 4, "Kowadło: Ostrość III + książka III = IV za 4", $"{r.Cost}");
        r = AnvilRules.Compute(s3, Book((Ench.Sharpness, 5)), "", false);
        Check(EnchantHelper.GetLevel(r.Output, Ench.Sharpness) == 5 && r.Cost == 5, "Kowadło: wyższy poziom z książki wygrywa");
        var smite = new ItemStack(I.DiamondSword); EnchantHelper.Set(smite, Ench.Smite, 1);
        r = AnvilRules.Compute(smite, Book((Ench.Sharpness, 5)), "", false);
        Check(r.Cost == 1 && EnchantHelper.GetLevel(r.Output, Ench.Sharpness) == 0, "Kowadło: niezgodne zaklęcie odrzucone za 1 poziom", $"{r.Cost}");
        var silk = new ItemStack(I.DiamondPickaxe); EnchantHelper.Set(silk, Ench.SilkTouch, 1);
        r = AnvilRules.Compute(silk, Book((Ench.Fortune, 3)), "", false);
        Check(EnchantHelper.GetLevel(r.Output, Ench.Fortune) == 0 && r.Cost == 1, "Kowadło: Szczęście nie łączy się z Jedwabnym dotykiem");
        r = AnvilRules.Compute(new ItemStack(I.DiamondSword), Book((Ench.Thorns, 1)), "", false);
        Check(r.Output == null, "Kowadło: Ciernie nie trafią na miecz");
        r = AnvilRules.Compute(new ItemStack(I.DiamondSword), Book((Ench.Thorns, 1)), "", true);
        Check(EnchantHelper.GetLevel(r.Output, Ench.Thorns) == 1, "Kowadło: w kreatywnym dowolne zaklęcie");
        r = AnvilRules.Compute(new ItemStack(I.DiamondAxe), Book((Ench.Sharpness, 1)), "", false);
        Check(EnchantHelper.GetLevel(r.Output, Ench.Sharpness) == 1, "Kowadło: Ostrość na siekierze z książki");

        r = AnvilRules.Compute(Book((Ench.Unbreaking, 2)), Book((Ench.Unbreaking, 2)), "", false);
        Check(EnchantHelper.GetLevel(r.Output, Ench.Unbreaking, "StoredEnchantments") == 3 && r.Cost == 3 && r.Output.Tag.GetList("ench") == null,
            "Kowadło: dwie książki Niezniszczalność II = III za 3 (zapis w StoredEnchantments)", $"{r.Cost}");

        var worn = new ItemStack(I.DiamondPickaxe) { Damage = 500 }; AnvilRules.SetRepairCost(worn, 39);
        Check(AnvilRules.Compute(worn, new ItemStack(I.Diamond), "", false).Output == null && AnvilRules.Compute(worn, new ItemStack(I.Diamond), "", true).Output != null,
            "Kowadło: koszt 40 = Zbyt drogie! (kreatywny bez limitu)");
        var rn = AnvilRules.Compute(worn, null, "X", false);
        Check(rn.Output != null && rn.Cost == 39, "Kowadło: sama zmiana nazwy obcięta do 39", $"{rn.Cost}");
        var once = AnvilRules.Compute(new ItemStack(I.DiamondSword), null, "A", false).Output;
        var twice = AnvilRules.Compute(once, null, "B", false);
        Check(AnvilRules.RepairCost(twice.Output) == 3 && twice.Cost == 2, "Kowadło: kara za pracę 2n+1 (1, 3)");

        // kontener: poziomy, materiał, uszkadzanie kowadła
        var w = MakeWorld(9, WorldType.Superflat, 1);
        var p = new Player(w); p.SetPositionAndPrev(8.5, 4, 8.5); w.AddEntityNow(p);
        w.SetBlock(9, 4, 8, B.Anvil, 0);
        var c = new AnvilContainer(p, w, 9, 4, 8);
        p.XpLevel = 10;
        c.Inv.Set(0, new ItemStack(I.DiamondPickaxe) { Damage = 1000 }); c.Inv.Set(1, new ItemStack(I.Diamond, 5)); c.Inv.Changed();
        Check(c.MaximumCost == 3 && c.Output.Get(0) != null && c.Slots[2].CanTake(p), "Kowadło: kontener liczy wynik i koszt");
        c.Click(2, 0, ClickType.Pickup, p);
        Check(p.Cursor?.Id == I.DiamondPickaxe && p.Cursor.Damage == 0 && p.XpLevel == 7 && c.Inv.Get(0) == null && c.Inv.Get(1)?.Count == 2,
            "Kowadło: odbiór zabiera 3 poziomy i 3 diamenty", $"lvl {p.XpLevel} mat {c.Inv.Get(1)?.Count}");
        p.Cursor = null;
        p.XpLevel = 0;
        c.Inv.Set(0, new ItemStack(I.DiamondPickaxe) { Damage = 1000 }); c.Inv.Changed();
        c.Click(2, 0, ClickType.Pickup, p);
        Check(p.Cursor == null && c.Inv.Get(0) != null, "Kowadło: bez poziomów nie da się odebrać");
        int uses = 0; p.XpLevel = 10000;
        while (w.GetBlock(9, 4, 8) == B.Anvil && uses < 2000)
        {
            c.Inv.Set(0, new ItemStack(I.Stick)); c.Inv.Set(1, null); c.SetName("n" + uses); p.Cursor = null;
            c.Click(2, 0, ClickType.Pickup, p); uses++;
        }
        Check(w.GetBlock(9, 4, 8) == B.Air && uses is > 3 and < 200, "Kowadło: niszczy się po kilkunastu użyciach (12% na stopień)", uses.ToString());

        // spadające kowadło: 2 obrażenia za blok ponad pierwszy
        foreach (var e in w.Entities.OfType<LivingEntity>().Where(e => e != p).ToList()) e.Kill();
        var z = new Zombie(w); z.SetPositionAndPrev(4.5, 4, 4.5); w.AddEntityNow(z);
        w.SetBlock(4, 10, 4, B.Anvil, 1 << 2);
        for (int i = 0; i < 60 && w.GetBlock(4, 4, 4) != B.Anvil && w.GetBlock(4, 5, 4) != B.Anvil; i++) w.Tick();
        Check(z.Health < z.MaxHealth - 5, "Kowadło: spadające kowadło rani moba", $"{z.Health}");
        int landed = w.GetBlock(4, 4, 4) == B.Anvil ? w.GetMeta(4, 4, 4) : w.GetMeta(4, 5, 4);
        Check(AnvilBlock.Damage(landed) >= 1, "Kowadło: spadając zachowuje uszkodzenie");
    }
}
