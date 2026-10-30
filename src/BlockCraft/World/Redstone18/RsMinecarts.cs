using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Reguły bloków eksplozji pojazdu (zapalone TNT chroni tory i ich podłoże).</summary>
public interface IExplosionBlockRules
{
    float Resistance(World.World w, int x, int y, int z, Block block);
    bool CanDestroy(World.World w, int x, int y, int z);
}

public enum MinecartKind { Rideable, Chest, Furnace, Tnt, Hopper }

/// <summary>Fizyka wagonika z MC 1.8: macierz torów, prędkości w blokach/tick, opory, zbocza i zderzenia.</summary>
public class MinecartEntity : Entity, IRailVehicle
{
    public virtual MinecartKind Kind => MinecartKind.Rideable;
    public override string TypeKey => "minecart";
    public override bool Persistent => true;
    public override bool CanBeCollidedWith => !Dead;
    public override bool CanBeAttacked => !Dead;
    public string CustomName;
    public float DamageTaken;
    public int RollingAmplitude, RollingDirection = 1;
    private bool _reverse;
    public virtual double MaximumSpeed => 0.4;
    public virtual int ItemId => I.Minecart;
    public virtual int DisplayBlock => 0;
    // Końce szyny i ich wysokości: podjazd ma niski koniec o y=-1 (jak macierz EntityMinecart).
    public static readonly (int x, int y, int z)[,] RailEnds =
    {
        { (0, 0, -1), (0, 0, 1) }, { (-1, 0, 0), (1, 0, 0) },
        { (-1, -1, 0), (1, 0, 0) }, { (-1, 0, 0), (1, -1, 0) },
        { (0, 0, -1), (0, -1, 1) }, { (0, -1, -1), (0, 0, 1) },
        { (0, 0, 1), (1, 0, 0) }, { (0, 0, 1), (-1, 0, 0) },
        { (0, 0, -1), (-1, 0, 0) }, { (0, 0, -1), (1, 0, 0) }
    };
    public MinecartEntity(World.World w) : base(w) { Width = 0.98f; Height = 0.7f; StepHeight = 0; }
    public static MinecartEntity Create(World.World w, MinecartKind kind) => kind switch
    {
        MinecartKind.Chest => new ChestMinecartEntity(w), MinecartKind.Furnace => new FurnaceMinecartEntity(w),
        MinecartKind.Tnt => new TntMinecartEntity(w), MinecartKind.Hopper => new HopperMinecartEntity(w), _ => new MinecartEntity(w)
    };
    public static (int x, int y, int z)? RailAt(World.World w, double x, double y, double z)
    {
        int bx = MathUtil.FloorToInt(x), by = MathUtil.FloorToInt(y), bz = MathUtil.FloorToInt(z);
        if (RailBlock.IsRail(w.GetBlock(bx, by - 1, bz))) by--;
        return RailBlock.IsRail(w.GetBlock(bx, by, bz)) ? (bx, by, bz) : null;
    }
    public static (double x, double y, double z)? RailPoint(World.World w, double x, double y, double z)
    {
        var rail = RailAt(w, x, y, z); if (rail == null) return null;
        var (bx, by, bz) = rail.Value;
        int s = RailBlock.Shape(w.GetBlock(bx, by, bz), w.GetMeta(bx, by, bz));
        var a = RailEnds[s, 0]; var b = RailEnds[s, 1];
        double ax = bx + 0.5 + a.x * 0.5, ay = by + 0.0625 + a.y * 0.5, az = bz + 0.5 + a.z * 0.5;
        double dx = (b.x - a.x) * 0.5, dy = b.y - a.y, dz = (b.z - a.z) * 0.5;
        double t = dx == 0 ? z - bz : dz == 0 ? x - bx : ((x - ax) * dx + (z - az) * dz) * 2;
        return (ax + dx * t, ay + dy * t + (dy < 0 ? 1 : dy > 0 ? 0.5 : 0), az + dz * t);
    }
    public override void Tick()
    {
        base.Tick(); if (Removed || IsRemoteControlled) { UpdatePassenger(); return; }
        if (RollingAmplitude > 0) RollingAmplitude--; if (DamageTaken > 0) DamageTaken = Math.Max(0, DamageTaken - 1);
        MotionY -= 0.03999999910593033;
        var rail = RailAt(World, X, Y, Z);
        double speedBefore = MotionX * MotionX + MotionZ * MotionZ;
        if (rail is { } p)
        {
            int id = World.GetBlock(p.x, p.y, p.z), meta = World.GetMeta(p.x, p.y, p.z);
            MoveOnRail(p.x, p.y, p.z, id, meta);
            if (id == B.ActivatorRail) OnActivatorRail((meta & 8) != 0);
        }
        else
        {
            MotionX = Math.Clamp(MotionX, -MaximumSpeed, MaximumSpeed); MotionZ = Math.Clamp(MotionZ, -MaximumSpeed, MaximumSpeed);
            if (OnGround) { MotionX *= 0.5; MotionY *= 0.5; MotionZ *= 0.5; }
            MoveWithCollision(MotionX, MotionY, MotionZ);
            if (!OnGround) { MotionX *= 0.95; MotionY *= 0.95; MotionZ *= 0.95; }
        }
        double dx = PrevX - X, dz = PrevZ - Z;
        Pitch = 0;
        if (dx * dx + dz * dz > 0.001) Yaw = (float)(Math.Atan2(dz, dx) * MathUtil.Rad2Deg) + (_reverse ? 180 : 0);
        float turn = MathUtil.WrapDegrees(Yaw - PrevYaw);
        if (turn < -170 || turn >= 170) { Yaw += 180; _reverse = !_reverse; }
        foreach (var e in World.GetEntitiesInBox(BoundingBox.Grow(0.2, 0, 0.2), this).ToArray())
            if (e != Passenger && !e.NoClip && (e is MinecartEntity || e is LivingEntity)) Collide(e);
        UpdatePassenger();
        if (this is TntMinecartEntity tnt && CollidedH && speedBefore >= 0.01) tnt.Explode(speedBefore);
        double speed = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
        if (rail != null && speed > 0.01 && Age % 5 == 0)
        {
            World.PlaySound("minecart.base", X, Y, Z, (float)Math.Min(0.7, speed * 0.7), 1f);
            if (Passenger is Player) World.PlaySound("minecart.inside", X, Y, Z, (float)Math.Min(0.75, speed * 0.75), 1f);
        }
    }
    protected virtual void MoveOnRail(int x, int y, int z, int id, int meta)
    {
        FallDistance = 0;
        var before = RailPoint(World, X, Y, Z);
        Y = y;
        bool powered = id == B.PoweredRail && (meta & 8) != 0, brake = id == B.PoweredRail && !powered;
        int s = RailBlock.Shape(id, meta);
        switch (s)
        {
            case 2: MotionX -= 0.0078125; Y++; break; case 3: MotionX += 0.0078125; Y++; break;
            case 4: MotionZ += 0.0078125; Y++; break; case 5: MotionZ -= 0.0078125; Y++; break;
        }
        var a = RailEnds[s, 0]; var b = RailEnds[s, 1];
        double dx = b.x - a.x, dz = b.z - a.z, len = Math.Sqrt(dx * dx + dz * dz);
        if (MotionX * dx + MotionZ * dz < 0) { dx = -dx; dz = -dz; }
        double speed = Math.Min(2, Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ));
        MotionX = speed * dx / len; MotionZ = speed * dz / len;
        if (Passenger is LivingEntity { MoveForward: > 0 } rider && speed * speed < 0.01)
        { MotionX -= Math.Sin(rider.Yaw * MathUtil.Deg2Rad) * 0.1; MotionZ += Math.Cos(rider.Yaw * MathUtil.Deg2Rad) * 0.1; brake = false; }
        if (brake)
        {
            if (Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ) < 0.03) MotionX = MotionY = MotionZ = 0;
            else { MotionX *= 0.5; MotionZ *= 0.5; MotionY = 0; }
        }
        double ax = x + 0.5 + a.x * 0.5, az = z + 0.5 + a.z * 0.5;
        dx = (b.x - a.x) * 0.5; dz = (b.z - a.z) * 0.5;
        double t;
        if (dx == 0) { X = x + 0.5; t = Z - z; }
        else if (dz == 0) { Z = z + 0.5; t = X - x; }
        else t = ((X - ax) * dx + (Z - az) * dz) * 2;
        X = ax + dx * t; Z = az + dz * t;
        double ridden = Passenger != null ? 0.75 : 1;
        MoveWithCollision(Math.Clamp(MotionX * ridden, -MaximumSpeed, MaximumSpeed), 0, Math.Clamp(MotionZ * ridden, -MaximumSpeed, MaximumSpeed));
        if (a.y != 0 && MathUtil.FloorToInt(X) - x == a.x && MathUtil.FloorToInt(Z) - z == a.z) Y += a.y;
        else if (b.y != 0 && MathUtil.FloorToInt(X) - x == b.x && MathUtil.FloorToInt(Z) - z == b.z) Y += b.y;
        ApplyDrag();
        var after = RailPoint(World, X, Y, Z);
        if (after is { } to && before is { } from)
        {
            double change = (from.y - to.y) * 0.05;
            speed = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
            if (speed > 0) { MotionX = MotionX / speed * (speed + change); MotionZ = MotionZ / speed * (speed + change); }
            Y = to.y;
        }
        int bx = MathUtil.FloorToInt(X), bz = MathUtil.FloorToInt(Z);
        if (bx != x || bz != z)
        { speed = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ); MotionX = speed * (bx - x); MotionZ = speed * (bz - z); }
        if (powered)
        {
            speed = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
            if (speed > 0.01) { MotionX += MotionX / speed * 0.06; MotionZ += MotionZ / speed * 0.06; }
            else if (s == 1)
            {
                if (Redstone.IsNormalCube(World.GetBlock(x - 1, y, z))) MotionX = 0.02;
                else if (Redstone.IsNormalCube(World.GetBlock(x + 1, y, z))) MotionX = -0.02;
            }
            else if (s == 0)
            {
                if (Redstone.IsNormalCube(World.GetBlock(x, y, z - 1))) MotionZ = 0.02;
                else if (Redstone.IsNormalCube(World.GetBlock(x, y, z + 1))) MotionZ = -0.02;
            }
        }
        // Po dopasowaniu do szyny wykonaj detekcję także przy zerowym ruchu.
        var current = RailAt(World, X, Y, Z);
        if (current is { } cp) Block.Get(World.GetBlock(cp.x, cp.y, cp.z)).OnEntityCollide(World, cp.x, cp.y, cp.z, this);
    }
    protected virtual void ApplyDrag() { double drag = Passenger != null ? 0.996999979019165 : 0.9599999785423279; MotionX *= drag; MotionY = 0; MotionZ *= drag; }
    protected virtual void OnActivatorRail(bool powered)
    {
        if (!powered) return;
        Passenger?.Mount(null);
        if (RollingAmplitude == 0) { RollingDirection = -RollingDirection; RollingAmplitude = 10; DamageTaken = 50; }
    }
    public override bool Interact(Player p)
    {
        if (Kind != MinecartKind.Rideable) return false;
        if (p.Sneaking || Passenger is Player && Passenger != p) return true;
        if (!World.IsRemote) p.Mount(this);
        return true;
    }
    public void Collide(Entity e)
    {
        if (World.IsRemote || e == Passenger || e.Removed || e is Player { Spectator: true }) return;
        if (Kind == MinecartKind.Rideable && Passenger == null && e.RidingEntity == null && e is LivingEntity && e is not Player && e.TypeKey != "villager_golem"
            && MotionX * MotionX + MotionZ * MotionZ > 0.01) e.Mount(this);
        double dx = e.X - X, dz = e.Z - Z, dist2 = dx * dx + dz * dz;
        if (dist2 < 0.0001) return;
        double dist = Math.Sqrt(dist2), scale = Math.Min(1, 1 / dist) * 0.05;
        dx = dx / dist * scale; dz = dz / dist * scale;
        if (e is MinecartEntity c)
        {
            double alignment = Math.Abs((e.X - X) / dist * Math.Cos(Yaw * MathUtil.Deg2Rad) + (e.Z - Z) / dist * Math.Sin(Yaw * MathUtil.Deg2Rad));
            if (alignment < 0.8) return;
            if (c.Kind == MinecartKind.Furnace && Kind != MinecartKind.Furnace)
            { MotionX = MotionX * 0.2 + c.MotionX - dx; MotionZ = MotionZ * 0.2 + c.MotionZ - dz; c.MotionX *= 0.95; c.MotionZ *= 0.95; }
            else if (c.Kind != MinecartKind.Furnace && Kind == MinecartKind.Furnace)
            { c.MotionX = c.MotionX * 0.2 + MotionX + dx; c.MotionZ = c.MotionZ * 0.2 + MotionZ + dz; MotionX *= 0.95; MotionZ *= 0.95; }
            else
            {
                double mx = (MotionX + c.MotionX) / 2, mz = (MotionZ + c.MotionZ) / 2;
                MotionX = MotionX * 0.2 + mx - dx; MotionZ = MotionZ * 0.2 + mz - dz;
                c.MotionX = c.MotionX * 0.2 + mx + dx; c.MotionZ = c.MotionZ * 0.2 + mz + dz;
            }
        }
        else { MotionX -= dx; MotionZ -= dz; e.MotionX += dx / 4; e.MotionZ += dz / 4; }
    }
    public override bool Attack(DamageSource source, float amount)
    {
        if (World.IsRemote || Removed) return true;
        RollingDirection = -RollingDirection; RollingAmplitude = 10; DamageTaken += amount * 10;
        bool creative = source.Attacker is Player { Creative: true };
        if (creative || DamageTaken > 40) Destroy(source, creative);
        return true;
    }
    protected virtual void Destroy(DamageSource source, bool creative)
    {
        Kill();
        if ((!creative || !string.IsNullOrEmpty(CustomName)) && World.Rules.DoEntityDrops)
        {
            var s = new ItemStack(I.Minecart);
            if (!string.IsNullOrEmpty(CustomName)) { s.Tag = new Nbt(); s.Tag.GetOrCreateCompound("display")["Name"] = CustomName; }
            World.DropItem(X, Y, Z, s);
            if (DisplayBlock != 0 && !(Kind == MinecartKind.Furnace && source.IsExplosion)) World.DropItem(X, Y, Z, new ItemStack(DisplayBlock == B.FurnaceLit ? B.Furnace : DisplayBlock));
        }
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(CustomName ?? ""); bw.Write(_reverse); }
    public override void Load(BinaryReader br) { base.Load(br); CustomName = br.ReadString(); _reverse = br.ReadBoolean(); }
    public override int NetData => ((int)Kind & 7) | (RollingAmplitude << 3);
    public override void SetNetData(int d) { RollingAmplitude = (d >> 3) & 31; }
}

public abstract class ContainerMinecartEntity : MinecartEntity, IInventory
{
    public readonly ItemStack[] Slots;
    protected ContainerMinecartEntity(World.World w, int size) : base(w) { Slots = new ItemStack[size]; }
    public int Size => Slots.Length;
    public ItemStack Get(int i) => Slots[i];
    public void Set(int i, ItemStack s) { Slots[i] = s != null && s.Count > 0 ? s : null; }
    public void Changed() { }
    public override bool Interact(Player p)
    {
        RsGui.Open(p, new RsContainer(p.Inventory, this, CustomName ?? (Kind == MinecartKind.Hopper ? "Wagonik z lejem" : "Wagonik ze skrzynią"),
            Kind == MinecartKind.Hopper ? RsContainer.Layout.Hopper : RsContainer.Layout.Chest27));
        return true;
    }
    protected override void ApplyDrag()
    {
        double drag = 0.98 + (15 - ComparatorRead.FromInventory(this)) * 0.001;
        if (InWater) drag *= 0.95;
        MotionX *= drag; MotionY = 0; MotionZ *= drag;
    }
    protected override void OnActivatorRail(bool powered) { }
    protected override void Destroy(DamageSource source, bool creative)
    {
        base.Destroy(source, creative);
        if (!World.Rules.DoEntityDrops) return;
        for (int i = 0; i < Size; i++)
        {
            var s = Slots[i]; if (s == null) continue;
            while (s.Count > 0)
            {
                int count = Math.Min(s.Count, World.Rand.Next(21) + 10); s.Count -= count;
                World.DropItem(X + World.Rand.NextDouble() * 0.8 - 0.4, Y + World.Rand.NextDouble() * 0.8, Z + World.Rand.NextDouble() * 0.8 - 0.4, s.CopyWith(count));
            }
            Slots[i] = null;
        }
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); foreach (var s in Slots) bw.WriteStack(s); }
    public override void Load(BinaryReader br) { base.Load(br); for (int i = 0; i < Size; i++) Slots[i] = br.ReadStack(); }
}

public sealed class ChestMinecartEntity : ContainerMinecartEntity
{
    public ChestMinecartEntity(World.World w) : base(w, 27) { }
    public override MinecartKind Kind => MinecartKind.Chest;
    public override string TypeKey => "chest_minecart";
    public override int ItemId => I.ChestMinecart;
    public override int DisplayBlock => B.Chest;
}

public sealed class FurnaceMinecartEntity : MinecartEntity
{
    public int Fuel;
    public double PushX, PushZ;
    public FurnaceMinecartEntity(World.World w) : base(w) { }
    public override MinecartKind Kind => MinecartKind.Furnace;
    public override string TypeKey => "furnace_minecart";
    public override double MaximumSpeed => 0.2;
    public override int ItemId => I.FurnaceMinecart;
    public override int DisplayBlock => Fuel > 0 ? B.FurnaceLit : B.Furnace;
    public override void Tick()
    {
        base.Tick(); if (IsRemoteControlled) return;
        if (Fuel > 0) Fuel--; if (Fuel <= 0) PushX = PushZ = 0;
        if (Fuel > 0 && World.Rand.Next(4) == 0) World.SpawnParticles(ParticleKind.LargeSmoke, X, Y + 0.8, Z, 1);
    }
    protected override void MoveOnRail(int x, int y, int z, int id, int meta)
    {
        base.MoveOnRail(x, y, z, id, meta);
        double d = PushX * PushX + PushZ * PushZ;
        if (d > 0.0001 && MotionX * MotionX + MotionZ * MotionZ > 0.001)
        {
            double len = Math.Sqrt(d); PushX /= len; PushZ /= len;
            if (PushX * MotionX + PushZ * MotionZ < 0) PushX = PushZ = 0;
            else { PushX *= len / MaximumSpeed; PushZ *= len / MaximumSpeed; }
        }
    }
    protected override void ApplyDrag()
    {
        double len = Math.Sqrt(PushX * PushX + PushZ * PushZ);
        if (len > 0.01) { PushX /= len; PushZ /= len; MotionX = MotionX * 0.8 + PushX; MotionZ = MotionZ * 0.8 + PushZ; }
        else { MotionX *= 0.98; MotionZ *= 0.98; }
        base.ApplyDrag();
    }
    public override bool Interact(Player p)
    {
        var s = p.Inventory.Selected;
        if (s != null && s.Id is I.Coal or I.Charcoal)
        { Fuel += 3600; if (!p.Creative && --s.Count <= 0) p.Inventory.Main[p.Inventory.SelectedSlot] = null; }
        PushX = X - p.X; PushZ = Z - p.Z; return true;
    }
    protected override void OnActivatorRail(bool powered) { }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Fuel); bw.Write(PushX); bw.Write(PushZ); }
    public override void Load(BinaryReader br) { base.Load(br); Fuel = br.ReadInt32(); PushX = br.ReadDouble(); PushZ = br.ReadDouble(); }
}

public sealed class HopperMinecartEntity : ContainerMinecartEntity
{
    public bool Enabled = true;
    public int TransferCooldown;
    private (int x, int y, int z) _last = (int.MinValue, 0, 0);
    public HopperMinecartEntity(World.World w) : base(w, 5) { }
    public override MinecartKind Kind => MinecartKind.Hopper;
    public override string TypeKey => "hopper_minecart";
    public override int ItemId => I.HopperMinecart;
    public override int DisplayBlock => B.Hopper;
    protected override void OnActivatorRail(bool powered) { Enabled = !powered; }
    public override void Tick()
    {
        base.Tick(); if (Removed || World.IsRemote || !Enabled) return;
        var pos = (MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y), MathUtil.FloorToInt(Z));
        if (pos == _last) TransferCooldown--; else TransferCooldown = 0;
        _last = pos;
        if (TransferCooldown > 0) return;
        TransferCooldown = 0;
        if (HopperLogic.Capture(World, this, X, Y + 0.5, Z)) TransferCooldown = 4;
        else
        {
            var item = World.GetEntitiesInBox(BoundingBox.Grow(0.25, 0, 0.25)).OfType<ItemEntity>().FirstOrDefault();
            if (item != null) HopperLogic.PutDrop(this, item);
        }
    }
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(TransferCooldown); bw.Write(Enabled); }
    public override void Load(BinaryReader br) { base.Load(br); TransferCooldown = br.ReadInt32(); Enabled = br.ReadBoolean(); }
}

public sealed class TntMinecartEntity : MinecartEntity, IExplosionBlockRules
{
    public int Fuse = -1;
    public TntMinecartEntity(World.World w) : base(w) { }
    public override MinecartKind Kind => MinecartKind.Tnt;
    public override string TypeKey => "tnt_minecart";
    public override int ItemId => I.TntMinecart;
    public override int DisplayBlock => B.Tnt;
    public void Ignite()
    {
        if (Fuse >= 0) return;
        Fuse = 80; World.PlaySound("game.tnt.primed", X, Y, Z, 1f, 1f);
    }
    protected override void OnActivatorRail(bool powered) { if (powered) Ignite(); }
    public override void Tick()
    {
        base.Tick(); if (Removed || IsRemoteControlled) return;
        if (Fuse > 0) { Fuse--; World.SpawnParticles(ParticleKind.Smoke, X, Y + 0.5, Z, 1); }
        else if (Fuse == 0) Explode(MotionX * MotionX + MotionZ * MotionZ);
    }
    public void Explode(double speedSq)
    {
        if (World.IsRemote || Removed) return;
        Kill(); Explosion.Explode(World, this, X, Y, Z, (float)(4 + World.Rand.NextDouble() * 1.5 * Math.Min(5, Math.Sqrt(speedSq))), true);
    }
    protected override void OnFall(float distance) { if (distance >= 3) Explode(distance * distance / 100); }
    public override bool Attack(DamageSource source, float amount)
    {
        if (source.Source is ArrowEntity { FireTicks: > 0 } arrow) { Explode(arrow.MotionX * arrow.MotionX + arrow.MotionY * arrow.MotionY + arrow.MotionZ * arrow.MotionZ); return true; }
        return base.Attack(source, amount);
    }
    protected override void Destroy(DamageSource source, bool creative)
    {
        if (source.IsFire || source.IsExplosion || MotionX * MotionX + MotionZ * MotionZ >= 0.01)
        { Explode(MotionX * MotionX + MotionZ * MotionZ); return; }
        base.Destroy(source, creative);
    }
    private bool ProtectedRail(World.World w, int x, int y, int z) => Fuse >= 0 && (RailBlock.IsRail(w.GetBlock(x, y, z)) || RailBlock.IsRail(w.GetBlock(x, y + 1, z)));
    public float Resistance(World.World w, int x, int y, int z, Block b) => ProtectedRail(w, x, y, z) ? 0 : b.IsLiquid ? 100 : b.BlastResistance;
    public bool CanDestroy(World.World w, int x, int y, int z) => !ProtectedRail(w, x, y, z);
    public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(Fuse); }
    public override void Load(BinaryReader br) { base.Load(br); Fuse = br.ReadInt32(); }
}
