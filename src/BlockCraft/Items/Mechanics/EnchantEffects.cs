using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items;

/// <summary>
/// Działanie zaklęć w rozgrywce według MC 1.8 (EnchantmentHelper, EnchantmentProtection/Damage/Thorns/Durability,
/// EntityPlayer.attackTargetEntityWithCurrentItem i getToolDigEfficiency, EntityLivingBase.decreaseAirSupply i ruch w wodzie).
/// </summary>
public static class EnchantEffects
{
    public enum Creature { Undefined, Undead, Arthropod }

    /// <summary>EntityLivingBase.getCreatureAttribute.</summary>
    public static Creature CreatureOf(LivingEntity e) => e switch
    {
        Zombie or Skeleton or ZombiePigman or WitherSkeleton or Wither => Creature.Undead,
        Horse h when h.Undead => Creature.Undead,
        Spider or Silverfish or Endermite => Creature.Arthropod,
        _ => Creature.Undefined
    };

    /// <summary>EntityLivingBase.getInventory: gracz - zbroja; inne - przedmiot w ręce.</summary>
    public static ItemStack[] Equipment(LivingEntity e) => e is Player p ? p.Inventory.Armor : new[] { e.HeldItem };

    public static int MaxLevel(LivingEntity e, Ench id)
    {
        int m = 0;
        foreach (var s in Equipment(e)) m = Math.Max(m, EnchantHelper.GetLevel(s, id));
        return m;
    }

    private static ItemStack Held(LivingEntity e) => e is Player p ? p.Inventory.Selected : e.HeldItem;

    /// <summary>EnchantmentHelper.getModifierForCreature (Ostrość, Pogromca nieumarłych, Zmora stawonogów).</summary>
    public static float DamageBonus(ItemStack held, Creature c)
    {
        float f = 0;
        foreach (var (id, lvl) in Entries(held))
            f += id switch
            {
                Ench.Sharpness => lvl * 1.25f,
                Ench.Smite when c == Creature.Undead => lvl * 2.5f,
                Ench.BaneOfArthropods when c == Creature.Arthropod => lvl * 2.5f,
                _ => 0f
            };
        return f;
    }

    /// <summary>Wpisy listy "ench" w kolejności zapisu (jak applyEnchantmentModifier).</summary>
    public static IEnumerable<(Ench id, int lvl)> Entries(ItemStack s)
    {
        var l = s?.Tag?.GetList("ench");
        if (l == null) yield break;
        foreach (var o in l) if (o is Core.Nbt n) yield return ((Ench)n.GetInt("id"), n.GetInt("lvl"));
    }

    /// <summary>EnchantmentProtection.calcModifierDamage dla jednego wpisu.</summary>
    public static int ProtectionModifier(Ench id, int level, DamageSource src)
    {
        if (src.Type is "outOfWorld" or "kill") return 0;
        float f = (6 + level * level) / 3f;
        return id switch
        {
            Ench.Protection => (int)MathF.Floor(f * .75f),
            Ench.FireProtection when src.IsFire => (int)MathF.Floor(f * 1.25f),
            Ench.FeatherFalling when src.Type == "fall" => (int)MathF.Floor(f * 2.5f),
            Ench.BlastProtection when src.IsExplosion => (int)MathF.Floor(f * 1.5f),
            Ench.ProjectileProtection when src.IsProjectile => (int)MathF.Floor(f * 1.5f),
            _ => 0
        };
    }

    /// <summary>EnchantmentHelper.getEnchantmentModifierDamage: suma (limit 25), potem losowo od połowy do całości.</summary>
    public static int ProtectionEpf(LivingEntity e, DamageSource src, Rng r)
    {
        int m = 0;
        foreach (var s in Equipment(e)) foreach (var (id, lvl) in Entries(s)) m += ProtectionModifier(id, lvl, src);
        m = Math.Clamp(m, 0, 25);
        return (m + 1 >> 1) + r.Next((m >> 1) + 1);
    }

    /// <summary>EntityLivingBase.applyPotionDamageCalculations, część zaklęć (po odporności, przed pochłanianiem).</summary>
    public static float ApplyProtection(LivingEntity e, DamageSource src, float amount)
    {
        if (src.Type == "starve" || amount <= 0) return amount;
        int k = ProtectionEpf(e, src, e.World.Rand);
        if (k > 20) k = 20;
        if (k > 0) amount = amount * (25 - k) / 25f;
        return amount;
    }

    /// <summary>EnchantmentProtection.getFireTimeForEntity.</summary>
    public static int FireTime(LivingEntity e, int ticks)
    {
        int i = MaxLevel(e, Ench.FireProtection);
        if (i > 0) ticks -= (int)MathF.Floor(ticks * (float)i * .15f);
        return ticks;
    }

    /// <summary>EnchantmentProtection.func_92092_a: Ochrona przed wybuchami zmniejsza odrzut wybuchu.</summary>
    public static double BlastKnockback(Entity e, double impact)
    {
        if (e is not LivingEntity le) return impact;
        int i = MaxLevel(le, Ench.BlastProtection);
        if (i > 0) impact -= Math.Floor(impact * (i * .15f));
        return impact;
    }

    /// <summary>EnchantmentHelper.applyThornEnchantments(user, attacker) - Ciernie po trafieniu wręcz albo strzałą.</summary>
    public static void ApplyThorns(LivingEntity user, Entity attacker)
    {
        if (user == null || user.World.IsRemote) return;
        var r = user.World.Rand;
        foreach (var s in Equipment(user))
            foreach (var (id, lvl) in Entries(s).ToList())
            {
                if (id != Ench.Thorns) continue;
                var item = FirstWith(user, Ench.Thorns);
                if (lvl > 0 && r.NextFloat() < .15f * lvl)
                {
                    if (attacker != null)
                    {
                        attacker.Attack(new DamageSource("thorns", user), lvl > 10 ? lvl - 10 : 1 + r.Next(4));
                        user.World.PlaySound("damage.thorns", attacker.X, attacker.Y, attacker.Z, .5f, 1f);
                    }
                    DamageEquipped(user, item, 3);
                }
                else DamageEquipped(user, item, 1);
            }
    }

    /// <summary>EnchantmentHelper.getEnchantedItem: pierwszy przedmiot wyposażenia z danym zaklęciem.</summary>
    public static ItemStack FirstWith(LivingEntity e, Ench id) => Equipment(e).FirstOrDefault(s => EnchantHelper.GetLevel(s, id) > 0);

    private static void DamageEquipped(LivingEntity e, ItemStack s, int amount)
    {
        if (s == null || e is Player { Creative: true }) return;
        if (!s.DamageItem(amount)) return;
        if (e is Player p) { for (int i = 0; i < 4; i++) if (p.Inventory.Armor[i] == s) p.Inventory.Armor[i] = null; p.Inventory.Dirty = true; }
        else if (e.HeldItem == s) e.HeldItem = null;
        e.World.PlaySound("break", e.X, e.Y, e.Z, .8f, .8f + e.World.Rand.NextFloat() * .4f);
    }

    /// <summary>EnchantmentDamage.onEntityDamaged: Zmora stawonogów daje spowolnienie IV na 1-1,5 s * poziom.</summary>
    public static void ApplyArthropod(LivingEntity user, Entity target)
    {
        if (target is not LivingEntity le || CreatureOf(le) != Creature.Arthropod) return;
        int lvl = EnchantHelper.GetLevel(Held(user), Ench.BaneOfArthropods);
        if (lvl > 0) le.AddEffect(Effect.Slowness, 20 + user.World.Rand.Next(10 * lvl), 3);
    }

    /// <summary>EnchantmentDurability.negateDamage: zbroja tylko w 40% przypadków korzysta z Niezniszczalności.</summary>
    public static int Unbreaking(ItemStack s, int amount, Random r)
    {
        int lvl = EnchantHelper.GetLevel(s, Ench.Unbreaking);
        if (lvl <= 0 || amount <= 0) return amount;
        bool armor = s.Item?.ArmorSlot >= 0;
        int negated = 0;
        for (int i = 0; i < amount; i++)
            if (!(armor && r.NextSingle() < .6f) && r.Next(lvl + 1) > 0) negated++;
        return amount - negated;
    }

    /// <summary>EntityPlayer.getToolDigEfficiency: Wydajność dodaje poziom^2 + 1 przy narzędziu właściwym dla bloku.</summary>
    public static float Efficiency(Player p, ItemStack tool, float speed)
    {
        int i = EnchantHelper.GetLevel(tool, Ench.Efficiency);
        return speed > 1f && i > 0 ? speed + i * i + 1 : speed;
    }

    public static bool AquaAffinity(LivingEntity e) => MaxLevel(e, Ench.AquaAffinity) > 0;

    /// <summary>EntityLivingBase.decreaseAirSupply: Oddychanie daje szansę, że powietrze nie spadnie.</summary>
    public static int DecreaseAir(LivingEntity e, int air)
    {
        int i = MaxLevel(e, Ench.Respiration);
        return i > 0 && e.World.Rand.Next(i + 1) > 0 ? air : air - 1;
    }

    /// <summary>Głębinowy wędrowiec: tarcie i przyspieszenie w wodzie (moveEntityWithHeading).</summary>
    public static (float friction, float accel) WaterMovement(LivingEntity e, float friction, float accel, float aiSpeed)
    {
        float f3 = Math.Min(3, MaxLevel(e, Ench.DepthStrider));
        if (!e.OnGround) f3 *= .5f;
        if (f3 > 0) { friction += (.54600006f - friction) * f3 / 3f; accel += (aiSpeed - accel) * f3 / 3f; }
        return (friction, accel);
    }
}
