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
        Check(Potions.Brew(0, new ItemStack(I.NetherWartItem)) == 16, "Brodawka zmienia wodę w niezręczną miksturę");
        Check(Potions.Brew(16, new ItemStack(I.Sugar)) == 8194, "Cukier warzy szybkość");
        Check(Potions.Brew(8194, new ItemStack(I.Redstone)) == 8258, "Redstone przedłuża miksturę szybkości");
        Check(Potions.Brew(8194, new ItemStack(I.GlowstoneDust)) == 8226, "Glowstone wzmacnia miksturę szybkości");
        Check(Potions.Brew(8194, new ItemStack(I.Gunpowder)) == 16386, "Proch zmienia miksturę w rozpryskową i usuwa bit 13");
        var brewWorld = MakeWorld(22, WorldType.Superflat, 0);
        var stand = new BrewingStandTile { X = 8, Y = 80, Z = 8 };
        stand.Items[0] = new ItemStack(I.Potion); stand.Items[3] = new ItemStack(I.NetherWartItem);
        stand.Tick(brewWorld); // rozpoczęcie warzenia ustawia licznik na 400
        for (int i = 0; i < 400; i++) stand.Tick(brewWorld);
        Check(stand.Items[0].Damage == 16 && stand.Items[3] == null, "Statyw warzy w 400 ticków bez paliwa");
        p.ClearEffects(); p.AddEffect(Effect.MiningFatigue, 100, 2);
        Check(Math.Abs(EffectLogic.Mining(p) - .0027f) < .000001, "Zmęczenie III: współczynnik 0.0027");
    }
}
