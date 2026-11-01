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
        Step(5, () =>
        {
            SetScreen(null); HideGui = true;
            int wx = tx + 10, wz = tz;
            for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) { World.SetBlock(wx + x, ty - 1, wz + z, B.Water); World.SetBlock(wx + x, ty, wz + z, B.Air); }
            var b = new BoatEntity(World); b.SetPositionAndPrev(wx + .5, ty, wz + .5); b.Yaw = b.PrevYaw = 30; World.SpawnEntity(b);
            Player.SetPositionAndPrev(wx + .5, ty + 1, wz - 3.5); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 35;
        });
        Step(60, () => Shot("me_08_lodka"));
        Step(5, () =>
        {
            int sx = tx - 10, sz = tz;
            for (int x = -4; x <= 4; x++) for (int z = -4; z <= 4; z++) for (int y = 0; y <= 3; y++) World.SetBlock(sx + x, ty + y, sz + z, B.Air);
            int[] metas = { 0, 2, 14 };
            for (int i = 0; i < 3; i++)
            {
                World.SetBlock(sx - 2 + i * 2, ty, sz, B.StandingSign, metas[i]);
                if (World.GetTileEntity(sx - 2 + i * 2, ty, sz) is SignTile st) { st.Lines[0] = "Obrót"; st.Lines[1] = metas[i].ToString(); st.Lines[3] = "BlockCraft"; }
            }
            World.SetBlock(sx, ty + 1, sz - 2, B.Stone); World.SetBlock(sx, ty + 2, sz - 2, B.Stone);
            World.SetBlock(sx, ty + 2, sz - 1, B.WallSign, 3);
            if (World.GetTileEntity(sx, ty + 2, sz - 1) is SignTile ws) { ws.Lines[1] = "Ścienna"; ws.Lines[2] = "tabliczka"; }
            Player.SetPositionAndPrev(sx + .5, ty, sz + 3.5); Player.Yaw = Player.PrevYaw = 180; Player.Pitch = Player.PrevPitch = 10;
        });
        Step(30, () => Shot("me_09_tabliczki"));
        Step(5, () => { HideGui = false; OpenBlockGui(GuiKind.SignEdit, tx - 10, ty, tz); foreach (char ch in "Edycja") CurrentScreen.CharTyped(ch); });
        Step(10, () => Shot("me_10_tabliczka_edycja"));
        Step(5, () =>
        {
            SetScreen(null);
            MeCheck(World.GetTileEntity(tx - 10, ty, tz) is SignTile e && e.Lines[0] == "ObrótEdycja", "tabliczka: zapis po zamknięciu edytora", (World.GetTileEntity(tx - 10, ty, tz) as SignTile)?.Lines[0]);
        });
        // ME-g: obrazy, ramki, stojak na zbroję, zbroja na graczu
        Step(5, () =>
        {
            HideGui = true;
            int wx = tx, wz = tz + 16;
            for (int x = -8; x <= 8; x++) for (int z = -10; z <= 0; z++) for (int y = 0; y <= 5; y++) World.SetBlock(wx + x, ty + y, wz + z, B.Air);
            for (int x = -7; x <= 7; x++) for (int y = 0; y <= 4; y++) World.SetBlock(wx + x, ty + y, wz, B.Stone);
            foreach (var e in World.Entities.Where(e => e is HangingEntity or ArmorStandEntity).ToList()) e.Kill();
            void Hang(string art, int x, int y)
            {
                var pe = new PaintingEntity(World) { HangX = x, HangY = y, HangZ = wz - 1, Art = PaintingArt.IndexOf(art) }; pe.SetFacing(2);
                MeCheck(pe.OnValidSurface(), "obraz " + art + " mieści się"); World.SpawnEntity(pe);
            }
            Hang("Pointer", wx - 4, ty + 1); Hang("Sunset", wx, ty + 3); Hang("Kebab", wx + 2, ty + 1); Hang("Wanderer", wx + 4, ty + 1);
            void Frame(int x, int y, ItemStack it, int rot)
            {
                var f = ItemFrameEntity.Create(World, x, y, wz - 1, 2); f.Item = it; f.Rotation = rot;
                MeCheck(f.OnValidSurface(), "ramka mieści się"); World.SpawnEntity(f);
            }
            Frame(wx + 2, ty + 3, new ItemStack(I.DiamondSword), 1); Frame(wx + 6, ty + 1, new ItemStack(B.Stone), 0); Frame(wx - 1, ty + 1, null, 0);
            var st = new ArmorStandEntity(World); st.SetPositionAndPrev(wx + 1.5, ty, wz - 3.5); st.Yaw = st.PrevYaw = 180;
            st.Armor[0] = new ItemStack(I.DiamondHelmet); st.Armor[1] = new ItemStack(I.IronChest); st.Armor[2] = new ItemStack(I.GoldLegs); st.Armor[3] = new ItemStack(I.LeatherBoots);
            World.SpawnEntity(st);
            var bare = new ArmorStandEntity(World); bare.SetPositionAndPrev(wx - 1.5, ty, wz - 3.5); bare.Yaw = bare.PrevYaw = 200; World.SpawnEntity(bare);
            World.DayTime = 6000;
            Player.SetPositionAndPrev(wx + .5, ty, wz - 10); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 8;
        });
        Step(40, () => Shot("me_11_obrazy_ramki_stojak"));
        Step(5, () => { Player.SetPositionAndPrev(tx + .5, ty + .6, tz + 16 - 6.5); Player.Pitch = Player.PrevPitch = -5; Player.Yaw = Player.PrevYaw = 15; });
        Step(20, () => Shot("me_11b_zblizenie"));
        Step(5, () =>
        {
            Player.Inventory.Armor[0] = new ItemStack(I.DiamondHelmet); Player.Inventory.Armor[1] = new ItemStack(I.IronChest);
            Player.Inventory.Armor[2] = new ItemStack(I.ChainLegs); Player.Inventory.Armor[3] = new ItemStack(I.GoldBoots);
            CameraMode = 2;
        });
        Step(20, () => Shot("me_12_gracz_w_zbroi"));
        Step(5, () => { CameraMode = 0; for (int i = 0; i < 4; i++) Player.Inventory.Armor[i] = null; HideGui = false; });
        // ME-h 39a: mapa w ręce i w ramce
        long mapUntil = 0; int mapId = -1;
        Step(5, () =>
        {
            Player.SetPositionAndPrev(tx + 6.5, ty, tz + 4.5); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 45;
            var m = MapStore.Of(World).Create(0, Player.X, Player.Z, 0); mapId = m.Id;
            Player.Inventory.Main[Player.Inventory.SelectedSlot] = new ItemStack(I.FilledMap, 1, m.Id);
            mapUntil = _tickCount + 80;
        });
        WaitUntil(() => _tickCount >= mapUntil);
        Step(10, () =>
        {
            var m = MapStore.Of(World).Get(mapId);
            int drawn = m?.Colors.Count(c => c != 0) ?? 0;
            MeCheck(drawn > 2000, "mapa w ręce rysuje teren", drawn.ToString());
            Shot("me_13_mapa_w_rece");
        });
        Step(5, () =>
        {
            var f = World.Entities.OfType<ItemFrameEntity>().FirstOrDefault(e => e.HangX == tx - 1 && e.Item == null);
            if (f != null) f.Item = new ItemStack(I.FilledMap, 1, mapId);
            MeCheck(f != null, "ramka na mapę");
            Player.Inventory.Main[Player.Inventory.SelectedSlot] = null;
            Player.SetPositionAndPrev(tx - .5, ty, tz + 16 - 3.2); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 0;
        });
        Step(25, () => Shot("me_14_mapa_w_ramce"));
        // ME-h 39b: fajerwerki nocą (5 kształtów)
        long fwUntil = 0;
        Step(5, () =>
        {
            World.DayTime = 18000; HideGui = true;
            Player.SetPositionAndPrev(tx + .5, ty, tz - 20); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = -35;
            int[][] cols = { new[] { 11743532 }, new[] { 2437522, 15790320 }, new[] { 14602026 }, new[] { 3887386 }, new[] { 14188952, 6719955 } };
            for (int t = 0; t < 5; t++)
            {
                var e = new Nbt { ["Type"] = t, ["Colors"] = cols[t], ["Trail"] = t == 1 };
                if (t == 4) e["FadeColors"] = new[] { 15790320 };
                var st = new ItemStack(I.FireworkRocket); st.GetOrCreateTag()["Fireworks"] = new Nbt { ["Flight"] = 1, ["Explosions"] = new List<object> { e } };
                var r = new FireworkRocketEntity(World, tx - 12 + t * 6 + .5, ty + 1, tz + 8, st) { Lifetime = 22 };
                World.SpawnEntity(r);
            }
            fwUntil = _tickCount + 27;
        });
        WaitUntil(() => _tickCount >= fwUntil);
        Step(1, () => { MeCheck(Renderer.FireworkSparkCount > 300, "iskry fajerwerków", Renderer.FireworkSparkCount.ToString()); Shot("me_15_fajerwerki"); });
        Step(5, () => { World.DayTime = 6000; HideGui = false; });
        Step(5, () => { Log.Info($"ME-TEST KONIEC: {(_meFails == 0 ? "OK" : _meFails + " FAIL")}"); Log.Info("AUTOTEST DONE"); Close(); });
    }
}
