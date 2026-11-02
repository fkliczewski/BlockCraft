using BlockCraft.Entities;

namespace BlockCraft.Net;

/// <summary>Zdalny gracz na hoście: jazda, powiązania własnej postaci, części smoka i zdarzenia własnej encji.</summary>
public sealed partial class RemoteClient
{
    private readonly List<(int id, EntityLinkIds l)> _links = new();
    private EntityLinkIds _selfLinks = EntityLinkIds.None;

    /// <summary>Gracz siedzi w pojeździe albo na wierzchowcu: jego pozycję prowadzi host.</summary>
    private bool Riding => P.RidingEntity != null || P.RidingMob != null;

    /// <summary>ID encji od klienta; ujemne to część smoka (NetLinks.DragonPartId).</summary>
    private Entity ResolveEntity(int id)
    {
        if (NetLinks.DecodeDragonPart(id, out int dragonId, out int index))
            return W.EntityById.TryGetValue(dragonId, out var d) && d is EnderDragon dragon && dragon.Parts != null && index < dragon.Parts.Length
                ? dragon.Parts[index] : null;
        return W.EntityById.TryGetValue(id, out var e) ? e : null;
    }

    /// <summary>Pojazd i wierzchowiec własnej postaci (S1BPacketEntityAttach z ID gracza). Wysyłane przy zmianie i co 2 s,
    /// żeby klient, który sam przewidział wysiadanie, wrócił do stanu hosta. Wysiadanie kończy się teleportem na miejsce obok pojazdu.</summary>
    private void SyncSelfLinks()
    {
        var l = NetLinks.Of(P);
        if (l == _selfLinks && _ticks % 40 != 0) return;
        bool dismounted = _selfLinks.Rides && !l.Rides;
        _selfLinks = l;
        NetServer.WriteLinks(Out, P.Id, l);
        if (dismounted) SendTeleport();
    }

    /// <summary>Zdarzenia własnej encji, których klient nie przewiduje: trafienie (pochylenie kamery, czerwony błysk).</summary>
    private void SendSelfEvents()
    {
        var st = Server.StateOf(P);
        if (st == null) return;
        foreach (var (kind, arg) in st.Events)
        {
            if (kind != EntityEventKind.Hurt) continue;
            var w = Out.Begin(S2C.EntityEvent); w.Write(P.Id); w.Write((byte)kind); w.Write(arg); Out.End();
        }
    }
}
