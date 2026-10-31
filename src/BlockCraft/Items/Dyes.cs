using BlockCraft.World;

namespace BlockCraft.Items;

/// <summary>
/// Kolory jak EnumDyeColor w MC 1.8. Meta wełny/gliny/szkła/dywanu = kolor (0 biały ... 15 czarny),
/// meta barwnika (I.Dye) = 15 - kolor. Lazuryt (I.Lapis) i mączka kostna (I.BoneMeal) to osobne itemy, ale liczą się jako barwniki 4 i 15.
/// </summary>
public static class Dyes
{
    public const int White = 0, Orange = 1, Magenta = 2, LightBlue = 3, Yellow = 4, Lime = 5, Pink = 6, Gray = 7, LightGray = 8,
        Cyan = 9, Purple = 10, Blue = 11, Brown = 12, Green = 13, Red = 14, Black = 15;

    /// <summary>Nazwy kolorów w rodzaju żeńskim ("Biała wełna").</summary>
    public static readonly string[] NamesF = { "Biała", "Pomarańczowa", "Karmazynowa", "Jasnoniebieska", "Żółta", "Limonkowa", "Różowa", "Szara",
        "Jasnoszara", "Błękitna", "Fioletowa", "Niebieska", "Brązowa", "Zielona", "Czerwona", "Czarna" };
    /// <summary>Nazwy kolorów w rodzaju nijakim ("Białe szkło").</summary>
    public static readonly string[] NamesN = { "Białe", "Pomarańczowe", "Karmazynowe", "Jasnoniebieskie", "Żółte", "Limonkowe", "Różowe", "Szare",
        "Jasnoszare", "Błękitne", "Fioletowe", "Niebieskie", "Brązowe", "Zielone", "Czerwone", "Czarne" };
    /// <summary>Nazwy barwników wg meta I.Dye (jak w MC: 0 worek z atramentem, 3 ziarna kakaowe...).</summary>
    public static readonly string[] DyeNames = { "Worek z atramentem", "Czerwony barwnik", "Zielony barwnik", "Ziarna kakaowe", "Lazuryt",
        "Fioletowy barwnik", "Błękitny barwnik", "Jasnoszary barwnik", "Szary barwnik", "Różowy barwnik", "Limonkowy barwnik",
        "Żółty barwnik", "Jasnoniebieski barwnik", "Karmazynowy barwnik", "Pomarańczowy barwnik", "Mączka kostna" };

    /// <summary>RGB koloru (EnumDyeColor.getMapColor / kolory owiec i skóry z MC 1.8, tabela fleeceColorTable * 255).</summary>
    public static readonly uint[] Rgb = { 0xFFFFFF, 0xD87F33, 0xB24CD8, 0x6699D8, 0xE5E533, 0x7FCC19, 0xF27FA5, 0x4C4C4C,
        0x999999, 0x4C7F99, 0x7F3FB2, 0x334CB2, 0x664C33, 0x667F33, 0x993333, 0x191919 };

    /// <summary>Kolor (0-15) barwnika w stacku albo -1, gdy to nie barwnik.</summary>
    public static int ColorOf(ItemStack s)
    {
        if (s == null) return -1;
        if (s.Id == I.Dye) return 15 - (s.Damage & 15);
        if (s.Id == I.Lapis) return Blue;
        if (s.Id == I.BoneMeal) return White;
        return -1;
    }

    public static int DyeMetaFromColor(int color) => 15 - (color & 15);

    /// <summary>Stack barwnika dla koloru (dla 4/15 zwraca lazuryt / mączkę kostną).</summary>
    public static ItemStack DyeStack(int color, int count = 1) => color switch
    {
        Blue => new ItemStack(I.Lapis, count),
        White => new ItemStack(I.BoneMeal, count),
        _ => new ItemStack(I.Dye, count, DyeMetaFromColor(color))
    };
}
