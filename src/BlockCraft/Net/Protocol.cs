using System.IO.Compression;
using System.Text;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>
/// Protokół BlockCraft (binarny, little-endian). Ramka = ciąg pakietów: [int32 długość treści][byte typ][treść].
/// Klient zaczyna od Login z numerem wersji; host odpowiada LoginOk albo Disconnect.
/// Pakiety o współrzędnych świata niosą numer wymiaru (sbyte: 0 Overworld, -1 Nether, 1 End).
/// </summary>
public static class Protocol
{
    public const int Version = 4;
}

/// <summary>Pakiety klient -> host.</summary>
public enum C2S : byte
{
    Login = 1,          // int wersja, string nick, byte zasięg widzenia
    Input = 2,          // ruch + akcje co tick (patrz NetInput)
    Chat = 3,           // string
    Click = 4,          // byte okno, short slot, byte przycisk, byte typ (ClickType)
    Distribute = 5,     // byte okno, byte przycisk, byte n, short[n] sloty
    CloseWindow = 6,    // byte okno
    CreativeSlot = 7,   // short slot (0-39), stack
    CreativeDrop = 8,   // stack
    DropSelected = 9,   // bool cały stos
    Respawn = 10,
    KeepAlive = 11,     // long czas klienta (ms)
    SelectRecipe = 12,  // byte okno, int indeks oferty
    EnchantItem = 13,   // byte okno, byte przycisk (0-2)
}

/// <summary>Pakiety host -> klient.</summary>
public enum S2C : byte
{
    LoginOk = 1,        // patrz NetServer.SendLogin
    Disconnect = 2,     // string powód
    ChunkData = 3,      // sbyte wymiar, int cx, int cz, int rozmiar, deflate(sekcje, bloki, meta, biomy, TE)
    UnloadChunk = 4,    // sbyte wymiar, int cx, int cz
    BlockChanges = 5,   // sbyte wymiar, int n, n x (int x, short y, int z, byte id, byte meta)
    TileEntity = 6,     // sbyte wymiar, int x, int y, int z, int typ, int len, bajty Save
    SpawnEntity = 7,    // int id, string klucz, string nazwa, int netData, int len, bajty Save
    EntityMoves = 8,    // int n, n x (int id, int x*32, int y*32, int z*32, byte yaw, byte pitch, byte flagi)
    EntityData = 9,     // int id, int netData, int len, bajty Save
    EntityEvent = 10,   // int id, byte zdarzenie, float arg
    EntityEquip = 11,   // int id, stack w ręce, 4 x stack zbroi
    RemoveEntities = 12,// int n, n x int id
    Sound = 13,         // string nazwa, double x,y,z, float głośność, float wysokość
    Particles = 14,     // byte rodzaj, double x,y,z, int ile, int dane
    Time = 15,          // long czas, long pora dnia, bool cykl dnia
    Chat = 16,          // string
    Stats = 17,         // patrz NetServer.SendStats
    InventorySlots = 18,// byte n, n x (byte slot, stack)
    Cursor = 19,        // stack
    OpenWindow = 20,    // byte okno, int rodzaj (GuiKind), int x,y,z, string tytuł
    WindowSlots = 21,   // byte okno, short n, n x (short slot, stack)
    CloseWindow = 22,   // byte okno
    Teleport = 23,      // int id teleportu, double x,y,z, float yaw, float pitch
    Velocity = 24,      // double mx,my,mz
    Death = 25,         // string komunikat
    PlayerList = 26,    // byte n, n x (string nick, int ping)
    WorldState = 27,    // string klucz, int len, bajty (stan modułów, np. pogoda)
    ChangeDimension = 28,// sbyte wymiar
    KeepAlive = 29,     // long czas klienta (echo)
    Settings = 30,      // byte trudność, bool hardcore, bool komendy, gamerules (byte n, n x (string, bool))
    MerchantOffers = 31,// byte okno, int indeks, int liczba, MerchantRecipe[]
}

/// <summary>Zdarzenia encji (EntityEvent).</summary>
public enum EntityEventKind : byte { Hurt = 1, Swing = 2, Death = 3, Pickup = 4 }

/// <summary>Bufor wychodzący: zbiera pakiety jednego ticku w jedną ramkę.</summary>
public sealed class PacketBuffer
{
    private readonly MemoryStream _ms = new();
    public readonly BinaryWriter W;
    private long _start = -1;
    public PacketBuffer() { W = new BinaryWriter(_ms, Encoding.UTF8, true); }
    public long Length => _ms.Length;

    public BinaryWriter Begin(byte type)
    {
        if (_start >= 0) throw new InvalidOperationException("Poprzedni pakiet nie został zamknięty");
        _start = _ms.Position;
        W.Write(0); W.Write(type);
        return W;
    }
    public BinaryWriter Begin(S2C t) => Begin((byte)t);
    public BinaryWriter Begin(C2S t) => Begin((byte)t);

    public void End()
    {
        long end = _ms.Position;
        _ms.Position = _start;
        W.Write((int)(end - _start - 5));
        _ms.Position = end;
        _start = -1;
    }

    /// <summary>Zwraca ramkę i czyści bufor (null gdy pusty).</summary>
    public byte[] Take()
    {
        if (_ms.Length == 0) return null;
        var f = _ms.ToArray();
        _ms.SetLength(0);
        return f;
    }

    public void FlushTo(INetConnection c) { var f = Take(); if (f != null) c.Send(f); }
}

/// <summary>Czytanie pakietów z ramki.</summary>
public static class PacketReader
{
    public static IEnumerable<(byte type, BinaryReader r)> Read(byte[] frame)
    {
        int pos = 0;
        while (pos + 5 <= frame.Length)
        {
            int len = BitConverter.ToInt32(frame, pos);
            byte type = frame[pos + 4];
            if (len < 0 || pos + 5 + len > frame.Length) throw new InvalidDataException("Uszkodzona ramka");
            var br = new BinaryReader(new MemoryStream(frame, pos + 5, len, false), Encoding.UTF8);
            yield return (type, br);
            pos += 5 + len;
        }
    }
}

/// <summary>Ruch i akcje lokalnego gracza w jednym ticku (C2S.Input).</summary>
public struct NetInput
{
    public int TeleportAck;
    public double X, Y, Z; public float Yaw, Pitch;
    public bool OnGround, Sneaking, Sprinting, Flying, Jumping;
    public sbyte Forward, Strafe;
    public byte Selected;
    public bool LeftDown, LeftPressed, RightDown, RightPressed, Middle;
    public byte HitKind; // 0 brak, 1 blok, 2 encja
    public int HitX, HitY, HitZ, HitFace, HitEntity;
    public float HitFx, HitFy, HitFz; // punkt trafienia względem bloku

    public void Write(BinaryWriter w)
    {
        w.Write(TeleportAck);
        w.Write(X); w.Write(Y); w.Write(Z); w.Write(Yaw); w.Write(Pitch);
        w.Write((byte)((OnGround ? 1 : 0) | (Sneaking ? 2 : 0) | (Sprinting ? 4 : 0) | (Flying ? 8 : 0) | (Jumping ? 16 : 0)));
        w.Write(Forward); w.Write(Strafe); w.Write(Selected);
        w.Write((byte)((LeftDown ? 1 : 0) | (LeftPressed ? 2 : 0) | (RightDown ? 4 : 0) | (RightPressed ? 8 : 0) | (Middle ? 16 : 0)));
        w.Write(HitKind);
        if (HitKind == 1) { w.Write(HitX); w.Write(HitY); w.Write(HitZ); w.Write((byte)HitFace); w.Write(HitFx); w.Write(HitFy); w.Write(HitFz); }
        else if (HitKind == 2) w.Write(HitEntity);
    }

    public static NetInput Read(BinaryReader r)
    {
        var i = new NetInput { TeleportAck = r.ReadInt32(), X = r.ReadDouble(), Y = r.ReadDouble(), Z = r.ReadDouble(), Yaw = r.ReadSingle(), Pitch = r.ReadSingle() };
        int f = r.ReadByte();
        i.OnGround = (f & 1) != 0; i.Sneaking = (f & 2) != 0; i.Sprinting = (f & 4) != 0; i.Flying = (f & 8) != 0; i.Jumping = (f & 16) != 0;
        i.Forward = r.ReadSByte(); i.Strafe = r.ReadSByte(); i.Selected = r.ReadByte();
        int a = r.ReadByte();
        i.LeftDown = (a & 1) != 0; i.LeftPressed = (a & 2) != 0; i.RightDown = (a & 4) != 0; i.RightPressed = (a & 8) != 0; i.Middle = (a & 16) != 0;
        i.HitKind = r.ReadByte();
        if (i.HitKind == 1) { i.HitX = r.ReadInt32(); i.HitY = r.ReadInt32(); i.HitZ = r.ReadInt32(); i.HitFace = r.ReadByte(); i.HitFx = r.ReadSingle(); i.HitFy = r.ReadSingle(); i.HitFz = r.ReadSingle(); }
        else if (i.HitKind == 2) i.HitEntity = r.ReadInt32();
        return i;
    }
}

/// <summary>Kodowanie chunków i tile entity do sieci.</summary>
public static class ChunkCodec
{
    public static byte[] SaveTile(TileEntity te)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, Encoding.UTF8, true)) te.Save(bw);
        return ms.ToArray();
    }

    public static TileEntity LoadTile(int type, int x, int y, int z, byte[] data)
    {
        var te = TileEntity.Create(type);
        if (te == null) return null;
        te.Load(new BinaryReader(new MemoryStream(data)));
        te.X = x; te.Y = y; te.Z = z;
        return te;
    }

    /// <summary>Treść chunku skompresowana deflate: liczba sekcji, bloki i meta tych sekcji, biomy, tile entity.</summary>
    public static byte[] Encode(Chunk c)
    {
        int sections = 0;
        for (int s = Chunk.Sections - 1; s >= 0; s--) if (c.SectionCounts[s] > 0) { sections = s + 1; break; }
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, true))
        using (var bw = new BinaryWriter(ds, Encoding.UTF8, true))
        {
            bw.Write((byte)sections);
            bw.Write(c.Blocks, 0, sections * 4096);
            bw.Write(c.Meta, 0, sections * 4096);
            bw.Write(c.Biomes);
            var tes = c.TileEntities.Values.ToArray();
            bw.Write(tes.Length);
            foreach (var te in tes)
            {
                var d = SaveTile(te);
                bw.Write(te.TypeId); bw.Write(te.X); bw.Write(te.Y); bw.Write(te.Z); bw.Write(d.Length); bw.Write(d);
            }
        }
        return ms.ToArray();
    }

    /// <summary>Odtwarza chunk (bez światła; światło liczy LightEngine.InitChunkLocal).</summary>
    public static Chunk Decode(int cx, int cz, byte[] data)
    {
        var c = new Chunk(cx, cz) { Populated = true };
        using var br = new BinaryReader(new DeflateStream(new MemoryStream(data), CompressionMode.Decompress), Encoding.UTF8);
        int sections = br.ReadByte();
        ReadFully(br, c.Blocks, sections * 4096);
        ReadFully(br, c.Meta, sections * 4096);
        ReadFully(br, c.Biomes, 256);
        int n = br.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            int type = br.ReadInt32(), x = br.ReadInt32(), y = br.ReadInt32(), z = br.ReadInt32();
            var d = br.ReadBytes(br.ReadInt32());
            try
            {
                var te = LoadTile(type, x, y, z, d);
                if (te != null) c.TileEntities[Chunk.Index(x & 15, y, z & 15)] = te;
            }
            catch (Exception ex) { BlockCraft.Core.Log.Error("Sieć: tile entity " + type + ": " + ex.Message); }
        }
        c.RecountSections();
        c.RecalcHeightMap();
        return c;
    }

    private static void ReadFully(BinaryReader br, byte[] buf, int n)
    {
        int off = 0;
        while (off < n) { int r = br.Read(buf, off, n - off); if (r <= 0) throw new EndOfStreamException(); off += r; }
    }
}

/// <summary>Porównywanie stosów do wykrywania zmian w ekwipunku.</summary>
public static class StackUtil
{
    public static long Hash(ItemStack s)
    {
        if (s == null) return 0;
        long h = s.Id * 1_000_003L + s.Count * 7919L + s.Damage * 31L + 1;
        if (s.Tag != null && s.Tag.Count > 0)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms)) s.Tag.Write(bw);
            foreach (var b in ms.ToArray()) h = h * 131 + b;
        }
        return h;
    }
}
