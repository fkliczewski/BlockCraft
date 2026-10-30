using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World
{
    /// <summary>Transfer przedmiotów między kontenerami jak TileEntityHopper 1.8 (statyczne metody używane też przez podajnik i wagonik z lejem).</summary>
    public static class HopperLogic
    {
        /// <summary>Sloty dostępne od strony face (null = wszystkie, MC: brak strony).</summary>
        private static IEnumerable<int> SlotsFor(IInventory inv, int face)
        {
            if (face >= 0 && inv is ISidedInventory s) return s.SlotsForFace(face);
            if (face >= 0 && inv is FurnaceTileEntity)
                return face == Dir.Down ? new[] { 2, 1 } : face == Dir.Up ? new[] { 0 } : new[] { 1 };
            return Enumerable.Range(0, inv.Size);
        }
        /// <summary>MC isItemValidForSlot (piec: wyjście zamknięte, paliwo tylko paliwo lub wiadro).</summary>
        private static bool ValidForSlot(IInventory inv, int slot, ItemStack s)
        {
            if (inv is FurnaceTileEntity)
            {
                if (slot == 2) return false;
                if (slot == 1) return Smelting.BurnTime(s) > 0 || s.Id == I.Bucket;
            }
            return true;
        }
        private static bool CanInsert(IInventory inv, ItemStack s, int slot, int face)
        {
            if (!ValidForSlot(inv, slot, s)) return false;
            return face < 0 || inv is not ISidedInventory si || si.CanInsert(slot, s, face);
        }
        private static bool CanExtract(IInventory inv, ItemStack s, int slot, int face)
        {
            if (inv is ISidedInventory si) return si.CanExtract(slot, s, face);
            if (inv is FurnaceTileEntity && face == Dir.Down && slot == 1) return s.Id == I.Bucket;
            return true;
        }
        private static bool CanCombine(ItemStack a, ItemStack b) => a.Id == b.Id && a.Damage == b.Damage && a.Count <= a.MaxStack && Nbt.Same(a.Tag, b.Tag);

        /// <summary>MC isInventoryFull.</summary>
        public static bool IsFull(IInventory inv, int face)
        {
            foreach (int i in SlotsFor(inv, face))
            {
                var s = inv.Get(i);
                if (s == null || s.Count != s.MaxStack) return false;
            }
            return true;
        }
        /// <summary>MC isInventoryEmpty.</summary>
        public static bool IsEmpty(IInventory inv, int face)
        {
            foreach (int i in SlotsFor(inv, face)) if (inv.Get(i) != null) return false;
            return true;
        }

        /// <summary>MC putStackInInventoryAllSlots: zwraca resztę (null = wszystko weszło).</summary>
        public static ItemStack PutStack(IInventory inv, ItemStack stack, int face)
        {
            foreach (int i in SlotsFor(inv, face).ToArray())
            {
                if (stack == null || stack.Count <= 0) break;
                stack = InsertStack(inv, stack, i, face);
            }
            return stack == null || stack.Count <= 0 ? null : stack;
        }
        private static ItemStack InsertStack(IInventory inv, ItemStack stack, int index, int face)
        {
            var cur = inv.Get(index);
            if (!CanInsert(inv, stack, index, face)) return stack;
            bool moved = false;
            if (cur == null) { inv.Set(index, stack); stack = null; moved = true; }
            else if (CanCombine(cur, stack))
            {
                int j = Math.Min(stack.Count, stack.MaxStack - cur.Count);
                stack.Count -= j; cur.Count += j;
                moved = j > 0;
            }
            if (moved)
            {
                if (inv is HopperTileEntity h && h.TransferCooldown <= 1) h.TransferCooldown = 8;
                inv.Changed();
            }
            return stack;
        }

        /// <summary>MC getInventoryAtPosition: tile entity z inventory albo losowa encja z inventory (wagonik) w kostce.</summary>
        public static IInventory InventoryAt(World w, int x, int y, int z)
        {
            int id = w.GetBlock(x, y, z);
            if (Block.Get(id).HasTileEntity && w.GetTileEntity(x, y, z) is IInventory inv) return inv;
            var list = RsUtil.EntitiesIn(w, new AABB(x, y, z, x + 1, y + 1, z + 1)).Where(e => e is IInventory).ToList();
            return list.Count > 0 ? (IInventory)list[w.Rand.Next(list.Count)] : null;
        }

        /// <summary>MC pullItemFromSlot.</summary>
        private static bool PullFromSlot(IInventory hopper, IInventory src, int index, int face)
        {
            var s = src.Get(index);
            if (s == null || !CanExtract(src, s, index, face)) return false;
            var copy = s.Copy();
            var one = s.Copy(); one.Count = 1;
            s.Count--; src.Set(index, s.Count > 0 ? s : null);
            var rest = PutStack(hopper, one, -1);
            if (rest == null) { src.Changed(); return true; }
            src.Set(index, copy);
            return false;
        }
        /// <summary>MC putDropInInventoryAllSlots: true, gdy cały stos przedmiotu wszedł.</summary>
        public static bool PutDrop(IInventory inv, ItemEntity item)
        {
            if (item == null || item.Removed) return false;
            var rest = PutStack(inv, item.Stack.Copy(), -1);
            if (rest != null) { item.Stack = rest; return false; }
            item.Kill();
            return true;
        }
        /// <summary>MC captureDroppedItems: pobiera z kontenera nad lejem albo zasysa przedmioty (cx, cy, cz = środek leja).</summary>
        public static bool Capture(World w, IInventory hopper, double cx, double cy, double cz)
        {
            int ax = MathUtil.FloorToInt(cx), ay = MathUtil.FloorToInt(cy + 1), az = MathUtil.FloorToInt(cz);
            var above = InventoryAt(w, ax, ay, az);
            if (above != null)
            {
                if (IsEmpty(above, Dir.Down)) return false;
                foreach (int i in SlotsFor(above, Dir.Down).ToArray()) if (PullFromSlot(hopper, above, i, Dir.Down)) return true;
                return false;
            }
            // MC: kostka nad lejem; dodatkowo misa leja (od 0.6), bo przedmiot leży w niej na wysokości 0.625
            var box = new AABB(cx - 0.5, cy + 0.1, cz - 0.5, cx + 0.5, cy + 1.5, cz + 0.5);
            foreach (var e in w.GetEntitiesInBox(box))
                if (e is ItemEntity ie && !ie.Removed && PutDrop(hopper, ie)) return true;
            return false;
        }
        /// <summary>Dodaj stos do pierwszego wolnego slotu (MC TileEntityDispenser.addItemStack). Zwraca slot albo -1.</summary>
        public static int AddToFreeSlot(IInventory inv, ItemStack s)
        {
            for (int i = 0; i < inv.Size; i++)
                if (inv.Get(i) == null) { inv.Set(i, s); inv.Changed(); return i; }
            return -1;
        }
    }

    /// <summary>Lej jak BlockHopper 1.8. meta: bity 0-2 kierunek wylotu (bez góry), bit 3 = zablokowany sygnałem.</summary>
    public sealed class HopperBlock : Block
    {
        public HopperBlock(byte id, string k, string n) : base(id, k, n)
        {
            T("hopper_outside");
            Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
            Hardness = 3f; BlastResistance = 4.8f; Tool = ToolType.Pickaxe; HarvestLevel = 0; Sound = SoundType.Metal; HasTileEntity = true;
        }
        public static int Facing(int meta) => meta & 7;
        public static bool Enabled(int meta) => (meta & 8) == 0;
        public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        {
            int f = Dir.Opp[face];
            return f == Dir.Up ? Dir.Down : f;
        }
        public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        {
            const double t = 0.125;
            list.Add(new AABB(0, 0, 0, 1, 0.625, 1));
            list.Add(new AABB(0, 0, 0, t, 1, 1)); list.Add(new AABB(0, 0, 0, 1, 1, t));
            list.Add(new AABB(1 - t, 0, 0, 1, 1, 1)); list.Add(new AABB(0, 0, 1 - t, 1, 1, 1));
        }
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1, 1));
        public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        {
            var o = ModelBox.All(Tex[0]);
            int inside = TextureGen.Layer("hopper_inside");
            boxes.Add(ModelBox.Px(0, 10, 0, 16, 16, 2, o));
            boxes.Add(ModelBox.Px(0, 10, 14, 16, 16, 16, o));
            boxes.Add(ModelBox.Px(0, 10, 2, 2, 16, 14, o));
            boxes.Add(ModelBox.Px(14, 10, 2, 16, 16, 14, o));
            boxes.Add(ModelBox.Px(2, 10, 2, 14, 11, 14, new[] { Tex[0], inside, Tex[0], Tex[0], Tex[0], Tex[0] }));
            boxes.Add(ModelBox.Px(4, 4, 4, 12, 10, 12, o));
            boxes.Add(Facing(meta) switch
            {
                Dir.North => ModelBox.Px(6, 4, 0, 10, 8, 4, o),
                Dir.South => ModelBox.Px(6, 4, 12, 10, 8, 16, o),
                Dir.West => ModelBox.Px(0, 4, 6, 4, 8, 10, o),
                Dir.East => ModelBox.Px(12, 4, 6, 16, 8, 10, o),
                _ => ModelBox.Px(6, 0, 6, 10, 4, 10, o),
            });
        }
        private static void UpdateState(World w, int x, int y, int z)
        {
            int m = w.GetMeta(x, y, z);
            bool enabled = !Redstone.IsBlockPowered(w, x, y, z);
            if (enabled != Enabled(m)) w.SetMeta(x, y, z, enabled ? m & ~8 : m | 8, false);
        }
        public override void OnAdded(World w, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is not HopperTileEntity) w.SetTileEntity(x, y, z, new HopperTileEntity());
            if (!w.IsRemote) UpdateState(w, x, y, z);
        }
        public override void OnNeighborChanged(World w, int x, int y, int z, int nid) { if (!w.IsRemote) UpdateState(w, x, y, z); }
        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            if (w.GetTileEntity(x, y, z) is HopperTileEntity te) RsGui.Open(p, new RsContainer(p.Inventory, te, "Lej", RsContainer.Layout.Hopper));
            return true;
        }
        public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
        {
            if (w.GetTileEntity(x, y, z) is HopperTileEntity te)
            {
                if (!w.IsRemote) foreach (var s in te.Slots) if (s != null) w.DropItem(x + 0.5, y + 0.5, z + 0.5, s);
                w.RemoveTileEntity(x, y, z);
            }
            if (!w.IsRemote) Redstone.UpdateComparatorOutputLevel(w, x, y, z, Id);
        }
    }

    /// <summary>Tile entity leja (MC TileEntityHopper): 5 slotów, transfer co 8 ticków.</summary>
    public sealed class HopperTileEntity : TileEntity, IInventory
    {
        public readonly ItemStack[] Slots = new ItemStack[5];
        public int TransferCooldown = -1;
        public override int TypeId => 13;
        public int Size => 5;
        public ItemStack Get(int i) => Slots[i];
        public void Set(int i, ItemStack s) { Slots[i] = s != null && s.Count <= 0 ? null : s; }
        public void Changed() { }

        public override void Tick(World w)
        {
            if (w.IsRemote) return;
            TransferCooldown--;
            if (TransferCooldown > 0) return;
            TransferCooldown = 0;
            UpdateHopper(w);
        }
        /// <summary>MC updateHopper.</summary>
        public bool UpdateHopper(World w)
        {
            if (TransferCooldown > 0 || !HopperBlock.Enabled(w.GetMeta(X, Y, Z))) return false;
            bool moved = false;
            if (!HopperLogic.IsEmpty(this, -1)) moved = TransferOut(w);
            if (!HopperLogic.IsFull(this, -1)) moved = HopperLogic.Capture(w, this, X + 0.5, Y + 0.5, Z + 0.5) || moved;
            if (moved) { TransferCooldown = 8; return true; }
            return false;
        }
        /// <summary>MC transferItemsOut.</summary>
        private bool TransferOut(World w)
        {
            int f = HopperBlock.Facing(w.GetMeta(X, Y, Z));
            var target = HopperLogic.InventoryAt(w, X + Dir.X[f], Y + Dir.Y[f], Z + Dir.Z[f]);
            if (target == null) return false;
            int side = Dir.Opp[f];
            if (HopperLogic.IsFull(target, side)) return false;
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] == null) continue;
                var copy = Slots[i].Copy();
                var one = Slots[i].Copy(); one.Count = 1;
                Slots[i].Count--; if (Slots[i].Count <= 0) Slots[i] = null;
                var rest = HopperLogic.PutStack(target, one, side);
                if (rest == null) { target.Changed(); return true; }
                Slots[i] = copy;
            }
            return false;
        }
        public override void Save(BinaryWriter bw) { foreach (var s in Slots) bw.WriteStack(s); bw.Write(TransferCooldown); }
        public override void Load(BinaryReader br) { for (int i = 0; i < 5; i++) Slots[i] = br.ReadStack(); TransferCooldown = br.ReadInt32(); }
    }

    /// <summary>Źródło dozowania (MC IBlockSource): dozownik / podajnik w świecie.</summary>
    public sealed class DispenseSource
    {
        public World W; public int X, Y, Z; public int Facing; public DispenserTileEntity Te;
        public double CX => X + 0.5; public double CY => Y + 0.5; public double CZ => Z + 0.5;
        /// <summary>MC getDispensePosition: 0.7 bloku przed środkiem.</summary>
        public (double x, double y, double z) Position => (CX + 0.7 * Dir.X[Facing], CY + 0.7 * Dir.Y[Facing], CZ + 0.7 * Dir.Z[Facing]);
        public (int x, int y, int z) Front => (X + Dir.X[Facing], Y + Dir.Y[Facing], Z + Dir.Z[Facing]);
    }

    /// <summary>Zachowanie dozownika dla przedmiotu (MC IBehaviorDispenseItem). Zwraca stos, który zostaje w slocie.</summary>
    public delegate ItemStack DispenseBehavior(DispenseSource src, ItemStack stack);

    /// <summary>
    /// Zachowania dozownika 1.8 (MC BlockDispenser.dispenseBehaviorRegistry). Moduły dopisują swoje: DispenserBehaviors.ById[itemId] = ...
    /// Pomocnicze: Projectile (pociski), Default (wyrzucenie przedmiotu), Throw (kierunek lotu jak setThrowableHeading).
    /// </summary>
    public static class DispenserBehaviors
    {
        public static readonly Dictionary<int, DispenseBehavior> ById = new();
        /// <summary>Mączka kostna na bloku (MC ItemDye.applyBonemeal). OW może podmienić na pełną wersję.</summary>
        public static Func<World, int, int, int, bool> ApplyBonemeal = DefaultBonemeal;

        public static DispenseBehavior Get(ItemStack s) => s != null && ById.TryGetValue(s.Id, out var b) ? b : Default;

        /// <summary>MC BehaviorDefaultDispenseItem: wyrzuca jeden przedmiot.</summary>
        public static ItemStack Default(DispenseSource src, ItemStack stack)
        {
            var one = stack.Copy(); one.Count = 1; stack.Count--;
            var (x, y, z) = src.Position;
            DropItem(src.W, one, 6, src.Facing, x, y, z);
            Click(src); Smoke(src);
            return stack;
        }
        /// <summary>MC doDispense.</summary>
        public static void DropItem(World w, ItemStack s, int speed, int facing, double x, double y, double z)
        {
            y -= facing <= 1 ? 0.125 : 0.15625;
            var e = new ItemEntity(w, x, y, z, s);
            double d = w.Rand.NextDouble() * 0.1 + 0.2;
            e.MotionX = Dir.X[facing] * d; e.MotionY = 0.2; e.MotionZ = Dir.Z[facing] * d;
            e.MotionX += w.Rand.NextGaussian() * 0.0075 * speed;
            e.MotionY += w.Rand.NextGaussian() * 0.0075 * speed;
            e.MotionZ += w.Rand.NextGaussian() * 0.0075 * speed;
            w.SpawnEntity(e);
        }
        public static void Click(DispenseSource s) => s.W.PlaySound("random.click", s.CX, s.CY, s.CZ, 1f, 1f);
        public static void Fail(DispenseSource s) => s.W.PlaySound("random.click", s.CX, s.CY, s.CZ, 1f, 1.2f);
        public static void Smoke(DispenseSource s)
        {
            var (x, y, z) = s.Position;
            s.W.SpawnParticles(ParticleKind.Smoke, x, y, z, 5);
        }
        /// <summary>MC setThrowableHeading.</summary>
        public static void Throw(Entity e, double x, double y, double z, float velocity, float inaccuracy)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            x /= len; y /= len; z /= len;
            var r = e.World.Rand;
            x += r.NextGaussian() * 0.0075 * inaccuracy; y += r.NextGaussian() * 0.0075 * inaccuracy; z += r.NextGaussian() * 0.0075 * inaccuracy;
            e.MotionX = x * velocity; e.MotionY = y * velocity; e.MotionZ = z * velocity;
            double h = Math.Sqrt(x * x + z * z);
            e.Yaw = e.PrevYaw = (float)(Math.Atan2(x, z) * MathUtil.Rad2Deg);
            e.Pitch = e.PrevPitch = (float)(Math.Atan2(y, h) * MathUtil.Rad2Deg);
        }
        /// <summary>MC BehaviorProjectileDispense: create(świat, x, y, z, stos) tworzy pocisk; domyślnie prędkość 1.1, rozrzut 6.</summary>
        public static DispenseBehavior Projectile(Func<World, double, double, double, ItemStack, Entity> create, float velocity = 1.1f, float inaccuracy = 6f)
            => (src, stack) =>
            {
                var (x, y, z) = src.Position;
                var e = create(src.W, x, y, z, stack);
                e.SetPositionAndPrev(x, y, z);
                Throw(e, Dir.X[src.Facing], Dir.Y[src.Facing] + 0.1f, Dir.Z[src.Facing], velocity, inaccuracy);
                src.W.SpawnEntity(e);
                stack.Count--;
                src.W.PlaySound("random.bow", src.CX, src.CY, src.CZ, 1f, 1.2f);
                Smoke(src);
                return stack;
            };

        static DispenserBehaviors()
        {
            ById[I.Arrow] = Projectile((w, x, y, z, s) => new ArrowEntity(w) { PickupAllowed = true });
            ById[I.Egg] = Projectile((w, x, y, z, s) => new ThrownEntity(w, 1, null));
            ById[I.WaterBucket] = FilledBucket;
            ById[I.LavaBucket] = FilledBucket;
            ById[I.Bucket] = EmptyBucket;
            ById[I.FlintAndSteel] = FlintAndSteel;
            ById[I.BoneMeal] = BoneMeal;
            ById[B.Tnt] = (src, stack) =>
            {
                var (bx, by, bz) = src.Front;
                src.W.SpawnEntity(new TntEntity(src.W, bx + 0.5, by, bz + 0.5, 80));
                src.W.PlaySound("game.tnt.primed", bx + 0.5, by + 0.5, bz + 0.5, 1f, 1f);
                stack.Count--;
                Click(src); Smoke(src);
                return stack;
            };
            foreach (var it in Item.ById)
                if (it != null && it.ArmorSlot >= 0) ById[it.Id] = Armor;
        }

        private static ItemStack FilledBucket(DispenseSource src, ItemStack stack)
        {
            var (bx, by, bz) = src.Front;
            var at = Block.Get(src.W.GetBlock(bx, by, bz));
            if (at.Id != B.Air && at.Solid) return Default(src, stack);
            if (at.Id != B.Air && !at.IsLiquid) src.W.BreakBlock(bx, by, bz, true);
            src.W.SetBlock(bx, by, bz, stack.Id == I.WaterBucket ? B.Water : B.Lava, 0);
            Click(src); Smoke(src);
            return new ItemStack(I.Bucket, 1);
        }
        private static ItemStack EmptyBucket(DispenseSource src, ItemStack stack)
        {
            var (bx, by, bz) = src.Front;
            int id = src.W.GetBlock(bx, by, bz);
            if (id != B.Water && id != B.Lava || src.W.GetMeta(bx, by, bz) != 0) return Default(src, stack);
            src.W.SetBlock(bx, by, bz, B.Air);
            int full = id == B.Water ? I.WaterBucket : I.LavaBucket;
            Click(src); Smoke(src);
            if (--stack.Count <= 0) return new ItemStack(full, 1);
            if (HopperLogic.AddToFreeSlot(src.Te, new ItemStack(full, 1)) < 0) Default(src, new ItemStack(full, 1));
            return stack;
        }
        private static ItemStack FlintAndSteel(DispenseSource src, ItemStack stack)
        {
            var (bx, by, bz) = src.Front;
            int id = src.W.GetBlock(bx, by, bz);
            bool ok = true;
            if (id == B.Air && Block.ById[B.Fire]?.Id == B.Fire)
            {
                src.W.SetBlock(bx, by, bz, B.Fire, 0);
                if (stack.DamageItem(1)) stack.Count = 0;
            }
            else if (id == B.Tnt) TntBlock.Ignite(src.W, bx, by, bz, 80);
            else ok = false;
            if (ok) Click(src); else Fail(src);
            Smoke(src);
            return stack;
        }
        private static ItemStack BoneMeal(DispenseSource src, ItemStack stack)
        {
            var (bx, by, bz) = src.Front;
            if (ApplyBonemeal(src.W, bx, by, bz))
            {
                stack.Count--;
                src.W.SpawnParticles(ParticleKind.Heart, bx + 0.5, by + 0.5, bz + 0.5, 3);
                Click(src);
            }
            else Fail(src);
            Smoke(src);
            return stack;
        }
        /// <summary>Mączka kostna jak w fundamencie (PlayerController.BoneMeal): pszenica, sadzonki, trawa.</summary>
        private static bool DefaultBonemeal(World w, int x, int y, int z)
        {
            int id = w.GetBlock(x, y, z);
            if (id == B.Wheat && w.GetMeta(x, y, z) < 7) { w.SetMeta(x, y, z, Math.Min(7, w.GetMeta(x, y, z) + 2 + w.Rand.Next(4))); return true; }
            if (id == B.OakSapling || id == B.BirchSapling) { if (w.Rand.NextFloat() < 0.45f) ((SaplingBlock)Block.Get(id)).Grow(w, x, y, z, w.Rand); return true; }
            if (id == B.Grass)
            {
                for (int i = 0; i < 64; i++)
                {
                    int tx = x + w.Rand.Next(7) - 3, tz = z + w.Rand.Next(7) - 3, ty = y + w.Rand.Next(3) - 1;
                    if (w.GetBlock(tx, ty, tz) == B.Grass && w.GetBlock(tx, ty + 1, tz) == B.Air)
                        w.SetBlock(tx, ty + 1, tz, w.Rand.Next(10) == 0 ? (w.Rand.NextBool() ? B.Dandelion : B.Poppy) : B.TallGrass);
                }
                return true;
            }
            return false;
        }
        /// <summary>MC ItemArmor.dispenserBehavior: zakłada zbroję graczowi przed dozownikiem.</summary>
        private static ItemStack Armor(DispenseSource src, ItemStack stack)
        {
            var (bx, by, bz) = src.Front;
            int slot = stack.Item.ArmorSlot;
            var target = RsUtil.EntitiesIn(src.W, new AABB(bx, by, bz, bx + 1, by + 1, bz + 1))
                .OfType<Player>().FirstOrDefault(p => !p.Dead && p.Inventory.Armor[slot] == null);
            if (target == null) return Default(src, stack);
            var one = stack.Copy(); one.Count = 1;
            target.Inventory.Armor[slot] = one; target.Inventory.Dirty = true;
            stack.Count--;
            Click(src); Smoke(src);
            return stack;
        }
    }

    /// <summary>Dozownik (BlockDispenser) i podajnik (BlockDropper) 1.8. meta: bity 0-2 kierunek, bit 3 = wyzwolony.</summary>
    public sealed class DispenserBlock : Block
    {
        public readonly bool Dropper;
        private readonly int _front, _frontV, _side, _top;
        public DispenserBlock(byte id, string k, string n, bool dropper) : base(id, k, n)
        {
            Dropper = dropper;
            string f = dropper ? "dropper" : "dispenser";
            _front = TextureGen.Layer(f + "_front_horizontal"); _frontV = TextureGen.Layer(f + "_front_vertical");
            _side = TextureGen.Layer("furnace_side"); _top = TextureGen.Layer("furnace_top");
            Tex = new[] { _top, _top, _front, _side, _side, _side };
            Hardness = 3.5f; BlastResistance = 3.5f; Tool = ToolType.Pickaxe; HarvestLevel = 0; HasTileEntity = true;
        }
        public static int Facing(int meta) => meta & 7;
        public override int GetTexture(int face, int meta)
        {
            int f = Facing(meta);
            if (face == f) return f <= 1 ? _frontV : _front;
            if (f <= 1) return _top;
            return face <= 1 ? _top : _side;
        }
        public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
            => PistonBlock.FacingFromEntity(x, y, z, p);
        public override void OnAdded(World w, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is not DispenserTileEntity) w.SetTileEntity(x, y, z, new DispenserTileEntity(Dropper));
        }
        public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
        {
            if (w.IsRemote) return;
            int m = w.GetMeta(x, y, z);
            bool powered = Redstone.IsBlockPowered(w, x, y, z) || Redstone.IsBlockPowered(w, x, y + 1, z);
            bool triggered = (m & 8) != 0;
            if (powered && !triggered) { w.ScheduleTick(x, y, z, Id, 4); w.SetMeta(x, y, z, m | 8, false); }
            else if (!powered && triggered) w.SetMeta(x, y, z, m & ~8, false);
        }
        public override void OnScheduledTick(World w, int x, int y, int z, Rng r) { if (!w.IsRemote) Dispense(w, x, y, z); }
        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            if (w.GetTileEntity(x, y, z) is DispenserTileEntity te) RsGui.Open(p, new RsContainer(p.Inventory, te, Dropper ? "Podajnik" : "Dozownik", RsContainer.Layout.Grid3));
            return true;
        }
        public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
        {
            if (w.GetTileEntity(x, y, z) is DispenserTileEntity te)
            {
                if (!w.IsRemote) foreach (var s in te.Slots) if (s != null) w.DropItem(x + 0.5, y + 0.5, z + 0.5, s);
                w.RemoveTileEntity(x, y, z);
            }
            if (!w.IsRemote) Redstone.UpdateComparatorOutputLevel(w, x, y, z, Id);
        }
        /// <summary>MC BlockDispenser.dispense / BlockDropper.dispense.</summary>
        public void Dispense(World w, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is not DispenserTileEntity te) return;
            var src = new DispenseSource { W = w, X = x, Y = y, Z = z, Facing = Facing(w.GetMeta(x, y, z)), Te = te };
            int i = te.DispenseSlot(w.Rand);
            if (i < 0) { DispenserBehaviors.Fail(src); return; }
            var stack = te.Slots[i];
            if (Dropper)
            {
                var (fx, fy, fz) = src.Front;
                var inv = HopperLogic.InventoryAt(w, fx, fy, fz);
                ItemStack left;
                if (inv == null) left = DispenserBehaviors.Default(src, stack);
                else
                {
                    var one = stack.Copy(); one.Count = 1;
                    var rest = HopperLogic.PutStack(inv, one, Dir.Opp[src.Facing]);
                    left = stack.Copy();
                    if (rest == null) { left.Count--; inv.Changed(); }
                }
                te.Slots[i] = left == null || left.Count <= 0 ? null : left;
            }
            else
            {
                var left = DispenserBehaviors.Get(stack)(src, stack);
                te.Slots[i] = left == null || left.Count <= 0 ? null : left;
            }
            te.Changed();
        }
    }

    /// <summary>Tile entity dozownika i podajnika (MC TileEntityDispenser / TileEntityDropper): 9 slotów.</summary>
    public sealed class DispenserTileEntity : TileEntity, IInventory
    {
        public readonly bool Dropper;
        public readonly ItemStack[] Slots = new ItemStack[9];
        public DispenserTileEntity(bool dropper) { Dropper = dropper; }
        public override int TypeId => Dropper ? 15 : 14;
        public int Size => 9;
        public ItemStack Get(int i) => Slots[i];
        public void Set(int i, ItemStack s) { Slots[i] = s != null && s.Count <= 0 ? null : s; }
        public void Changed() { }
        /// <summary>MC getDispenseSlot: losowy niepusty slot (losowanie zbiornikowe).</summary>
        public int DispenseSlot(Rng r)
        {
            int i = -1, j = 1;
            for (int k = 0; k < Slots.Length; k++)
                if (Slots[k] != null && r.Next(j++) == 0) i = k;
            return i;
        }
        public override void Save(BinaryWriter bw) { foreach (var s in Slots) bw.WriteStack(s); }
        public override void Load(BinaryReader br) { for (int i = 0; i < 9; i++) Slots[i] = br.ReadStack(); }
    }

    /// <summary>Blok nut jak BlockNote 1.8: 25 tonów, instrument zależny od bloku pod spodem, gra przez zdarzenie bloku.</summary>
    public sealed class NoteBlockBlock : Block, IBlockEventReceiver
    {
        public static readonly string[] Instruments = { "harp", "bd", "snare", "hat", "bassattack" };
        /// <summary>Instrument wymuszony dla bloku pod spodem (255 = według materiału). Moduły mogą ustawiać.</summary>
        public static readonly byte[] InstrumentOverride = Enumerable.Repeat((byte)255, 256).ToArray();
        static NoteBlockBlock()
        {
            // bloki, których dźwięk nie odpowiada materiałowi MC
            foreach (int id in new[] { B.Dirt, B.Clay, B.Farmland, B.Ice, B.RedstoneLamp, B.RedstoneLampOn, B.Wool, B.IronBlock, B.GoldBlock, B.DiamondBlock })
                InstrumentOverride[id] = 0;
            InstrumentOverride[B.Gravel] = 2; InstrumentOverride[B.Sand] = 2;
        }
        public NoteBlockBlock(byte id, string k, string n) : base(id, k, n)
        {
            T("noteblock"); Hardness = 0.8f; BlastResistance = 0.8f; Tool = ToolType.Axe; Sound = SoundType.Wood; HasTileEntity = true; Flammable = true;
        }
        /// <summary>MC: kamień -> bd, piasek -> snare, szkło -> hat, drewno -> bassattack, reszta -> harp.</summary>
        public static int InstrumentFor(int id)
        {
            if (InstrumentOverride[id] != 255) return InstrumentOverride[id];
            var b = Block.Get(id);
            if (b.IsLiquid || id == B.Air) return 0;
            return b.Sound switch { SoundType.Stone => 1, SoundType.Sand => 2, SoundType.Glass => 3, SoundType.Wood => 4, _ => 0 };
        }
        private static NoteTileEntity Te(World w, int x, int y, int z)
        {
            if (w.GetTileEntity(x, y, z) is not NoteTileEntity te) { te = new NoteTileEntity(); w.SetTileEntity(x, y, z, te); }
            return te;
        }
        public override void OnAdded(World w, int x, int y, int z) => Te(w, x, y, z);
        public override void OnRemoved(World w, int x, int y, int z, int oldMeta) => w.RemoveTileEntity(x, y, z);
        public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
        {
            if (w.IsRemote) return;
            bool p = Redstone.IsBlockPowered(w, x, y, z);
            var te = Te(w, x, y, z);
            if (te.PreviousPowered != p)
            {
                if (p) Trigger(w, x, y, z, te);
                te.PreviousPowered = p;
            }
        }
        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            if (w.IsRemote) return true;
            var te = Te(w, x, y, z);
            te.Note = (te.Note + 1) % 25;
            Trigger(w, x, y, z, te);
            return true;
        }
        public override void OnClicked(World w, int x, int y, int z, Player p) { if (!w.IsRemote) Trigger(w, x, y, z, Te(w, x, y, z)); }
        /// <summary>MC TileEntityNote.triggerNote.</summary>
        private void Trigger(World w, int x, int y, int z, NoteTileEntity te)
        {
            if (w.GetBlock(x, y + 1, z) != B.Air) return;
            w.AddBlockEvent(x, y, z, Id, InstrumentFor(w.GetBlock(x, y - 1, z)), te.Note);
        }
        public bool OnBlockEvent(World w, int x, int y, int z, int eventId, int param)
        {
            float pitch = (float)Math.Pow(2.0, (param - 12) / 12.0);
            w.PlaySound("note." + Instruments[Math.Clamp(eventId, 0, 4)], x + 0.5, y + 0.5, z + 0.5, 3f, pitch);
            w.SpawnParticles(ParticleKind.Note, x + 0.5, y + 1.2, z + 0.5, 1, param);
            return true;
        }
    }

    /// <summary>MC TileEntityNote: ton 0-24 i poprzedni stan zasilania.</summary>
    public sealed class NoteTileEntity : TileEntity
    {
        public int Note; public bool PreviousPowered;
        public override int TypeId => 16;
        public override void Save(BinaryWriter bw) { bw.Write((byte)Note); bw.Write(PreviousPowered); }
        public override void Load(BinaryReader br) { Note = br.ReadByte() % 25; PreviousPowered = br.ReadBoolean(); }
    }

    /// <summary>Skrzynia-pułapka (BlockChest typu TRAP): moc = liczba graczy z otwartą skrzynią, silna w dół.</summary>
    public sealed class TrappedChestBlock : ChestBlock
    {
        private readonly int _front;
        public TrappedChestBlock(byte id, string k, string n) : base(id, k, n)
        {
            _front = TextureGen.Layer("trapped_chest_front"); IsPowerSource = true;
        }
        public override int GetTexture(int face, int meta) => face == Dir.HorizToFace(meta & 3) && face > 1 ? _front : base.GetTexture(face, meta);
        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            if (w.GetTileEntity(x, y, z) is ChestTileEntity te)
                RsGui.Open(p, new RsContainer(p.Inventory, te, "Skrzynia-pułapka", RsContainer.Layout.Chest27, () => Viewers(w, x, y, z, te, 1), () => Viewers(w, x, y, z, te, -1)));
            return true;
        }
        private void Viewers(World w, int x, int y, int z, ChestTileEntity te, int d)
        {
            te.Viewers = Math.Max(0, te.Viewers + d);
            if (w.IsRemote || w.GetBlock(x, y, z) != Id) return;
            Redstone.NotifyNeighbors(w, x, y, z, Id);
            Redstone.NotifyNeighbors(w, x, y - 1, z, Id);
            w.PlaySound(d > 0 ? "random.chestopen" : "random.chestclosed", x + 0.5, y + 0.5, z + 0.5, 0.5f, w.Rand.NextFloat() * 0.1f + 0.9f);
        }
        private static int Users(IBlockAccess w, int x, int y, int z)
            => w is World ww && ww.GetTileEntity(x, y, z) is ChestTileEntity te ? Math.Clamp(te.Viewers, 0, 15) : 0;
        public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => Users(w, x, y, z);
        public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => side == Dir.Down ? Users(w, x, y, z) : 0;
    }

    /// <summary>Kontener GUI dla bloków RS: lej (5 slotów), dozownik/podajnik (3x3), skrzynia (27).</summary>
    public sealed class RsContainer : Container
    {
        public enum Layout { Hopper, Grid3, Chest27 }
        public readonly IInventory Inv;
        public readonly Layout Kind;
        private readonly Action _onClose;
        public RsContainer(PlayerInventory pinv, IInventory inv, string title, Layout layout, Action onOpen = null, Action onClose = null)
        {
            Inv = inv; Title = title; Kind = layout; _onClose = onClose;
            switch (layout)
            {
                case Layout.Hopper:
                    Height = 133;
                    for (int i = 0; i < 5; i++) Slots.Add(new Slot(inv, i, 44 + i * 18, 20));
                    AddPlayerSlots(pinv, 51, 109);
                    break;
                case Layout.Grid3:
                    for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) Slots.Add(new Slot(inv, r * 3 + c, 62 + c * 18, 17 + r * 18));
                    AddPlayerSlots(pinv, 84, 142);
                    break;
                default:
                    Height = 114 + 3 * 18;
                    for (int r = 0; r < 3; r++) for (int c = 0; c < 9; c++) Slots.Add(new Slot(inv, r * 9 + c, 8 + c * 18, 18 + r * 18));
                    AddPlayerSlots(pinv, 85, 143);
                    break;
            }
            onOpen?.Invoke();
        }
        protected override bool TransferFromPlayer(ItemStack s) => MergeStack(s, 0, Inv.Size, false);
        public override void OnClose(Player p) { base.OnClose(p); _onClose?.Invoke(); }
    }

    /// <summary>Otwieranie GUI kontenerów RS (bez własnego GuiKind w fundamencie).</summary>
    public static class RsGui
    {
        public static void Open(Player p, RsContainer c)
        {
            if (p.World.IsRemote || p.Handler is not Game g) return;
            p.CloseContainer();
            p.OpenContainer = c;
            g.SetScreen(new RsContainerScreen(p, c));
        }
    }
}

namespace BlockCraft.Gui
{
    /// <summary>Ekran kontenerów RS: panel, sloty, tytuł i napis "Ekwipunek".</summary>
    public sealed class RsContainerScreen : ContainerScreen
    {
        public RsContainerScreen(Player p, Container c) : base(p, c) { }
        protected override void DrawBackground(int mx, int my)
        {
            GuiDraw.Panel(G, Left, Top, C.Width, C.Height);
            foreach (var s in C.Slots) GuiDraw.Slot(G, Left + s.X - 1, Top + s.Y - 1, 18);
            var sb = G.Sb;
            if (C is World.RsContainer rc && rc.Kind != World.RsContainer.Layout.Chest27)
                sb.TextCentered(C.Title, Left + C.Width / 2f, Top + 6, 0xFF404040, false);
            else sb.Text(C.Title, Left + 8, Top + 6, 0xFF404040, false);
            sb.Text("Ekwipunek", Left + 8, Top + C.Height - 96 + 2, 0xFF404040, false);
        }
    }
}
