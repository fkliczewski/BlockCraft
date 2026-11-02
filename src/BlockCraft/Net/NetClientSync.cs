using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>Klient: pęknięcia kopania innych graczy, jazda i smycze (EntityLinks), pasek bossa.</summary>
public sealed partial class NetClient
{
    private readonly Dictionary<int, EntityLinkIds> _linkState = new();
    private readonly HashSet<int> _linkPending = new();
    private int _syncTicks;

    private void BlockBreakAnim(int id, int x, int y, int z, int stage)
    {
        var w = W;
        if (w == null || id == P?.Id) return;
        if (stage is < 0 or > 9) w.DestroyProgress.Remove(id);
        else w.DestroyProgress[id] = new BreakAnim(x, y, z, stage, w.TotalTime);
    }

    private Entity EntityOrSelf(int id) => P != null && id == P.Id ? P : W.EntityById.TryGetValue(id, out var e) ? e : null;

    private void EntityLinks(int id, EntityLinkIds l)
    {
        if (l.IsNone) _linkState.Remove(id); else _linkState[id] = l;
        _linkPending.Remove(id);
        var e = EntityOrSelf(id);
        if (e == null) { if (!l.IsNone) _linkPending.Add(id); return; }
        if (!NetLinks.Apply(W, e, l)) _linkPending.Add(id);
    }

    /// <summary>Nowa encja: jej własne powiązania i powiązania innych, które na nią czekały.</summary>
    private void OnSpawned(Entity e)
    {
        if (_linkState.TryGetValue(e.Id, out var own) && !NetLinks.Apply(W, e, own)) _linkPending.Add(e.Id);
        if (_linkPending.Count > 0) RetryLinks();
    }

    private void RetryLinks()
    {
        foreach (var id in _linkPending.ToList())
        {
            var e = EntityOrSelf(id);
            if (e == null || !_linkState.TryGetValue(id, out var l)) continue;
            if (NetLinks.Apply(W, e, l)) _linkPending.Remove(id);
        }
    }

    private void OnRemoved(int id)
    {
        if (id == P?.Id) return;
        _linkState.Remove(id); _linkPending.Remove(id);
        W?.DestroyProgress.Remove(id);
        // części smoka mają ID z ID smoka: znikają razem z nim, żeby nowy smok o tym ID mógł dodać swoje
        if (W != null && W.EntityById.TryGetValue(id, out var e) && e is EnderDragon { Parts: { } parts })
            foreach (var part in parts) W.RemoveEntity(part);
    }

    private void ResetSync() { _linkState.Clear(); _linkPending.Clear(); W?.DestroyProgress.Clear(); }

    private void TickSync()
    {
        var w = W;
        _syncTicks++;
        if (_linkPending.Count > 0 && _syncTicks % 20 == 0) RetryLinks();
        // pęknięcie bez odświeżenia przez 400 ticków znika (RenderGlobal: cloudTickCounter - lastUpdate > 400)
        if (w.DestroyProgress.Count > 0)
            foreach (var (id, a) in w.DestroyProgress.ToList())
                if (Math.Abs(w.TotalTime - a.Tick) > 400 || w.GetBlock(a.X, a.Y, a.Z) == 0) w.DestroyProgress.Remove(id);
        // pasek bossa: host liczy go tylko dla siebie (BossStatus jest lokalny), klient z encji w swoim świecie jak RenderDragon/RenderWither
        foreach (var e in w.Entities)
        {
            if (e is EnderDragon d && d.Health > 0 && d.DistanceSq(P) < 150 * 150) { BossStatus.Set(d.DisplayName, d.Health, d.MaxHealth, false); break; }
            if (e is Wither wi && !wi.Dead && wi.Health > 0 && wi.DistanceSq(P) < 100 * 100) { BossStatus.Set(wi.DisplayName, wi.Health, wi.MaxHealth, true); break; }
        }
    }
}
