using BlockCraft.Core;

namespace BlockCraft.Entities;

/// <summary>Część gracza potrzebna w sieci (moduł G).</summary>
public partial class Player
{
    /// <summary>Zdalny gracz na hoście: pozycję i ruch podaje jego klient, host nie liczy mu fizyki (Player.LivingUpdate).</summary>
    public bool NetPuppet;
    /// <summary>Host: ruchy od klienta nakładane w ticku encji (po PrevTick), żeby interpolacja i animacja nóg widziały przesunięcie.</summary>
    public Action NetPuppetMove;

    /// <summary>Host: przyjęcie ruchu od klienta. Obrażenia od upadku i wyczerpanie od skoku liczone jak na serwerze MC.</summary>
    public void NetApplyMove(double x, double y, double z, float yaw, float pitch, bool onGround)
    {
        double dy = y - Y;
        bool wasGround = OnGround;
        X = x; Y = y; Z = z;
        Yaw = yaw; Pitch = pitch;
        if (IsOnLadder() || InWater || Flying) FallDistance = 0;
        if (onGround) { if (FallDistance > 0) { OnFall(FallDistance); FallDistance = 0; } }
        else if (dy < 0) FallDistance -= (float)dy;
        if (wasGround && !onGround && dy > 0 && !Flying) AddExhaustion(Sprinting ? 0.8f : 0.2f);
        OnGround = onGround;
    }

    /// <summary>Klient: ostrzeżenie od hosta o śmierci (komunikat do ekranu śmierci).</summary>
    public void NetSetDeath(string message) { DeathMessage = message; }
}
