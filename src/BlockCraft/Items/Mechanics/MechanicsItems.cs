using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

public static partial class ItemRegistry
{
    static partial void ItemsMechanics()
    {
        Simple(I.GlassBottle, "glass_bottle", "Szklana butelka", Blocks.TabBrewing);
        var potion = Simple(I.Potion, "potion", "Mikstura", Blocks.TabBrewing, 1);
        potion.Variants = Potions.CreativeVariants(); potion.NameFn = Potions.Name;
        potion.OverlayFn = s => (TextureGen.Layer("me_potion_liquid"), Potions.Color(s));
        potion.SpriteFn = s => TextureGen.Layer(Potions.Splash(s.Damage) ? "item_splash_potion" : "item_potion");
        Simple(I.ExpBottle, "experience_bottle", "Butelka z doświadczeniem", Blocks.TabMisc);
        Simple(I.FermentedSpiderEye, "fermented_spider_eye", "Fermentowane oko pająka", Blocks.TabBrewing);
        Simple(I.GlisteringMelon, "speckled_melon", "Błyszczący arbuz", Blocks.TabBrewing);
        var carrot = Simple(I.GoldenCarrot, "golden_carrot", "Złota marchewka", Blocks.TabBrewing); carrot.Hunger = 6; carrot.SaturationMod = 1.2f;
        BlockItem(I.BrewingStandItem, "brewing_stand", "Statyw alchemiczny", B.BrewingStand, Blocks.TabBrewing);
        BlockItem(I.CauldronItem, "cauldron", "Kocioł", B.Cauldron, Blocks.TabBrewing);
        Item.ById[I.GoldenApple].Variants = new[] { 0, 1 };
        var rod = Simple(I.FishingRod, "fishing_rod", "Wędka", Blocks.TabTools, 1); rod.MaxDamage = 64; rod.Handheld = true;
        Simple(I.EnchantedBook, "enchanted_book", "Zaklęta książka", Blocks.TabMisc, 1);
        string[] keys = { "helmet", "chestplate", "leggings", "boots" }, names = { "Kolczy hełm", "Kolczy napierśnik", "Kolcze spodnie", "Kolcze buty" };
        int[] points = { 2, 5, 4, 1 }, baseDur = { 11, 16, 15, 13 };
        for (int s = 0; s < 4; s++)
        {
            var a = Simple(I.ChainHelmet + s, "chainmail_" + keys[s], names[s], Blocks.TabCombat, 1);
            a.ArmorSlot = s; a.ArmorPoints = points[s]; a.ArmorMaterial = 4; a.MaxDamage = baseDur[s] * 15;
        }
    }
}
