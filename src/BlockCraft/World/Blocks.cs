using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

public static partial class Blocks
{
    // 8 i 9 zajmują w GUI "Szukaj" i "Ekwipunek przetrwania"
    public const int TabBuilding = 0, TabDecoration = 1, TabRedstone = 2, TabMisc = 3, TabFood = 4, TabTools = 5, TabCombat = 6, TabMaterials = 7,
        TabTransport = 10, TabBrewing = 11;
    private static bool _init;

    public static void Init()
    {
        if (_init) return; _init = true;
        new Block(B.Air, "air", "Powietrze") { Opaque = false, Solid = false, FullCube = false, Render = RenderType.None, LightOpacity = 0, Replaceable = true, InCreative = false };
        new StoneBlock(B.Stone, "stone", "Kamień").T("stone").H(1.5f, 6).Tool_(ToolType.Pickaxe, 0);
        new GrassBlock(B.Grass, "grass", "Blok trawy").H(0.6f).Tool_(ToolType.Shovel).Snd(SoundType.Grass);
        new Block(B.Dirt, "dirt", "Ziemia").T("dirt").H(0.5f).Tool_(ToolType.Shovel).Snd(SoundType.Gravel);
        new Block(B.Cobblestone, "cobblestone", "Bruk").T("cobblestone").H(2f, 6).Tool_(ToolType.Pickaxe, 0);
        new Block(B.OakPlanks, "oak_planks", "Dębowe deski").T("planks_oak").H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood).Flam();
        new Block(B.BirchPlanks, "birch_planks", "Brzozowe deski").T("planks_birch").H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood).Flam();
        new Block(B.Bedrock, "bedrock", "Skała macierzysta").T("bedrock").H(-1f, 3600000);
        new FluidBlock(B.Water, "water", "Woda", false);
        new FluidBlock(B.Lava, "lava", "Lawa", true);
        new GravityBlock(B.Sand, "sand", "Piasek").T("sand").H(0.5f).Tool_(ToolType.Shovel).Snd(SoundType.Sand);
        new GravityBlock(B.Gravel, "gravel", "Żwir").T("gravel").H(0.6f).Tool_(ToolType.Shovel).Snd(SoundType.Gravel);
        new OreBlock(B.GoldOre, "gold_ore", "Ruda złota", 0, 0, 0).T("gold_ore").H(3f).Tool_(ToolType.Pickaxe, 2);
        new OreBlock(B.IronOre, "iron_ore", "Ruda żelaza", 0, 0, 0).T("iron_ore").H(3f).Tool_(ToolType.Pickaxe, 1);
        new OreBlock(B.CoalOre, "coal_ore", "Ruda węgla", Items.I.Coal, 1, 1).T("coal_ore").H(3f).Tool_(ToolType.Pickaxe, 0);
        new LogBlock(B.OakLog, "oak_log", "Dębowe drewno", "log_oak", "log_oak_top");
        new LogBlock(B.BirchLog, "birch_log", "Brzozowe drewno", "log_birch", "log_birch_top");
        new LeavesBlock(B.OakLeaves, "oak_leaves", "Dębowe liście", "leaves_oak", TintType.Foliage, B.OakSapling, true);
        new LeavesBlock(B.BirchLeaves, "birch_leaves", "Brzozowe liście", "leaves_birch", TintType.Birch, B.BirchSapling, false);
        new GlassBlock(B.Glass, "glass", "Szkło").T("glass").H(0.3f).Transparent().Snd(SoundType.Glass).Tab(TabBuilding);
        new OreBlock(B.LapisOre, "lapis_ore", "Ruda lazurytu", Items.I.Lapis, 4, 8).T("lapis_ore").H(3f).Tool_(ToolType.Pickaxe, 1);
        new Block(B.LapisBlock, "lapis_block", "Blok lazurytu").T("lapis_block").H(3f).Tool_(ToolType.Pickaxe, 1);
        new Block(B.Sandstone, "sandstone", "Piaskowiec").T("sandstone_top", "sandstone_bottom", "sandstone_side").H(0.8f).Tool_(ToolType.Pickaxe, 0);
        new WoolBlock(B.Wool, "wool", "Wełna").H(0.8f).Snd(SoundType.Wool).Tool_(ToolType.Shears).Flam();
        new PlantBlock(B.Dandelion, "dandelion", "Mniszek").T("flower_dandelion").Plant().Tab(TabDecoration);
        new PlantBlock(B.Poppy, "poppy", "Mak").T("flower_poppy").Plant().Tab(TabDecoration);
        new TallGrassBlock(B.TallGrass, "tall_grass", "Wysoka trawa").T("tallgrass").Plant().Tab(TabDecoration);
        new DeadBushBlock(B.DeadBush, "dead_bush", "Uschnięty krzak").T("deadbush").Plant().Tab(TabDecoration);
        new CactusBlock(B.Cactus, "cactus", "Kaktus");
        new SugarCaneBlock(B.SugarCane, "reeds", "Trzcina cukrowa").T("reeds").Plant().NotCreative();
        new SaplingBlock(B.OakSapling, "oak_sapling", "Sadzonka dębu", 0).T("sapling_oak").Plant().Tab(TabDecoration);
        new SaplingBlock(B.BirchSapling, "birch_sapling", "Sadzonka brzozy", 1).T("sapling_birch").Plant().Tab(TabDecoration);
        new Block(B.GoldBlock, "gold_block", "Blok złota").T("gold_block").H(3f, 6).Tool_(ToolType.Pickaxe, 2).Snd(SoundType.Metal);
        new Block(B.IronBlock, "iron_block", "Blok żelaza").T("iron_block").H(5f, 6).Tool_(ToolType.Pickaxe, 1).Snd(SoundType.Metal);
        new OreBlock(B.DiamondOre, "diamond_ore", "Ruda diamentu", Items.I.Diamond, 1, 1).T("diamond_ore").H(3f).Tool_(ToolType.Pickaxe, 2);
        new Block(B.DiamondBlock, "diamond_block", "Blok diamentu").T("diamond_block").H(5f, 6).Tool_(ToolType.Pickaxe, 2).Snd(SoundType.Metal);
        new CraftingTableBlock(B.CraftingTable, "crafting_table", "Stół rzemieślniczy").H(2.5f).Tool_(ToolType.Axe).Snd(SoundType.Wood).Tab(TabDecoration);
        new FurnaceBlock(B.Furnace, "furnace", "Piec", false).H(3.5f).Tool_(ToolType.Pickaxe, 0).Tab(TabDecoration);
        new FurnaceBlock(B.FurnaceLit, "lit_furnace", "Piec", true).H(3.5f).Tool_(ToolType.Pickaxe, 0).Light(13).NotCreative();
        new ChestBlock(B.Chest, "chest", "Skrzynia").H(2.5f).Tool_(ToolType.Axe).Snd(SoundType.Wood).Tab(TabDecoration);
        new TorchBlock(B.Torch, "torch", "Pochodnia", "torch", 14).Tab(TabDecoration);
        new OreBlock(B.RedstoneOre, "redstone_ore", "Ruda czerwonego kamienia", Items.I.Redstone, 4, 5).T("redstone_ore").H(3f).Tool_(ToolType.Pickaxe, 2);
        new RedstoneBlockBlock(B.RedstoneBlock, "redstone_block", "Blok czerwonego kamienia").T("redstone_block").H(5f, 6).Tool_(ToolType.Pickaxe, 0).Snd(SoundType.Metal).Tab(TabRedstone);
        new Block(B.CoalBlock, "coal_block", "Blok węgla").T("coal_block").H(5f, 6).Tool_(ToolType.Pickaxe, 0);
        new Block(B.Obsidian, "obsidian", "Obsydian").T("obsidian").H(50f, 1200).Tool_(ToolType.Pickaxe, 3);
        new Block(B.MossyCobblestone, "mossy_cobblestone", "Zamszony bruk").T("cobblestone_mossy").H(2f, 6).Tool_(ToolType.Pickaxe, 0);
        new Block(B.StoneBricks, "stonebrick", "Kamienne cegły").T("stonebrick").H(1.5f, 6).Tool_(ToolType.Pickaxe, 0);
        new FarmlandBlock(B.Farmland, "farmland", "Pole uprawne").H(0.6f).Tool_(ToolType.Shovel).Snd(SoundType.Gravel).NotCreative();
        new WheatBlock(B.Wheat, "wheat", "Pszenica").Plant(RenderType.Crops).NotCreative();
        new LadderBlock(B.Ladder, "ladder", "Drabina").Tab(TabDecoration);
        new DoorBlock(B.OakDoor, "wooden_door", "Drewniane drzwi").NotCreative();
        new FenceBlock(B.Fence, "fence", "Płot").Tab(TabDecoration);
        new TntBlock(B.Tnt, "tnt", "TNT").Tab(TabRedstone);
        new RedstoneWireBlock(B.RedstoneWire, "redstone_wire", "Czerwony proszek").NotCreative();
        new RedstoneTorchBlock(B.RedstoneTorch, "redstone_torch", "Pochodnia z czerwonego kamienia", true).Tab(TabRedstone);
        new RedstoneTorchBlock(B.RedstoneTorchOff, "unlit_redstone_torch", "Pochodnia z czerwonego kamienia", false).NotCreative();
        new LeverBlock(B.Lever, "lever", "Dźwignia").Tab(TabRedstone);
        new ButtonBlock(B.StoneButton, "stone_button", "Kamienny przycisk").Tab(TabRedstone);
        new LampBlock(B.RedstoneLamp, "redstone_lamp", "Lampa", false).Tab(TabRedstone);
        new LampBlock(B.RedstoneLampOn, "lit_redstone_lamp", "Lampa", true).NotCreative();
        new RepeaterBlock(B.Repeater, "unpowered_repeater", "Przekaźnik", false).NotCreative();
        new RepeaterBlock(B.RepeaterOn, "powered_repeater", "Przekaźnik", true).NotCreative();
        new PistonBlock(B.Piston, "piston", "Tłok", false).Tab(TabRedstone);
        new PistonBlock(B.StickyPiston, "sticky_piston", "Lepki tłok", true).Tab(TabRedstone);
        new PistonHeadBlock(B.PistonHead, "piston_head", "Głowica tłoka").NotCreative();
        new BedBlock(B.Bed, "bed", "Łóżko").NotCreative();
        new SlabBlock(B.CobbleSlab, "cobblestone_slab", "Brukowa płyta", "cobblestone", "cobblestone", B.Cobblestone).H(2f, 6).Tool_(ToolType.Pickaxe, 0);
        new SlabBlock(B.OakSlab, "oak_slab", "Dębowa płyta", "planks_oak", "planks_oak", B.OakPlanks).H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood);
        new SlabBlock(B.StoneSlab, "stone_slab", "Kamienna płyta", "stone_slab_top", "stone_slab_side", B.DoubleStoneSlab).H(2f, 6).Tool_(ToolType.Pickaxe, 0);
        new StairsBlock(B.OakStairs, "oak_stairs", "Dębowe schody", "planks_oak").H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood);
        new StairsBlock(B.CobbleStairs, "stone_stairs", "Brukowe schody", "cobblestone").H(2f, 6).Tool_(ToolType.Pickaxe, 0);
        new FenceGateBlock(B.FenceGate, "fence_gate", "Furtka").Tab(TabRedstone);
        new Block(B.Bricks, "brick_block", "Cegły").T("brick").H(2f, 6).Tool_(ToolType.Pickaxe, 0);
        new BookshelfBlock(B.Bookshelf, "bookshelf", "Biblioteczka").T("planks_oak", "planks_oak", "bookshelf").H(1.5f).Tool_(ToolType.Axe).Snd(SoundType.Wood);
        new Block(B.Glowstone, "glowstone", "Jasnogłaz").T("glowstone").H(0.3f).Light(15).Snd(SoundType.Glass).Tab(TabBuilding);
        new Block(B.Snow, "snow", "Śnieg").T("snow").H(0.2f).Tool_(ToolType.Shovel).Snd(SoundType.Wool);
        new Block(B.Ice, "ice", "Lód").T("ice").H(0.5f).Transparent(RenderLayer.Translucent, 2).Snd(SoundType.Glass).SetSlip(0.98f);
        new ClayBlock(B.Clay, "clay", "Glina").T("clay").H(0.6f).Tool_(ToolType.Shovel).Snd(SoundType.Gravel);
        new Block(B.DoubleStoneSlab, "double_stone_slab", "Podwójna kamienna płyta").T("stone_slab_top", "stone_slab_top", "stone_slab_side").H(2f, 6).Tool_(ToolType.Pickaxe, 0).NotCreative().DropAs(B.StoneSlab, 2);

        ModuleBlocks();

        for (int i = 0; i < 256; i++) if (Block.ById[i] == null) Block.ById[i] = Block.ById[0];
        for (int i = 0; i < 256; i++) { var b = Block.ById[i]; if (b.BlastResistance < 0) b.BlastResistance = b.Hardness; }
    }

    public static Block Flam(this Block b) { b.Flammable = true; return b; }
    public static Block SetSlip(this Block b, float s) { b.Slipperiness = s; return b; }
    public static Block DropAs(this Block b, int id, int count) { b.DropOverride = id; b.DropOverrideCount = count; return b; }

    public static bool IsAirOrReplaceable(int id) => Block.ById[id].Replaceable;
}

public class StoneBlock : Block
{
    public StoneBlock(byte id, string k, string n) : base(id, k, n) { }
    public override int DropId(int meta, Rng r) => B.Cobblestone;
}

public class GrassBlock : Block
{
    public GrassBlock(byte id, string k, string n) : base(id, k, n)
    {
        Tex[1] = TextureGen.Layer("grass_top"); Tex[0] = TextureGen.Layer("dirt");
        Tex[2] = Tex[3] = Tex[4] = Tex[5] = TextureGen.Layer("grass_side");
        Tint = TintType.Grass; TintMaskSides = true; RandomTicks = true;
    }
    public override int DropId(int meta, Rng r) => B.Dirt;
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        var above = Block.Get(w.GetBlock(x, y + 1, z));
        if (above.Opaque || above.IsLiquid) { if (w.GetRawLight(x, y + 1, z) < 4 || above.Opaque) { w.SetBlock(x, y, z, B.Dirt); return; } }
        if (w.GetRawLight(x, y + 1, z) >= 9)
        {
            for (int i = 0; i < 4; i++)
            {
                int tx = x + r.Next(3) - 1, ty = y + r.Next(5) - 3, tz = z + r.Next(3) - 1;
                if (w.GetBlock(tx, ty, tz) == B.Dirt && w.GetMeta(tx, ty, tz) == 0)
                {
                    var ab = Block.Get(w.GetBlock(tx, ty + 1, tz));
                    if (!ab.Opaque && !ab.IsLiquid && w.GetRawLight(tx, ty + 1, tz) >= 4) w.SetBlock(tx, ty, tz, B.Grass);
                }
            }
        }
    }
}

public class GravityBlock : Block
{
    public GravityBlock(byte id, string k, string n) : base(id, k, n) { }
    public override void OnAdded(World w, int x, int y, int z) => w.ScheduleTick(x, y, z, Id, 2);
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) => w.ScheduleTick(x, y, z, Id, 2);
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote || y <= 0) return;
        int below = w.GetBlock(x, y - 1, z);
        var bb = Block.Get(below);
        if (below == B.Air || bb.IsLiquid || (bb.Replaceable && !bb.Solid))
        {
            if (!w.IsAreaLoaded(x, z, 2)) return;
            w.SetBlock(x, y, z, B.Air);
            w.SpawnEntity(new FallingBlockEntity(w, x + 0.5, y, z + 0.5, Id, 0));
        }
    }
    public override int DropId(int meta, Rng r) => Id == B.Gravel && r.Next(10) == 0 ? Items.I.Flint : Id;
}

public class OreBlock : Block
{
    private readonly int _drop, _min, _max;
    public OreBlock(byte id, string k, string n, int drop, int min, int max) : base(id, k, n) { _drop = drop; _min = min; _max = max; }
    public override int DropId(int meta, Rng r) => _drop > 0 ? _drop : Id;
    public override int DropCount(Rng r) => _drop > 0 ? _min + r.Next(_max - _min + 1) : 1;
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    {
        int xp = Id switch { B.CoalOre => w.Rand.Next(0, 3), B.DiamondOre => w.Rand.Next(3, 8), B.LapisOre => w.Rand.Next(2, 6), B.RedstoneOre => w.Rand.Next(1, 6), _ => 0 };
        if (xp > 0 && !p.Creative) w.SpawnXp(x + 0.5, y + 0.5, z + 0.5, xp);
    }
}

public class LogBlock : Block
{
    private readonly int _side, _top;
    public LogBlock(byte id, string k, string n, string side, string top) : base(id, k, n)
    {
        _side = TextureGen.Layer(side); _top = TextureGen.Layer(top);
        H(2f); Tool_(ToolType.Axe); Snd(SoundType.Wood); Flammable = true;
    }
    // meta: 0=Y, 1=X, 2=Z
    public override int GetTexture(int face, int meta)
    {
        int axis = meta & 3;
        bool end = axis == 0 ? face <= 1 : axis == 1 ? face >= 4 : (face == 2 || face == 3);
        return end ? _top : _side;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => face <= 1 ? 0 : face >= 4 ? 1 : 2;
}

public class LeavesBlock : Block
{
    private readonly int _sapling; private readonly bool _apples;
    /// <summary>Tekstura liści w trybie szybkiej grafiki (nieprzezroczysta).</summary>
    public int OpaqueTex;
    public LeavesBlock(byte id, string k, string n, string tex, TintType tint, int sapling, bool apples) : base(id, k, n)
    {
        T(tex); Tint = tint; _sapling = sapling; _apples = apples;
        OpaqueTex = TextureGen.Has(tex + "_opaque") ? TextureGen.Layer(tex + "_opaque") : Tex[0];
        Opaque = false; Layer = RenderLayer.Cutout; LightOpacity = 1; Hardness = 0.2f; BlastResistance = 0.2f;
        Sound = SoundType.Grass; RandomTicks = true; Tab(Blocks.TabDecoration); Flammable = true;
    }
    // meta bit 4 = postawione przez gracza (nie znika)
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta) => 4;
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shears) { drops.Add(new ItemStack(Id, 1)); return; }
        if (r.Next(20) == 0) drops.Add(new ItemStack(_sapling, 1));
        if (_apples && r.Next(200) == 0) drops.Add(new ItemStack(Items.I.Apple, 1));
    }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r) => CheckDecay(w, x, y, z);
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r) => CheckDecay(w, x, y, z);

    private void CheckDecay(World w, int x, int y, int z)
    {
        if (w.IsRemote || (w.GetMeta(x, y, z) & 4) != 0) return;
        if (!w.IsAreaLoaded(x, z, 5)) return;
        // BFS przez liście do 4 bloków szukając pnia
        var q = new Queue<(int, int, int, int)>(); var seen = new HashSet<long>();
        q.Enqueue((x, y, z, 0)); seen.Add(Key(x, y, z));
        while (q.Count > 0)
        {
            var (cx, cy, cz, d) = q.Dequeue();
            for (int f = 0; f < 6; f++)
            {
                int nx = cx + Dir.X[f], ny = cy + Dir.Y[f], nz = cz + Dir.Z[f];
                int id = w.GetBlock(nx, ny, nz);
                if (id == B.OakLog || id == B.BirchLog) return;
                if ((id == B.OakLeaves || id == B.BirchLeaves) && d < 4 && seen.Add(Key(nx, ny, nz))) q.Enqueue((nx, ny, nz, d + 1));
            }
        }
        w.BreakBlock(x, y, z, true);
    }
    private static long Key(int x, int y, int z) => ((long)(x & 0xFFFFFF) << 32) | ((long)(z & 0xFFFFFF) << 8) | (uint)(y & 0xFF);
}

public class GlassBlock : Block
{
    public GlassBlock(byte id, string k, string n) : base(id, k, n) { }
    public override int DropId(int meta, Rng r) => 0;
}

public class WoolBlock : Block
{
    public static readonly string[] ColorNames = { "Biała", "Jasnoszara", "Szara", "Czarna", "Brązowa", "Różowa" };
    private readonly int[] _tex = new int[6];
    public WoolBlock(byte id, string k, string n) : base(id, k, n)
    {
        for (int i = 0; i < 6; i++) _tex[i] = TextureGen.Layer("wool_" + i);
        Tex = ModelBox.All(_tex[0]); Tab(Blocks.TabBuilding); Vars(6);
    }
    public override int GetTexture(int face, int meta) => _tex[Math.Clamp(meta, 0, 5)];
    public override int DropMeta(int meta) => meta;
    public override string DisplayName(int meta) => ColorNames[Math.Clamp(meta, 0, 5)] + " wełna";
}

public class BookshelfBlock : Block
{
    public BookshelfBlock(byte id, string k, string n) : base(id, k, n) { }
    public override int DropId(int meta, Rng r) => Items.I.Book;
    public override int DropCount(Rng r) => 3;
}

public class ClayBlock : Block
{
    public ClayBlock(byte id, string k, string n) : base(id, k, n) { }
    public override int DropId(int meta, Rng r) => Items.I.ClayBall;
    public override int DropCount(Rng r) => 4;
}

public class PlantBlock : Block
{
    public PlantBlock(byte id, string k, string n) : base(id, k, n) { }
    public virtual bool CanGrowOn(int id) => id == B.Grass || id == B.Dirt || id == B.Farmland;
    public override bool CanPlaceAt(World w, int x, int y, int z) => CanGrowOn(w.GetBlock(x, y - 1, z));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(0.3, 0, 0.3, 0.7, 0.6, 0.7));
}

public class TallGrassBlock : PlantBlock
{
    public TallGrassBlock(byte id, string k, string n) : base(id, k, n) { Tint = TintType.Grass; Replaceable = true; }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shears) { drops.Add(new ItemStack(Id, 1)); return; }
        if (r.Next(8) == 0) drops.Add(new ItemStack(Items.I.Seeds, 1));
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(0.1, 0, 0.1, 0.9, 0.8, 0.9));
}

public class DeadBushBlock : PlantBlock
{
    public DeadBushBlock(byte id, string k, string n) : base(id, k, n) { Replaceable = true; }
    public override bool CanGrowOn(int id) => id == B.Sand || id == B.Dirt || id == B.Grass;
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shears) drops.Add(new ItemStack(Id, 1));
        else drops.Add(new ItemStack(Items.I.Stick, r.Next(3)));
    }
}

public class SaplingBlock : PlantBlock
{
    private readonly int _type;
    public SaplingBlock(byte id, string k, string n, int type) : base(id, k, n) { _type = type; RandomTicks = true; }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetRawLight(x, y + 1, z) >= 9 && r.Next(7) == 0) Grow(w, x, y, z, r);
    }
    public bool Grow(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote) return false;
        int meta = w.GetMeta(x, y, z);
        if (meta == 0) { w.SetMeta(x, y, z, 1); return true; }
        w.SetBlock(x, y, z, B.Air, 0, false);
        var writer = new WorldTreeWriter(w);
        if (!TreeGen.Generate(writer, x, y, z, _type, r, false)) { w.SetBlock(x, y, z, Id, meta, false); return false; }
        return true;
    }
}

public class CactusBlock : Block
{
    public CactusBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("cactus_top", "cactus_bottom", "cactus_side");
        Opaque = false; FullCube = false; Render = RenderType.Cactus; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0.4f; BlastResistance = 0.4f; Sound = SoundType.Wool; RandomTicks = true; NeedsSupport = true; Tab(Blocks.TabDecoration);
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 15 / 16.0, 15 / 16.0));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 1, 15 / 16.0));
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        for (int h = 0; h < 4; h++)
        {
            var b = Block.Get(w.GetBlock(x + Dir.HorizX[h], y, z + Dir.HorizZ[h]));
            if (b.Solid) return false;
        }
        int below = w.GetBlock(x, y - 1, z);
        return below == B.Sand || below == B.Cactus;
    }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetBlock(x, y + 1, z) != B.Air) return;
        int h = 1; while (w.GetBlock(x, y - h, z) == B.Cactus) h++;
        if (h >= 3) return;
        int m = w.GetMeta(x, y, z);
        if (m >= 15) { w.SetMeta(x, y, z, 0, false); if (CanPlaceAt(w, x, y + 1, z)) w.SetBlock(x, y + 1, z, B.Cactus); }
        else w.SetMeta(x, y, z, m + 1, false);
    }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e)
    {
        if (e is LivingEntity le) le.Attack(DamageSource.Cactus, 1f);
        else if (e is ItemEntity ie) ie.Kill();
    }
}

public class SugarCaneBlock : Block
{
    public SugarCaneBlock(byte id, string k, string n) : base(id, k, n) { RandomTicks = true; }
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        int below = w.GetBlock(x, y - 1, z);
        if (below == B.SugarCane) return true;
        if (below != B.Grass && below != B.Dirt && below != B.Sand) return false;
        for (int h = 0; h < 4; h++) if (Block.Get(w.GetBlock(x + Dir.HorizX[h], y - 1, z + Dir.HorizZ[h])) is FluidBlock { IsLava: false }) return true;
        return false;
    }
    public override int DropId(int meta, Rng r) => Items.I.SugarCane;
    public override ItemStack PickStack(int meta) => new(Items.I.SugarCane, 1);
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetBlock(x, y + 1, z) != B.Air) return;
        int h = 1; while (w.GetBlock(x, y - h, z) == B.SugarCane) h++;
        if (h >= 3) return;
        int m = w.GetMeta(x, y, z);
        if (m >= 15) { w.SetMeta(x, y, z, 0, false); w.SetBlock(x, y + 1, z, B.SugarCane); }
        else w.SetMeta(x, y, z, m + 1, false);
    }
}

public class CraftingTableBlock : Block
{
    public CraftingTableBlock(byte id, string k, string n) : base(id, k, n)
    {
        int top = TextureGen.Layer("crafting_table_top"), side = TextureGen.Layer("crafting_table_side"), front = TextureGen.Layer("crafting_table_front");
        Tex = new[] { TextureGen.Layer("planks_oak"), top, front, front, side, side };
        Flammable = true;
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p) { p.OpenBlockGui(GuiKind.Crafting, x, y, z); return true; }
}

public class FurnaceBlock : Block
{
    private readonly bool _lit; private readonly int _front, _side, _top;
    public FurnaceBlock(byte id, string k, string n, bool lit) : base(id, k, n)
    {
        _lit = lit; _front = TextureGen.Layer(lit ? "furnace_front_on" : "furnace_front_off");
        _side = TextureGen.Layer("furnace_side"); _top = TextureGen.Layer("furnace_top");
        Tex = new[] { _top, _top, _front, _side, _side, _side }; HasTileEntity = true;
    }
    // meta = kierunek poziomy frontu (0=S,1=W,2=N,3=E)
    public override int GetTexture(int face, int meta)
    {
        if (face <= 1) return _top;
        return face == Dir.HorizToFace(meta & 3) ? _front : _side;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => (Dir.HorizFromYaw(p.Yaw) + 2) & 3;
    public override int DropId(int meta, Rng r) => B.Furnace;
    public override ItemStack PickStack(int meta) => new(B.Furnace, 1);
    public override bool OnActivated(World w, int x, int y, int z, Player p) { p.OpenBlockGui(GuiKind.Furnace, x, y, z); return true; }
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.GetTileEntity(x, y, z) is not FurnaceTileEntity) w.SetTileEntity(x, y, z, new FurnaceTileEntity());
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (FurnaceTileEntity.Swapping) return;
        if (w.GetTileEntity(x, y, z) is FurnaceTileEntity te)
        {
            if (!w.IsRemote) foreach (var s in te.Slots) if (s != null) w.DropItem(x + 0.5, y + 0.5, z + 0.5, s);
            w.RemoveTileEntity(x, y, z);
        }
    }
}

public class ChestBlock : Block
{
    private readonly int _front, _side, _top;
    public ChestBlock(byte id, string k, string n) : base(id, k, n)
    {
        _front = TextureGen.Layer("chest_front"); _side = TextureGen.Layer("chest_side"); _top = TextureGen.Layer("chest_top");
        Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; HasTileEntity = true; Flammable = true;
    }
    public override int GetTexture(int face, int meta) => face <= 1 ? _top : face == Dir.HorizToFace(meta & 3) ? _front : _side;
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 14 / 16.0, 15 / 16.0));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var t = new int[6]; for (int f = 0; f < 6; f++) t[f] = GetTexture(f, meta);
        var mb = ModelBox.Px(1, 0, 1, 15, 14, 15, t);
        mb.Uv = new float[24];
        for (int f = 0; f < 6; f++) { mb.Uv[f * 4] = 0; mb.Uv[f * 4 + 1] = 0; mb.Uv[f * 4 + 2] = 1; mb.Uv[f * 4 + 3] = 1; }
        boxes.Add(mb);
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => (Dir.HorizFromYaw(p.Yaw) + 2) & 3;
    public override bool OnActivated(World w, int x, int y, int z, Player p) { p.OpenBlockGui(GuiKind.Chest, x, y, z); return true; }
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.GetTileEntity(x, y, z) is not ChestTileEntity) w.SetTileEntity(x, y, z, new ChestTileEntity());
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (w.GetTileEntity(x, y, z) is ChestTileEntity te)
        {
            if (!w.IsRemote) foreach (var s in te.Slots) if (s != null) w.DropItem(x + 0.5, y + 0.5, z + 0.5, s);
            w.RemoveTileEntity(x, y, z);
        }
    }
}

public class TorchBlock : Block
{
    public TorchBlock(byte id, string k, string n, string tex, int light) : base(id, k, n)
    {
        T(tex); Opaque = false; Solid = false; FullCube = false; Render = RenderType.Torch; Layer = RenderLayer.Cutout;
        LightOpacity = 0; LightEmit = light; Hardness = 0; BlastResistance = 0; Sound = SoundType.Wood; NeedsSupport = true;
    }
    // meta: 0=stoi na podłodze, 1..4 = przyczepiona do ściany; 1:+X ściana po W? -> kierunek "od ściany": 1=E,2=W,3=S,4=N (jak MC)
    public static bool SolidTop(World w, int x, int y, int z)
    {
        var b = Block.Get(w.GetBlock(x, y, z));
        return b.Opaque || b.Id == B.Fence || b.Id == B.Glass || (b is SlabBlock && (w.GetMeta(x, y, z) & 1) == 1) || b is StairsBlock && (w.GetMeta(x, y, z) & 4) != 0;
    }
    public override bool CanPlaceAt(World w, int x, int y, int z)
        => SolidTop(w, x, y - 1, z) || Block.Get(w.GetBlock(x - 1, y, z)).Opaque || Block.Get(w.GetBlock(x + 1, y, z)).Opaque
           || Block.Get(w.GetBlock(x, y, z - 1)).Opaque || Block.Get(w.GetBlock(x, y, z + 1)).Opaque;
    public override bool CanStay(World w, int x, int y, int z)
    {
        int m = w.GetMeta(x, y, z);
        return m switch
        {
            1 => Block.Get(w.GetBlock(x - 1, y, z)).Opaque,
            2 => Block.Get(w.GetBlock(x + 1, y, z)).Opaque,
            3 => Block.Get(w.GetBlock(x, y, z - 1)).Opaque,
            4 => Block.Get(w.GetBlock(x, y, z + 1)).Opaque,
            _ => SolidTop(w, x, y - 1, z)
        };
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        int m = face switch { Dir.East => 1, Dir.West => 2, Dir.South => 3, Dir.North => 4, _ => 0 };
        if (m == 0 && !SolidTop(w, x, y - 1, z)) m = -1;
        bool ok = m switch
        {
            1 => Block.Get(w.GetBlock(x - 1, y, z)).Opaque,
            2 => Block.Get(w.GetBlock(x + 1, y, z)).Opaque,
            3 => Block.Get(w.GetBlock(x, y, z - 1)).Opaque,
            4 => Block.Get(w.GetBlock(x, y, z + 1)).Opaque,
            0 => true,
            _ => false
        };
        if (ok) return m;
        if (SolidTop(w, x, y - 1, z)) return 0;
        if (Block.Get(w.GetBlock(x - 1, y, z)).Opaque) return 1;
        if (Block.Get(w.GetBlock(x + 1, y, z)).Opaque) return 2;
        if (Block.Get(w.GetBlock(x, y, z - 1)).Opaque) return 3;
        return 4;
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        const double f = 0.15;
        switch (meta)
        {
            case 1: list.Add(new AABB(0, 0.2, 0.5 - f, f * 2, 0.8, 0.5 + f)); break;
            case 2: list.Add(new AABB(1 - f * 2, 0.2, 0.5 - f, 1, 0.8, 0.5 + f)); break;
            case 3: list.Add(new AABB(0.5 - f, 0.2, 0, 0.5 + f, 0.8, f * 2)); break;
            case 4: list.Add(new AABB(0.5 - f, 0.2, 1 - f * 2, 0.5 + f, 0.8, 1)); break;
            default: list.Add(new AABB(0.4, 0, 0.4, 0.6, 0.6, 0.6)); break;
        }
    }
}

public class FarmlandBlock : Block
{
    private readonly int _dry, _wet, _dirt;
    public FarmlandBlock(byte id, string k, string n) : base(id, k, n)
    {
        _dry = TextureGen.Layer("farmland_dry"); _wet = TextureGen.Layer("farmland_wet"); _dirt = TextureGen.Layer("dirt");
        Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; RandomTicks = true; Tex = ModelBox.All(_dirt);
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 15 / 16.0, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int top = meta > 0 ? _wet : _dry;
        boxes.Add(ModelBox.Px(0, 0, 0, 16, 15, 16, new[] { _dirt, top, _dirt, _dirt, _dirt, _dirt }));
    }
    public override int DropId(int meta, Rng r) => B.Dirt;
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        bool water = false;
        for (int dx = -4; dx <= 4 && !water; dx++) for (int dz = -4; dz <= 4 && !water; dz++) for (int dy = 0; dy <= 1; dy++)
                    if (w.GetBlock(x + dx, y + dy, z + dz) == B.Water) { water = true; break; }
        int m = w.GetMeta(x, y, z);
        if (water) { if (m != 7) w.SetMeta(x, y, z, 7); }
        else if (m > 0) w.SetMeta(x, y, z, m - 1);
        else if (w.GetBlock(x, y + 1, z) != B.Wheat) w.SetBlock(x, y, z, B.Dirt);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (Block.Get(w.GetBlock(x, y + 1, z)).Opaque) w.SetBlock(x, y, z, B.Dirt);
    }
}

public class WheatBlock : PlantBlock
{
    private readonly int[] _stages = new int[8];
    public WheatBlock(byte id, string k, string n) : base(id, k, n)
    {
        for (int i = 0; i < 8; i++) _stages[i] = TextureGen.Layer("wheat_" + i);
        Tex = ModelBox.All(_stages[0]); RandomTicks = true;
    }
    public override bool CanGrowOn(int id) => id == B.Farmland;
    public override int GetTexture(int face, int meta) => _stages[Math.Clamp(meta, 0, 7)];
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        int m = w.GetMeta(x, y, z);
        if (m < 7 && w.GetRawLight(x, y + 1, z) >= 9)
        {
            float chance = w.GetMeta(x, y - 1, z) > 0 ? 4f : 2f;
            if (r.Next((int)(25f / chance) + 1) == 0) w.SetMeta(x, y, z, m + 1);
        }
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (meta >= 7) { drops.Add(new ItemStack(Items.I.Wheat, 1)); int s = r.Next(4); if (s > 0) drops.Add(new ItemStack(Items.I.Seeds, s)); }
        else drops.Add(new ItemStack(Items.I.Seeds, 1));
    }
    public override ItemStack PickStack(int meta) => new(Items.I.Seeds, 1);
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(0, 0, 0, 1, 0.25 + meta * 0.1, 1));
}

public class LadderBlock : Block
{
    public LadderBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("ladder"); Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0.4f; BlastResistance = 0.4f; Climbable = true; Sound = SoundType.Ladder; Tool = ToolType.Axe; NeedsSupport = true;
    }
    // meta = strona ściany do której jest przyczepiona: 2=N(ściana z -Z),3=S,4=W,5=E (face id ściany przy której stoi)
    private static AABB Box(int m)
    {
        const double t = 0.125;
        return m switch
        {
            2 => new AABB(0, 0, 0, 1, 1, t),
            3 => new AABB(0, 0, 1 - t, 1, 1, 1),
            4 => new AABB(0, 0, 0, t, 1, 1),
            _ => new AABB(1 - t, 0, 0, 1, 1, 1)
        };
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box(meta));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var b = Box(meta);
        double e = 0.01; // płaska ściana tuż przy murze
        var mb = meta switch
        {
            2 => new ModelBox(0, 0, (float)e, 1, 1, (float)e, Tex),
            3 => new ModelBox(0, 0, 1 - (float)e, 1, 1, 1 - (float)e, Tex),
            4 => new ModelBox((float)e, 0, 0, (float)e, 1, 1, Tex),
            _ => new ModelBox(1 - (float)e, 0, 0, 1 - (float)e, 1, 1, Tex),
        };
        mb.NoCull = true;
        boxes.Add(mb);
    }
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        for (int f = 2; f < 6; f++) if (Block.Get(w.GetBlock(x + Dir.X[f], y, z + Dir.Z[f])).Opaque) return true;
        return false;
    }
    public override bool CanStay(World w, int x, int y, int z)
    {
        int m = w.GetMeta(x, y, z); if (m < 2 || m > 5) return false;
        return Block.Get(w.GetBlock(x + Dir.X[m], y, z + Dir.Z[m])).Opaque;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        if (face >= 2) { int wall = Dir.Opp[face]; if (Block.Get(w.GetBlock(x + Dir.X[wall], y, z + Dir.Z[wall])).Opaque) return wall; }
        for (int f = 2; f < 6; f++) if (Block.Get(w.GetBlock(x + Dir.X[f], y, z + Dir.Z[f])).Opaque) return f;
        return 2;
    }
}

public class FenceBlock : Block
{
    public FenceBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("planks_oak"); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; H(2f, 3); Tool = ToolType.Axe; Sound = SoundType.Wood;
    }
    public static bool Connects(IBlockAccess w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z); var b = Block.Get(id);
        return id == B.Fence || id == B.FenceGate || (b.Opaque && id != B.Tnt);
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        double x0 = 0.375, x1 = 0.625, z0 = 0.375, z1 = 0.625;
        if (Connects(w, x - 1, y, z)) x0 = 0; if (Connects(w, x + 1, y, z)) x1 = 1;
        if (Connects(w, x, y, z - 1)) z0 = 0; if (Connects(w, x, y, z + 1)) z1 = 1;
        list.Add(new AABB(x0, 0, 0.375, x1, 1.5, 0.625));
        list.Add(new AABB(0.375, 0, z0, 0.625, 1.5, z1));
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        double x0 = 0.375, x1 = 0.625, z0 = 0.375, z1 = 0.625;
        if (Connects(w, x - 1, y, z)) x0 = 0; if (Connects(w, x + 1, y, z)) x1 = 1;
        if (Connects(w, x, y, z - 1)) z0 = 0; if (Connects(w, x, y, z + 1)) z1 = 1;
        list.Add(new AABB(x0, 0, z0, x1, 1, z1));
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        boxes.Add(ModelBox.Px(6, 0, 6, 10, 16, 10, Tex));
        void Arm(int dx, int dz)
        {
            float ax0 = dx < 0 ? 0 : dx > 0 ? 10 : 7, ax1 = dx < 0 ? 6 : dx > 0 ? 16 : 9;
            float az0 = dz < 0 ? 0 : dz > 0 ? 10 : 7, az1 = dz < 0 ? 6 : dz > 0 ? 16 : 9;
            boxes.Add(ModelBox.Px(ax0, 12, az0, ax1, 15, az1, Tex));
            boxes.Add(ModelBox.Px(ax0, 6, az0, ax1, 9, az1, Tex));
        }
        if (Connects(w, x - 1, y, z)) Arm(-1, 0); if (Connects(w, x + 1, y, z)) Arm(1, 0);
        if (Connects(w, x, y, z - 1)) Arm(0, -1); if (Connects(w, x, y, z + 1)) Arm(0, 1);
    }
}

public class FenceGateBlock : Block
{
    public FenceGateBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("planks_oak"); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; H(2f, 3); Tool = ToolType.Axe; Sound = SoundType.Wood;
    }
    // meta: bity 0-1 kierunek (0=S..), bit 2 = otwarta
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta) => Dir.HorizFromYaw(p.Yaw);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        if ((meta & 4) != 0) return;
        if ((meta & 1) == 0) list.Add(new AABB(0, 0, 0.375, 1, 1.5, 0.625)); else list.Add(new AABB(0.375, 0, 0, 0.625, 1.5, 1));
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        if ((meta & 1) == 0) list.Add(new AABB(0, 0, 0.375, 1, 1, 0.625)); else list.Add(new AABB(0.375, 0, 0, 0.625, 1, 1));
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        bool open = (meta & 4) != 0; bool alongX = (meta & 1) == 0;
        void Box(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            if (alongX) boxes.Add(ModelBox.Px(x0, y0, z0, x1, y1, z1, Tex)); else boxes.Add(ModelBox.Px(z0, y0, x0, z1, y1, x1, Tex));
        }
        Box(0, 5, 7, 2, 16, 9); Box(14, 5, 7, 16, 16, 9);
        if (!open)
        {
            Box(6, 6, 7, 8, 15, 9); Box(8, 6, 7, 10, 15, 9);
            Box(2, 6, 7, 6, 9, 9); Box(2, 12, 7, 6, 15, 9); Box(10, 6, 7, 14, 9, 9); Box(10, 12, 7, 14, 15, 9);
        }
        else
        {
            int dir = (meta & 2) == 0 ? 1 : -1;
            float zA = dir > 0 ? 9 : 1, zB = dir > 0 ? 15 : 7;
            Box(0, 6, zA, 2, 15, zB); Box(14, 6, zA, 16, 15, zB);
            Box(0, 6, zB - (dir > 0 ? 2 : 6), 2, 9, zB - (dir > 0 ? 0 : 4)); Box(14, 6, zB - (dir > 0 ? 2 : 6), 16, 9, zB - (dir > 0 ? 0 : 4));
        }
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 4) != 0) m &= ~4;
        else
        {
            int face = Dir.HorizFromYaw(p.Yaw);
            m = (m & ~3) | ((face == ((m & 3) + 2) % 4) ? (m & 3) ^ 2 : (m & 3)) | 4;
        }
        w.SetMeta(x, y, z, m);
        w.PlaySound("door_" + ((m & 4) != 0 ? "open" : "close"), x + 0.5, y + 0.5, z + 0.5);
        return true;
    }
}

public class SlabBlock : Block
{
    private readonly int _double;
    public SlabBlock(byte id, string k, string n, string top, string side, int doubleId) : base(id, k, n)
    {
        T(top, top, side); _double = doubleId; Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0;
    }
    public int DoubleId => _double;
    // meta: 0 = dolna, 1 = górna
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(meta == 1 ? new AABB(0, 0.5, 0, 1, 1, 1) : new AABB(0, 0, 0, 1, 0.5, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        => boxes.Add(meta == 1 ? new ModelBox(0, 0.5f, 0, 1, 1, 1, Tex) : new ModelBox(0, 0, 0, 1, 0.5f, 1, Tex));
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => face == Dir.Down || (face != Dir.Up && hy > 0.5) ? 1 : 0;
}

public class StairsBlock : Block
{
    public StairsBlock(byte id, string k, string n, string tex) : base(id, k, n)
    {
        T(tex); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0;
    }
    // meta: bity 0-1 kierunek "wejścia" (strona wysoka): 0=+Z,1=-X,2=-Z,3=+X ; bit 2 = odwrócone
    private static void Boxes(int meta, Action<double, double, double, double, double, double> add)
    {
        bool up = (meta & 4) != 0; int d = meta & 3;
        if (up) add(0, 0.5, 0, 1, 1, 1); else add(0, 0, 0, 1, 0.5, 1);
        double y0 = up ? 0 : 0.5, y1 = up ? 0.5 : 1;
        switch (d)
        {
            case 0: add(0, y0, 0.5, 1, y1, 1); break;
            case 1: add(0, y0, 0, 0.5, y1, 1); break;
            case 2: add(0, y0, 0, 1, y1, 0.5); break;
            default: add(0.5, y0, 0, 1, y1, 1); break;
        }
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => Boxes(meta, (a, b, c, d, e, f) => list.Add(new AABB(a, b, c, d, e, f)));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        => Boxes(meta, (a, b, c, d, e, f) => boxes.Add(new ModelBox((float)a, (float)b, (float)c, (float)d, (float)e, (float)f, Tex)));
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        int d = Dir.HorizFromYaw(p.Yaw); // patrzy w kierunku d -> wysoka strona dalej od gracza
        bool up = face == Dir.Down || (face != Dir.Up && hy > 0.5);
        return d | (up ? 4 : 0);
    }
}

public class BedBlock : Block
{
    private readonly int _headTop, _footTop, _headSide, _footSide, _headEnd, _footEnd, _planks;
    public BedBlock(byte id, string k, string n) : base(id, k, n)
    {
        _headTop = TextureGen.Layer("bed_head_top"); _footTop = TextureGen.Layer("bed_feet_top");
        _headSide = TextureGen.Layer("bed_head_side"); _footSide = TextureGen.Layer("bed_feet_side");
        _headEnd = TextureGen.Layer("bed_head_end"); _footEnd = TextureGen.Layer("bed_feet_end"); _planks = TextureGen.Layer("planks_oak");
        Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; Hardness = 0.2f; BlastResistance = 0.2f; Sound = SoundType.Wood; Tex = ModelBox.All(_planks);
    }
    // meta: bity 0-1 kierunek (od nóg do głowy: 0=+Z,1=-X,2=-Z,3=+X), bit 3 = głowa
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 9 / 16.0, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        bool head = (meta & 8) != 0; int d = meta & 3;
        int top = head ? _headTop : _footTop, side = head ? _headSide : _footSide, end = head ? _headEnd : _footEnd;
        int headFace = Dir.HorizToFace(d);
        var t = new int[6];
        t[0] = _planks; t[1] = top;
        for (int f = 2; f < 6; f++) t[f] = side;
        if (head) t[headFace] = end; else t[Dir.Opp[headFace]] = end;
        if (head) t[Dir.Opp[headFace]] = side;
        var mb = ModelBox.Px(0, 3, 0, 16, 9, 16, t);
        mb.UvRot = new int[6];
        mb.UvRot[1] = d switch { 0 => 0, 1 => 1, 2 => 2, _ => 3 };
        mb.Uv = new float[24];
        for (int f = 0; f < 6; f++) { mb.Uv[f * 4] = 0; mb.Uv[f * 4 + 1] = f <= 1 ? 0 : 7 / 16f; mb.Uv[f * 4 + 2] = 1; mb.Uv[f * 4 + 3] = f <= 1 ? 1 : 13 / 16f; }
        if (head) { int opp = Dir.Opp[headFace]; mb.Uv[opp * 4 + 1] = 0; mb.Uv[opp * 4 + 3] = 0; }
        boxes.Add(mb);
        // nóżki
        var leg = ModelBox.All(_planks);
        boxes.Add(ModelBox.Px(0, 0, 0, 3, 3, 3, leg)); boxes.Add(ModelBox.Px(13, 0, 0, 16, 3, 3, leg));
        boxes.Add(ModelBox.Px(0, 0, 13, 3, 3, 16, leg)); boxes.Add(ModelBox.Px(13, 0, 13, 16, 3, 16, leg));
    }
    public override int DropId(int meta, Rng r) => (meta & 8) != 0 ? Items.I.Bed : 0;
    public override ItemStack PickStack(int meta) => new(Items.I.Bed, 1);
    public static (int, int, int) Other(int x, int y, int z, int meta)
    {
        int d = meta & 3; int s = (meta & 8) != 0 ? -1 : 1;
        return (x + Dir.HorizX[d] * s, y, z + Dir.HorizZ[d] * s);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        var (ox, oy, oz) = Other(x, y, z, w.GetMeta(x, y, z));
        if (w.GetBlock(ox, oy, oz) != B.Bed) w.SetBlock(x, y, z, B.Air);
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        if (w.IsRemote) return true;
        int meta = w.GetMeta(x, y, z);
        if ((meta & 8) == 0) { var o = Other(x, y, z, meta); x = o.Item1; z = o.Item3; }
        p.SpawnX = x; p.SpawnY = y + 1; p.SpawnZ = z; p.HasSpawn = true;
        if (!w.IsNight()) { p.SendMessage("§eMożesz spać tylko w nocy. Ustawiono punkt odrodzenia."); return true; }
        if (w.HasMonstersNear(x, y, z, 8)) { p.SendMessage("§cNie możesz teraz odpocząć, w pobliżu są potwory."); return true; }
        p.StartSleeping(x, y, z);
        return true;
    }
}

public class TntBlock : Block
{
    public TntBlock(byte id, string k, string n) : base(id, k, n) { T("tnt_top", "tnt_bottom", "tnt_side"); Hardness = 0; BlastResistance = 0; Sound = SoundType.Grass; }
    public override void OnAdded(World w, int x, int y, int z) { if (Redstone.IsBlockPowered(w, x, y, z)) Ignite(w, x, y, z, 80); }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) { if (Redstone.IsBlockPowered(w, x, y, z)) Ignite(w, x, y, z, 80); }
    public static void Ignite(World w, int x, int y, int z, int fuse)
    {
        if (w.IsRemote) return;
        w.SetBlock(x, y, z, B.Air);
        w.SpawnEntity(new TntEntity(w, x + 0.5, y, z + 0.5, fuse));
        w.PlaySound("fuse", x + 0.5, y + 0.5, z + 0.5);
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        var held = p.Inventory.Selected;
        if (held != null && held.Id == Items.I.FlintAndSteel)
        {
            Ignite(w, x, y, z, 80); p.DamageHeld(1); return true;
        }
        return false;
    }
}

public class DoorBlock : Block
{
    private readonly int _lower, _upper;
    public DoorBlock(byte id, string k, string n) : base(id, k, n)
    {
        _lower = TextureGen.Layer("door_wood_lower"); _upper = TextureGen.Layer("door_wood_upper");
        Tex = ModelBox.All(_lower); Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 3f; BlastResistance = 3f; Tool = ToolType.Axe; Sound = SoundType.Wood;
    }
    // dolna: bity 0-1 kierunek (0=+Z..), bit 2 = otwarte ; górna: bit 3 = górna, bit 0 = zawias prawy, bit 1 = zasilone
    public static int FullMeta(IBlockAccess w, int x, int y, int z)
    {
        int m = w.GetMeta(x, y, z);
        bool top = (m & 8) != 0;
        int lower = top ? w.GetMeta(x, y - 1, z) : m;
        int upper = top ? m : w.GetMeta(x, y + 1, z);
        return (lower & 7) | (top ? 8 : 0) | ((upper & 1) << 4);
    }
    private static AABB Box(int full)
    {
        const double t = 3 / 16.0;
        int d = full & 3; bool open = (full & 4) != 0; bool hingeR = (full & 16) != 0;
        // zamknięte: drzwi wzdłuż krawędzi w zależności od kierunku
        int side = d;
        if (open) side = hingeR ? (d + 3) & 3 : (d + 1) & 3;
        return side switch
        {
            0 => new AABB(0, 0, 0, t, 1, 1),        // przy ścianie -X
            1 => new AABB(0, 0, 0, 1, 1, t),        // przy -Z
            2 => new AABB(1 - t, 0, 0, 1, 1, 1),    // przy +X
            _ => new AABB(0, 0, 1 - t, 1, 1, 1)     // przy +Z
        };
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box(FullMeta(w, x, y, z)));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var b = Box(FullMeta(w, x, y, z));
        int tex = (meta & 8) != 0 ? _upper : _lower;
        var mb = new ModelBox((float)b.MinX, (float)b.MinY, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, ModelBox.All(tex));
        mb.Uv = new float[24];
        for (int f = 0; f < 6; f++) { mb.Uv[f * 4] = 0; mb.Uv[f * 4 + 1] = 0; mb.Uv[f * 4 + 2] = f <= 1 ? 3 / 16f : 1; mb.Uv[f * 4 + 3] = 1; }
        // krawędzie: wąski pasek
        for (int f = 0; f < 6; f++)
        {
            double w0 = f <= 1 ? 1 : (f == 2 || f == 3) ? b.MaxX - b.MinX : b.MaxZ - b.MinZ;
            if (w0 < 0.5) { mb.Uv[f * 4] = 0; mb.Uv[f * 4 + 2] = 3 / 16f; }
        }
        boxes.Add(mb);
    }
    public override int DropId(int meta, Rng r) => (meta & 8) != 0 ? 0 : Items.I.Door;
    public override ItemStack PickStack(int meta) => new(Items.I.Door, 1);
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z);
        int ly = (m & 8) != 0 ? y - 1 : y;
        int lm = w.GetMeta(x, ly, z) ^ 4;
        w.SetMeta(x, ly, z, lm);
        w.MarkDirty(x, ly + 1, z);
        w.PlaySound((lm & 4) != 0 ? "door_open" : "door_close", x + 0.5, y + 0.5, z + 0.5);
        return true;
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 8) != 0)
        {
            if (w.GetBlock(x, y - 1, z) != B.OakDoor) w.SetBlock(x, y, z, B.Air);
            return;
        }
        if (w.GetBlock(x, y + 1, z) != B.OakDoor) { w.BreakBlock(x, y, z, true); return; }
        if (!Block.Get(w.GetBlock(x, y - 1, z)).Opaque) { w.BreakBlock(x, y, z, true); return; }
        bool powered = Redstone.IsBlockPowered(w, x, y, z) || Redstone.IsBlockPowered(w, x, y + 1, z);
        int um = w.GetMeta(x, y + 1, z);
        bool wasPowered = (um & 2) != 0;
        if (powered != wasPowered)
        {
            w.SetMeta(x, y + 1, z, (um & ~2) | (powered ? 2 : 0), false);
            bool open = (m & 4) != 0;
            if (open != powered)
            {
                w.SetMeta(x, y, z, m ^ 4);
                w.MarkDirty(x, y + 1, z);
                w.PlaySound(powered ? "door_open" : "door_close", x + 0.5, y + 0.5, z + 0.5);
            }
        }
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if ((oldMeta & 8) != 0) { if (w.GetBlock(x, y - 1, z) == B.OakDoor) w.SetBlock(x, y - 1, z, B.Air); }
        else if (w.GetBlock(x, y + 1, z) == B.OakDoor) w.SetBlock(x, y + 1, z, B.Air);
    }
}
