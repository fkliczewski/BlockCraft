using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Smok Endu (EntityDragon z MC 1.8): lata bez kolizji, krąży wokół wyspy albo nurkuje na gracza,
/// niszczy bloki poza obsydianem, kamieniem Endu i bedrockiem, leczy się promieniem kryształów. Trafiany przez części ciała.</summary>
public sealed class EnderDragon : Mob
{
    public readonly double[,] Ring = new double[64, 3];
    public int RingIndex = -1;
    public float AnimTime, PrevAnimTime, RandomYawVelocity;
    public double TargetX, TargetY = 100, TargetZ;
    public bool Slowed, ForceNewTarget;
    /// <summary>Tag NoAI: smok wisi w miejscu z zamrożoną animacją (isAIDisabled, animTime 0.5).</summary>
    public bool NoAI;
    public int DeathTicks;
    public EnderCrystal HealingCrystal;
    private Entity _target;
    public DragonPart Head, Body, Tail1, Tail2, Tail3, Wing1, Wing2;
    public DragonPart[] Parts;

    public EnderDragon(World.World w) : base(w) { Width = 16; Height = 8; MaxHealth = 200; Health = 200; NoClip = true; NoGravity = true; }
    public override string TypeKey => "ender_dragon";
    public override string DisplayName => "Smok Endu";
    public override bool Persistent => true;
    public override bool ImmuneToFire => true;
    // sam smok nie jest celem: trafia się w części ciała
    public override bool CanBeAttacked => false;
    public override bool CanBeCollidedWith => false;
    public override string LivingSound => "mob.enderdragon.growl";
    public override string HurtSound => "mob.enderdragon.hit";
    public override string DeathSound => null;
    protected override void Think() { }

    private void EnsureParts()
    {
        if (Parts != null) return;
        Head = new(this, "head", 6, 6); Body = new(this, "body", 8, 8);
        Tail1 = new(this, "tail", 4, 4); Tail2 = new(this, "tail", 4, 4); Tail3 = new(this, "tail", 4, 4);
        Wing1 = new(this, "wing", 4, 4); Wing2 = new(this, "wing", 4, 4);
        Parts = new[] { Head, Body, Tail1, Tail2, Tail3, Wing1, Wing2 };
        foreach (var p in Parts) { p.SetPositionAndPrev(X, Y, Z); World.SpawnEntity(p); }
    }

    /// <summary>getMovementOffsets: odczyt bufora ostatnich 64 pozycji (odchylenie, wysokość) z interpolacją.</summary>
    public (double yaw, double y) MovementOffsets(int offset, float pt)
    {
        if (Health <= 0) pt = 0;
        pt = 1 - pt;
        int j = RingIndex - offset & 63, k = RingIndex - offset - 1 & 63;
        double yaw = Ring[j, 0] + MathUtil.WrapDegrees((float)(Ring[k, 0] - Ring[j, 0])) * pt;
        double y = Ring[j, 1] + (Ring[k, 1] - Ring[j, 1]) * pt;
        return (yaw, y);
    }

    public override void Tick()
    {
        Age++;
        if (IsRemoteControlled) NetInterpolate();
        EnsureParts();
        if (HurtTime > 0) HurtTime--;
        if (HurtResistantTime > 0) HurtResistantTime--;
        if (RecentlyHitByPlayer > 0) RecentlyHitByPlayer--;
        PrevAnimTime = AnimTime;
        if (Health <= 0) { DeathUpdate(); UpdateParts(); return; }
        if (!World.IsRemote && World.Players.Any(p => p.DistanceSq(this) < 150 * 150)) BossStatus.Set(DisplayName, Health, MaxHealth, false);
        if (!World.IsRemote) UpdateCrystal();
        float f = .2f / ((float)Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ) * 10f + 1f);
        f *= (float)Math.Pow(2.0, MotionY);
        AnimTime = NoAI ? .5f : AnimTime + (Slowed ? f * .5f : f);
        Yaw = MathUtil.WrapDegrees(Yaw);
        if (RingIndex < 0) for (int i = 0; i < 64; i++) { Ring[i, 0] = Yaw; Ring[i, 1] = Y; }
        if (++RingIndex == 64) RingIndex = 0;
        Ring[RingIndex, 0] = Yaw; Ring[RingIndex, 1] = Y;
        if (!IsRemoteControlled && !World.IsRemote && !NoAI)
        {
            if (LivingSound != null && World.Rand.Next(1000) < LivingSoundTimer++) { LivingSoundTimer = -LivingSoundInterval; World.PlaySound(LivingSound, X, Y, Z, 5f, .8f + World.Rand.NextFloat() * .3f); }
            Fly();
        }
        BodyYaw = HeadYaw = Yaw;
        UpdateParts();
        if (World.IsRemote) return;
        if (HurtTime == 0)
        {
            CollideWithEntities(Wing1.BoundingBox.Grow(4, 2, 4).Offset(0, -2, 0));
            CollideWithEntities(Wing2.BoundingBox.Grow(4, 2, 4).Offset(0, -2, 0));
            AttackEntitiesIn(Head.BoundingBox.Grow(1, 1, 1));
        }
        Slowed = DestroyBlocksIn(Head.BoundingBox) | DestroyBlocksIn(Body.BoundingBox);
    }

    private void Fly()
    {
        var r = World.Rand;
        double dx = TargetX - X, dy = TargetY - Y, dz = TargetZ - Z, d2 = dx * dx + dy * dy + dz * dz;
        if (_target != null)
        {
            if (_target.Dead || _target.Removed || _target.World != World || _target is Player { Spectator: true }) { _target = null; ForceNewTarget = true; }
            else
            {
                TargetX = _target.X; TargetZ = _target.Z;
                double hx = TargetX - X, hz = TargetZ - Z, up = .4 + Math.Sqrt(hx * hx + hz * hz) / 80 - 1;
                if (up > 10) up = 10;
                TargetY = _target.Y + up;
            }
        }
        else { TargetX += r.NextGaussian() * 2; TargetZ += r.NextGaussian() * 2; }
        if (ForceNewTarget || d2 < 100 || d2 > 22500 || CollidedH || CollidedV) SetNewTarget();
        dy /= Math.Max(1e-6, Math.Sqrt(dx * dx + dz * dz));
        dy = Math.Clamp(dy, -.6, .6);
        MotionY += dy * .1;
        Yaw = MathUtil.WrapDegrees(Yaw);
        double turn = Math.Clamp(MathUtil.WrapDegrees((float)(180 - Math.Atan2(dx, dz) * MathUtil.Rad2Deg - Yaw)), -50, 50);
        var toTarget = Norm(TargetX - X, TargetY - Y, TargetZ - Z);
        float yr = Yaw * MathUtil.Deg2Rad;
        var fwd = Norm(Math.Sin(yr), MotionY, -Math.Cos(yr));
        float f4 = (float)((Dot(fwd, toTarget) + .5) / 1.5);
        if (f4 < 0) f4 = 0;
        RandomYawVelocity *= .8f;
        double hs = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ) + 1, d9 = Math.Min(40, hs);
        RandomYawVelocity += (float)(turn * (.7 / d9 / hs));
        Yaw += RandomYawVelocity * .1f;
        float f6 = (float)(2.0 / (d9 + 1)), f7 = .06f;
        MoveFlying(0, -1, f7 * (f4 * f6 + (1 - f6)));
        if (Slowed) MoveWithCollision(MotionX * .8, MotionY * .8, MotionZ * .8);
        else MoveWithCollision(MotionX, MotionY, MotionZ);
        var mv = Norm(MotionX, MotionY, MotionZ);
        float f8 = (float)(Dot(mv, fwd) + 1) / 2f;
        f8 = .8f + .15f * f8;
        MotionX *= f8; MotionZ *= f8; MotionY *= .91;
    }

    private static (double x, double y, double z) Norm(double x, double y, double z)
    {
        double d = Math.Sqrt(x * x + y * y + z * z);
        return d < 1e-4 ? (0, 0, 0) : (x / d, y / d, z / d);
    }
    private static double Dot((double x, double y, double z) a, (double x, double y, double z) b) => a.x * b.x + a.y * b.y + a.z * b.z;

    private void SetNewTarget()
    {
        ForceNewTarget = false;
        var r = World.Rand;
        var players = World.Players.Where(p => !p.Spectator && !p.Dead).ToList();
        if (r.Next(2) == 0 && players.Count > 0) { _target = players[r.Next(players.Count)]; return; }
        do
        {
            TargetX = r.NextFloat() * 120 - 60; TargetY = 70 + r.NextFloat() * 50; TargetZ = r.NextFloat() * 120 - 60;
        } while ((X - TargetX) * (X - TargetX) + (Y - TargetY) * (Y - TargetY) + (Z - TargetZ) * (Z - TargetZ) <= 100);
        _target = null;
    }

    private void UpdateParts()
    {
        var (y5, h5) = MovementOffsets(5, 1); var (_, h10) = MovementOffsets(10, 1);
        float f1 = (float)(h5 - h10) * 10f / 180f * MathF.PI, f2 = MathF.Cos(f1), f10 = -MathF.Sin(f1);
        float yr = Yaw * MathUtil.Deg2Rad, f11 = MathF.Sin(yr), f4 = MathF.Cos(yr);
        Place(Body, X + f11 * .5, Y, Z - f4 * .5);
        Place(Wing1, X + f4 * 4.5, Y + 2, Z + f11 * 4.5);
        Place(Wing2, X - f4 * 4.5, Y + 2, Z - f11 * 4.5);
        var (_, h0) = MovementOffsets(0, 1);
        float hy = yr - RandomYawVelocity * .01f;
        Place(Head, X + MathF.Sin(hy) * 5.5f * f2, Y + (h0 - h5) + f10 * 5.5f, Z - MathF.Cos(hy) * 5.5f * f2);
        var tails = new[] { Tail1, Tail2, Tail3 };
        for (int j = 0; j < 3; j++)
        {
            var (ty, th) = MovementOffsets(12 + j * 2, 1);
            float f14 = yr + MathUtil.WrapDegrees((float)(ty - y5)) * MathUtil.Deg2Rad, f15 = MathF.Sin(f14), f16 = MathF.Cos(f14);
            const float f17 = 1.5f; float f18 = (j + 1) * 2f;
            Place(tails[j], X - (f11 * f17 + f15 * f18) * f2, Y + (th - h5) - (f18 + f17) * f10 + 1.5, Z + (f4 * f17 + f16 * f18) * f2);
        }
    }

    private static void Place(DragonPart p, double x, double y, double z) { p.PrevTick(); p.SetPosition(x, y, z); }

    private bool IsOwnPart(Entity e) => e == this || e is DragonPart || e is EnderCrystal;

    /// <summary>collideWithEntities: skrzydła odrzucają stworzenia od środka tułowia.</summary>
    private void CollideWithEntities(AABB box)
    {
        double cx = (Body.BoundingBox.MinX + Body.BoundingBox.MaxX) / 2, cz = (Body.BoundingBox.MinZ + Body.BoundingBox.MaxZ) / 2;
        foreach (var e in World.GetEntitiesInBox(box, this))
        {
            if (e is not LivingEntity || IsOwnPart(e) || e is Player { Creative: true }) continue;
            double dx = e.X - cx, dz = e.Z - cz, d = Math.Max(.01, dx * dx + dz * dz);
            e.MotionX += dx / d * 4; e.MotionY += .2; e.MotionZ += dz / d * 4;
        }
    }

    /// <summary>attackEntitiesInList: głowa zadaje 10 obrażeń.</summary>
    private void AttackEntitiesIn(AABB box)
    {
        foreach (var e in World.GetEntitiesInBox(box, this))
            if (e is LivingEntity le && !IsOwnPart(e)) le.Attack(DamageSource.Mob(this), 10f);
    }

    /// <summary>destroyBlocksInAABB: zwraca true, gdy smok uderzył w blok, którego nie niszczy (wtedy zwalnia).</summary>
    private bool DestroyBlocksIn(AABB bb)
    {
        int x0 = MathUtil.FloorToInt(bb.MinX), y0 = MathUtil.FloorToInt(bb.MinY), z0 = MathUtil.FloorToInt(bb.MinZ);
        int x1 = MathUtil.FloorToInt(bb.MaxX), y1 = MathUtil.FloorToInt(bb.MaxY), z1 = MathUtil.FloorToInt(bb.MaxZ);
        bool blocked = false, destroyed = false;
        for (int x = x0; x <= x1; x++) for (int y = Math.Max(0, y0); y <= Math.Min(Chunk.Height - 1, y1); y++) for (int z = z0; z <= z1; z++)
        {
            int id = World.GetBlock(x, y, z);
            if (id == B.Air) continue;
            if (id is B.Obsidian or B.EndStone or B.Bedrock or B.Barrier or B.CommandBlock || !World.Rules.MobGriefing) blocked = true;
            else { destroyed = World.SetBlock(x, y, z, B.Air) | destroyed; }
        }
        if (destroyed)
        {
            var r = World.Rand;
            World.SpawnParticles(ParticleKind.Explosion, bb.MinX + (bb.MaxX - bb.MinX) * r.NextDouble(), bb.MinY + (bb.MaxY - bb.MinY) * r.NextDouble(), bb.MinZ + (bb.MaxZ - bb.MinZ) * r.NextDouble(), 1, 20);
        }
        return blocked;
    }

    /// <summary>updateDragonEnderCrystal: co 10 ticków +1 HP od kryształu; zniszczenie leczącego kryształu rani głowę za 10.</summary>
    private void UpdateCrystal()
    {
        if (HealingCrystal != null)
        {
            if (HealingCrystal.Dead || HealingCrystal.Removed)
            {
                AttackFromPart(Head, DamageSource.Explosion(null), 10f);
                HealingCrystal = null;
            }
            else if (Age % 10 == 0 && Health < MaxHealth) Health = Math.Min(MaxHealth, Health + 1);
        }
        if (World.Rand.Next(10) == 0)
        {
            EnderCrystal best = null; double bd = double.MaxValue;
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(32)))
                if (e is EnderCrystal c && !c.Dead) { double d = c.DistanceSq(this); if (d < bd) { bd = d; best = c; } }
            HealingCrystal = best;
        }
    }

    public override bool Attack(DamageSource src, float amount) => false;

    /// <summary>attackEntityFromPart: poza głową obrażenia /4+1; trafiony smok odlatuje w nowy punkt.</summary>
    public bool AttackFromPart(DragonPart part, DamageSource src, float amount)
    {
        if (Dead || Health <= 0) return false;
        if (part != Head) amount = amount / 4f + 1f;
        float yr = Yaw * MathUtil.Deg2Rad; var r = World.Rand;
        TargetX = X + MathF.Sin(yr) * 5 + (r.NextFloat() - .5f) * 2;
        TargetY = Y + r.NextFloat() * 3 + 1;
        TargetZ = Z - MathF.Cos(yr) * 5 + (r.NextFloat() - .5f) * 2;
        _target = null;
        return (src.Attacker is Player || src.IsExplosion) && base.Attack(src, amount);
    }

    protected override void OnHurt(DamageSource src) => World.PlaySound(HurtSound, X, Y, Z, 5f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f);

    /// <summary>onDeathUpdate: 200 ticków unoszenia z obrotem i eksplozjami, 12000 XP, portal wyjściowy z jajem.</summary>
    private void DeathUpdate()
    {
        DeathTicks++; DeathTime = DeathTicks;
        var r = World.Rand;
        if (DeathTicks >= 180 && DeathTicks <= 200)
            World.SpawnParticles(ParticleKind.Explosion, X + (r.NextFloat() - .5f) * 8, Y + 2 + (r.NextFloat() - .5f) * 4, Z + (r.NextFloat() - .5f) * 8, 1, 40);
        if (!World.IsRemote)
        {
            if (DeathTicks > 150 && DeathTicks % 5 == 0) DropXp(1000);
            if (DeathTicks == 1) foreach (var p in World.Players) World.PlaySound("mob.enderdragon.end", p.X, p.Y, p.Z, 1f, 1f);
        }
        PrevTick();
        Y += .1;
        BodyYaw = Yaw += 20f;
        if (DeathTicks >= 200 && !World.IsRemote)
        {
            DropXp(2000);
            GeneratePortal(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Z));
            BossStatus.StatusBarTime = 0; BossStatus.BossName = null;
            Removed = true;
        }
    }

    private void DropXp(int amount)
    {
        // EntityXPOrb.getXPSplit
        int[] split = { 2477, 1237, 617, 307, 149, 73, 37, 17, 7, 3, 1 };
        while (amount > 0)
        {
            int v = split.First(s => amount >= s);
            amount -= v;
            World.SpawnEntity(new XpOrbEntity(World, X, Y, Z, v));
        }
    }

    /// <summary>generatePortal (1.8): misa z bedrocku o promieniu 3.5, portal w środku, kolumna z pochodniami i jajo smoka.</summary>
    public static void GeneratePortal(World.World w, int px, int py, int pz)
    {
        for (int j = -1; j <= 32; j++) for (int k = -4; k <= 4; k++) for (int l = -4; l <= 4; l++)
        {
            double d2 = k * k + l * l;
            if (d2 > 12.25) continue;
            int x = px + k, y = py + j, z = pz + l;
            if (j < 0) { if (d2 <= 6.25) w.SetBlock(x, y, z, B.Bedrock); }
            else if (j > 0) w.SetBlock(x, y, z, B.Air);
            else w.SetBlock(x, y, z, d2 > 6.25 ? B.Bedrock : B.EndPortal);
        }
        for (int i = 0; i < 4; i++) w.SetBlock(px, py + i, pz, B.Bedrock);
        w.SetBlock(px - 1, py + 2, pz, B.Torch, 2); w.SetBlock(px + 1, py + 2, pz, B.Torch, 1);
        w.SetBlock(px, py + 2, pz - 1, B.Torch, 4); w.SetBlock(px, py + 2, pz + 1, B.Torch, 3);
        w.SetBlock(px, py + 4, pz, B.DragonEgg);
    }
    private void GeneratePortal(int x, int z) => GeneratePortal(World, x, 64, z);

    public override void OnDeath(DamageSource src)
    {
        if (Dead) return;
        // bez Dead: świat usuwa martwe encje od razu, a smok ma 200 ticków animacji śmierci
        Health = 0;
    }

    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(DeathTicks); bw.Write(NoAI); }
    public override void Load(BinaryReader br) { base.Load(br); DeathTicks = br.ReadInt32(); NoAI = br.ReadBoolean(); if (DeathTicks > 0) Health = 0; }
}

/// <summary>Część ciała smoka (EntityDragonPart): sama nie myśli, obrażenia przekazuje smokowi. Nie jest zapisywana.</summary>
public sealed class DragonPart : LivingEntity
{
    public readonly EnderDragon Dragon;
    public readonly string PartName;
    public DragonPart(EnderDragon d, string name, float w, float h) : base(d.World)
    { Dragon = d; PartName = name; Width = w; Height = h; NoClip = true; NoGravity = true; MaxHealth = Health = 200; }
    public override string TypeKey => "dragon_part";
    public override string DisplayName => Dragon.DisplayName;
    public override bool CanBeAttacked => Dragon.Health > 0 && !Dragon.Removed;
    public override bool CanBeCollidedWith => CanBeAttacked;
    public override bool ImmuneToFire => true;
    public override void Tick()
    {
        Age++;
        if (Dragon.Removed || Dragon.World != World) Removed = true;
    }
    public override bool Attack(DamageSource src, float amount) => Dragon.AttackFromPart(this, src, amount);
}

/// <summary>Kryształ Endu (EntityEnderCrystal 1.8): stoi na filarze, podtrzymuje ogień pod sobą, leczy smoka.
/// Każde trafienie niszczy go wybuchem o mocy 6.</summary>
public sealed class EnderCrystal : LivingEntity
{
    public int InnerRotation;
    public EnderCrystal(World.World w) : base(w)
    {
        Width = Height = 2; NoGravity = true; MaxHealth = Health = 5;
        InnerRotation = w?.Rand.Next(100000) ?? 0;
    }
    public override string TypeKey => "ender_crystal";
    public override string DisplayName => "Kryształ Endu";
    public override bool Persistent => true;
    public override bool ImmuneToFire => true;
    public override void Tick()
    {
        Age++; InnerRotation++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        if (World.IsRemote) return;
        int x = MathUtil.FloorToInt(X), y = MathUtil.FloorToInt(Y), z = MathUtil.FloorToInt(Z);
        if (World.Dimension == DimensionId.End && World.GetBlock(x, y, z) != B.Fire && World.IsLoadedAt(x, z)) World.SetBlock(x, y, z, B.Fire);
    }
    public override bool Attack(DamageSource src, float amount)
    {
        if (Dead || World.IsRemote) return false;
        Health = 0; Dead = true; Removed = true;
        Explosion.Explode(World, null, X, Y, Z, 6f, true);
        return true;
    }
}
