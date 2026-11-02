using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Commands
{
    private static readonly string[] ModeNames = { "Tryb przetrwania", "Tryb kreatywny", "Tryb przygody", "Tryb obserwatora" };
    private static readonly string[] DiffNames = { "Pokojowy", "Łatwy", "Normalny", "Trudny" };

    /// <summary>getGameModeFromCommand: nazwy, skróty, numery 0-3 (i polskie aliasy BlockCraft).</summary>
    public static int ParseGameMode(string s) => s.ToLowerInvariant() switch
    {
        "survival" or "s" or "przetrwanie" => 0,
        "creative" or "c" or "kreatywny" => 1,
        "adventure" or "a" or "przygoda" => 2,
        "spectator" or "sp" or "obserwator" => 3,
        _ => Arg.Int(s, 0, 3)
    };

    private static void RegisterBasic()
    {
        Reg("help", 0, "/help [strona|nazwa komendy]", Help, tab: (s, a) => a.Length == 1 ? Names : Array.Empty<string>(), aliases: "?");

        Reg("time", 2, "/time <set|add|query> <wartość>", (s, a) =>
        {
            if (a.Length <= 1) throw new UsageException("/time <set|add|query> <wartość>");
            var home = Home(s.World);
            if (a[0] == "set")
            {
                int v = a[1] switch { "day" => 1000, "night" => 13000, "noon" => 6000, "midnight" => 18000, "sunrise" => 23000, "sunset" => 12000, _ => Arg.Int(a[1], 0) };
                foreach (var w in Worlds(s.World)) w.DayTime = v;
                Notify(s, "Ustawiono czas na " + v);
                return;
            }
            if (a[0] == "add")
            {
                int v = Arg.Int(a[1], 0);
                foreach (var w in Worlds(s.World)) w.DayTime += v;
                Notify(s, "Dodano " + v + " do czasu");
                return;
            }
            if (a[0] == "query")
            {
                if (a[1] == "daytime") { int v = (int)(s.World.DayTime % int.MaxValue % 24000); s.SetStat(StatType.QueryResult, v); Notify(s, "Czas wynosi " + v); return; }
                if (a[1] == "gametime") { int v = (int)(home.TotalTime % int.MaxValue); s.SetStat(StatType.QueryResult, v); Notify(s, "Czas wynosi " + v); return; }
            }
            throw new UsageException("/time <set|add|query> <wartość>");
        }, tab: (s, a) => a.Length == 1 ? new[] { "set", "add", "query" } : a.Length == 2 && a[0] == "set" ? new[] { "day", "night" } : a.Length == 2 && a[0] == "query" ? new[] { "daytime", "gametime" } : Array.Empty<string>());

        Reg("weather", 2, "/weather <clear|rain|thunder> [czas w sekundach]", (s, a) =>
        {
            if (a.Length < 1 || a.Length > 2) throw new UsageException("/weather <clear|rain|thunder> [czas w sekundach]");
            int ticks = (300 + s.World.Rand.Next(600)) * 20;
            if (a.Length >= 2) ticks = Arg.Int(a[1], 1, 1000000) * 20;
            var home = Home(s.World);
            switch (a[0])
            {
                case "clear": home.SetWeather("clear", ticks); Notify(s, "Zmiana pogody na bezchmurną"); break;
                case "rain": home.SetWeather("rain", ticks); Notify(s, "Zmiana pogody na deszczową"); break;
                case "thunder": home.SetWeather("thunder", ticks); Notify(s, "Zmiana pogody na deszcz i burzę"); break;
                default: throw new UsageException("/weather <clear|rain|thunder> [czas w sekundach]");
            }
        }, tab: (s, a) => a.Length == 1 ? new[] { "clear", "rain", "thunder" } : Array.Empty<string>());

        Reg("toggledownfall", 2, "/toggledownfall", (s, a) =>
        {
            var home = Home(s.World);
            bool raining = home.Weather.Raining;
            home.SetWeather(raining ? "clear" : "rain", (300 + s.World.Rand.Next(600)) * 20);
            Notify(s, "Przełączono opady");
        });

        Reg("difficulty", 2, "/difficulty <nowy poziom trudności>", (s, a) =>
        {
            if (a.Length <= 0) throw new UsageException("/difficulty <nowy poziom trudności>");
            int d = a[0] switch
            {
                "peaceful" or "p" => 0, "easy" or "e" => 1, "normal" or "n" => 2, "hard" or "h" => 3,
                _ => Arg.Int(a[0], 0, 3)
            };
            foreach (var w in Worlds(s.World)) w.Difficulty = (Difficulty)d;
            Notify(s, "Ustawiono poziom trudności gry na " + DiffNames[d]);
        }, tab: (s, a) => a.Length == 1 ? new[] { "peaceful", "easy", "normal", "hard" } : Array.Empty<string>());

        Reg("gamerule", 2, "/gamerule <nazwa reguły> [wartość]", (s, a) =>
        {
            var rules = Home(s.World).Rules;
            string name = a.Length > 0 ? a[0] : "", value = a.Length > 1 ? Arg.Join(a, 1) : "";
            switch (a.Length)
            {
                case 0: s.Message(Arg.Nice(rules.AllNames)); return;
                case 1:
                    string v = rules.Get(name) ?? throw new CommandException($"Nie ma reguły gry o nazwie '{name}'");
                    s.Message(name + " = " + v);
                    s.SetStat(StatType.QueryResult, v == "true" ? 1 : int.TryParse(v, out int iv) ? iv : 0);
                    return;
                default:
                    bool known = GameRules.Names.Contains(name, StringComparer.OrdinalIgnoreCase);
                    if (known && !name.Equals("randomTickSpeed", StringComparison.OrdinalIgnoreCase) && value is not ("true" or "false"))
                        throw new CommandException($"'{value}' nie jest wartością true ani false");
                    rules.Set(name, value, out _, true);
                    foreach (var w in Worlds(s.World)) if (w != Home(s.World)) w.Rules.CopyFrom(rules);
                    Notify(s, "Reguła gry została zmieniona");
                    return;
            }
        }, tab: (s, a) => a.Length == 1 ? Home(s.World).Rules.AllNames : a.Length == 2 && !a[0].Equals("randomTickSpeed", StringComparison.OrdinalIgnoreCase) ? new[] { "true", "false" } : Array.Empty<string>());

        Reg("seed", 0, "/seed", (s, a) => { s.Message("Ziarno: " + Home(s.World).Seed); s.SetStat(StatType.QueryResult, (int)Home(s.World).Seed); });

        Reg("setworldspawn", 2, "/setworldspawn [<x> <y> <z>]", (s, a) =>
        {
            (int X, int Y, int Z) p;
            if (a.Length == 0) p = s.BlockPos;
            else if (a.Length == 3) p = Arg.BlockPos(s, a, 0, true);
            else throw new UsageException("/setworldspawn [<x> <y> <z>]");
            var home = Home(s.World);
            home.SpawnX = p.X; home.SpawnY = p.Y; home.SpawnZ = p.Z;
            Notify(s, $"Ustawiono punkt odrodzenia świata na ({p.X}, {p.Y}, {p.Z})");
        });

        Reg("spawnpoint", 2, "/spawnpoint [gracz] [<x> <y> <z>]", (s, a) =>
        {
            if (a.Length > 1 && a.Length < 4) throw new UsageException("/spawnpoint [gracz] [<x> <y> <z>]");
            var p = a.Length > 0 ? GetPlayer(s, a[0]) : AsPlayer(s);
            var pos = a.Length > 3 ? Arg.BlockPos(s, a, 1, true) : CommandSender.Floor(p.X, p.Y + 0.5, p.Z);
            p.SpawnX = pos.Item1; p.SpawnY = pos.Item2; p.SpawnZ = pos.Item3; p.HasSpawn = true;
            Notify(s, $"Ustawiono punkt odrodzenia gracza {p.Name} na ({pos.Item1}, {pos.Item2}, {pos.Item3})");
        }, user: (a, i) => i == 0);

        Reg("defaultgamemode", 2, "/defaultgamemode <tryb>", (s, a) =>
        {
            if (a.Length <= 0) throw new UsageException("/defaultgamemode <tryb>");
            int gm = ParseGameMode(a[0]);
            var srv = FindServer(s.World);
            if (srv != null) srv.DefaultGameMode = gm;
            Home(s.World).DefaultGameMode = gm;
            Notify(s, "Domyślny tryb gry w świecie to teraz " + ModeNames[gm]);
        }, tab: (s, a) => a.Length == 1 ? new[] { "survival", "creative", "adventure", "spectator" } : Array.Empty<string>());

        Reg("gamemode", 2, "/gamemode <tryb> [gracz]", (s, a) =>
        {
            if (a.Length <= 0) throw new UsageException("/gamemode <tryb> [gracz]");
            int gm = ParseGameMode(a[0]);
            var p = a.Length >= 2 ? GetPlayer(s, a[1]) : AsPlayer(s);
            p.SetGameMode(gm);
            p.FallDistance = 0;
            p.NetGameModeHook?.Invoke();
            if (Home(s.World).Rules.SendCommandFeedback) p.SendMessage("Twój tryb gry został zaktualizowany");
            if (s.Entity != p) Notify(s, $"Ustawiono tryb gry gracza {p.Name} na {ModeNames[gm]}");
            else Notify(s, "Ustawiono własny tryb gry na " + ModeNames[gm]);
        }, user: (a, i) => i == 1, tab: (s, a) => a.Length == 1 ? new[] { "survival", "creative", "adventure", "spectator" } : a.Length == 2 ? PlayerNames(s.World) : Array.Empty<string>(), aliases: "gm");

        Reg("say", 1, "/say <wiadomość ...>", (s, a) =>
        {
            if (a.Length <= 0 || a[0].Length <= 0) throw new UsageException("/say <wiadomość ...>");
            Broadcast(s.World, "§d[" + s.Name + "] " + ChatArg(s, a, 0, true));
        });
        Reg("me", 0, "/me <akcja ...>", (s, a) =>
        {
            if (a.Length <= 0) throw new UsageException("/me <akcja ...>");
            Broadcast(s.World, "* " + s.Name + " " + ChatArg(s, a, 0, s is not PlayerSender));
        });
        Reg("tell", 0, "/tell <gracz> <prywatna wiadomość ...>", (s, a) =>
        {
            if (a.Length < 2) throw new UsageException("/tell <gracz> <prywatna wiadomość ...>");
            var p = GetPlayer(s, a[0]);
            if (p == s.Entity) throw new CommandException("Nie możesz wysłać prywatnej wiadomości do samego siebie!");
            string msg = ChatArg(s, a, 1, s is not PlayerSender);
            p.SendMessage($"§7§o{s.Name} szepcze do ciebie: {msg}");
            s.Message($"§7§oSzepczesz do {p.Name}: {msg}");
        }, user: (a, i) => i == 0, aliases: new[] { "msg", "w" });

        Reg("tellraw", 2, "/tellraw <gracz> <wiadomość json>", (s, a) =>
        {
            if (a.Length < 2) throw new UsageException("/tellraw <gracz> <wiadomość json>");
            var p = GetPlayer(s, a[0]);
            p.SendMessage(TextComponent.Render(Arg.Join(a, 1), s, p));
        }, user: (a, i) => i == 0);

        Reg("title", 2, "/title <gracz> <title|subtitle|clear|reset|times> ...", Title, user: (a, i) => i == 0,
            tab: (s, a) => a.Length == 1 ? PlayerNames(s.World) : a.Length == 2 ? new[] { "title", "subtitle", "clear", "reset", "times" } : Array.Empty<string>());

        Reg("kick", 3, "/kick <gracz> [powód ...]", (s, a) =>
        {
            if (a.Length <= 0 || a[0].Length <= 1) throw new UsageException("/kick <gracz> [powód ...]");
            var p = AllPlayers(s.World).FirstOrDefault(x => x.Name.Equals(a[0], StringComparison.OrdinalIgnoreCase)) ?? throw new CommandException("Nie można odnaleźć tego gracza");
            string reason = a.Length >= 2 ? ChatArg(s, a, 1, true) : "Wyrzucony przez operatora.";
            if (p.Handler is not Net.RemoteClient rc) throw new CommandException("Nie można wyrzucić gospodarza gry");
            rc.Kick(reason);
            Notify(s, a.Length >= 2 ? $"Wyrzucono {p.Name} z gry: '{reason}'" : $"Wyrzucono {p.Name} z gry");
        });

        Reg("list", 0, "/list", (s, a) =>
        {
            var ps = AllPlayers(s.World).ToList();
            s.Message($"Na serwerze jest {ps.Count}/{Math.Max(8, ps.Count)} graczy:");
            s.Message(Arg.Nice(ps.Select(p => p.Name)));
            s.SetStat(StatType.QueryResult, ps.Count);
        });

        Reg("particle", 2, "/particle <nazwa> <x> <y> <z> <xd> <yd> <zd> <prędkość> [ilość] [tryb]", Particle,
            tab: (s, a) => a.Length == 1 ? ParticleNames.Select(p => p.name) : a.Length == 10 ? new[] { "normal", "force" } : Array.Empty<string>());
        Reg("playsound", 2, "/playsound <dźwięk> <gracz> [x] [y] [z] [głośność] [wysokość] [minimalna głośność]", PlaySound, user: (a, i) => i == 1);

        Reg("xp", 2, "/xp <ilość> [gracz] LUB /xp <ilość>L [gracz]", (s, a) =>
        {
            if (a.Length <= 0) throw new UsageException("/xp <ilość> [gracz] LUB /xp <ilość>L [gracz]");
            string v = a[0];
            bool lv = v.EndsWith('l') || v.EndsWith('L');
            if (lv && v.Length > 1) v = v[..^1];
            int n = Arg.Int(v);
            bool take = n < 0;
            if (take) n = -n;
            var p = a.Length > 1 ? GetPlayer(s, a[1]) : AsPlayer(s);
            if (lv)
            {
                s.SetStat(StatType.QueryResult, p.XpLevel);
                if (take) { p.XpLevel = Math.Max(0, p.XpLevel - n); if (p.XpLevel == 0) p.XpProgress = 0; Notify(s, $"Zabrano {n} poziomów graczowi {p.Name}"); }
                else { p.XpLevel += n; Notify(s, $"Dano {n} poziomów graczowi {p.Name}"); }
            }
            else
            {
                s.SetStat(StatType.QueryResult, p.XpTotal);
                if (take) throw new CommandException("Nie można odebrać punktów doświadczenia, odbierz poziomy (L)");
                p.AddXp(n);
                Notify(s, $"Dano {n} doświadczenia graczowi {p.Name}");
            }
        }, user: (a, i) => i == 1, aliases: "experience");

        Reg("achievement", 2, "/achievement <give|take> <nazwa|*> [gracz]", Achievement, user: (a, i) => i == 2,
            tab: (s, a) => a.Length == 1 ? new[] { "give", "take" } : a.Length == 2 ? AchievementList.All.Select(x => "achievement." + x.Id).Prepend("*") : a.Length == 3 ? PlayerNames(s.World) : Array.Empty<string>());

        // dodatki BlockCraft spoza 1.8
        Reg("heal", 2, "/heal [gracz]", (s, a) => { var p = a.Length > 0 ? GetPlayer(s, a[0]) : AsPlayer(s); p.Health = p.MaxHealth; Notify(s, "Uleczono " + p.Name); }, user: (a, i) => i == 0);
        Reg("feed", 2, "/feed [gracz]", (s, a) => { var p = a.Length > 0 ? GetPlayer(s, a[0]) : AsPlayer(s); p.FoodLevel = 20; p.Saturation = 20; Notify(s, "Najedzono " + p.Name); }, user: (a, i) => i == 0);
        Reg("locate", 2, "/locate <Stronghold|Village|Temple|Mineshaft|Monument|Fortress>", (s, a) =>
        {
            var p = AsPlayer(s);
            if (!OwLocators.Command(p.World, p, a.Prepend("locate").ToArray())) throw new CommandException("Nie znaleziono struktury");
        });
    }

    /// <summary>getCommandSenderAsPlayer: nadawca musi być graczem.</summary>
    public static Player AsPlayer(CommandSender s) => s.Entity as Player ?? throw new CommandException("Musisz podać nazwę gracza, gdy wykonujesz komendę z konsoli");

    private static void Broadcast(World.World w, string msg) { foreach (var p in AllPlayers(w)) p.SendMessage(msg); }

    private static void Help(CommandSender s, string[] a)
    {
        var list = Defs.Where(d => s.PermissionLevel >= d.Level).OrderBy(d => d.Name).ToList();
        int pages = (list.Count - 1) / 7;
        int page;
        try { page = a.Length == 0 ? 0 : Arg.Int(a[0], 1, pages + 1) - 1; }
        catch (CommandException)
        {
            if (Map.TryGetValue(a[0], out var d)) throw new UsageException(d.Usage);
            if (int.TryParse(a[0], out _)) throw;
            throw new CommandException("Nieznana komenda. Wpisz \"/help\", aby wyświetlić listę komend.");
        }
        s.Message($"§2--- Pomoc, strona {page + 1} z {pages + 1} (/help <strona>) ---");
        foreach (var d in list.Skip(page * 7).Take(7)) s.Message(d.Usage);
        if (page == 0 && s is PlayerSender) s.Message("§aWskazówka: Użyj klawisza <tab> w trakcie pisania komendy, aby uzupełnić komendę lub jej argumenty");
    }

    private static void Title(CommandSender s, string[] a)
    {
        const string u = "/title <gracz> <title|subtitle|clear|reset|times> ...";
        if (a.Length < 2) throw new UsageException(u);
        if (a.Length < 3 && a[1] is "title" or "subtitle" or "times") throw new UsageException(u);
        var p = GetPlayer(s, a[0]);
        int type = a[1].ToLowerInvariant() switch { "subtitle" => 1, "times" => 2, "clear" => 3, "reset" => 4, _ => 0 };
        if (type is 3 or 4)
        {
            if (a.Length != 2) throw new UsageException(u);
            SendTitle(p, type, "", -1, -1, -1);
        }
        else if (type == 2)
        {
            if (a.Length != 5) throw new UsageException(u);
            SendTitle(p, 2, "", Arg.Int(a[2]), Arg.Int(a[3]), Arg.Int(a[4]));
        }
        else
        {
            if (a.Length < 3) throw new UsageException(u);
            SendTitle(p, type, TextComponent.Render(Arg.Join(a, 2), s, p), -1, -1, -1);
        }
        Notify(s, "Polecenie title wykonane pomyślnie");
    }

    /// <summary>S45PacketTitle: lokalnie stan HUD gracza, gościowi pakiet S2C.CommandUi.</summary>
    public static void SendTitle(Player p, int action, string text, int fi, int st, int fo)
    {
        if (p.Handler is Net.RemoteClient rc) rc.SendTitle(action, text, fi, st, fo);
        else p.Title.Show(action, text, fi, st, fo);
    }

    // ---------- cząsteczki ----------
    /// <summary>EnumParticleTypes 1.8 i najbliższy rodzaj cząsteczki BlockCraft.</summary>
    private static readonly (string name, ParticleKind kind, bool args)[] ParticleNames =
    {
        ("explode", ParticleKind.Poof, false), ("largeexplode", ParticleKind.Explosion, false), ("hugeexplosion", ParticleKind.Explosion, false),
        ("fireworksSpark", ParticleKind.Crit, false), ("bubble", ParticleKind.Bubble, false), ("splash", ParticleKind.Splash, false), ("wake", ParticleKind.Splash, false),
        ("suspended", ParticleKind.Bubble, false), ("depthsuspend", ParticleKind.Smoke, false), ("crit", ParticleKind.Crit, false), ("magicCrit", ParticleKind.Crit, false),
        ("smoke", ParticleKind.Smoke, false), ("largesmoke", ParticleKind.LargeSmoke, false), ("spell", ParticleKind.Spell, false), ("instantSpell", ParticleKind.Spell, false),
        ("mobSpell", ParticleKind.Spell, false), ("mobSpellAmbient", ParticleKind.Spell, false), ("witchMagic", ParticleKind.Spell, false), ("dripWater", ParticleKind.Splash, false),
        ("dripLava", ParticleKind.Flame, false), ("angryVillager", ParticleKind.Angry, false), ("happyVillager", ParticleKind.Note, false), ("townaura", ParticleKind.Smoke, false),
        ("note", ParticleKind.Note, false), ("portal", ParticleKind.Portal, false), ("enchantmenttable", ParticleKind.Portal, false), ("flame", ParticleKind.Flame, false),
        ("lava", ParticleKind.Flame, false), ("footstep", ParticleKind.Poof, false), ("cloud", ParticleKind.Poof, false), ("reddust", ParticleKind.RedDust, false),
        ("snowballpoof", ParticleKind.Poof, false), ("snowshovel", ParticleKind.Poof, false), ("slime", ParticleKind.Splash, false), ("heart", ParticleKind.Heart, false),
        ("barrier", ParticleKind.Angry, false), ("droplet", ParticleKind.Splash, false), ("take", ParticleKind.Poof, false), ("mobappearance", ParticleKind.LargeSmoke, false),
        ("iconcrack_", ParticleKind.BlockBreak, true), ("blockcrack_", ParticleKind.BlockBreak, true), ("blockdust_", ParticleKind.BlockBreak, true),
    };

    private static void Particle(CommandSender s, string[] a)
    {
        if (a.Length < 8) throw new UsageException("/particle <nazwa> <x> <y> <z> <xd> <yd> <zd> <prędkość> [ilość] [tryb]");
        var def = ParticleNames.FirstOrDefault(p => p.args ? a[0].StartsWith(p.name) : a[0] == p.name);
        if (def.name == null) throw new CommandException($"Nieznana nazwa efektu ({a[0]})");
        var v = s.Pos;
        double x = Arg.Coord(v.X, a[1], true), y = Arg.Coord(v.Y, a[2], true), z = Arg.Coord(v.Z, a[3], true);
        double dx = Arg.Double(a[4]), dy = Arg.Double(a[5]), dz = Arg.Double(a[6]);
        Arg.Double(a[7], 0);
        int count = a.Length > 8 ? Arg.Int(a[8], 0) : 0;
        int data = 0;
        if (def.args)
        {
            var parts = a[0].Split('_', 3);
            int id = parts.Length > 1 && int.TryParse(parts[1], out int i1) ? i1 : 1;
            int meta = parts.Length > 2 && int.TryParse(parts[2], out int i2) ? i2 : 0;
            data = def.name == "iconcrack_" ? (Items.Item.ById.ElementAtOrDefault(id)?.IsBlock == true ? id | meta << 12 : 1) : id | meta << 12;
        }
        var r = s.World.Rand;
        int n = Math.Clamp(count, 1, 2000);
        for (int i = 0; i < n; i++)
        {
            double ox = count == 0 ? 0 : r.NextGaussian() * dx, oy = count == 0 ? 0 : r.NextGaussian() * dy, oz = count == 0 ? 0 : r.NextGaussian() * dz;
            s.World.SpawnParticles(def.kind, x + ox, y + oy, z + oz, 1, data);
        }
        Notify(s, $"Odtwarzanie efektu {a[0]} {Math.Max(count, 1)} razy");
    }

    private static void PlaySound(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/playsound <dźwięk> <gracz> [x] [y] [z] [głośność] [wysokość] [minimalna głośność]");
        int i = 0;
        string name = a[i++];
        var p = GetPlayer(s, a[i++]);
        var v = s.Pos;
        double x = v.X, y = v.Y, z = v.Z;
        if (a.Length > i) x = Arg.Coord(x, a[i++], true);
        if (a.Length > i) y = Arg.Coord(y, a[i++], false);
        if (a.Length > i) z = Arg.Coord(z, a[i++], true);
        double vol = a.Length > i ? Arg.Double(a[i++], 0, float.MaxValue) : 1;
        double pitch = a.Length > i ? Arg.Double(a[i++], 0, 2) : 1;
        double min = a.Length > i ? Arg.Double(a[i++], 0, 1) : 0;
        double range = vol > 1 ? vol * 16 : 16;
        double dist = Math.Sqrt(p.DistanceSq(x, y, z));
        if (dist > range)
        {
            if (min <= 0) throw new CommandException($"Gracz {p.Name} jest za daleko, by usłyszeć dźwięk");
            double ddx = x - p.X, ddy = y - p.Y, ddz = z - p.Z, d = Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
            if (d > 0) { x = p.X + ddx / d * 2; y = p.Y + ddy / d * 2; z = p.Z + ddz / d * 2; }
            vol = min;
        }
        if (p.Handler is Net.RemoteClient rc) rc.SendSound(name, x, y, z, (float)vol, (float)pitch);
        else if (p.Handler is Game g) g.Renderer?.PlaySound(name, x, y, z, (float)vol, (float)pitch);
        Notify(s, $"Odtworzono dźwięk '{name}' dla {p.Name}");
    }

    private static void Achievement(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/achievement <give|take> <nazwa|*> [gracz]");
        bool give = a[0].Equals("give", StringComparison.OrdinalIgnoreCase);
        if (!give && !a[0].Equals("take", StringComparison.OrdinalIgnoreCase)) throw new UsageException("/achievement <give|take> <nazwa|*> [gracz]");
        var p = a.Length > 2 ? GetPlayer(s, a[2]) : AsPlayer(s);
        if (a[1] == "*")
        {
            if (give)
            {
                foreach (var ach in AchievementList.All) p.Achievements.Add(ach.Id);
                Notify(s, $"Dano graczowi {p.Name} wszystkie osiągnięcia");
            }
            else { p.Achievements.Clear(); Notify(s, $"Odebrano graczowi {p.Name} wszystkie osiągnięcia"); }
            return;
        }
        string id = a[1].StartsWith("achievement.") ? a[1][12..] : a[1];
        var x = AchievementList.Find(id) ?? throw new CommandException($"Nieznane osiągnięcie lub statystyka '{a[1]}'");
        if (give)
        {
            if (p.Achievements.Contains(x.Id)) throw new CommandException($"Gracz {p.Name} ma już osiągnięcie {x.Title}");
            // MC: najpierw brakujący łańcuch rodziców
            var chain = new List<string>();
            for (var c = x; c != null && !p.Achievements.Contains(c.Id); c = AchievementList.Find(c.Parent)) chain.Add(c.Id);
            chain.Reverse();
            foreach (var c in chain) p.AwardAchievement(c);
            Notify(s, $"Dano graczowi {p.Name} osiągnięcie [{x.Title}]");
        }
        else
        {
            if (!p.Achievements.Contains(x.Id)) throw new CommandException($"Gracz {p.Name} nie ma osiągnięcia {x.Title}");
            // odebranie zabiera też osiągnięcia zależne
            foreach (var c in AchievementList.All)
                for (var q = c; q != null; q = AchievementList.Find(q.Parent)) if (q == x) { p.Achievements.Remove(c.Id); break; }
            Notify(s, $"Odebrano graczowi {p.Name} osiągnięcie [{x.Title}]");
        }
    }
}
