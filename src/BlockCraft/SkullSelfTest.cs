using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestSkulls()
    {
        Check(Item.ById[I.SkullItem].Variants.SequenceEqual(new[]{0,1,2,3,4})&&Item.ById[I.SkullItem].ArmorSlot==0,
            "Czaszki: pięć typów i możliwość noszenia na głowie");
        var w=MakeWorld(37,WorldType.Superflat,1);w.Rules.DoMobSpawning=false;
        var p=new Player(w);p.SetPositionAndPrev(8.5,4,12.5);p.Yaw=90;w.AddEntityNow(p);
        var held=new ItemStack(I.SkullItem,2,1);p.Inventory.Selected=held;
        Check(Skulls.Place(p,held,8,3,9,Dir.Up)&&w.GetMeta(8,4,9)==Dir.Up
            &&w.GetTileEntity(8,4,9) is SkullTile{SkullType:1,Rotation:4}&&held.Count==1,
            "Czaszki: stawianie na podłodze, obrót bez przesunięcia o 180 stopni, koszt 1");
        Check(Skulls.IsWither(w,8,4,9),"Czaszki: rozpoznanie czaszki Witherowego szkieleta z TE");
        for(int type=0;type<5;type++)
        {
            var s=new ItemStack(I.SkullItem,1,type);p.Inventory.Selected=s;
            w.SetBlock(3+type*2,4,5,B.Stone);
            Check(Skulls.Place(p,s,3+type*2,4,5,Dir.South)&&w.GetMeta(3+type*2,4,6)==Dir.South
                &&w.GetTileEntity(3+type*2,4,6) is SkullTile tile&&tile.SkullType==type,
                "Czaszki: montaż na ścianie, typ "+type);
        }
        Check(!Skulls.Place(p,new ItemStack(I.SkullItem),8,3,8,Dir.Down),"Czaszki: brak montażu pod sufitem");
        w.SetBlock(8,3,9,B.Air);w.Tick();
        Check(w.GetBlock(8,4,9)==B.Skull,"Czaszki: po postawieniu mogą wisieć bez podparcia (1.8)");
        var playerHead=new ItemStack(I.SkullItem,1,3);
        var owner=new Nbt{["Name"]="Filip",["Id"]="test-id",["Properties"]=new Nbt{["textures"]=new List<object>{new Nbt{["Value"]="signed-texture-data"}}}};
        playerHead.GetOrCreateTag()["SkullOwner"]=owner;
        var profile=new SkullTile();profile.SetFromItem(playerHead,13);
        var loaded=ChunkCodec.LoadTile(64,8,4,9,ChunkCodec.SaveTile(profile)) as SkullTile;
        Check(loaded is {SkullType:3,Rotation:13}&&Nbt.Same(loaded.Owner,owner)&&Nbt.Same(loaded.ToItem().Tag.GetCompound("SkullOwner"),owner),
            "Czaszki: profil właściciela, tekstury i obrót przechodzą przez zapis/format sieciowy");
        owner["Name"]="Zmieniony";
        Check(profile.Owner.GetString("Name")=="Filip"&&Skulls.Name(profile.ToItem()).Contains("Filip"),"Czaszki: profil jest kopią, nazwa przedmiotu wskazuje właściciela");
        var legacy=new ItemStack(I.SkullItem,1,3);legacy.GetOrCreateTag()["SkullOwner"]="Steve";profile.SetFromItem(legacy,0);
        Check(profile.Owner.GetString("Name")=="Steve","Czaszki: obsługa starego SkullOwner jako tekstu");
        w.SetBlock(14,4,6,B.Skull,Dir.Up);w.SetTileEntity(14,4,6,loaded);w.BreakBlock(14,4,6,true);w.Tick();
        Check(w.GetTileEntity(14,4,6)==null&&w.Entities.OfType<ItemEntity>().Any(e=>e.Stack.Id==I.SkullItem&&e.Stack.Damage==3&&Skulls.Name(e.Stack).Contains("Filip")),
            "Czaszki: zniszczenie zachowuje typ i profil oraz usuwa TE");
        var before=w.Entities.OfType<ItemEntity>().Count(e=>e.Stack.Id==I.SkullItem);
        w.SetBlock(14,4,8,B.Skull,Dir.Up|8);w.BreakBlock(14,4,8,true);w.Tick();
        Check(w.Entities.OfType<ItemEntity>().Count(e=>e.Stack.Id==I.SkullItem)==before,"Czaszki: flaga bez dropu jest respektowana");
        foreach(bool alongX in new[]{true,false})
        {
            var sw=MakeWorld(alongX?39:41,WorldType.Superflat,1);sw.Rules.DoMobSpawning=false;sw.Difficulty=Difficulty.Normal;
            var summoner=new Player(sw);summoner.SetPositionAndPrev(8,4,13);sw.AddEntityNow(summoner);
            for(int i=0;i<3;i++)
            {
                int x=alongX?6+i:7,z=alongX?8:7+i;
                sw.SetBlock(x,5,z,B.SoulSand);if(i==1)sw.SetBlock(x,4,z,B.SoulSand);
                Skulls.Place(summoner,new ItemStack(I.SkullItem,1,i==1?0:1),x,5,z,Dir.Up);
            }
            Check(Wither.TrySummon(sw,7,6,8)==null,"Czaszki: zwykły szkielet w środku nie przywołuje Withera, oś "+alongX);
            int mx=7,mz=8;
            sw.SetBlock(mx,6,mz,B.Air);
            var last=new ItemStack(I.SkullItem,2,1);summoner.Inventory.Selected=last;
            Skulls.Place(summoner,last,mx,5,mz,Dir.Up);sw.Tick();
            Check(sw.Entities.OfType<Wither>().Count()==1&&sw.GetBlock(7,5,8)==B.Air&&sw.GetTileEntity(7,6,8)==null&&last.Count==1,
                "Czaszki: ostatnia właściwa czaszka automatycznie przywołuje jednego Withera i zużywa wzór, oś "+alongX);
        }
        var killer=new Creeper(w){Charged=true};
        var ws=new WitherSkeleton(w);ws.OnDeath(new DamageSource("explosion",killer,killer){IsExplosion=true});w.Tick();
        Check(killer.HeadsDropped==1&&w.Entities.OfType<ItemEntity>().Any(e=>e.Stack.Id==I.SkullItem&&e.Stack.Damage==1),"Czaszki: naładowany creeper daje właściwą głowę Witherowego szkieleta");
        var second=new Skeleton(w);second.OnDeath(new DamageSource("explosion",killer,killer){IsExplosion=true});
        Check(killer.HeadsDropped==1,"Czaszki: jeden naładowany creeper upuszcza najwyżej jedną głowę");
        // Vanilla afw.a(wh) runs the normal rare drop before the extra charged-creeper head.
        // Use an out-of-range test enchantment to force the rare branch deterministically.
        var looter=new Player(w);looter.Inventory.Selected=new ItemStack(I.DiamondSword);
        EnchantingRules.AddEnchantment(looter.Inventory.Selected,new EnchantmentRoll(Ench.Looting,100));
        int skullsBefore=w.Entities.OfType<ItemEntity>().Where(e=>e.Stack.Id==I.SkullItem&&e.Stack.Damage==1).Sum(e=>e.Stack.Count);
        var charged=new Creeper(w){Charged=true};var rare=new WitherSkeleton(w){RecentlyHitByPlayer=100,LastAttacker=looter};
        rare.OnDeath(new DamageSource("explosion",charged,charged){IsExplosion=true});w.Tick();
        int skullsAfter=w.Entities.OfType<ItemEntity>().Where(e=>e.Stack.Id==I.SkullItem&&e.Stack.Damage==1).Sum(e=>e.Stack.Count);
        Check(skullsAfter-skullsBefore==2&&charged.HeadsDropped==1,"Czaszki: zwykły rzadki drop jest niezależny od dodatkowej głowy naładowanego creepera (1.8)");
    }
}
