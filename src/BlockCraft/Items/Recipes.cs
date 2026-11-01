using BlockCraft.World;

namespace BlockCraft.Items;

public sealed class Recipe
{
    public int Width, Height;
    public int[] Pattern;      // id lub 0 (pusto); -1 = dowolne deski, -2 = dowolne drewno
    public int[] PatternMeta;  // -1 = dowolne
    public bool Shapeless;
    public List<(int id, int meta)> Ingredients;
    public ItemStack Result;
}

public static partial class Recipes
{
    public static readonly List<Recipe> All = new();
    public const int AnyPlanks = -1, AnyLog = -2, AnyCoal = -3, AnyWoodSlab = -4;

    private static bool Matches(int want, int wantMeta, ItemStack s)
    {
        if (want == 0) return s == null;
        if (s == null) return false;
        if (want == AnyPlanks) return s.Id is B.OakPlanks or B.BirchPlanks or B.SprucePlanks or B.JunglePlanks or B.AcaciaPlanks or B.DarkOakPlanks;
        if (want == AnyLog) return s.Id is B.OakLog or B.BirchLog or B.SpruceLog or B.JungleLog or B.AcaciaLog or B.DarkOakLog;
        if (want == AnyWoodSlab) return s.Id is B.OakSlab or B.BirchSlab or B.SpruceSlab or B.JungleSlab or B.AcaciaSlab or B.DarkOakSlab;
        if (want == AnyCoal) return s.Id == I.Coal || s.Id == I.Charcoal;
        return s.Id == want && (wantMeta < 0 || s.Damage == wantMeta);
    }

    public static void Shaped(ItemStack result, params object[] def)
    {
        var rows = new List<string>();
        int i = 0;
        while (i < def.Length && def[i] is string s) { rows.Add(s); i++; }
        var key = new Dictionary<char, (int, int)>();
        for (; i + 1 < def.Length; i += 2)
        {
            char c = (char)def[i];
            if (def[i + 1] is ValueTuple<int, int> t) key[c] = t;
            else key[c] = (Convert.ToInt32(def[i + 1]), -1);
        }
        int w = rows.Max(r => r.Length), h = rows.Count;
        var r = new Recipe { Width = w, Height = h, Pattern = new int[w * h], PatternMeta = new int[w * h], Result = result };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = x < rows[y].Length ? rows[y][x] : ' ';
                if (c == ' ') { r.Pattern[y * w + x] = 0; r.PatternMeta[y * w + x] = -1; }
                else { var (id, m) = key[c]; r.Pattern[y * w + x] = id; r.PatternMeta[y * w + x] = m; }
            }
        All.Add(r);
    }

    public static void Shapeless(ItemStack result, params int[] ids)
    {
        All.Add(new Recipe { Shapeless = true, Ingredients = ids.Select(i => (i, -1)).ToList(), Result = result });
    }

    /// <summary>Znajdź wynik dla siatki (grid w x h).</summary>
    public static ItemStack Find(ItemStack[] grid, int gw, int gh)
    {
        // obszar zajęty
        int minX = gw, minY = gh, maxX = -1, maxY = -1, count = 0;
        for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
                if (grid[y * gw + x] != null) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); count++; }
        if (count == 0) return null;
        int w = maxX - minX + 1, h = maxY - minY + 1;
        // naprawa narzędzi: dwa takie same uszkodzone narzędzia
        if (count == 2)
        {
            ItemStack a = null, b = null;
            foreach (var s in grid) if (s != null) { if (a == null) a = s; else b = s; }
            if (a.Id == b.Id && a.Item?.MaxDamage > 0 && a.Count == 1 && b.Count == 1)
            {
                int max = a.Item.MaxDamage;
                int dur = (max - a.Damage) + (max - b.Damage) + max * 5 / 100;
                return new ItemStack(a.Id, 1, Math.Max(0, max - dur));
            }
        }
        foreach (var special in Special) { var sr = special(grid, gw, gh); if (sr != null) return sr; }
        foreach (var r in All)
        {
            if (r.Shapeless)
            {
                if (r.Ingredients.Count != count) continue;
                var used = new bool[grid.Length];
                bool ok = true;
                foreach (var (id, m) in r.Ingredients)
                {
                    bool found = false;
                    for (int i = 0; i < grid.Length; i++)
                        if (!used[i] && grid[i] != null && Matches(id, m, grid[i])) { used[i] = true; found = true; break; }
                    if (!found) { ok = false; break; }
                }
                if (ok) return r.Result.Copy();
                continue;
            }
            if (r.Width != w || r.Height != h) continue;
            for (int mirror = 0; mirror < 2; mirror++)
            {
                bool ok = true;
                for (int y = 0; y < h && ok; y++)
                    for (int x = 0; x < w && ok; x++)
                    {
                        int px = mirror == 1 ? w - 1 - x : x;
                        if (!Matches(r.Pattern[y * w + px], r.PatternMeta[y * w + px], grid[(minY + y) * gw + minX + x])) ok = false;
                    }
                if (ok) return r.Result.Copy();
            }
        }
        return null;
    }

    private static ItemStack S(int id, int c = 1, int m = 0) => new(id, c, m);

    public static void Init()
    {
        if (All.Count > 0) return;
        // drewno
        Shaped(S(B.OakPlanks, 4), "#", '#', B.OakLog);
        Shaped(S(B.BirchPlanks, 4), "#", '#', B.BirchLog);
        Shaped(S(I.Stick, 4), "#", "#", '#', AnyPlanks);
        Shaped(S(B.CraftingTable), "##", "##", '#', AnyPlanks);
        Shaped(S(B.Furnace), "###", "# #", "###", '#', B.Cobblestone);
        Shaped(S(B.Chest), "###", "# #", "###", '#', AnyPlanks);
        Shaped(S(B.Torch, 4), "C", "S", 'C', AnyCoal, 'S', I.Stick);
        Shaped(S(B.Ladder, 3), "S S", "SSS", "S S", 'S', I.Stick);
        Shaped(S(B.Fence, 3), "PSP", "PSP", 'P', B.OakPlanks, 'S', I.Stick);
        Shaped(S(B.FenceGate), "SPS", "SPS", 'P', B.OakPlanks, 'S', I.Stick);
        Shaped(S(I.Door, 3), "PP", "PP", "PP", 'P', B.OakPlanks);
        Shaped(S(I.Bed), "WWW", "PPP", 'W', B.Wool, 'P', AnyPlanks);
        Shaped(S(I.Bowl, 4), "P P", " P ", 'P', AnyPlanks);
        Shaped(S(B.OakSlab, 6), "PPP", 'P', B.OakPlanks);
        Shaped(S(B.OakStairs, 4), "P  ", "PP ", "PPP", 'P', B.OakPlanks);
        Shaped(S(B.Bookshelf), "PPP", "BBB", "PPP", 'P', AnyPlanks, 'B', I.Book);

        // narzędzia
        int[] mats = { AnyPlanks, B.Cobblestone, I.IronIngot, I.GoldIngot, I.Diamond };
        int[] first = { I.WoodPickaxe, I.StonePickaxe, I.IronPickaxe, I.GoldPickaxe, I.DiamondPickaxe };
        for (int m = 0; m < 5; m++)
        {
            int X = mats[m], f = first[m];
            Shaped(S(f + 0), "XXX", " S ", " S ", 'X', X, 'S', I.Stick);
            Shaped(S(f + 1), "XX", "XS", " S", 'X', X, 'S', I.Stick);
            Shaped(S(f + 2), "X", "S", "S", 'X', X, 'S', I.Stick);
            Shaped(S(f + 3), "X", "X", "S", 'X', X, 'S', I.Stick);
            Shaped(S(f + 4), "XX", " S", " S", 'X', X, 'S', I.Stick);
        }
        Shaped(S(I.Shears), " #", "# ", '#', I.IronIngot);
        Shaped(S(I.Bucket), "# #", " # ", '#', I.IronIngot);
        Shapeless(S(I.FlintAndSteel), I.IronIngot, I.Flint);
        Shaped(S(I.Bow), " #S", "# S", " #S", '#', I.Stick, 'S', I.String);
        Shaped(S(I.Arrow, 4), "F", "#", "E", 'F', I.Flint, '#', I.Stick, 'E', I.Feather);

        // zbroje
        int[] amats = { I.Leather, I.IronIngot, I.GoldIngot, I.Diamond };
        int[] afirst = { I.LeatherHelmet, I.IronHelmet, I.GoldHelmet, I.DiamondHelmet };
        for (int m = 0; m < 4; m++)
        {
            int X = amats[m], f = afirst[m];
            Shaped(S(f + 0), "XXX", "X X", 'X', X);
            Shaped(S(f + 1), "X X", "XXX", "XXX", 'X', X);
            Shaped(S(f + 2), "XXX", "X X", "X X", 'X', X);
            Shaped(S(f + 3), "X X", "X X", 'X', X);
        }

        // jedzenie
        Shaped(S(I.Bread), "WWW", 'W', I.Wheat);
        Shaped(S(I.Cookie, 8), "WCW", 'W', I.Wheat, 'C', (I.Dye, 3));
        Shaped(S(I.GoldenApple), "GGG", "GAG", "GGG", 'G', I.GoldIngot, 'A', I.Apple);
        Shapeless(S(I.Sugar), I.SugarCane);
        Shaped(S(I.Paper, 3), "###", '#', I.SugarCane);
        Shapeless(S(I.Book), I.Paper, I.Paper, I.Paper, I.Leather);

        // bloki surowców
        void Compress(int item, int block, int itemCount = 9)
        {
            Shaped(S(block), "###", "###", "###", '#', item);
            Shaped(S(item, itemCount), "#", '#', block);
        }
        Compress(I.IronIngot, B.IronBlock); Compress(I.GoldIngot, B.GoldBlock); Compress(I.Diamond, B.DiamondBlock);
        Compress(I.Coal, B.CoalBlock); Compress(I.Redstone, B.RedstoneBlock); Compress(I.Lapis, B.LapisBlock);
        Shaped(S(I.GoldIngot), "###", "###", "###", '#', I.GoldNugget);
        Shaped(S(B.StoneBricks, 4), "##", "##", '#', ((int)B.Stone, 0));
        Shaped(S(B.Sandstone), "##", "##", '#', ((int)B.Sand, 0));
        Shaped(S(B.CobbleSlab, 6), "###", '#', B.Cobblestone);
        Shaped(S(B.StoneSlab, 6), "###", '#', ((int)B.Stone, 0));
        Shaped(S(B.CobbleStairs, 4), "#  ", "## ", "###", '#', B.Cobblestone);
        Shaped(S(B.Bricks), "##", "##", '#', I.Brick);
        Shaped(S(B.Clay), "##", "##", '#', I.ClayBall);
        Shaped(S(B.Wool), "##", "##", '#', I.String);
        Shaped(S(B.Tnt), "GSG", "SGS", "GSG", 'G', I.Gunpowder, 'S', B.Sand);
        Shapeless(S(I.BoneMeal, 3), I.Bone);
        Shapeless(S(B.MossyCobblestone), B.Cobblestone, B.Vine);
        Shapeless(S(B.Glowstone), I.Redstone, I.GoldNugget, I.Redstone, I.GoldNugget);

        // redstone
        Shaped(S(B.RedstoneTorch), "R", "S", 'R', I.Redstone, 'S', I.Stick);
        Shaped(S(B.Lever), "S", "C", 'S', I.Stick, 'C', B.Cobblestone);
        Shapeless(S(B.StoneButton), B.Stone);
        Shaped(S(B.RedstoneLamp), " R ", "RTR", " R ", 'R', I.Redstone, 'T', B.Torch);
        Shaped(S(I.RepeaterItem), "TRT", "SSS", 'T', B.RedstoneTorch, 'R', I.Redstone, 'S', B.Stone);
        Shaped(S(B.Piston), "PPP", "CIC", "CRC", 'P', AnyPlanks, 'C', B.Cobblestone, 'I', I.IronIngot, 'R', I.Redstone);
        Shapeless(S(B.StickyPiston), B.Piston, I.String);

        Smelting.Init();
        ModuleRecipes();
    }
}

public static class Smelting
{
    public static readonly Dictionary<int, (ItemStack result, float xp)> Table = new();

    public static void Init()
    {
        if (Table.Count > 0) return;
        void Add(int input, int output, float xp, int count = 1) => Table[input] = (new ItemStack(output, count), xp);
        Add(B.IronOre, I.IronIngot, 0.7f);
        Add(B.GoldOre, I.GoldIngot, 1f);
        Add(B.DiamondOre, I.Diamond, 1f);
        Add(B.CoalOre, I.Coal, 0.1f);
        Add(B.RedstoneOre, I.Redstone, 0.7f, 4);
        Add(B.LapisOre, I.Lapis, 0.2f, 4);
        Add(B.Sand, B.Glass, 0.1f);
        Add(B.Cobblestone, B.Stone, 0.1f);
        Add(B.OakLog, I.Charcoal, 0.15f);
        Add(B.BirchLog, I.Charcoal, 0.15f);
        Add(I.RawPorkchop, I.CookedPorkchop, 0.35f);
        Add(I.RawBeef, I.Steak, 0.35f);
        Add(I.RawChicken, I.CookedChicken, 0.35f);
        Add(I.RawMutton, I.CookedMutton, 0.35f);
        Add(I.ClayBall, I.Brick, 0.3f);
    }

    /// <summary>Przetapianie zależne od meta wejścia (np. mokra gąbka, kamienne cegły). Sprawdzane przed Table.</summary>
    public static readonly Dictionary<(int id, int meta), (ItemStack result, float xp)> MetaTable = new();
    /// <summary>Paliwa modułów (id -> czas palenia w tickach). Sprawdzane przed wbudowaną listą.</summary>
    public static readonly Dictionary<int, int> Fuel = new();

    private static bool Lookup(ItemStack input, out (ItemStack result, float xp) r)
    {
        r = default;
        return input != null && (MetaTable.TryGetValue((input.Id, input.Damage), out r) || Table.TryGetValue(input.Id, out r));
    }
    public static ItemStack Result(ItemStack input) => Lookup(input, out var r) ? r.result : null;
    public static float Xp(ItemStack input) => Lookup(input, out var r) ? r.xp : 0;

    public static int BurnTime(ItemStack s)
    {
        if (s == null) return 0;
        int id = s.Id;
        if (Fuel.TryGetValue(id, out int fuel)) return fuel;
        if (id == I.Coal || id == I.Charcoal) return 1600;
        if (id == B.CoalBlock) return 16000;
        if (id == I.LavaBucket) return 20000;
        if (id == I.Stick) return 100;
        if (id == B.OakPlanks || id == B.BirchPlanks || id == B.OakLog || id == B.BirchLog || id == B.CraftingTable || id == B.Chest
            || id == B.Fence || id == B.FenceGate || id == B.OakStairs || id == B.Bookshelf || id == B.Ladder) return 300;
        if (id == B.OakSlab) return 150;
        if (id == B.OakSapling || id == B.BirchSapling) return 100;
        var it = s.Item;
        if (it != null && it.IsTool && it.ToolLevel == 0 && it.MaxDamage == 59) return 200; // drewniane narzędzia
        if (id == I.Bow) return 300;
        return 0;
    }
}
