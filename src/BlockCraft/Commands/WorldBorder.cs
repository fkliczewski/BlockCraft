using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Cmd;

/// <summary>
/// WorldBorder z MC 1.8: środek, średnica (z płynną zmianą w czasie), obrażenia poza granicą (0.2 na blok za buforem 5 bloków),
/// ostrzeżenia (15 s, 5 bloków). Czas zmiany liczony w tickach świata (MC liczy milisekundy, przy 20 TPS to to samo).
/// Nether ma środek podzielony przez 8 (WorldProviderHell.getWorldBorder), End dzieli granicę z Overworldem.
/// </summary>
public sealed class WorldBorder
{
    public const double WorldSize = 29999984;
    public double CenterX, CenterZ, StartDiameter = 60000000, EndDiameter = 60000000;
    public long StartTick, EndTick;
    public double DamageAmount = 0.2, DamageBuffer = 5;
    public int WarningTime = 15, WarningDistance = 5;
    private string _file;

    public static WorldBorder Client = new();

    public static WorldBorder Of(World.World w)
    {
        if (w == null || w.IsRemote) return Client;
        var home = Commands.Home(w);
        if (home.BorderField == null)
        {
            var b = new WorldBorder();
            if (home.Storage != null) { b._file = Path.Combine(home.Storage.Dir, "data", "worldborder.dat"); b.Load(); }
            home.BorderField = b;
        }
        return home.BorderField;
    }

    public static double CenterScale(World.World w) => w.Dimension == DimensionId.Nether ? 8 : 1;

    public double Diameter(long now)
    {
        if (EndTick > StartTick && now < EndTick)
        {
            double f = Math.Clamp((now - StartTick) / (double)(EndTick - StartTick), 0, 1);
            return StartDiameter + (EndDiameter - StartDiameter) * f;
        }
        return EndDiameter;
    }
    public bool Moving(long now) => EndTick > StartTick && now < EndTick && StartDiameter != EndDiameter;
    /// <summary>EnumBorderStatus: 1 rośnie, -1 maleje, 0 stoi.</summary>
    public int Status(long now) => !Moving(now) ? 0 : EndDiameter > StartDiameter ? 1 : -1;
    /// <summary>Prędkość zmiany w blokach na sekundę (MC: na milisekundę).</summary>
    public double ResizeSpeedPerSecond => EndTick > StartTick ? Math.Abs(EndDiameter - StartDiameter) / ((EndTick - StartTick) / 20.0) : 0;
    public long TimeUntilTarget(long now) => Moving(now) ? EndTick - now : 0;

    public (double MinX, double MinZ, double MaxX, double MaxZ) Bounds(World.World w)
    {
        double d = Diameter(w.TotalTime) / 2, s = CenterScale(w), cx = CenterX / s, cz = CenterZ / s;
        return (Math.Max(cx - d, -WorldSize), Math.Max(cz - d, -WorldSize), Math.Min(cx + d, WorldSize), Math.Min(cz + d, WorldSize));
    }

    /// <summary>contains(BlockPos): blok w całości lub częściowo w granicy.</summary>
    public bool Contains(World.World w, int x, int z)
    {
        var b = Bounds(w);
        return x + 1 > b.MinX && x < b.MaxX && z + 1 > b.MinZ && z < b.MaxZ;
    }
    public bool Contains(World.World w, AABB box)
    {
        var b = Bounds(w);
        return box.MaxX > b.MinX && box.MinX < b.MaxX && box.MaxZ > b.MinZ && box.MinZ < b.MaxZ;
    }

    /// <summary>getClosestDistance: odległość do najbliższej ściany (ujemna poza granicą).</summary>
    public double ClosestDistance(World.World w, double x, double z)
    {
        var b = Bounds(w);
        return Math.Min(Math.Min(x - b.MinX, b.MaxX - x), Math.Min(z - b.MinZ, b.MaxZ - z));
    }

    public void SetSize(double d, long now) { StartDiameter = EndDiameter = d; StartTick = EndTick = now; Save(); }
    public void SetTransition(double from, double to, long ticks, long now)
    {
        StartDiameter = from; EndDiameter = to; StartTick = now; EndTick = now + Math.Max(0, ticks);
        if (ticks <= 0) StartDiameter = to;
        Save();
    }

    /// <summary>Siła ostrzeżenia (GuiIngame.renderVignette): 0 brak, 1 przy samej ścianie.</summary>
    public float Warning(World.World w, double x, double z)
    {
        double f = ClosestDistance(w, x, z);
        long now = w.TotalTime;
        double d0 = Math.Min(ResizeSpeedPerSecond * WarningTime, Math.Abs(EndDiameter - Diameter(now)));
        if (!Moving(now)) d0 = 0;
        double d1 = Math.Max(WarningDistance, d0);
        return f < d1 ? (float)(1 - f / d1) : 0;
    }

    public void Write(BinaryWriter bw)
    {
        bw.Write(CenterX); bw.Write(CenterZ); bw.Write(StartDiameter); bw.Write(EndDiameter); bw.Write(StartTick); bw.Write(EndTick);
        bw.Write(DamageAmount); bw.Write(DamageBuffer); bw.Write(WarningTime); bw.Write(WarningDistance);
    }
    public void Read(BinaryReader br)
    {
        CenterX = br.ReadDouble(); CenterZ = br.ReadDouble(); StartDiameter = br.ReadDouble(); EndDiameter = br.ReadDouble();
        StartTick = br.ReadInt64(); EndTick = br.ReadInt64(); DamageAmount = br.ReadDouble(); DamageBuffer = br.ReadDouble();
        WarningTime = br.ReadInt32(); WarningDistance = br.ReadInt32();
    }
    public byte[] ToBytes() { using var ms = new MemoryStream(); using (var bw = new BinaryWriter(ms)) Write(bw); return ms.ToArray(); }

    private void Load()
    {
        if (_file == null || !File.Exists(_file)) return;
        try { using var br = new BinaryReader(File.OpenRead(_file)); Read(br); }
        catch (Exception ex) { Log.Error("Granica świata: " + ex.Message); }
    }
    public void Save()
    {
        if (_file == null) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); File.WriteAllBytes(_file, ToBytes()); }
        catch (Exception ex) { Log.Error("Zapis granicy świata: " + ex.Message); }
    }
}
