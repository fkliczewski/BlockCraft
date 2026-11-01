using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public partial class Player
{
    public FishingHook FishHook;
}

/// <summary>EntityFishHook 1.8: rzut, zahaczenie, fazy brania, wypór i zwijanie po stronie hosta.</summary>
public sealed class FishingHook : Entity
{
    public override string TypeKey => "fishing_hook";
    public Player Angler;
    public Entity Caught;
    private int _anglerId, _groundAge, _groundX, _groundY, _groundZ;
    private byte _groundBlock;
    public bool InGround;
    public int Catchable, WaitTicks, ApproachTicks;

    public FishingHook(World.World w):base(w){Width=Height=.25f;StepHeight=0;}
    public FishingHook(Player p):this(p.World)
    {
        Angler=p;p.FishHook=this;
        float yaw=p.Yaw*MathUtil.Deg2Rad,pitch=p.Pitch*MathUtil.Deg2Rad;
        SetPositionAndPrev(p.X-Math.Cos(yaw)*.16,p.Y+p.EyeHeight-.1,p.Z-Math.Sin(yaw)*.16);
        MotionX=(-Math.Sin(yaw)*Math.Cos(pitch)+World.Rand.NextGaussian()*.0075)*1.5;
        MotionY=(-Math.Sin(pitch)+World.Rand.NextGaussian()*.0075)*1.5;
        MotionZ=(Math.Cos(yaw)*Math.Cos(pitch)+World.Rand.NextGaussian()*.0075)*1.5;
    }
    public static bool Use(Player p,ItemStack rod)
    {
        if(p.World.IsRemote){p.SwingArm();return true;}
        if(p.FishHook is {Removed:false} hook) p.DamageHeld(hook.Retract());
        else
        {
            p.World.PlaySound("random.bow",p.X,p.Y,p.Z,.5f,.4f/(p.World.Rand.NextFloat()*.4f+.8f));
            p.World.SpawnEntity(new FishingHook(p));
        }
        p.SwingArm();return true;
    }
    public override int NetData=>Angler?.Id??_anglerId;
    public override void SetNetData(int value){_anglerId=value;ResolveAngler();}
    public void ResolveAngler()
    {
        Angler??=World.Entities.OfType<Player>().FirstOrDefault(p=>p.Id==_anglerId);
        if(Angler!=null&&!Removed) Angler.FishHook=this;
    }
    private void RemoveHook(){if(Angler?.FishHook==this)Angler.FishHook=null;Kill();}
    public double Submerged()
    {
        var bb=BoundingBox;double result=0;
        for(int j=0;j<5;j++)
        {
            double y0=bb.MinY+(bb.MaxY-bb.MinY)*j/5.0,y1=bb.MinY+(bb.MaxY-bb.MinY)*(j+1)/5.0;
            if(World.IsAnyLiquid(new AABB(bb.MinX,y0,bb.MinZ,bb.MaxX,y1,bb.MaxZ),false)) result+=.2;
        }
        return result;
    }
    public override void Tick()
    {
        if(IsRemoteControlled){base.Tick();ResolveAngler();return;}
        PrevX=X;PrevY=Y;PrevZ=Z;Age++;
        if(Angler==null||Angler.Dead||Angler.Removed||Angler.World!=World||Angler.Inventory.Selected?.Id!=I.FishingRod||DistanceSq(Angler)>1024)
        {RemoveHook();return;}
        if(Caught!=null)
        {
            if(!Caught.Removed&&!Caught.Dead){SetPosition(Caught.X,Caught.Y+Caught.Height*.8,Caught.Z);return;}
            Caught=null;
        }
        if(InGround)
        {
            if(World.GetBlock(_groundX,_groundY,_groundZ)==_groundBlock){if(++_groundAge>=1200)RemoveHook();return;}
            InGround=false;MotionX*=World.Rand.NextFloat()*.2;MotionY*=World.Rand.NextFloat()*.2;MotionZ*=World.Rand.NextFloat()*.2;_groundAge=0;
        }
        double speed=Math.Sqrt(MotionX*MotionX+MotionY*MotionY+MotionZ*MotionZ);
        if(speed>1e-8)
        {
            var block=World.RayCast(X,Y,Z,MotionX/speed,MotionY/speed,MotionZ/speed,speed);
            double closest=block.Hit?block.Distance:speed;
            foreach(var e in World.GetEntitiesInBox(BoundingBox.Grow(speed+1),this))
            {
                if(!e.CanBeCollidedWith||e==Angler&&Age<5)continue;
                if(e.BoundingBox.Grow(.3).RayIntersect(X,Y,Z,MotionX/speed,MotionY/speed,MotionZ/speed,out double t,out _)&&t>=0&&t<closest)
                {closest=t;Caught=e;}
            }
            if(Caught!=null)
            {
                if(!Caught.Attack(new DamageSource("thrown",this,Angler),0))Caught=null;
                else return;
            }
            if(block.Hit)
            {
                _groundX=block.X;_groundY=block.Y;_groundZ=block.Z;_groundBlock=World.GetBlock(block.X,block.Y,block.Z);
                SetPosition(block.HitX,block.HitY,block.HitZ);InGround=true;return;
            }
        }
        MoveWithCollision(MotionX,MotionY,MotionZ);
        double submerged=Submerged();
        if(submerged>0) TickBite();
        MotionY+=.04*(submerged*2-1);
        double drag=OnGround||CollidedH ? .5 : .92;
        if(submerged>0){drag*=.9;MotionY*=.8;}
        MotionX*=drag;MotionY*=drag;MotionZ*=drag;
    }
    public void TickBite()
    {
        var r=World.Rand;
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y)+1,z=MathUtil.FloorToInt(Z);
        int progress=1;
        if(r.NextFloat()<.25f&&WorldHooks.IsRainingAt?.Invoke(World,x,y,z)==true)progress++;
        if(r.NextFloat()<.5f&&World.GetHeight(x,z)>y)progress--;
        if(Catchable>0)
        {
            Catchable--;
            MotionY-=r.NextFloat()*r.NextFloat()*r.NextFloat()*.2;
            if(Catchable==0){WaitTicks=ApproachTicks=0;}
        }
        else if(ApproachTicks>0)
        {
            ApproachTicks-=progress;
            if(Age%3==0)World.SpawnParticles(ParticleKind.Bubble,X,Y+.1,Z,1);
            if(ApproachTicks<=0)
            {
                MotionY-=.2;Catchable=10+r.Next(21);
                World.PlaySound("random.splash",X,Y,Z,.25f,1+(r.NextFloat()-r.NextFloat())*.4f);
                World.SpawnParticles(ParticleKind.Splash,X,Y+.5,Z,6);World.SpawnParticles(ParticleKind.Bubble,X,Y+.5,Z,6);
            }
        }
        else if(WaitTicks>0)
        {
            WaitTicks-=progress;
            if(WaitTicks<=0)ApproachTicks=20+r.Next(61);
        }
        else WaitTicks=100+r.Next(801)-EnchantHelper.GetLevel(Angler.Inventory.Selected,Ench.Lure)*100;
    }
    public int Retract()
    {
        if(World.IsRemote||Removed||Angler==null)return 0;
        int damage=0;
        void Pull(Entity e)
        {
            double dx=Angler.X-X,dy=Angler.Y-Y,dz=Angler.Z-Z;
            double distance=Math.Sqrt(dx*dx+dy*dy+dz*dz);
            e.MotionX+=dx*.1;e.MotionY+=dy*.1+Math.Sqrt(distance)*.08;e.MotionZ+=dz*.1;
        }
        if(Caught!=null){Pull(Caught);damage=3;}
        else if(Catchable>0)
        {
            var stack=FishingLoot.Roll(World.Rand,Angler.Inventory.Selected,out string category);
            var item=new ItemEntity(World,X,Y,Z,stack);Pull(item);World.SpawnEntity(item);
            World.SpawnXp(Angler.X,Angler.Y+.5,Angler.Z+.5,1+World.Rand.Next(6));
            string key=category switch {"junk"=>"stat.junkFished","treasure"=>"stat.treasureFished",_=>"stat.fishCaught"};
            Angler.Statistics[key]=Angler.Statistics.GetValueOrDefault(key)+1;
            damage=1;
        }
        if(InGround)damage=2;
        RemoveHook();return damage;
    }
}

public static class FishingLoot
{
    private sealed record Entry(int Id,int Weight,int Count=1,int Meta=0,float Damage=0,bool Enchanted=false);
    private static readonly Entry[] Junk={new(I.LeatherBoots,10,Damage:.9f),new(I.Leather,10),new(I.Bone,10),new(I.Potion,10),new(I.String,5),new(I.FishingRod,2,Damage:.9f),new(I.Bowl,10),new(I.Stick,5),new(I.Dye,1,10),new(B.TripwireHook,10),new(I.RottenFlesh,10)};
    private static readonly Entry[] Treasure={new(B.LilyPad,1),new(I.NameTag,1),new(I.Saddle,1),new(I.Bow,1,Damage:.25f,Enchanted:true),new(I.FishingRod,1,Damage:.25f,Enchanted:true),new(I.Book,1,Enchanted:true)};
    private static readonly Entry[] Fish={new(I.RawFish,60),new(I.RawFish,25,Meta:1),new(I.RawFish,2,Meta:2),new(I.RawFish,13,Meta:3)};
    public static (float junk,float treasure) Chances(int luck,int lure)=>(Math.Clamp(.1f-luck*.025f-lure*.01f,0,1),Math.Clamp(.05f+luck*.01f-lure*.01f,0,1));
    public static ItemStack Roll(Rng r,ItemStack rod,out string category)
    {
        var chances=Chances(EnchantHelper.GetLevel(rod,Ench.LuckOfTheSea),EnchantHelper.GetLevel(rod,Ench.Lure));
        float f=r.NextFloat();Entry[] table;
        if(f<chances.junk){category="junk";table=Junk;}
        else if(f-chances.junk<chances.treasure){category="treasure";table=Treasure;}
        else{category="fish";table=Fish;}
        int pick=r.Next(table.Sum(e=>e.Weight));Entry chosen=table[^1];
        foreach(var e in table){pick-=e.Weight;if(pick<0){chosen=e;break;}}
        var stack=new ItemStack(chosen.Id,chosen.Count,chosen.Meta);
        if(chosen.Damage>0&&stack.Item?.MaxDamage>0)
        {
            int max=stack.Item.MaxDamage;
            int limit=(int)(max*chosen.Damage);
            stack.Damage=Math.Clamp(max-r.Next(r.Next(limit)+1),1,limit);
        }
        if(chosen.Enchanted)
        {
            var random=new JRandom((long)(r.NextDouble()*long.MaxValue));
            var ench=EnchantingRules.BuildList(random,stack,30);
            if(stack.Id==I.Book){stack.Id=I.EnchantedBook;foreach(var e in ench)EnchantingRules.AddStored(stack,e);}
            else foreach(var e in ench)EnchantingRules.AddEnchantment(stack,e);
        }
        return stack;
    }
}
