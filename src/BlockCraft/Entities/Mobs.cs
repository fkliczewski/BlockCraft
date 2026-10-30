using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public abstract partial class Mob : LivingEntity
{
    public readonly Navigator Nav;
    public LivingEntity Target;
    public double MoveSpeed = 0.25;
    protected int WanderCooldown;
    protected int LivingSoundTimer;
    public int IdleTime;
    protected Entity LookTarget; protected int LookTime;

    protected Mob(World.World w) : base(w) { Nav = new Navigator(this); Health = MaxHealth; }

    public void SetMoveSpeed(float s) { AiMoveSpeed = s; MoveForward = s; }

    public virtual string LivingSound => null;
    public virtual int LivingSoundInterval => 80;

    protected override void UpdateAI()
    {
        IdleTime++;
        MoveForward = 0; MoveStrafe = 0; IsJumping = false;
        if (InWater && World.Rand.NextFloat() < 0.8f) IsJumping = true;
        if (LivingSound != null && World.Rand.Next(1000) < LivingSoundTimer++) { LivingSoundTimer = -LivingSoundInterval; World.PlaySound(LivingSound, X, Y + EyeHeight, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.2f + 1f); }
        Think();
        Nav.Tick();
        if (InWater && !Nav.HasPath) IsJumping = true;
        UpdateLook();
    }

    protected abstract void Think();

    protected void UpdateLook()
    {
        if (LookTarget != null && LookTime > 0)
        {
            LookTime--;
            double dx = LookTarget.X - X, dz = LookTarget.Z - Z, dy = LookTarget.Y + LookTarget.EyeHeight - (Y + EyeHeight);
            float yaw = (float)(Math.Atan2(dz, dx) * MathUtil.Rad2Deg) - 90f;
            if (!Nav.HasPath && MoveForward == 0) Yaw += Math.Clamp(MathUtil.WrapDegrees(yaw - Yaw), -10f, 10f);
            Pitch = (float)(-Math.Atan2(dy, Math.Sqrt(dx * dx + dz * dz)) * MathUtil.Rad2Deg);
            HeadYaw = yaw;
        }
        else Pitch *= 0.9f;
    }

    protected void LookAtPlayersRandomly()
    {
        if (LookTime <= 0 && World.Rand.NextFloat() < 0.02f)
        {
            foreach (var p in World.Players)
                if (p.DistanceSq(this) < 64 && !p.Spectator) { LookTarget = p; LookTime = 40 + World.Rand.Next(40); break; }
        }
    }

    protected void Wander(double speed, int chance = 120)
    {
        if (Nav.HasPath) return;
        if (World.Rand.Next(chance) != 0) return;
        for (int i = 0; i < 10; i++)
        {
            int tx = MathUtil.FloorToInt(X) + World.Rand.Next(21) - 10, tz = MathUtil.FloorToInt(Z) + World.Rand.Next(21) - 10;
            int ty = MathUtil.FloorToInt(Y) + World.Rand.Next(7) - 3;
            if (Nav.MoveTo(tx, ty, tz, speed)) return;
        }
    }

    protected void FaceTowards(double x, double z, float maxTurn = 30f)
    {
        float yaw = (float)(Math.Atan2(z - Z, x - X) * MathUtil.Rad2Deg) - 90f;
        Yaw += Math.Clamp(MathUtil.WrapDegrees(yaw - Yaw), -maxTurn, maxTurn);
    }

    protected Player NearestPlayer(double range, bool needSight)
    {
        Player best = null; double bd = range * range;
        foreach (var p in World.Players)
        {
            if (p.Dead || p.Creative || p.Spectator) continue;
            double d = p.DistanceSq(this);
            if (d < bd && (!needSight || CanSee(p))) { bd = d; best = p; }
        }
        return best;
    }

    public override void Save(BinaryWriter bw) { base.Save(bw); }
    public override void Load(BinaryReader br) { base.Load(br); }
}

// ===================== ZWIERZĘTA =====================
public abstract class Animal : Mob
{
    public int GrowingAge; // <0 młode
    public int InLove;
    protected int PanicTicks;
    protected Animal Mate; protected int MateTicks;
    public override bool Persistent => true;
    public bool IsBaby => GrowingAge < 0;
    public abstract bool IsBreedingItem(ItemStack s);
    public abstract Animal CreateChild();
    public override int XpValue => 1 + World.Rand.Next(3);

    protected Animal(World.World w) : base(w) { }

    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled || Dead) return;
        if (GrowingAge < 0) { GrowingAge++; }
        else if (GrowingAge > 0) GrowingAge--;
        if (InLove > 0)
        {
            InLove--;
            if (InLove % 10 == 0) World.SpawnParticles(ParticleKind.Heart, X, Y + Height + 0.2, Z, 1);
        }
    }

    protected override void OnHurt(DamageSource src)
    {
        base.OnHurt(src);
        PanicTicks = 60 + World.Rand.Next(40);
        InLove = 0;
        Nav.Stop();
    }

    protected override void Think()
    {
        double baseSpeed = MoveSpeed;
        if (PanicTicks > 0)
        {
            PanicTicks--;
            if (!Nav.HasPath)
                for (int i = 0; i < 10; i++)
                {
                    int tx = MathUtil.FloorToInt(X) + World.Rand.Next(11) - 5, tz = MathUtil.FloorToInt(Z) + World.Rand.Next(11) - 5;
                    if (Nav.MoveTo(tx, Y, tz, baseSpeed * 1.25 * 1.25)) break;
                }
            else Nav.SetSpeed(baseSpeed * 1.6);
            return;
        }
        // rozmnażanie
        if (InLove > 0 && !IsBaby)
        {
            if (Mate == null || Mate.Dead || Mate.InLove <= 0)
            {
                Mate = null;
                foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(8, 4, 8), this))
                    if (e.GetType() == GetType() && e is Animal a && a.InLove > 0 && !a.IsBaby && a != this) { Mate = a; break; }
            }
            if (Mate != null)
            {
                LookTarget = Mate; LookTime = 10;
                if (DistanceSq(Mate) > 4) { if (!Nav.HasPath || World.Rand.Next(20) == 0) Nav.MoveTo(Mate.X, Mate.Y, Mate.Z, baseSpeed); }
                else Nav.Stop();
                if (++MateTicks >= 60 && DistanceSq(Mate) < 9)
                {
                    var child = CreateChild();
                    child.GrowingAge = -24000;
                    child.SetPositionAndPrev(X, Y, Z);
                    child.Yaw = Yaw;
                    World.SpawnEntity(child);
                    GrowingAge = 6000; Mate.GrowingAge = 6000; InLove = 0; Mate.InLove = 0; MateTicks = 0; Mate.MateTicks = 0;
                    World.SpawnParticles(ParticleKind.Heart, X, Y + Height, Z, 7);
                    World.SpawnXp(X, Y, Z, 1 + World.Rand.Next(7));
                    Mate = null;
                }
                return;
            }
        }
        // podążanie za graczem z jedzeniem
        foreach (var p in World.Players)
        {
            if (p.Dead || p.Spectator) continue;
            if (p.DistanceSq(this) < 100 && IsBreedingItem(p.Inventory.Selected))
            {
                LookTarget = p; LookTime = 10;
                if (p.DistanceSq(this) > 6.25) { if (!Nav.HasPath || World.Rand.Next(10) == 0) Nav.MoveTo(p.X, p.Y, p.Z, baseSpeed * 1.2); }
                else Nav.Stop();
                return;
            }
        }
        // młode podążają za rodzicem
        if (IsBaby && World.Rand.Next(40) == 0)
        {
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(8, 4, 8), this))
                if (e.GetType() == GetType() && e is Animal a && !a.IsBaby)
                {
                    if (DistanceSq(a) > 9) Nav.MoveTo(a.X, a.Y, a.Z, baseSpeed * 1.1);
                    break;
                }
        }
        LookAtPlayersRandomly();
        Wander(baseSpeed);
    }

    public override bool Interact(Player p)
    {
        var held = p.Inventory.Selected;
        if (IsBreedingItem(held))
        {
            if (IsBaby) { GrowingAge = Math.Min(0, GrowingAge + (-GrowingAge) / 10); }
            else if (GrowingAge == 0 && InLove <= 0) { InLove = 600; }
            else return false;
            if (!p.Creative) { held.Count--; if (held.Count <= 0) p.Inventory.Selected = null; p.Inventory.Dirty = true; }
            World.SpawnParticles(ParticleKind.Heart, X, Y + Height, Z, 3);
            return true;
        }
        return false;
    }

    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(GrowingAge); bw.Write(InLove); }
    public override void Load(BinaryReader br) { base.Load(br); GrowingAge = br.ReadInt32(); InLove = br.ReadInt32(); }
    public override int NetData => GrowingAge < 0 ? 1 : 0;
    public override void SetNetData(int d) { GrowingAge = (d & 1) != 0 ? -1 : 0; }
}

public sealed class Cow : Animal
{
    public Cow(World.World w) : base(w) { Width = 0.9f; Height = 1.4f; MaxHealth = 10; Health = 10; MoveSpeed = 0.2; }
    public override string TypeKey => "cow";
    public override string DisplayName => "Krowa";
    public override string LivingSound => "cow";
    public override string HurtSound => "cow_hurt";
    public override bool IsBreedingItem(ItemStack s) => s?.Id == I.Wheat;
    public override Animal CreateChild() => new Cow(World);
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        if (IsBaby) return;
        Drop(I.Leather, World.Rand.Next(3));
        Drop(burning ? I.Steak : I.RawBeef, 1 + World.Rand.Next(3));
    }
    public override bool Interact(Player p)
    {
        var held = p.Inventory.Selected;
        if (held?.Id == I.Bucket && !IsBaby)
        {
            if (p.Creative) return true;
            held.Count--;
            if (held.Count <= 0) p.Inventory.Selected = new ItemStack(I.MilkBucket);
            else if (!p.Inventory.Add(new ItemStack(I.MilkBucket))) p.DropStack(new ItemStack(I.MilkBucket), false);
            p.Inventory.Dirty = true;
            return true;
        }
        return base.Interact(p);
    }
}

public sealed class Pig : Animal
{
    public Pig(World.World w) : base(w) { Width = 0.9f; Height = 0.9f; MaxHealth = 10; Health = 10; MoveSpeed = 0.25; }
    public override string TypeKey => "pig";
    public override string DisplayName => "Świnia";
    public override string LivingSound => "pig";
    public override string HurtSound => "pig";
    public override bool IsBreedingItem(ItemStack s) => s?.Id == I.Wheat || s?.Id == I.Apple;
    public override Animal CreateChild() => new Pig(World);
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        if (IsBaby) return;
        Drop(burning ? I.CookedPorkchop : I.RawPorkchop, 1 + World.Rand.Next(3));
    }
}

public sealed class Sheep : Animal
{
    public int Color; public bool Sheared; private int _eatTimer;
    public Sheep(World.World w) : base(w)
    {
        Width = 0.9f; Height = 1.3f; MaxHealth = 8; Health = 8; MoveSpeed = 0.23;
        int r = w.Rand.Next(100);
        Color = r < 5 ? 15 : r < 10 ? 7 : r < 15 ? 8 : r < 18 ? 12 : w.Rand.Next(500) == 0 ? 6 : 0; // EntitySheep.getRandomSheepColor (kolory EnumDyeColor)
    }
    public override string TypeKey => "sheep";
    public override string DisplayName => "Owca";
    public override string LivingSound => "sheep";
    public override string HurtSound => "sheep";
    public int EatAnim => _eatTimer;
    public override bool IsBreedingItem(ItemStack s) => s?.Id == I.Wheat;
    public override Animal CreateChild()
    {
        var s = new Sheep(World);
        s.Color = World.Rand.NextBool() ? Color : (Mate as Sheep)?.Color ?? Color;
        return s;
    }
    protected override void Think()
    {
        if (_eatTimer > 0)
        {
            _eatTimer--;
            Nav.Stop();
            if (_eatTimer == 4)
            {
                int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(Y), bz = MathUtil.FloorToInt(Z);
                if (World.GetBlock(bx, by, bz) == B.TallGrass) { World.BreakBlock(bx, by, bz, false); EatGrass(); }
                else if (World.GetBlock(bx, by - 1, bz) == B.Grass)
                {
                    World.PlayEffectBreak(bx, by - 1, bz, B.Grass, 0);
                    World.SetBlock(bx, by - 1, bz, B.Dirt);
                    EatGrass();
                }
            }
            return;
        }
        if (World.Rand.Next(IsBaby ? 50 : 1000) == 0 && PanicTicks == 0 && InLove == 0)
        {
            int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(Y), bz = MathUtil.FloorToInt(Z);
            if (World.GetBlock(bx, by, bz) == B.TallGrass || World.GetBlock(bx, by - 1, bz) == B.Grass) { _eatTimer = 40; return; }
        }
        base.Think();
    }
    private void EatGrass()
    {
        Sheared = false;
        if (IsBaby) GrowingAge = Math.Min(0, GrowingAge + 60 * 20);
    }
    public override bool Interact(Player p)
    {
        var held = p.Inventory.Selected;
        if (held?.Id == I.Shears && !Sheared && !IsBaby)
        {
            Sheared = true;
            int n = 1 + World.Rand.Next(3);
            for (int i = 0; i < n; i++)
            {
                var e = new ItemEntity(World, X, Y + 1, Z, new ItemStack(B.Wool, 1, Color));
                e.MotionY += World.Rand.NextFloat() * 0.05f; e.MotionX += (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.1f; e.MotionZ += (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.1f;
                World.SpawnEntity(e);
            }
            p.DamageHeld(1);
            World.PlaySound("shear", X, Y, Z);
            return true;
        }
        return base.Interact(p);
    }
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        if (IsBaby) return;
        if (!Sheared) Drop(B.Wool, 1, Color);
        Drop(burning ? I.CookedMutton : I.RawMutton, 1 + World.Rand.Next(2));
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Color); bw.Write(Sheared); }
    public override void Load(BinaryReader br) { base.Load(br); Color = br.ReadInt32(); Sheared = br.ReadBoolean(); }
    public override int NetData => base.NetData | (Sheared ? 2 : 0) | (Color << 2) | (_eatTimer > 0 ? 64 : 0);
    public override void SetNetData(int d) { base.SetNetData(d); Sheared = (d & 2) != 0; Color = (d >> 2) & 15; _eatTimer = (d & 64) != 0 ? Math.Max(_eatTimer, 20) : 0; }
}

public sealed class Chicken : Animal
{
    public float WingRotation, PrevWingRotation, FlapSpeed = 1f;
    public int EggTimer;
    public Chicken(World.World w) : base(w)
    {
        Width = 0.4f; Height = 0.7f; MaxHealth = 4; Health = 4; MoveSpeed = 0.25; EggTimer = w.Rand.Next(6000) + 6000;
    }
    public override string TypeKey => "chicken";
    public override string DisplayName => "Kurczak";
    public override string LivingSound => "chicken";
    public override string HurtSound => "chicken_hurt";
    public override bool IsBreedingItem(ItemStack s) => s?.Id == I.Seeds;
    public override Animal CreateChild() => new Chicken(World);
    public override void PrevTick() { base.PrevTick(); PrevWingRotation = WingRotation; }
    public override void Tick()
    {
        base.Tick();
        WingRotation += (OnGround ? 0.3f : 1.2f) * FlapSpeed;
        if (IsRemoteControlled || Dead) return;
        if (!OnGround && MotionY < 0) MotionY *= 0.6;
        if (!World.IsRemote && !IsBaby && --EggTimer <= 0)
        {
            World.PlaySound("chicken_plop", X, Y, Z, 1f, (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.2f + 1f);
            World.DropItem(X, Y, Z, new ItemStack(I.Egg), false);
            EggTimer = World.Rand.Next(6000) + 6000;
        }
    }
    protected override void OnFall(float distance) { }
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        if (IsBaby) return;
        Drop(I.Feather, World.Rand.Next(3));
        Drop(burning ? I.CookedChicken : I.RawChicken, 1);
    }
}

// ===================== POTWORY =====================
public abstract class Monster : Mob
{
    public float AttackDamage = 3f;
    protected int AttackCooldown;
    protected double FollowRange = 32;
    public override int XpValue => 5;
    protected Monster(World.World w) : base(w) { }

    public float ScaledDamage => World.Difficulty switch
    {
        Difficulty.Easy => AttackDamage / 2f + 1f,
        Difficulty.Hard => AttackDamage * 1.5f,
        _ => AttackDamage
    };

    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled || World.IsRemote) return;
        if (World.Difficulty == Difficulty.Peaceful) { Removed = true; return; }
        // despawn
        Player near = null; double nd = double.MaxValue;
        foreach (var p in World.Players) { double d = p.DistanceSq(this); if (d < nd) { nd = d; near = p; } }
        if (near == null || nd > 128 * 128) { Removed = true; return; }
        if (nd > 32 * 32 && IdleTime > 600 && World.Rand.Next(800) == 0) { Removed = true; return; }
        if (nd < 32 * 32) IdleTime = 0;
    }

    protected bool BurnsInDaylight()
    {
        if (!World.IsDaytime() || World.IsRemote) return false;
        float br = World.GetRawLight(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y + EyeHeight), MathUtil.FloorToInt(Z)) / 15f;
        if (br > 0.5f && World.Rand.NextFloat() * 30f < (br - 0.4f) * 2f && World.CanSeeSky(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y + EyeHeight), MathUtil.FloorToInt(Z)) && !InWater)
        { FireTicks = Math.Max(FireTicks, 160); return true; }
        return false;
    }

    protected void AcquireTarget()
    {
        if (Target != null && (Target.Dead || Target is Player p && (p.Creative || p.Spectator) || Target.DistanceSq(this) > FollowRange * FollowRange)) Target = null;
        if (Target == null && World.Rand.Next(10) == 0) Target = NearestPlayer(16, true);
        if (LastAttacker is Player lp && !lp.Creative && !lp.Spectator && !lp.Dead) Target = lp;
    }

    protected void MeleeChase(double speed)
    {
        if (Target == null) return;
        LookTarget = Target; LookTime = 10;
        double d2 = DistanceSq(Target);
        if (!Nav.HasPath || World.Rand.Next(10) == 0 || Nav.Target.Item1 != MathUtil.FloorToInt(Target.X) && World.Rand.Next(4) == 0)
        {
            if (!Nav.MoveTo(Target.X, Target.Y, Target.Z, speed) && d2 < 16)
            {
                FaceTowards(Target.X, Target.Z); SetMoveSpeed((float)speed);
                if (CollidedH) IsJumping = true;
            }
        }
        if (AttackCooldown > 0) AttackCooldown--;
        double reach = Width * 2 * Width * 2 + Target.Width;
        if (d2 <= reach + 0.5 && Math.Abs(Target.Y - Y) < 2 && AttackCooldown <= 0)
        {
            AttackCooldown = 20;
            SwingArm();
            if (Target.Attack(DamageSource.Mob(this), ScaledDamage))
            {
                if (FireTicks > 0 && World.Rand.NextFloat() < 0.3f * (int)World.Difficulty) Target.FireTicks = 60;
            }
        }
    }
}

public sealed class Zombie : Monster
{
    public Zombie(World.World w) : base(w) { MaxHealth = 20; Health = 20; MoveSpeed = 0.23; AttackDamage = 3; FollowRange = 40; }
    public override string TypeKey => "zombie";
    public override string DisplayName => "Zombie";
    public override string LivingSound => "zombie";
    public override string HurtSound => "zombie_hurt";
    public override string DeathSound => "zombie_death";
    public override int ArmorValue => 2;
    protected override void Think()
    {
        BurnsInDaylight();
        AcquireTarget();
        if (Target != null) MeleeChase(MoveSpeed * 1.0);
        else { LookAtPlayersRandomly(); Wander(MoveSpeed); }
    }
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        Drop(I.RottenFlesh, World.Rand.Next(3));
        if (byPlayer && World.Rand.Next(200) == 0) Drop(I.IronIngot, 1);
    }
}

public sealed class Skeleton : Monster
{
    private int _shootTimer = 40; private int _strafeTime; private bool _strafeLeft;
    public Skeleton(World.World w) : base(w) { MaxHealth = 20; Health = 20; MoveSpeed = 0.25; AttackDamage = 2; HeldItem = new ItemStack(I.Bow); }
    public override string TypeKey => "skeleton";
    public override string DisplayName => "Szkielet";
    public override string LivingSound => "skeleton";
    public override string HurtSound => "skeleton_hurt";
    public override string DeathSound => "skeleton_death";
    public bool Aiming;
    protected override void Think()
    {
        BurnsInDaylight();
        AcquireTarget();
        Aiming = false;
        if (Target == null) { LookAtPlayersRandomly(); Wander(MoveSpeed); return; }
        LookTarget = Target; LookTime = 10;
        double d2 = DistanceSq(Target);
        bool see = CanSee(Target);
        if (d2 > 15 * 15 || !see)
        {
            if (!Nav.HasPath || World.Rand.Next(20) == 0) Nav.MoveTo(Target.X, Target.Y, Target.Z, MoveSpeed);
        }
        else
        {
            Nav.Stop();
            FaceTowards(Target.X, Target.Z, 30);
            if (++_strafeTime > 20 + World.Rand.Next(20)) { _strafeTime = 0; _strafeLeft = !_strafeLeft; }
            MoveStrafe = _strafeLeft ? 0.5f : -0.5f; AiMoveSpeed = (float)MoveSpeed * 0.5f;
            if (d2 < 25) { MoveForward = -0.5f; }
        }
        if (see && d2 < 15 * 15)
        {
            Aiming = _shootTimer < 20;
            if (--_shootTimer <= 0)
            {
                double dist = Math.Sqrt(d2);
                _shootTimer = (int)(20 + dist / 15 * 40);
                var a = new ArrowEntity(World) { Shooter = this, PickupAllowed = false };
                a.SetPositionAndPrev(X, Y + EyeHeight - 0.1, Z);
                double dx = Target.X - X, dz = Target.Z - Z, dy = Target.Y + Target.Height / 3 - a.Y;
                double h = Math.Sqrt(dx * dx + dz * dz);
                a.Shoot(dx, dy + h * 0.2, dz, 1.6f, 14 - (int)World.Difficulty * 4);
                a.Damage = 2 + (int)World.Difficulty * 0.11 + World.Rand.NextGaussian() * 0.25;
                World.SpawnEntity(a);
                World.PlaySound("bow", X, Y, Z, 1f, 1f / (World.Rand.NextFloat() * 0.4f + 0.8f));
                SwingArm();
            }
        }
    }
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        Drop(I.Bone, World.Rand.Next(3));
        Drop(I.Arrow, World.Rand.Next(3));
    }
    public override int NetData => Aiming ? 1 : 0;
    public override void SetNetData(int d) => Aiming = d != 0;
}

public sealed class Creeper : Monster
{
    public int Fuse, PrevFuse; public const int MaxFuse = 30;
    private bool _ignited;
    public Creeper(World.World w) : base(w) { MaxHealth = 20; Health = 20; MoveSpeed = 0.25; Height = 1.7f; }
    public override string TypeKey => "creeper";
    public override string DisplayName => "Creeper";
    public override string HurtSound => "creeper_hurt";
    public override string DeathSound => "creeper_death";
    public override void PrevTick() { base.PrevTick(); PrevFuse = Fuse; }
    protected override void Think()
    {
        AcquireTarget();
        if (Target == null) { if (Fuse > 0) Fuse--; LookAtPlayersRandomly(); Wander(MoveSpeed); return; }
        double d2 = DistanceSq(Target);
        bool swelling = d2 < 9 && CanSee(Target) || _ignited && d2 < 49;
        if (swelling)
        {
            Nav.Stop();
            LookTarget = Target; LookTime = 10;
            if (Fuse == 0) World.PlaySound("fuse", X, Y, Z, 1f, 0.5f);
            _ignited = true;
            Fuse++;
            if (Fuse >= MaxFuse)
            {
                Removed = true; Dead = true;
                Explosion.Explode(World, this, X, Y + 0.5, Z, 3f, true);
            }
        }
        else
        {
            _ignited = false;
            if (Fuse > 0) Fuse--;
            if (d2 > 49 || Fuse == 0) MeleeChaseNoAttack();
        }
    }
    private void MeleeChaseNoAttack()
    {
        LookTarget = Target; LookTime = 10;
        if (!Nav.HasPath || World.Rand.Next(10) == 0) Nav.MoveTo(Target.X, Target.Y, Target.Z, MoveSpeed);
    }
    protected override void DropLoot(bool byPlayer, bool burning) => Drop(I.Gunpowder, World.Rand.Next(3));
    public override int NetData => Fuse;
    public override void SetNetData(int d) { Fuse = d; }
}

public sealed class Spider : Monster
{
    private int _leapCooldown;
    public Spider(World.World w) : base(w) { Width = 1.4f; Height = 0.9f; MaxHealth = 16; Health = 16; MoveSpeed = 0.3; AttackDamage = 2; }
    public override string TypeKey => "spider";
    public override string DisplayName => "Pająk";
    public override string LivingSound => "spider";
    public override string HurtSound => "spider";
    public override string DeathSound => "spider_death";
    public override float EyeHeight => 0.65f;
    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled || Dead) return;
        if (CollidedH) { MotionY = 0.2; FallDistance = 0; }
    }
    protected override void OnFall(float distance) { }
    protected override void Think()
    {
        float br = World.GetLight(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y + 0.5), MathUtil.FloorToInt(Z)) / 15f;
        if (Target == null && br < 0.5f) AcquireTarget();
        else if (Target != null)
        {
            AcquireTarget();
            if (br > 0.5f && World.Rand.Next(100) == 0 && LastAttacker != Target) Target = null;
        }
        if (LastAttacker is Player lp && !lp.Creative && !lp.Dead) Target = lp;
        if (Target == null) { LookAtPlayersRandomly(); Wander(MoveSpeed); return; }
        double d2 = DistanceSq(Target);
        if (_leapCooldown > 0) _leapCooldown--;
        if (d2 > 4 && d2 < 16 && OnGround && _leapCooldown == 0 && World.Rand.Next(10) == 0)
        {
            double dx = Target.X - X, dz = Target.Z - Z, h = Math.Sqrt(dx * dx + dz * dz);
            MotionX = dx / h * 0.5 * 0.8 + MotionX * 0.2; MotionZ = dz / h * 0.5 * 0.8 + MotionZ * 0.2; MotionY = 0.4;
            _leapCooldown = 20;
        }
        MeleeChase(MoveSpeed);
    }
    protected override void DropLoot(bool byPlayer, bool burning)
    {
        Drop(I.String, World.Rand.Next(3));
        if (byPlayer && World.Rand.Next(3) == 0) Drop(I.SpiderEye, 1);
    }
}

public sealed class Enderman : Monster
{
    public bool Angry; private int _stareTicks; private int _teleportDelay;
    public Enderman(World.World w) : base(w) { Width = 0.6f; Height = 2.9f; MaxHealth = 40; Health = 40; MoveSpeed = 0.3; AttackDamage = 7; FollowRange = 64; StepHeight = 1f; }
    public override string TypeKey => "enderman";
    public override string DisplayName => "Enderman";
    public override string LivingSound => Angry ? "endermen_scream" : "endermen_idle";
    public override string HurtSound => "endermen_hit";
    public override string DeathSound => "endermen_death";
    public override float EyeHeight => 2.55f;
    public override bool IsInvulnerableTo(DamageSource s) => s.Type == "arrow" && TeleportRandomly();
    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled || Dead) return;
        if (World.Rand.Next(4) == 0) World.SpawnParticles(ParticleKind.Portal, X, Y + World.Rand.NextDouble() * Height, Z, 1);
        if (World.IsRemote) return;
        if (InWater) { Attack(DamageSource.Drown, 1f); TeleportRandomly(); }
        if (World.IsDaytime() && World.Rand.NextFloat() * 30 < (World.GetRawLight(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y + EyeHeight), MathUtil.FloorToInt(Z)) / 15f - 0.4f) * 2f
            && World.CanSeeSky(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y + EyeHeight), MathUtil.FloorToInt(Z)) && !Angry)
        { if (World.Rand.Next(30) == 0) TeleportRandomly(); }
    }
    private bool IsLookedAtBy(Player p)
    {
        var (lx, ly, lz) = p.Look();
        double dx = X - p.X, dy = Y + EyeHeight - (p.Y + p.EyeHeight), dz = Z - p.Z;
        double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        double dot = (lx * dx + ly * dy + lz * dz) / d;
        return dot > 1 - 0.025 / d && p.CanSee(this);
    }
    protected override void Think()
    {
        if (!Angry)
        {
            foreach (var p in World.Players)
            {
                if (p.Dead || p.Creative || p.Spectator || p.DistanceSq(this) > 64 * 64) continue;
                if (IsLookedAtBy(p))
                {
                    if (++_stareTicks > 5) { Angry = true; Target = p; World.PlaySound("endermen_stare", X, Y, Z, 1f, 1f); _teleportDelay = 0; }
                    LookTarget = p; LookTime = 20;
                }
            }
            if (LastAttacker is Player lp && !lp.Creative) { Angry = true; Target = lp; }
            if (World.Rand.Next(400) == 0) TeleportRandomly();
            LookAtPlayersRandomly(); Wander(MoveSpeed * 0.8);
            return;
        }
        if (Target == null || Target.Dead || Target is Player pl && (pl.Creative || pl.Spectator)) { Angry = false; Target = null; _stareTicks = 0; return; }
        if (DistanceSq(Target) > 16 * 16 && ++_teleportDelay > 30 && TeleportTowards(Target)) _teleportDelay = 0;
        MeleeChase(MoveSpeed * 1.2);
    }
    public bool TeleportRandomly()
    {
        double tx = X + (World.Rand.NextDouble() - 0.5) * 64, ty = Y + World.Rand.Next(64) - 32, tz = Z + (World.Rand.NextDouble() - 0.5) * 64;
        return TeleportTo(tx, ty, tz);
    }
    private bool TeleportTowards(Entity e)
    {
        double dx = X - e.X, dy = Y + Height / 2 - e.Y - e.EyeHeight, dz = Z - e.Z;
        double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        dx /= d; dy /= d; dz /= d;
        return TeleportTo(X + (World.Rand.NextDouble() - 0.5) * 8 - dx * 16, Y + World.Rand.Next(16) - 8 - dy * 16, Z + (World.Rand.NextDouble() - 0.5) * 8 - dz * 16);
    }
    private bool TeleportTo(double x, double y, double z)
    {
        int bx = MathUtil.FloorToInt(x), by = MathUtil.FloorToInt(y), bz = MathUtil.FloorToInt(z);
        if (!World.IsLoadedAt(bx, bz)) return false;
        while (by > 1 && !Block.Get(World.GetBlock(bx, by - 1, bz)).Solid) by--;
        if (by <= 1) return false;
        for (int i = 0; i < 3; i++) if (Block.Get(World.GetBlock(bx, by + i, bz)).Solid || World.GetBlock(bx, by + i, bz) == B.Water) return false;
        World.SpawnParticles(ParticleKind.Portal, X, Y + 1, Z, 64);
        SetPositionAndPrev(bx + 0.5, by, bz + 0.5);
        Nav.Stop();
        World.PlaySound("endermen_portal", X, Y, Z, 1f, 1f);
        World.SpawnParticles(ParticleKind.Portal, X, Y + 1, Z, 64);
        return true;
    }
    protected override void OnHurt(DamageSource src)
    {
        base.OnHurt(src);
        if (src.Attacker is Player p && !p.Creative) { Angry = true; Target = p; }
        if (World.Rand.Next(10) != 0) TeleportRandomly();
    }
    protected override void DropLoot(bool byPlayer, bool burning) => Drop(I.EnderPearl, World.Rand.Next(2));
    public override int NetData => Angry ? 1 : 0;
    public override void SetNetData(int d) => Angry = d != 0;
    public override int XpValue => 5;
}
