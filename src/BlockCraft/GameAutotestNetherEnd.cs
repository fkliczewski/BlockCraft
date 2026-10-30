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
            NeCheck("ogień się rozprzestrzenia", fire >= 5, $"ognia {fire}, drewna {wood}/76");
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
        Step(5, () => { Log.Info($"NE-TEST KONIEC, błędów: {_neFails}"); Log.Info("AUTOTEST DONE"); Close(); });
    }
}
