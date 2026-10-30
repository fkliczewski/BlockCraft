using Tailcat;
using Tailcat.Keys;
using Tailcat.Link.Transport;
using Tailcat.Net;
using Tailcat.Tailcfg;

namespace BlockCraft.Net;

/// <summary>Ustawienia przekaźnika DERP. Domyślnie własny przekaźnik Filipa (derp.fimson.pl), nie publiczne DERP Tailscale.</summary>
public sealed class RelayOptions
{
    public bool Own { get; set; } = true;
    public string Host { get; set; } = "derp.fimson.pl";
    public int Port { get; set; } = 443;
    public int Region { get; set; } = 303;

    public static RelayOptions Default => new();
}

/// <summary>Brama węzłów Tailcat kierująca ruch przez własny przekaźnik (wzorzec z Fimson.Dom.Lacze).</summary>
public sealed class RelayGateway : INodeGatewayFactory
{
    private readonly RelayOptions _o;
    public RelayGateway(RelayOptions o) { _o = o; }

    public static INodeGatewayFactory For(RelayOptions o) => o.Own ? new RelayGateway(o) : new TailcatNodeGatewayFactory();

    public async Task<INodeGateway> CreateAsync(NodePrivate privateKey, int? homeRegionId, CancellationToken ct = default)
    {
        int region = homeRegionId ?? _o.Region;
        var node = await TailcatNode.CreateAsync(new TailcatNodeOptions
        {
            PrivateKey = privateKey,
            HomeRegionId = region,
            DerpMap = Map(_o, region)
        }, ct);
        return new RelayNode(node);
    }

    public static DerpMap Map(RelayOptions o, int? nodeRegion)
    {
        var map = new DerpMap();
        foreach (var r in new[] { o.Region, nodeRegion ?? o.Region }.Distinct())
        {
            map.Regions[r] = new DerpRegion
            {
                RegionID = r, RegionCode = "fimson", RegionName = "Fimson",
                Nodes = [new DerpNode { Name = $"{r}fimson", RegionID = r, HostName = o.Host, DERPPort = o.Port, STUNPort = -1 }]
            };
        }
        return map;
    }
}

public sealed class RelayNode : INodeGateway
{
    private readonly TailcatNode _node;
    private bool _closed;
    public RelayNode(TailcatNode node) { _node = node; }
    public ConnBlob Address => _node.Address;
    public int HomeRegionId => _node.HomeRegionId;
    public async Task<ITailcatConnection> ConnectAsync(ConnBlob peer, CancellationToken ct = default) => await _node.ConnectAsync(peer, ct);
    public async Task<ITailcatConnection> AcceptAsync(CancellationToken ct = default) => await _node.AcceptConnectionAsync(ct);
    public async ValueTask DisposeAsync()
    {
        if (_closed) return;
        _closed = true;
        await _node.DisposeAsync();
    }
}
