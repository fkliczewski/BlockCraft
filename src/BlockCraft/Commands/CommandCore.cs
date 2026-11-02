using System.Globalization;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Cmd;

/// <summary>CommandException z MC 1.8: komunikat trafia do nadawcy na czerwono, komenda kończy się porażką.</summary>
public class CommandException : Exception { public CommandException(string msg) : base(msg) { } }

/// <summary>WrongUsageException: "Użycie: /..." (commands.generic.usage).</summary>
public sealed class UsageException : CommandException { public UsageException(string usage) : base("Użycie: " + usage) { } }

/// <summary>CommandResultStats.Type z 1.8 (/stats).</summary>
public enum StatType { SuccessCount, AffectedBlocks, AffectedEntities, AffectedItems, QueryResult }

/// <summary>ICommandSender z MC 1.8: kto wykonuje komendę, gdzie jest i dokąd idą komunikaty.</summary>
public abstract class CommandSender
{
    public abstract string Name { get; }
    public abstract World.World World { get; }
    /// <summary>getPositionVector: środek bloku poleceń albo pozycja encji.</summary>
    public abstract (double X, double Y, double Z) Pos { get; }
    /// <summary>getPosition: dla encji BlockPos(x, y + 0.5, z), dla bloku jego pozycja.</summary>
    public abstract (int X, int Y, int Z) BlockPos { get; }
    public virtual Entity Entity => null;
    public abstract void Message(string msg);
    /// <summary>Poziom uprawnień (0 zwykły gracz, 2 blok poleceń, 4 operator/host).</summary>
    public abstract int PermissionLevel { get; }
    /// <summary>sendCommandFeedback: czy operatorzy dostają szare powiadomienie o komendzie.</summary>
    public virtual bool CommandFeedback => Commands.Home(World).Rules.SendCommandFeedback;
    public virtual void SetStat(StatType t, int v) { if (Entity != null) CommandStats.Apply(this, Entity, t, v); }
    public static (int, int, int) Floor(double x, double y, double z) => (MathUtil.FloorToInt(x), MathUtil.FloorToInt(y), MathUtil.FloorToInt(z));
}

public sealed class PlayerSender : CommandSender
{
    public readonly Player P;
    public PlayerSender(Player p) { P = p; }
    public override string Name => P.Name;
    public override World.World World => P.World;
    public override (double X, double Y, double Z) Pos => (P.X, P.Y, P.Z);
    public override (int X, int Y, int Z) BlockPos => Floor(P.X, P.Y + 0.5, P.Z);
    public override Entity Entity => P;
    public override void Message(string msg) => P.SendMessage(msg);
    public override int PermissionLevel => P.IsLocalOp || P.World.AllowCheats ? 4 : 0;
}

/// <summary>Nadawca z /execute: pozycja i encja podmienione, uprawnienia i komunikaty od pierwotnego nadawcy.</summary>
public sealed class ExecuteSender : CommandSender
{
    private readonly CommandSender _orig; private readonly Entity _e; private readonly double _x, _y, _z;
    public ExecuteSender(CommandSender orig, Entity e, double x, double y, double z) { _orig = orig; _e = e; _x = x; _y = y; _z = z; }
    public override string Name => Commands.EntityName(_e);
    public override World.World World => _e.World;
    public override (double X, double Y, double Z) Pos => (_x, _y, _z);
    public override (int X, int Y, int Z) BlockPos => Floor(_x, _y, _z);
    public override Entity Entity => _e;
    public override void Message(string msg) => _orig.Message(msg);
    public override int PermissionLevel => _orig.PermissionLevel;
    public override bool CommandFeedback => Commands.Home(World).Rules.CommandBlockOutput;
    public override void SetStat(StatType t, int v) => CommandStats.Apply(this, _e, t, v);
}

/// <summary>Nadawca bez encji (testy, konsola): poziom 4, komunikaty do listy.</summary>
public sealed class ConsoleSender : CommandSender
{
    private readonly World.World _w;
    public readonly List<string> Log = new();
    public ConsoleSender(World.World w) { _w = w; }
    public override string Name => "Serwer";
    public override World.World World => _w;
    public override (double X, double Y, double Z) Pos => (_w.SpawnX + .5, _w.SpawnY, _w.SpawnZ + .5);
    public override (int X, int Y, int Z) BlockPos => (_w.SpawnX, _w.SpawnY, _w.SpawnZ);
    public override void Message(string msg) => Log.Add(msg);
    public override int PermissionLevel => 4;
}

/// <summary>Definicja komendy (ICommand): nazwa, aliasy, poziom uprawnień, użycie, indeks gracza (isUsernameIndex), wykonanie.</summary>
public sealed class CommandDef
{
    public string Name; public string[] Aliases = Array.Empty<string>(); public int Level = 2; public string Usage;
    public Func<string[], int, bool> UserIndex = (_, _) => false;
    public Action<CommandSender, string[]> Run;
    public Func<CommandSender, string[], IEnumerable<string>> Tab;
}

/// <summary>Parsowanie argumentów jak CommandBase z MC 1.8.</summary>
public static class Arg
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Int(string s)
    {
        if (!int.TryParse(s, NumberStyles.AllowLeadingSign, Inv, out int v)) throw new CommandException($"'{s}' nie jest prawidłową liczbą");
        return v;
    }
    public static int Int(string s, int min, int max = int.MaxValue)
    {
        int v = Int(s);
        if (v < min) throw new CommandException($"Wprowadzona liczba ({v}) jest zbyt mała, musi wynosić co najmniej {min}");
        if (v > max) throw new CommandException($"Wprowadzona liczba ({v}) jest zbyt duża, może wynosić najwyżej {max}");
        return v;
    }
    public static double Double(string s)
    {
        if (!double.TryParse(s, NumberStyles.Float, Inv, out double v) || double.IsNaN(v) || double.IsInfinity(v)) throw new CommandException($"'{s}' nie jest prawidłową liczbą");
        return v;
    }
    public static double Double(string s, double min, double max = double.MaxValue)
    {
        double v = Double(s);
        if (v < min) throw new CommandException($"Wprowadzona liczba ({F(v)}) jest zbyt mała, musi wynosić co najmniej {F(min)}");
        if (v > max) throw new CommandException($"Wprowadzona liczba ({F(v)}) jest zbyt duża, może wynosić najwyżej {F(max)}");
        return v;
    }
    public static bool Bool(string s)
    {
        if (s is "true" or "1") return true;
        if (s is "false" or "0") return false;
        throw new CommandException($"'{s}' nie jest wartością true ani false");
    }
    public static string F(double v) => v.ToString("0.##", Inv);

    /// <summary>CommandBase.parseDouble(base, input, min, max, centerBlock): "~" względnie, liczba całkowita + 0.5 przy centerBlock.</summary>
    public static double Coord(double baseV, string input, bool centerBlock, int min = 0, int max = 0)
    {
        bool rel = input.StartsWith('~');
        double d = rel ? baseV : 0;
        if (!rel || input.Length > 1)
        {
            bool dot = input.Contains('.');
            d += Double(rel ? input[1..] : input);
            if (!dot && !rel && centerBlock) d += 0.5;
        }
        if (min != 0 || max != 0)
        {
            if (d < min) throw new CommandException($"Wprowadzona liczba ({F(d)}) jest zbyt mała, musi wynosić co najmniej {min}");
            if (d > max) throw new CommandException($"Wprowadzona liczba ({F(d)}) jest zbyt duża, może wynosić najwyżej {max}");
        }
        return d;
    }

    /// <summary>CommandBase.parseBlockPos: względem getPosition nadawcy, y w 0..256.</summary>
    public static (int X, int Y, int Z) BlockPos(CommandSender s, string[] a, int i, bool center = false)
    {
        var p = s.BlockPos;
        return (MathUtil.FloorToInt(Coord(p.X, a[i], center, -30000000, 30000000)), MathUtil.FloorToInt(Coord(p.Y, a[i + 1], false, 0, 256)),
            MathUtil.FloorToInt(Coord(p.Z, a[i + 2], center, -30000000, 30000000)));
    }

    public static string Join(string[] a, int from) => from >= a.Length ? "" : string.Join(' ', a, from, a.Length - from);

    /// <summary>getBlockByText: nazwa (z "minecraft:" albo bez) lub numer.</summary>
    public static int Block(string s)
    {
        string k = s.StartsWith("minecraft:") ? s[10..] : s;
        if (k == "air") return 0;
        var it = Items.Item.Get(k);
        if (it != null && it.IsBlock && World.Block.ById[it.BlockId] != null) return it.BlockId;
        if (int.TryParse(k, out int id) && id is > 0 and < 256 && World.Block.ById[id] != null) return id;
        foreach (var b in World.Block.ById) if (b != null && b.Key == k) return b.Id;
        throw new CommandException($"Nie ma bloku o nazwie {s}");
    }

    /// <summary>getItemByText.</summary>
    public static Item Item(string s)
    {
        var it = Items.Item.Get(s);
        if (it == null) throw new CommandException($"Nie ma przedmiotu o nazwie {s}");
        return it;
    }

    /// <summary>Lista "a, b i c" (CommandBase.joinNiceString).</summary>
    public static string Nice(IEnumerable<string> items)
    {
        var l = items.ToList();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < l.Count; i++)
        {
            if (i > 0) sb.Append(i == l.Count - 1 ? " i " : ", ");
            sb.Append(l[i]);
        }
        return sb.ToString();
    }

    public static IEnumerable<string> Match(string last, IEnumerable<string> opts) => opts.Where(o => o.StartsWith(last, StringComparison.OrdinalIgnoreCase));
}
