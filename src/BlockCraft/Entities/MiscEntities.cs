using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public sealed class ItemEntity : Entity
{
    public ItemStack Stack;
    public int PickupDelay;
    public int Lifetime;
    public float Bob;

    public ItemEntity(World.World w, double x, double y, double z, ItemStack s) : base(w)
    {
        Width = 0.25f; Height = 0.25f; StepHeight = 0;
        SetPositionAndPrev(x, y, z); Stack = s;
        Bob = (float)(w.Rand.NextDouble() * Math.PI * 2);
        Yaw = (float)(w.Rand.NextDouble() * 360);
    }
    public override string TypeKey => "item";
    public override bool Persistent => true;

    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled) return;
        if (PickupDelay > 0) PickupDelay--;
        MotionY -= 0.04;
        if (InWater) { MotionY += 0.05; if (MotionY > 0.1) MotionY = 0.1; }
        if (InLava) { MotionY = 0.2; MotionX = (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.2; MotionZ = (World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.2; World.PlaySound("fizz", X, Y, Z, 0.4f, 2f); Kill(); return; }
        // wypychanie z bloku
        int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(Y + 0.1), bz = MathUtil.FloorToInt(Z);
        if (Block.Get(World.GetBlock(bx, by, bz)).Opaque) { MotionY = 0.2; NoClip = true; } else NoClip = false;
        MoveWithCollision(MotionX, MotionY, MotionZ);
        float f = 0.98f;
        if (OnGround) f = 0.6f * 0.98f;
        MotionX *= f; MotionY *= 0.98; MotionZ *= f;
        if (OnGround) MotionY *= -0.5;
        Lifetime++;
        if (Lifetime >= 6000) Kill();
        // łączenie stosów
        if (Age % 20 == 0 && !World.IsRemote && Stack.Count < Stack.MaxStack)
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(0.5, 0, 0.5), this))
                if (e is ItemEntity o && !o.Removed && o.Stack.CanStackWith(Stack) && o.Stack.Count + Stack.Count <= Stack.MaxStack)
                { Stack.Count += o.Stack.Count; o.Kill(); Lifetime = Math.Min(Lifetime, o.Lifetime); }
    }

    public override void OnCollideWithPlayer(Player p)
    {
        if (PickupDelay > 0 || Removed || p.Dead) return;
        int before = Stack.Count;
        if (p.Inventory.Add(Stack))
        {
            p.Handler?.OnPickup(this, p);
            World.PlaySound("pop", X, Y, Z, 0.2f, ((World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.7f + 1f) * 2f);
            Kill();
        }
        else if (Stack.Count != before) p.Handler?.OnPickup(this, p);
    }

    public override void Save(BinaryWriter bw) { base.Save(bw); bw.WriteStack(Stack); bw.Write(Lifetime); bw.Write(PickupDelay); }
    public override void Load(BinaryReader br) { base.Load(br); Stack = br.ReadStack() ?? new ItemStack(1); Lifetime = br.ReadInt32(); PickupDelay = br.ReadInt32(); }
    public override int NetData => Stack.Id | (Stack.Damage << 16);
    public override void SetNetData(int d) { Stack = new ItemStack(d & 0xFFFF, 1, d >> 16); }
}

public sealed class XpOrbEntity : Entity
{
    public int Value;
    public int PickupDelay;
    public XpOrbEntity(World.World w, double x, double y, double z, int value) : base(w)
    {
        Width = 0.5f; Height = 0.5f; StepHeight = 0; SetPositionAndPrev(x, y, z); Value = value; PickupDelay = 10;
    }
    public override string TypeKey => "xp_orb";
    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled) return;
        if (PickupDelay > 0) PickupDelay--;
        MotionY -= 0.03;
        Player closest = null; double best = 64;
        foreach (var p in World.Players) { double d = p.DistanceSq(X, Y, Z); if (d < best && !p.Dead && !p.Spectator) { best = d; closest = p; } }
        if (closest != null)
        {
            double dx = (closest.X - X) / 8, dy = (closest.Y + closest.EyeHeight / 2 - Y) / 8, dz = (closest.Z - Z) / 8;
            double d = Math.Sqrt(dx * dx + dy * dy + dz * dz), f = 1 - d;
            if (f > 0) { f *= f; MotionX += dx / d * f * 0.1; MotionY += dy / d * f * 0.1; MotionZ += dz / d * f * 0.1; }
        }
        MoveWithCollision(MotionX, MotionY, MotionZ);
        float fr = OnGround ? 0.6f * 0.98f : 0.98f;
        MotionX *= fr; MotionY *= 0.98; MotionZ *= fr;
        if (OnGround) MotionY *= -0.9;
        if (Age > 6000) Kill();
    }
    public override void OnCollideWithPlayer(Player p)
    {
        if (PickupDelay > 0 || Removed) return;
        World.PlaySound("orb", X, Y, Z, 0.1f, 0.5f * ((World.Rand.NextFloat() - World.Rand.NextFloat()) * 0.7f + 1.8f));
        p.AddXp(Value);
        Kill();
    }
    public override int NetData => Value;
    public override void SetNetData(int d) => Value = d;
}

public sealed class FallingBlockEntity : Entity
{
    public byte BlockId; public int Meta;
    public FallingBlockEntity(World.World w, double x, double y, double z, byte id, int meta) : base(w)
    {
        Width = 0.98f; Height = 0.98f; StepHeight = 0; SetPositionAndPrev(x, y, z); BlockId = id; Meta = meta;
    }
    public override string TypeKey => "falling_block";
    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled) return;
        MotionY -= 0.04;
        MoveWithCollision(MotionX, MotionY, MotionZ);
        MotionX *= 0.98; MotionY *= 0.98; MotionZ *= 0.98;
        if (World.IsRemote) return;
        if (OnGround)
        {
            int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(Y + 0.01), bz = MathUtil.FloorToInt(Z);
            var at = Block.Get(World.GetBlock(bx, by, bz));
            Kill();
            if (at.Replaceable || at.Id == 0) World.SetBlock(bx, by, bz, BlockId, Meta);
            else World.DropItem(X, Y + 0.5, Z, new ItemStack(BlockId));
        }
        else if (Age > 600) { Kill(); World.DropItem(X, Y, Z, new ItemStack(BlockId)); }
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(BlockId); bw.Write(Meta); }
    public override void Load(BinaryReader br) { base.Load(br); BlockId = br.ReadByte(); Meta = br.ReadInt32(); }
    public override int NetData => BlockId | (Meta << 8);
    public override void SetNetData(int d) { BlockId = (byte)(d & 0xFF); Meta = d >> 8; }
}

public sealed class TntEntity : Entity
{
    public int Fuse;
    public Entity Igniter;
    public TntEntity(World.World w, double x, double y, double z, int fuse) : base(w)
    {
        Width = 0.98f; Height = 0.98f; StepHeight = 0; SetPositionAndPrev(x, y, z); Fuse = fuse;
        float a = (float)(w.Rand.NextDouble() * Math.PI * 2);
        MotionX = -Math.Sin(a) * 0.02; MotionY = 0.2; MotionZ = -Math.Cos(a) * 0.02;
    }
    public override string TypeKey => "tnt";
    public override void Tick()
    {
        base.Tick();
        if (IsRemoteControlled) { Fuse--; return; }
        MotionY -= 0.04;
        MoveWithCollision(MotionX, MotionY, MotionZ);
        MotionX *= 0.98; MotionY *= 0.98; MotionZ *= 0.98;
        if (OnGround) { MotionX *= 0.7; MotionZ *= 0.7; MotionY *= -0.5; }
        if (Fuse-- <= 0)
        {
            Kill();
            if (!World.IsRemote) Explosion.Explode(World, this, X, Y + Height / 2, Z, 4f, true);
        }
        else World.SpawnParticles(ParticleKind.Smoke, X, Y + 0.5, Z, 1);
    }
    public override int NetData => Fuse;
    public override void SetNetData(int d) => Fuse = d;
}

public sealed class ArrowEntity : Entity
{
    public Entity Shooter;
    public bool InGround; public int InGroundTicks; public int Shake;
    public double Damage = 2.0;
    public bool Critical, PickupAllowed = true;
    private int _gx, _gy, _gz; private byte _gid;
    public ArrowEntity(World.World w) : base(w) { Width = 0.5f; Height = 0.5f; StepHeight = 0; }
    public ArrowEntity(World.World w, LivingEntity shooter, float velocity) : this(w)
    {
        Shooter = shooter;
        SetPositionAndPrev(shooter.X, shooter.Y + shooter.EyeHeight - 0.1, shooter.Z);
        Yaw = shooter.Yaw; Pitch = shooter.Pitch;
        float yr = Yaw * MathUtil.Deg2Rad, pr = Pitch * MathUtil.Deg2Rad;
        X -= MathF.Cos(yr) * 0.16f; Z -= MathF.Sin(yr) * 0.16f; Y -= 0.1;
        double mx = -MathF.Sin(yr) * MathF.Cos(pr), my = -MathF.Sin(pr), mz = MathF.Cos(yr) * MathF.Cos(pr);
        Shoot(mx, my, mz, velocity, 1f);
        PrevX = X; PrevY = Y; PrevZ = Z;
    }
    public void Shoot(double x, double y, double z, float velocity, float inaccuracy)
    {
        double len = Math.Sqrt(x * x + y * y + z * z);
        x /= len; y /= len; z /= len;
        x += World.Rand.NextGaussian() * 0.0075 * inaccuracy; y += World.Rand.NextGaussian() * 0.0075 * inaccuracy; z += World.Rand.NextGaussian() * 0.0075 * inaccuracy;
        MotionX = x * velocity; MotionY = y * velocity; MotionZ = z * velocity;
        double h = Math.Sqrt(x * x + z * z);
        Yaw = PrevYaw = (float)(Math.Atan2(x, z) * MathUtil.Rad2Deg);
        Pitch = PrevPitch = (float)(Math.Atan2(y, h) * MathUtil.Rad2Deg);
    }
    public override string TypeKey => "arrow";
    public override bool Persistent => InGround;

    public override void Tick()
    {
        Age++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        if (Shake > 0) Shake--;
        if (InGround)
        {
            if (World.GetBlock(_gx, _gy, _gz) != _gid) { InGround = false; MotionX *= World.Rand.NextFloat() * 0.2f; MotionY *= World.Rand.NextFloat() * 0.2f; MotionZ *= World.Rand.NextFloat() * 0.2f; InGroundTicks = 0; }
            else { if (++InGroundTicks >= 1200) Kill(); return; }
        }
        // promień
        double len = Math.Sqrt(MotionX * MotionX + MotionY * MotionY + MotionZ * MotionZ);
        var hit = len > 0 ? World.RayCast(X, Y, Z, MotionX / len, MotionY / len, MotionZ / len, len) : default;
        double ex = X + MotionX, ey = Y + MotionY, ez = Z + MotionZ;
        double maxT = hit.Hit ? hit.Distance : len;
        // encje
        Entity target = null; double bestT = maxT;
        if (len > 0)
            foreach (var e in World.GetEntitiesInBox(BoundingBox.Expand(MotionX, MotionY, MotionZ).Grow(1), this))
            {
                if (!e.CanBeAttacked || (e == Shooter && Age < 5)) continue;
                var bb = e.BoundingBox.Grow(0.3);
                if (bb.RayIntersect(X, Y, Z, MotionX / len, MotionY / len, MotionZ / len, out double t, out _) && t <= bestT) { bestT = t; target = e; }
            }
        if (target != null && !World.IsRemote)
        {
            float speed = (float)len;
            int dmg = (int)Math.Ceiling(speed * Damage);
            if (Critical) dmg += World.Rand.Next(dmg / 2 + 2);
            if (target is LivingEntity le && le.Attack(DamageSource.Arrow(this, Shooter ?? this), dmg))
            {
                double h = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
                if (h > 0) { le.MotionX += MotionX * 0.6 / h * 0.6; le.MotionY += 0.1; le.MotionZ += MotionZ * 0.6 / h * 0.6; }
                World.PlaySound("arrow_hit", X, Y, Z, 1f, 1.2f / (World.Rand.NextFloat() * 0.2f + 0.9f));
                Kill();
            }
            else
            {
                MotionX *= -0.1; MotionY *= -0.1; MotionZ *= -0.1; Yaw += 180; PrevYaw += 180;
            }
            return;
        }
        if (hit.Hit)
        {
            _gx = hit.X; _gy = hit.Y; _gz = hit.Z; _gid = World.GetBlock(hit.X, hit.Y, hit.Z);
            X = hit.HitX - MotionX / len * 0.05; Y = hit.HitY - MotionY / len * 0.05; Z = hit.HitZ - MotionZ / len * 0.05;
            InGround = true; Shake = 7; Critical = false;
            World.PlaySound("arrow_hit", X, Y, Z, 1f, 1.2f / (World.Rand.NextFloat() * 0.2f + 0.9f));
            if (_gid == B.Tnt && FireTicks > 0) TntBlock.Ignite(World, _gx, _gy, _gz, 80);
            return;
        }
        X = ex; Y = ey; Z = ez;
        double hz = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
        Yaw = (float)(Math.Atan2(MotionX, MotionZ) * MathUtil.Rad2Deg);
        Pitch = (float)(Math.Atan2(MotionY, hz) * MathUtil.Rad2Deg);
        float drag = 0.99f;
        UpdateFluidState();
        if (InWater) { drag = 0.6f; World.SpawnParticles(ParticleKind.Bubble, X, Y, Z, 1); }
        MotionX *= drag; MotionY *= drag; MotionZ *= drag;
        MotionY -= 0.05;
        if (Critical) World.SpawnParticles(ParticleKind.Crit, X, Y, Z, 1);
        if (Age > 1200) Kill();
    }

    public override void OnCollideWithPlayer(Player p)
    {
        if (!InGround || Shake > 0 || World.IsRemote) return;
        if (!PickupAllowed) { if (p.Creative) Kill(); return; }
        var s = new ItemStack(I.Arrow);
        if (p.Inventory.Add(s))
        {
            p.Handler?.OnPickup(this, p);
            World.PlaySound("pop", X, Y, Z, 0.2f, 2f);
            Kill();
        }
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(InGround); bw.Write(_gx); bw.Write(_gy); bw.Write(_gz); bw.Write(_gid); bw.Write(PickupAllowed); }
    public override void Load(BinaryReader br) { base.Load(br); InGround = br.ReadBoolean(); _gx = br.ReadInt32(); _gy = br.ReadInt32(); _gz = br.ReadInt32(); _gid = br.ReadByte(); PickupAllowed = br.ReadBoolean(); }
}

/// <summary>Rzucane: perła kresu (typ 0), jajko (typ 1).</summary>
public sealed class ThrownEntity : Entity
{
    public int Kind;
    public LivingEntity Thrower;
    public ThrownEntity(World.World w, int kind, LivingEntity thrower) : base(w)
    {
        Kind = kind; Thrower = thrower; Width = 0.25f; Height = 0.25f; StepHeight = 0;
        if (thrower != null)
        {
            SetPositionAndPrev(thrower.X, thrower.Y + thrower.EyeHeight - 0.1, thrower.Z);
            float yr = thrower.Yaw * MathUtil.Deg2Rad, pr = thrower.Pitch * MathUtil.Deg2Rad;
            MotionX = -MathF.Sin(yr) * MathF.Cos(pr) * 1.5; MotionZ = MathF.Cos(yr) * MathF.Cos(pr) * 1.5; MotionY = -MathF.Sin(pr) * 1.5;
        }
    }
    public override string TypeKey => "thrown";
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
            OnImpact(target);
            return;
        }
        X += MotionX; Y += MotionY; Z += MotionZ;
        MotionX *= 0.99; MotionY *= 0.99; MotionZ *= 0.99; MotionY -= 0.03;
        if (Y < -64) Kill();
    }
    private void OnImpact(Entity target)
    {
        if (World.IsRemote) return;
        if (target is LivingEntity le) le.Attack(DamageSource.Mob(Thrower ?? (Entity)this), 0f);
        if (Kind == 0)
        {
            World.SpawnParticles(ParticleKind.Portal, X, Y, Z, 32);
            if (Thrower is Player p && !p.Dead)
            {
                // MC 1.8: 5% szans na endermita w miejscu gracza (moduł NE rejestruje "endermite")
                if (World.Rand.NextFloat() < 0.05f && World.Rules.DoMobSpawning && EntitySerializer.Create("endermite", World) is { } mite) { mite.SetPositionAndPrev(p.X, p.Y, p.Z); mite.Yaw = p.Yaw; World.SpawnEntity(mite); }
                p.SetPositionAndPrev(X, Y, Z); p.FallDistance = 0;
                p.NetTeleportHook?.Invoke();
                if (!p.Creative) p.Attack(DamageSource.Fall, 5f);
            }
        }
        else
        {
            if (World.Rand.Next(8) == 0)
            {
                int n = World.Rand.Next(32) == 0 ? 4 : 1;
                for (int i = 0; i < n; i++) { var c = new Chicken(World) { Age = -24000 }; c.SetPositionAndPrev(X, Y, Z); c.GrowingAge = -24000; World.SpawnEntity(c); }
            }
            World.SpawnParticles(ParticleKind.BlockBreak, X, Y, Z, 8, -I.Egg);
        }
        Kill();
    }
    public override int NetData => Kind;
    public override void SetNetData(int d) => Kind = d;
}

public static class Explosion
{
    public static void Explode(World.World w, Entity source, double x, double y, double z, float power, bool breakBlocks)
    {
        w.PlaySound("explode", x, y, z, 4f, (1f + (w.Rand.NextFloat() - w.Rand.NextFloat()) * 0.2f) * 0.7f);
        w.SpawnParticles(power >= 2 ? ParticleKind.Explosion : ParticleKind.LargeSmoke, x, y, z, 1, (int)(power * 10));
        var affected = new HashSet<(int, int, int)>();
        if (breakBlocks && w.Rules.MobGriefing || breakBlocks && source is TntEntity)
        {
            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 16; j++)
                    for (int k = 0; k < 16; k++)
                    {
                        if (i != 0 && i != 15 && j != 0 && j != 15 && k != 0 && k != 15) continue;
                        double dx = i / 15.0 * 2 - 1, dy = j / 15.0 * 2 - 1, dz = k / 15.0 * 2 - 1;
                        double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        dx /= len; dy /= len; dz /= len;
                        float f = power * (0.7f + w.Rand.NextFloat() * 0.6f);
                        double px = x, py = y, pz = z;
                        for (; f > 0; f -= 0.225f)
                        {
                            int bx = MathUtil.FloorToInt(px), by = MathUtil.FloorToInt(py), bz = MathUtil.FloorToInt(pz);
                            int id = w.GetBlock(bx, by, bz);
                            if (id != 0)
                            {
                                var b = Block.ById[id];
                                float res = b.IsLiquid ? 100 : b.BlastResistance;
                                f -= (res + 0.3f) * 0.3f;
                            }
                            if (f > 0 && by >= 0 && by < Chunk.Height) affected.Add((bx, by, bz));
                            px += dx * 0.3; py += dy * 0.3; pz += dz * 0.3;
                        }
                    }
        }
        // encje
        float r2 = power * 2f;
        var box = new AABB(x - r2 - 1, y - r2 - 1, z - r2 - 1, x + r2 + 1, y + r2 + 1, z + r2 + 1);
        foreach (var e in w.GetEntitiesInBox(box))
        {
            double dist = Math.Sqrt(e.DistanceSq(x, y, z)) / r2;
            if (dist > 1) continue;
            double ex = e.X - x, ey = e.Y + e.EyeHeight - y, ez = e.Z - z;
            double d = Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (d == 0) continue;
            ex /= d; ey /= d; ez /= d;
            double exposure = Exposure(w, x, y, z, e.BoundingBox);
            double impact = (1 - dist) * exposure;
            float dmg = (float)((impact * impact + impact) / 2 * 8 * r2 + 1);
            if (e is LivingEntity le) le.Attack(DamageSource.Explosion(source is TntEntity t ? (t.Igniter ?? source) : source), dmg);
            else if (e is ItemEntity || e is XpOrbEntity) { e.Kill(); continue; }
            e.MotionX += ex * impact; e.MotionY += ey * impact; e.MotionZ += ez * impact;
        }
        // niszczenie bloków
        var drops = new List<ItemStack>();
        foreach (var (bx, by, bz) in affected)
        {
            int id = w.GetBlock(bx, by, bz);
            if (id == 0) continue;
            var b = Block.ById[id];
            if (id == B.Tnt) { w.SetBlock(bx, by, bz, B.Air); var t = new TntEntity(w, bx + 0.5, by, bz + 0.5, w.Rand.Next(20) + 10); w.SpawnEntity(t); continue; }
            if (b.IsLiquid) continue;
            int meta = w.GetMeta(bx, by, bz);
            if (w.Rand.NextFloat() < 1f / power)
            {
                drops.Clear();
                b.GetDrops(w, bx, by, bz, meta, w.Rand, null, drops);
                foreach (var s in drops) w.DropItem(bx + 0.5, by + 0.5, bz + 0.5, s);
            }
            w.SetBlock(bx, by, bz, B.Air);
        }
    }

    private static double Exposure(World.World w, double x, double y, double z, AABB bb)
    {
        int hit = 0, total = 0;
        for (double fx = 0; fx <= 1; fx += 0.5)
            for (double fy = 0; fy <= 1; fy += 0.5)
                for (double fz = 0; fz <= 1; fz += 0.5)
                {
                    double tx = bb.MinX + (bb.MaxX - bb.MinX) * fx, ty = bb.MinY + (bb.MaxY - bb.MinY) * fy, tz = bb.MinZ + (bb.MaxZ - bb.MinZ) * fz;
                    double dx = tx - x, dy = ty - y, dz = tz - z;
                    double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    total++;
                    if (len < 0.01) { hit++; continue; }
                    var r = w.RayCast(x, y, z, dx / len, dy / len, dz / len, len);
                    if (!r.Hit || !Block.Get(w.GetBlock(r.X, r.Y, r.Z)).Opaque) hit++;
                }
        return (double)hit / total;
    }
}
