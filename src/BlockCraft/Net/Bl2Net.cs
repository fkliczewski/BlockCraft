using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Net;

/// <summary>Sieć pionka BL2: edycja i podpis książki (C2S.EditBook, jak MC|BEdit i MC|BSign) oraz statystyki (C2S.RequestStats, S2C.Statistics).</summary>
public static class Bl2Net
{
    /// <summary>Klient: wysyła strony (i ewentualnie podpis) trzymanej książki z piórem do hosta.</summary>
    public static void SendEditBook(NetClient client, IReadOnlyList<string> pages, bool sign, string title)
    {
        var w = client.Out.Begin(C2S.EditBook);
        w.Write(sign); w.Write(title ?? "");
        int n = Math.Min(pages.Count, Books.MaxPages);
        w.Write((short)n);
        for (int i = 0; i < n; i++) w.Write(pages[i] ?? "");
        client.Out.End();
    }

    public static void RequestStatistics(NetClient client) { client.Out.Begin(C2S.RequestStats); client.Out.End(); }

    public static void ReadStatistics(Player p, BinaryReader r)
    {
        var data = r.ReadBytes(r.ReadInt32());
        if (p != null) Bl2Stats.Deserialize(data, p.Statistics);
        Gui.StatsScreen.Received++;
    }
}

public sealed partial class RemoteClient
{
    /// <summary>NetHandlerPlayServer: MC|BEdit / MC|BSign - tylko książka z piórem w ręce, limity stron i tytułu sprawdza Books.Apply.</summary>
    public void EditBook(BinaryReader r)
    {
        bool sign = r.ReadBoolean(); string title = r.ReadString();
        int n = r.ReadInt16();
        if (n < 0 || n > Books.MaxPages) return;
        var pages = new List<string>(n);
        for (int i = 0; i < n; i++) pages.Add(r.ReadString());
        if (P == null || P.Dead) return;
        Books.Apply(P, pages, sign, title);
        ForceSync();
    }

    /// <summary>S37PacketStatistics: cała kopia statystyk gracza.</summary>
    public void SendStatistics()
    {
        if (P == null) return;
        var bytes = Bl2Stats.Serialize(P.Statistics);
        var w = Out.Begin(S2C.Statistics); w.Write(bytes.Length); w.Write(bytes); Out.End();
    }
}
