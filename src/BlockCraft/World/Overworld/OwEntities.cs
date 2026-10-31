using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Entities;

/// <summary>Śnieżka (EntitySnowball): 0 obrażeń (3 dla płomyka), odrzut, cząsteczki.</summary>
public sealed class SnowballEntity : Entity
{
    public LivingEntity Thrower;
    public SnowballEntity(World.World w) : base(w) { Width = 0.25f; Height = 0.25f; StepHeight = 0; }
    public SnowballEntity(World.World w, LivingEntity thrower) : this(w)
    {
        Thrower = thrower;
        SetPositionAndPrev(thrower.X, thrower.Y + thrower.EyeHeight - 0.1, thrower.Z);
        float yr = thrower.Yaw * MathUtil.Deg2Rad, pr = thrower.Pitch * MathUtil.Deg2Rad;
        double mx = -MathF.Sin(yr) * MathF.Cos(pr), mz = MathF.Cos(yr) * MathF.Cos(pr), my = -MathF.Sin(pr);
        double len = Math.Sqrt(mx * mx + my * my + mz * mz);
        // EntityThrowable.setThrowableHeading: prędkość 1.5, rozrzut 1.0
        mx = mx / len + w.Rand.NextGaussian() * 0.0075; my = my / len + w.Rand.NextGaussian() * 0.0075; mz = mz / len + w.Rand.NextGaussian() * 0.0075;
        MotionX = mx * 1.5; MotionY = my * 1.5; MotionZ = mz * 1.5;
    }
    public override string TypeKey => "snowball";
    public override void Tick()
    {
        Age++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        double len = Math.Sqrt(MotionX * MotionX + MotionY * MotionY + MotionZ * MotionZ);
        var hit = len > 0 ? World.RayCast(X, Y, Z, MotionX / len, MotionY / len, MotionZ / len, len) : default;
        Entity target = null;
        if (len > 0)
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Expand(MotionX, MotionY, MotionZ).Grow(1), this))
            {
                if (!e.CanBeAttacked || (e == Thrower && Age < 5)) continue;
                if (e.BoundingBox.Grow(0.3).RayIntersect(X, Y, Z, MotionX / len, MotionY / len, MotionZ / len, out double t, out _) && t <= (hit.Hit ? hit.Distance : len)) target = e;
            }
        if (target != null || hit.Hit)
        {
            if (hit.Hit && target == null) { X = hit.HitX; Y = hit.HitY; Z = hit.HitZ; }
            else if (target != null) { X = target.X; Y = target.Y + 0.5; Z = target.Z; }
            if (!World.IsRemote)
            {
                if (target is LivingEntity le) le.Attack(DamageSource.Mob(Thrower ?? (Entity)this), target.TypeKey == "blaze" ? 3f : 0f);
                World.SpawnParticles(ParticleKind.BlockBreak, X, Y, Z, 8, -I.Snowball);
                Kill();
            }
            return;
        }
        X += MotionX; Y += MotionY; Z += MotionZ;
        bool inWater = World.GetBlock((int)Math.Floor(X), (int)Math.Floor(Y), (int)Math.Floor(Z)) == B.Water;
        double drag = inWater ? 0.8 : 0.99;
        MotionX *= drag; MotionY *= drag; MotionZ *= drag; MotionY -= 0.03;
        if (Y < -64 || Age > 1200) Kill();
    }
}

public static partial class Registries
{
    static partial void EntitiesOverworld()
    {
        Entity("snowball", w => new SnowballEntity(w));
        EntityRenderer.Custom[typeof(SnowballEntity)] = (r, e, pt, pos, cam)
            => r.RenderBillboardSprite(TextureGen.Layer("item_snowball"), pos + new Vector3(0, 0.1f, 0), 0.25f, Vector4.One, cam);
    }
}
