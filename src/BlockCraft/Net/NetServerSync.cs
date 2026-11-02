using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>Host: postęp kopania wszystkich graczy (S2C.BlockBreakAnim) i powiązania encji (S2C.EntityLinks).</summary>
public sealed partial class NetServer
{
    /// <summary>Kontroler gracza hosta (jego pęknięcia kopania widzą goście).</summary>
    public Func<PlayerController> HostCtrl = () => null;

    private readonly Dictionary<int, (World.World w, BreakAnim a)> _breaks = new();
    private readonly Dictionary<int, (World.World w, Player p, BreakAnim a)> _breaksNow = new();

    /// <summary>Wysyła zmiany etapu pęknięcia (jak PlayerControllerMP/ItemInWorldManager: tylko gdy etap się zmienia).</summary>
    private void UpdateBreaks()
    {
        _breaksNow.Clear();
        void Add(Player p, PlayerController c)
        {
            if (p == null || c == null || p.Removed || p.Dead || c.BreakX == int.MinValue) return;
            int st = c.BreakStage;
            if (st < 0) return;
            _breaksNow[p.Id] = (p.World, p, new BreakAnim(c.BreakX, c.BreakY, c.BreakZ, Math.Min(st, 9), p.World.TotalTime));
        }
        Add(HostPlayer(), HostCtrl());
        foreach (var c in Clients) Add(c.P, c.Ctrl);
        foreach (var (id, n) in _breaksNow)
        {
            if (_breaks.TryGetValue(id, out var o) && o.w == n.w && o.a.X == n.a.X && o.a.Y == n.a.Y && o.a.Z == n.a.Z && o.a.Stage == n.a.Stage) continue;
            if (o.w != null && o.w != n.w) SendBreak(id, o.w, o.a with { Stage = -1 });
            _breaks[id] = (n.w, n.a);
            SendBreak(id, n.w, n.a);
        }
        if (_breaks.Count > _breaksNow.Count)
            foreach (var id in _breaks.Keys.Where(k => !_breaksNow.ContainsKey(k)).ToList())
            {
                var o = _breaks[id];
                _breaks.Remove(id);
                SendBreak(id, o.w, o.a with { Stage = -1 });
            }
    }

    private void SendBreak(int id, World.World w, BreakAnim a)
    {
        // host rysuje pęknięcia gości ze swojego świata (własne ma w PlayerController)
        if (HostPlayer()?.Id != id || HostPlayer()?.World != w)
        {
            if (a.Stage < 0) w.DestroyProgress.Remove(id); else w.DestroyProgress[id] = a;
        }
        foreach (var c in Clients)
        {
            if (c.P == null || c.P.Id == id || c.P.World != w || c.Conn.IsClosed) continue;
            if (a.Stage >= 0 && c.P.DistanceSq(a.X + .5, a.Y + .5, a.Z + .5) > 32 * 32) continue;
            var bw = c.Out.Begin(S2C.BlockBreakAnim);
            bw.Write(id); bw.Write(a.X); bw.Write(a.Y); bw.Write(a.Z); bw.Write((sbyte)a.Stage);
            c.Out.End();
        }
    }

    internal static void WriteLinks(PacketBuffer o, int id, EntityLinkIds l)
    {
        var bw = o.Begin(S2C.EntityLinks);
        bw.Write(id); bw.Write(l.Riding); bw.Write(l.Mount); bw.Write(l.Leash);
        o.End();
    }
}
