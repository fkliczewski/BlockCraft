using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public static partial class Blocks
{
    static partial void BlocksMechanics()
    {
        new CakeBlock();
        new SkullBlock();
        new BrewingStandBlock(); new CauldronBlock(); new EnchantingTableBlock(); new AnvilBlock(); new JukeboxBlock(); new StandingSignBlock(); new WallSignBlock(); new StandingBannerBlock(); new WallBannerBlock();
    }
}

public sealed class BrewingStandBlock : Block
{
    public BrewingStandBlock() : base(B.BrewingStand, "brewing_stand", "Statyw alchemiczny")
    {
        T("me_brewing"); H(.5f); Tool_(ToolType.Pickaxe, 0); Tab(Blocks.TabBrewing);
        HasTileEntity = true; Opaque = FullCube = false; LightOpacity = 0; LightEmit = 1; Render = RenderType.Model; DropOverride = I.BrewingStandItem; InCreative = false;
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        boxes.Add(ModelBox.Px(7, 0, 7, 9, 14, 9, Tex));
        boxes.Add(ModelBox.Px(2, 0, 2, 6, 2, 6, Tex)); boxes.Add(ModelBox.Px(10, 0, 2, 14, 2, 6, Tex)); boxes.Add(ModelBox.Px(6, 0, 10, 10, 2, 14, Tex));
        for (int i = 0; i < 3; i++) if ((meta & 1 << i) != 0) boxes.Add(ModelBox.Px(2 + i * 5, 3, 4, 5 + i * 5, 8, 7, Tex));
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes)
    { boxes.Add(new AABB(0, 0, 0, 1, .125, 1)); boxes.Add(new AABB(.4375, 0, .4375, .5625, .875, .5625)); }
    public override void OnAdded(World w, int x, int y, int z) => w.SetTileEntity(x, y, z, new BrewingStandTile());
    public override bool OnActivated(World w, int x, int y, int z, Player p) { p.OpenBlockGui(GuiKind.Brewing, x, y, z); return true; }
    public override void OnRemoved(World w, int x, int y, int z, int meta)
    {
        if (w.GetTileEntity(x, y, z) is BrewingStandTile tile) foreach (var s in tile.Items) if (s != null) w.DropItem(x + .5, y + .5, z + .5, s);
    }
}

public sealed class CauldronBlock : Block
{
    public CauldronBlock() : base(B.Cauldron, "cauldron", "Kocioł")
    {
        T("me_cauldron"); H(2); Tool_(ToolType.Pickaxe, 0); Tab(Blocks.TabBrewing); Opaque = FullCube = false;
        LightOpacity = 0; Render = RenderType.Model; RandomTicks = true; DropOverride = I.CauldronItem; InCreative = false;
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        boxes.Add(ModelBox.Px(0, 0, 0, 16, 5, 16, Tex)); boxes.Add(ModelBox.Px(0, 5, 0, 2, 16, 16, Tex));
        boxes.Add(ModelBox.Px(14, 5, 0, 16, 16, 16, Tex)); boxes.Add(ModelBox.Px(2, 5, 0, 14, 16, 2, Tex)); boxes.Add(ModelBox.Px(2, 5, 14, 14, 16, 16, Tex));
        if (meta > 0) boxes.Add(ModelBox.Px(2, 5, 2, 14, 6 + meta * 3, 14, ModelBox.All(BlockCraft.Render.TextureGen.Layer("water_still"))));
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes)
    {
        boxes.Add(new AABB(0, 0, 0, 1, .3125, 1)); boxes.Add(new AABB(0, 0, 0, .125, 1, 1)); boxes.Add(new AABB(.875, 0, 0, 1, 1, 1));
        boxes.Add(new AABB(.125, 0, 0, .875, 1, .125)); boxes.Add(new AABB(.125, 0, .875, .875, 1, 1));
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int level = w.GetMeta(x, y, z); var stack = p.Inventory.Selected;
        if (stack?.Id == I.WaterBucket && level < 3)
        { w.SetMeta(x, y, z, 3); if (!p.Creative) p.Inventory.Selected = new ItemStack(I.Bucket); }
        else if (stack?.Id == I.GlassBottle && level > 0)
        {
            if (!p.Creative) { PlayerAlchemy.ExchangeOne(p, stack, new ItemStack(I.Potion)); w.SetMeta(x, y, z, level - 1); }
        }
        else if (stack?.Id == I.Banner && Banners.Wash(w, x, y, z, p, stack)) return true;
        else if (level > 0 && stack?.Item?.ArmorMaterial == 0 && stack.Tag?.GetCompound("display") is { } display && display.ContainsKey("color"))
        { display.Remove("color"); w.SetMeta(x, y, z, level - 1); }
        p.Inventory.Dirty = true; return true;
    }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    { if (r.Next(20) == 0 && w.GetMeta(x, y, z) < 3 && WorldHooks.IsRainingAt?.Invoke(w, x, y + 1, z) == true) w.SetMeta(x, y, z, w.GetMeta(x, y, z) + 1); }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e)
    { int level = w.GetMeta(x, y, z); if (level > 0 && e.FireTicks > 0 && e.BoundingBox.MinY <= y + (6 + level * 3) / 16.0) { e.FireTicks = 0; w.SetMeta(x, y, z, level - 1); } }
    public int ComparatorSignal(World w, int x, int y, int z) => w.GetMeta(x, y, z);
}
