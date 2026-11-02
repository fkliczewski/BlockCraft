using System.Collections.Concurrent;
using BlockCraft.Core;

namespace BlockCraft.World;

public sealed partial class OwGenerator
{
    private readonly ConcurrentDictionary<long, Lazy<OwStructurePlan[]>> _spawnAreas = new();

    /// <summary>ChunkProviderGenerate.getPossibleCreatures: chatka i monument zastępują listę potworów biomu.</summary>
    public string StructureMonsterAt(int x, int y, int z)
    {
        int cx = x >> 4, cz = z >> 4;
        var areas = _spawnAreas.GetOrAdd(MathUtil.ChunkKey(cx, cz), _ => new Lazy<OwStructurePlan[]>(() =>
        {
            var list = new List<OwStructurePlan>();
            // Nie uruchamiaj wyszukiwania kopalń/wiosek/twierdz przy każdej próbie spawnu.
            for (int rx = (cx - 2) >> 5; rx <= (cx + 2) >> 5; rx++)
                for (int rz = (cz - 2) >> 5; rz <= (cz + 2) >> 5; rz++)
                {
                    var p = GridCandidate(rx, rz, 32, 8, 14357617);
                    if (Math.Abs(p.x - cx) <= 2 && Math.Abs(p.z - cz) <= 2 && Biomes.BiomeAt(p.x * 16 + 8, p.z * 16 + 8) == Biome.Swampland)
                        Add(Temple(p.x, p.z));
                }
            for (int rx = (cx - 4) >> 5; rx <= (cx + 4) >> 5; rx++)
                for (int rz = (cz - 4) >> 5; rz <= (cz + 4) >> 5; rz++)
                {
                    var p = GridCandidate(rx, rz, 32, 5, 10387313, true);
                    if (Math.Abs(p.x - cx) <= 4 && Math.Abs(p.z - cz) <= 4) Add(Monument(p.x, p.z));
                }
            return list.ToArray();
            void Add(OwStructurePlan plan)
            {
                if (plan?.MonsterSpawnBox is { } box && box.X1 >= cx * 16 && box.X0 <= cx * 16 + 15
                    && box.Z1 >= cz * 16 && box.Z0 <= cz * 16 + 15) list.Add(plan);
            }
        })).Value;
        if (_spawnAreas.Count > 1024) _spawnAreas.TryRemove(_spawnAreas.Keys.First(), out _);
        foreach (var area in areas) if (area.MonsterSpawnBox.Contains(x, y, z)) return area.MonsterSpawnKey;
        return null;
    }
}
