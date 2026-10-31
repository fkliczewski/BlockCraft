using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Autotest modułu ME: BlockCraft.exe --autotest --me (stół do zaklinania, zrzuty autotest/me_*.png, wyniki "ME-TEST").</summary>
public sealed partial class Game
{
    private int _meFails;
    private void MeCheck(bool ok, string name, string detail = "")
    {
        if (!ok) _meFails++;
        Log.Info($"ME-TEST {(ok ? "OK" : "FAIL")} {name} {detail}");
    }

    private void SetupAutotestMechanics()
    {
        int t0 = 0, tx = 0, ty = 0, tz = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(5, () => CreateWorld("AutoTestME " + DateTime.Now.ToString("HHmmss"), 4343, 0, false, WorldType.Superflat, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        Step(10, () =>
        {
            World.DayTime = 6000; World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false;
            int px = MathUtil.FloorToInt(Player.X), py = MathUtil.FloorToInt(Player.Y), pz = MathUtil.FloorToInt(Player.Z);
            tx = px; ty = py; tz = pz + 4;
            for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) for (int y = 0; y <= 2; y++) World.SetBlock(tx + x, ty + y, tz + z, B.Air);
            World.SetBlock(tx, ty, tz, B.EnchantingTable);
            for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
                if (Math.Max(Math.Abs(x), Math.Abs(z)) == 2 && z != -2) for (int y = 0; y <= 1; y++) World.SetBlock(tx + x, ty + y, tz + z, B.Bookshelf);
            Player.SetPositionAndPrev(tx + .5, ty, tz - 2.5); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 25;
            Player.XpLevel = 30; HideGui = true;
            MeCheck(EnchantingRules.BookshelfPower(World, tx, ty, tz) == 15, "biblioteczki", EnchantingRules.BookshelfPower(World, tx, ty, tz).ToString());
        });
        Step(30, () => Shot("me_01_stol"));
        EnchantingContainer ec = null; EnchantOffer offer = null; int seed = 0;
        Step(5, () =>
        {
            HideGui = false;
            OpenBlockGui(GuiKind.Enchanting, tx, ty, tz);
            ec = Player.OpenContainer as EnchantingContainer;
            MeCheck(ec != null && CurrentScreen is Gui.EnchantingScreen, "otwarcie GUI");
            ec.Inv.Set(0, new ItemStack(I.DiamondSword)); ec.Inv.Set(1, new ItemStack(I.Lapis, 5));
            var o = ec.Offers; seed = Player.EnchantmentSeed;
            Log.Info($"ME-TEST oferty {o[0].RequiredLevel}/{o[1].RequiredLevel}/{o[2].RequiredLevel} podpowiedź {o[2].Clue}");
            MeCheck(o[2].RequiredLevel == 30 && o[0].RequiredLevel > 0 && ec.ButtonState(2) == 2, "trzy oferty przy 15 półkach");
            offer = o[2];
        });
        Step(10, () => Shot("me_02_gui"));
        Step(5, () =>
        {
            MeCheck(ec.Enchant(Player, 2), "zaklinanie slot 3");
            var sword = ec.Inv.Get(0);
            var got = EnchantHelper.GetAll(sword);
            MeCheck(offer.Enchantments.All(e => got.TryGetValue(e.Id, out int l) && l == e.Level) && got.Count == offer.Enchantments.Count, "zaklęcia zgodne z ofertą", string.Join(",", got.Select(kv => kv.Key + " " + kv.Value)));
            MeCheck(Player.XpLevel == 27 && ec.Lapis == 2 && Player.EnchantmentSeed != seed, "koszt 3 poziomy i 3 lazuryty, nowe ziarno", $"{Player.XpLevel} {ec.Lapis}");
            MeCheck(ec.ButtonState(0) == 0, "zaklęty przedmiot bez ofert");
        });
        Step(10, () => Shot("me_03_po_zakleciu"));
        Step(5, () =>
        {
            var sword = ec.Inv.Get(0); ec.Inv.Set(0, new ItemStack(I.Book));
            MeCheck(ec.ButtonState(1) == 2 && ec.Enchant(Player, 1), "zaklinanie książki");
            var book = ec.Inv.Get(0);
            MeCheck(book.Id == I.EnchantedBook && EnchantHelper.GetAll(book, "StoredEnchantments").Count > 0 && EnchantHelper.GetAll(book).Count == 0, "książka -> zaklęta książka (StoredEnchantments)");
            MeCheck(ec.Lapis == 0 && Player.XpLevel == 25, "koszt 2 poziomy i 2 lazuryty");
            MeCheck(ec.ButtonState(0) == 0, "bez lazurytu brak oferty do kliknięcia");
            Player.Inventory.Add(sword);
            SetScreen(null);
            MeCheck(Player.Inventory.Main.Any(s => s?.Id == I.EnchantedBook), "zamknięcie zwraca przedmioty");
            Player.Inventory.Add(new ItemStack(I.GoldenApple, 1, 1));
            SetScreen(new Gui.ContainerScreen(Player, Player.InventoryContainer));
        });
        Step(10, () => Shot("me_04_ekwipunek_polysk"));
        AnvilContainer ac = null;
        Step(5, () =>
        {
            SetScreen(null);
            for (int d = 0; d < 3; d++) World.SetBlock(tx - 1 + d, ty, tz - 2, B.Anvil, d << 2 | (d == 1 ? 1 : 0));
            Player.SetPositionAndPrev(tx + .5, ty, tz - 4.5); Player.Pitch = Player.PrevPitch = 30; HideGui = true;
        });
        Step(20, () => Shot("me_05_kowadla"));
        Step(5, () =>
        {
            HideGui = false;
            OpenBlockGui(GuiKind.Anvil, tx, ty, tz - 2);
            ac = Player.OpenContainer as AnvilContainer;
            MeCheck(ac != null && CurrentScreen is Gui.AnvilScreen, "otwarcie kowadła");
            var s = new ItemStack(I.DiamondSword); EnchantHelper.Set(s, Ench.Sharpness, 3);
            var book = new ItemStack(I.EnchantedBook); EnchantingRules.AddStored(book, new EnchantmentRoll(Ench.Sharpness, 3));
            ac.Inv.Set(0, s); ac.Inv.Set(1, book); ac.Inv.Changed();
        });
        Step(5, () =>
        {
            foreach (char ch in "Ostrze") CurrentScreen.CharTyped(ch);
            MeCheck(ac.MaximumCost == 5 && ac.Output.Get(0)?.CustomName != null && EnchantHelper.GetLevel(ac.Output.Get(0), Ench.Sharpness) == 4, "kowadło: Ostrość IV i nazwa za 5", ac.MaximumCost + " " + ac.Output.Get(0)?.CustomName);
        });
        Step(10, () => Shot("me_06_kowadlo_gui"));
        Step(5, () =>
        {
            int lvl = Player.XpLevel;
            ac.Click(2, 0, ClickType.QuickMove, Player);
            MeCheck(Player.XpLevel == lvl - 5 && Player.Inventory.Main.Any(x => x != null && EnchantHelper.GetLevel(x, Ench.Sharpness) == 4), "kowadło: odbiór", $"{lvl} -> {Player.XpLevel}");
        });
        Step(5, () =>
        {
            SetScreen(null);
            for (int i = 0; i < 36; i++) Player.Inventory.Main[i] = null;
            Player.Inventory.Main[0] = new ItemStack(I.Compass); Player.Inventory.Main[1] = new ItemStack(I.Clock); Player.Inventory.Main[2] = new ItemStack(B.Jukebox);
            for (int i = 0; i < 12; i++) Player.Inventory.Main[9 + i] = new ItemStack(I.Record13 + i);
            Player.Inventory.Main[3] = new ItemStack(B.Anvil, 1, 0); Player.Inventory.Main[4] = new ItemStack(B.Anvil, 1, 1); Player.Inventory.Main[5] = new ItemStack(B.Anvil, 1, 2);
            World.SetBlock(tx, ty, tz - 3, B.Jukebox);
            SetScreen(new Gui.ContainerScreen(Player, Player.InventoryContainer));
        });
        Step(15, () => Shot("me_07_kompas_zegar_plyty"));
        Step(5, () => { Log.Info($"ME-TEST KONIEC: {(_meFails == 0 ? "OK" : _meFails + " FAIL")}"); Log.Info("AUTOTEST DONE"); Close(); });
    }
}
