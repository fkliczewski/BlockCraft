using System.Globalization;
using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Commands
{
    /// <summary>Double.toString z Javy (np. 20.0, 10.5) do komunikatów.</summary>
    public static string JD(double v) => v == Math.Floor(v) && Math.Abs(v) < 1e7 ? v.ToString("0.0", CultureInfo.InvariantCulture) : v.ToString("0.0###############", CultureInfo.InvariantCulture);

    private static readonly string[] EffectKeys = { "", "speed", "slowness", "haste", "mining_fatigue", "strength", "instant_health", "instant_damage", "jump_boost",
        "nausea", "regeneration", "resistance", "fire_resistance", "water_breathing", "invisibility", "blindness", "night_vision", "hunger", "weakness", "poison",
        "wither", "health_boost", "absorption", "saturation" };

    /// <summary>Nazwa zaklęcia jak ResourceLocation 1.8 (np. fire_aspect, luck_of_the_sea).</summary>
    public static string EnchKey(Ench e) => string.Concat(e.ToString().Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private static void RegisterEntities()
    {
        Reg("tp", 2, "/tp [cel] <gracz> LUB /tp [cel] <x> <y> <z> [<obrót-y> <obrót-x>]", Tp, user: (a, i) => i == 0, tab: (s, a) => a.Length <= 2 ? PlayerNames(s.World) : Array.Empty<string>(), aliases: "teleport");
        Reg("give", 2, "/give <gracz> <przedmiot> [ilość] [dane] [tag danych]", Give, user: (a, i) => i == 0,
            tab: (s, a) => a.Length == 1 ? PlayerNames(s.World).Concat(new[] { "@p", "@a", "@r" }) : a.Length == 2 ? ItemKeys : Array.Empty<string>());
        Reg("clear", 2, "/clear [gracz] [przedmiot] [dane] [maks. ilość] [tag danych]", Clear, user: (a, i) => i == 0,
            tab: (s, a) => a.Length == 1 ? PlayerNames(s.World) : a.Length == 2 ? ItemKeys : Array.Empty<string>());
        Reg("kill", 2, "/kill [gracz|encja]", (s, a) =>
        {
            Entity e = a.Length == 0 ? AsPlayer(s) : GetEntity(s, a[0]);
            KillEntity(e);
            Notify(s, "Zabito " + EntityName(e));
        }, user: (a, i) => i == 0, tab: (s, a) => a.Length == 1 ? PlayerNames(s.World).Concat(new[] { "@e", "@a", "@p", "@r" }) : Array.Empty<string>());
        Reg("summon", 2, "/summon <nazwa encji> [x] [y] [z] [tag danych]", Summon, tab: (s, a) => a.Length == 1 ? EntityNames.All : Array.Empty<string>());
        Reg("effect", 2, "/effect <gracz> <efekt> [sekundy] [wzmocnienie] [ukryj cząsteczki] LUB /effect <gracz> clear", EffectCmd, user: (a, i) => i == 0,
            tab: (s, a) => a.Length == 1 ? PlayerNames(s.World) : a.Length == 2 ? EffectKeys.Skip(1).Select(k => "minecraft:" + k).Prepend("clear") : a.Length == 5 ? new[] { "true", "false" } : Array.Empty<string>());
        Reg("enchant", 2, "/enchant <gracz> <id zaklęcia> [poziom]", Enchant, user: (a, i) => i == 0,
            tab: (s, a) => a.Length == 1 ? PlayerNames(s.World) : a.Length == 2 ? Enum.GetValues<Ench>().Select(e => EnchKey(e)) : Array.Empty<string>());
        Reg("entitydata", 2, "/entitydata <encja> <tag danych>", EntityData, user: (a, i) => i == 0);
        Reg("replaceitem", 2, "/replaceitem <block|entity> ...", ReplaceItem, user: (a, i) => a.Length > 0 && a[0] == "entity" && i == 1,
            tab: (s, a) => a.Length == 1 ? new[] { "entity", "block" } : (a.Length == 3 && a[0] == "entity") || (a.Length == 5 && a[0] == "block") ? SlotNames.Keys : (a.Length == 4 && a[0] == "entity") || (a.Length == 6 && a[0] == "block") ? ItemKeys : Array.Empty<string>());
        Reg("spreadplayers", 2, "/spreadplayers <x> <z> <odstęp> <maks. zasięg> <drużyny true|false> <gracze ...>", SpreadPlayers);
        Reg("testfor", 2, "/testfor <gracz> [tag danych]", (s, a) =>
        {
            if (a.Length < 1) throw new UsageException("/testfor <gracz> [tag danych]");
            var e = GetEntity(s, a[0]);
            if (a.Length >= 2)
            {
                var tag = ParseTag(Arg.Join(a, 1));
                if (!Mojangson.Matches(tag, EntityNbt.Write(e), true)) throw new CommandException($"{EntityName(e)} nie pasuje do wymaganej struktury danych");
            }
            Notify(s, "Znaleziono " + EntityName(e));
        }, user: (a, i) => i == 0);
        Reg("execute", 2, "/execute <encja> <x> <y> <z> <komenda> LUB /execute <encja> <x> <y> <z> detect <x> <y> <z> <blok> <dane> <komenda>", Execute, user: (a, i) => i == 0,
            tab: (s, a) => a.Length == 1 ? PlayerNames(s.World).Concat(new[] { "@e", "@a", "@p", "@r" }) : a.Length == 5 ? Names.Prepend("detect") : a.Length == 9 ? BlockKeys : Array.Empty<string>());
    }

    public static Nbt ParseTag(string s)
    {
        try { return Mojangson.Parse(s); }
        catch (CommandException e) { throw new CommandException("Nie udało się przetworzyć tagu danych: " + e.Message); }
    }

    public static void KillEntity(Entity e)
    {
        if (e is LivingEntity le)
        {
            if (le.Dead) return;
            le.Health = 0;
            le.OnDeath(new DamageSource("outOfWorld") { BypassArmor = true });
        }
        else e.Kill();
    }

    private static void Tp(CommandSender s, string[] a)
    {
        const string u = "/tp [cel] <gracz> LUB /tp [cel] <x> <y> <z> [<obrót-y> <obrót-x>]";
        if (a.Length < 1) throw new UsageException(u);
        int i = 0;
        Entity e;
        if (a.Length != 2 && a.Length != 4 && a.Length != 6) e = AsPlayer(s);
        else { e = GetEntity(s, a[0]); i = 1; }
        if (a.Length != 1 && a.Length != 2)
        {
            if (a.Length < i + 3) throw new UsageException(u);
            double x = Arg.Coord(e.X, a[i], true), y = Arg.Coord(e.Y, a[i + 1], false), z = Arg.Coord(e.Z, a[i + 2], true);
            double yaw = Arg.Coord(e.Yaw, a.Length > i + 3 ? a[i + 3] : "~", false), pitch = Arg.Coord(e.Pitch, a.Length > i + 4 ? a[i + 4] : "~", false);
            float f = MathUtil.WrapDegrees((float)yaw), f1 = MathUtil.WrapDegrees((float)pitch);
            if (f1 > 90 || f1 < -90) { f1 = MathUtil.WrapDegrees(180 - f1); f = MathUtil.WrapDegrees(f + 180); }
            if (e.RidingEntity != null) e.Mount(null);
            Teleport(e, x, y, z, f, f1);
            Notify(s, $"Przeteleportowano {EntityName(e)} na {JD(x)}, {JD(y)}, {JD(z)}");
        }
        else
        {
            var d = GetEntity(s, a[^1]);
            if (d.World != e.World) throw new CommandException("Nie można przeteleportować, ponieważ gracze nie są w tym samym wymiarze");
            if (e.RidingEntity != null) e.Mount(null);
            Teleport(e, d.X, d.Y, d.Z, d.Yaw, d.Pitch);
            Notify(s, $"Przeteleportowano {EntityName(e)} do {EntityName(d)}");
        }
    }

    public static void Teleport(Entity e, double x, double y, double z, float yaw, float pitch)
    {
        e.SetPositionAndPrev(x, y, z);
        e.Yaw = e.PrevYaw = yaw; e.Pitch = e.PrevPitch = pitch;
        if (e is LivingEntity le) { le.HeadYaw = le.PrevHeadYaw = yaw; }
        e.MotionX = e.MotionY = e.MotionZ = 0; e.FallDistance = 0;
        if (e is Player p) p.NetTeleportHook?.Invoke();
    }

    private static void Give(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/give <gracz> <przedmiot> [ilość] [dane] [tag danych]");
        var p = GetPlayer(s, a[0]);
        var it = Arg.Item(a[1]);
        int count = a.Length >= 3 ? Arg.Int(a[2], 1, 64) : 1;
        int meta = a.Length >= 4 ? Arg.Int(a[3]) : 0;
        var st = new ItemStack(it.Id, count, meta);
        if (a.Length >= 5) st.Tag = ParseTag(ChatArg(s, a, 4, true));
        var copy = st.Copy();
        int before = CountMatching(p, copy);
        bool added = p.Inventory.Add(st);
        int got = CountMatching(p, copy) - before;
        p.Inventory.Dirty = true;
        if (got > 0) p.World.PlaySound("random.pop", p.X, p.Y, p.Z, 0.2f, ((p.World.Rand.NextFloat() - p.World.Rand.NextFloat()) * 0.7f + 1f) * 2f);
        int left = count - got;
        s.SetStat(StatType.AffectedItems, got);
        if (left > 0 && !added)
        {
            var drop = copy.CopyWith(left);
            var ie = new ItemEntity(p.World, p.X, p.Y + p.EyeHeight - 0.3, p.Z, drop) { PickupDelay = 0 };
            p.World.SpawnEntity(ie);
        }
        Notify(s, $"Dano [{copy.Name}] * {count} graczowi {p.Name}");
    }

    private static int CountMatching(Player p, ItemStack like)
    {
        int n = 0;
        for (int i = 0; i < 40; i++) { var x = p.Inventory.Get(i); if (x != null && x.Id == like.Id && x.Damage == like.Damage && Nbt.Same(x.Tag, like.Tag)) n += x.Count; }
        return n;
    }

    private static void Clear(CommandSender s, string[] a)
    {
        var p = a.Length == 0 ? AsPlayer(s) : GetPlayer(s, a[0]);
        Item it = a.Length >= 2 ? Arg.Item(a[1]) : null;
        int meta = a.Length >= 3 ? Arg.Int(a[2], -1) : -1;
        int max = a.Length >= 4 ? Arg.Int(a[3], -1) : -1;
        Nbt tag = a.Length >= 5 ? ParseTag(Arg.Join(a, 4)) : null;
        int n = 0;
        bool Match(ItemStack x) => x != null && (it == null || x.Id == it.Id) && (meta <= -1 || x.Damage == meta) && (tag == null || Mojangson.Matches(tag, x.Tag ?? new Nbt(), true));
        for (int i = 0; i < 40; i++)
        {
            var x = p.Inventory.Get(i);
            if (!Match(x)) continue;
            if (max == 0) { n += x.Count; continue; }
            int take = max < 0 ? x.Count : Math.Min(x.Count, max - n);
            n += take; x.Count -= take;
            if (x.Count <= 0) p.Inventory.Set(i, null);
            if (max > 0 && n >= max) break;
        }
        if (Match(p.Cursor) && (max < 0 || n < max || max == 0))
        {
            if (max == 0) n += p.Cursor.Count;
            else { int take = max < 0 ? p.Cursor.Count : Math.Min(p.Cursor.Count, max - n); n += take; p.Cursor.Count -= take; if (p.Cursor.Count <= 0) p.Cursor = null; }
        }
        p.Inventory.Dirty = true;
        s.SetStat(StatType.AffectedItems, n);
        if (n == 0) throw new CommandException($"Nie można wyczyścić ekwipunku gracza {p.Name}, brak przedmiotów do usunięcia");
        if (max == 0) s.Message($"Gracz {p.Name} ma {n} przedmiotów spełniających kryteria");
        else Notify(s, $"Wyczyszczono ekwipunek gracza {p.Name}, usunięto {n} przedmiotów");
    }

    private static void Summon(CommandSender s, string[] a)
    {
        if (a.Length < 1) throw new UsageException("/summon <nazwa encji> [x] [y] [z] [tag danych]");
        string name = a[0];
        var v = s.Pos;
        double x = v.X, y = v.Y, z = v.Z;
        if (a.Length >= 4) { x = Arg.Coord(x, a[1], true); y = Arg.Coord(y, a[2], false); z = Arg.Coord(z, a[3], true); }
        var w = s.World;
        if (!w.IsLoadedAt(MathUtil.FloorToInt(x), MathUtil.FloorToInt(z)) || y < 0 || y >= Chunk.Height) throw new CommandException("Nie można przywołać obiektu poza światem");
        Nbt tag = null;
        if (a.Length >= 5) tag = ParseTag(ChatArg(s, a, 4, true));
        var e = CreateEntity(name, w, tag) ?? throw new CommandException("Nie udało się przywołać obiektu");
        e.SetPositionAndPrev(x, y, z);
        if (tag != null) { EntityNbt.Read(e, tag); if (!tag.ContainsKey("Pos")) e.SetPositionAndPrev(x, y, z); }
        w.SpawnEntity(e);
        var cur = e; var t = tag;
        while (cur != null && t?.GetCompound("Riding") is { } rt)
        {
            var mount = CreateEntity(rt.GetString("id") ?? "", w, rt);
            if (mount != null)
            {
                mount.SetPositionAndPrev(x, y, z);
                EntityNbt.Read(mount, rt); if (!rt.ContainsKey("Pos")) mount.SetPositionAndPrev(x, y, z);
                w.SpawnEntity(mount); w.FlushAdds();
                cur.Mount(mount);
            }
            cur = mount; t = rt;
        }
        Notify(s, "Obiekt został przywołany");
    }

    /// <summary>EntityList.createEntityFromNBT: nazwa MC (lub klucz BlockCraft) z wariantami z tagów (SkeletonType, IsVillager, IsBaby, Elder, Type, RabbitType).</summary>
    public static Entity CreateEntity(string name, World.World w, Nbt tag)
    {
        string key = EntityNames.KeyOf(name);
        if (key == null) return null;
        if (tag != null)
        {
            if (key == "skeleton" && tag.GetInt("SkeletonType") == 1) key = "wither_skeleton";
            if (key == "zombie" && tag.GetInt("IsVillager") != 0) key = "zombie_villager";
            else if (key == "zombie" && tag.GetInt("IsBaby") != 0) key = "baby_zombie";
            if (key == "guardian" && tag.GetInt("Elder") != 0) key = "elder_guardian";
            if (key == "horse") key = tag.GetInt("Type") switch { 1 => "donkey", 2 => "mule", 3 => "zombie_horse", 4 => "skeleton_horse", _ => "horse" };
            if (key == "rabbit" && tag.GetInt("RabbitType") == 99) key = "killer_bunny";
        }
        if (!EntitySerializer.Types.ContainsKey(key)) return null;
        try { return EntitySerializer.Create(key, w); } catch (Exception ex) { Log.Error("summon " + key + ": " + ex.Message); return null; }
    }

    private static void EffectCmd(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/effect <gracz> <efekt> [sekundy] [wzmocnienie] [ukryj cząsteczki] LUB /effect <gracz> clear");
        if (GetEntity(s, a[0]) is not LivingEntity e) throw new CommandException("Nie można odnaleźć tej encji");
        if (a[1] == "clear")
        {
            if (e.Effects.Count == 0) throw new CommandException($"Nie można zabrać efektów od {EntityName(e)}, ponieważ ich nie ma");
            e.ClearEffects();
            Notify(s, "Zabrano wszystkie efekty od " + EntityName(e));
            return;
        }
        int id;
        try { id = Arg.Int(a[1], 1); }
        catch (CommandException)
        {
            string k = a[1].StartsWith("minecraft:") ? a[1][10..] : a[1];
            id = Array.IndexOf(EffectKeys, k);
            if (id <= 0) throw;
        }
        if (id < 1 || id >= EffectKeys.Length) throw new CommandException($"Nie ma efektu o ID {id}");
        var eff = (Effect)id;
        bool instant = EffectLogic.Instant(eff);
        int sec = 30, dur = 600, amp = 0;
        if (a.Length >= 3) { sec = Arg.Int(a[2], 0, 1000000); dur = instant ? sec : sec * 20; }
        else if (instant) dur = 1;
        if (a.Length >= 4) amp = Arg.Int(a[3], 0, 255);
        bool particles = !(a.Length >= 5 && a[4].Equals("true", StringComparison.OrdinalIgnoreCase));
        string en = EffectLogic.Name(eff);
        if (sec > 0)
        {
            if (instant) for (int i = 0; i < Math.Min(dur, 100); i++) EffectLogic.ApplyInstant(e, eff, amp);
            else e.AddEffect(new EffectInstance(eff, dur, amp, false, particles));
            Notify(s, $"Dano {en} (ID {id}) * {amp} dla {EntityName(e)} na {sec} sekund");
        }
        else if (e.HasEffect(eff)) { e.RemoveEffect(eff); Notify(s, $"Zabrano {en} od {EntityName(e)}"); }
        else throw new CommandException($"Nie można zabrać {en} od {EntityName(e)}, ponieważ nie ma tego efektu");
    }

    private static void Enchant(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/enchant <gracz> <id zaklęcia> [poziom]");
        var p = GetPlayer(s, a[0]);
        s.SetStat(StatType.AffectedItems, 0);
        int id;
        try { id = Arg.Int(a[1], 0); }
        catch (CommandException)
        {
            string k = a[1].StartsWith("minecraft:") ? a[1][10..] : a[1];
            var m = Enum.GetValues<Ench>().Where(e => EnchKey(e) == k).ToList();
            if (m.Count == 0) throw;
            id = (int)m[0];
        }
        var st = p.Inventory.Selected ?? throw new CommandException("Cel nie trzyma żadnego przedmiotu");
        if (!Enum.IsDefined(typeof(Ench), id)) throw new CommandException($"Nie ma zaklęcia o ID {id}");
        var ench = (Ench)id;
        if (!EnchantingRules.CanApply(ench, st, false)) throw new CommandException("Wybrane zaklęcie nie może zostać dodane do tego przedmiotu");
        int lvl = a.Length >= 3 ? Arg.Int(a[2], 1, EnchantHelper.MaxLevel(ench)) : 1;
        foreach (var kv in EnchantHelper.GetAll(st))
            if (kv.Key == ench || !EnchantingRules.Compatible(kv.Key, ench))
                throw new CommandException($"{EnchantHelper.Name(ench)} {EnchantHelper.Roman(lvl)} nie może być połączone z {EnchantHelper.Name(kv.Key)} {EnchantHelper.Roman(kv.Value)}");
        st.GetOrCreateTag().GetOrCreateList("ench").Add(new Nbt { ["id"] = id, ["lvl"] = lvl });
        p.Inventory.Dirty = true;
        Notify(s, "Zaklinanie zakończone sukcesem");
        s.SetStat(StatType.AffectedItems, 1);
    }

    private static void EntityData(CommandSender s, string[] a)
    {
        if (a.Length < 2) throw new UsageException("/entitydata <encja> <tag danych>");
        var e = GetEntity(s, a[0]);
        if (e is Player) throw new CommandException($"{EntityName(e)} jest graczem i nie może zostać zmieniony");
        var nbt = EntityNbt.Write(e);
        var copy = nbt.Copy();
        var tag = ParseTag(ChatArg(s, a, 1, true));
        tag.Remove("UUIDMost"); tag.Remove("UUIDLeast");
        Mojangson.Merge(nbt, tag);
        if (Nbt.Same(nbt, copy)) throw new CommandException("Tag danych nie zmienił się: " + Mojangson.ToText(nbt));
        EntityNbt.Read(e, nbt);
        Notify(s, "Dane encji zaktualizowane na: " + Mojangson.ToText(nbt));
    }

    /// <summary>CommandReplaceItem.SHORTCUTS z 1.8.</summary>
    private static readonly Dictionary<string, int> SlotNames = BuildSlots();
    private static Dictionary<string, int> BuildSlots()
    {
        var d = new Dictionary<string, int>();
        for (int i = 0; i < 54; i++) d["slot.container." + i] = i;
        for (int i = 0; i < 9; i++) d["slot.hotbar." + i] = i;
        for (int i = 0; i < 27; i++) d["slot.inventory." + i] = 9 + i;
        for (int i = 0; i < 27; i++) d["slot.enderchest." + i] = 200 + i;
        for (int i = 0; i < 8; i++) d["slot.villager." + i] = 300 + i;
        for (int i = 0; i < 15; i++) d["slot.horse." + i] = 500 + i;
        d["slot.weapon"] = 99; d["slot.armor.head"] = 103; d["slot.armor.chest"] = 102; d["slot.armor.legs"] = 101; d["slot.armor.feet"] = 100;
        d["slot.horse.saddle"] = 400; d["slot.horse.armor"] = 401; d["slot.horse.chest"] = 499;
        return d;
    }

    private static void ReplaceItem(CommandSender s, string[] a)
    {
        const string u = "/replaceitem <block|entity> ...";
        if (a.Length < 1) throw new UsageException(u);
        bool block;
        if (a[0] == "entity") block = false;
        else if (a[0] == "block") block = true;
        else throw new UsageException(u);
        int i;
        if (block) { if (a.Length < 6) throw new UsageException("/replaceitem block <x> <y> <z> <miejsce> <przedmiot> [ilość] [dane] [tag danych]"); i = 4; }
        else { if (a.Length < 4) throw new UsageException("/replaceitem entity <selektor> <miejsce> <przedmiot> [ilość] [dane] [tag danych]"); i = 2; }
        if (!SlotNames.TryGetValue(a[i++], out int slot)) throw new CommandException($"Nieznane miejsce '{a[i - 1]}'");
        Item it = a[i] is "air" or "minecraft:air" ? null : Arg.Item(a[i]);
        i++;
        int count = a.Length > i ? Arg.Int(a[i++], 1, 64) : 1;
        int meta = a.Length > i ? Arg.Int(a[i++]) : 0;
        ItemStack st = it == null ? null : new ItemStack(it.Id, count, meta);
        if (a.Length > i) { var tag = ParseTag(ChatArg(s, a, i, true)); if (st != null) st.Tag = tag; }
        string what = st == null ? "Powietrze" : "[" + st.Name + "]";
        if (block)
        {
            s.SetStat(StatType.AffectedItems, 0);
            var pos = Arg.BlockPos(s, a, 1);
            if (s.World.GetTileEntity(pos.X, pos.Y, pos.Z) is not IInventory inv) throw new CommandException($"Blok na {pos.X}, {pos.Y}, {pos.Z} nie jest pojemnikiem");
            if (slot >= 0 && slot < inv.Size) { inv.Set(slot, st); inv.Changed(); }
        }
        else
        {
            var e = GetEntity(s, a[1]);
            s.SetStat(StatType.AffectedItems, 0);
            if (!ReplaceSlot(e, slot, st)) throw new CommandException($"Nie można zastąpić miejsca {slot} przedmiotem {what} * {count}");
        }
        s.SetStat(StatType.AffectedItems, count);
        Notify(s, $"Zastąpiono miejsce {slot} przedmiotem {what} * {count}");
    }

    /// <summary>Entity.replaceItemInInventory dla gracza, mobów (ręka) i wagoników/pojemników.</summary>
    private static bool ReplaceSlot(Entity e, int slot, ItemStack st)
    {
        if (e is Player p)
        {
            if (slot == 99) { p.Inventory.Selected = st; }
            else if (slot is >= 100 and <= 103) p.Inventory.Armor[103 - slot] = st;
            else if (slot is >= 0 and < 36) p.Inventory.Set(slot, st);
            else return false;
            p.Inventory.Dirty = true;
            return true;
        }
        if (e is LivingEntity le && slot == 99) { le.HeldItem = st; return true; }
        if (e is IInventory inv && slot >= 0 && slot < inv.Size) { inv.Set(slot, st); inv.Changed(); return true; }
        return false;
    }

    private static void SpreadPlayers(CommandSender s, string[] a)
    {
        if (a.Length < 6) throw new UsageException("/spreadplayers <x> <z> <odstęp> <maks. zasięg> <drużyny true|false> <gracze ...>");
        int i = 0;
        var bp = s.BlockPos;
        double cx = Arg.Coord(bp.X, a[i++], true), cz = Arg.Coord(bp.Z, a[i++], true);
        double spread = Arg.Double(a[i++], 0), range = Arg.Double(a[i++], spread + 1);
        bool teams = Arg.Bool(a[i++]);
        var list = new List<Entity>();
        while (i < a.Length)
        {
            string x = a[i++];
            if (Selector.StartsSelector(x)) { var l = Selector.Match(s, x); if (l.Count == 0) throw new CommandException("Nie można odnaleźć tej encji"); list.AddRange(l); }
            else list.Add(AllPlayers(s.World).FirstOrDefault(p => p.Name.Equals(x, StringComparison.OrdinalIgnoreCase)) ?? throw new CommandException("Nie można odnaleźć tego gracza"));
        }
        s.SetStat(StatType.AffectedEntities, list.Count);
        if (list.Count == 0) throw new CommandException("Nie można odnaleźć tej encji");
        s.Message($"Rozmieszczanie {list.Count} {(teams ? "drużyn" : "graczy")} w promieniu {JD(range)} bloków wokół {JD(cx)},{JD(cz)} (co najmniej {JD(spread)} bloków od siebie)");
        var w = s.World;
        var sb = Scoreboard.Of(w);
        var rnd = new Random();
        double x0 = cx - range, z0 = cz - range, x1 = cx + range, z1 = cz + range;
        int n = teams ? list.Select(e => e is Player p ? sb.TeamOf(p.Name)?.Name ?? "" : "").Distinct().Count() : list.Count;
        var pos = new (double x, double z)[n];
        for (int k = 0; k < n; k++) pos[k] = (x0 + rnd.NextDouble() * (x1 - x0), z0 + rnd.NextDouble() * (z1 - z0));
        bool Safe((double x, double z) p)
        {
            int bx = MathUtil.FloorToInt(p.x), bz = MathUtil.FloorToInt(p.z);
            for (int y = Chunk.Height - 1; y > 0; y--)
            {
                int id = w.GetBlock(bx, y, bz);
                if (id != 0) return !Block.Get(id).IsLiquid && id != B.Fire;
            }
            return false;
        }
        bool moved = true; double minDist = float.MaxValue; int it;
        for (it = 0; it < 10000 && moved; it++)
        {
            moved = false; minDist = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                var p = pos[j]; int cnt = 0; double mx = 0, mz = 0;
                for (int l = 0; l < n; l++)
                {
                    if (j == l) continue;
                    double d = Math.Sqrt((p.x - pos[l].x) * (p.x - pos[l].x) + (p.z - pos[l].z) * (p.z - pos[l].z));
                    minDist = Math.Min(d, minDist);
                    if (d < spread) { cnt++; mx += pos[l].x - p.x; mz += pos[l].z - p.z; }
                }
                if (cnt > 0)
                {
                    mx /= cnt; mz /= cnt;
                    double len = Math.Sqrt(mx * mx + mz * mz);
                    if (len > 0) { p.x -= mx / len; p.z -= mz / len; }
                    else p = (x0 + rnd.NextDouble() * (x1 - x0), z0 + rnd.NextDouble() * (z1 - z0));
                    moved = true;
                }
                var c = (Math.Clamp(p.x, x0, x1), Math.Clamp(p.z, z0, z1));
                if (c != p) { p = c; moved = true; }
                pos[j] = p;
            }
            if (!moved)
                for (int j = 0; j < n; j++)
                    if (!Safe(pos[j])) { pos[j] = (x0 + rnd.NextDouble() * (x1 - x0), z0 + rnd.NextDouble() * (z1 - z0)); moved = true; }
        }
        if (it >= 10000)
            throw new CommandException($"Nie można rozmieścić {n} {(teams ? "drużyn" : "graczy")} wokół {JD(cx)},{JD(cz)} (za dużo graczy na tę przestrzeń, spróbuj odstępu najwyżej {minDist.ToString("0.00", CultureInfo.InvariantCulture)})");
        double sum = 0; int next = 0;
        var byTeam = new Dictionary<string, int>();
        foreach (var e in list)
        {
            int k;
            if (teams) { string tn = e is Player p ? sb.TeamOf(p.Name)?.Name ?? "" : ""; if (!byTeam.TryGetValue(tn, out k)) byTeam[tn] = k = next++; }
            else k = next++;
            var q = pos[k];
            int bx = MathUtil.FloorToInt(q.x), bz = MathUtil.FloorToInt(q.z), top = 257;
            for (int y = Chunk.Height - 1; y > 0; y--) if (w.GetBlock(bx, y - 1, bz) != 0) { top = y; break; }
            if (e.RidingEntity != null) e.Mount(null);
            Teleport(e, bx + 0.5, top, bz + 0.5, e.Yaw, e.Pitch);
            double best = double.MaxValue;
            for (int l = 0; l < n; l++) if (l != k) best = Math.Min(best, Math.Sqrt((q.x - pos[l].x) * (q.x - pos[l].x) + (q.z - pos[l].z) * (q.z - pos[l].z)));
            sum += best;
        }
        Notify(s, $"Rozmieszczono {n} {(teams ? "drużyn" : "graczy")} wokół {JD(cx)},{JD(cz)}");
        if (n > 1) s.Message($"(Średnia odległość między {(teams ? "drużynami" : "graczami")} to {(sum / list.Count).ToString("0.00", CultureInfo.InvariantCulture)} bloków po {it} iteracjach)");
    }

    private static void Execute(CommandSender s, string[] a)
    {
        if (a.Length < 5) throw new UsageException("/execute <encja> <x> <y> <z> <komenda> LUB /execute <encja> <x> <y> <z> detect <x> <y> <z> <blok> <dane> <komenda>");
        var e = GetEntity(s, a[0]);
        double x = Arg.Coord(e.X, a[1], false), y = Arg.Coord(e.Y, a[2], false), z = Arg.Coord(e.Z, a[3], false);
        int i = 4;
        if (a[4] == "detect" && a.Length > 10)
        {
            double dx = Arg.Coord(x, a[5], false), dy = Arg.Coord(y, a[6], false), dz = Arg.Coord(z, a[7], false);
            int id = Arg.Block(a[8]);
            int meta = Arg.Int(a[9], -1, 15);
            int bx = MathUtil.FloorToInt(dx), by = MathUtil.FloorToInt(dy), bz = MathUtil.FloorToInt(dz);
            var w = e.World;
            if (w.GetBlock(bx, by, bz) != id || (meta >= 0 && w.GetMeta(bx, by, bz) != meta))
                throw new CommandException($"Nie udało się wykonać 'detect' jako {EntityName(e)}");
            i = 10;
        }
        string cmd = Arg.Join(a, i);
        var sender = new ExecuteSender(s, e, x, y, z);
        // MC 1.8 łapie też własny wyjątek "allInvocationsFailed" i zgłasza zawsze "commands.execute.failed"
        int n;
        try { n = Run(sender, cmd); } catch (Exception) { n = 0; }
        if (n < 1) throw new CommandException($"Nie udało się wykonać '{cmd}' jako {EntityName(e)}");
    }
}
