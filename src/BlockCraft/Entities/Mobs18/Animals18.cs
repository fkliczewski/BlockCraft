using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public static partial class Registries
{
    static partial void RegisterAnimals18()
    {
        Mob("wolf",w=>new Wolf(w)); Mob("ocelot",w=>new Ocelot(w)); Mob("rabbit",w=>new Rabbit(w));
        Mob("bat",w=>new Bat(w)); Mob("squid",w=>new Squid(w)); Mob("mooshroom",w=>new Mooshroom(w));
        Mob("iron_golem",w=>new IronGolem(w)); Mob("snow_golem",w=>new SnowGolem(w));
        Mob("horse",w=>new Horse(w)); Mob("donkey",w=>new Horse(w,1)); Mob("mule",w=>new Horse(w,2));
        Mob("zombie_horse",w=>new Horse(w,3)); Mob("skeleton_horse",w=>new Horse(w,4));
        Mob("killer_bunny",w=>new Rabbit(w){RabbitType=99});
        Entity("leash_knot",w=>new LeashKnot(w)); Entity("golem_snowball",w=>new GolemSnowball(w));
        RegisterVillagers18();
    }
    static partial void RegisterVillagers18();
}

/// <summary>Temperatury biomów 1.8 (BiomeGenBase.temperature) z poprawką na wysokość.</summary>
public static class BiomeInfo
{
    private static readonly float[] Temp = {
        .5f,.8f,2f,.2f,.7f,.25f,.8f,.5f,2f,.5f, 0f,0f,0f,0f,.9f,.9f,.8f,2f,.7f,.25f,
        .2f,.95f,.95f,.95f,.5f,.2f,.05f,.6f,.6f,.7f, -.5f,-.5f,.3f,.3f,.2f,1.2f,1f,2f,2f,2f };
    public static float Temperature(int biome,int y=64)
    {
        int b=biome>=128?biome-128:biome;
        float t=b>=0&&b<Temp.Length?Temp[b]:.8f;
        if(biome==140) t=0; else if(biome==163) t=1.1f;
        return y>64?t-(y-64)*.05f/30f:t;
    }
    public static bool IsSnowy(int biome)=>Temperature(biome)<.15f;
    public static bool IsDesert(int biome)=>biome is 2 or 17 or 130;
}

public abstract class Tameable : Animal
{
    public bool Tamed,Sitting;
    public string Owner="";
    protected Tameable(World.World w):base(w) { }
    public Player OwnerPlayer=>string.IsNullOrEmpty(Owner)?null:World.Players.FirstOrDefault(p=>p.Name==Owner);
    public virtual void Tame(Player p) { Tamed=true; Owner=p.Name; PersistenceRequired=true; Nav.Stop(); Target=null; Sitting=true; World.SpawnParticles(ParticleKind.Heart,X,Y+Height,Z,7); }
    public override bool Attack(DamageSource src,float amount) { if(!World.IsRemote) Sitting=false; return base.Attack(src,amount); }
    /// <summary>EntityAIFollowOwner: rusza od startDist, staje przy stopDist, teleportuje się, gdy właściciel jest dalej niż 12 bloków.</summary>
    protected bool FollowOwner(double startDist,double stopDist)
    {
        if(Sitting) { Nav.Stop(); return true; }
        var p=OwnerPlayer;
        if(!Tamed||p==null||p.Spectator||LeashHolder!=null) return false;
        double d=DistanceSq(p);
        if(d<startDist*startDist&&!Nav.HasPath) return false;
        if(d<stopDist*stopDist) { Nav.Stop(); return false; }
        LookTarget=p; LookTime=10;
        if(d>=144) {
            int px=MathUtil.FloorToInt(p.X)-2,pz=MathUtil.FloorToInt(p.Z)-2,py=MathUtil.FloorToInt(p.Y);
            for(int dx=0;dx<=4;dx++) for(int dz=0;dz<=4;dz++)
                if((dx<1||dz<1||dx>3||dz>3)&&Block.Get(World.GetBlock(px+dx,py-1,pz+dz)).Opaque&&PathFinder.IsPassable(World,px+dx,py,pz+dz)&&PathFinder.IsPassable(World,px+dx,py+1,pz+dz))
                { SetPositionAndPrev(px+dx+.5,py,pz+dz+.5); Nav.Stop(); return true; }
        }
        if(Age%10==0) Nav.MoveTo(p.X,p.Y,p.Z,MoveSpeed);
        return true;
    }
    /// <summary>EntityAIOwnerHurtByTarget i EntityAIOwnerHurtTarget.</summary>
    protected LivingEntity OwnerTarget()
    {
        var p=OwnerPlayer; if(p==null) return null;
        if(p.LastAttacker is LivingEntity a&&!a.Dead&&a!=this&&ShouldAttack(a,p)) return a;
        foreach(var e in World.GetEntitiesInBox(p.BoundingBox.Grow(16),p))
            if(e is LivingEntity le&&!le.Dead&&le.LastAttacker==p&&le.LastAttackerTime>90&&ShouldAttack(le,p)) return le;
        return null;
    }
    protected virtual bool ShouldAttack(LivingEntity target,Player owner)
    {
        if(target is Creeper) return false;
        if(target is Tameable t&&t.Tamed&&t.Owner==owner.Name) return false;
        if(target is Horse h&&h.Tamed) return false;
        return target is not Player;
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Tamed); b.Write(Sitting); b.Write(Owner??""); }
    public override void Load(BinaryReader b) { base.Load(b); Tamed=b.ReadBoolean(); Sitting=b.ReadBoolean(); Owner=b.ReadString(); }
    public override int NetData=>base.NetData|(Tamed?2:0)|(Sitting?4:0);
    public override void SetNetData(int d) { base.SetNetData(d); Tamed=(d&2)!=0; Sitting=(d&4)!=0; }
}

public sealed class Wolf : Tameable
{
    public int Collar=14; public bool Angry,Begging; private int _attack;
    public Wolf(World.World w):base(w) { Width=.6f; Height=.8f; MaxHealth=Health=8; MoveSpeed=.3; }
    public override string TypeKey=>"wolf";
    public override string DisplayName=>"Wilk";
    public override string LivingSound=>Angry?"mob.wolf.growl":World.Rand.Next(3)==0?(Tamed&&Health<10?"mob.wolf.whine":"mob.wolf.panting"):"mob.wolf.bark";
    public override string HurtSound=>"mob.wolf.hurt";
    public override string DeathSound=>"mob.wolf.death";
    public override float EyeHeight=>Height*.8f;
    public override bool IsBreedingItem(ItemStack s)=>s!=null&&IsWolfMeat(s.Id);
    public static bool IsWolfMeat(int id)=>id is I.RawBeef or I.Steak or I.RawPorkchop or I.CookedPorkchop or I.RawChicken or I.CookedChicken or I.RawMutton or I.CookedMutton or I.RottenFlesh or I.RawRabbit or I.CookedRabbit;
    public override bool CanMateWith(Animal a)=>a is Wolf w&&w!=this&&Tamed&&w.Tamed&&!Sitting&&!w.Sitting;
    public override Animal CreateChild() { var c=new Wolf(World); if(Tamed) { c.Tamed=true; c.Owner=Owner; c.MaxHealth=c.Health=20; } return c; }
    public override void Tame(Player p) { base.Tame(p); MaxHealth=Health=20; Angry=false; }
    /// <summary>Kąt ogona jak EntityWolf.getTailRotation.</summary>
    public float TailRotation=>Angry?1.5393804f:Tamed?(.55f-(20-Health)*.02f)*MathF.PI:MathF.PI/5f;
    public override bool Interact(Player p)
    {
        var s=p.Inventory.Selected;
        if(Tamed) {
            if(s!=null&&IsWolfMeat(s.Id)&&Health<20) { Heal(s.Item?.Hunger??2); ConsumeHeld(p); return true; }
            if(Dyes.ColorOf(s) is int c&&c>=0&&c!=Collar) { Collar=c; ConsumeHeld(p); return true; }
            if(InteractLead(p)) return true;
            if(p.Name==Owner&&!IsBreedingItem(s)) { if(!World.IsRemote) { Sitting=!Sitting; IsJumping=false; Nav.Stop(); Target=null; } return true; }
        }
        else if(s?.Id==I.Bone&&!Angry) {
            ConsumeHeld(p);
            if(!World.IsRemote) { if(World.Rand.Next(3)==0) Tame(p); else World.SpawnParticles(ParticleKind.Smoke,X,Y+Height,Z,7); }
            return true;
        }
        return base.Interact(p);
    }
    protected override void OnHurt(DamageSource src)
    {
        base.OnHurt(src); PanicTicks=0;
        if(src.Attacker is LivingEntity a&&a!=OwnerPlayer&&!(a is Player pl&&(pl.Creative||pl.Spectator))) {
            Target=a; if(!Tamed) Angry=true;
            foreach(var e in World.GetEntitiesInBox(BoundingBox.Grow(16,10,16),this))
                if(e is Wolf w&&w.Target==null&&w.Tamed==Tamed&&w.Owner==Owner&&!w.Sitting) { w.Target=a; if(!w.Tamed) w.Angry=true; }
        }
    }
    protected override void Think()
    {
        if(_attack>0) _attack--;
        if(Target!=null&&(Target.Dead||Target.Removed||Target is Player tp&&(tp.Creative||tp.Spectator)||DistanceSq(Target)>256)) { Target=null; if(!Tamed&&Angry&&World.Rand.Next(5)==0) Angry=false; }
        if(Sitting) { Nav.Stop(); Target=null; LookAtPlayersRandomly(); return; }
        if(Target==null&&Tamed) Target=OwnerTarget();
        if(Target==null&&!Tamed&&World.Rand.Next(10)==0)
            Target=World.GetEntitiesInBox(BoundingBox.Grow(16,4,16),this).OfType<LivingEntity>().Where(e=>!e.Dead&&(e is Sheep or Rabbit||e is Skeleton)&&CanSee(e)).OrderBy(e=>DistanceSq(e)).FirstOrDefault();
        if(Target!=null) {
            LookTarget=Target; LookTime=10;
            if(Age%10==0||!Nav.HasPath) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed*1.0);
            double d=DistanceSq(Target);
            if(OnGround&&d>4&&d<16&&World.Rand.Next(5)==0) { double dx=Target.X-X,dz=Target.Z-Z,l=Math.Sqrt(dx*dx+dz*dz); MotionX+=dx/l*.4*.8+MotionX*.2; MotionZ+=dz/l*.4*.8+MotionZ*.2; MotionY=.4; }
            if(_attack==0&&d<Width*2*Width*2+Target.Width) { _attack=20; SwingArm(); Target.Attack(DamageSource.Mob(this),4); }
            return;
        }
        if(FollowOwner(10,2)) return;
        var beg=World.Players.FirstOrDefault(p=>!p.Spectator&&DistanceSq(p)<64&&(Tamed?IsWolfMeat(p.Inventory.Selected?.Id??0):p.Inventory.Selected?.Id==I.Bone));
        Begging=beg!=null;
        if(beg!=null) { LookTarget=beg; LookTime=10; Nav.Stop(); return; }
        base.Think();
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Collar); b.Write(Angry); }
    public override void Load(BinaryReader b) { base.Load(b); Collar=b.ReadInt32(); Angry=b.ReadBoolean(); MaxHealth=Tamed?20:8; }
    public override int NetData=>base.NetData|(Angry?8:0)|(Begging?16:0)|(Collar<<5)|((int)Math.Ceiling(Health)<<9);
    public override void SetNetData(int d) { base.SetNetData(d); Angry=(d&8)!=0; Begging=(d&16)!=0; Collar=(d>>5)&15; MaxHealth=Tamed?20:8; Health=(d>>9)&31; }
}

public sealed class Ocelot : Tameable
{
    public int CatType; private int _attack;
    public Ocelot(World.World w):base(w) { Width=.6f; Height=.7f; MaxHealth=Health=10; MoveSpeed=.3; }
    public override string TypeKey=>"ocelot";
    public override string DisplayName=>Tamed?"Kot":"Ocelot";
    public override string LivingSound=>!Tamed?null:InLove>0?"mob.cat.purreow":World.Rand.Next(4)==0?"mob.cat.purr":"mob.cat.meow";
    public override string HurtSound=>"mob.cat.hitt";
    public override string DeathSound=>"mob.cat.hitt";
    public override bool IsBreedingItem(ItemStack s)=>s?.Id==I.RawFish;
    public override bool CanMateWith(Animal a)=>a is Ocelot o&&o!=this&&Tamed&&o.Tamed&&!Sitting&&!o.Sitting;
    public override Animal CreateChild() { var c=new Ocelot(World); if(Tamed) { c.Tamed=true; c.Owner=Owner; c.CatType=CatType; } return c; }
    protected override void OnFall(float distance) { }
    private Player Tempter=>World.Players.FirstOrDefault(p=>!p.Spectator&&DistanceSq(p)<100&&p.Inventory.Selected?.Id==I.RawFish&&!p.Sprinting);
    public override bool Interact(Player p)
    {
        var s=p.Inventory.Selected;
        if(Tamed) {
            if(InteractLead(p)) return true;
            if(p.Name==Owner&&!IsBreedingItem(s)) { if(!World.IsRemote) { Sitting=!Sitting; Nav.Stop(); } return true; }
        }
        else if(s?.Id==I.RawFish&&Tempter==p&&DistanceSq(p)<9) {
            ConsumeHeld(p);
            if(!World.IsRemote) { if(World.Rand.Next(3)==0) { CatType=1+World.Rand.Next(3); Tame(p); } else World.SpawnParticles(ParticleKind.Smoke,X,Y+Height,Z,7); }
            return true;
        }
        return base.Interact(p);
    }
    protected override void Think()
    {
        if(_attack>0) _attack--;
        if(Sitting) { Nav.Stop(); LookAtPlayersRandomly(); return; }
        if(!Tamed) {
            if(Target!=null&&(Target.Dead||DistanceSq(Target)>256)) Target=null;
            if(Target==null&&World.Rand.Next(200)==0) Target=World.GetEntitiesInBox(BoundingBox.Grow(10,4,10),this).OfType<Chicken>().FirstOrDefault(c=>!c.Dead);
            if(Target!=null) {
                if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed*1.33);
                if(OnGround&&DistanceSq(Target)<16&&World.Rand.Next(5)==0) { MotionY=.3; MotionX+=(Target.X-X)*.1; MotionZ+=(Target.Z-Z)*.1; }
                if(_attack==0&&DistanceSq(Target)<2) { _attack=20; Target.Attack(DamageSource.Mob(this),3); }
                return;
            }
            var t=Tempter;
            if(t!=null) { LookTarget=t; LookTime=10; if(DistanceSq(t)>6.25) { if(Age%10==0) Nav.MoveTo(t.X,t.Y,t.Z,MoveSpeed*.6/.3*.3); } else Nav.Stop(); return; }
            var scary=World.Players.FirstOrDefault(p=>!p.Spectator&&!p.Creative&&DistanceSq(p)<256);
            if(scary!=null) { if(Age%10==0||!Nav.HasPath) Nav.MoveTo(X+(X-scary.X)*1.5,Y,Z+(Z-scary.Z)*1.5,DistanceSq(scary)<49?MoveSpeed*1.33:MoveSpeed*.8); return; }
        }
        else if(FollowOwner(10,5)) return;
        base.Think();
    }
    public override bool CanSpawnHere()
    {
        if(World.Rand.Next(3)==0||!HasSpawnSpace()) return false;
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        if(y<63) return false;
        int below=World.GetBlock(x,y-1,z);
        return below==B.Grass||Block.Get(below).Key.Contains("leaves");
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(CatType); }
    public override void Load(BinaryReader b) { base.Load(b); CatType=b.ReadInt32(); }
    public override int NetData=>base.NetData|(CatType<<3);
    public override void SetNetData(int d) { base.SetNetData(d); CatType=(d>>3)&3; }
}

public sealed class Rabbit : Animal
{
    public int RabbitType; private int _hop,_raid,_attack,_carrotTicks;
    public float JumpAnim;
    public Rabbit(World.World w):base(w) { Width=.6f; Height=.7f; MaxHealth=Health=3; MoveSpeed=.3; RabbitType=-1; }
    public override string TypeKey=>RabbitType==99?"killer_bunny":"rabbit";
    public override string DisplayName=>RabbitType==99?"Zabójczy królik":"Królik";
    public override int ArmorValue=>RabbitType==99?8:0;
    public override string LivingSound=>"mob.rabbit.idle";
    public override string HurtSound=>"mob.rabbit.hurt";
    public override string DeathSound=>"mob.rabbit.death";
    public override bool IsBreedingItem(ItemStack s)=>s?.Id is I.Carrot or I.GoldenCarrot or B.Dandelion;
    public override bool CanMateWith(Animal a)=>RabbitType!=99&&base.CanMateWith(a);
    public override Animal CreateChild()
    {
        int other=(Mate as Rabbit)?.RabbitType??RabbitType;
        return new Rabbit(World){RabbitType=World.Rand.Next(20)==0?BiomeType():World.Rand.NextBool()?RabbitType:other};
    }
    /// <summary>EntityRabbit.func_175523_cj: typ zależny od biomu.</summary>
    private int BiomeType()
    {
        int b=World.GetBiome(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Z)),i=World.Rand.Next(100);
        if(BiomeInfo.IsSnowy(b)) return i<80?1:3;
        if(BiomeInfo.IsDesert(b)) return 4;
        return i<50?0:i<90?5:2;
    }
    public override void Tick()
    {
        if(RabbitType<0&&!World.IsRemote) RabbitType=BiomeType();
        if(RabbitType==99&&string.IsNullOrEmpty(CustomName)&&!World.IsRemote) CustomName="The Killer Bunny";
        base.Tick();
        if(JumpAnim>0) JumpAnim=Math.Max(0,JumpAnim-.1f);
    }
    public override void Jump() { MotionY=CollidedH?.5:.3; OnGround=false; JumpAnim=1; World.PlaySound("mob.rabbit.hop",X,Y,Z,.8f,((World.Rand.NextFloat()-World.Rand.NextFloat())*.2f+1f)*.8f); }
    protected override void Think()
    {
        if(_attack>0) _attack--;
        if(RabbitType==99) {
            if(Target==null||Target.Dead||World.Rand.Next(20)==0) Target=(LivingEntity)NearestPlayer(16,true)??World.GetEntitiesInBox(BoundingBox.Grow(16,4,16),this).OfType<Wolf>().FirstOrDefault(w=>!w.Dead);
            if(Target!=null) {
                if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed*1.4);
                if(_attack==0&&DistanceSq(Target)<Width*2*Width*2+Target.Width) { _attack=20; World.PlaySound("mob.attack",X,Y,Z); Target.Attack(DamageSource.Mob(this),8); }
            }
            else base.Think();
        } else {
            Entity danger=World.GetEntitiesInBox(BoundingBox.Grow(10,3,10),this).FirstOrDefault(e=>e is Wolf&&DistanceSq(e)<100||e is Monster&&DistanceSq(e)<16||e is Player p&&!p.Spectator&&!p.Creative&&DistanceSq(e)<64&&!IsBreedingItem(p.Inventory.Selected));
            if(danger!=null&&PanicTicks==0) { if(Age%10==0||!Nav.HasPath) Nav.MoveTo(X+(X-danger.X)*2,Y,Z+(Z-danger.Z)*2,MoveSpeed*2.2); }
            else if(!RaidFarm()) base.Think();
        }
        if(OnGround&&Nav.HasPath&&--_hop<=0) { IsJumping=true; _hop=PanicTicks>0?2:10; }
    }
    /// <summary>EntityRabbit.AIRaidFarm: zjada dojrzałe marchewki (wiek - 1 albo zniszczenie).</summary>
    private bool RaidFarm()
    {
        if(_raid>0) { _raid--; return false; }
        if(_carrotTicks>0) _carrotTicks--;
        if(World.Rand.Next(40)!=0) return false;
        int x0=MathUtil.FloorToInt(X),y0=MathUtil.FloorToInt(Y),z0=MathUtil.FloorToInt(Z);
        for(int dy=-1;dy<=1;dy++) for(int dx=-8;dx<=8;dx++) for(int dz=-8;dz<=8;dz++) {
            int x=x0+dx,y=y0+dy,z=z0+dz;
            if(World.GetBlock(x,y,z)!=B.Carrots||World.GetMeta(x,y,z)<7) continue;
            if(Math.Abs(dx)<=1&&Math.Abs(dz)<=1) {
                int age=World.GetMeta(x,y,z);
                if(age==0) World.SetBlock(x,y,z,B.Air); else World.SetMeta(x,y,z,age-1);
                World.PlayEffectBreak(x,y,z,B.Carrots,age); _raid=40+World.Rand.Next(40); _carrotTicks=0;
                return true;
            }
            Nav.MoveTo(x,y,z,MoveSpeed*.7); return true;
        }
        _raid=100; return false;
    }
    protected override void DropLoot(bool player,bool burning)
    {
        Drop(I.RabbitHide,World.Rand.Next(2)+World.Rand.Next(Looting+1));
        Drop(burning?I.CookedRabbit:I.RawRabbit,World.Rand.Next(2)+World.Rand.Next(Looting+1));
        if(player&&World.Rand.NextFloat()<.025f+.01f*Looting) Drop(I.RabbitFoot,1);
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(RabbitType); }
    public override void Load(BinaryReader b) { base.Load(b); RabbitType=b.ReadInt32(); }
    public override int NetData=>base.NetData|((RabbitType==99?7:Math.Max(0,RabbitType))<<1)|(JumpAnim>0?16:0);
    public override void SetNetData(int d) { base.SetNetData(d); int t=(d>>1)&7; RabbitType=t==7?99:t; if((d&16)!=0&&JumpAnim<=0) JumpAnim=1; }
}

public sealed class Bat : Mob
{
    public bool Hanging=true; private int _tx,_ty,_tz; private bool _hasTarget;
    public Bat(World.World w):base(w) { Width=.5f; Height=.9f; MaxHealth=Health=6; NoGravity=true; }
    public override string TypeKey=>"bat";
    public override string DisplayName=>"Nietoperz";
    public override string LivingSound=>Hanging&&World.Rand.Next(4)!=0?null:"mob.bat.idle";
    public override string HurtSound=>"mob.bat.hurt";
    public override string DeathSound=>"mob.bat.death";
    protected override void OnFall(float distance) { }
    public override bool Attack(DamageSource src,float amount) { if(!World.IsRemote&&Hanging) Hanging=false; return base.Attack(src,amount); }
    public override bool CanSpawnHere()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        if(y>=63||!HasSpawnSpace()) return false;
        var now=DateTime.Now; int max=now.Month==10&&now.Day>=20||now.Month==11&&now.Day<=3?7:4;
        return World.GetLight(x,y,z)<=World.Rand.Next(max);
    }
    public override void Tick()
    {
        base.Tick();
        if(Hanging) { MotionX=MotionY=MotionZ=0; Y=Math.Floor(Y)+1-Height; }
        else MotionY*=.6;
    }
    /// <summary>EntityBat.updateAITasks z 1.8.</summary>
    protected override void Think()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        bool ceiling=Block.Get(World.GetBlock(x,y+1,z)).Opaque;
        if(Hanging) {
            if(!ceiling) Hanging=false;
            else {
                if(World.Rand.Next(200)==0) HeadYaw=World.Rand.Next(360);
                if(World.Players.Any(p=>!p.Spectator&&DistanceSq(p)<16)) Hanging=false;
            }
            return;
        }
        if(_hasTarget&&(World.GetBlock(_tx,_ty,_tz)!=B.Air||_ty<1)) _hasTarget=false;
        if(!_hasTarget||World.Rand.Next(30)==0||DistanceSq(_tx+.5,_ty+.5,_tz+.5)<4) {
            _tx=x+World.Rand.Next(7)-World.Rand.Next(7); _ty=y+World.Rand.Next(6)-2; _tz=z+World.Rand.Next(7)-World.Rand.Next(7); _hasTarget=true;
        }
        double dx=_tx+.5-X,dy=_ty+.1-Y,dz=_tz+.5-Z;
        MotionX+=(Math.Sign(dx)*.5-MotionX)*.1; MotionY+=(Math.Sign(dy)*.7-MotionY)*.1; MotionZ+=(Math.Sign(dz)*.5-MotionZ)*.1;
        float yaw=(float)(Math.Atan2(MotionZ,MotionX)*MathUtil.Rad2Deg)-90f;
        Yaw+=MathUtil.WrapDegrees(yaw-Yaw); BodyYaw=Yaw; MoveForward=.5f;
        if(World.Rand.Next(100)==0&&ceiling) Hanging=true;
    }
    public override void Travel(float strafe,float forward) { MoveWithCollision(MotionX,MotionY,MotionZ); MotionX*=.91; MotionZ*=.91; }
    public override int NetData=>Hanging?1:0;
    public override void SetNetData(int d)=>Hanging=d!=0;
}

public sealed class Squid : Mob
{
    public float BodyPitch,PrevBodyPitch,BodyRoll,PrevBodyRoll,Tentacle,PrevTentacle;
    private float _rot,_rotSpeed,_speed; private double _rx,_ry,_rz;
    public Squid(World.World w):base(w) { Width=Height=.95f; MaxHealth=Health=10; _rotSpeed=1f/(w.Rand.NextFloat()+1f)*.2f; }
    public override string TypeKey=>"squid";
    public override string DisplayName=>"Kałamarnica";
    public override bool CanBreatheUnderwater=>true;
    public override string HurtSound=>null;
    public override string DeathSound=>null;
    public override int XpValue=>1+World.Rand.Next(3);
    public override bool CanSpawnHere()=>Y>45&&Y<63&&World.IsAnyLiquid(BoundingBox,false)&&HasSpawnSpace(true);
    public override void PrevTick() { base.PrevTick(); PrevBodyPitch=BodyPitch; PrevBodyRoll=BodyRoll; PrevTentacle=Tentacle; }
    /// <summary>EntitySquid.onLivingUpdate: pływanie zrywami i animacja macek.</summary>
    protected override void Think()
    {
        _rot+=_rotSpeed;
        if(_rot>MathF.PI*2) { _rot-=MathF.PI*2; if(World.Rand.Next(10)==0) _rotSpeed=1f/(World.Rand.NextFloat()+1f)*.2f; }
        if(InWater) {
            Air=300;
            if(_rot<MathF.PI) { float f=_rot/MathF.PI; Tentacle=MathF.Sin(f*f*MathF.PI)*MathF.PI*.25f; if(f>.75) _speed=1f; }
            else { Tentacle=0; _speed*=.9f; }
            MotionX=_rx*_speed; MotionY=_ry*_speed; MotionZ=_rz*_speed;
            double h=Math.Sqrt(MotionX*MotionX+MotionZ*MotionZ);
            BodyYaw+=(-(float)Math.Atan2(MotionX,MotionZ)*MathUtil.Rad2Deg-BodyYaw)*.1f; Yaw=BodyYaw;
            BodyRoll+=MathF.PI*_rotSpeed*1.5f;
            BodyPitch+=(-(float)Math.Atan2(h,MotionY)*MathUtil.Rad2Deg-BodyPitch)*.1f;
            if(Age%50==0||World.Rand.Next(50)==0||!InWater||_rx==0&&_ry==0&&_rz==0) {
                float a=World.Rand.NextFloat()*MathF.PI*2;
                _rx=MathF.Cos(a)*.2f; _ry=-.1f+World.Rand.NextFloat()*.2f; _rz=MathF.Sin(a)*.2f;
            }
        } else {
            Tentacle=MathF.Abs(MathF.Sin(_rot))*MathF.PI*.25f;
            MotionX=MotionZ=0; MotionY-=.08; MotionY*=.98; BodyPitch+=(-90f-BodyPitch)*.02f;
            if(--Air<=-20) { Air=0; Attack(DamageSource.Drown,2); }
        }
    }
    public override void Travel(float strafe,float forward)=>MoveWithCollision(MotionX,MotionY,MotionZ);
    protected override void DropLoot(bool player,bool burning)=>Drop(I.Dye,1+World.Rand.Next(3+Looting),0);
}

public sealed class Mooshroom : Cow
{
    public Mooshroom(World.World w):base(w) { }
    public override string TypeKey=>"mooshroom";
    public override string DisplayName=>"Grzybowa krowa";
    public override Animal CreateChild()=>new Mooshroom(World);
    public override bool CanSpawnHere()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        return HasSpawnSpace()&&World.GetBlock(x,y-1,z)==B.Mycelium&&World.GetRawLight(x,y,z)>8;
    }
    public override bool Interact(Player p)
    {
        var s=p.Inventory.Selected;
        if(s?.Id==I.Bowl&&GrowingAge>=0) {
            if(s.Count==1) p.Inventory.Selected=new ItemStack(I.MushroomStew);
            else if(p.Inventory.Add(new ItemStack(I.MushroomStew))&&!p.Creative) s.Count--;
            p.Inventory.Dirty=true; return true;
        }
        if(s?.Id==I.Shears&&GrowingAge>=0) {
            if(World.IsRemote) return true;
            Removed=true; World.SpawnParticles(ParticleKind.Explosion,X,Y+Height/2,Z,1);
            var cow=new Cow(World){Health=Health,CustomName=CustomName,PersistenceRequired=PersistenceRequired};
            cow.SetPositionAndPrev(X,Y,Z); cow.Yaw=cow.PrevYaw=Yaw; cow.BodyYaw=BodyYaw; World.SpawnEntity(cow);
            for(int i=0;i<5;i++) World.DropItem(X,Y+Height,Z,new ItemStack(B.RedMushroom));
            p.DamageHeld(1); World.PlaySound("mob.sheep.shear",X,Y,Z); return true;
        }
        return base.Interact(p);
    }
}

public sealed class IronGolem : Mob
{
    public bool PlayerCreated; public int RoseTicks,AttackTicks;
    public IronGolem(World.World w):base(w) { Width=1.4f; Height=2.9f; MaxHealth=Health=100; MoveSpeed=.25; }
    public override string TypeKey=>"iron_golem";
    public override string DisplayName=>"Żelazny golem";
    public override bool Persistent=>true;
    public override string HurtSound=>"mob.irongolem.hit";
    public override string DeathSound=>"mob.irongolem.death";
    public override bool CanSpawnHere()=>HasSpawnSpace();
    public override bool CanBeLeashed=>!PlayerCreated||true;
    public override bool Interact(Player p)=>InteractLead(p);
    protected override void OnFall(float distance) { }
    public override void PrevTick() { base.PrevTick(); }
    public override bool Attack(DamageSource src,float amount)=>!src.IsFire&&src.Type!="drown"&&base.Attack(src,amount);
    protected override void Think()
    {
        if(AttackTicks>0) AttackTicks--;
        if(RoseTicks>0) RoseTicks--;
        if(Target!=null&&(Target.Dead||Target.Removed||Target is Player tp&&(tp.Creative||tp.Spectator)||DistanceSq(Target)>1024)) Target=null;
        if(LastAttacker is LivingEntity attacker&&!attacker.Dead&&!(attacker is Player&&PlayerCreated)&&attacker is not Creeper) Target=attacker;
        if(Target==null&&World.Rand.Next(10)==0)
            Target=World.GetEntitiesInBox(BoundingBox.Grow(16,4,16),this).OfType<Monster>().Where(e=>e is not Creeper&&!e.Dead&&CanSee(e)).OrderBy(e=>DistanceSq(e)).FirstOrDefault();
        if(Target==null) Target=World.GetEntitiesInBox(BoundingBox.Grow(16,4,16),this).OfType<Villager>().Select(v=>v.LastAttacker).OfType<LivingEntity>()
            .FirstOrDefault(a=>!a.Dead&&!(a is Player pl&&(PlayerCreated||pl.Creative||pl.Spectator)));
        if(Target!=null) {
            LookTarget=Target; LookTime=10;
            if(Age%10==0||!Nav.HasPath) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed);
            if(AttackTicks==0&&DistanceSq(Target)<Width*2*Width*2+Target.Width) {
                AttackTicks=10; World.PlaySound("mob.irongolem.throw",X,Y,Z);
                if(Target.Attack(DamageSource.Mob(this),7+World.Rand.Next(15))) Target.MotionY+=.4;
            }
            return;
        }
        if(RoseTicks==0&&World.Rand.Next(8000)==0) {
            var kid=World.GetEntitiesInBox(BoundingBox.Grow(6,2,6),this).OfType<Villager>().FirstOrDefault(v=>v.IsBaby);
            if(kid!=null) { RoseTicks=400; LookTarget=kid; LookTime=400; }
        }
        if(!PlayerCreated&&World.Rand.Next(120)==0) {
            var home=World.GetEntitiesInBox(BoundingBox.Grow(32,8,32),this).OfType<Villager>().FirstOrDefault();
            if(home!=null&&DistanceSq(home)>256) { Nav.MoveTo(home.X,home.Y,home.Z,MoveSpeed*.6/.25*.25); return; }
        }
        Wander(MoveSpeed*.6/.25*.25); LookAtPlayersRandomly();
    }
    protected override void DropLoot(bool player,bool burning) { Drop(I.IronIngot,3+World.Rand.Next(3)); Drop(B.Poppy,World.Rand.Next(3)); }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(PlayerCreated); }
    public override void Load(BinaryReader b) { base.Load(b); PlayerCreated=b.ReadBoolean(); }
    public override int NetData=>(AttackTicks>0?1:0)|(RoseTicks>0?2:0);
    public override void SetNetData(int d) { if((d&1)!=0&&AttackTicks==0) AttackTicks=10; else if((d&1)==0) AttackTicks=0; RoseTicks=(d&2)!=0?Math.Max(RoseTicks,2):0; }
}

public sealed class SnowGolem : Mob
{
    private int _shoot;
    public SnowGolem(World.World w):base(w) { Width=.7f; Height=1.9f; MaxHealth=Health=4; MoveSpeed=.2; }
    public override string TypeKey=>"snow_golem";
    public override string DisplayName=>"Śnieżny golem";
    public override bool Persistent=>true;
    public override bool CanSpawnHere()=>HasSpawnSpace();
    public override float EyeHeight=>1.7f;
    public override bool Interact(Player p)=>InteractLead(p);
    protected override void Think()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        bool wet=InWater||WorldHooks.IsRainingAt?.Invoke(World,x,y,z)==true;
        if(wet) Attack(DamageSource.Drown,1);
        if(BiomeInfo.Temperature(World.GetBiome(x,z),y)>1f) Attack(DamageSource.OnFire,1);
        for(int i=0;i<4;i++) {
            int sx=MathUtil.FloorToInt(X+(i%2*2-1)*.25),sz=MathUtil.FloorToInt(Z+(i/2%2*2-1)*.25);
            if(World.GetBlock(sx,y,sz)==B.Air&&BiomeInfo.Temperature(World.GetBiome(sx,sz),y)<.8f&&Block.Get(World.GetBlock(sx,y-1,sz)).Opaque) World.SetBlock(sx,y,sz,B.SnowLayer);
        }
        if(_shoot>0) _shoot--;
        if(Target!=null&&(Target.Dead||!CanSee(Target)||DistanceSq(Target)>256)) Target=null;
        if(Target==null&&World.Rand.Next(10)==0)
            Target=World.GetEntitiesInBox(BoundingBox.Grow(16,4,16),this).OfType<Monster>().Where(e=>!e.Dead&&CanSee(e)).OrderBy(e=>DistanceSq(e)).FirstOrDefault();
        if(Target!=null) {
            LookTarget=Target; LookTime=10;
            double d=DistanceSq(Target);
            if(d>100) { if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed*1.25); }
            else Nav.Stop();
            if(_shoot==0&&d<=100) {
                _shoot=20; var ball=new GolemSnowball(World); ball.Throw(this,Target); World.SpawnEntity(ball);
                World.PlaySound("random.bow",X,Y,Z,1f,1f/(World.Rand.NextFloat()*.4f+.8f));
            }
            return;
        }
        Wander(MoveSpeed); LookAtPlayersRandomly();
    }
    protected override void DropLoot(bool player,bool burning)=>Drop(I.Snowball,World.Rand.Next(16));
}

public sealed class GolemSnowball : Entity
{
    public LivingEntity Shooter;
    public GolemSnowball(World.World w):base(w) { Width=Height=.25f; }
    public override string TypeKey=>"golem_snowball";
    public void Throw(LivingEntity shooter,LivingEntity target)
    {
        Shooter=shooter; SetPositionAndPrev(shooter.X,shooter.Y+shooter.EyeHeight-.1,shooter.Z);
        double dx=target.X-X,dz=target.Z-Z,dy=target.Y+target.EyeHeight-1.1-Y,h=Math.Sqrt(dx*dx+dz*dz)*.2;
        dy+=h; double len=Math.Sqrt(dx*dx+dy*dy+dz*dz);
        MotionX=dx/len*1.6+World.Rand.NextGaussian()*.0075*12; MotionY=dy/len*1.6+World.Rand.NextGaussian()*.0075*12; MotionZ=dz/len*1.6+World.Rand.NextGaussian()*.0075*12;
    }
    public override void Tick()
    {
        base.Tick();
        MoveWithCollision(MotionX,MotionY,MotionZ); MotionX*=.99; MotionZ*=.99; MotionY=MotionY*.99-.03;
        if(World.IsRemote) return;
        foreach(var e in World.GetEntitiesInBox(BoundingBox.Grow(.3),this).OfType<LivingEntity>())
            if(e!=Shooter&&!e.Dead) { e.Attack(new DamageSource("thrown",this,Shooter),e.TypeKey=="blaze"?3:0); Hit(); return; }
        if(CollidedH||CollidedV||Age>200) Hit();
    }
    private void Hit() { World.SpawnParticles(ParticleKind.Smoke,X,Y,Z,4); Removed=true; }
}

public sealed class LeashKnot : Entity
{
    public LeashKnot(World.World w):base(w) { Width=.375f; Height=.5f; NoGravity=true; }
    public override string TypeKey=>"leash_knot";
    public override bool Persistent=>true;
    public override bool CanBeAttacked=>true;
    public int BX=>MathUtil.FloorToInt(X); public int BY=>MathUtil.FloorToInt(Y); public int BZ=>MathUtil.FloorToInt(Z);
    public static bool IsFence(int id)=>id!=0&&Block.Get(id).Key is { } k&&k.Contains("fence")&&!k.Contains("gate");
    /// <summary>ItemLead.attachToFence: prawy klik na płot przywiązuje wszystkie moby prowadzone przez gracza w promieniu 7.</summary>
    public static bool TryTie(World.World w,Player p,int x,int y,int z)
    {
        if(!IsFence(w.GetBlock(x,y,z))) return false;
        var mobs=w.GetEntitiesInBox(new AABB(x-7,y-7,z-7,x+8,y+8,z+8),p).OfType<Mob>().Where(m=>m.LeashHolder==p).ToList();
        var existing=w.GetEntitiesInBox(new AABB(x,y,z,x+1,y+1,z+1),null).OfType<LeashKnot>().FirstOrDefault();
        if(mobs.Count==0) {
            if(existing==null) return false;
            return existing.Interact(p);
        }
        if(w.IsRemote) return true;
        var knot=existing??new LeashKnot(w);
        if(existing==null) { knot.SetPositionAndPrev(x+.5,y+.375-.25,z+.5); w.SpawnEntity(knot); w.PlaySound("dig.wood",x+.5,y+.5,z+.5); }
        foreach(var m in mobs) m.LeashHolder=knot;
        return true;
    }
    public override bool Interact(Player p)
    {
        // EntityLeashKnot.interactFirst: ze smyczą w ręce przypina moby gracza, bez niej zrywa węzeł
        if(World.IsRemote) return true;
        if(p.Inventory.Selected?.Id==I.Lead) {
            var mine=World.GetEntitiesInBox(BoundingBox.Grow(7),this).OfType<Mob>().Where(m=>m.LeashHolder==p).ToList();
            foreach(var m in mine) m.LeashHolder=this;
            if(mine.Count>0) return true;
        }
        Break(p.Creative);
        return true;
    }
    public void Break(bool creative)
    {
        foreach(var m in World.Entities.OfType<Mob>().Where(m=>m.LeashHolder==this).ToList()) { m.LeashHolder=null; if(!creative) World.DropItem(m.X,m.Y,m.Z,new ItemStack(I.Lead)); }
        World.PlaySound("dig.wood",X,Y,Z); Removed=true;
    }
    public override void Tick()
    {
        base.Tick();
        if(World.IsRemote) return;
        if(!IsFence(World.GetBlock(BX,BY,BZ))) { Break(false); return; }
        if(Age%100==0&&!World.Entities.OfType<Mob>().Any(m=>m.LeashHolder==this)) Removed=true;
    }
}

/// <summary>BlockPumpkin.onBlockAdded: golem śnieżny (2 bloki śniegu + dynia) i żelazny (T z 4 bloków żelaza + dynia).</summary>
public static class GolemBuilder
{
    public static void AfterPlace(World.World w,int x,int y,int z,int face)
    {
        if(w.IsRemote) return;
        if(!TryBuild(w,x,y,z)) TryBuild(w,x+Dir.X[face],y+Dir.Y[face],z+Dir.Z[face]);
    }
    public static bool TryBuild(World.World w,int x,int y,int z)
    {
        int id=w.GetBlock(x,y,z);
        if(id!=B.Pumpkin&&id!=B.JackOLantern) return false;
        if(w.GetBlock(x,y-1,z)==B.Snow&&w.GetBlock(x,y-2,z)==B.Snow) {
            for(int i=0;i<3;i++) w.SetBlock(x,y-i,z,B.Air);
            var g=new SnowGolem(w); g.SetPositionAndPrev(x+.5,y-1.95,z+.5); w.SpawnEntity(g);
            for(int i=0;i<120;i++) w.SpawnParticles(ParticleKind.Smoke,x+.5,y-2+w.Rand.NextDouble()*2.5,z+.5,1);
            return true;
        }
        if(w.GetBlock(x,y-1,z)!=B.IronBlock||w.GetBlock(x,y-2,z)!=B.IronBlock) return false;
        foreach(var (ax,az) in new[]{(1,0),(0,1)}) {
            if(w.GetBlock(x+ax,y-1,z+az)!=B.IronBlock||w.GetBlock(x-ax,y-1,z-az)!=B.IronBlock) continue;
            if(w.GetBlock(x+ax,y-2,z+az)!=B.Air&&!Block.Get(w.GetBlock(x+ax,y-2,z+az)).Replaceable) continue;
            if(w.GetBlock(x-ax,y-2,z-az)!=B.Air&&!Block.Get(w.GetBlock(x-ax,y-2,z-az)).Replaceable) continue;
            w.SetBlock(x,y,z,B.Air); w.SetBlock(x,y-1,z,B.Air); w.SetBlock(x,y-2,z,B.Air);
            w.SetBlock(x+ax,y-1,z+az,B.Air); w.SetBlock(x-ax,y-1,z-az,B.Air);
            var g=new IronGolem(w){PlayerCreated=true}; g.SetPositionAndPrev(x+.5,y-1.95,z+.5); w.SpawnEntity(g);
            for(int i=0;i<120;i++) w.SpawnParticles(ParticleKind.Smoke,x+.5,y-2+w.Rand.NextDouble()*3.9,z+.5,1);
            return true;
        }
        return false;
    }
}
