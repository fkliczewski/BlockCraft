using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestRedstone18Rails(World.World w)
    {
        const int x = -70, y = 4, z = 80;
        for (int i = 0; i < 12; i++) w.SetBlock(x + i, y, z, B.PoweredRail);
        w.SetBlock(x - 1, y, z, B.RedstoneBlock);
        Check(Enumerable.Range(0, 9).All(i => (w.GetMeta(x + i, y, z) & 8) != 0) && (w.GetMeta(x + 9, y, z) & 8) == 0,
            "Zasilane tory: sygnał do 8 torów od źródła", string.Join(",", Enumerable.Range(0, 12).Select(i => w.GetMeta(x + i, y, z))));
        w.SetBlock(x - 1, y, z, B.Air);
        Check(Enumerable.Range(0, 12).All(i => (w.GetMeta(x + i, y, z) & 8) == 0), "Zasilane tory gasną po odłączeniu");
        for (int i = 0; i < 4; i++)
        {
            int xx = x + i * 5, zz = z + 5;
            int dx = i is 0 or 3 ? 1 : -1, dz = i < 2 ? 1 : -1;
            w.SetBlock(xx, y, zz, B.Rail);
            w.SetBlock(xx + dx, y, zz, B.Rail);
            w.SetBlock(xx, y, zz + dz, B.Rail);
            Check(w.GetMeta(xx, y, zz) == 6 + i, "Tory: łuk " + (6 + i), w.GetMeta(xx, y, zz).ToString());
        }
        w.SetBlock(x, y, z + 10, B.Rail);
        w.SetBlock(x + 1, y, z + 10, B.Stone);
        w.SetBlock(x + 1, y + 1, z + 10, B.Rail);
        Check(w.GetMeta(x, y, z + 10) == 2, "Tory łączą podjazd ze szczytem", w.GetMeta(x, y, z + 10).ToString());
        var boxes = new List<ModelBox>();
        Block.Get(B.Rail).GetModel(w, x, y, z + 10, 2, boxes);
        Check(boxes.Count == 1 && boxes[0].SlopeX == 1, "Model podjazdu ma ciągłe nachylenie");
        w.SetBlock(x + 1, y, z + 10, B.Air);
        Check(w.GetBlock(x, y, z + 10) == B.Air && w.GetBlock(x + 1, y + 1, z + 10) == B.Air, "Tory wypadają po zabraniu podpory podjazdu");

        // Zwrotnica T: trzy wolne końce, tor dokładany w środku.
        int sx = x + 25, sz = z + 6;
        w.SetBlock(sx, y, sz - 1, B.Rail); w.SetBlock(sx, y, sz + 1, B.Rail); w.SetBlock(sx + 1, y, sz, B.Rail);
        w.SetBlock(sx, y, sz, B.Rail);
        int unpowered = w.GetMeta(sx, y, sz);
        w.SetBlock(sx - 1, y, sz, B.RedstoneBlock);
        int powered = w.GetMeta(sx, y, sz);
        Check(unpowered == 6 && powered == 9, "Zwrotnica przełącza odnogę po zasileniu", $"{unpowered}/{powered}");
        w.SetBlock(sx - 1, y, sz, B.Air);
        Check(w.GetMeta(sx, y, sz) == 6, "Zwrotnica wraca po odłączeniu sygnału");
        TestMinecarts18(w);
        TestRsRecipes();
    }

    private static void TestMinecarts18(World.World w)
    {
        w.Rules.DoMobSpawning = false;
        const int y = 4;
        MinecartEntity Cart(MinecartKind kind, double x, double z)
        {
            var c = MinecartEntity.Create(w, kind); c.SetPositionAndPrev(x, y + 0.0625, z); w.AddEntityNow(c); return c;
        }
        for (int x = 0; x < 24; x++)
        {
            w.SetBlock(x, y, -80, B.PoweredRail);
            w.SetBlock(x, y - 1, -80, B.RedstoneBlock);
            w.SetBlock(x, y, -76, B.PoweredRail);
            w.SetBlock(x, y, -72, B.Rail);
        }
        var fast = Cart(MinecartKind.Rideable, 2.5, -79.5); fast.MotionX = 0.05;
        double maxStep = 0;
        for (int i = 0; i < 20; i++) { double old = fast.X; fast.PrevTick(); fast.Tick(); maxStep = Math.Max(maxStep, fast.X - old); }
        Check(fast.X > 8 && maxStep <= 0.400001 && maxStep > 0.39, "Wagonik przyspiesza i osiąga 0.4 bloku/tick", $"x={fast.X} max={maxStep}");
        fast.Kill();
        var braking = Cart(MinecartKind.Rideable, 2.5, -75.5); braking.MotionX = 0.4;
        for (int i = 0; i < 12; i++) { braking.PrevTick(); braking.Tick(); }
        Check(Math.Abs(braking.MotionX) < 0.000001 && braking.X - 2.5 < 0.5, "Niezasilany tor hamuje wagonik", $"x={braking.X} v={braking.MotionX}"); braking.Kill();
        var empty = Cart(MinecartKind.Rideable, 2.5, -71.5); empty.MotionX = 0.4; empty.PrevTick(); empty.Tick();
        Check(Math.Abs(empty.MotionX - 0.4 * 0.9599999785423279) < 0.00001, "Pusty wagonik: opór 0.96"); empty.Kill();

        var rider = SpawnPlayer(w, 2.5, 4, -70); rider.SetGameMode(0);
        var ridden = Cart(MinecartKind.Rideable, 2.5, -71.5);
        ridden.Interact(rider); ridden.MotionX = 0.4;
        ridden.PrevTick(); ridden.Tick(); rider.Tick();
        Check(rider.RidingEntity == ridden && ridden.Passenger == rider && Math.Abs(rider.X - ridden.X) < 0.001 && Math.Abs(ridden.MotionX - 0.4 * 0.996999979019165) < 0.00001,
            "PPM: pasażer jedzie z wagonikiem (opór 0.997)");
        rider.Sneaking = true; rider.Tick(); rider.Sneaking = false;
        Check(rider.RidingEntity == null && ridden.Passenger == null, "Shift: gracz wysiada");
        var cow = new Cow(w); cow.SetPosition(ridden.X + 0.1, ridden.Y, ridden.Z); w.SpawnEntity(cow);
        ridden.MotionX = 0.2; ridden.Collide(cow);
        Check(cow.RidingEntity == ridden, "Jadący wagonik zabiera moba");
        cow.Mount(null); cow.Kill(); ridden.Kill(); rider.SetPosition(0, 4, -100);

        int loopz = -64;
        for (int x = 1; x <= 7; x++) { w.SetBlock(x, y, loopz, B.Rail); w.SetBlock(x, y, loopz + 6, B.Rail); }
        for (int z = 1; z < 6; z++) { w.SetBlock(1, y, loopz + z, B.Rail); w.SetBlock(7, y, loopz + z, B.Rail); }
        var curved = Cart(MinecartKind.Rideable, 6.5, loopz + 0.5); curved.MotionX = 0.4;
        for (int i = 0; i < 10; i++) { curved.PrevTick(); curved.Tick(); }
        Check(curved.Z > loopz + 1.5 && Math.Abs(curved.X - 7.5) < 0.01 && curved.MotionZ > 0, "Wagonik przejeżdża zakręt 90 stopni", $"{curved.X}/{curved.Z}"); curved.Kill();

        for (int x = 30; x <= 34; x++)
        {
            int rise = x <= 30 ? 0 : x == 31 ? 1 : 2;
            for (int h = 0; h < rise; h++) w.SetBlock(x, y + h, -80, B.Stone);
            w.SetBlock(x, y + rise, -80, B.Rail);
        }
        var uphill = Cart(MinecartKind.Rideable, 30.05, -79.5); uphill.MotionX = 0.7;
        for (int i = 0; i < 10; i++) { uphill.PrevTick(); uphill.Tick(); }
        Check(uphill.Y > 5.8 && uphill.X > 32, "Wagonik wjeżdża na podjazd", $"{uphill.X}/{uphill.Y}"); uphill.Kill();

        w.SetBlock(40, y, -80, B.DetectorRail);
        var cargo = (ChestMinecartEntity)Cart(MinecartKind.Chest, 40.5, -79.5);
        for (int i = 0; i < cargo.Size; i++) cargo.Set(i, new ItemStack(B.Cobblestone, 64));
        cargo.Tick();
        Check((w.GetMeta(40, y, -80) & 8) != 0 && ComparatorRead.Read(w, 40, y, -80) == 15, "Tor detektorowy: wagonik ze skrzynią = sygnał i komparator 15");
        cargo.Kill(); Ticks(w, 21);
        Check((w.GetMeta(40, y, -80) & 8) == 0, "Tor detektorowy gaśnie po odjeździe");
        var stone = new ItemEntity(w, 40.5, 4.1, -79.5, new ItemStack(B.Stone)); w.SpawnEntity(stone);
        Block.Get(B.DetectorRail).OnEntityCollide(w, 40, y, -80, stone);
        Check((w.GetMeta(40, y, -80) & 8) == 0, "Tor detektorowy ignoruje przedmioty"); stone.Kill();

        w.SetBlock(45, y, -80, B.Rail); w.SetBlock(45, y + 1, -80, B.Chest);
        ((ChestTileEntity)w.GetTileEntity(45, y + 1, -80)).Set(0, new ItemStack(B.Dirt, 4));
        var hopper = (HopperMinecartEntity)Cart(MinecartKind.Hopper, 45.5, -79.5);
        hopper.Tick();
        Check(hopper.Get(0)?.Id == B.Dirt && hopper.Get(0)?.Count == 1, "Wagonik z lejem pobiera z kontenera");
        for (int i = 0; i < 4; i++) hopper.Tick();
        Check(hopper.Get(0)?.Count == 2, "Wagonik z lejem pobiera co 4 ticki");
        w.SetBlock(45, y, -80, B.ActivatorRail); w.SetBlock(45, y - 1, -80, B.RedstoneBlock);
        hopper.Tick(); int countBefore = hopper.Get(0)?.Count ?? 0;
        for (int i = 0; i < 8; i++) hopper.Tick();
        Check(!hopper.Enabled && hopper.Get(0)?.Count == countBefore, "Tor aktywacyjny blokuje pobieranie wagonika");
        w.SetBlock(45, y - 1, -80, B.Stone); hopper.Tick();
        Check(hopper.Enabled, "Niezasilany tor aktywacyjny włącza lej"); hopper.Kill();

        w.SetBlock(50, y, -80, B.Rail);
        var furnace = (FurnaceMinecartEntity)Cart(MinecartKind.Furnace, 50.5, -79.5);
        rider.SetPosition(49, 4, -79.5); rider.Inventory.Main[rider.Inventory.SelectedSlot] = new ItemStack(I.Coal, 2);
        furnace.Interact(rider); furnace.Tick();
        Check(furnace.Fuel == 3599 && rider.Inventory.Selected.Count == 1 && furnace.MotionX > 0.9 && furnace.MaximumSpeed == 0.2, "Wagonik z piecem: węgiel = 3600 ticków, napęd i limit 0.2"); furnace.Kill();

        w.SetBlock(55, y, -80, B.ActivatorRail); w.SetBlock(55, y - 1, -80, B.RedstoneBlock);
        var eject = Cart(MinecartKind.Rideable, 55.5, -79.5); rider.Sneaking = false; eject.Interact(rider); eject.Tick();
        Check(eject.Passenger == null && rider.RidingEntity == null, "Zasilany tor aktywacyjny wysadza pasażera"); eject.Kill();

        var named = (ChestMinecartEntity)Cart(MinecartKind.Chest, 60.5, -79.5);
        named.CustomName = "Transport"; named.Set(2, new ItemStack(I.Diamond, 7) { Tag = new Nbt { ["test"] = 42 } }); named.MotionX = 0.2;
        using (var ms = new MemoryStream())
        {
            using var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true); named.Save(bw); ms.Position = 0;
            var loaded = new ChestMinecartEntity(w); loaded.Load(new BinaryReader(ms));
            Check(loaded.CustomName == "Transport" && loaded.Get(2)?.Count == 7 && loaded.Get(2).Tag.GetInt("test") == 42 && loaded.MotionX == 0.2,
                "Zapis wagonika zachowuje nazwę, ruch, przedmioty i NBT");
        }
        named.Kill();

        w.SetBlock(65, y, -80, B.Rail);
        var item = (MinecartItem)Item.ById[I.Minecart]; rider.SetGameMode(0); rider.Inventory.Main[rider.Inventory.SelectedSlot] = new ItemStack(I.Minecart);
        int cartsBefore = w.Entities.OfType<MinecartEntity>().Count(c => !c.Removed);
        bool placed = item.UseOnBlock(w, rider, rider.Inventory.Selected, 65, y, -80, Dir.Up);
        w.Tick(); // SpawnEntity dodaje encje na końcu ticku świata.
        Check(placed && rider.Inventory.Selected == null && w.Entities.OfType<MinecartEntity>().Count(c => !c.Removed) == cartsBefore + 1, "Przedmiot stawia wagonik na torach i znika z ręki");
        Check(!item.UseOnBlock(w, rider, new ItemStack(I.Minecart), 65, y, -81, Dir.Up), "Wagonika nie można postawić bez toru");
        w.SetBlock(67, y, -80, B.Dispenser, Dir.East); w.SetBlock(68, y, -80, B.Rail);
        var disp = (DispenserTileEntity)w.GetTileEntity(67, y, -80); disp.Set(0, new ItemStack(I.ChestMinecart));
        w.SetBlock(66, y, -80, B.RedstoneBlock); Ticks(w, 6);
        Check(disp.Get(0) == null && w.Entities.OfType<ChestMinecartEntity>().Any(c => !c.Removed && c.X > 68 && c.X < 69), "Dozownik stawia wagonik na torze przed nim");

        var broken = Cart(MinecartKind.Chest, 74.5, -79.5); ((IInventory)broken).Set(0, new ItemStack(I.Diamond, 3));
        broken.Attack(DamageSource.PlayerAttack(rider), 5);
        w.Tick();
        var drops = w.GetEntitiesInBox(new AABB(73, 3, -81, 76, 6, -78)).OfType<ItemEntity>().ToList();
        Check(broken.Removed && drops.Any(d => d.Stack.Id == I.Minecart) && drops.Any(d => d.Stack.Id == B.Chest) && drops.Where(d => d.Stack.Id == I.Diamond).Sum(d => d.Stack.Count) == 3,
            "Zniszczony wagonik upuszcza wagonik, skrzynię i zawartość");
        foreach (var c in w.Entities.OfType<MinecartEntity>().ToArray()) if (!c.Removed) c.Kill();

        var collisionA = new MinecartEntity(w) { MotionX = 0.3, Yaw = 0 };
        var collisionB = new MinecartEntity(w); collisionB.SetPosition(0.8, 0, 0);
        collisionA.Collide(collisionB);
        Check(collisionB.MotionX > 0.1 && collisionA.MotionX < 0.3, "Zderzenie wagoników przekazuje pęd");
        var transverse = new MinecartEntity(w) { MotionX = 0.3, Yaw = 0 };
        var beside = new MinecartEntity(w); beside.SetPosition(0, 0, 0.8); transverse.Collide(beside);
        Check(transverse.MotionX == 0.3 && beside.MotionX == 0 && beside.MotionZ == 0, "Wagoniki na równoległych torach nie zderzają się bokami");
        var locomotive = new FurnaceMinecartEntity(w) { MotionX = 0.2 }; var wagon = new MinecartEntity(w); wagon.SetPosition(0.8, 0, 0);
        locomotive.Collide(wagon);
        Check(wagon.MotionX > 0.2 && locomotive.MotionX > 0.18, "Wagonik z piecem pcha zwykły wagonik");
        var occupant = new Cow(w); var occupied = new MinecartEntity(w); occupant.Mount(occupied);
        Check(!rider.Mount(occupied) && occupied.Passenger == occupant && rider.RidingEntity == null, "Zajęty wagonik nie nadpisuje pasażera"); occupant.Mount(null);
        Check(!new TntMinecartEntity(w).Interact(rider) && rider.RidingEntity == null, "Wagonik TNT nie przyjmuje pasażera");

        w.Rules.Set("doEntityDrops", "false", out _);
        var noDrops = Cart(MinecartKind.Rideable, 78.5, -84.5); noDrops.Attack(DamageSource.Generic, 5); w.Tick();
        Check(noDrops.Removed && !w.GetEntitiesInBox(new AABB(77, 3, -86, 80, 6, -83)).OfType<ItemEntity>().Any(), "doEntityDrops=false wyłącza drop wagonika");
        w.Rules.DoEntityDrops = true;
        var savedFurnace = new FurnaceMinecartEntity(w) { Fuel = 1234, PushX = 0.75, PushZ = -0.25 };
        var savedTnt = new TntMinecartEntity(w) { Fuse = 37 };
        var savedHopper = new HopperMinecartEntity(w) { Enabled = false, TransferCooldown = 3 };
        savedHopper.Set(1, new ItemStack(I.Diamond, 9));
        var passengerDataSource = new Player(w); passengerDataSource.Mount(savedFurnace);
        var loadedWorld = new World.World(42, WorldType.Superflat, false); loadedWorld.Rules.DoMobSpawning = false;
        EntitySerializer.LoadEntities(loadedWorld, EntitySerializer.SaveEntities(new List<Entity> { savedFurnace, savedTnt, savedHopper })); loadedWorld.Tick();
        var restoredFurnace = loadedWorld.Entities.OfType<FurnaceMinecartEntity>().Single();
        var restoredTnt = loadedWorld.Entities.OfType<TntMinecartEntity>().Single();
        var restoredHopper = loadedWorld.Entities.OfType<HopperMinecartEntity>().Single();
        Check(restoredFurnace.Fuel == 1234 && restoredFurnace.PushX == 0.75 && restoredFurnace.PushZ == -0.25 && restoredTnt.Fuse == 37 && !restoredHopper.Enabled && restoredHopper.TransferCooldown == 3 && restoredHopper.Get(1)?.Count == 9,
            "Rejestr i zapis encji odtwarzają paliwo, lont, blokadę i ładunek");
        var restoredPassenger = new Player(loadedWorld); restoredPassenger.FromData(passengerDataSource.ToData()); loadedWorld.AddEntityNow(restoredPassenger);
        restoredPassenger.Tick();
        Check(restoredPassenger.RidingEntity == restoredFurnace && restoredFurnace.Passenger == restoredPassenger, "Po wczytaniu gracz ponownie dosiada zapisanego wagonika");

        w.SetBlock(85, y, -80, B.ActivatorRail); w.SetBlock(85, y - 1, -80, B.RedstoneBlock);
        var tnt = (TntMinecartEntity)Cart(MinecartKind.Tnt, 85.5, -79.5); tnt.Tick();
        Check(tnt.Fuse == 79, "Tor aktywacyjny zapala TNT (80 ticków)");
        for (int i = 0; i < 79; i++) tnt.Tick();
        Check(!tnt.Removed && tnt.Fuse == 0, "Wagonik TNT odlicza lont"); tnt.Tick();
        Check(tnt.Removed && w.GetBlock(85, y, -80) == B.ActivatorRail && w.GetBlock(85, y - 1, -80) == B.RedstoneBlock,
            "Wagonik TNT wybucha i chroni tory z podłożem");
        rider.Kill();
    }

    private static void TestRsRecipes()
    {
        void Craft(string name, int expected, int count, params int[] ids)
        {
            var grid = ids.Select(id => id == 0 ? null : new ItemStack(id)).ToArray();
            var result = Recipes.Find(grid, 3, 3);
            Check(result?.Id == expected && result?.Count == count, name, result?.ToString() ?? "brak");
        }
        Craft("Receptura: 16 torów", B.Rail, 16, I.IronIngot, 0, I.IronIngot, I.IronIngot, I.Stick, I.IronIngot, I.IronIngot, 0, I.IronIngot);
        Craft("Receptura: 6 zasilanych torów", B.PoweredRail, 6, I.GoldIngot, 0, I.GoldIngot, I.GoldIngot, I.Stick, I.GoldIngot, I.GoldIngot, I.Redstone, I.GoldIngot);
        Craft("Receptura: 6 torów detektorowych", B.DetectorRail, 6, I.IronIngot, 0, I.IronIngot, I.IronIngot, B.StonePressurePlate, I.IronIngot, I.IronIngot, I.Redstone, I.IronIngot);
        Craft("Receptura: 6 torów aktywacyjnych", B.ActivatorRail, 6, I.IronIngot, I.Stick, I.IronIngot, I.IronIngot, B.RedstoneTorch, I.IronIngot, I.IronIngot, I.Stick, I.IronIngot);
        Craft("Receptura: wagonik", I.Minecart, 1, 0, 0, 0, I.IronIngot, 0, I.IronIngot, I.IronIngot, I.IronIngot, I.IronIngot);
        foreach (var (block, item) in new[] { (B.Chest, I.ChestMinecart), (B.Furnace, I.FurnaceMinecart), (B.Tnt, I.TntMinecart), (B.Hopper, I.HopperMinecart) })
            Craft("Receptura: " + Item.ById[item].Name, item, 1, 0, block, 0, 0, 0, 0, 0, 0, I.Minecart);
        Craft("Receptura: 3 żelazne drzwi", I.IronDoorItem, 3, I.IronIngot, I.IronIngot, 0, I.IronIngot, I.IronIngot, 0, I.IronIngot, I.IronIngot, 0);
        Craft("Receptura: komparator", I.ComparatorItem, 1, 0, B.RedstoneTorch, 0, B.RedstoneTorch, I.Quartz, B.RedstoneTorch, B.Stone, B.Stone, B.Stone);
        Craft("Receptura: lej", B.Hopper, 1, I.IronIngot, 0, I.IronIngot, I.IronIngot, B.Chest, I.IronIngot, 0, I.IronIngot, 0);
        Craft("Receptura: czujnik z mieszanymi płytami drewna", B.DaylightSensor, 1, B.Glass, B.Glass, B.Glass, I.Quartz, I.Quartz, I.Quartz, B.OakSlab, B.BirchSlab, B.DarkOakSlab);
        Craft("Receptura: klapa z mieszanych desek", B.Trapdoor, 2, B.OakPlanks, B.BirchPlanks, B.SprucePlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks, 0, 0, 0);
        Craft("Receptura: lepki tłok ze szlamem", B.StickyPiston, 1, 0, I.Slimeball, 0, 0, B.Piston, 0, 0, 0, 0);
        Check(Recipes.Find(new ItemStack[] { new(B.Piston), new(I.String), null, null }, 2, 2) == null, "Nić nie zastępuje kulki szlamu w lepkim tłoku");
        Craft("Receptura: lampa z jasnogłazem", B.RedstoneLamp, 1, 0, I.Redstone, 0, I.Redstone, B.Glowstone, I.Redstone, 0, I.Redstone, 0);
        Craft("Receptura: odzyskanie 9 kulek szlamu", I.Slimeball, 9, 0, 0, 0, 0, B.SlimeBlock, 0, 0, 0, 0);
        var transport = ItemRegistry.CreativeItems(Blocks.TabTransport);
        Check(transport.Count == 9 && transport.Select(s => s.Id).Contains(I.HopperMinecart) && transport.All(s => s.Id is >= B.Rail and <= B.ActivatorRail || s.Item.MaxStack == 1),
            "Transport zawiera 4 tory i 5 niełączących się w stos wagoników", transport.Count.ToString());
    }
}
