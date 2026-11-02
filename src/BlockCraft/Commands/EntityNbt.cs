using System.Reflection;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Cmd;

/// <summary>
/// Odpowiednik Entity.writeToNBT/readFromNBT dla komend (/summon, /entitydata, /testfor): wspólne tagi MC 1.8 (Pos, Motion,
/// Rotation, Fire, Air, CustomName, Health, ActiveEffects, Equipment, NoAI...) plus tagi typów mapowane na pola klas BlockCraft.
/// Encje zapisują się binarnie, więc tagi bez odpowiednika w BlockCraft są pomijane.
/// </summary>
public static class EntityNbt
{
    /// <summary>Tag MC -> kandydaci na nazwę pola/właściwości w klasach encji BlockCraft.</summary>
    private static readonly Dictionary<string, string[]> Extra = new()
    {
        ["Size"] = new[] { "Size" }, ["powered"] = new[] { "Powered", "Charged" }, ["ExplosionRadius"] = new[] { "ExplosionRadius" },
        ["Fuse"] = new[] { "Fuse" }, ["Color"] = new[] { "FleeceColor", "Color" }, ["Sheared"] = new[] { "Sheared" }, ["Saddle"] = new[] { "Saddled" },
        ["Profession"] = new[] { "Profession" }, ["Angry"] = new[] { "Angry" }, ["Sitting"] = new[] { "Sitting" }, ["CollarColor"] = new[] { "CollarColor" },
        ["IsBaby"] = new[] { "IsBaby", "Baby", "IsChild" }, ["Age"] = new[] { "GrowingAge" }, ["NoAI"] = new[] { "NoAI" }, ["CatType"] = new[] { "CatType", "SkinType" },
        ["RabbitType"] = new[] { "RabbitType" }, ["Elder"] = new[] { "Elder" }, ["PlayerCreated"] = new[] { "PlayerCreated" }, ["Tame"] = new[] { "Tamed" },
        ["ChestedHorse"] = new[] { "Chested" }, ["Variant"] = new[] { "Variant" }, ["Invul"] = new[] { "Invul" }, ["Riches"] = new[] { "Riches" },
        ["Willing"] = new[] { "Willing" }, ["Career"] = new[] { "Career" }, ["Anger"] = new[] { "Anger" }, ["ExplosionPower"] = new[] { "ExplosionPower" },
        ["Invisible"] = new[] { "Invisible" }, ["Small"] = new[] { "Small" }, ["ShowArms"] = new[] { "ShowArms" }, ["NoBasePlate"] = new[] { "NoBasePlate" },
        ["Silent"] = new[] { "Silent" }, ["CustomNameVisible"] = new[] { "CustomNameVisible" }, ["PersistenceRequired"] = new[] { "PersistenceRequired" },
    };

    public static Nbt ItemToNbt(ItemStack s)
    {
        var n = new Nbt { ["id"] = "minecraft:" + (s.Item?.Key ?? s.Id.ToString()), ["Count"] = s.Count, ["Damage"] = s.Damage };
        if (s.Tag != null && s.Tag.Count > 0) n["tag"] = s.Tag.Copy();
        return n;
    }

    public static ItemStack ItemFromNbt(Nbt n)
    {
        if (n == null) return null;
        Item it = n.TryGetValue("id", out var id) ? id is string str ? Item.Get(str) : Item.ById.ElementAtOrDefault(Convert.ToInt32(id)) : null;
        if (it == null) return null;
        var s = new ItemStack(it.Id, Math.Max(1, n.GetInt("Count", 1)), n.GetInt("Damage"));
        if (n.GetCompound("tag") is { } t) s.Tag = t.Copy();
        return s;
    }

    public static Nbt Write(Entity e)
    {
        var n = new Nbt
        {
            ["Pos"] = new List<object> { e.X, e.Y, e.Z },
            ["Motion"] = new List<object> { e.MotionX, e.MotionY, e.MotionZ },
            ["Rotation"] = new List<object> { e.Yaw, e.Pitch },
            ["FallDistance"] = e.FallDistance, ["Fire"] = e.FireTicks, ["OnGround"] = e.OnGround ? 1 : 0,
            ["Dimension"] = (int)e.World.Dimension, ["NoGravity"] = e.NoGravity ? 1 : 0,
        };
        var u = Guid.TryParseExact(e.SaveId, "N", out var g) ? g : Guid.Empty;
        var b = u.ToByteArray();
        n["UUIDMost"] = BitConverter.ToInt64(b, 0); n["UUIDLeast"] = BitConverter.ToInt64(b, 8);
        if (e is LivingEntity le)
        {
            n["Air"] = le.Air; n["HealF"] = le.Health; n["Health"] = (int)Math.Ceiling(le.Health); n["HurtTime"] = le.HurtTime; n["DeathTime"] = le.DeathTime;
            n["AbsorptionAmount"] = le.Absorption;
            if (le.Effects.Count > 0)
                n["ActiveEffects"] = le.Effects.Values.Select(x => (object)new Nbt { ["Id"] = (int)x.Id, ["Amplifier"] = x.Amplifier, ["Duration"] = x.Duration, ["Ambient"] = x.Ambient ? 1 : 0, ["ShowParticles"] = x.ShowParticles ? 1 : 0 }).ToList();
            var eq = new List<object> { le.HeldItem != null ? ItemToNbt(le.HeldItem) : new Nbt() };
            if (e is Player pa) for (int i = 3; i >= 0; i--) eq.Add(pa.Inventory.Armor[i] != null ? ItemToNbt(pa.Inventory.Armor[i]) : new Nbt());
            else for (int i = 0; i < 4; i++) eq.Add(new Nbt());
            n["Equipment"] = eq;
        }
        if (e is Mob m)
        {
            if (!string.IsNullOrEmpty(m.CustomName)) n["CustomName"] = m.CustomName;
            n["PersistenceRequired"] = m.PersistenceRequired ? 1 : 0;
        }
        if (e is Player p)
        {
            n["XpLevel"] = p.XpLevel; n["XpP"] = p.XpProgress; n["XpTotal"] = p.XpTotal; n["foodLevel"] = p.FoodLevel; n["foodSaturationLevel"] = p.Saturation;
            n["foodExhaustionLevel"] = p.Exhaustion; n["playerGameType"] = p.GameMode; n["SelectedItemSlot"] = p.Inventory.SelectedSlot; n["Sleeping"] = p.Sleeping ? 1 : 0;
            var inv = new List<object>();
            for (int i = 0; i < 36; i++) if (p.Inventory.Get(i) is { } st) { var x = ItemToNbt(st); x["Slot"] = i; inv.Add(x); }
            for (int i = 0; i < 4; i++) if (p.Inventory.Armor[i] is { } st) { var x = ItemToNbt(st); x["Slot"] = 103 - i; inv.Add(x); }
            n["Inventory"] = inv;
            if (p.HasSpawn) { n["SpawnX"] = p.SpawnX; n["SpawnY"] = p.SpawnY; n["SpawnZ"] = p.SpawnZ; }
        }
        switch (e)
        {
            case CommandMinecart cart: cart.Logic.WriteNbt(n); break;
            case ItemEntity ie:
                n["Age"] = ie.Age; n["PickupDelay"] = ie.PickupDelay; if (ie.Stack != null) n["Item"] = ItemToNbt(ie.Stack); break;
            case XpOrbEntity xo: n["Value"] = xo.Value; n["Age"] = xo.Age; break;
            case TntEntity t: n["Fuse"] = t.Fuse; break;
            case FallingBlockEntity fb: n["Block"] = "minecraft:" + (World.Block.ById[fb.BlockId]?.Key ?? "air"); n["TileID"] = (int)fb.BlockId; n["Data"] = fb.Meta; break;
        }
        foreach (var kv in Extra)
        {
            if (n.ContainsKey(kv.Key)) continue;
            var mem = Find(e, kv.Value);
            if (mem == null) continue;
            object v = Get(mem, e);
            if (v is bool bv) n[kv.Key] = bv ? 1 : 0;
            else if (v is int or float or double or long or string) n[kv.Key] = v;
            else if (v is Enum ev) n[kv.Key] = Convert.ToInt32(ev);
            else if (v is byte or short) n[kv.Key] = Convert.ToInt32(v);
        }
        return n;
    }

    public static void Read(Entity e, Nbt n)
    {
        if (n.GetList("Pos") is { Count: 3 } pos) e.SetPositionAndPrev(D(pos[0]), D(pos[1]), D(pos[2]));
        if (n.GetList("Motion") is { Count: 3 } mot) { e.MotionX = D(mot[0]); e.MotionY = D(mot[1]); e.MotionZ = D(mot[2]); }
        if (n.GetList("Rotation") is { Count: 2 } rot) { e.Yaw = e.PrevYaw = (float)D(rot[0]); e.Pitch = e.PrevPitch = (float)D(rot[1]); if (e is LivingEntity l0) { l0.HeadYaw = l0.PrevHeadYaw = l0.BodyYaw = l0.PrevBodyYaw = e.Yaw; } }
        if (n.ContainsKey("FallDistance")) e.FallDistance = n.GetFloat("FallDistance");
        if (n.ContainsKey("Fire")) e.FireTicks = n.GetInt("Fire");
        if (n.ContainsKey("OnGround")) e.OnGround = n.GetInt("OnGround") != 0;
        if (n.ContainsKey("NoGravity")) e.NoGravity = n.GetInt("NoGravity") != 0;
        if (e is LivingEntity le)
        {
            if (n.ContainsKey("Air")) le.Air = n.GetInt("Air");
            if (n.ContainsKey("HealF")) le.Health = n.GetFloat("HealF");
            else if (n.ContainsKey("Health")) le.Health = n.GetFloat("Health");
            if (n.ContainsKey("AbsorptionAmount")) le.Absorption = n.GetFloat("AbsorptionAmount");
            if (n.GetList("ActiveEffects") is { } effs)
                foreach (var o in effs.OfType<Nbt>())
                {
                    var id = (Effect)o.GetInt("Id");
                    if (!Enum.IsDefined(id) || id == Effect.None) continue;
                    le.AddEffect(new EffectInstance(id, o.GetInt("Duration"), o.GetInt("Amplifier"), o.GetInt("Ambient") != 0, !o.ContainsKey("ShowParticles") || o.GetInt("ShowParticles") != 0));
                }
            if (n.GetList("Equipment") is { Count: > 0 } eq)
            {
                le.HeldItem = ItemFromNbt(eq[0] as Nbt);
                if (e is Player pe) for (int i = 1; i < Math.Min(5, eq.Count); i++) pe.Inventory.Armor[4 - i] = ItemFromNbt(eq[i] as Nbt);
            }
        }
        if (e is Mob m)
        {
            if (n.ContainsKey("CustomName")) m.CustomName = n.GetString("CustomName");
            if (n.ContainsKey("PersistenceRequired")) m.PersistenceRequired = n.GetInt("PersistenceRequired") != 0;
        }
        switch (e)
        {
            case CommandMinecart cart: cart.Logic.ReadNbt(n); break;
            case ItemEntity ie:
                if (n.ContainsKey("PickupDelay")) ie.PickupDelay = n.GetInt("PickupDelay");
                if (n.ContainsKey("Age")) ie.Age = n.GetInt("Age");
                if (n.GetCompound("Item") is { } itn && ItemFromNbt(itn) is { } st) ie.Stack = st;
                break;
            case XpOrbEntity xo: if (n.ContainsKey("Value")) xo.Value = n.GetInt("Value"); break;
            case TntEntity t: if (n.ContainsKey("Fuse")) t.Fuse = n.GetInt("Fuse"); break;
            case FallingBlockEntity fb:
                if (n.GetString("Block") is { } bk) { try { fb.BlockId = (byte)Arg.Block(bk); } catch (CommandException) { } }
                else if (n.ContainsKey("TileID")) fb.BlockId = (byte)n.GetInt("TileID");
                if (n.ContainsKey("Data")) fb.Meta = n.GetInt("Data");
                break;
        }
        foreach (var kv in Extra)
        {
            if (!n.TryGetValue(kv.Key, out var v) || kv.Key is "Fuse" && e is TntEntity) continue;
            var mem = Find(e, kv.Value);
            if (mem == null) continue;
            try { Set(mem, e, v); } catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException) { }
        }
    }

    private static double D(object o) => Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture);

    public static MemberInfo Find(object e, string[] names)
    {
        var t = e.GetType();
        foreach (var name in names)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && !f.IsInitOnly) return f;
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0) return p;
        }
        return null;
    }
    public static object Get(MemberInfo m, object o) => m is FieldInfo f ? f.GetValue(o) : ((PropertyInfo)m).GetValue(o);
    public static void Set(MemberInfo m, object o, object v)
    {
        var t = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
        object c = t == typeof(bool) ? Convert.ToInt32(v) != 0 : t.IsEnum ? Enum.ToObject(t, Convert.ToInt32(v)) : Convert.ChangeType(v, t, System.Globalization.CultureInfo.InvariantCulture);
        if (m is FieldInfo fi) fi.SetValue(o, c); else ((PropertyInfo)m).SetValue(o, c);
    }
}
