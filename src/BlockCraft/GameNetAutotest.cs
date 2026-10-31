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
}
