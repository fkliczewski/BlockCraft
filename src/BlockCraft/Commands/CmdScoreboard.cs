using BlockCraft.Cmd;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Commands
{
    private static void RegisterScoreboard()
    {
        Reg("scoreboard", 2, "/scoreboard <objectives|players|teams> ...", ScoreboardCommand);
        Reg("trigger", 0, "/trigger <cel> <add|set> <wartość>", (s, a) =>
        {
            if (a.Length != 3) throw new UsageException("/trigger <cel> <add|set> <wartość>");
            if (s.Entity is not Player p) throw new CommandException("Tylko gracz może użyć tej komendy");
            var sb = Scoreboard.Of(s.World); var o = Objective(sb, a[0]);
            if (o.Criteria != "trigger" || !sb.HasScore(p.Name, o) || sb.GetScore(p.Name, o).Locked)
                throw new CommandException("Nie możesz jeszcze użyć tego wyzwalacza");
            int v = Arg.Int(a[2]); var score = sb.GetScore(p.Name, o);
            if (a[1] == "set") score.Points = v;
            else if (a[1] == "add") score.Points = unchecked(score.Points + v);
            else throw new UsageException("/trigger <cel> <add|set> <wartość>");
            score.Locked = true; sb.Dirty = true;
            Notify(s, $"Zmieniono wyzwalacz {o.Name} na {score.Points}");
        });
        Reg("stats", 2, "/stats <entity|block> ...", StatsCommand);
    }

    private static ScoreObjective Objective(Scoreboard sb, string name, bool write = false)
    {
        var o = sb.GetObjective(name) ?? throw new CommandException($"Nie znaleziono celu o nazwie '{name}'");
        if (write && o.ReadOnly) throw new CommandException($"Cel '{name}' jest tylko do odczytu");
        return o;
    }

    private static string ScoreHolder(CommandSender s, string arg)
    {
        string n = EntityNameArg(s, arg);
        if (n.Length is 0 or > 40) throw new CommandException("Nazwa wyniku musi mieć od 1 do 40 znaków");
        return n;
    }

    private static void ScoreboardCommand(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/scoreboard <objectives|players|teams> ...");
        var sb = Scoreboard.Of(s.World);
        // Znak * rozwija istniejących posiadaczy wyników, również w drugim argumencie operacji.
        int wildcard = Array.IndexOf(a, "*");
        if (wildcard >= 0)
        {
            if (a.Count(x => x == "*") > 1) throw new CommandException("W komendzie można użyć tylko jednego '*'");
            int success = 0;
            foreach (string n in sb.Scores.Keys.ToArray())
            {
                var copy = (string[])a.Clone(); copy[wildcard] = n;
                try { ScoreboardCommand(s, copy); success++; } catch (CommandException e) { s.Message("§c" + e.Message); }
            }
            s.SetStat(StatType.AffectedEntities, success);
            if (success == 0) throw new CommandException("Nie znaleziono pasujących wyników");
            return;
        }
        switch (a[0])
        {
            case "objectives": ObjectivesCommand(s, sb, a); break;
            case "players": PlayersCommand(s, sb, a); break;
            case "teams": TeamsCommand(s, sb, a); break;
            default: throw new UsageException("/scoreboard <objectives|players|teams> ...");
        }
    }

    private static void ObjectivesCommand(CommandSender s, Scoreboard sb, string[] a)
    {
        switch (a[1])
        {
            case "list" when a.Length == 2:
                s.SetStat(StatType.QueryResult, sb.Objectives.Count);
                s.Message($"Cele ({sb.Objectives.Count}): " + Arg.Nice(sb.Objectives.Values.Select(o => $"{o.Name}: {o.DisplayName} ({o.Criteria})"))); return;
            case "add" when a.Length >= 4:
                if (a[2].Length is 0 or > 16) throw new CommandException("Nazwa celu musi mieć od 1 do 16 znaków");
                if (sb.GetObjective(a[2]) != null) throw new CommandException("Cel o tej nazwie już istnieje");
                if (!Scoreboard.ValidCriteria(a[3])) throw new CommandException("Nieprawidłowe kryterium: " + a[3]);
                string display = a.Length > 4 ? Arg.Join(a, 4) : a[2];
                if (display.Length > 32) throw new CommandException("Wyświetlana nazwa może mieć najwyżej 32 znaki");
                sb.AddObjective(a[2], a[3], display); Notify(s, "Dodano cel " + a[2]); return;
            case "remove" when a.Length == 3:
                sb.RemoveObjective(Objective(sb, a[2])); Notify(s, "Usunięto cel " + a[2]); return;
            case "setdisplay" when a.Length is 3 or 4:
                int slot = Scoreboard.SlotIndex(a[2]);
                if (slot < 0) throw new CommandException("Nieprawidłowy slot wyświetlania: " + a[2]);
                sb.Display[slot] = a.Length == 4 ? Objective(sb, a[3]) : null; sb.Dirty = true;
                Notify(s, "Zmieniono slot " + a[2]); return;
            default: throw new UsageException("/scoreboard objectives <list|add|remove|setdisplay> ...");
        }
    }

    private static void PlayersCommand(CommandSender s, Scoreboard sb, string[] a)
    {
        if (a[1] == "list" && a.Length is 2 or 3)
        {
            if (a.Length == 2) { s.SetStat(StatType.QueryResult, sb.Scores.Count); s.Message("Posiadacze wyników: " + Arg.Nice(sb.Scores.Keys)); }
            else
            {
                string holder = ScoreHolder(s, a[2]);
                if (!sb.Scores.TryGetValue(holder, out var scores)) throw new CommandException("Brak wyników dla " + holder);
                s.SetStat(StatType.QueryResult, scores.Count);
                foreach (var kv in scores) s.Message($"{kv.Key.DisplayName}: {kv.Value.Points} ({kv.Key.Name})");
            }
            return;
        }
        if (a.Length < 3) throw new UsageException("/scoreboard players <set|add|remove|reset|list|enable|test|operation> ...");
        string name = ScoreHolder(s, a[2]);
        if (a[1] == "reset" && a.Length is 3 or 4)
        { sb.ResetScores(name, a.Length == 4 ? Objective(sb, a[3]) : null); Notify(s, "Zresetowano wyniki " + name); return; }
        if (a.Length < 4) throw new UsageException("/scoreboard players " + a[1] + " <gracz> <cel> ...");
        var o = Objective(sb, a[3], a[1] is "set" or "add" or "remove" or "operation");
        switch (a[1])
        {
            case "enable" when a.Length == 4:
                if (o.Criteria != "trigger") throw new CommandException("Cel nie jest wyzwalaczem");
                sb.GetScore(name, o).Locked = false; sb.Dirty = true; break;
            case "set" or "add" or "remove" when a.Length >= 5:
                int v = a[1] == "set" ? Arg.Int(a[4]) : Arg.Int(a[4], 0);
                if (a.Length > 5 && !Mojangson.Matches(Mojangson.Parse(Arg.Join(a, 5)), EntityNbt.Write(GetEntity(s, a[2])), true))
                    throw new CommandException("Dane encji nie pasują");
                int old = sb.GetScore(name, o).Points;
                sb.SetScore(name, o, a[1] == "set" ? v : a[1] == "add" ? unchecked(old + v) : unchecked(old - v)); break;
            case "test" when a.Length is 5 or 6:
                if (!sb.HasScore(name, o)) throw new CommandException("Brak wyniku dla " + name);
                int min = a[4] == "*" ? int.MinValue : Arg.Int(a[4]);
                int max = a.Length == 5 || a[5] == "*" ? int.MaxValue : Arg.Int(a[5], min);
                int points = sb.GetScore(name, o).Points;
                if (points < min || points > max) throw new CommandException($"Wynik {points} nie mieści się w przedziale {min} do {max}");
                s.SetStat(StatType.QueryResult, points); break;
            case "operation" when a.Length == 7:
                string source = ScoreHolder(s, a[5]); var so = Objective(sb, a[6]);
                if (!sb.HasScore(source, so)) throw new CommandException("Brak wyniku źródłowego");
                var target = sb.GetScore(name, o); var from = sb.GetScore(source, so);
                int x = target.Points, y = from.Points;
                switch (a[4])
                {
                    case "+=": target.Points = unchecked(x + y); break;
                    case "-=": target.Points = unchecked(x - y); break;
                    case "*=": target.Points = unchecked(x * y); break;
                    case "/=": if (y != 0) target.Points = x == int.MinValue && y == -1 ? int.MinValue : x / y; break;
                    case "%=": if (y != 0) target.Points = x == int.MinValue && y == -1 ? 0 : x % y; break;
                    case "=": target.Points = y; break;
                    case "<": target.Points = Math.Min(x, y); break;
                    case ">": target.Points = Math.Max(x, y); break;
                    case "><": target.Points = y; from.Points = x; break;
                    default: throw new CommandException("Nieprawidłowa operacja: " + a[4]);
                }
                sb.Dirty = true; break;
            default: throw new UsageException("/scoreboard players " + a[1] + " <gracz> <cel> ...");
        }
        Notify(s, "Zmieniono wynik " + o.Name + " gracza " + name);
    }

    private static void TeamsCommand(CommandSender s, Scoreboard sb, string[] a)
    {
        if (a[1] == "list" && a.Length is 2 or 3)
        {
            if (a.Length == 2) { s.SetStat(StatType.QueryResult, sb.Teams.Count); s.Message("Drużyny: " + Arg.Nice(sb.Teams.Keys)); }
            else { var team = sb.GetTeam(a[2]) ?? throw new CommandException("Nie ma takiej drużyny"); s.SetStat(StatType.QueryResult, team.Members.Count); s.Message(Arg.Nice(team.Members)); }
            return;
        }
        if (a.Length < 3) throw new UsageException("/scoreboard teams <add|remove|empty|join|leave|option|list> ...");
        if (a[1] == "add")
        {
            if (a[2].Length is 0 or > 16 || sb.GetTeam(a[2]) != null) throw new CommandException("Nieprawidłowa lub zajęta nazwa drużyny");
            string display = a.Length > 3 ? Arg.Join(a, 3) : a[2];
            if (display.Length > 32) throw new CommandException("Nazwa drużyny może mieć najwyżej 32 znaki");
            sb.AddTeam(a[2], display); Notify(s, "Dodano drużynę " + a[2]); return;
        }
        if (a[1] is "join" or "leave")
        {
            var team = a[1] == "join" ? sb.GetTeam(a[2]) ?? throw new CommandException("Nie ma takiej drużyny") : null;
            int start = a[1] == "join" ? 3 : 2;
            var names = new List<string>();
            if (a.Length == start) names.Add(s.Entity is Player p ? p.Name : throw new CommandException("Nie podano gracza"));
            for (int i = start; i < a.Length; i++)
            {
                if (Selector.IsSelector(a[i])) names.AddRange(Selector.Match(s, a[i]).Select(ScoreName));
                else names.Add(ScoreHolder(s, a[i]));
            }
            int count = 0;
            foreach (var n in names) if (team != null ? sb.Join(n, team) : sb.Leave(n)) count++;
            s.SetStat(StatType.AffectedEntities, count);
            if (count == 0) throw new CommandException("Nie zmieniono żadnej drużyny");
            Notify(s, $"Zmieniono drużynę {count} graczy"); return;
        }
        var t = sb.GetTeam(a[2]) ?? throw new CommandException("Nie ma takiej drużyny");
        if (a[1] == "remove" && a.Length == 3) sb.RemoveTeam(t);
        else if (a[1] == "empty" && a.Length == 3)
        { s.SetStat(StatType.AffectedEntities, t.Members.Count); foreach (var n in t.Members.ToArray()) sb.Leave(n, t); }
        else if (a[1] == "option" && a.Length == 5)
        {
            switch (a[3])
            {
                case "color":
                    char c = TextComponent.ColorCode(a[4]) ?? throw new CommandException("Nieprawidłowy kolor");
                    t.Color = c; t.Prefix = "§" + c; t.Suffix = "§r"; break;
                case "friendlyfire": t.FriendlyFire = Arg.Bool(a[4]); break;
                case "seeFriendlyInvisibles": t.SeeFriendlyInvisibles = Arg.Bool(a[4]); break;
                case "nametagVisibility" or "deathMessageVisibility":
                    if (a[4] is not ("always" or "never" or "hideForOtherTeams" or "hideForOwnTeam")) throw new CommandException("Nieprawidłowa widoczność");
                    if (a[3] == "nametagVisibility") t.NameTagVisibility = a[4]; else t.DeathMessageVisibility = a[4]; break;
                default: throw new CommandException("Nieprawidłowa opcja drużyny");
            }
        }
        else throw new UsageException("/scoreboard teams <remove|empty|option> <drużyna> ...");
        sb.Dirty = true; Notify(s, "Zmieniono drużynę " + t.Name);
    }

    private static void StatsCommand(CommandSender s, string[] a)
    {
        if (a.Length < 4) throw new UsageException("/stats <entity|block> ...");
        CommandStats stats; int i;
        if (a[0] == "entity")
        {
            var e = GetEntity(s, a[1]); var sb = Scoreboard.Of(s.World); string name = ScoreName(e);
            if (!sb.EntityStats.TryGetValue(name, out stats)) sb.EntityStats[name] = stats = new CommandStats();
            i = 2;
        }
        else if (a[0] == "block" && a.Length >= 6)
        {
            var pos = Arg.BlockPos(s, a, 1);
            if (s.World.GetTileEntity(pos.X, pos.Y, pos.Z) is not CommandBlockTile cb) throw new CommandException("Ten blok nie przechowuje wyników komend");
            stats = cb.Logic.Stats; i = 4;
        }
        else throw new UsageException("/stats <entity|block> ...");
        int type = Array.IndexOf(CommandStats.TypeNames, a[i + 1]);
        if (type < 0) throw new CommandException("Nieprawidłowy rodzaj wyniku");
        if (a[i] == "clear" && a.Length == i + 2) stats.Selector[type] = stats.Objective[type] = null;
        else if (a[i] == "set" && a.Length == i + 4) { stats.Selector[type] = a[i + 2]; stats.Objective[type] = a[i + 3]; }
        else throw new UsageException("/stats ... <set|clear> <wynik> [selektor] [cel]");
        Scoreboard.Of(s.World).Dirty = true; Notify(s, "Zmieniono powiązanie wyniku " + a[i + 1]);
    }
}
