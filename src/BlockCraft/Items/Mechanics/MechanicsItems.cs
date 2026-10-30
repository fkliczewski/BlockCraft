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
    }
}
