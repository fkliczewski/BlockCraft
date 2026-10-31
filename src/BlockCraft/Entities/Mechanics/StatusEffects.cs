using BlockCraft.Items;
using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

public abstract partial class LivingEntity
{
    public float Absorption;
    internal float EffectHealthBonus;
    public virtual bool IsUndead => TypeKey is "zombie" or "skeleton" or "wither_skeleton" or "pig_zombie" or "zombie_pigman" or "wither";
}

public static partial class EffectLogic
{
    public static readonly int[] Colors = { 0, 8171462, 5926017, 14270531, 4866583, 9643043, 16262179, 4393481,
        2293580, 5578058, 13458603, 10044730, 14981690, 3035801, 8356754, 2039587, 2039713, 5797459,
        4738376, 5149489, 3484199, 16284963, 2445989, 16262179 };
    private static readonly string[] Names = { "", "Szybkość", "Spowolnienie", "Pośpiech", "Zmęczenie", "Siła", "Natychmiastowe zdrowie",
        "Natychmiastowe obrażenia", "Zwiększony skok", "Nudności", "Regeneracja", "Odporność", "Odporność na ogień", "Oddychanie pod wodą",
        "Niewidzialność", "Ślepota", "Noktowizja", "Głód", "Osłabienie", "Trucizna", "Wither", "Zwiększone zdrowie", "Pochłanianie", "Nasycenie" };
    public static string Name(Effect e) => (int)e > 0 && (int)e < Names.Length ? Names[(int)e] : e.ToString();
    public static string Time(EffectInstance e) => $"{e.Duration / 1200}:{e.Duration / 20 % 60:00}";
    public static float Movement(LivingEntity e) => Math.Max(0, (1 + .2f * (e.EffectLevel(Effect.Speed) + 1)) * (1 - .15f * (e.EffectLevel(Effect.Slowness) + 1)));
    public static float Mining(LivingEntity e)
    {
        float f = 1 + .2f * (e.EffectLevel(Effect.Haste) + 1);
        return f * (e.EffectLevel(Effect.MiningFatigue) switch { -1 => 1, 0 => .3f, 1 => .09f, 2 => .0027f, _ => .00081f });
    }
    public static float Melee(LivingEntity e, float damage) => Math.Max(0, (damage - .5f * (e.EffectLevel(Effect.Weakness) + 1)) * (1 + 1.3f * (e.EffectLevel(Effect.Strength) + 1)));
    public static bool Instant(Effect e) => e is Effect.InstantHealth or Effect.InstantDamage or Effect.Saturation;
    public static void ApplyInstant(LivingEntity e, Effect id, int amplifier, double strength = 1, Entity source = null)
    {
        amplifier = Math.Clamp(amplifier, 0, 20);
        if (id == Effect.Saturation && e is Player p)
        {
            p.FoodLevel = Math.Min(20, p.FoodLevel + amplifier + 1);
            p.Saturation = Math.Min(p.FoodLevel, p.Saturation + (amplifier + 1) * 2);
        }
        else if ((id == Effect.InstantHealth && !e.IsUndead) || (id == Effect.InstantDamage && e.IsUndead)) e.Heal((int)(strength * (4 << amplifier) + .5));
        else if (id is Effect.InstantHealth or Effect.InstantDamage) e.Attack(new DamageSource("magic", source) { BypassArmor = true }, (int)(strength * (6 << amplifier) + .5));
    }
    static partial void OnTick(LivingEntity e, EffectInstance ef)
    {
        int interval = ef.Id switch { Effect.Regeneration => 50, Effect.Poison => 25, Effect.Wither => 40, _ => 0 };
        interval = interval >> Math.Clamp(ef.Amplifier, 0, 30);
        if (interval == 0 || ef.Duration % interval == 0)
        {
            if (ef.Id == Effect.Regeneration) e.Heal(1);
            if (ef.Id == Effect.Poison && e.Health > 1) e.Attack(new DamageSource("magic") { BypassArmor = true }, Math.Min(1, e.Health - 1));
            if (ef.Id == Effect.Wither) e.Attack(new DamageSource("wither") { BypassArmor = true }, 1);
        }
        if (ef.Id == Effect.Hunger && e is Player p) p.AddExhaustion(.025f * (ef.Amplifier + 1));
        if (Instant(ef.Id)) ApplyInstant(e, ef.Id, ef.Amplifier);
    }
    static partial void OnApplied(LivingEntity e, EffectInstance ef)
    {
        if (ef.Id == Effect.HealthBoost)
        {
            e.MaxHealth -= e.EffectHealthBonus;
            e.EffectHealthBonus = 4 * (ef.Amplifier + 1);
            e.MaxHealth += e.EffectHealthBonus;
        }
        if (ef.Id == Effect.Absorption) e.Absorption += 4 * (ef.Amplifier + 1);
    }
    static partial void OnRemoved(LivingEntity e, EffectInstance ef)
    {
        if (ef.Id == Effect.HealthBoost)
        {
            e.MaxHealth -= e.EffectHealthBonus; e.EffectHealthBonus = 0;
            e.Health = Math.Min(e.Health, e.MaxHealth);
        }
        if (ef.Id == Effect.Absorption) e.Absorption = Math.Max(0, e.Absorption - 4 * (ef.Amplifier + 1));
    }
    public static int MixedColor(IEnumerable<EffectInstance> effects)
    {
        int r = 0, g = 0, b = 0, n = 0;
        foreach (var ef in effects.Where(e => e.ShowParticles && (int)e.Id > 0 && (int)e.Id < Colors.Length))
        {
            int weight = ef.Amplifier + 1, color = Colors[(int)ef.Id]; n += weight;
            r += (color >> 16 & 255) * weight; g += (color >> 8 & 255) * weight; b += (color & 255) * weight;
        }
        return n == 0 ? 0x385DC6 : (r / n << 16) | (g / n << 8) | b / n;
    }
    public static void Particles(LivingEntity e)
    {
        if (!e.Effects.Values.Any(f => f.ShowParticles)) return;
        int chance = e.HasEffect(Effect.Invisibility) ? 15 : 2;
        if (e.World.Rand.Next(chance) != 0 || e.Effects.Values.All(f => f.Ambient) && e.World.Rand.Next(5) != 0) return;
        e.World.SpawnParticles(ParticleKind.Spell, e.X, e.Y + e.World.Rand.NextDouble() * e.Height, e.Z, 1, MixedColor(e.Effects.Values));
    }
}

public partial class Player
{
    public void Eat(ItemStack stack)
    {
        var food = stack.Item; if (food == null) return;
        int hunger = food.Hunger; float saturation = food.SaturationMod;
        if (stack.Id == I.RawFish) { hunger = stack.Damage is 2 or 3 ? 1 : 2; saturation = stack.Damage is 2 or 3 ? .1f : .1f; }
        if (stack.Id == I.CookedFish) { hunger = stack.Damage == 1 ? 6 : 5; saturation = stack.Damage == 1 ? .8f : .6f; }
        FoodLevel = Math.Min(20, FoodLevel + hunger);
        Saturation = Math.Min(FoodLevel, Saturation + hunger * saturation * 2);
        if (food.Id == I.RawChicken && World.Rand.NextFloat() < .3f || food.Id == I.RottenFlesh && World.Rand.NextFloat() < .8f) AddEffect(Effect.Hunger, 600);
        if (food.Id == I.SpiderEye) AddEffect(Effect.Poison, 100);
        if (food.Id == I.PoisonousPotato && World.Rand.NextFloat() < .6f) AddEffect(Effect.Poison, 100);
        if (food.Id == I.RawFish && stack.Damage == 3) { AddEffect(Effect.Poison, 1200, 3); AddEffect(Effect.Hunger, 300, 2); AddEffect(Effect.Nausea, 300, 1); }
        if (food.Id == I.GoldenApple)
        {
            AddEffect(Effect.Absorption, 2400);
            AddEffect(Effect.Regeneration, stack.Damage == 1 ? 600 : 100, stack.Damage == 1 ? 4 : 1);
            if (stack.Damage == 1) { AddEffect(Effect.Resistance, 6000); AddEffect(Effect.FireResistance, 6000); }
        }
    }
}
