using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

public enum RenderType { None, Cube, Cross, Crops, Liquid, Torch, Model, Wire, Cactus }
public enum RenderLayer { Solid, Cutout, Translucent }
public enum ToolType { None, Pickaxe, Axe, Shovel, Sword, Shears, Hoe }
public enum TintType { None, Grass, Foliage, Birch, RedstoneWire }
public enum SoundType { Stone, Wood, Gravel, Grass, Sand, Glass, Wool, Metal, Ladder }

/// <summary>Box modelu (współrzędne 0..1) z teksturami per ściana.</summary>
public struct ModelBox
{
    public float X0, Y0, Z0, X1, Y1, Z1;
    public int[] Tex;               // 6 warstw
    public float[] Uv;              // opcjonalnie: 6*4 (u0,v0,u1,v1) w pikselach /16
    public int[] UvRot;             // opcjonalnie rotacja UV per ściana
    public bool NoCull;
    public ModelBox(float x0, float y0, float z0, float x1, float y1, float z1, int[] tex)
    { X0 = x0; Y0 = y0; Z0 = z0; X1 = x1; Y1 = y1; Z1 = z1; Tex = tex; Uv = null; UvRot = null; NoCull = false; }
    public static ModelBox Px(float x0, float y0, float z0, float x1, float y1, float z1, int[] tex)
        => new(x0 / 16f, y0 / 16f, z0 / 16f, x1 / 16f, y1 / 16f, z1 / 16f, tex);
    public static int[] All(int t) => new[] { t, t, t, t, t, t };
}

public class Block
{
    public static readonly Block[] ById = new Block[256];

    public byte Id;
    public string Key;
    public string Name;
    public float Hardness = 1f;
    public float BlastResistance = -1f;
    public bool Opaque = true;        // pełny nieprzezroczysty sześcian
    public bool Solid = true;         // ma kolizję
    public bool FullCube = true;      // zasłania sąsiednie ściany (kultywowanie)
    public RenderType Render = RenderType.Cube;
    public RenderLayer Layer = RenderLayer.Solid;
    public int[] Tex = new int[6];
    public int LightEmit;
    public int LightOpacity = 15;
    public ToolType Tool = ToolType.None;
    public int HarvestLevel = -1;     // -1 = nie wymaga narzędzia
    public TintType Tint = TintType.None;
    public bool TintMaskSides;        // trawa: tint tylko na nakładce boku
    public bool Replaceable;
    public bool IsLiquid;
    public bool RandomTicks;
    public bool Climbable;
    public bool InCreative = true;
    public int CreativeTab = 0;
    public SoundType Sound = SoundType.Stone;
    public float Slipperiness = 0.6f;
    public bool NeedsSupport;         // np. rośliny, pochodnie
    public bool IsPowerSource;
    public bool HasTileEntity;
    public bool Flammable;
    public int DropOverride = -1, DropOverrideCount = 1;
    /// <summary>Warianty meta pokazywane w kreatywnym i jako osobne ikony (null = tylko 0).</summary>
    public int[] Variants;

    public Block(byte id, string key, string name)
    {
        Id = id; Key = key; Name = name;
        ById[id] = this;
    }

    public static Block Get(int id) => ById[id] ?? ById[0];

    // --- builder ---
    public Block T(string all) { int t = TextureGen.Layer(all); for (int i = 0; i < 6; i++) Tex[i] = t; return this; }
    public Block T(string top, string bottom, string side)
    {
        int a = TextureGen.Layer(top), b = TextureGen.Layer(bottom), c = TextureGen.Layer(side);
        Tex[0] = b; Tex[1] = a; Tex[2] = Tex[3] = Tex[4] = Tex[5] = c; return this;
    }
    public Block H(float hardness, float blast = -1) { Hardness = hardness; BlastResistance = blast < 0 ? hardness : blast; return this; }
    public Block Tool_(ToolType t, int level = -1) { Tool = t; HarvestLevel = level; return this; }
    public Block Snd(SoundType s) { Sound = s; return this; }
    public Block Transparent(RenderLayer layer = RenderLayer.Cutout, int opacity = 0)
    { Opaque = false; Layer = layer; LightOpacity = opacity; return this; }
    public Block Plant(RenderType r = RenderType.Cross)
    {
        Opaque = false; Solid = false; FullCube = false; Render = r; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0; BlastResistance = 0; Sound = SoundType.Grass; NeedsSupport = true; return this;
    }
    public Block Light(int l) { LightEmit = l; return this; }
    public Block Tab(int t) { CreativeTab = t; return this; }
    public Block NotCreative() { InCreative = false; return this; }
    public Block Vars(params int[] metas) { Variants = metas; return this; }
    public Block Vars(int count) { Variants = Enumerable.Range(0, count).ToArray(); return this; }

    // --- zachowanie ---
    public virtual int GetTexture(int face, int meta) => Tex[face];
    public virtual bool IsOpaqueCube(int meta) => Opaque;

    /// <summary>Boxy kolizji w lokalnych współrzędnych bloku (0..1).</summary>
    public virtual void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        if (!Solid) return;
        list.Add(new AABB(0, 0, 0, 1, 1, 1));
    }
    /// <summary>Boxy zaznaczenia (raycast/obrys).</summary>
    public virtual void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        if (Render == RenderType.Cross || Render == RenderType.Crops)
        {
            list.Add(new AABB(0.125, 0, 0.125, 0.875, 0.8, 0.875)); return;
        }
        int c = list.Count;
        GetCollisionBoxes(w, x, y, z, meta, list);
        if (list.Count == c) list.Add(new AABB(0, 0, 0, 1, 1, 1));
    }

    /// <summary>Model dla RenderType.Model.</summary>
    public virtual void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes) { }

    public virtual bool CanPlaceAt(World w, int x, int y, int z) => true;
    public virtual bool CanStay(World w, int x, int y, int z) => CanPlaceAt(w, x, y, z);
    public virtual int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta) => itemMeta;

    public virtual void OnPlaced(World w, int x, int y, int z, Player p) { }
    public virtual void OnAdded(World w, int x, int y, int z) { }
    public virtual void OnRemoved(World w, int x, int y, int z, int oldMeta) { }
    public virtual bool OnActivated(World w, int x, int y, int z, Player p) => false;
    public virtual void OnClicked(World w, int x, int y, int z, Player p) { }
    public virtual void OnNeighborChanged(World w, int x, int y, int z, int neighborId)
    {
        if (NeedsSupport && !CanStay(w, x, y, z)) w.BreakBlock(x, y, z, true);
    }
    public virtual void OnScheduledTick(World w, int x, int y, int z, Rng r) { }
    public virtual void OnRandomTick(World w, int x, int y, int z, Rng r) { }
    public virtual void OnEntityCollide(World w, int x, int y, int z, Entity e) { }
    public virtual void OnEntityWalk(World w, int x, int y, int z, Entity e) { }
    public virtual void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p) { }

    public virtual void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (DropOverride >= 0) { if (DropOverride > 0) drops.Add(new ItemStack(DropOverride, DropOverrideCount)); return; }
        int id = DropId(meta, r);
        if (id > 0) { int c = DropCount(r); if (c > 0) drops.Add(new ItemStack(id, c, DropMeta(meta))); }
    }
    public virtual int DropId(int meta, Rng r) => Id;
    public virtual int DropCount(Rng r) => 1;
    public virtual int DropMeta(int meta) => 0;
    public virtual ItemStack PickStack(int meta) => Item.ById[Id] != null ? new ItemStack(Id, 1, DropMeta(meta)) : null;

    // --- redstone ---
    /// <summary>Słaba moc emitowana w kierunku 'side' (od tego bloku do odpytującego).</summary>
    public virtual int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => 0;
    public virtual int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => 0;
    public virtual bool ConnectsToRedstone(int meta, int side) => IsPowerSource;

    public virtual string DisplayName(int meta) => Name;

    public bool CanHarvest(ItemStack tool)
    {
        if (HarvestLevel < 0) return true;
        var it = tool?.Item;
        if (it == null) return false;
        return it.Tool == Tool && it.ToolLevel >= HarvestLevel;
    }

    public float GetDigSpeed(ItemStack tool)
    {
        var it = tool?.Item;
        if (it == null) return 1f;
        if (it.Tool == Tool && Tool != ToolType.None) return it.ToolSpeed;
        if (it.Tool == ToolType.Sword && (Id == B.OakLeaves || Id == B.BirchLeaves)) return 1.5f;
        if (it.Tool == ToolType.Shears && (Id == B.OakLeaves || Id == B.BirchLeaves)) return 15f;
        if (it.Tool == ToolType.Shears && Id == B.Wool) return 5f;
        return 1f;
    }

    public virtual AABB? GetRenderBounds() => null;
}

public interface IBlockAccess
{
    byte GetBlock(int x, int y, int z);
    int GetMeta(int x, int y, int z);
}
