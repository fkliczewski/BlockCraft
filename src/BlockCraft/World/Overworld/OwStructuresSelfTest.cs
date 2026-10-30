using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private sealed class OwTestSpawner : TileEntity
    {
        public string EntityId;
        public override int TypeId => 50;
        public override void SetData(string key, string value) { if (key == "EntityId") EntityId = value; }
        public override void Save(BinaryWriter w) { }
        public override void Load(BinaryReader r) { }
    }

    private static void TestOwStructures()
    {
        var gen = new OwGenerator(1, WorldType.Default);
        var g = new OwGenRegion(gen, true);
        for (int x = -5; x <= 5; x++) for (int y = 38; y <= 45; y++) for (int z = -5; z <= 5; z++) g.Set(x, y, z, B.Stone);
        g.Set(4, 40, 0, B.Air); g.Set(4, 41, 0, B.Air);
        bool dungeon = OwDungeons.Generate(g, new JRandom(0), 0, 40, 0);
        Check(dungeon && g.Get(0, 40, 0) == B.MobSpawner, "Loch: wnętrze, wejście i spawner");
        TileEntity.Types.TryGetValue(50, out var original);
        try
        {
            TileEntity.Types[50] = () => new OwTestSpawner();
            var c = new Chunk(0, 0); g.Apply(c);
            Check(c.TileEntities.Values.OfType<OwTestSpawner>().Any(t => t.EntityId is "zombie" or "skeleton" or "spider"), "Loch ustawia EntityId spawnera przez API między modułami");
        }
        finally { if (original == null) TileEntity.Types.Remove(50); else TileEntity.Types[50] = original; }
        bool pyramids = true, jungle = true, huts = true, copies = true;
        foreach (int direction in new[] { 2, 3, 4, 5 })
        {
            var region = new OwGenRegion(gen, true); var r = new JRandom(123);
            var s = new OwStructureWriter(region, r, -16, 80, 16, 21, 21, direction);
            OwTemples.Desert(s, r);
            var pressure = s.Point(10, -11, 10);
            pyramids &= region.Get(pressure.x, pressure.y, pressure.z) == B.StonePressurePlate;
            int tnt = 0;
            for (int x = 9; x <= 11; x++) for (int z = 9; z <= 11; z++) { var p = s.Point(x, -13, z); if (region.Get(p.x, p.y, p.z) == B.Tnt) tnt++; }
            pyramids &= tnt == 9;
            int chests = 0;
            for (int cx = -1; cx <= 0; cx++) for (int cz = 1; cz <= 2; cz++)
            {
                var a = new Chunk(cx, cz); region.Apply(a);
                chests += a.TileEntities.Values.OfType<ChestTileEntity>().Count();
                foreach (var chest in a.TileEntities.Values.OfType<ChestTileEntity>()) foreach (var stack in chest.Slots) if (stack != null) stack.Count = 99;
                var b = new Chunk(cx, cz); region.Apply(b);
                copies &= b.TileEntities.Values.OfType<ChestTileEntity>().All(te => te.Slots.All(stack => stack == null || stack.Count != 99));
            }
            pyramids &= chests == 4;
            region = new OwGenRegion(gen, true); r = new JRandom(123);
            s = new OwStructureWriter(region, r, -16, 80, 16, 12, 15, direction);
            OwTemples.Jungle(s, r);
            foreach (var pos in new[] { (3, -2, 1), (9, -2, 3) }) { var p = s.Point(pos.Item1, pos.Item2, pos.Item3); jungle &= region.Get(p.x, p.y, p.z) == B.Dispenser; }
            foreach (var pos in new[] { (8, -3, 3), (9, -3, 10) }) { var p = s.Point(pos.Item1, pos.Item2, pos.Item3); jungle &= region.Get(p.x, p.y, p.z) == B.Chest; }
            region = new OwGenRegion(gen, true); r = new JRandom(123);
            s = new OwStructureWriter(region, r, -16, 80, 16, 7, 9, direction);
            OwTemples.Hut(s, r);
            var table = s.Point(3, 2, 6); var cauldron = s.Point(4, 2, 6);
            huts &= region.Get(table.x, table.y, table.z) == B.CraftingTable && region.Get(cauldron.x, cauldron.y, cauldron.z) == B.Cauldron;
        }
        Check(pyramids, "Pustynna świątynia: 4 skrzynie i pułapka 9 TNT w czterech obrotach");
        Check(jungle, "Dżunglowa świątynia: 2 dozowniki i 2 skrzynie w czterech obrotach");
        Check(huts, "Chatka wiedźmy: wyposażenie i cztery obroty");
        Check(copies, "Skrzynie wygenerowane z cache mają niezależne kopie łupów");
        var mine = OwMineshaft.Build(gen, new JRandom(1), 20, -10);
        Check(mine.Box.SizeX > 10 && mine.Box.SizeZ > 10 && mine.Box.Y0 > 0 && mine.Box.Y1 < 63, "Kopalnia: rozgałęzienia i podziemne położenie");
    }
}
