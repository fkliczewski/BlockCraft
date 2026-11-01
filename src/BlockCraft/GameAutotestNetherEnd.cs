using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Autotest modułu NE: BlockCraft.exe --autotest --ne (zrzuty do autotest/ne_*.png, wyniki w logu "NE-TEST").</summary>
public sealed partial class Game
{
    private int _neFails;
    private void NeCheck(string what, bool ok, string detail = "")
    {
        if (!ok) _neFails++;
        Log.Info($"NE-TEST {what}: {(ok ? "OK" : "FAIL")} {detail}");
    }

    private bool NeReady(int min, ref int t) => ++t > 600 || (t > min && World.GetChunk(MathUtil.FloorToInt(Player.X) >> 4, MathUtil.FloorToInt(Player.Z) >> 4)?.Merged == true && Renderer.PendingMeshes < 10);

    private void SetupAutotestNetherEnd()
    {
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(5, () => CreateWorld("AutoTestNE " + DateTime.Now.ToString("HHmmss"), 12345, 0, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        int gx = 0, gy = 0, gz = 0;
        Step(20, () =>
        {
            World.DayTime = 6000; World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false;
            gx = MathUtil.FloorToInt(Player.X); gz = MathUtil.FloorToInt(Player.Z); gy = MathUtil.FloorToInt(Player.Y);
            // płaski plac i ramka 4x5 z obsydianu (wnętrze 2x3) w osi X, dwa bloki przed graczem
            for (int dx = -4; dx <= 5; dx++) for (int dz = -2; dz <= 5; dz++) for (int dy = -1; dy < 7; dy++)
                World.SetBlock(gx + dx, gy + dy, gz + dz, dy < 0 ? B.Stone : B.Air, 0);
            DimensionPortals.BuildFrame(World, gx, gy + 1, gz + 2, 1);
            Player.SetPositionAndPrev(gx + 0.5, gy, gz + 0.5); Player.Yaw = 0; Player.Pitch = 17;
            Player.Inventory.Main[0] = new ItemStack(I.FlintAndSteel); Player.Inventory.SelectedSlot = 0;
        });
        Step(10, () =>
        {
            Player.Yaw = 0; Player.Pitch = 17;
            Ctrl.UpdateTarget(1f);
            Log.Info($"NE-TEST cel krzesiwa {Ctrl.Hit.X} {Ctrl.Hit.Y} {Ctrl.Hit.Z} face={Ctrl.Hit.Face} = {Block.Get(World.GetBlock(Ctrl.Hit.X, Ctrl.Hit.Y, Ctrl.Hit.Z)).Key}");
            _rightPressed = true;
        });
        Step(10, () =>
        {
            int portals = 0;
            for (int i = 0; i < 2; i++) for (int j = 1; j <= 3; j++) if (World.GetBlock(gx + i, gy + j, gz + 2) == B.Portal) portals++;
            NeCheck("zapalenie ramki krzesiwem", portals == 6, $"bloków portalu {portals}/6");
            Player.Pitch = 5;
        });
        Step(20, () => Shot("ne_01_portal_overworld"));
        int t2 = 0;
        Step(5, () => { Player.SetPositionAndPrev(gx + 1.0, gy + 1, gz + 2.5); Player.Yaw = 180; Player.Pitch = 0; });
        WaitUntil(() => World.Dimension == DimensionId.Nether || ++t2 > 300);
        Step(1, () => NeCheck("przejście do Netheru (survival, 80 ticków)", World.Dimension == DimensionId.Nether, $"po {t2} klatkach, poz {Player.X:F1} {Player.Y:F1} {Player.Z:F1}"));
        int t3 = 0, npx = 0, npy = 0, npz = 0;
        WaitUntil(() => NeReady(60, ref t3));
        Step(20, () =>
        {
            int px = MathUtil.FloorToInt(Player.X), py = MathUtil.FloorToInt(Player.Y), pz = MathUtil.FloorToInt(Player.Z);
            npx = px; npy = py; npz = pz;
            NeCheck("portal w Netherze", World.GetBlock(px, py, pz) == B.Portal || World.GetBlock(px, py + 1, pz) == B.Portal, $"blok {Block.Get(World.GetBlock(px, py, pz)).Key}, pod {Block.Get(World.GetBlock(px, py - 1, pz)).Key}");
            NeCheck("gracz na świecie Netheru", World.Entities.Contains(Player) && Player.World == World, $"hp {Player.Health}");
            // wyjście z portalu na platformę i widok na portal
            Player.SetPositionAndPrev(px + 0.5, py, pz - 0.7); Player.Yaw = 0; Player.Pitch = 10;
        });
        Step(30, () => Shot("ne_02_nether_portal"));
        Step(5, () => { Player.Yaw = 180; Player.Pitch = 0; });
        Step(20, () => Shot("ne_03_nether_view"));
        Step(5, () => { Player.Yaw = 90; Player.Pitch = -20; });
        Step(20, () => Shot("ne_04_nether_view2"));
        // moby Netheru: galeria i testy zachowań
        int mx0 = 0, my0 = 0, mz0 = 0;
        Step(5, () =>
        {
            Cmds.Execute(Player, "/gamemode creative");
            mx0 = MathUtil.FloorToInt(Player.X); my0 = MathUtil.FloorToInt(Player.Y) + 30; mz0 = MathUtil.FloorToInt(Player.Z) + 40;
            World.EnsureDimensionArea(mx0, mz0 + 8, 32);
            for (int dx = -12; dx <= 12; dx++) for (int dz = -4; dz <= 20; dz++) for (int dy = -1; dy < 12; dy++)
                World.SetBlock(mx0 + dx, my0 + dy, mz0 + dz, dy < 0 ? ((dx + dz) % 4 == 0 ? B.Glowstone : B.Netherrack) : B.Air, 0);
            string[] mobs = { "zombie_pigman", "blaze", "magma_cube", "wither_skeleton", "endermite" };
            for (int i = 0; i < mobs.Length; i++) Cmds.Execute(Player, $"/summon {mobs[i]} {mx0 - 6 + i * 3} {my0} {mz0 + 6}");
            Cmds.Execute(Player, $"/summon ghast {mx0} {my0 + 3} {mz0 + 15}");
            foreach (var e in World.Entities.OfType<Mob>()) { e.Yaw = e.BodyYaw = e.HeadYaw = 180; }
            Player.SetPositionAndPrev(mx0 + .5, my0 + 1, mz0 - 2); Player.Yaw = 0; Player.Pitch = 12; Player.Flying = true;
        });
        Step(40, () => Shot("ne_09_nether_mobs"));
        Step(5, () =>
        {
            foreach (var e in World.Entities.OfType<Mob>().ToList()) e.Removed = true;
            Cmds.Execute(Player, "/gamemode survival"); Player.Flying = false;
            Player.SetPositionAndPrev(mx0 + .5, my0, mz0 + .5);
            // gniew grupowy pigmanów
            var pigs = new List<ZombiePigman>();
            for (int i = 0; i < 3; i++) { var z = new ZombiePigman(World); z.SetPositionAndPrev(mx0 + i * 2 - 2 + .5, my0, mz0 + 6.5); World.SpawnEntity(z); pigs.Add(z); }
            for (int i = 0; i < 3; i++) World.Tick();
            Player.AttackEntity(pigs[0]);
            NeCheck("gniew grupowy pigmanów", pigs.All(z => z.Angry), $"wściekłe {pigs.Count(z => z.Angry)}/3");
            foreach (var z in pigs) z.Removed = true;
            // odbicie kuli ognia ghasta w niego samego
            var g = new Ghast(World); g.SetPositionAndPrev(mx0 + .5, my0 + 3, mz0 + 16.5); World.SpawnEntity(g);
            var fb = new LargeFireballEntity(World); fb.Launch(g, mx0 + .5, my0 + 3, mz0 + 13, 0, -.1, -1, 0); World.SpawnEntity(fb);
            Player.Yaw = 0; Player.Pitch = -8; Player.Health = 20;
            fb.AttackedByPlayer(Player);
            for (int i = 0; i < 60 && !fb.Removed; i++) World.Tick();
            NeCheck("odbita kula zabija ghasta", fb.Removed && (g.Dead || g.Removed), $"kula {(fb.Removed ? "wybuchła" : "leci")}, ghast hp {g.Health}");
            g.Removed = true;
            // płomyk strzela salwą
            var bl = new Blaze(World); bl.SetPositionAndPrev(mx0 + .5, my0, mz0 + 10.5); World.SpawnEntity(bl);
            int shots = 0;
            for (int i = 0; i < 160; i++) { World.Tick(); shots = Math.Max(shots, World.Entities.OfType<SmallFireballEntity>().Count()); Player.Health = 20; }
            NeCheck("płomyk strzela kulami", shots >= 1, $"kul naraz {shots}, cel {(bl.Target == Player ? "gracz" : "brak")}");
            bl.Removed = true;
            foreach (var e in World.Entities.OfType<FireballEntity>().ToList()) e.Removed = true;
            // kostka magmy dzieli się
            var mc = new MagmaCube(World); mc.SetSize(4); mc.SetPositionAndPrev(mx0 + 4.5, my0, mz0 + 4.5); World.SpawnEntity(mc);
            World.Tick();
            mc.Attack(DamageSource.Generic, 100);
            for (int i = 0; i < 3; i++) World.Tick();
            int small = World.Entities.OfType<MagmaCube>().Count(m => m.Size == 2 && !m.Dead);
            NeCheck("kostka magmy dzieli się", small >= 2, $"mniejszych {small}");
            foreach (var e in World.Entities.OfType<Mob>().ToList()) e.Removed = true;
            Player.Health = 20;
            // spawner Netheru
            Dimensions.Get(DimensionId.Overworld).Rules.DoMobSpawning = true;
            for (int i = 0; i < 400; i++) { Dimensions.Tick(); Player.Health = 20; Player.FireTicks = 0; }
            var kinds = World.Entities.OfType<Monster>().GroupBy(m => m.TypeKey).Select(k => k.Key + "=" + k.Count());
            NeCheck("spawn mobów w Netherze", World.Entities.OfType<Monster>().Any(), string.Join(", ", kinds));
            Dimensions.Get(DimensionId.Overworld).Rules.DoMobSpawning = false;
            foreach (var e in World.Entities.OfType<Mob>().ToList()) e.Removed = true;
            Player.SetPositionAndPrev(npx + 0.5, npy, npz - 0.7);
        });
        // forteca Netheru: znajdź najbliższą, obejrzyj z góry
        int t8 = 0;
        Step(5, () =>
        {
            var loc = WorldHooks.Locators.TryGetValue("Fortress", out var f) ? f(World, MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z)) : null;
            NeCheck("lokalizacja fortecy", loc != null, loc?.ToString() ?? "");
            if (loc == null) return;
            int fx = loc.Value.x, fz = loc.Value.z;
            World.EnsureDimensionArea(fx, fz, 64);
            int bricks = 0, chests = 0, top = 0;
            for (int x = fx - 40; x <= fx + 40; x++) for (int z = fz - 40; z <= fz + 40; z++) for (int y = 30; y < 100; y++)
            {
                byte b = World.GetBlock(x, y, z);
                if (b == B.NetherBrick) { bricks++; if (Math.Abs(x - fx) < 3 && Math.Abs(z - fz) < 3) top = Math.Max(top, y); }
                else if (b == B.Chest) chests++;
            }
            NeCheck("forteca wygenerowana", bricks > 500, $"cegieł {bricks}, skrzyń {chests}, pokład y {top}, w obrysie {NetherFortress.IsInside(World.Seed, fx, top, fz)}");
            Cmds.Execute(Player, "/gamemode creative"); Player.Flying = true;
            Player.SetPositionAndPrev(fx + .5, top + 14, fz - 22.5); Player.Yaw = 0; Player.Pitch = 30;
        });
        WaitUntil(() => NeReady(80, ref t8));
        Step(30, () => Shot("ne_10_fortress"));
        Step(5, () => { Player.SetPositionAndPrev(Player.X, Player.Y - 9, Player.Z + 16); Player.Pitch = 10; });
        Step(30, () => { Shot("ne_11_fortress_bridge"); Cmds.Execute(Player, "/gamemode survival"); Player.Flying = false; Player.SetPositionAndPrev(npx + 0.5, npy, npz - 0.7); });
        Step(30, () => { });
        // powrót: gracz był poza portalem (cooldown minął), wchodzi z powrotem
        int t4 = 0;
        Step(5, () => { Player.SetPositionAndPrev(npx + 0.5, npy, npz + 0.5); Log.Info($"NE-TEST Nether portal {npx} {npy} {npz}"); });
        WaitUntil(() => World.Dimension == DimensionId.Overworld || ++t4 > 300);
        Step(1, () => NeCheck("powrót do Overworldu", World.Dimension == DimensionId.Overworld, $"poz {Player.X:F1} {Player.Y:F1} {Player.Z:F1}, start {gx} {gy} {gz}"));
        int t5 = 0;
        WaitUntil(() => NeReady(40, ref t5));
        Step(10, () => { Player.Pitch = 10; Shot("ne_05_back_overworld"); });
        // ogień 1.8: ładunek ognisty na drewnianej chatce, rozprzestrzenianie, gaszenie deszczem, wieczny ogień
        int fx = 0, fy = 0, fz = 0;
        Step(5, () =>
        {
            fx = MathUtil.FloorToInt(Player.X) - 14; fz = MathUtil.FloorToInt(Player.Z); fy = MathUtil.FloorToInt(Player.Y);
            for (int dx = -5; dx <= 5; dx++) for (int dz = -10; dz <= 6; dz++) for (int dy = -1; dy < 8; dy++)
                World.SetBlock(fx + dx, fy + dy, fz + dz, dy < 0 ? B.Stone : B.Air, 0);
            for (int dx = -2; dx <= 2; dx++) for (int dz = -2; dz <= 2; dz++) for (int dy = 0; dy < 4; dy++)
                if (Math.Abs(dx) == 2 || Math.Abs(dz) == 2 || dy == 3) World.SetBlock(fx + dx, fy + dy, fz + dz, dy == 3 ? B.OakPlanks : Math.Abs(dx) == 2 && Math.Abs(dz) == 2 ? B.OakLog : B.OakPlanks, 0);
            World.SetBlock(fx, fy + 1, fz + 4, B.Netherrack, 0);
            Player.SetPositionAndPrev(fx + 0.5, fy, fz - 5.5); Player.Yaw = 0; Player.Pitch = 15;
            Player.Inventory.Main[2] = new ItemStack(I.FireCharge, 3); Player.Inventory.SelectedSlot = 2;
        });
        Step(5, () => { Ctrl.UpdateTarget(1f); Log.Info($"NE-TEST cel ładunku {Ctrl.Hit.X} {Ctrl.Hit.Y} {Ctrl.Hit.Z} face {Ctrl.Hit.Face}"); _rightPressed = true; });
        Step(5, () =>
        {
            NeCheck("ładunek ognisty stawia ogień", World.GetBlock(fx, fy, fz - 3) == B.Fire, $"blok {Block.Get(World.GetBlock(fx, fy, fz - 3)).Key}, ładunków {Player.Inventory.Main[2]?.Count ?? 0}");
            World.SetBlock(fx, fy + 2, fz + 4, B.Fire, 0);
            for (int i = 0; i < 200; i++) World.Tick();
        });
        Step(20, () =>
        {
            int fire = 0, wood = 0;
            for (int dx = -3; dx <= 3; dx++) for (int dz = -3; dz <= 3; dz++) for (int dy = 0; dy < 6; dy++)
            { byte b = World.GetBlock(fx + dx, fy + dy, fz + dz); if (b == B.Fire) fire++; else if (b is B.OakPlanks or B.OakLog) wood++; }
            NeCheck("ogień się rozprzestrzenia", fire + 76 - wood >= 3, $"ognia {fire}, drewna {wood}/76");
            NeCheck("wieczny ogień na netherracku", World.GetBlock(fx, fy + 2, fz + 4) == B.Fire, "");
            Player.SetPositionAndPrev(fx + 0.5, fy + 3, fz - 7.5); Player.Pitch = 15;
        });
        Step(20, () => Shot("ne_05b_fire_spread"));
        Step(5, () =>
        {
            WorldHooks.IsRainingAt = (w, x, y, z) => true;
            for (int i = 0; i < 120; i++) World.Tick();
            int fire = 0;
            for (int dx = -3; dx <= 3; dx++) for (int dz = -3; dz <= 3; dz++) for (int dy = 0; dy < 6; dy++) if (World.GetBlock(fx + dx, fy + dy, fz + dz) == B.Fire) fire++;
            NeCheck("deszcz gasi ogień", fire == 0, $"ognia {fire}");
            NeCheck("deszcz nie gasi ognia na netherracku", World.GetBlock(fx, fy + 2, fz + 4) == B.Fire, "");
            WorldHooks.IsRainingAt = null;
        });
        // oko Endu: rzut w stronę twierdzy (hak OW podstawiony na czas testu)
        EnderEyeEntity eye = null; ItemStack eyes = null; Func<World.World, int, int, (int x, int y, int z)?> oldFind = null;
        Step(5, () =>
        {
            oldFind = WorldHooks.FindStronghold;
            WorldHooks.FindStronghold = (w, x, z) => (x + 600, 30, z + 100);
            Player.SetPositionAndPrev(fx + 0.5, fy, fz - 7.5);
            Player.Pitch = -10; Player.Inventory.Main[3] = eyes = new ItemStack(I.EyeOfEnder, 2); Player.Inventory.SelectedSlot = 3;
            _rightPressed = true;
        });
        Step(10, () =>
        {
            WorldHooks.FindStronghold = oldFind;
            eye = World.Entities.OfType<EnderEyeEntity>().FirstOrDefault();
            NeCheck("rzut oka Endu", eye != null && eyes.Count == 1, $"oczu {eyes.Count}, cel {eye?.TargetX:F0} {eye?.TargetY:F0} {eye?.TargetZ:F0}");
        });
        Step(5, () => Shot("ne_05c_eye_flight"));
        Step(5, () =>
        {
            double sx = eye?.X ?? 0;
            for (int i = 0; i < 90; i++) World.Tick();
            NeCheck("oko leci do twierdzy i znika", eye != null && eye.Removed && eye.X > sx, $"x {sx:F1} -> {eye?.X:F1}, upuszczone {World.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.EyeOfEnder)}");
        });
        // portal Endu: rama 12 bloków z okami (tryb kreatywny)
        int ex = 0, ey = 0, ez = 0;
        Step(5, () =>
        {
            Cmds.Execute(Player, "/gamemode creative");
            ex = MathUtil.FloorToInt(Player.X) + 8; ez = MathUtil.FloorToInt(Player.Z); ey = MathUtil.FloorToInt(Player.Y);
            for (int dx = -5; dx <= 5; dx++) for (int dz = -5; dz <= 5; dz++) { World.SetBlock(ex + dx, ey - 1, ez + dz, B.Stone, 0); for (int dy = 0; dy < 4; dy++) World.SetBlock(ex + dx, ey + dy, ez + dz, B.Air, 0); }
            for (int i = -1; i <= 1; i++)
            {
                World.SetBlock(ex + i, ey, ez - 2, B.EndPortalFrame, 0 | (i == 1 ? 0 : 4));
                World.SetBlock(ex + i, ey, ez + 2, B.EndPortalFrame, 2 | 4);
                World.SetBlock(ex - 2, ey, ez + i, B.EndPortalFrame, 3 | 4);
                World.SetBlock(ex + 2, ey, ez + i, B.EndPortalFrame, 1 | 4);
            }
            // ostatnie oko wkłada gracz
            Player.SetPositionAndPrev(ex + 1.5, ey, ez - 3.5); Player.Yaw = 0; Player.Pitch = 22;
            Player.Inventory.Main[1] = new ItemStack(I.EyeOfEnder, 4); Player.Inventory.SelectedSlot = 1;
        });
        Step(5, () =>
        {
            Ctrl.UpdateTarget(1f);
            Log.Info($"NE-TEST cel oka {Ctrl.Hit.X} {Ctrl.Hit.Y} {Ctrl.Hit.Z} = {Block.Get(World.GetBlock(Ctrl.Hit.X, Ctrl.Hit.Y, Ctrl.Hit.Z)).Key}");
            _rightPressed = true;
        });
        Step(10, () =>
        {
            int n = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (World.GetBlock(ex + dx, ey, ez + dz) == B.EndPortal) n++;
            NeCheck("aktywacja portalu Endu okiem", n == 9, $"bloków {n}/9");
            Player.SetPositionAndPrev(ex + 0.5, ey + 3, ez - 4.5); Player.Pitch = 35;
        });
        Step(20, () => Shot("ne_06_end_portal"));
        int t6 = 0;
        Step(5, () => { Player.Flying = false; Player.SetPositionAndPrev(ex + 0.5, ey + 0.8, ez + 0.5); });
        WaitUntil(() => World.Dimension == DimensionId.End || ++t6 > 200);
        Step(1, () => NeCheck("przejście do Endu", World.Dimension == DimensionId.End, $"poz {Player.X:F1} {Player.Y:F1} {Player.Z:F1}"));
        int t7 = 0;
        WaitUntil(() => NeReady(60, ref t7));
        Step(20, () => { Player.Yaw = 270; Player.Pitch = 0; Shot("ne_07_end"); });
        Step(5, () => { Player.Yaw = 90; Player.Pitch = -10; });
        Step(20, () => Shot("ne_08_end2"));
        SetupAutotestEndBoss();
    }

    private void NeShot(string name)
    {
        Log.Info($"NE-TEST zrzut {name}: gracz {Player.X:F1} {Player.Y:F1} {Player.Z:F1} lot {Player.Flying} yaw {Player.Yaw:F0} pitch {Player.Pitch:F0}");
        Shot(name);
    }

    /// <summary>Gracz w kreatywnym lata w danym punkcie (lot wyłącza się po dotknięciu ziemi).</summary>
    private void NeFly(double x, double y, double z)
    {
        Player.SetPositionAndPrev(x, y, z); Player.MotionX = Player.MotionY = Player.MotionZ = 0; Player.OnGround = false; Player.Flying = true;
    }

    /// <summary>Kamera gracza na punkt (x, y, z).</summary>
    private void NeLookAt(double x, double y, double z)
    {
        double dx = x - Player.X, dy = y - (Player.Y + Player.EyeHeight), dz = z - Player.Z;
        Player.Yaw = Player.PrevYaw = (float)(Math.Atan2(-dx, dz) * MathUtil.Rad2Deg);
        Player.Pitch = Player.PrevPitch = (float)(-Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)) * MathUtil.Rad2Deg);
    }

    /// <summary>Część Endu: filary z kryształami, smok i jego części, leczenie promieniem, śmierć, portal wyjściowy, napisy; potem Wither.</summary>
    private void SetupAutotestEndBoss()
    {
        EnderDragon dragon = null;
        Step(5, () =>
        {
            Cmds.Execute(Player, "/gamemode creative"); Player.Flying = true;
            World.EnsureDimensionArea(0, 0, 112);
            Player.SetPositionAndPrev(60, 95, 60); NeLookAt(0, 70, 0);
        });
        int t8 = 0;
        WaitUntil(() => NeReady(80, ref t8));
        Step(10, () =>
        {
            var crystals = World.Entities.OfType<EnderCrystal>().ToList();
            int pillars = 0, bedrockTops = 0;
            foreach (var c in crystals)
            {
                int x = MathUtil.FloorToInt(c.X), y = MathUtil.FloorToInt(c.Y), z = MathUtil.FloorToInt(c.Z);
                if (World.GetBlock(x, y - 1, z) == B.Bedrock) bedrockTops++;
                if (World.GetBlock(x, y - 2, z) == B.Obsidian) pillars++;
            }
            int stone = 0;
            for (int x = -96; x <= 96; x += 4) for (int z = -96; z <= 96; z += 4) for (int y = 10; y < 80; y++) if (World.GetBlock(x, y, z) == B.EndStone) { stone++; break; }
            NeCheck("wyspa Endu z kamienia", stone > 800, $"kolumn z kamieniem {stone}/2401");
            NeCheck("filary z kryształami", crystals.Count >= 5 && pillars == crystals.Count && bedrockTops == crystals.Count, $"kryształów {crystals.Count}, na obsydianie {pillars}, na bedrocku {bedrockTops}");
            int fires = crystals.Count(c => World.GetBlock(MathUtil.FloorToInt(c.X), MathUtil.FloorToInt(c.Y), MathUtil.FloorToInt(c.Z)) == B.Fire);
            NeCheck("ogień pod kryształami", fires == crystals.Count, $"{fires}/{crystals.Count}");
            dragon = World.Entities.OfType<EnderDragon>().FirstOrDefault();
            NeCheck("smok w Endzie", dragon != null, dragon == null ? "" : $"poz {dragon.X:F0} {dragon.Y:F0} {dragon.Z:F0}, hp {dragon.Health}");
            NeCheck("części ciała smoka", dragon?.Parts?.Length == 7 && dragon.Parts.All(p => World.Entities.Contains(p)), $"części {dragon?.Parts?.Length}");
            NeCheck("pasek bossa", BossStatus.Visible && BossStatus.BossName == "Smok Endu", $"{BossStatus.BossName} {BossStatus.HealthScale:F2}");
            var c0 = crystals.OrderBy(c => c.DistanceSq(Player)).FirstOrDefault();
            if (c0 != null) { NeFly(c0.X + 3, c0.Y + 3, c0.Z + 3); NeLookAt(c0.X, c0.Y + 1, c0.Z); Log.Info($"NE-TEST kryształ do zrzutu {c0.X:F1} {c0.Y:F1} {c0.Z:F1}"); }
            // do zrzutów smok wisi nieruchomo (NoAI) nad środkiem wyspy
            if (dragon != null) { dragon.NoAI = true; dragon.SetPositionAndPrev(0, 85, 0); dragon.Yaw = dragon.PrevYaw = 30; dragon.MotionX = dragon.MotionY = dragon.MotionZ = 0; dragon.RingIndex = -1; }
        });
        Step(20, () => NeShot("ne_12_end_pillar"));
        Step(5, () =>
        {
            if (dragon == null) return;
            NeFly(dragon.X + 22, dragon.Y + 6, dragon.Z + 8); NeLookAt(dragon.X, dragon.Y + 2, dragon.Z);
        });
        Step(3, () => { if (dragon != null) NeLookAt(dragon.X, dragon.Y + 2, dragon.Z); NeShot("ne_13_dragon"); });
        // leczenie: kryształ tuż przy smoku
        EnderCrystal healer = null; float hp0 = 0;
        Step(5, () =>
        {
            if (dragon == null) return;
            dragon.Health = 150; hp0 = dragon.Health;
            healer = new EnderCrystal(World); healer.SetPositionAndPrev(dragon.X - 10, dragon.Y - 6, dragon.Z + 12); World.SpawnEntity(healer);
        });
        int t9 = 0;
        WaitUntil(() => dragon == null || dragon.HealingCrystal == healer && dragon.Health > hp0 || ++t9 > 200);
        Step(1, () =>
        {
            if (dragon == null) return;
            NeCheck("kryształ leczy smoka", dragon.HealingCrystal == healer && dragon.Health > hp0, $"hp {hp0} -> {dragon.Health}, leczący {(dragon.HealingCrystal == healer ? "nowy" : dragon.HealingCrystal == null ? "brak" : "inny")}");
            NeFly(healer.X + 5, healer.Y + 3, healer.Z + 5); NeLookAt(healer.X, healer.Y + 1.5, healer.Z);
            Log.Info($"NE-TEST smok {dragon.X:F1} {dragon.Y:F1} {dragon.Z:F1}, kryształ {healer.X:F1} {healer.Y:F1} {healer.Z:F1}");
        });
        Step(10, () => NeShot("ne_14_crystal_beam"));
        Step(2, () =>
        {
            if (dragon == null) return;
            hp0 = dragon.Health;
            Player.AttackEntity(healer);
            NeCheck("uderzenie niszczy kryształ", healer.Removed, "");
        });
        int t10 = 0;
        WaitUntil(() => dragon == null || dragon.HealingCrystal != healer || ++t10 > 40);
        Step(2, () =>
        {
            if (dragon == null) return;
            NeCheck("zniszczenie leczącego kryształu rani smoka", dragon.Health < hp0, $"hp {hp0} -> {dragon.Health}");
            // obrażenia w skrzydło są dzielone (/4+1), w głowę pełne
            float h = dragon.Health; dragon.HurtResistantTime = 0;
            Player.AttackEntity(dragon.Wing1);
            NeCheck("trafienie w część ciała", dragon.Health < h && h - dragon.Health <= 2.5f, $"hp {h} -> {dragon.Health}");
            dragon.HurtResistantTime = 0;
            dragon.Head.Attack(DamageSource.PlayerAttack(Player), 1000);
            NeCheck("smok ginie od trafienia w głowę", dragon.Health <= 0, $"hp {dragon.Health}");
        });
        WaitUntil(() => dragon == null || dragon.DeathTicks >= 150);
        Step(1, () => { if (dragon != null) { NeFly(dragon.X + 34, dragon.Y + 2, dragon.Z + 12); NeLookAt(dragon.X, dragon.Y + 2, dragon.Z); } });
        Step(3, () => { if (dragon != null) NeLookAt(dragon.X, dragon.Y + 2, dragon.Z); NeShot("ne_15_dragon_death"); });
        int t11 = 0, ex = 0, ez = 0;
        WaitUntil(() => dragon == null || dragon.Removed || ++t11 > 400);
        Step(5, () =>
        {
            if (dragon == null) return;
            ex = MathUtil.FloorToInt(dragon.X); ez = MathUtil.FloorToInt(dragon.Z);
            int portal = 0;
            for (int dx = -4; dx <= 4; dx++) for (int dz = -4; dz <= 4; dz++) if (World.GetBlock(ex + dx, 64, ez + dz) == B.EndPortal) portal++;
            int orbs = World.Entities.OfType<XpOrbEntity>().Count();
            NeCheck("portal wyjściowy po smoku", portal == 20, $"bloków portalu {portal}/20 (środek to kolumna z bedrocku) w {ex} 64 {ez}");
            NeCheck("jajo smoka na kolumnie", World.GetBlock(ex, 68, ez) == B.DragonEgg && World.GetBlock(ex, 67, ez) == B.Bedrock, Block.Get(World.GetBlock(ex, 68, ez)).Key);
            NeCheck("XP ze smoka", orbs > 0, $"kul {orbs}");
            NeFly(ex + 9, 72, ez + 9); NeLookAt(ex, 65, ez);
        });
        Step(20, () => NeShot("ne_16_exit_portal"));
        int t12 = 0;
        Step(5, () => { Cmds.Execute(Player, "/gamemode survival"); Player.Flying = false; Player.SetPositionAndPrev(ex + 1.5, 64.2, ez + .5); });
        WaitUntil(() => World.Dimension == DimensionId.Overworld || ++t12 > 200);
        Step(30, () =>
        {
            NeCheck("powrót z Endu do Overworldu", World.Dimension == DimensionId.Overworld, "");
            NeCheck("napisy końcowe", CurrentScreen is CreditsScreen, CurrentScreen?.GetType().Name ?? "brak ekranu");
            (CurrentScreen as CreditsScreen)?.SkipIntro();
        });
        Step(5, () =>
        {
            Shot("ne_17_credits");
            SetScreen(null);
        });
        // Wither: rzeczywiste czaszki z typem1 w encji bloku, bez zastępczego znacznika.
        Wither wither = null; int wx = 0, wy = 0, wz = 0;
        Step(10, () =>
        {
            Cmds.Execute(Player, "/gamemode creative"); Player.Flying = true;
            World.Difficulty = Difficulty.Normal;
            wx = MathUtil.FloorToInt(Player.X) + 8; wz = MathUtil.FloorToInt(Player.Z); wy = MathUtil.FloorToInt(Player.Y);
            for (int dx = -6; dx <= 6; dx++) for (int dz = -6; dz <= 6; dz++) for (int dy = -1; dy < 8; dy++) World.SetBlock(wx + dx, wy + dy, wz + dz, dy < 0 ? B.Stone : B.Air, 0);
            World.SetBlock(wx, wy, wz, B.SoulSand);
            for (int i = -1; i <= 1; i++)
            {
                World.SetBlock(wx,wy+1,wz+i,B.SoulSand);World.SetBlock(wx,wy+2,wz+i,B.Skull,Dir.Up);
                if(World.GetTileEntity(wx,wy+2,wz+i) is SkullTile skull)skull.SetFromItem(new ItemStack(I.SkullItem,1,1),0);
            }
            wither = Wither.TrySummon(World, wx, wy + 2, wz + 1);
            NeCheck("przywołanie Withera ze wzoru T", wither != null && World.GetBlock(wx, wy + 1, wz) == B.Air, wither == null ? "" : $"hp {wither.Health}, nietykalny {wither.InvulTime}");
            NeFly(wx - 7, wy + 2, wz + 0.5); NeLookAt(wx + .5, wy + 2, wz + .5);
            var cr = new EnderCrystal(World); cr.SetPositionAndPrev(wx - 3.5, wy, wz - 2.5); World.SpawnEntity(cr);
        });
        Step(30, () => { NeLookAt(wx + .5, wy + 2, wz + .5); NeShot("ne_18_wither_spawn"); });
        int t13 = 0;
        WaitUntil(() => wither == null || wither.InvulTime <= 0 || ++t13 > 4000);
        Step(5, () =>
        {
            if (wither == null) return;
            NeCheck("Wither po wybuchu startowym", wither.InvulTime == 0 && !wither.Dead && wither.Health > 200, $"hp {wither.Health}, pod nim {Block.Get(World.GetBlock(wx, wy - 1, wz)).Key}");
            var cow = new Cow(World); cow.SetPositionAndPrev(wx + 6.5, wy + 1, wz + 6.5); World.SpawnEntity(cow);
        });
        int t14 = 0;
        WaitUntil(() => wither == null || World.Entities.OfType<WitherSkullEntity>().Any() || ++t14 > 3000);
        Step(1, () =>
        {
            NeCheck("Wither strzela czaszkami", World.Entities.OfType<WitherSkullEntity>().Any(), $"cel {wither?.Target?.TypeKey}, głowy {string.Join(",", wither?.HeadTargets ?? Array.Empty<int>())}");
            if (wither != null) { NeFly(wither.X - 8, wither.Y + 2, wither.Z); NeLookAt(wither.X, wither.Y + 2, wither.Z); }
        });
        Step(3, () => { if (wither != null) NeLookAt(wither.X, wither.Y + 2, wither.Z); NeShot("ne_19_wither_fight"); });
        Step(5, () =>
        {
            if (wither == null) return;
            wither.Health = 140;
            var arrow = new ArrowEntity(World); arrow.SetPositionAndPrev(wither.X - 3, wither.Y + 1.5, wither.Z); arrow.MotionX = 2; World.SpawnEntity(arrow);
        });
        Step(5, () =>
        {
            if (wither == null) return;
            NeCheck("tarcza Withera odbija strzały", wither.Armored && wither.Health >= 140, $"hp {wither.Health}");
            NeFly(wither.X - 7, wither.Y + 2, wither.Z); NeLookAt(wither.X, wither.Y + 2, wither.Z);
        });
        Step(5, () => { if (wither != null) NeLookAt(wither.X, wither.Y + 2, wither.Z); NeShot("ne_20_wither_armor"); });
        Step(5, () =>
        {
            if (wither == null) return;
            wither.HurtResistantTime = 0;
            wither.Attack(DamageSource.PlayerAttack(Player), 1000);
        });
        Step(30, () =>
        {
            bool star = World.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.NetherStar) || Player.Inventory.Main.Any(s => s?.Id == I.NetherStar);
            NeCheck("Wither upuszcza gwiazdę Netheru", wither != null && wither.Dead && star, $"gwiazda {star}");
            Log.Info($"NE-TEST KONIEC, błędów: {_neFails}"); Log.Info("AUTOTEST DONE"); Close();
        });
    }
}
