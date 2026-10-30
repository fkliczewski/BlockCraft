using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public abstract class LivingEntity : Entity
{
    public float Health, MaxHealth = 20;
    public int HurtTime, MaxHurtTime = 10, DeathTime, HurtResistantTime;
    public float LastDamage;
    public float AttackedAtYaw;
    public float LimbSwing, LimbSwingAmount, PrevLimbSwingAmount;
    public float BodyYaw, PrevBodyYaw, HeadYaw, PrevHeadYaw;
    public float SwingProgress, PrevSwingProgress; public int SwingTicks; public bool IsSwinging;
    public float MoveForward, MoveStrafe;
    public bool IsJumping;
    public int JumpTicks;
    public float AiMoveSpeed = 0.1f;
    public bool Sprinting, Sneaking;
    public int Air = 300;
    public Entity LastAttacker; public int LastAttackerTime;
    public bool KilledByPlayer; public int RecentlyHitByPlayer;
    public ItemStack HeldItem;
    public virtual string DisplayName => TypeKey;
    public virtual bool CanBreatheUnderwater => false;
    public virtual bool ImmuneToFire => false;
    public virtual int ArmorValue => 0;
    public virtual int XpValue => 0;

    protected LivingEntity(World.World w) : base(w) { StepHeight = 0.6f; }

    public override bool CanBeAttacked => !Dead;
    public override bool CanBeCollidedWith => !Dead;
    protected override bool IsSneakingForEdge => Sneaking;

    public override void PrevTick()
    {
        base.PrevTick();
        PrevLimbSwingAmount = LimbSwingAmount;
        PrevBodyYaw = BodyYaw; PrevHeadYaw = HeadYaw;
        PrevSwingProgress = SwingProgress;
    }

    public void SwingArm()
    {
        if (!IsSwinging || SwingTicks >= 3 || SwingTicks < 0) { SwingTicks = -1; IsSwinging = true; }
    }

    private void UpdateSwing()
    {
        const int len = 6;
        if (IsSwinging) { SwingTicks++; if (SwingTicks >= len) { SwingTicks = 0; IsSwinging = false; } }
        else SwingTicks = 0;
        SwingProgress = (float)SwingTicks / len;
    }

    // ---- efekty statusu (mikstury) ----
    public readonly Dictionary<Effect, EffectInstance> Effects = new();
    public bool HasEffect(Effect id) => Effects.ContainsKey(id);
    /// <summary>Poziom efektu (0 = I) albo -1 gdy brak.</summary>
    public int EffectLevel(Effect id) => Effects.TryGetValue(id, out var e) ? e.Amplifier : -1;
    public virtual bool CanHaveEffect(Effect id) => true;
    public void AddEffect(EffectInstance ef)
    {
        if (!CanHaveEffect(ef.Id)) return;
        if (Effects.TryGetValue(ef.Id, out var cur))
        {
            // jak w MC: wyższy poziom nadpisuje, ten sam poziom przedłuża
            if (ef.Amplifier > cur.Amplifier) { cur.Amplifier = ef.Amplifier; cur.Duration = ef.Duration; }
            else if (ef.Amplifier == cur.Amplifier && cur.Duration < ef.Duration) cur.Duration = ef.Duration;
            cur.Ambient = ef.Ambient; cur.ShowParticles = ef.ShowParticles;
            EffectLogic.Applied(this, cur);
        }
        else { Effects[ef.Id] = ef; EffectLogic.Applied(this, ef); }
    }
    public void AddEffect(Effect id, int duration, int amplifier = 0) => AddEffect(new EffectInstance(id, duration, amplifier));
    public void RemoveEffect(Effect id) { if (Effects.Remove(id, out var e)) EffectLogic.Removed(this, e); }
    public void ClearEffects() { foreach (var id in Effects.Keys.ToList()) RemoveEffect(id); }
    private void TickEffects()
    {
        if (Effects.Count == 0) return;
        foreach (var ef in Effects.Values.ToList())
        {
            EffectLogic.Tick(this, ef);
            if (--ef.Duration <= 0) RemoveEffect(ef.Id);
        }
    }

    public override void Tick()
    {
        base.Tick();
        UpdateSwing();
        if (!IsRemoteControlled) TickEffects();
        if (IsRemoteControlled)
        {
            UpdateLimbs();
            if (HurtTime > 0) HurtTime--;
            if (Dead) DeathTime++;
            return;
        }
        if (HurtTime > 0) HurtTime--;
        if (HurtResistantTime > 0) HurtResistantTime--;
        if (LastAttackerTime > 0 && --LastAttackerTime == 0) LastAttacker = null;
        if (RecentlyHitByPlayer > 0) RecentlyHitByPlayer--;
        if (Dead || Health <= 0)
        {
            DeathTime++;
            if (DeathTime >= 20 && this is not Player)
            {
                World.SpawnParticles(ParticleKind.Poof, X, Y + Height / 2, Z, 20);
                Removed = true;
            }
            return;
        }
        BaseLivingTick();
        if (Dead) return;
        if (RidingEntity != null)
        {
            if (RidingEntity.Removed || this is Player && Sneaking) Mount(null);
            else { MotionX = MotionY = MotionZ = 0; RidingEntity.UpdatePassenger(); UpdateLimbs(); return; }
        }
        UpdateAI();
        LivingUpdate();
        UpdateLimbs();
    }

    protected virtual void BaseLivingTick()
    {
        // duszenie się w bloku
        if (IsInsideOpaqueBlock() && !NoClip) Attack(DamageSource.InWall, 1f);
        // tonięcie
        if (IsHeadInWater() && !CanBreatheUnderwater && !(this is Player { Invulnerable: true }))
        {
            Air--;
            if (Air <= -20)
            {
                Air = 0;
                World.SpawnParticles(ParticleKind.Bubble, X, Y + EyeHeight, Z, 8);
                Attack(DamageSource.Drown, 2f);
            }
        }
        else Air = Math.Min(300, Air + (Air < 300 ? 4 : 0));
        if (ImmuneToFire) FireTicks = 0;
    }

    protected virtual void UpdateAI() { }

    protected virtual void LivingUpdate()
    {
        if (JumpTicks > 0) JumpTicks--;
        if (Math.Abs(MotionX) < 0.005) MotionX = 0;
        if (Math.Abs(MotionY) < 0.005) MotionY = 0;
        if (Math.Abs(MotionZ) < 0.005) MotionZ = 0;

        if (IsJumping)
        {
            if (InWater || InLava) MotionY += 0.04;
            else if (OnGround && JumpTicks == 0) { Jump(); JumpTicks = 10; }
        }
        else JumpTicks = 0;

        float strafe = MoveStrafe * 0.98f, forward = MoveForward * 0.98f;
        Travel(strafe, forward);
        CollideWithNearby();
    }

    protected virtual void CollideWithNearby()
    {
        if (World.IsRemote) return;
        foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(0.2, 0, 0.2), this))
        {
            if (!(e is LivingEntity) || e.Dead || e is Player) continue;
            double dx = e.X - X, dz = e.Z - Z;
            double d = Math.Max(Math.Abs(dx), Math.Abs(dz));
            if (d < 0.01) { dx = World.Rand.NextDouble() - 0.5; dz = World.Rand.NextDouble() - 0.5; d = 0.5; }
            d = Math.Sqrt(d);
            dx /= d; dz /= d;
            double f = 1.0 / d; if (f > 1) f = 1;
            dx *= f * 0.05; dz *= f * 0.05;
            MotionX -= dx; MotionZ -= dz;
            e.MotionX += dx; e.MotionZ += dz;
        }
    }

    public virtual void Jump()
    {
        MotionY = 0.42;
        if (Sprinting)
        {
            float yr = Yaw * MathUtil.Deg2Rad;
            MotionX -= MathF.Sin(yr) * 0.2f; MotionZ += MathF.Cos(yr) * 0.2f;
        }
    }

    protected bool IsOnLadder()
    {
        int x = MathUtil.FloorToInt(X), y = MathUtil.FloorToInt(Y), z = MathUtil.FloorToInt(Z);
        return Block.Get(World.GetBlock(x, y, z)).Climbable;
    }

    protected void MoveFlying(float strafe, float forward, float friction)
    {
        float f = strafe * strafe + forward * forward;
        if (f >= 1e-4f)
        {
            f = MathF.Sqrt(f); if (f < 1) f = 1;
            f = friction / f; strafe *= f; forward *= f;
            float yr = Yaw * MathUtil.Deg2Rad;
            float s = MathF.Sin(yr), c = MathF.Cos(yr);
            MotionX += strafe * c - forward * s;
            MotionZ += forward * c + strafe * s;
        }
    }

    public virtual float GetSpeed() => AiMoveSpeed * (Sprinting ? 1.3f : 1f);
    protected virtual float JumpMovementFactor => Sprinting ? 0.026f : 0.02f;
    protected virtual bool IsFlyingNow => false;

    public virtual void Travel(float strafe, float forward)
    {
        if (InWater && !IsFlyingNow)
        {
            double py = Y;
            MoveFlying(strafe, forward, 0.02f);
            MoveWithCollision(MotionX, MotionY, MotionZ);
            MotionX *= 0.8; MotionY *= 0.8; MotionZ *= 0.8;
            MotionY -= 0.02;
            if (CollidedH && IsOffsetFree(MotionX, MotionY + 0.6 - Y + py, MotionZ)) MotionY = 0.3;
        }
        else if (InLava && !IsFlyingNow)
        {
            double py = Y;
            MoveFlying(strafe, forward, 0.02f);
            MoveWithCollision(MotionX, MotionY, MotionZ);
            MotionX *= 0.5; MotionY *= 0.5; MotionZ *= 0.5;
            MotionY -= 0.02;
            if (CollidedH && IsOffsetFree(MotionX, MotionY + 0.6 - Y + py, MotionZ)) MotionY = 0.3;
        }
        else
        {
            float f4 = 0.91f;
            if (OnGround) f4 = SlipperinessBelow() * 0.91f;
            float f = 0.16277136f / (f4 * f4 * f4);
            float f5 = OnGround ? GetSpeed() * f : JumpMovementFactor;
            MoveFlying(strafe, forward, f5);
            f4 = 0.91f;
            if (OnGround) f4 = SlipperinessBelow() * 0.91f;
            if (IsOnLadder())
            {
                const double c = 0.15;
                MotionX = Math.Clamp(MotionX, -c, c); MotionZ = Math.Clamp(MotionZ, -c, c);
                FallDistance = 0;
                if (MotionY < -0.15) MotionY = -0.15;
                if (Sneaking && MotionY < 0) MotionY = 0;
            }
            MoveWithCollision(MotionX, MotionY, MotionZ);
            if (CollidedH && IsOnLadder()) MotionY = 0.2;
            if (!NoGravity) MotionY -= 0.08;
            MotionY *= 0.98;
            MotionX *= f4; MotionZ *= f4;
        }
    }

    protected float SlipperinessBelow()
    {
        int id = World.GetBlock(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y - 0.5), MathUtil.FloorToInt(Z));
        return id == 0 ? 0.6f : Block.ById[id].Slipperiness;
    }

    private readonly List<AABB> _tmp = new();
    private bool IsOffsetFree(double dx, double dy, double dz)
    {
        var bb = BoundingBox.Offset(dx, dy, dz);
        return World.GetCollisionBoxes(bb, _tmp).Count == 0 && !World.IsAnyLiquid(bb, false) && !World.IsAnyLiquid(bb, true);
    }

    protected virtual void UpdateLimbs()
    {
        double dx = X - PrevX, dz = Z - PrevZ;
        float dist = (float)Math.Sqrt(dx * dx + dz * dz) * 4f;
        if (dist > 1) dist = 1;
        LimbSwingAmount += (dist - LimbSwingAmount) * 0.4f;
        LimbSwing += LimbSwingAmount;
        // obrót ciała
        float target = BodyYaw;
        if (dx * dx + dz * dz > 0.0025)
        {
            target = (float)(Math.Atan2(dz, dx) * MathUtil.Rad2Deg) - 90f;
        }
        if (SwingProgress > 0) target = Yaw;
        float diff = MathUtil.WrapDegrees(target - BodyYaw);
        BodyYaw += diff * 0.3f;
        float headDiff = MathUtil.WrapDegrees(Yaw - BodyYaw);
        if (headDiff < -75) BodyYaw = Yaw + 75; if (headDiff > 75) BodyYaw = Yaw - 75;
        HeadYaw = Yaw;
    }

    public virtual bool IsInvulnerableTo(DamageSource s) => false;

    public override bool Attack(DamageSource src, float amount)
    {
        if (World.IsRemote && this is not Player) return false;
        if (Dead || Health <= 0 || IsInvulnerableTo(src)) return false;
        if (src.IsFire && ImmuneToFire) return false;
        if (HurtResistantTime > 10)
        {
            if (amount <= LastDamage) return false;
            DamageEntity(src, amount - LastDamage);
            LastDamage = amount;
        }
        else
        {
            LastDamage = amount;
            HurtResistantTime = 20;
            DamageEntity(src, amount);
            HurtTime = MaxHurtTime;
            AttackedAtYaw = 0;
            if (src.Attacker != null)
            {
                double dx = src.Attacker.X - X, dz = src.Attacker.Z - Z;
                while (dx * dx + dz * dz < 1e-4) { dx = (World.Rand.NextDouble() - World.Rand.NextDouble()) * 0.01; dz = (World.Rand.NextDouble() - World.Rand.NextDouble()) * 0.01; }
                AttackedAtYaw = (float)(Math.Atan2(dz, dx) * MathUtil.Rad2Deg) - Yaw;
                if (src.Type != "arrow" || true) Knockback(0.4f, dx, dz);
            }
            OnHurt(src);
        }
        if (src.Attacker is LivingEntity a) { LastAttacker = a; LastAttackerTime = 100; }
        if (src.Attacker is Player) RecentlyHitByPlayer = 100;
        if (Health <= 0) OnDeath(src);
        return true;
    }

    protected virtual void OnHurt(DamageSource src)
    {
        World.PlaySound(HurtSound, X, Y + Height / 2, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.2f + 1f);
    }

    public virtual string HurtSound => "hurt";
    public virtual string DeathSound => HurtSound;

    protected virtual void DamageEntity(DamageSource src, float amount)
    {
        if (!src.BypassArmor)
        {
            int armor = ArmorValue;
            amount = amount * (25 - armor) / 25f;
            OnArmorDamaged(amount);
        }
        Health = Math.Max(0, Health - amount);
    }

    protected virtual void OnArmorDamaged(float amount) { }

    public void Knockback(float strength, double dx, double dz)
    {
        double d = Math.Sqrt(dx * dx + dz * dz);
        MotionX /= 2; MotionY /= 2; MotionZ /= 2;
        MotionX -= dx / d * strength; MotionZ -= dz / d * strength;
        MotionY += strength;
        if (MotionY > 0.4) MotionY = 0.4;
    }

    public virtual void OnDeath(DamageSource src)
    {
        if (Dead) return;
        Dead = true;
        Health = 0;
        World.PlaySound(DeathSound, X, Y + Height / 2, Z, 1f, 1f);
        if (!World.IsRemote && this is not Player)
        {
            bool byPlayer = RecentlyHitByPlayer > 0;
            DropLoot(byPlayer, FireTicks > 0);
            if (byPlayer && XpValue > 0) World.SpawnXp(X, Y + 0.5, Z, XpValue);
        }
    }

    protected virtual void DropLoot(bool byPlayer, bool burning) { }

    protected void Drop(int id, int count, int meta = 0)
    {
        if (count <= 0) return;
        World.DropItem(X, Y + 0.5, Z, new ItemStack(id, count, meta));
    }

    public void Heal(float amount) { if (Health > 0) Health = Math.Min(MaxHealth, Health + amount); }

    public bool CanSee(Entity e)
    {
        double sx = X, sy = Y + EyeHeight, sz = Z;
        double tx = e.X, ty = e.Y + e.EyeHeight, tz = e.Z;
        double dx = tx - sx, dy = ty - sy, dz = tz - sz;
        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 0.01) return true;
        var hit = World.RayCast(sx, sy, sz, dx / len, dy / len, dz / len, len);
        if (!hit.Hit) return true;
        var b = Block.Get(World.GetBlock(hit.X, hit.Y, hit.Z));
        return !b.Opaque;
    }

    public override void Save(BinaryWriter bw)
    {
        base.Save(bw);
        bw.Write(Health); bw.Write(Air);
        bw.Write(Effects.Count);
        foreach (var e in Effects.Values) { bw.Write((int)e.Id); bw.Write(e.Duration); bw.Write(e.Amplifier); bw.Write(e.Ambient); bw.Write(e.ShowParticles); }
    }
    public override void Load(BinaryReader br)
    {
        base.Load(br);
        Health = br.ReadSingle(); Air = br.ReadInt32();
        int ne = br.ReadInt32();
        for (int i = 0; i < ne; i++) { var e = new EffectInstance((Effect)br.ReadInt32(), br.ReadInt32(), br.ReadInt32(), br.ReadBoolean(), br.ReadBoolean()); Effects[e.Id] = e; }
        BodyYaw = PrevBodyYaw = HeadYaw = PrevHeadYaw = Yaw;
    }
}
