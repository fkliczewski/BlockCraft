using System.Globalization;
using System.Text;
using BlockCraft.Core;

namespace BlockCraft.Cmd;

/// <summary>
/// JsonToNBT z MC 1.8 ("{CustomName:Bob,Health:20f,Tags:[a,b]}") na Nbt BlockCraft. Typy: b/s -> int, L -> long, f -> float,
/// d lub kropka -> double, true/false -> int 1/0, [1,2,3] -> int[], inne listy -> List&lt;object&gt;.
/// </summary>
public static class Mojangson
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static Nbt Parse(string s)
    {
        s = s.Trim();
        if (!s.StartsWith('{')) throw new CommandException("Nieprawidłowy tag danych: musi zaczynać się od '{'");
        int i = 0;
        var v = Value(s, ref i);
        Skip(s, ref i);
        if (i < s.Length) throw new CommandException("Nieprawidłowy tag danych: nadmiarowe znaki za końcem tagu");
        return (Nbt)v;
    }

    private static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    private static object Value(string s, ref int i)
    {
        Skip(s, ref i);
        if (i >= s.Length) throw new CommandException("Nieprawidłowy tag danych: nieoczekiwany koniec");
        char c = s[i];
        if (c == '{')
        {
            var n = new Nbt(); i++;
            Skip(s, ref i);
            while (i < s.Length && s[i] != '}')
            {
                string key = Key(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new CommandException("Nieprawidłowy tag danych: brak ':' po kluczu " + key);
                i++;
                n[key] = Value(s, ref i);
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; Skip(s, ref i); }
                else if (i < s.Length && s[i] != '}') throw new CommandException("Nieprawidłowy tag danych: oczekiwano ',' lub '}'");
            }
            if (i >= s.Length) throw new CommandException("Nieprawidłowy tag danych: brak '}'");
            i++;
            return n;
        }
        if (c == '[')
        {
            int start = i; i++;
            var list = new List<object>();
            Skip(s, ref i);
            while (i < s.Length && s[i] != ']')
            {
                // indeksy "0:" w listach 1.8
                int save = i;
                while (i < s.Length && char.IsDigit(s[i])) i++;
                if (!(i < s.Length && s[i] == ':' && i > save)) i = save; else i++;
                list.Add(Value(s, ref i));
                Skip(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; Skip(s, ref i); }
                else if (i < s.Length && s[i] != ']') throw new CommandException("Nieprawidłowy tag danych: oczekiwano ',' lub ']'");
            }
            if (i >= s.Length) throw new CommandException("Nieprawidłowy tag danych: brak ']'");
            i++;
            string raw = s[start..i];
            if (list.Count > 0 && list.All(o => o is int) && System.Text.RegularExpressions.Regex.IsMatch(raw, @"^\[[-+\d|,\s]+\]$"))
                return list.Select(o => (int)o).ToArray();
            return list;
        }
        if (c == '"') return Quoted(s, ref i);
        int b = i;
        while (i < s.Length && s[i] is not (',' or '}' or ']')) i++;
        return Primitive(s[b..i].Trim());
    }

    private static string Key(string s, ref int i)
    {
        Skip(s, ref i);
        if (i < s.Length && s[i] == '"') return Quoted(s, ref i);
        int b = i;
        while (i < s.Length && s[i] != ':' && s[i] != '}' && s[i] != ',') i++;
        return s[b..i].Trim();
    }

    private static string Quoted(string s, ref int i)
    {
        var sb = new StringBuilder(); i++;
        while (i < s.Length && s[i] != '"')
        {
            if (s[i] == '\\' && i + 1 < s.Length) i++;
            sb.Append(s[i]); i++;
        }
        if (i >= s.Length) throw new CommandException("Nieprawidłowy tag danych: niezamknięty cudzysłów");
        i++;
        return sb.ToString();
    }

    private static object Primitive(string v)
    {
        if (v.Length == 0) return "";
        try
        {
            char last = char.ToLowerInvariant(v[^1]);
            string num = v[..^1];
            bool isNum(string x) => x.Length > 0 && double.TryParse(x, NumberStyles.Float, Inv, out _) && !x.Contains('e', StringComparison.OrdinalIgnoreCase);
            if (last == 'b' && isNum(num) && !num.Contains('.')) return (int)sbyte.Parse(num, Inv);
            if (last == 's' && isNum(num) && !num.Contains('.')) return (int)short.Parse(num, Inv);
            if (last == 'l' && isNum(num) && !num.Contains('.')) return long.Parse(num, Inv);
            if (last == 'f' && isNum(num)) return float.Parse(num, Inv);
            if (last == 'd' && isNum(num)) return double.Parse(num, Inv);
            if (int.TryParse(v, NumberStyles.AllowLeadingSign, Inv, out int iv)) return iv;
            if (v.Contains('.') && double.TryParse(v, NumberStyles.Float, Inv, out double dv)) return dv;
        }
        catch (OverflowException) { }
        if (v.Equals("true", StringComparison.OrdinalIgnoreCase)) return 1;
        if (v.Equals("false", StringComparison.OrdinalIgnoreCase)) return 0;
        return v;
    }

    /// <summary>NBTUtil.func_181123_a: czy wzorzec zawiera się w danych (listy częściowo: każdy element wzorca ma odpowiednik).</summary>
    public static bool Matches(object expected, object actual, bool partialLists = true)
    {
        if (expected == null) return true;
        if (actual == null) return false;
        switch (expected)
        {
            case Nbt e:
                if (actual is not Nbt a) return false;
                foreach (var kv in e) if (!a.TryGetValue(kv.Key, out var av) || !Matches(kv.Value, av, partialLists)) return false;
                return true;
            case List<object> el:
                if (actual is not List<object> al) return false;
                if (!partialLists) return el.Count == al.Count && el.Zip(al).All(p => Matches(p.First, p.Second, false));
                if (el.Count == 0) return al.Count == 0;
                return el.All(x => al.Any(y => Matches(x, y, true)));
            case int[] ei: return actual is int[] ai && ei.AsSpan().SequenceEqual(ai);
            case string es: return actual is string s2 && s2 == es;
            default:
                if (actual is bool ab) actual = ab ? 1 : 0;
                if (expected is bool eb) expected = eb ? 1 : 0;
                try { return Convert.ToDouble(expected, Inv) == Convert.ToDouble(actual, Inv); } catch { return Equals(expected, actual); }
        }
    }

    /// <summary>Scala tag (NBTTagCompound.merge): klucze z 'src' nadpisują, złożone tagi łączone rekurencyjnie.</summary>
    public static void Merge(Nbt dst, Nbt src)
    {
        foreach (var kv in src)
        {
            if (kv.Value is Nbt sc && dst.GetCompound(kv.Key) is { } dc) Merge(dc, sc);
            else dst[kv.Key] = kv.Value is Nbt n ? n.Copy() : kv.Value;
        }
    }

    /// <summary>Zapis w składni 1.8 (do komunikatów /blockdata, /entitydata).</summary>
    public static string ToText(object v) => v switch
    {
        Nbt n => "{" + string.Join(",", n.Select(kv => kv.Key + ":" + ToText(kv.Value))) + "}",
        List<object> l => "[" + string.Join(",", l.Select((o, i) => i + ":" + ToText(o))) + "]",
        int[] a => "[" + string.Join(",", a) + "]",
        byte[] b => "[" + b.Length + " bytes]",
        string s => "\"" + s.Replace("\"", "\\\"") + "\"",
        float f => f.ToString(Inv) + "f",
        double d => d.ToString(Inv) + "d",
        long l => l.ToString(Inv) + "L",
        bool b => b ? "1b" : "0b",
        _ => Convert.ToString(v, Inv)
    };
}
