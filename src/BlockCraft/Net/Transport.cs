using System.Collections.Concurrent;
using System.Threading.Channels;
using BlockCraft.Core;
using Microsoft.Extensions.Logging;
using Tailcat.Link;
using Tailcat.Link.Storage;
using Tailcat.Link.Transport;

namespace BlockCraft.Net;

/// <summary>Połączenie z drugą stroną: ramki wysyłane w kolejności, odbierane do kolejki czytanej w wątku gry.</summary>
public interface INetConnection
{
    /// <summary>Nazwa podana przez drugą stronę (niezweryfikowana).</summary>
    string PeerName { get; }
    /// <summary>Stały identyfikator drugiej strony (klucz publiczny Tailcat albo nazwa w testach).</summary>
    string PeerKey { get; }
    void Send(byte[] frame);
    bool TryReceive(out byte[] frame);
    bool IsClosed { get; }
    string CloseReason { get; }
    /// <summary>Bajty czekające na wysłanie (do dławienia wysyłki chunków).</summary>
    long PendingBytes { get; }
    void Close(string reason);
}

/// <summary>Wspólna część: kolejka wysyłki obsługiwana przez jedno zadanie w tle i kolejka odbioru.</summary>
public abstract class QueuedConnection : INetConnection
{
    private readonly Channel<byte[]> _out = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentQueue<byte[]> _in = new();
    private long _pending;
    private volatile string _closeReason;
    public string PeerName { get; protected set; } = "?";
    public string PeerKey { get; protected set; } = "?";
    public bool IsClosed => _closeReason != null;
    public string CloseReason => _closeReason;
    public long PendingBytes => Interlocked.Read(ref _pending);
    public long BytesSent, BytesReceived;

    public void Send(byte[] frame)
    {
        if (IsClosed || frame == null || frame.Length == 0) return;
        Interlocked.Add(ref _pending, frame.Length);
        _out.Writer.TryWrite(frame);
    }

    public bool TryReceive(out byte[] frame) => _in.TryDequeue(out frame);

    protected void Received(byte[] frame) { BytesReceived += frame.Length; _in.Enqueue(frame); }

    /// <summary>Pętla wysyłki: czeka na otwarcie kanału i przepycha ramki w kolejności.</summary>
    protected async Task SendLoop(Func<CancellationToken, Task<Func<byte[], CancellationToken, ValueTask>>> open, CancellationToken ct)
    {
        try
        {
            var send = await open(ct);
            await foreach (var f in _out.Reader.ReadAllAsync(ct))
            {
                await send(f, ct);
                BytesSent += f.Length;
                Interlocked.Add(ref _pending, -f.Length);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { MarkClosed("Błąd wysyłania: " + ex.Message); }
    }

    public void MarkClosed(string reason)
    {
        if (_closeReason != null) return;
        _closeReason = reason ?? "Rozłączono";
        _out.Writer.TryComplete();
    }

    public virtual void Close(string reason) => MarkClosed(reason);
}

/// <summary>Para połączeń w pamięci (testy i serwer bez sieci).</summary>
public sealed class LoopbackConnection : QueuedConnection
{
    private LoopbackConnection _other;
    private readonly CancellationTokenSource _cts = new();

    public static (LoopbackConnection a, LoopbackConnection b) Pair(string nameA, string nameB)
    {
        var a = new LoopbackConnection { PeerName = nameB, PeerKey = "loop:" + nameB };
        var b = new LoopbackConnection { PeerName = nameA, PeerKey = "loop:" + nameA };
        a._other = b; b._other = a;
        a.Start(); b.Start();
        return (a, b);
    }

    private void Start()
    {
        _ = Task.Run(() => SendLoop(_ => Task.FromResult<Func<byte[], CancellationToken, ValueTask>>((f, c) =>
        {
            if (!_other.IsClosed) _other.Received(f);
            return ValueTask.CompletedTask;
        }), _cts.Token));
    }

    public override void Close(string reason)
    {
        MarkClosed(reason);
        _other.MarkClosed("Druga strona zamknęła połączenie");
        _cts.CancelAfter(200);
    }
}

/// <summary>Połączenie przez kanały Tailcat.Link: jeden kanał w każdą stronę (ramki uporządkowane, nietrwałe).</summary>
public sealed class LinkConnection : QueuedConnection
{
    public const string ToHost = "blockcraft-c2s", ToClient = "blockcraft-s2c";
    private readonly CancellationTokenSource _cts = new();
    private ILinkChannelWriter _writer;
    private LinkConnection(string name, string key) { PeerName = name; PeerKey = key; }

    /// <summary>Strona hosta: kanał od klienta już przyszedł, otwieramy kanał zwrotny do niego.</summary>
    public static LinkConnection ForPeer(ILinkPeer peer)
    {
        var c = new LinkConnection(string.IsNullOrWhiteSpace(peer.Name) ? "Gracz" : peer.Name, peer.Key.ToString());
        _ = Task.Run(() => c.SendLoop(async ct => { c._writer = await peer.OpenChannelAsync(ToClient, ct); return c.Write; }, c._cts.Token));
        return c;
    }

    /// <summary>Strona klienta: otwieramy kanał do hosta.</summary>
    public static LinkConnection ForHost(ILink link, string hostName)
    {
        var c = new LinkConnection(hostName, link.Peer.ToString());
        _ = Task.Run(() => c.SendLoop(async ct => { c._writer = await link.OpenChannelAsync(ToHost, ct); return c.Write; }, c._cts.Token));
        return c;
    }

    private ValueTask Write(byte[] f, CancellationToken ct) => new(_writer.SendAsync(f, ct));

    /// <summary>Czyta ramki kanału aż do jego końca.</summary>
    public async Task ReadFrom(ILinkChannelReader reader, CancellationToken ct)
    {
        try
        {
            await foreach (var frame in reader.ReadAllAsync(ct)) Received(frame.ToArray());
            MarkClosed("Druga strona zamknęła połączenie");
        }
        catch (OperationCanceledException) { MarkClosed("Połączenie zamknięte"); }
        catch (Exception ex) { MarkClosed("Połączenie przerwane: " + ex.Message); }
    }

    public override void Close(string reason)
    {
        MarkClosed(reason);
        _ = Task.Run(async () =>
        {
            await Task.Delay(300); // daj szansę wyjść ostatnim ramkom (np. powód rozłączenia)
            try { if (_writer != null) await _writer.DisposeAsync(); } catch { }
            _cts.Cancel();
        });
    }
}

/// <summary>Strona hosta w Tailcat.Link: przyjmuje graczy (HostManyAsync), wydaje kody zaproszeń.</summary>
public sealed class LinkHostService : IAsyncDisposable
{
    public const string App = "blockcraft";
    public const int MaxPeers = 8;
    private ILinkHost _host;
    private readonly CancellationTokenSource _cts = new();
    public readonly ConcurrentQueue<LinkConnection> NewConnections = new();
    private readonly ConcurrentDictionary<string, LinkConnection> _byKey = new();
    public string Code { get; private set; }
    public DateTimeOffset? CodeExpires { get; private set; }
    public IReadOnlyList<ILinkPeer> Peers => _host?.Peers ?? (IReadOnlyList<ILinkPeer>)Array.Empty<ILinkPeer>();

    public static async Task<LinkHostService> StartAsync(LinkOptions options, CancellationToken ct = default)
    {
        var s = new LinkHostService();
        s._host = await TailcatLink.HostManyAsync(App, options, ct);
        s._host.OnChannel(LinkConnection.ToHost, s.OnChannel);
        s._host.PeerLeft += (_, e) => { if (s._byKey.TryRemove(e.Peer.Key.ToString(), out var c)) c.MarkClosed("Gracz rozłączył się"); };
        return s;
    }

    private async Task OnChannel(ILinkPeer peer, ILinkChannelReader channel, CancellationToken ct)
    {
        var conn = LinkConnection.ForPeer(peer);
        var key = peer.Key.ToString();
        if (_byKey.TryGetValue(key, out var old)) old.Close("Połączono ponownie");
        _byKey[key] = conn;
        NewConnections.Enqueue(conn);
        await conn.ReadFrom(channel, ct);
        _byKey.TryRemove(new KeyValuePair<string, LinkConnection>(key, conn));
    }

    /// <summary>Nowy kod zaproszenia (wielokrotny, ważny godzinę). Gdy brak miejsca, zapomina najdawniej widzianego nieaktywnego gracza.</summary>
    public async Task<string> InviteAsync(CancellationToken ct = default)
    {
        if (_host.Peers.Count >= MaxPeers)
        {
            var victim = _host.Peers.Where(p => !p.IsConnected).OrderBy(p => p.LastSeen).FirstOrDefault();
            if (victim != null) await _host.ForgetPeerAsync(victim, ct);
        }
        var inv = await _host.InviteAsync(new InvitationRequest { Label = "BlockCraft", Lifetime = TimeSpan.FromHours(1), SingleUse = false }, ct);
        Code = inv.Code.ToString();
        CodeExpires = inv.ExpiresAt;
        return Code;
    }

    public async Task ForgetAsync(string key)
    {
        var p = _host.Peers.FirstOrDefault(x => x.Key.ToString() == key);
        if (p != null) await _host.ForgetPeerAsync(p);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        foreach (var c in _byKey.Values) c.MarkClosed("Host zamknął grę");
        if (_host != null) { try { await _host.DisposeAsync(); } catch (Exception ex) { Log.Error("Zamykanie hosta: " + ex.Message); } }
    }
}

/// <summary>Strona klienta w Tailcat.Link: dołączenie kodem albo ponowne połączenie z ostatnim hostem.</summary>
public sealed class LinkClientService : IAsyncDisposable
{
    private ILink _link;
    public LinkConnection Connection { get; private set; }

    /// <param name="code">Kod zaproszenia albo null (ponowne połączenie z zapamiętanym hostem).</param>
    public static async Task<LinkClientService> JoinAsync(string code, string nick, LinkOptions options, CancellationToken ct = default)
    {
        var s = new LinkClientService();
        s._link = string.IsNullOrWhiteSpace(code)
            ? await TailcatLink.JoinAsync(LinkHostService.App, null, new JoinRequest { DisplayName = nick }, options, ct)
            : await TailcatLink.JoinAsync(LinkHostService.App, code.Trim(), new JoinRequest { DisplayName = nick }, options, ct);
        await s._link.WaitUntilConnectedAsync(ct);
        LinkConnection conn = null;
        var ready = new TaskCompletionSource();
        s._link.OnChannel(LinkConnection.ToClient, async (reader, c) =>
        {
            while (conn == null) await Task.Delay(5, c);
            ready.TrySetResult();
            await conn.ReadFrom(reader, c);
        });
        conn = LinkConnection.ForHost(s._link, "Host");
        s.Connection = conn;
        s._link.Disconnected += reason => conn.MarkClosed("Utracono połączenie z hostem (" + reason + ")");
        return s;
    }

    public async ValueTask DisposeAsync()
    {
        Connection?.MarkClosed("Rozłączono");
        if (_link != null) { try { await _link.DisposeAsync(); } catch (Exception ex) { Log.Error("Zamykanie łącza: " + ex.Message); } }
    }
}

/// <summary>Ustawienia Tailcat dla gry: przekaźnik, magazyn parowania w %LOCALAPPDATA%\BlockCraft\net, logi do blockcraft.log.</summary>
public static class LinkSetup
{
    /// <summary>Podfolder profilu (dwie instancje na jednym komputerze: --net-profile nazwa).</summary>
    public static string Profile = "";

    public static string StoreDir(string role)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlockCraft", "net",
            string.IsNullOrEmpty(Profile) ? role : Profile + "-" + role);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static LinkOptions Options(string role, INodeGatewayFactory gateway = null, ILinkStore store = null) => new()
    {
        MaxPeers = role == "host" ? LinkHostService.MaxPeers : 1,
        Gateway = gateway ?? RelayGateway.For(RelayOptions.Default),
        Store = store ?? new FileLinkStore(StoreDir(role)),
        LoggerFactory = new LogBridge(),
    };

    /// <summary>Czy klient ma zapamiętane parowanie z hostem (można dołączyć bez kodu).</summary>
    public static bool HasClientPairing() => Directory.Exists(StoreDir("client")) && Directory.EnumerateFiles(StoreDir("client")).Any();
}

/// <summary>Przekazuje ostrzeżenia i błędy Tailcat do logu gry.</summary>
public sealed class LogBridge : ILoggerFactory, ILogger
{
    public ILogger CreateLogger(string categoryName) => this;
    public void AddProvider(ILoggerProvider provider) { }
    public void Dispose() { }
    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        BlockCraft.Core.Log.Info("[Tailcat " + logLevel + "] " + formatter(state, exception) + (exception != null ? " " + exception.Message : ""));
    }
}
