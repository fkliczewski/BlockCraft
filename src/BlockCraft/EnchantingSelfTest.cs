using System.Text;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Reguły zaklinania porównane z oryginalnym server.jar 1.8 (tools/mechanics/EnchOracle.java).</summary>
public static partial class SelfTest
{
    // mcid liczba_linii fnv1a64 - wynik EnchOracle dla 46 przedmiotów, 16 biblioteczek, 10 ziaren i mocy 1..58.
    private const string EnchGolden = "268 360 c7fe59043edcaa19;269 360 089fa231983b9bbc;270 360 715c9da8111af59c;271 360 97f966b118630b6a;272 360 59451cb5affc59c9;273 360 62d3010908bc5333;274 360 bad4daed031085d5;275 360 aa49d2462aac4e37;267 360 7216e83d84796011;256 360 05d3b054ab975d40;257 360 f75adcfac61a97e6;258 360 1947677ddf70b5ec;276 360 43fb617b81e9d068;277 360 9545851d24d6f5bf;278 360 434957c235db0831;279 360 1bb32fac13e9b333;283 360 4eba9e3c4c779d22;284 360 376ba5fe559bb0a9;285 360 1dd38a17f9f954f3;286 360 e2451d0afe449aad;293 360 d268aecaa774cfef;298 360 e692280005d64851;299 360 5e1b3571f9be2878;300 360 e667784237b121e4;301 360 b601e21fb97522b0;302 360 4c2e0c269eb9fcbd;303 360 a48134499d0e07b8;304 360 69b3bd14376addac;305 360 0d42eca28d96d578;306 360 28634bb7dba4916b;307 360 3523d3d5c4879061;308 360 1f1b61ba4fa8ad90;309 360 76a5aaa17c4dc7b1;310 360 45f98c3a2317e913;311 360 ac68493b7cc83701;312 360 012431930f47b33c;313 360 f245d167e421419d;314 360 286994985dbc05ca;315 360 9a6690f2ddd26b6b;316 360 6df53da2e324741d;317 360 4ae25e7a9005052c;261 360 b373b03e7202a999;346 360 6c851fd67cf49f3b;340 360 5d8ad5a09cbeb992;359 360 f747f14921f60035;280 360 ad3f4376a3a681e7;";

    private static readonly Dictionary<int, int> McItem = new()
    {
        [268] = I.WoodSword, [269] = I.WoodShovel, [270] = I.WoodPickaxe, [271] = I.WoodAxe,
        [272] = I.StoneSword, [273] = I.StoneShovel, [274] = I.StonePickaxe, [275] = I.StoneAxe,
        [267] = I.IronSword, [256] = I.IronShovel, [257] = I.IronPickaxe, [258] = I.IronAxe,
        [276] = I.DiamondSword, [277] = I.DiamondShovel, [278] = I.DiamondPickaxe, [279] = I.DiamondAxe,
        [283] = I.GoldSword, [284] = I.GoldShovel, [285] = I.GoldPickaxe, [286] = I.GoldAxe, [293] = I.DiamondHoe,
        [298] = I.LeatherHelmet, [299] = I.LeatherHelmet + 1, [300] = I.LeatherHelmet + 2, [301] = I.LeatherHelmet + 3,
        [302] = I.ChainHelmet, [303] = I.ChainChest, [304] = I.ChainLegs, [305] = I.ChainBoots,
        [306] = I.IronHelmet, [307] = I.IronHelmet + 1, [308] = I.IronHelmet + 2, [309] = I.IronHelmet + 3,
        [310] = I.DiamondHelmet, [311] = I.DiamondHelmet + 1, [312] = I.DiamondHelmet + 2, [313] = I.DiamondHelmet + 3,
        [314] = I.GoldHelmet, [315] = I.GoldHelmet + 1, [316] = I.GoldHelmet + 2, [317] = I.GoldHelmet + 3,
        [261] = I.Bow, [346] = I.FishingRod, [340] = I.Book, [359] = I.Shears, [280] = I.Stick,
    };

    private static string EnchFmt(IReadOnlyList<EnchantmentRoll> l) => l.Count == 0 ? "null" : string.Join(",", l.Select(e => (int)e.Id + ":" + e.Level));

    /// <summary>Te same linie co EnchOracle.java.</summary>
    internal static IEnumerable<string> EnchOracleLines(int mcId)
    {
        int[] seeds = { 0, 1, -1, 7, 12345, -987654321, 2147483647, -2147483648, 99, 31337 };
        int id = McItem[mcId];
        for (int shelves = 0; shelves <= 15; shelves++) foreach (int seed in seeds)
        {
            var o = EnchantingRules.Offers(new ItemStack(id), seed, shelves);
            string L(int i) => o[i].RequiredLevel > 0 ? EnchFmt(o[i].Enchantments) : "-";
            string C(int i) => o[i].Clue is { } c ? (int)c.Id + ":" + c.Level : "-";
            yield return $"T {mcId} {shelves} {seed} {o[0].RequiredLevel} {o[1].RequiredLevel} {o[2].RequiredLevel} | {L(0)} ; {L(1)} ; {L(2)} | {C(0)} {C(1)} {C(2)}";
        }
        for (int power = 1; power <= 60; power += 3) foreach (int seed in seeds)
            yield return $"B {mcId} {power} {seed} {EnchFmt(EnchantingRules.BuildList(new JRandom(seed), new ItemStack(id), power))}";
    }

    private static void TestEnchanting()
    {
        int ok = 0; var bad = new List<string>();
        foreach (var row in EnchGolden.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = row.Split(' '); int mc = int.Parse(p[0]);
            ulong h = 0xcbf29ce484222325UL; int n = 0; string first = null; var all = EnchOracleLines(mc).ToList();
            foreach (var line in all)
            {
                foreach (byte b in Encoding.UTF8.GetBytes(line + "\n")) { h ^= b; h *= 0x100000001b3UL; }
                n++; first ??= line;
            }
            if (n == int.Parse(p[1]) && h.ToString("x16") == p[2]) ok++; else { bad.Add(mc + ":" + n + ":" + h.ToString("x16") + "/" + p[1] + ":" + p[2]); File.WriteAllLines(Path.Combine(Path.GetTempPath(), "bc-enchant-" + mc + ".txt"), all); }
        }
        Check(bad.Count == 0 && ok == 46, "Zaklinanie: oferty i listy zgodne z server.jar 1.8 dla 46 przedmiotów", string.Join("; ", bad.Take(4)));

        var o = EnchantingRules.Offers(new ItemStack(I.Book), 0, 15);
        Check(o[0].RequiredLevel == 8 && o[1].RequiredLevel == 13 && o[2].RequiredLevel == 30 && o[2].Clue == new EnchantmentRoll(Ench.Power, 4),
            "Zaklinanie: książka, 15 biblioteczek, ziarno 0 = 8/13/30 i podpowiedź Moc IV");
        var sword = EnchantingRules.Offers(new ItemStack(I.DiamondSword), 1, 15)[2];
        Check(EnchFmt(sword.Enchantments) == "16:4,19:2,34:3", "Zaklinanie: diamentowy miecz, ziarno 1, slot 3 = Ostrość IV, Odrzut II, Niezniszczalność III", EnchFmt(sword.Enchantments));
        var enchanted = new ItemStack(I.DiamondSword); EnchantHelper.Set(enchanted, Ench.Sharpness, 1);
        Check(!EnchantingRules.CanUseTable(enchanted) && !EnchantingRules.CanUseTable(new ItemStack(I.Book, 2)), "Zaklinanie: zaklęty przedmiot i stos książek nie wchodzą na stół");
        Check(EnchantingRules.CanApply(Ench.Sharpness, new ItemStack(I.DiamondAxe), false) && !EnchantingRules.CanApply(Ench.Sharpness, new ItemStack(I.DiamondAxe)),
            "Zaklinanie: Ostrość na siekierze tylko w kowadle");
        Check(EnchantingRules.CanApply(Ench.Thorns, new ItemStack(I.IronHelmet), false) && !EnchantingRules.CanApply(Ench.Thorns, new ItemStack(I.IronHelmet)),
            "Zaklinanie: Ciernie na hełmie tylko w kowadle");

        var w = MakeWorld(3, WorldType.Superflat, 0);
        int y = 5, c = 8; for (int x = -3; x <= 3; x++) for (int z = -3; z <= 3; z++) for (int dy = 0; dy <= 2; dy++) w.SetBlock(c + x, y + dy, c + z, B.Air);
        Check(EnchantingRules.BookshelfPower(w, c, y, c) == 0, "Biblioteczki: brak = 0");
        for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++)
            if (Math.Max(Math.Abs(x), Math.Abs(z)) == 2) for (int dy = 0; dy <= 1; dy++) w.SetBlock(c + x, y + dy, c + z, B.Bookshelf);
        Check(EnchantingRules.BookshelfPower(w, c, y, c) == 15, "Biblioteczki: pełny pierścień 32 daje limit 15");
        for (int x = -2; x <= 2; x++) for (int z = -2; z <= 2; z++) for (int dy = 0; dy <= 1; dy++) if (Math.Max(Math.Abs(x), Math.Abs(z)) == 2) w.SetBlock(c + x, y + dy, c + z, B.Air);
        w.SetBlock(c + 2, y, c, B.Bookshelf); w.SetBlock(c + 2, y + 1, c + 1, B.Bookshelf);
        Check(EnchantingRules.BookshelfPower(w, c, y, c) == 2, "Biblioteczki: półka na wprost i półka przy rogu", EnchantingRules.BookshelfPower(w, c, y, c).ToString());
        w.SetBlock(c + 1, y + 1, c, B.Torch);
        Check(EnchantingRules.BookshelfPower(w, c, y, c) == 1, "Biblioteczki: pochodnia między stołem a półką blokuje", EnchantingRules.BookshelfPower(w, c, y, c).ToString());
    }
}
