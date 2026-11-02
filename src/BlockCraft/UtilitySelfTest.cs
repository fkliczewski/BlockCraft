using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Kompas, zegar, szafa grająca i płyty (moduł ME-e).</summary>
public static partial class SelfTest
{
    private sealed class UtilSoundLog : IWorldListener
    {
        public readonly List<(string n, double x, double y, double z)> All = new();
        public void PlaySound(string n, double x, double y, double z, float volume, float pitch) => All.Add((n, x, y, z));
        public void OnBlockChanged(int x, int y, int z, byte id, int meta) { }
        public void SpawnParticles(ParticleKind k, double x, double y, double z, int count, int data) { }
        public void OnEntityAdded(Entity e) { }
        public void OnEntityRemoved(Entity e) { }
    }

    private static void TestUtilityItems()
    {
        var grid = new ItemStack[9];
        grid[1] = grid[3] = grid[5] = grid[7] = new ItemStack(I.IronIngot); grid[4] = new ItemStack(I.Redstone);
        Check(Recipes.Find(grid, 3, 3)?.Id == I.Compass, "Kompas: receptura");
        grid[1] = grid[3] = grid[5] = grid[7] = new ItemStack(I.GoldIngot);
        Check(Recipes.Find(grid, 3, 3)?.Id == I.Clock, "Zegar: receptura");
        grid = Enumerable.Repeat(new ItemStack(B.OakPlanks), 9).ToArray(); grid[4] = new ItemStack(I.Diamond);
        Check(Recipes.Find(grid, 3, 3)?.Id == B.Jukebox, "Szafa grająca: receptura");
        Check(Enumerable.Range(I.Record13, 12).All(id => Item.ById[id] != null && Item.ById[id].MaxStack == 1 && Item.ById[id].LoreFn(new ItemStack(id)).StartsWith("C418 - ")),
            "Płyty: 12 płyt, stos 1, opis C418");

        var w = MakeWorld(11, WorldType.Superflat, 1);
        var p = new Player(w); p.SetPositionAndPrev(8.5, 4, 8.5); w.AddEntityNow(p);
        // kompas: punkt odrodzenia na wprost (gracz patrzy na południe, +Z) -> igła do góry (klatka 16)
        w.SpawnX = 8; w.SpawnZ = 100; p.Yaw = 0;
        for (int i = 0; i < 200; i++) ItemAnimations.Tick(w, p);
        Check(ItemAnimations.CompassFrame is 15 or 16 or 17, "Kompas: odrodzenie na wprost = igła do góry", ItemAnimations.CompassFrame.ToString());
        w.SpawnX = -100; w.SpawnZ = 8;
        for (int i = 0; i < 200; i++) ItemAnimations.Tick(w, p);
        Check(ItemAnimations.CompassFrame is 23 or 24 or 25, "Kompas: odrodzenie po prawej (zachód przy patrzeniu na południe) = klatka 24", ItemAnimations.CompassFrame.ToString());
        w.DayTime = 6000;
        for (int i = 0; i < 300; i++) ItemAnimations.Tick(w, p);
        int noon = ItemAnimations.ClockFrame;
        w.DayTime = 18000;
        for (int i = 0; i < 300; i++) ItemAnimations.Tick(w, p);
        int midnight = ItemAnimations.ClockFrame;
        Check(noon is 0 or 1 or 63 && midnight is >= 31 and <= 33, "Zegar: południe klatka 0, północ klatka 32", $"{noon}/{midnight}");

        // szafa grająca
        var log = new UtilSoundLog(); w.Listeners.Add(log);
        int jx = 10, jy = 4, jz = 8;
        w.SetBlock(jx, jy, jz, B.Jukebox);
        p.Inventory.Selected = new ItemStack(I.RecordCat);
        bool used = Item.ById[I.RecordCat].UseOnBlock(p, p.Inventory.Selected, jx, jy, jz, Dir.Up, .5, 1, .5);
        Check(used && p.Inventory.Selected == null && w.GetMeta(jx, jy, jz) == 1 && log.All.Any(s => s.n == "record:cat"), "Szafa grająca: włożenie płyty gra cat");
        Check(Block.Get(B.Jukebox) is IComparatorSource cs && cs.GetComparatorOutput(w, jx, jy, jz) == 2, "Szafa grająca: komparator = numer płyty (cat = 2)");
        Check(!Item.ById[I.RecordWait].UseOnBlock(p, new ItemStack(I.RecordWait), jx, jy, jz, Dir.Up, .5, 1, .5), "Szafa grająca: druga płyta nie wchodzi");
        log.All.Clear();
        Check(Block.Get(B.Jukebox).OnActivated(w, jx, jy, jz, p) && w.GetMeta(jx, jy, jz) == 0 && log.All.Any(s => s.n == "record:"), "Szafa grająca: PPM wyjmuje płytę i zatrzymuje muzykę");
        w.Tick();
        Check(w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.RecordCat), "Szafa grająca: płyta wypada");
        foreach (var e in w.Entities.OfType<ItemEntity>().ToList()) e.Kill();
        Item.ById[I.Record11].UseOnBlock(p, new ItemStack(I.Record11), jx, jy, jz, Dir.Up, .5, 1, .5);
        w.BreakBlock(jx, jy, jz, true, p);
        w.Tick();
        Check(w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.Record11) && w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == B.Jukebox),
            "Szafa grająca: zniszczenie wyrzuca płytę i blok");
        w.Listeners.Remove(log);

        // creeper zabity strzałą szkieleta
        foreach (var e in w.Entities.OfType<ItemEntity>().ToList()) e.Kill();
        w.Tick();
        var sk = new Skeleton(w); sk.SetPositionAndPrev(3, 4, 3); w.AddEntityNow(sk);
        var cr = new Creeper(w); cr.SetPositionAndPrev(5.5, 4, 5.5); w.AddEntityNow(cr);
        cr.Attack(DamageSource.Arrow(sk, sk), 100);
        w.Tick();
        Check(w.Entities.OfType<ItemEntity>().Any(e => Records.IsRecord(e.Stack.Id)), "Creeper zabity przez szkieleta wyrzuca płytę");
    }
}
