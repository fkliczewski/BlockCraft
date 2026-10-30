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

        w.SetBlock(85, y, -80, B.ActivatorRail); w.SetBlock(85, y - 1, -80, B.RedstoneBlock);
        var tnt = (TntMinecartEntity)Cart(MinecartKind.Tnt, 85.5, -79.5); tnt.Tick();
        Check(tnt.Fuse == 79, "Tor aktywacyjny zapala TNT (80 ticków)");
        for (int i = 0; i < 79; i++) tnt.Tick();
        Check(!tnt.Removed && tnt.Fuse == 0, "Wagonik TNT odlicza lont"); tnt.Tick();
        Check(tnt.Removed && w.GetBlock(85, y, -80) == B.ActivatorRail && w.GetBlock(85, y - 1, -80) == B.RedstoneBlock,
            "Wagonik TNT wybucha i chroni tory z podłożem");
        rider.Kill();
    }
}
