using System.Collections.Concurrent;
using BlockCraft.Core;
using BlockCraft.Items;

namespace BlockCraft.World;

public sealed record OwEntityPlan(string Key, double X, double Y, double Z, ItemStack[] Slots = null)
{
    public int Profession { get; init; } = -1;
    public bool Elder { get; init; }
}

public sealed class OwBox
{
    public int X0, Y0, Z0, X1, Y1, Z1;
    public int SizeX => X1 - X0 + 1;
    public int SizeY => Y1 - Y0 + 1;
    public int SizeZ => Z1 - Z0 + 1;
    public OwBox(int x0, int y0, int z0, int x1, int y1, int z1) { X0 = x0; Y0 = y0; Z0 = z0; X1 = x1; Y1 = y1; Z1 = z1; }
    public bool Intersects(OwBox b) => X1 >= b.X0 && X0 <= b.X1 && Y1 >= b.Y0 && Y0 <= b.Y1 && Z1 >= b.Z0 && Z0 <= b.Z1;
    public bool Contains(int x, int y, int z) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1 && z >= Z0 && z <= Z1;
    public void MoveY(int y) { Y0 += y; Y1 += y; }
}

public sealed record OwStructurePlan(string Name, OwBox Box, OwGenRegion Region);

public sealed partial class OwGenerator
{
    private readonly ConcurrentDictionary<(string type, int x, int z), Lazy<OwStructurePlan>> _structures = new();

    public JRandom MapRandom(int cx, int cz)
    {
        var r = new JRandom(Seed); long a = r.NextLong(), b = r.NextLong();
        r.SetSeed(unchecked(cx * a ^ cz * b ^ Seed));
        r.NextInt(); // MapGenStructure.recursiveGenerate
        return r;
    }

    public (int x, int z) GridCandidate(int regionX, int regionZ, int spacing, int separation, int salt, bool triangular = false)
    {
        var r = new JRandom(unchecked(regionX * 341873128712L + regionZ * 132897987541L + Seed + salt));
        int n = spacing - separation;
        int x = triangular ? (r.NextInt(n) + r.NextInt(n)) / 2 : r.NextInt(n);
        int z = triangular ? (r.NextInt(n) + r.NextInt(n)) / 2 : r.NextInt(n);
        return (regionX * spacing + x, regionZ * spacing + z);
    }

    private OwStructurePlan CachedStructure(string type, int cx, int cz, Func<OwStructurePlan> build)
    {
        var p = _structures.GetOrAdd((type, cx, cz), _ => new Lazy<OwStructurePlan>(build)).Value;
        if (_structures.Count > 2048) _structures.TryRemove(_structures.Keys.First(), out _);
        return p;
    }

    public bool IsTempleStart(int cx, int cz)
    {
        var start = GridCandidate(cx >> 5, cz >> 5, 32, 8, 14357617);
        if (start != (cx, cz)) return false;
        int b = Biomes.BiomeAt(cx * 16 + 8, cz * 16 + 8);
        return b is Biome.Desert or Biome.DesertHills or Biome.Jungle or Biome.JungleHills or Biome.Swampland;
    }

    public OwStructurePlan Temple(int cx, int cz) => CachedStructure("Temple", cx, cz, () =>
    {
        if (!IsTempleStart(cx, cz)) return null;
        int b = Biomes.BiomeAt(cx * 16 + 8, cz * 16 + 8);
        bool desert = b is Biome.Desert or Biome.DesertHills, hut = b == Biome.Swampland;
        int width = desert ? 21 : hut ? 7 : 12, depth = desert ? 21 : hut ? 9 : 15;
        var r = MapRandom(cx, cz); int direction = new[] { 2, 5, 3, 4 }[r.NextInt(4)];
        var g = new OwGenRegion(this, true);
        int y = 64, sx = direction is 2 or 3 ? width : depth, sz = direction is 2 or 3 ? depth : width;
        if (!desert)
        {
            int total = 0;
            for (int x = 0; x < sx; x++) for (int z = 0; z < sz; z++) total += Math.Max(64, g.TopSolid(cx * 16 + x, cz * 16 + z));
            y = total / (sx * sz);
        }
        var s = new OwStructureWriter(g, r, cx * 16, y, cz * 16, width, depth, direction);
        if (desert) OwTemples.Desert(s, r);
        else if (hut)
        {
            OwTemples.Hut(s, r); var p = s.Point(2, 2, 5);
            g.Entities.Add(new("witch", p.x + 0.5, p.y, p.z + 0.5));
        }
        else OwTemples.Jungle(s, r);
        g.ReleaseReadCache();
        return new("Temple", new OwBox(cx * 16, y - (desert ? 14 : 4), cz * 16, cx * 16 + sx - 1, y + (desert ? 14 : hut ? 6 : 9), cz * 16 + sz - 1), g);
    });

    public bool IsMineshaftStart(int cx, int cz)
    {
        var r = MapRandom(cx, cz);
        return r.NextDouble() < 0.004 && r.NextInt(80) < Math.Max(Math.Abs(cx), Math.Abs(cz));
    }
    public OwStructurePlan Mineshaft(int cx, int cz) => CachedStructure("Mineshaft", cx, cz, () =>
    {
        var r = MapRandom(cx, cz);
        if (!(r.NextDouble() < 0.004 && r.NextInt(80) < Math.Max(Math.Abs(cx), Math.Abs(cz)))) return null;
        return OwMineshaft.Build(this, r, cx, cz);
    });

    partial void CollectOtherStructures(List<OwStructurePlan> plans, int cx, int cz);

    public List<OwStructurePlan> StructurePlans(int cx, int cz)
    {
        var list = new List<OwStructurePlan>();
        for (int rx = (cx - 2) >> 5; rx <= (cx + 2) >> 5; rx++)
            for (int rz = (cz - 2) >> 5; rz <= (cz + 2) >> 5; rz++)
            {
                var candidate = GridCandidate(rx, rz, 32, 8, 14357617);
                if (Math.Abs(candidate.x - cx) > 2 || Math.Abs(candidate.z - cz) > 2) continue;
                var p = Temple(candidate.x, candidate.z); if (p != null) list.Add(p);
            }
        for (int sx = cx - 8; sx <= cx + 8; sx++) for (int sz = cz - 8; sz <= cz + 8; sz++)
        {
            if (!IsMineshaftStart(sx, sz)) continue;
            var p = Mineshaft(sx, sz); if (p != null) list.Add(p);
        }
        CollectOtherStructures(list, cx, cz);
        return list;
    }

    partial void GenerateStructures(int cx, int cz, ProtoChunk p)
    {
        foreach (var structure in StructurePlans(cx, cz)) structure.Region.Apply(p, cx, cz);
    }
}
