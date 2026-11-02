using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Kula ognia (EntityFireball z MC 1.8): leci z przyspieszeniem, tłumienie 0.95 na tick.</summary>
public abstract class FireballEntity : Entity
{
    public LivingEntity Shooter;
    public double AccelX, AccelY, AccelZ;
    protected int TicksInAir;
    protected FireballEntity(World.World w) : base(w) { Width = Height = 1f; NoGravity = true; NoClip = true; }

    /// <summary>Strzał z pozycji strzelca w kierunku (dx, dy, dz); rozrzut jak w konstruktorze EntityFireball z 1.8.</summary>
    public void Launch(LivingEntity shooter, double x, double y, double z, double dx, double dy, double dz, double spread)
    {
        Shooter = shooter;
        SetPositionAndPrev(x, y, z);
        var r = World.Rand;
        dx += r.NextGaussian() * spread; dy += r.NextGaussian() * spread; dz += r.NextGaussian() * spread;
        double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (d < 1e-6) d = 1;
        AccelX = dx / d * .1; AccelY = dy / d * .1; AccelZ = dz / d * .1;
    }

    public static DamageSource FireballDamage(Entity fireball, Entity shooter) => new("fireball", fireball, shooter ?? fireball) { IsFire = true };

    protected virtual float MotionFactor => .95f;
    protected abstract void OnImpact(Entity hit, World.World.HitResult block);

    public override void Tick()
    {
        Age++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        if (Shooter != null && Shooter.Removed || !World.IsLoadedAt(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Z)) || Age > 1200) { Kill(); return; }
        TicksInAir++;
        double len = Math.Sqrt(MotionX * MotionX + MotionY * MotionY + MotionZ * MotionZ);
        double cy = Y + Height / 2;
        var hit = len > 0 ? World.RayCast(X, cy, Z, MotionX / len, MotionY / len, MotionZ / len, len) : default;
        Entity target = null; double best = hit.Hit ? hit.Distance : len;
        if (len > 0)
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Expand(MotionX, MotionY, MotionZ).Grow(1), this))
            {
                if (!e.CanBeCollidedWith || e is FireballEntity || (e == Shooter && TicksInAir < 25)) continue;
                if (e.BoundingBox.Grow(.3).RayIntersect(X, cy, Z, MotionX / len, MotionY / len, MotionZ / len, out double t, out _) && t <= best) { best = t; target = e; }
            }
        if (target != null || hit.Hit)
        {
            if (target == null) { X = hit.HitX; Y = hit.HitY - Height / 2; Z = hit.HitZ; }
            OnImpact(target, hit);
            if (Removed) return;
        }
        X += MotionX; Y += MotionY; Z += MotionZ;
        float f = MotionFactor;
        if (World.IsAnyLiquid(BoundingBox, false)) { f = .8f; World.SpawnParticles(ParticleKind.Bubble, X, Y + Height / 2, Z, 4); }
        MotionX += AccelX; MotionY += AccelY; MotionZ += AccelZ;
        MotionX *= f; MotionY *= f; MotionZ *= f;
        World.SpawnParticles(ParticleKind.Smoke, X, Y + Height / 2 + .5, Z, 1);
    }
}

/// <summary>Duża kula ognia ghasta (EntityLargeFireball): 6 obrażeń, eksplozja mocy 1 z podpaleniem, odbijalna uderzeniem.</summary>
public sealed class LargeFireballEntity : FireballEntity
{
    public int ExplosionPower = 1;
    public LargeFireballEntity(World.World w) : base(w) { }
    public override string TypeKey => "fireball";
    public override bool CanBeAttacked => !Removed;
    public override bool CanBeCollidedWith => !Removed;

    protected override void OnImpact(Entity hit, World.World.HitResult block)
    {
        if (World.IsRemote) return;
        if (hit is LivingEntity le) le.Attack(FireballDamage(this, Shooter), 6f);
        NetherMobUtil.Explode(World, this, X, Y + Height / 2, Z, ExplosionPower, true, World.Rules.MobGriefing);
        Kill();
    }

    /// <summary>MC 1.8 EntityFireball.attackEntityFrom: uderzenie odbija kulę w kierunku patrzenia atakującego.</summary>
    public override bool AttackedByPlayer(Player p) { Deflect(p); return true; }
    public void Deflect(LivingEntity by)
    {
        var (lx, ly, lz) = by.Look();
        MotionX = lx; MotionY = ly; MotionZ = lz;
        AccelX = lx * .1; AccelY = ly * .1; AccelZ = lz * .1;
        Shooter = by; TicksInAir = 0;
    }
}

/// <summary>Mała kula ognia płomyka (EntitySmallFireball): 5 obrażeń i podpalenie na 5 s, na bloku stawia ogień.</summary>
public sealed class SmallFireballEntity : FireballEntity
{
    public SmallFireballEntity(World.World w) : base(w) { Width = Height = .3125f; }
    public override string TypeKey => "small_fireball";

    protected override void OnImpact(Entity hit, World.World.HitResult block)
    {
        if (World.IsRemote) return;
        if (hit != null)
        {
            if (hit is LivingEntity le && le.Attack(FireballDamage(this, Shooter), 5f) && !le.ImmuneToFire) le.SetFire(5);
        }
        else if (block.Hit && World.Rules.MobGriefing)
        {
            int fx = block.X + Dir.X[block.Face], fy = block.Y + Dir.Y[block.Face], fz = block.Z + Dir.Z[block.Face];
            if (World.GetBlock(fx, fy, fz) == B.Air) World.SetBlock(fx, fy, fz, B.Fire, 0);
        }
        Kill();
    }
}

public static class NetherMobUtil
{
    /// <summary>Eksplozja z opcjonalnym podpaleniem (World.newExplosion z flaming=true: 1/3 pustych pól nad pełnym blokiem).</summary>
    public static void Explode(World.World w, Entity source, double x, double y, double z, float power, bool flaming, bool breakBlocks)
    {
        Explosion.Explode(w, source, x, y, z, power, breakBlocks);
        if (!flaming || w.IsRemote) return;
        int r = (int)Math.Ceiling(power * 1.3);
        for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++) for (int dz = -r; dz <= r; dz++)
        {
            int bx = MathUtil.FloorToInt(x) + dx, by = MathUtil.FloorToInt(y) + dy, bz = MathUtil.FloorToInt(z) + dz;
            if (dx * dx + dy * dy + dz * dz > r * r || w.GetBlock(bx, by, bz) != B.Air) continue;
            var below = Block.Get(w.GetBlock(bx, by - 1, bz));
            if (below.Opaque && w.Rand.Next(3) == 0) w.SetBlock(bx, by, bz, B.Fire, 0);
        }
    }

    /// <summary>Poziom grabieży broni ostatniego atakującego gracza.</summary>
    public static int Looting(LivingEntity e) => e.LastAttacker is Player p ? Items.EnchantHelper.GetLevel(p.Inventory.Selected, Items.Ench.Looting) : 0;
}
