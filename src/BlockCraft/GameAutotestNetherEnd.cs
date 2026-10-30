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
        // powrót: gracz był poza portalem (cooldown minął), wchodzi z powrotem
        int t4 = 0;
        Step(5, () => { Player.SetPositionAndPrev(npx + 0.5, npy, npz + 0.5); Log.Info($"NE-TEST Nether portal {npx} {npy} {npz}"); });
        WaitUntil(() => World.Dimension == DimensionId.Overworld || ++t4 > 300);
        Step(1, () => NeCheck("powrót do Overworldu", World.Dimension == DimensionId.Overworld, $"poz {Player.X:F1} {Player.Y:F1} {Player.Z:F1}, start {gx} {gy} {gz}"));
        int t5 = 0;
        WaitUntil(() => NeReady(40, ref t5));
        Step(10, () => { Player.Pitch = 10; Shot("ne_05_back_overworld"); });
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
