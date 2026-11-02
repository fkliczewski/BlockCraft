using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Entities
{
    /// <summary>
    /// EntityBoat z 1.8: wypór liczony z 5 warstw zanurzenia, sterowanie kierunkiem patrzenia jeźdźca,
    /// przyspieszenie 0.07-0.35, rozbicie przy uderzeniu z prędkością ponad 0.2975 i przy upadku z ponad 3 bloków.
    /// </summary>
    public sealed class BoatEntity : Entity
    {
        public override string TypeKey => "boat";
        public override bool Persistent => true;
        public override bool CanBeCollidedWith => !Dead;
        public override bool CanBeAttacked => !Dead;
        public float DamageTaken; public int TimeSinceHit, ForwardDirection = 1;
        private double _speedMultiplier = .07;

        public BoatEntity(World.World w) : base(w) { Width = 1.5f; Height = .6f; StepHeight = 0; }

        /// <summary>Ułamek wysokości łódki zanurzony w wodzie (0-1), jak pętla 5 warstw w onUpdate.</summary>
        public double Submerged()
        {
            var bb = BoundingBox; double d = 0;
            for (int j = 0; j < 5; j++)
            {
                double y0 = bb.MinY + (bb.MaxY - bb.MinY) * j / 5 - .125, y1 = bb.MinY + (bb.MaxY - bb.MinY) * (j + 1) / 5 - .125;
                if (World.IsAnyLiquid(new AABB(bb.MinX, y0, bb.MinZ, bb.MaxX, y1, bb.MaxZ), false)) d += 1.0 / 5;
            }
            return d;
        }

        public override void Tick()
        {
            base.Tick();
            if (Removed || IsRemoteControlled) { UpdatePassenger(); return; }
            if (TimeSinceHit > 0) TimeSinceHit--;
            if (DamageTaken > 0) DamageTaken--;
            double d0 = Submerged();
            double before = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
            if (before > .2975)
            {
                double c = Math.Cos(Yaw * Math.PI / 180), s = Math.Sin(Yaw * Math.PI / 180);
                for (int k = 0; k < 1 + before * 60; k++)
                {
                    double f = World.Rand.NextDouble() * 2 - 1, g = (World.Rand.Next(2) * 2 - 1) * .7;
                    World.SpawnParticles(ParticleKind.Splash, X - c * f * .8 + s * g, Y - .125, Z - s * f * .8 - c * g, 1);
                }
            }
            if (d0 < 1) MotionY += .04 * (d0 * 2 - 1);
            else { if (MotionY < 0) MotionY /= 2; MotionY += .007; }
            if (Passenger is LivingEntity rider)
            {
                float f = rider.Yaw + -rider.MoveStrafe * 90f;
                MotionX += -Math.Sin(f * Math.PI / 180) * _speedMultiplier * rider.MoveForward * .05;
                MotionZ += Math.Cos(f * Math.PI / 180) * _speedMultiplier * rider.MoveForward * .05;
            }
            double speed = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
            if (speed > .35) { double k = .35 / speed; MotionX *= k; MotionZ *= k; speed = .35; }
            if (speed > before && _speedMultiplier < .35) _speedMultiplier = Math.Min(.35, _speedMultiplier + (.35 - _speedMultiplier) / 35);
            else _speedMultiplier = Math.Max(.07, _speedMultiplier - (_speedMultiplier - .07) / 35);
            // śnieg i lilie wodne pod rogami łódki
            for (int i = 0; i < 4; i++)
            {
                int bx = MathUtil.FloorToInt(X + (i % 2 - .5) * .8), bz = MathUtil.FloorToInt(Z + (i / 2 - .5) * .8);
                for (int j = 0; j < 2; j++)
                {
                    int by = MathUtil.FloorToInt(Y) + j, id = World.GetBlock(bx, by, bz);
                    if (id == B.SnowLayer) { World.SetBlock(bx, by, bz, B.Air); CollidedH = false; }
                    else if (id == B.LilyPad) { World.BreakBlock(bx, by, bz, true); CollidedH = false; }
                }
            }
            if (OnGround) { MotionX *= .5; MotionY *= .5; MotionZ *= .5; }
            double fallBefore = FallDistance; double yBefore = Y;
            MoveWithCollision(MotionX, MotionY, MotionZ);
            if (CollidedH && before > .2975) { BreakApart(); return; }
            MotionX *= .99; MotionY *= .95; MotionZ *= .99;
            // updateFallState: rozbicie przy upadku na ląd
            if (OnGround)
            {
                if (fallBefore > 3) { BreakApart(); return; }
                FallDistance = 0;
            }
            else if (Y < yBefore && !World.IsAnyLiquid(BoundingBox.Offset(0, -.1, 0), false)) FallDistance = (float)(fallBefore + (yBefore - Y));
            Pitch = 0;
            double yaw = Yaw, dx = PrevX - X, dz = PrevZ - Z;
            if (dx * dx + dz * dz > .001) yaw = Math.Atan2(dz, dx) * 180 / Math.PI;
            double turn = Math.Clamp(MathUtil.WrapDegrees((float)(yaw - Yaw)), -20, 20);
            Yaw += (float)turn;
            if (Passenger is { Removed: true } or { Dead: true }) Passenger.Mount(null);
            UpdatePassenger();
        }

        private void BreakApart()
        {
            if (World.IsRemote || Dead) return;
            Kill();
            if (!World.Rules.DoEntityDrops) return;
            for (int i = 0; i < 3; i++) World.DropItem(X, Y, Z, new ItemStack(B.OakPlanks));
            for (int i = 0; i < 2; i++) World.DropItem(X, Y, Z, new ItemStack(I.Stick));
        }

        /// <summary>updateRiderPosition: jeździec 0.4 bloku do przodu, nisko w łódce.</summary>
        public override void UpdatePassenger()
        {
            if (Passenger == null) return;
            if (Passenger.Dead || Passenger.Removed) { Passenger.Mount(null); return; }
            double r = Yaw * Math.PI / 180;
            Passenger.SetPosition(X + Math.Cos(r) * .4, Y - .1, Z + Math.Sin(r) * .4);
            Passenger.FallDistance = 0;
        }

        public override bool Interact(Player p)
        {
            if (Passenger is Player && Passenger != p) return true;
            if (!World.IsRemote) p.Mount(this);
            return true;
        }

        public override bool Attack(DamageSource source, float amount)
        {
            if (World.IsRemote || Dead) return true;
            if (Passenger != null && Passenger == source.Attacker && source.Type == "arrow") return false;
            ForwardDirection = -ForwardDirection; TimeSinceHit = 10; DamageTaken += amount * 10;
            bool creative = source.Attacker is Player { Creative: true };
            if (creative || DamageTaken > 40)
            {
                Passenger?.Mount(null);
                if (!creative && World.Rules.DoEntityDrops) World.DropItem(X, Y, Z, new ItemStack(I.Boat));
                Kill();
            }
            return true;
        }

        public override void Save(BinaryWriter bw) { base.Save(bw); bw.Write(DamageTaken); }
        public override void Load(BinaryReader br) { base.Load(br); DamageTaken = br.ReadSingle(); }
        public override int NetData => TimeSinceHit | ((int)DamageTaken & 0xFFF) << 8 | (ForwardDirection > 0 ? 1 << 20 : 0);
        public override void SetNetData(int d) { TimeSinceHit = d & 0xFF; DamageTaken = d >> 8 & 0xFFF; ForwardDirection = (d >> 20 & 1) != 0 ? 1 : -1; }

        /// <summary>ItemBoat.onItemRightClick: celowanie z płynami, łódka na wierzchu trafionego bloku.</summary>
        public static bool Place(Player p, ItemStack held)
        {
            var w = p.World;
            var (lx, ly, lz) = p.Look();
            var h = w.RayCast(p.X, p.Y + p.EyeHeight, p.Z, lx, ly, lz, 5, true);
            if (!h.Hit) return false;
            int by = h.Y;
            if (w.GetBlock(h.X, by, h.Z) == B.SnowLayer) by--;
            var boat = new BoatEntity(w);
            boat.SetPositionAndPrev(h.X + .5, by + 1, h.Z + .5);
            boat.Yaw = boat.PrevYaw = ((Dir.HorizFromYaw(p.Yaw) - 1) * 90);
            if (w.GetCollisionBoxes(boat.BoundingBox.Grow(-.1, -.1, -.1), new List<AABB>()).Count > 0) return false;
            if (!w.IsRemote) w.SpawnEntity(boat);
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            return true;
        }
    }

    public static partial class Registries
    {
        internal static void EntitiesBoat()
        {
            Entity("boat", w => new BoatEntity(w));
            EntityRenderer.Custom[typeof(BoatEntity)] = RenderBoat;
        }

        /// <summary>ModelBoat 1.8: dno 24x16 i cztery burty, kołysanie po uderzeniu.</summary>
        private static void RenderBoat(EntityRenderer r, Entity e, float pt, Vector3 pos, Vector3 cam)
        {
            var b = (BoatEntity)e;
            float yaw = b.PrevYaw + MathUtil.WrapDegrees(b.Yaw - b.PrevYaw) * pt;
            float hit = b.TimeSinceHit - pt, dmg = Math.Max(0, b.DamageTaken - pt);
            float wobble = hit > 0 ? MathF.Sin(hit) * hit * dmg / 10f * b.ForwardDirection * MathUtil.Deg2Rad : 0;
            var m = Matrix4.CreateRotationX(wobble) * Matrix4.CreateRotationY((-yaw + 90) * MathUtil.Deg2Rad) * Matrix4.CreateTranslation(pos);
            var tex = ModelBox.All(TextureGen.Layer("planks_oak"));
            r.EmitModelBox(new ModelBox(-.5f, 0, -.75f, .5f, .1875f, .75f, tex), m);
            r.EmitModelBox(new ModelBox(-.5f, .1875f, -.75f, .5f, .5625f, -.625f, tex), m);
            r.EmitModelBox(new ModelBox(-.5f, .1875f, .625f, .5f, .5625f, .75f, tex), m);
            r.EmitModelBox(new ModelBox(-.5f, .1875f, -.625f, -.375f, .5625f, .625f, tex), m);
            r.EmitModelBox(new ModelBox(.375f, .1875f, -.625f, .5f, .5625f, .625f, tex), m);
        }
    }
}

namespace BlockCraft.Items
{
    public static partial class ItemRegistry
    {
        internal static void ItemsBoat()
        {
            var boat = Simple(I.Boat, "boat", "Łódka", Blocks.TabTransport, 1);
            boat.UseInAir = Entities.BoatEntity.Place;
            boat.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => Entities.BoatEntity.Place(p, s);
        }
    }

    public static partial class Recipes
    {
        internal static void RecipesBoat()
        {
            foreach (int planks in new[] { B.OakPlanks, B.SprucePlanks, B.BirchPlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks })
                Shaped(new ItemStack(I.Boat), "# #", "###", '#', planks);
        }
    }
}
