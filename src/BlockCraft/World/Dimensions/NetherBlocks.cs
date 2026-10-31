using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

public static partial class Blocks
{
    static partial void BlocksNetherEnd()
    {
        new Block(B.Netherrack, "netherrack", "Netherrack").T("netherrack").H(.4f).Tool_(ToolType.Pickaxe, 0);
        new SoulSandBlock().T("soul_sand").H(.5f).Tool_(ToolType.Shovel).Snd(SoundType.Sand);
        new Block(B.NetherBrick, "nether_brick", "Cegły netherowe").T("nether_brick").H(2, 6).Tool_(ToolType.Pickaxe, 0);
        new NetherFenceBlock().T("nether_brick").H(2, 6).Tool_(ToolType.Pickaxe, 0);
        new StairsBlock(B.NetherBrickStairs, "nether_brick_stairs", "Schody z cegieł netherowych", "nether_brick").H(2, 6).Tool_(ToolType.Pickaxe, 0);
        new SlabBlock(B.NetherBrickSlab, "nether_brick_slab", "Płyta z cegieł netherowych", "nether_brick", "nether_brick", B.NetherBrick).H(2, 6).Tool_(ToolType.Pickaxe, 0);
        new NetherWartBlock();
        new QuartzOreBlock().T("quartz_ore").H(3).Tool_(ToolType.Pickaxe, 0);
        new QuartzBlock().T("quartz_block").H(.8f).Tool_(ToolType.Pickaxe, 0).Vars(0, 1, 2);
        new StairsBlock(B.QuartzStairs, "quartz_stairs", "Kwarcowe schody", "quartz_block").H(.8f).Tool_(ToolType.Pickaxe, 0);
        new SlabBlock(B.QuartzSlab, "quartz_slab", "Kwarcowa płyta", "quartz_block", "quartz_block", B.QuartzBlock).H(2, 6).Tool_(ToolType.Pickaxe, 0);
        new DimensionPortalBlock(B.Portal, "portal", "Portal Netheru").T("portal").Light(11);
        new NetherFireBlock();
        new Block(B.EndStone, "end_stone", "Kamień Endu").T("end_stone").H(3, 9).Tool_(ToolType.Pickaxe, 0);
        new DimensionPortalBlock(B.EndPortal, "end_portal", "Portal Endu").T("end_portal").Light(15);
        new EndFrameBlock();
        new DragonEggBlock();
        new GlowstoneDustBlock().T("glowstone").H(.3f).Light(15).Snd(SoundType.Glass);
    }
}

public sealed class SoulSandBlock : Block
{
    public SoulSandBlock() : base(B.SoulSand, "soul_sand", "Piasek dusz") { }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new(0, 0, 0, 1, .875, 1));
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e) { e.MotionX *= .4; e.MotionZ *= .4; }
}

public sealed class NetherFenceBlock : Block
{
    public NetherFenceBlock() : base(B.NetherBrickFence, "nether_brick_fence", "Płot z cegieł netherowych")
    { Opaque = FullCube = false; Render = RenderType.Model; LightOpacity = 0; }
    private static bool Joins(IBlockAccess w, int x, int y, int z)
    { byte id = w.GetBlock(x, y, z); return id == B.NetherBrickFence || id == B.FenceGate || (Block.Get(id).Opaque && id != B.Tnt); }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        list.Add(new(Joins(w,x-1,y,z) ? 0 : .375, 0, .375, Joins(w,x+1,y,z) ? 1 : .625, 1.5, .625));
        list.Add(new(.375, 0, Joins(w,x,y,z-1) ? 0 : .375, .625, 1.5, Joins(w,x,y,z+1) ? 1 : .625));
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        boxes.Add(ModelBox.Px(6,0,6,10,16,10,Tex));
        for (int f = 2; f < 6; f++) if (Joins(w,x+Dir.X[f],y,z+Dir.Z[f]))
            foreach (int h in new[] { 6,12 }) boxes.Add(ModelBox.Px(Dir.X[f] < 0 ? 0 : 7, h, Dir.Z[f] < 0 ? 0 : 7, Dir.X[f] > 0 ? 16 : 9, h+3, Dir.Z[f] > 0 ? 16 : 9, Tex));
    }
}

public sealed class NetherWartBlock : Block
{
    public NetherWartBlock() : base(B.NetherWart, "nether_wart", "Brodawka netherowa")
    { Plant(RenderType.Crops); T("nether_wart_0"); RandomTicks = true; InCreative = false; }
    public override bool CanPlaceAt(World w, int x, int y, int z) => w.GetBlock(x,y-1,z) == B.SoulSand;
    public override int GetTexture(int face, int meta) => TextureGen.Layer("nether_wart_" + (meta >= 3 ? 2 : meta >= 1 ? 1 : 0));
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    { int m = w.GetMeta(x,y,z); if (m < 3 && r.Next(10) == 0) w.SetMeta(x,y,z,m+1); }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    { int fortune = EnchantHelper.GetLevel(tool, Ench.Fortune); drops.Add(new(I.NetherWartItem, meta >= 3 ? 2 + r.Next(3) + (fortune > 0 ? r.Next(fortune+1) : 0) : 1)); }
    public override ItemStack PickStack(int meta) => new(I.NetherWartItem);
}

public sealed class QuartzOreBlock : OreBlock
{
    public QuartzOreBlock() : base(B.QuartzOre, "quartz_ore", "Ruda kwarcu Netheru", I.Quartz, 1, 1) { }
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    { if (!p.Creative) w.SpawnXp(x+.5,y+.5,z+.5,w.Rand.Next(2,6)); }
}

public sealed class QuartzBlock : Block
{
    public QuartzBlock() : base(B.QuartzBlock, "quartz_block", "Blok kwarcu") { }
    public override int DropMeta(int meta) => meta >= 2 ? 2 : meta;
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => itemMeta == 2 ? face <= 1 ? 2 : face >= 4 ? 3 : 4 : itemMeta;
    public override int GetTexture(int face, int meta) => TextureGen.Layer(meta == 1 ? "quartz_chiseled" : meta >= 2 && !(meta == 2 ? face <= 1 : meta == 3 ? face >= 4 : face is 2 or 3) ? "quartz_pillar" : "quartz_block");
    public override string DisplayName(int meta) => meta == 1 ? "Rzeźbiony blok kwarcu" : meta >= 2 ? "Kwarcowy filar" : Name;
}

public sealed class GlowstoneDustBlock : Block
{
    public GlowstoneDustBlock() : base(B.Glowstone, "glowstone", "Jasnogłaz") { }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (EnchantHelper.GetLevel(tool, Ench.SilkTouch) > 0) { drops.Add(new(Id)); return; }
        int f = EnchantHelper.GetLevel(tool, Ench.Fortune);
        drops.Add(new(I.GlowstoneDust, Math.Min(4, 2 + r.Next(3) + (f > 0 ? r.Next(f+1) : 0))));
    }
}

public sealed partial class DimensionPortalBlock : Block
{
    public DimensionPortalBlock(byte id, string key, string name) : base(id,key,name)
    { H(-1,3600000); Opaque = Solid = FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Translucent; LightOpacity = 0; InCreative = false; DropOverride = 0; }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    { boxes.Add(Id == B.EndPortal ? new(0, .75f, 0, 1, .76f, 1, Tex) : meta == 2 ? new(.375f,0,0,.625f,1,1,Tex) : new(0,0,.375f,1,1,.625f,Tex)); }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e) => WorldHooks.OnEntityInPortal?.Invoke(e,Id);
}

public sealed partial class NetherFireBlock : Block
{
    public NetherFireBlock() : base(B.Fire,"fire","Ogień")
    { Plant(); T("fire"); Light(15); InCreative = false; NeedsSupport = false; DropOverride = 0; RandomTicks = true; }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e) { if (e is LivingEntity le) le.Attack(DamageSource.Fire,1); e.SetFire(8); }
}

public sealed partial class EndFrameBlock : Block
{
    public EndFrameBlock() : base(B.EndPortalFrame,"end_portal_frame","Rama portalu Endu")
    { H(-1,3600000); T("end_portal_frame"); Light(1); Opaque = FullCube = false; Render = RenderType.Model; DropOverride = 0; }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta) => (MathUtil.FloorToInt(p.Yaw * 4 / 360 + .5) + 2) & 3;
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    { list.Add(new(0,0,0,1,.8125,1)); if ((meta & 4) != 0) list.Add(new(.25,.8125,.25,.75,1,.75)); }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    { boxes.Add(new(0,0,0,1,.8125f,1,Tex)); if ((meta & 4) != 0) boxes.Add(new(.25f,.8125f,.25f,.75f,1,.75f,ModelBox.All(TextureGen.Layer("item_ender_eye")))); }
}

public sealed class DragonEggBlock : GravityBlock
{
    public DragonEggBlock() : base(B.DragonEgg,"dragon_egg","Jajo smoka")
    { T("dragon_egg"); H(3,9); Light(1); Opaque = FullCube = false; LightOpacity = 0; Render = RenderType.Model; }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new(.0625,0,.0625,.9375,1,.9375));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    { for (int i = 0; i < 8; i++) { float width = new float[] { 8,12,14,14,12,10,8,4 }[i]; boxes.Add(ModelBox.Px(8-width/2,i*2,8-width/2,8+width/2,i*2+2,8+width/2,Tex)); } }
    public override bool OnActivated(World w, int x, int y, int z, Player p) { Teleport(w,x,y,z); return true; }
    public override void OnClicked(World w, int x, int y, int z, Player p) => Teleport(w,x,y,z);
    private static void Teleport(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        for (int i = 0; i < 1000; i++)
        {
            int tx = x+w.Rand.Next(16)-w.Rand.Next(16), ty = y+w.Rand.Next(8)-w.Rand.Next(8), tz = z+w.Rand.Next(16)-w.Rand.Next(16);
            if ((uint)ty >= Chunk.Height || !w.IsLoadedAt(tx,tz) || w.GetBlock(tx,ty,tz) != B.Air) continue;
            w.SetBlock(tx,ty,tz,B.DragonEgg); w.SetBlock(x,y,z,B.Air); w.SpawnParticles(ParticleKind.Portal,x+.5,y+.5,z+.5,128); return;
        }
    }
}
