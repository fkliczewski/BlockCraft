namespace BlockCraft.Core;

/// <summary>
/// Uproszczony odpowiednik NBT z MC: słownik nazwanych wartości. Dozwolone typy wartości: int, long, float, double,
/// string, bool, byte[], int[], Nbt oraz List&lt;object&gt; z tymi typami. Używany w ItemStack.Tag (enchanty "ench",
/// nazwa "display/Name", kolor skóry "display/color", mikstury "CustomPotionEffects", książki, fajerwerki, sztandary itd.).
/// </summary>
public sealed class Nbt : Dictionary<string, object>
{
    public int GetInt(string k, int def = 0) => TryGetValue(k, out var v) ? Convert.ToInt32(v) : def;
    public long GetLong(string k, long def = 0) => TryGetValue(k, out var v) ? Convert.ToInt64(v) : def;
    public float GetFloat(string k, float def = 0) => TryGetValue(k, out var v) ? Convert.ToSingle(v) : def;
    public double GetDouble(string k, double def = 0) => TryGetValue(k, out var v) ? Convert.ToDouble(v) : def;
    public bool GetBool(string k, bool def = false) => TryGetValue(k, out var v) ? Convert.ToBoolean(v) : def;
    public string GetString(string k, string def = null) => TryGetValue(k, out var v) ? v as string ?? def : def;
    public Nbt GetCompound(string k) => TryGetValue(k, out var v) ? v as Nbt : null;
    public Nbt GetOrCreateCompound(string k) { var c = GetCompound(k); if (c == null) this[k] = c = new Nbt(); return c; }
    public List<object> GetList(string k) => TryGetValue(k, out var v) ? v as List<object> : null;
    public List<object> GetOrCreateList(string k) { var l = GetList(k); if (l == null) this[k] = l = new List<object>(); return l; }

    public Nbt Copy()
    {
        var n = new Nbt();
        foreach (var kv in this) n[kv.Key] = CopyValue(kv.Value);
        return n;
    }
    private static object CopyValue(object v) => v switch
    {
        Nbt c => c.Copy(),
        List<object> l => l.Select(CopyValue).ToList(),
        byte[] b => (byte[])b.Clone(),
        int[] i => (int[])i.Clone(),
        _ => v
    };

    /// <summary>Porównanie zawartości (null i pusty traktowane jak równe).</summary>
    public static bool Same(Nbt a, Nbt b)
    {
        if ((a == null || a.Count == 0) && (b == null || b.Count == 0)) return true;
        if (a == null || b == null || a.Count != b.Count) return false;
        foreach (var kv in a) if (!b.TryGetValue(kv.Key, out var o) || !SameValue(kv.Value, o)) return false;
        return true;
    }
    private static bool SameValue(object x, object y) => (x, y) switch
    {
        (Nbt a, Nbt b) => Same(a, b),
        (List<object> a, List<object> b) => a.Count == b.Count && a.Zip(b).All(p => SameValue(p.First, p.Second)),
        (byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b),
        (int[] a, int[] b) => a.AsSpan().SequenceEqual(b),
        _ => Equals(x, y)
    };

    public void Write(BinaryWriter bw)
    {
        bw.Write(Count);
        foreach (var kv in this) { bw.Write(kv.Key); WriteValue(bw, kv.Value); }
    }
    public static Nbt Read(BinaryReader br)
    {
        var n = new Nbt();
        int c = br.ReadInt32();
        for (int i = 0; i < c; i++) { string k = br.ReadString(); n[k] = ReadValue(br); }
        return n;
    }
    private static void WriteValue(BinaryWriter bw, object v)
    {
        switch (v)
        {
            case int x: bw.Write((byte)1); bw.Write(x); break;
            case long x: bw.Write((byte)2); bw.Write(x); break;
            case float x: bw.Write((byte)3); bw.Write(x); break;
            case double x: bw.Write((byte)4); bw.Write(x); break;
            case string x: bw.Write((byte)5); bw.Write(x); break;
            case bool x: bw.Write((byte)6); bw.Write(x); break;
            case byte[] x: bw.Write((byte)7); bw.Write(x.Length); bw.Write(x); break;
            case int[] x: bw.Write((byte)8); bw.Write(x.Length); foreach (var e in x) bw.Write(e); break;
            case Nbt x: bw.Write((byte)9); x.Write(bw); break;
            case List<object> x: bw.Write((byte)10); bw.Write(x.Count); foreach (var e in x) WriteValue(bw, e); break;
            case short x: bw.Write((byte)1); bw.Write((int)x); break;
            case byte x: bw.Write((byte)1); bw.Write((int)x); break;
            default: throw new InvalidOperationException("Nbt: nieobsługiwany typ " + v?.GetType().Name);
        }
    }
    private static object ReadValue(BinaryReader br)
    {
        byte t = br.ReadByte();
        switch (t)
        {
            case 1: return br.ReadInt32();
            case 2: return br.ReadInt64();
            case 3: return br.ReadSingle();
            case 4: return br.ReadDouble();
            case 5: return br.ReadString();
            case 6: return br.ReadBoolean();
            case 7: return br.ReadBytes(br.ReadInt32());
            case 8: { var a = new int[br.ReadInt32()]; for (int i = 0; i < a.Length; i++) a[i] = br.ReadInt32(); return a; }
            case 9: return Read(br);
            case 10: { int n = br.ReadInt32(); var l = new List<object>(n); for (int i = 0; i < n; i++) l.Add(ReadValue(br)); return l; }
            default: throw new InvalidDataException("Nbt: nieznany typ " + t);
        }
    }

    public byte[] ToBytes() { using var ms = new MemoryStream(); using (var bw = new BinaryWriter(ms)) Write(bw); return ms.ToArray(); }
    public static Nbt FromBytes(byte[] b) { using var br = new BinaryReader(new MemoryStream(b)); return Read(br); }
}
