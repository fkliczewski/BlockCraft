# src/BlockCraft/Entities/MobSpawner.cs

- MobSpawner · class · L6-L155 — public sealed class MobSpawner
- MobSpawner · method · L9-L9 — public MobSpawner(World.World w) { _w = w; }
- Create · method · L20-L25 — public static Mob Create(string key, World.World w) => key.ToLowerInvariant() switch
- Pick · method · L28-L34 — private static Func<World.World, Mob> Pick((Func<World.World, Mob> f, int weight)[] list, Rng r)
- Tick · method · L36-L63 — public void Tick()
- SpawnPass · method · L65-L103 — private void SpawnPass(HashSet<long> chunks, (Func<World.World, Mob>, int)[] table, bool hostile, int budget)
- CanSpawnAt · method · L105-L113 — private bool CanSpawnAt(int x, int y, int z, bool hostile)
- FitsAt · method · L115-L122 — private bool FitsAt(Mob m)
- LightOk · method · L124-L130 — private bool LightOk(int x, int y, int z)
- PopulateChunk · method · L133-L154 — public void PopulateChunk(Chunk c)
