using System.Globalization;
using System.Text;
using BlockCraft.Entities;

namespace BlockCraft.Cmd;

/// <summary>
/// Tekst JSON z MC 1.8 (IChatComponent.Serializer, Gson w trybie lenient) dla /tellraw i /title. Zamienia komponenty
/// (text, translate+with, score, selector, extra, kolory i formatowanie) na tekst z kodami §. clickEvent, hoverEvent
/// i insertion są parsowane, ale BlockCraft nie ma klikalnego czatu, więc ich nie wykonuje.
/// </summary>
public static class TextComponent
{
    private static readonly Dictionary<string, char> Colors = new()
    {
        ["black"] = '0', ["dark_blue"] = '1', ["dark_green"] = '2', ["dark_aqua"] = '3', ["dark_red"] = '4', ["dark_purple"] = '5', ["gold"] = '6',
        ["gray"] = '7', ["dark_gray"] = '8', ["blue"] = '9', ["green"] = 'a', ["aqua"] = 'b', ["red"] = 'c', ["light_purple"] = 'd', ["yellow"] = 'e',
        ["white"] = 'f', ["reset"] = 'r',
    };
    public static IEnumerable<string> ColorNames => Colors.Keys;
    public static char? ColorCode(string name) => Colors.TryGetValue(name.ToLowerInvariant(), out var c) ? c : null;
    public static string ColorName(char code) => Colors.FirstOrDefault(kv => kv.Value == code).Key;

    private sealed class Style
    {
        public char? Color; public bool? Bold, Italic, Under, Strike, Obf;
        public Style Child(Dictionary<string, object> o)
        {
            var s = new Style { Color = Color, Bold = Bold, Italic = Italic, Under = Under, Strike = Strike, Obf = Obf };
            if (o.TryGetValue("color", out var c) && c is string cs) s.Color = ColorCode(cs) ?? s.Color;
            s.Bold = B(o, "bold") ?? s.Bold; s.Italic = B(o, "italic") ?? s.Italic; s.Under = B(o, "underlined") ?? s.Under;
            s.Strike = B(o, "strikethrough") ?? s.Strike; s.Obf = B(o, "obfuscated") ?? s.Obf;
            return s;
        }
        private static bool? B(Dictionary<string, object> o, string k) => o.TryGetValue(k, out var v) ? v is bool b ? b : v is string s ? s == "true" : null : null;
        public string Codes()
        {
            var sb = new StringBuilder("§r");
            if (Color is { } c && c != 'r') sb.Append('§').Append(c);
            if (Obf == true) sb.Append("§k");
            if (Bold == true) sb.Append("§l");
            if (Strike == true) sb.Append("§m");
            if (Under == true) sb.Append("§n");
            if (Italic == true) sb.Append("§o");
            return sb.ToString();
        }
    }

    /// <summary>Parsuje JSON i rozwiązuje selektory i wyniki względem nadawcy (ChatComponentProcessor.processComponent).</summary>
    public static string Render(string json, CommandSender sender, Entity viewer)
    {
        object root;
        try { int i = 0; root = Json.Parse(json, ref i); Json.SkipWs(json, ref i); if (i < json.Length) throw new FormatException("nadmiarowe znaki"); }
        catch (Exception e) when (e is FormatException or IndexOutOfRangeException or ArgumentOutOfRangeException)
        { throw new CommandException("Nieprawidłowy tekst json: " + e.Message); }
        var sb = new StringBuilder();
        Emit(root, new Style(), sender, viewer, sb);
        string s = sb.ToString();
        while (s.StartsWith("§r")) s = s[2..];
        return s;
    }

    private static void Emit(object node, Style st, CommandSender sender, Entity viewer, StringBuilder sb)
    {
        switch (node)
        {
            case null: return;
            case string s: sb.Append(st.Codes()).Append(s); return;
            case List<object> list:
                // tablica: pierwszy element jest rodzicem pozostałych (dziedziczą jego styl)
                if (list.Count == 0) return;
                Emit(list[0], st, sender, viewer, sb);
                var parent = list[0] is Dictionary<string, object> po ? st.Child(po) : st;
                for (int i = 1; i < list.Count; i++) Emit(list[i], parent, sender, viewer, sb);
                return;
            case Dictionary<string, object> o:
                var my = st.Child(o);
                string text = null;
                if (o.TryGetValue("text", out var t)) text = Str(t);
                else if (o.TryGetValue("translate", out var tr))
                {
                    var args = o.TryGetValue("with", out var w) && w is List<object> wl
                        ? wl.Select(a => { var b = new StringBuilder(); Emit(a, my, sender, viewer, b); return b.ToString() + my.Codes(); }).ToArray() : Array.Empty<string>();
                    text = Translate(Str(tr), args);
                }
                else if (o.TryGetValue("score", out var sc) && sc is Dictionary<string, object> so)
                {
                    string name = so.TryGetValue("name", out var n) ? Str(n) : "";
                    string obj = so.TryGetValue("objective", out var ob) ? Str(ob) : "";
                    if (name == "*") name = viewer != null ? Commands.ScoreName(viewer) : "";
                    else if (Selector.IsSelector(name)) { var e = Selector.MatchOne(sender, name); name = e != null ? Commands.ScoreName(e) : name; }
                    var board = Scoreboard.Of(sender.World);
                    var objective = board.GetObjective(obj);
                    var score = objective != null ? board.GetScore(name, objective, false) : null;
                    text = score != null ? score.Points.ToString(CultureInfo.InvariantCulture) : so.TryGetValue("value", out var val) ? Str(val) : "";
                }
                else if (o.TryGetValue("selector", out var sel))
                    text = Selector.MatchToString(sender, Str(sel)) ?? "";
                if (text != null) sb.Append(my.Codes()).Append(text);
                if (o.TryGetValue("extra", out var ex) && ex is List<object> el)
                {
                    if (el.Count == 0) throw new CommandException("Nieprawidłowy tekst json: pusta lista extra");
                    foreach (var c in el) Emit(c, my, sender, viewer, sb);
                }
                return;
            default: sb.Append(st.Codes()).Append(Str(node)); return;
        }
    }

    private static string Str(object o) => o switch
    {
        null => "", string s => s, bool b => b ? "true" : "false",
        double d => d == Math.Floor(d) && Math.Abs(d) < 1e15 ? ((long)d).ToString(CultureInfo.InvariantCulture) : d.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(o, CultureInfo.InvariantCulture)
    };

    /// <summary>Kilka kluczy tłumaczeń używanych w mapach przygodowych; nieznany klucz wypisuje się dosłownie (jak w MC).</summary>
    private static readonly Dictionary<string, string> Lang = new()
    {
        ["chat.type.text"] = "<%s> %s", ["chat.type.announcement"] = "[%s] %s", ["chat.type.emote"] = "* %s %s", ["chat.type.admin"] = "[%s: %s]",
        ["multiplayer.player.joined"] = "%s dołączył do gry", ["multiplayer.player.left"] = "%s opuścił grę", ["death.attack.generic"] = "%s zginął",
        ["commands.generic.usage"] = "Użycie: %s",
    };

    public static string Translate(string key, string[] args)
    {
        string fmt = Lang.TryGetValue(key, out var f) ? f : key;
        var sb = new StringBuilder();
        int next = 0;
        for (int i = 0; i < fmt.Length; i++)
        {
            if (fmt[i] == '%' && i + 1 < fmt.Length)
            {
                if (fmt[i + 1] == '%') { sb.Append('%'); i++; continue; }
                if (fmt[i + 1] == 's') { sb.Append(next < args.Length ? args[next] : ""); next++; i++; continue; }
                int j = i + 1; while (j < fmt.Length && char.IsDigit(fmt[j])) j++;
                if (j > i + 1 && j + 2 < fmt.Length + 1 && fmt.Length > j + 1 && fmt[j] == '$' && fmt[j + 1] == 's')
                {
                    int idx = int.Parse(fmt[(i + 1)..j]) - 1;
                    sb.Append(idx >= 0 && idx < args.Length ? args[idx] : ""); i = j + 1; continue;
                }
            }
            sb.Append(fmt[i]);
        }
        return sb.ToString();
    }
}

/// <summary>Łagodny parser JSON (jak Gson z setLenient): klucze i napisy bez cudzysłowów, apostrofy, liczby, true/false/null.</summary>
public static class Json
{
    public static void SkipWs(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    public static object Parse(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length) throw new FormatException("nieoczekiwany koniec");
        char c = s[i];
        if (c == '{')
        {
            var o = new Dictionary<string, object>(); i++;
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != '}')
            {
                string key = s[i] is '"' or '\'' ? Quoted(s, ref i) : Bare(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || (s[i] != ':' && s[i] != '=')) throw new FormatException("brak ':' po kluczu " + key);
                i++;
                o[key] = Parse(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && (s[i] == ',' || s[i] == ';')) { i++; SkipWs(s, ref i); }
                else if (i < s.Length && s[i] != '}') throw new FormatException("oczekiwano ',' albo '}'");
            }
            if (i >= s.Length) throw new FormatException("brak '}'");
            i++;
            return o;
        }
        if (c == '[')
        {
            var l = new List<object>(); i++;
            SkipWs(s, ref i);
            while (i < s.Length && s[i] != ']')
            {
                l.Add(Parse(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && (s[i] == ',' || s[i] == ';')) { i++; SkipWs(s, ref i); }
                else if (i < s.Length && s[i] != ']') throw new FormatException("oczekiwano ',' albo ']'");
            }
            if (i >= s.Length) throw new FormatException("brak ']'");
            i++;
            return l;
        }
        if (c is '"' or '\'') return Quoted(s, ref i);
        string bare = Bare(s, ref i);
        if (bare.Length == 0) throw new FormatException("nieoczekiwany znak '" + c + "'");
        if (bare == "true") return true;
        if (bare == "false") return false;
        if (bare == "null") return null;
        if (double.TryParse(bare, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
        return bare;
    }

    private static string Bare(string s, ref int i)
    {
        int b = i;
        while (i < s.Length && "{}[]:,;=\"' \t\r\n".IndexOf(s[i]) < 0) i++;
        return s[b..i];
    }

    private static string Quoted(string s, ref int i)
    {
        char q = s[i++]; var sb = new StringBuilder();
        while (i < s.Length && s[i] != q)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                i++;
                switch (s[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u' when i + 4 < s.Length: sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; break;
                    default: sb.Append(s[i]); break;
                }
                i++; continue;
            }
            sb.Append(s[i++]);
        }
        if (i >= s.Length) throw new FormatException("niezamknięty napis");
        i++;
        return sb.ToString();
    }
}
