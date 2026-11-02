using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy pionka BL2: skrzynia Endu, doniczka, książki, bariera, statystyki 1.8 i ich sieć.</summary>
public static partial class SelfTest
{
    /// <summary>Handler gracza bez okna: otwiera kontener jak Game.OpenBlockGui.</summary>
    private sealed class Bl2Handler : IPlayerHandler
    {
        public Player P;
        public void OpenBlockGui(GuiKind kind, int x, int y, int z)
        {
            var c = ContainerFactory.Create(P.World, P, kind, x, y, z);
            if (c != null) P.OpenContainer = c;
        }
        public void OnMessage(string msg) { }
        public void OnDeath(string message) { }
        public void OnPickup(Entity item, Entity collector) { }
    }

    private static ItemStack Bl2Craft(params ItemStack[] grid9) => Recipes.Find(grid9, 3, 3);

    private static void TestBl2()
    {
        Console.WriteLine("[BL2]");
        TestBl2Barrier();
        TestBl2EnderChest();
        TestBl2FlowerPot();
        TestBl2Books();
        TestBl2Stats();
        TestBl2InteractionStats();
        TestBl2Net();
    }

    private static void TestBl2InteractionStats()
    {
        var w = MakeWorld(225, WorldType.Superflat, 1);
        var p = new Player(w);
        w.SetBlock(8, 4, 8, B.Cake);
        p.FoodLevel = 20;
        Block.Get(B.Cake).OnActivated(w, 8, 4, 8, p);
        Check(Bl2Stats.Get(p, "stat.cakeSlicesEaten") == 0, "Statystyki: syty gracz nie zalicza kęsa");
        p.FoodLevel = 0;
        for (int i = 0; i < 7; i++) Block.Get(B.Cake).OnActivated(w, 8, 4, 8, p);
        Check(Bl2Stats.Get(p, "stat.cakeSlicesEaten") == 7 && w.GetBlock(8, 4, 8) == B.Air,
            "Statystyki: siedem rzeczywistych kęsów ciasta");
        w.SetBlock(10, 4, 8, B.Jukebox);
        p.Inventory.Selected = new ItemStack(I.RecordCat);
        var record = p.Inventory.Selected;
        bool inserted = Item.ById[record.Id].UseOnBlock(p, record, 10, 4, 8, Dir.Up, .5, 1, .5);
        Check(inserted && Bl2Stats.Get(p, "stat.recordPlayed") == 1, "Statystyki: włożenie płyty zalicza odtworzenie");
        record = new ItemStack(I.RecordCat);
        bool rejected = !Item.ById[record.Id].UseOnBlock(p, record, 10, 4, 8, Dir.Up, .5, 1, .5);
        Check(rejected && Bl2Stats.Get(p, "stat.recordPlayed") == 1, "Statystyki: zajęta szafa nie zalicza drugiej płyty");
        var hostWorld = w;
        w = new World.World(225, WorldType.Superflat, true);
        foreach (var entry in hostWorld.Chunks) w.Chunks[entry.Key] = entry.Value;
        p = new Player(w);
        w.SetBlock(12, 4, 8, B.Cake);
        p.FoodLevel = 0;
        Block.Get(B.Cake).OnActivated(w, 12, 4, 8, p);
        w.SetBlock(14, 4, 8, B.Jukebox);
        Item.ById[record.Id].UseOnBlock(p, record, 14, 4, 8, Dir.Up, .5, 1, .5);
        Check(Bl2Stats.Get(p, "stat.cakeSlicesEaten") == 0 && Bl2Stats.Get(p, "stat.recordPlayed") == 0,
            "Statystyki: klient nie dolicza lokalnie ciasta ani płyt");
    }

    private static void TestBl2Barrier()
    {
        var b = Block.ById[B.Barrier];
        Check(b is BarrierBlock && b.Hardness < 0 && b.BlastResistance >= 3600000 && b.LightOpacity == 0 && b.Render == RenderType.None && !b.InCreative,
            "Bariera: niezniszczalna, przezroczysta dla światła, niewidzialna, poza kreatywnym");
        var drops = new List<ItemStack>(); var w = MakeWorld(220, WorldType.Superflat, 1);
        b.GetDrops(w, 0, 0, 0, 0, w.Rand, new ItemStack(I.DiamondPickaxe), drops);
        var p = new Player(w);
        Check(drops.Count == 0 && PlayerController.BreakDelta(p, b, new ItemStack(I.DiamondPickaxe)) == 0, "Bariera: brak dropu i kopania w survivalu");
        var it = Item.ById[B.Barrier];
        Check(it != null && it.Sprite > 0 && !it.InCreative, "Bariera: przedmiot z ikoną tylko przez /give");
        var col = new List<AABB>(); b.GetCollisionBoxes(w, 0, 0, 0, 0, col);
        Check(col.Count == 1 && col[0].MaxY == 1, "Bariera: pełna kolizja");
    }

    private static void TestBl2EnderChest()
    {
        var b = Block.ById[B.EnderChest];
        Check(b is EnderChestBlock && b.Hardness == 22.5f && b.BlastResistance == 600 && b.LightEmit == 7 && b.Tool == ToolType.Pickaxe,
            "Skrzynia Endu: twardość 22.5, odporność 3000 (600 wg skali wybuchu), światło 7, kilof");
        ItemStack O = new(B.Obsidian), E = new(I.EyeOfEnder);
        Check(Bl2Craft(O, O, O, O, E, O, O, O, O)?.Id == B.EnderChest, "Skrzynia Endu: receptura 8 obsydianu + oko Endu");
        var w = MakeWorld(211, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        var drops = new List<ItemStack>(); b.GetDrops(w, 0, 0, 0, 3, w.Rand, new ItemStack(I.IronPickaxe), drops);
        Check(drops.Count == 1 && drops[0].Id == B.Obsidian && drops[0].Count == 8 && b.SilkTouchStack(3)?.Id == B.EnderChest && !b.CanHarvest(null),
            "Skrzynia Endu: 8 obsydianu, z jedwabnym dotykiem sama skrzynia, ręką nic");

        var p = new Player(w) { Name = "Endu" }; var h = new Bl2Handler { P = p }; p.Handler = h;
        p.SetPositionAndPrev(8.5, 10, 6.5); w.AddEntityNow(p);
        w.SetBlock(8, 10, 8, B.EnderChest, Dir.North); w.SetBlock(12, 10, 8, B.EnderChest, Dir.South);
        var t = w.GetTileEntity(8, 10, 8) as EnderChestTile;
        Check(t != null && b.GetPlacementMeta(w, 8, 10, 8, Dir.Up, 0, 0, 0, new Player(w) { Yaw = 0 }, 0) == Dir.North, "Skrzynia Endu: TE i kierunek przodem do gracza");
        b.OnActivated(w, 8, 10, 8, p);
        Check(p.OpenContainer is RsContainer rc && rc.Inv == p.EnderInventory && rc.Slots.Count == 27 + 36 && t.Viewers == 1 && Bl2Stats.Get(p, "stat.enderchestOpened") == 1,
            "Skrzynia Endu: otwiera wspólny ekwipunek 27 slotów, licznik i statystyka");
        p.OpenContainer.Slots[4].Stack = new ItemStack(I.Diamond, 5);
        for (int i = 0; i < 12; i++) t.Tick(w);
        Check(t.Lid == 1 && t.LidAngle(1) == 1, "Skrzynia Endu: wieko otwiera się w 10 tickach");
        p.CloseContainer(); t.Tick(w);
        Check(t.Viewers == 0 && t.Lid < 1, "Skrzynia Endu: zamknięcie zmniejsza licznik i opuszcza wieko");
        b.OnActivated(w, 12, 10, 8, p);
        var other = w.GetTileEntity(12, 10, 8) as EnderChestTile;
        Check(p.OpenContainer?.Slots[4].Stack?.Count == 5 && other.Viewers == 1, "Skrzynia Endu: ta sama zawartość w każdej skrzyni gracza");
        var p2 = new Player(w) { Name = "Drugi" }; p2.Handler = new Bl2Handler { P = p2 }; p2.SetPositionAndPrev(12.5, 10, 6.5); w.AddEntityNow(p2);
        b.OnActivated(w, 12, 10, 8, p2);
        Check(p2.OpenContainer?.Slots[4].Stack == null && other.Viewers == 2, "Skrzynia Endu: każdy gracz ma własny ekwipunek Endu");
        p2.SetPositionAndPrev(40, 10, 40); other.Tick(w);
        Check(p2.OpenContainer == null && other.Viewers == 1, "Skrzynia Endu: odejście dalej niż 8 bloków zamyka GUI");
        w.SetBlock(12, 10, 8, B.Air);
        Check(p.OpenContainer == null, "Skrzynia Endu: zniszczenie skrzyni zamyka GUI");
        w.SetBlock(8, 11, 8, B.Stone); b.OnActivated(w, 8, 10, 8, p);
        Check(p.OpenContainer == null, "Skrzynia Endu: blok nad skrzynią blokuje otwarcie");
        var copy = new Player(w); copy.FromData(p.ToData());
        Check(copy.EnderInventory.Get(4)?.Id == I.Diamond && copy.EnderInventory.Get(4).Count == 5, "Skrzynia Endu: ekwipunek zapisany w danych gracza");
        t.Viewers = 3; var loaded = ChunkCodec.LoadTile(Bl2.EnderChestTileId, 8, 10, 8, ChunkCodec.SaveTile(t)) as EnderChestTile;
        Check(loaded?.Viewers == 3, "Skrzynia Endu: liczba patrzących w synchronizacji TE");
        int portal = 0; EnderChestBlock.DisplayTick(w, 8, 10, 8, w.Rand, (x, y, z, mx, my, mz) => { if (Math.Abs(x - 8.5) == .25 && Math.Abs(mx) <= 1) portal++; });
        Check(portal == 3, "Skrzynia Endu: 3 cząsteczki portalu na randomDisplayTick");
    }

    private static void TestBl2FlowerPot()
    {
        Check(Bl2Craft(new(I.Brick), null, new(I.Brick), null, new(I.Brick), null, null, null, null)?.Id == I.FlowerPotItem, "Doniczka: receptura z 3 cegieł");
        var w = MakeWorld(218, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        var p = new Player(w); p.SetPositionAndPrev(8.5, 10, 6.5); w.AddEntityNow(p);
        w.SetBlock(8, 9, 8, B.Stone); w.SetBlock(8, 10, 8, B.Air);
        p.Inventory.Selected = new ItemStack(I.FlowerPotItem, 2);
        Check(FlowerPots.Place(p, p.Inventory.Selected, 8, 9, 8, Dir.Up) && w.GetBlock(8, 10, 8) == B.FlowerPot && p.Inventory.Selected.Count == 1,
            "Doniczka: przedmiot stawia blok na stałym podłożu");
        w.SetBlock(9, 9, 8, B.Air);
        Check(!FlowerPots.Place(p, p.Inventory.Selected, 9, 9, 8, Dir.Up) && w.GetBlock(9, 10, 8) == B.Air, "Doniczka: bez podłoża nie da się postawić");
        var pot = w.GetTileEntity(8, 10, 8) as FlowerPotTile;
        var potBlock = Block.ById[B.FlowerPot];
        p.Inventory.Selected = new ItemStack(B.Stone, 3);
        Check(pot != null && pot.Empty && !potBlock.OnActivated(w, 8, 10, 8, p), "Doniczka: kamienia nie da się włożyć");
        p.Inventory.Selected = new ItemStack(B.TallGrass, 3, 1);
        Check(!potBlock.OnActivated(w, 8, 10, 8, p), "Doniczka: zwykła trawa nie, tylko paproć");
        p.Inventory.Selected = new ItemStack(B.Poppy, 3, 3);
        Check(potBlock.OnActivated(w, 8, 10, 8, p) && pot.ItemId == B.Poppy && pot.Data == 3 && p.Inventory.Selected.Count == 2 && Bl2Stats.Get(p, "stat.flowerPotted") == 1,
            "Doniczka: kwiat z meta wkładany PPM, zużyty i policzony");
        p.Inventory.Selected = new ItemStack(B.Dandelion);
        Check(!potBlock.OnActivated(w, 8, 10, 8, p) && pot.ItemId == B.Poppy, "Doniczka: zajęta doniczka nie przyjmuje drugiej rośliny");
        bool all = new[] { (B.Dandelion, 0), (B.Poppy, 8), (B.OakSapling, 0), (B.DarkOakSapling, 0), (B.BrownMushroom, 0), (B.RedMushroom, 0), (B.Cactus, 0), (B.DeadBush, 0), (B.TallGrass, 2) }
            .All(c => FlowerPots.CanContain(c.Item1, c.Item2));
        Check(all, "Doniczka: wszystkie kwiaty, sadzonki, grzyby, kaktus, martwy krzak i paproć");
        var drops = new List<ItemStack>(); potBlock.GetDrops(w, 8, 10, 8, 0, w.Rand, null, drops);
        Check(drops.Count == 2 && drops[0].Id == I.FlowerPotItem && drops[1].Id == B.Poppy && drops[1].Damage == 3, "Doniczka: wypada doniczka i roślina");
        var saved = ChunkCodec.LoadTile(Bl2.FlowerPotTileId, 8, 10, 8, ChunkCodec.SaveTile(pot)) as FlowerPotTile;
        Check(saved is { ItemId: B.Poppy, Data: 3 }, "Doniczka: TE z typem i meta zapisane");
        Check(potBlock.PickStack(0)?.Id == I.FlowerPotItem, "Doniczka: wybór bloku daje przedmiot doniczki");
        w.SetBlock(10, 9, 8, B.Stone); w.SetBlock(10, 10, 8, B.FlowerPot, 7);
        Check(w.GetTileEntity(10, 10, 8) is FlowerPotTile { ItemId: B.RedMushroom }, "Doniczka: stare meta 7 (chatka czarownicy) = czerwony grzyb");
        w.SetBlock(10, 9, 8, B.Air);
        Check(w.GetBlock(10, 10, 8) == B.Air, "Doniczka: usunięcie podłoża zrzuca doniczkę");
    }

    private static void TestBl2Books()
    {
        Check(Bl2Craft(new(I.Book), new(I.Dye, 1, 0), new(I.Feather), null, null, null, null, null, null)?.Id == I.WritableBook, "Książka z piórem: książka + atrament + pióro");
        Check(Bl2Craft(new(I.Book), new(I.Dye, 1, 1), new(I.Feather), null, null, null, null, null, null) == null, "Książka z piórem: inny barwnik nie pasuje");
        Check(Item.ById[I.WritableBook].MaxStack == 1, "Książka z piórem: nie stackuje się");
        var w = MakeWorld(486, WorldType.Superflat, 1);
        var p = new Player(w) { Name = "Autor" };
        p.Inventory.Selected = new ItemStack(I.WritableBook);
        var pages = Enumerable.Range(0, 60).Select(i => i == 0 ? new string('a', 300) : i < 3 ? "strona " + i + "§c" : "").ToList();
        Check(Books.Apply(p, pages, false, null), "Książka: zapis stron");
        var held = Books.Pages(p.Inventory.Selected);
        Check(held.Count == 3 && held[0].Length == Books.MaxPageChars && held[1] == "strona 1c", "Książka: maks. 256 znaków, bez kodów §, puste strony z końca odcięte");
        var many = Enumerable.Range(0, 70).Select(i => "s" + i).ToList(); Books.Apply(p, many, false, null);
        Check(Books.Pages(p.Inventory.Selected).Count == Books.MaxPages, "Książka: maks. 50 stron");
        Check(!Books.Apply(p, many, true, "  "), "Książka: podpis wymaga tytułu");
        Check(Books.Apply(p, new[] { "Pierwsza", "Druga" }, true, "Bardzo dlugi tytul ksiazki"), "Książka: podpisanie");
        var book = p.Inventory.Selected;
        Check(book.Id == I.WrittenBook && Books.Title(book) == "Bardzo dlugi tyt" && Books.Author(book) == "Autor" && Books.Generation(book) == 0 && Books.Pages(book).Count == 2 && Books.ValidWritten(book),
            "Książka: napisana z tytułem do 16 znaków, autorem i stronami");
        Check(book.Name == "Bardzo dlugi tyt" && Books.Lore(book).Contains("autor: Autor") && Books.Lore(book).Contains("Oryginał") && Item.ById[I.WrittenBook].Glint, "Książka: nazwa, opis i połysk");
        Check(!Books.Apply(p, new[] { "x" }, false, null), "Książka: napisanej nie da się edytować");
        ItemStack W() => new(I.WritableBook);
        var grid = new[] { book, W(), W(), null, null, null, null, null, null };
        var copies = Bl2Craft(grid);
        Check(copies is { Id: I.WrittenBook, Count: 2 } && Books.Generation(copies) == 1 && Books.Title(copies) == Books.Title(book) && Recipes.KeepSlotHook(grid, copies) == 0,
            "Książka: kopiowanie, generacja 1, oryginał zostaje w siatce");
        var copy1 = copies.CopyWith(1);
        var copy2 = Bl2Craft(copy1, W(), null, null, null, null, null, null, null);
        Check(Books.Generation(copy2) == 2 && Books.Lore(copy2).Contains("Kopia kopii"), "Książka: kopia kopii (generacja 2)");
        Check(Bl2Craft(copy2.CopyWith(1), W(), null, null, null, null, null, null, null) == null, "Książka: kopii kopii nie da się kopiować");
        Check(Bl2Craft(book, null, null, null, null, null, null, null, null) == null && Bl2Craft(book, book.Copy(), W(), null, null, null, null, null, null) == null, "Książka: bez czystej książki albo z dwoma oryginałami brak wyniku");
        var banner = Banners.MakeItem(1, new[] { ("cre", 4) });
        var bannerGrid = new[] { banner, new ItemStack(I.Banner, 1, 1), null, null, null, null, null, null, null };
        Check(Recipes.KeepSlotHook(bannerGrid, Bl2Craft(bannerGrid)) == 0, "Książka: hak pozostawiania nie psuje kopiowania sztandarów");
    }

    private static void TestBl2Stats()
    {
        var w = MakeWorld(1818, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        var p = new Player(w) { Name = "Licz" }; p.SetPositionAndPrev(8.5, 10, 8.5); w.AddEntityNow(p);
        p.OnGround = true; Bl2Stats.Moved(p, 1, 0, 0);
        p.Sprinting = true; Bl2Stats.Moved(p, .5, 0, 0); p.Sprinting = false;
        p.Sneaking = true; Bl2Stats.Moved(p, 0, 0, .3); p.Sneaking = false;
        Check(Bl2Stats.Get(p, "stat.walkOneCm") == 180 && Bl2Stats.Get(p, "stat.sprintOneCm") == 50 && Bl2Stats.Get(p, "stat.crouchOneCm") == 30,
            "Statystyki: chód obejmuje sprint i skradanie (cm)");
        p.OnGround = false; Bl2Stats.Moved(p, .2, 0, 0); Bl2Stats.Moved(p, .3, 0, 0);
        Check(Bl2Stats.Get(p, "stat.flyOneCm") == 30, "Statystyki: lot liczony powyżej 25 cm na tick");
        Bl2Stats.Fell(p, 1.5f); Bl2Stats.Fell(p, 3.25f);
        Check(Bl2Stats.Get(p, "stat.fallOneCm") == 325, "Statystyki: upadki od 2 bloków");
        p.OnGround = true; p.Jump();
        Check(Bl2Stats.Get(p, "stat.jump") == 1, "Statystyki: skok");
        int t0 = Bl2Stats.Get(p, "stat.playOneMinute"); p.Tick();
        Check(Bl2Stats.Get(p, "stat.playOneMinute") == t0 + 1 && Bl2Stats.Get(p, "stat.timeSinceDeath") > 0, "Statystyki: czas gry i od śmierci co tick");
        p.OnCrafted(new ItemStack(B.CraftingTable)); p.OnCrafted(new ItemStack(I.Stick, 4));
        Check(Bl2Stats.Get(p, Bl2Stats.Craft + B.CraftingTable) == 1 && Bl2Stats.Get(p, Bl2Stats.Craft + I.Stick) == 4, "Statystyki: wytworzone przedmioty z liczbą sztuk");
        var z = Entities.EntitySerializer.Create("zombie", w) as LivingEntity;
        z.SetPositionAndPrev(9.5, 10, 8.5); w.AddEntityNow(z);
        z.Attack(DamageSource.PlayerAttack(p), 1000);
        Check(Bl2Stats.Get(p, "stat.mobKills") == 1 && Bl2Stats.Get(p, Bl2Stats.KillEntity + "zombie") == 1, "Statystyki: zabite moby i zabójstwa zombie");
        var z2 = Entities.EntitySerializer.Create("zombie", w) as LivingEntity; z2.SetPositionAndPrev(9.5, 10, 8.5); w.AddEntityNow(z2);
        float hp = p.Health; p.Attack(new DamageSource("mob", z2), 3);
        Check(Bl2Stats.Get(p, "stat.damageTaken") == (int)Math.Round((hp - p.Health) * 10) && Bl2Stats.Get(p, "stat.damageTaken") > 0, "Statystyki: otrzymane obrażenia w dziesiątych");
        p.HurtTime = 0; p.Attack(new DamageSource("mob", z2), 1000);
        Check(Bl2Stats.Get(p, "stat.deaths") == 1 && Bl2Stats.Get(p, Bl2Stats.KilledBy + "zombie") == 1 && Bl2Stats.Get(p, "stat.timeSinceDeath") == 0,
            "Statystyki: śmierć, zabójca zombie i wyzerowany czas od śmierci");
        Check(Bl2Stats.Format(StatFormat.Time, 72000) == "1.00 godz." && Bl2Stats.Format(StatFormat.Time, 100) == "5.00 s" && Bl2Stats.Format(StatFormat.Distance, 150) == "1.50 m"
              && Bl2Stats.Format(StatFormat.Distance, 49) == "49 cm" && Bl2Stats.Format(StatFormat.Distance, 600000) == "6.00 km" && Bl2Stats.Format(StatFormat.Damage, 25) == "2.50"
              && Bl2Stats.Format(StatFormat.Simple, 1234567) == "1,234,567", "Statystyki: formaty czasu, odległości, obrażeń i liczb jak StatBase");
        var into = new Dictionary<string, int>(); Bl2Stats.Deserialize(Bl2Stats.Serialize(p.Statistics), into);
        Check(into.Count == p.Statistics.Count && into["stat.jump"] == 1, "Statystyki: serializacja do sieci");
        var copy = new Player(w); copy.FromData(p.ToData());
        Check(copy.Statistics.GetValueOrDefault("stat.walkOneCm") == 180, "Statystyki: zapis w danych gracza");
        var remote = new Player(new World.World(0, WorldType.Default, true)); Bl2Stats.Add(remote, "stat.jump");
        Check(Bl2Stats.Get(remote, "stat.jump") == 0, "Statystyki: klient sieciowy nie liczy (liczy host)");
        Check(StatsScreen.Rows(p.Statistics, true).Contains(B.CraftingTable) && StatsScreen.Rows(p.Statistics, false).Contains(I.Stick)
              && StatsScreen.MobRows(p.Statistics).Any(e => e.Key == "zombie") && !StatsScreen.Rows(p.Statistics, true).Contains(B.Stone),
            "Statystyki: zakładki Bloki/Przedmioty/Moby pokazują tylko niezerowe pozycje");
        Bl2Stats.Add(p, Bl2Stats.Use + I.Bread, 5); Bl2Stats.Add(p, Bl2Stats.Use + I.Stick, 2);
        var sorted = StatsScreen.Rows(p.Statistics, false, 2, 1);
        Check(sorted.Count >= 2 && sorted[0] == I.Bread, "Statystyki: sortowanie po kolumnie");
        Check(Bl2Stats.General.Length == 48 && Bl2Stats.General[0].Key == "stat.leaveGame" && Bl2Stats.General[^1].Key == "stat.chestOpened", "Statystyki: 48 ogólnych w kolejności 1.8");
    }

    /// <summary>Host i klient w pamięci: skrzynia Endu gościa, doniczka, książka i statystyki.</summary>
    private static void TestBl2Net()
    {
        var hw = MakeWorld(9218, WorldType.Default, 3);
        using var dimensions = new DimensionSet(hw);
        int gy = 70;
        for (int x = 0; x < 16; x++) for (int z = 0; z < 16; z++) { hw.SetBlock(x, gy, z, B.Stone); for (int y = gy + 1; y < gy + 8; y++) hw.SetBlock(x, y, z, B.Air); }
        hw.SpawnX = 8; hw.SpawnY = gy + 1; hw.SpawnZ = 8;
        var server = new NetServer(hw) { ViewDistance = 2 };
        var (a, bconn) = LoopbackConnection.Pair("Host", "Gosc");
        server.Accept(a);
        var client = NetClient.Start(bconn, "Gosc", 2);
        var host = new TestClientHost();
        bool started = false;
        void Step(int n)
        {
            for (int k = 0; k < n; k++)
            {
                server.Tick(); dimensions.Tick(); server.AfterTick();
                Thread.Sleep(2);
                if (!started && client.PollLogin())
                {
                    var L = client.Login; var cw = new World.World(0, WorldType.Default, true);
                    var pl = new Player(cw) { Name = L.Name, IsLocal = true, Id = L.EntityId };
                    pl.SetGameMode(L.GameMode); pl.SetPositionAndPrev(L.X, L.Y, L.Z);
                    host.World = cw; host.Player = pl; client.Attach(host); started = true;
                }
                if (started && !host.World.EntityById.ContainsKey(host.Player.Id))
                {
                    int pcx = MathUtil.FloorToInt(host.Player.X) >> 4, pcz = MathUtil.FloorToInt(host.Player.Z) >> 4, ready = 0;
                    for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (host.World.GetChunk(pcx + dx, pcz + dz)?.Merged == true) ready++;
                    if (ready == 9) host.World.AddEntityNow(host.Player);
                }
                if (started)
                {
                    client.Tick(); host.World.Tick(); EnderChestTile.TickRemote(host.World);
                    var pl = host.Player;
                    client.SendInput(new NetInput { X = pl.X, Y = pl.Y, Z = pl.Z, Yaw = pl.Yaw, Pitch = pl.Pitch, OnGround = pl.OnGround, Selected = (byte)pl.Inventory.SelectedSlot });
                }
                client.Flush();
                Thread.Sleep(2);
            }
        }
        Step(5);
        if (!started) { Check(false, "BL2 sieć: logowanie", client.Error ?? ""); return; }
        var puppet = server.Clients[0].P;
        Step(40);
        var cw = host.World; var p0 = host.Player;
        void Click(int x, int y, int z, int slot)
            => client.SendInput(new NetInput { X = p0.X, Y = p0.Y, Z = p0.Z, OnGround = true, Selected = (byte)slot, RightPressed = true, HitKind = 1, HitX = x, HitY = y, HitZ = z, HitFace = Dir.Up, HitFx = .5f, HitFy = 1f, HitFz = .5f });

        // skrzynia Endu: okno od hosta z ekwipunkiem Endu gościa, wieko widoczne u klienta
        puppet.EnderInventory.Set(0, new ItemStack(I.Emerald, 9));
        hw.SetBlock(6, gy + 1, 8, B.EnderChest, Dir.South); Step(4);
        Click(6, gy + 1, 8, 0); Step(8);
        var ht = hw.GetTileEntity(6, gy + 1, 8) as EnderChestTile; var ct = cw.GetTileEntity(6, gy + 1, 8) as EnderChestTile;
        Check(puppet.OpenContainer is RsContainer prc && prc.Inv == puppet.EnderInventory && p0.OpenContainer is RsContainer crc && crc.Slots.Count == 63
              && crc.Slots[0].Stack?.Id == I.Emerald && crc.Slots[0].Stack.Count == 9, "BL2 sieć: gość otwiera swój ekwipunek Endu (host autorytatywny)",
            puppet.OpenContainer?.GetType().Name + "/" + p0.OpenContainer?.GetType().Name);
        Check(ht?.Viewers == 1 && ct?.Viewers == 1 && ct.Lid > 0, "BL2 sieć: licznik patrzących i wieko u klienta", $"host {ht?.Viewers} klient {ct?.Viewers} wieko {ct?.Lid}");
        Check(puppet.Statistics.GetValueOrDefault("stat.enderchestOpened") == 1, "BL2 sieć: statystykę liczy host");
        p0.CloseContainer(); Step(15);
        Check(puppet.OpenContainer == null && ht.Viewers == 0 && ct.Viewers == 0, "BL2 sieć: zamknięcie okna gościa opuszcza wieko");

        // doniczka: roślina gościa trafia do TE hosta i wraca do klienta
        hw.SetBlock(6, gy + 1, 8, B.FlowerPot); puppet.Inventory.Main[1] = new ItemStack(B.Poppy, 2, 4); Step(4);
        Click(6, gy + 1, 8, 1); Step(6);
        Check(hw.GetTileEntity(6, gy + 1, 8) is FlowerPotTile { ItemId: B.Poppy, Data: 4 } && cw.GetTileEntity(6, gy + 1, 8) is FlowerPotTile { ItemId: B.Poppy, Data: 4 }
              && puppet.Inventory.Main[1]?.Count == 1, "BL2 sieć: roślina w doniczce u hosta i klienta");

        // książka: strony i podpis wysyłane pakietem, host zamienia trzymaną książkę
        puppet.Inventory.Main[2] = new ItemStack(I.WritableBook); p0.Inventory.SelectedSlot = 2; Step(1);
        client.SendInput(new NetInput { X = p0.X, Y = p0.Y, Z = p0.Z, OnGround = true, Selected = 2 }); Step(4);
        Bl2Net.SendEditBook(client, new[] { "Raz", "Dwa" }, false, ""); Step(4);
        Check(Books.Pages(puppet.Inventory.Main[2]).SequenceEqual(new[] { "Raz", "Dwa" }) && Books.Pages(p0.Inventory.Main[2]).Count == 2, "BL2 sieć: strony książki zapisane przez hosta");
        Bl2Net.SendEditBook(client, new[] { "Raz", "Dwa" }, true, "Kronika"); Step(4);
        Check(puppet.Inventory.Main[2] is { Id: I.WrittenBook } wb && Books.Author(wb) == "Gosc" && Books.Title(wb) == "Kronika" && p0.Inventory.Main[2]?.Id == I.WrittenBook,
            "BL2 sieć: podpis książki u hosta z nickiem gościa");
        puppet.Inventory.Main[2] = new ItemStack(I.Diamond); Step(2);
        Bl2Net.SendEditBook(client, new[] { "hack" }, true, "X"); Step(3);
        Check(puppet.Inventory.Main[2]?.Id == I.Diamond, "BL2 sieć: bez książki z piórem w ręce pakiet odrzucony");

        // statystyki: klient prosi, host odsyła kopię
        int before = StatsScreen.Received;
        Bl2Net.RequestStatistics(client); Step(4);
        Check(StatsScreen.Received > before && p0.Statistics.GetValueOrDefault("stat.enderchestOpened") == 1 && p0.Statistics.GetValueOrDefault("stat.flowerPotted") == 1
              && p0.Statistics.GetValueOrDefault("stat.playOneMinute") > 0, "BL2 sieć: statystyki hosta u klienta");
        client.Disconnect(); Step(3);
        Check(puppet.Statistics.GetValueOrDefault("stat.leaveGame") == 1, "BL2 sieć: wyjście z gry liczone przy rozłączeniu");
    }
}
