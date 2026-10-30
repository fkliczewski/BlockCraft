using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Wither (EntityWither z MC 1.8): 300 HP, przez 220 ticków po przywołaniu nietykalny i rośnie, potem wybuch mocy 7.
/// Trzy głowy strzelają czaszkami (środkowa w cel ataku, boczne w losowe cele i co jakiś czas niebieską czaszką w okolicę).
/// Poniżej połowy zdrowia ma tarczę odbijającą strzały. Upuszcza gwiazdę Netheru.</summary>
public sealed class Wither : Mob
{
    public int InvulTime;
    public readonly int[] HeadTargets = new int[3];
    private readonly int[] _nextHeadUpdate = new int[2], _idleHeadUpdates = new int[2];
    public readonly float[] HeadPitch = new float[2], HeadYawAbs = new float[2], PrevHeadPitch = new float[2], PrevHeadYawAbs = new float[2];
    private int _blockBreakCounter, _rangedCooldown;
    private const double FollowRange = 40;

    public Wither(World.World w) : base(w) { Width = .9f; Height = 3.5f; MaxHealth = 300; Health = 300; MoveSpeed = .6; }
    public override string TypeKey => "wither";
    public override string DisplayName => "Wither";
    public override bool ImmuneToFire => true;
    public override bool Persistent => true;
    public override int ArmorValue => 4;
    public override int XpValue => 50;
    public override string LivingSound => "mob.wither.idle";
    public override string HurtSound => "mob.wither.hurt";
    public override string DeathSound => "mob.wither.death";
    public bool Armored => Health <= MaxHealth / 2;
    protected override void OnFall(float distance) { }
    public override bool CanHaveEffect(Effect id) => id != Effect.Wither;

    /// <summary>func_82206_m: start po przywołaniu - 220 ticków nietykalności i 1/3 zdrowia.</summary>
    public void StartSpawn() { InvulTime = 220; Health = MaxHealth / 3; }

    public override void Tick()
    {
        // Wither jest trwały jak w MC (nie znika z dala od gracza); tryb pokojowy go usuwa
        if (!World.IsRemote && World.Difficulty == Difficulty.Peaceful) { Removed = true; return; }
        LivingTickBase();
    }
    private void LivingTickBase()
    {
        for (int i = 0; i < 2; i++) { PrevHeadPitch[i] = HeadPitch[i]; PrevHeadYawAbs[i] = HeadYawAbs[i]; }
        if (!World.IsRemote && World.Players.Any(p => p.DistanceSq(this) < 100 * 100)) BossStatus.Set(DisplayName, Health, MaxHealth, true);
        FlyTowardsTarget();
        TickLiving();
        UpdateHeads();
        var r = World.Rand;
        for (int i = 0; i < 3; i++)
            World.SpawnParticles(ParticleKind.Smoke, HeadX(i) + r.NextGaussian() * .3, HeadY(i) + r.NextGaussian() * .3, HeadZ(i) + r.NextGaussian() * .3, 1);
        if (InvulTime > 0)
            for (int i = 0; i < 3; i++) World.SpawnParticles(ParticleKind.Portal, X + r.NextGaussian(), Y + r.NextFloat() * 3.3, Z + r.NextGaussian(), 1);
    }
    private void TickLiving() => base.Tick();

    /// <summary>onLivingUpdate: ruch w stronę celu głowy środkowej (wznosi się nad niego, z tarczą trzyma się niżej).</summary>
    private void FlyTowardsTarget()
    {
        MotionY *= .6;
        if (World.IsRemote || HeadTargets[0] == 0) return;
        if (World.EntityById.TryGetValue(HeadTargets[0], out var e) && !e.Removed)
        {
            if (Y < e.Y || !Armored && Y < e.Y + 5)
            {
                if (MotionY < 0) MotionY = 0;
                MotionY += (.5 - MotionY) * .6;
            }
            double dx = e.X - X, dz = e.Z - Z, d3 = dx * dx + dz * dz;
            if (d3 > 9)
            {
                double d5 = Math.Sqrt(d3);
                MotionX += (dx / d5 * .5 - MotionX) * .6; MotionZ += (dz / d5 * .5 - MotionZ) * .6;
            }
        }
        if (MotionX * MotionX + MotionZ * MotionZ > .05) Yaw = (float)(Math.Atan2(MotionZ, MotionX) * MathUtil.Rad2Deg) - 90f;
    }

    protected override void Think()
    {
        var r = World.Rand;
        if (InvulTime > 0)
        {
            int left = InvulTime - 1;
            if (left <= 0)
            {
                Explosion.Explode(World, this, X, Y + EyeHeight, Z, 7f, World.Rules.MobGriefing);
                foreach (var p in World.Players) World.PlaySound("mob.wither.spawn", p.X, p.Y, p.Z, 1f, 1f);
            }
            InvulTime = left;
            if (Age % 10 == 0) Heal(10);
            return;
        }
        // EntityAIArrowAttack(1.0, 40, 20) + EntityAIWander + cel: najbliższy żywy nie-nieumarły
        AcquireWitherTarget();
        if (Target != null)
        {
            LookTarget = Target; LookTime = 10;
            bool see = CanSee(Target);
            double d2 = DistanceSq(Target);
            if (d2 > 20 * 20 * .25 || !see) Nav.MoveTo(Target.X, Target.Y, Target.Z, MoveSpeed);
            if (--_rangedCooldown <= 0 && see && d2 <= 400)
            {
                LaunchSkullAt(0, Target);
                _rangedCooldown = 40;
            }
        }
        else Wander(MoveSpeed * .5);
        for (int i = 1; i < 3; i++)
        {
            if (Age < _nextHeadUpdate[i - 1]) continue;
            _nextHeadUpdate[i - 1] = Age + 10 + r.Next(10);
            if (World.Difficulty is Difficulty.Normal or Difficulty.Hard)
            {
                if (_idleHeadUpdates[i - 1]++ > 15)
                {
                    LaunchSkullTo(i, X + (r.NextDouble() * 2 - 1) * 10, Y + (r.NextDouble() * 2 - 1) * 5, Z + (r.NextDouble() * 2 - 1) * 10, true);
                    _idleHeadUpdates[i - 1] = 0;
                }
            }
            int id = HeadTargets[i];
            if (id > 0)
            {
                if (World.EntityById.TryGetValue(id, out var e) && e is LivingEntity le && !le.Dead && DistanceSq(le) <= 900 && CanSee(le))
                {
                    if (le is Player { Creative: true }) HeadTargets[i] = 0;
                    else { LaunchSkullAt(i, le); _nextHeadUpdate[i - 1] = Age + 40 + r.Next(20); _idleHeadUpdates[i - 1] = 0; }
                }
                else HeadTargets[i] = 0;
            }
            else
            {
                var list = World.GetEntitiesInBox(BoundingBox.Grow(20, 8, 20), this).OfType<LivingEntity>().Where(IsValidTarget).ToList();
                for (int k = 0; k < 10 && list.Count > 0; k++)
                {
                    var e = list[r.Next(list.Count)];
                    if (CanSee(e)) { HeadTargets[i] = e.Id; break; }
                    list.Remove(e);
                }
            }
        }
        HeadTargets[0] = Target?.Id ?? 0;
        if (_blockBreakCounter > 0 && --_blockBreakCounter == 0 && World.Rules.MobGriefing) BreakSurroundingBlocks();
        if (Age % 20 == 0) Heal(1);
    }

    private bool IsValidTarget(LivingEntity e) =>
        !e.Dead && e != this && e is not (DragonPart or EnderCrystal) && !IsUndead(e) && e is not Player { Creative: true } and not Player { Spectator: true };

    /// <summary>EntityLivingBase.getCreatureAttribute() == UNDEAD: Wither nie atakuje nieumarłych.</summary>
    public static bool IsUndead(Entity e) => e is Zombie or Skeleton or ZombiePigman or WitherSkeleton or Wither;

    private void AcquireWitherTarget()
    {
        if (Target != null && (!IsValidTarget(Target) || Target.DistanceSq(this) > FollowRange * FollowRange)) Target = null;
        if (LastAttacker is LivingEntity la && IsValidTarget(la) && !IsUndead(la)) Target = la;
        if (Target == null && World.Rand.Next(10) == 0)
        {
            LivingEntity best = null; double bd = FollowRange * FollowRange;
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(FollowRange, 4, FollowRange), this))
                if (e is LivingEntity le && IsValidTarget(le) && CanSee(le)) { double d = le.DistanceSq(this); if (d < bd) { bd = d; best = le; } }
            Target = best;
        }
    }

    /// <summary>Po trafieniu (licznik 20 ticków) niszczy bloki 3x4x3 wokół siebie, poza bedrockiem i portalem Endu.</summary>
    private void BreakSurroundingBlocks()
    {
        int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(Y), bz = MathUtil.FloorToInt(Z);
        bool any = false;
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) for (int dy = 0; dy <= 3; dy++)
        {
            int x = bx + dx, y = by + dy, z = bz + dz, id = World.GetBlock(x, y, z);
            if (id == B.Air || id is B.Bedrock or B.EndPortal or B.EndPortalFrame or B.CommandBlock or B.Barrier) continue;
            World.BreakBlock(x, y, z, true); any = true;
        }
        if (any) World.PlaySound("mob.zombie.woodbreak", X, Y, Z, 1f, 1f);
    }

    public double HeadX(int i) { if (i <= 0) return X; float f = (BodyYaw + 180 * (i - 1)) * MathUtil.Deg2Rad; return X + MathF.Cos(f) * 1.3; }
    public double HeadY(int i) => i <= 0 ? Y + 3 : Y + 2.2;
    public double HeadZ(int i) { if (i <= 0) return Z; float f = (BodyYaw + 180 * (i - 1)) * MathUtil.Deg2Rad; return Z + MathF.Sin(f) * 1.3; }

    private void LaunchSkullAt(int head, LivingEntity t) => LaunchSkullTo(head, t.X, t.Y + t.EyeHeight * .5, t.Z, head == 0 && World.Rand.NextFloat() < .001f);

    private void LaunchSkullTo(int head, double x, double y, double z, bool blue)
    {
        World.PlaySound("mob.wither.shoot", X, Y, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f);
        double hx = HeadX(head), hy = HeadY(head), hz = HeadZ(head);
        var s = new WitherSkullEntity(World) { Blue = blue };
        s.Launch(this, hx, hy, hz, x - hx, y - hy, z - hz, .4);
        World.SpawnEntity(s);
    }

    private void UpdateHeads()
    {
        for (int j = 0; j < 2; j++)
        {
            int id = HeadTargets[j + 1];
            if (id > 0 && World.EntityById.TryGetValue(id, out var e))
            {
                double hx = HeadX(j + 1), hy = HeadY(j + 1), hz = HeadZ(j + 1);
                double dx = e.X - hx, dy = e.Y + e.EyeHeight - hy, dz = e.Z - hz, dh = Math.Sqrt(dx * dx + dz * dz);
                float yaw = (float)(Math.Atan2(dz, dx) * MathUtil.Rad2Deg) - 90f, pitch = (float)-(Math.Atan2(dy, dh) * MathUtil.Rad2Deg);
                HeadPitch[j] = Approach(HeadPitch[j], pitch, 40f);
                HeadYawAbs[j] = Approach(HeadYawAbs[j], yaw, 10f);
            }
            else HeadYawAbs[j] = Approach(HeadYawAbs[j], BodyYaw, 10f);
        }
    }
    private static float Approach(float from, float to, float max) => from + Math.Clamp(MathUtil.WrapDegrees(to - from), -max, max);

    public override bool Attack(DamageSource src, float amount)
    {
        if (src.Type == "drown" || src.Attacker is Wither) return false;
        if (InvulTime > 0 && src.Type != "outOfWorld") return false;
        if (Armored && src.Source is ArrowEntity) return false;
        if (src.Attacker is LivingEntity a && a is not Player && IsUndead(a)) return false;
        if (_blockBreakCounter <= 0) _blockBreakCounter = 20;
        for (int i = 0; i < 2; i++) _idleHeadUpdates[i] += 3;
        return base.Attack(src, amount);
    }

    protected override void DropLoot(bool byPlayer, bool burning) => Drop(I.NetherStar, 1);

    public override int NetData => InvulTime;
    public override void SetNetData(int d) => InvulTime = d;
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(InvulTime); }
    public override void Load(BinaryReader br) { base.Load(br); InvulTime = br.ReadInt32(); }

    // ---------- przywołanie ----------
    /// <summary>Czy w danym miejscu stoi czaszka witherowego szkieleta. Domyślnie każdy blok czaszki;
    /// moduł z blokiem czaszki może podmienić na sprawdzenie typu czaszki (1 = wither).</summary>
    public static Func<World.World, int, int, int, bool> IsWitherSkullAt = (w, x, y, z) => w.GetBlock(x, y, z) == B.Skull;

    /// <summary>BlockSkull.checkWitherSpawn: wołać po postawieniu czaszki witherowej na (x,y,z).
    /// Wzór "^^^ / ### / ~#~" (czaszki, piasek dusz, puste) w osi X albo Z. Zwraca przywołanego Withera albo null.</summary>
    public static Wither TrySummon(World.World w, int x, int y, int z)
    {
        if (w.IsRemote || y < 2 || w.Difficulty == Difficulty.Peaceful) return null;
        foreach (bool alongX in new[] { true, false })
            for (int off = -2; off <= 0; off++)
            {
                // lewy górny róg wzoru: czaszki w rzędzie y, piasek dusz w y-1 (3 bloki) i y-2 (środek)
                int sx = alongX ? x + off : x, sz = alongX ? z : z + off;
                (int x, int z) At(int i) => alongX ? (sx + i, sz) : (sx, sz + i);
                bool ok = true;
                for (int i = 0; i < 3 && ok; i++)
                {
                    var (px, pz) = At(i);
                    ok = IsWitherSkullAt(w, px, y, pz) && w.GetBlock(px, y - 1, pz) == B.SoulSand
                         && (i == 1 ? w.GetBlock(px, y - 2, pz) == B.SoulSand : w.GetBlock(px, y - 2, pz) == B.Air);
                }
                if (!ok) continue;
                for (int i = 0; i < 3; i++)
                {
                    var (px, pz) = At(i);
                    w.SetBlock(px, y, pz, B.Air); w.SetBlock(px, y - 1, pz, B.Air);
                    if (i == 1) w.SetBlock(px, y - 2, pz, B.Air);
                }
                var (cx, cz) = At(1);
                var wither = new Wither(w) { Yaw = alongX ? 0 : 90 };
                wither.SetPositionAndPrev(cx + .5, y - 2 + .55, cz + .5);
                wither.BodyYaw = wither.PrevBodyYaw = wither.Yaw;
                wither.StartSpawn();
                w.SpawnEntity(wither);
                for (int i = 0; i < 120; i++) w.SpawnParticles(ParticleKind.Poof, cx + w.Rand.NextDouble(), y - 2 + w.Rand.NextDouble() * 3.9, cz + w.Rand.NextDouble(), 1);
                return wither;
            }
        return null;
    }
}

/// <summary>Czaszka Withera (EntityWitherSkull 1.8): 8 obrażeń (5 bez strzelca), efekt Wither II 10 s (normalny) albo 40 s (trudny),
/// leczy Withera o 5 po zabiciu celu, wybuch mocy 1. Niebieska jest wolniejsza (0.73).</summary>
public sealed class WitherSkullEntity : FireballEntity
{
    public bool Blue;
    public WitherSkullEntity(World.World w) : base(w) { Width = Height = .3125f; }
    public override string TypeKey => "wither_skull";
    protected override float MotionFactor => Blue ? .73f : base.MotionFactor;

    protected override void OnImpact(Entity hit, World.World.HitResult block)
    {
        if (World.IsRemote) return;
        if (hit is LivingEntity le)
        {
            if (Shooter != null)
            {
                if (le.Attack(DamageSource.Mob(Shooter), 8f) && (le.Dead || le.Health <= 0)) Shooter.Heal(5f);
            }
            else le.Attack(new DamageSource("magic") { BypassArmor = true }, 5f);
            int sec = World.Difficulty switch { Difficulty.Normal => 10, Difficulty.Hard => 40, _ => 0 };
            if (sec > 0 && !le.Dead) le.AddEffect(Effect.Wither, 20 * sec, 1);
        }
        Explosion.Explode(World, this, X, Y, Z, 1f, World.Rules.MobGriefing);
        Kill();
    }

    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Blue); }
    public override void Load(BinaryReader br) { base.Load(br); Blue = br.ReadBoolean(); }
}
