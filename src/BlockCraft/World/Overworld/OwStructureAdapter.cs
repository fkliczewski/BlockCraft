using BlockCraft.Items;

namespace BlockCraft.World.Structures18;

// Adapter nie tworzy świata ani encji podczas generacji. Zapisuje wyłącznie plany OW.
public enum EnumFacing { NONE = -1, DOWN, UP, NORTH, SOUTH, WEST, EAST }
public enum FacingAxis { X, Y, Z }
public static class Facing
{
    public static readonly EnumFacing[] Values = { EnumFacing.DOWN, EnumFacing.UP, EnumFacing.NORTH, EnumFacing.SOUTH, EnumFacing.WEST, EnumFacing.EAST };
    public static EnumFacing GetFront(int i) => Values[Math.Abs(i % 6)];
    public static EnumFacing getHorizontal(int i) => new[] { EnumFacing.SOUTH, EnumFacing.WEST, EnumFacing.NORTH, EnumFacing.EAST }[Math.Abs(i % 4)];
    public static EnumFacing RandomHorizontal(JRandom r) => new[] { EnumFacing.NORTH, EnumFacing.EAST, EnumFacing.SOUTH, EnumFacing.WEST }[r.NextInt(4)];
    public static int getIndex(this EnumFacing f) => (int)f;
    public static bool NextBoolean(this JRandom r) => r.NextBool();
    public static EnumFacing rotateY(this EnumFacing f) => f switch { EnumFacing.NORTH => EnumFacing.EAST, EnumFacing.EAST => EnumFacing.SOUTH, EnumFacing.SOUTH => EnumFacing.WEST, _ => EnumFacing.NORTH };
    public static EnumFacing rotateYCCW(this EnumFacing f) => f.rotateY().getOpposite();
    public static int getHorizontalIndex(this EnumFacing f) => f switch { EnumFacing.SOUTH => 0, EnumFacing.WEST => 1, EnumFacing.NORTH => 2, _ => 3 };
    public static EnumFacing getOpposite(this EnumFacing f) => (EnumFacing)((int)f ^ 1);
    public static int XOffset(this EnumFacing f) => f == EnumFacing.WEST ? -1 : f == EnumFacing.EAST ? 1 : 0;
    public static int YOffset(this EnumFacing f) => f == EnumFacing.DOWN ? -1 : f == EnumFacing.UP ? 1 : 0;
    public static int ZOffset(this EnumFacing f) => f == EnumFacing.NORTH ? -1 : f == EnumFacing.SOUTH ? 1 : 0;
    public static FacingAxis getAxis(this EnumFacing f) => f is EnumFacing.WEST or EnumFacing.EAST ? FacingAxis.X : f is EnumFacing.UP or EnumFacing.DOWN ? FacingAxis.Y : FacingAxis.Z;
}

public class JavaList<T> : List<T>
{
    public JavaList() { }
    public JavaList(IEnumerable<T> values) : base(values) { }
    public void add(T value) => Add(value);
    public void addAll(IEnumerable<T> values) => AddRange(values);
    public int size() => Count;
    public bool isEmpty() => Count == 0;
    public T get(int index) => this[index];
    public T remove(int index) { T value = this[index]; RemoveAt(index); return value; }
    public bool remove(T value) => Remove(value);
    public void clear() => Clear();
    public JavaIterator<T> iterator() => new(this);
}
public class JavaIterator<T>
{
    private readonly JavaList<T> _list;
    private int _index;
    public JavaIterator(JavaList<T> list) { _list = list; }
    public bool hasNext() => _index < _list.Count;
    public T next() => _list[_index++];
    public void remove() => _list.RemoveAt(--_index);
}
public static class Collections
{
    public static void shuffle<T>(JavaList<T> values, JRandom r)
    { for (int n = values.Count; n > 1; n--) { int j = r.NextInt(n); (values[n - 1], values[j]) = (values[j], values[n - 1]); } }
}
public static class MathHelper
{
    public static int getRandomIntegerInRange(JRandom r, int min, int max) => min >= max ? min : r.NextInt(max - min + 1) + min;
}
public class BlockPos
{
    public int X, Y, Z;
    public BlockPos(int x, int y, int z) { X = x; Y = y; Z = z; }
    public int getX() => X;
    public int getY() => Y;
    public int getZ() => Z;
    public BlockPos down() => new(X, Y - 1, Z);
    public BlockPos up() => new(X, Y + 1, Z);
    public class MutableBlockPos : BlockPos
    {
        public MutableBlockPos() : base(0, 0, 0) { }
        public void set(int x, int y, int z) { X = x; Y = y; Z = z; }
    }
}
public class StructureBoundingBox
{
    public int minX, minY, minZ, maxX, maxY, maxZ;
    public StructureBoundingBox(int x0, int y0, int z0, int x1, int y1, int z1)
    { minX = x0; minY = y0; minZ = z0; maxX = x1; maxY = y1; maxZ = z1; }
    public StructureBoundingBox(int x0, int z0, int x1, int z1) : this(x0, 1, z0, x1, 512, z1) { }
    public static StructureBoundingBox getComponentToAddBoundingBox(int x, int y, int z, int ox, int oy, int oz, int sx, int sy, int sz, EnumFacing f) => f switch
    {
        EnumFacing.NORTH => new(x + ox, y + oy, z - sz + 1 + oz, x + sx - 1 + ox, y + sy - 1 + oy, z + oz),
        EnumFacing.WEST => new(x - sz + 1 + oz, y + oy, z + ox, x + oz, y + sy - 1 + oy, z + sx - 1 + ox),
        EnumFacing.EAST => new(x + oz, y + oy, z + ox, x + sz - 1 + oz, y + sy - 1 + oy, z + sx - 1 + ox),
        _ => new(x + ox, y + oy, z + oz, x + sx - 1 + ox, y + sy - 1 + oy, z + sz - 1 + oz)
    };
    public static StructureBoundingBox func_175899_a(int x0, int y0, int z0, int x1, int y1, int z1) => new(Math.Min(x0, x1), Math.Min(y0, y1), Math.Min(z0, z1), Math.Max(x0, x1), Math.Max(y0, y1), Math.Max(z0, z1));
    public int getXSize() => maxX - minX + 1;
    public int getYSize() => maxY - minY + 1;
    public int getZSize() => maxZ - minZ + 1;
    public void offset(int x, int y, int z) { minX += x; maxX += x; minY += y; maxY += y; minZ += z; maxZ += z; }
    public bool isVecInside(BlockPos p) => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY && p.Z >= minZ && p.Z <= maxZ;
    public bool intersectsWith(StructureBoundingBox b) => maxX >= b.minX && minX <= b.maxX && maxY >= b.minY && minY <= b.maxY && maxZ >= b.minZ && minZ <= b.maxZ;
    public bool intersectsWith(int x0, int z0, int x1, int z1) => maxX >= x0 && minX <= x1 && maxZ >= z0 && minZ <= z1;
    public OwBox ToBox() => new(minX, minY, minZ, maxX, maxY, maxZ);
}
public enum Material { air, solid, water, lava }
public sealed class Block
{
    public readonly int Id;
    public Block(int id) { Id = id; }
    public IBlockState getDefaultState() => new(this, Id is B.Furnace or B.Chest or B.Ladder ? 2 : Id == B.Torch ? 5 : 0);
    public IBlockState getStateFromMeta(int meta) => new(this, meta);
    public Material getMaterial() => Id switch { B.Air => Material.air, B.Water => Material.water, B.Lava => Material.lava, _ => Material.solid };
}
public readonly record struct IBlockState(Block Block, int Meta)
{
    public Block getBlock() => Block;
    public IBlockState withProperty(string property, object value)
    {
        if (property == "eye") return new(Block, Meta | ((bool)value ? 4 : 0));
        if (property == "facing")
        {
            var f = (EnumFacing)value;
            int m = Block.Id == B.Torch ? f switch { EnumFacing.EAST => 1, EnumFacing.WEST => 2, EnumFacing.SOUTH => 3, EnumFacing.NORTH => 4, _ => 5 }
                : f switch { EnumFacing.EAST => 0, EnumFacing.WEST => 1, EnumFacing.SOUTH => 2, _ => 3 };
            return new(Block, Block.Id == B.Torch ? m : (Meta & ~3) | m);
        }
        return this;
    }
    public object getValue(string property) => (Meta & 3) switch { 0 => EnumFacing.EAST, 1 => EnumFacing.WEST, 2 => EnumFacing.SOUTH, _ => EnumFacing.NORTH };
}
public static class BlockStairs { public const string FACING = "facing"; }
public static class BlockTorch { public const string FACING = "facing"; }
public static class BlockEndPortalFrame { public const string EYE = "eye"; }
public static class BlockPrismarine { public const int ROUGH_META = 0, BRICKS_META = 1, DARK_META = 2; }
public static class BlockStoneBrick { public const int MOSSY_META = 1, CRACKED_META = 2; }
public static class EnumDyeColor { public static readonly Variant WHITE = new(0); }
public readonly record struct Variant(int Meta) { public int getMetadata() => Meta; public int getDyeDamage() => 15 - Meta; }
public static class BlockSandStone { public static class EnumType { public static readonly Variant DEFAULT = new(0), SMOOTH = new(2); } }
public static class BlockStoneSlab { public static class EnumType { public static readonly Variant STONE = new(0), SMOOTHBRICK = new(5); } }
public static class BlockSilverfish { public static class EnumType { public static readonly Variant STONEBRICK = new(2); } }

public sealed class WorldChunkManager
{
    public readonly OwBiomeProvider Biomes;
    public WorldChunkManager(OwBiomeProvider biomes) { Biomes = biomes; }
    public bool areBiomesViable(int x, int z, int radius, ICollection<int> ids) => Biomes.AreBiomesViable(x, z, radius, ids);
    public int getBiomeGenerator(BlockPos p, int fallback) => Biomes.BiomeAt(p.X, p.Z);
}
public static class BiomeGenBase { public const int desert = Biome.Desert, desertHills = Biome.DesertHills, field_180279_ad = Biome.Plains; }
public static class MapGenVillage { public static readonly int[] villageSpawnBiomes = { Biome.Plains, Biome.Desert, Biome.Savanna }; }
public sealed class StructureWorld
{
    public readonly OwGenRegion Region;
    public readonly OwGenerator Generator;
    public readonly JRandom EntityRandom;
    public StructureWorld(OwGenerator generator, OwGenRegion region) { Generator = generator; Region = region; EntityRandom = new JRandom(generator.Seed); }
    public int getSeaLevel() => 63;
    public IBlockState getBlockState(BlockPos p) => new(Blocks.ById[Region.Get(p.X, p.Y, p.Z)], Region.GetMeta(p.X, p.Y, p.Z));
    public void setBlockState(BlockPos p, IBlockState b, int flags)
    {
        int id = b.Block.Id, meta = b.Meta;
        if (id == B.StoneSlab)
        {
            id = (meta & 7) switch { 1 => B.SandstoneSlab, 3 => B.CobbleSlab, 4 => B.BrickSlab, 5 => B.StoneBrickSlab, _ => id };
            meta = (meta & 8) >> 3;
        }
        if (id is B.OakStairs or B.CobbleStairs or B.SandstoneStairs or B.StoneBrickStairs)
            meta = (meta & ~3) | ((meta & 3) switch { 0 => 3, 1 => 1, 2 => 0, _ => 2 });
        if (id is B.Chest or B.Furnace) meta = meta switch { 3 => 0, 4 => 1, 5 => 3, _ => 2 };
        if (id == B.Torch && meta == 5) meta = 0;
        Region.Set(p.X, p.Y, p.Z, id, meta);
    }
    public BlockPos getTopSolidOrLiquidBlock(BlockPos p)
    {
        int y = 255;
        while (y > 0) { int id = Region.Get(p.X, y, p.Z); var b = BlockCraft.World.Block.Get(id); if (id is B.Water or B.Lava || b.Solid && b is not LeavesBlock) break; y--; }
        return new(p.X, y + 1, p.Z);
    }
    public BlockPos getTopSolidOrLiquidBlock(int x, int z) => new(x, Region.TopSolid(x, z), z);
    public readonly WorldProvider provider = new();
    public class WorldProvider { public int getAverageGroundLevel() => 64; }
}
public class WeightedRandomChestContent
{
    public readonly OwLoot.Entry Entry;
    public Ench? Enchantment;
    public int Level;
    public WeightedRandomChestContent(int id, int meta, int min, int max, int weight) { Entry = new(id, min, max, weight, meta); }
    public static JavaList<WeightedRandomChestContent> func_177629_a(JavaList<WeightedRandomChestContent> entries, params WeightedRandomChestContent[] extra) => new(entries.Concat(extra));
}
public static class Item { public static int getItemFromBlock(Block b) => b.Id; }

public abstract class StructureComponent
{
    public StructureBoundingBox boundingBox;
    public EnumFacing coordBaseMode = EnumFacing.NONE;
    public int componentType;
    protected StructureComponent() { }
    protected StructureComponent(int type) { componentType = type; }
    public StructureBoundingBox getBoundingBox() => boundingBox;
    public int getComponentType() => componentType;
    public virtual BlockPos getBoundingBoxCenter() => new((boundingBox.minX + boundingBox.maxX + 1) / 2, (boundingBox.minY + boundingBox.maxY + 1) / 2, (boundingBox.minZ + boundingBox.maxZ + 1) / 2);
    public static StructureComponent findIntersecting(JavaList<StructureComponent> pieces, StructureBoundingBox b) => pieces.FirstOrDefault(p => p.boundingBox.intersectsWith(b));
    public virtual void buildComponent(StructureComponent root, JavaList<StructureComponent> pieces, JRandom r) { }
    public abstract bool addComponentParts(StructureWorld w, JRandom r, StructureBoundingBox clip);
    public int getXWithOffset(int x, int z) => coordBaseMode switch { EnumFacing.NORTH or EnumFacing.SOUTH => boundingBox.minX + x, EnumFacing.WEST => boundingBox.maxX - z, EnumFacing.EAST => boundingBox.minX + z, _ => x };
    public int getYWithOffset(int y) => coordBaseMode == EnumFacing.NONE ? y : boundingBox.minY + y;
    public int getZWithOffset(int x, int z) => coordBaseMode switch { EnumFacing.NORTH => boundingBox.maxZ - z, EnumFacing.SOUTH => boundingBox.minZ + z, EnumFacing.WEST or EnumFacing.EAST => boundingBox.minZ + x, _ => z };
    public int getMetadataWithOffset(Block b, int meta) => new OwStructureWriter(null, null, 0, 0, 0, 0, 0, (int)coordBaseMode).Meta(b.Id, meta);
    public virtual void setBlockState(StructureWorld w, IBlockState state, int x, int y, int z, StructureBoundingBox clip)
    { var p = new BlockPos(getXWithOffset(x, z), getYWithOffset(y), getZWithOffset(x, z)); if (clip.isVecInside(p)) w.setBlockState(p, state, 2); }
    public IBlockState getBlockStateFromPos(StructureWorld w, int x, int y, int z, StructureBoundingBox clip)
    { var p = new BlockPos(getXWithOffset(x, z), getYWithOffset(y), getZWithOffset(x, z)); return clip.isVecInside(p) ? w.getBlockState(p) : Blocks.air.getDefaultState(); }
    public void fillWithAir(StructureWorld w, StructureBoundingBox c, int x0, int y0, int z0, int x1, int y1, int z1) => fillWithBlocks(w, c, x0, y0, z0, x1, y1, z1, Blocks.air.getDefaultState(), Blocks.air.getDefaultState(), false);
    public virtual void fillWithBlocks(StructureWorld w, StructureBoundingBox c, int x0, int y0, int z0, int x1, int y1, int z1, IBlockState edge, IBlockState inside, bool existing)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
        { if (existing && getBlockStateFromPos(w, x, y, z, c).Block.Id == B.Air) continue; setBlockState(w, x == x0 || x == x1 || y == y0 || y == y1 || z == z0 || z == z1 ? edge : inside, x, y, z, c); }
    }
    public void fillWithRandomizedBlocks(StructureWorld w, StructureBoundingBox c, int x0, int y0, int z0, int x1, int y1, int z1, bool existing, JRandom r, BlockSelector selector)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
        { if (existing && getBlockStateFromPos(w, x, y, z, c).Block.Id == B.Air) continue; selector.selectBlocks(r, x, y, z, x == x0 || x == x1 || y == y0 || y == y1 || z == z0 || z == z1); setBlockState(w, selector.blockstate, x, y, z, c); }
    }
    public void func_175805_a(StructureWorld w, StructureBoundingBox c, JRandom r, float chance, int x0, int y0, int z0, int x1, int y1, int z1, IBlockState edge, IBlockState inside, bool existing)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
        { if (r.NextFloat() > chance || existing && getBlockStateFromPos(w, x, y, z, c).Block.Id == B.Air) continue; setBlockState(w, x == x0 || x == x1 || y == y0 || y == y1 || z == z0 || z == z1 ? edge : inside, x, y, z, c); }
    }
    public void randomlyPlaceBlock(StructureWorld w, StructureBoundingBox c, JRandom r, float chance, int x, int y, int z, IBlockState b)
    { if (r.NextFloat() < chance) setBlockState(w, b, x, y, z, c); }
    public void clearCurrentPositionBlocksUpwards(StructureWorld w, int x, int y, int z, StructureBoundingBox c)
    { while (getYWithOffset(y) < 255 && getBlockStateFromPos(w, x, y, z, c).Block.Id != B.Air) setBlockState(w, Blocks.air.getDefaultState(), x, y++, z, c); }
    public virtual void replaceAirAndLiquidDownwards(StructureWorld w, IBlockState b, int x, int y, int z, StructureBoundingBox c)
    { while (getYWithOffset(y) > 1 && getBlockStateFromPos(w, x, y, z, c).Block.getMaterial() is Material.air or Material.water or Material.lava) setBlockState(w, b, x, y--, z, c); }
    public bool generateChestContents(StructureWorld w, StructureBoundingBox c, JRandom r, int x, int y, int z, JavaList<WeightedRandomChestContent> table, int rolls)
    {
        var p = new BlockPos(getXWithOffset(x, z), getYWithOffset(y), getZWithOffset(x, z));
        if (!c.isVecInside(p) || w.Region.Get(p.X, p.Y, p.Z) == B.Chest) return false;
        var book = table.FirstOrDefault(e => e.Enchantment != null);
        w.Region.Set(p.X, p.Y, p.Z, B.Chest, 2);
        w.Region.Tile(p.X, p.Y, p.Z, new OwTilePlan { BlockId = B.Chest, TypeId = 2, Slots = OwLoot.Generate(r, table.Select(e => e.Entry).ToArray(), rolls, 27, book?.Enchantment, book?.Level ?? 0) });
        return true;
    }
    public void placeDoorCurrentPosition(StructureWorld w, StructureBoundingBox c, JRandom r, int x, int y, int z, EnumFacing facing)
    { int meta = facing.getHorizontalIndex(); setBlockState(w, Blocks.oak_door.getStateFromMeta(meta), x, y, z, c); setBlockState(w, Blocks.oak_door.getStateFromMeta(8), x, y + 1, z, c); }
    public bool isLiquidInStructureBoundingBox(StructureWorld w, StructureBoundingBox c)
    {
        int x0 = Math.Max(boundingBox.minX - 1, c.minX), x1 = Math.Min(boundingBox.maxX + 1, c.maxX), y0 = Math.Max(boundingBox.minY - 1, c.minY), y1 = Math.Min(boundingBox.maxY + 1, c.maxY), z0 = Math.Max(boundingBox.minZ - 1, c.minZ), z1 = Math.Min(boundingBox.maxZ + 1, c.maxZ);
        bool Liquid(int x, int y, int z) => w.Region.Get(x, y, z) is B.Water or B.Lava;
        for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) if (Liquid(x, y0, z) || Liquid(x, y1, z)) return true;
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) if (Liquid(x, y, z0) || Liquid(x, y, z1)) return true;
        for (int z = z0; z <= z1; z++) for (int y = y0; y <= y1; y++) if (Liquid(x0, y, z) || Liquid(x1, y, z)) return true;
        return false;
    }
    public void func_181138_a(int x, int y, int z) => boundingBox.offset(x, y, z);
    public abstract class BlockSelector
    {
        public IBlockState blockstate = Blocks.air.getDefaultState();
        public abstract void selectBlocks(JRandom r, int x, int y, int z, bool boundary);
        public IBlockState getBlockState() => blockstate;
    }
}
