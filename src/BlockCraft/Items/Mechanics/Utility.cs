using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;
using BlockCraft.Items;

namespace BlockCraft.Items
{
    /// <summary>
    /// Animowane ikony kompasu i zegara (TextureCompass 32 klatki, TextureClock 64 klatki z 1.8):
    /// liczone co tick z gracza lokalnego, z tym samym wygładzaniem igły i tarczy co w MC.
    /// </summary>
    public static class ItemAnimations
    {
        public const int CompassFrames = 32, ClockFrames = 64;
        public static int CompassFrame, ClockFrame;
        private static double _compassAngle, _compassDelta, _clockAngle, _clockDelta;
        private static readonly Random R = new();

        public static void Tick(World.World w, Player p)
        {
            double target = 0;
            if (w != null && p != null)
            {
                double dx = w.SpawnX - p.X, dz = w.SpawnZ - p.Z;
                double yaw = p.Yaw % 360.0;
                target = -((yaw - 90.0) * Math.PI / 180.0 - Math.Atan2(dz, dx));
                if (!w.Provider.HasDaylightCycle) target = R.NextDouble() * Math.PI * 2.0;
            }
            double d = target - _compassAngle;
            while (d < -Math.PI) d += Math.PI * 2; while (d >= Math.PI) d -= Math.PI * 2;
            d = Math.Clamp(d, -1, 1);
            _compassDelta += d * .1; _compassDelta *= .8; _compassAngle += _compassDelta;
            CompassFrame = Frame(_compassAngle / (Math.PI * 2), CompassFrames);

            double t = 0;
            if (w != null && p != null) t = w.Provider.HasDaylightCycle ? w.CelestialAngle(1f) : R.NextDouble();
            double c = t - _clockAngle;
            while (c < -.5) c++; while (c >= .5) c--;
            c = Math.Clamp(c, -1, 1);
            _clockDelta += c * .1; _clockDelta *= .8; _clockAngle += _clockDelta;
            ClockFrame = Frame(_clockAngle, ClockFrames);
        }

        private static int Frame(double turns, int frames)
        {
            int i = (int)((turns + 1.0) * frames) % frames;
            while (i < 0) i = (i + frames) % frames;
            return i;
        }
    }

    /// <summary>Płyty muzyczne 1.8 (ItemRecord): kolejność od 13 do wait jak identyfikatory 2256-2267.</summary>
    public static class Records
    {
        public static readonly string[] Names = { "13", "cat", "blocks", "chirp", "far", "mall", "mellohi", "stal", "strad", "ward", "11", "wait" };
        public static bool IsRecord(int id) => id is >= I.Record13 and <= I.RecordWait;
        public static string Name(int id) => IsRecord(id) ? Names[id - I.Record13] : null;

        /// <summary>Komunikat dźwiękowy przez świat (działa też w sieci): "record:cat" gra, "record:" zatrzymuje.</summary>
        public static void Play(World.World w, int x, int y, int z, int recordId)
        {
            LivingEntity.UnpredictedSound = true;
            try { w.PlaySound("record:" + (Name(recordId) ?? ""), x, y, z, 4f, 1f); }
            finally { LivingEntity.UnpredictedSound = false; }
        }
    }

    public static partial class ItemRegistry
    {
        private static void ItemsUtility()
        {
            var compass = Simple(I.Compass, "compass", "Kompas", Blocks.TabTools);
            compass.SpriteFn = _ => TextureGen.Layer("item_compass_" + ItemAnimations.CompassFrame);
            var clock = Simple(I.Clock, "clock", "Zegar", Blocks.TabTools);
            clock.SpriteFn = _ => TextureGen.Layer("item_clock_" + ItemAnimations.ClockFrame);
            for (int i = 0; i < Records.Names.Length; i++)
            {
                var rec = Simple(I.Record13 + i, "record_" + Records.Names[i], "Płyta muzyczna", Blocks.TabMisc, 1);
                string n = Records.Names[i];
                rec.LoreFn = _ => "C418 - " + n;
                rec.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) =>
                {
                    var w = p.World;
                    if (w.GetBlock(x, y, z) != B.Jukebox || w.GetMeta(x, y, z) != 0) return false;
                    if (w.IsRemote) return true;
                    JukeboxBlock.Insert(w, x, y, z, s.CopyWith(1));
                    if (!p.Creative) { s.Count--; if (s.Count <= 0) p.Inventory.Selected = null; }
                    return true;
                };
            }
        }
    }

    public static partial class Recipes
    {
        private static void RecipesUtility()
        {
            Shaped(new ItemStack(I.Compass), " I ", "IRI", " I ", 'I', I.IronIngot, 'R', I.Redstone);
            Shaped(new ItemStack(I.Clock), " G ", "GRG", " G ", 'G', I.GoldIngot, 'R', I.Redstone);
            foreach (int planks in new[] { B.OakPlanks, B.SprucePlanks, B.BirchPlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks })
                Shaped(new ItemStack(B.Jukebox), "###", "#X#", "###", '#', planks, 'X', I.Diamond);
        }
    }
}

namespace BlockCraft.World
{
    /// <summary>Tile entity szafy grającej (TileEntityJukebox, RecordItem).</summary>
    public sealed class JukeboxTile : TileEntity
    {
        public override int TypeId => 61;
        public ItemStack Record;
        public override void Save(BinaryWriter bw) => bw.WriteStack(Record);
        public override void Load(BinaryReader br) => Record = br.ReadStack();
    }

    /// <summary>BlockJukebox 1.8: meta 1 = jest płyta; PPM wyjmuje, zniszczenie wyrzuca, komparator = numer płyty.</summary>
    public sealed class JukeboxBlock : Block, IComparatorSource
    {
        public JukeboxBlock() : base(B.Jukebox, "jukebox", "Szafa grająca")
        {
            T("me_jukebox_top", "me_jukebox_side", "me_jukebox_side"); H(2, 10); Tool_(ToolType.Axe); Tab(Blocks.TabDecoration);
            HasTileEntity = true; Sound = SoundType.Wood;
        }

        public override void OnAdded(World w, int x, int y, int z) { if (w.GetTileEntity(x, y, z) is not JukeboxTile) w.SetTileEntity(x, y, z, new JukeboxTile()); }

        public static void Insert(World w, int x, int y, int z, ItemStack record)
        {
            if (w.GetTileEntity(x, y, z) is not JukeboxTile t) { t = new JukeboxTile(); w.SetTileEntity(x, y, z, t); }
            t.Record = record;
            w.SetMeta(x, y, z, 1);
            Records.Play(w, x, y, z, record.Id);
        }

        /// <summary>BlockJukebox.dropRecord.</summary>
        private static void Eject(World w, int x, int y, int z)
        {
            if (w.IsRemote || w.GetTileEntity(x, y, z) is not JukeboxTile t || t.Record == null) return;
            var rec = t.Record; t.Record = null;
            Records.Play(w, x, y, z, 0);
            var r = w.Rand; const double f = .7;
            w.DropItem(x + r.NextDouble() * f + (1 - f) * .5, y + r.NextDouble() * f + (1 - f) * .2 + .6, z + r.NextDouble() * f + (1 - f) * .5, rec);
        }

        public override bool OnActivated(World w, int x, int y, int z, Player p)
        {
            if (w.GetMeta(x, y, z) == 0) return false;
            if (w.IsRemote) return true;
            Eject(w, x, y, z);
            w.SetMeta(x, y, z, 0);
            return true;
        }

        public override void OnRemoved(World w, int x, int y, int z, int meta) => Eject(w, x, y, z);

        public int GetComparatorOutput(World w, int x, int y, int z)
            => w.GetTileEntity(x, y, z) is JukeboxTile { Record: { } r } && Records.IsRecord(r.Id) ? r.Id - Items.I.Record13 + 1 : 0;
    }
}

namespace BlockCraft.Render
{
    public static partial class TextureGen
    {
        /// <summary>Kompas (32 klatki), zegar (64), płyty i szafa grająca - proceduralnie w stylu 1.8.</summary>
        private static void TexUtility()
        {
            for (int f = 0; f < Items.ItemAnimations.CompassFrames; f++)
            {
                var px = New();
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    double dx = x - 7.5, dy = y - 7.5, r = Math.Sqrt(dx * dx + dy * dy);
                    if (r < 6.6) px[y * 16 + x] = Hex(r > 5.4 ? 0x5B5B5Bu : 0xC7C7C7u);
                    if (r >= 6.6 && r < 7.6) px[y * 16 + x] = Hex(0x2D2D2Du);
                }
                // igła: klatka 16 = do góry, rośnie zgodnie z ruchem wskazówek (TextureCompass)
                double phi = f / (double)Items.ItemAnimations.CompassFrames * Math.PI * 2 - Math.PI;
                double sx = Math.Sin(phi), sy = -Math.Cos(phi);
                for (double k = -4.5; k <= 4.5; k += .25)
                {
                    int x = (int)Math.Round(7.5 + sx * k), y = (int)Math.Round(7.5 + sy * k);
                    if ((uint)x < 16 && (uint)y < 16) px[y * 16 + x] = Hex(k > 0 ? 0xFF1414u : 0x6B6B6Bu);
                }
                px[7 * 16 + 7] = Hex(0x3A3A3Au);
                Add("item_compass_" + f, px);
                if (f == 16) Add("item_compass", (uint[])px.Clone());
            }
            for (int f = 0; f < Items.ItemAnimations.ClockFrames; f++)
            {
                var px = New();
                double rot = f / (double)Items.ItemAnimations.ClockFrames * Math.PI * 2;
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    double dx = x - 7.5, dy = y - 7.5, r = Math.Sqrt(dx * dx + dy * dy);
                    if (r >= 6.6 && r < 7.6) { px[y * 16 + x] = Hex(0xE8C23Bu); continue; }
                    if (r >= 6.6) continue;
                    // tarcza obraca się z porą dnia: dzień (niebo, słońce) i noc (granat, księżyc), dolna połowa zakryta jak w MC
                    double a = Math.Atan2(dx, -dy) - rot;
                    bool day = Math.Cos(a) > 0;
                    uint col = day ? 0x3E7CD9u : 0x10184Au;
                    double sunX = Math.Sin(rot) * 3.6, sunY = -Math.Cos(rot) * 3.6;
                    if ((dx - sunX) * (dx - sunX) + (dy - sunY) * (dy - sunY) < 2.3) col = 0xFFE24Au;
                    if ((dx + sunX) * (dx + sunX) + (dy + sunY) * (dy + sunY) < 2.3) col = 0xD8D8E8u;
                    if (dy > 1.5) col = 0x8A6A1Fu;
                    px[y * 16 + x] = Hex(col);
                }
                Add("item_clock_" + f, px);
                if (f == 0) Add("item_clock", (uint[])px.Clone());
            }
            uint[] label = { 0xE8D23Au, 0x5BC43Bu, 0xE5602Eu, 0xC4302Eu, 0x9BE07Au, 0x9A5FD0u, 0xE0A0E8u, 0x2B2B2Bu, 0xF2F2F2u, 0x2F6B2Fu, 0x6B6B6Bu, 0x4FD0E0u };
            for (int i = 0; i < 12; i++)
            {
                var px = New();
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                {
                    double dx = x - 7.5, dy = y - 7.5, r = Math.Sqrt(dx * dx + dy * dy);
                    if (r >= 7.2) continue;
                    uint col = r < 1 ? 0x111111u : r < 3.2 ? label[i] : (x + y) % 3 == 0 ? 0x2A2A2Au : 0x1A1A1Au;
                    if (i == 10 && Math.Abs(dx - dy * .4) < .6 && r > 3.2) col = 0x6B6B6Bu; // płyta 11 pęknięta
                    px[y * 16 + x] = Hex(col);
                }
                Add("item_record_" + Items.Records.Names[i], px);
            }
            var noise = TileNoise(97, 8);
            var side = New(); var top = New();
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                int i = y * 16 + x; bool edge = x == 0 || y == 0 || x == 15 || y == 15;
                side[i] = Mul(Hex(edge ? 0x4A2E1Bu : 0x6B4429u), .85f + noise[i] * .25f);
                if (!edge && (y == 4 || y == 11)) side[i] = Mul(side[i], .8f);
                top[i] = Mul(Hex(edge ? 0x4A2E1Bu : 0x6B4429u), .85f + noise[i] * .25f);
                if (y is 7 or 8 && x is >= 3 and <= 12) top[i] = Hex(0x1A120Au);
            }
            Add("me_jukebox_side", side); Add("me_jukebox_top", top);
            Add("item_sign", Sprite(new[]
            {
                "................", "..oooooooooooo..", "..ollllllllllo..", "..oldldddldldo..", "..ollllllllllo..", "..olddldldddlo..",
                "..ollllllllllo..", "..oooooooooooo..", "......oddo......", "......oddo......", "......oddo......", "......oddo......",
                "......oddo......", "......oddo......", "......oooo......", "................",
            }, Pal(('o', 0x4A3219), ('l', 0xB08F5A), ('d', 0x6B5331))));
            Add("item_boat", Sprite(new[]
            {
                "................", "................", "................", "................", "................", "................",
                "o..............o", "ol............lo", "oll..........llo", "olllllllllllllo.", ".ollllllllllllo.", "..oddddddddddo..",
                "...oooooooooo...", "................", "................", "................",
            }, Pal(('o', 0x4A3219), ('l', 0x9C7F4E), ('d', 0x6B5331))));
        }
    }
}

namespace BlockCraft.Entities
{
    public sealed partial class Creeper
    {
        /// <summary>EntityCreeper.onDeath 1.8: zabity strzałą szkieleta wyrzuca losową płytę 13-wait.</summary>
        public override void OnDeath(DamageSource src)
        {
            bool wasDead = Dead;
            base.OnDeath(src);
            if (!wasDead && !World.IsRemote && src.Attacker is Skeleton) Drop(Items.I.Record13 + World.Rand.Next(Items.I.RecordWait - Items.I.Record13 + 1), 1);
        }
    }
}
