using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestNetherEnd()
    {
        Console.WriteLine("[Nether i End]");
        Check(Enumerable.Range(187,17).All(i=>Block.ById[i].Id==i),"NE: wszystkie bloki zarejestrowane");
        Check(Enumerable.Range(450,11).All(i=>Item.ById[i]!=null),"NE: wszystkie przedmioty zarejestrowane");
        var powder=Recipes.Find(new ItemStack[]{new(I.BlazeRod),null,null,null},2,2);
        Check(powder?.Id==I.BlazePowder&&powder.Count==2,"NE: różdżka daje dwa proszki");
        var pillar=Recipes.Find(new ItemStack[]{new(B.QuartzBlock),null,new(B.QuartzBlock),null},2,2);
        Check(pillar?.Id==B.QuartzBlock&&pillar.Damage==2&&pillar.Count==2,"NE: receptura filaru kwarcu");
        var w=MakeWorld(18,WorldType.Superflat,1); w.Rules.DoMobSpawning=false;
        using var dimensions=new DimensionSet(w);
        var nether=dimensions.Get(DimensionId.Nether); var end=dimensions.Get(DimensionId.End);
        Check(nether==dimensions.Get(DimensionId.Nether)&&nether!=w,"NE: leniwe instancje wymiarów");
        var chunk=nether.GenerateChunk(0,0); LightEngine.InitChunkLocal(chunk,true);
        nether.Chunks[MathUtil.ChunkKey(0,0)]=chunk; nether.Light.MergeChunk(chunk);
        Check(chunk.Blocks.SequenceEqual(nether.GenerateChunk(0,0).Blocks),"NE: deterministyczny generator");
        Check(Enumerable.Range(0,256).All(i=>chunk.Blocks[i]==B.Bedrock&&chunk.Blocks[(127<<8)+i]==B.Bedrock),"NE: dolny i górny bedrock");
        Check(chunk.Light.All(l=>(l>>4)==0),"NE: brak światła nieba w chunku");
        nether.SetBlock(8,127,8,B.Air); nether.SetBlock(8,126,8,B.Air);
        Check(nether.GetSkyLight(8,128,8)==0&&nether.GetSkyLight(8,126,8)==0,"NE: odsłonięcie sufitu nie tworzy skylight");
        Check(Math.Abs(nether.Provider.LightBrightnessTable[0]-.1f)<.0001,"NE: ambient Netheru 0.1");
        long day=nether.DayTime; nether.Tick(); Check(nether.DayTime==day,"NE: brak cyklu dnia");
        Check(end.GenerateChunk(0,0).Blocks.Contains(B.EndStone),"NE: centralna wyspa Endu");
        var p=new Player(w);p.SetPositionAndPrev(0,8,0);w.AddEntityNow(p);
        dimensions.Transfer(p,DimensionId.End,100,49,0);
        Check(p.World==end&&!w.Players.Contains(p)&&end.Players.Contains(p)&&!w.EntityById.Values.Contains(p),"NE: przeniesienie usuwa stare indeksy gracza");
        Check(p.ToData().Dimension==1,"NE: zapis wymiaru gracza");
        dimensions.Transfer(p,DimensionId.Overworld,.5,8,.5);
        Check(p.World==w&&!end.Players.Contains(p),"NE: powrót do Overworldu");
        var drops=new List<ItemStack>();
        for(int i=0;i<50;i++) { drops.Clear();Block.ById[B.Glowstone].GetDrops(w,0,8,0,0,w.Rand,null,drops);Check(drops.Count==1&&drops[0].Id==I.GlowstoneDust&&drops[0].Count is >=2 and <=4,"NE: drop pyłu jasnogłazu"); }
    }
}
