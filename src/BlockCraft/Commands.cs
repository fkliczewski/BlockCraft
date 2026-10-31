using System.Globalization;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Komendy czatu jak w Minecraft.</summary>
public sealed class Commands
{
    private readonly World.World _w;
    public Commands(World.World w) { _w = w; }

    public static readonly string[] Names =
    {
        "help", "time", "gamemode", "tp", "teleport", "give", "kill", "summon", "setblock", "fill", "difficulty", "gamerule", "clear",
        "seed", "spawnpoint", "setworldspawn", "say", "me", "xp", "list", "weather", "heal", "feed", "effect", "locate"
    };

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public bool Execute(Player p, string line)
    {
        line = line.Trim();
        if (line.StartsWith("/")) line = line[1..];
        if (line.Length == 0) return false;
        var a = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string cmd = a[0].ToLowerInvariant();
        bool needsCheats = cmd is not ("help" or "me" or "say" or "list" or "seed");
        if (needsCheats && !_w.AllowCheats && !p.IsLocalOp)
        {
            p.SendMessage("§cNie masz uprawnień do użycia tej komendy (komendy wyłączone).");
            return false;
        }
        try
        {
            switch (cmd)
            {
                case "help": Help(p); return true;
                case "time": return Time(p, a);
                case "gamemode": case "gm": return GameMode(p, a);
                case "tp": case "teleport": return Tp(p, a);
                case "give": return Give(p, a);
                case "kill": return Kill(p, a);
                case "summon": return Summon(p, a);
                case "setblock": return SetBlock(p, a);
                case "fill": return Fill(p, a);
                case "difficulty": return DifficultyCmd(p, a);
                case "gamerule": return GameRule(p, a);
                case "clear": return Clear(p, a);
                case "seed": p.SendMessage("Ziarno: [§a" + _w.Seed + "§r]"); return true;
                case "spawnpoint": return SpawnPoint(p, a);
                case "setworldspawn":
                    _w.SpawnX = MathUtil.FloorToInt(p.X); _w.SpawnY = MathUtil.FloorToInt(p.Y); _w.SpawnZ = MathUtil.FloorToInt(p.Z);
                    p.SendMessage($"Ustawiono punkt odrodzenia świata na {_w.SpawnX}, {_w.SpawnY}, {_w.SpawnZ}"); return true;
                case "say": _w.BroadcastMessage($"§d[{p.Name}] " + string.Join(' ', a.Skip(1))); return true;
                case "me": _w.BroadcastMessage($"* {p.Name} " + string.Join(' ', a.Skip(1))); return true;
                case "xp": return Xp(p, a);
                case "list": p.SendMessage($"Graczy online: {_w.Players.Count}: " + string.Join(", ", _w.Players.Select(x => x.Name))); return true;
                case "weather": p.SendMessage("W tym świecie zawsze świeci słońce :) (pogoda nieobsługiwana)"); return true;
                case "heal": p.Health = p.MaxHealth; p.SendMessage("Uleczono."); return true;
                case "feed": p.FoodLevel = 20; p.Saturation = 20; p.SendMessage("Najedzono."); return true;
                case "locate": p.SendMessage("Brak struktur do zlokalizowania."); return true;
                default: p.SendMessage("§cNieznana komenda. Wpisz /help aby zobaczyć listę komend."); return false;
            }
        }
        catch (Exception ex)
        {
            p.SendMessage("§cBłąd składni: " + ex.Message);
            return false;
        }
    }

    private void Help(Player p)
    {
        p.SendMessage("§e--- Komendy ---");
        p.SendMessage("/time set <day|night|noon|midnight|liczba>, /time add <n>");
        p.SendMessage("/gamemode <survival|creative|spectator|0|1|3>");
        p.SendMessage("/tp [gracz] <x> <y> <z>  (~ = względnie)");
        p.SendMessage("/give <gracz> <przedmiot> [ilość] [meta]");
        p.SendMessage("/summon <zombie|skeleton|creeper|spider|enderman|cow|pig|sheep|chicken> [x y z]");
        p.SendMessage("/setblock, /fill, /kill [@e|@a|gracz], /clear, /xp <n>[L]");
        p.SendMessage("/difficulty <peaceful|easy|normal|hard>, /gamerule <reguła> <wartość>");
        p.SendMessage("/seed, /spawnpoint, /setworldspawn, /say, /me, /list, /heal, /feed");
    }

    private List<Player> Players(Player self, string sel)
    {
        if (sel == "@p" || sel == "@s") return new() { self };
        if (sel == "@a") return _w.Players.ToList();
        if (sel == "@r") return _w.Players.Count > 0 ? new() { _w.Players[_w.Rand.Next(_w.Players.Count)] } : new();
        var pl = _w.Players.FirstOrDefault(x => x.Name.Equals(sel, StringComparison.OrdinalIgnoreCase));
        return pl != null ? new() { pl } : new();
    }

    private bool Time(Player p, string[] a)
    {
        if (a.Length < 2) throw new Exception("/time <set|add|query> <wartość>");
        string sub = a[1].ToLowerInvariant();
        if (sub == "query") { p.SendMessage($"Czas dnia: {_w.DayTime % 24000}, dzień {_w.DayTime / 24000}"); return true; }
        if (a.Length < 3) throw new Exception("/time set <wartość>");
        long v = a[2].ToLowerInvariant() switch
        {
            "day" or "dzien" or "dzień" => 1000, "night" or "noc" => 13000, "noon" or "poludnie" or "południe" => 6000, "midnight" or "polnoc" or "północ" => 18000,
            "sunrise" => 23000, "sunset" => 12000,
            _ => long.Parse(a[2], Inv)
        };
        if (sub == "set") { _w.DayTime = (_w.DayTime / 24000) * 24000 + v; p.SendMessage("Ustawiono czas na " + v); }
        else if (sub == "add") { _w.DayTime += v; p.SendMessage("Dodano do czasu " + v); }
        else throw new Exception("/time <set|add|query>");
        return true;
    }

    private bool GameMode(Player p, string[] a)
    {
        if (a.Length < 2) throw new Exception("/gamemode <tryb> [gracz]");
        int gm = a[1].ToLowerInvariant() switch
        {
            "0" or "s" or "survival" or "przetrwanie" => 0,
            "1" or "c" or "creative" or "kreatywny" => 1,
            "2" or "a" or "adventure" => 0,
            "3" or "sp" or "spectator" or "obserwator" => 3,
            _ => throw new Exception("Nieznany tryb gry: " + a[1])
        };
        var targets = a.Length > 2 ? Players(p, a[2]) : new() { p };
        foreach (var t in targets)
        {
            t.SetGameMode(gm);
            t.NetGameModeHook?.Invoke();
            string name = gm switch { 0 => "Przetrwanie", 1 => "Kreatywny", _ => "Obserwator" };
            t.SendMessage("Twój tryb gry został zmieniony na: " + name);
        }
        return true;
    }

    private static double Coord(string s, double cur)
    {
        if (s.StartsWith("~")) return cur + (s.Length > 1 ? double.Parse(s[1..], Inv) : 0);
        double v = double.Parse(s, Inv);
        return v;
    }

    private bool Tp(Player p, string[] a)
    {
        var target = p;
        int i = 1;
        if (a.Length == 2 || a.Length == 5)
        {
            var t = Players(p, a[1]);
            if (t.Count == 0) throw new Exception("Nie znaleziono gracza " + a[1]);
            if (a.Length == 2) { p.SetPositionAndPrev(t[0].X, t[0].Y, t[0].Z); p.NetTeleportHook?.Invoke(); p.SendMessage("Przeteleportowano do " + t[0].Name); return true; }
            target = t[0]; i = 2;
        }
        if (a.Length - i < 3) throw new Exception("/tp [gracz] <x> <y> <z>");
        double x = Coord(a[i], target.X), y = Coord(a[i + 1], target.Y), z = Coord(a[i + 2], target.Z);
        if (!a[i].StartsWith("~") && !a[i].Contains('.')) x += 0.5;
        if (!a[i + 2].StartsWith("~") && !a[i + 2].Contains('.')) z += 0.5;
        target.SetPositionAndPrev(x, y, z);
        target.MotionX = target.MotionY = target.MotionZ = 0; target.FallDistance = 0;
        target.NetTeleportHook?.Invoke();
        p.SendMessage($"Przeteleportowano {target.Name} na {x:F1}, {y:F1}, {z:F1}");
        return true;
    }

    private bool Give(Player p, string[] a)
    {
        if (a.Length < 3) throw new Exception("/give <gracz> <przedmiot> [ilość] [meta]");
        var targets = Players(p, a[1]);
        if (targets.Count == 0) throw new Exception("Nie znaleziono gracza " + a[1]);
        var it = Item.Get(a[2]) ?? throw new Exception("Nieznany przedmiot: " + a[2]);
        int count = a.Length > 3 ? int.Parse(a[3], Inv) : 1;
        int meta = a.Length > 4 ? int.Parse(a[4], Inv) : 0;
        count = Math.Clamp(count, 1, 64 * 36);
        foreach (var t in targets)
        {
            int left = count;
            while (left > 0)
            {
                int n = Math.Min(left, it.MaxStack);
                var s = new ItemStack(it.Id, n, meta);
                if (!t.Inventory.Add(s)) _w.DropItem(t.X, t.Y + 0.5, t.Z, s, false);
                left -= n;
            }
            t.Inventory.Dirty = true;
            p.SendMessage($"Dano {count} x [{it.DisplayName(meta)}] graczowi {t.Name}");
        }
        return true;
    }

    private bool Kill(Player p, string[] a)
    {
        if (a.Length > 1 && a[1] == "@e")
        {
            int n = 0;
            foreach (var e in _w.Entities.ToList()) if (e is not Player) { e.Kill(); n++; }
            p.SendMessage($"Zabito {n} encji");
            return true;
        }
        var targets = a.Length > 1 ? Players(p, a[1]) : new() { p };
        foreach (var t in targets)
        {
            t.Health = 0;
            t.OnDeath(new DamageSource("kill") { BypassArmor = true });
        }
        return true;
    }

    private bool Summon(Player p, string[] a)
    {
        if (a.Length < 2) throw new Exception("/summon <mob> [x y z]");
        string key = a[1].ToLowerInvariant().Replace("minecraft:", "");
        double x = p.X, y = p.Y, z = p.Z;
        if (a.Length >= 5) { x = Coord(a[2], p.X); y = Coord(a[3], p.Y); z = Coord(a[4], p.Z); }
        Entity e = MobSpawner.Create(key, _w);
        if (e == null && (key == "tnt" || key == "primedtnt")) e = new TntEntity(_w, x, y, z, 80);
        if (e == null) throw new Exception("Nieznana encja: " + a[1]);
        e.SetPositionAndPrev(x, y, z);
        _w.SpawnEntity(e);
        p.SendMessage("Przywołano: " + (e is LivingEntity le ? le.DisplayName : key));
        return true;
    }

    private static (byte id, int meta) ParseBlock(string s, string metaStr)
    {
        var it = Item.Get(s);
        if (it == null || !it.IsBlock) throw new Exception("Nieznany blok: " + s);
        return ((byte)it.BlockId, metaStr != null ? int.Parse(metaStr, Inv) : 0);
    }

    private bool SetBlock(Player p, string[] a)
    {
        if (a.Length < 5) throw new Exception("/setblock <x> <y> <z> <blok> [meta]");
        int x = MathUtil.FloorToInt(Coord(a[1], p.X)), y = MathUtil.FloorToInt(Coord(a[2], p.Y)), z = MathUtil.FloorToInt(Coord(a[3], p.Z));
        var (id, meta) = a[4] == "air" ? ((byte)0, 0) : ParseBlock(a[4], a.Length > 5 ? a[5] : null);
        _w.SetBlock(x, y, z, id, meta);
        p.SendMessage("Postawiono blok.");
        return true;
    }

    private bool Fill(Player p, string[] a)
    {
        if (a.Length < 8) throw new Exception("/fill <x1> <y1> <z1> <x2> <y2> <z2> <blok> [meta]");
        int x1 = MathUtil.FloorToInt(Coord(a[1], p.X)), y1 = MathUtil.FloorToInt(Coord(a[2], p.Y)), z1 = MathUtil.FloorToInt(Coord(a[3], p.Z));
        int x2 = MathUtil.FloorToInt(Coord(a[4], p.X)), y2 = MathUtil.FloorToInt(Coord(a[5], p.Y)), z2 = MathUtil.FloorToInt(Coord(a[6], p.Z));
        var (id, meta) = a[7] == "air" ? ((byte)0, 0) : ParseBlock(a[7], a.Length > 8 ? a[8] : null);
        long vol = (long)(Math.Abs(x2 - x1) + 1) * (Math.Abs(y2 - y1) + 1) * (Math.Abs(z2 - z1) + 1);
        if (vol > 32768) throw new Exception($"Za duży obszar ({vol} > 32768)");
        int n = 0;
        for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
            for (int y = Math.Max(0, Math.Min(y1, y2)); y <= Math.Min(Chunk.Height - 1, Math.Max(y1, y2)); y++)
                for (int z = Math.Min(z1, z2); z <= Math.Max(z1, z2); z++)
                    if (_w.SetBlock(x, y, z, id, meta)) n++;
        p.SendMessage($"Wypełniono {n} bloków");
        return true;
    }

    private bool DifficultyCmd(Player p, string[] a)
    {
        if (a.Length < 2) { p.SendMessage("Poziom trudności: " + _w.Difficulty); return true; }
        _w.Difficulty = a[1].ToLowerInvariant() switch
        {
            "0" or "peaceful" or "p" or "pokojowy" => Difficulty.Peaceful,
            "1" or "easy" or "e" or "latwy" or "łatwy" => Difficulty.Easy,
            "2" or "normal" or "n" or "normalny" => Difficulty.Normal,
            "3" or "hard" or "h" or "trudny" => Difficulty.Hard,
            _ => throw new Exception("Nieznany poziom: " + a[1])
        };
        p.SendMessage("Ustawiono poziom trudności na " + _w.Difficulty);
        return true;
    }

    private bool GameRule(Player p, string[] a)
    {
        if (a.Length < 2) { p.SendMessage("Reguły: " + string.Join(", ", GameRules.Names)); return true; }
        if (a.Length < 3)
        {
            var r = _w.Rules;
            bool? v = a[1].ToLowerInvariant() switch
            {
                "dodaylightcycle" => r.DoDaylightCycle, "keepinventory" => r.KeepInventory, "domobspawning" => r.DoMobSpawning,
                "mobgriefing" => r.MobGriefing, "naturalregeneration" => r.NaturalRegeneration, "doentitydrops" => r.DoEntityDrops, _ => null
            };
            p.SendMessage(v == null ? "Nieznana reguła" : $"{a[1]} = {v.Value.ToString().ToLowerInvariant()}");
            return true;
        }
        if (!_w.Rules.Set(a[1], a[2], out var err)) throw new Exception(err);
        p.SendMessage($"Reguła {a[1]} zmieniona na {a[2]}");
        return true;
    }

    private bool Clear(Player p, string[] a)
    {
        var targets = a.Length > 1 ? Players(p, a[1]) : new() { p };
        int? id = a.Length > 2 ? Item.Get(a[2])?.Id : null;
        foreach (var t in targets)
        {
            int n = 0;
            for (int i = 0; i < 40; i++)
            {
                var s = t.Inventory.Get(i);
                if (s != null && (id == null || s.Id == id)) { n += s.Count; t.Inventory.Set(i, null); }
            }
            p.SendMessage($"Wyczyszczono ekwipunek gracza {t.Name}, usunięto {n} przedmiotów");
        }
        return true;
    }

    private bool SpawnPoint(Player p, string[] a)
    {
        var t = a.Length > 1 ? Players(p, a[1]).FirstOrDefault() ?? p : p;
        int x = MathUtil.FloorToInt(t.X), y = MathUtil.FloorToInt(t.Y), z = MathUtil.FloorToInt(t.Z);
        if (a.Length >= 5) { x = MathUtil.FloorToInt(Coord(a[2], t.X)); y = MathUtil.FloorToInt(Coord(a[3], t.Y)); z = MathUtil.FloorToInt(Coord(a[4], t.Z)); }
        t.SpawnX = x; t.SpawnY = y; t.SpawnZ = z; t.HasSpawn = true;
        p.SendMessage($"Ustawiono punkt odrodzenia gracza {t.Name} na {x}, {y}, {z}");
        return true;
    }

    private bool Xp(Player p, string[] a)
    {
        if (a.Length < 2) throw new Exception("/xp <ilość>[L] [gracz]");
        var t = a.Length > 2 ? Players(p, a[2]).FirstOrDefault() ?? p : p;
        string v = a[1];
        if (v.EndsWith("L", StringComparison.OrdinalIgnoreCase))
        {
            int lv = int.Parse(v[..^1], Inv);
            t.XpLevel = Math.Max(0, t.XpLevel + lv); if (t.XpLevel == 0) t.XpProgress = 0;
            p.SendMessage($"Dano {lv} poziomów graczowi {t.Name}");
        }
        else { int n = int.Parse(v, Inv); t.AddXp(n); p.SendMessage($"Dano {n} doświadczenia graczowi {t.Name}"); }
        return true;
    }

    /// <summary>Podpowiedzi dla TAB.</summary>
    public static List<string> Complete(string text, World.World w)
    {
        var res = new List<string>();
        if (!text.StartsWith("/")) return res;
        var parts = text[1..].Split(' ');
        if (parts.Length == 1)
        {
            foreach (var n in Names) if (n.StartsWith(parts[0], StringComparison.OrdinalIgnoreCase)) res.Add("/" + n);
            return res;
        }
        string cmd = parts[0].ToLowerInvariant(); string last = parts[^1].ToLowerInvariant();
        IEnumerable<string> opts = cmd switch
        {
            "time" when parts.Length == 2 => new[] { "set", "add", "query" },
            "time" when parts.Length == 3 => new[] { "day", "night", "noon", "midnight" },
            "gamemode" when parts.Length == 2 => new[] { "survival", "creative", "spectator" },
            "difficulty" when parts.Length == 2 => new[] { "peaceful", "easy", "normal", "hard" },
            "summon" when parts.Length == 2 => MobSpawner.MobKeys,
            "give" when parts.Length == 2 => new[] { "@p", "@a" }.Concat(w.Players.Select(x => x.Name)),
            "give" when parts.Length == 3 => Item.All.Select(i => i.Key),
            "setblock" when parts.Length == 5 => Item.All.Where(i => i.IsBlock).Select(i => i.Key),
            "fill" when parts.Length == 8 => Item.All.Where(i => i.IsBlock).Select(i => i.Key),
            "gamerule" when parts.Length == 2 => GameRules.Names,
            "gamerule" when parts.Length == 3 => new[] { "true", "false" },
            "kill" when parts.Length == 2 => new[] { "@e", "@a", "@p" },
            "tp" when parts.Length == 2 => w.Players.Select(x => x.Name),
            _ => Array.Empty<string>()
        };
        string prefix = string.Join(' ', parts.Take(parts.Length - 1)) + " ";
        foreach (var o in opts) if (o.StartsWith(last, StringComparison.OrdinalIgnoreCase)) res.Add("/" + prefix + o);
        return res;
    }
}
