using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestFishing()
    {
        var w=MakeWorld(35,WorldType.Superflat,1);w.Rules.DoMobSpawning=false;
        var p=new Player(w);p.SetPositionAndPrev(8.5,4,8.5);w.AddEntityNow(p);
        p.Inventory.Selected=new ItemStack(I.FishingRod);
        var rod=p.Inventory.Selected;
        Check(Item.ById[I.FishingRod].UseInAir(p,rod)&&p.FishHook!=null&&rod.Damage==0,"Wędka: rzut tworzy spławik bez zużycia");
        var cast=p.FishHook;
        Check(Math.Abs(Math.Sqrt(cast.MotionX*cast.MotionX+cast.MotionY*cast.MotionY+cast.MotionZ*cast.MotionZ)-1.5)<.08,"Wędka: prędkość rzutu 1.5 z rozrzutem");
        Check(cast.Retract()==0&&cast.Removed&&p.FishHook==null,"Wędka: pusty zwój nie zużywa i usuwa powiązanie");
        Check(cast.Retract()==0,"Wędka: ponowne zwinięcie nie daje łupów");
        var ground=new FishingHook(p);ground.SetPositionAndPrev(8.5,4.2,8.5);ground.MotionX=ground.MotionZ=0;ground.MotionY=-1;
        ground.Tick();Check(ground.InGround&&ground.Retract()==2,"Wędka: zahaczenie podłoża kosztuje 2 trwałości");
        var hook=new FishingHook(p);hook.SetPositionAndPrev(10,4,8);hook.Caught=new Cow(w);hook.Caught.SetPositionAndPrev(10,4,8);
        var cow=hook.Caught;
        Check(hook.Retract()==3&&cow.MotionX<0&&cow.MotionY>0,"Wędka: holowanie encji w stronę gracza kosztuje 3");
        hook=new FishingHook(p);hook.Catchable=15;hook.SetPositionAndPrev(8.5,6.5,8.5);
        Check(hook.Retract()==1&&p.Statistics.Where(kv=>kv.Key is "stat.fishCaught" or "stat.junkFished" or "stat.treasureFished").Sum(kv=>kv.Value)==1,
            "Wędka: branie daje łup, XP i statystykę, koszt 1");
        var old=new FishingHook(p);p.Inventory.Selected=new ItemStack(I.Stick);old.Tick();
        Check(old.Removed&&p.FishHook==null,"Wędka: odłożenie wędki usuwa spławik");
        p.Inventory.Selected=rod;old=new FishingHook(p);old.SetPositionAndPrev(p.X+33,p.Y,p.Z);old.Tick();
        Check(old.Removed,"Wędka: żyłka zrywa się poza 32 blokami");
        var chances=FishingLoot.Chances(3,3);
        Check(Math.Abs(chances.junk-.0f)<.0001&&Math.Abs(chances.treasure-.05f)<.0001,"Wędka: szanse 1.8 z Morską fortuną III i Przynętą III");
        var rng=new Rng(42);int junk=0,treasure=0,fish=0;bool valid=true,enchanted=false;var types=new HashSet<int>();
        for(int i=0;i<20000;i++)
        {
            var s=FishingLoot.Roll(rng,rod,out string kind);valid &= s?.Item!=null&&s.Count>0;
            if(kind=="junk")junk++;else if(kind=="treasure")treasure++;else{fish++;types.Add(s.Damage);}
            if(kind=="treasure"&&s.Id is I.Bow or I.FishingRod or I.EnchantedBook)enchanted|=EnchantHelper.IsEnchanted(s);
        }
        Check(valid&&types.Count==4&&enchanted,"Wędka: wszystkie łupy mają rejestracje, cztery ryby i zaklęte skarby");
        Check(junk is >1700 and <2300&&treasure is >800 and <1200&&fish is >16500 and <17500,"Wędka: rozkład bez zaklęć około 10/5/85 procent",$"{junk}/{treasure}/{fish}");
        for(int x=3;x<=13;x++)for(int z=3;z<=13;z++)for(int y=4;y<=6;y++)w.SetBlock(x,y,z,B.Water);
        hook=new FishingHook(p);hook.SetPositionAndPrev(8.5,6.85,8.5);hook.MotionX=hook.MotionY=hook.MotionZ=0;
        int ticks=0;while(hook.Catchable==0&&ticks++<1400)hook.Tick();
        Check(!hook.Removed&&hook.Catchable>0&&ticks<1400&&hook.Y>5.5&&hook.Y<7.5,"Wędka: fizyka utrzymuje spławik i naturalne branie dochodzi do okna połowu",$"ticks={ticks}, y={hook.Y:F2}");
        var remoteWorld=new World.World(1,WorldType.Superflat,true);var remote=new Player(remoteWorld);remote.Id=99;remoteWorld.AddEntityNow(remote);
        var remoteHook=new FishingHook(remoteWorld);remoteHook.SetNetData(remote.Id);
        Check(remoteHook.Angler==remote&&remote.FishHook==remoteHook,"Wędka: identyfikator sieciowy wiąże żyłkę z graczem");
        p.Inventory.Selected=rod;var busy=new FishingHook(p){Catchable=10};FishingHook.Use(p,rod);
        Check(busy.Removed&&rod.Damage==1,"Wędka: zwinięcie przez przedmiot rozlicza trwałość");
    }
}
