using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public sealed class DamageSource
{
    public readonly string Type;
    public readonly Entity Source;     // bezpośrednie źródło (np. strzała)
    public readonly Entity Attacker;   // kto zaatakował
    public bool BypassArmor;
    public bool IsExplosion;
    public bool IsFire;
    public DamageSource(string type, Entity src = null, Entity attacker = null) { Type = type; Source = src; Attacker = attacker ?? src; }

    public static readonly DamageSource Fall = new("fall") { BypassArmor = true };
    public static readonly DamageSource Drown = new("drown") { BypassArmor = true };
    public static readonly DamageSource Lava = new("lava") { IsFire = true };
    public static readonly DamageSource Fire = new("fire") { IsFire = true, BypassArmor = true };
    public static readonly DamageSource OnFire = new("onFire") { IsFire = true, BypassArmor = true };
    public static readonly DamageSource Cactus = new("cactus");
    public static readonly DamageSource Starve = new("starve") { BypassArmor = true };
    public static readonly DamageSource InWall = new("inWall") { BypassArmor = true };
    public static readonly DamageSource Void = new("outOfWorld") { BypassArmor = true };
    public static readonly DamageSource Generic = new("generic") { BypassArmor = true };
    public static DamageSource Mob(Entity e) => new("mob", e);
    public static DamageSource PlayerAttack(Entity p) => new("player", p);
    public static DamageSource Arrow(Entity arrow, Entity shooter) => new("arrow", arrow, shooter);
    public static DamageSource Explosion(Entity e) => new("explosion", e) { IsExplosion = true };

    public string DeathMessage(string victim)
    {
        string a = Attacker is Player p ? p.Name : Attacker is LivingEntity le ? le.DisplayName : null;
        return Type switch
        {
            "fall" => victim + " spadł z wysoka",
            "drown" => victim + " utonął",
            "lava" => victim + " próbował pływać w lawie",
            "fire" or "onFire" => victim + " spłonął",
            "cactus" => victim + " został zakłuty na śmierć",
            "starve" => victim + " umarł z głodu",
            "inWall" => victim + " udusił się w ścianie",
            "outOfWorld" => victim + " wypadł poza świat",
            "explosion" => a != null ? victim + " został wysadzony przez " + a : victim + " wyleciał w powietrze",
            "arrow" => victim + " został zastrzelony przez " + (a ?? "?"),
            "mob" or "player" => victim + " został zabity przez " + (a ?? "?"),
            _ => victim + " zginął"
        };
    }
}

public abstract class Entity
{
    public int Id;
    public World.World World;
    public double X, Y, Z, PrevX, PrevY, PrevZ, MotionX, MotionY, MotionZ;
    public float Yaw, Pitch, PrevYaw, PrevPitch;
    public float Width = 0.6f, Height = 1.8f;
    public bool OnGround, CollidedH, CollidedV, InWater, InLava, Dead, Removed, NoClip, NoGravity;
    public float FallDistance;
    public int Age;
    public int FireTicks;
    public float StepHeight = 0.5f;
    public bool IsRemoteControlled; // pozycja z sieci
    // interpolacja sieciowa
    public double NetX, NetY, NetZ; public float NetYaw, NetPitch; public int NetSteps;
    public int RiddenTicks;

    protected Entity(World.World w) { World = w; }

    public abstract string TypeKey { get; }
    public virtual bool Persistent => false;
    public virtual float EyeHeight => Height * 0.85f;
    public virtual bool CanBeCollidedWith => false;
    public virtual bool CanBeAttacked => false;

    public AABB BoundingBox => new(X - Width / 2, Y, Z - Width / 2, X + Width / 2, Y + Height, Z + Width / 2);

    public void SetPosition(double x, double y, double z) { X = x; Y = y; Z = z; }
    public void SetPositionAndPrev(double x, double y, double z) { X = PrevX = x; Y = PrevY = y; Z = PrevZ = z; }

    public virtual void PrevTick()
    {
        PrevX = X; PrevY = Y; PrevZ = Z; PrevYaw = Yaw; PrevPitch = Pitch;
    }

    public double IX(float pt) => PrevX + (X - PrevX) * pt;
    public double IY(float pt) => PrevY + (Y - PrevY) * pt;
    public double IZ(float pt) => PrevZ + (Z - PrevZ) * pt;

    public virtual void Tick()
    {
        Age++;
        if (IsRemoteControlled) { NetInterpolate(); return; }
        UpdateFluidState();
        if (FireTicks > 0)
        {
            if (InWater) FireTicks = 0;
            else
            {
                if (FireTicks % 20 == 0 && this is LivingEntity le) le.Attack(DamageSource.OnFire, 1f);
                FireTicks--;
            }
        }
        if (InLava) { if (this is LivingEntity le2) { le2.Attack(DamageSource.Lava, 4f); } FireTicks = Math.Max(FireTicks, 300); FallDistance *= 0.5f; }
        if (Y < -64) OnVoid();
    }

    protected virtual void OnVoid() { if (this is LivingEntity le) le.Attack(DamageSource.Void, 4f); else Kill(); }

    public void NetInterpolate()
    {
        if (NetSteps > 0)
        {
            X += (NetX - X) / NetSteps; Y += (NetY - Y) / NetSteps; Z += (NetZ - Z) / NetSteps;
            Yaw += MathUtil.WrapDegrees(NetYaw - Yaw) / NetSteps; Pitch += (NetPitch - Pitch) / NetSteps;
            NetSteps--;
        }
    }

    public void UpdateFluidState()
    {
        var bb = BoundingBox.Grow(-0.001, -0.401, -0.001);
        bool wasIn = InWater;
        InWater = World.IsAnyLiquid(bb, false);
        InLava = World.IsAnyLiquid(BoundingBox.Grow(-0.1, -0.4, -0.1), true);
        if (InWater)
        {
            if (!wasIn && FallDistance > 1 && this is LivingEntity)
            {
                World.PlaySound("splash", X, Y, Z, Math.Min(1f, (float)Math.Abs(MotionY) * 2f), 1f);
                World.SpawnParticles(ParticleKind.Splash, X, Y + 0.2, Z, 20);
            }
            FallDistance = 0; FireTicks = 0;
            // pchanie przez prąd
            ApplyFlow(bb);
        }
    }

    private void ApplyFlow(AABB bb)
    {
        if (this is Player p && p.Flying) return;
        double fx = 0, fz = 0; int n = 0;
        for (int x = MathUtil.FloorToInt(bb.MinX); x <= MathUtil.FloorToInt(bb.MaxX); x++)
            for (int y = MathUtil.FloorToInt(bb.MinY); y <= MathUtil.FloorToInt(bb.MaxY); y++)
                for (int z = MathUtil.FloorToInt(bb.MinZ); z <= MathUtil.FloorToInt(bb.MaxZ); z++)
                    if (World.GetBlock(x, y, z) == B.Water && World.GetMeta(x, y, z) != 0)
                    {
                        var (vx, vz) = ((FluidBlock)Block.Get(B.Water)).FlowVector(World, x, y, z);
                        fx += vx; fz += vz; n++;
                    }
        if (n > 0)
        {
            double len = Math.Sqrt(fx * fx + fz * fz);
            if (len > 0) { MotionX += fx / len * 0.014; MotionZ += fz / len * 0.014; }
        }
    }

    public bool IsInsideOpaqueBlock()
    {
        for (int i = 0; i < 8; i++)
        {
            double x = X + ((i & 1) - 0.5) * Width * 0.8, y = Y + (((i >> 1) & 1) - 0.5) * 0.1 + EyeHeight, z = Z + (((i >> 2) & 1) - 0.5) * Width * 0.8;
            var b = Block.Get(World.GetBlock(MathUtil.FloorToInt(x), MathUtil.FloorToInt(y), MathUtil.FloorToInt(z)));
            if (b.Opaque && b.Solid) return true;
        }
        return false;
    }

    public bool IsHeadInWater()
    {
        double ey = Y + EyeHeight;
        int bx = MathUtil.FloorToInt(X), by = MathUtil.FloorToInt(ey), bz = MathUtil.FloorToInt(Z);
        if (World.GetBlock(bx, by, bz) != B.Water) return false;
        double top = by + FluidBlock.HeightOf(World.GetMeta(bx, by, bz)) - 0.11;
        return ey < top;
    }
    public bool IsHeadInLava()
    {
        double ey = Y + EyeHeight;
        return World.GetBlock(MathUtil.FloorToInt(X), MathUtil.FloorToInt(ey), MathUtil.FloorToInt(Z)) == B.Lava;
    }

    private readonly List<AABB> _boxes = new();
    protected virtual bool IsSneakingForEdge => false;

    public void MoveWithCollision(double dx, double dy, double dz)
    {
        if (NoClip) { X += dx; Y += dy; Z += dz; return; }
        double odx = dx, ody = dy, odz = dz;
        var box = BoundingBox;

        if (OnGround && IsSneakingForEdge)
        {
            const double step = 0.05;
            while (dx != 0 && World.GetCollisionBoxes(box.Offset(dx, -1, 0), _boxes).Count == 0)
            { if (dx < step && dx >= -step) dx = 0; else if (dx > 0) dx -= step; else dx += step; odx = dx; }
            while (dz != 0 && World.GetCollisionBoxes(box.Offset(0, -1, dz), _boxes).Count == 0)
            { if (dz < step && dz >= -step) dz = 0; else if (dz > 0) dz -= step; else dz += step; odz = dz; }
            while (dx != 0 && dz != 0 && World.GetCollisionBoxes(box.Offset(dx, -1, dz), _boxes).Count == 0)
            {
                if (dx < step && dx >= -step) dx = 0; else if (dx > 0) dx -= step; else dx += step;
                if (dz < step && dz >= -step) dz = 0; else if (dz > 0) dz -= step; else dz += step;
                odx = dx; odz = dz;
            }
        }

        World.GetCollisionBoxes(box.Expand(dx, dy, dz), _boxes);
        var b = box;
        foreach (var c in _boxes) dy = c.ClipY(b, dy);
        b = b.Offset(0, dy, 0);
        bool groundAfter = OnGround || (ody != dy && ody < 0);
        foreach (var c in _boxes) dx = c.ClipX(b, dx);
        b = b.Offset(dx, 0, 0);
        foreach (var c in _boxes) dz = c.ClipZ(b, dz);
        b = b.Offset(0, 0, dz);

        if (StepHeight > 0 && groundAfter && (odx != dx || odz != dz))
        {
            double sdx = odx, sdy = StepHeight, sdz = odz;
            World.GetCollisionBoxes(box.Expand(odx, StepHeight, odz), _boxes);
            var sb = box;
            foreach (var c in _boxes) sdy = c.ClipY(sb, sdy);
            sb = sb.Offset(0, sdy, 0);
            foreach (var c in _boxes) sdx = c.ClipX(sb, sdx);
            sb = sb.Offset(sdx, 0, 0);
            foreach (var c in _boxes) sdz = c.ClipZ(sb, sdz);
            sb = sb.Offset(0, 0, sdz);
            double down = -sdy;
            foreach (var c in _boxes) down = c.ClipY(sb, down);
            sb = sb.Offset(0, down, 0);
            if (sdx * sdx + sdz * sdz > dx * dx + dz * dz)
            {
                dx = sdx; dz = sdz; dy = sdy + down; b = sb;
            }
        }

        X = (b.MinX + b.MaxX) / 2; Y = b.MinY; Z = (b.MinZ + b.MaxZ) / 2;
        CollidedH = odx != dx || odz != dz;
        CollidedV = ody != dy;
        OnGround = CollidedV && ody < 0;
        if (odx != dx) MotionX = 0;
        if (odz != dz) MotionZ = 0;
        if (ody != dy) MotionY = 0;

        if (OnGround)
        {
            if (FallDistance > 0) { OnFall(FallDistance); FallDistance = 0; }
        }
        else if (dy < 0) FallDistance -= (float)dy;

        // kolizje z blokami (kaktus itp.)
        var bb = BoundingBox.Grow(0.001);
        int x0 = MathUtil.FloorToInt(bb.MinX), x1 = MathUtil.FloorToInt(bb.MaxX);
        int y0 = MathUtil.FloorToInt(bb.MinY), y1 = MathUtil.FloorToInt(bb.MaxY);
        int z0 = MathUtil.FloorToInt(bb.MinZ), z1 = MathUtil.FloorToInt(bb.MaxZ);
        if (!World.IsRemote)
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                    {
                        int id = World.GetBlock(x, y, z);
                        if (id == B.Cactus)
                        {
                            var cb = new AABB(x + 1 / 16.0, y, z + 1 / 16.0, x + 15 / 16.0, y + 1, z + 15 / 16.0);
                            if (cb.Intersects(bb)) Block.ById[id].OnEntityCollide(World, x, y, z, this);
                        }
                    }
    }

    protected virtual void OnFall(float distance) { }

    public void Kill() { Dead = true; Removed = true; }

    public double DistanceSq(Entity o) { double dx = X - o.X, dy = Y - o.Y, dz = Z - o.Z; return dx * dx + dy * dy + dz * dz; }
    public double DistanceSq(double x, double y, double z) { double dx = X - x, dy = Y - y, dz = Z - z; return dx * dx + dy * dy + dz * dz; }
    public double Distance(Entity o) => Math.Sqrt(DistanceSq(o));

    public (double x, double y, double z) Look(float pt = 1f)
    {
        float yaw = PrevYaw + (Yaw - PrevYaw) * pt, pitch = PrevPitch + (Pitch - PrevPitch) * pt;
        float yr = yaw * MathUtil.Deg2Rad, pr = pitch * MathUtil.Deg2Rad;
        return (-MathF.Sin(yr) * MathF.Cos(pr), -MathF.Sin(pr), MathF.Cos(yr) * MathF.Cos(pr));
    }

    public virtual void OnCollideWithPlayer(Player p) { }
    public virtual bool Interact(Player p) => false;

    public virtual void Save(BinaryWriter bw)
    {
        bw.Write(X); bw.Write(Y); bw.Write(Z); bw.Write(MotionX); bw.Write(MotionY); bw.Write(MotionZ);
        bw.Write(Yaw); bw.Write(Pitch); bw.Write(FallDistance); bw.Write(FireTicks); bw.Write(Age);
    }
    public virtual void Load(BinaryReader br)
    {
        X = br.ReadDouble(); Y = br.ReadDouble(); Z = br.ReadDouble();
        MotionX = br.ReadDouble(); MotionY = br.ReadDouble(); MotionZ = br.ReadDouble();
        Yaw = br.ReadSingle(); Pitch = br.ReadSingle(); FallDistance = br.ReadSingle(); FireTicks = br.ReadInt32(); Age = br.ReadInt32();
        PrevX = X; PrevY = Y; PrevZ = Z; PrevYaw = Yaw; PrevPitch = Pitch;
    }

    /// <summary>Dane do synchronizacji sieciowej (dodatkowe).</summary>
    public virtual int NetData => 0;
    public virtual void SetNetData(int d) { }
}

public static class EntitySerializer
{
    public static Entity Create(string key, World.World w) => key switch
    {
        "item" => new ItemEntity(w, 0, 0, 0, new ItemStack(1)),
        "cow" => new Cow(w),
        "pig" => new Pig(w),
        "sheep" => new Sheep(w),
        "chicken" => new Chicken(w),
        "zombie" => new Zombie(w),
        "skeleton" => new Skeleton(w),
        "creeper" => new Creeper(w),
        "spider" => new Spider(w),
        "enderman" => new Enderman(w),
        "arrow" => new ArrowEntity(w),
        "tnt" => new TntEntity(w, 0, 0, 0, 80),
        "falling_block" => new FallingBlockEntity(w, 0, 0, 0, B.Sand, 0),
        "xp_orb" => new XpOrbEntity(w, 0, 0, 0, 1),
        "thrown" => new ThrownEntity(w, 0, null),
        _ => null
    };

    public static byte[] SaveEntities(List<Entity> ents)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(ents.Count);
        foreach (var e in ents)
        {
            using var ems = new MemoryStream();
            using var ebw = new BinaryWriter(ems);
            e.Save(ebw);
            bw.Write(e.TypeKey);
            bw.Write((int)ems.Length);
            bw.Write(ems.ToArray());
        }
        return ms.ToArray();
    }

    public static void LoadEntities(World.World w, byte[] data)
    {
        try
        {
            using var br = new BinaryReader(new MemoryStream(data));
            int n = br.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                string key = br.ReadString();
                int len = br.ReadInt32();
                var bytes = br.ReadBytes(len);
                var e = Create(key, w);
                if (e == null) continue;
                try { e.Load(new BinaryReader(new MemoryStream(bytes))); w.SpawnEntity(e); }
                catch (Exception ex) { Log.Error("Entity load " + key + ": " + ex.Message); }
            }
        }
        catch (Exception ex) { Log.Error("Entities load: " + ex.Message); }
    }
}
