using System.Text;

namespace BlockCraft.Items;

/// <summary>Wyszukiwanie przedmiotów (zakładka „Szukaj” trybu kreatywnego): bez wielkości liter i polskich znaków,
/// słowa w dowolnej kolejności, po nazwie (także polskiej nazwie gry przy innym języku), kluczu (iron_pickaxe) i zaklęciach.</summary>
public static class ItemSearch
{
    /// <summary>Małe litery bez polskich znaków i kodów kolorów §; '_' jak spacja.</summary>
    public static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = char.ToLowerInvariant(s[i]);
            if (c == '§') { i++; continue; }
            sb.Append(c switch
            {
                'ą' => 'a', 'ć' => 'c', 'ę' => 'e', 'ł' => 'l', 'ń' => 'n', 'ó' => 'o', 'ś' => 's', 'ź' or 'ż' => 'z',
                '_' => ' ',
                _ => c,
            });
        }
        return sb.ToString();
    }

    /// <summary>Tekst, w którym szukamy: nazwy, klucz i zaklęcia (książki i przedmioty).</summary>
    public static string Haystack(ItemStack s)
    {
        // s.Name = nazwa w aktualnym języku; NameFn = polska nazwa wariantu (mikstury, barwniki, jajka) — szukamy w obu.
        var parts = new List<string> { s.Name, s.Item?.NameFn?.Invoke(s), s.Item?.DisplayName(s.Damage), s.Item?.Key };
        parts.AddRange(EnchantNames(s));
        return Normalize(string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p))));
    }

    /// <summary>Nazwy zaklęć: rzeczywiste z NBT przedmiotu; zaklęta książka na liście kreatywnej nie ma NBT,
    /// więc dopisujemy wszystkie zaklęcia (książka może przechowywać każde — EnchantingRules.CanApply).</summary>
    private static IEnumerable<string> EnchantNames(ItemStack s)
    {
        foreach (var key in new[] { "ench", "StoredEnchantments" })
            foreach (var e in EnchantHelper.GetAll(s, key).Keys) yield return EnchantHelper.Name(e);
        if (s.Id == I.EnchantedBook)
            foreach (var e in Enum.GetValues<Ench>()) yield return EnchantHelper.Name(e);
    }

    /// <summary>Każde słowo zapytania występuje gdzieś w tekście przedmiotu (pusta fraza = wszystko).</summary>
    public static bool Matches(ItemStack s, string query)
    {
        var words = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return true;
        string hay = Haystack(s);
        return words.All(hay.Contains);
    }

    /// <summary>Pasujące przedmioty: najpierw te, których nazwa zaczyna się od zapytania, dalej w kolejności listy.</summary>
    public static List<ItemStack> Filter(IEnumerable<ItemStack> items, string query)
    {
        string q = Normalize(query).Trim();
        var hits = items.Where(s => Matches(s, query)).ToList();
        if (q.Length == 0) return hits;
        return hits.Select((s, i) => (s, i, first: Normalize(s.Name).StartsWith(q) ? 0 : 1))
            .OrderBy(t => t.first).ThenBy(t => t.i).Select(t => t.s).ToList();
    }
}
