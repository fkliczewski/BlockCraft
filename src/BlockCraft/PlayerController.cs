using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Logika interakcji gracza: kopanie, stawianie, używanie przedmiotów, atak.</summary>
public sealed partial class PlayerController
{
    private readonly Game _g;
    public World.World W => _g.World;
    public Player P => _g.Player;
    public float BreakProgress; public int BreakX, BreakY, BreakZ = int.MinValue; private int _breakSoundTicks;
    public int BlockHitDelay, RightClickDelay;
    public World.World.HitResult Hit; public Entity HitEntity;
    public bool LeftWasDown;

    public PlayerController(Game g) { _g = g; }

    public double Reach => P.Creative ? 5.0 : 4.5;

    public void UpdateTarget(float pt)
    {
        var p = P;
        double ex = p.IX(pt), ey = p.IY(pt) + p.EyeHeight, ez = p.IZ(pt);
        var (lx, ly, lz) = p.Look(pt);
        Hit = W.RayCast(ex, ey, ez, lx, ly, lz, Reach);
        HitEntity = null;
        double best = Hit.Hit ? Hit.Distance : Reach;
        double er = p.Creative ? 5 : 3;
        if (best > er) best = er;
        foreach (var e in W.Entities)
        {
            if (e == p || !e.CanBeAttacked || e.Removed) continue;
            if (e.DistanceSq(ex, ey, ez) > 64) continue;
            var bb = e.BoundingBox.Grow(0.1);
            if (bb.Contains(ex, ey, ez)) { HitEntity = e; best = 0; continue; }
            if (bb.RayIntersect(ex, ey, ez, lx, ly, lz, out double t, out _) && t < best) { best = t; HitEntity = e; }
        }
        if (HitEntity != null) Hit.Hit = false;
    }

    public int BreakStage => BreakProgress > 0 && BreakX != int.MinValue ? (int)(BreakProgress * 10) - 1 : -1;

    public static float BreakDelta(Player p, Block b, ItemStack tool)
    {
        if (b.Hardness < 0) return 0;
        if (b.Hardness == 0) return 1;
        float f = b.GetDigSpeed(tool);
        if (p.IsHeadInWater()) f /= 5f;
        if (!p.OnGround && !p.Flying) f /= 5f;
        return b.CanHarvest(tool) ? f / b.Hardness / 30f : f / b.Hardness / 100f;
    }

    public void ResetBreak() { BreakProgress = 0; BreakX = int.MinValue; }

    /// <summary>Wywoływane co tick, gdy LPM jest wciśnięty.</summary>
    public void TickLeft(bool pressedNow)
    {
        var p = P;
        if (p.Spectator) return;
        if (pressedNow)
        {
            p.SwingArm();
            if (HitEntity != null) { p.AttackEntity(HitEntity); ResetBreak(); return; }
        }
        if (!Hit.Hit) { ResetBreak(); if (!pressedNow) { } return; }
        if (BlockHitDelay > 0) { BlockHitDelay--; return; }
        int x = Hit.X, y = Hit.Y, z = Hit.Z;
        var b = Block.Get(W.GetBlock(x, y, z));
        if (b.Id == 0) return;
        var tool = p.Inventory.Selected;
        if (p.Creative)
        {
            if (tool?.Item?.Tool == ToolType.Sword) return;
            if (!pressedNow && BlockHitDelay > 0) return;
            DestroyBlock(x, y, z, false);
            BlockHitDelay = 5;
            p.SwingArm();
            return;
        }
        if (x != BreakX || y != BreakY || z != BreakZ) { BreakX = x; BreakY = y; BreakZ = z; BreakProgress = 0; b.OnClicked(W, x, y, z, p); }
        BreakProgress += BreakDelta(p, b, tool);
        p.SwingArm();
        if (_breakSoundTicks++ % 4 == 0)
        {
            W.PlaySound("step_" + b.Sound.ToString().ToLowerInvariant(), x + 0.5, y + 0.5, z + 0.5, 0.25f, 0.5f);
            _g.Renderer.BlockCrackParticle(x, y, z, Hit.Face);
        }
        if (BreakProgress >= 1f)
        {
            bool harvest = b.CanHarvest(tool);
            DestroyBlock(x, y, z, harvest);
            ResetBreak();
            BlockHitDelay = 5;
            if (tool?.Item != null && tool.Item.IsTool && b.Hardness > 0) p.DamageHeld(tool.Item.Tool == ToolType.Sword ? 2 : 1);
            p.AddExhaustion(0.025f);
        }
    }

    private void DestroyBlock(int x, int y, int z, bool drop)
    {
        var b = Block.Get(W.GetBlock(x, y, z));
        int meta = W.GetMeta(x, y, z);
        var tool = P.Inventory.Selected;
        if (!P.Creative) b.OnDestroyedByPlayer(W, x, y, z, meta, P);
        // liście: ścinanie sąsiadów przyspiesza zanik
        if (b.Id == B.OakLog || b.Id == B.BirchLog)
            for (int dx = -4; dx <= 4; dx++) for (int dy = -4; dy <= 4; dy++) for (int dz = -4; dz <= 4; dz++)
                    {
                        int id = W.GetBlock(x + dx, y + dy, z + dz);
                        if ((id == B.OakLeaves || id == B.BirchLeaves) && W.Rand.Next(3) == 0) W.ScheduleTick(x + dx, y + dy, z + dz, (byte)id, 20 + W.Rand.Next(200));
                    }
        W.BreakBlock(x, y, z, drop && !P.Creative, P, tool);
    }

    /// <summary>PPM: co 4 ticki gdy trzymany.</summary>
    public void TickRight(bool pressedNow)
    {
        var p = P;
        if (p.Spectator) return;
        if (RightClickDelay > 0 && !pressedNow) { RightClickDelay--; return; }
        if (p.ItemInUse != null) return;
        RightClickDelay = 4;
        var held = p.Inventory.Selected;
        if (p.RidingMob is Pig ridden && held?.Id == I.CarrotOnAStick) { if (ridden.Boost(p)) p.SwingArm(); return; }
        if (HitEntity != null && HitEntity != p.RidingMob)
        {
            if (HitEntity is Mob named && held?.Id == I.NameTag && !string.IsNullOrEmpty(held.CustomName))
            { named.CustomName = held.CustomName; named.PersistenceRequired = true; Mob.ConsumeHeld(p); p.SwingArm(); return; }
            if (HitEntity.Interact(p)) { p.SwingArm(); return; }
        }
        if (Hit.Hit)
        {
            int x = Hit.X, y = Hit.Y, z = Hit.Z;
            var b = Block.Get(W.GetBlock(x, y, z));
            if (LeashKnot.TryTie(W, p, x, y, z)) { p.SwingArm(); return; }
            if (!p.Sneaking || held == null)
                if (b.OnActivated(W, x, y, z, p)) { p.SwingArm(); return; }
            if (held != null && UseOnBlock(held, x, y, z, Hit.Face, Hit.HitX - x, Hit.HitY - y, Hit.HitZ - z)) { p.SwingArm(); p.Inventory.Dirty = true; GolemBuilder.AfterPlace(W, x, y, z, Hit.Face); return; }
        }
        if (held != null) UseInAir(held);
    }

    private bool UseInAir(ItemStack held)
    {
        var p = P; var it = held.Item;
        if (it.IsFood) { if (p.CanEat(it.Id == I.GoldenApple)) p.StartUsing(held); return true; }
        if (it.Id == I.MilkBucket) { p.StartUsing(held); return true; }
        if (it.Id == I.Bow) { if (p.Creative || p.Inventory.CountOf(I.Arrow) > 0) p.StartUsing(held); return true; }
        if (it.Id == I.EnderPearl || it.Id == I.Egg)
        {
            W.SpawnEntity(new ThrownEntity(W, it.Id == I.EnderPearl ? 0 : 1, p));
            W.PlaySound("bow", p.X, p.Y, p.Z, 0.5f, 0.4f);
            if (!p.Creative) Consume(held);
            p.SwingArm();
            return true;
        }
        if (it.ArmorSlot >= 0 && p.Inventory.Armor[it.ArmorSlot] == null)
        {
            p.Inventory.Armor[it.ArmorSlot] = held.CopyWith(1);
            Consume(held, true);
            return true;
        }
        if (it.Id == I.Bucket || it.Id == I.WaterBucket || it.Id == I.LavaBucket) return UseBucket(held);
        return false;
    }

    private void Consume(ItemStack held, bool force = false)
    {
        if (P.Creative && !force) return;
        held.Count--;
        if (held.Count <= 0) P.Inventory.Selected = null;
        P.Inventory.Dirty = true;
    }

    private bool UseBucket(ItemStack held)
    {
        var p = P;
        double ex = p.X, ey = p.Y + p.EyeHeight, ez = p.Z;
        var (lx, ly, lz) = p.Look();
        var h = W.RayCast(ex, ey, ez, lx, ly, lz, 5, true);
        if (!h.Hit) return false;
        int id = W.GetBlock(h.X, h.Y, h.Z);
        if (held.Id == I.Bucket)
        {
            if ((id == B.Water || id == B.Lava) && W.GetMeta(h.X, h.Y, h.Z) == 0)
            {
                W.SetBlock(h.X, h.Y, h.Z, B.Air);
                W.PlaySound("bucket_fill", h.X, h.Y, h.Z);
                if (p.Creative) return true;
                var full = new ItemStack(id == B.Water ? I.WaterBucket : I.LavaBucket);
                if (held.Count == 1) p.Inventory.Selected = full;
                else { held.Count--; if (!p.Inventory.Add(full)) p.DropStack(full, false); }
                return true;
            }
            return false;
        }
        int tx = h.X, ty = h.Y, tz = h.Z;
        var tb = Block.Get(id);
        if (!tb.Replaceable || tb.IsLiquid && W.GetMeta(tx, ty, tz) == 0) { tx += Dir.X[h.Face]; ty += Dir.Y[h.Face]; tz += Dir.Z[h.Face]; }
        var at = Block.Get(W.GetBlock(tx, ty, tz));
        if (!at.Replaceable && at.Id != 0) return false;
        if (at.Id != 0 && !at.IsLiquid) W.BreakBlock(tx, ty, tz, true);
        W.SetBlock(tx, ty, tz, held.Id == I.WaterBucket ? B.Water : B.Lava, 0);
        W.PlaySound("bucket_empty", tx, ty, tz);
        if (!p.Creative) p.Inventory.Selected = new ItemStack(I.Bucket);
        return true;
    }

    private bool UseOnBlock(ItemStack held, int x, int y, int z, int face, double hx, double hy, double hz)
    {
        var p = P; var it = held.Item;
        int id = W.GetBlock(x, y, z);
        if (held.Id == I.SpawnEgg)
        {
            var egg = SpawnEggs.Find(held.Damage);
            if (egg == null) return false;
            if (W.GetTileEntity(x, y, z) is MobSpawnerTile spawner)
            { spawner.SetData("EntityId", egg.Key); Consume(held); return true; }
            double sx = x + Dir.X[face] + .5, sy = y + Dir.Y[face], sz = z + Dir.Z[face] + .5;
            if (SpawnEggs.Spawn(W, held, sx, sy, sz) == null) return false;
            Consume(held); return true;
        }
        // motyka
        if (it.Tool == ToolType.Hoe && face != Dir.Down && (id == B.Grass || id == B.Dirt) && W.GetBlock(x, y + 1, z) == B.Air)
        {
            W.SetBlock(x, y, z, B.Farmland, 0);
            W.PlaySound("step_gravel", x + 0.5, y + 0.5, z + 0.5);
            p.DamageHeld(1);
            return true;
        }
        if (it.Id == I.Seeds)
        {
            if (face == Dir.Up && id == B.Farmland && W.GetBlock(x, y + 1, z) == B.Air) { W.SetBlock(x, y + 1, z, B.Wheat, 0); Consume(held); W.PlaySound("step_grass", x, y, z); return true; }
            return false;
        }
        if (it.Id == I.BoneMeal) return BoneMeal(held, x, y, z);
        if (it.Id == I.Bucket || it.Id == I.WaterBucket || it.Id == I.LavaBucket) return UseBucket(held);
        if (it.Id == I.FlintAndSteel)
        {
            if (id == B.Tnt) { TntBlock.Ignite(W, x, y, z, 80); p.DamageHeld(1); return true; }
            return false;
        }
        if (it.IsFood || it.Id == I.MilkBucket || it.Id == I.Bow || it.Id == I.EnderPearl || it.Id == I.Egg || it.ArmorSlot >= 0) return UseInAir(held);
        if (!it.IsBlock) return false;
        return PlaceBlock(held, x, y, z, face, hx, hy, hz);
    }

    private bool BoneMeal(ItemStack held, int x, int y, int z)
    {
        int id = W.GetBlock(x, y, z);
        bool used = false;
        if (id == B.Wheat && W.GetMeta(x, y, z) < 7) { W.SetMeta(x, y, z, Math.Min(7, W.GetMeta(x, y, z) + 2 + W.Rand.Next(4))); used = true; }
        else if (id == B.OakSapling || id == B.BirchSapling) { if (W.Rand.NextFloat() < 0.45f) ((SaplingBlock)Block.Get(id)).Grow(W, x, y, z, W.Rand); used = true; }
        else if (id == B.Grass)
        {
            for (int i = 0; i < 64; i++)
            {
                int tx = x + W.Rand.Next(7) - 3, tz = z + W.Rand.Next(7) - 3, ty = y + W.Rand.Next(3) - 1;
                if (W.GetBlock(tx, ty, tz) == B.Grass && W.GetBlock(tx, ty + 1, tz) == B.Air)
                    W.SetBlock(tx, ty + 1, tz, W.Rand.Next(10) == 0 ? (W.Rand.NextBool() ? B.Dandelion : B.Poppy) : B.TallGrass);
            }
            used = true;
        }
        if (used) { Consume(held); W.SpawnParticles(ParticleKind.Heart, x + 0.5, y + 1, z + 0.5, 3); }
        return used;
    }

    private bool PlaceBlock(ItemStack held, int x, int y, int z, int face, double hx, double hy, double hz)
    {
        var p = P; var it = held.Item;
        byte bid = (byte)it.BlockId;
        var nb = Block.Get(bid);
        // płyty łączą się
        if (nb is SlabBlock slab && W.GetBlock(x, y, z) == bid)
        {
            int m = W.GetMeta(x, y, z);
            if (face == Dir.Up && m == 0 || face == Dir.Down && m == 1) { W.SetBlock(x, y, z, (byte)slab.DoubleId, 0); Consume(held); PlaceSound(nb, x, y, z); return true; }
        }
        var target = Block.Get(W.GetBlock(x, y, z));
        int tx = x, ty = y, tz = z;
        if (!target.Replaceable || target.IsLiquid && false) { tx += Dir.X[face]; ty += Dir.Y[face]; tz += Dir.Z[face]; }
        else face = Dir.Up;
        if (ty < 0 || ty >= Chunk.Height) return false;
        var at = Block.Get(W.GetBlock(tx, ty, tz));
        if (nb is SlabBlock slab2 && at.Id == bid)
        {
            int m = W.GetMeta(tx, ty, tz);
            W.SetBlock(tx, ty, tz, (byte)slab2.DoubleId, 0); Consume(held); PlaceSound(nb, tx, ty, tz); return true;
        }
        if (!at.Replaceable && at.Id != 0) return false;
        if (!nb.CanPlaceAt(W, tx, ty, tz)) return false;
        int meta = nb.GetPlacementMeta(W, tx, ty, tz, face, hx, hy, hz, p, held.Damage);
        if (meta < 0) return false;
        // kolizja z encjami
        if (nb.Solid)
        {
            var boxes = new List<AABB>();
            nb.GetCollisionBoxes(W, tx, ty, tz, meta, boxes);
            foreach (var b in boxes)
            {
                var wb = b.Offset(tx, ty, tz);
                foreach (var e in W.GetEntitiesInBox(wb))
                    if (e is LivingEntity && !e.Dead && !(e is Player pl && pl.Spectator)) return false;
            }
        }
        if (bid == B.OakDoor)
        {
            if (ty + 1 >= Chunk.Height || !Block.Get(W.GetBlock(tx, ty + 1, tz)).Replaceable || !Block.Get(W.GetBlock(tx, ty - 1, tz)).Opaque) return false;
            int d = (Dir.HorizFromYaw(p.Yaw) + 3) & 3;
            // zawias: zależnie od sąsiadów
            int lx = tx - Dir.HorizX[(d + 1) & 3], lz = tz - Dir.HorizZ[(d + 1) & 3];
            bool hinge = W.GetBlock(lx, ty, lz) == B.OakDoor;
            W.SetBlock(tx, ty, tz, B.OakDoor, d, false);
            W.SetBlock(tx, ty + 1, tz, B.OakDoor, 8 | (hinge ? 1 : 0), true);
            W.NotifyNeighbors(tx, ty, tz, B.OakDoor);
            Consume(held); PlaceSound(nb, tx, ty, tz);
            return true;
        }
        if (bid == B.Bed)
        {
            int d = Dir.HorizFromYaw(p.Yaw);
            int hx2 = tx + Dir.HorizX[d], hz2 = tz + Dir.HorizZ[d];
            if (!Block.Get(W.GetBlock(hx2, ty, hz2)).Replaceable || !Block.Get(W.GetBlock(tx, ty - 1, tz)).Opaque || !Block.Get(W.GetBlock(hx2, ty - 1, hz2)).Opaque) return false;
            W.SetBlock(tx, ty, tz, B.Bed, d, false);
            W.SetBlock(hx2, ty, hz2, B.Bed, d | 8, true);
            Consume(held); PlaceSound(nb, tx, ty, tz);
            return true;
        }
        if (at.Id != 0 && !at.IsLiquid) W.BreakBlock(tx, ty, tz, false);
        W.SetBlock(tx, ty, tz, bid, meta);
        nb.OnPlaced(W, tx, ty, tz, p);
        PlaceSound(nb, tx, ty, tz);
        Consume(held);
        return true;
    }

    private void PlaceSound(Block b, int x, int y, int z) => W.PlaySound("place_" + b.Sound.ToString().ToLowerInvariant(), x + 0.5, y + 0.5, z + 0.5, 1f, 0.8f);

    public void PickBlock()
    {
        if (!Hit.Hit) return;
        var b = Block.Get(W.GetBlock(Hit.X, Hit.Y, Hit.Z));
        var s = b.PickStack(W.GetMeta(Hit.X, Hit.Y, Hit.Z));
        if (s == null || s.Item == null) return;
        var inv = P.Inventory;
        int slot = inv.FindSlot(s.Id, s.Item.MaxDamage > 0 ? -1 : s.Damage);
        if (slot >= 0 && slot < 9) { inv.SelectedSlot = slot; return; }
        if (!P.Creative)
        {
            if (slot >= 9) { var tmp = inv.Main[inv.SelectedSlot]; inv.Main[inv.SelectedSlot] = inv.Main[slot]; inv.Main[slot] = tmp; inv.Dirty = true; }
            return;
        }
        int empty = inv.FirstEmptyHotbar();
        if (empty >= 0) inv.SelectedSlot = empty;
        inv.Main[inv.SelectedSlot] = s.CopyWith(s.MaxStack);
        inv.Dirty = true;
    }
}
