using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy logiki modułu OW (receptury, przetapianie, haki użycia itemów, rośliny).</summary>
public static partial class SelfTest
{
    private static ItemStack Sm(int id, int meta) => new(id, 1, meta);

    private static void TestOverworld()
    {
        Console.WriteLine("[Overworld]");
        var r = Recipes.Find(new[] { S(B.SpruceLog), null, null, null }, 2, 2);
        Check(r != null && r.Id == B.SprucePlanks && r.Count == 4, "Świerkowa kłoda -> 4 świerkowe deski");
        var g = new ItemStack[9]; g[0] = S(B.SprucePlanks); g[1] = S(B.SprucePlanks); g[2] = S(B.SprucePlanks);
        r = Recipes.Find(g, 3, 3);
        Check(r != null && r.Id == B.SpruceSlab && r.Count == 6, "Świerkowe deski -> świerkowe płyty", r?.ToString());
        g[0] = S(B.OakPlanks); g[1] = S(B.OakPlanks); g[2] = S(B.OakPlanks);
        r = Recipes.Find(g, 3, 3);
        Check(r != null && r.Id == B.OakSlab, "Dębowe deski -> dębowe płyty", r?.ToString());
        r = Recipes.Find(new[] { S(B.JunglePlanks), null, S(B.DarkOakPlanks), null }, 2, 2);
        Check(r != null && r.Id == I.Stick, "Patyki z mieszanych desek 1.8");
        r = Recipes.Find(new[] { Sm(B.Poppy, 0), null, null, null }, 2, 2);
        Check(r != null && r.Id == I.Dye && r.Damage == 1, "Mak -> czerwony barwnik", r?.ToString());
        r = Recipes.Find(new[] { Sm(I.Dye, 1), Sm(B.Wool, 0), null, null }, 2, 2);
        Check(r != null && r.Id == B.Wool && r.Damage == Dyes.Red, "Barwienie wełny", r?.ToString());
        r = Recipes.Find(new[] { Sm(B.Stone, 3), S(I.Quartz), null, null }, 2, 2);
        Check(r != null && r.Id == B.Stone && r.Damage == 1, "Dioryt + kwarc -> granit", r?.ToString());
        r = Recipes.Find(new[] { Sm(B.Stone, 1), Sm(B.Stone, 1), Sm(B.Stone, 1), Sm(B.Stone, 1) }, 2, 2);
        Check(r != null && r.Id == B.Stone && r.Damage == 2, "Polerowany granit (nie kamienne cegły)", r?.ToString());

        Check(Smelting.Result(Sm(B.Sponge, 1)) is { Id: B.Sponge, Damage: 0 }, "Suszenie mokrej gąbki");
        Check(Smelting.Result(Sm(B.Sponge, 0)) == null, "Sucha gąbka się nie przetapia");
        Check(Smelting.Result(Sm(B.StoneBricks, 0)) is { Id: B.StoneBricks, Damage: 2 }, "Popękane kamienne cegły z pieca");
        Check(Smelting.Result(S(B.Clay))?.Id == B.HardenedClay, "Glina -> utwardzona glina");
        Check(Smelting.Result(S(B.Cactus)) is { Id: I.Dye, Damage: 2 }, "Kaktus -> zielony barwnik");
        Check(Smelting.BurnTime(S(B.AcaciaPlanks)) == 300 && Smelting.BurnTime(S(B.JungleSlab)) == 150, "Paliwo z nowych drewien");
        Check(Item.Get("spruce_door")?.Id == I.SpruceDoorItem, "/give spruce_door daje item drzwi");

        var w = MakeWorld(1, WorldType.Superflat, 2);
        var p = SpawnPlayer(w, 0.5, 4, 8.5);
        p.GameMode = 0;
        // sadzonka + mączka kostna
        w.SetBlock(0, 4, 0, B.OakSapling, 0);
        var meal = new ItemStack(I.BoneMeal, 64);
        for (int i = 0; i < 40 && w.GetBlock(0, 4, 0) == B.OakSapling; i++)
            Item.ById[I.BoneMeal].UseOnBlock(p, meal, 0, 4, 0, Dir.Up, 0.5, 1, 0.5);
        Check(w.GetBlock(0, 4, 0) == B.OakLog, "Mączka kostna wyrasta dąb", w.GetBlock(0, 4, 0).ToString());
        Check(meal.Count < 64, "Mączka kostna zużyta");
        // marchew na polu uprawnym
        w.SetBlock(4, 3, 0, B.Farmland, 0);
        var carrot = new ItemStack(I.Carrot, 2);
        Item.ById[I.Carrot].UseOnBlock(p, carrot, 4, 3, 0, Dir.Up, 0.5, 1, 0.5);
        Check(w.GetBlock(4, 4, 0) == B.Carrots && carrot.Count == 1, "Sadzenie marchewki");
        // warstwy śniegu
        w.SetBlock(6, 4, 0, B.SnowLayer, 0);
        var snow = new ItemStack(B.SnowLayer, 4);
        Item.ById[B.SnowLayer].UseOnBlock(p, snow, 6, 4, 0, Dir.Up, 0.5, 0.125, 0.5);
        Check(w.GetBlock(6, 4, 0) == B.SnowLayer && w.GetMeta(6, 4, 0) == 1, "Dokładanie warstwy śniegu");
        // drzwi świerkowe
        var door = new ItemStack(I.SpruceDoorItem, 1);
        Item.ById[I.SpruceDoorItem].UseOnBlock(p, door, 8, 3, 0, Dir.Up, 0.5, 1, 0.5);
        Check(w.GetBlock(8, 4, 0) == B.SpruceDoor && w.GetBlock(8, 5, 0) == B.SpruceDoor, "Stawianie świerkowych drzwi");
        // kakao na dżunglowym drewnie
        w.SetBlock(10, 4, 0, B.JungleLog, 0);
        Item.ById[I.Dye].UseOnBlock(p, new ItemStack(I.Dye, 1, 3), 10, 4, 0, Dir.South, 0.5, 0.5, 1);
        Check(w.GetBlock(10, 4, 1) == B.Cocoa && Block.Get(B.Cocoa).CanStay(w, 10, 4, 1), "Kakao na dżunglowym drewnie");
        // śnieżka
        int before = w.Entities.Count;
        Item.ById[I.Snowball].UseInAir(p, new ItemStack(I.Snowball, 1));
        Ticks(w, 1);
        Check(w.Entities.Count > before || w.Entities.Any(e => e is SnowballEntity), "Rzut śnieżką");
        // owce: kolory EnumDyeColor
        bool ok = true;
        for (int i = 0; i < 200; i++) { var s = new Sheep(w); if (s.Color is not (0 or 6 or 7 or 8 or 12 or 15)) ok = false; }
        Check(ok, "Kolory owiec jak w 1.8");
        TestOwGenLayers();
        TestOwTerrain();
        TestOwCavesAndPopulation();
        TestOwStructures();
    }
}
