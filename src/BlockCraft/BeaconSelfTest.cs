using BlockCraft.Entities;
using BlockCraft.Net;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;
public static partial class SelfTest
{
    private static void TestBeacon()
    {
        var w=MakeWorld(38,WorldType.Superflat,1);w.Rules.DoMobSpawning=false;
        var p=new Player(w);p.SetPositionAndPrev(8.5,10,9.5);w.AddEntityNow(p);
        w.SetBlock(8,10,8,B.Beacon);var t=(BeaconTile)w.GetTileEntity(8,10,8);
        t.Update(w);Check(t.Levels==0&&!t.Active,"Latarnia: bez piramidy nieaktywna");
        p.Achievements.Add("killWither");
        var far=new Player(w);far.SetPositionAndPrev(8,200,8);far.Achievements.Add("killWither");w.AddEntityNow(far);
        for(int layer=1;layer<=4;layer++)
        {
            for(int x=-layer;x<=layer;x++)for(int z=-layer;z<=layer;z++)w.SetBlock(8+x,10-layer,8+z,layer%2==0?B.IronBlock:B.EmeraldBlock);
            t.Update(w);Check(t.Levels==layer&&t.Active,"Latarnia: mieszana piramida poziom "+layer);
        }
        Check(p.Achievements.Contains("fullBeacon")&&!far.Achievements.Contains("fullBeacon"),"Latarnia: osiągnięcie tylko przy przejściu do czterech warstw w obszarze 10/5/10");
        var c=new BeaconContainer(p,w,t);
        p.GameMode=1;
        Check(!c.Confirm(p,Effect.Speed,Effect.Regeneration),"Latarnia: nawet creative potrzebuje zapłaty");
        t.Payment.Set(0,new ItemStack(I.Diamond));
        Check(!c.Confirm(p,Effect.Poison,Effect.None)&&t.Payment.Get(0)!=null,"Latarnia: odrzucenie sfałszowanego efektu bez utraty opłaty");
        Check(!c.Confirm(p,Effect.Haste,Effect.Strength),"Latarnia: odrzucenie niedozwolonego drugiego efektu");
        Check(c.Confirm(p,Effect.Speed,Effect.Speed)&&t.Payment.Get(0)==null,"Latarnia: zapłata i wybór poziomu II");
        t.Update(w);Check(p.Effects.GetValueOrDefault(Effect.Speed) is {Duration:180,Amplifier:1,Ambient:true},"Latarnia: efekt II na 180 ticków, ambient");
        p.SetPositionAndPrev(8.5,200,8.5);t.Update(w);Check(p.Effects.GetValueOrDefault(Effect.Speed)?.Duration==180,"Latarnia: zasięg ponad piramidą aż do nieba");
        p.RemoveEffect(Effect.Speed);p.SetPositionAndPrev(70,10,8.5);t.Update(w);
        Check(!p.HasEffect(Effect.Speed),"Latarnia: poza promieniem 50 brak efektu");
        p.SetPositionAndPrev(8.5,10,9.5);t.Payment.Set(0,new ItemStack(I.IronIngot));
        Check(c.Confirm(p,Effect.Haste,Effect.Regeneration),"Latarnia: oddzielna regeneracja na pełnej piramidzie");
        t.Update(w);Check(p.HasEffect(Effect.Haste)&&p.HasEffect(Effect.Regeneration),"Latarnia: dwa efekty jednocześnie");
        w.SetBlock(8,12,8,B.Glass);t.Rebuild(w);Check(t.Active,"Latarnia: zwykłe szkło przepuszcza promień");
        w.SetBlock(8,12,8,B.StainedGlass,14);w.SetBlock(8,14,8,B.StainedGlassPane,11);t.Rebuild(w);
        Check(t.Active&&t.Beam.Count==3&&t.Beam[1].R>t.Beam[1].B&&t.Beam[2].B>t.Beam[1].B,"Latarnia: barwione szkło i panel mieszają promień");
        w.SetBlock(8,16,8,B.Stone);t.Rebuild(w);Check(!t.Active&&t.Levels==0&&t.Beam.Count==0,"Latarnia: nieprzezroczysty dach wyłącza latarnię");
        w.SetBlock(8,16,8,B.Air);w.SetBlock(4,6,4,B.Air);t.Rebuild(w);
        Check(t.Levels==3,"Latarnia: dziura w czwartej warstwie obniża moc do 3");
        t.Payment.Set(0,new ItemStack(I.GoldIngot));Check(!c.Confirm(p,Effect.Haste,Effect.Regeneration),"Latarnia: regeneracja wymaga czterech warstw");
        Check(c.Confirm(p,Effect.Strength,Effect.None),"Latarnia: siła dostępna przy trzech warstwach");
        var saved=ChunkCodec.LoadTile(65,8,10,8,ChunkCodec.SaveTile(t)) as BeaconTile;
        Check(saved is {Levels:3,Primary:Effect.Strength,Secondary:Effect.None},"Latarnia: zapis i odczyt TE/sieci");
        t.Payment.Set(0,new ItemStack(I.Emerald));p.SetPositionAndPrev(80,10,8);
        Check(!c.Confirm(p,Effect.Speed,Effect.None)&&t.Payment.Get(0)!=null,"Latarnia: zdalna płatność poza zasięgiem odrzucona");
        p.SetPositionAndPrev(8.5,10,9.5);p.RemoveEffect(Effect.Strength);t.Payment.Set(0,null);t.Primary=Effect.Strength;
        w.TotalTime=79;t.Tick(w);Check(!p.HasEffect(Effect.Strength),"Latarnia: brak odświeżenia pomiędzy okresami 80 ticków");
        w.TotalTime=80;t.Tick(w);Check(p.Effects.GetValueOrDefault(Effect.Strength) is {Duration:180},"Latarnia: odświeżanie co 80 ticków");
        w.SetBlock(8,10,8,B.Air);Check(w.GetTileEntity(8,10,8)==null&&!c.StillValid(p),"Latarnia: usunięcie bloku zamyka GUI");
    }
}
