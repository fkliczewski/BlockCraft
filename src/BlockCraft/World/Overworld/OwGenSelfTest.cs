using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static ulong BiomeHash(int[] values)
    {
        ulong hash = 0xcbf29ce484222325;
        foreach (int value in values) hash = unchecked((hash ^ (ulong)value) * 0x100000001b3);
        return hash;
    }

    private static void TestOwGenLayers()
    {
        // Wzorzec: oficjalny server.jar 1.8.9 (SHA1 b58b2ceb36e01bcd8dbf49c8fb66c55a9f0676cd).
        // Odtworzenie: tools/overworld/GenLayerOracle.java. Hash FNV-1a po ID biomów (bez nieużytego końca IntCache).
        (long seed, bool large, int layer, int x, int z, int w, int h, ulong hash)[] golden =
        {
            (0L, false, 0, -32, -32, 64, 64, 0xf23907fe18a95f56UL),
            (0L, false, 0, 137, -231, 32, 24, 0x90147f03ce416bdeUL),
            (0L, false, 0, -10000, 2345, 24, 32, 0xc26555df3a8f32fdUL),
            (0L, false, 1, -32, -32, 64, 64, 0x177c54a67330d009UL),
            (0L, false, 1, 137, -231, 32, 24, 0x3df2951d2ff41f25UL),
            (0L, false, 1, -10000, 2345, 24, 32, 0x9fa9e040e0eedf25UL),
            (0L, true, 0, -32, -32, 64, 64, 0x594c07016452292dUL),
            (0L, true, 0, 137, -231, 32, 24, 0xb9673e609fee205dUL),
            (0L, true, 0, -10000, 2345, 24, 32, 0x9fa9e040e0eedf25UL),
            (0L, true, 1, -32, -32, 64, 64, 0x215646dc29a3a325UL),
            (0L, true, 1, 137, -231, 32, 24, 0xaddb8e28faa9c225UL),
            (0L, true, 1, -10000, 2345, 24, 32, 0x89528ec950e0af25UL),
            (1L, false, 0, -32, -32, 64, 64, 0x0cef548d64b56164UL),
            (1L, false, 0, 137, -231, 32, 24, 0xafd576080f30148cUL),
            (1L, false, 0, -10000, 2345, 24, 32, 0xd7441517c404a380UL),
            (1L, false, 1, -32, -32, 64, 64, 0x2318312b9ee5448dUL),
            (1L, false, 1, 137, -231, 32, 24, 0x9b152f7ad78988e1UL),
            (1L, false, 1, -10000, 2345, 24, 32, 0xa0f97854fe334cf5UL),
            (1L, true, 0, -32, -32, 64, 64, 0x76d7035076881b0dUL),
            (1L, true, 0, 137, -231, 32, 24, 0xe2ef6abbec524babUL),
            (1L, true, 0, -10000, 2345, 24, 32, 0xd69eed795142b325UL),
            (1L, true, 1, -32, -32, 64, 64, 0xb93a0c83ce3b6325UL),
            (1L, true, 1, 137, -231, 32, 24, 0x3df2951d2ff41f25UL),
            (1L, true, 1, -10000, 2345, 24, 32, 0x66e328a07a035325UL),
            (12345L, false, 0, -32, -32, 64, 64, 0xce815dc195a0ae21UL),
            (12345L, false, 0, 137, -231, 32, 24, 0x6e7c1db1c00e1ecbUL),
            (12345L, false, 0, -10000, 2345, 24, 32, 0xc5cadbe2a65a1143UL),
            (12345L, false, 1, -32, -32, 64, 64, 0x89dd58c4417ee275UL),
            (12345L, false, 1, 137, -231, 32, 24, 0x966c5bf0fd2d1225UL),
            (12345L, false, 1, -10000, 2345, 24, 32, 0xb9b9cbfaecf0e325UL),
            (12345L, true, 0, -32, -32, 64, 64, 0xbabb9711f9ec8bc3UL),
            (12345L, true, 0, 137, -231, 32, 24, 0x8b90220aa2934d8fUL),
            (12345L, true, 0, -10000, 2345, 24, 32, 0x86eccb7d4ec206f9UL),
            (12345L, true, 1, -32, -32, 64, 64, 0xe6bc86ca4f1c7325UL),
            (12345L, true, 1, 137, -231, 32, 24, 0x85a3239aa46b1225UL),
            (12345L, true, 1, -10000, 2345, 24, 32, 0xd87097e147da6b25UL),
            (-987654321012345L, false, 0, -32, -32, 64, 64, 0x13b4a4461c813218UL),
            (-987654321012345L, false, 0, 137, -231, 32, 24, 0x3df2951d2ff41f25UL),
            (-987654321012345L, false, 0, -10000, 2345, 24, 32, 0x384db43851803cfbUL),
            (-987654321012345L, false, 1, -32, -32, 64, 64, 0x5adac181d3f74d69UL),
            (-987654321012345L, false, 1, 137, -231, 32, 24, 0xb88a526fef5bd234UL),
            (-987654321012345L, false, 1, -10000, 2345, 24, 32, 0xad829fb95320ad55UL),
            (-987654321012345L, true, 0, -32, -32, 64, 64, 0xeda4f10eabc00c85UL),
            (-987654321012345L, true, 0, 137, -231, 32, 24, 0xd3eab3775125d2b9UL),
            (-987654321012345L, true, 0, -10000, 2345, 24, 32, 0x5505154253992925UL),
            (-987654321012345L, true, 1, -32, -32, 64, 64, 0x5adac181d3f74d69UL),
            (-987654321012345L, true, 1, 137, -231, 32, 24, 0x1c0b9148a5774031UL),
            (-987654321012345L, true, 1, -10000, 2345, 24, 32, 0xf280839b53dc6f25UL),
        };
        int failures = 0;
        foreach (var t in golden)
        {
            var layers = GenLayer.Build(t.seed, t.large);
            if (BiomeHash(layers[t.layer].GetInts(t.x, t.z, t.w, t.h)) != t.hash) failures++;
        }
        Check(failures == 0, "GenLayer: 48 map zgodnych z MC 1.8.9", failures.ToString());
        var provider = new OwBiomeProvider(12345, false);
        var whole = provider.BlockBiomes(-37, -29, 73, 61);
        bool cropped = true;
        for (int z = 0; z < 61; z += 7)
            for (int x = 0; x < 73; x += 9)
            {
                int w = Math.Min(9, 73 - x), h = Math.Min(7, 61 - z);
                var part = provider.BlockBiomes(-37 + x, -29 + z, w, h);
                for (int dz = 0; dz < h; dz++)
                    for (int dx = 0; dx < w; dx++)
                        cropped &= part[dx + dz * w] == whole[x + dx + (z + dz) * 73];
            }
        Check(cropped, "Voronoi: dowolne prostokąty zgodne na granicach, także ujemnych");
        bool parallel = true;
        Parallel.For(0, 16, i =>
        {
            var a = provider.BlockBiomes(-37, -29, 73, 61);
            if (!a.SequenceEqual(whole)) parallel = false;
        });
        Check(parallel, "Biomy: generowanie współbieżne daje ten sam wynik");
        Check(provider.ChunkBiomes(-1, -1).Select((b, i) => b == provider.BiomeAt(-16 + (i & 15), -16 + (i >> 4))).All(b => b),
            "Cache biomów zgodny z odczytem blokowym");
        Check(Biome.ById.Count(b => b != null && b.Kind != BiomeKind.Hell && b.Kind != BiomeKind.Sky) == 59,
            "59 biomów Overworldu 1.8 (38 bazowych i 21 mutacji)");
    }
}
