namespace BlockCraft.Entities;

/// <summary>Pasek życia bossa (BossStatus z MC 1.8). Boss ustawia go, gdy jest blisko gracza; znika po 100 tickach bez odświeżenia.</summary>
public static class BossStatus
{
    public static string BossName;
    public static float HealthScale;
    public static int StatusBarTime;
    public static bool HasColorModifier;
    public static bool Visible => BossName != null && StatusBarTime > 0;

    public static void Set(string name, float health, float maxHealth, bool colorModifier)
    {
        BossName = name; HealthScale = Math.Clamp(health / maxHealth, 0, 1); StatusBarTime = 100; HasColorModifier = colorModifier;
    }

    public static void Tick() { if (StatusBarTime > 0 && --StatusBarTime == 0) BossName = null; }
}
