using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Autotest modułu MO: BlockCraft.exe --autotest --mo (galerie mobów i GUI do autotest/mo_*.png, wyniki w logu "MO-TEST").</summary>
public sealed partial class Game
{
    private readonly List<(Entity e, double x, double y, double z, float yaw)> _moPose = new();
    private int _moX, _moY, _moZ;

    private T MoAdd<T>(T e, double dx, double dy, double dz, float yaw = 180) where T : Entity
    {
        double x = _moX + .5 + dx, y = _moY + dy, z = _moZ + .5 + dz;
        e.SetPositionAndPrev(x, y, z);
        World.SpawnEntity(e);
        _moPose.Add((e, x, y, z, yaw));
        return e;
    }

    /// <summary>Ustawia moby z powrotem na miejsca tuż przed zrzutem (AI nie da się wyłączyć, a w kilka klatek prawie nie zdążą się ruszyć).</summary>
    private void MoRepose()
    {
        foreach (var (e, x, y, z, yaw) in _moPose)
        {
            if (e is Mob m && m.Vehicle != null) continue;
            e.SetPositionAndPrev(x, y, z); e.MotionX = e.MotionY = e.MotionZ = 0; e.FireTicks = 0;
            e.Yaw = e.PrevYaw = yaw;
            if (e is LivingEntity l) { l.BodyYaw = l.PrevBodyYaw = l.HeadYaw = l.PrevHeadYaw = yaw; l.Pitch = l.PrevPitch = 0; }
        }
    }

    private void MoClear()
    {
        foreach (var e in World.Entities.ToList()) if (e is not Entities.Player) e.Removed = true;
        _moPose.Clear();
    }

    /// <summary>Galeria: moby w rzędzie przed graczem, kamera cofnięta o back bloków.</summary>
    private void MoGallery(string name, Action spawn, double back = 7, float pitch = 12, double camY = 1.2)
    {
        Step(20, () =>
        {
            MoClear(); spawn();
            Player.SetPositionAndPrev(_moX + .5, _moY + camY, _moZ + .5 - back); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = pitch;
        });
        Step(3, MoRepose);
        Step(2, () => { Shot(name); Log.Info($"MO-TEST zrzut {name}: {_moPose.Count} encji, {string.Join(",", _moPose.Select(p => p.e.TypeKey))}"); });
    }

    private static Horse MoHorse(World.World w, int type, int variant, int armor = 0, bool saddle = false, bool chest = false)
    {
        var h = new Horse(w, type) { Variant = variant, Tamed = true, Chested = chest };
        if (saddle) h.Inv.Set(0, new ItemStack(I.Saddle));
        if (armor != 0) h.Inv.Set(1, new ItemStack(armor));
        h.Inv.Changed();
        return h;
    }

    private void SetupAutotestMobs()
    {
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(5, () => CreateWorld("AutoTestMO " + DateTime.Now.ToString("HHmmss"), 4242, 1, false, WorldType.Superflat, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        Step(20, () =>
        {
            World.DayTime = 6000; World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false;
            _moX = MathUtil.FloorToInt(Player.X); _moY = MathUtil.FloorToInt(Player.Y); _moZ = MathUtil.FloorToInt(Player.Z);
            Player.Flying = true; HideGui = true;
            Log.Info($"MO-TEST plac {_moX} {_moY} {_moZ}");
        });
        var w = (Func<World.World>)(() => World);

        MoGallery("mo_01_farma", () =>
        {
            MoAdd(new Pig(w()) { Saddled = true }, -6, 0, 0); MoAdd(new Cow(w()), -3.5, 0, 0); MoAdd(new Mooshroom(w()), -0.5, 0, 0);
            MoAdd(new Sheep(w()) { Color = Dyes.White }, 2.5, 0, 0); MoAdd(new Chicken(w()), 5, 0, 0);
        }, 8);
        MoGallery("mo_02_owce", () =>
        {
            int[] colors = { Dyes.Black, Dyes.Gray, Dyes.LightGray, Dyes.Brown, Dyes.Pink, Dyes.Red, Dyes.Blue };
            for (int i = 0; i < colors.Length; i++) MoAdd(new Sheep(w()) { Color = colors[i] }, -6 + i * 2, 0, 0);
            MoAdd(new Sheep(w()) { Sheared = true }, 8, 0, 0);
        }, 9);
        MoGallery("mo_03_mlode", () =>
        {
            MoAdd(new Pig(w()) { GrowingAge = -24000 }, -5, 0, 0); MoAdd(new Cow(w()) { GrowingAge = -24000 }, -3, 0, 0);
            MoAdd(new Sheep(w()) { GrowingAge = -24000, Color = Dyes.Pink }, -1, 0, 0); MoAdd(new Chicken(w()) { GrowingAge = -24000 }, 1, 0, 0);
            MoAdd(new Villager(w(), 0) { GrowingAge = -24000 }, 3, 0, 0); MoAdd(new Zombie(w()) { Baby = true }, 5, 0, 0);
        }, 6, 15, 0.8);
        MoGallery("mo_04_wilki_koty", () =>
        {
            MoAdd(new Wolf(w()), -6, 0, 0);
            MoAdd(new Wolf(w()) { Angry = true }, -4, 0, 0);
            var tw = MoAdd(new Wolf(w()) { Tamed = true, Sitting = true, Collar = Dyes.Blue, Owner = Player.Name }, -2, 0, 0); tw.MaxHealth = tw.Health = 20;
            MoAdd(new Ocelot(w()), 0.5, 0, 0);
            for (int c = 1; c <= 3; c++) MoAdd(new Ocelot(w()) { CatType = c, Tamed = true, Sitting = c == 2, Owner = Player.Name }, 0.5 + c * 2, 0, 0);
        }, 7, 15, 0.8);
        MoGallery("mo_05_kroliki", () =>
        {
            for (int t = 0; t <= 5; t++) MoAdd(new Rabbit(w()) { RabbitType = t }, -5 + t * 1.6, 0, 0);
            MoAdd(new Rabbit(w()) { RabbitType = 99 }, 5, 0, 0);
        }, 5, 20, 0.6);
        MoGallery("mo_06_konie", () =>
        {
            MoAdd(MoHorse(w(), 0, 0 | (0 << 8), I.DiamondHorseArmor, true), -7, 0, 0);
            MoAdd(MoHorse(w(), 0, 3 | (1 << 8), I.GoldHorseArmor), -4, 0, 0);
            MoAdd(MoHorse(w(), 0, 5 | (4 << 8)), -1, 0, 0);
            MoAdd(MoHorse(w(), 1, 0, 0, true, true), 2, 0, 0);
            MoAdd(MoHorse(w(), 2, 0), 4.5, 0, 0);
            MoAdd(MoHorse(w(), 3, 0), 7, 0, 0);
            MoAdd(MoHorse(w(), 4, 0), 9.5, 0, 0);
        }, 11, 8, 1.6);
        MoGallery("mo_07_kon_z_bliska", () =>
        {
            MoAdd(MoHorse(w(), 0, 1 | (2 << 8), I.IronHorseArmor, true), 0, 0, 0, 150);
        }, 4, 12, 1.3);
        MoGallery("mo_08_wiesniacy", () =>
        {
            for (int p = 0; p < 5; p++) MoAdd(new Villager(w(), p), -5 + p * 2.2, 0, 0);
            MoAdd(new Witch(w()), 6, 0, 0);
        }, 7, 8, 1.3);
        MoGallery("mo_09_golemy", () =>
        {
            MoAdd(new IronGolem(w()), -3, 0, 0); MoAdd(new SnowGolem(w()), 0.5, 0, 0); MoAdd(new Zombie(w()) { Villager = true }, 3, 0, 0);
            MoAdd(new Villager(w(), 3), 5.5, 0, 0);
        }, 8, 5, 1.6);
        MoGallery("mo_10_potwory", () =>
        {
            MoAdd(new Zombie(w()), -7, 0, 0); MoAdd(new Skeleton(w()), -5, 0, 0); MoAdd(new Creeper(w()), -3, 0, 0);
            MoAdd(new Creeper(w()) { Charged = true }, -1, 0, 0); MoAdd(new Spider(w()), 1.5, 0, 0); MoAdd(new CaveSpider(w()), 4, 0, 0);
            MoAdd(new Enderman(w()), 6.5, 0, 0);
        }, 10, 6, 1.6);
        MoGallery("mo_11_potwory2", () =>
        {
            var s1 = MoAdd(new Slime(w()), -6, 0, 0); s1.SetSize(1);
            var s2 = MoAdd(new Slime(w()), -4.5, 0, 0); s2.SetSize(2);
            var s4 = MoAdd(new Slime(w()), -1.5, 0, 0); s4.SetSize(4);
            MoAdd(new Silverfish(w()), 1, 0, 0); MoAdd(new Guardian(w()), 3, 0, 0); MoAdd(new Guardian(w(), true), 7, 0, 0);
        }, 9, 8, 1.6);
        MoGallery("mo_12_dzokeje", () =>
        {
            MoAdd(new ChickenJockey(w()), -2, 0, 0); MoAdd(new SpiderJockey(w()), 2, 0, 0);
        }, 5, 8, 1.4);
        MoGallery("mo_13_gigant", () => MoAdd(new Giant(w()), 0, 0, 0), 22, -12, 2);
        MoGallery("mo_14_woda", () =>
        {
            for (int dx = -6; dx <= 6; dx++) for (int dz = -3; dz <= 3; dz++) for (int dy = -3; dy <= -1; dy++) World.SetBlock(_moX + dx, _moY + dy, _moZ + dz, B.Water, 0);
            MoAdd(new Squid(w()), -2, -2, 0); MoAdd(new Guardian(w()), 2, -2.2, 0);
        }, 5, 0, -2.4);
        MoGallery("mo_14b_nietoperz", () => MoAdd(new Bat(w()), 0, 1.5, 0), 3, -10, 1.2);
        Step(5, () => { for (int dx = -6; dx <= 6; dx++) for (int dz = -3; dz <= 3; dz++) for (int dy = -3; dy <= -1; dy++) World.SetBlock(_moX + dx, _moY + dy, _moZ + dz, dy == -1 ? B.Grass : B.Dirt, 0); });
        MoGallery("mo_15_smycze", () =>
        {
            World.SetBlock(_moX + 3, _moY, _moZ, B.Fence, 0);
            var knot = new LeashKnot(World); knot.SetPositionAndPrev(_moX + 3.5, _moY + .125, _moZ + .5); World.SpawnEntity(knot);
            MoAdd(new Cow(w()), 5, 0, 1.5).LeashHolder = knot;
            MoAdd(new Sheep(w()) { Color = Dyes.Lime }, 1, 0, 2).LeashHolder = knot;
            MoAdd(new Pig(w()), -2, 0, -3.5).LeashHolder = Player;
        }, 6, 15, 1.4);

        // jazda: koń w trzeciej osobie i świnia z marchewką
        Horse ride = null;
        Step(20, () =>
        {
            MoClear();
            ride = MoHorse(World, 0, 2 | (3 << 8), I.DiamondHorseArmor, true); ride.Owner = Player.Name;
            ride.SetPositionAndPrev(_moX + .5, _moY, _moZ + .5); World.SpawnEntity(ride);
            Player.Flying = false;
        });
        Step(5, () => { Player.Inventory.Selected = null; Player.Sneaking = false; MoCheck("wsiadanie na konia", ride.Interact(Player) && Player.RidingMob == ride); CameraMode = 1; Player.Yaw = 30; Player.Pitch = 15; });
        Step(20, () => Shot("mo_16_jazda_kon"));
        Step(5, () => { ride.Dismount(); CameraMode = 0; Player.Flying = true; });

        // GUI konia (koń ze zbroją) i osła ze skrzynią
        Horse gh = null;
        Step(10, () =>
        {
            HideGui = false;
            MoClear();
            gh = MoHorse(World, 0, 4 | (1 << 8), I.GoldHorseArmor, true); gh.Owner = Player.Name;
            gh.SetPositionAndPrev(_moX + .5, _moY, _moZ + 2.5); World.SpawnEntity(gh);
            Player.SetPositionAndPrev(_moX + .5, _moY, _moZ + .5); Player.Inventory.Main[3] = new ItemStack(I.Wheat, 12); Player.Inventory.Main[4] = new ItemStack(I.Saddle);
        });
        Step(10, () => { Player.Sneaking = true; MoCheck("GUI konia (kucnięcie)", gh.Interact(Player) && Player.OpenContainer is HorseContainer); Player.Sneaking = false; });
        Step(10, () => Shot("mo_17_gui_kon"));
        Step(5, () => { Player.CloseContainer(); SetScreen(null); });
        Horse gd = null;
        Step(10, () =>
        {
            gd = MoHorse(World, 1, 0, 0, true, true); gd.Owner = Player.Name;
            gd.SetPositionAndPrev(_moX + 2.5, _moY, _moZ + 2.5); World.SpawnEntity(gd);
            gd.Inv.Set(2, new ItemStack(I.Apple, 5)); gd.Inv.Set(8, new ItemStack(B.OakPlanks, 32));
        });
        Step(10, () => { Player.Sneaking = true; MoCheck("GUI osła ze skrzynią", gd.Interact(Player) && Player.OpenContainer is HorseContainer); Player.Sneaking = false; });
        Step(10, () => Shot("mo_18_gui_osiol"));
        Step(5, () => { Player.CloseContainer(); SetScreen(null); });

        // handel
        Villager vt = null;
        Step(10, () =>
        {
            vt = new Villager(World, 0); vt.SetPositionAndPrev(_moX + .5, _moY, _moZ + 3.5); World.SpawnEntity(vt);
            Player.Inventory.Main[5] = new ItemStack(I.Emerald, 20); Player.Inventory.Main[6] = new ItemStack(I.Wheat, 40);
        });
        Step(10, () => MoCheck("GUI handlu", vt.Interact(Player) && Player.OpenContainer is MerchantContainer, $"ofert {vt.Recipes.Count}"));
        Step(10, () =>
        {
            if (Player.OpenContainer is MerchantContainer mc && mc.Current is { } r)
            {
                mc.Slots[0].Stack = r.Buy1.Copy(); if (r.Buy2 != null) mc.Slots[1].Stack = r.Buy2.Copy();
                MoCheck("wynik oferty w slocie", mc.Inv.Get(2)?.Id == r.Sell.Id, $"{Item.ById[r.Buy1.Id]?.Key} x{r.Buy1.Count} -> {Item.ById[r.Sell.Id]?.Key} x{r.Sell.Count}");
            }
        });
        Step(10, () => Shot("mo_19_gui_handel"));
        Step(5, () => { Player.CloseContainer(); SetScreen(null); });
        Step(5, () => { Log.Info($"MO-TEST KONIEC: {(_moFails == 0 ? "OK" : _moFails + " FAIL")}"); Log.Info("AUTOTEST DONE"); Close(); });
    }

    private int _moFails;
    private void MoCheck(string what, bool ok, string detail = "")
    {
        if (!ok) _moFails++;
        Log.Info($"MO-TEST {what}: {(ok ? "OK" : "FAIL")} {detail}");
    }
}
