using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Dwa rzeczywiste okna gry przez gry.fimson.pl, z ograniczonym czasem i zapisem wyniku.</summary>
public sealed partial class Game
{
    private bool ArgsNetAutotest => _args.Contains("--autotest-host") || _args.Contains("--autotest-join");
    private string _netTestDir, _netTestRole;
    private DateTime _netTestDeadline;
    private bool _netTestFinished;
    private Settings _netTestOriginalSettings;

    private string NetTestFile(string name) => Path.Combine(_netTestDir, name);
    private void NetTestSignal(string name, string value = "OK") => File.WriteAllText(NetTestFile(name), value);

    private void NetTestFinish(bool ok, string message)
    {
        if (_netTestFinished) return;
        _netTestFinished = true;
        Log.Info($"NET AUTOTEST {_netTestRole}: {(ok ? "OK" : "FAIL")} {message}");
        NetTestSignal(_netTestRole + ".result", (ok ? "OK " : "FAIL ") + message);
        _steps.Clear();
        Close();
    }

    private void SetupNetAutotest()
    {
        _netTestRole = _args.Contains("--autotest-host") ? "host" : "join";
        int dir = Array.IndexOf(_args, "--net-test-dir");
        _netTestDir = dir >= 0 && dir + 1 < _args.Length ? Path.GetFullPath(_args[dir + 1]) : Path.Combine(AppContext.BaseDirectory, "autotest-net");
        Directory.CreateDirectory(_netTestDir);
        Log.FilePath = NetTestFile(_netTestRole + ".log");
        _shotDir = Path.Combine(_netTestDir, _netTestRole);
        Directory.CreateDirectory(_shotDir);
        _netTestDeadline = DateTime.UtcNow.AddMinutes(4);
        _netTestOriginalSettings = Settings;
        Settings = System.Text.Json.JsonSerializer.Deserialize<Settings>(System.Text.Json.JsonSerializer.Serialize(Settings));
        Settings.RenderDistance = 4;
        Settings.MaxFps = 60;
        Settings.VSync = false;
        ApplySettings();
        // Nick nie jest zapisywany w ustawieniach użytkownika.
        Step(30, () => Shot("01_title"));
        Step(1, () => SetScreen(new MultiplayerScreen(new TitleScreen())));
        Step(10, () => Shot("02_multiplayer"));
        if (_netTestRole == "host") SetupNetHostTest();
        else SetupNetJoinTest();
    }

    private void SetupNetHostTest()
    {
        Step(1, () => SetScreen(new DisconnectedScreen("Host zamknął świat (test ekranu)")));
        Step(10, () => Shot("03_disconnected"));
        Step(1, () => CreateWorld("NetTest " + DateTime.Now.ToString("yyyyMMdd-HHmmss"), 424242, 1, false, WorldType.Superflat, true));
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10);
        Step(1, () =>
        {
            Player.Name = "TestHost";
            Player.SetPositionAndPrev(8.5, 4, 12.5); Player.Yaw = 180; Player.Pitch = 5;
            World.SpawnX = 8; World.SpawnY = 4; World.SpawnZ = 8;
            World.DayTime = 6000; World.Rules.DoDaylightCycle = false;
            SetScreen(new PauseScreen());
        });
        Step(10, () => Shot("04_pause"));
        Step(1, () => SetScreen(new NetHostScreen(new PauseScreen())));
        Step(10, () => Shot("05_open_network"));
        Step(1, OpenToNetwork);
        WaitUntil(() =>
        {
            if (!HostStarting && HostLink == null && HostStatus != null) throw new InvalidOperationException(HostStatus);
            return HostLink?.Code != null;
        });
        Step(15, () => { Shot("06_invite"); NetTestSignal("invite.txt", HostLink.Code); });
        WaitUntil(() => Server.Clients.Any(c => c.P != null) && File.Exists(NetTestFile("joined")));
        Step(20, () =>
        {
            Shot("07_players");
            var remote = Server.Clients.Single(c => c.P != null);
            Server.SetOp(remote.Conn.PeerKey, true);
            World.SetBlock(10, 4, 10, B.GoldBlock);
            remote.P.Inventory.Add(new ItemStack(I.Diamond, 3));
            SendChat("Witaj przez gry.fimson.pl!");
            SetScreen(null);
        });
        WaitUntil(() => File.Exists(NetTestFile("verified")) && _chat.Any(c => c.text.Contains("NET-CLIENT-ACK")));
        Step(20, () => Shot("08_remote_player"));
        SetupNetWeatherHostTest();
        SetupNetEntityHostTest();
        Step(1, () => Server.Kick(Server.Clients.Single(c => c.P != null), "Test wyrzucania przez hosta"));
        WaitUntil(() => File.Exists(NetTestFile("disconnected")));
        WaitUntil(() => File.Exists(NetTestFile("rejoined")) && Server.Clients.Any(c => c.P != null));
        Step(20, () => { Shot("09_rejoined"); StopNetwork("Test zamknięcia świata przez hosta"); });
        WaitUntil(() => File.Exists(NetTestFile("closed")));
        Step(1, () => NetTestFinish(true, "kod, logowanie, teren, gracz, blok, ekwipunek, czat, operator, wyrzucenie, powrót bez kodu, zamknięcie świata"));
    }

    private void SetupNetJoinTest()
    {
        WaitUntil(() => File.Exists(NetTestFile("invite.txt")));
        Step(1, () => SetScreen(new ConnectingScreen(new MultiplayerScreen(new TitleScreen()), File.ReadAllText(NetTestFile("invite.txt")).Trim(), "TestGuest")));
        WaitUntil(() => Client != null && _sessionStarted && Client.ChunksReceived >= 25);
        Step(30, () =>
        {
            Shot("03_connected");
            if (!World.Players.Any(p => p != Player && p.Name == "TestHost")) throw new InvalidOperationException("Brak modelu hosta");
            NetTestSignal("joined");
        });
        WaitUntil(() => World.GetBlock(10, 4, 10) == B.GoldBlock && Player.Inventory.CountOf(I.Diamond) == 3
            && _chat.Any(c => c.text.Contains("Witaj przez gry.fimson.pl!"))
            && _chat.Any(c => c.text.Contains("uprawnienia operatora")));
        Step(20, () => { Shot("04_synced"); SendChat("NET-CLIENT-ACK"); NetTestSignal("verified"); });
        SetupNetWeatherJoinTest();
        SetupNetEntityJoinTest();
        WaitUntil(() => Client == null && _screen is DisconnectedScreen);
        Step(15, () => { Shot("05_kicked"); NetTestSignal("disconnected"); });
        Step(10, () => SetScreen(new ConnectingScreen(new MultiplayerScreen(new TitleScreen()), null, "TestGuest")));
        WaitUntil(() => Client != null && _sessionStarted && Player.Inventory.CountOf(I.Diamond) == 3);
        Step(20, () => { Shot("06_rejoined"); NetTestSignal("rejoined"); });
        WaitUntil(() => Client == null && _screen is DisconnectedScreen);
        Step(15, () =>
        {
            Shot("07_host_closed"); NetTestSignal("closed");
            NetTestFinish(true, "teren, model hosta, zmiana bloku, 3 diamenty, czat, operator, wyrzucenie, powrót bez kodu i zapis ekwipunku");
        });
    }
    private void SetupNetEntityHostTest()
    {
        Horse horse = null; ChestMinecartEntity cart = null; Villager villager = null;
        Player Guest() => Server.Clients.Single(c => c.P != null).P;
        Step(1, () =>
        {
            var p = Guest(); p.Inventory.Main[8] = new ItemStack(I.Saddle);
            horse = new Horse(p.World) { Tamed = true }; horse.SetPositionAndPrev(p.X + 1, p.Y, p.Z);
            p.World.AddEntityNow(horse); p.OpenEntityContainer(new HorseContainer(p.Inventory, horse));
        });
        WaitUntil(() => File.Exists(NetTestFile("horse-window")) && horse.Inv.Get(0)?.Id == I.Saddle);
        Step(1, () =>
        {
            if (horse.Inv.Get(0)?.Id != I.Saddle) throw new InvalidOperationException("Brak siodła u konia hosta");
            var p = Guest(); p.CloseContainer(); cart = new ChestMinecartEntity(p.World);
            cart.SetPositionAndPrev(p.X + 1, p.Y, p.Z); p.World.AddEntityNow(cart);
            cart.Set(0, new ItemStack(I.Apple, 5)); cart.Interact(p);
        });
        // Plik gościa może wyprzedzić pakiet kliknięcia. Czekamy także na wynik u hosta.
        WaitUntil(() => File.Exists(NetTestFile("cart-window")) && cart.Get(0) == null && Guest().Inventory.CountOf(I.Apple) == 5);
        Step(1, () =>
        {
            if (cart.Get(0) != null || Guest().Inventory.CountOf(I.Apple) != 5) throw new InvalidOperationException("Niezgodny ładunek wagonika");
            var p = Guest(); p.CloseContainer(); villager = new Villager(p.World, 0) { Customer = p };
            villager.SetPositionAndPrev(p.X + 1, p.Y, p.Z); p.World.AddEntityNow(villager);
            villager.Recipes.Add(new(new ItemStack(I.Wheat, 4), null, new ItemStack(I.Emerald)));
            villager.Recipes.Add(new(new ItemStack(I.Wheat, 8), null, new ItemStack(I.Emerald, 2)) { MaxUses = 1 });
            p.Cursor = new ItemStack(I.Wheat, 8); p.OpenEntityContainer(new MerchantContainer(p.Inventory, villager));
        });
        WaitUntil(() => File.Exists(NetTestFile("merchant-window")) && villager.Recipes[1].Uses == 1
            && Guest().Cursor is { Id: I.Emerald, Count: 2 });
        Step(1, () =>
        {
            if (villager.Recipes[1].Uses != 1 || Guest().Cursor?.Id != I.Emerald || Guest().Cursor.Count != 2
                || Guest().Inventory.CountOf(I.Wheat) != 0)
                throw new InvalidOperationException("Handel nie został poprawnie rozliczony na hoście");
            Guest().CloseContainer();
        });
    }

    private void SetupNetEntityJoinTest()
    {
        WaitUntil(() => _screen is HorseScreen && Player.OpenContainer is HorseContainer && Player.Inventory.Main[8]?.Id == I.Saddle);
        Step(1, () => { var c = (HorseContainer)Player.OpenContainer; c.Click(c.FirstPlayerSlot + 35, 0, ClickType.QuickMove, Player); });
        WaitUntil(() => Player.OpenContainer is HorseContainer c && c.Horse.Inv.Get(0)?.Id == I.Saddle);
        Step(10, () => { Shot("08_horse_inventory"); NetTestSignal("horse-window"); });
        WaitUntil(() => _screen is RsContainerScreen && Player.OpenContainer is RsContainer);
        Step(1, () => Player.OpenContainer.Click(0, 0, ClickType.QuickMove, Player));
        WaitUntil(() => Player.Inventory.CountOf(I.Apple) == 5);
        Step(10, () => { Shot("09_cart_inventory"); NetTestSignal("cart-window"); });
        WaitUntil(() => _screen is MerchantScreen && Player.OpenContainer is MerchantContainer && Player.Cursor?.Id == I.Wheat);
        Step(1, () => Client.SelectRecipe(1));
        WaitUntil(() => Player.OpenContainer is MerchantContainer c && c.Index == 1);
        Step(1, () => Player.OpenContainer.Click(0, 0, ClickType.Pickup, Player));
        WaitUntil(() => Player.OpenContainer.Slots[2].Stack?.Count == 2);
        Step(1, () => Player.OpenContainer.Click(2, 0, ClickType.Pickup, Player));
        WaitUntil(() => Player.Cursor?.Id == I.Emerald && Player.Cursor.Count == 2 && Player.OpenContainer is MerchantContainer c && c.Current.Disabled);
        Step(10, () =>
        {
            if (Player.Inventory.CountOf(I.Wheat) != 0) throw new InvalidOperationException("Powielona zapłata po zmianie okna");
            Shot("10_merchant_trade"); NetTestSignal("merchant-window");
        });
    }
}
