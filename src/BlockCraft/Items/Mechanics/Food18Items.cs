using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

public static class FishFood
{
    public static int Type(int meta)=>meta is >=0 and <=3?meta:0;
    public static readonly string[] RawNames={"Surowa ryba","Surowy łosoś","Błazenek","Rozdymka"};
    public static readonly string[] RawTextures={"fish_cod_raw","fish_salmon_raw","fish_clownfish_raw","fish_pufferfish_raw"};
    public static readonly string[] CookedTextures={"fish_cod_cooked","fish_salmon_cooked"};
    public static (int hunger,float saturation) Nutrition(int meta,bool cooked)
    {
        int type=Type(meta);
        if(cooked&&type<2)return type==1?(6,.8f):(5,.6f);
        return type<2?(2,.1f):(1,.1f);
    }
}

public static partial class ItemRegistry
{
    internal static void ItemsFood18()
    {
        BlockItem(I.CakeItem,"cake","Ciasto",B.Cake,Blocks.TabFood).MaxStack=1;
        var raw=Simple(I.RawFish,"fish_cod_raw","Surowa ryba",Blocks.TabFood);raw.Hunger=2;raw.SaturationMod=.1f;
        raw.Variants=new[]{0,1,2,3};raw.NameFn=s=>FishFood.RawNames[FishFood.Type(s.Damage)];
        raw.SpriteFn=s=>TextureGen.Layer("item_"+FishFood.RawTextures[FishFood.Type(s.Damage)]);
        var cooked=Simple(I.CookedFish,"fish_cod_cooked","Pieczona ryba",Blocks.TabFood);cooked.Hunger=5;cooked.SaturationMod=.6f;
        cooked.Variants=new[]{0,1};cooked.NameFn=s=>FishFood.Type(s.Damage)==1?"Pieczony łosoś":"Pieczona ryba";
        cooked.SpriteFn=s=>TextureGen.Layer("item_"+FishFood.CookedTextures[FishFood.Type(s.Damage)==1?1:0]);
    }
}

public static partial class Recipes
{
    internal static void RecipesFood18()
    {
        Shaped(new ItemStack(I.CakeItem),"MMM","SES","WWW",'M',I.MilkBucket,'S',I.Sugar,'E',I.Egg,'W',I.Wheat);
        Smelting.MetaTable[(I.RawFish,0)]=(new ItemStack(I.CookedFish),.35f);
        Smelting.MetaTable[(I.RawFish,1)]=(new ItemStack(I.CookedFish,1,1),.35f);
    }
}
