using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>
/// BlockAnvil z 1.8: spada jak piasek, meta = kierunek (bity 0-1) i uszkodzenie 0-2 (bity 2-3),
/// twardość 5, odporność 2000, otwiera ContainerRepair. Model z anvil_undamaged.json.
/// </summary>
public sealed class AnvilBlock : GravityBlock
{
    private static readonly string[] Names = { "Kowadło", "Lekko uszkodzone kowadło", "Mocno uszkodzone kowadło" };
    private readonly int _base;
    private readonly int[] _top = new int[3];

    public AnvilBlock() : base(B.Anvil, "anvil", "Kowadło")
    {
        T("me_anvil_base"); H(5, 2000); Tool_(ToolType.Pickaxe, 0); Tab(Blocks.TabDecoration);
        Opaque = FullCube = false; LightOpacity = 0; Render = RenderType.Model; Sound = SoundType.Metal;
        Vars(0, 1, 2);
        _base = TextureGen.Layer("me_anvil_base");
        for (int i = 0; i < 3; i++) _top[i] = TextureGen.Layer("me_anvil_top_" + i);
    }

    public static int Damage(int meta) => meta >> 2 & 3;
    private static bool AxisX(int meta) => (meta & 1) == 1; // zachód/wschód

    public override string DisplayName(int meta) => Names[Math.Min(2, meta)];
    public override int DropMeta(int meta) => Damage(meta);
    public override ItemStack PickStack(int meta) => new(B.Anvil, 1, Damage(meta));
    public override int GetTexture(int face, int meta) => face == Dir.Up ? _top[Math.Min(2, Damage(meta))] : _base;

    /// <summary>onBlockPlaced: kierunek = poziomy kierunek gracza obrócony o 90 stopni, uszkodzenie z metadanych przedmiotu.</summary>
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => (Dir.HorizFromYaw(p?.Yaw ?? 0) + 1 & 3) | Math.Clamp(itemMeta, 0, 2) << 2;

    private static AABB Box(float x0, float y0, float z0, float x1, float y1, float z1, bool axisX)
        => axisX ? new AABB(z0 / 16, y0 / 16, x0 / 16, z1 / 16, y1 / 16, x1 / 16) : new AABB(x0 / 16, y0 / 16, z0 / 16, x1 / 16, y1 / 16, z1 / 16);

    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        bool ax = AxisX(meta);
        int top = _top[Math.Min(2, Damage(meta))];
        var baseTex = ModelBox.All(_base);
        var topTex = new[] { _base, top, _base, _base, _base, _base };
        void Add(float x0, float y0, float z0, float x1, float y1, float z1, int[] tex)
        {
            var b = Box(x0, y0, z0, x1, y1, z1, ax);
            boxes.Add(new ModelBox((float)b.MinX, (float)b.MinY, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, tex));
        }
        Add(2, 0, 2, 14, 4, 14, baseTex);
        Add(4, 4, 3, 12, 5, 13, baseTex);
        Add(6, 5, 4, 10, 10, 12, baseTex);
        Add(3, 10, 0, 13, 16, 16, topTex);
    }

    /// <summary>setBlockBoundsBasedOnState: oś X (0, 0, 0.125)-(1, 1, 0.875), inaczej (0.125, 0, 0)-(0.875, 1, 1).</summary>
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes)
        => boxes.Add(AxisX(meta) ? new AABB(0, 0, .125, 1, 1, .875) : new AABB(.125, 0, 0, .875, 1, 1));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) => GetCollisionBoxes(w, x, y, z, meta, boxes);

    public override bool OnActivated(World w, int x, int y, int z, Player p) { p.OpenBlockGui(GuiKind.Anvil, x, y, z); return true; }

    public override void OnPlaced(World w, int x, int y, int z, Player p)
    {
        if (!w.IsRemote) w.PlaySound("random.anvil_land", x + .5, y + .5, z + .5, .3f, w.Rand.NextFloat() * .1f + .9f);
    }

    /// <summary>BlockFalling.checkFallable: kowadło spada z zachowaniem kierunku i uszkodzenia, rani to, na co spadnie.</summary>
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote || y <= 0) return;
        int below = w.GetBlock(x, y - 1, z);
        var bb = Block.Get(below);
        if (below == B.Air || bb.IsLiquid || bb.Replaceable && !bb.Solid)
        {
            if (!w.IsAreaLoaded(x, z, 2)) return;
            int meta = w.GetMeta(x, y, z);
            w.SetBlock(x, y, z, B.Air);
            w.SpawnEntity(new FallingBlockEntity(w, x + .5, y, z + .5, Id, meta) { HurtEntities = true });
        }
    }
}
