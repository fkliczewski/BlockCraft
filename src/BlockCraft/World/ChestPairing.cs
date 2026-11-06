using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>Łączenie sąsiednich skrzyń w dużą skrzynię (reguła świata, wspólna dla okien i mechanizmów).</summary>
public static class ChestPairing
{
    private static readonly (int dx, int dz)[] HorizontalNeighbours = { (-1, 0), (0, -1), (1, 0), (0, 1) };

    /// <summary>Skrzynia albo duża skrzynia, gdy skrzynia i jej jedyny sąsiad tego samego rodzaju nie mają innych sąsiadów
    /// (jak w 1.8 najwyżej dwie obok siebie; dłuższy rząd ze starych zapisów = osobne małe skrzynie); zachodnia/północna połowa pierwsza.</summary>
    public static IInventory Inventory(World w, ChestTileEntity chest, int x, int y, int z)
    {
        int block = w.GetBlock(x, y, z);
        var neighbours = Neighbours(w, block, x, y, z);
        if (neighbours.Count == 1)
        {
            var (dx, dz, other) = neighbours[0];
            if (Neighbours(w, block, x + dx, y, z + dz).Count == 1)
                return dx + dz < 0 ? new LargeChestInventory(other, chest) : new LargeChestInventory(chest, other);
        }
        return chest;
    }

    private static List<(int dx, int dz, ChestTileEntity chest)> Neighbours(World w, int block, int x, int y, int z) =>
        HorizontalNeighbours
            .Where(d => w.GetBlock(x + d.dx, y, z + d.dz) == block)
            .Select(d => (d.dx, d.dz, chest: w.GetTileEntity(x + d.dx, y, z + d.dz) as ChestTileEntity))
            .Where(n => n.chest != null)
            .ToList();
}
