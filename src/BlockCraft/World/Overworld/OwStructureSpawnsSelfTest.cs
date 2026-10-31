using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestOwStructureSpawns()
    {
        var w = new World.World(12345, WorldType.Default, false);
        var gen = w.Generator.Overworld;
        var monument = gen.Monument(45, -185); var box = monument.MonsterSpawnBox;
        Check(box is { X0: 699, X1: 756, Y0: 39, Y1: 61, Z0: -2981, Z1: -2924 },
            "Spawn monumentu: oryginalny obszar 58×23×58 bez rozszerzonych fundamentów");
        int x = (box.X0 + box.X1) / 2, z = (box.Z0 + box.Z1) / 2;
        Check(gen.StructureMonsterAt(x, 50, z) == "guardian" && gen.StructureMonsterAt(box.X0, box.Y0, box.Z0) == "guardian"
            && gen.StructureMonsterAt(box.X1, box.Y1, box.Z1) == "guardian", "Spawn guardianów obejmuje wnętrze i granice monumentu");
        Check(gen.StructureMonsterAt(x, 38, z) == null && gen.StructureMonsterAt(x, 62, z) == null
            && gen.StructureMonsterAt(box.X0 - 1, 50, z) == null && gen.StructureMonsterAt(box.X1 + 1, 50, z) == null,
            "Guardian nie ma specjalnego spawnu pod, nad i obok monumentu");
        var entries = w.Spawner.EntriesAt(SpawnKind.Monster, x, 50, z);
        Check(entries.Length == 1 && entries[0] is { Key: "guardian", Weight: 1, Min: 2, Max: 4 }, "Monument zastępuje listę potworów strażnikami 1/2–4");
        Check(!w.Spawner.EntriesAt(SpawnKind.Water, x, 50, z).Any(e => e.Key == "guardian")
            && BiomeSpawns.CountsAs(new Guardian(w), SpawnKind.Monster) && !BiomeSpawns.CountsAs(new Guardian(w), SpawnKind.Water),
            "Strażnik korzysta z limitu potworów i nie zastępuje wodnej listy kałamarnic");
        var hut = gen.Temple(49, -18); var h = hut.MonsterSpawnBox;
        Check(h is { X0: 784, Y0: 64, Z0: -288, X1: 792, Y1: 70, Z1: -282 }, "Chatka: obszar spawnu nie obejmuje podpór pod podłogą");
        Check(gen.StructureMonsterAt(788, 66, -285) == "witch" && gen.StructureMonsterAt(788, 63, -285) == null
            && gen.StructureMonsterAt(793, 66, -285) == null, "Specjalna lista wiedźm ograniczona do obszaru chatki");
        Check(w.Spawner.EntriesAt(SpawnKind.Monster, 788, 66, -285).Single() is { Key: "witch", Weight: 1, Min: 1, Max: 1 },
            "Chatka zastępuje zwykłe potwory listą wiedźm 1/1–1");
        var flat = new World.World(12345, WorldType.Superflat, false);
        var nether = new World.World(12345, WorldType.Default, false, DimensionId.Nether);
        Check(!flat.Spawner.EntriesAt(SpawnKind.Monster, x, 50, z).Any(e => e.Key == "guardian")
            && !nether.Spawner.EntriesAt(SpawnKind.Monster, x, 50, z).Any(e => e.Key == "guardian"), "Brak reguł monumentów w superflat i Netherze");
        TestOwStructureSpawnPass(box, true);
        TestOwStructureSpawnPass(h, false);
    }

    private static void TestOwStructureSpawnPass(OwBox area, bool water)
    {
        // Kontrolowana komora w rzeczywistym obszarze struktury: badamy całą ścieżkę
        // World.Tick -> limit kategorii -> losowanie listy -> umieszczenie w wodzie/na podłożu.
        var w = new World.World(12345, WorldType.Default, false);
        w.Rand.SetSeed(1809); w.Rules.DoDaylightCycle = false; w.Rules.DoFireTick = false; w.DayTime = 6000;
        for (int cx = (area.X0 >> 4) - 1; cx <= (area.X1 >> 4) + 1; cx++)
            for (int cz = (area.Z0 >> 4) - 1; cz <= (area.Z1 >> 4) + 1; cz++)
            {
                var c = new Chunk(cx, cz); Array.Fill(c.Biomes, (byte)(water ? Biome.DeepOcean : Biome.Swampland));
                for (int lx = 0; lx < 16; lx++) for (int lz = 0; lz < 16; lz++)
                {
                    int x = cx * 16 + lx, z = cz * 16 + lz;
                    if (x < area.X0 || x > area.X1 || z < area.Z0 || z > area.Z1) continue;
                    c.SetBlockRaw(lx, area.Y0, lz, B.Stone, 0);
                    c.SetBlockRaw(lx, area.Y1 + 1, lz, B.Stone, 0);
                    if (water) for (int y = area.Y0 + 1; y <= area.Y1; y++) c.SetBlockRaw(lx, y, lz, B.Water, 0);
                    // Wąski dach bez ścian przepuszcza światło boczne i nie zapewnia
                    // wiedźmom ciemności w południe. Zamknij komorę zamiast zmieniać reguły spawnu.
                    else if (x == area.X0 || x == area.X1 || z == area.Z0 || z == area.Z1)
                        for (int y = area.Y0 + 1; y <= area.Y1; y++) c.SetBlockRaw(lx, y, lz, B.Stone, 0);
                }
                c.RecalcHeightMap(); LightEngine.InitChunkLocal(c); c.Merged = true; c.Populated = true;
                w.Chunks[MathUtil.ChunkKey(cx, cz)] = c;
            }
        var p = SpawnPlayer(w, area.X0 - 28, area.Y0 + 2, (area.Z0 + area.Z1) / 2.0); p.SetGameMode(1); p.Flying = true;
        int ticks = 0;
        while (++ticks <= 8000 && !w.Entities.Any(e => water ? e is Guardian { Elder: false } : e is Witch)) w.Tick();
        var mobs = w.Entities.OfType<Monster>().ToArray();
        Check(mobs.Any(e => water ? e is Guardian { Elder: false } : e is Witch),
            water ? "Naturalny spawner tworzy zwykłego guardiana pod wodą monumentu" : "Naturalny spawner tworzy wiedźmę w chatce", $"tick={ticks}, mobs={mobs.Length}");
        Check(mobs.All(e => (water ? e is Guardian : e is Witch)
            && area.Contains(MathUtil.FloorToInt(e.X), MathUtil.FloorToInt(e.Y), MathUtil.FloorToInt(e.Z))),
            water ? "Grupy guardianów nie wychodzą poza obszar spawnu" : "Grupy wiedźm nie wychodzą poza obszar chatki");
    }
}
