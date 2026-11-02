using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Autotest pionka BL2: BlockCraft.exe --autotest --bl2 (zrzuty autotest/bl2_*.png, wyniki "BL2-TEST").</summary>
public sealed partial class Game
{
    private int _bl2Fails;
    private void Bl2Check(bool ok, string name, string detail = "")
    {
        if (!ok) _bl2Fails++;
        Log.Info($"BL2-TEST {(ok ? "OK" : "FAIL")} {name} {detail}");
    }

    private void SetupAutotestBl2()
    {
        int t0 = 0, tx = 0, ty = 0, tz = 0;
        Player other = null; EnderChestTile open = null;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(5, () => CreateWorld("AutoTestBL2 " + DateTime.Now.ToString("HHmmss"), 2211, 0, false, WorldType.Superflat, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        Step(10, () =>
        {
            World.DayTime = 6000; World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false;
            int px = MathUtil.FloorToInt(Player.X), py = MathUtil.FloorToInt(Player.Y), pz = MathUtil.FloorToInt(Player.Z);
            tx = px; ty = py; tz = pz + 4;
            for (int x = -6; x <= 6; x++) for (int z = -3; z <= 6; z++) for (int y = 0; y <= 4; y++) World.SetBlock(tx + x, ty + y, tz + z, B.Air);
            // skrzynie Endu: zamknięta i otwarta (patrzy drugi gracz), przodem do kamery
            World.SetBlock(tx - 2, ty, tz, B.EnderChest, Dir.North);
            World.SetBlock(tx + 2, ty, tz, B.EnderChest, Dir.North);
            World.SetBlock(tx + 4, ty, tz + 1, B.EnderChest, Dir.West);
            open = World.GetTileEntity(tx + 2, ty, tz) as EnderChestTile;
            other = new Player(World) { Name = "Patrzacy" }; other.SetPositionAndPrev(tx + 2.5, ty, tz + 2.5);
            other.OpenContainer = EnderChestTile.CreateContainer(World, other, tx + 2, ty, tz);
            // doniczki z roślinami
            int[][] plants = { new[] { B.Poppy, 0 }, new[] { B.Poppy, 3 }, new[] { B.TallGrass, 2 }, new[] { B.Cactus, 0 }, new[] { B.OakSapling, 0 }, new[] { B.RedMushroom, 0 }, new[] { B.DeadBush, 0 }, new[] { B.Dandelion, 0 } };
            for (int i = 0; i < plants.Length; i++)
            {
                int x = tx - 4 + i, z = tz - 1;
                World.SetBlock(x, ty, z, B.FlowerPot);
                if (World.GetTileEntity(x, ty, z) is FlowerPotTile pot) { pot.ItemId = plants[i][0]; pot.Data = plants[i][1]; }
            }
            World.SetBlock(tx + 5, ty, tz - 1, B.FlowerPot);
            Player.SetPositionAndPrev(tx + .5, ty, tz - 3.5); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 30;
            HideGui = true;
        });
        Step(40, () =>
        {
            Bl2Check(open?.Viewers == 1 && open.Lid == 1, "skrzynia Endu otwarta przez drugiego gracza", $"widzow {open?.Viewers} wieko {open?.Lid}");
            Shot("bl2_01_skrzynie_doniczki");
        });
        Step(5, () => { Player.SetPositionAndPrev(tx + 2.5, ty + 1.2, tz - 1.6); Player.Pitch = Player.PrevPitch = 40; });
        Step(20, () => Shot("bl2_02_skrzynia_blisko"));
        Step(5, () => { Player.SetPositionAndPrev(tx - .5, ty + .6, tz - 2.6); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 35; });
        Step(20, () => Shot("bl2_03_doniczki_blisko"));
        // GUI skrzyni Endu
        Step(5, () =>
        {
            HideGui = false; Player.SetPositionAndPrev(tx - 1.5, ty, tz - 1.5);
            Player.EnderInventory.Set(0, new ItemStack(I.Diamond, 12)); Player.EnderInventory.Set(13, new ItemStack(B.Obsidian, 8));
            if (World.GetBlock(tx - 2, ty, tz) == B.EnderChest) Block.Get(B.EnderChest).OnActivated(World, tx - 2, ty, tz, Player);
            Bl2Check(Player.OpenContainer is RsContainer rc && rc.Inv == Player.EnderInventory, "GUI skrzyni Endu otwarte");
            Bl2Check(Player.Statistics.GetValueOrDefault("stat.enderchestOpened") == 1, "statystyka otwarcia skrzyni Endu");
        });
        Step(20, () => Shot("bl2_04_skrzynia_gui"));
        Step(5, () => SetScreen(null));
        // bariera: widoczna jako cząsteczka w kreatywnym z barierą w ręce
        Step(5, () =>
        {
            for (int x = -1; x <= 1; x++) for (int y = 0; y <= 2; y++) World.SetBlock(tx + x, ty + y, tz + 5, B.Barrier);
            Player.SetGameMode(1); Player.Inventory.Main[0] = new ItemStack(B.Barrier); Player.Inventory.SelectedSlot = 0;
            Player.SetPositionAndPrev(tx + .5, ty, tz + 1.5); Player.Pitch = Player.PrevPitch = 5; HideGui = false;
        });
        Step(30, () => Bl2Check(World.GetBlock(tx, ty + 1, tz + 5) == B.Barrier, "bariery postawione"));
        Step(40, () => Shot("bl2_05_bariera_czasteczki"));
        Step(5, () => Player.Inventory.SelectedSlot = 1);
        Step(90, () => Shot("bl2_06_bariera_niewidoczna"));
        // książka z piórem: edycja, podpis, czytanie
        BookScreen bs = null;
        Step(5, () =>
        {
            Player.SetGameMode(0);
            Player.Inventory.Main[2] = new ItemStack(I.WritableBook); Player.Inventory.SelectedSlot = 2;
            Player.Inventory.Main[3] = new ItemStack(I.FlowerPotItem, 3); Player.Inventory.Main[4] = new ItemStack(B.EnderChest);
            Books.Open(Player, Player.Inventory.Selected, true);
            bs = CurrentScreen as BookScreen;
            if (bs != null) foreach (char c in "Dziennik wyprawy. Dzien pierwszy: znalezlismy twierdze i portal do Endu. Skrzynia Endu trzyma nasze diamenty.") bs.CharTyped(c);
            Bl2Check(bs != null && bs.Pages[0].StartsWith("Dziennik"), "edycja książki");
        });
        Step(10, () => Shot("bl2_07_ksiazka_edycja"));
        Step(5, () => { bs?.NextPage(); foreach (char c in "Strona druga.") bs?.CharTyped(c); });
        Step(10, () => Shot("bl2_08_ksiazka_strona2"));
        Step(5, () =>
        {
            if (bs == null) return;
            foreach (var w in typeof(Screen).GetField("Widgets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(bs) as List<Widget> ?? new())
                if (w is Button b && b.Text() == "Podpisz") b.OnClick();
            foreach (char c in "Kronika Endu") bs.CharTyped(c);
            Bl2Check(bs.Signing && bs.TitleText == "Kronika Endu", "tryb podpisywania");
        });
        Step(10, () => Shot("bl2_09_ksiazka_podpis"));
        Step(5, () =>
        {
            bool sent = bs?.Send(true) == true; SetScreen(null);
            var book = Player.Inventory.Selected;
            Bl2Check(sent && book?.Id == I.WrittenBook && Books.Title(book) == "Kronika Endu" && Books.Pages(book).Count == 2, "podpisana książka", book?.ToString());
            Books.Open(Player, book, false);
            Bl2Check(CurrentScreen is BookScreen, "czytanie napisanej książki");
        });
        Step(10, () => Shot("bl2_10_ksiazka_czytanie"));
        Step(5, () =>
        {
            SetScreen(null); HideGui = false;
            Player.Inventory.Main[5] = new ItemStack(I.WrittenBook) { Tag = Player.Inventory.Main[2]?.Tag?.Copy() };
            Player.Inventory.Main[6] = new ItemStack(I.WritableBook);
            SetScreen(new ContainerScreen(Player, Player.InventoryContainer));
        });
        Step(10, () => Shot("bl2_11_ekwipunek_ikony"));
        // statystyki 1.8
        StatsScreen ss = null;
        Step(5, () =>
        {
            SetScreen(null);
            Bl2Stats.Add(Player, Bl2Stats.Mine + B.Stone, 37); Bl2Stats.Add(Player, Bl2Stats.Mine + B.Dirt, 12); Bl2Stats.Add(Player, Bl2Stats.Use + B.EnderChest, 2);
            Bl2Stats.Add(Player, Bl2Stats.Craft + I.WritableBook); Bl2Stats.Add(Player, Bl2Stats.Break + I.IronPickaxe); Bl2Stats.Add(Player, Bl2Stats.Use + I.Bread, 6);
            Bl2Stats.Add(Player, Bl2Stats.KillEntity + "zombie", 5); Bl2Stats.Add(Player, Bl2Stats.KilledBy + "creeper", 2); Bl2Stats.Add(Player, Bl2Stats.KillEntity + "creeper", 1);
            SetScreen(new PauseScreen());
        });
        Step(10, () => Shot("bl2_12_menu_pauzy"));
        Step(5, () => { ss = new StatsScreen(CurrentScreen); SetScreen(ss); });
        Step(10, () =>
        {
            Bl2Check(Player.Statistics.GetValueOrDefault("stat.playOneMinute") > 100 && Player.Statistics.GetValueOrDefault("stat.walkOneCm") >= 0, "statystyki czasu liczone");
            Shot("bl2_13_statystyki_ogolne");
        });
        Step(5, () => ss.Select(StatsScreen.Tab.Blocks));
        Step(10, () => Shot("bl2_14_statystyki_bloki"));
        Step(5, () => ss.Select(StatsScreen.Tab.Items));
        Step(10, () => Shot("bl2_15_statystyki_przedmioty"));
        Step(5, () => ss.Select(StatsScreen.Tab.Mobs));
        Step(10, () => Shot("bl2_16_statystyki_moby"));
        Step(5, () => { SetScreen(null); other.OpenContainer = null; });
        Step(30, () => { Bl2Check(open.Viewers == 0 && open.Lid == 0, "wieko zamknięte po wyjściu patrzącego", $"wieko {open.Lid}"); });
        Step(5, () => { Log.Info($"BL2-TEST KONIEC: {(_bl2Fails == 0 ? "OK" : _bl2Fails + " FAIL")}"); Log.Info("AUTOTEST DONE"); Close(); });
    }
}
