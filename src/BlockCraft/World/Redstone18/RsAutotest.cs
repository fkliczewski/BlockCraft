using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Kroki autotestu modułu RS. Zmienna środowiskowa BC_AUTOTEST=rs uruchamia tylko je.</summary>
public sealed partial class Game
{
    private int _rsX, _rsY, _rsZ;
    private void RsCamera() { Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(_rsX + 2.5, _rsY + 10, _rsZ - 3.5); Player.Yaw = 0; Player.Pitch = 58; }

    private void AutotestRedstone(bool only)
    {
        if (only)
        {
            _steps.Clear();
            int t0 = 0;
            WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
            Step(5, () => CreateWorld("AutoTest RS " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Superflat, true));
            int t = 0;
            WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t > 1500);
            Step(30, () => { });
        }
        Step(5, () =>
        {
            SetScreen(null);
            Cmds.Execute(Player, "/gamemode creative");
            Player.Flying = true;
            World.Rules.DoDaylightCycle = false; World.DayTime = 13500;
            int x = MathUtil.FloorToInt(Player.X) + 2, z = MathUtil.FloorToInt(Player.Z) + 4;
            int y = World.GetHeight(x, z);
            _rsX = x; _rsY = y; _rsZ = z;
            for (int dx = -2; dx <= 10; dx++)
                for (int dz = -2; dz <= 10; dz++)
                {
                    World.SetBlock(x + dx, y - 1, z + dz, B.Stone);
                    for (int dy = 0; dy < 5; dy++) World.SetBlock(x + dx, y + dy, z + dz, B.Air);
                }
            // dźwignia -> pył x4 -> lampa
            World.SetBlock(x, y, z, B.Lever, Dir.Down);
            for (int i = 1; i <= 4; i++) World.SetBlock(x + i, y, z, B.RedstoneWire, 0);
            World.SetBlock(x + 5, y, z, B.RedstoneLamp);
            // blok redstone -> tłok pcha bruk, lepki tłok obok
            World.SetBlock(x, y, z + 2, B.RedstoneBlock);
            World.SetBlock(x + 1, y, z + 2, B.Piston, Dir.East);
            World.SetBlock(x + 2, y, z + 2, B.Cobblestone);
            World.SetBlock(x, y, z + 4, B.Stone); World.SetBlock(x, y + 1, z + 4, B.Lever, Dir.Down); // szlam w powietrzu (w MC klei się do podłoża)
            World.SetBlock(x + 1, y + 1, z + 4, B.StickyPiston, Dir.East);
            World.SetBlock(x + 2, y + 1, z + 4, B.SlimeBlock);
            World.SetBlock(x + 2, y + 2, z + 4, B.Glass);
            // inwerter: dźwignia -> pył -> blok z pochodnią -> lampa
            World.SetBlock(x, y, z + 6, B.Lever, Dir.Down);
            World.SetBlock(x + 1, y, z + 6, B.RedstoneWire, 0);
            World.SetBlock(x + 2, y, z + 6, B.Stone);
            World.SetBlock(x + 3, y, z + 6, B.RedstoneTorch, 1);
            World.SetBlock(x + 4, y, z + 6, B.RedstoneWire, 0);
            World.SetBlock(x + 5, y, z + 6, B.RedstoneLamp);
            // przekaźnik 4 ticki -> lampa
            World.SetBlock(x, y, z + 8, B.Lever, Dir.Down);
            World.SetBlock(x + 1, y, z + 8, B.Repeater, 3 | (3 << 2));
            World.SetBlock(x + 2, y, z + 8, B.RedstoneWire, 0);
            World.SetBlock(x + 3, y, z + 8, B.RedstoneLamp);
            Player.SetPositionAndPrev(x + 2.5, y + 10, z - 3.5); Player.Yaw = 0; Player.Pitch = 58;
        });
        Step(10, () => { RsCamera(); Shot("rs_01_przed"); });
        Step(5, () =>
        {
            var lever = Block.Get(B.Lever);
            lever.OnActivated(World, _rsX, _rsY, _rsZ, Player);
            lever.OnActivated(World, _rsX, _rsY + 1, _rsZ + 4, Player);
            lever.OnActivated(World, _rsX, _rsY, _rsZ + 8, Player);
        });
        Step(30, () =>
        {
            int x = _rsX, y = _rsY, z = _rsZ;
            bool lamp = World.GetBlock(x + 5, y, z) == B.RedstoneLampOn;
            bool piston = (World.GetMeta(x + 1, y, z + 2) & 8) != 0 && World.GetBlock(x + 3, y, z + 2) == B.Cobblestone;
            bool slime = World.GetBlock(x + 3, y + 1, z + 4) == B.SlimeBlock && World.GetBlock(x + 3, y + 2, z + 4) == B.Glass;
            bool inv = World.GetBlock(x + 5, y, z + 6) == B.RedstoneLampOn;
            bool rep = World.GetBlock(x + 3, y, z + 8) == B.RedstoneLampOn;
            Log.Info($"AUTOTEST RS lampa={(lamp ? "OK" : "FAIL")} tlok={(piston ? "OK" : "FAIL")} szlam={(slime ? "OK" : "FAIL")} inwerter={(inv ? "OK" : "FAIL")} przekaznik={(rep ? "OK" : "FAIL")}");
            RsCamera(); Shot("rs_02_wlaczone");
        });
        Step(5, () => Block.Get(B.Lever).OnActivated(World, _rsX, _rsY, _rsZ + 6, Player));
        Step(30, () =>
        {
            bool inv = World.GetBlock(_rsX + 5, _rsY, _rsZ + 6) == B.RedstoneLamp && World.GetBlock(_rsX + 3, _rsY, _rsZ + 6) == B.RedstoneTorchOff;
            Log.Info($"AUTOTEST RS inwerter_wylaczony={(inv ? "OK" : "FAIL")}");
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(_rsX + 8.5, _rsY + 3, _rsZ + 7.5); Player.Yaw = 125; Player.Pitch = 30;
            Shot("rs_03_inwerter");
        });
        if (only) Step(5, () => { Log.Info("AUTOTEST DONE"); Close(); });
    }
}
