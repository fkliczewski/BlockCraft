using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    // Numery stanów MC służą wyłącznie do niezależnego porównania z oficjalnym serwerem, nie do zapisu gry.
    private static int McTerrainState(byte id, int meta) => (id switch
    {
        B.Air => 0, B.Stone => 1, B.Grass => 2, B.Dirt => 3, B.Bedrock => 7, B.Water => 9, B.Lava => 11,
        B.Sand => 12, B.Gravel => 13, B.Sandstone => 24, B.SnowLayer => 78, B.Ice => 79, B.Snow => 80,
        B.Mycelium => 110, B.LilyPad => 111, B.StainedClay => 159, B.HardenedClay => 172, B.RedSandstone => 179,
        _ => throw new InvalidOperationException("Nieznany stan surowego terenu: " + id)
    }) | (meta << 12);

    private static ulong TerrainHash(ProtoChunk p)
    {
        ulong hash = 0xcbf29ce484222325;
        for (int i = 0; i < Chunk.Volume; i++) hash = unchecked((hash ^ (ulong)McTerrainState(p.Blocks[i], p.Meta[i])) * 0x100000001b3);
        return hash;
    }

    private static void TestOwTerrain()
    {
        // Oficjalny server.jar 1.8.9, jak w TestOwGenLayers. Bez jaskiń i populacji.
        (long seed, WorldType type, int x, int z, ulong hash)[] golden =
        {
            (0L, WorldType.Default, 0, 0, 0x6f14d2991e50e798UL),
            (0L, WorldType.Default, -3, 7, 0xce46e0f515444df1UL),
            (0L, WorldType.Default, 137, -231, 0xa26a10898006a6c4UL),
            (0L, WorldType.Default, -625, 146, 0xf8010fc084cba27dUL),
            (0L, WorldType.LargeBiomes, 0, 0, 0x6f14d2991e50e798UL),
            (0L, WorldType.LargeBiomes, -3, 7, 0xa3aee0fba799909bUL),
            (0L, WorldType.LargeBiomes, 137, -231, 0x9b0859936ba4b6b2UL),
            (0L, WorldType.LargeBiomes, -625, 146, 0xdce5f73a7ac718c4UL),
            (0L, WorldType.Amplified, 0, 0, 0x85de500362a8790dUL),
            (0L, WorldType.Amplified, -3, 7, 0x318b6dbb885be8b0UL),
            (0L, WorldType.Amplified, 137, -231, 0x4ce9171ffe7ae8e2UL),
            (0L, WorldType.Amplified, -625, 146, 0xf8010fc084cba27dUL),
            (1L, WorldType.Default, 0, 0, 0x5029eb6d270de175UL),
            (1L, WorldType.Default, -3, 7, 0xe9d79897c7c2386aUL),
            (1L, WorldType.Default, 137, -231, 0x04a80a8c4203e7ddUL),
            (1L, WorldType.Default, -625, 146, 0x9caa90c0282cabe8UL),
            (1L, WorldType.LargeBiomes, 0, 0, 0xd26abbdeb4165d35UL),
            (1L, WorldType.LargeBiomes, -3, 7, 0xa61f0b81a4b14effUL),
            (1L, WorldType.LargeBiomes, 137, -231, 0xe89e0be49be505c7UL),
            (1L, WorldType.LargeBiomes, -625, 146, 0xe26206c6f183ed9fUL),
            (1L, WorldType.Amplified, 0, 0, 0x5029eb6d270de175UL),
            (1L, WorldType.Amplified, -3, 7, 0xb0ddf0483ee48df6UL),
            (1L, WorldType.Amplified, 137, -231, 0xcd693c04502796fcUL),
            (1L, WorldType.Amplified, -625, 146, 0xf130719cea256bc8UL),
            (12345L, WorldType.Default, 0, 0, 0xf1572203a1e18556UL),
            (12345L, WorldType.Default, -3, 7, 0xc730ea8545700ae9UL),
            (12345L, WorldType.Default, 137, -231, 0x33989ea0ddf2be76UL),
            (12345L, WorldType.Default, -625, 146, 0x68e11903f7deb81aUL),
            (12345L, WorldType.LargeBiomes, 0, 0, 0xa4e411edf996e030UL),
            (12345L, WorldType.LargeBiomes, -3, 7, 0xc730ea8545700ae9UL),
            (12345L, WorldType.LargeBiomes, 137, -231, 0xe2ae18f9fe3031e9UL),
            (12345L, WorldType.LargeBiomes, -625, 146, 0x04ee05681cd73da3UL),
            (12345L, WorldType.Amplified, 0, 0, 0x3d51394fb4c92c0bUL),
            (12345L, WorldType.Amplified, -3, 7, 0x0733914f964a4f41UL),
            (12345L, WorldType.Amplified, 137, -231, 0x33989ea0ddf2be76UL),
            (12345L, WorldType.Amplified, -625, 146, 0x6ed830beeaa86728UL),
            (-987654321012345L, WorldType.Default, 0, 0, 0x318e455a1a7f6aa6UL),
            (-987654321012345L, WorldType.Default, -3, 7, 0x95bbefad879d408dUL),
            (-987654321012345L, WorldType.Default, 137, -231, 0x4bbb80e1679902a1UL),
            (-987654321012345L, WorldType.Default, -625, 146, 0x5bfdfe999218e535UL),
            (-987654321012345L, WorldType.LargeBiomes, 0, 0, 0x318e455a1a7f6aa6UL),
            (-987654321012345L, WorldType.LargeBiomes, -3, 7, 0x95bbefad879d408dUL),
            (-987654321012345L, WorldType.LargeBiomes, 137, -231, 0x4bbb80e1679902a1UL),
            (-987654321012345L, WorldType.LargeBiomes, -625, 146, 0x409875639ac50ca1UL),
            (-987654321012345L, WorldType.Amplified, 0, 0, 0x8416d351113ea0a8UL),
            (-987654321012345L, WorldType.Amplified, -3, 7, 0x1dfb2fb6774c14b8UL),
            (-987654321012345L, WorldType.Amplified, 137, -231, 0x4bbb80e1679902a1UL),
            (-987654321012345L, WorldType.Amplified, -625, 146, 0x5bfdfe999218e535UL),
        };
        int failures = 0;
        foreach (var t in golden)
        {
            var gen = new OwTerrain(t.seed, t.type == WorldType.Amplified, t.type == WorldType.LargeBiomes);
            if (TerrainHash(gen.Generate(t.x, t.z)) != t.hash) failures++;
        }
        Check(failures == 0, "Teren: 48 chunków zgodnych blok po bloku z MC 1.8.9", failures.ToString());
        var terrain = new OwTerrain(12345, false, false);
        var expected = terrain.Generate(-3, 7);
        bool parallel = true;
        Parallel.For(0, 8, i =>
        {
            var p = terrain.Generate(-3, 7);
            if (!p.Blocks.SequenceEqual(expected.Blocks) || !p.Meta.SequenceEqual(expected.Meta)) parallel = false;
        });
        Check(parallel, "Teren: współbieżność nie zmienia bloków ani meta");
        Check(expected.Biomes.SequenceEqual(terrain.Biomes.ChunkBiomes(-3, 7)), "Teren zapisuje ID biomów z GenLayer w Chunk.Biomes");
        foreach (WorldType type in new[] { WorldType.Default, WorldType.LargeBiomes, WorldType.Amplified })
        {
            var gen = new WorldGenerator(12345, type);
            var c = gen.Generate(-3, 7);
            Check(c.Biomes.Select((b, i) => b == gen.BiomeAt(-48 + (i & 15), 112 + (i >> 4))).All(b => b), "Generator podłączony do biomów: " + type);
        }
    }
}
