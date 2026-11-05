using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Etap 5 paczek: pliki .lang 1.8 (en_US jako baza, wybrany język, paczki) i nazwy przedmiotów z kluczy 1.8.</summary>
public static partial class SelfTest
{
    private static void TestPackLang()
    {
        Console.WriteLine("[Paczki zasobów: język]");
        ItemRegistry.Init();
        byte[] J(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        var keysByItem = Item.All.Select(i => i.Key).ToHashSet();
        var badItems = LangKeys.ByItem.Keys.Where(k => !keysByItem.Contains(k[..k.IndexOf(':')])).ToList();
        Check(LangKeys.ByItem.Count > 500 && badItems.Count == 0, "Tabela kluczy: same znane przedmioty", LangKeys.ByItem.Count + " " + string.Join(",", badItems.Take(5)));

        string polish = new ItemStack(I.IronPickaxe).Name;
        string polishDiamond = new ItemStack(I.Diamond).Name;
        var official = new Dictionary<string, byte[]>
        {
            ["en_US"] = J("# komentarz\r\nitem.pickaxeIron.name=Iron Pickaxe\r\ntile.cloth.red.name=Red Wool\r\nitem.monsterPlacer.name=Spawn\r\nentity.Creeper.name=Creeper\r\n" +
                          "item.emptyPotion.name=Water Bottle\r\npotion.prefix.grenade=Splash\r\npotion.regeneration.postfix=Potion of Regeneration\r\n"),
            ["de_DE"] = J("item.pickaxeIron.name=Eisenspitzhacke\n"),
        };
        var pack = new Dictionary<string, byte[]> { ["assets/minecraft/lang/de_DE.lang"] = J("tile.cloth.red.name=Rote Wolle (Paczka)\n") };
        try
        {
            Lang.Load("en_US", null, c => official.GetValueOrDefault(c));
            Check(new ItemStack(I.IronPickaxe).Name == "Iron Pickaxe", "en_US: nazwa z klucza 1.8");
            Check(new ItemStack(B.Wool, 1, 14).Name == "Red Wool", "Wariant (wełna:14) z klucza koloru");
            Check(new ItemStack(I.SpawnEgg, 1, 50).Name == "Spawn Creeper", "Jajko przywołujące: przedrostek + encja");
            Check(new ItemStack(I.Potion, 1, 0).Name == "Water Bottle" && new ItemStack(I.Potion, 1, 16385).Name == "Splash Potion of Regeneration", "Mikstury jak ItemPotion 1.8");
            Lang.Load("de_DE", new Func<string, byte[]>[] { p => pack.GetValueOrDefault(p) }, c => official.GetValueOrDefault(c));
            Check(new ItemStack(I.IronPickaxe).Name == "Eisenspitzhacke" && new ItemStack(B.Wool, 1, 14).Name == "Rote Wolle (Paczka)", "Wybrany język na en_US, paczka nadpisuje");
            Check(new ItemStack(I.Diamond).Name == polishDiamond, "Brak klucza w języku: nazwa gry");
        }
        finally { Lang.Load("", null); }
        Check(new ItemStack(I.IronPickaxe).Name == polish, "Bez języka: polskie nazwy gry");

        var en = Lang.OfficialFile("en_US");
        if (en == null) { Console.WriteLine("    (brak client.jar - pomijam sprawdzenie kluczy w en_US.lang)"); return; }
        var t = new Dictionary<string, string>(); Lang.Merge(t, en);
        var missing = LangKeys.ByItem.Values.Distinct().Where(k => !t.ContainsKey(k)).ToList();
        Check(missing.Count == 0, "Wszystkie klucze tabeli istnieją w en_US.lang 1.8", string.Join(",", missing.Take(5)));
    }
}
