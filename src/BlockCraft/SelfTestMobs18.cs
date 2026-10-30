using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy modułu MO: konie, handel, wilk, golemy, smycz, świnia, zapis i odczyt każdego moba.</summary>
public static partial class SelfTest
{
    private static byte[] EntityBytes(Entity e)
    {
        using var ms = new MemoryStream(); using var bw = new BinaryWriter(ms);
        e.Save(bw); bw.Flush(); return ms.ToArray();
    }

    /// <summary>Zapis, odczyt do świeżej encji i ponowny zapis: odczyt musi zjeść wszystkie bajty, a ponowny zapis dać te same.</summary>
    private static string LastRoundTrip = "";
    private static Entity RoundTrip(World.World w, Entity e, out bool exact)
    {
        var a = EntityBytes(e);
        var e2 = EntitySerializer.Create(e.TypeKey, w);
        exact = false;
        if (e2 == null) return null;
        using var br = new BinaryReader(new MemoryStream(a));
        e2.Load(br);
        var b = EntityBytes(e2);
        exact = br.BaseStream.Position == a.Length && b.AsSpan().SequenceEqual(a);
        if (!exact)
        {
            int i = 0; while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
            LastRoundTrip = $"odczyt {br.BaseStream.Position}/{a.Length} B, ponowny zapis {b.Length} B, różnica od bajtu {i}";
        }
        return e2;
    }

    private static T Put<T>(World.World w, T e, double x, double z) where T : Entity { e.SetPositionAndPrev(x, 4, z); w.AddEntityNow(e); return e; }

    private static void TestMobs18()
    {
        Console.WriteLine("[Moby MO]");
        var w = MakeWorld(7, WorldType.Superflat, 3);
        w.Rules.DoMobSpawning = false; w.DayTime = 6000;
        TestHorses18(w); TestTrade18(w); TestWolf18(w); TestGolems18(w); TestLeash18(w); TestPig18(w); TestMobSaves18(w);
    }

    private static void TestHorses18(World.World w)
    {
        var h = Put(w, new Horse(w), 0.5, -30.5); h.Temper = 0;
        var p = SpawnPlayer(w, 2, 4, -30.5); p.Name = "Jezdziec"; p.Inventory.Selected = null;
        int tries = 0;
        for (int i = 0; i < 6000 && !h.Tamed; i++)
        {
            if (h.Rider == null) { tries++; p.SetPositionAndPrev(h.X + 1.5, h.Y, h.Z); h.Interact(p); }
            w.Tick();
        }
        Check(h.Tamed && h.Owner == p.Name, "Koń oswojony jazdą", $"podejść {tries}, temperament {h.Temper}");
        h.Dismount();
        p.Sneaking = true;
        Check(h.Interact(p) && p.OpenContainer is HorseContainer, "Kucnięcie otwiera GUI konia");
        if (p.OpenContainer is HorseContainer hc)
        {
            Check(hc.Slots[0].CanPlace(new ItemStack(I.Saddle)) && !hc.Slots[0].CanPlace(new ItemStack(B.Dirt)), "Slot siodła przyjmuje tylko siodło");
            Check(hc.Slots[1].CanPlace(new ItemStack(I.DiamondHorseArmor)) && !hc.Slots[1].CanPlace(new ItemStack(I.Saddle)), "Slot zbroi przyjmuje tylko zbroję");
            hc.Slots[0].Stack = new ItemStack(I.Saddle); hc.Slots[1].Stack = new ItemStack(I.IronHorseArmor);
        }
        p.CloseContainer(); p.Sneaking = false;
        Check(h.Saddled && h.ArmorValue == 5, "Siodło i żelazna zbroja założone", $"siodło={h.Saddled} pancerz={h.ArmorValue}");
        Check(h.Interact(p) && p.RidingMob == h, "Wsiadanie na konia");
        double x0 = h.X, z0 = h.Z;
        p.Yaw = 0; p.MoveForward = 1;
        Ticks(w, 60);
        double moved = Math.Sqrt((h.X - x0) * (h.X - x0) + (h.Z - z0) * (h.Z - z0));
        Check(moved > 4, "Jazda na koniu", $"{moved:F1} bloku");
        p.MoveForward = 0; Ticks(w, 20);
        double y0 = h.Y, maxY = h.Y;
        p.IsJumping = true; Ticks(w, 8); p.IsJumping = false;
        for (int i = 0; i < 25; i++) { w.Tick(); maxY = Math.Max(maxY, h.Y); }
        Check(maxY - y0 > 0.6, "Skok ładowany konia", $"{maxY - y0:F2}");
        h.Dismount();
        var back = RoundTrip(w, h, out bool exact) as Horse;
        Check(exact && back != null && back.Tamed && back.Saddled && back.ArmorValue == 5 && back.Owner == p.Name, "Zapis i odczyt konia z ekwipunkiem", LastRoundTrip);

        // osioł: skrzynia tak, zbroja nie
        var d = Put(w, new Horse(w, 1), 6.5, -36.5); d.Tame(p);
        p.Inventory.Selected = new ItemStack(B.Chest);
        Check(d.Interact(p) && d.Chested && !d.CanWearArmor, "Osioł bierze skrzynię, nie zbroję");
        var h2 = Put(w, new Horse(w), 7.5, -36.5); h2.Tame(p);
        p.Inventory.Selected = new ItemStack(B.Chest); h2.Interact(p);
        Check(!h2.Chested, "Koń nie bierze skrzyni");
        p.RidingMob?.Dismount();
        // koń + osioł = muł
        d.SetPositionAndPrev(7.5, 4, -38.5); h2.SetPositionAndPrev(8.5, 4, -38.5);
        p.Inventory.Selected = new ItemStack(I.GoldenApple, 2);
        d.Interact(p); h2.Interact(p);
        Check(d.InLove > 0 && h2.InLove > 0, "Złote jabłko budzi miłość koni", $"osioł={d.InLove} koń={h2.InLove} wiek={d.GrowingAge}/{h2.GrowingAge}");
        Ticks(w, 300);
        var mule = w.Entities.OfType<Horse>().FirstOrDefault(e => e.HorseType == 2 && e.IsBaby);
        Check(mule != null, "Koń z osłem daje muła");
        Check(mule != null && !mule.CanMate, "Muł bezpłodny");
        var zh = Put(w, new Horse(w, 3), 12.5, -38.5);
        p.Inventory.Selected = null; zh.Interact(p);
        Check(zh.Rider == null, "Koń zombie nie daje się dosiąść");
    }

    private static void TestTrade18(World.World w)
    {
        var v = Put(w, new Villager(w, 0), -20.5, 0.5);
        v.Career = 1; v.CareerLevel = 1; v.Recipes.Clear();
        var r = new MerchantRecipe(new ItemStack(I.Wheat, 20), null, new ItemStack(I.Emerald));
        v.Recipes.Add(r);
        var p = SpawnPlayer(w, -22, 4, 0.5); p.Name = "Kupiec"; p.Inventory.Selected = null;
        Check(v.Interact(p) && p.OpenContainer is MerchantContainer && v.Customer == p, "Wieśniak otwiera handel");
        if (p.OpenContainer is not MerchantContainer mc) return;
        mc.Slots[0].Stack = new ItemStack(I.Wheat, 25);
        Check(mc.Inv.Get(2)?.Id == I.Emerald, "Oferta pokazuje wynik po zapłacie");
        mc.TransferStack(p, 2);
        Check(p.Inventory.CountOf(I.Emerald) == 1 && mc.Inv.Get(0)?.Count == 5 && r.Uses == 1, "Handel: 20 pszenicy za szmaragd", $"szmaragdy={p.Inventory.CountOf(I.Emerald)} reszta={mc.Inv.Get(0)?.Count} użycia={r.Uses}");
        r.Uses = r.MaxUses; mc.Slots[0].Stack = new ItemStack(I.Wheat, 25);
        Check(mc.Inv.Get(2) == null, "Wyczerpana oferta zablokowana");
        r.Uses = 1;
        p.CloseContainer();
        Check(v.Customer == null && p.Inventory.CountOf(I.Wheat) == 25, "Zamknięcie handlu oddaje zapłatę");
        Ticks(w, 100);
        Check(v.Recipes.Count > 1 && v.CareerLevel == 2, "Handel odblokowuje kolejne oferty", $"oferty={v.Recipes.Count} poziom={v.CareerLevel}");

        int bad = 0, total = 0, skipped = 0; var unknown = new SortedSet<int>();
        void Note(ItemStack st) { if (st != null && Item.ById[st.Id] == null) unknown.Add(st.Id); }
        foreach (var careers in VillagerTrades.Professions)
            foreach (var career in careers)
                foreach (var level in career.Levels)
                {
                    var raw = new List<MerchantRecipe>();
                    foreach (var t in level) t(w.Rand, raw);
                    if (raw.Count == 0) bad++;
                    foreach (var rr in raw)
                    {
                        total++; Note(rr.Buy1); Note(rr.Buy2); Note(rr.Sell);
                        if (rr.Buy1 == null || rr.Sell == null || rr.Buy1.Count <= 0 || rr.Sell.Count <= 0) bad++;
                    }
                }
        Check(bad == 0 && total > 60, "Tabele handlu 1.8 kompletne", $"oferty={total} błędy={bad}");
        if (unknown.Count > 0) Console.WriteLine("    oferty czekają na przedmioty innych modułów: " + string.Join(",", unknown));
        for (int prof = 0; prof < VillagerTrades.Professions.Length; prof++)
            for (int c = 1; c <= VillagerTrades.Professions[prof].Length; c++)
            {
                var vv = new Villager(w, prof) { Career = c, CareerLevel = 0 };
                for (int l = 0; l < VillagerTrades.Professions[prof][c - 1].Levels.Length; l++) vv.PopulateTrades();
                skipped += vv.Recipes.Count(rr => !VillagerTrades.Registered(rr));
            }
        Check(skipped == 0, "Wieśniak nie daje ofert z niezarejestrowanymi przedmiotami", skipped.ToString());

        var v2 = Put(w, new Villager(w, 1), -25.5, 5.5);
        v2.OnStruckByLightning(null); Ticks(w, 1);
        Check(v2.Removed && w.Entities.OfType<Witch>().Any(e => e.Distance(v2) < 1), "Piorun zmienia wieśniaka w wiedźmę");
    }

    private static void TestWolf18(World.World w)
    {
        var wf = Put(w, new Wolf(w), 20.5, 20.5);
        var p = SpawnPlayer(w, 22, 4, 20.5); p.Name = "Pan"; p.Inventory.Selected = new ItemStack(I.Bone, 64);
        for (int i = 0; i < 60 && !wf.Tamed; i++) wf.Interact(p);
        Check(wf.Tamed && wf.Owner == p.Name && wf.MaxHealth == 20 && wf.Sitting, "Wilk oswojony kością (siedzi, 20 HP)");
        p.Inventory.Selected = null; wf.Interact(p);
        Check(!wf.Sitting, "Prawy klik właściciela wstaje wilka");
        p.Inventory.Selected = new ItemStack(I.Lapis); wf.Interact(p);
        Check(wf.Collar == Dyes.Blue, "Barwienie obroży", wf.Collar.ToString());
        p.Inventory.Selected = null;
        p.SetPositionAndPrev(wf.X + 25, 4, wf.Z);
        Ticks(w, 100);
        Check(wf.Distance(p) < 6, "Wilk dogania albo teleportuje się do właściciela", $"{wf.Distance(p):F1}");
        var back = RoundTrip(w, wf, out bool exact) as Wolf;
        Check(exact && back != null && back.Tamed && back.Collar == Dyes.Blue && back.MaxHealth == 20, "Zapis i odczyt oswojonego wilka", LastRoundTrip);
    }

    private static void TestGolems18(World.World w)
    {
        w.SetBlock(-30, 4, -30, B.Snow); w.SetBlock(-30, 5, -30, B.Snow); w.SetBlock(-30, 6, -30, B.Pumpkin);
        Check(GolemBuilder.TryBuild(w, -30, 6, -30) && w.GetBlock(-30, 5, -30) == B.Air, "Golem śnieżny z bloków");
        int ix = -36, iz = -30;
        w.SetBlock(ix, 4, iz, B.IronBlock); w.SetBlock(ix, 5, iz, B.IronBlock); w.SetBlock(ix + 1, 5, iz, B.IronBlock); w.SetBlock(ix - 1, 5, iz, B.IronBlock);
        w.SetBlock(ix, 6, iz, B.Pumpkin);
        Check(GolemBuilder.TryBuild(w, ix, 6, iz) && w.GetBlock(ix + 1, 5, iz) == B.Air, "Golem żelazny z bloków (T)");
        Ticks(w, 1);
        Check(w.Entities.OfType<SnowGolem>().Any(), "Golem śnieżny pojawia się");
        Check(w.Entities.OfType<IronGolem>().Any(g => g.PlayerCreated), "Golem żelazny pojawia się (zbudowany przez gracza)");
        w.SetBlock(-40, 4, -30, B.IronBlock); w.SetBlock(-40, 5, -30, B.IronBlock); w.SetBlock(-40, 6, -30, B.Pumpkin);
        Check(!GolemBuilder.TryBuild(w, -40, 6, -30), "Bez ramion nie ma golema");
    }

    private static void TestLeash18(World.World w)
    {
        var cow = Put(w, new Cow(w), 30.5, -10.5);
        var p = SpawnPlayer(w, 32, 4, -10.5); p.Inventory.Selected = new ItemStack(I.Lead, 2);
        Check(cow.Interact(p) && cow.LeashHolder == p, "Smycz na krowie");
        p.SetPositionAndPrev(cow.X + 8, 4, cow.Z);
        Ticks(w, 80);
        Check(cow.LeashHolder == p && cow.Distance(p) < 7, "Smycz ciągnie krowę", $"{cow.Distance(p):F1}");
        int fx = MathUtil.FloorToInt(p.X) + 1, fz = MathUtil.FloorToInt(p.Z);
        w.SetBlock(fx, 4, fz, B.Fence);
        Check(LeashKnot.TryTie(w, p, fx, 4, fz) && cow.LeashHolder is LeashKnot, "Przywiązanie do płotu");
        Ticks(w, 1);
        if (cow.LeashHolder is LeashKnot knot)
        {
            knot.Interact(p);
            Check(cow.LeashHolder == null && knot.Removed, "Zdjęcie węzła uwalnia krowę");
        }
        var sq = new Squid(w); var zb = new Zombie(w);
        Check(!sq.CanBeLeashed && !zb.CanBeLeashed && new IronGolem(w).CanBeLeashed, "Kto może mieć smycz (jak 1.8)");
    }

    private static void TestPig18(World.World w)
    {
        var pig = Put(w, new Pig(w), -10.5, 30.5);
        var p = SpawnPlayer(w, -12, 4, 30.5); p.Inventory.Selected = new ItemStack(I.Saddle);
        Check(pig.Interact(p) && pig.Saddled && p.Inventory.Selected == null, "Siodło na świni");
        p.Inventory.Selected = new ItemStack(I.CarrotOnAStick);
        Check(pig.Interact(p) && p.RidingMob == pig, "Wsiadanie na świnię");
        pig.Yaw = 0; p.Yaw = 90;
        double x0 = pig.X, z0 = pig.Z;
        Ticks(w, 120);
        double moved = Math.Sqrt((pig.X - x0) * (pig.X - x0) + (pig.Z - z0) * (pig.Z - z0));
        Check(moved > 2, "Świnia jedzie za marchewką", $"{moved:F1}");
        Check(Math.Abs(MathUtil.WrapDegrees(pig.Yaw - 90)) < 15, "Świnia skręca za wzrokiem", pig.Yaw.ToString("F0"));
        Check(pig.Boost(p) && p.Inventory.Selected?.Damage == 7 && !pig.Boost(p), "Przyspieszenie marchewką (-7 trwałości)");
        pig.Dismount();
    }

    private static void TestMobSaves18(World.World w)
    {
        var broken = new List<string>();
        foreach (var key in Entities.Registries.MobKeys)
        {
            var e = EntitySerializer.Create(key, w);
            if (e == null) { broken.Add(key + "(brak)"); continue; }
            e.SetPositionAndPrev(3.5, 4, 3.5);
            var back = RoundTrip(w, e, out bool exact);
            if (!exact || back == null || back.GetType() != e.GetType()) broken.Add(key + " (" + LastRoundTrip + ")");
        }
        Check(broken.Count == 0, $"Zapis i odczyt każdego moba ({Entities.Registries.MobKeys.Count})", string.Join(", ", broken));
        foreach (var key in new[] { "leash_knot", "golem_snowball", "witch_potion" })
            if (EntitySerializer.Create(key, w) == null) broken.Add(key);
        Check(broken.Count == 0, "Encje pomocnicze MO zarejestrowane", string.Join(", ", broken));
        var sheep = new Sheep(w) { Color = Dyes.Black, Sheared = true };
        var sb = RoundTrip(w, sheep, out bool ok) as Sheep;
        Check(ok && sb.Color == Dyes.Black && sb.Sheared, "Zapis i odczyt owcy");
        var vil = new Villager(w, 3); vil.PopulateTrades();
        var vb = RoundTrip(w, vil, out ok) as Villager;
        Check(ok && vb.Profession == 3 && vb.Recipes.Count == vil.Recipes.Count && vb.Career == vil.Career, "Zapis i odczyt wieśniaka z ofertami", LastRoundTrip);
    }
}
