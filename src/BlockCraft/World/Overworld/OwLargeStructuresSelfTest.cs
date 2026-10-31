using BlockCraft.World;
using BlockCraft.World.Structures18;
using BlockCraft.Items;
using VillagePieces = BlockCraft.World.Structures18.StructureVillagePieces;
using StrongholdPieces = BlockCraft.World.Structures18.StructureStrongholdPieces;
using Structures18 = BlockCraft.World.Structures18;

namespace BlockCraft;

public static partial class SelfTest
{
    private static ulong OwComponentHash(IEnumerable<StructureComponent> pieces)
    {
        ulong h = 0xcbf29ce484222325;
        foreach (var p in pieces)
        {
            var b = p.boundingBox;
            foreach (int v in new[] { b.minX, b.minY, b.minZ, b.maxX, b.maxY, b.maxZ, p.coordBaseMode == EnumFacing.NONE ? -1 : p.coordBaseMode.getHorizontalIndex(), p.componentType })
                h = unchecked((h ^ (ulong)v) * 0x100000001b3);
        }
        return h;
    }
    private static void TestOwLargeStructures()
    {
        TestOwStructureGolden();
        var gen = new OwGenerator(1, WorldType.Default);
        var clip = new StructureBoundingBox(-256, 0, -256, 256, 255, 256);
        bool portals = true, smiths = true, libraries = true, stairs = true;
        foreach (var direction in new[] { EnumFacing.NORTH, EnumFacing.SOUTH, EnumFacing.WEST, EnumFacing.EAST })
        {
            var r = new JRandom(1); var g = new OwGenRegion(gen, true); var w = new StructureWorld(gen, g);
            var box = StructureBoundingBox.getComponentToAddBoundingBox(0, 120, 0, 0, 0, 0, 11, 8, 16, direction);
            var portal = new StrongholdPieces.PortalRoom(1, r, box, direction);
            portal.addComponentParts(w, r, clip);
            var facingCounts = new int[4]; int count = 0, spawners = 0;
            for (int x = box.minX; x <= box.maxX; x++) for (int y = box.minY; y <= box.maxY; y++) for (int z = box.minZ; z <= box.maxZ; z++)
            {
                int id = g.Get(x, y, z);
                if (id == B.EndPortalFrame) { count++; facingCounts[g.GetMeta(x, y, z) & 3]++; }
                if (id == B.MobSpawner) spawners++;
            }
            portals &= count == 12 && facingCounts.All(n => n == 3) && spawners == 1;
            var stair = new Structures18.IBlockState(Structures18.Blocks.stone_brick_stairs, 0);
            w.setBlockState(new BlockPos(200, 120, 200), stair, 2);
            stairs &= g.GetMeta(200, 120, 200) == 3;
            g = new OwGenRegion(gen, true); w = new StructureWorld(gen, g); r = new JRandom(10);
            box = StructureBoundingBox.getComponentToAddBoundingBox(0, 120, 0, 0, 0, 0, 10, 6, 7, direction);
            var smith = new VillagePieces.House2(new VillagePieces.Start(), 1, r, box, direction) { field_143015_k = 120 };
            smith.addComponentParts(w, r, clip);
            int chestX = smith.getXWithOffset(5, 5), chestY = smith.getYWithOffset(1), chestZ = smith.getZWithOffset(5, 5);
            var c = new Chunk(chestX >> 4, chestZ >> 4); g.Apply(c);
            smiths &= c.TileEntities.Values.OfType<ChestTileEntity>().Any(te => te.Slots.Any(s => s != null))
                && g.Entities.Any(e => e.Key == "villager" && e.Profession == 3);
            g = new OwGenRegion(gen, true); w = new StructureWorld(gen, g); r = new JRandom(2);
            box = StructureBoundingBox.getComponentToAddBoundingBox(0, 180, 0, 0, 0, 0, 14, 11, 15, direction);
            var library = new StrongholdPieces.Library(1, r, box, direction);
            library.addComponentParts(w, r, clip);
            int shelves = 0;
            for (int x = box.minX; x <= box.maxX; x++) for (int y = box.minY; y <= box.maxY; y++) for (int z = box.minZ; z <= box.maxZ; z++) if (g.Get(x, y, z) == B.Bookshelf) shelves++;
            libraries &= shelves > 100;
        }
        Check(portals, "Twierdza: 12 ram portalu, cztery kierunki i spawner rybików w każdym obrocie");
        Check(stairs, "Struktury tłumaczą metadane kierunku schodów MC na format fundamentu");
        Check(smiths, "Wioska: kowal ze skrzynią łupów i planem wieśniaka o profesji 3");
        Check(libraries, "Twierdza: duża biblioteka z regałami we wszystkich obrotach");
        var region = new OwGenRegion(gen, true); var random = new JRandom(1);
        var monument = new StructureOceanMonumentPieces.MonumentBuilding(random, 0, 0, EnumFacing.SOUTH);
        monument.addComponentParts(new StructureWorld(gen, region), random, clip);
        int gold = 0, wetSponges = 0, lanterns = 0;
        for (int x = 0; x < 58; x++) for (int y = 39; y <= 61; y++) for (int z = 0; z < 58; z++)
        {
            int id = region.Get(x, y, z);
            if (id == B.GoldBlock) gold++;
            if (id == B.Sponge && region.GetMeta(x, y, z) == 1) wetSponges++;
            if (id == B.SeaLantern) lanterns++;
        }
        Check(gold == 8 && wetSponges > 0 && lanterns > 0, "Monument: skarbiec 8 bloków złota, mokre gąbki i latarnie", $"{gold}/{wetSponges}/{lanterns}");
        Check(region.Entities.Count(e => e.Key == "guardian" && e.Elder) == 3, "Monument: trzy plany starszych strażników");
        var location = gen.Locate("Stronghold", 0, 0);
        Check(location != null && gen.StrongholdChunks().Contains((location.Value.x >> 4, location.Value.z >> 4)), "Lokator twierdzy wskazuje jeden z trzech startów");
        var world = MakeWorld(1, WorldType.Superflat, 0);
        Check(WorldHooks.FindStronghold != null && WorldHooks.Locators.ContainsKey("Monument") && WorldHooks.FindStronghold(world, 0, 0) == null, "Haki lokatorów rejestrują struktury i uwzględniają typ świata");
    }
}
