using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestBanners()
    {
        for (int color = 0; color < 16; color++)
        {
            var g = new ItemStack[9];
            for (int i = 0; i < 6; i++) g[i] = new ItemStack(B.Wool, 1, color);
            g[7] = new ItemStack(I.Stick);
            var result = Recipes.Find(g, 3, 3);
            Check(result?.Id == I.Banner && result.Damage == 15 - color && result.Count == 1,
                "Sztandar: receptura wełny kolor " + color);
        }
        var red = Banners.MakeItem(1, new[] { ("cs", 15), ("bo", 4) });
        Check(Item.ById[I.Banner].MaxStack == 16 && Item.ById[I.Banner].Variants.Length == 16,
            "Sztandar: stos 16 i 16 kolorów w kreatywnym");
        var corner = new ItemStack[9]; corner[0] = Dyes.DyeStack(Dyes.Blue); corner[4] = red;
        var patterned = Recipes.Find(corner, 3, 3);
        Check(patterned?.Id == I.Banner && Banners.PatternsOf(patterned).Last() == ("tl", 4)
            && Banners.PatternsOf(red).Count == 2, "Sztandar: wzór narożnika dodany bez zmiany oryginału");
        corner[1] = Dyes.DyeStack(Dyes.Red);
        Check(Recipes.Find(corner, 3, 3) == null, "Sztandar: różne kolory w siatce odrzucone");
        Check(Craft(new ItemStack(I.Banner), new ItemStack(I.SkullItem, 1, 4)) is { } creeper
            && Banners.PatternsOf(creeper).Single() == ("cre", 0), "Sztandar: czaszka creepera bez barwnika daje czarny wzór (1.8)");
        Check(Craft(new ItemStack(I.Banner), new ItemStack(I.SkullItem, 1, 4), Dyes.DyeStack(Dyes.Red)) is { } cr
            && Banners.PatternsOf(cr).Single() == ("cre", 1), "Sztandar: specjalny wzór z barwnikiem");
        var six = Banners.MakeItem(1, Enumerable.Repeat(("cs", 0), 6));
        Check(Craft(six, new ItemStack(B.Vine)) == null, "Sztandar: siódma warstwa odrzucona");
        Check(Craft(six, new ItemStack(I.Banner, 1, 1)) is { } copy && Banners.PatternsOf(copy).Count == 6,
            "Sztandar: można kopiować sześć warstw");
        Check(Craft(red, new ItemStack(I.Banner, 1, 4)) == null, "Sztandar: kopiowanie wymaga tego samego tła");
        var named = red.Copy(); named.GetOrCreateTag().GetOrCreateCompound("display")["Name"] = "Flaga";
        named.GetOrCreateTag().GetOrCreateCompound("BlockEntityTag")["Base"] = 4;
        var namedPattern = Craft(named, new ItemStack(B.Vine));
        Check(namedPattern?.CustomName == "Flaga" && Banners.BaseOf(namedPattern) == 4 && Banners.PatternsOf(named).Count == 2,
            "Sztandar: dodawanie wzoru zachowuje nazwę i Base NBT bez zmiany oryginału");

        var w = MakeWorld(25, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        var p = new Player(w); p.SetPositionAndPrev(8.5, 4, 12.5); w.AddEntityNow(p); p.Yaw = 225;
        var bench = new WorkbenchContainer(p.Inventory, 0, 4, 0);
        bench.CraftGrid.Set(0, red.Copy()); bench.CraftGrid.Set(1, new ItemStack(I.Banner, 2, 1)); bench.UpdateCrafting();
        bench.Slots[0].OnTake(p, bench.CraftResult.Get(0).Copy());
        Check(Banners.PatternsOf(bench.CraftGrid.Get(0)).Count == 2 && bench.CraftGrid.Get(1)?.Count == 1,
            "Sztandar: odbiór kopii zużywa tylko czysty sztandar");

        p.Inventory.Selected = red.CopyWith(2);
        Check(Banners.Place(p, p.Inventory.Selected, 8, 3, 9, Dir.Up) && w.GetBlock(8, 4, 9) == B.StandingBanner
            && w.GetMeta(8, 4, 9) == 2 && p.Inventory.Selected.Count == 1
            && w.GetTileEntity(8, 4, 9) is BannerTile placed && placed.Design == Banners.Design(1, Banners.PatternsOf(red)),
            "Sztandar: postawienie z obrotem i wzorami");
        var tile = (BannerTile)w.GetTileEntity(8, 4, 9);
        var back = ChunkCodec.LoadTile(tile.TypeId, 8, 4, 9, ChunkCodec.SaveTile(tile)) as BannerTile;
        Check(back?.Design == tile.Design, "Sztandar: zapis i format synchronizacji zachowują warstwy");
        w.SetBlock(10, 4, 6, B.Stone);
        Check(Banners.Place(p, red.Copy(), 10, 4, 6, Dir.South) && w.GetBlock(10, 4, 7) == B.WallBanner,
            "Sztandar: montaż ścienny");
        Check(!Banners.Place(p, red.Copy(), 10, 4, 6, Dir.Down), "Sztandar: nie wisi pod sufitem");
        w.SetBlock(10, 4, 6, B.Air); w.Tick();
        Check(w.GetBlock(10, 4, 7) == B.Air && w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.Banner
            && Banners.PatternsOf(e.Stack).Count == 2), "Sztandar: utrata podparcia upuszcza wzory");
        w.SetBlock(5, 4, 5, B.Cauldron, 3); p.Inventory.Selected = red.Copy();
        Check(Banners.Wash(w, 5, 4, 5, p, p.Inventory.Selected) && Banners.PatternsOf(p.Inventory.Selected).Count == 1
            && w.GetMeta(5, 4, 5) == 2, "Sztandar: kocioł usuwa ostatnią warstwę i zużywa wodę");
        Banners.Wash(w, 5, 4, 5, p, p.Inventory.Selected);
        Check(p.Inventory.Selected.Tag == null, "Sztandar: wypranie ostatniej warstwy czyści tag wzorów");
        Check(!Banners.Wash(w, 5, 4, 5, p, p.Inventory.Selected), "Sztandar: czystego nie można prać");
        p.Inventory.Selected = red.CopyWith(2); p.GameMode = 1;
        Banners.Wash(w, 5, 4, 5, p, p.Inventory.Selected);
        Check(p.Inventory.Selected.Count == 2 && Banners.PatternsOf(p.Inventory.Selected).Count == 2
            && p.Inventory.Main.Any(s => s?.Id == I.Banner && Banners.PatternsOf(s).Count == 1),
            "Sztandar: pranie w kreatywnym zachowuje oryginał i daje wypraną kopię");
        var pixels = BannerTextureCache.Composite(1, new[] { ("cs", 15) });
        Check(pixels.Length == 4096 && pixels.Any(px => px >> 24 != 0) && pixels.Distinct().Count() > 3,
            "Sztandar: kompozycja tła i maski tworzy teksturę");
    }
}
