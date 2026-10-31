using BlockCraft.World;

namespace BlockCraft.Items;

/// <summary>Receptury i przetapianie modułu OW (CraftingManager, RecipesDyes, FurnaceRecipes z MC 1.8).</summary>
public static partial class Recipes
{
    private static (int, int) M(int id, int meta) => (id, meta);

    /// <summary>Receptura bezkształtna ze składnikami zależnymi od meta (meta -1 = dowolne).</summary>
    public static void ShapelessM(ItemStack result, params (int id, int meta)[] ing)
        => All.Add(new Recipe { Shapeless = true, Ingredients = ing.ToList(), Result = result });

    /// <summary>Składnik barwnika koloru (EnumDyeColor): lazuryt i mączka kostna to osobne itemy.</summary>
    private static (int, int) DyeIng(int color) => color switch
    {
        Dyes.Blue => (I.Lapis, -1),
        Dyes.White => (I.BoneMeal, -1),
        _ => (I.Dye, Dyes.DyeMetaFromColor(color))
    };

    static partial void RecipesOverworld()
    {
        // ---- drewno (kolejność MC: dąb, świerk, brzoza, dżungla, akacja, ciemny dąb) ----
        byte[] planks = { B.OakPlanks, B.SprucePlanks, B.BirchPlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks };
        byte[] logs = { B.OakLog, B.SpruceLog, B.BirchLog, B.JungleLog, B.AcaciaLog, B.DarkOakLog };
        byte[] slabs = { B.OakSlab, B.SpruceSlab, B.BirchSlab, B.JungleSlab, B.AcaciaSlab, B.DarkOakSlab };
        byte[] stairs = { B.OakStairs, B.SpruceStairs, B.BirchStairs, B.JungleStairs, B.AcaciaStairs, B.DarkOakStairs };
        byte[] fences = { B.Fence, B.SpruceFence, B.BirchFence, B.JungleFence, B.AcaciaFence, B.DarkOakFence };
        byte[] gates = { B.FenceGate, B.SpruceFenceGate, B.BirchFenceGate, B.JungleFenceGate, B.AcaciaFenceGate, B.DarkOakFenceGate };
        int[] doors = { I.Door, I.SpruceDoorItem, I.BirchDoorItem, I.JungleDoorItem, I.AcaciaDoorItem, I.DarkOakDoorItem };
        byte[] saplings = { B.OakSapling, B.SpruceSapling, B.BirchSapling, B.JungleSapling, B.AcaciaSapling, B.DarkOakSapling };
        for (int t = 1; t < 6; t++)
        {
            int p = planks[t];
            if (t != 2) Shaped(S(p, 4), "#", '#', logs[t]);
            Shaped(S(slabs[t], 6), "PPP", 'P', p);
            Shaped(S(stairs[t], 4), "P  ", "PP ", "PPP", 'P', p);
            Shaped(S(fences[t], 3), "PSP", "PSP", 'P', p, 'S', I.Stick);
            Shaped(S(gates[t]), "SPS", "SPS", 'P', p, 'S', I.Stick);
            Shaped(S(doors[t], 3), "PP", "PP", "PP", 'P', p);
        }

        // ---- kamień i warianty ----
        int stone = B.Stone;
        ShapelessM(S(B.Stone, 1, 1), M(stone, 3), M(I.Quartz, -1));                               // granit
        Shaped(S(B.Stone, 4, 2), "##", "##", '#', M(stone, 1));
        Shaped(S(B.Stone, 2, 3), "CQ", "QC", 'C', B.Cobblestone, 'Q', I.Quartz);                    // dioryt
        Shaped(S(B.Stone, 4, 4), "##", "##", '#', M(stone, 3));
        ShapelessM(S(B.Stone, 2, 5), M(stone, 3), M(B.Cobblestone, -1));                          // andezyt
        Shaped(S(B.Stone, 4, 6), "##", "##", '#', M(stone, 5));
        Shaped(S(B.Dirt, 4, 1), "DG", "GD", 'D', M(B.Dirt, 0), 'G', B.Gravel);                    // gruboziarnista ziemia
        ShapelessM(S(B.StoneBricks, 1, 1), M(B.StoneBricks, 0), M(B.Vine, -1));
        Shaped(S(B.StoneBricks, 1, 3), "#", "#", '#', B.StoneBrickSlab);
        Shaped(S(B.StoneBrickSlab, 6), "###", '#', B.StoneBricks);
        Shaped(S(B.StoneBrickStairs, 4), "#  ", "## ", "###", '#', B.StoneBricks);
        Shaped(S(B.BrickSlab, 6), "###", '#', B.Bricks);
        Shaped(S(B.BrickStairs, 4), "#  ", "## ", "###", '#', B.Bricks);
        Shaped(S(B.CobbleWall, 6), "###", "###", '#', B.Cobblestone);
        Shaped(S(B.CobbleWall, 6, 1), "###", "###", '#', B.MossyCobblestone);

        // ---- piaskowce ----
        Shaped(S(B.Sandstone, 1, 1), "#", "#", '#', B.SandstoneSlab);
        Shaped(S(B.Sandstone, 4, 2), "##", "##", '#', B.Sandstone);
        Shaped(S(B.SandstoneSlab, 6), "###", '#', B.Sandstone);
        Shaped(S(B.SandstoneStairs, 4), "#  ", "## ", "###", '#', B.Sandstone);
        Shaped(S(B.RedSandstone), "##", "##", '#', M(B.Sand, 1));
        Shaped(S(B.RedSandstone, 1, 1), "#", "#", '#', B.RedSandstoneSlab);
        Shaped(S(B.RedSandstone, 4, 2), "##", "##", '#', B.RedSandstone);
        Shaped(S(B.RedSandstoneSlab, 6), "###", '#', B.RedSandstone);
        Shaped(S(B.RedSandstoneStairs, 4), "#  ", "## ", "###", '#', B.RedSandstone);

        // ---- szkło, kraty, glina, wełna, dywany ----
        Shaped(S(B.GlassPane, 16), "###", "###", '#', B.Glass);
        Shaped(S(B.IronBars, 16), "###", "###", '#', I.IronIngot);
        for (int c = 0; c < 16; c++)
        {
            var dye = DyeIng(c);
            Shaped(S(B.StainedGlass, 8, c), "###", "#X#", "###", '#', B.Glass, 'X', dye);
            Shaped(S(B.StainedGlassPane, 16, c), "###", "###", '#', M(B.StainedGlass, c));
            Shaped(S(B.StainedClay, 8, c), "###", "#X#", "###", '#', B.HardenedClay, 'X', dye);
            if (c != Dyes.White) ShapelessM(S(B.Wool, 1, c), dye, M(B.Wool, 0));
            Shaped(S(B.Carpet, 3, c), "##", '#', M(B.Wool, c));
        }

        // ---- budulce ----
        Shaped(S(B.HayBale), "###", "###", "###", '#', I.Wheat);
        Shaped(S(I.Wheat, 9), "#", '#', B.HayBale);
        Shaped(S(B.EmeraldBlock), "###", "###", "###", '#', I.Emerald);
        Shaped(S(I.Emerald, 9), "#", '#', B.EmeraldBlock);
        Shaped(S(B.Prismarine), "##", "##", '#', I.PrismarineShard);
        Shaped(S(B.Prismarine, 1, 1), "###", "###", "###", '#', I.PrismarineShard);
        Shaped(S(B.Prismarine, 1, 2), "###", "#I#", "###", '#', I.PrismarineShard, 'I', M(I.Dye, 0));
        Shaped(S(B.SeaLantern), "SCS", "CCC", "SCS", 'S', I.PrismarineShard, 'C', I.PrismarineCrystals);
        Shaped(S(B.Snow), "##", "##", '#', I.Snowball);
        Shaped(S(B.SnowLayer, 6), "###", '#', B.Snow);

        // ---- rośliny i jedzenie ----
        Shaped(S(B.JackOLantern), "P", "T", 'P', B.Pumpkin, 'T', B.Torch);
        Shaped(S(I.PumpkinSeeds, 4), "#", '#', B.Pumpkin);
        Shaped(S(B.Melon), "###", "###", "###", '#', I.MelonSlice);
        Shaped(S(I.MelonSeeds), "#", '#', I.MelonSlice);
        Shapeless(S(I.PumpkinPie), B.Pumpkin, I.Sugar, I.Egg);
        Shapeless(S(I.MushroomStew), B.BrownMushroom, B.RedMushroom, I.Bowl);

        // ---- barwniki (RecipesDyes) ----
        ShapelessM(S(I.Dye, 1, 11), M(B.Dandelion, 0));
        (int meta, int dye, int count)[] flowers =
        {
            (0, 1, 1), (1, 12, 1), (2, 13, 1), (3, 7, 1), (4, 1, 1), (5, 14, 1), (6, 7, 1), (7, 9, 1), (8, 7, 1)
        };
        foreach (var (meta, dyeMeta, count) in flowers) ShapelessM(S(I.Dye, count, dyeMeta), M(B.Poppy, meta));
        ShapelessM(S(I.Dye, 2, 11), M(B.DoublePlant, 0));   // słonecznik
        ShapelessM(S(I.Dye, 2, 13), M(B.DoublePlant, 1));   // bez
        ShapelessM(S(I.Dye, 2, 1), M(B.DoublePlant, 4));    // róża
        ShapelessM(S(I.Dye, 2, 9), M(B.DoublePlant, 5));    // piwonia
        ShapelessM(S(I.Dye, 2, 14), DyeIng(Dyes.Red), DyeIng(Dyes.Yellow));
        ShapelessM(S(I.Dye, 2, 10), DyeIng(Dyes.Green), DyeIng(Dyes.White));
        ShapelessM(S(I.Dye, 2, 8), DyeIng(Dyes.Black), DyeIng(Dyes.White));
        ShapelessM(S(I.Dye, 2, 7), DyeIng(Dyes.Gray), DyeIng(Dyes.White));
        ShapelessM(S(I.Dye, 3, 7), DyeIng(Dyes.Black), DyeIng(Dyes.White), DyeIng(Dyes.White));
        ShapelessM(S(I.Dye, 2, 12), DyeIng(Dyes.Blue), DyeIng(Dyes.White));
        ShapelessM(S(I.Dye, 2, 6), DyeIng(Dyes.Blue), DyeIng(Dyes.Green));
        ShapelessM(S(I.Dye, 2, 5), DyeIng(Dyes.Blue), DyeIng(Dyes.Red));
        ShapelessM(S(I.Dye, 2, 13), DyeIng(Dyes.Purple), DyeIng(Dyes.Pink));
        ShapelessM(S(I.Dye, 3, 13), DyeIng(Dyes.Blue), DyeIng(Dyes.Red), DyeIng(Dyes.Pink));
        ShapelessM(S(I.Dye, 4, 13), DyeIng(Dyes.Blue), DyeIng(Dyes.Red), DyeIng(Dyes.Red), DyeIng(Dyes.White));
        ShapelessM(S(I.Dye, 2, 9), DyeIng(Dyes.Red), DyeIng(Dyes.White));

        // ---- przetapianie (FurnaceRecipes 1.8) ----
        var tab = Smelting.Table;
        void Smelt(int input, ItemStack output, float xp) => tab[input] = (output, xp);
        foreach (var l in new[] { B.SpruceLog, B.JungleLog, B.AcaciaLog, B.DarkOakLog }) Smelt(l, S(I.Charcoal), 0.15f);
        Smelt(B.Clay, S(B.HardenedClay), 0.35f);
        Smelt(B.Cactus, S(I.Dye, 1, 2), 0.2f);
        Smelt(I.Potato, S(I.BakedPotato), 0.35f);
        Smelt(B.EmeraldOre, S(I.Emerald), 1f);
        Smelting.MetaTable[(B.Sponge, 1)] = (S(B.Sponge), 0.15f);
        Smelting.MetaTable[(B.StoneBricks, 0)] = (S(B.StoneBricks, 1, 2), 0.1f);

        // ---- paliwa (TileEntityFurnace.getItemBurnTime: drewniane bloki 300, płyty 150, sadzonki 100) ----
        var fuel = Smelting.Fuel;
        for (int i = 1; i < 6; i++)
        {
            fuel[planks[i]] = 300; fuel[logs[i]] = 300; fuel[stairs[i]] = 300; fuel[fences[i]] = 300; fuel[gates[i]] = 300;
            fuel[slabs[i]] = 150; fuel[saplings[i]] = 100;
        }
        fuel[B.BrownMushroomBlock] = 300; fuel[B.RedMushroomBlock] = 300;
    }
}
