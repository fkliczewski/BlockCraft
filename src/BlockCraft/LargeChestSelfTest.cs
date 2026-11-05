using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Duża skrzynia: podział indeksów między połowy i okno z sześcioma rzędami.</summary>
public static partial class SelfTest
{
    private static void TestLargeChest()
    {
        Console.WriteLine("[Duża skrzynia]");
        var first = new ChestTileEntity();
        var second = new ChestTileEntity();
        var large = new LargeChestInventory(first, second);
        large.Set(5, new ItemStack(I.Diamond, 1));
        large.Set(27 + 3, new ItemStack(I.Diamond, 2));
        Check(large.Size == 54, "Duża skrzynia ma 54 sloty");
        Check(first.Slots[5]?.Count == 1 && second.Slots[3]?.Count == 2, "Indeksy 27+ trafiają do drugiej skrzyni");
        Check(large.Get(30)?.Count == 2, "Odczyt z drugiej połowy");

        var c = new ChestContainer(new PlayerInventory(), large, "Duża skrzynia");
        Check(c.Height == 114 + 6 * 18, "Okno dużej skrzyni: 6 rzędów");
        Check(c.FirstPlayerSlot == 54 && c.Slots[54].Y == 85 + 54, "Sloty gracza przesunięte o 3 rzędy");
    }
}
