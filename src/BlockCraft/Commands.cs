using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>
/// Komendy MC 1.8 (CommandHandler/ServerCommandManager): rozwijanie selektorów wielu encji w argumencie gracza
/// (isUsernameIndex), wynik = liczba udanych wykonań (SuccessCount), powiadomienia operatorów i reguły
/// sendCommandFeedback/commandBlockOutput/logAdminCommands. Implementacje komend w Commands/Cmd*.cs.
/// </summary>
public sealed partial class Commands
{
    private readonly World.World _w;
    public Commands(World.World w) { _w = w; }

    private static readonly Dictionary<string, CommandDef> Map = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<CommandDef> Defs = new();
    [ThreadStatic] private static Dictionary<string, Entity> _resolved;

    public static string[] Names => Defs.SelectMany(d => d.Aliases.Prepend(d.Name)).ToArray();

    static Commands()
    {
        RegisterBasic(); RegisterEntities(); RegisterBlocks(); RegisterScoreboard(); RegisterExtra();
    }

    private static CommandDef Reg(string name, int level, string usage, Action<CommandSender, string[]> run, Func<string[], int, bool> user = null,
        Func<CommandSender, string[], IEnumerable<string>> tab = null, params string[] aliases)
    {
        var d = new CommandDef { Name = name, Level = level, Usage = usage, Run = run, Aliases = aliases, Tab = tab };
        if (user != null) d.UserIndex = user;
        Defs.Add(d); Map[name] = d;
        foreach (var a in aliases) Map[a] = d;
        return d;
    }

    /// <summary>Wykonanie komendy przez gracza (czat). Zwraca true, gdy przynajmniej jedno wykonanie się udało.</summary>
    public bool Execute(Player p, string line) => Run(new PlayerSender(p), line) > 0;

    /// <summary>CommandHandler.executeCommand: zwraca liczbę udanych wykonań.</summary>
    public static int Run(CommandSender s, string line)
    {
        line = (line ?? "").Trim();
        if (line.StartsWith('/')) line = line[1..];
        var parts = SplitJava(line);
        if (parts.Length == 0 || parts[0].Length == 0) return 0;
        var args = parts[1..];
        int count = 0;
        if (!Map.TryGetValue(parts[0], out var def))
            s.Message("§cNieznana komenda. Wpisz \"/help\", aby wyświetlić listę komend.");
        else if (s.PermissionLevel < def.Level)
            s.Message("§cNie masz uprawnień do użycia tej komendy");
        else
        {
            int ui = UsernameIndex(def, args);
            if (ui > -1)
            {
                var list = Selector.Match(s, args[ui]);
                string orig = args[ui];
                s.SetStat(StatType.AffectedEntities, list.Count);
                var prev = _resolved; _resolved = new Dictionary<string, Entity>();
                try
                {
                    foreach (var e in list)
                    {
                        string id = Selector.Uuid(e); _resolved[id] = e;
                        args[ui] = id;
                        if (TryRun(s, def, args)) count++;
                    }
                }
                finally { _resolved = prev; args[ui] = orig; }
            }
            else
            {
                s.SetStat(StatType.AffectedEntities, 1);
                if (TryRun(s, def, args)) count++;
            }
        }
        s.SetStat(StatType.SuccessCount, count);
        return count;
    }

    private static int UsernameIndex(CommandDef d, string[] args)
    {
        for (int i = 0; i < args.Length; i++)
            if (d.UserIndex(args, i) && Selector.MatchesMultiple(args[i])) return i;
        return -1;
    }

    private static bool TryRun(CommandSender s, CommandDef d, string[] args)
    {
        try { d.Run(s, args); return true; }
        catch (CommandException e) { s.Message("§c" + e.Message); }
        catch (Exception e) { s.Message("§cWystąpił nieznany błąd podczas wykonywania tej komendy"); Log.Error("Komenda /" + d.Name + ": " + e); }
        return false;
    }

    /// <summary>String.split(" ") z Javy: puste elementy w środku zostają, końcowe puste są usuwane.</summary>
    public static string[] SplitJava(string line)
    {
        var a = line.Split(' ');
        int n = a.Length;
        while (n > 0 && a[n - 1].Length == 0) n--;
        return n == a.Length ? a : a[..n];
    }

    // ================= wspólne narzędzia =================

    public static World.World Home(World.World w)
    {
        if (w?.Dimensions != null && w.Dimensions.Loaded.TryGetValue(DimensionId.Overworld, out var o)) return o;
        return w;
    }
    public static IEnumerable<World.World> Worlds(World.World w) => w?.Dimensions != null ? w.Dimensions.Loaded.Values.ToList() : new List<World.World> { w };
    public static IEnumerable<Player> AllPlayers(World.World w) => Worlds(w).SelectMany(x => x.Players).ToList();

    /// <summary>Entity.getName: gracz, własna nazwa, nazwa typu (po polsku).</summary>
    public static string EntityName(Entity e) => e switch
    {
        Player p => p.Name,
        Mob { CustomName: { Length: > 0 } n } => n,
        LivingEntity le => le.DisplayName,
        ItemEntity it => it.Stack?.Name ?? "Przedmiot",
        _ => EntityNames.McName(e)
    };
    /// <summary>getDisplayName: z prefiksem i sufiksem drużyny.</summary>
    public static string EntityDisplayName(Entity e)
    {
        string n = EntityName(e);
        return e.World != null ? Scoreboard.Of(e.World).TeamOf(ScoreName(e))?.Format(n) ?? n : n;
    }
    /// <summary>Nazwa w scoreboardzie: gracz po nicku, inne encje po UUID (func_175758_e).</summary>
    public static string ScoreName(Entity e) => e is Player p ? p.Name : Selector.Uuid(e);

    public static Player GetPlayer(CommandSender s, string arg)
    {
        Player p = Selector.MatchOnePlayer(s, arg);
        if (p == null && _resolved != null && _resolved.TryGetValue(arg, out var e)) p = e as Player;
        if (p == null)
        {
            var all = AllPlayers(s.World);
            p = all.FirstOrDefault(x => x.Name.Equals(arg, StringComparison.OrdinalIgnoreCase)) ?? all.FirstOrDefault(x => Selector.Uuid(x) == arg);
            // wygoda BlockCraft: jednoznaczny początek nicku
            if (p == null && !arg.StartsWith('@') && arg.Length > 0) { var pre = all.Where(x => x.Name.StartsWith(arg, StringComparison.OrdinalIgnoreCase)).ToList(); if (pre.Count == 1) p = pre[0]; }
        }
        return p ?? throw new CommandException("Nie można odnaleźć tego gracza");
    }

    public static Entity GetEntity(CommandSender s, string arg)
    {
        Entity e = Selector.MatchOne(s, arg);
        if (e == null && _resolved != null) _resolved.TryGetValue(arg, out e);
        if (e == null)
        {
            var all = AllPlayers(s.World);
            e = all.FirstOrDefault(x => x.Name.Equals(arg, StringComparison.OrdinalIgnoreCase));
            if (e == null && Guid.TryParse(arg, out var g))
            {
                string n = g.ToString("N");
                foreach (var w in Worlds(s.World)) { e = w.Entities.FirstOrDefault(x => x.SaveId == n && !x.Removed); if (e != null) break; }
            }
            else if (e == null && !arg.StartsWith('@') && arg.Count(c => c == '-') == 4) throw new CommandException("Nieprawidłowy format UUID");
        }
        return e ?? throw new CommandException("Nie można odnaleźć tej encji");
    }

    /// <summary>getEntityName: gracz, encja (UUID) albo dosłowny tekst (fałszywy gracz, np. #licznik).</summary>
    public static string EntityNameArg(CommandSender s, string arg)
    {
        try { return GetPlayer(s, arg).Name; }
        catch (CommandException)
        {
            try { return ScoreName(GetEntity(s, arg)); }
            catch (CommandException) { if (arg != null && !arg.StartsWith('@')) return arg; throw; }
        }
    }

    /// <summary>Encje z argumentu: selektor (wiele) albo jedna encja/gracz.</summary>
    public static List<Entity> GetEntities(CommandSender s, string arg)
    {
        if (Selector.IsSelector(arg)) return Selector.Match(s, arg);
        return new List<Entity> { GetEntity(s, arg) };
    }

    /// <summary>notifyOperators: szare powiadomienie do innych operatorów, komunikat do nadawcy (gdy sendCommandFeedback / trackOutput).</summary>
    public static void Notify(CommandSender s, string msg)
    {
        var home = Home(s.World);
        if (s.CommandFeedback)
            foreach (var p in AllPlayers(s.World))
                if (p != s.Entity && p.IsLocalOp && p.Handler != null && !(s is PlayerSender ps && ps.P == p))
                    p.SendMessage("§7§o[" + s.Name + ": " + msg + "]");
        if (home.Rules.LogAdminCommands && s is not ConsoleSender) Log.Info("[" + s.Name + ": " + msg + "]");
        bool own = home.Rules.SendCommandFeedback;
        if (s is CommandBlockLogic cb) own = cb.TrackOutput;
        if (own || s is ConsoleSender) s.Message(msg);
    }

    /// <summary>Łączy argumenty od indeksu i zamienia w nich selektory na nazwy encji (getChatComponentFromNthArg).</summary>
    public static string ChatArg(CommandSender s, string[] a, int from, bool selectors)
    {
        var parts = new List<string>();
        for (int i = from; i < a.Length; i++)
        {
            string w = a[i];
            if (selectors && Selector.IsSelector(w))
                w = Selector.MatchToString(s, w) ?? throw new CommandException("Nie można odnaleźć tego gracza");
            parts.Add(w);
        }
        return string.Join(' ', parts);
    }

    /// <summary>Zapis stanu komend świata (scoreboard, granica).</summary>
    public static void SaveState(World.World home)
    {
        if (home?.ScoreboardField != null) home.ScoreboardField.Save();
        home?.BorderField?.Save();
    }

    /// <summary>Wyszukanie serwera sieciowego (host) przez handlery graczy.</summary>
    public static Net.NetServer FindServer(World.World w)
    {
        foreach (var p in AllPlayers(w))
        {
            if (p.Handler is Net.RemoteClient rc) return rc.Server;
            if (p.Handler is Game g && g.Server != null) return g.Server;
        }
        return null;
    }

    // ================= podpowiedzi TAB =================

    public static List<string> Complete(string text, World.World w)
    {
        var res = new List<string>();
        if (!text.StartsWith("/") || w == null) return res;
        var parts = text[1..].Split(' ');
        if (parts.Length == 1)
        {
            foreach (var n in Names.OrderBy(x => x)) if (n.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)) res.Add("/" + n);
            return res;
        }
        if (!Map.TryGetValue(parts[0], out var d)) return res;
        var args = parts[1..];
        IEnumerable<string> opts;
        try { opts = d.Tab?.Invoke(new ConsoleSender(w), args) ?? PlayerNames(w); } catch { opts = Array.Empty<string>(); }
        string last = args[^1];
        string prefix = string.Join(' ', parts.Take(parts.Length - 1)) + " ";
        foreach (var o in opts.Distinct()) if (o.StartsWith(last, StringComparison.OrdinalIgnoreCase)) res.Add("/" + prefix + o);
        return res;
    }

    public static IEnumerable<string> PlayerNames(World.World w) => AllPlayers(w).Select(p => p.Name);
    private static IEnumerable<string> BlockKeys => Item.All.Where(i => i.IsBlock).Select(i => i.Key);
    private static IEnumerable<string> ItemKeys => Item.All.Select(i => i.Key);
}
