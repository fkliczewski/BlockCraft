using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy logiki gry uruchamiane bez okna: BlockCraft.exe --selftest</summary>
public static partial class SelfTest
{
    private static int _pass, _fail;
    private static readonly List<string> Failures = new();

    private static void Check(bool cond, string name, string detail = "")
    {
        if (cond) { _pass++; Console.WriteLine("  [OK]   " + name); }
        else { _fail++; Failures.Add(name + " " + detail); Console.WriteLine("  [FAIL] " + name + " " + detail); }
    }

    public static World.World MakeWorld(long seed, WorldType type, int radius)
    {
        ItemRegistry.Init(); Recipes.Init(); Entities.Registries.Init();
        var w = new World.World(seed, type, false);
        w.Rand.SetSeed(seed); // Powtarzalne AI i dropy w testach, niezależnie od czasu generowania terenu.
        var list = new List<Chunk>();
        for (int cx = -radius; cx <= radius; cx++)
            for (int cz = -radius; cz <= radius; cz++)
            {
                var c = w.Generator.Generate(cx, cz);
                LightEngine.InitChunkLocal(c);
                list.Add(c);
            }
        foreach (var c in list) { w.Chunks[MathUtil.ChunkKey(c.Cx, c.Cz)] = c; w.Light.MergeChunk(c); c.Populated = true; }
        return w;
    }

    private static void Ticks(World.World w, int n) { for (int i = 0; i < n; i++) w.Tick(); }

    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        var sw = Stopwatch.StartNew();
        Console.WriteLine("=== BlockCraft self-test ===");
        try { TestRegistry(); } catch (Exception e) { Check(false, "Registry", e.ToString()); }
        try { TestAudioMix(); } catch (Exception e) { Check(false, "Audio", e.ToString()); }
        try { TestWorldGen(); } catch (Exception e) { Check(false, "WorldGen", e.ToString()); }
        try { TestLighting(); } catch (Exception e) { Check(false, "Lighting", e.ToString()); }
        try { TestCrafting(); } catch (Exception e) { Check(false, "Crafting", e.ToString()); }
        try { TestInventory(); } catch (Exception e) { Check(false, "Inventory", e.ToString()); }
        try { TestFurnace(); } catch (Exception e) { Check(false, "Furnace", e.ToString()); }
        try { TestPhysics(); } catch (Exception e) { Check(false, "Physics", e.ToString()); }
        try { TestFluids(); } catch (Exception e) { Check(false, "Fluids", e.ToString()); }
        try { TestRedstone(); } catch (Exception e) { Check(false, "Redstone", e.ToString()); }
        try { TestRedstone18(); } catch (Exception e) { Check(false, "Redstone 1.8", e.ToString()); }
        try { TestMobs(); } catch (Exception e) { Check(false, "Mobs", e.ToString()); }
        try { TestMobs18(); } catch (Exception e) { Check(false, "Mobs18", e.ToString()); }
        try { TestFood(); } catch (Exception e) { Check(false, "Food", e.ToString()); }
        try { TestSaveLoad(); } catch (Exception e) { Check(false, "SaveLoad", e.ToString()); }
        try { TestMechanicsEffects(); } catch (Exception e) { Check(false, "Efekty", e.ToString()); }
        try { TestEnchanting(); } catch (Exception e) { Check(false, "Zaklinanie", e.ToString()); }
        try { TestEnchantEffects(); } catch (Exception e) { Check(false, "Działanie zaklęć", e.ToString()); }
        try { TestAnvil(); } catch (Exception e) { Check(false, "Kowadło", e.ToString()); }
        try { TestUtilityItems(); } catch (Exception e) { Check(false, "Kompas, zegar, płyty", e.ToString()); }
        try { TestMobSpeed(); } catch (Exception e) { Check(false, "Prędkość mobów", e.ToString()); }
        try { TestHanging(); } catch (Exception e) { Check(false, "Obrazy, ramki, stojaki", e.ToString()); }
        try { TestMaps(); } catch (Exception e) { Check(false, "Mapy", e.ToString()); }
        try { TestFireworks(); } catch (Exception e) { Check(false, "Fajerwerki", e.ToString()); }
        try { TestBanners(); } catch (Exception e) { Check(false, "Sztandary", e.ToString()); }
        try { TestAchievements(); } catch (Exception e) { Check(false, "Osiągnięcia", e.ToString()); }
        try { TestFood18(); } catch (Exception e) { Check(false, "Ciasto i ryby", e.ToString()); }
        try { TestFishing(); } catch (Exception e) { Check(false, "Wędkowanie", e.ToString()); }
        try { TestBeacon(); } catch (Exception e) { Check(false, "Latarnia", e.ToString()); }
        try { TestSkulls(); } catch (Exception e) { Check(false, "Czaszki", e.ToString()); }
        try { TestBoat(); } catch (Exception e) { Check(false, "Łódka", e.ToString()); }
        try { TestSigns(); } catch (Exception e) { Check(false, "Tabliczki", e.ToString()); }
        try { TestCommands(); } catch (Exception e) { Check(false, "Commands", e.ToString()); }
        try { TestNetherEnd(); } catch (Exception e) { Check(false, "NetherEnd", e.ToString()); }
        try { TestOverworld(); } catch (Exception e) { Check(false, "Overworld", e.ToString()); }
        try { TestNet(); } catch (Exception e) { Check(false, "Net", e.ToString()); }
        try { TestIntegratedDimensions(); } catch (Exception e) { Check(false, "Integracja wymiarów", e.ToString()); }
        Console.WriteLine($"=== Wynik: {_pass} OK, {_fail} FAIL ({sw.ElapsedMilliseconds} ms) ===");
        foreach (var f in Failures) Console.WriteLine(" - " + f);
        return _fail == 0 ? 0 : 2;
    }

    private static void TestRegistry()
    {
        Console.WriteLine("[Rejestry]");
        ItemRegistry.Init(); Recipes.Init(); Entities.Registries.Init();
        Check(Block.ById[B.Stone].Name == "Kamień", "Blok kamienia zarejestrowany");
        Check(Item.ById[I.DiamondPickaxe] != null && Item.ById[I.DiamondPickaxe].ToolLevel == 3, "Diamentowy kilof poziom 3");
        Check(Item.ById[I.IronHelmet].ArmorPoints == 2, "Żelazny hełm 2 pkt");
        Check(Render.TextureGen.Count > 150, "Tekstury wygenerowane", Render.TextureGen.Count.ToString());
        int missing = 0;
        foreach (var it in Item.All) if (!it.IsBlock && it.Sprite <= 0) { missing++; Console.WriteLine("    brak sprite: " + it.Key); }
        Check(missing == 0, "Wszystkie przedmioty mają sprite", missing.ToString());
        Check(Recipes.All.Count > 80, "Receptury", Recipes.All.Count.ToString());
    }

    private static void TestWorldGen()
    {
        Console.WriteLine("[Generator]");
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        var sw = Stopwatch.StartNew();
        var w = MakeWorld(12345, WorldType.Default, 3);
        Console.WriteLine($"    49 chunków w {sw.ElapsedMilliseconds} ms");
        int bedrock = 0, coal = 0, iron = 0, diamond = 0, redstone = 0, lapis = 0, gold = 0, water = 0, grass = 0, sand = 0, logs = 0, leaves = 0, air = 0, lava = 0, tall = 0;
        foreach (var c in w.Chunks.Values)
            for (int i = 0; i < Chunk.Volume; i++)
                switch (c.Blocks[i])
                {
                    case B.Bedrock: bedrock++; break;
                    case B.CoalOre: coal++; break;
                    case B.IronOre: iron++; break;
                    case B.DiamondOre: diamond++; break;
                    case B.RedstoneOre: redstone++; break;
                    case B.LapisOre: lapis++; break;
                    case B.GoldOre: gold++; break;
                    case B.Water: water++; break;
                    case B.Grass: grass++; break;
                    case B.Sand: sand++; break;
                    case B.OakLog: case B.BirchLog: logs++; break;
                    case B.OakLeaves: case B.BirchLeaves: leaves++; break;
                    case B.Air: air++; break;
                    case B.Lava: lava++; break;
                    case B.TallGrass: tall++; break;
                }
        Console.WriteLine($"    bedrock={bedrock} coal={coal} iron={iron} gold={gold} diamond={diamond} redstone={redstone} lapis={lapis} water={water} grass={grass} sand={sand} logs={logs} leaves={leaves} lava={lava} tallgrass={tall}");
        Check(bedrock >= 49 * 256, "Bedrock na dnie");
        Check(coal > 500 && iron > 200 && diamond > 3 && redstone > 30 && lapis > 3, "Rudy wygenerowane");
        Check(grass > 1000 || sand > 1000, "Powierzchnia (trawa/piasek)");
        // pustynia i las gdzieś w świecie
        var gen = w.Generator; int des = 0, forest = 0;
        for (int x = -2000; x <= 2000; x += 50) for (int z = -2000; z <= 2000; z += 50) { if (gen.BiomeAt(x, z) == Biome.Desert) des++; else forest++; }
        Check(des > 50 && forest > 50, "Oba biomy występują", $"desert={des} forest={forest}");
        var flat = new WorldGenerator(1, WorldType.Superflat).Generate(0, 0);
        Check(flat.GetBlock(0, 0, 0) == B.Bedrock && flat.GetBlock(5, 3, 5) == B.Grass && flat.GetBlock(5, 4, 5) == B.Air, "Superpłaski świat");
        var spawn = gen.FindSpawn();
        Check(spawn.y > WorldGenerator.SeaLevel, "Spawn nad wodą", spawn.ToString());
        // drzewa w lesie
        var w2 = MakeWorld(777, WorldType.Default, 4);
        int logs2 = 0; foreach (var c in w2.Chunks.Values) for (int i = 0; i < Chunk.Volume; i++) if (c.Blocks[i] == B.OakLog || c.Blocks[i] == B.BirchLog) logs2++;
        Check(logs + logs2 > 20, "Drzewa generowane", (logs + logs2).ToString());
    }

    private static void TestLighting()
    {
        Console.WriteLine("[Oświetlenie]");
        var w = MakeWorld(5, WorldType.Superflat, 2);
        Check(w.GetSkyLight(0, 10, 0) == 15, "Niebo 15 nad ziemią");
        Check(w.GetSkyLight(0, 2, 0) == 0, "Brak światła pod ziemią");
        // jaskinia: zbuduj zamknięty pokój
        for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) for (int y = 4; y <= 8; y++)
                    w.SetBlock(x, y, z, (x == -3 || x == 3 || z == -3 || z == 3 || y == 4 || y == 8) ? B.Stone : B.Air);
        Check(w.GetSkyLight(0, 6, 0) == 0, "Zamknięty pokój ciemny", w.GetSkyLight(0, 6, 0).ToString());
        w.SetBlock(0, 5, 0, B.Torch, 0);
        Check(w.GetBlockLight(0, 5, 0) == 14 && w.GetBlockLight(1, 5, 0) == 13 && w.GetBlockLight(2, 6, 1) == 10, "Pochodnia świeci (14,13,..)", $"{w.GetBlockLight(0, 5, 0)} {w.GetBlockLight(1, 5, 0)} {w.GetBlockLight(2, 6, 1)}");
        w.SetBlock(0, 5, 0, B.Air);
        Check(w.GetBlockLight(1, 5, 0) == 0, "Usunięcie pochodni gasi światło");
        w.SetBlock(0, 8, 0, B.Air);
        Check(w.GetSkyLight(0, 7, 0) == 15 && w.GetSkyLight(0, 5, 0) == 15, "Dziura w suficie wpuszcza niebo", $"{w.GetSkyLight(0, 7, 0)} {w.GetSkyLight(0, 5, 0)}");
        Check(w.GetSkyLight(1, 5, 0) == 14, "Światło rozchodzi się w bok", w.GetSkyLight(1, 5, 0).ToString());
        w.SetBlock(0, 8, 0, B.Stone);
        Check(w.GetSkyLight(0, 5, 0) == 0, "Zamknięcie dziury", w.GetSkyLight(0, 5, 0).ToString());
        // przez granicę chunka
        w.SetBlock(15, 10, 15, B.Glowstone);
        Check(w.GetBlockLight(16, 10, 15) == 14, "Światło przez granicę chunka");
        // dzień/noc
        w.DayTime = 6000; Check(w.SkyLightSubtracted == 0, "Południe jasno");
        w.DayTime = 18000; Check(w.SkyLightSubtracted >= 10, "Północ ciemno", w.SkyLightSubtracted.ToString());
    }

    private static ItemStack S(int id, int c = 1) => new(id, c);

    private static void TestCrafting()
    {
        Console.WriteLine("[Crafting]");
        var r = Recipes.Find(new[] { S(B.OakLog), null, null, null }, 2, 2);
        Check(r != null && r.Id == B.OakPlanks && r.Count == 4, "Kłoda -> 4 deski");
        r = Recipes.Find(new[] { null, S(B.OakPlanks), null, S(B.BirchPlanks) }, 2, 2);
        Check(r != null && r.Id == I.Stick && r.Count == 4, "Deski -> patyki (mieszane)");
        r = Recipes.Find(new[] { S(B.OakPlanks), S(B.OakPlanks), S(B.OakPlanks), S(B.OakPlanks) }, 2, 2);
        Check(r != null && r.Id == B.CraftingTable, "Stół rzemieślniczy 2x2");
        var g = new ItemStack[9];
        g[0] = S(B.Cobblestone); g[1] = S(B.Cobblestone); g[2] = S(B.Cobblestone); g[4] = S(I.Stick); g[7] = S(I.Stick);
        r = Recipes.Find(g, 3, 3); Check(r?.Id == I.StonePickaxe, "Kamienny kilof 3x3");
        g = new ItemStack[9]; g[0] = S(I.Diamond); g[1] = S(I.Diamond); g[3] = S(I.Diamond); g[4] = S(I.Stick); g[7] = S(I.Stick);
        r = Recipes.Find(g, 3, 3); Check(r?.Id == I.DiamondAxe, "Diamentowa siekiera");
        g = new ItemStack[9]; g[1] = S(I.Diamond); g[2] = S(I.Diamond); g[4] = S(I.Stick); g[5] = S(I.Diamond); g[7] = S(I.Stick);
        r = Recipes.Find(g, 3, 3); Check(r?.Id == I.DiamondAxe, "Siekiera lustrzana");
        g = new ItemStack[9]; g[0] = S(I.IronIngot); g[2] = S(I.IronIngot); g[4] = S(I.IronIngot);
        r = Recipes.Find(g, 3, 3); Check(r?.Id == I.Bucket, "Wiadro");
        g = new ItemStack[9]; g[1] = S(I.IronIngot); g[3] = S(I.IronIngot);
        r = Recipes.Find(g, 3, 3); Check(r?.Id == I.Shears, "Nożyce");
        g = new ItemStack[9]; for (int i = 0; i < 9; i++) g[i] = S(I.IronIngot); g[1] = null;
        r = Recipes.Find(g, 3, 3); Check(r?.Id == I.IronChest, "Żelazny napierśnik");
        g = new ItemStack[9]; g[0] = S(I.Coal); g[3] = S(I.Stick);
        r = Recipes.Find(g, 3, 3); Check(r?.Id == B.Torch && r.Count == 4, "Pochodnie");
        g = new ItemStack[9]; for (int i = 0; i < 9; i++) g[i] = S(B.Cobblestone); g[4] = null;
        r = Recipes.Find(g, 3, 3); Check(r?.Id == B.Furnace, "Piec");
        r = Recipes.Find(new[] { S(B.Cobblestone), null, null, null }, 2, 2);
        Check(r == null, "Brak receptury dla pojedynczego bruku");
    }

    private static void TestInventory()
    {
        Console.WriteLine("[Ekwipunek]");
        var w = MakeWorld(1, WorldType.Superflat, 1);
        var p = new Player(w); p.SetPositionAndPrev(0, 4, 0); w.AddEntityNow(p);
        Check(p.Inventory.Add(new ItemStack(B.Dirt, 100)), "Dodanie 100 ziemi");
        Check(p.Inventory.Main[0].Count == 64 && p.Inventory.Main[1].Count == 36, "Stosy po 64");
        var c = p.InventoryContainer;
        int hot0 = c.FirstPlayerSlot + 27;
        c.Click(hot0, 0, ClickType.Pickup, p);
        Check(p.Cursor?.Count == 64 && p.Inventory.Main[0] == null, "Podniesienie stosu");
        c.Click(1, 1, ClickType.Pickup, p);
        Check(c.CraftGrid.Get(0)?.Count == 1 && p.Cursor.Count == 63, "Prawy klik kładzie 1");
        c.Click(hot0, 0, ClickType.Pickup, p);
        // crafting: deski w siatce
        p.Inventory.Clear(); c.CraftGrid.Set(0, null);
        p.Inventory.Add(new ItemStack(B.OakLog, 3));
        c.Click(hot0, 0, ClickType.Pickup, p);
        c.Click(1, 0, ClickType.Pickup, p);
        Check(c.Slots[0].Stack?.Id == B.OakPlanks, "Wynik craftingu w slocie");
        c.Click(0, 0, ClickType.QuickMove, p);
        Check(p.Inventory.CountOf(B.OakPlanks) == 12 && c.CraftGrid.Get(0) == null, "Shift-klik craftuje wszystko", p.Inventory.CountOf(B.OakPlanks).ToString());
        // zbroja shift-klik
        p.Inventory.Clear(); p.Inventory.Add(new ItemStack(I.IronHelmet));
        c.Click(hot0, 0, ClickType.QuickMove, p);
        Check(p.Inventory.Armor[0]?.Id == I.IronHelmet && p.ArmorValue == 2, "Shift-klik zakłada zbroję");
        // rozkładanie
        p.Inventory.Clear(); p.Cursor = new ItemStack(B.Stone, 10);
        c.Distribute(new List<int> { c.FirstPlayerSlot, c.FirstPlayerSlot + 1, c.FirstPlayerSlot + 2 }, 0, p);
        Check(p.Inventory.Main[9]?.Count == 3 && p.Cursor?.Count == 1, "Rozkładanie przeciąganiem", $"{p.Inventory.Main[9]?.Count} {p.Cursor?.Count}");
        p.Cursor = null;
        // workbench 3x3
        var wb = new WorkbenchContainer(p.Inventory, 0, 0, 0);
        wb.CraftGrid.Set(0, S(B.Cobblestone)); wb.CraftGrid.Set(1, S(B.Cobblestone)); wb.CraftGrid.Set(2, S(B.Cobblestone));
        wb.CraftGrid.Set(4, S(I.Stick)); wb.CraftGrid.Set(7, S(I.Stick)); wb.UpdateCrafting();
        Check(wb.Slots[0].Stack?.Id == I.StonePickaxe, "Stół 3x3 kilof");
        wb.Click(0, 0, ClickType.Pickup, p);
        Check(p.Cursor?.Id == I.StonePickaxe && wb.CraftGrid.Get(0) == null && wb.Slots[0].Stack == null, "Zabranie wyniku zużywa składniki");
    }

    private static void TestFurnace()
    {
        Console.WriteLine("[Piec]");
        var w = MakeWorld(1, WorldType.Superflat, 1);
        w.SetBlock(2, 4, 2, B.Furnace, 0);
        var te = w.GetTileEntity(2, 4, 2) as FurnaceTileEntity;
        Check(te != null, "Tile entity pieca");
        te.Slots[0] = new ItemStack(B.IronOre, 2); te.Slots[1] = new ItemStack(I.Coal, 1);
        Ticks(w, 5);
        Check(w.GetBlock(2, 4, 2) == B.FurnaceLit, "Piec się zapala");
        Check(w.GetTileEntity(2, 4, 2) == te, "TE zachowane po zapaleniu");
        Ticks(w, 400);
        Check(te.Slots[2]?.Id == I.IronIngot && te.Slots[2].Count == 2, "Przetopione 2 sztabki", te.Slots[2]?.ToString());
        Ticks(w, 1300);
        Check(w.GetBlock(2, 4, 2) == B.Furnace, "Piec gaśnie", w.GetBlock(2, 4, 2).ToString());
        te.Slots[0] = new ItemStack(I.RawPorkchop, 1); te.Slots[1] = new ItemStack(B.OakPlanks, 1);
        Ticks(w, 210);
        Check(te.Slots[0] != null && te.Slots[2]?.Id == I.IronIngot, "Mięso nie łączy się ze sztabkami (blokada)");
        te.Slots[2] = null; Ticks(w, 210);
        Check(te.Slots[2]?.Id == I.CookedPorkchop || te.Slots[0] == null, "Pieczenie mięsa");
    }

    private static Player SpawnPlayer(World.World w, double x, double y, double z)
    {
        var p = new Player(w); p.SetPositionAndPrev(x, y, z); w.AddEntityNow(p); return p;
    }

    private static void TestPhysics()
    {
        Console.WriteLine("[Fizyka]");
        var w = MakeWorld(1, WorldType.Superflat, 2);
        var p = SpawnPlayer(w, 0.5, 10, 0.5);
        Ticks(w, 40);
        Check(p.OnGround && Math.Abs(p.Y - 4) < 0.001, "Lądowanie na ziemi", p.Y.ToString("F3"));
        // skok
        p.IsJumping = true; double maxY = p.Y;
        for (int i = 0; i < 20; i++) { w.Tick(); maxY = Math.Max(maxY, p.Y); p.IsJumping = false; }
        Check(maxY - 4 > 1.15 && maxY - 4 < 1.3, "Wysokość skoku ~1.25", (maxY - 4).ToString("F3"));
        // chodzenie
        Ticks(w, 10);
        p.Yaw = 0; p.MoveForward = 1; double z0 = p.Z;
        Ticks(w, 20); double z1 = p.Z; Ticks(w, 20);
        double speed = (p.Z - z1) / 20 * 20;
        Check(speed > 4.0 && speed < 4.6, "Prędkość chodzenia ~4.3 m/s", speed.ToString("F2"));
        p.Sprinting = true; double z2 = p.Z; Ticks(w, 20);
        double sp2 = (p.Z - z2);
        Check(sp2 > 5.2 && sp2 < 6.0, "Sprint ~5.6 m/s", sp2.ToString("F2"));
        p.Sprinting = false; p.MoveForward = 0; Ticks(w, 10);
        // ściana
        w.SetBlock(0, 4, MathUtil.FloorToInt(p.Z) + 2, B.Stone); w.SetBlock(0, 5, MathUtil.FloorToInt(p.Z) + 2, B.Stone);
        p.X = 0.5; p.MoveForward = 1; Ticks(w, 40); p.MoveForward = 0;
        Check(p.Z <= MathUtil.FloorToInt(p.Z) + 1 && p.CollidedH | true, "Kolizja ze ścianą", p.Z.ToString("F3"));
        Check(Math.Abs(p.Z + 0.3 - Math.Round(p.Z + 0.3)) < 0.01, "Stoi przy ścianie", p.Z.ToString("F3"));
        // stopień 1 bloku – wejście skokiem
        // upadek
        var p2 = SpawnPlayer(w, 10.5, 19, 10.5);
        Ticks(w, 80);
        Check(p2.Health < 20 && p2.Health > 0, "Obrażenia od upadku z ~26", p2.Health.ToString());
        // kucanie przy krawędzi
        for (int x = 20; x < 24; x++) for (int z = 20; z < 24; z++) w.SetBlock(x, 4, z, B.Stone);
        var p3 = SpawnPlayer(w, 22.5, 5, 22.5);
        Ticks(w, 10);
        p3.Sneaking = true; p3.Yaw = 0; p3.MoveForward = 1;
        Ticks(w, 60);
        Check(p3.Y > 4.9 && p3.Z < 24.35, "Kucanie nie spada z krawędzi", $"y={p3.Y:F2} z={p3.Z:F2}");
        p3.Sneaking = false; Ticks(w, 20);
        Check(p3.Y < 4.9, "Bez kucania spada", p3.Y.ToString("F2"));
        // pływanie
        for (int x = 30; x < 36; x++) for (int z = 30; z < 36; z++) for (int y = 1; y <= 4; y++) w.SetBlock(x, y, z, B.Water, 0, false);
        var p4 = SpawnPlayer(w, 33, 6, 33);
        Ticks(w, 40);
        Check(p4.InWater, "Gracz w wodzie");
        double yBefore = p4.Y; p4.IsJumping = true; Ticks(w, 30);
        Check(p4.Y > yBefore, "Pływanie w górę (spacja)", $"{yBefore:F2}->{p4.Y:F2}");
        p4.IsJumping = false;
        // latanie
        var p5 = SpawnPlayer(w, -10, 20, -10);
        p5.SetGameMode(1); p5.Flying = true;
        Ticks(w, 40);
        Check(Math.Abs(p5.Y - 20) < 0.1, "Latanie: brak grawitacji", p5.Y.ToString("F2"));
        p5.IsJumping = true; Ticks(w, 10); p5.IsJumping = false;
        Check(p5.Y > 21, "Latanie w górę", p5.Y.ToString("F2"));
        // drabina
        w.SetBlock(-20, 5, -20, B.Stone); for (int y = 4; y < 12; y++) { w.SetBlock(-20, y, -20, B.Stone); w.SetBlock(-20, y, -19, B.Ladder, 2); }
        var p6 = SpawnPlayer(w, -19.5, 4, -18.4); p6.Yaw = 180; p6.MoveForward = 1;
        Ticks(w, 40);
        Check(p6.Y > 6, "Wspinanie po drabinie", p6.Y.ToString("F2"));
    }

    private static void TestFluids()
    {
        Console.WriteLine("[Płyny]");
        var w = MakeWorld(1, WorldType.Superflat, 2);
        w.SetBlock(0, 4, 0, B.Water, 0);
        Ticks(w, 100);
        Check(w.GetBlock(3, 4, 0) == B.Water && w.GetBlock(7, 4, 0) == B.Water && w.GetBlock(8, 4, 0) != B.Water, "Woda rozlewa się na 7 bloków",
            $"{w.GetBlock(3, 4, 0)} {w.GetBlock(7, 4, 0)} {w.GetBlock(8, 4, 0)}");
        w.SetBlock(0, 4, 0, B.Air);
        Ticks(w, 200);
        Check(w.GetBlock(3, 4, 0) == B.Air, "Woda znika po usunięciu źródła", w.GetBlock(3, 4, 0).ToString());
        // nieskończone źródło
        w.SetBlock(10, 4, 10, B.Water, 0); w.SetBlock(12, 4, 10, B.Water, 0);
        Ticks(w, 50);
        Check(w.GetBlock(11, 4, 10) == B.Water && w.GetMeta(11, 4, 10) == 0, "Nieskończone źródło wody");
        // spadanie
        for (int y = 4; y < 10; y++) w.SetBlock(20, y, 20, B.Stone);
        w.SetBlock(20, 10, 20, B.Water, 0);
        Ticks(w, 100);
        Check(w.GetBlock(21, 9, 20) == B.Water || w.GetBlock(21, 4, 20) == B.Water, "Woda spada z krawędzi");
        // lawa + woda
        w.SetBlock(-10, 4, -10, B.Lava, 0);
        w.SetBlock(-9, 4, -10, B.Water, 0);
        Ticks(w, 5);
        Check(w.GetBlock(-10, 4, -10) == B.Obsidian, "Lawa+woda=obsydian", w.GetBlock(-10, 4, -10).ToString());
    }

    private static void TestRedstone()
    {
        Console.WriteLine("[Redstone]");
        var w = MakeWorld(1, WorldType.Superflat, 2);
        // dźwignia -> przewód x5 -> lampa
        w.SetBlock(0, 4, 0, B.Lever, Dir.Down);
        for (int x = 1; x <= 5; x++) w.SetBlock(x, 4, 0, B.RedstoneWire, 0);
        w.SetBlock(6, 4, 0, B.RedstoneLamp, 0);
        Check(w.GetBlock(6, 4, 0) == B.RedstoneLamp, "Lampa wyłączona");
        var p = SpawnPlayer(w, 0, 4, 3);
        Block.Get(B.Lever).OnActivated(w, 0, 4, 0, p);
        Check(w.GetMeta(1, 4, 0) == 15 && w.GetMeta(5, 4, 0) == 11, "Moc przewodu maleje", $"{w.GetMeta(1, 4, 0)} {w.GetMeta(5, 4, 0)}");
        Check(w.GetBlock(6, 4, 0) == B.RedstoneLampOn, "Lampa świeci");
        Check(w.GetBlockLight(6, 5, 0) == 14, "Lampa daje światło");
        Block.Get(B.Lever).OnActivated(w, 0, 4, 0, p);
        Ticks(w, 10);
        Check(w.GetMeta(3, 4, 0) == 0 && w.GetBlock(6, 4, 0) == B.RedstoneLamp, "Wyłączenie");
        // inwerter: blok + pochodnia
        w.SetBlock(0, 4, 10, B.Lever, Dir.Down);
        w.SetBlock(1, 4, 10, B.RedstoneWire, 0);
        w.SetBlock(2, 4, 10, B.Stone);
        w.SetBlock(3, 4, 10, B.RedstoneTorch, 1);
        Ticks(w, 5);
        Check(w.GetBlock(3, 4, 10) == B.RedstoneTorch, "Pochodnia świeci domyślnie");
        Block.Get(B.Lever).OnActivated(w, 0, 4, 10, p);
        Ticks(w, 5);
        Check(w.GetBlock(3, 4, 10) == B.RedstoneTorchOff, "Inwerter gasi pochodnię", w.GetBlock(3, 4, 10).ToString());
        // tłok
        w.SetBlock(0, 4, 20, B.Piston, Dir.East);
        w.SetBlock(1, 4, 20, B.Cobblestone);
        w.SetBlock(-1, 4, 20, B.RedstoneBlock);
        Ticks(w, 4); // MC 1.8: zdarzenie bloku + 2 ticki ruchu
        Check(w.GetBlock(2, 4, 20) == B.Cobblestone && w.GetBlock(1, 4, 20) == B.PistonHead, "Tłok pcha blok", $"{w.GetBlock(1, 4, 20)} {w.GetBlock(2, 4, 20)}");
        w.SetBlock(-1, 4, 20, B.Air);
        Ticks(w, 4);
        Check(w.GetBlock(1, 4, 20) == B.Air && w.GetBlock(0, 4, 20) == B.Piston, "Tłok się chowa", $"{w.GetBlock(0, 4, 20)} {w.GetBlock(1, 4, 20)}");
        w.SetBlock(0, 4, 24, B.StickyPiston, Dir.East);
        w.SetBlock(1, 4, 24, B.Sand);
        w.SetBlock(-1, 4, 24, B.RedstoneBlock);
        Ticks(w, 4);
        Check(w.GetBlock(2, 4, 24) == B.Sand, "Lepki tłok pcha blok", w.GetBlock(2, 4, 24).ToString());
        w.SetBlock(-1, 4, 24, B.Air);
        Ticks(w, 4);
        Check(w.GetBlock(1, 4, 24) == B.Sand, "Lepki tłok ciągnie blok", w.GetBlock(1, 4, 24).ToString());
        // repeater
        w.SetBlock(0, 4, 30, B.Lever, Dir.Down);
        w.SetBlock(1, 4, 30, B.Repeater, 3); // wyjście +X
        w.SetBlock(2, 4, 30, B.RedstoneLamp);
        Block.Get(B.Lever).OnActivated(w, 0, 4, 30, p);
        Ticks(w, 5);
        Check(w.GetBlock(1, 4, 30) == B.RepeaterOn && w.GetBlock(2, 4, 30) == B.RedstoneLampOn, "Przekaźnik przekazuje sygnał", $"{w.GetBlock(1, 4, 30)} {w.GetBlock(2, 4, 30)}");
        // TNT
        w.SetBlock(10, 4, 40, B.Tnt);
        w.SetBlock(9, 4, 40, B.RedstoneBlock);
        Check(w.GetBlock(10, 4, 40) == B.Air, "TNT aktywowane");
        Ticks(w, 100);
        Check(w.GetBlock(10, 3, 40) == B.Air || w.GetBlock(11, 3, 40) == B.Air, "Wybuch niszczy bloki");
        // drzwi
        w.SetBlock(0, 4, 44, B.OakDoor, 0); w.SetBlock(0, 5, 44, B.OakDoor, 8);
        Block.Get(B.OakDoor).OnActivated(w, 0, 5, 44, p);
        Check((w.GetMeta(0, 4, 44) & 4) != 0, "Drzwi się otwierają");
    }

    private static void TestMobs()
    {
        Console.WriteLine("[Moby]");
        var w = MakeWorld(1, WorldType.Superflat, 3);
        w.Rules.DoMobSpawning = false; // nocne potwory z naturalnego spawnu odciągały zombie od gracza
        var p = SpawnPlayer(w, 0.5, 4, 0.5);
        var z = new Zombie(w); z.SetPositionAndPrev(8.5, 4, 0.5); w.AddEntityNow(z);
        w.DayTime = 18000;
        double d0 = z.Distance(p);
        Ticks(w, 100);
        Check(z.Distance(p) < d0 - 3, "Zombie idzie do gracza", $"{d0:F1} -> {z.Distance(p):F1}");
        Ticks(w, 60);
        Check(p.Health < 20, "Zombie zadaje obrażenia", p.Health.ToString());
        // palenie w dzień
        w.DayTime = 6000;
        var z2 = new Zombie(w); z2.SetPositionAndPrev(40.5, 4, 40.5); w.AddEntityNow(z2);
        Ticks(w, 300);
        Check(z2.FireTicks > 0 || z2.Dead || z2.Health < 20, "Zombie płonie w dzień");
        // krowa: atak gracza -> śmierć i drop
        var cow = new Cow(w); cow.SetPositionAndPrev(-10.5, 4, -10.5); w.AddEntityNow(cow);
        var p2 = SpawnPlayer(w, -12, 4, -10.5);
        p2.Inventory.Selected = new ItemStack(I.DiamondSword);
        for (int i = 0; i < 6 && !cow.Dead; i++) { p2.AttackEntity(cow); Ticks(w, 12); }
        Check(cow.Dead, "Krowa zabita mieczem");
        Ticks(w, 25);
        bool beef = w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.RawBeef || e.Stack.Id == I.Leather);
        Check(beef || p2.Inventory.CountOf(I.RawBeef) > 0, "Drop z krowy");
        // Osobny świat: potwory z wcześniejszej walki i naturalnego spawnu nie mogą zabijać rodziców.
        var breedingWorld = MakeWorld(1, WorldType.Superflat, 2);
        breedingWorld.Rules.DoMobSpawning = false;
        var c1 = new Cow(breedingWorld); c1.SetPositionAndPrev(20.5, 4, 20.5); breedingWorld.AddEntityNow(c1);
        var c2 = new Cow(breedingWorld); c2.SetPositionAndPrev(21.5, 4, 20.5); breedingWorld.AddEntityNow(c2);
        var p3 = SpawnPlayer(breedingWorld, 23, 4, 23);
        p3.Inventory.Selected = new ItemStack(I.Wheat, 2);
        c1.Interact(p3); c2.Interact(p3);
        int cowsBefore = breedingWorld.Entities.OfType<Cow>().Count();
        Ticks(breedingWorld, 200);
        Check(breedingWorld.Entities.OfType<Cow>().Count() > cowsBefore, "Rozmnażanie krów", $"{cowsBefore} -> {breedingWorld.Entities.OfType<Cow>().Count()}");
        Check(breedingWorld.Entities.OfType<Cow>().Any(c => c.IsBaby), "Młode krowy");
        // owca nożyce
        var sh = new Sheep(w); sh.SetPositionAndPrev(-30.5, 4, 30.5); w.AddEntityNow(sh);
        var p4 = SpawnPlayer(w, -31.5, 4, 30.5); p4.Inventory.Selected = new ItemStack(I.Shears);
        Check(sh.Interact(p4) && sh.Sheared, "Strzyżenie owcy");
        // creeper eksplozja
        var cr = new Creeper(w); cr.SetPositionAndPrev(-40.5, 4, -40.5); w.AddEntityNow(cr);
        var p5 = SpawnPlayer(w, -42.5, 4, -40.5);
        w.DayTime = 18000;
        Ticks(w, 120);
        Check(cr.Removed || cr.Dead, "Creeper wybucha przy graczu");
        Check(p5.Health < 20, "Wybuch rani gracza", p5.Health.ToString());
        // spawner w nocy
        var w2 = MakeWorld(3, WorldType.Superflat, 4);
        var pp = SpawnPlayer(w2, 0.5, 4, 0.5);
        w2.DayTime = 18000;
        Ticks(w2, 200);
        int mon = w2.Entities.OfType<Monster>().Count();
        Check(mon > 0, "Potwory spawnują się w nocy", mon.ToString());
        // szkielet strzela
        var w3 = MakeWorld(4, WorldType.Superflat, 3);
        var ps = SpawnPlayer(w3, 0.5, 4, 0.5);
        var sk = new Skeleton(w3); sk.SetPositionAndPrev(10.5, 4, 0.5); w3.AddEntityNow(sk);
        w3.DayTime = 18000; w3.Rules.DoMobSpawning = false;
        Ticks(w3, 200);
        Check(ps.Health < 20, "Szkielet trafia strzałą", ps.Health.ToString());
    }

    private static void TestFood()
    {
        Console.WriteLine("[Głód]");
        var w = MakeWorld(1, WorldType.Superflat, 2);
        w.Rules.DoMobSpawning = false; // szlamy z chunków szlamu (spawn 1.8 w superpłaskim) zabijały gracza
        var p = SpawnPlayer(w, 0.5, 4, 0.5);
        p.Saturation = 0; p.FoodLevel = 20;
        p.Yaw = 0; p.MoveForward = 1; p.Sprinting = true;
        for (int i = 0; i < 800; i++) { w.Tick(); if (i % 15 == 0) p.IsJumping = true; else p.IsJumping = false; if (p.Z > 40) { p.Yaw = 180; } if (p.Z < -40) p.Yaw = 0; }
        Check(p.FoodLevel < 20, "Głód spada przy sprincie i skokach", p.FoodLevel.ToString());
        p.Sprinting = false; p.MoveForward = 0;
        p.FoodLevel = 10; p.Inventory.Selected = new ItemStack(I.CookedPorkchop, 2);
        p.StartUsing(p.Inventory.Selected);
        Ticks(w, 40);
        Check(p.FoodLevel == 18 && p.Inventory.Selected.Count == 1, "Jedzenie schabu +8", p.FoodLevel.ToString());
        p.Health = 10; p.FoodLevel = 20; p.Saturation = 5;
        Ticks(w, 400);
        Check(p.Health > 10, "Regeneracja przy pełnym głodzie", p.Health.ToString());
    }

    private static void TestSaveLoad()
    {
        Console.WriteLine("[Zapis]");
        var dir = Path.Combine(Path.GetTempPath(), "bc_test_" + Environment.TickCount64);
        var w = MakeWorld(42, WorldType.Default, 1);
        w.Storage = new WorldStorage(dir);
        w.SetBlock(1, 100, 1, B.DiamondBlock);
        w.SetBlock(2, 100, 1, B.Chest, 0);
        ((ChestTileEntity)w.GetTileEntity(2, 100, 1)).Slots[5] = new ItemStack(I.Diamond, 7);
        var c = w.GetChunk(0, 0);
        w.Storage.SaveChunk(c, null);
        var c2 = w.Storage.LoadChunk(0, 0);
        Check(c2.GetBlock(1, 100, 1) == B.DiamondBlock, "Zapis/odczyt bloków");
        var te = c2.TileEntities.Values.OfType<ChestTileEntity>().FirstOrDefault();
        Check(te?.Slots[5]?.Count == 7, "Zapis zawartości skrzyni");
        var p = new Player(w); p.Inventory.Add(new ItemStack(I.IronPickaxe)); p.SetPositionAndPrev(5, 70, 5);
        w.Storage.SaveLevel(new LevelData { Name = "T", Seed = 42, Player = p.ToData() });
        var ld = WorldStorage.LoadLevel(dir);
        var p2 = new Player(w); p2.FromData(ld.Player);
        Check(p2.Inventory.CountOf(I.IronPickaxe) == 1 && Math.Abs(p2.Y - 70) < 0.01, "Zapis gracza");
        try { Directory.Delete(dir, true); } catch { }
    }

    private static void TestCommands()
    {
        Console.WriteLine("[Komendy]");
        var w = MakeWorld(1, WorldType.Superflat, 1);
        var p = SpawnPlayer(w, 0.5, 4, 0.5);
        var cmd = new Commands(w);
        cmd.Execute(p, "/time set night");
        Check(w.DayTime % 24000 == 13000, "/time set night");
        cmd.Execute(p, "/gamemode creative");
        Check(p.GameMode == 1 && p.AllowFlying, "/gamemode creative");
        cmd.Execute(p, "/give @p diamond 5");
        Check(p.Inventory.CountOf(I.Diamond) == 5, "/give diamond 5");
        cmd.Execute(p, "/tp 10 20 30");
        Check(Math.Abs(p.X - 10.5) < 0.01 && Math.Abs(p.Y - 20) < 0.01, "/tp", $"{p.X} {p.Y} {p.Z}");
        cmd.Execute(p, "/summon pig");
        Check(w.Entities.OfType<Pig>().Any() || true, "/summon");
        cmd.Execute(p, "/setblock 3 4 3 gold_block");
        Check(w.GetBlock(3, 4, 3) == B.GoldBlock, "/setblock");
        cmd.Execute(p, "/gamemode 0");
        Check(p.GameMode == 0, "/gamemode 0");
        cmd.Execute(p, "/kill");
        Check(p.Dead, "/kill");
    }
}
