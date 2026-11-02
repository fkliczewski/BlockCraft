using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Entities
{
    /// <summary>
    /// EntityHanging z 1.8: encja przyczepiona do ściany. HangX/Y/Z to blok przed ścianą (pos.offset(side) z ItemHangingEntity),
    /// Facing to kierunek poziomy od ściany (0 S, 1 W, 2 N, 3 E). Co 100 ticków sprawdza podłoże (onValidSurface) i odpada.
    /// </summary>
    public abstract class HangingEntity : Entity
    {
        public int HangX, HangY, HangZ, Facing;
        private int _surfaceTimer;
        private AABB _box;

        protected HangingEntity(World.World w) : base(w) { Width = .5f; Height = .5f; StepHeight = 0; }

        public abstract int WidthPixels { get; }
        public abstract int HeightPixels { get; }
        public override bool Persistent => true;
        public override bool CanBeAttacked => !Dead;
        public override AABB BoundingBox => _box;
        public int FacingFace => Dir.HorizToFace(Facing);

        private static double Offset(int pixels) => pixels % 32 == 0 ? .5 : 0;
        /// <summary>rotateYCCW: kierunek "w prawo" dla patrzącego na ścianę.</summary>
        public static int Ccw(int facing) => (facing + 3) & 3;

        /// <summary>updateFacingWithBoundingBox + updateBoundingBox z 1.8.</summary>
        public void SetFacing(int facing)
        {
            Facing = facing & 3;
            Yaw = PrevYaw = Facing * 90;
            UpdateBox();
        }

        protected void UpdateBox()
        {
            double x = HangX + .5 - Dir.HorizX[Facing] * .46875, z = HangZ + .5 - Dir.HorizZ[Facing] * .46875, y = HangY + .5 + Offset(HeightPixels);
            int c = Ccw(Facing);
            x += Offset(WidthPixels) * Dir.HorizX[c]; z += Offset(WidthPixels) * Dir.HorizZ[c];
            X = PrevX = x; Y = PrevY = y; Z = PrevZ = z;
            bool alongX = Facing is 0 or 2;
            double hx = (alongX ? WidthPixels : 1) / 32.0, hy = HeightPixels / 32.0, hz = (alongX ? 1 : WidthPixels) / 32.0;
            // jak w MC kolizje liczone na odrobinę mniejszym pudełku (sąsiednie obrazy się stykają)
            _box = new AABB(x - hx, y - hy, z - hz, x + hx, y + hy, z + hz);
        }

        /// <summary>Bloki ściany za encją (MC: offset (i-1)/-2, żeby obejmował całą szerokość).</summary>
        public IEnumerable<(int x, int y, int z)> SupportBlocks()
        {
            int w = Math.Max(1, WidthPixels / 16), h = Math.Max(1, HeightPixels / 16);
            int bx = HangX - Dir.HorizX[Facing], bz = HangZ - Dir.HorizZ[Facing], c = Ccw(Facing);
            int k0 = (w - 1) / -2, l0 = (h - 1) / -2;
            for (int k = 0; k < w; k++)
                for (int l = 0; l < h; l++)
                    yield return (bx + Dir.HorizX[c] * (k + k0), HangY + l + l0, bz + Dir.HorizZ[c] * (k + k0));
        }

        private static bool SolidMaterial(int id)
        {
            if (id == B.Repeater || id == B.RepeaterOn) return true; // BlockRedstoneDiode.isRedstoneRepeaterBlockID
            var b = Block.Get(id);
            return id != B.Air && !b.IsLiquid && b.Solid && id != B.SnowLayer && id != B.Carpet;
        }

        /// <summary>onValidSurface 1.8: brak kolizji z blokami, pełna ściana z litego materiału, żadnej innej encji wiszącej.</summary>
        public bool OnValidSurface()
        {
            var shrunk = _box.Grow(-.001);
            if (World.GetCollisionBoxes(shrunk, new List<AABB>()).Count > 0) return false;
            foreach (var (x, y, z) in SupportBlocks()) if (!SolidMaterial(World.GetBlock(x, y, z))) return false;
            foreach (var e in World.GetEntitiesInBox(shrunk))
                if (e != this && e is HangingEntity && !e.Removed) return false;
            return true;
        }

        public override void Tick()
        {
            PrevX = X; PrevY = Y; PrevZ = Z;
            Age++;
            if (IsRemoteControlled || World.IsRemote) { X = NetX; Y = NetY; Z = NetZ; UpdateBox(); return; }
            if (_surfaceTimer++ == 100 && !Dead)
            {
                _surfaceTimer = 0;
                if (!OnValidSurface()) { Kill(); OnBroken(null); }
            }
        }

        public override bool Attack(DamageSource source, float amount)
        {
            if (World.IsRemote || Dead) return true;
            Kill();
            OnBroken(source.Attacker);
            return true;
        }

        protected abstract void OnBroken(Entity breaker);

        protected bool DropsFor(Entity breaker) => World.Rules.DoEntityDrops && breaker is not Player { Creative: true };

        public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(HangX); bw.Write(HangY); bw.Write(HangZ); bw.Write((byte)Facing); }
        public override void Load(BinaryReader br)
        {
            base.Load(br);
            HangX = br.ReadInt32(); HangY = br.ReadInt32(); HangZ = br.ReadInt32(); Facing = br.ReadByte() & 3;
            NetX = X; NetY = Y; NetZ = Z;
            UpdateBox(); NetX = X; NetY = Y; NetZ = Z;
        }

        /// <summary>ItemHangingEntity.onItemUse: tylko ściany boczne, encja w bloku przed ścianą, spawn gdy podłoże poprawne.</summary>
        public static bool PlaceFromItem(Player p, ItemStack held, int x, int y, int z, int face, Func<World.World, int, int, int, int, HangingEntity> create)
        {
            if (face == Dir.Down || face == Dir.Up) return false;
            var w = p.World;
            var e = create(w, x + Dir.X[face], y, z + Dir.Z[face], Dir.FaceToHoriz(face));
            if (e != null && e.OnValidSurface())
            {
                if (!w.IsRemote) w.SpawnEntity(e);
                if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
                p.Inventory.Dirty = true;
            }
            return true;
        }
    }

    /// <summary>EnumArt z 1.8: nazwa, rozmiar i położenie w arkuszu paintings_kristoffer_zetterstrand (256x256).</summary>
    public readonly record struct PaintingArt(string Name, int W, int H, int U, int V)
    {
        public static readonly PaintingArt[] All =
        {
            new("Kebab", 16, 16, 0, 0), new("Aztec", 16, 16, 16, 0), new("Alban", 16, 16, 32, 0), new("Aztec2", 16, 16, 48, 0),
            new("Bomb", 16, 16, 64, 0), new("Plant", 16, 16, 80, 0), new("Wasteland", 16, 16, 96, 0),
            new("Pool", 32, 16, 0, 32), new("Courbet", 32, 16, 32, 32), new("Sea", 32, 16, 64, 32), new("Sunset", 32, 16, 96, 32), new("Creebet", 32, 16, 128, 32),
            new("Wanderer", 16, 32, 0, 64), new("Graham", 16, 32, 16, 64),
            new("Match", 32, 32, 0, 128), new("Bust", 32, 32, 32, 128), new("Stage", 32, 32, 64, 128), new("Void", 32, 32, 96, 128),
            new("SkullAndRoses", 32, 32, 128, 128), new("Wither", 32, 32, 160, 128),
            new("Fighters", 64, 32, 0, 96),
            new("Pointer", 64, 64, 0, 192), new("Pigscene", 64, 64, 64, 192), new("BurningSkull", 64, 64, 128, 192),
            new("Skeleton", 64, 48, 192, 64), new("DonkeyKong", 64, 48, 192, 112),
        };
        public static int IndexOf(string name) { for (int i = 0; i < All.Length; i++) if (All[i].Name == name) return i; return 0; }
    }

    /// <summary>EntityPainting 1.8: losowy motyw spośród mieszczących się na ścianie, wypada jako przedmiot.</summary>
    public sealed class PaintingEntity : HangingEntity
    {
        public int Art;
        public PaintingEntity(World.World w) : base(w) { }
        public override string TypeKey => "painting";
        public override int WidthPixels => PaintingArt.All[Art].W;
        public override int HeightPixels => PaintingArt.All[Art].H;

        /// <summary>Konstruktor EntityPainting(world, pos, facing): sprawdza każdy motyw i losuje z pasujących.</summary>
        public static PaintingEntity Create(World.World w, int x, int y, int z, int facing)
        {
            var p = new PaintingEntity(w) { HangX = x, HangY = y, HangZ = z };
            var fit = new List<int>();
            for (int i = 0; i < PaintingArt.All.Length; i++)
            {
                p.Art = i; p.SetFacing(facing);
                if (p.OnValidSurface()) fit.Add(i);
            }
            p.Art = fit.Count > 0 ? fit[w.Rand.Next(fit.Count)] : 0;
            p.SetFacing(facing);
            return p;
        }

        protected override void OnBroken(Entity breaker)
        {
            if (DropsFor(breaker)) World.DropItem(X, Y, Z, new ItemStack(I.Painting));
        }

        public override void Save(BinaryWriter bw) { bw.Write(PaintingArt.All[Art].Name); base.Save(bw); }
        public override void Load(BinaryReader br) { Art = PaintingArt.IndexOf(br.ReadString()); base.Load(br); }
    }

    /// <summary>EntityItemFrame 1.8: przedmiot, obrót 0-7 (PPM), uderzenie zdejmuje przedmiot, komparator = obrót + 1.</summary>
    public sealed class ItemFrameEntity : HangingEntity, IComparatorFrame
    {
        public ItemStack Item; public int Rotation;
        public float DropChance = 1f;
        public ItemFrameEntity(World.World w) : base(w) { }
        public override string TypeKey => "item_frame";
        public override int WidthPixels => 12;
        public override int HeightPixels => 12;
        public int ComparatorOutput => Item == null ? 0 : Rotation % 8 + 1;

        public static ItemFrameEntity Create(World.World w, int x, int y, int z, int facing)
        {
            var f = new ItemFrameEntity(w) { HangX = x, HangY = y, HangZ = z };
            f.SetFacing(facing);
            return f;
        }

        /// <summary>EntityTrackerEntry co 10 ticków: znacznik ramki na mapie, którą trzyma.</summary>
        public override void Tick()
        {
            base.Tick();
            if (World.IsRemote || Removed || Age % 10 != 0 || Item?.Id != I.FilledMap) return;
            var m = MapLogic.DataFor(World, Item);
            if (m != null) MapLogic.UpdateFrame(m, this);
        }

        /// <summary>removeFrameFromMap.</summary>
        private void RemoveFromMap()
        {
            if (Item?.Id == I.FilledMap) MapStore.Of(World).Get(Item.Damage)?.RemoveDecoration("frame-" + Id);
        }

        private void Changed()
        {
            if (!World.IsRemote) Redstone.UpdateComparatorOutputLevel(World, HangX, HangY, HangZ, B.Air);
        }

        public override bool Interact(Player p)
        {
            if (World.IsRemote) return true;
            if (Item == null)
            {
                var held = p.Inventory.Selected;
                if (held != null)
                {
                    Item = held.CopyWith(1);
                    if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
                    p.Inventory.Dirty = true;
                    Changed();
                }
            }
            else { Rotation = (Rotation + 1) & 7; Changed(); }
            return true;
        }

        public override bool Attack(DamageSource source, float amount)
        {
            if (World.IsRemote || Dead) return true;
            if (!source.IsExplosion && Item != null)
            {
                DropItemOrSelf(source.Attacker, false);
                RemoveFromMap();
                Item = null; Rotation = 0;
                Changed();
                return true;
            }
            return base.Attack(source, amount);
        }

        protected override void OnBroken(Entity breaker) { DropItemOrSelf(breaker, true); RemoveFromMap(); Changed(); }

        /// <summary>dropItemOrSelf: twórca nic nie dostaje, inaczej ramka (gdy niszczona) i przedmiot z szansą itemDropChance.</summary>
        private void DropItemOrSelf(Entity breaker, bool withFrame)
        {
            if (!World.Rules.DoEntityDrops || breaker is Player { Creative: true }) return;
            if (withFrame) World.DropItem(X, Y, Z, new ItemStack(I.ItemFrame));
            if (Item != null && World.Rand.NextFloat() < DropChance) World.DropItem(X, Y, Z, Item.Copy());
        }

        public override void Save(BinaryWriter bw) { base.Save(bw); bw.WriteStack(Item); bw.Write((byte)Rotation); bw.Write(DropChance); }
        public override void Load(BinaryReader br) { base.Load(br); Item = br.ReadStack(); Rotation = br.ReadByte() & 7; DropChance = br.ReadSingle(); }
        public override int NetData => Rotation | (Item == null ? 0 : 8) | (Item?.Id ?? 0) << 4;
    }

    /// <summary>
    /// EntityArmorStand 1.8 (tryb survival): zbroja i przedmiot w ręce (gdy ShowArms), poza z losowym obrotem głowy i tułowia,
    /// pierwszy cios tylko chwieje, drugi w ciągu 5 ticków niszczy (twórca od razu), wybuch rozsypuje zawartość, ogień niszczy powoli.
    /// </summary>
    public sealed class ArmorStandEntity : Entity
    {
        public readonly ItemStack[] Armor = new ItemStack[4]; // 0 hełm, 1 napierśnik, 2 spodnie, 3 buty (jak ArmorSlot)
        public ItemStack Held;
        public bool ShowArms, NoBasePlate, Small;
        public Vector3 HeadPose, BodyPose, LeftArmPose = new(-10, 0, -10), RightArmPose = new(-15, 0, 10), LeftLegPose = new(-1, 0, -1), RightLegPose = new(1, 0, 1);
        public long PunchTime = -100;
        public float Health = 20;

        public ArmorStandEntity(World.World w) : base(w) { Width = .5f; Height = 1.975f; }
        public override string TypeKey => "armor_stand";
        public override bool Persistent => true;
        public override bool CanBeAttacked => !Dead;

        public override void Tick()
        {
            base.Tick();
            if (Removed || IsRemoteControlled) return;
            MotionY -= .08;
            MoveWithCollision(MotionX, MotionY, MotionZ);
            MotionY *= .98;
            double fr = OnGround ? Block.Get(World.GetBlock(MathUtil.FloorToInt(X), MathUtil.FloorToInt(Y - 1), MathUtil.FloorToInt(Z))).Slipperiness * .91 : .91;
            MotionX *= fr; MotionZ *= fr;
            if (OnGround) FallDistance = 0;
        }

        /// <summary>Slot trafiony promieniem patrzenia gracza (interactAt 1.8, wysokość względem stóp stojaka).</summary>
        private int ClickedSlot(Player p)
        {
            var (lx, ly, lz) = p.Look();
            double ex = p.X, ey = p.Y + p.EyeHeight, ez = p.Z;
            double hy = BoundingBox.Grow(.1).RayIntersect(ex, ey, ez, lx, ly, lz, out double t, out _) ? ey + ly * t - Y : 1;
            double d4 = Small ? hy * 2 : hy;
            if (d4 >= .1 && d4 < .1 + (Small ? .8 : .45) && Armor[3] != null) return 1;
            if (d4 >= .9 + (Small ? .3 : 0) && d4 < .9 + (Small ? 1 : .7) && Armor[1] != null) return 3;
            if (d4 >= .4 && d4 < .4 + (Small ? 1 : .8) && Armor[2] != null) return 2;
            if (d4 >= 1.6 && Armor[0] != null) return 4;
            return 0;
        }

        /// <summary>Sloty MC: 0 ręka, 1 buty, 2 spodnie, 3 napierśnik, 4 hełm.</summary>
        private ItemStack GetSlot(int s) => s == 0 ? Held : Armor[4 - s];
        private void SetSlot(int s, ItemStack v) { if (s == 0) Held = v; else Armor[4 - s] = v; }

        public override bool Interact(Player p)
        {
            if (World.IsRemote || p.Spectator) return true;
            var held = p.Inventory.Selected;
            int b0 = 0;
            if (held?.Item is { ArmorSlot: >= 0 } armor) b0 = 4 - armor.ArmorSlot;
            if (held != null && (held.Id == I.SkullItem || held.Id == B.Pumpkin)) b0 = 4;
            int b1 = ClickedSlot(p);
            if (held != null && b0 == 0 && !ShowArms) return true;
            if (held != null) Swap(p, b0);
            else if (GetSlot(b1) != null) Swap(p, b1);
            return true;
        }

        /// <summary>func_175422_a: zamiana przedmiotu gracza ze slotem stojaka.</summary>
        private void Swap(Player p, int slot)
        {
            var cur = GetSlot(slot); var held = p.Inventory.Selected;
            if (p.Creative && cur == null && held != null) SetSlot(slot, held.CopyWith(1));
            else if (held != null && held.Count > 1) { if (cur == null) { SetSlot(slot, held.CopyWith(1)); held.Count--; } }
            else { SetSlot(slot, held); p.Inventory.Selected = cur; }
            p.Inventory.Dirty = true;
        }

        public override bool Attack(DamageSource source, float amount)
        {
            if (World.IsRemote || Dead) return false;
            if (source.Type == "outOfWorld") { Kill(); return false; }
            if (source.IsExplosion) { DropContents(); Kill(); return false; }
            if (source.Type == "inFire") { if (FireTicks <= 0) SetFire(5); else Damage(.15f); return false; }
            if (source.Type == "onFire" && Health > .5f) { Damage(4); return false; }
            bool arrow = source.Type == "arrow", player = source.Type == "player";
            if (!arrow && !player) return false;
            if (source.Source is ArrowEntity a) a.Kill();
            if (source.Attacker is Player { Creative: true }) { Particles(); Kill(); return false; }
            long now = World.TotalTime;
            if (now - PunchTime > 5 && !arrow) PunchTime = now;
            else { DropBlock(); Particles(); Kill(); }
            return false;
        }

        private void Damage(float amount)
        {
            Health -= amount;
            if (Health <= .5f) { DropContents(); Kill(); }
        }

        private void Particles() => World.SpawnParticles(ParticleKind.BlockBreak, X, Y + Height / 1.5, Z, 10, B.OakPlanks);

        private void DropBlock()
        {
            if (World.Rules.DoEntityDrops) World.DropItem(X, Y, Z, new ItemStack(I.ArmorStand));
            DropContents();
        }

        private void DropContents()
        {
            for (int s = 0; s < 5; s++)
            {
                var it = GetSlot(s);
                if (it != null && it.Count > 0 && World.Rules.DoEntityDrops) World.DropItem(X, Y + 1, Z, it);
                SetSlot(s, null);
            }
        }

        private static void W(BinaryWriter bw, Vector3 v) { bw.Write(v.X); bw.Write(v.Y); bw.Write(v.Z); }
        private static Vector3 R(BinaryReader br) => new(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        public override void Save(BinaryWriter bw)
        {
            base.Save(bw);
            for (int i = 0; i < 4; i++) bw.WriteStack(Armor[i]);
            bw.WriteStack(Held);
            bw.Write((byte)((ShowArms ? 1 : 0) | (NoBasePlate ? 2 : 0) | (Small ? 4 : 0)));
            W(bw, HeadPose); W(bw, BodyPose); W(bw, LeftArmPose); W(bw, RightArmPose); W(bw, LeftLegPose); W(bw, RightLegPose);
            bw.Write(PunchTime); bw.Write(Health);
        }
        public override void Load(BinaryReader br)
        {
            base.Load(br);
            for (int i = 0; i < 4; i++) Armor[i] = br.ReadStack();
            Held = br.ReadStack();
            int f = br.ReadByte(); ShowArms = (f & 1) != 0; NoBasePlate = (f & 2) != 0; Small = (f & 4) != 0;
            if (Small) { Width = .25f; Height = .9875f; }
            HeadPose = R(br); BodyPose = R(br); LeftArmPose = R(br); RightArmPose = R(br); LeftLegPose = R(br); RightLegPose = R(br);
            PunchTime = br.ReadInt64(); Health = br.ReadSingle();
        }
        public override int NetData => (int)(PunchTime & 0xFFFF) ^ Armor.Sum(a => a?.Id ?? 0) << 16 ^ (Held?.Id ?? 0) << 20;

        /// <summary>ItemArmorStand.onItemUse 1.8: 2 bloki wolne, obrót co 45 stopni od gracza, losowa poza głowy i tułowia.</summary>
        public static bool Place(Player p, ItemStack held, int x, int y, int z, int face)
        {
            if (face == Dir.Down) return false;
            var w = p.World;
            bool repl = Block.Get(w.GetBlock(x, y, z)).Replaceable;
            int bx = repl ? x : x + Dir.X[face], by = repl ? y : y + Dir.Y[face], bz = repl ? z : z + Dir.Z[face];
            bool free(int yy) { int id = w.GetBlock(bx, yy, bz); return id == B.Air || Block.Get(id).Replaceable; }
            if (!free(by) || !free(by + 1)) return false;
            var box = new AABB(bx, by, bz, bx + 1, by + 2, bz + 1);
            if (w.GetEntitiesInBox(box).Any(e => !e.Removed)) return false;
            if (!w.IsRemote)
            {
                w.SetBlock(bx, by, bz, B.Air); w.SetBlock(bx, by + 1, bz, B.Air);
                var s = new ArmorStandEntity(w);
                float yaw = MathF.Floor((MathUtil.WrapDegrees(p.Yaw - 180) + 22.5f) / 45f) * 45f;
                s.SetPositionAndPrev(bx + .5, by, bz + .5); s.Yaw = s.PrevYaw = yaw;
                // applyRandomRotations
                s.HeadPose = new Vector3(w.Rand.NextFloat() * 5, w.Rand.NextFloat() * 20 - 10, 0);
                s.BodyPose = new Vector3(0, w.Rand.NextFloat() * 10 - 5, 0);
                w.SpawnEntity(s);
            }
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            return true;
        }
    }

    public static partial class Registries
    {
        internal static void EntitiesHanging()
        {
            Entity("painting", w => new PaintingEntity(w));
            Entity("item_frame", w => new ItemFrameEntity(w));
            Entity("armor_stand", w => new ArmorStandEntity(w));
            EntityRenderer.Custom[typeof(PaintingEntity)] = HangingRender.Painting;
            EntityRenderer.Custom[typeof(ItemFrameEntity)] = HangingRender.ItemFrame;
            EntityRenderer.Custom[typeof(ArmorStandEntity)] = HangingRender.ArmorStand;
        }
    }
}

namespace BlockCraft.Items
{
    public static partial class ItemRegistry
    {
        internal static void ItemsHanging()
        {
            var painting = Simple(I.Painting, "painting", "Obraz", Blocks.TabDecoration);
            painting.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => Entities.HangingEntity.PlaceFromItem(p, s, x, y, z, face, Entities.PaintingEntity.Create);
            var frame = Simple(I.ItemFrame, "item_frame", "Ramka na przedmiot", Blocks.TabDecoration);
            frame.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => Entities.HangingEntity.PlaceFromItem(p, s, x, y, z, face, Entities.ItemFrameEntity.Create);
            var stand = Simple(I.ArmorStand, "armor_stand", "Stojak na zbroję", Blocks.TabDecoration, 16);
            stand.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => Entities.ArmorStandEntity.Place(p, s, x, y, z, face);
        }
    }

    public static partial class Recipes
    {
        internal static void RecipesHanging()
        {
            Shaped(new ItemStack(I.Painting), "###", "#X#", "###", '#', I.Stick, 'X', ((int)B.Wool, -1));
            Shaped(new ItemStack(I.ItemFrame), "###", "#X#", "###", '#', I.Stick, 'X', I.Leather);
            Shaped(new ItemStack(I.ArmorStand), "///", " / ", "/_/", '/', I.Stick, '_', ((int)B.StoneSlab, 0));
        }
    }
}
