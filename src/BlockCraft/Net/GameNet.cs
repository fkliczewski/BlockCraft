using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Net;
using BlockCraft.World;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft;

/// <summary>Gra sieciowa w oknie gry: host (Otwórz w sieci) i klient (dołączenie kodem).</summary>
public sealed partial class Game : INetClientHost
{
    World.World INetClientHost.World => World;
    Player INetClientHost.Player => Player;

    public NetServer Server;
    public NetClient Client;
    public LinkHostService HostLink;
    /// <summary>Stan uruchamiania hosta dla ekranu (null = nic się nie dzieje).</summary>
    public string HostStatus;
    public bool HostStarting;

    // ================= host =================

    /// <summary>Otwiera bieżący świat w sieci: serwer gry + Tailcat (kod zaproszenia).</summary>
    public async void OpenToNetwork()
    {
        if (World == null || Client != null || HostStarting) return;
        EnsureServer();
        if (HostLink != null) return;
        HostStarting = true;
        HostStatus = "Łączenie z przekaźnikiem derp.fimson.pl...";
        try
        {
            var link = await LinkHostService.StartAsync(LinkSetup.Options("host"));
            HostStatus = "Tworzenie kodu zaproszenia...";
            await link.InviteAsync();
            if (World == null || Server == null) { await link.DisposeAsync(); return; }
            HostLink = link;
            HostStatus = null;
            AddChat("§eŚwiat otwarty w sieci. Kod zaproszenia: §f" + link.Code);
            Log.Info("Sieć: host gotowy, kod " + link.Code);
        }
        catch (Exception ex)
        {
            Log.Error("Sieć: start hosta: " + ex);
            HostStatus = "Nie udało się otworzyć świata w sieci: " + NetErrors.Describe(ex);
        }
        finally { HostStarting = false; }
    }

    /// <summary>Serwer gry bez Tailcat (sam serwer: testy w pamięci i start przed łączem).</summary>
    public NetServer EnsureServer()
    {
        if (Server != null || World == null || Client != null) return Server;
        Server = new NetServer(Dimensions?.Get(DimensionId.Overworld) ?? World)
        {
            HostPlayer = () => Player,
            ViewDistance = Math.Max(4, Settings.RenderDistance),
            DefaultGameMode = _level?.GameMode ?? 0,
        };
        return Server;
    }

    public async void NewInviteCode()
    {
        if (HostLink == null) return;
        try { await HostLink.InviteAsync(); AddChat("§eNowy kod zaproszenia: §f" + HostLink.Code); }
        catch (Exception ex) { HostStatus = "Nie udało się utworzyć kodu: " + NetErrors.Describe(ex); }
    }

    public void StopNetwork(string reason = "Host zamknął świat")
    {
        Server?.Shutdown(reason);
        Server = null;
        if (HostLink != null) { var l = HostLink; HostLink = null; _ = Task.Run(async () => { await Task.Delay(500); await l.DisposeAsync(); }); }
        HostStatus = null;
    }

    public void SetClipboard(string s) { try { ClipboardString = s; } catch (Exception ex) { Log.Error("Schowek: " + ex.Message); } }

    // ================= klient =================

    /// <summary>Start sesji na świecie zdalnym po S2C.LoginOk.</summary>
    public void StartRemoteSession(NetClient c)
    {
        StopMenuWorld();
        var L = c.Login;
        var w = NetHooks.CreateRemoteWorld(L.Dim);
        w.Name = L.WorldName; w.TotalTime = L.TotalTime; w.DayTime = L.DayTime;
        w.SpawnX = L.SpawnX; w.SpawnY = L.SpawnY; w.SpawnZ = L.SpawnZ;
        var p = new Player(w) { Name = L.Name, Handler = this, IsLocal = true, Id = L.EntityId };
        p.SetGameMode(L.GameMode);
        p.SetPositionAndPrev(L.X, L.Y, L.Z);
        p.Yaw = p.PrevYaw = L.Yaw; p.Pitch = p.PrevPitch = L.Pitch;
        _storage = null;
        _level = new LevelData { Name = L.WorldName, Player = new PlayerData(), GameMode = L.GameMode };
        World = w; Player = p;
        Ctrl = new PlayerController(this);
        Cmds = new Commands(w);
        Renderer.SetWorld(w);
        Client = c;
        c.Attach(this);
        _chat.Clear();
        _loading = new LoadingScreen { Title = "Łączenie ze światem " + L.WorldName, Sub = "Pobieranie terenu..." };
        _loadTicks = 0;
        SetScreen(_loading);
        _sessionStarted = false;
        AddChat("§ePołączono z " + L.HostName + ". Grasz jako " + L.Name + ".");
    }

    /// <summary>Klient: zamiana świata zdalnego (wymiar).</summary>
    public void NetChangeWorld(World.World next)
    {
        var p = Player;
        if (World.EntityById.TryGetValue(p.Id, out var old) && old == p) World.RemoveEntity(p);
        next.Name = World.Name; next.TotalTime = World.TotalTime; next.DayTime = World.DayTime;
        p.World = next;
        p.OpenContainer = null;
        World = next;
        Ctrl = new PlayerController(this);
        Cmds = new Commands(next);
        Renderer.SetWorld(next);
        if (_sessionStarted) next.AddEntityNow(p);
        SetScreen(null);
    }

    /// <summary>Klient: host rozłączył albo łącze padło.</summary>
    public void NetDisconnected(string reason)
    {
        if (Client == null) return;
        Log.Info("Sieć: rozłączono: " + reason);
        var c = Client; Client = null;
        c.Disconnect(reason);
        Renderer.SetWorld(null);
        World = null; Player = null; Ctrl = null; _storage = null;
        StartMenuWorld();
        SetScreen(new DisconnectedScreen(reason));
    }

    private void NetShutdown()
    {
        if (Server != null || HostLink != null) StopNetwork();
        if (Client != null) { var c = Client; Client = null; c.Flush(); c.Disconnect("Gracz wyszedł"); }
    }

    // ================= haki w pętli gry =================

    /// <summary>Przed sterowaniem i World.Tick.</summary>
    private void NetBeforeTick()
    {
        if (HostLink != null && Server != null)
            while (HostLink.NewConnections.TryDequeue(out var conn)) Server.Accept(conn);
        Server?.Tick();
        if (Client != null)
        {
            Client.Tick();
            if (Client != null && Client.Conn.IsClosed) NetDisconnected(Client.Conn.CloseReason ?? "Utracono połączenie");
        }
    }

    /// <summary>Po World.Tick: wysyłka.</summary>
    private void NetAfterTick()
    {
        Server?.AfterTick();
        if (Client != null && Player != null && _sessionStarted)
        {
            Client.SendInput(BuildInput());
            Client.Flush();
        }
    }

    /// <summary>W trakcie wczytywania świata zdalnego.</summary>
    private void NetLoadingTick()
    {
        if (Client == null) return;
        Client.Tick();
        if (Client == null) return;
        if (Client.Conn.IsClosed) { NetDisconnected(Client.Conn.CloseReason ?? "Utracono połączenie"); return; }
        Client.Flush();
    }

    private bool _netLeftDown, _netLeftPressed, _netRightDown, _netRightPressed, _netMiddle;

    /// <summary>Zapamiętuje stan przycisków przed ControlTick (który zeruje "wciśnięte w tym ticku").</summary>
    private void NetCaptureButtons()
    {
        if (Client == null || Player == null) return;
        bool inGame = _screen == null && IsFocused || Scripted && _screen == null;
        _netLeftDown = inGame && _leftDown; _netLeftPressed = inGame && _leftPressed;
        _netRightDown = inGame && _rightDown; _netRightPressed = inGame && _rightPressed;
        _netMiddle = inGame && _middlePressed;
    }

    private NetInput BuildInput()
    {
        var p = Player;
        var i = new NetInput
        {
            X = p.X, Y = p.Y, Z = p.Z, Yaw = p.Yaw, Pitch = p.Pitch,
            OnGround = p.OnGround, Sneaking = p.Sneaking, Sprinting = p.Sprinting, Flying = p.Flying, Jumping = p.IsJumping,
            Forward = (sbyte)Math.Sign(p.MoveForward), Strafe = (sbyte)Math.Sign(p.MoveStrafe), Selected = (byte)p.Inventory.SelectedSlot,
            LeftDown = _netLeftDown, LeftPressed = _netLeftPressed, RightDown = _netRightDown, RightPressed = _netRightPressed, Middle = _netMiddle,
        };
        if (Ctrl.HitEntity != null) { i.HitKind = 2; i.HitEntity = Ctrl.HitEntity.Id; }
        else if (Ctrl.Hit.Hit)
        {
            var h = Ctrl.Hit;
            i.HitKind = 1; i.HitX = h.X; i.HitY = h.Y; i.HitZ = h.Z; i.HitFace = h.Face;
            i.HitFx = (float)(h.HitX - h.X); i.HitFy = (float)(h.HitY - h.Y); i.HitFz = (float)(h.HitZ - h.Z);
        }
        _netLeftPressed = _netRightPressed = _netMiddle = false;
        return i;
    }

    /// <summary>Środki ładowania chunków hosta: gracz hosta i wszyscy zdalni gracze w tym świecie.</summary>
    private IReadOnlyList<(int cx, int cz)> ChunkCenters()
    {
        var p = Player;
        var list = new List<(int, int)> { (MathUtil.FloorToInt(p.X) >> 4, MathUtil.FloorToInt(p.Z) >> 4) };
        if (Server != null)
            foreach (var c in Server.Clients)
                if (c.P != null && c.P.World == World) list.Add((MathUtil.FloorToInt(c.P.X) >> 4, MathUtil.FloorToInt(c.P.Z) >> 4));
        return list;
    }

    // ================= nicki nad głowami =================

    private Vector3d _netCam; private Matrix4 _netViewProj; private bool _netCamOk;

    private void NetCaptureCamera(Vector3d cam, Matrix4 view)
    {
        _netCam = cam; _netViewProj = view * _proj; _netCamOk = true;
    }

    private void NetDrawNameTags(float pt)
    {
        if (!_netCamOk || (Server == null && Client == null) || World == null) return;
        var sb = Sb;
        foreach (var other in World.Players)
        {
            if (other == Player || other.Removed || other.Dead) continue;
            double dx = other.IX(pt) - _netCam.X, dy = other.IY(pt) + other.Height + 0.45 - _netCam.Y, dz = other.IZ(pt) - _netCam.Z;
            double d2 = dx * dx + dy * dy + dz * dz;
            if (d2 > 64 * 64) continue;
            var clip = new Vector4((float)dx, (float)dy, (float)dz, 1f) * _netViewProj;
            if (clip.W <= 0.05f) continue;
            float nx = clip.X / clip.W, ny = clip.Y / clip.W;
            if (nx < -1.2f || nx > 1.2f || ny < -1.2f || ny > 1.2f) continue;
            float gx = (nx * 0.5f + 0.5f) * GuiW, gy = (1f - (ny * 0.5f + 0.5f)) * GuiH;
            string name = other.Name ?? "?";
            float wpx = sb.Measure(name);
            sb.Fill(gx - wpx / 2 - 2, gy - 9, wpx + 4, 10, 0x60000000);
            sb.TextCentered(name, gx, gy - 8, other.Sneaking ? 0x80FFFFFFu : 0xFFFFFFFFu);
        }
        _netCamOk = false;
    }
}

/// <summary>Opisy błędów sieci po polsku.</summary>
public static class NetErrors
{
    public static string Describe(Exception ex)
    {
        var e = ex is AggregateException ae && ae.InnerException != null ? ae.InnerException : ex;
        string m = e.Message ?? "";
        string t = e.GetType().Name;
        if (e is OperationCanceledException || e is TimeoutException || t.Contains("Timeout")) return "przekroczono czas oczekiwania (host wyłączony albo brak internetu)";
        if (t.Contains("Invitation") || m.Contains("invitation", StringComparison.OrdinalIgnoreCase) || m.Contains("code", StringComparison.OrdinalIgnoreCase))
            return "nieprawidłowy albo wygasły kod zaproszenia";
        if (m.Contains("refused", StringComparison.OrdinalIgnoreCase)) return "host odrzucił połączenie (kod wygasł albo brak miejsc)";
        if (e is System.Net.Sockets.SocketException || e is HttpRequestException || m.Contains("DNS", StringComparison.OrdinalIgnoreCase))
            return "brak połączenia z przekaźnikiem derp.fimson.pl (" + m + ")";
        if (m.Contains("No stored", StringComparison.OrdinalIgnoreCase) || m.Contains("not paired", StringComparison.OrdinalIgnoreCase) || m.Contains("pair", StringComparison.OrdinalIgnoreCase))
            return "brak zapamiętanego hosta - dołącz kodem";
        return m.Length > 0 ? m : t;
    }
}
