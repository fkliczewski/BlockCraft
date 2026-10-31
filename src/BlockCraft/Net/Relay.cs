using Tailcat;
using Tailcat.Keys;
using Tailcat.Link.Transport;
using Tailcat.Net;
using Tailcat.Tailcfg;

namespace BlockCraft.Net;

/// <summary>Ustawienia przekaźnika DERP. Domyślnie przekaźnik gier Filipa (gry.fimson.pl), nie publiczne DERP Tailscale.</summary>
public sealed class RelayOptions
{
    public bool Own { get; set; } = true;
    public string Host { get; set; } = "gry.fimson.pl";
    public int Port { get; set; } = 443;
    public int Region { get; set; } = 304;
    public string RegionCode { get; set; } = "fimson-gry";

    public static RelayOptions Default => new();
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

    public static INodeGatewayFactory For(RelayOptions o) => o.Own ? new RelayGateway(o) : new TailcatNodeGatewayFactory();

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
            RegionID = o.Region, RegionCode = o.RegionCode, RegionName = "Fimson gry",
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
