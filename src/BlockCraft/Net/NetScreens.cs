using BlockCraft.Core;
using BlockCraft.Gui;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Net;

/// <summary>Menu pauzy -> "Otwórz w sieci": kod zaproszenia, lista graczy, wyrzucanie i operatorzy.</summary>
public sealed class NetHostScreen : Screen
{
    private readonly Screen _parent;
    private int _copiedTicks;
    public NetHostScreen(Screen parent) { _parent = parent; }
    public override bool PausesGame => false;

    protected override void Init()
    {
        Rebuild();
    }

    private int _lastClients = -1; private bool _lastHosting, _lastStarting;
    private int CodeColumns => Math.Max(1, (W - 24) / 6);
    private int HostButtonsY => Math.Max(74, 54 + ((G.HostLink?.Code?.Length ?? 0) + CodeColumns - 1) / CodeColumns * 10);

    private void Rebuild()
    {
        Widgets.Clear();
        bool hosting = G.HostLink != null;
        _lastHosting = hosting;
        _lastStarting = G.HostStarting;
        _lastClients = G.Server?.Clients.Count ?? 0;
        if (!hosting)
        {
            _nick = Add(new TextField(W / 2 - 100, H / 4 + 98, 200, 20) { MaxLength = 16, Text = _nick?.Text ?? G.Player?.Name ?? G.Settings.PlayerName ?? "Steve" });
            _nick.OnEnter = Open;
            Add(new Button(W / 2 - 100, H / 4 + 60, 200, 20, () => G.HostStarting ? "Uruchamianie..." : "Otwórz świat w sieci", Open) { Enabled = !G.HostStarting });
        }
        else
        {
            Add(new Button(W / 2 - 154, HostButtonsY, 100, 20, () => _copiedTicks > 0 ? "Skopiowano!" : "Kopiuj kod", () => { G.SetClipboard(G.HostLink.Code); _copiedTicks = 40; }));
            Add(new Button(W / 2 - 50, HostButtonsY, 100, 20, "Nowy kod", () => G.NewInviteCode()));
            Add(new Button(W / 2 + 54, HostButtonsY, 100, 20, "Zamknij sieć", () => { G.StopNetwork(); Rebuild(); }));
            int y = HostButtonsY + 48;
            if (G.Server != null)
                foreach (var c in G.Server.Clients.ToList())
                {
                    var cc = c;
                    Add(new Button(W / 2 + 40, y - 4, 54, 16, () => G.Server != null && G.Server.Ops.Contains(cc.Conn.PeerKey) ? "Bez op" : "Op", () => G.Server?.SetOp(cc.Conn.PeerKey, !G.Server.Ops.Contains(cc.Conn.PeerKey))));
                    Add(new Button(W / 2 + 98, y - 4, 56, 16, "Wyrzuć", () => { G.Server?.Kick(cc, "Wyrzucono przez hosta"); }));
                    y += 20;
                }
        }
        if (hosting) Add(new Button(W / 2 - 100, H - 30, 200, 20, "Gotowe", () => G.SetScreen(_parent)));
        else
        {
            // przekaźnik zmienia się tylko przed otwarciem sieci (otwarty host zostaje na swoim)
            Add(new Button(W / 2 - 100, H - 30, 98, 20, "Przekaźnik...", () => G.SetScreen(new RelayScreen(this))) { Enabled = !G.HostStarting });
            Add(new Button(W / 2 + 2, H - 30, 98, 20, "Gotowe", () => G.SetScreen(_parent)));
        }
    }

    private TextField _nick;

    /// <summary>Nick hosta ustawiany przed otwarciem świata (goście widzą go nad głową i w wiadomościach).</summary>
    private void Open()
    {
        if (G.HostStarting) return;
        string nick = NetServer.SanitizeName(_nick?.Text);
        G.Settings.PlayerName = nick; G.Settings.Save();
        if (G.Player != null) G.Player.Name = nick;
        G.OpenToNetwork();
    }

    protected override void Escape() => G.SetScreen(_parent);

    public override void Tick()
    {
        if (_copiedTicks > 0) _copiedTicks--;
        bool hosting = G.HostLink != null;
        if (hosting != _lastHosting || G.HostStarting != _lastStarting || (G.Server?.Clients.Count ?? 0) != _lastClients) Rebuild();
    }

    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Gra w sieci", W / 2f, 16, 0xFFFFFFFF);
        if (G.HostLink == null)
        {
            sb.TextCentered("Inni gracze dołączą do tego świata kodem zaproszenia,", W / 2f, H / 4f, 0xFFE0E0E0);
            sb.TextCentered("bez serwera i bez przekierowania portów (przekaźnik: " + G.Relay.Current.Describe() + ").", W / 2f, H / 4f + 12, 0xFFA0A0A0);
            if (G.HostStatus != null) sb.TextCentered(G.HostStatus, W / 2f, H / 4f + 30, G.HostStatus.StartsWith("Nie") ? 0xFFFF6060 : 0xFFFFFF80);
            sb.Text("Twój nick", W / 2 - 100, H / 4 + 88, 0xFFA0A0A0);
        }
        else
        {
            sb.TextCentered("Kod zaproszenia (ważny godzinę, wielokrotny):", W / 2f, 32, 0xFFA0A0A0);
            string code = G.HostLink.Code ?? "...";
            for (int i = 0; i < code.Length; i += CodeColumns)
                sb.TextCentered(code.Substring(i, Math.Min(CodeColumns, code.Length - i)), W / 2f, 46 + i / CodeColumns * 10, 0xFF80FF80);
            if (G.HostStatus != null) sb.TextCentered(G.HostStatus, W / 2f, HostButtonsY + 24, 0xFFFF6060);
            sb.Text("Gracze:", W / 2 - 154, HostButtonsY + 32, 0xFFFFFFFF);
            int y = HostButtonsY + 48;
            var clients = G.Server?.Clients ?? new List<RemoteClient>();
            if (clients.Count == 0) sb.Text("§7(nikt jeszcze nie dołączył)", W / 2 - 150, y, 0xFFA0A0A0);
            foreach (var c in clients)
            {
                sb.Text($"{c.Name}  §7{c.P?.Ping ?? 0} ms", W / 2 - 150, y, 0xFFFFFFFF);
                y += 20;
            }
        }
        base.Render(mx, my, pt);
    }
}

/// <summary>Menu główne -> "Tryb wieloosobowy": dołączenie kodem albo do ostatniego hosta.</summary>
public sealed class MultiplayerScreen : Screen
{
    private readonly Screen _parent;
    private TextField _code, _nick;
    public MultiplayerScreen(Screen parent) { _parent = parent; }
    public override bool PausesGame => false;

    protected override void Init()
    {
        _code = Add(new TextField(W / 2 - 100, 70, 200, 20) { MaxLength = 200, Placeholder = "np. kod od hosta", Text = _code?.Text ?? "", Focused = true });
        _nick = Add(new TextField(W / 2 - 100, 112, 200, 20) { MaxLength = 16, Text = _nick?.Text ?? G.Settings.PlayerName ?? "Steve" });
        _code.OnEnter = Join;
        Add(new Button(W / 2 - 100, 142, 200, 20, "Dołącz", Join));
        Add(new Button(W / 2 - 100, 166, 200, 20, "Wklej kod ze schowka", () => { var s = G.GetClipboard(); if (!string.IsNullOrWhiteSpace(s)) _code.Text = s.Trim(); }));
        Add(new Button(W / 2 - 100, 190, 200, 20, "Dołącz ponownie (ostatni host)", () => Start(null)) { Enabled = LinkSetup.HasClientPairing() });
        Add(new Button(W / 2 - 100, H - 30, 98, 20, "Przekaźnik...", () => G.SetScreen(new RelayScreen(this))));
        Add(new Button(W / 2 + 2, H - 30, 98, 20, "Anuluj", () => G.SetScreen(_parent)));
    }

    private void Join()
    {
        if (string.IsNullOrWhiteSpace(_code.Text)) return;
        Start(_code.Text.Trim());
    }

    private void Start(string code)
    {
        string nick = NetServer.SanitizeName(_nick.Text);
        G.Settings.PlayerName = nick; G.Settings.Save();
        G.SetScreen(new ConnectingScreen(this, code, nick));
    }

    protected override void Escape() => G.SetScreen(_parent);

    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        sb.TextCentered("Tryb wieloosobowy", W / 2f, 20, 0xFFFFFFFF);
        sb.TextCentered("Host otwiera świat: Esc -> Otwórz w sieci i podaje Ci kod.", W / 2f, 36, 0xFFA0A0A0);
        sb.Text("Kod zaproszenia", W / 2 - 100, 60, 0xFFA0A0A0);
        sb.Text("Twój nick", W / 2 - 100, 102, 0xFFA0A0A0);
        sb.TextCentered("Przekaźnik: " + G.Relay.Current.Describe() + " (taki sam jak u hosta)", W / 2f, H - 44, 0xFFA0A0A0);
        base.Render(mx, my, pt);
    }
}

/// <summary>Wybór przekaźnika: publiczne przekaźniki Tailscale albo własny (np. gry.fimson.pl). Host i gracze muszą mieć ten sam.</summary>
public sealed class RelayScreen : Screen
{
    private readonly Screen _parent;
    private TextField _host;
    private string _error;
    public RelayScreen(Screen parent) { _parent = parent; }
    public override bool PausesGame => false;

    protected override void Init()
    {
        _host = Add(new TextField(W / 2 - 100, H / 2 - 10, 200, 20) { MaxLength = 120, Placeholder = "puste = publiczne Tailscale", Text = G.Relay.Text, Focused = true });
        _host.OnEnter = Save;
        Add(new Button(W / 2 - 100, H / 2 + 20, 200, 20, "Publiczne przekaźniki Tailscale", () => { _host.Text = ""; Save(); }));
        Add(new Button(W / 2 - 100, H - 30, 98, 20, "Zapisz", Save));
        Add(new Button(W / 2 + 2, H - 30, 98, 20, "Anuluj", () => G.SetScreen(_parent)));
    }

    private void Save()
    {
        string text = (_host.Text ?? "").Trim();
        if (!RelayOptions.IsValid(text))
        {
            _error = "Podaj nazwę hosta, np. gry.fimson.pl albo gry.fimson.pl:443";
            return;
        }
        G.Relay.Save(text);
        G.SetScreen(_parent);
    }

    protected override void Escape() => G.SetScreen(_parent);

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Przekaźnik sieci", W / 2f, 20, 0xFFFFFFFF);
        sb.TextCentered("Host i wszyscy gracze muszą używać tego samego przekaźnika.", W / 2f, H / 2f - 52, 0xFFE0E0E0);
        sb.TextCentered("Puste pole: publiczne przekaźniki Tailscale. Własny: np. gry.fimson.pl", W / 2f, H / 2f - 40, 0xFFA0A0A0);
        sb.Text("Własny przekaźnik (host[:port])", W / 2 - 100, H / 2 - 20, 0xFFA0A0A0);
        if (_error != null) sb.TextCentered(_error, W / 2f, H / 2f + 48, 0xFFFF6060);
        base.Render(mx, my, pt);
    }
}

/// <summary>Łączenie z hostem (Tailcat w tle, potem logowanie do gry).</summary>
public sealed class ConnectingScreen : Screen
{
    private readonly Screen _parent;
    private readonly string _code, _nick;
    private readonly CancellationTokenSource _cts = new();
    private Task<NetClient> _task;
    private NetClient _client;
    private string _status = "Łączenie z przekaźnikiem...", _error;
    private int _ticks;
    public ConnectingScreen(Screen parent, string code, string nick) { _parent = parent; _code = code; _nick = nick; }
    public override bool PausesGame => false;

    protected override void Init()
    {
        Add(new Button(W / 2 - 100, H / 2 + 40, 200, 20, () => _error != null ? "Wróć" : "Anuluj", Cancel));
        if (_task == null)
        {
            _cts.CancelAfter(TimeSpan.FromSeconds(45));
            _task = NetClient.JoinAsync(_code, _nick, G.Settings.RenderDistance, LinkSetup.Options("client", RelayGateway.For(G.Relay.Current)), _cts.Token);
        }
    }

    private void Cancel()
    {
        _cts.Cancel();
        if (_client != null) _client.Disconnect("Anulowano");
        // łączenie jeszcze trwa: klient, który mimo anulowania powstanie, trzeba zamknąć
        else _task?.ContinueWith(t => { if (t.Status == TaskStatus.RanToCompletion) t.Result.Disconnect("Anulowano"); }, TaskScheduler.Default);
        G.SetScreen(_parent);
    }

    protected override void Escape() => Cancel();

    public override void Tick()
    {
        _ticks++;
        if (_error != null) return;
        if (_client == null)
        {
            if (!_task.IsCompleted) return;
            if (_task.IsFaulted || _task.IsCanceled)
            {
                _error = _task.IsCanceled ? "Przekroczono czas łączenia (" + NetErrors.TimeoutHint + ")" : NetErrors.Describe(_task.Exception, G.Relay.Current);
                Log.Error("Sieć: dołączanie: " + _task.Exception);
                return;
            }
            _client = _task.Result;
            _status = "Logowanie do gry...";
        }
        if (_client.PollLogin()) { G.StartRemoteSession(_client); return; }
        if (_client.Error != null) { _error = _client.Error; _client.Disconnect(_error); }
        else if (_ticks > 20 * 60) { _error = "Host nie odpowiada"; _client.Disconnect(_error); }
    }

    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        if (_error == null)
        {
            sb.TextCentered(_code == null ? "Łączenie z ostatnim hostem" : "Dołączanie do gry", W / 2f, H / 2f - 30, 0xFFFFFFFF);
            sb.TextCentered(_status + new string('.', _ticks / 10 % 4), W / 2f, H / 2f - 10, 0xFFA0A0A0);
        }
        else
        {
            sb.TextCentered("Nie udało się połączyć", W / 2f, H / 2f - 30, 0xFFFF6060);
            sb.TextCentered(_error, W / 2f, H / 2f - 10, 0xFFE0E0E0);
        }
        base.Render(mx, my, pt);
    }
}

/// <summary>Po rozłączeniu przez hosta albo utracie łącza.</summary>
public sealed class DisconnectedScreen : Screen
{
    private readonly string _reason;
    public DisconnectedScreen(string reason) { _reason = reason; }
    public override bool PausesGame => false;
    protected override void Init() { Add(new Button(W / 2 - 100, H / 2 + 30, 200, 20, "Menu główne", () => G.SetScreen(new TitleScreen()))); }
    protected override void Escape() => G.SetScreen(new TitleScreen());
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        G.Sb.TextCentered("Rozłączono z grą", W / 2f, H / 2f - 30, 0xFFFFFFFF);
        G.Sb.TextCentered(_reason ?? "", W / 2f, H / 2f - 10, 0xFFA0A0A0);
        base.Render(mx, my, pt);
    }
}
