using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Net;

/// <summary>S2C.MapData (jak S34PacketMaps): host wysyła gościowi zmienione prostokąty pikseli i znaczniki map z jego ekwipunku i widzianych ramek.</summary>
public static class MapSync
{
    public const byte Full = 0, Rect = 1, DecoOnly = 2;

    internal static void Send(PacketBuffer o, World.World w, Dictionary<int, (int ver, int deco)> sent, IEnumerable<int> ids)
    {
        var store = MapStore.Of(w);
        foreach (int id in ids.Distinct())
        {
            var m = store.Get(id);
            if (m == null) continue;
            var (ver, deco) = sent.TryGetValue(id, out var s) ? s : (-1, -1);
            if (ver == m.Version && deco == m.DecoVersion) continue;
            byte kind = DecoOnly; int x0 = 0, z0 = 0, x1 = 127, z1 = 127;
            if (ver != m.Version)
            {
                kind = Full;
                if (ver >= 0 && m.Changes.Count > 0 && m.Changes[0].ver <= ver + 1)
                {
                    kind = Rect; x0 = 127; z0 = 127; x1 = 0; z1 = 0;
                    foreach (var c in m.Changes)
                        if (c.ver > ver) { x0 = Math.Min(x0, c.x0); z0 = Math.Min(z0, c.z0); x1 = Math.Max(x1, c.x1); z1 = Math.Max(z1, c.z1); }
                }
                if (kind == Full) { x0 = 0; z0 = 0; x1 = 127; z1 = 127; }
            }
            var bw = o.Begin(S2C.MapData);
            bw.Write(id); bw.Write((byte)m.Scale); bw.Write((sbyte)m.Dimension); bw.Write(m.XCenter); bw.Write(m.ZCenter); bw.Write(m.Version);
            bw.Write(kind);
            if (kind != DecoOnly)
            {
                bw.Write((byte)x0); bw.Write((byte)z0); bw.Write((byte)(x1 - x0 + 1)); bw.Write((byte)(z1 - z0 + 1));
                for (int z = z0; z <= z1; z++) bw.Write(m.Colors, z * 128 + x0, x1 - x0 + 1);
            }
            bw.Write((byte)Math.Min(255, m.Decorations.Count));
            foreach (var d in m.Decorations.Values.Take(255)) { bw.Write(d.Type); bw.Write(d.X); bw.Write(d.Z); bw.Write(d.Rot); }
            o.End();
            sent[id] = (m.Version, m.DecoVersion);
        }
    }

    internal static void Read(BinaryReader r)
    {
        int id = r.ReadInt32();
        var store = MapStore.Client;
        if (!store.Maps.TryGetValue(id, out var m)) store.Maps[id] = m = new MapData { Id = id };
        m.Scale = r.ReadByte(); m.Dimension = r.ReadSByte(); m.XCenter = r.ReadInt32(); m.ZCenter = r.ReadInt32();
        int ver = r.ReadInt32();
        byte kind = r.ReadByte();
        if (kind != DecoOnly)
        {
            int x0 = r.ReadByte(), z0 = r.ReadByte(), w = r.ReadByte(), h = r.ReadByte();
            if (w == 0) w = 256; if (h == 0) h = 256;
            for (int z = 0; z < h; z++) r.Read(m.Colors, (z0 + z) * 128 + x0, w);
        }
        m.Version = ver;
        m.Decorations.Clear();
        int n = r.ReadByte();
        for (int i = 0; i < n; i++) m.Decorations["d" + i] = new MapDecoration { Type = r.ReadByte(), X = r.ReadSByte(), Z = r.ReadSByte(), Rot = r.ReadByte() };
        m.DecoVersion++;
    }
}

public sealed partial class RemoteClient
{
    private readonly Dictionary<int, (int ver, int deco)> _mapSent = new();

    private void SendMaps()
    {
        var ids = new List<int>();
        foreach (var s in P.Inventory.Main) if (s?.Id == I.FilledMap) ids.Add(s.Damage);
        foreach (var e in _tracked.Values) if (e is ItemFrameEntity { Item.Id: I.FilledMap } f) ids.Add(f.Item.Damage);
        if (ids.Count > 0) MapSync.Send(Out, W, _mapSent, ids);
    }
}
