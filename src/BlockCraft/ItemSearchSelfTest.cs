using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Zadanie A: wyszukiwanie przedmiotów (polskie znaki, kolejność słów, klucz, zaklęcia, kolejność wyników).</summary>
public static partial class SelfTest
{
    private static void TestItemSearch()
    {
        Console.WriteLine("[Wyszukiwanie przedmiotów]");
        ItemRegistry.Init();
        var all = ItemRegistry.CreativeItems(-1);
        bool Finds(string q, int id, int meta = 0) => ItemSearch.Filter(all, q).Any(s => s.Id == id && s.Damage == meta);
        Check(ItemSearch.Normalize("Żelazny KILOF_ąęłńóśźć §cx") == "zelazny kilof aelnoszc x", "Normalizacja: polskie znaki, wielkość liter, _ i kody §", ItemSearch.Normalize("Żelazny KILOF_ąęłńóśźć §cx"));
        Check(Finds("zelazny kilof", I.IronPickaxe), "Bez polskich znaków: „zelazny kilof”");
        Check(Finds("kilof żelazny", I.IronPickaxe), "Słowa w dowolnej kolejności");
        Check(Finds("iron_pickaxe", I.IronPickaxe) && Finds("iron pickaxe", I.IronPickaxe), "Po kluczu przedmiotu");
        Check(Finds("CZERWONA WEŁNA", B.Wool, 14), "Wielkie litery i wariant (czerwona wełna)");
        Check(!Finds("zelazny kilof", I.DiamondPickaxe), "Inne przedmioty odrzucone");
        Check(ItemSearch.Filter(all, "  ").Count == all.Count, "Puste zapytanie: wszystko");
        Check(ItemSearch.Filter(all, "qqqzzz").Count == 0, "Brak wyników");
        var book = new ItemStack(I.EnchantedBook);
        EnchantingRules.AddStored(book, new EnchantmentRoll(Ench.Sharpness, 1));
        Check(ItemSearch.Matches(book, EnchantHelper.Name(Ench.Sharpness)), "Zaklęta książka po nazwie zaklęcia");
        // książka na liście kreatywnej nie ma NBT — ma być znajdowana po każdym zaklęciu (może je przechowywać)
        var creativeBook = all.First(s => s.Id == I.EnchantedBook);
        Check(creativeBook.Tag == null && ItemSearch.Matches(creativeBook, EnchantHelper.Name(Ench.Sharpness)),
            "Kreatywna zaklęta książka (bez NBT) po nazwie zaklęcia");
        var kilofy = ItemSearch.Filter(all, "kilof");
        Check(kilofy.Count > 3, "Wszystkie kilofy", kilofy.Count.ToString());
        var tory = ItemSearch.Filter(all, "tor");
        Check(tory.Count > 0 && ItemSearch.Normalize(tory[0].Name).StartsWith("tor"), "Najpierw nazwy zaczynające się od zapytania", tory.FirstOrDefault()?.Name);
    }
}
