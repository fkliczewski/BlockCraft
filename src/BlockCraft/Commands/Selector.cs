using System.Text.RegularExpressions;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Cmd;

/// <summary>Nazwy encji z EntityList MC 1.8 (summon, type=, testfor) i ich klucze w BlockCraft.</summary>
public static class EntityNames
{
    public static readonly Dictionary<string, string> McToKey = new()
    {
        ["Item"] = "item", ["XPOrb"] = "xp_orb", ["LeashKnot"] = "leash_knot", ["Painting"] = "painting", ["Arrow"] = "arrow",
        ["Snowball"] = "snowball", ["Fireball"] = "fireball", ["SmallFireball"] = "small_fireball", ["ThrownEnderpearl"] = "thrown",
        ["EyeOfEnderSignal"] = "eye_of_ender_signal", ["ThrownPotion"] = "me_potion", ["ThrownExpBottle"] = "thrown", ["ItemFrame"] = "item_frame",
        ["WitherSkull"] = "wither_skull", ["PrimedTnt"] = "tnt", ["FallingSand"] = "falling_block", ["FireworksRocketEntity"] = "fireworks_rocket",
        ["ArmorStand"] = "armor_stand", ["Boat"] = "boat", ["MinecartRideable"] = "minecart", ["MinecartChest"] = "chest_minecart",
        ["MinecartFurnace"] = "furnace_minecart", ["MinecartTNT"] = "tnt_minecart", ["MinecartHopper"] = "hopper_minecart",
        ["MinecartCommandBlock"] = "command_block_minecart", ["Creeper"] = "creeper", ["Skeleton"] = "skeleton", ["Spider"] = "spider",
        ["Giant"] = "giant", ["Zombie"] = "zombie", ["Slime"] = "slime", ["Ghast"] = "ghast", ["PigZombie"] = "zombie_pigman",
        ["Enderman"] = "enderman", ["CaveSpider"] = "cave_spider", ["Silverfish"] = "silverfish", ["Blaze"] = "blaze", ["LavaSlime"] = "magma_cube",
        ["EnderDragon"] = "ender_dragon", ["WitherBoss"] = "wither", ["Bat"] = "bat", ["Witch"] = "witch", ["Endermite"] = "endermite",
        ["Guardian"] = "guardian", ["Pig"] = "pig", ["Sheep"] = "sheep", ["Cow"] = "cow", ["Chicken"] = "chicken", ["Squid"] = "squid",
        ["Wolf"] = "wolf", ["MushroomCow"] = "mooshroom", ["SnowMan"] = "snow_golem", ["Ozelot"] = "ocelot", ["VillagerGolem"] = "iron_golem",
        ["EntityHorse"] = "horse", ["Rabbit"] = "rabbit", ["Villager"] = "villager", ["EnderCrystal"] = "ender_crystal", ["LightningBolt"] = "lightning_bolt",
    };

    private static readonly Dictionary<string, string> Variants = new()
    {
        ["wither_skeleton"] = "Skeleton", ["baby_zombie"] = "Zombie", ["zombie_villager"] = "Zombie", ["elder_guardian"] = "Guardian",
        ["donkey"] = "EntityHorse", ["mule"] = "EntityHorse", ["zombie_horse"] = "EntityHorse", ["skeleton_horse"] = "EntityHorse",
        ["killer_bunny"] = "Rabbit", ["chicken_jockey"] = "Chicken", ["spider_jockey"] = "Spider", ["golem_snowball"] = "Snowball",
        ["witch_potion"] = "ThrownPotion", ["lightningbolt"] = "LightningBolt",
    };

    /// <summary>EntityList.getEntityString: nazwa MC encji (gracz: "Player").</summary>
    public static string McName(Entity e)
    {
        if (e is Player) return "Player";
        string k = e.TypeKey;
        if (Variants.TryGetValue(k, out var v)) return v;
        foreach (var kv in McToKey) if (kv.Value == k) return kv.Key;
        return k;
    }

    /// <summary>Czy nazwa jest znaną nazwą encji (dopuszcza też klucze BlockCraft).</summary>
    public static bool IsValid(string name) => name == "Player" || McToKey.ContainsKey(name) || EntitySerializer.Types.ContainsKey(name);

    public static bool Is(Entity e, string name) => McName(e) == name || e.TypeKey == name;

    /// <summary>Klucz rejestru dla nazwy z /summon (nazwa MC albo klucz BlockCraft).</summary>
    public static string KeyOf(string name)
    {
        if (McToKey.TryGetValue(name, out var k)) return k;
        string low = name.ToLowerInvariant();
        if (low.StartsWith("minecraft:")) low = low[10..];
        if (EntitySerializer.Types.ContainsKey(low)) return low;
        foreach (var kv in McToKey) if (kv.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) return kv.Value;
        return null;
    }

    public static IEnumerable<string> All => McToKey.Keys.Where(k => EntitySerializer.Types.ContainsKey(McToKey[k]));
}

/// <summary>PlayerSelector z MC 1.8: @p @a @r @e z argumentami x,y,z,r,rm,dx,dy,dz,m,c,l,lm,team,name,type,score_X(_min),rx,rxm,ry,rym.</summary>
public static class Selector
{
    private static readonly Regex Token = new(@"^@([pare])(?:\[([A-Za-z0-9_=,!-]*)\])?$", RegexOptions.CultureInvariant);
    private static readonly Regex IntList = new(@"\G([-!]?[A-Za-z0-9_-]*)(?:$|,)", RegexOptions.CultureInvariant);
    private static readonly Regex KeyValue = new(@"\G([A-Za-z0-9_]+)=([-!]?[A-Za-z0-9_-]*)(?:$|,)", RegexOptions.CultureInvariant);
    private static readonly Regex AnyToken = new(@"^@[pare](?:\[[A-Za-z0-9_=,!-]*\])?", RegexOptions.CultureInvariant);

    public static bool IsSelector(string s) => s != null && Token.IsMatch(s);
    /// <summary>hasArguments: czy to selektor (z argumentami lub bez), używane w tekstach /say /tell.</summary>
    public static bool StartsSelector(string s) => s != null && AnyToken.IsMatch(s);

    /// <summary>getArgumentMap: najpierw lista pozycyjna x,y,z,r, potem pary klucz=wartość.</summary>
    public static Dictionary<string, string> Args(string s)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(s)) return map;
        int i = 0, j = -1;
        foreach (Match m in IntList.Matches(s))
        {
            string key = i++ switch { 0 => "x", 1 => "y", 2 => "z", 3 => "r", _ => null };
            if (key != null && m.Groups[1].Length > 0) map[key] = m.Groups[1].Value;
            j = m.Index + m.Length;
        }
        if (j < s.Length)
            foreach (Match m in KeyValue.Matches(j == -1 ? s : s[j..])) map[m.Groups[1].Value] = m.Groups[2].Value;
        return map;
    }

    private static int IntArg(Dictionary<string, string> m, string k, int def) => m.TryGetValue(k, out var v) && int.TryParse(v, out int r) ? r : def;
    private static string StrArg(Dictionary<string, string> m, string k) => m.TryGetValue(k, out var v) ? v : null;

    public static bool MatchesMultiple(string token)
    {
        var m = Token.Match(token ?? "");
        if (!m.Success) return false;
        var map = Args(m.Groups[2].Value);
        string t = m.Groups[1].Value;
        return IntArg(map, "c", t is "a" or "e" ? 0 : 1) != 1;
    }

    /// <summary>Uniwersalny UUID encji (z SaveId), używany przez rozwijanie selektorów i scoreboard.</summary>
    public static string Uuid(Entity e) => Guid.TryParseExact(e.SaveId, "N", out var g) ? g.ToString("D") : e.SaveId;

    public static List<Entity> Match(CommandSender sender, string token)
    {
        var res = new List<Entity>();
        var m = Token.Match(token ?? "");
        if (!m.Success || sender.PermissionLevel < 1) return res;
        var map = Args(m.Groups[2].Value);
        string t = m.Groups[1].Value;
        string type = StrArg(map, "type");
        bool typeNeg = type != null && type.StartsWith('!');
        if (typeNeg) type = type[1..];
        if (type != null && !EntityNames.IsValid(type))
        {
            sender.Message($"§cTyp encji '{type}' jest nieprawidłowy");
            return res;
        }
        var sp = sender.BlockPos;
        int px = IntArg(map, "x", sp.X), py = IntArg(map, "y", sp.Y), pz = IntArg(map, "z", sp.Z);
        bool local = new[] { "x", "y", "z", "dx", "dy", "dz", "r", "rm" }.Any(map.ContainsKey);
        var worlds = local ? new List<World.World> { sender.World } : Commands.Worlds(sender.World).ToList();

        var preds = new List<Func<Entity, bool>>();
        // typ (func_179663_a)
        bool onlyPlayers = t != "e";
        bool rWithType = t == "r" && type != null;
        if ((type == null || t != "e") && !rWithType) { if (onlyPlayers) preds.Add(e => e is Player); }
        else { string ty = type; bool neg = typeNeg; preds.Add(e => EntityNames.Is(e, ty) != neg); }
        // poziom
        int lm = IntArg(map, "lm", -1), l = IntArg(map, "l", -1);
        if (lm > -1 || l > -1) preds.Add(e => e is Player p && (lm <= -1 || p.XpLevel >= lm) && (l <= -1 || p.XpLevel <= l));
        // tryb gry
        int gm = IntArg(map, "m", -1);
        if (gm != -1) preds.Add(e => e is Player p && p.GameMode == gm);
        // drużyna
        string team = StrArg(map, "team");
        if (team != null)
        {
            bool neg = team.StartsWith('!'); if (neg) team = team[1..];
            string tn = team;
            preds.Add(e => e is LivingEntity && ((Scoreboard.Of(sender.World).TeamOf(Commands.ScoreName(e))?.Name ?? "") == tn) != neg);
        }
        // wyniki
        var scores = map.Where(kv => kv.Key.StartsWith("score_") && kv.Key.Length > 6).ToList();
        if (scores.Count > 0)
        {
            var sb = Scoreboard.Of(sender.World);
            preds.Add(e =>
            {
                foreach (var kv in scores)
                {
                    string s = kv.Key[6..]; bool min = false;
                    if (s.EndsWith("_min") && s.Length > 4) { min = true; s = s[..^4]; }
                    if (!int.TryParse(kv.Value, out int val)) continue;
                    var obj = sb.GetObjective(s);
                    if (obj == null) return false;
                    var sc = sb.GetScore(Commands.ScoreName(e), obj, false);
                    if (sc == null) return false;
                    if (sc.Points < val && min) return false;
                    if (sc.Points > val && !min) return false;
                }
                return true;
            });
        }
        // nazwa
        string name = StrArg(map, "name");
        if (name != null)
        {
            bool neg = name.StartsWith('!'); if (neg) name = name[1..];
            string nm = name;
            preds.Add(e => (Commands.EntityName(e) == nm) != neg);
        }
        // promień (func_180698_a): (int) kwadrat odległości do środka bloku
        int rm = IntArg(map, "rm", -1), r = IntArg(map, "r", -1);
        if (rm >= 0 || r >= 0)
        {
            long rm2 = (long)rm * rm, r2 = (long)r * r;
            preds.Add(e =>
            {
                double dx = e.X - (px + .5), dy = e.Y - (py + .5), dz = e.Z - (pz + .5);
                long d = (long)(dx * dx + dy * dy + dz * dz);
                return (rm < 0 || d >= rm2) && (r < 0 || d <= r2);
            });
        }
        // obrót
        if (map.ContainsKey("rym") || map.ContainsKey("ry"))
        {
            int a = WrapRot(IntArg(map, "rym", 0)), b = WrapRot(IntArg(map, "ry", 359));
            preds.Add(e => { int y = WrapRot(MathUtil.FloorToInt(e.Yaw)); return a > b ? y >= a || y <= b : y >= a && y <= b; });
        }
        if (map.ContainsKey("rxm") || map.ContainsKey("rx"))
        {
            int a = WrapRot(IntArg(map, "rxm", 0)), b = WrapRot(IntArg(map, "rx", 359));
            preds.Add(e => { int x = WrapRot(MathUtil.FloorToInt(e.Pitch)); return a > b ? x >= a || x <= b : x >= a && x <= b; });
        }

        bool All(Entity e) { foreach (var p in preds) if (!p(e)) return false; return true; }
        bool Alive(Entity e) => !e.Removed && !e.Dead && (e is not LivingEntity le || le.Health > 0);

        bool hasD = map.ContainsKey("dx") || map.ContainsKey("dy") || map.ContainsKey("dz");
        int ddx = IntArg(map, "dx", 0), ddy = IntArg(map, "dy", 0), ddz = IntArg(map, "dz", 0);
        foreach (var w in worlds)
        {
            if (w == null) continue;
            IEnumerable<Entity> src;
            if (!hasD)
            {
                if (r >= 0)
                {
                    if (onlyPlayers && !rWithType) src = w.Players.Where(e => Alive(e) && All(e));
                    else
                    {
                        var box = new AABB(px - r, py - r, pz - r, px + r + 1, py + r + 1, pz + r + 1);
                        src = w.Entities.Where(e => Alive(e) && e.BoundingBox.Intersects(box) && All(e));
                    }
                }
                else if (t == "a") src = w.Players.Where(All);
                else if (t != "p" && (t != "r" || rWithType)) src = w.Entities.Where(e => Alive(e) && All(e));
                else src = w.Players.Where(e => Alive(e) && All(e));
            }
            else
            {
                int x0 = px + (ddx < 0 ? ddx : 0), y0 = py + (ddy < 0 ? ddy : 0), z0 = pz + (ddz < 0 ? ddz : 0);
                int x1 = px + (ddx < 0 ? 0 : ddx) + 1, y1 = py + (ddy < 0 ? 0 : ddy) + 1, z1 = pz + (ddz < 0 ? 0 : ddz) + 1;
                var box = new AABB(x0, y0, z0, x1, y1, z1);
                if (onlyPlayers && !rWithType)
                    src = w.Players.Where(e => Alive(e) && All(e) && e.X >= x0 && e.Y >= y0 && e.Z >= z0 && e.X < x1 && e.Y < y1 && e.Z < z1);
                else src = w.Entities.Where(e => Alive(e) && e.BoundingBox.Intersects(box) && All(e));
            }
            res.AddRange(src.Distinct());
        }

        // sortowanie i limit (func_179658_a)
        int c = IntArg(map, "c", t is "a" or "e" ? 0 : 1);
        if (t == "r") Shuffle(res, sender.World.Rand);
        else res = res.OrderBy(e => { double dx = e.X - px, dy = e.Y - py, dz = e.Z - pz; return dx * dx + dy * dy + dz * dz; }).ToList();
        var self = sender.Entity;
        if (self != null && c == 1 && res.Contains(self) && t != "r") res = new List<Entity> { self };
        if (c != 0)
        {
            if (c < 0) res.Reverse();
            res = res.Take(Math.Abs(c)).ToList();
        }
        return res;
    }

    private static void Shuffle(List<Entity> l, Rng r)
    {
        for (int i = l.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
    }

    /// <summary>func_179650_a z PlayerSelector 1.8 (z jego osobliwym progiem 160).</summary>
    public static int WrapRot(int v)
    {
        v %= 360;
        if (v >= 160) v -= 360;
        if (v < 0) v += 360;
        return v;
    }

    public static Player MatchOnePlayer(CommandSender s, string token)
    {
        var l = Match(s, token).OfType<Player>().ToList();
        return l.Count == 1 ? l[0] : null;
    }
    public static Entity MatchOne(CommandSender s, string token)
    {
        var l = Match(s, token);
        return l.Count == 1 ? l[0] : null;
    }

    /// <summary>matchEntitiesToString: nazwy wybranych encji ("A, B i C") albo null.</summary>
    public static string MatchToString(CommandSender s, string token)
    {
        var l = Match(s, token);
        return l.Count == 0 ? null : Arg.Nice(l.Select(Commands.EntityDisplayName));
    }
}
