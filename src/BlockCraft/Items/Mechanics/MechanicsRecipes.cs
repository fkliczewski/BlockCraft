using BlockCraft.World;

namespace BlockCraft.Items;

public static partial class Recipes
{
    /// <summary>Receptury modułu ME z 1.8 (RecipesCrafting/RecipesFood/RecipeBookCloning bez wariantów NBT).</summary>
    static partial void RecipesMechanics()
    {
        Shaped(new ItemStack(B.EnchantingTable), " B ", "D#D", "###", 'B', I.Book, 'D', I.Diamond, '#', B.Obsidian);
        Shaped(new ItemStack(I.BrewingStandItem), " B ", "###", 'B', I.BlazeRod, '#', B.Cobblestone);
        Shaped(new ItemStack(I.CauldronItem), "# #", "# #", "###", '#', I.IronIngot);
        Shaped(new ItemStack(I.GlassBottle, 3), "# #", " # ", '#', B.Glass);
        Shaped(new ItemStack(I.GlisteringMelon), "###", "#X#", "###", '#', I.GoldNugget, 'X', I.MelonSlice);
        Shaped(new ItemStack(I.GoldenCarrot), "###", "#X#", "###", '#', I.GoldNugget, 'X', I.Carrot);
        Shapeless(new ItemStack(I.FermentedSpiderEye), I.SpiderEye, B.BrownMushroom, I.Sugar);
        RecipesUtility(); RecipesBoat(); RecipesSigns(); RecipesHanging(); RecipesMaps(); RecipesFireworks();
        Shaped(new ItemStack(B.Anvil), "III", " i ", "iii", 'I', B.IronBlock, 'i', I.IronIngot);
        Shaped(new ItemStack(I.FishingRod), "  #", " #X", "# X", '#', I.Stick, 'X', I.String);
    }
}
