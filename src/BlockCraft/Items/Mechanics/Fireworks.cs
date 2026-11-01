using BlockCraft.Core;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Items
{
    /// <summary>RecipeFireworks i dane fajerwerków z 1.8 (tagi Explosion / Fireworks jak w NBT MC).</summary>
    public static class Fireworks
    {
        /// <summary>ItemDye.dyeColors (indeks = meta barwnika: 0 atrament ... 15 mączka kostna).</summary>
        public static readonly int[] DyeColors = { 1973019, 11743532, 3887386, 5320730, 2437522, 8073150, 2651799, 11250603, 4408131, 14188952, 4312372, 14602026, 6719955, 12801229, 15435844, 15790320 };
        public static int ColorOfDye(ItemStack s) { int c = Dyes.ColorOf(s); return c < 0 ? -1 : DyeColors[Dyes.DyeMetaFromColor(c)]; }

        public const int Small = 0, Large = 1, Star = 2, Creeper = 3, Burst = 4;

        /// <summary>RecipeFireworks.matches + getCraftingResult (rakieta, gwiazdka, zanikanie gwiazdki).</summary>
        public static ItemStack Recipe(ItemStack[] grid, int gw, int gh)
        {
            int paper = 0, gunpowder = 0, dyes = 0, charges = 0, extras = 0, shapes = 0;
            foreach (var s in grid)
            {
                if (s == null) continue;
                if (s.Id == I.Gunpowder) gunpowder++;
                else if (s.Id == I.FireworkStar) charges++;
                else if (Dyes.ColorOf(s) >= 0) dyes++;
                else if (s.Id == I.Paper) paper++;
                else if (s.Id == I.GlowstoneDust || s.Id == I.Diamond) extras++;
                else if (s.Id == I.FireCharge || s.Id == I.Feather || s.Id == I.GoldNugget || s.Id == I.SkullItem) shapes++;
                else return null;
            }
            int others = extras + dyes + shapes;
            if (gunpowder > 3 || paper > 1) return null;
            if (gunpowder >= 1 && paper == 1 && others == 0)
            {
                var r = new ItemStack(I.FireworkRocket);
                if (charges > 0)
                {
                    var fw = new Nbt(); var list = new List<object>();
                    foreach (var s in grid) if (s?.Id == I.FireworkStar && s.Tag?.GetCompound("Explosion") is { } e) list.Add(e.Copy());
                    fw["Explosions"] = list; fw["Flight"] = gunpowder;
                    r.GetOrCreateTag()["Fireworks"] = fw;
                }
                return r;
            }
            if (gunpowder == 1 && paper == 0 && charges == 0 && dyes > 0 && shapes <= 1)
            {
                var e = new Nbt(); var colors = new List<int>(); int type = Small;
                foreach (var s in grid)
                {
                    if (s == null) continue;
                    int dc = ColorOfDye(s);
                    if (dc >= 0) colors.Add(dc);
                    else if (s.Id == I.GlowstoneDust) e["Flicker"] = true;
                    else if (s.Id == I.Diamond) e["Trail"] = true;
                    else if (s.Id == I.FireCharge) type = Large;
                    else if (s.Id == I.Feather) type = Burst;
                    else if (s.Id == I.GoldNugget) type = Star;
                    else if (s.Id == I.SkullItem) type = Creeper;
                }
                e["Colors"] = colors.ToArray(); e["Type"] = type;
                var r = new ItemStack(I.FireworkStar);
                r.GetOrCreateTag()["Explosion"] = e;
                return r;
            }
            if (gunpowder == 0 && paper == 0 && charges == 1 && dyes > 0 && dyes == others)
            {
                ItemStack r = null; var fade = new List<int>();
                foreach (var s in grid)
                {
                    if (s == null) continue;
                    int dc = ColorOfDye(s);
                    if (dc >= 0) fade.Add(dc);
                    else if (s.Id == I.FireworkStar) r = s.CopyWith(1);
                }
                var e = r?.Tag?.GetCompound("Explosion");
                if (e == null) return null;
                e["FadeColors"] = fade.ToArray();
                return r;
            }
            return null;
        }

        /// <summary>ItemFireworkCharge.getColorFromItemStack: średnia kolorów gwiazdki, bez kolorów 0x8A8A8A.</summary>
        public static uint StarColor(ItemStack s)
        {
            var colors = s?.Tag?.GetCompound("Explosion")?["Colors"] as int[];
            if (colors == null || colors.Length == 0) return 0xFF8A8A8A;
            int r = 0, g = 0, b = 0;
            foreach (int c in colors) { r += c >> 16 & 255; g += c >> 8 & 255; b += c & 255; }
            r /= colors.Length; g /= colors.Length; b /= colors.Length;
            return 0xFF000000 | (uint)(b << 16 | g << 8 | r);
        }

        public static string TypeName(int t) => t switch { Large => "Duża kula", Star => "Gwiazda", Creeper => "Creeper", Burst => "Wybuch", _ => "Mała kula" };
    }

    public static partial class ItemRegistry
    {
        internal static void ItemsFireworks()
        {
            var rocket = Simple(I.FireworkRocket, "fireworks", "Fajerwerk", Blocks.TabMisc);
            rocket.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) =>
            {
                // ItemFirework.onItemUse: rakieta w punkcie kliknięcia
                if (!p.World.IsRemote) p.World.SpawnEntity(new Entities.FireworkRocketEntity(p.World, x + hx, y + hy, z + hz, s));
                if (!p.Creative && --s.Count <= 0) p.Inventory.Selected = null;
                p.Inventory.Dirty = true;
                return true;
            };
            var star = Simple(I.FireworkStar, "firework_charge", "Gwiazdka fajerwerku", Blocks.TabMisc);
            star.ColorFn = Fireworks.StarColor;
        }
    }

    public static partial class Recipes
    {
        internal static void RecipesFireworks()
        {
            Special.Add(Fireworks.Recipe);
            // BehaviorDispenseFirework: rakieta przed dozownikiem na wysokości y + 0.2, dźwięk 1002 (random.bow)
            DispenserBehaviors.ById[I.FireworkRocket] = (src, stack) =>
            {
                src.W.SpawnEntity(new Entities.FireworkRocketEntity(src.W, src.CX + Dir.X[src.Facing], src.Y + .2, src.CZ + Dir.Z[src.Facing], stack));
                stack.Count--;
                src.W.PlaySound("random.bow", src.CX, src.CY, src.CZ, 1f, 1.2f);
                DispenserBehaviors.Smoke(src);
                return stack;
            };
        }
    }
}

namespace BlockCraft.Entities
{
    using BlockCraft.Items;

    /// <summary>
    /// EntityFireworkRocket 1.8: czas lotu 10 * (1 + Flight) + rand(6) + rand(7), przyspieszenie poziome x1.15 i pionowe +0.04 na tick,
    /// dźwięk startu, iskry smugi. Po czasie lotu wybucha (efekt u każdego klienta, jak status 17) i znika.
    /// </summary>
    public sealed class FireworkRocketEntity : Entity
    {
        public ItemStack Stack; public int FlightAge, Lifetime;
        public bool Exploded; private bool _fxDone; private int _removeAt = -1;

        public FireworkRocketEntity(World.World w) : base(w) { Width = Height = .25f; }
        public FireworkRocketEntity(World.World w, double x, double y, double z, ItemStack stack) : this(w)
        {
            SetPositionAndPrev(x, y, z);
            Stack = stack?.CopyWith(1);
            int flight = 1 + (Stack?.Tag?.GetCompound("Fireworks")?.GetInt("Flight") ?? 0);
            Lifetime = 10 * flight + w.Rand.Next(6) + w.Rand.Next(7);
            MotionX = Gaussian(w.Rand) * .001; MotionZ = Gaussian(w.Rand) * .001; MotionY = .05;
        }
        private static double Gaussian(Rng r) { double u = 1 - r.NextDouble(), v = r.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

        public override string TypeKey => "fireworks_rocket";

        public override void Tick()
        {
            if (IsRemoteControlled)
            {
                base.Tick();
                FlightAge++;
                if (!Exploded) FireworkFx.Trail(World, X, Y, Z, MotionY);
                if ((Exploded || FlightAge > Lifetime + 2) && !_fxDone) Explode();
                return;
            }
            PrevX = X; PrevY = Y; PrevZ = Z; PrevYaw = Yaw; PrevPitch = Pitch; Age++;
            if (Exploded)
            {
                // host trzyma niewidoczną rakietę kilka ticków, żeby goście dostali flagę wybuchu
                if (Age >= _removeAt) Kill();
                return;
            }
            MotionX *= 1.15; MotionZ *= 1.15; MotionY += .04;
            MoveWithCollision(MotionX, MotionY, MotionZ);
            double h = Math.Sqrt(MotionX * MotionX + MotionZ * MotionZ);
            Yaw = (float)(Math.Atan2(MotionX, MotionZ) * 180 / Math.PI);
            Pitch = (float)(Math.Atan2(MotionY, h) * 180 / Math.PI);
            if (FlightAge == 0) World.PlaySound("fireworks.launch", X, Y, Z, 3f, 1f);
            FlightAge++;
            FireworkFx.Trail(World, X, Y, Z, MotionY);
            if (FlightAge > Lifetime && !World.IsRemote) { Explode(); Exploded = true; _removeAt = Age + 10; }
        }

        private void Explode()
        {
            _fxDone = true;
            FireworkFx.Explode(World, X, Y, Z, MotionX, MotionY, MotionZ, Stack?.Tag?.GetCompound("Fireworks"));
        }

        public override void Save(BinaryWriter bw) { base.Save(bw); bw.WriteStack(Stack); bw.Write(FlightAge); bw.Write(Lifetime); bw.Write(Exploded); }
        public override void Load(BinaryReader br) { base.Load(br); Stack = br.ReadStack(); FlightAge = br.ReadInt32(); Lifetime = br.ReadInt32(); Exploded = br.ReadBoolean(); }
        public override int NetData => Exploded ? 1 : 0;
        public override void SetNetData(int d) { if ((d & 1) != 0) Exploded = true; }
    }

    public static partial class Registries
    {
        internal static void EntitiesFireworks()
        {
            Entity("fireworks_rocket", w => new FireworkRocketEntity(w));
            EntityRenderer.Custom[typeof(FireworkRocketEntity)] = (r, e, pt, pos, cam) =>
            {
                var f = (FireworkRocketEntity)e;
                if (f.Exploded) return;
                r.RenderBillboardSprite(TextureGen.Layer("item_fireworks"), pos + new Vector3(0, .125f, 0), .5f, Vector4.One, cam);
            };
        }
    }
}
