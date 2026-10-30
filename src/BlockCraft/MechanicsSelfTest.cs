using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestMechanicsEffects()
    {
        var w = MakeWorld(21, WorldType.Superflat, 0);
        var p = new Player(w); p.SetPosition(8, 80, 8);
        p.AddEffect(Effect.Speed, 200, 1);
        Check(Math.Abs(p.GetSpeed() - .14f) < .0001, "Szybkość II: +40%");
        p.AddEffect(Effect.HealthBoost, 200, 1); p.AddEffect(Effect.HealthBoost, 300, 1);
        Check(p.MaxHealth == 28, "Zwiększenie zdrowia nie kumuluje się przy odświeżeniu");
        p.AddEffect(Effect.Absorption, 200); p.AddEffect(Effect.Absorption, 300);
        Check(p.Absorption == 4, "Pochłanianie nie kumuluje się");
        p.Health = 25; p.Absorption = 2;
        var data = p.ToData(); var q = new Player(w); q.FromData(data);
        Check(q.MaxHealth == 28 && q.Health == 25 && q.Absorption == 2 && q.HasEffect(Effect.Speed), "Zapis efektów i częściowo zużytego pochłaniania");
        q.ClearEffects(); Check(q.MaxHealth == 20 && q.Health == 20 && q.Absorption == 0, "Mleko usuwa bonusy efektów");
        p.ClearEffects(); p.Health = 1;
        EffectLogic.Tick(p, new EffectInstance(Effect.Poison, 25)); Check(p.Health == 1, "Trucizna nie zabija");
        p.Health = 10; EffectLogic.ApplyInstant(p, Effect.InstantHealth, 1); Check(p.Health == 18, "Leczenie II przywraca 8 HP");
        p.ClearEffects(); p.Eat(new ItemStack(I.GoldenApple, 1, 1));
        Check(p.EffectLevel(Effect.Regeneration) == 4 && p.Effects[Effect.Regeneration].Duration == 600 && p.HasEffect(Effect.FireResistance), "Zaklęte złote jabłko 1.8");
        p.ClearEffects(); p.Eat(new ItemStack(I.SpiderEye)); Check(p.HasEffect(Effect.Poison), "Oko pająka zatruwa");
        p.ClearEffects(); p.AddEffect(Effect.Resistance, 100, 4); p.Health = 20; p.HurtResistantTime = 0;
        p.Attack(DamageSource.Generic, 10); Check(p.Health == 20, "Odporność V blokuje obrażenia");
        p.HurtResistantTime = 0; p.Attack(DamageSource.Void, 4); Check(p.Health == 16, "Odporność nie chroni przed pustką");
        p.ClearEffects(); p.AddEffect(Effect.MiningFatigue, 100, 2);
        Check(Math.Abs(EffectLogic.Mining(p) - .0027f) < .000001, "Zmęczenie III: współczynnik 0.0027");
    }
}
