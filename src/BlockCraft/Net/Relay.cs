using Tailcat;
using Tailcat.Keys;
using Tailcat.Link.Transport;
using Tailcat.Net;
using Tailcat.Tailcfg;

namespace BlockCraft.Net;

/// <summary>
/// Ustawienia przekaźnika DERP. Domyślnie publiczne przekaźniki Tailscale (mapa DERP pobierana przez Tailcat, jak w bibliotece);
/// własny przekaźnik (np. gry.fimson.pl) wpisuje się w grze. Host i gracze muszą używać tego samego przekaźnika.
/// </summary>
public sealed class RelayOptions
{
    public bool Own { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 443;
    /// <summary>Region własnego przekaźnika: 304 jak w starszych wersjach gry (gry.fimson.pl), więc ich kody nadal działają.</summary>
    public int Region { get; set; } = 304;
    public string RegionCode { get; set; } = "wlasny";

    /// <summary>"gry.fimson.pl", "gry.fimson.pl:443" albo "https://gry.fimson.pl/"; puste lub "tailscale" = publiczne przekaźniki Tailscale.</summary>
    public static bool IsPublic(string text)
    {
        var s = (text ?? "").Trim();
        return s.Length == 0 || s.Equals("tailscale", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Poprawny wpis: publiczne przekaźniki albo nazwa hosta z liter, cyfr, kropek i myślników.</summary>
    public static bool IsValid(string text)
    {
        if (IsPublic(text)) return true;
        var o = Parse(text);
        return o.Own && o.Host.All(c => char.IsAsciiLetterOrDigit(c) || c == '.' || c == '-');
    }

    public static RelayOptions Parse(string text)
    {
        if (IsPublic(text)) return new RelayOptions();
        var s = text.Trim();
        int scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) s = s[(scheme + 3)..];
        int slash = s.IndexOf('/');
        if (slash >= 0) s = s[..slash];
        int port = 443;
        int colon = s.LastIndexOf(':');
        if (colon > 0 && int.TryParse(s[(colon + 1)..], out int p) && p is > 0 and < 65536) { port = p; s = s[..colon]; }
        if (s.Length == 0) return new RelayOptions();
        return new RelayOptions { Own = true, Host = s.ToLowerInvariant(), Port = port };
    }

    /// <summary>Wpis do zapisu w ustawieniach (odczytywany przez Parse): host[:port] własnego przekaźnika albo "" dla publicznych.</summary>
    public string ToSetting() => !Own ? "" : Port == 443 ? Host : Host + ":" + Port;

    /// <summary>Opis dla gracza: nazwa hosta własnego przekaźnika albo publiczne Tailscale.</summary>
    public string Describe() => Own ? (Port == 443 ? Host : Host + ":" + Port) : "publiczne Tailscale";
}

/// <summary>Wybrany przekaźnik: zapisany w ustawieniach, na czas uruchomienia nadpisywany przez --relay.</summary>
public sealed class RelaySetting
{
    private readonly BlockCraft.Settings _settings;
    private string _sessionOverride;

    public RelaySetting(BlockCraft.Settings settings, string sessionOverride = null)
    {
        _settings = settings;
        _sessionOverride = sessionOverride;
    }

    /// <summary>Wpis przekaźnika ("" = publiczne przekaźniki Tailscale).</summary>
    public string Text => _sessionOverride ?? _settings.Relay ?? "";

    /// <summary>Przekaźnik używany przy następnym otwarciu sieci lub dołączaniu.</summary>
    public RelayOptions Current => RelayOptions.Parse(Text);

    /// <summary>Zapis wyboru gracza w options.json; zastępuje nadpisanie z --relay.</summary>
    public void Save(string text)
    {
        _sessionOverride = null;
        _settings.Relay = RelayOptions.Parse(text).ToSetting();
        _settings.Save();
    }
}

/// <summary>
/// Publiczne przekaźniki Tailscale (brama z biblioteki). Region spoza ich mapy (np. 304 z czasów własnego przekaźnika)
/// daje w Tailcat wyjątek: zapisany region domowy jest wtedy wybierany od nowa, a zapamiętany host - zgłaszany czytelnie.
/// </summary>
public sealed class PublicRelayGateway : INodeGatewayFactory
{
    private readonly TailcatNodeGatewayFactory _inner = new();

    public async Task<INodeGateway> CreateAsync(NodePrivate privateKey, int? homeRegionId, CancellationToken ct = default)
    {
        if (homeRegionId == null) return new PublicRelayNode(await _inner.CreateAsync(privateKey, null, ct));
        try { return new PublicRelayNode(await _inner.CreateAsync(privateKey, homeRegionId, ct)); }
        catch (Exception ex) when (IsUnknownRegion(ex))
        {
            BlockCraft.Core.Log.Info($"Sieć: zapisanego regionu {homeRegionId} nie ma wśród przekaźników Tailscale, wybór najbliższego");
            return new PublicRelayNode(await _inner.CreateAsync(privateKey, null, ct));
        }
    }

    /// <summary>Tailcat: "the DERP map has no region" / "... but no such region".</summary>
    public static bool IsUnknownRegion(Exception ex)
        => ex.Message.Contains("no region", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("no such region", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Węzeł publicznych przekaźników: zapamiętany host z regionu spoza mapy (inny przekaźnik) daje czytelny błąd.</summary>
public sealed class PublicRelayNode : INodeGateway
{
    private readonly INodeGateway _inner;
    public PublicRelayNode(INodeGateway inner) { _inner = inner; }
    public ConnBlob Address => _inner.Address;
    public int HomeRegionId => _inner.HomeRegionId;
    public async Task<ITailcatConnection> ConnectAsync(ConnBlob peer, CancellationToken ct = default)
    {
        try { return await _inner.ConnectAsync(peer, ct); }
        catch (Exception ex) when (PublicRelayGateway.IsUnknownRegion(ex))
        {
            BlockCraft.Core.Log.Info("Sieć: host używa innego przekaźnika niż publiczne Tailscale");
            throw new InvalidOperationException(NetErrors.OtherRelay, ex);
        }
    }
    public Task<ITailcatConnection> AcceptAsync(CancellationToken ct = default) => _inner.AcceptAsync(ct);
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

/// <summary>Brama węzłów Tailcat kierująca ruch przez własny przekaźnik (wzorzec z Fimson.Dom.Lacze).</summary>
/// <remarks>
/// Gra ma jeden przekaźnik, więc zapisany region (np. 303 z czasów derp.fimson.pl) jest ignorowany:
/// węzeł zawsze stoi w <see cref="RelayOptions.Region"/>, a adres peera (kod zaproszenia niesie tylko numer
/// regionu, bez nazwy hosta) jest kierowany do tego samego regionu.
/// </remarks>
public sealed class RelayGateway : INodeGatewayFactory
{
    private readonly RelayOptions _o;
    public RelayGateway(RelayOptions o) { _o = o; }

    public static INodeGatewayFactory For(RelayOptions o) => o.Own ? new RelayGateway(o) : new PublicRelayGateway();

    public async Task<INodeGateway> CreateAsync(NodePrivate privateKey, int? homeRegionId, CancellationToken ct = default)
    {
        if (homeRegionId is int old && old != _o.Region)
            BlockCraft.Core.Log.Info($"Sieć: zapisany region {old} zastąpiony regionem {_o.Region} ({_o.Host})");
        var node = await TailcatNode.CreateAsync(new TailcatNodeOptions
        {
            PrivateKey = privateKey,
            HomeRegionId = _o.Region,
            DerpMap = Map(_o)
        }, ct);
        return new RelayNode(node, _o.Region);
    }

    public static DerpMap Map(RelayOptions o)
    {
        var map = new DerpMap();
        map.Regions[o.Region] = new DerpRegion
        {
            RegionID = o.Region, RegionCode = o.RegionCode, RegionName = o.Host,
            Nodes = [new DerpNode { Name = $"{o.Region}{o.RegionCode}", RegionID = o.Region, HostName = o.Host, DERPPort = o.Port, STUNPort = -1 }]
        };
        return map;
    }
}

public sealed class RelayNode : INodeGateway
{
    private readonly TailcatNode _node;
    private readonly int _region;
    private bool _closed;
    public RelayNode(TailcatNode node, int region) { _node = node; _region = region; }
    public ConnBlob Address => _node.Address;
    public int HomeRegionId => _node.HomeRegionId;
    /// <summary>Łączy z peerem zawsze przez nasz region: stary zapisany kod z regionem 303 trafia na ten sam przekaźnik.</summary>
    public async Task<ITailcatConnection> ConnectAsync(ConnBlob peer, CancellationToken ct = default)
        => await _node.ConnectAsync(peer.Parse().ServerPublic, _region, ct);
    public async Task<ITailcatConnection> AcceptAsync(CancellationToken ct = default) => await _node.AcceptConnectionAsync(ct);
    public async ValueTask DisposeAsync()
    {
        if (_closed) return;
        _closed = true;
        await _node.DisposeAsync();
    }
}
