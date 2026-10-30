using System.IO.Compression;
using System.Text.Json;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public sealed class SlotData
{
    public int Slot { get; set; } public int Id { get; set; } public int Count { get; set; } public int Damage { get; set; }
    public string Tag { get; set; } // Nbt w base64
    public static SlotData From(int slot, ItemStack s) => new() { Slot = slot, Id = s.Id, Count = s.Count, Damage = s.Damage, Tag = s.Tag != null && s.Tag.Count > 0 ? Convert.ToBase64String(s.Tag.ToBytes()) : null };
    public ItemStack ToStack() => new(Id, Count, Damage) { Tag = Tag != null ? Nbt.FromBytes(Convert.FromBase64String(Tag)) : null };
}

public sealed class PlayerData
{
    public string Name { get; set; } = "Steve";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public float Yaw { get; set; }
    public float Pitch { get; set; }
    public float Health { get; set; } = 20;
    public int Food { get; set; } = 20;
    public float Saturation { get; set; } = 5;
    public float Exhaustion { get; set; }
    public int GameMode { get; set; }
    public bool Flying { get; set; }
    public int XpLevel { get; set; }
    public float XpProgress { get; set; }
    public int XpTotal { get; set; }
    public int Selected { get; set; }
    public int Air { get; set; } = 300;
    public bool HasSpawn { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int SpawnZ { get; set; }
    public bool Dead { get; set; }
    public List<SlotData> Inventory { get; set; } = new();
}

public sealed class LevelData
{
    public string Name { get; set; }
    public long Seed { get; set; }
    public int Type { get; set; }
    public int GameMode { get; set; }
    public long Time { get; set; }
    public long DayTime { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int SpawnZ { get; set; }
    public bool AllowCheats { get; set; }
    public bool Hardcore { get; set; }
    public int Difficulty { get; set; } = 2;
    public Dictionary<string, bool> Rules { get; set; } = new();
    public PlayerData Player { get; set; }
    public DateTime LastPlayed { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class WorldStorage
{
    public readonly string Dir;
    public static string Root
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".blockcraft");
            Directory.CreateDirectory(d);
            return d;
        }
    }
    public static string SavesRoot { get { var d = Path.Combine(Root, "saves"); Directory.CreateDirectory(d); return d; } }

    public WorldStorage(string dir)
    {
        Dir = dir;
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "chunks"));
        Directory.CreateDirectory(Path.Combine(dir, "players"));
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static string SafeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var s = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(s)) s = "Swiat";
        string baseS = s; int i = 1;
        while (Directory.Exists(Path.Combine(SavesRoot, s))) s = baseS + " (" + (++i) + ")";
        return s;
    }

    public void SaveLevel(LevelData d)
    {
        d.LastPlayed = DateTime.Now;
        var tmp = Path.Combine(Dir, "level.json.tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(d, JsonOpts));
        File.Move(tmp, Path.Combine(Dir, "level.json"), true);
    }

    public static LevelData LoadLevel(string dir)
    {
        var p = Path.Combine(dir, "level.json");
        if (!File.Exists(p)) return null;
        try { return JsonSerializer.Deserialize<LevelData>(File.ReadAllText(p)); }
        catch (Exception ex) { Log.Error("level.json: " + ex.Message); return null; }
    }

    public void SavePlayer(PlayerData d)
    {
        File.WriteAllText(Path.Combine(Dir, "players", SafeName(d.Name) + ".json"), JsonSerializer.Serialize(d, JsonOpts));
    }
    public PlayerData LoadPlayer(string name)
    {
        var p = Path.Combine(Dir, "players", SafeName(name) + ".json");
        if (!File.Exists(p)) return null;
        try { return JsonSerializer.Deserialize<PlayerData>(File.ReadAllText(p)); } catch { return null; }
    }
    private static string SafeName(string n) => new string(n.Where(char.IsLetterOrDigit).ToArray());

    public static List<(string dir, LevelData data)> ListWorlds()
    {
        var res = new List<(string, LevelData)>();
        foreach (var d in Directory.GetDirectories(SavesRoot))
        {
            var ld = LoadLevel(d);
            if (ld != null) res.Add((d, ld));
        }
        return res.OrderByDescending(x => x.Item2.LastPlayed).ToList();
    }

    private string ChunkPath(int cx, int cz) => Path.Combine(Dir, "chunks", $"c.{cx}.{cz}.dat");

    public void SaveChunk(Chunk c, byte[] entityData)
    {
        var path = ChunkPath(c.Cx, c.Cz);
        using var ms = new MemoryStream();
        using (var ds = new DeflateStream(ms, CompressionLevel.Fastest, true))
        using (var bw = new BinaryWriter(ds))
        {
            bw.Write(3); // wersja (3: wysokość 256)
            bw.Write(c.Blocks);
            bw.Write(c.Meta);
            bw.Write(c.Biomes);
            bw.Write(c.Populated);
            bw.Write(c.TileEntities.Count);
            foreach (var te in c.TileEntities.Values.ToArray())
            {
                bw.Write(te.TypeId); bw.Write(te.X); bw.Write(te.Y); bw.Write(te.Z);
                using var tms = new MemoryStream();
                using (var tbw = new BinaryWriter(tms, System.Text.Encoding.UTF8, true)) te.Save(tbw);
                bw.Write((int)tms.Length); bw.Write(tms.GetBuffer(), 0, (int)tms.Length);
            }
            if (entityData != null) { bw.Write(entityData.Length); bw.Write(entityData); }
            else bw.Write(0);
        }
        var tmp = path + ".tmp";
        File.WriteAllBytes(tmp, ms.ToArray());
        File.Move(tmp, path, true);
        c.Modified = false;
    }

    public Chunk LoadChunk(int cx, int cz)
    {
        var path = ChunkPath(cx, cz);
        if (!File.Exists(path)) return null;
        try
        {
            using var fs = File.OpenRead(path);
            using var ds = new DeflateStream(fs, CompressionMode.Decompress);
            using var br = new BinaryReader(ds);
            int ver = br.ReadInt32();
            var c = new Chunk(cx, cz);
            (ver >= 3 ? Chunk.Volume : Chunk.Volume / 2).ReadFully(br, c.Blocks);
            (ver >= 3 ? Chunk.Volume : Chunk.Volume / 2).ReadFully(br, c.Meta);
            256.ReadFully(br, c.Biomes);
            c.Populated = br.ReadBoolean();
            int nte = br.ReadInt32();
            for (int i = 0; i < nte; i++)
            {
                int type = br.ReadInt32(); int x = br.ReadInt32(), y = br.ReadInt32(), z = br.ReadInt32();
                TileEntity te;
                if (ver >= 3)
                {
                    var tbytes = br.ReadBytes(br.ReadInt32());
                    te = TileEntity.Create(type);
                    if (te == null) { Log.Error("Nieznany typ tile entity: " + type); continue; }
                    try { te.Load(new BinaryReader(new MemoryStream(tbytes))); }
                    catch (Exception ex) { Log.Error("Tile entity " + type + ": " + ex.Message); continue; }
                }
                else { te = TileEntity.Create(type); te.Load(br); }
                te.X = x; te.Y = y; te.Z = z;
                c.TileEntities[Chunk.Index(x & 15, y, z & 15)] = te;
            }
            int elen = br.ReadInt32();
            if (elen > 0) c.PendingEntityData = br.ReadBytes(elen);
            c.RecountSections();
            c.RecalcHeightMap();
            c.FromDisk = true;
            return c;
        }
        catch (Exception ex) { Log.Error($"Load chunk {cx},{cz}: {ex.Message}"); return null; }
    }
}

public static class StreamExt
{
    /// <summary>Czyta dokładnie n bajtów do bufora (DeflateStream może zwracać mniej).</summary>
    public static int ReadFully(this int n, BinaryReader br, byte[] buf)
    {
        int off = 0;
        while (off < n) { int r = br.Read(buf, off, n - off); if (r <= 0) throw new EndOfStreamException(); off += r; }
        return 0;
    }
    public static void WriteStack(this BinaryWriter bw, ItemStack s)
    {
        if (s == null) { bw.Write((short)-1); return; }
        bool tag = s.Tag != null && s.Tag.Count > 0;
        bw.Write((short)(s.Id | (tag ? 0x4000 : 0))); bw.Write((byte)s.Count); bw.Write((short)s.Damage);
        if (tag) s.Tag.Write(bw);
    }
    public static ItemStack ReadStack(this BinaryReader br)
    {
        short id = br.ReadInt16();
        if (id < 0) return null;
        bool tag = (id & 0x4000) != 0; id &= 0x3FFF;
        int count = br.ReadByte(); int dmg = br.ReadInt16();
        var t = tag ? Nbt.Read(br) : null;
        if (Item.ById[id] == null) return null;
        return new ItemStack(id, count, dmg) { Tag = t };
    }
}
