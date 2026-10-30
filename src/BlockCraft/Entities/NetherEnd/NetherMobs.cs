using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Zombie pigman (EntityPigZombie 1.8): neutralny, po uderzeniu gracza cała grupa w promieniu złości się na 400-799 ticków.</summary>
public sealed class ZombiePigman : Monster
{
    public int Anger, RandomSoundDelay;
    public string AngerTarget;
    public ZombiePigman(World.World w) : base(w)
    {
        MaxHealth = 20; Health = 20; MoveSpeed = .23; AttackDamage = 5; FollowRange = 35;
        HeldItem = new ItemStack(I.GoldSword);
    }
    public override string TypeKey => "zombie_pigman";
    public override string DisplayName => "Zombie pigman";
    public override bool ImmuneToFire => true;
    public override int ArmorValue => 2;
    public override string LivingSound => "mob.zombiepig.zpig";
    public override string HurtSound => "mob.zombiepig.zpighurt";
    public override string DeathSound => "mob.zombiepig.zpigdeath";
    public override string StepSoundName => "mob.zombie.step";
    public bool Angry => Anger > 0;

    public void BecomeAngryAt(Player p)
    {
        Anger = 400 + World.Rand.Next(400);
        RandomSoundDelay = World.Rand.Next(40);
        AngerTarget = p.Name; Target = p;
    }

    protected override void OnHurt(DamageSource src)
    {
        base.OnHurt(src);
        if (src.Attacker is not Player p || p.Creative) return;
        // EntityAIHurtByTarget z wezwaniem pomocy: pigmany w zasięgu śledzenia (35 poziomo, 10 w pionie)
        foreach (var e in World.GetEntitiesInBox(new AABB(X - FollowRange, Y - 10, Z - FollowRange, X + FollowRange, Y + 10, Z + FollowRange)))
            if (e is ZombiePigman zp && !zp.Dead) zp.BecomeAngryAt(p);
        BecomeAngryAt(p);
    }

    protected override void Think()
    {
        if (RandomSoundDelay > 0 && --RandomSoundDelay == 0)
            World.PlaySound("mob.zombiepig.zpigangry", X, Y + EyeHeight, Z, 2f, ((World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f) * 1.8f);
        if (Anger > 0)
        {
            Anger--;
            if (Target == null || Target.Dead) Target = World.Players.FirstOrDefault(p => p.Name == AngerTarget && !p.Dead && !p.Creative && !p.Spectator);
            if (Target != null && Target.DistanceSq(this) > FollowRange * FollowRange) Target = null;
            if (Target != null) { MeleeChase(MoveSpeed + .05); return; }
        }
        else Target = null;
        LookAtPlayersRandomly(); Wander(MoveSpeed);
    }

    protected override void DropLoot(bool byPlayer, bool burning)
    {
        int looting = NetherMobUtil.Looting(this);
        Drop(I.RottenFlesh, World.Rand.Next(2 + looting));
        Drop(I.GoldNugget, World.Rand.Next(2 + looting));
        if (byPlayer && World.Rand.Next(200) - looting < 5) Drop(I.GoldIngot, 1);
        if (byPlayer && World.Rand.NextFloat() - looting * .01f < .085f) Drop(I.GoldSword, 1);
    }
    public override int NetData => Angry ? 1 : 0;
    public override void SetNetData(int d) => Anger = d != 0 ? Math.Max(Anger, 1) : 0;
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Anger); bw.Write(AngerTarget ?? ""); }
    public override void Load(BinaryReader br) { base.Load(br); Anger = br.ReadInt32(); AngerTarget = br.ReadString(); if (AngerTarget == "") AngerTarget = null; }
}

/// <summary>Ghast (EntityGhast 1.8): lata losowo w promieniu 16, strzela dużymi kulami ognia (ładowanie 20 ticków).</summary>
public sealed class Ghast : Monster
{
    public int AttackTimer, PrevAttackTimer;
    private double _tx, _ty, _tz; private bool _moving; private int _courseCooldown;
    public bool Attacking;
    public Ghast(World.World w) : base(w) { Width = Height = 4f; MaxHealth = 10; Health = 10; FollowRange = 100; NoGravity = true; }
    public override string TypeKey => "ghast";
    public override string DisplayName => "Ghast";
    public override bool ImmuneToFire => true;
    public override string LivingSound => "mob.ghast.moan";
    public override string HurtSound => "mob.ghast.scream";
    public override string DeathSound => "mob.ghast.death";
    public override float EyeHeight => 2.6f;
    public override int XpValue => 5;
    protected override bool IsFlyingNow => true;
    protected override void OnFall(float distance) { }

    public override bool Attack(DamageSource src, float amount)
    {
        // odbita kula ognia gracza zabija ghasta (MC 1.8 attackEntityFrom: 1000 obrażeń)
        if (src.Type == "fireball" && src.Attacker is Player) return base.Attack(new DamageSource("fireball_reflected", src.Source, src.Attacker), 1000f);
        return base.Attack(src, amount);
    }

    protected override void OnHurt(DamageSource src) => World.PlaySound(HurtSound, X, Y + Height / 2, Z, 10f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f);

    protected override void Think()
    {
        var r = World.Rand;
        // EntityAIRandomFly + GhastMoveHelper
        double d2 = (_tx - X) * (_tx - X) + (_ty - Y) * (_ty - Y) + (_tz - Z) * (_tz - Z);
        if (!_moving || d2 < 1 || d2 > 3600)
        {
            _tx = X + (r.NextFloat() * 2 - 1) * 16; _ty = Y + (r.NextFloat() * 2 - 1) * 16; _tz = Z + (r.NextFloat() * 2 - 1) * 16;
            _moving = true;
        }
        if (_moving && _courseCooldown-- <= 0)
        {
            _courseCooldown += r.Next(5) + 2;
            double dx = _tx - X, dy = _ty - Y, dz = _tz - Z, d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (d > 0 && PathFree(dx / d, dy / d, dz / d, d)) { MotionX += dx / d * .1; MotionY += dy / d * .1; MotionZ += dz / d * .1; }
            else _moving = false;
        }
        // cel: najbliższy gracz (EntityAIFindEntityNearestPlayer)
        if (Target != null && (Target.Dead || Target is Player tp && (tp.Creative || tp.Spectator) || Target.DistanceSq(this) > FollowRange * FollowRange)) Target = null;
        if (Target == null && r.Next(10) == 0) Target = NearestPlayer(FollowRange, false);
        // EntityAILookAround
        if (Target == null) Yaw = (float)(-Math.Atan2(MotionX, MotionZ) * MathUtil.Rad2Deg);
        else if (Target.DistanceSq(this) < 64 * 64) Yaw = (float)(-Math.Atan2(Target.X - X, Target.Z - Z) * MathUtil.Rad2Deg);
        BodyYaw = HeadYaw = Yaw;
        // EntityAIFireballAttack
        PrevAttackTimer = AttackTimer;
        if (Target != null && Target.DistanceSq(this) < 64 * 64 && CanSee(Target))
        {
            AttackTimer++;
            if (AttackTimer == 10) World.PlaySound("mob.ghast.charge", X, Y, Z, 10f, (r.NextFloat() - r.NextFloat()) * .2f + 1f);
            if (AttackTimer == 20)
            {
                var (lx, _, lz) = Look();
                double dx = Target.X - (X + lx * 4), dy = Target.Y + Target.Height / 2 - (.5 + Y + Height / 2), dz = Target.Z - (Z + lz * 4);
                World.PlaySound("mob.ghast.fireball", X, Y, Z, 10f, (r.NextFloat() - r.NextFloat()) * .2f + 1f);
                var fb = new LargeFireballEntity(World);
                fb.Launch(this, X + lx * 4, Y + Height / 2 + .5, Z + lz * 4, dx, dy, dz, .4);
                World.SpawnEntity(fb);
                AttackTimer = -40;
            }
        }
        else if (AttackTimer > 0) AttackTimer--;
        Attacking = AttackTimer > 10;
    }

    private bool PathFree(double nx, double ny, double nz, double d)
    {
        var tmp = new List<AABB>();
        for (int i = 1; i < d; i++)
        {
            tmp.Clear();
            if (World.GetCollisionBoxes(BoundingBox.Offset(nx * i, ny * i, nz * i), tmp).Count > 0) return false;
        }
        return true;
    }

    public override void Travel(float strafe, float forward)
    {
        // EntityFlying.moveEntityWithHeading bez grawitacji, tłumienie 0.91
        MoveWithCollision(MotionX, MotionY, MotionZ);
        double f = InWater ? .8 : InLava ? .5 : .91;
        MotionX *= f; MotionY *= f; MotionZ *= f;
    }

    protected override void DropLoot(bool byPlayer, bool burning)
    {
        int looting = NetherMobUtil.Looting(this);
        Drop(I.GhastTear, World.Rand.Next(2 + looting));
        Drop(I.Gunpowder, World.Rand.Next(3 + looting));
    }
    public override int NetData => Attacking ? 1 : 0;
    public override void SetNetData(int d) => Attacking = d != 0;
}

/// <summary>Płomyk (EntityBlaze 1.8): unosi się nad celem, salwy 3 małych kul ognia, woda zadaje obrażenia.</summary>
public sealed class Blaze : Monster
{
    private float _heightOffset = .5f; private int _heightOffsetTime;
    private int _attackStep, _attackTime;
    public bool Charged;
    public Blaze(World.World w) : base(w) { MaxHealth = 20; Health = 20; MoveSpeed = .23; AttackDamage = 6; FollowRange = 48; }
    public override string TypeKey => "blaze";
    public override string DisplayName => "Płomyk";
    public override bool ImmuneToFire => true;
    public override string LivingSound => "mob.blaze.breathe";
    public override string HurtSound => "mob.blaze.hit";
    public override string DeathSound => "mob.blaze.death";
    public override int XpValue => 10;
    protected override void OnFall(float distance) { }

    public override void Tick()
    {
        base.Tick();
        if (Dead || Removed) return;
        var r = World.Rand;
        if (r.Next(24) == 0) World.PlaySound("fire.fire", X + .5, Y + .5, Z + .5, 1 + r.NextFloat(), r.NextFloat() * .7f + .3f);
        if (r.Next(2) == 0) World.SpawnParticles(ParticleKind.LargeSmoke, X + (r.NextDouble() - .5) * Width, Y + r.NextDouble() * Height, Z + (r.NextDouble() - .5) * Width, 1);
        if (!World.IsRemote && (InWater || WorldHooks.IsRainingAt?.Invoke(World, MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y + Height), MathUtil.FloorToInt(Z)) == true))
            Attack(DamageSource.Drown, 1f);
    }

    protected override void LivingUpdate()
    {
        if (!OnGround && MotionY < 0) MotionY *= .6;
        if (--_heightOffsetTime <= 0) { _heightOffsetTime = 100; _heightOffset = .5f + (float)World.Rand.NextGaussian() * 3; }
        if (Target != null && Target.Y + Target.EyeHeight > Y + EyeHeight + _heightOffset) MotionY += (.3 - MotionY) * .3;
        base.LivingUpdate();
    }

    protected override void Think()
    {
        if (Target != null && (Target.Dead || Target is Player tp && (tp.Creative || tp.Spectator) || Target.DistanceSq(this) > FollowRange * FollowRange)) Target = null;
        if (Target == null && World.Rand.Next(10) == 0) Target = NearestPlayer(FollowRange, true);
        if (LastAttacker is Player lp && !lp.Creative && !lp.Spectator && !lp.Dead) Target = lp;
        if (Target == null) { Charged = false; _attackStep = 0; LookAtPlayersRandomly(); Wander(MoveSpeed); return; }
        // EntityBlaze.AIFireballAttack
        _attackTime--;
        double d0 = DistanceSq(Target);
        LookTarget = Target; LookTime = 10;
        if (d0 < 4)
        {
            if (_attackTime <= 0) { _attackTime = 20; SwingArm(); Target.Attack(DamageSource.Mob(this), ScaledDamage); }
            Nav.Stop(); FaceTowards(Target.X, Target.Z); SetMoveSpeed((float)MoveSpeed);
        }
        else if (d0 < FollowRange * FollowRange)
        {
            double dx = Target.X - X, dy = Target.Y + Target.Height / 2 - (Y + Height / 2), dz = Target.Z - Z;
            if (_attackTime <= 0)
            {
                _attackStep++;
                if (_attackStep == 1) { _attackTime = 60; Charged = true; }
                else if (_attackStep <= 4) _attackTime = 6;
                else { _attackTime = 100; _attackStep = 0; Charged = false; }
                if (_attackStep > 1)
                {
                    float f = MathF.Sqrt(MathF.Sqrt((float)d0)) * .5f;
                    World.PlaySound("mob.ghast.fireball", X, Y, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f);
                    var fb = new SmallFireballEntity(World);
                    fb.Launch(this, X, Y + Height / 2 + .5 - fb.Height / 2, Z, dx + World.Rand.NextGaussian() * f, dy, dz + World.Rand.NextGaussian() * f, 0);
                    World.SpawnEntity(fb);
                }
            }
            FaceTowards(Target.X, Target.Z, 10);
        }
        else { Nav.Stop(); FaceTowards(Target.X, Target.Z); SetMoveSpeed((float)MoveSpeed); }
    }

    protected override void DropLoot(bool byPlayer, bool burning)
    {
        if (byPlayer) Drop(I.BlazeRod, World.Rand.Next(2 + NetherMobUtil.Looting(this)));
    }
    public override int NetData => Charged ? 1 : 0;
    public override void SetNetData(int d) => Charged = d != 0;
}

/// <summary>Kostka magmy (EntityMagmaCube 1.8): rozmiary 1, 2, 4, skoki co (10-29)*4 ticków, dzieli się po śmierci.</summary>
public sealed class MagmaCube : Monster
{
    public int Size = 1;
    public float SquishAmount, SquishFactor, PrevSquishFactor;
    private bool _wasOnGround; private int _jumpDelay;
    public MagmaCube(World.World w) : base(w) { SetSize(1 << w.Rand.Next(3)); MoveSpeed = .2; }
    public override string TypeKey => "magma_cube";
    public override string DisplayName => "Kostka magmy";
    public override bool ImmuneToFire => true;
    public override int ArmorValue => Size * 3;
    public override int XpValue => Size;
    public override string HurtSound => Size > 1 ? "mob.slime.big" : "mob.slime.small";
    public override string DeathSound => HurtSound;
    protected override void OnFall(float distance) { }

    public void SetSize(int size)
    {
        Size = size; Width = Height = .51000005f * size;
        MaxHealth = size * size; Health = MaxHealth;
    }

    public override void PrevTick() { base.PrevTick(); PrevSquishFactor = SquishFactor; }

    public override void Tick()
    {
        SquishFactor += (SquishAmount - SquishFactor) * .5f;
        base.Tick();
        if (Dead) return;
        if (OnGround && !_wasOnGround)
        {
            World.SpawnParticles(ParticleKind.Flame, X, Y, Z, Size * 8);
            World.PlaySound(Size > 1 ? "mob.magmacube.big" : "mob.magmacube.small", X, Y, Z, .4f * Size, ((World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f) / .8f);
            SquishAmount = -.5f;
        }
        else if (!OnGround && _wasOnGround) SquishAmount = 1;
        _wasOnGround = OnGround;
        SquishAmount *= .9f;
    }

    protected override void Think()
    {
        AcquireTarget();
        if (Target != null) FaceTowards(Target.X, Target.Z, 10);
        else if (World.Rand.NextFloat() < .05f) Yaw += (World.Rand.NextFloat() * 2 - 1) * 90;
        BodyYaw = Yaw;
        // SlimeMoveHelper: skok w stronę patrzenia, opóźnienie (10-29)*4, przy celu /3
        if (OnGround)
        {
            if (_jumpDelay-- <= 0)
            {
                _jumpDelay = (World.Rand.Next(20) + 10) * 4;
                if (Target != null) _jumpDelay /= 3;
                MotionY = .42 + Size * .1;
                SetMoveSpeed((float)MoveSpeed * 1.5f);
            }
            else { MoveForward = MoveStrafe = 0; }
        }
        else SetMoveSpeed((float)MoveSpeed * 1.5f);
        if (InLava) MotionY = .22 + Size * .05;
        // atak przy zetknięciu (EntitySlime.onCollideWithPlayer)
        if (Target != null && DistanceSq(Target) < .6 * Size * .6 * Size + .6 && CanSee(Target) && AttackCooldown-- <= 0)
        {
            AttackCooldown = 10;
            if (Target.Attack(DamageSource.Mob(this), Size + 2)) World.PlaySound("mob.attack", X, Y, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * .2f + 1f);
        }
    }

    public override void OnDeath(DamageSource src)
    {
        base.OnDeath(src);
        if (World.IsRemote || Size <= 1) return;
        int n = 2 + World.Rand.Next(3);
        for (int i = 0; i < n; i++)
        {
            var c = new MagmaCube(World); c.SetSize(Size / 2);
            double ox = (i % 2 - .5) * Size / 4.0, oz = (i / 2 - .5) * Size / 4.0;
            c.SetPositionAndPrev(X + ox, Y + .5, Z + oz); c.Yaw = World.Rand.NextFloat() * 360;
            World.SpawnEntity(c);
        }
    }

    protected override void DropLoot(bool byPlayer, bool burning)
    {
        if (Size <= 1) return;
        int looting = NetherMobUtil.Looting(this);
        int n = World.Rand.Next(4) - 2; if (looting > 0) n += World.Rand.Next(looting + 1);
        Drop(I.MagmaCream, n);
    }
    public override int NetData => Size;
    public override void SetNetData(int d) { if (d != Size && d > 0) SetSize(d); }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Size); }
    public override void Load(BinaryReader br) { base.Load(br); float h = Health; SetSize(br.ReadInt32()); Health = h; }
}

/// <summary>Witherowy szkielet (EntitySkeleton typ 1): kamienny miecz, efekt Wither 10 s, czaszka 2,5%.</summary>
public sealed class WitherSkeleton : Monster
{
    public WitherSkeleton(World.World w) : base(w)
    {
        Width = .72f; Height = 2.34f; MaxHealth = 20; Health = 20; MoveSpeed = .25; AttackDamage = 4;
        HeldItem = new ItemStack(I.StoneSword);
    }
    public override string TypeKey => "wither_skeleton";
    public override string DisplayName => "Witherowy szkielet";
    public override bool ImmuneToFire => true;
    public override string LivingSound => "mob.skeleton.say";
    public override string StepSoundName => "mob.skeleton.step";
    public override string HurtSound => "mob.skeleton.hurt";
    public override string DeathSound => "mob.skeleton.death";
    public override float EyeHeight => 2.1f;

    protected override void Think()
    {
        AcquireTarget();
        if (Target != null)
        {
            float hp = Target.Health;
            MeleeChase(MoveSpeed * 1.2);
            if (Target != null && Target.Health < hp) Target.AddEffect(Effect.Wither, 200);
        }
        else { LookAtPlayersRandomly(); Wander(MoveSpeed); }
    }

    protected override void DropLoot(bool byPlayer, bool burning)
    {
        int looting = NetherMobUtil.Looting(this);
        Drop(I.Coal, World.Rand.Next(3 + looting) - 1);
        Drop(I.Bone, World.Rand.Next(3 + looting));
        if (byPlayer && World.Rand.Next(200) - looting < 5 && Item.ById[I.SkullItem] != null) Drop(I.SkullItem, 1, 1);
        if (byPlayer && World.Rand.NextFloat() - looting * .01f < .085f) Drop(I.StoneSword, 1);
    }
}

/// <summary>Endermit (EntityEndermite 1.8): znika po 2400 tickach, pojawia się z perły Endu (5%).</summary>
public sealed class Endermite : Monster
{
    public int Lifetime; public bool SpawnedByPlayer;
    public Endermite(World.World w) : base(w) { Width = .4f; Height = .3f; MaxHealth = 8; Health = 8; MoveSpeed = .25; AttackDamage = 2; }
    public override string TypeKey => "endermite";
    public override string DisplayName => "Endermit";
    public override string LivingSound => "mob.silverfish.say";
    public override string StepSoundName => "mob.silverfish.step";
    public override string HurtSound => "mob.silverfish.hit";
    public override string DeathSound => "mob.silverfish.kill";
    public override float EyeHeight => .1f;
    public override int XpValue => 3;

    public override void Tick()
    {
        base.Tick();
        if (Dead) return;
        World.SpawnParticles(ParticleKind.Portal, X, Y + .1, Z, 2);
        if (!World.IsRemote && ++Lifetime >= 2400) Removed = true;
    }

    protected override void Think()
    {
        AcquireTarget();
        if (Target != null) MeleeChase(MoveSpeed);
        else { LookAtPlayersRandomly(); Wander(MoveSpeed); }
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Lifetime); bw.Write(SpawnedByPlayer); }
    public override void Load(BinaryReader br) { base.Load(br); Lifetime = br.ReadInt32(); SpawnedByPlayer = br.ReadBoolean(); }
}
