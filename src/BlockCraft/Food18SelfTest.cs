using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestFood18()
    {
        var w=MakeWorld(33,WorldType.Superflat,1);w.Rules.DoMobSpawning=false;
        var p=new Player(w);p.SetPositionAndPrev(8,4,8);w.AddEntityNow(p);
        var bench=new WorkbenchContainer(p.Inventory,8,3,8);
        int[] recipe={I.MilkBucket,I.MilkBucket,I.MilkBucket,I.Sugar,I.Egg,I.Sugar,I.Wheat,I.Wheat,I.Wheat};
        for(int i=0;i<9;i++)bench.CraftGrid.Set(i,new ItemStack(recipe[i]));bench.UpdateCrafting();
        var cake=bench.CraftResult.Get(0);
        Check(cake?.Id==I.CakeItem&&cake.Count==1&&cake.MaxStack==1,"Ciasto: receptura mleko/cukier/jajko/pszenica, stos 1");
        p.Achievements.UnionWith(new[]{"openInventory","mineWood","buildWorkBench","buildHoe"});
        bench.Slots[0].OnTake(p,cake.Copy());
        Check(Enumerable.Range(0,3).All(i=>bench.CraftGrid.Get(i)?.Id==I.Bucket)
            &&Enumerable.Range(3,6).All(i=>bench.CraftGrid.Get(i)==null)&&p.Achievements.Contains("bakeCake"),
            "Ciasto: odbiór zwraca trzy wiadra i przyznaje osiągnięcie");
        var b=(CakeBlock)Block.Get(B.Cake);
        Check(b.CanPlaceAt(w,8,4,8)&&!b.CanPlaceAt(w,8,5,8),"Ciasto: wymaga litego podłoża");
        w.SetBlock(8,4,8,B.Cake);p.FoodLevel=20;p.Saturation=0;
        Check(b.OnActivated(w,8,4,8,p)&&w.GetMeta(8,4,8)==0,"Ciasto: pełny gracz nie zużywa kęsa");
        for(int i=0;i<7;i++)
        {
            Check(b.GetComparatorOutput(w,8,4,8)==14-i*2&&CakeBlock.Shape(i).MinX==(1+i*2)/16.0,"Ciasto: geometria i komparator kęsa "+i);
            p.FoodLevel=10;p.Saturation=0;
            b.OnActivated(w,8,4,8,p);
            Check(p.FoodLevel==12&&Math.Abs(p.Saturation-.4f)<.0001f,"Ciasto: kęs daje 2 głodu i 0.4 nasycenia "+i);
        }
        Check(w.GetBlock(8,4,8)==B.Air,"Ciasto: siódmy kęs usuwa blok");
        w.SetBlock(9,4,8,B.Cake);w.SetBlock(9,3,8,B.Air);w.Tick();
        Check(w.GetBlock(9,4,8)==B.Air&&!w.Entities.OfType<ItemEntity>().Any(e=>e.Stack.Id==I.CakeItem),"Ciasto: traci podparcie bez dropu");
        w.SetBlock(10,4,8,B.Cake);w.BreakBlock(10,4,8,true);w.Tick();
        Check(!w.Entities.OfType<ItemEntity>().Any(e=>e.Stack.Id==I.CakeItem),"Ciasto: zniszczone nie wypada");
        Check(Item.ById[I.RawFish].Variants.SequenceEqual(new[]{0,1,2,3})&&Item.ById[I.CookedFish].Variants.SequenceEqual(new[]{0,1}),"Ryby: cztery surowe i dwa pieczone warianty");
        for(int i=0;i<4;i++)
        {
            var cooked=Smelting.Result(new ItemStack(I.RawFish,1,i));
            Check(i<2?cooked?.Id==I.CookedFish&&cooked.Damage==i&&Math.Abs(Smelting.Xp(new ItemStack(I.RawFish,1,i))-.35f)<.001:cooked==null,
                "Ryby: pieczenie dozwolone tylko dla dorsza i łososia, meta "+i);
            p.ClearEffects();p.FoodLevel=0;p.Saturation=0;p.Eat(new ItemStack(I.RawFish,1,i));
            Check(p.FoodLevel==(i<2?2:1)&&Math.Abs(p.Saturation-(i<2?.4f:.2f))<.001,"Ryby: wartości surowego wariantu "+i);
        }
        Check(p.EffectLevel(Effect.Poison)==3&&p.EffectLevel(Effect.Hunger)==2&&p.EffectLevel(Effect.Nausea)==1,"Ryby: rozdymka daje Truciznę IV, Głód III i Mdłości II");
        for(int i=0;i<2;i++)
        {
            p.ClearEffects();p.FoodLevel=10;p.Saturation=0;p.Eat(new ItemStack(I.CookedFish,1,i));
            Check(p.FoodLevel==(i==0?15:16)&&Math.Abs(p.Saturation-(i==0?6:9.6f))<.001,"Ryby: wartości pieczonego wariantu "+i);
        }
        var furnace=new FurnaceTileEntity();furnace.Slots[0]=new ItemStack(I.RawFish,2,1);furnace.Slots[1]=new ItemStack(I.Coal);
        w.SetBlock(11,4,8,B.Furnace);w.SetTileEntity(11,4,8,furnace);
        for(int tick=0;tick<201;tick++)furnace.Tick(w);
        Check(furnace.Slots[2]?.Id==I.CookedFish&&furnace.Slots[2].Damage==1&&furnace.Slots[0]?.Count==1,"Ryby: rzeczywisty piec piecze łososia z zachowaniem meta");
        p.Achievements.UnionWith(new[]{"buildPickaxe","buildFurnace"});new FurnaceOutputSlot(furnace,0,0).OnTake(p,furnace.Slots[2]);
        Check(p.Achievements.Contains("cookFish"),"Ryby: odbiór z pieca odblokowuje osiągnięcie");
    }
}
