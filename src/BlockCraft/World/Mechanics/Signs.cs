using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;
using OpenTK.Mathematics;
using B = BlockCraft.World.B;

namespace BlockCraft.World
{
    /// <summary>TileEntitySign: cztery linie tekstu.</summary>
    public sealed class SignTile : TileEntity
    {
        public override int TypeId => 62;
        public readonly string[] Lines = { "", "", "", "" };
        public override void Save(BinaryWriter bw) { foreach (var l in Lines) bw.Write(l ?? ""); }
        public override void Load(BinaryReader br) { for (int i = 0; i < 4; i++) Lines[i] = br.ReadString(); }
    }

    /// <summary>Wspólne zasady tabliczek 1.8 (BlockSign, BlockStandingSign, BlockWallSign, ItemSign, TileEntitySignRenderer).</summary>
    public static class Signs
    {
        public const int MaxLineWidth = 90, MaxLineChars = 40;
        const float F = 2f / 3f;

        public static bool IsSign(int id) => id is B.StandingSign or B.WallSign;

        /// <summary>Tekst dopuszczony do zapisu (ChatAllowedCharacters, bez kodów formatowania).</summary>
        public static string Filter(string s)
        {
            if (s == null) return "";
            var t = new string(s.Where(c => c != '§' && c >= ' ' && c != 127).ToArray());
            return t.Length > MaxLineChars ? t[..MaxLineChars] : t;
        }

        /// <summary>Kąt obrotu wokół osi Y (stopnie, jak w rendererze MC przed zanegowaniem).</summary>
        public static float Angle(int id, int meta) => id == B.StandingSign ? (meta & 15) * 360f / 16f
            : meta switch { 2 => 180f, 4 => 90f, 5 => -90f, _ => 0f };

        /// <summary>Macierz lokalna tabliczki: środek bloku na wysokości 0.75*2/3, obrót, przesunięcie ściennej.</summary>
        public static Matrix4 Frame(int id, int meta, Vector3 blockRel)
        {
            var m = Matrix4.CreateRotationY(-Angle(id, meta) * MathUtil.Deg2Rad) * Matrix4.CreateTranslation(blockRel + new Vector3(.5f, .75f * F, .5f));
            if (id == B.WallSign) m = Matrix4.CreateTranslation(0, -.3125f, -.4375f) * m;
            return m;
        }

        /// <summary>Płaszczyzna tekstu: piksel czcionki (x w prawo, y w dół) -> przestrzeń tabliczki.</summary>
        public static Matrix4 TextFrame(int id, int meta, Vector3 blockRel)
        {
            float f3 = .015625f * F;
            return Matrix4.CreateScale(f3, -f3, f3) * Matrix4.CreateTranslation(0, .5f * F, .07f * F) * Frame(id, meta, blockRel);
        }

        /// <summary>ModelSign: deska 24x12x2 i słupek 2x14x2 (piksele), skala 2/3.</summary>
        public static void Model(int id, int meta, Vector3 blockRel, Action<ModelBox, Matrix4> emit, int tex)
        {
            var m = Frame(id, meta, blockRel);
            var t = ModelBox.All(tex);
            float p = F / 16f;
            emit(new ModelBox(-12 * p, 2 * p, -1 * p, 12 * p, 14 * p, 1 * p, t), m);
            if (id == B.StandingSign) emit(new ModelBox(-1 * p, -12 * p, -1 * p, 1 * p, 2 * p, 1 * p, ModelBox.All(TextureGen.Layer("log_oak"))), m);
        }

        /// <summary>Tabliczki w zasięgu 64 bloków od kamery.</summary>
        public static IEnumerable<SignTile> Near(World w, Vector3d cam)
        {
            int ccx = MathUtil.FloorToInt(cam.X) >> 4, ccz = MathUtil.FloorToInt(cam.Z) >> 4;
            for (int cx = ccx - 4; cx <= ccx + 4; cx++)
                for (int cz = ccz - 4; cz <= ccz + 4; cz++)
                {
                    var c = w.GetChunk(cx, cz); if (c == null) continue;
                    foreach (var te in c.TileEntities.Values)
                        if (te is SignTile s && (s.X + .5 - cam.X) * (s.X + .5 - cam.X) + (s.Y + .5 - cam.Y) * (s.Y + .5 - cam.Y) + (s.Z + .5 - cam.Z) * (s.Z + .5 - cam.Z) < 64 * 64)
                            yield return s;
                }
        }

        /// <summary>ItemSign.onItemUse z 1.8.</summary>
        public static bool Place(Player p, ItemStack held, int x, int y, int z, int face)
        {
            var w = p.World;
            if (face == Dir.Down || !Block.Get(w.GetBlock(x, y, z)).Solid) return false;
            x += Dir.X[face]; y += Dir.Y[face]; z += Dir.Z[face];
            var at = Block.Get(w.GetBlock(x, y, z));
            if (at.Id != 0 && !at.Replaceable) return false;
            if (w.IsRemote) return true;
            if (face == Dir.Up) w.SetBlock(x, y, z, B.StandingSign, MathUtil.FloorToInt((p.Yaw + 180f) * 16f / 360f + .5f) & 15);
            else w.SetBlock(x, y, z, B.WallSign, face);
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            w.PlaySound("place_wood", x + .5, y + .5, z + .5, 1f, .8f);
            p.OpenBlockGui(GuiKind.SignEdit, x, y, z);
            return true;
        }
    }

    public abstract class SignBlock : Block
    {
        protected SignBlock(byte id, string key) : base(id, key, "Tabliczka")
        {
            T("planks_oak"); H(1); Tool_(ToolType.Axe); Snd(SoundType.Wood);
            Opaque = Solid = FullCube = false; LightOpacity = 0; Render = RenderType.None; InCreative = false;
            HasTileEntity = true; DropOverride = Items.I.SignItem;
        }
        public override ItemStack PickStack(int meta) => new(Items.I.SignItem);
        public override void OnAdded(World w, int x, int y, int z) { if (w.GetTileEntity(x, y, z) is not SignTile) w.SetTileEntity(x, y, z, new SignTile()); }
        public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) { }
        protected abstract bool Supported(World w, int x, int y, int z, int meta);
        public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
        {
            if (w.IsRemote || Supported(w, x, y, z, w.GetMeta(x, y, z))) return;
            w.BreakBlock(x, y, z, true);
        }
    }

    /// <summary>BlockStandingSign: meta = obrót 0-15, wymaga stałego bloku pod spodem.</summary>
    public sealed class StandingSignBlock : SignBlock
    {
        public StandingSignBlock() : base(B.StandingSign, "standing_sign") { }
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) => boxes.Add(new AABB(.25, 0, .25, .75, 1, .75));
        protected override bool Supported(World w, int x, int y, int z, int meta) => Block.Get(w.GetBlock(x, y - 1, z)).Solid;
    }

    /// <summary>BlockWallSign: meta = ściana 2-5 (N, S, W, E), wymaga stałego bloku za plecami.</summary>
    public sealed class WallSignBlock : SignBlock
    {
        public WallSignBlock() : base(B.WallSign, "wall_sign") { }
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes)
        {
            const double a = .28125, b = .78125, d = .125;
            boxes.Add(meta switch
            {
                2 => new AABB(0, a, 1 - d, 1, b, 1),
                3 => new AABB(0, a, 0, 1, b, d),
                4 => new AABB(1 - d, a, 0, 1, b, 1),
                _ => new AABB(0, a, 0, d, b, 1),
            });
        }
        protected override bool Supported(World w, int x, int y, int z, int meta)
        {
            int f = meta is >= 2 and <= 5 ? meta : 2, o = Dir.Opp[f];
            return Block.Get(w.GetBlock(x + Dir.X[o], y + Dir.Y[o], z + Dir.Z[o])).Solid;
        }
    }
}

namespace BlockCraft.Items
{
    public static partial class ItemRegistry
    {
        internal static void ItemsSigns()
        {
            var sign = Simple(I.SignItem, "sign", "Tabliczka", World.Blocks.TabDecoration, 16);
            sign.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => World.Signs.Place(p, s, x, y, z, face);
        }
    }

    public static partial class Recipes
    {
        internal static void RecipesSigns()
        {
            foreach (int planks in new[] { B.OakPlanks, B.SprucePlanks, B.BirchPlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks })
                Shaped(new ItemStack(I.SignItem, 3), "###", "###", " X ", '#', planks, 'X', I.Stick);
        }
    }
}
