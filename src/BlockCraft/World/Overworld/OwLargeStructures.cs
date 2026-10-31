using BlockCraft.World.Structures18;
using Component = BlockCraft.World.Structures18.StructureComponent;
using Facing18 = BlockCraft.World.Structures18.Facing;
using VillagePieces = BlockCraft.World.Structures18.StructureVillagePieces;
using StrongholdPieces = BlockCraft.World.Structures18.StructureStrongholdPieces;

namespace BlockCraft.World;

public sealed partial class OwGenerator
{
    private readonly object _strongholdPositionLock = new();
    private (int x, int z)[] _strongholdChunks;
    private static readonly object StrongholdBuildLock = new();
    private static readonly int[] VillageBiomes = { Biome.Plains, Biome.Desert, Biome.Savanna };
    private static readonly int[] MonumentBiomes = { Biome.Ocean, Biome.DeepOcean, Biome.River, Biome.FrozenOcean, Biome.FrozenRiver };

    public (int x, int z)[] StrongholdChunks()
    {
        lock (_strongholdPositionLock)
        {
            if (_strongholdChunks == null)
            {
                var positions = new (int x, int z)[3];
                var allowed = Biome.ById.Where(b => b != null && b.MinHeight > 0).Select(b => b.Id).ToArray();
                var r = new JRandom(Seed); double angle = r.NextDouble() * Math.PI * 2;
                for (int i = 0; i < positions.Length; i++)
                {
                    double distance = (1.25 + r.NextDouble()) * 32;
                    int cx = (int)Math.Floor(Math.Cos(angle) * distance + 0.5), cz = (int)Math.Floor(Math.Sin(angle) * distance + 0.5);
                    var position = Biomes.FindBiomePosition(cx * 16 + 8, cz * 16 + 8, 112, allowed, r);
                    if (position != null) { cx = position.Value.x >> 4; cz = position.Value.z >> 4; }
                    positions[i] = (cx, cz); angle += Math.PI * 2 / 3;
                }
                _strongholdChunks = positions;
            }
            return (ValueTuple<int, int>[])_strongholdChunks.Clone();
        }
    }

    public bool IsVillageStart(int cx, int cz) => GridCandidate(cx >> 5, cz >> 5, 32, 8, 10387312) == (cx, cz)
        && Biomes.AreBiomesViable(cx * 16 + 8, cz * 16 + 8, 0, VillageBiomes);
    public bool IsMonumentStart(int cx, int cz) => GridCandidate(cx >> 5, cz >> 5, 32, 5, 10387313, true) == (cx, cz)
        && Biomes.BiomeAt(cx * 16 + 8, cz * 16 + 8) == Biome.DeepOcean
        && Biomes.AreBiomesViable(cx * 16 + 8, cz * 16 + 8, 29, MonumentBiomes);

    private static Structures18.StructureBoundingBox Bounds(IEnumerable<Component> components)
    {
        var boxes = components.Select(p => p.boundingBox).ToArray();
        return new(boxes.Min(b => b.minX), boxes.Min(b => b.minY), boxes.Min(b => b.minZ), boxes.Max(b => b.maxX), boxes.Max(b => b.maxY), boxes.Max(b => b.maxZ));
    }
    public JavaList<Component> VillageComponents(JRandom r, int cx, int cz)
    {
        var weights = VillagePieces.getStructureVillageWeightedPieceList(r, 0);
        var root = new VillagePieces.Start(new WorldChunkManager(Biomes), 0, r, cx * 16 + 2, cz * 16 + 2, weights, 0);
        var pieces = new JavaList<Component> { root }; root.buildComponent(root, pieces, r);
        while (root.field_74930_j.Count > 0 || root.field_74932_i.Count > 0)
        {
            var queue = root.field_74930_j.Count > 0 ? root.field_74930_j : root.field_74932_i;
            queue.remove(r.NextInt(queue.Count)).buildComponent(root, pieces, r);
        }
        return pieces;
    }
    public OwStructurePlan Village(int cx, int cz) => CachedStructure("Village", cx, cz, () =>
    {
        if (!IsVillageStart(cx, cz)) return null;
        var r = MapRandom(cx, cz); var pieces = VillageComponents(r, cx, cz);
        if (pieces.Count(p => p is not VillagePieces.Road) <= 2) return null;
        return DrawComponents("Village", pieces, r);
    });

    public JavaList<Component> StrongholdComponents(JRandom r, int cx, int cz)
    {
        // MCP przechowuje listę wag w polach statycznych. Blokada obejmuje cały graf i jego rysowanie.
        lock (StrongholdBuildLock)
        {
            for (;;)
            {
                StrongholdPieces.prepareStructurePieces();
                var root = new StrongholdPieces.Stairs2(0, r, cx * 16 + 2, cz * 16 + 2);
                var pieces = new JavaList<Component> { root }; root.buildComponent(root, pieces, r);
                while (root.field_75026_c.Count > 0) root.field_75026_c.remove(r.NextInt(root.field_75026_c.Count)).buildComponent(root, pieces, r);
                var box = Bounds(pieces);
                int targetTop = box.getYSize() + 1;
                if (targetTop < 53) targetTop += r.NextInt(53 - targetTop);
                int dy = targetTop - box.maxY;
                foreach (var p in pieces) p.func_181138_a(0, dy, 0);
                if (root.strongholdPortalRoom != null) return pieces;
            }
        }
    }
    public OwStructurePlan Stronghold(int cx, int cz) => CachedStructure("Stronghold", cx, cz, () =>
    {
        if (!StrongholdChunks().Contains((cx, cz))) return null;
        lock (StrongholdBuildLock)
        {
            var r = MapRandom(cx, cz);
            return DrawComponents("Stronghold", StrongholdComponents(r, cx, cz), r);
        }
    });
    public OwStructurePlan Monument(int cx, int cz) => CachedStructure("Monument", cx, cz, () =>
    {
        if (!IsMonumentStart(cx, cz)) return null;
        var seedRandom = new JRandom(Seed); long a = seedRandom.NextLong(), b = seedRandom.NextLong();
        var r = new JRandom(unchecked(cx * a ^ cz * b ^ Seed));
        var piece = new StructureOceanMonumentPieces.MonumentBuilding(r, cx * 16 - 21, cz * 16 - 21, Facing18.RandomHorizontal(r));
        return DrawComponents("Monument", new JavaList<Component> { piece }, r);
    });
    private OwStructurePlan DrawComponents(string type, JavaList<Component> pieces, JRandom r)
    {
        var g = new OwGenRegion(this, true); var world = new StructureWorld(this, g);
        // Cały komponent powstaje w jednej transakcji. Dopiero Apply przycina zapis do docelowego chunka.
        var clip = new Structures18.StructureBoundingBox(int.MinValue, 0, int.MinValue, int.MaxValue, 255, int.MaxValue);
        foreach (var piece in pieces) piece.addComponentParts(world, r, clip);
        var box = Bounds(pieces).ToBox();
        var spawnBox = type == "Monument" ? Bounds(pieces).ToBox() : null;
        if (type == "Monument") { box.X0 -= 5; box.Z0 -= 5; box.X1 += 5; box.Z1 += 5; box.Y0 = 1; box.Y1 = 64; }
        if (type == "Village") { box.Y0 = 1; box.Y1 = Math.Min(255, box.Y1 + 8); }
        g.ReleaseReadCache();
        return new(type, box, g) { MonsterSpawnBox = spawnBox, MonsterSpawnKey = spawnBox != null ? "guardian" : null };
    }
    partial void CollectOtherStructures(List<OwStructurePlan> plans, int cx, int cz)
    {
        for (int rx = (cx - 9) >> 5; rx <= (cx + 9) >> 5; rx++) for (int rz = (cz - 9) >> 5; rz <= (cz + 9) >> 5; rz++)
        {
            var p = GridCandidate(rx, rz, 32, 8, 10387312);
            if (Math.Abs(p.x - cx) <= 9 && Math.Abs(p.z - cz) <= 9) Add(Village(p.x, p.z));
        }
        foreach (var p in StrongholdChunks()) if (Math.Abs(p.x - cx) <= 9 && Math.Abs(p.z - cz) <= 9) Add(Stronghold(p.x, p.z));
        for (int rx = (cx - 4) >> 5; rx <= (cx + 4) >> 5; rx++) for (int rz = (cz - 4) >> 5; rz <= (cz + 4) >> 5; rz++)
        {
            var p = GridCandidate(rx, rz, 32, 5, 10387313, true);
            if (Math.Abs(p.x - cx) <= 4 && Math.Abs(p.z - cz) <= 4) Add(Monument(p.x, p.z));
        }
        void Add(OwStructurePlan p)
        { if (p != null && p.Box.X1 >= cx * 16 && p.Box.X0 <= cx * 16 + 15 && p.Box.Z1 >= cz * 16 && p.Box.Z0 <= cz * 16 + 15) plans.Add(p); }
    }
}
