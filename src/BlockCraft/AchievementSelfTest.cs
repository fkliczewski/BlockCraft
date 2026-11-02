using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;
using System.Text.Json;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestAchievements()
    {
        Check(AchievementList.All.Length==34&&AchievementList.All.Select(a=>a.Id).Distinct().Count()==34,
            "Osiągnięcia: komplet 34 unikalnych węzłów 1.8");
        foreach(var a in AchievementList.All)
        {
            var seen=new HashSet<string>();var cur=a;
            while(cur!=null&&seen.Add(cur.Id)) cur=AchievementList.Find(cur.Parent);
            Check(cur==null&&(a.Parent==null||AchievementList.Find(a.Parent)!=null)&&(Item.ById[a.Icon]!=null||AchievementList.IconTexture(a.Icon)!=null),
                "Osiągnięcie: poprawne drzewo i ikona "+a.Id);
        }
        var w=MakeWorld(31,WorldType.Superflat,1);w.Rules.DoMobSpawning=false;
        var p=new Player(w);p.SetPositionAndPrev(8,4,8);w.AddEntityNow(p);
        Check(!p.AwardAchievement("diamonds")&&!p.AwardAchievement("nieistnieje"),"Osiągnięcia: wymagany rodzic, nieznany klucz odrzucony");
        p.AwardAchievement("openInventory");
        Check(!p.AwardAchievement("openInventory")&&p.AchievementNotices.Count==1,"Osiągnięcia: popup tylko raz");
        var drop=new ItemEntity(w,8,4,8,new ItemStack(B.BirchLog));drop.OnCollideWithPlayer(p);
        p.OnCrafted(new ItemStack(B.CraftingTable));p.OnCrafted(new ItemStack(I.StonePickaxe));
        Check(p.Achievements.Contains("mineWood")&&p.Achievements.Contains("buildWorkBench")
            &&p.Achievements.Contains("buildPickaxe")&&p.Achievements.Contains("buildBetterPickaxe"),
            "Osiągnięcia: podniesienie drewna i crafting lepszego kilofa daje obie nagrody");
        p.OnCrafted(new ItemStack(B.Furnace));
        var furnace=new FurnaceTileEntity();var output=new FurnaceOutputSlot(furnace,0,0);
        output.OnTake(p,new ItemStack(I.IronIngot));
        Check(p.Achievements.Contains("acquireIron"),"Osiągnięcia: żelazo z odbioru pieca, niezależnie od pozostałego XP");
        var diamond=new ItemEntity(w,8,4,8,new ItemStack(I.Diamond));diamond.OnCollideWithPlayer(p);
        p.OnCrafted(new ItemStack(I.WoodSword));
        var zombie=new Zombie(w);zombie.OnDeath(DamageSource.PlayerAttack(p));
        Check(p.Achievements.Contains("diamonds")&&p.Achievements.Contains("killEnemy"),"Osiągnięcia: podniesiony diament i zabity potwór");
        var skeleton=new Skeleton(w);skeleton.SetPositionAndPrev(58,4,8);skeleton.OnDeath(DamageSource.Arrow(null,p));
        Check(p.Achievements.Contains("snipeSkeleton"),"Osiągnięcia: szkielet zabity strzałą z 50 metrów");
        var other=new Player(w);p.DropStack(new ItemStack(I.Diamond),false);
        var gift=w.Entities.OfType<ItemEntity>().LastOrDefault(e=>e.Thrower==p);
        // DropStack queues entity creation; use the same pickup path without waiting for physics.
        gift ??=new ItemEntity(w,8,4,8,new ItemStack(I.Diamond)){Thrower=p};gift.PickupDelay=0;gift.OnCollideWithPlayer(other);
        Check(p.Achievements.Contains("diamondsToYou")&&!other.Achievements.Contains("diamondsToYou"),"Osiągnięcia: darczyńca diamentu otrzymuje nagrodę");
        p.Achievements.Remove("diamondsToYou");
        for(int i=0;i<36;i++)other.Inventory.Main[i]=new ItemStack(B.Cobblestone,64);
        other.Inventory.Main[0]=new ItemStack(I.Diamond,63);
        var partialGift=new ItemEntity(w,8,4,8,new ItemStack(I.Diamond,2)){Thrower=p};partialGift.OnCollideWithPlayer(other);
        Check(partialGift.Stack.Count==1&&p.Achievements.Contains("diamondsToYou"),"Osiągnięcia: częściowe podniesienie stosu też nagradza darczyńcę");
        var json=JsonSerializer.Serialize(p.ToData());var restored=new Player(w);
        restored.FromData(JsonSerializer.Deserialize<PlayerData>(json));
        Check(restored.AchievementBits==p.AchievementBits&&restored.AchievementNotices.Count==0,"Osiągnięcia: zapis JSON i odczyt bez odtwarzania popupów");
        var remoteWorld=new World.World(1,WorldType.Superflat,true);var remote=new Player(remoteWorld);
        Check(!remote.AwardAchievement("openInventory"),"Osiągnięcia: klient nie przyznaje sobie nagród rozgrywki");
        remote.ReceiveAchievements(p.AchievementBits);
        Check(remote.AchievementBits==p.AchievementBits&&remote.AchievementNotices.Count==0,"Osiągnięcia: pierwsza synchronizacja bez historycznych popupów");
        p.OnCrafted(new ItemStack(I.GoldenApple,1,1));remote.ReceiveAchievements(p.AchievementBits);
        Check(remote.AchievementNotices.Count==1&&remote.AchievementNotices.Peek()=="overpowered","Osiągnięcia: zmiana od hosta wyświetla jedną nową nagrodę");
        remote.ReceiveAchievements(p.AchievementBits);
        Check(remote.AchievementNotices.Count==1,"Osiągnięcia: ponowiony pakiet nie powiela popupu");
        for(int l=1;l<=4;l++)for(int x=-l;x<=l;x++)for(int z=-l;z<=l;z++)w.SetBlock(8+x,10-l,8+z,B.IronBlock);
        w.SetBlock(8,10,8,B.Beacon);
        Check(AchievementList.FullBeacon(w,8,10,8),"Osiągnięcia: pełna piramida latarni");
        w.SetBlock(4,6,4,B.Air);
        Check(!AchievementList.FullBeacon(w,8,10,8),"Osiągnięcia: brak bloku w dolnej warstwie wyklucza pełną latarnię");
    }
}
