using BlockCraft.Cmd;

namespace BlockCraft;

public sealed partial class Commands
{
    private static void RegisterExtra() => Reg("worldborder", 2, "/worldborder <set|add|center|damage|warning|get> ...", (s, a) =>
    {
        if (a.Length == 0) throw new UsageException("/worldborder <set|add|center|damage|warning|get> ...");
        var b = WorldBorder.Of(s.World); long now = Home(s.World).TotalTime;
        switch (a[0])
        {
            case "get" when a.Length == 1:
                double d = b.Diameter(now); s.SetStat(StatType.QueryResult, (int)Math.Floor(d + .5)); s.Message("Średnica granicy świata wynosi " + Arg.F(d)); return;
            case "set" or "add" when a.Length is 2 or 3:
                double old = b.Diameter(now);
                double size = a[0] == "set" ? Arg.Double(a[1], 1, 60000000) : old + Arg.Double(a[1], -old, 60000000 - old);
                long ticks = a.Length == 3 ? (long)Arg.Int(a[2], 0) * 20 : 0;
                if (a[0] == "add") ticks += b.TimeUntilTarget(now);
                b.SetTransition(old, size, ticks, now); break;
            case "center" when a.Length == 3:
                b.CenterX = Arg.Coord(s.BlockPos.X, a[1], true, -30000000, 30000000);
                b.CenterZ = Arg.Coord(s.BlockPos.Z, a[2], true, -30000000, 30000000); break;
            case "damage" when a.Length == 3:
                double v = Arg.Double(a[2], 0);
                if (a[1] == "buffer") b.DamageBuffer = v;
                else if (a[1] == "amount") b.DamageAmount = v;
                else throw new UsageException("/worldborder damage <buffer|amount> <wartość>");
                break;
            case "warning" when a.Length == 3:
                int w = Arg.Int(a[2], 0);
                if (a[1] == "time") b.WarningTime = w;
                else if (a[1] == "distance") b.WarningDistance = w;
                else throw new UsageException("/worldborder warning <time|distance> <wartość>");
                break;
            default: throw new UsageException("/worldborder <set|add|center|damage|warning|get> ...");
        }
        b.Save(); Notify(s, "Zmieniono granicę świata");
    });
}
