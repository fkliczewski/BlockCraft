using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>EnumCreatureType z 1.8: limit na 289 chunków, czy spawn tylko co 400 ticków, czy w wodzie.</summary>
public enum SpawnKind { Monster, Creature, Ambient, Water }

public sealed record SpawnEntry(string Key, int Weight, int Min, int Max);

/// <summary>Listy spawnu biomów jak BiomeGenBase i podklasy w 1.8 (spawnableMonsterList, spawnableCreatureList, ambient, water).</summary>
public static class BiomeSpawns
{
    public static int Cap(SpawnKind k) => k switch { SpawnKind.Monster => 70, SpawnKind.Creature => 10, SpawnKind.Ambient => 15, _ => 5 };
    public static bool CountsAs(Entity e, SpawnKind k) => k switch
    {
        SpawnKind.Monster => e is Monster,
        SpawnKind.Creature => e is Animal && e is not Villager,
        SpawnKind.Ambient => e is Bat,
        _ => e is Squid
    };

    private static readonly Dictionary<(int, SpawnKind), SpawnEntry[]> Cache = new();

    public static SpawnEntry[] For(int biome, SpawnKind kind)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue((biome, kind), out var list)) Cache[(biome, kind)] = list = Build(biome, kind).ToArray();
            return list;
        }
    }

    private static List<SpawnEntry> Build(int biome, SpawnKind kind)
    {
        var def = biome >= 0 && biome < Biome.ById.Length ? Biome.ById[biome] : null;
        var k = def?.Kind ?? BiomeKind.Plains;
        var l = new List<SpawnEntry>();
        if (k is BiomeKind.Hell or BiomeKind.Sky) return l; // Nether i End: DimensionSpawner (NE)
        switch (kind)
        {
            case SpawnKind.Monster:
                if (k == BiomeKind.Mushroom) return l;
                l.Add(new("spider", 100, 4, 4)); l.Add(new("zombie", 100, 4, 4)); l.Add(new("skeleton", 100, 4, 4)); l.Add(new("creeper", 100, 4, 4));
                l.Add(new("slime", 100, 4, 4)); l.Add(new("enderman", 10, 1, 4)); l.Add(new("witch", 5, 1, 1));
                if (k == BiomeKind.Swamp) l.Add(new("slime", 1, 1, 1));
                if (k == BiomeKind.Jungle) l.Add(new("ocelot", 2, 1, 1)); // BiomeGenJungle dodaje ocelota do listy potworów
                break;
            case SpawnKind.Creature:
                switch (k)
                {
                    case BiomeKind.Ocean or BiomeKind.River or BiomeKind.Beach or BiomeKind.StoneBeach or BiomeKind.Mesa: return l;
                    case BiomeKind.Desert: l.Add(new("rabbit", 4, 2, 3)); return l;
                    case BiomeKind.Mushroom: l.Add(new("mooshroom", 8, 4, 8)); return l;
                    case BiomeKind.Snow: l.Add(new("rabbit", 10, 2, 3)); return l;
                }
                l.Add(new("sheep", 12, 4, 4)); l.Add(new("rabbit", 10, 3, 3)); l.Add(new("pig", 10, 4, 4)); l.Add(new("chicken", 10, 4, 4)); l.Add(new("cow", 8, 4, 4));
                if (k == BiomeKind.Plains) l.Add(new("horse", 5, 2, 6));
                if (k == BiomeKind.Savanna) l.Add(new("horse", 1, 2, 6));
                if (k == BiomeKind.Forest && def?.Variant == 0) l.Add(new("wolf", 5, 4, 4));
                if (k == BiomeKind.Forest && def?.Variant == 1) l.Add(new("rabbit", 4, 2, 3));
                if (k == BiomeKind.Taiga) { l.Add(new("wolf", 8, 4, 4)); l.Add(new("rabbit", 4, 2, 3)); }
                if (k == BiomeKind.Jungle) l.Add(new("chicken", 10, 4, 4));
                break;
            case SpawnKind.Ambient: l.Add(new("bat", 10, 8, 8)); break;
            case SpawnKind.Water: if (k != BiomeKind.Mushroom) l.Add(new("squid", 10, 4, 4)); break;
        }
        return l;
    }

    public static SpawnEntry Pick(SpawnEntry[] list, Core.Rng r)
    {
        if (list.Length == 0) return null;
        int total = 0; foreach (var e in list) total += e.Weight;
        int v = r.Next(total);
        foreach (var e in list) { if (v < e.Weight) return e; v -= e.Weight; }
        return list[0];
    }
}
