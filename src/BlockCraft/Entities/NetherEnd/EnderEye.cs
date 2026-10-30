using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Lecące oko Endu (EntityEnderEye z MC 1.8): leci w stronę twierdzy, po 80 tickach spada jako item (80%) albo pęka.</summary>
public sealed class EnderEyeEntity : Entity
{
    public double TargetX, TargetY, TargetZ;
    public int DespawnTimer;
    public bool ShatterOrDrop;
    public EnderEyeEntity(World.World w) : base(w) { Width = Height = .25f; NoGravity = true; NoClip = true; }
    public override string TypeKey => "eye_of_ender_signal";

    /// <summary>MC 1.8 moveTowards: cel dalej niż 12 bloków jest skracany do 12 bloków, 8 bloków wyżej.</summary>
    public void MoveTowards(double tx, int ty, double tz)
    {
        double dx = tx - X, dz = tz - Z; double d = Math.Sqrt(dx * dx + dz * dz);
        if (d > 12) { TargetX = X + dx / d * 12; TargetZ = Z + dz / d * 12; TargetY = Y + 8; }
        else { TargetX = tx; TargetY = ty; TargetZ = tz; }
        DespawnTimer = 0;
        ShatterOrDrop = World.Rand.Next(5) > 0;
    }

    public override void Tick()
    {
        Age++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        X += MotionX; Y += MotionY; Z += MotionZ;
        double h = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
        Yaw = (float)(Math.Atan2(MotionX, MotionZ) * 180 / Math.PI);
        Pitch = (float)(Math.Atan2(MotionY, h) * 180 / Math.PI);
        double tdx = TargetX - X, tdz = TargetZ - Z;
        double dist = Math.Sqrt(tdx * tdx + tdz * tdz);
        double angle = Math.Atan2(tdz, tdx);
        double speed = h + (dist - h) * .0025;
        if (dist < 1) { speed *= .8; MotionY *= .8; }
        MotionX = Math.Cos(angle) * speed; MotionZ = Math.Sin(angle) * speed;
        MotionY += ((Y < TargetY ? 1 : -1) - MotionY) * .015;
        World.SpawnParticles(ParticleKind.Portal, X - MotionX * .25, Y - MotionY * .25 - .5, Z - MotionZ * .25, 1);
        if (++DespawnTimer > 80)
        {
            Kill();
            if (ShatterOrDrop) World.DropItem(X, Y, Z, new ItemStack(I.EyeOfEnder), false);
            else
            {
                // playAuxSFX 2003: cząsteczki pękającego oka i dźwięk szkła
                World.SpawnParticles(ParticleKind.BlockBreak, X, Y, Z, 8, -I.EyeOfEnder);
                World.SpawnParticles(ParticleKind.Portal, X, Y, Z, 16);
                World.PlaySound("dig.glass", X, Y, Z, 1f, World.Rand.NextFloat() * .1f + .9f);
            }
        }
    }

}
