using BlockCraft.World;

namespace BlockCraft.Items;

public static partial class ItemRegistry
{
    static partial void ItemsNetherEnd()
    {
        Simple(I.NetherWartItem, "nether_wart", "Brodawka netherowa", Blocks.TabBrewing).BlockId = B.NetherWart;
        Simple(I.Quartz, "quartz", "Kwarc Netheru", Blocks.TabMaterials);
        Simple(I.NetherBrickItem, "netherbrick", "Cegła netherowa", Blocks.TabMaterials);
        Simple(I.GlowstoneDust, "glowstone_dust", "Pył jasnogłazu", Blocks.TabMaterials);
        Simple(I.BlazeRod, "blaze_rod", "Różdżka płomyka", Blocks.TabMaterials).Handheld = true;
        Simple(I.BlazePowder, "blaze_powder", "Proszek płomyka", Blocks.TabBrewing);
        Simple(I.GhastTear, "ghast_tear", "Łza ghasta", Blocks.TabBrewing);
        Simple(I.MagmaCream, "magma_cream", "Magmowy krem", Blocks.TabBrewing);
        Simple(I.EyeOfEnder, "ender_eye", "Oko Endu", Blocks.TabMisc);
        Simple(I.FireCharge, "fire_charge", "Ładunek ognisty", Blocks.TabMisc);
        Simple(I.NetherStar, "nether_star", "Gwiazda Netheru", Blocks.TabMaterials).Glint = true;
    }
}

public static partial class Recipes
{
    static partial void RecipesNetherEnd()
    {
        Shaped(S(B.NetherBrick), "##", "##", '#', I.NetherBrickItem);
        Shaped(S(B.NetherBrickFence, 6), "###", "###", '#', B.NetherBrick);
        Shaped(S(B.NetherBrickStairs, 4), "#  ", "## ", "###", '#', B.NetherBrick);
        Shaped(S(B.NetherBrickSlab, 6), "###", '#', B.NetherBrick);
        Shaped(S(B.QuartzBlock), "##", "##", '#', I.Quartz);
        Shaped(S(B.QuartzBlock, 1, 1), "#", "#", '#', B.QuartzSlab);
        Shaped(S(B.QuartzBlock, 2, 2), "#", "#", '#', ((int)B.QuartzBlock, 0));
        Shaped(S(B.QuartzStairs, 4), "#  ", "## ", "###", '#', ((int)B.QuartzBlock, 0));
        Shaped(S(B.QuartzSlab, 6), "###", '#', ((int)B.QuartzBlock, 0));
        Shaped(S(B.Glowstone), "##", "##", '#', I.GlowstoneDust);
        Shapeless(S(I.BlazePowder, 2), I.BlazeRod);
        Shapeless(S(I.MagmaCream), I.Slimeball, I.BlazePowder);
        Shapeless(S(I.EyeOfEnder), I.EnderPearl, I.BlazePowder);
        Shapeless(S(I.FireCharge, 3), AnyCoal, I.Gunpowder, I.BlazePowder);
        Smelting.Table[B.Netherrack] = (S(I.NetherBrickItem), 0.1f);
        Smelting.Table[B.QuartzOre] = (S(I.Quartz), 0.2f);
    }
}
