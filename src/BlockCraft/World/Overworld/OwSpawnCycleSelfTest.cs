using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestOwSpawnCycle()
    {
        var w = new World.World(18, WorldType.Superflat, false);
        var p = SpawnPlayer(w, .5, 64, .5);
        var region = w.Spawner.SpawnRegion();
        Check(region.CapChunks == 289 && region.Chunks.Count == 225,
            "Spawn 1.8: 289 chunków do limitu i 225 do prób, także przy niewczytanym terenie");
        Check(region.Chunks.Contains(MathUtil.ChunkKey(-7, -7)) && region.Chunks.Contains(MathUtil.ChunkKey(7, 7))
            && !region.Chunks.Contains(MathUtil.ChunkKey(8, 0)), "Zewnętrzny pierścień nie wykonuje prób spawnu");
        var p2 = SpawnPlayer(w, .5, 64, .5);
        region = w.Spawner.SpawnRegion();
        Check(region.CapChunks == 353 && region.Chunks.Count == 225,
            "Spawn 1.8: dwóch graczy w tym samym miejscu ponownie dolicza pierścień do limitu");
        p2.SetPositionAndPrev(1024, 64, 1024); region = w.Spawner.SpawnRegion();
        Check(region.CapChunks == 578 && region.Chunks.Count == 450, "Odlegli gracze mają rozłączne obszary spawnu");
        p2.SetGameMode(3); p.SetPositionAndPrev(-.5, 64, -.5); region = w.Spawner.SpawnRegion();
        Check(region.CapChunks == 289 && region.Chunks.Contains(MathUtil.ChunkKey(-8, -8))
            && !region.Chunks.Contains(MathUtil.ChunkKey(7, 0)), "Ujemne współrzędne chunków i pomijanie widza w regionie spawnu");
        p.SetGameMode(3); region = w.Spawner.SpawnRegion();
        Check(region.CapChunks == 0 && region.Chunks.Count == 0, "Sami widzowie nie tworzą obszarów naturalnego spawnu");

        var zombie = new Zombie(w); w.AddEntityNow(zombie);
        var named = new Zombie(w) { CustomName = "named" }; w.AddEntityNow(named);
        var persistent = new Zombie(w) { PersistenceRequired = true }; w.AddEntityNow(persistent);
        var cow = new Cow(w); w.AddEntityNow(cow);
        Check(w.Spawner.CountForCap(SpawnKind.Monster) == 1 && w.Spawner.CountForCap(SpawnKind.Creature) == 1,
            "Mobcap pomija nazwane i oznaczone trwałe moby, ale liczy zwykłe zwierzęta");
        zombie.Removed = true;
        Check(w.Spawner.CountForCap(SpawnKind.Monster) == 0, "Usunięty mob nie zajmuje limitu spawnu");
        w.SpawnX = 0; w.SpawnY = 64; w.SpawnZ = 0;
        Check(!w.Spawner.FarEnough(.5, 64, .5) && w.Spawner.FarEnough(.5, 100, .5),
            "Odległość 24 bloków od spawnu świata uwzględnia wysokość");
        p.SetPositionAndPrev(100, 64, 100); p.SetGameMode(1);
        Check(!w.Spawner.FarEnough(100, 64, 100) && w.Spawner.FarEnough(124, 64, 100),
            "Gracz kreatywny wyklucza spawn bliżej niż 24 bloki; granica 24 jest dozwolona");
        p.SetGameMode(3);
        Check(w.Spawner.FarEnough(100, 64, 100), "Widz nie blokuje spawnu mobów w swoim otoczeniu");
    }
}
