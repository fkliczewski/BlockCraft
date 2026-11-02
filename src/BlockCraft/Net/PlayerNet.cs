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
        double dy = y - Y, dxz = (x - X) * (x - X) + (z - Z) * (z - Z);
        bool wasGround = OnGround;
        X = x; Y = y; Z = z;
        Yaw = yaw; Pitch = pitch;
        // blok pod stopami jak w Entity.MoveWithCollision (floor(posY - 0.2)): mnożnik upadku (szlam) i chodzenie (ruda redstone)
        int fx = MathUtil.FloorToInt(X), fy = MathUtil.FloorToInt(Y - 0.2), fz = MathUtil.FloorToInt(Z);
        var feet = BlockCraft.World.Block.Get(World.GetBlock(fx, fy, fz));
        if (IsOnLadder() || InWater || Flying) FallDistance = 0;
        if (onGround)
        {
            if (FallDistance > 0) { OnFall(FallDistance * feet.FallDamageMultiplier(World, this)); FallDistance = 0; }
            if (!Sneaking && dxz > 1e-6) feet.OnEntityWalk(World, fx, fy, fz, this);
        }
        else if (dy < 0) FallDistance -= (float)dy;
        if (wasGround && !onGround && dy > 0 && !Flying) AddExhaustion(Sprinting ? 0.8f : 0.2f);
        OnGround = onGround;
        if (!Spectator) NetBlockContacts();
    }

    /// <summary>Host: kontakt zdalnego gracza z blokami (płyty naciskowe, linka, kaktus, pajęczyna, tory), jak doBlockCollisions
    /// w moveEntity; zdalny gracz nie przechodzi przez fizykę hosta.</summary>
    private void NetBlockContacts()
    {
        var bb = BoundingBox.Grow(0.001);
        int x0 = MathUtil.FloorToInt(bb.MinX), x1 = MathUtil.FloorToInt(bb.MaxX);
        int y0 = MathUtil.FloorToInt(bb.MinY), y1 = MathUtil.FloorToInt(bb.MaxY);
        int z0 = MathUtil.FloorToInt(bb.MinZ), z1 = MathUtil.FloorToInt(bb.MaxZ);
        for (int bx = x0; bx <= x1; bx++)
            for (int by = y0; by <= y1; by++)
                for (int bz = z0; bz <= z1; bz++)
                {
                    int id = World.GetBlock(bx, by, bz);
                    if (id == 0) continue;
                    if (id == BlockCraft.World.B.Cactus && !new AABB(bx + 1 / 16.0, by, bz + 1 / 16.0, bx + 15 / 16.0, by + 1, bz + 15 / 16.0).Intersects(bb)) continue;
                    BlockCraft.World.Block.ById[id].OnEntityCollide(World, bx, by, bz, this);
                }
    }

    /// <summary>Klient: ostrzeżenie od hosta o śmierci (komunikat do ekranu śmierci).</summary>
    public void NetSetDeath(string message) { DeathMessage = message; }
}
