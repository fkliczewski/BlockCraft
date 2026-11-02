using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Cmd;

public sealed class ScoreObjective
{
    public string Name, Criteria, DisplayName;
    /// <summary>"integer" albo "hearts" (kryterium health).</summary>
    public string RenderType => Criteria == "health" ? "hearts" : "integer";
    public bool ReadOnly => Criteria == "health";
}

public sealed class Score
{
    public int Points;
    /// <summary>Score.isLocked: trigger zablokowany do czasu /scoreboard players enable.</summary>
    public bool Locked = true;
}

public sealed class ScoreTeam
{
    public string Name, DisplayName, Prefix = "", Suffix = "";
    public char Color = 'r';
    public bool FriendlyFire = true, SeeFriendlyInvisibles = true;
    public string NameTagVisibility = "always", DeathMessageVisibility = "always";
    public readonly HashSet<string> Members = new();
    public string Format(string name) => Prefix + name + Suffix;
}

/// <summary>
/// Scoreboard z MC 1.8 (net.minecraft.scoreboard): cele z kryteriami, wyniki graczy i encji (UUID), drużyny i sloty wyświetlania
/// (0 list, 1 sidebar, 2 belowName, 3-18 sidebar.team.kolor). Jeden na świat (Overworld hosta), zapis w data/scoreboard.dat,
/// goście dostają kopię przez NetHooks.WorldState["scoreboard"].
/// </summary>
public sealed class Scoreboard
{
    public const int MaxObjectiveName = 16, MaxDisplayName = 32, MaxTeamName = 16, MaxPlayerName = 40;
    public static readonly string[] SlotNames;
    static Scoreboard()
    {
        var l = new List<string> { "list", "sidebar", "belowName" };
        foreach (var c in new[] { "black", "dark_blue", "dark_green", "dark_aqua", "dark_red", "dark_purple", "gold", "gray", "dark_gray", "blue", "green", "aqua", "red", "light_purple", "yellow", "white" })
            l.Add("sidebar.team." + c);
        SlotNames = l.ToArray();
    }

    public readonly Dictionary<string, ScoreObjective> Objectives = new();
    public readonly Dictionary<string, Dictionary<ScoreObjective, Score>> Scores = new();
    public readonly Dictionary<string, ScoreTeam> Teams = new();
    public readonly Dictionary<string, ScoreTeam> TeamOfMember = new();
    public readonly ScoreObjective[] Display = new ScoreObjective[19];
    /// <summary>/stats entity: wiązania wyników komend dla encji (klucz: ScoreName encji).</summary>
    public readonly Dictionary<string, CommandStats> EntityStats = new();
    private string _file;
    public bool Dirty;
    /// <summary>Ostatnio widziane wartości statystyk graczy (dla kryteriów stat.*).</summary>
    public readonly Dictionary<string, int> StatSeen = new();

    public static Scoreboard Client = new();

    public static Scoreboard Of(World.World w)
    {
        if (w == null || w.IsRemote) return Client;
        var home = Commands.Home(w);
        if (home.ScoreboardField == null)
        {
            var sb = new Scoreboard();
            if (home.Storage != null) { sb._file = Path.Combine(home.Storage.Dir, "data", "scoreboard.dat"); sb.Load(); }
            home.ScoreboardField = sb;
        }
        return home.ScoreboardField;
    }

    public static bool ValidCriteria(string c)
    {
        if (c is "dummy" or "trigger" or "deathCount" or "playerKillCount" or "totalKillCount" or "health") return true;
        if (c.StartsWith("teamkill.") || c.StartsWith("killedByTeam.")) return TextComponent.ColorCode(c[(c.IndexOf('.') + 1)..]) is { } k && k != 'r';
        if (c.StartsWith("achievement.")) return AchievementList.Find(c[12..]) != null;
        if (c.StartsWith("stat.")) return c.Length > 5;
        return false;
    }

    public ScoreObjective GetObjective(string name) => name != null && Objectives.TryGetValue(name, out var o) ? o : null;

    public ScoreObjective AddObjective(string name, string criteria, string display)
    {
        var o = new ScoreObjective { Name = name, Criteria = criteria, DisplayName = display ?? name };
        Objectives[name] = o; Dirty = true;
        return o;
    }

    public void RemoveObjective(ScoreObjective o)
    {
        Objectives.Remove(o.Name);
        for (int i = 0; i < Display.Length; i++) if (Display[i] == o) Display[i] = null;
        foreach (var s in Scores.Values) s.Remove(o);
        foreach (var k in Scores.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList()) Scores.Remove(k);
        Dirty = true;
    }

    /// <summary>getValueFromObjective (create = true) albo odczyt bez tworzenia.</summary>
    public Score GetScore(string name, ScoreObjective o, bool create = true)
    {
        if (!Scores.TryGetValue(name, out var m))
        {
            if (!create) return null;
            Scores[name] = m = new Dictionary<ScoreObjective, Score>();
        }
        if (!m.TryGetValue(o, out var s))
        {
            if (!create) return null;
            m[o] = s = new Score(); Dirty = true;
        }
        return s;
    }

    public bool HasScore(string name, ScoreObjective o) => Scores.TryGetValue(name, out var m) && m.ContainsKey(o);

    public void SetScore(string name, ScoreObjective o, int v) { GetScore(name, o).Points = v; Dirty = true; }

    public void ResetScores(string name, ScoreObjective o = null)
    {
        if (!Scores.TryGetValue(name, out var m)) return;
        if (o == null) Scores.Remove(name); else { m.Remove(o); if (m.Count == 0) Scores.Remove(name); }
        Dirty = true;
    }

    /// <summary>getSortedScores: rosnąco, przy remisie odwrotnie alfabetycznie; nazwy z "#" ukryte.</summary>
    public List<(string Name, int Points)> Sorted(ScoreObjective o)
    {
        var l = new List<(string, int)>();
        foreach (var kv in Scores) if (!kv.Key.StartsWith('#') && kv.Value.TryGetValue(o, out var s)) l.Add((kv.Key, s.Points));
        l.Sort((a, b) => a.Item2 != b.Item2 ? a.Item2.CompareTo(b.Item2) : string.Compare(b.Item1, a.Item1, StringComparison.OrdinalIgnoreCase));
        return l;
    }

    public ScoreTeam TeamOf(string name) => name != null && TeamOfMember.TryGetValue(name, out var t) ? t : null;
    public ScoreTeam GetTeam(string name) => name != null && Teams.TryGetValue(name, out var t) ? t : null;

    public ScoreTeam AddTeam(string name, string display)
    {
        var t = new ScoreTeam { Name = name, DisplayName = display ?? name };
        Teams[name] = t; Dirty = true; return t;
    }
    public void RemoveTeam(ScoreTeam t)
    {
        foreach (var m in t.Members) TeamOfMember.Remove(m);
        Teams.Remove(t.Name); Dirty = true;
    }
    public bool Join(string member, ScoreTeam t)
    {
        if (member.Length > MaxPlayerName) throw new CommandException($"Nazwa '{member}' jest za długa dla gracza, może mieć najwyżej {MaxPlayerName} znaków");
        if (TeamOf(member) is { } old) Leave(member, old);
        t.Members.Add(member); TeamOfMember[member] = t; Dirty = true;
        return true;
    }
    public bool Leave(string member, ScoreTeam t = null)
    {
        t ??= TeamOf(member);
        if (t == null || TeamOf(member) != t) return false;
        t.Members.Remove(member); TeamOfMember.Remove(member); Dirty = true;
        return true;
    }

    /// <summary>Nazwa gracza sformatowana drużyną (ScorePlayerTeam.formatPlayerName).</summary>
    public string FormatName(string name) => TeamOf(name)?.Format(name) ?? name;

    public static int SlotIndex(string slot) => Array.IndexOf(SlotNames, slot);

    /// <summary>Slot boczny dla gracza: najpierw sidebar.team.kolor jego drużyny, potem sidebar.</summary>
    public ScoreObjective SidebarFor(string player)
    {
        var t = TeamOf(player);
        if (t != null && t.Color != 'r')
        {
            string n = TextComponent.ColorName(t.Color);
            int i = SlotIndex("sidebar.team." + n);
            if (i >= 0 && Display[i] != null) return Display[i];
        }
        return Display[1];
    }

    // ---------- kryteria ----------

    /// <summary>Zwiększa wyniki gracza we wszystkich celach o danym kryterium (func_96646_a / increaseScore).</summary>
    public void AddByCriteria(string player, string criteria, int amount)
    {
        foreach (var o in Objectives.Values)
            if (o.Criteria == criteria) { GetScore(player, o).Points += amount; Dirty = true; }
    }
    public void SetByCriteria(string player, string criteria, int value)
    {
        foreach (var o in Objectives.Values)
            if (o.Criteria == criteria) { var s = GetScore(player, o); if (s.Points != value) { s.Points = value; Dirty = true; } }
    }

    // ---------- zapis ----------

    public void Write(BinaryWriter w)
    {
        w.Write(1);
        w.Write(Objectives.Count);
        foreach (var o in Objectives.Values) { w.Write(o.Name); w.Write(o.Criteria); w.Write(o.DisplayName); }
        w.Write(Scores.Count);
        foreach (var kv in Scores)
        {
            w.Write(kv.Key); w.Write(kv.Value.Count);
            foreach (var s in kv.Value) { w.Write(s.Key.Name); w.Write(s.Value.Points); w.Write(s.Value.Locked); }
        }
        w.Write(Teams.Count);
        foreach (var t in Teams.Values)
        {
            w.Write(t.Name); w.Write(t.DisplayName); w.Write(t.Prefix); w.Write(t.Suffix); w.Write(t.Color); w.Write(t.FriendlyFire);
            w.Write(t.SeeFriendlyInvisibles); w.Write(t.NameTagVisibility); w.Write(t.DeathMessageVisibility);
            w.Write(t.Members.Count); foreach (var m in t.Members) w.Write(m);
        }
        for (int i = 0; i < Display.Length; i++) w.Write(Display[i]?.Name ?? "");
    }

    /// <summary>Pełny zapis (z /stats encji) do pliku świata.</summary>
    private void WriteFull(BinaryWriter w)
    {
        Write(w);
        w.Write(EntityStats.Count);
        foreach (var kv in EntityStats) { w.Write(kv.Key); kv.Value.Write(w); }
    }

    public void Read(BinaryReader r, bool full = false)
    {
        Objectives.Clear(); Scores.Clear(); Teams.Clear(); TeamOfMember.Clear(); Array.Clear(Display);
        r.ReadInt32();
        int n = r.ReadInt32();
        for (int i = 0; i < n; i++) { var o = new ScoreObjective { Name = r.ReadString(), Criteria = r.ReadString(), DisplayName = r.ReadString() }; Objectives[o.Name] = o; }
        n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            string name = r.ReadString(); int c = r.ReadInt32();
            var m = new Dictionary<ScoreObjective, Score>();
            for (int j = 0; j < c; j++)
            {
                string on = r.ReadString(); var s = new Score { Points = r.ReadInt32(), Locked = r.ReadBoolean() };
                if (Objectives.TryGetValue(on, out var o)) m[o] = s;
            }
            if (m.Count > 0) Scores[name] = m;
        }
        n = r.ReadInt32();
        for (int i = 0; i < n; i++)
        {
            var t = new ScoreTeam
            {
                Name = r.ReadString(), DisplayName = r.ReadString(), Prefix = r.ReadString(), Suffix = r.ReadString(), Color = r.ReadChar(),
                FriendlyFire = r.ReadBoolean(), SeeFriendlyInvisibles = r.ReadBoolean(), NameTagVisibility = r.ReadString(), DeathMessageVisibility = r.ReadString()
            };
            int c = r.ReadInt32();
            for (int j = 0; j < c; j++) { string m = r.ReadString(); t.Members.Add(m); TeamOfMember[m] = t; }
            Teams[t.Name] = t;
        }
        for (int i = 0; i < Display.Length; i++) Display[i] = GetObjective(r.ReadString());
        if (full)
        {
            EntityStats.Clear();
            n = r.ReadInt32();
            for (int i = 0; i < n; i++) { string k = r.ReadString(); var st = new CommandStats(); st.Read(r); EntityStats[k] = st; }
        }
    }

    public byte[] ToBytes(bool full = false)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms)) { if (full) WriteFull(w); else Write(w); }
        return ms.ToArray();
    }

    private void Load()
    {
        if (_file == null || !File.Exists(_file)) return;
        try { using var r = new BinaryReader(File.OpenRead(_file)); Read(r, true); }
        catch (Exception ex) { Log.Error("Scoreboard: " + ex.Message); }
    }

    public void Save()
    {
        if (_file == null) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(_file)!); File.WriteAllBytes(_file, ToBytes(true)); Dirty = false; }
        catch (Exception ex) { Log.Error("Zapis scoreboardu: " + ex.Message); }
    }
}

/// <summary>CommandResultStats z 1.8: dla każdego typu wyniku selektor i cel, do którego trafia liczba.</summary>
public sealed class CommandStats
{
    public readonly string[] Selector = new string[5], Objective = new string[5];
    public bool Empty => Selector.All(s => s == null);

    public void Write(BinaryWriter w) { for (int i = 0; i < 5; i++) { w.Write(Selector[i] ?? ""); w.Write(Objective[i] ?? ""); } }
    public void Read(BinaryReader r)
    {
        for (int i = 0; i < 5; i++) { string s = r.ReadString(), o = r.ReadString(); Selector[i] = s.Length > 0 ? s : null; Objective[i] = o.Length > 0 ? o : null; }
    }

    /// <summary>func_179672_a: wynik trafia do istniejącego już wyniku wskazanej encji.</summary>
    public void Set(CommandSender sender, StatType t, int amount)
    {
        string sel = Selector[(int)t], objName = Objective[(int)t];
        if (sel == null || objName == null) return;
        string name;
        try { name = Commands.EntityNameArg(sender, sel); } catch (CommandException) { return; }
        var sb = Scoreboard.Of(sender.World);
        var obj = sb.GetObjective(objName);
        if (obj != null && sb.HasScore(name, obj)) sb.SetScore(name, obj, amount);
    }

    public static void Apply(CommandSender sender, Entity e, StatType t, int amount)
    {
        var sb = Scoreboard.Of(e.World);
        if (sb.EntityStats.TryGetValue(Commands.ScoreName(e), out var st)) st.Set(sender, t, amount);
    }

    public static readonly string[] TypeNames = { "SuccessCount", "AffectedBlocks", "AffectedEntities", "AffectedItems", "QueryResult" };
}
