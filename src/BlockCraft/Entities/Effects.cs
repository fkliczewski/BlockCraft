namespace BlockCraft.Entities;

/// <summary>Efekty statusu MC 1.8 (numeracja jak w MC: Potion.potionTypes).</summary>
public enum Effect
{
    None = 0, Speed = 1, Slowness = 2, Haste = 3, MiningFatigue = 4, Strength = 5, InstantHealth = 6, InstantDamage = 7,
    JumpBoost = 8, Nausea = 9, Regeneration = 10, Resistance = 11, FireResistance = 12, WaterBreathing = 13, Invisibility = 14,
    Blindness = 15, NightVision = 16, Hunger = 17, Weakness = 18, Poison = 19, Wither = 20, HealthBoost = 21, Absorption = 22, Saturation = 23
}

public sealed class EffectInstance
{
    public Effect Id;
    public int Duration;      // ticki
    public int Amplifier;     // 0 = poziom I
    public bool Ambient;      // z beaconu
    public bool ShowParticles = true;
    public EffectInstance(Effect id, int duration, int amplifier = 0, bool ambient = false, bool particles = true)
    { Id = id; Duration = duration; Amplifier = amplifier; Ambient = ambient; ShowParticles = particles; }
    public EffectInstance Copy() => new(Id, Duration, Amplifier, Ambient, ShowParticles);
}

/// <summary>
/// Logika efektów. Działanie każdego efektu (obrażenia trucizny, regeneracja, prędkość, kolory cząsteczek, ikony w GUI)
/// implementuje moduł ME w metodach częściowych. Inne moduły tylko wołają LivingEntity.AddEffect/HasEffect.
/// </summary>
public static partial class EffectLogic
{
    /// <summary>Wołane co tick dla każdego aktywnego efektu, przed zmniejszeniem czasu.</summary>
    static partial void OnTick(LivingEntity e, EffectInstance ef);
    /// <summary>Wołane po dodaniu lub wzmocnieniu efektu (np. Health Boost, natychmiastowe leczenie).</summary>
    static partial void OnApplied(LivingEntity e, EffectInstance ef);
    /// <summary>Wołane po wygaśnięciu lub usunięciu efektu.</summary>
    static partial void OnRemoved(LivingEntity e, EffectInstance ef);

    public static void Tick(LivingEntity e, EffectInstance ef) => OnTick(e, ef);
    public static void Applied(LivingEntity e, EffectInstance ef) => OnApplied(e, ef);
    public static void Removed(LivingEntity e, EffectInstance ef) => OnRemoved(e, ef);
}
