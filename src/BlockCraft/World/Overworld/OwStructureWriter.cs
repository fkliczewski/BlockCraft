using BlockCraft.Items;

namespace BlockCraft.World;

public readonly record struct OwState(int Id, int Meta = 0);

/// <summary>Lokalne współrzędne komponentu i przekształcenia StructureComponent z 1.8.</summary>
public sealed class OwStructureWriter
{
    public readonly OwGenRegion Region;
    public readonly JRandom Random;
    public readonly int X, Y, Z, Width, Depth, Direction;
    public OwStructureWriter(OwGenRegion region, JRandom random, int x, int y, int z, int width, int depth, int direction)
    { Region = region; Random = random; X = x; Y = y; Z = z; Width = width; Depth = depth; Direction = direction; }
    public (int x, int y, int z) Point(int x, int y, int z) => Direction switch
    {
        2 => (X + x, Y + y, Z + Depth - 1 - z),
        4 => (X + Depth - 1 - z, Y + y, Z + x),
        5 => (X + z, Y + y, Z + x),
        _ => (X + x, Y + y, Z + z)
    };
    public int Get(int x, int y, int z) { var p = Point(x, y, z); return Region.Get(p.x, p.y, p.z); }
    public void Set(OwState state, int x, int y, int z)
    {
        var p = Point(x, y, z); int meta = state.Meta;
        // Schody fundamentu używają kolejności S,W,N,E, MC przechowuje E,W,S,N.
        if (state.Id is B.OakStairs or B.CobbleStairs or B.SandstoneStairs or B.StoneBrickStairs)
            meta = (meta & ~3) | ((meta & 3) switch { 0 => 3, 1 => 1, 2 => 0, _ => 2 });
        Region.Set(p.x, p.y, p.z, state.Id, meta);
    }
    public void Fill(int x0, int y0, int z0, int x1, int y1, int z1, OwState edge, OwState inside, bool existing = false)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
        {
            if (existing && Get(x, y, z) == B.Air) continue;
            Set(y == y0 || y == y1 || x == x0 || x == x1 || z == z0 || z == z1 ? edge : inside, x, y, z);
        }
    }
    public void Air(int x0, int y0, int z0, int x1, int y1, int z1) => Fill(x0, y0, z0, x1, y1, z1, new(B.Air), new(B.Air));
    public void RandomFill(int x0, int y0, int z0, int x1, int y1, int z1)
    {
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
            Set(new(Random.NextFloat() < 0.4f ? B.Cobblestone : B.MossyCobblestone), x, y, z);
    }
    public void Down(OwState state, int x, int y, int z)
    {
        var p = Point(x, y, z);
        while (p.y > 1 && (Region.Get(p.x, p.y, p.z) == B.Air || Block.Get(Region.Get(p.x, p.y, p.z)).IsLiquid)) Region.Set(p.x, p.y--, p.z, state.Id, state.Meta);
    }
    public int Meta(int block, int meta)
    {
        if (block == B.Rail && Direction is 4 or 5) return meta == 1 ? 0 : 1;
        bool stairs = block is B.CobbleStairs or B.OakStairs or B.SandstoneStairs or B.StoneBrickStairs;
        if (stairs) return Direction switch
        {
            3 => meta switch { 2 => 3, 3 => 2, _ => meta },
            4 => meta switch { 0 => 2, 1 => 3, 2 => 0, 3 => 1, _ => meta },
            5 => meta switch { 0 => 2, 1 => 3, 2 => 1, 3 => 0, _ => meta },
            _ => meta
        };
        if (block is B.TripwireHook or B.Repeater or B.RepeaterOn)
            return Direction switch
            {
                3 => meta switch { 0 => 2, 2 => 0, _ => meta },
                4 => meta switch { 2 => 1, 0 => 3, 1 => 2, 3 => 0, _ => meta },
                5 => meta switch { 2 => 3, 0 => 1, 1 => 2, 3 => 0, _ => meta },
                _ => meta
            };
        if (block is B.Ladder or B.Dispenser or B.Piston or B.StickyPiston or B.Lever)
            return Direction switch
            {
                3 => meta switch { 2 => 3, 3 => 2, _ => meta },
                4 => meta switch { 2 => 4, 3 => 5, 4 => 2, 5 => 3, _ => meta },
                5 => meta switch { 2 => 5, 3 => 4, 4 => 2, 5 => 3, _ => meta },
                _ => meta
            };
        if (block is B.OakDoor or B.IronDoor) return Direction switch { 3 => meta switch { 0 => 2, 2 => 0, _ => meta }, 4 => (meta + 1) & 3, 5 => (meta + 3) & 3, _ => meta };
        return meta;
    }
    public int LeverMeta(int facing) => Meta(B.Lever, facing) switch { 2 => 4, 3 => 3, 4 => 2, 5 => 1, _ => 0 };
    public void Chest(int x, int y, int z, OwLoot.Entry[] table, int rolls)
    { var p = Point(x, y, z); OwLoot.Chest(Region, Random, p.x, p.y, p.z, table, rolls); }
    public void Dispenser(int x, int y, int z, int facing)
    {
        var p = Point(x, y, z); Region.Set(p.x, p.y, p.z, B.Dispenser, Meta(B.Dispenser, facing));
        Region.Tile(p.x, p.y, p.z, new OwTilePlan { BlockId = B.Dispenser, TypeId = -1, Slots = OwLoot.Generate(Random, new[] { new OwLoot.Entry(I.Arrow, 2, 7, 30) }, 2, 9) });
    }
}
