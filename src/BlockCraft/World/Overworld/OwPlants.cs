using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>Blok reagujący na mączkę kostną (IGrowable z MC 1.8).</summary>
public interface IBoneMealable
{
    bool CanBoneMeal(World w, int x, int y, int z);
    void BoneMeal(World w, int x, int y, int z, Rng r);
}

/// <summary>Mączka kostna i kwiaty biomów (ItemDye.applyBonemeal, BiomeGenBase.pickRandomFlower).</summary>
public static class OwFlora
{
    /// <summary>Zwraca (blok, meta) kwiatu dla biomu jak pickRandomFlower w MC 1.8.</summary>
    public static (byte id, int meta) PickFlower(int biome, JRandom r, int x, int z)
    {
        var d = Biome.Get(biome);
        if (d.Kind == BiomeKind.Swamp) return (B.Poppy, 1);
        if (d.Kind == BiomeKind.Plains)
        {
            double n = Biome.GrassColorNoise(x / 200.0, z / 200.0);
            if (n < -0.8) return (B.Poppy, r.NextInt(4) switch { 0 => 5, 1 => 4, 2 => 7, _ => 6 });
            if (r.NextInt(3) > 0) { int i = r.NextInt(3); return i == 0 ? (B.Poppy, 0) : i == 1 ? (B.Poppy, 3) : (B.Poppy, 8); }
            return (B.Dandelion, 0);
        }
        if (d.Kind == BiomeKind.Forest && d.Variant == 1)
        {
            double v = Math.Clamp((1.0 + Biome.GrassColorNoise(x / 48.0, z / 48.0)) / 2.0, 0.0, 0.9999);
            int i = (int)(v * 10);
            // kolejność EnumFlowerType: mniszek, mak, orchidea, czosnek, houstonia, 4 tulipany, złocień (orchidea -> mak)
            return i switch { 0 => (B.Dandelion, 0), 1 or 2 => (B.Poppy, 0), 3 => (B.Poppy, 2), 4 => (B.Poppy, 3), 5 => (B.Poppy, 4), 6 => (B.Poppy, 5), 7 => (B.Poppy, 6), 8 => (B.Poppy, 7), _ => (B.Poppy, 8) };
        }
        return r.NextInt(3) > 0 ? (B.Dandelion, 0) : (B.Poppy, 0);
    }

    /// <summary>Użycie mączki kostnej na bloku. Zwraca true, gdy zużyta.</summary>
    public static bool ApplyBoneMeal(World w, int x, int y, int z)
    {
        var b = Block.ById[w.GetBlock(x, y, z)];
        if (b is IBoneMealable g)
        {
            if (!g.CanBoneMeal(w, x, y, z)) return false;
            if (!w.IsRemote) g.BoneMeal(w, x, y, z, w.Rand);
            w.SpawnParticles(ParticleKind.Heart, x + 0.5, y + 0.8, z + 0.5, 4);
            return true;
        }
        if (b.Id == B.Wheat)
        {
            int m = w.GetMeta(x, y, z); if (m >= 7) return false;
            w.SetMeta(x, y, z, Math.Min(7, m + 2 + w.Rand.Next(4)));
            w.SpawnParticles(ParticleKind.Heart, x + 0.5, y + 0.8, z + 0.5, 4);
            return true;
        }
        return false;
    }

    /// <summary>BlockGrass.grow: trawa i kwiaty wokół.</summary>
    public static void GrassBoneMeal(World w, int x, int y, int z, Rng rng)
    {
        var r = new JRandom(rng.NextLong());
        for (int i = 0; i < 128; i++)
        {
            int px = x, py = y + 1, pz = z;
            int j = 0;
            while (true)
            {
                if (j >= i / 16)
                {
                    if (w.GetBlock(px, py, pz) == B.Air)
                    {
                        if (r.NextInt(8) == 0)
                        {
                            var (id, meta) = PickFlower(w.GetBiome(px, pz), r, px, pz);
                            if (Block.ById[id].CanPlaceAt(w, px, py, pz)) w.SetBlock(px, py, pz, id, meta);
                        }
                        else if (Block.ById[B.TallGrass].CanPlaceAt(w, px, py, pz)) w.SetBlock(px, py, pz, B.TallGrass, 1);
                    }
                    break;
                }
                px += r.NextInt(3) - 1; py += (r.NextInt(3) - 1) * r.NextInt(3) / 2; pz += r.NextInt(3) - 1;
                if (w.GetBlock(px, py - 1, pz) != B.Grass || Block.Get(w.GetBlock(px, py, pz)).Opaque) break;
                j++;
            }
        }
    }
}

// ======================= trawa, kwiaty =======================

public sealed class OwGrassBlock : GrassBlock, IBoneMealable
{
    public OwGrassBlock(byte id, string k, string n) : base(id, k, n) { }
    public bool CanBoneMeal(World w, int x, int y, int z) => true;
    public void BoneMeal(World w, int x, int y, int z, Rng r) => OwFlora.GrassBoneMeal(w, x, y, z, r);
}

/// <summary>Kwiaty (red_flower / yellow_flower) z wariantami meta.</summary>
public sealed class OwFlowerBlock : PlantBlock
{
    private readonly string[] _names; private readonly int[] _tex;
    public OwFlowerBlock(byte id, string k, string[] names, string[] tex) : base(id, k, names[0])
    {
        _names = names; _tex = tex.Select(TextureGen.Layer).ToArray();
        Plant(); Tex = ModelBox.All(_tex[0]); if (names.Length > 1) Vars(names.Length);
    }
    public override int GetTexture(int face, int meta) => _tex[meta < _tex.Length ? meta : 0];
    public override int DropMeta(int meta) => meta < _tex.Length ? meta : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, DropMeta(meta));
    public override string DisplayName(int meta) => _names[meta < _names.Length ? meta : 0];
}

/// <summary>Wysoka trawa: meta 0 i 1 trawa, 2 paproć (BlockTallGrass).</summary>
public sealed class OwTallGrassBlock : TallGrassBlock, IBoneMealable
{
    private readonly int _grass, _fern;
    public OwTallGrassBlock(byte id, string k, string n) : base(id, k, n)
    {
        _grass = TextureGen.Layer("tallgrass"); _fern = TextureGen.Layer("fern"); Tex = ModelBox.All(_grass); Vars(1, 2);
    }
    public override int GetTexture(int face, int meta) => meta == 2 ? _fern : _grass;
    public override string DisplayName(int meta) => meta == 2 ? "Paproć" : "Wysoka trawa";
    public override int DropMeta(int meta) => meta == 2 ? 2 : 1;
    public override ItemStack PickStack(int meta) => new(Id, 1, DropMeta(meta));
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shears) { drops.Add(new ItemStack(Id, 1, DropMeta(meta))); return; }
        if (r.Next(8) == 0) drops.Add(new ItemStack(I.Seeds, 1));
    }
    public bool CanBoneMeal(World w, int x, int y, int z) => w.GetBlock(x, y + 1, z) == B.Air;
    public void BoneMeal(World w, int x, int y, int z, Rng r)
    {
        int type = w.GetMeta(x, y, z) == 2 ? 3 : 2;
        w.SetBlock(x, y, z, B.DoublePlant, type, false);
        w.SetBlock(x, y + 1, z, B.DoublePlant, 8 | type);
    }
}

public sealed class OwDeadBushBlock : DeadBushBlock
{
    public OwDeadBushBlock(byte id, string k, string n) : base(id, k, n) { }
    public override bool CanGrowOn(int id) => id is B.Sand or B.HardenedClay or B.StainedClay or B.Dirt;
}

/// <summary>Podwójne rośliny (double_plant): meta dolnej połowy 0 słonecznik, 1 bez, 2 trawa, 3 paproć, 4 róża, 5 piwonia; górna = 8 | typ.</summary>
public sealed class DoublePlantBlock : PlantBlock, IBoneMealable
{
    private static readonly string[] Names = { "Słonecznik", "Bez", "Podwójna trawa", "Duża paproć", "Krzak róży", "Piwonia" };
    private static readonly string[] TexN = { "sunflower", "syringa", "grass", "fern", "rose", "paeonia" };
    private readonly int[] _bot = new int[6], _top = new int[6];
    public DoublePlantBlock(byte id, string k, string n) : base(id, k, n)
    {
        for (int i = 0; i < 6; i++) { _bot[i] = TextureGen.Layer("double_plant_" + TexN[i] + "_bottom"); _top[i] = TextureGen.Layer("double_plant_" + TexN[i] + "_top"); }
        Plant(); Replaceable = false; Tex = ModelBox.All(_top[0]); Vars(6);
    }
    private static int Type(int meta) => Math.Min(meta & 7, 5);
    public override int GetTexture(int face, int meta) => (meta & 8) != 0 ? _top[Type(meta)] : _bot[Type(meta)];
    public override TintType TintAt(int meta) => Type(meta) is 2 or 3 ? TintType.Grass : TintType.None;
    public override string DisplayName(int meta) => Names[Type(meta)];
    public override int DropMeta(int meta) => Type(meta);
    public override ItemStack PickStack(int meta) => new(Id, 1, Type(meta));
    public override bool CanPlaceAt(World w, int x, int y, int z)
        => base.CanPlaceAt(w, x, y, z) && y + 1 < Chunk.Height && Block.Get(w.GetBlock(x, y + 1, z)).Replaceable;
    public override bool CanStay(World w, int x, int y, int z)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 8) != 0) return w.GetBlock(x, y - 1, z) == Id;
        return CanGrowOn(w.GetBlock(x, y - 1, z)) && w.GetBlock(x, y + 1, z) == Id;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta) => Type(itemMeta);
    public override void OnPlaced(World w, int x, int y, int z, Player p) => w.SetBlock(x, y + 1, z, Id, 8 | Type(w.GetMeta(x, y, z)));
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (CanStay(w, x, y, z)) return;
        int m = w.GetMeta(x, y, z);
        if ((m & 8) != 0) w.SetBlock(x, y, z, B.Air); else w.BreakBlock(x, y, z, true);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        int oy = (oldMeta & 8) != 0 ? y - 1 : y + 1;
        if (w.GetBlock(x, oy, z) == Id) w.SetBlock(x, oy, z, B.Air, 0, true);
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        int t = Type(meta);
        if (t is 2 or 3)
        {
            if (tool?.Item?.Tool == ToolType.Shears) drops.Add(new ItemStack(B.TallGrass, 2, t == 2 ? 1 : 2));
            else if (t == 2 && r.Next(8) == 0) drops.Add(new ItemStack(I.Seeds, 1));
            return;
        }
        drops.Add(new ItemStack(Id, 1, t));
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0.1, 0, 0.1, 0.9, 1, 0.9));
    public bool CanBoneMeal(World w, int x, int y, int z) => Type(w.GetMeta(x, y, z)) is not (2 or 3);
    public void BoneMeal(World w, int x, int y, int z, Rng r) => w.DropItem(x + 0.5, y + 0.5, z + 0.5, new ItemStack(Id, 1, Type(w.GetMeta(x, y, z))));
}

// ======================= grzyby =======================

/// <summary>Mały grzyb: rośnie w ciemności (światło &lt; 13) albo na grzybni/bielicy, rozprzestrzenia się (BlockMushroom).</summary>
public sealed class OwMushroomBlock : PlantBlock, IBoneMealable
{
    public OwMushroomBlock(byte id, string k, string n, string tex) : base(id, k, n) { T(tex); Plant(); RandomTicks = true; }
    public override bool CanGrowOn(int id) => Block.Get(id).Opaque;
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        if (y < 0 || y >= Chunk.Height) return false;
        int below = w.GetBlock(x, y - 1, z);
        if (below == B.Mycelium || below == B.Dirt && w.GetMeta(x, y - 1, z) == 2) return true;
        return w.GetLight(x, y, z) < 13 && Block.Get(below).Opaque;
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0.3, 0, 0.3, 0.7, 0.4, 0.7));
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (r.Next(25) != 0) return;
        int count = 5;
        for (int dx = -4; dx <= 4; dx++) for (int dz = -4; dz <= 4; dz++) for (int dy = -1; dy <= 1; dy++)
                    if (w.GetBlock(x + dx, y + dy, z + dz) == Id && --count <= 0) return;
        int px = x + r.Next(3) - 1, py = y + r.Next(2) - r.Next(2), pz = z + r.Next(3) - 1;
        for (int k = 0; k < 4; k++)
        {
            if (w.GetBlock(px, py, pz) == B.Air && CanPlaceAt(w, px, py, pz)) { x = px; y = py; z = pz; }
            px = x + r.Next(3) - 1; py = y + r.Next(2) - r.Next(2); pz = z + r.Next(3) - 1;
        }
        if (w.GetBlock(px, py, pz) == B.Air && CanPlaceAt(w, px, py, pz)) w.SetBlock(px, py, pz, Id);
    }
    public bool CanBoneMeal(World w, int x, int y, int z) => true;
    public void BoneMeal(World w, int x, int y, int z, Rng r)
    {
        if (r.NextFloat() >= 0.4f) return;
        w.SetBlock(x, y, z, B.Air, 0, false);
        if (!OwTrees.HugeMushroom(new WorldGenAccess(w), new JRandom(r.NextLong()), x, y, z, Id == B.RedMushroom ? 1 : 0))
            w.SetBlock(x, y, z, Id, 0, false);
    }
}

/// <summary>Blok wielkiego grzyba (BlockHugeMushroom 1.8): meta 0 środek, 1-9 kapelusz wg stron, 10 trzon, 14 cały kapelusz, 15 cały trzon.</summary>
public sealed class HugeMushroomBlock : Block
{
    private readonly int _skin, _stem, _inside; private readonly byte _small;
    public HugeMushroomBlock(byte id, string k, string n, string skin, byte small) : base(id, k, n)
    {
        _skin = TextureGen.Layer(skin); _stem = TextureGen.Layer("mushroom_block_skin_stem"); _inside = TextureGen.Layer("mushroom_block_inside");
        _small = small; Tex = ModelBox.All(_skin); H(0.2f); Tool = ToolType.Axe; Sound = SoundType.Wood; Variants = new[] { 14 };
    }
    public override int GetTexture(int face, int meta)
    {
        if (meta == 15) return _stem;
        if (meta == 14) return _skin;
        if (meta == 10) return face <= 1 ? _inside : _stem;
        bool cap = face switch
        {
            Dir.Up => meta >= 1 && meta <= 9,
            Dir.North => meta is 1 or 2 or 3,
            Dir.South => meta is 7 or 8 or 9,
            Dir.West => meta is 1 or 4 or 7,
            Dir.East => meta is 3 or 6 or 9,
            _ => false
        };
        return cap ? _skin : _inside;
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        int n = Math.Max(0, r.Next(10) - 7);
        if (n > 0) drops.Add(new ItemStack(_small, n));
    }
    public override ItemStack PickStack(int meta) => new(Id, 1, 14);
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta) => 14;
}

/// <summary>Grzybnia: rozrasta się po ziemi jak trawa (BlockMycelium).</summary>
public sealed class MyceliumBlock : Block
{
    public MyceliumBlock(byte id, string k, string n) : base(id, k, n) { T("mycelium_top", "dirt", "mycelium_side"); RandomTicks = true; }
    public override int DropId(int meta, Rng r) => B.Dirt;
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        var above = Block.Get(w.GetBlock(x, y + 1, z));
        if (w.GetRawLight(x, y + 1, z) < 4 && above.LightOpacity > 2) { w.SetBlock(x, y, z, B.Dirt); return; }
        if (w.GetRawLight(x, y + 1, z) < 9) return;
        for (int i = 0; i < 4; i++)
        {
            int tx = x + r.Next(3) - 1, ty = y + r.Next(5) - 3, tz = z + r.Next(3) - 1;
            if (w.GetBlock(tx, ty, tz) == B.Dirt && w.GetMeta(tx, ty, tz) == 0 && w.GetRawLight(tx, ty + 1, tz) >= 4 && Block.Get(w.GetBlock(tx, ty + 1, tz)).LightOpacity <= 2)
                w.SetBlock(tx, ty, tz, B.Mycelium);
        }
    }
}

// ======================= pnącza, lilie, kakao =======================

/// <summary>Pnącza (BlockVine 1.8): bity 1 południe, 2 zachód, 4 północ, 8 wschód; 0 = tylko pod sufitem.</summary>
public sealed class VineBlock : Block
{
    public VineBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("vine"); Opaque = false; Solid = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        H(0.2f); Sound = SoundType.Grass; Tint = TintType.Foliage; Climbable = true; Replaceable = true; RandomTicks = true; Flammable = true;
    }
    public static readonly int[] BitDx = { 0, -1, 0, 1 }, BitDz = { 1, 0, -1, 0 }; // bity 1,2,4,8: S, W, N, E
    public static bool CanAttachTo(int id) { var b = Block.Get(id); return b.Opaque && b.FullCube && b.Solid; }
    private static int BitForOffset(int dx, int dz) => dz > 0 ? 1 : dx < 0 ? 2 : dz < 0 ? 4 : 8;

    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        const double t = 1 / 16.0;
        if ((meta & 1) != 0) list.Add(new AABB(0, 0, 1 - t, 1, 1, 1));
        if ((meta & 2) != 0) list.Add(new AABB(0, 0, 0, t, 1, 1));
        if ((meta & 4) != 0) list.Add(new AABB(0, 0, 0, 1, 1, t));
        if ((meta & 8) != 0) list.Add(new AABB(1 - t, 0, 0, 1, 1, 1));
        if (meta == 0) list.Add(new AABB(0, 1 - t, 0, 1, 1, 1));
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        const float e = 0.8f / 16f;
        void Add(ModelBox mb) { mb.NoCull = true; boxes.Add(mb); }
        if ((meta & 1) != 0) Add(new ModelBox(0, 0, 1 - e, 1, 1, 1 - e, Tex));
        if ((meta & 2) != 0) Add(new ModelBox(e, 0, 0, e, 1, 1, Tex));
        if ((meta & 4) != 0) Add(new ModelBox(0, 0, e, 1, 1, e, Tex));
        if ((meta & 8) != 0) Add(new ModelBox(1 - e, 0, 0, 1 - e, 1, 1, Tex));
        if (CanAttachTo(w.GetBlock(x, y + 1, z))) Add(new ModelBox(0, 1 - e, 0, 1, 1 - e, 1, Tex));
    }
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        for (int i = 0; i < 4; i++) if (CanAttachTo(w.GetBlock(x + BitDx[i], y, z + BitDz[i]))) return true;
        return CanAttachTo(w.GetBlock(x, y + 1, z));
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        if (face >= 2)
        {
            int wall = Dir.Opp[face];
            if (CanAttachTo(w.GetBlock(x + Dir.X[wall], y, z + Dir.Z[wall]))) return BitForOffset(Dir.X[wall], Dir.Z[wall]);
        }
        for (int i = 0; i < 4; i++) if (CanAttachTo(w.GetBlock(x + BitDx[i], y, z + BitDz[i]))) return 1 << i;
        return 0;
    }
    /// <summary>Usuwa strony bez oparcia (canAttachTo albo pnącze nad z tą stroną). Zwraca false, gdy nic nie zostało.</summary>
    private bool Recheck(World w, int x, int y, int z)
    {
        int m = w.GetMeta(x, y, z), nm = m;
        int above = w.GetBlock(x, y + 1, z), am = w.GetMeta(x, y + 1, z);
        for (int i = 0; i < 4; i++)
        {
            int bit = 1 << i;
            if ((m & bit) == 0) continue;
            if (CanAttachTo(w.GetBlock(x + BitDx[i], y, z + BitDz[i]))) continue;
            if (above == Id && (am & bit) != 0) continue;
            nm &= ~bit;
        }
        if (nm == 0 && !CanAttachTo(above)) return false;
        if (nm != m) w.SetMeta(x, y, z, nm);
        return true;
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (!w.IsRemote && !Recheck(w, x, y, z)) w.BreakBlock(x, y, z, false);
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shears) drops.Add(new ItemStack(Id, 1));
    }
    public override ItemStack PickStack(int meta) => new(Id, 1);
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote || r.Next(4) != 0) return;
        int limit = 5; bool crowded = false;
        for (int dx = -4; dx <= 4 && !crowded; dx++) for (int dz = -4; dz <= 4 && !crowded; dz++) for (int dy = -1; dy <= 1; dy++)
                    if (w.GetBlock(x + dx, y + dy, z + dz) == Id && --limit <= 0) { crowded = true; break; }
        int meta = w.GetMeta(x, y, z);
        int dir = r.Next(6); // 0 dół, 1 góra, 2-5 poziomo (N, S, W, E)
        if (dir == Dir.Up && y < Chunk.Height - 1 && w.GetBlock(x, y + 1, z) == B.Air)
        {
            if (crowded) return;
            int nm = meta;
            for (int i = 0; i < 4; i++) if (r.NextBool() || !CanAttachTo(w.GetBlock(x + BitDx[i], y + 1, z + BitDz[i]))) nm &= ~(1 << i);
            if (nm != 0) w.SetBlock(x, y + 1, z, Id, nm);
        }
        else if (dir >= 2)
        {
            int dx = Dir.X[dir], dz = Dir.Z[dir]; int bit = BitForOffset(dx, dz);
            if ((meta & bit) != 0) return;
            if (crowded) return;
            int tx = x + dx, tz = z + dz; int tid = w.GetBlock(tx, y, tz);
            if (tid == B.Air)
            {
                // obrót w prawo/lewo (rotateY / rotateYCCW)
                int li = (Array.IndexOf(new[] { 1, 2, 4, 8 }, bit) + 1) & 3, ri = (Array.IndexOf(new[] { 1, 2, 4, 8 }, bit) + 3) & 3;
                int lb = 1 << li, rb = 1 << ri;
                bool f1 = (meta & lb) != 0, f2 = (meta & rb) != 0;
                int l1x = tx + BitDx[li], l1z = tz + BitDz[li], r1x = tx + BitDx[ri], r1z = tz + BitDz[ri];
                int opp = BitForOffset(-dx, -dz);
                if (f1 && CanAttachTo(w.GetBlock(l1x, y, l1z))) w.SetBlock(tx, y, tz, Id, lb);
                else if (f2 && CanAttachTo(w.GetBlock(r1x, y, r1z))) w.SetBlock(tx, y, tz, Id, rb);
                else if (f1 && w.GetBlock(l1x, y, l1z) == B.Air && CanAttachTo(w.GetBlock(x + BitDx[li], y, z + BitDz[li]))) w.SetBlock(l1x, y, l1z, Id, opp);
                else if (f2 && w.GetBlock(r1x, y, r1z) == B.Air && CanAttachTo(w.GetBlock(x + BitDx[ri], y, z + BitDz[ri]))) w.SetBlock(r1x, y, r1z, Id, opp);
                else if (CanAttachTo(w.GetBlock(tx, y + 1, tz))) w.SetBlock(tx, y, tz, Id, 0);
            }
            else if (CanAttachTo(tid)) w.SetMeta(x, y, z, meta | bit);
        }
        else if (y > 1)
        {
            int bid = w.GetBlock(x, y - 1, z);
            if (bid == B.Air)
            {
                int nm = meta;
                for (int i = 0; i < 4; i++) if (r.NextBool()) nm &= ~(1 << i);
                if (nm != 0) w.SetBlock(x, y - 1, z, Id, nm);
            }
            else if (bid == Id)
            {
                int bm = w.GetMeta(x, y - 1, z), nm = bm;
                for (int i = 0; i < 4; i++) if (r.NextBool() && (meta & (1 << i)) != 0) nm |= 1 << i;
                if (nm != bm) w.SetMeta(x, y - 1, z, nm);
            }
        }
    }
}

/// <summary>Lilia wodna: stawiana na stojącej wodzie (BlockLilyPad).</summary>
public sealed class LilyPadBlock : Block
{
    public LilyPadBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("waterlily"); Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0; BlastResistance = 0; Sound = SoundType.Grass; Tint = TintType.Custom; NeedsSupport = true;
    }
    public override uint CustomTint(int meta) => 0x208030;
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1 / 64.0, 1));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1 / 64.0, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var mb = new ModelBox(0, 1 / 64f, 0, 1, 1 / 64f, 1, Tex) { NoCull = true };
        mb.UvRot = new int[6]; mb.UvRot[1] = mb.UvRot[0] = (int)(MathUtil.HashU(x, 0, z) & 3);
        boxes.Add(mb);
    }
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        int b = w.GetBlock(x, y - 1, z);
        return (b == B.Water && w.GetMeta(x, y - 1, z) == 0 || b == B.Ice) && w.GetBlock(x, y, z) == B.Air;
    }
    public override bool CanStay(World w, int x, int y, int z) { int b = w.GetBlock(x, y - 1, z); return b == B.Water && w.GetMeta(x, y - 1, z) == 0 || b == B.Ice; }
}

/// <summary>Kakao na bocznej ścianie dżunglowego drewna: bity 0-1 kierunek do pnia (S, W, N, E), bity 2-3 wiek 0-2 (BlockCocoa).</summary>
public sealed class CocoaBlock : Block, IBoneMealable
{
    private readonly int[] _stage = new int[3];
    public CocoaBlock(byte id, string k, string n) : base(id, k, n)
    {
        for (int i = 0; i < 3; i++) _stage[i] = TextureGen.Layer("cocoa_stage_" + i);
        Tex = ModelBox.All(_stage[0]); Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        H(0.2f, 3); Tool = ToolType.Axe; Sound = SoundType.Wood; RandomTicks = true; NeedsSupport = true;
    }
    public override int GetTexture(int face, int meta) => _stage[Math.Min(2, (meta >> 2) & 3)];
    private static AABB Box(int meta)
    {
        int age = Math.Min(2, (meta >> 2) & 3), i = 4 + age * 2, j = 5 + age * 2; double f = i / 2.0;
        int d = meta & 3;
        return d switch
        {
            0 => new AABB((8 - f) / 16, (12.0 - j) / 16, (15.0 - i) / 16, (8 + f) / 16, 0.75, 15 / 16.0),   // pień na południu
            1 => new AABB(1 / 16.0, (12.0 - j) / 16, (8 - f) / 16, (1.0 + i) / 16, 0.75, (8 + f) / 16),      // zachód
            2 => new AABB((8 - f) / 16, (12.0 - j) / 16, 1 / 16.0, (8 + f) / 16, 0.75, (1.0 + i) / 16),      // północ
            _ => new AABB((15.0 - i) / 16, (12.0 - j) / 16, (8 - f) / 16, 15 / 16.0, 0.75, (8 + f) / 16),    // wschód
        };
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box(meta));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var b = Box(meta); int t = GetTexture(0, meta);
        boxes.Add(new ModelBox((float)b.MinX, (float)b.MinY, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, ModelBox.All(t)));
    }
    public static (int dx, int dz) LogOffset(int meta) => (meta & 3) switch { 0 => (0, 1), 1 => (-1, 0), 2 => (0, -1), _ => (1, 0) };
    public override bool CanStay(World w, int x, int y, int z)
    {
        var (dx, dz) = LogOffset(w.GetMeta(x, y, z));
        return w.GetBlock(x + dx, y, z + dz) == B.JungleLog;
    }
    public override bool CanPlaceAt(World w, int x, int y, int z) => true;
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (!CanStay(w, x, y, z)) { w.BreakBlock(x, y, z, true); return; }
        int m = w.GetMeta(x, y, z);
        if (r.Next(5) == 0 && ((m >> 2) & 3) < 2) w.SetMeta(x, y, z, m + 4);
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
        => drops.Add(new ItemStack(I.Dye, ((meta >> 2) & 3) >= 2 ? 3 : 1, 3));
    public override ItemStack PickStack(int meta) => new(I.Dye, 1, 3);
    public bool CanBoneMeal(World w, int x, int y, int z) => ((w.GetMeta(x, y, z) >> 2) & 3) < 2;
    public void BoneMeal(World w, int x, int y, int z, Rng r) => w.SetMeta(x, y, z, w.GetMeta(x, y, z) + 4);
}

// ======================= dynie, arbuzy, uprawy =======================

/// <summary>Dynia i świecąca dynia: meta = kierunek twarzy (0 S, 1 W, 2 N, 3 E).</summary>
public sealed class PumpkinBlock : Block
{
    private readonly int _top, _side, _face;
    public PumpkinBlock(byte id, string k, string n, bool lit) : base(id, k, n)
    {
        _top = TextureGen.Layer("pumpkin_top"); _side = TextureGen.Layer("pumpkin_side"); _face = TextureGen.Layer(lit ? "pumpkin_face_on" : "pumpkin_face_off");
        Tex = new[] { _top, _top, _side, _face, _side, _side }; H(1f); Tool = ToolType.Axe; Sound = SoundType.Wood;
    }
    public override int GetTexture(int face, int meta) => face <= 1 ? _top : face == Dir.HorizToFace(meta & 3) ? _face : _side;
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => (Dir.HorizFromYaw(p.Yaw) + 2) & 3;
    public override ItemStack PickStack(int meta) => new(Id, 1);
}

public sealed class MelonBlock : Block
{
    public MelonBlock(byte id, string k, string n) : base(id, k, n) { T("melon_top", "melon_top", "melon_side"); H(1f); Tool = ToolType.Axe; Sound = SoundType.Wood; }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        int fortune = EnchantHelper.GetLevel(tool, Ench.Fortune);
        drops.Add(new ItemStack(I.MelonSlice, Math.Min(9, 3 + r.Next(5) + r.Next(1 + fortune))));
    }
}

/// <summary>Wspólne MC BlockCrops.getGrowthChance.</summary>
public static class CropGrowth
{
    public static float Chance(World w, int x, int y, int z, int id)
    {
        float f = 1f;
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                float f1 = 0;
                if (w.GetBlock(x + i, y - 1, z + j) == B.Farmland) { f1 = 1; if (w.GetMeta(x + i, y - 1, z + j) > 0) f1 = 3; }
                if (i != 0 || j != 0) f1 /= 4f;
                f += f1;
            }
        bool ew = w.GetBlock(x - 1, y, z) == id || w.GetBlock(x + 1, y, z) == id;
        bool ns = w.GetBlock(x, y, z - 1) == id || w.GetBlock(x, y, z + 1) == id;
        if (ew && ns) f /= 2f;
        else if (w.GetBlock(x - 1, y, z - 1) == id || w.GetBlock(x + 1, y, z - 1) == id || w.GetBlock(x + 1, y, z + 1) == id || w.GetBlock(x - 1, y, z + 1) == id) f /= 2f;
        return f;
    }
}

/// <summary>Marchew / ziemniaki (BlockCarrot, BlockPotato): wiek 0-7, 4 tekstury etapów.</summary>
public sealed class OwCropBlock : PlantBlock, IBoneMealable
{
    private readonly int[] _st = new int[4]; private readonly int _item; private readonly bool _potato;
    public OwCropBlock(byte id, string k, string n, string tex, int item, bool potato) : base(id, k, n)
    {
        for (int i = 0; i < 4; i++) _st[i] = TextureGen.Layer(tex + i);
        _item = item; _potato = potato; Plant(RenderType.Crops); Tex = ModelBox.All(_st[0]); RandomTicks = true;
    }
    public override bool CanGrowOn(int id) => id == B.Farmland;
    public override int GetTexture(int face, int meta) => _st[meta >= 7 ? 3 : meta >= 4 ? 2 : meta >= 2 ? 1 : 0];
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetRawLight(x, y + 1, z) < 9) return;
        int m = w.GetMeta(x, y, z);
        if (m < 7 && r.Next((int)(25f / CropGrowth.Chance(w, x, y, z, Id)) + 1) == 0) w.SetMeta(x, y, z, m + 1);
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        int n = 1;
        if (meta >= 7) { int fortune = EnchantHelper.GetLevel(tool, Ench.Fortune); for (int k = 0; k < 3 + fortune; k++) if (r.Next(15) <= meta) n++; }
        drops.Add(new ItemStack(_item, n));
        if (_potato && meta >= 7 && r.Next(50) == 0) drops.Add(new ItemStack(I.PoisonousPotato, 1));
    }
    public override ItemStack PickStack(int meta) => new(_item, 1);
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 0.25, 1));
    public bool CanBoneMeal(World w, int x, int y, int z) => w.GetMeta(x, y, z) < 7;
    public void BoneMeal(World w, int x, int y, int z, Rng r) => w.SetMeta(x, y, z, Math.Min(7, w.GetMeta(x, y, z) + 2 + r.Next(4)));
}

/// <summary>Łodyga dyni/arbuza (BlockStem): wiek 0-7, dojrzała wypuszcza owoc obok.</summary>
public sealed class StemBlock : PlantBlock, IBoneMealable
{
    private readonly byte _fruit; private readonly int _seeds, _free, _conn;
    public StemBlock(byte id, string k, string n, byte fruit, int seeds) : base(id, k, n)
    {
        _fruit = fruit; _seeds = seeds; _free = TextureGen.Layer("pumpkin_stem_disconnected"); _conn = TextureGen.Layer("pumpkin_stem_connected");
        Plant(RenderType.Model); Tex = ModelBox.All(_free); RandomTicks = true; Tint = TintType.Custom;
    }
    public override bool CanGrowOn(int id) => id == B.Farmland;
    public override uint CustomTint(int meta) { int a = meta & 7; return (uint)((a * 32) << 16 | (255 - a * 8) << 8 | (a * 4)); }
    private int FruitDir(IBlockAccess w, int x, int y, int z)
    {
        if ((w.GetMeta(x, y, z) & 7) < 7) return -1;
        for (int h = 0; h < 4; h++) if (w.GetBlock(x + Dir.HorizX[h], y, z + Dir.HorizZ[h]) == _fruit) return h;
        return -1;
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int fd = FruitDir(w, x, y, z);
        if (fd >= 0)
        {
            var t = ModelBox.All(_conn);
            int dx = Dir.HorizX[fd], dz = Dir.HorizZ[fd];
            var mb = dx != 0 ? new ModelBox(dx > 0 ? 0.5f : 0f, -1 / 16f, 0.5f, dx > 0 ? 1f : 0.5f, 10 / 16f, 0.5f, t) : new ModelBox(0.5f, -1 / 16f, dz > 0 ? 0.5f : 0f, 0.5f, 10 / 16f, dz > 0 ? 1f : 0.5f, t);
            mb.NoCull = true; mb.Uv = new float[24];
            for (int f = 0; f < 6; f++) { bool flip = dx < 0 || dz < 0; mb.Uv[f * 4] = flip ? 1 : 0; mb.Uv[f * 4 + 1] = 5 / 16f; mb.Uv[f * 4 + 2] = flip ? 0.5f : 0.5f; mb.Uv[f * 4 + 3] = 1; if (!flip) { mb.Uv[f * 4] = 0.5f; mb.Uv[f * 4 + 2] = 1; } }
            boxes.Add(mb);
            return;
        }
        float h = ((meta & 7) * 2 + 2) / 16f;
        var tx = ModelBox.All(_free);
        var a = new ModelBox(0.5f, -1 / 16f, 0, 0.5f, h - 1 / 16f, 1, tx) { NoCull = true };
        var b = new ModelBox(0, -1 / 16f, 0.5f, 1, h - 1 / 16f, 0.5f, tx) { NoCull = true };
        a.Uv = new float[24]; b.Uv = new float[24];
        for (int f = 0; f < 6; f++) { a.Uv[f * 4] = b.Uv[f * 4] = 0; a.Uv[f * 4 + 1] = b.Uv[f * 4 + 1] = 1 - h; a.Uv[f * 4 + 2] = b.Uv[f * 4 + 2] = 1; a.Uv[f * 4 + 3] = b.Uv[f * 4 + 3] = 1; }
        boxes.Add(a); boxes.Add(b);
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(0.375, 0, 0.375, 0.625, ((meta & 7) * 2 + 2) / 16.0, 0.625));
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetRawLight(x, y + 1, z) < 9) return;
        if (r.Next((int)(25f / CropGrowth.Chance(w, x, y, z, Id)) + 1) != 0) return;
        int m = w.GetMeta(x, y, z);
        if (m < 7) { w.SetMeta(x, y, z, m + 1); return; }
        for (int h = 0; h < 4; h++) if (w.GetBlock(x + Dir.HorizX[h], y, z + Dir.HorizZ[h]) == _fruit) return;
        int d = r.Next(4);
        int tx = x + Dir.HorizX[d], tz = z + Dir.HorizZ[d];
        int below = w.GetBlock(tx, y - 1, tz);
        if (w.GetBlock(tx, y, tz) == B.Air && (below == B.Farmland || below == B.Dirt || below == B.Grass))
        {
            w.SetBlock(tx, y, tz, _fruit, _fruit == B.Pumpkin ? r.Next(4) : 0);
            w.MarkDirty(x, y, z);
        }
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) { base.OnNeighborChanged(w, x, y, z, nid); w.MarkDirty(x, y, z); }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        int age = meta & 7, n = 0;
        for (int j = 0; j < 3; j++) if (r.Next(15) <= age) n++;
        if (n > 0) drops.Add(new ItemStack(_seeds, n));
    }
    public override ItemStack PickStack(int meta) => new(_seeds, 1);
    public bool CanBoneMeal(World w, int x, int y, int z) => (w.GetMeta(x, y, z) & 7) < 7;
    public void BoneMeal(World w, int x, int y, int z, Rng r) => w.SetMeta(x, y, z, Math.Min(7, (w.GetMeta(x, y, z) & 7) + 2 + r.Next(4)));
}

// ======================= gąbka, pajęczyna =======================

/// <summary>Gąbka 1.8: sucha (0) wchłania wodę w promieniu 6 (max 65 bloków) i staje się mokra (1).</summary>
public sealed class SpongeBlock : Block
{
    private readonly int _dry, _wet;
    public SpongeBlock(byte id, string k, string n) : base(id, k, n)
    {
        _dry = TextureGen.Layer("sponge"); _wet = TextureGen.Layer("sponge_wet"); Tex = ModelBox.All(_dry); Vars(2);
    }
    public override int GetTexture(int face, int meta) => meta == 1 ? _wet : _dry;
    public override int DropMeta(int meta) => meta == 1 ? 1 : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, DropMeta(meta));
    public override string DisplayName(int meta) => meta == 1 ? "Mokra gąbka" : "Gąbka";
    public override void OnAdded(World w, int x, int y, int z) => TryAbsorb(w, x, y, z);
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) => TryAbsorb(w, x, y, z);
    private void TryAbsorb(World w, int x, int y, int z)
    {
        if (w.IsRemote || w.GetMeta(x, y, z) != 0) return;
        if (Absorb(w, x, y, z))
        {
            w.SetMeta(x, y, z, 1);
            w.PlayEffectBreak(x, y, z, B.Water, 0);
        }
    }
    private static bool Absorb(World w, int x, int y, int z)
    {
        var q = new Queue<(int, int, int, int)>(); var removed = new List<(int, int, int)>();
        q.Enqueue((x, y, z, 0)); int count = 0;
        while (q.Count > 0)
        {
            var (cx, cy, cz, d) = q.Dequeue();
            for (int f = 0; f < 6; f++)
            {
                int nx = cx + Dir.X[f], ny = cy + Dir.Y[f], nz = cz + Dir.Z[f];
                if (w.GetBlock(nx, ny, nz) != B.Water) continue;
                w.SetBlock(nx, ny, nz, B.Air, 0, false);
                removed.Add((nx, ny, nz)); count++;
                if (d < 6) q.Enqueue((nx, ny, nz, d + 1));
            }
            if (count > 64) break;
        }
        foreach (var (rx, ry, rz) in removed) w.NotifyNeighbors(rx, ry, rz, B.Air);
        return count > 0;
    }
}

/// <summary>Pajęczyna: spowalnia encje, miecz i nożyce kopią szybko i dają nić.</summary>
public sealed class WebBlock : Block
{
    public WebBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("web"); Opaque = false; Solid = false; FullCube = false; Render = RenderType.Cross; Layer = RenderLayer.Cutout; LightOpacity = 1;
        H(4f); Sound = SoundType.Stone; EntityInside = true;
    }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e) => e.InWeb = true;
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1, 1));
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        var t = tool?.Item?.Tool;
        if (t == ToolType.Shears || t == ToolType.Sword) drops.Add(new ItemStack(I.String, 1));
    }
}

// ======================= śnieg, lód =======================

/// <summary>Warstwa śniegu (BlockSnow): meta 0-7 = 1-8 warstw.</summary>
public sealed class SnowLayerBlock : Block
{
    public SnowLayerBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("snow"); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; H(0.1f); Tool = ToolType.Shovel; HarvestLevel = 0;
        Sound = SoundType.Wool; Replaceable = true; RandomTicks = true; NeedsSupport = true; Vars(8);
    }
    public override string DisplayName(int meta) => "Śnieg";
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        int l = meta & 7;
        if (l > 0) list.Add(new AABB(0, 0, 0, 1, l * 2 / 16.0, 1));
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, ((meta & 7) + 1) * 2 / 16.0, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        => boxes.Add(new ModelBox(0, 0, 0, 1, ((meta & 7) + 1) * 2 / 16f, 1, Tex));
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y - 1, z); var b = Block.Get(id);
        if (id == B.Ice || id == B.PackedIce) return false;
        if (b is LeavesBlock) return true;
        if (id == Id && (w.GetMeta(x, y - 1, z) & 7) == 7) return true;
        return b.Opaque && b.FullCube;
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (tool?.Item?.Tool == ToolType.Shovel) drops.Add(new ItemStack(I.Snowball, (meta & 7) + 2));
    }
    public override ItemStack PickStack(int meta) => new(Id, 1);
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetBlockLight(x, y, z) > 11) w.BreakBlock(x, y, z, false);
    }
}

public sealed class SnowBlockOw : Block
{
    public SnowBlockOw(byte id, string k, string n) : base(id, k, n) { RandomTicks = true; }
    public override int DropId(int meta, Rng r) => I.Snowball;
    public override int DropCount(Rng r) => 4;
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetBlockLight(x, y, z) > 11) w.BreakBlock(x, y, z, false);
    }
}

/// <summary>Lód: topnieje przy świetle bloków &gt; 11 - 3, rozbity nad czymś stałym zostawia wodę (BlockIce).</summary>
public sealed class IceBlockOw : Block
{
    [ThreadStatic] private static (int, int, int)? _broken;
    public IceBlockOw(byte id, string k, string n) : base(id, k, n) { RandomTicks = true; }
    public override int DropId(int meta, Rng r) => 0;
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    {
        if (EnchantHelper.GetLevel(p.Inventory.Selected, Ench.SilkTouch) > 0) return;
        _broken = (x, y, z);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (_broken != (x, y, z)) return;
        _broken = null;
        var below = Block.Get(w.GetBlock(x, y - 1, z));
        if (w.GetBlock(x, y, z) == B.Air && (below.Solid || below.IsLiquid)) w.SetBlock(x, y, z, B.Water);
    }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetBlockLight(x, y, z) > 11 - LightOpacity) w.SetBlock(x, y, z, B.Water);
    }
}

// ======================= szyby, mury, dywany =======================

/// <summary>Szyby i kraty (BlockPane): łączą się z szybami, szkłem, kratami i pełnymi blokami.</summary>
public sealed class PaneBlock : Block
{
    private readonly int[] _tex; private readonly int _edge; private readonly bool _colored;
    public PaneBlock(byte id, string k, string n, string tex, bool colored) : base(id, k, n)
    {
        _colored = colored;
        _tex = colored ? Enumerable.Range(0, 16).Select(i => TextureGen.Layer(tex + i)).ToArray() : new[] { TextureGen.Layer(tex) };
        _edge = id == B.IronBars ? _tex[0] : TextureGen.Layer("glass_pane_top");
        Tex = ModelBox.All(_tex[0]); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0;
        Layer = colored ? RenderLayer.Translucent : RenderLayer.Cutout;
        if (id == B.IronBars) { H(5f, 6); Tool = ToolType.Pickaxe; HarvestLevel = 0; Sound = SoundType.Metal; }
        else { H(0.3f); Sound = SoundType.Glass; }
        if (colored) Vars(16);
    }
    public override int GetTexture(int face, int meta) => _tex[_colored ? meta & 15 : 0];
    public override string DisplayName(int meta) => _colored ? Dyes.NamesF[meta & 15] + " szyba" : Name;
    public override int DropMeta(int meta) => _colored ? meta & 15 : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, DropMeta(meta));
    public override int DropId(int meta, Rng r) => Id == B.IronBars ? Id : 0;
    public static bool ConnectsTo(IBlockAccess w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z); var b = Block.Get(id);
        return b is PaneBlock || id == B.Glass || id == B.StainedGlass || b.Opaque && b.FullCube && id is not (B.Pumpkin or B.JackOLantern or B.Melon);
    }
    private void Shape(IBlockAccess w, int x, int y, int z, Action<float, float, float, float> add)
    {
        bool n = ConnectsTo(w, x, y, z - 1), s = ConnectsTo(w, x, y, z + 1), we = ConnectsTo(w, x - 1, y, z), e = ConnectsTo(w, x + 1, y, z);
        if (!n && !s && !we && !e) { add(0, 7, 16, 9); add(7, 0, 9, 16); return; }
        if (we || e) add(we ? 0 : 7, 7, e ? 16 : 9, 9);
        if (n || s) add(7, n ? 0 : 7, 9, s ? 16 : 9);
        if (!(we || e)) { }
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => Shape(w, x, y, z, (x0, z0, x1, z1) => list.Add(new AABB(x0 / 16.0, 0, z0 / 16.0, x1 / 16.0, 1, z1 / 16.0)));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => GetCollisionBoxes(w, x, y, z, meta, list);
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int t = GetTexture(0, meta);
        var tex = new[] { _edge, _edge, t, t, t, t };
        Shape(w, x, y, z, (x0, z0, x1, z1) => boxes.Add(ModelBox.Px(x0, 0, z0, x1, 16, z1, tex)));
    }
}

public sealed class StainedGlassBlock : ColoredBlock
{
    public StainedGlassBlock(byte id, string k) : base(id, k, Dyes.NamesN, " szkło", "glass_")
    {
        H(0.3f); Sound = SoundType.Glass; Opaque = false; Layer = RenderLayer.Translucent; LightOpacity = 0;
    }
    public override int DropId(int meta, Rng r) => 0;
}

/// <summary>Mur z bruku (BlockWall): 0 zwykły, 1 zamszony; łączy się z murami, furtkami i pełnymi blokami.</summary>
public sealed class WallBlock : Block
{
    private readonly int _cob, _mossy;
    public WallBlock(byte id, string k) : base(id, k, "Murek z bruku")
    {
        _cob = TextureGen.Layer("cobblestone"); _mossy = TextureGen.Layer("cobblestone_mossy"); Tex = ModelBox.All(_cob);
        Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; H(2f, 6); Tool = ToolType.Pickaxe; HarvestLevel = 0; Vars(2);
    }
    public override int GetTexture(int face, int meta) => meta == 1 ? _mossy : _cob;
    public override string DisplayName(int meta) => meta == 1 ? "Zamszony murek z bruku" : "Murek z bruku";
    public override int DropMeta(int meta) => meta == 1 ? 1 : 0;
    public override ItemStack PickStack(int meta) => new(Id, 1, DropMeta(meta));
    public static bool ConnectsTo(IBlockAccess w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z); var b = Block.Get(id);
        return b is WallBlock || b is FenceGateBlock || b.Opaque && b.FullCube && id is not (B.Pumpkin or B.JackOLantern or B.Melon);
    }
    private void Shape(IBlockAccess w, int x, int y, int z, Action<float, float, float, float, float, float> add)
    {
        bool n = ConnectsTo(w, x, y, z - 1), s = ConnectsTo(w, x, y, z + 1), we = ConnectsTo(w, x - 1, y, z), e = ConnectsTo(w, x + 1, y, z);
        bool straight = (n && s && !we && !e) || (we && e && !n && !s);
        if (straight && w.GetBlock(x, y + 1, z) == B.Air)
        {
            if (n) add(5, 0, 0, 11, 13, 16); else add(0, 0, 5, 16, 13, 11);
            return;
        }
        add(4, 0, 4, 12, 16, 12);
        if (n) add(5, 0, 0, 11, 13, 4);
        if (s) add(5, 0, 12, 11, 13, 16);
        if (we) add(0, 0, 5, 4, 13, 11);
        if (e) add(12, 0, 5, 16, 13, 11);
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => Shape(w, x, y, z, (x0, y0, z0, x1, y1, z1) => list.Add(new AABB(x0 / 16.0, 0, z0 / 16.0, x1 / 16.0, 1.5, z1 / 16.0)));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => Shape(w, x, y, z, (x0, y0, z0, x1, y1, z1) => list.Add(new AABB(x0 / 16.0, y0 / 16.0, z0 / 16.0, x1 / 16.0, y1 / 16.0, z1 / 16.0)));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var t = ModelBox.All(GetTexture(0, meta));
        Shape(w, x, y, z, (x0, y0, z0, x1, y1, z1) => boxes.Add(ModelBox.Px(x0, y0, z0, x1, y1, z1, t)));
    }
}

/// <summary>Dywan w 16 kolorach, wysokość 1/16.</summary>
public sealed class CarpetBlock : Block
{
    private readonly int[] _tex = new int[16];
    public CarpetBlock(byte id, string k) : base(id, k, "Dywan")
    {
        for (int i = 0; i < 16; i++) _tex[i] = TextureGen.Layer("wool_colored_" + i);
        Tex = ModelBox.All(_tex[0]); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0;
        H(0.1f); Sound = SoundType.Wool; NeedsSupport = true; Flammable = true; Vars(16);
    }
    public override int GetTexture(int face, int meta) => _tex[meta & 15];
    public override string DisplayName(int meta) => Blocks.NamesM[meta & 15] + " dywan";
    public override int DropMeta(int meta) => meta & 15;
    public override ItemStack PickStack(int meta) => new(Id, 1, meta & 15);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1 / 16.0, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes) => boxes.Add(new ModelBox(0, 0, 0, 1, 1 / 16f, 1, ModelBox.All(GetTexture(0, meta))));
    public override bool CanPlaceAt(World w, int x, int y, int z) => w.GetBlock(x, y - 1, z) != B.Air;
}
