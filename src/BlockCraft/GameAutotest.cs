using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Automatyczny test wizualny: BlockCraft.exe --autotest (zrzuty do folderu autotest).</summary>
public sealed partial class Game
{
    private readonly Queue<(int frames, Func<bool> action)> _steps = new();
    private int _waitFrames;
    private string _shotDir;

    private void Shot(string name) => TakeScreenshot(Path.Combine(_shotDir, name + ".png"));

    private void Step(int frames, Action a) => _steps.Enqueue((frames, () => { a(); return true; }));
    private void WaitUntil(Func<bool> cond) => _steps.Enqueue((1, cond));

    private void SetupAutotest()
    {
        _shotDir = Path.Combine(AppContext.BaseDirectory, "autotest");
        Directory.CreateDirectory(_shotDir);
        foreach (var f in Directory.GetFiles(_shotDir, "*.png")) File.Delete(f);
        if (Environment.GetEnvironmentVariable("BC_AUTOTEST") == "rs") { AutotestRedstone(true); return; }
        if (_args.Contains("--ne")) { SetupAutotestNetherEnd(); return; }
        if (_args.Contains("--mo")) { SetupAutotestMobs(); return; }
        if (_args.Contains("--ow")) { SetupAutotestOverworld(); return; }
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(30, () => Shot("01_title"));
        Step(5, () => SetScreen(new OptionsScreen(new TitleScreen())));
        Step(5, () => Shot("02_options"));
        Step(5, () => SetScreen(new BlockCraft.Audio.SoundOptionsScreen(new TitleScreen())));
        Step(5, () => { Shot("02a_dzwieki"); BlockCraft.Core.Log.Info("AUDIO: enabled=" + Sound.Enabled + " voices=" + Sound.ActiveVoices); });
        Step(2, () => { Sound.PlayUi("random.click"); Sound.Play("dig.stone", 0, 0, 0); });
        Step(10, () => BlockCraft.Core.Log.Info("AUDIO: po odtworzeniu voices=" + Sound.ActiveVoices));
        Step(5, () => SetScreen(new VideoScreen(new TitleScreen())));
        Step(5, () => Shot("02b_video"));
        Step(5, () => SetScreen(new CreateWorldScreen(new TitleScreen())));
        Step(5, () => Shot("03_create"));
        Step(5, () => CreateWorld("AutoTest " + DateTime.Now.ToString("HHmmss"), 12345, 0, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        Step(60, () => { Player.Yaw = 30; Player.Pitch = 10; World.DayTime = 6000; });
        Step(30, () => Shot("04_world"));
        Step(5, () =>
        {
            Player.Inventory.Add(new ItemStack(I.IronPickaxe)); Player.Inventory.Add(new ItemStack(B.OakPlanks, 32)); Player.Inventory.Add(new ItemStack(B.Torch, 16));
            Player.Inventory.Add(new ItemStack(I.Bread, 5)); Player.Inventory.Add(new ItemStack(B.CraftingTable)); Player.Inventory.Add(new ItemStack(I.DiamondSword));
            Player.Inventory.Add(new ItemStack(B.Grass, 10)); Player.Inventory.Add(new ItemStack(B.Furnace)); Player.Inventory.Armor[1] = new ItemStack(I.IronChest);
            Player.Inventory.SelectedSlot = 0;
        });
        Step(20, () => Shot("05_hand_pickaxe"));
        Step(1, () => Player.Inventory.SelectedSlot = 1);
        Step(20, () => Shot("06_hand_block"));
        Step(1, () => Player.Inventory.SelectedSlot = 8);
        Step(20, () => Shot("06b_hand_empty"));
        Step(1, () => { CameraMode = 1; Player.Inventory.SelectedSlot = 5; });
        Step(20, () => Shot("07_third_back"));
        Step(1, () => CameraMode = 2);
        Step(20, () => Shot("08_third_front"));
        Step(1, () => CameraMode = 0);
        Step(5, () => SetScreen(new ContainerScreen(Player, Player.InventoryContainer)));
        Step(10, () => Shot("09_inventory"));
        // kopanie i stawianie przez kontroler (jak myszką)
        int mx = 0, my = 0, mz = 0; byte before = 0;
        Step(5, () =>
        {
            SetScreen(null);
            Player.Inventory.SelectedSlot = 0; Player.Pitch = 55; Player.Yaw = 0;
            Ctrl.UpdateTarget(1f);
            mx = Ctrl.Hit.X; my = Ctrl.Hit.Y; mz = Ctrl.Hit.Z; before = World.GetBlock(mx, my, mz);
            Log.Info($"AUTOTEST target {mx} {my} {mz} = {Block.Get(before).Key} hit={Ctrl.Hit.Hit}");
            _leftDown = true; _leftPressed = true;
        });
        Step(60, () =>
        {
            _leftDown = false;
            byte now = World.GetBlock(mx, my, mz);
            Log.Info($"AUTOTEST mine: {(now != before ? "OK" : "FAIL")} ({Block.Get(before).Key} -> {Block.Get(now).Key}), inv dirt={Player.Inventory.CountOf(B.Dirt)}");
            Player.Inventory.SelectedSlot = 1;
            _rightPressed = true;
        });
        Step(10, () =>
        {
            byte now = World.GetBlock(mx, my, mz);
            Log.Info($"AUTOTEST place: {(now == B.OakPlanks ? "OK" : "FAIL")} ({Block.Get(now).Key}), planks left={Player.Inventory.CountOf(B.OakPlanks)}");
            Shot("06c_after_place");
        });
        Step(5, () =>
        {
            int x = MathUtil.FloorToInt(Player.X) - 2, z = MathUtil.FloorToInt(Player.Z), y = MathUtil.FloorToInt(Player.Y);
            World.SetBlock(x, y, z, B.CraftingTable, 0);
            var wb = new WorkbenchContainer(Player.Inventory, x, y, z);
            wb.CraftGrid.Set(0, new ItemStack(B.Cobblestone)); wb.CraftGrid.Set(1, new ItemStack(B.Cobblestone)); wb.CraftGrid.Set(2, new ItemStack(B.Cobblestone));
            wb.CraftGrid.Set(4, new ItemStack(I.Stick)); wb.CraftGrid.Set(7, new ItemStack(I.Stick)); wb.UpdateCrafting();
            Player.OpenContainer = wb; SetScreen(new ContainerScreen(Player, wb));
        });
        Step(10, () => Shot("10_crafting"));
        Step(5, () =>
        {
            SetScreen(null);
            int x = MathUtil.FloorToInt(Player.X) + 2, z = MathUtil.FloorToInt(Player.Z), y = MathUtil.FloorToInt(Player.Y);
            World.SetBlock(x, y, z, B.Furnace, 0);
            OpenBlockGui(GuiKind.Furnace, x, y, z);
        });
        Step(10, () => Shot("11_furnace"));
        Step(5, () => { SetScreen(null); Cmds.Execute(Player, "/gamemode creative"); SetScreen(new CreativeScreen(Player)); });
        Step(10, () => Shot("12_creative"));
        Step(5, () =>
        {
            SetScreen(null);
            Player.Flying = true; Player.Pitch = 15;
            var (lx, _, lz) = Player.Look();
            string[] mobs = { "zombie", "skeleton", "creeper", "spider", "enderman" };
            for (int i = 0; i < mobs.Length; i++)
            {
                double ox = Player.X + lx * 6 + (i - 2) * 1.8 * lz, oz = Player.Z + lz * 6 - (i - 2) * 1.8 * lx;
                Cmds.Execute(Player, $"/summon {mobs[i]} {ox.ToString(System.Globalization.CultureInfo.InvariantCulture)} {Player.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {oz.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
            World.Rules.DoDaylightCycle = false;
        });
        Step(40, () => Shot("13_monsters_day"));
        Step(5, () =>
        {
            foreach (var e in World.Entities.ToList()) if (e is Monster) e.Kill();
            var (lx, _, lz) = Player.Look();
            string[] mobs = { "cow", "pig", "sheep", "chicken" };
            for (int i = 0; i < mobs.Length; i++)
            {
                double ox = Player.X + lx * 5 + (i - 1.5) * 2 * lz, oz = Player.Z + lz * 5 - (i - 1.5) * 2 * lx;
                Cmds.Execute(Player, $"/summon {mobs[i]} {ox.ToString(System.Globalization.CultureInfo.InvariantCulture)} {Player.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)} {oz.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            }
        });
        Step(40, () => Shot("14_animals"));
        Step(5, () => { Cmds.Execute(Player, "/time set 12500"); Player.Pitch = -10; });
        Step(20, () => Shot("15_sunset"));
        Step(5, () => { Cmds.Execute(Player, "/time set midnight"); Player.Pitch = -30; });
        Step(20, () => Shot("16_night"));
        Step(5, () => { Cmds.Execute(Player, "/time set noon"); Player.Pitch = -25; Player.SetPositionAndPrev(Player.X, 115, Player.Z); });
        Step(30, () => Shot("17_clouds"));
        Step(5, () => { Player.SetPositionAndPrev(Player.X, World.GetHeight(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z)) + 1.5, Player.Z); Player.Pitch = 20; SetScreen(new ChatScreen("/time set day")); AddChat("<Steve> Witaj świecie! Zażółć gęślą jaźń"); });
        Step(10, () => Shot("18_chat"));
        Step(5, () => { SetScreen(null); ShowDebug = true; });
        Step(10, () => Shot("19_debug"));
        Step(5, () => { ShowDebug = false; SetScreen(new PauseScreen()); });
        Step(10, () => Shot("20_pause"));
        Step(5, () =>
        {
            SetScreen(null);
            // pustynia
            var g = World.Generator;
            for (int r = 0; r < 3000; r += 64)
            {
                bool found = false;
                for (int a = 0; a < 16 && !found; a++)
                {
                    int x = (int)(Math.Cos(a / 16.0 * Math.PI * 2) * r), z = (int)(Math.Sin(a / 16.0 * Math.PI * 2) * r);
                    if (g.BiomeAt(x, z) == Biome.Desert && g.BiomeAt(x + 40, z) == Biome.Desert) { Player.SetPositionAndPrev(x, 100, z); found = true; }
                }
                if (found) break;
            }
            Player.Pitch = 20;
        });
        int t2 = 0;
        WaitUntil(() => ++t2 > 400 || (World.GetChunk(MathUtil.FloorToInt(Player.X) >> 4, MathUtil.FloorToInt(Player.Z) >> 4)?.Merged == true && Renderer.PendingMeshes < 10 && t2 > 100));
        Step(30, () => Shot("21_desert"));
        Step(5, () =>
        {
            // pod ziemią z pochodnią
            int x = MathUtil.FloorToInt(Player.X), z = MathUtil.FloorToInt(Player.Z);
            Cmds.Execute(Player, "/gamemode survival");
            for (int dx = -3; dx <= 3; dx++) for (int dz = -3; dz <= 3; dz++) for (int dy = 0; dy < 4; dy++) World.SetBlock(x + dx, 20 + dy, z + dz, dy == 0 ? B.Stone : B.Air);
            World.SetBlock(x + 2, 21, z + 2, B.Torch, 0);
            World.SetBlock(x - 2, 21, z - 3, B.DiamondOre); World.SetBlock(x - 1, 21, z - 3, B.IronOre); World.SetBlock(x, 21, z - 3, B.GoldOre); World.SetBlock(x + 1, 21, z - 3, B.RedstoneOre); World.SetBlock(x + 2, 21, z - 3, B.LapisOre);
            Player.SetPositionAndPrev(x + 0.5, 21, z + 0.5); Player.Yaw = 180; Player.Pitch = 15; Player.Flying = false;
        });
        Step(30, () => Shot("22_cave_torch"));
        Step(5, () => { World.DayTime = 6000; Player.SetPositionAndPrev(Player.X, World.GetHeight(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z)) + 1, Player.Z); _autoInput = (true, false, false, false); });
        Step(60, () => { _autoInput = null; Shot("23_after_walk"); Log.Info($"AUTOTEST walk: pos {Player.X:F1} {Player.Y:F1} {Player.Z:F1} hp {Player.Health} food {Player.FoodLevel}"); });
        AutotestRedstone(false);
        Step(5, () => QuitToTitle());
        Step(30, () => Shot("24_title_again"));
        Step(5, () => { Log.Info("AUTOTEST DONE"); Close(); });
    }

    private void AutotestStep()
    {
        if (ArgsNetAutotest && !_netTestFinished && DateTime.UtcNow > _netTestDeadline)
        {
            Shot("99_timeout");
            NetTestFinish(false, "Przekroczony czas testu; ekran=" + _screen?.GetType().Name + "; " + HostStatus);
            return;
        }
        if (_steps.Count == 0) return;
        if (_waitFrames > 0) { _waitFrames--; return; }
        var (frames, act) = _steps.Peek();
        bool ok;
        try { ok = act(); }
        catch (Exception ex)
        {
            Log.Error("Autotest step: " + ex);
            if (ArgsNetAutotest) { NetTestFinish(false, ex.Message); return; }
            ok = true;
        }
        if (_steps.Count == 0) return;
        if (ok) { _steps.Dequeue(); if (_steps.Count > 0) _waitFrames = _steps.Peek().frames; }
    }
}
