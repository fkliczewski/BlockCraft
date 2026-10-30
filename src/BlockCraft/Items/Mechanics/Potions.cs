using BlockCraft.Entities;

namespace BlockCraft.Items;

/// <summary>Metadane i operacje bitowe warzenia z PotionHelper 1.8.</summary>
public static class Potions
{
    private static readonly Dictionary<int, string> Ingredients = new()
    {
        [I.NetherWartItem] = "+4", [I.Sugar] = "-0+1-2-3&4-4+13", [I.GhastTear] = "+0-1-2-3&4-4+13",
        [I.SpiderEye] = "-0-1+2-3&4-4+13", [I.FermentedSpiderEye] = "-0+3-4+13", [I.GlisteringMelon] = "+0-1+2-3&4-4+13",
        [I.BlazePowder] = "+0-1-2+3&4-4+13", [I.MagmaCream] = "+0+1-2-3&4-4+13", [I.Redstone] = "-5+6-7",
        [I.GlowstoneDust] = "+5-6-7", [I.Gunpowder] = "+14&13-13", [I.GoldenCarrot] = "-0+1+2-3+13&4-4",
        [I.RawFish] = "+0-1+2+3+13&4-4", [I.RabbitFoot] = "+0+1-2+3&4-4+13"
    };
    public static bool IsIngredient(ItemStack s) => s != null && Ingredients.ContainsKey(s.Id) && (s.Id != I.RawFish || s.Damage == 3);
    public static bool Splash(int meta) => (meta & 16384) != 0;
    public static int Brew(int meta, ItemStack ingredient)
    {
        if (!IsIngredient(ingredient)) return meta;
        string formula = Ingredients[ingredient.Id]; int value = meta;
        for (int i = 0; i < formula.Length;)
        {
            char op = formula[i++];
            if (op == '&') continue;
            if (op is not ('+' or '-')) { while (i < formula.Length && char.IsDigit(formula[i])) i++; continue; }
            int bit = 0;
            while (i < formula.Length && char.IsDigit(formula[i])) bit = bit * 10 + formula[i++] - '0';
            int mask = 1 << bit;
            value = op switch { '+' => value | mask, '-' => value & ~mask, _ => value ^ mask };
        }
        value &= 32767;
        // Statyw nie zużywa składnika, jeżeli nie zmienia efektu ani rodzaju butelki.
        if (value == meta || (meta > 0 && SameEffects(meta, value) && !(Splash(value) && !Splash(meta)))) return meta;
        return value;
    }
    private static bool SameEffects(int a, int b)
    {
        var x = Effects(a); var y = Effects(b);
        return x.Count == y.Count && x.Zip(y).All(p => p.First.Id == p.Second.Id && p.First.Amplifier == p.Second.Amplifier && p.First.Duration == p.Second.Duration);
    }
    public static List<EffectInstance> Effects(int meta)
    {
        Effect effect = (meta & 15) switch
        {
            1 => Effect.Regeneration, 2 => Effect.Speed, 3 => Effect.FireResistance, 4 => Effect.Poison, 5 => Effect.InstantHealth,
            6 => Effect.NightVision, 8 => Effect.Weakness, 9 => Effect.Strength, 10 => Effect.Slowness, 11 => Effect.JumpBoost,
            12 => Effect.InstantDamage, 13 => Effect.WaterBreathing, 14 => Effect.Invisibility, _ => Effect.None
        };
        if (effect == Effect.None) return new();
        bool canAmplify = effect is Effect.Regeneration or Effect.Speed or Effect.Poison or Effect.InstantHealth or Effect.Strength or Effect.JumpBoost or Effect.InstantDamage;
        int amp = canAmplify && (meta & 32) != 0 ? 1 : 0;
        int duration = (meta & 64) != 0 ? 9600 : 3600;
        duration >>= amp;
        if (effect is Effect.Poison or Effect.Regeneration) duration /= 4;
        if (effect is Effect.Weakness or Effect.Slowness) duration /= 2;
        if (Splash(meta)) duration = (int)(duration * .75 + .5);
        if (EffectLogic.Instant(effect)) duration = 1;
        return new() { new(effect, duration, amp) };
    }
    public static List<EffectInstance> Effects(ItemStack stack)
    {
        var custom = stack.Tag?.GetList("CustomPotionEffects");
        if (custom == null) return Effects(stack.Damage);
        return custom.OfType<Core.Nbt>().Select(n => new EffectInstance((Effect)n.GetInt("Id"), n.GetInt("Duration"), n.GetInt("Amplifier"))).ToList();
    }
    public static string Name(ItemStack s)
    {
        var effects = Effects(s); string name = effects.Count == 0 ? (s.Damage == 0 ? "Butelka wody" : (s.Damage & 16) != 0 ? "Niezręczna mikstura" : (s.Damage & 32) != 0 ? "Gęsta mikstura" : "Zwyczajna mikstura") : "Mikstura: " + EffectLogic.Name(effects[0].Id);
        return Splash(s.Damage) ? "Rozpryskowa " + name.ToLowerInvariant() : name;
    }
    public static uint Color(ItemStack s)
    {
        int c = EffectLogic.MixedColor(Effects(s));
        return Render.TextureGen.Hex((uint)c);
    }
    public static int[] CreativeVariants() => new[] { 0, 16, 32, 64 }.Concat(new[] { 1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14 }
        .SelectMany(t => new[] { t | 8192, t | 8192 | 32, t | 8192 | 64 }).Where(m => (m & 32) == 0 || Effects(m)[0].Amplifier > 0)
        .SelectMany(m => new[] { m, (m & ~8192) | 16384 })).Distinct().ToArray();
}
