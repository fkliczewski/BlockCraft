using BlockCraft.Core;

namespace BlockCraft.Items;

/// <summary>Zaklęcia MC 1.8 (numeracja jak w MC).</summary>
public enum Ench
{
    Protection = 0, FireProtection = 1, FeatherFalling = 2, BlastProtection = 3, ProjectileProtection = 4, Respiration = 5,
    AquaAffinity = 6, Thorns = 7, DepthStrider = 8, Sharpness = 16, Smite = 17, BaneOfArthropods = 18, Knockback = 19,
    FireAspect = 20, Looting = 21, Efficiency = 32, SilkTouch = 33, Unbreaking = 34, Fortune = 35, Power = 48, Punch = 49,
    Flame = 50, Infinity = 51, LuckOfTheSea = 61, Lure = 62
}

/// <summary>
/// Odczyt i zapis zaklęć w ItemStack.Tag (lista "ench" z wpisami {id, lvl} jak w MC; zaklęta książka: "StoredEnchantments").
/// Działanie zaklęć (obrażenia, ochrona, Fortune, Silk Touch, Unbreaking...) implementuje moduł ME; inne moduły tylko odczytują poziomy.
/// </summary>
public static class EnchantHelper
{
    public static int GetLevel(ItemStack s, Ench e, string listKey = "ench")
    {
        var l = s?.Tag?.GetList(listKey);
        if (l == null) return 0;
        foreach (var o in l) if (o is Nbt n && n.GetInt("id") == (int)e) return n.GetInt("lvl");
        return 0;
    }

    public static Dictionary<Ench, int> GetAll(ItemStack s, string listKey = "ench")
    {
        var res = new Dictionary<Ench, int>();
        var l = s?.Tag?.GetList(listKey);
        if (l != null) foreach (var o in l) if (o is Nbt n) res[(Ench)n.GetInt("id")] = n.GetInt("lvl");
        return res;
    }

    public static void Set(ItemStack s, Ench e, int lvl, string listKey = "ench")
    {
        var l = s.GetOrCreateTag().GetOrCreateList(listKey);
        l.RemoveAll(o => o is Nbt n && n.GetInt("id") == (int)e);
        if (lvl > 0) l.Add(new Nbt { ["id"] = (int)e, ["lvl"] = lvl });
        if (l.Count == 0) s.Tag.Remove(listKey);
    }

    public static bool IsEnchanted(ItemStack s) => (s?.Tag?.GetList("ench")?.Count ?? 0) > 0 || (s?.Tag?.GetList("StoredEnchantments")?.Count ?? 0) > 0;

    /// <summary>Najwyższy poziom zaklęcia na założonej zbroi (np. Respiration, Depth Strider, Thorns).</summary>
    public static int MaxOnArmor(ItemStack[] armor, Ench e)
    {
        int m = 0;
        if (armor != null) foreach (var a in armor) m = Math.Max(m, GetLevel(a, e));
        return m;
    }

    public static int MaxLevel(Ench e) => e switch
    {
        Ench.Protection or Ench.FireProtection or Ench.FeatherFalling or Ench.BlastProtection or Ench.ProjectileProtection => 4,
        Ench.Respiration or Ench.Thorns or Ench.DepthStrider or Ench.Unbreaking or Ench.Fortune or Ench.Looting or Ench.LuckOfTheSea or Ench.Lure => 3,
        Ench.Sharpness or Ench.Smite or Ench.BaneOfArthropods or Ench.Efficiency or Ench.Power => 5,
        Ench.Knockback or Ench.FireAspect or Ench.Punch => 2,
        _ => 1
    };

    public static string Name(Ench e) => e switch
    {
        Ench.Protection => "Ochrona", Ench.FireProtection => "Ochrona przed ogniem", Ench.FeatherFalling => "Powolne opadanie",
        Ench.BlastProtection => "Ochrona przed wybuchami", Ench.ProjectileProtection => "Ochrona przed pociskami", Ench.Respiration => "Oddychanie",
        Ench.AquaAffinity => "Wydajność pod wodą", Ench.Thorns => "Ciernie", Ench.DepthStrider => "Głębinowy wędrowiec", Ench.Sharpness => "Ostrość",
        Ench.Smite => "Pogromca nieumarłych", Ench.BaneOfArthropods => "Zmora stawonogów", Ench.Knockback => "Odrzut", Ench.FireAspect => "Zaklęty ogień",
        Ench.Looting => "Grabież", Ench.Efficiency => "Wydajność", Ench.SilkTouch => "Jedwabny dotyk", Ench.Unbreaking => "Niezniszczalność",
        Ench.Fortune => "Szczęście", Ench.Power => "Moc", Ench.Punch => "Uderzenie", Ench.Flame => "Płomień", Ench.Infinity => "Nieskończoność",
        Ench.LuckOfTheSea => "Morska fortuna", Ench.Lure => "Przynęta", _ => e.ToString()
    };

    public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", 10 => "X", _ => n.ToString() };
}
