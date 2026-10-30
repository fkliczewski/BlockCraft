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
        // elementy kroku 4: komparator, płyty, hak z linką, czujnik, klapy, drzwi żelazne, ruda
        Step(5, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ;
            for (int dx = -2; dx <= 12; dx++)
                for (int dz = -2; dz <= 10; dz++)
                {
                    World.SetBlock(x + dx, y - 1, z + dz, B.Stone);
                    for (int dy = 0; dy < 5; dy++) World.SetBlock(x + dx, y + dy, z + dz, B.Air);
                }
            // skrzynia -> komparator -> pył -> lampa; obok komparator odejmujący
            World.SetBlock(x, y, z, B.Chest);
            World.SetBlock(x + 1, y, z, B.Comparator, Dir.FaceToHoriz(Dir.East));
            World.SetBlock(x + 2, y, z, B.RedstoneWire);
            World.SetBlock(x + 3, y, z, B.RedstoneLamp);
            if (World.GetTileEntity(x, y, z) is Items.ChestTileEntity ch) for (int i = 0; i < 20; i++) ch.Set(i, new Items.ItemStack(B.Cobblestone, 64));
            World.SetBlock(x + 5, y, z, B.RedstoneBlock);
            World.SetBlock(x + 6, y, z, B.Comparator, Dir.FaceToHoriz(Dir.East) | 4);
            World.SetBlock(x + 7, y, z, B.RedstoneWire);
            World.SetBlock(x + 6, y, z + 1, B.RedstoneWire);
            World.SetBlock(x + 6, y, z + 2, B.RedstoneBlock);
            // płyty naciskowe z lampami pod spodem
            byte[] plates = { B.StonePressurePlate, B.WoodPressurePlate, B.LightWeightedPlate, B.HeavyWeightedPlate };
            for (int i = 0; i < 4; i++) { World.SetBlock(x + i * 2, y - 1, z + 3, B.RedstoneLamp); World.SetBlock(x + i * 2, y, z + 3, plates[i]); }
            World.DropItem(x + 2.5, y + 0.2, z + 3.5, new Items.ItemStack(B.Dirt, 1), false);
            for (int i = 0; i < 5; i++) World.DropItem(x + 4.5, y + 0.2, z + 3.5, new Items.ItemStack(B.Dirt, 1), false);
            // hak + linka + hak
            World.SetBlock(x - 1, y, z + 6, B.Stone); World.SetBlock(x + 5, y, z + 6, B.Stone);
            World.SetBlock(x, y, z + 6, B.TripwireHook, Dir.FaceToHoriz(Dir.East)); Block.Get(B.TripwireHook).OnPlaced(World, x, y, z + 6, Player);
            for (int i = 1; i <= 3; i++) World.SetBlock(x + i, y, z + 6, B.Tripwire);
            World.SetBlock(x + 4, y, z + 6, B.TripwireHook, Dir.FaceToHoriz(Dir.West)); Block.Get(B.TripwireHook).OnPlaced(World, x + 4, y, z + 6, Player);
            // czujniki światła, klapy, drzwi żelazne, ruda
            World.SetBlock(x + 8, y, z + 6, B.DaylightSensor);
            World.SetBlock(x + 9, y, z + 6, B.DaylightSensorInverted);
            World.SetBlock(x + 10, y, z + 6, B.RedstoneLamp);
            World.SetBlock(x + 7, y, z + 8, B.Stone);
            World.SetBlock(x + 8, y, z + 8, B.Trapdoor, 3 | 4);
            World.SetBlock(x + 7, y + 1, z + 8, B.Stone);
            World.SetBlock(x + 8, y + 1, z + 8, B.IronTrapdoor, 3 | 8);
            World.SetBlock(x + 10, y, z + 8, B.IronDoor, 0, false);
            World.SetBlock(x + 10, y + 1, z + 8, B.IronDoor, 8, true);
            World.SetBlock(x + 11, y, z + 8, B.Lever, Dir.Down);
            World.SetBlock(x + 1, y, z + 9, B.RedstoneOreLit);
            World.SetBlock(x + 2, y, z + 9, B.RedstoneOre);
        });
        Step(25, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ;
            Block.Get(B.Lever).OnActivated(World, x + 11, y, z + 8, Player);
            World.DropItem(x + 2.5, y + 0.1, z + 6.5, new Items.ItemStack(B.Dirt, 1), false);
        });
        Step(10, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ;
            bool cmp = World.GetBlock(x + 3, y, z) == B.RedstoneLampOn && World.GetMeta(x + 2, y, z) > 0;
            bool sub = World.GetMeta(x + 7, y, z) == 15 - World.GetMeta(x + 6, y, z + 1);
            bool plates = World.GetBlock(x + 2, y - 1, z + 3) == B.RedstoneLampOn && World.GetBlock(x, y - 1, z + 3) == B.RedstoneLamp && World.GetMeta(x + 4, y, z + 3) == 5;
            bool hook = (World.GetMeta(x, y, z + 6) & 12) == 12;
            bool door = (World.GetMeta(x + 10, y, z + 8) & 4) != 0;
            Log.Info($"AUTOTEST RS komparator={(cmp ? "OK" : "FAIL")} odejmowanie={(sub ? "OK" : "FAIL")} plyty={(plates ? "OK" : "FAIL")} hak={(hook ? "OK" : "FAIL")} drzwi={(door ? "OK" : "FAIL")}");
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(x + 5.5, y + 9, z - 3.5); Player.Yaw = 0; Player.Pitch = 55;
        });
        Step(3, () => Shot("rs_04_elementy"));
        Step(3, () =>
        {
            int x = _rsX + 14, y = _rsY, z = _rsZ;
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(x + 9.5, y + 2.5, z + 3.5); Player.Yaw = 0; Player.Pitch = 25;
        });
        Step(3, () => Shot("rs_05_drzwi_klapy"));
        // krok 5: leje, podajnik, dozownik, blok nut, skrzynia-pułapka i ich GUI
        Step(5, () =>
        {
            int x = _rsX, y = _rsY, z = _rsZ + 14;
            for (int dx = -2; dx <= 12; dx++)
                for (int dz = -2; dz <= 8; dz++)
                {
                    World.SetBlock(x + dx, y - 1, z + dz, B.Stone);
                    for (int dy = 0; dy < 5; dy++) World.SetBlock(x + dx, y + dy, z + dz, B.Air);
                }
            World.SetBlock(x, y + 1, z, B.Chest);
            if (World.GetTileEntity(x, y + 1, z) is Items.ChestTileEntity ch) ch.Set(0, new Items.ItemStack(B.Cobblestone, 64));
            World.SetBlock(x, y, z, B.Hopper, Dir.East);
            World.SetBlock(x + 1, y, z, B.Hopper, Dir.East);
            World.SetBlock(x + 2, y, z, B.Hopper, Dir.East);
            World.SetBlock(x + 3, y, z, B.Dropper, Dir.East);
            World.SetBlock(x + 4, y, z, B.Chest);
            World.SetBlock(x + 1, y, z + 1, B.Comparator, Dir.FaceToHoriz(Dir.South));
            World.SetBlock(x + 1, y, z + 2, B.RedstoneWire);
            World.SetBlock(x + 6, y, z, B.Dispenser, Dir.East);
            if (World.GetTileEntity(x + 6, y, z) is DispenserTileEntity d) { d.Set(0, new Items.ItemStack(Items.I.Arrow, 16)); d.Set(4, new Items.ItemStack(Items.I.WaterBucket, 1)); }
            World.SetBlock(x + 6, y, z + 1, B.Lever, Dir.Down);
            World.SetBlock(x + 9, y, z, B.NoteBlock);
            World.SetBlock(x + 10, y, z, B.TrappedChest);
            World.SetBlock(x + 11, y, z, B.RedstoneLamp);
        });
        Step(5, () => Block.Get(B.Lever).OnActivated(World, _rsX + 6, _rsY, _rsZ + 15, Player));
        Step(150, () =>
        {
            int x = _rsX, y = _rsY, z = _rsZ + 14;
            // leje przekazują bruk ze skrzyni do podajnika (podajnik bez impulsu trzyma przedmioty)
            int moved = World.GetTileEntity(x + 3, y, z) is DispenserTileEntity c ? c.Slots.Where(s => s != null).Sum(s => s.Count) : -1;
            bool cmp = World.GetMeta(x + 1, y, z + 2) > 0 || moved > 0;
            Log.Info($"AUTOTEST RS leje={(moved > 0 ? "OK" : "FAIL")}({moved}) komparator_leja={(cmp ? "OK" : "FAIL")}");
            Player.Flying = true; Player.MotionY = 0; Player.SetPositionAndPrev(x + 5.5, y + 7, z - 4.5); Player.Yaw = 0; Player.Pitch = 45;
        });
        Step(3, () => Shot("rs_06_leje"));
        Step(3, () => Block.Get(B.Hopper).OnActivated(World, _rsX + 1, _rsY, _rsZ + 14, Player));
        Step(5, () => Shot("rs_07_gui_leja"));
        Step(3, () => { Player.CloseContainer(); SetScreen(null); Block.Get(B.Dispenser).OnActivated(World, _rsX + 6, _rsY, _rsZ + 14, Player); });
        Step(5, () => Shot("rs_08_gui_dozownika"));
        Step(3, () => { Player.CloseContainer(); SetScreen(null); Block.Get(B.TrappedChest).OnActivated(World, _rsX + 10, _rsY, _rsZ + 14, Player); });
        Step(5, () =>
        {
            bool lamp = World.GetBlock(_rsX + 11, _rsY, _rsZ + 14) == B.RedstoneLampOn;
            Log.Info($"AUTOTEST RS skrzynia_pulapka={(lamp ? "OK" : "FAIL")}");
            Shot("rs_09_gui_skrzyni");
        });
        Step(3, () => { Player.CloseContainer(); SetScreen(null); });
        if (only) Step(5, () => { Log.Info("AUTOTEST DONE"); Close(); });
    }
}
