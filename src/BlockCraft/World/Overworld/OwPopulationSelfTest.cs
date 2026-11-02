using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestOwCavesAndPopulation()
    {
        // Wzorzec z TerrainOracle.java uruchomionego z dodatkowym argumentem caves.
        // MC rozróżnia płynącą i nieruchomą lawę; obie mają w BlockCraft ID B.Lava.
        (long seed, WorldType type, int x, int z, ulong hash)[] golden =
        {
            (0L, WorldType.Default, 0, 0, 0xea3dbd7fb39cc7e8UL),
            (0L, WorldType.Default, -3, 7, 0xce46e0f515444df1UL),
            (0L, WorldType.Default, 137, -231, 0xa898aef5b6b49982UL),
            (0L, WorldType.Default, -625, 146, 0xf8010fc084cba27dUL),
            (0L, WorldType.LargeBiomes, 0, 0, 0xea3dbd7fb39cc7e8UL),
            (0L, WorldType.LargeBiomes, -3, 7, 0xa3aee0fba799909bUL),
            (0L, WorldType.LargeBiomes, 137, -231, 0xfff813a4c02a0ff4UL),
            (0L, WorldType.LargeBiomes, -625, 146, 0xdce5f73a7ac718c4UL),
            (0L, WorldType.Amplified, 0, 0, 0x01073ae9f7f4595dUL),
            (0L, WorldType.Amplified, -3, 7, 0x318b6dbb885be8b0UL),
            (0L, WorldType.Amplified, 137, -231, 0xeac744776216de38UL),
            (0L, WorldType.Amplified, -625, 146, 0xf8010fc084cba27dUL),
            (1L, WorldType.Default, 0, 0, 0x1e37c3834d1097b9UL),
            (1L, WorldType.Default, -3, 7, 0xe9d79897c7c2386aUL),
            (1L, WorldType.Default, 137, -231, 0xd473401a06fbbe12UL),
            (1L, WorldType.Default, -625, 146, 0xfa3d64f3ab407069UL),
            (1L, WorldType.LargeBiomes, 0, 0, 0x95cd0a84a64756e9UL),
            (1L, WorldType.LargeBiomes, -3, 7, 0xa61f0b81a4b14effUL),
            (1L, WorldType.LargeBiomes, 137, -231, 0xb48c7d39dda61001UL),
            (1L, WorldType.LargeBiomes, -625, 146, 0x87e50ef47a012107UL),
            (1L, WorldType.Amplified, 0, 0, 0x1e37c3834d1097b9UL),
            (1L, WorldType.Amplified, -3, 7, 0xb0ddf0483ee48df6UL),
            (1L, WorldType.Amplified, 137, -231, 0x3be520aac2aef3a0UL),
            (1L, WorldType.Amplified, -625, 146, 0x53bed694606e6ddaUL),
            (12345L, WorldType.Default, 0, 0, 0x3a73c777a005dd06UL),
            (12345L, WorldType.Default, -3, 7, 0xd39c162e53421db0UL),
            (12345L, WorldType.Default, 137, -231, 0x6ecc71050fa13dfaUL),
            (12345L, WorldType.Default, -625, 146, 0x68e11903f7deb81aUL),
            (12345L, WorldType.LargeBiomes, 0, 0, 0xee00b761f7bb37e0UL),
            (12345L, WorldType.LargeBiomes, -3, 7, 0xd39c162e53421db0UL),
            (12345L, WorldType.LargeBiomes, 137, -231, 0xfe99bc02b421babaUL),
            (12345L, WorldType.LargeBiomes, -625, 146, 0x04ee05681cd73da3UL),
            (12345L, WorldType.Amplified, 0, 0, 0x866ddec3b2ed83bbUL),
            (12345L, WorldType.Amplified, -3, 7, 0xa8095fbee31e69faUL),
            (12345L, WorldType.Amplified, 137, -231, 0x6ecc71050fa13dfaUL),
            (12345L, WorldType.Amplified, -625, 146, 0x6ed830beeaa86728UL),
            (-987654321012345L, WorldType.Default, 0, 0, 0x50d7973be9bea210UL),
            (-987654321012345L, WorldType.Default, -3, 7, 0x4081bdaed3f3c3ccUL),
            (-987654321012345L, WorldType.Default, 137, -231, 0x4bbb80e1679902a1UL),
            (-987654321012345L, WorldType.Default, -625, 146, 0x77e151437bb85d4aUL),
            (-987654321012345L, WorldType.LargeBiomes, 0, 0, 0x50d7973be9bea210UL),
            (-987654321012345L, WorldType.LargeBiomes, -3, 7, 0x4081bdaed3f3c3ccUL),
            (-987654321012345L, WorldType.LargeBiomes, 137, -231, 0x4bbb80e1679902a1UL),
            (-987654321012345L, WorldType.LargeBiomes, -625, 146, 0xe9f295efd41f892aUL),
            (-987654321012345L, WorldType.Amplified, 0, 0, 0x745db2c4e094e2eaUL),
            (-987654321012345L, WorldType.Amplified, -3, 7, 0xb14553f48706fcf9UL),
            (-987654321012345L, WorldType.Amplified, 137, -231, 0x4bbb80e1679902a1UL),
            (-987654321012345L, WorldType.Amplified, -625, 146, 0x77e151437bb85d4aUL),
        };
        int failures = 0;
        foreach (var t in golden)
        {
            var gen = new OwTerrain(t.seed, t.type == WorldType.Amplified, t.type == WorldType.LargeBiomes);
            var p = gen.Generate(t.x, t.z);
            OwCaves.Generate(t.seed, t.x, t.z, p);
            if (TerrainHash(p) != t.hash) failures++;
        }
        Check(failures == 0, "Jaskinie i wąwozy: 48 chunków zgodnych z oryginalnym serwerem", failures.ToString());
        var first = new WorldGenerator(777, WorldType.Default);
        var positions = new[] { (-1, -1), (0, -1), (-1, 0), (0, 0) };
        var snapshots = positions.Select(p => first.Generate(p.Item1, p.Item2)).ToArray();
        var second = new WorldGenerator(777, WorldType.Default);
        var reverse = positions.Reverse().Select(p => second.Generate(p.Item1, p.Item2)).Reverse().ToArray();
        bool same = snapshots.Select((c, i) => c.Blocks.SequenceEqual(reverse[i].Blocks) && c.Meta.SequenceEqual(reverse[i].Meta)).All(b => b);
        Check(same, "Populacja: kolejność ładowania sąsiednich chunków nie zmienia świata");
        var third = new WorldGenerator(777, WorldType.Default);
        var parallel = new Chunk[positions.Length];
        Parallel.For(0, positions.Length, i => parallel[i] = third.Generate(positions[i].Item1, positions[i].Item2));
        Check(parallel.Select((c, i) => c.Blocks.SequenceEqual(snapshots[i].Blocks) && c.Meta.SequenceEqual(snapshots[i].Meta)).All(b => b),
            "Populacja: generowanie sąsiadów na wielu wątkach jest powtarzalne");
        var baseBefore = first.Overworld.Base(0, 0).Blocks.ToArray();
        first.Generate(0, 0);
        Check(baseBefore.SequenceEqual(first.Overworld.Base(0, 0).Blocks), "Populacja nie modyfikuje bazowego cache terenu");
        Check(snapshots.Any(c => c.Meta.Where((m, i) => c.Blocks[i] == B.Stone).Any(m => m is 1 or 3 or 5)), "Żyły granitu, diorytu i andezytu");
    }
}