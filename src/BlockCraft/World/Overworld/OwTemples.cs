namespace BlockCraft.World;

/// <summary>Geometria komponentów pustynnej i dżunglowej świątyni oraz chatki z 1.8.</summary>
public static class OwTemples
{
    private static OwState S(int id, int meta = 0) => new(id, meta);
    public static void Desert(OwStructureWriter s, JRandom r)
    {
         s.Fill( 0, -4, 0, 21 - 1, 0, 21 - 1, S(B.Sandstone), S(B.Sandstone), false);
         for (int i = 1; i <= 9; ++i)
        {
            s.Fill( i, i, i, 21 - 1 - i, i, 21 - 1 - i, S(B.Sandstone), S(B.Sandstone), false);
            s.Fill( i + 1, i, i + 1, 21 - 2 - i, i, 21 - 2 - i, S(B.Air), S(B.Air), false);
        }
         for (int j2 = 0; j2 < 21; ++j2)
        {
            for (int j = 0; j < 21; ++j)
            {
                int k = -5;
                s.Down( S(B.Sandstone), j2, k, j);
            }
        }
         int k2 = s.Meta(B.SandstoneStairs, 3);
        int l2 = s.Meta(B.SandstoneStairs, 2);
        int i3 = s.Meta(B.SandstoneStairs, 0);
        int l = s.Meta(B.SandstoneStairs, 1);
        int i1 = 1;
        int j1 = 11;
        s.Fill( 0, 0, 0, 4, 9, 4, S(B.Sandstone), S(B.Air), false);
        s.Fill( 1, 10, 1, 3, 10, 3, S(B.Sandstone), S(B.Sandstone), false);
        s.Set( S(B.SandstoneStairs, k2), 2, 10, 0);
        s.Set( S(B.SandstoneStairs, l2), 2, 10, 4);
        s.Set( S(B.SandstoneStairs, i3), 0, 10, 2);
        s.Set( S(B.SandstoneStairs, l), 4, 10, 2);
        s.Fill( 21 - 5, 0, 0, 21 - 1, 9, 4, S(B.Sandstone), S(B.Air), false);
        s.Fill( 21 - 4, 10, 1, 21 - 2, 10, 3, S(B.Sandstone), S(B.Sandstone), false);
        s.Set( S(B.SandstoneStairs, k2), 21 - 3, 10, 0);
        s.Set( S(B.SandstoneStairs, l2), 21 - 3, 10, 4);
        s.Set( S(B.SandstoneStairs, i3), 21 - 5, 10, 2);
        s.Set( S(B.SandstoneStairs, l), 21 - 1, 10, 2);
        s.Fill( 8, 0, 0, 12, 4, 4, S(B.Sandstone), S(B.Air), false);
        s.Fill( 9, 1, 0, 11, 3, 4, S(B.Air), S(B.Air), false);
        s.Set( S(B.Sandstone, 2), 9, 1, 1);
        s.Set( S(B.Sandstone, 2), 9, 2, 1);
        s.Set( S(B.Sandstone, 2), 9, 3, 1);
        s.Set( S(B.Sandstone, 2), 10, 3, 1);
        s.Set( S(B.Sandstone, 2), 11, 3, 1);
        s.Set( S(B.Sandstone, 2), 11, 2, 1);
        s.Set( S(B.Sandstone, 2), 11, 1, 1);
        s.Fill( 4, 1, 1, 8, 3, 3, S(B.Sandstone), S(B.Air), false);
        s.Fill( 4, 1, 2, 8, 2, 2, S(B.Air), S(B.Air), false);
        s.Fill( 12, 1, 1, 16, 3, 3, S(B.Sandstone), S(B.Air), false);
        s.Fill( 12, 1, 2, 16, 2, 2, S(B.Air), S(B.Air), false);
        s.Fill( 5, 4, 5, 21 - 6, 4, 21 - 6, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 9, 4, 9, 11, 4, 11, S(B.Air), S(B.Air), false);
        s.Fill( 8, 1, 8, 8, 3, 8, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 12, 1, 8, 12, 3, 8, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 8, 1, 12, 8, 3, 12, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 12, 1, 12, 12, 3, 12, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 1, 1, 5, 4, 4, 11, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 21 - 5, 1, 5, 21 - 2, 4, 11, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 6, 7, 9, 6, 7, 11, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 21 - 7, 7, 9, 21 - 7, 7, 11, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 5, 5, 9, 5, 7, 11, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 21 - 6, 5, 9, 21 - 6, 7, 11, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Set( S(B.Air), 5, 5, 10);
        s.Set( S(B.Air), 5, 6, 10);
        s.Set( S(B.Air), 6, 6, 10);
        s.Set( S(B.Air), 21 - 6, 5, 10);
        s.Set( S(B.Air), 21 - 6, 6, 10);
        s.Set( S(B.Air), 21 - 7, 6, 10);
        s.Fill( 2, 4, 4, 2, 6, 4, S(B.Air), S(B.Air), false);
        s.Fill( 21 - 3, 4, 4, 21 - 3, 6, 4, S(B.Air), S(B.Air), false);
        s.Set( S(B.SandstoneStairs, k2), 2, 4, 5);
        s.Set( S(B.SandstoneStairs, k2), 2, 3, 4);
        s.Set( S(B.SandstoneStairs, k2), 21 - 3, 4, 5);
        s.Set( S(B.SandstoneStairs, k2), 21 - 3, 3, 4);
        s.Fill( 1, 1, 3, 2, 2, 3, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 21 - 3, 1, 3, 21 - 2, 2, 3, S(B.Sandstone), S(B.Sandstone), false);
        s.Set( S(B.SandstoneStairs), 1, 1, 2);
        s.Set( S(B.SandstoneStairs), 21 - 2, 1, 2);
        s.Set( S(B.SandstoneSlab, 0), 1, 2, 2);
        s.Set( S(B.SandstoneSlab, 0), 21 - 2, 2, 2);
        s.Set( S(B.SandstoneStairs, l), 2, 1, 2);
        s.Set( S(B.SandstoneStairs, i3), 21 - 3, 1, 2);
        s.Fill( 4, 3, 5, 4, 3, 18, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 21 - 5, 3, 5, 21 - 5, 3, 17, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 3, 1, 5, 4, 2, 16, S(B.Air), S(B.Air), false);
        s.Fill( 21 - 6, 1, 5, 21 - 5, 2, 16, S(B.Air), S(B.Air), false);
         for (int k1 = 5; k1 <= 17; k1 += 2)
        {
            s.Set( S(B.Sandstone, 2), 4, 1, k1);
            s.Set( S(B.Sandstone, 1), 4, 2, k1);
            s.Set( S(B.Sandstone, 2), 21 - 5, 1, k1);
            s.Set( S(B.Sandstone, 1), 21 - 5, 2, k1);
        }
         s.Set( S(B.StainedClay, i1), 10, 0, 7);
        s.Set( S(B.StainedClay, i1), 10, 0, 8);
        s.Set( S(B.StainedClay, i1), 9, 0, 9);
        s.Set( S(B.StainedClay, i1), 11, 0, 9);
        s.Set( S(B.StainedClay, i1), 8, 0, 10);
        s.Set( S(B.StainedClay, i1), 12, 0, 10);
        s.Set( S(B.StainedClay, i1), 7, 0, 10);
        s.Set( S(B.StainedClay, i1), 13, 0, 10);
        s.Set( S(B.StainedClay, i1), 9, 0, 11);
        s.Set( S(B.StainedClay, i1), 11, 0, 11);
        s.Set( S(B.StainedClay, i1), 10, 0, 12);
        s.Set( S(B.StainedClay, i1), 10, 0, 13);
        s.Set( S(B.StainedClay, j1), 10, 0, 10);
         for (int j3 = 0; j3 <= 21 - 1; j3 += 21 - 1)
        {
            s.Set( S(B.Sandstone, 2), j3, 2, 1);
            s.Set( S(B.StainedClay, i1), j3, 2, 2);
            s.Set( S(B.Sandstone, 2), j3, 2, 3);
            s.Set( S(B.Sandstone, 2), j3, 3, 1);
            s.Set( S(B.StainedClay, i1), j3, 3, 2);
            s.Set( S(B.Sandstone, 2), j3, 3, 3);
            s.Set( S(B.StainedClay, i1), j3, 4, 1);
            s.Set( S(B.Sandstone, 1), j3, 4, 2);
            s.Set( S(B.StainedClay, i1), j3, 4, 3);
            s.Set( S(B.Sandstone, 2), j3, 5, 1);
            s.Set( S(B.StainedClay, i1), j3, 5, 2);
            s.Set( S(B.Sandstone, 2), j3, 5, 3);
            s.Set( S(B.StainedClay, i1), j3, 6, 1);
            s.Set( S(B.Sandstone, 1), j3, 6, 2);
            s.Set( S(B.StainedClay, i1), j3, 6, 3);
            s.Set( S(B.StainedClay, i1), j3, 7, 1);
            s.Set( S(B.StainedClay, i1), j3, 7, 2);
            s.Set( S(B.StainedClay, i1), j3, 7, 3);
            s.Set( S(B.Sandstone, 2), j3, 8, 1);
            s.Set( S(B.Sandstone, 2), j3, 8, 2);
            s.Set( S(B.Sandstone, 2), j3, 8, 3);
        }
         for (int k3 = 2; k3 <= 21 - 3; k3 += 21 - 3 - 2)
        {
            s.Set( S(B.Sandstone, 2), k3 - 1, 2, 0);
            s.Set( S(B.StainedClay, i1), k3, 2, 0);
            s.Set( S(B.Sandstone, 2), k3 + 1, 2, 0);
            s.Set( S(B.Sandstone, 2), k3 - 1, 3, 0);
            s.Set( S(B.StainedClay, i1), k3, 3, 0);
            s.Set( S(B.Sandstone, 2), k3 + 1, 3, 0);
            s.Set( S(B.StainedClay, i1), k3 - 1, 4, 0);
            s.Set( S(B.Sandstone, 1), k3, 4, 0);
            s.Set( S(B.StainedClay, i1), k3 + 1, 4, 0);
            s.Set( S(B.Sandstone, 2), k3 - 1, 5, 0);
            s.Set( S(B.StainedClay, i1), k3, 5, 0);
            s.Set( S(B.Sandstone, 2), k3 + 1, 5, 0);
            s.Set( S(B.StainedClay, i1), k3 - 1, 6, 0);
            s.Set( S(B.Sandstone, 1), k3, 6, 0);
            s.Set( S(B.StainedClay, i1), k3 + 1, 6, 0);
            s.Set( S(B.StainedClay, i1), k3 - 1, 7, 0);
            s.Set( S(B.StainedClay, i1), k3, 7, 0);
            s.Set( S(B.StainedClay, i1), k3 + 1, 7, 0);
            s.Set( S(B.Sandstone, 2), k3 - 1, 8, 0);
            s.Set( S(B.Sandstone, 2), k3, 8, 0);
            s.Set( S(B.Sandstone, 2), k3 + 1, 8, 0);
        }
         s.Fill( 8, 4, 0, 12, 6, 0, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Set( S(B.Air), 8, 6, 0);
        s.Set( S(B.Air), 12, 6, 0);
        s.Set( S(B.StainedClay, i1), 9, 5, 0);
        s.Set( S(B.Sandstone, 1), 10, 5, 0);
        s.Set( S(B.StainedClay, i1), 11, 5, 0);
        s.Fill( 8, -14, 8, 12, -11, 12, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 8, -10, 8, 12, -10, 12, S(B.Sandstone, 1), S(B.Sandstone, 1), false);
        s.Fill( 8, -9, 8, 12, -9, 12, S(B.Sandstone, 2), S(B.Sandstone, 2), false);
        s.Fill( 8, -8, 8, 12, -1, 12, S(B.Sandstone), S(B.Sandstone), false);
        s.Fill( 9, -11, 9, 11, -1, 11, S(B.Air), S(B.Air), false);
        s.Set( S(B.StonePressurePlate), 10, -11, 10);
        s.Fill( 9, -13, 9, 11, -13, 11, S(B.Tnt), S(B.Air), false);
        s.Set( S(B.Air), 8, -11, 10);
        s.Set( S(B.Air), 8, -10, 10);
        s.Set( S(B.Sandstone, 1), 7, -10, 10);
        s.Set( S(B.Sandstone, 2), 7, -11, 10);
        s.Set( S(B.Air), 12, -11, 10);
        s.Set( S(B.Air), 12, -10, 10);
        s.Set( S(B.Sandstone, 1), 13, -10, 10);
        s.Set( S(B.Sandstone, 2), 13, -11, 10);
        s.Set( S(B.Air), 10, -11, 8);
        s.Set( S(B.Air), 10, -10, 8);
        s.Set( S(B.Sandstone, 1), 10, -10, 7);
        s.Set( S(B.Sandstone, 2), 10, -11, 7);
        s.Set( S(B.Air), 10, -11, 12);
        s.Set( S(B.Air), 10, -10, 12);
        s.Set( S(B.Sandstone, 1), 10, -10, 13);
        s.Set( S(B.Sandstone, 2), 10, -11, 13);

        for (int d = 0; d < 4; d++)
        s.Chest(10 + (d == 1 ? 2 : d == 3 ? -2 : 0), -11, 10 + (d == 0 ? -2 : d == 2 ? 2 : 0), OwLoot.DesertTemple, 2 + r.NextInt(5));
    }
    public static void Jungle(OwStructureWriter s, JRandom r)
    {
            int i = s.Meta(B.CobbleStairs, 3);
            int j = s.Meta(B.CobbleStairs, 2);
            int k = s.Meta(B.CobbleStairs, 0);
            int l = s.Meta(B.CobbleStairs, 1);
            s.RandomFill( 0, -4, 0, 12 - 1, 0, 15 - 1);
            s.RandomFill( 2, 1, 2, 9, 2, 2);
            s.RandomFill( 2, 1, 12, 9, 2, 12);
            s.RandomFill( 2, 1, 3, 2, 2, 11);
            s.RandomFill( 9, 1, 3, 9, 2, 11);
            s.RandomFill( 1, 3, 1, 10, 6, 1);
            s.RandomFill( 1, 3, 13, 10, 6, 13);
            s.RandomFill( 1, 3, 2, 1, 6, 12);
            s.RandomFill( 10, 3, 2, 10, 6, 12);
            s.RandomFill( 2, 3, 2, 9, 3, 12);
            s.RandomFill( 2, 6, 2, 9, 6, 12);
            s.RandomFill( 3, 7, 3, 8, 7, 11);
            s.RandomFill( 4, 8, 4, 7, 8, 10);
            s.Air( 3, 1, 3, 8, 2, 11);
            s.Air( 4, 3, 6, 7, 3, 9);
            s.Air( 2, 4, 2, 9, 5, 12);
            s.Air( 4, 6, 5, 7, 6, 9);
            s.Air( 5, 7, 6, 6, 7, 8);
            s.Air( 5, 1, 2, 6, 2, 2);
            s.Air( 5, 2, 12, 6, 2, 12);
            s.Air( 5, 5, 1, 6, 5, 1);
            s.Air( 5, 5, 13, 6, 5, 13);
            s.Set( S(B.Air), 1, 5, 5);
            s.Set( S(B.Air), 10, 5, 5);
            s.Set( S(B.Air), 1, 5, 9);
            s.Set( S(B.Air), 10, 5, 9);
             for (int i1 = 0; i1 <= 14; i1 += 14)
            {
                s.RandomFill( 2, 4, i1, 2, 5, i1);
                s.RandomFill( 4, 4, i1, 4, 5, i1);
                s.RandomFill( 7, 4, i1, 7, 5, i1);
                s.RandomFill( 9, 4, i1, 9, 5, i1);
            }
             s.RandomFill( 5, 6, 0, 6, 6, 0);
             for (int k1 = 0; k1 <= 11; k1 += 11)
            {
                for (int j1 = 2; j1 <= 12; j1 += 2)
                {
                    s.RandomFill( k1, 4, j1, k1, 5, j1);
                }
                 s.RandomFill( k1, 6, 5, k1, 6, 5);
                s.RandomFill( k1, 6, 9, k1, 6, 9);
            }
             s.RandomFill( 2, 7, 2, 2, 9, 2);
            s.RandomFill( 9, 7, 2, 9, 9, 2);
            s.RandomFill( 2, 7, 12, 2, 9, 12);
            s.RandomFill( 9, 7, 12, 9, 9, 12);
            s.RandomFill( 4, 9, 4, 4, 9, 4);
            s.RandomFill( 7, 9, 4, 7, 9, 4);
            s.RandomFill( 4, 9, 10, 4, 9, 10);
            s.RandomFill( 7, 9, 10, 7, 9, 10);
            s.RandomFill( 5, 9, 7, 6, 9, 7);
            s.Set( S(B.CobbleStairs, i), 5, 9, 6);
            s.Set( S(B.CobbleStairs, i), 6, 9, 6);
            s.Set( S(B.CobbleStairs, j), 5, 9, 8);
            s.Set( S(B.CobbleStairs, j), 6, 9, 8);
            s.Set( S(B.CobbleStairs, i), 4, 0, 0);
            s.Set( S(B.CobbleStairs, i), 5, 0, 0);
            s.Set( S(B.CobbleStairs, i), 6, 0, 0);
            s.Set( S(B.CobbleStairs, i), 7, 0, 0);
            s.Set( S(B.CobbleStairs, i), 4, 1, 8);
            s.Set( S(B.CobbleStairs, i), 4, 2, 9);
            s.Set( S(B.CobbleStairs, i), 4, 3, 10);
            s.Set( S(B.CobbleStairs, i), 7, 1, 8);
            s.Set( S(B.CobbleStairs, i), 7, 2, 9);
            s.Set( S(B.CobbleStairs, i), 7, 3, 10);
            s.RandomFill( 4, 1, 9, 4, 1, 9);
            s.RandomFill( 7, 1, 9, 7, 1, 9);
            s.RandomFill( 4, 1, 10, 7, 2, 10);
            s.RandomFill( 5, 4, 5, 6, 4, 5);
            s.Set( S(B.CobbleStairs, k), 4, 4, 5);
            s.Set( S(B.CobbleStairs, l), 7, 4, 5);
             for (int l1 = 0; l1 < 4; ++l1)
            {
                s.Set( S(B.CobbleStairs, j), 5, 0 - l1, 6 + l1);
                s.Set( S(B.CobbleStairs, j), 6, 0 - l1, 6 + l1);
                s.Air( 5, 0 - l1, 7 + l1, 6, 0 - l1, 9 + l1);
            }
             s.Air( 1, -3, 12, 10, -1, 13);
            s.Air( 1, -3, 1, 3, -1, 13);
            s.Air( 1, -3, 1, 9, -1, 5);
             for (int i2 = 1; i2 <= 13; i2 += 2)
            {
                s.RandomFill( 1, -3, i2, 1, -2, i2);
            }
             for (int j2 = 2; j2 <= 12; j2 += 2)
            {
                s.RandomFill( 1, -1, j2, 3, -1, j2);
            }
             s.RandomFill( 2, -2, 1, 5, -2, 1);
            s.RandomFill( 7, -2, 1, 9, -2, 1);
            s.RandomFill( 6, -3, 1, 6, -3, 1);
            s.RandomFill( 6, -1, 1, 6, -1, 1);
            s.Set( S(B.TripwireHook, s.Meta(B.TripwireHook, 3) | 4), 1, -3, 8);
            s.Set( S(B.TripwireHook, s.Meta(B.TripwireHook, 1) | 4), 4, -3, 8);
            s.Set( S(B.Tripwire, 4), 2, -3, 8);
            s.Set( S(B.Tripwire, 4), 3, -3, 8);
            s.Set( S(B.RedstoneWire), 5, -3, 7);
            s.Set( S(B.RedstoneWire), 5, -3, 6);
            s.Set( S(B.RedstoneWire), 5, -3, 5);
            s.Set( S(B.RedstoneWire), 5, -3, 4);
            s.Set( S(B.RedstoneWire), 5, -3, 3);
            s.Set( S(B.RedstoneWire), 5, -3, 2);
            s.Set( S(B.RedstoneWire), 5, -3, 1);
            s.Set( S(B.RedstoneWire), 4, -3, 1);
            s.Set( S(B.MossyCobblestone), 3, -3, 1);
             if (true)
            {
                s.Dispenser(3, -2, 1, 2);
            }
             s.Set( S(B.Vine, 15), 3, -2, 2);
            s.Set( S(B.TripwireHook, s.Meta(B.TripwireHook, 2) | 4), 7, -3, 1);
            s.Set( S(B.TripwireHook, s.Meta(B.TripwireHook, 0) | 4), 7, -3, 5);
            s.Set( S(B.Tripwire, 4), 7, -3, 2);
            s.Set( S(B.Tripwire, 4), 7, -3, 3);
            s.Set( S(B.Tripwire, 4), 7, -3, 4);
            s.Set( S(B.RedstoneWire), 8, -3, 6);
            s.Set( S(B.RedstoneWire), 9, -3, 6);
            s.Set( S(B.RedstoneWire), 9, -3, 5);
            s.Set( S(B.MossyCobblestone), 9, -3, 4);
            s.Set( S(B.RedstoneWire), 9, -2, 4);
             if (true)
            {
                s.Dispenser(9, -2, 3, 4);
            }
             s.Set( S(B.Vine, 15), 8, -1, 3);
            s.Set( S(B.Vine, 15), 8, -2, 3);
             if (true)
            {
                s.Chest(8, -3, 3, OwLoot.JungleTemple, 2 + r.NextInt(5));
            }
             s.Set( S(B.MossyCobblestone), 9, -3, 2);
            s.Set( S(B.MossyCobblestone), 8, -3, 1);
            s.Set( S(B.MossyCobblestone), 4, -3, 5);
            s.Set( S(B.MossyCobblestone), 5, -2, 5);
            s.Set( S(B.MossyCobblestone), 5, -1, 5);
            s.Set( S(B.MossyCobblestone), 6, -3, 5);
            s.Set( S(B.MossyCobblestone), 7, -2, 5);
            s.Set( S(B.MossyCobblestone), 7, -1, 5);
            s.Set( S(B.MossyCobblestone), 8, -3, 5);
            s.RandomFill( 9, -1, 1, 9, -1, 5);
            s.Air( 8, -3, 8, 10, -1, 10);
            s.Set( S(B.StoneBricks, 3), 8, -2, 11);
            s.Set( S(B.StoneBricks, 3), 9, -2, 11);
            s.Set( S(B.StoneBricks, 3), 10, -2, 11);
            s.Set( S(B.Lever, s.LeverMeta(2)), 8, -2, 12);
            s.Set( S(B.Lever, s.LeverMeta(2)), 9, -2, 12);
            s.Set( S(B.Lever, s.LeverMeta(2)), 10, -2, 12);
            s.RandomFill( 8, -3, 8, 8, -3, 10);
            s.RandomFill( 10, -3, 8, 10, -3, 10);
            s.Set( S(B.MossyCobblestone), 10, -2, 9);
            s.Set( S(B.RedstoneWire), 8, -2, 9);
            s.Set( S(B.RedstoneWire), 8, -2, 10);
            s.Set( S(B.RedstoneWire), 10, -1, 9);
            s.Set( S(B.StickyPiston, 1), 9, -2, 8);
            s.Set( S(B.StickyPiston, s.Meta(B.StickyPiston, 4)), 10, -2, 8);
            s.Set( S(B.StickyPiston, s.Meta(B.StickyPiston, 4)), 10, -1, 8);
            s.Set( S(B.Repeater, s.Meta(B.Repeater, 2)), 10, -2, 10);
             if (true)
            {
                s.Chest(9, -3, 10, OwLoot.JungleTemple, 2 + r.NextInt(5));
            }


    }
    public static void Hut(OwStructureWriter s, JRandom r)
    {
            s.Fill( 1, 1, 1, 5, 1, 7, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 1, 4, 2, 5, 4, 7, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 2, 1, 0, 4, 1, 0, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 2, 2, 2, 3, 3, 2, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 1, 2, 3, 1, 3, 6, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 5, 2, 3, 5, 3, 6, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 2, 2, 7, 4, 3, 7, S(B.SprucePlanks, 0), S(B.SprucePlanks, 0), false);
            s.Fill( 1, 0, 2, 1, 3, 2, S(B.OakLog), S(B.OakLog), false);
            s.Fill( 5, 0, 2, 5, 3, 2, S(B.OakLog), S(B.OakLog), false);
            s.Fill( 1, 0, 7, 1, 3, 7, S(B.OakLog), S(B.OakLog), false);
            s.Fill( 5, 0, 7, 5, 3, 7, S(B.OakLog), S(B.OakLog), false);
            s.Set( S(B.Fence), 2, 3, 2);
            s.Set( S(B.Fence), 3, 3, 7);
            s.Set( S(B.Air), 1, 3, 4);
            s.Set( S(B.Air), 5, 3, 4);
            s.Set( S(B.Air), 5, 3, 5);
            s.Set( S(B.FlowerPot, 7), 1, 3, 5);
            s.Set( S(B.CraftingTable), 3, 2, 6);
            s.Set( S(B.Cauldron), 4, 2, 6);
            s.Set( S(B.Fence), 1, 2, 1);
            s.Set( S(B.Fence), 5, 2, 1);
            int i = s.Meta(B.OakStairs, 3);
            int j = s.Meta(B.OakStairs, 1);
            int k = s.Meta(B.OakStairs, 0);
            int l = s.Meta(B.OakStairs, 2);
            s.Fill( 0, 4, 1, 6, 4, 1, S(B.SpruceStairs, i), S(B.SpruceStairs, i), false);
            s.Fill( 0, 4, 2, 0, 4, 7, S(B.SpruceStairs, k), S(B.SpruceStairs, k), false);
            s.Fill( 6, 4, 2, 6, 4, 7, S(B.SpruceStairs, j), S(B.SpruceStairs, j), false);
            s.Fill( 0, 4, 8, 6, 4, 8, S(B.SpruceStairs, l), S(B.SpruceStairs, l), false);
             for (int i1 = 2; i1 <= 7; i1 += 5)
            {
                for (int j1 = 1; j1 <= 5; j1 += 4)
                {
                    s.Down( S(B.OakLog), j1, -1, i1);
                }
            }


    }
}