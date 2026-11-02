using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>Rejestracja bloków modułu OW. Istniejące bloki z wariantami (kamień, ziemia, wełna, kwiaty...) są tu
/// rejestrowane ponownie klasami OW (dziedziczą po bazowych), więc wspólna lista w Blocks.cs zostaje bez zmian.</summary>
public static partial class Blocks
{
    public static readonly string[] NamesM = { "Biały", "Pomarańczowy", "Karmazynowy", "Jasnoniebieski", "Żółty", "Limonkowy", "Różowy", "Szary",
        "Jasnoszary", "Błękitny", "Fioletowy", "Niebieski", "Brązowy", "Zielony", "Czerwony", "Czarny" };

    static partial void BlocksOverworld()
    {
        const int Bld = TabBuilding, Deco = TabDecoration, Red = TabRedstone, Food = TabFood, Mats = TabMaterials;

        // ---- warianty istniejących bloków ----
        new VariantBlock(B.Stone, "stone", new[] { "Kamień", "Granit", "Polerowany granit", "Dioryt", "Polerowany dioryt", "Andezyt", "Polerowany andezyt" },
            new[] { "stone", "stone_granite", "stone_granite_smooth", "stone_diorite", "stone_diorite_smooth", "stone_andesite", "stone_andesite_smooth" }) { DropZeroAs = B.Cobblestone }
            .H(1.5f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new DirtVariantBlock(B.Dirt, "dirt", "Ziemia").H(0.5f).Tool_(ToolType.Shovel).Snd(SoundType.Gravel).Tab(Bld);
        new SandVariantBlock(B.Sand, "sand", "Piasek").H(0.5f).Tool_(ToolType.Shovel).Snd(SoundType.Sand).Tab(Bld);
        new VariantBlock(B.Sandstone, "sandstone", new[] { "Piaskowiec", "Rzeźbiony piaskowiec", "Gładki piaskowiec" },
            new[] { "sandstone_side", "sandstone_carved", "sandstone_smooth" }, "sandstone_top", "sandstone_bottom").H(0.8f).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new VariantBlock(B.RedSandstone, "red_sandstone", new[] { "Czerwony piaskowiec", "Rzeźbiony czerwony piaskowiec", "Gładki czerwony piaskowiec" },
            new[] { "red_sandstone_normal", "red_sandstone_carved", "red_sandstone_smooth" }, "red_sandstone_top", "red_sandstone_bottom").H(0.8f).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new VariantBlock(B.StoneBricks, "stonebrick", new[] { "Kamienne cegły", "Zamszone kamienne cegły", "Popękane kamienne cegły", "Rzeźbione kamienne cegły" },
            new[] { "stonebrick", "stonebrick_mossy", "stonebrick_cracked", "stonebrick_carved" }).H(1.5f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new ColoredBlock(B.Wool, "wool", Dyes.NamesF, " wełna", "wool_colored_").H(0.8f).Snd(SoundType.Wool).Tool_(ToolType.Shears).Flam().Tab(Bld);
        new OwFlowerBlock(B.Poppy, "red_flower", new[] { "Mak", "Niebieska orchidea", "Czosnek", "Houstonia błękitna", "Czerwony tulipan", "Pomarańczowy tulipan",
            "Biały tulipan", "Różowy tulipan", "Złocień" }, new[] { "flower_poppy", "flower_blue_orchid", "flower_allium", "flower_houstonia", "flower_tulip_red",
            "flower_tulip_orange", "flower_tulip_white", "flower_tulip_pink", "flower_oxeye_daisy" }).Tab(Deco);
        new OwFlowerBlock(B.Dandelion, "yellow_flower", new[] { "Mniszek" }, new[] { "flower_dandelion" }).Tab(Deco);
        new OwTallGrassBlock(B.TallGrass, "tallgrass", "Wysoka trawa").Plant().Tab(Deco);
        new OwDeadBushBlock(B.DeadBush, "deadbush", "Uschnięty krzak").T("deadbush").Plant().Tab(Deco);
        new OwGrassBlock(B.Grass, "grass", "Blok trawy").H(0.6f).Tool_(ToolType.Shovel).Snd(SoundType.Grass).Tab(Bld);

        // ---- drewna (kolejność MC: dąb, świerk, brzoza, dżungla, akacja, ciemny dąb) ----
        byte[] planks = { B.OakPlanks, B.SprucePlanks, B.BirchPlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks };
        byte[] logs = { B.OakLog, B.SpruceLog, B.BirchLog, B.JungleLog, B.AcaciaLog, B.DarkOakLog };
        byte[] leaves = { B.OakLeaves, B.SpruceLeaves, B.BirchLeaves, B.JungleLeaves, B.AcaciaLeaves, B.DarkOakLeaves };
        byte[] saplings = { B.OakSapling, B.SpruceSapling, B.BirchSapling, B.JungleSapling, B.AcaciaSapling, B.DarkOakSapling };
        byte[] slabs = { B.OakSlab, B.SpruceSlab, B.BirchSlab, B.JungleSlab, B.AcaciaSlab, B.DarkOakSlab };
        byte[] stairs = { B.OakStairs, B.SpruceStairs, B.BirchStairs, B.JungleStairs, B.AcaciaStairs, B.DarkOakStairs };
        byte[] fences = { B.Fence, B.SpruceFence, B.BirchFence, B.JungleFence, B.AcaciaFence, B.DarkOakFence };
        byte[] gates = { B.FenceGate, B.SpruceFenceGate, B.BirchFenceGate, B.JungleFenceGate, B.AcaciaFenceGate, B.DarkOakFenceGate };
        byte[] doors = { B.OakDoor, B.SpruceDoor, B.BirchDoor, B.JungleDoor, B.AcaciaDoor, B.DarkOakDoor };
        int[] doorItems = { I.Door, I.SpruceDoorItem, I.BirchDoorItem, I.JungleDoorItem, I.AcaciaDoorItem, I.DarkOakDoorItem };
        string[] keys = { "oak", "spruce", "birch", "jungle", "acacia", "dark_oak" };
        string[] adjP = { "Dębowe", "Świerkowe", "Brzozowe", "Dżunglowe", "Akacjowe", "" };
        string[] adjF = { "Dębowa", "Świerkowa", "Brzozowa", "Dżunglowa", "Akacjowa", "" };
        string[] adjM = { "Dębowy", "Świerkowy", "Brzozowy", "Dżunglowy", "Akacjowy", "" };
        string[] treeN = { "dębu", "świerku", "brzozy", "drzewa dżunglowego", "akacji", "ciemnego dębu" };
        string[] leafTex = { "leaves_oak", "leaves_spruce", "leaves_birch", "leaves_jungle", "leaves_acacia", "leaves_big_oak" };
        TintType[] leafTint = { TintType.Foliage, TintType.Spruce, TintType.Birch, TintType.Foliage, TintType.Foliage, TintType.Foliage };
        string[] doorTex = { "wood", "spruce", "birch", "jungle", "acacia", "dark_oak" };
        for (int t = 0; t < 6; t++)
        {
            string wn = TextureGen.WoodNames[t], k = keys[t];
            string P(string a, string noun) => t == 5 ? noun + " z ciemnego dębu" : a + " " + noun.ToLowerInvariant();
            string pl = "planks_" + wn;
            if (t != 0 && t != 2) new Block(planks[t], k + "_planks", P(adjP[t], "Deski")).T(pl).H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood).Flam().Tab(Bld);
            new OwLogBlock(logs[t], k + "_log", t == 5 ? "Drewno z ciemnego dębu" : adjP[t] + " drewno", "log_" + wn, "log_" + wn + "_top").Tab(Bld);
            new OwLeavesBlock(leaves[t], k + "_leaves", t == 5 ? "Liście ciemnego dębu" : adjP[t] + " liście", leafTex[t], leafTint[t], saplings[t], t == 0 || t == 5, t == 3 ? 40 : 20);
            new OwSaplingBlock(saplings[t], k + "_sapling", "Sadzonka " + treeN[t], t).T(t == 5 ? "sapling_roofed_oak" : "sapling_" + wn).Plant().Tab(Deco);
            if (t != 0)
            {
                new SlabBlock(slabs[t], k + "_slab", P(adjF[t], "Płyta"), pl, pl, planks[t]).H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood).Flam().Tab(Bld);
                new StairsBlock(stairs[t], k + "_stairs", P(adjP[t], "Schody"), pl).H(2f, 3).Tool_(ToolType.Axe).Snd(SoundType.Wood).Flam().Tab(Bld);
            }
            new OwFenceBlock(fences[t], t == 0 ? "fence" : k + "_fence", P(adjM[t], "Płot"), pl).Flam().Tab(Deco);
            new OwFenceGateBlock(gates[t], t == 0 ? "fence_gate" : k + "_fence_gate", P(adjF[t], "Furtka"), pl).Flam().Tab(Red);
            new OwDoorBlock(doors[t], t == 0 ? "wooden_door" : k + "_door", t == 5 ? "Drzwi z ciemnego dębu" : (t == 0 ? "Drewniane" : adjP[t]) + " drzwi",
                "door_" + doorTex[t] + "_lower", "door_" + doorTex[t] + "_upper", doorItems[t]).NotCreative();
        }

        // ---- piaskowce, cegły: płyty i schody ----
        new SlabBlock(B.SandstoneSlab, "sandstone_slab", "Płyta z piaskowca", "sandstone_top", "sandstone_side", B.Sandstone).H(2f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new SlabBlock(B.RedSandstoneSlab, "red_sandstone_slab", "Płyta z czerwonego piaskowca", "red_sandstone_top", "red_sandstone_normal", B.RedSandstone).H(2f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new SlabBlock(B.StoneBrickSlab, "stone_brick_slab", "Płyta z kamiennych cegieł", "stonebrick", "stonebrick", B.StoneBricks).H(2f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new SlabBlock(B.BrickSlab, "brick_slab", "Ceglana płyta", "brick", "brick", B.Bricks).H(2f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new StairsBlock(B.SandstoneStairs, "sandstone_stairs", "Schody z piaskowca", "sandstone_side").T("sandstone_top", "sandstone_bottom", "sandstone_side").H(0.8f).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new StairsBlock(B.RedSandstoneStairs, "red_sandstone_stairs", "Schody z czerwonego piaskowca", "red_sandstone_normal").T("red_sandstone_top", "red_sandstone_bottom", "red_sandstone_normal").H(0.8f).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new StairsBlock(B.StoneBrickStairs, "stone_brick_stairs", "Schody z kamiennych cegieł", "stonebrick").H(1.5f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new StairsBlock(B.BrickStairs, "brick_stairs", "Ceglane schody", "brick").H(2f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);

        // ---- rośliny ----
        new SpongeBlock(B.Sponge, "sponge", "Gąbka").H(0.6f).Snd(SoundType.Grass).Tab(Bld);
        new WebBlock(B.Cobweb, "web", "Pajęczyna").Tab(Deco);
        new DoublePlantBlock(B.DoublePlant, "double_plant", "Podwójna roślina").Tab(Deco);
        new OwMushroomBlock(B.BrownMushroom, "brown_mushroom", "Brązowy grzyb", "mushroom_brown").Light(1).Tab(Deco);
        new OwMushroomBlock(B.RedMushroom, "red_mushroom", "Czerwony grzyb", "mushroom_red").Tab(Deco);
        new HugeMushroomBlock(B.BrownMushroomBlock, "brown_mushroom_block", "Blok brązowego grzyba", "mushroom_block_skin_brown", B.BrownMushroom).Tab(Deco);
        new HugeMushroomBlock(B.RedMushroomBlock, "red_mushroom_block", "Blok czerwonego grzyba", "mushroom_block_skin_red", B.RedMushroom).Tab(Deco);
        new MyceliumBlock(B.Mycelium, "mycelium", "Grzybnia").H(0.6f).Tool_(ToolType.Shovel).Snd(SoundType.Grass).Tab(Bld);
        new VineBlock(B.Vine, "vine", "Pnącza").Tab(Deco);
        new LilyPadBlock(B.LilyPad, "waterlily", "Lilia wodna").Tab(Deco);
        new CocoaBlock(B.Cocoa, "cocoa", "Kakao").NotCreative();
        new PumpkinBlock(B.Pumpkin, "pumpkin", "Dynia", false).Tab(Bld);
        new PumpkinBlock(B.JackOLantern, "lit_pumpkin", "Świecąca dynia", true).Light(15).Tab(Bld);
        new MelonBlock(B.Melon, "melon_block", "Arbuz").Tab(Bld);
        new StemBlock(B.PumpkinStem, "pumpkin_stem", "Łodyga dyni", B.Pumpkin, I.PumpkinSeeds).NotCreative();
        new StemBlock(B.MelonStem, "melon_stem", "Łodyga arbuza", B.Melon, I.MelonSeeds).NotCreative();
        new OwCropBlock(B.Carrots, "carrots", "Marchewki", "carrots_stage_", I.Carrot, false).NotCreative();
        new OwCropBlock(B.Potatoes, "potatoes", "Ziemniaki", "potatoes_stage_", I.Potato, true).NotCreative();

        // ---- śnieg, lód ----
        new SnowLayerBlock(B.SnowLayer, "snow_layer", "Śnieg").Tab(Deco);
        new SnowBlockOw(B.Snow, "snow", "Blok śniegu").T("snow").H(0.2f).Tool_(ToolType.Shovel, 0).Snd(SoundType.Wool).Tab(Bld);
        new IceBlockOw(B.Ice, "ice", "Lód").T("ice").H(0.5f).Transparent(RenderLayer.Translucent, 3).Snd(SoundType.Glass).SetSlip(0.98f).Tab(Bld);
        new NoDropBlock(B.PackedIce, "packed_ice", "Zbity lód").T("ice_packed").H(0.5f).Snd(SoundType.Glass).SetSlip(0.98f).Tab(Bld);

        // ---- budulce ----
        new Block(B.HardenedClay, "hardened_clay", "Utwardzona glina").T("hardened_clay").H(1.25f, 4.2f).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new ColoredBlock(B.StainedClay, "stained_hardened_clay", Dyes.NamesF, " utwardzona glina", "hardened_clay_stained_").H(1.25f, 4.2f).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new WallBlock(B.CobbleWall, "cobblestone_wall").Tab(Deco);
        new PaneBlock(B.GlassPane, "glass_pane", "Szyba", "glass", false).Tab(Deco);
        new StainedGlassBlock(B.StainedGlass, "stained_glass").Tab(Bld);
        new PaneBlock(B.StainedGlassPane, "stained_glass_pane", "Szyba", "glass_", true).Tab(Deco);
        new PaneBlock(B.IronBars, "iron_bars", "Żelazne kraty", "iron_bars", false).Tab(Deco);
        new CarpetBlock(B.Carpet, "carpet").Tab(Deco);
        new HayBlock(B.HayBale, "hay_block", "Snopek siana").Tab(Bld);
        new VariantBlock(B.Prismarine, "prismarine", new[] { "Pryzmaryn", "Pryzmarynowe cegły", "Ciemny pryzmaryn" },
            new[] { "prismarine_rough", "prismarine_bricks", "prismarine_dark" }).H(1.5f, 6).Tool_(ToolType.Pickaxe, 0).Tab(Bld);
        new SeaLanternBlock(B.SeaLantern, "sea_lantern", "Latarnia morska").T("sea_lantern").H(0.3f).Light(15).Snd(SoundType.Glass).Tab(Bld);
        new EmeraldOreBlock(B.EmeraldOre, "emerald_ore", "Ruda szmaragdu").T("emerald_ore").H(3f).Tool_(ToolType.Pickaxe, 2).Tab(Bld);
        new Block(B.EmeraldBlock, "emerald_block", "Blok szmaragdu").T("emerald_block").H(5f, 6).Tool_(ToolType.Pickaxe, 2).Snd(SoundType.Metal).Tab(Bld);

        Block.ById[B.Water].Tint = TintType.Water;
    }
}

// ======================= bloki z wariantami =======================

/// <summary>Blok z wariantami meta: nazwa i tekstura boku per meta, opcjonalnie wspólna góra/spód.</summary>
public class VariantBlock : Block
{
    protected readonly string[] Names; protected readonly int[] Sides; private readonly int _top = -1, _bottom = -1;
    /// <summary>Meta 0 wypada jako ten blok (np. kamień -> bruk).</summary>
    public int DropZeroAs = -1;
    public VariantBlock(byte id, string k, string[] names, string[] sides, string top = null, string bottom = null) : base(id, k, names[0])
    {
        Names = names; Sides = sides.Select(TextureGen.Layer).ToArray();
        if (top != null) _top = TextureGen.Layer(top);
        if (bottom != null) _bottom = TextureGen.Layer(bottom);
        Tex = ModelBox.All(Sides[0]); if (_top >= 0) Tex[1] = _top; if (_bottom >= 0) Tex[0] = _bottom;
        Vars(names.Length);
    }
    public override int GetTexture(int face, int meta)
    {
        int m = meta < Sides.Length ? meta : 0;
        if (face == 1 && _top >= 0) return _top;
        if (face == 0 && _bottom >= 0) return m == 0 ? _bottom : _top >= 0 ? _top : _bottom;
        return Sides[m];
    }
    public override int DropId(int meta, Rng r) => meta == 0 && DropZeroAs >= 0 ? DropZeroAs : Id;
    public override int DropMeta(int meta) => meta == 0 && DropZeroAs >= 0 ? 0 : meta < Sides.Length ? meta : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, meta < Sides.Length ? meta : 0);
    public override string DisplayName(int meta) => Names[meta < Names.Length ? meta : 0];
}

/// <summary>Ziemia: 0 zwykła, 1 gruboziarnista, 2 bielica (bielica wypada jako ziemia).</summary>
public sealed class DirtVariantBlock : Block
{
    private readonly int _dirt, _coarse, _podTop, _podSide;
    private static readonly string[] N = { "Ziemia", "Gruboziarnista ziemia", "Bielica" };
    public DirtVariantBlock(byte id, string k, string n) : base(id, k, n)
    {
        _dirt = TextureGen.Layer("dirt"); _coarse = TextureGen.Layer("coarse_dirt"); _podTop = TextureGen.Layer("dirt_podzol_top"); _podSide = TextureGen.Layer("dirt_podzol_side");
        Tex = ModelBox.All(_dirt); Vars(3);
    }
    public override int GetTexture(int face, int meta) => meta switch { 1 => _coarse, 2 => face == 1 ? _podTop : face == 0 ? _dirt : _podSide, _ => _dirt };
    public override int DropMeta(int meta) => meta == 1 ? 1 : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, meta is >= 0 and <= 2 ? meta : 0);
    public override string DisplayName(int meta) => N[meta is >= 0 and <= 2 ? meta : 0];
}

/// <summary>Piasek z wariantem czerwonym (meta 1), spada zachowując meta.</summary>
public sealed class SandVariantBlock : GravityBlock
{
    private readonly int _sand, _red;
    public SandVariantBlock(byte id, string k, string n) : base(id, k, n)
    {
        _sand = TextureGen.Layer("sand"); _red = TextureGen.Layer("red_sand"); Tex = ModelBox.All(_sand); Vars(2);
    }
    public override int GetTexture(int face, int meta) => meta == 1 ? _red : _sand;
    public override int DropId(int meta, Rng r) => Id;
    public override int DropMeta(int meta) => meta == 1 ? 1 : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, meta == 1 ? 1 : 0);
    public override string DisplayName(int meta) => meta == 1 ? "Czerwony piasek" : "Piasek";
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote || y <= 0) return;
        int below = w.GetBlock(x, y - 1, z); var bb = Block.Get(below);
        if (below == B.Air || bb.IsLiquid || (bb.Replaceable && !bb.Solid))
        {
            if (!w.IsAreaLoaded(x, z, 2)) return;
            int meta = w.GetMeta(x, y, z);
            w.SetBlock(x, y, z, B.Air);
            w.SpawnEntity(new FallingBlockEntity(w, x + 0.5, y, z + 0.5, Id, meta));
        }
    }
}

/// <summary>Blok w 16 kolorach (wełna, barwiona glina): meta = kolor jak EnumDyeColor.</summary>
public class ColoredBlock : Block
{
    private readonly int[] _tex = new int[16]; private readonly string[] _names; private readonly string _suffix;
    public ColoredBlock(byte id, string k, string[] names, string suffix, string texPrefix) : base(id, k, names[0] + suffix)
    {
        _names = names; _suffix = suffix;
        for (int i = 0; i < 16; i++) _tex[i] = TextureGen.Layer(texPrefix + i);
        Tex = ModelBox.All(_tex[0]); Vars(16);
    }
    public override int GetTexture(int face, int meta) => _tex[meta & 15];
    public override int DropMeta(int meta) => meta & 15;
    public override ItemStack PickStack(int meta) => new(Id, 1, meta & 15);
    public override string DisplayName(int meta) => _names[meta & 15] + _suffix;
}

public sealed class NoDropBlock : Block
{
    public NoDropBlock(byte id, string k, string n) : base(id, k, n) { }
    public override int DropId(int meta, Rng r) => 0;
}

public sealed class SeaLanternBlock : Block
{
    public SeaLanternBlock(byte id, string k, string n) : base(id, k, n) { }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        int fortune = EnchantHelper.GetLevel(tool, Ench.Fortune);
        if (Item.ById[I.PrismarineCrystals] == null) { drops.Add(new ItemStack(Id)); return; }
        drops.Add(new ItemStack(I.PrismarineCrystals, Math.Min(5, 2 + r.Next(2) + r.Next(fortune + 1))));
    }
}

public sealed class EmeraldOreBlock : OreBlock
{
    public EmeraldOreBlock(byte id, string k, string n) : base(id, k, n, I.Emerald, 1, 1) { }
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    {
        if (!p.Creative && !Silked(p)) w.SpawnXp(x + 0.5, y + 0.5, z + 0.5, w.Rand.Next(3, 8));
    }
}

/// <summary>Snopek siana: oś jak kłoda (0 Y, 1 X, 2 Z).</summary>
public sealed class HayBlock : Block
{
    private readonly int _side, _top;
    public HayBlock(byte id, string k, string n) : base(id, k, n)
    {
        _side = TextureGen.Layer("hay_block_side"); _top = TextureGen.Layer("hay_block_top");
        Tex = new[] { _top, _top, _side, _side, _side, _side }; H(0.5f); Sound = SoundType.Grass; Flammable = true;
    }
    public override int GetTexture(int face, int meta)
    {
        int axis = meta & 3;
        bool end = axis == 0 ? face <= 1 : axis == 1 ? face >= 4 : (face == 2 || face == 3);
        return end ? _top : _side;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => face <= 1 ? 0 : face >= 4 ? 1 : 2;
}

// ======================= drewna =======================

/// <summary>Kłoda OW: po zniszczeniu oznacza okoliczne liście do sprawdzenia zaniku (BlockLog.breakBlock).</summary>
public sealed class OwLogBlock : LogBlock
{
    public OwLogBlock(byte id, string k, string n, string side, string top) : base(id, k, n, side, top) { }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (w.IsRemote || !w.IsAreaLoaded(x, z, 5)) return;
        for (int dx = -4; dx <= 4; dx++) for (int dy = -4; dy <= 4; dy++) for (int dz = -4; dz <= 4; dz++)
                {
                    int id = w.GetBlock(x + dx, y + dy, z + dz);
                    if (Block.ById[id] is LeavesBlock)
                    {
                        int m = w.GetMeta(x + dx, y + dy, z + dz);
                        if ((m & 8) == 0) w.SetMeta(x + dx, y + dy, z + dz, m | 8, false);
                    }
                }
    }
}

/// <summary>Liście jak BlockLeaves 1.8: bit 4 = postawione (nie znikają), bit 8 = do sprawdzenia; zanik w promieniu 4 od pnia.</summary>
public sealed class OwLeavesBlock : LeavesBlock
{
    private readonly int _sap, _chance; private readonly bool _apples;
    public OwLeavesBlock(byte id, string k, string n, string tex, TintType tint, int sapling, bool apples, int chance) : base(id, k, n, tex, tint, sapling, apples)
    { _sap = sapling; _apples = apples; _chance = chance; }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shears) { drops.Add(new ItemStack(Id, 1)); return; }
        int fortune = EnchantHelper.GetLevel(tool, Ench.Fortune);
        int c = _chance; if (fortune > 0) c = Math.Max(10, c - (2 << fortune));
        if (r.Next(c) == 0) drops.Add(new ItemStack(_sap, 1));
        if (_apples)
        {
            int a = 200; if (fortune > 0) a = Math.Max(40, a - (10 << fortune));
            if (r.Next(a) == 0) drops.Add(new ItemStack(I.Apple, 1));
        }
    }
    public override int DropMeta(int meta) => 0;
    public override ItemStack PickStack(int meta) => new(Id, 1);
    public override void OnRandomTick(World w, int x, int y, int z, Rng r) => Decay(w, x, y, z);
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r) => Decay(w, x, y, z);

    private void Decay(World w, int x, int y, int z)
    {
        int meta = w.GetMeta(x, y, z);
        if (w.IsRemote || (meta & 4) != 0 || (meta & 8) == 0) return;
        if (!w.IsAreaLoaded(x, z, 5)) return;
        var q = new Queue<(int, int, int, int)>(); var seen = new HashSet<(int, int, int)>();
        q.Enqueue((x, y, z, 0)); seen.Add((x, y, z));
        while (q.Count > 0)
        {
            var (cx, cy, cz, d) = q.Dequeue();
            for (int f = 0; f < 6; f++)
            {
                int nx = cx + Dir.X[f], ny = cy + Dir.Y[f], nz = cz + Dir.Z[f];
                var b = Block.ById[w.GetBlock(nx, ny, nz)];
                if (b is LogBlock) { w.SetMeta(x, y, z, meta & ~8, false); return; }
                if (b is LeavesBlock && d < 3 && seen.Add((nx, ny, nz))) q.Enqueue((nx, ny, nz, d + 1));
            }
        }
        w.BreakBlock(x, y, z, true);
    }
}

/// <summary>Sadzonka 1.8: meta bit 8 = etap; rośnie w drzewo gatunku (2x2 dla świerku, dżungli i ciemnego dębu).</summary>
public sealed class OwSaplingBlock : SaplingBlock, IBoneMealable
{
    public readonly int Type;
    public OwSaplingBlock(byte id, string k, string n, int type) : base(id, k, n, type) { Type = type; }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetRawLight(x, y + 1, z) >= 9 && r.Next(7) == 0) GrowStage(w, x, y, z, r);
    }
    public void GrowStage(World w, int x, int y, int z, Rng r)
    {
        int meta = w.GetMeta(x, y, z);
        if ((meta & 8) == 0) { w.SetMeta(x, y, z, meta | 8, false); return; }
        OwTrees.GrowSapling(w, x, y, z, Type, new JRandom(r.NextLong()));
    }
    public override int DropMeta(int meta) => 0;
    public bool CanBoneMeal(World w, int x, int y, int z) => true;
    public void BoneMeal(World w, int x, int y, int z, Rng r) { if (r.NextFloat() < 0.45f) GrowStage(w, x, y, z, r); }
}

/// <summary>Płot drewniany: łączy się z płotami z tego samego materiału, furtkami i pełnymi blokami (bez dyni i arbuzów).</summary>
public sealed class OwFenceBlock : FenceBlock
{
    public OwFenceBlock(byte id, string k, string n, string tex) : base(id, k, n) { T(tex); }
    public bool ConnectsTo(IBlockAccess w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z); var b = Block.Get(id);
        if (b is FenceBlock) return (b.Sound == SoundType.Wood) == (Sound == SoundType.Wood);
        if (b is FenceGateBlock) return true;
        return b.Opaque && b.FullCube && id is not (B.Pumpkin or B.JackOLantern or B.Melon);
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        double x0 = 0.375, x1 = 0.625, z0 = 0.375, z1 = 0.625;
        if (ConnectsTo(w, x - 1, y, z)) x0 = 0; if (ConnectsTo(w, x + 1, y, z)) x1 = 1;
        if (ConnectsTo(w, x, y, z - 1)) z0 = 0; if (ConnectsTo(w, x, y, z + 1)) z1 = 1;
        list.Add(new AABB(x0, 0, 0.375, x1, 1.5, 0.625));
        list.Add(new AABB(0.375, 0, z0, 0.625, 1.5, z1));
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        double x0 = 0.375, x1 = 0.625, z0 = 0.375, z1 = 0.625;
        if (ConnectsTo(w, x - 1, y, z)) x0 = 0; if (ConnectsTo(w, x + 1, y, z)) x1 = 1;
        if (ConnectsTo(w, x, y, z - 1)) z0 = 0; if (ConnectsTo(w, x, y, z + 1)) z1 = 1;
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
        if (ConnectsTo(w, x - 1, y, z)) Arm(-1, 0); if (ConnectsTo(w, x + 1, y, z)) Arm(1, 0);
        if (ConnectsTo(w, x, y, z - 1)) Arm(0, -1); if (ConnectsTo(w, x, y, z + 1)) Arm(0, 1);
    }
}

public sealed class OwFenceGateBlock : FenceGateBlock
{
    public OwFenceGateBlock(byte id, string k, string n, string tex) : base(id, k, n) { T(tex); }
}

/// <summary>Drzwi drewniane dowolnego gatunku (logika jak BlockDoor 1.8, meta jak DoorBlock).</summary>
public sealed class OwDoorBlock : DoorBlock
{
    private readonly int _lo, _up, _item;
    public OwDoorBlock(byte id, string k, string n, string lower, string upper, int item) : base(id, k, n)
    {
        _lo = TextureGen.Layer(lower); _up = TextureGen.Layer(upper); _item = item; Tex = ModelBox.All(_lo);
    }
    private static AABB Box(int full)
    {
        const double t = 3 / 16.0;
        int d = full & 3; bool open = (full & 4) != 0; bool hingeR = (full & 16) != 0;
        int side = d;
        if (open) side = hingeR ? (d + 3) & 3 : (d + 1) & 3;
        return side switch
        {
            0 => new AABB(0, 0, 0, t, 1, 1),
            1 => new AABB(0, 0, 0, 1, 1, t),
            2 => new AABB(1 - t, 0, 0, 1, 1, 1),
            _ => new AABB(0, 0, 1 - t, 1, 1, 1)
        };
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box(FullMeta(w, x, y, z)));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var b = Box(FullMeta(w, x, y, z));
        int tex = (meta & 8) != 0 ? _up : _lo;
        var mb = new ModelBox((float)b.MinX, (float)b.MinY, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, ModelBox.All(tex));
        mb.Uv = new float[24];
        for (int f = 0; f < 6; f++) { mb.Uv[f * 4] = 0; mb.Uv[f * 4 + 1] = 0; mb.Uv[f * 4 + 2] = f <= 1 ? 3 / 16f : 1; mb.Uv[f * 4 + 3] = 1; }
        for (int f = 0; f < 6; f++)
        {
            double w0 = f <= 1 ? 1 : (f == 2 || f == 3) ? b.MaxX - b.MinX : b.MaxZ - b.MinZ;
            if (w0 < 0.5) { mb.Uv[f * 4] = 0; mb.Uv[f * 4 + 2] = 3 / 16f; }
        }
        boxes.Add(mb);
    }
    public override int DropId(int meta, Rng r) => (meta & 8) != 0 ? 0 : _item;
    public override ItemStack PickStack(int meta) => new(_item, 1);
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z);
        int ly = (m & 8) != 0 ? y - 1 : y;
        int lm = w.GetMeta(x, ly, z) ^ 4;
        w.SetMeta(x, ly, z, lm);
        w.MarkDirty(x, ly + 1, z);
        w.PlaySound((lm & 4) != 0 ? "random.door_open" : "random.door_close", x + 0.5, y + 0.5, z + 0.5, 1f, w.Rand.NextFloat() * 0.1f + 0.9f);
        return true;
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 8) != 0) { if (w.GetBlock(x, y - 1, z) != Id) w.SetBlock(x, y, z, B.Air); return; }
        if (w.GetBlock(x, y + 1, z) != Id) { w.BreakBlock(x, y, z, true); return; }
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
                w.PlaySound(powered ? "random.door_open" : "random.door_close", x + 0.5, y + 0.5, z + 0.5);
            }
        }
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if ((oldMeta & 8) != 0) { if (w.GetBlock(x, y - 1, z) == Id) w.SetBlock(x, y - 1, z, B.Air); }
        else if (w.GetBlock(x, y + 1, z) == Id) w.SetBlock(x, y + 1, z, B.Air);
    }

    /// <summary>Postawienie drzwi z itemu (jak ItemDoor.placeDoor).</summary>
    public static bool Place(World w, Player p, byte door, int tx, int ty, int tz)
    {
        if (ty + 1 >= Chunk.Height || !Block.Get(w.GetBlock(tx, ty + 1, tz)).Replaceable || !Block.Get(w.GetBlock(tx, ty - 1, tz)).Opaque) return false;
        int d = (Dir.HorizFromYaw(p.Yaw) + 3) & 3;
        int lx = tx - Dir.HorizX[(d + 1) & 3], lz = tz - Dir.HorizZ[(d + 1) & 3];
        bool hinge = Block.ById[w.GetBlock(lx, ty, lz)] is DoorBlock;
        w.SetBlock(tx, ty, tz, door, d, false);
        w.SetBlock(tx, ty + 1, tz, door, 8 | (hinge ? 1 : 0), true);
        w.NotifyNeighbors(tx, ty, tz, door);
        return true;
    }
}
