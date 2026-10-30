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
        Entity("leash_knot",w=>new LeashKnot(w)); Entity("golem_snowball",w=>new GolemSnowball(w));
        RegisterVillagers18();
    }
    static partial void RegisterVillagers18();
}

public abstract class Tameable : Animal
{
    public bool Tamed,Sitting;
    public string Owner="";
    protected Tameable(World.World w):base(w) { }
    public Player OwnerPlayer=>World.Players.FirstOrDefault(p=>p.Name==Owner);
    public virtual void Tame(Player p) { Tamed=true; Owner=p.Name; PersistenceRequired=true; World.SpawnParticles(ParticleKind.Heart,X,Y+Height,Z,7); }
    protected bool FollowOwner()
    {
        if(Sitting) { Nav.Stop(); return true; }
        var p=OwnerPlayer;
        if(Tamed&&p!=null&&DistanceSq(p)>25) { if(Age%10==0) Nav.MoveTo(p.X,p.Y,p.Z,MoveSpeed); return true; }
        return false;
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Tamed); b.Write(Sitting); b.Write(Owner); }
    public override void Load(BinaryReader b) { base.Load(b); Tamed=b.ReadBoolean(); Sitting=b.ReadBoolean(); Owner=b.ReadString(); }
}

public sealed class Wolf : Tameable
{
    public int Collar=14; private int _attack;
    public Wolf(World.World w):base(w) { Width=.6f; Height=.8f; MaxHealth=Health=8; MoveSpeed=.3; }
    public override string TypeKey=>"wolf";
    public override string DisplayName=>"Wilk";
    public override string LivingSound=>Target!=null?"mob.wolf.growl":Tamed&&Health<10?"mob.wolf.whine":"mob.wolf.bark";
    public override string HurtSound=>"mob.wolf.hurt";
    public override string DeathSound=>"mob.wolf.death";
    public override bool IsBreedingItem(ItemStack s)=>Tamed && s!=null && IsMeat(s.Id);
    private static bool IsMeat(int id)=>id is I.RawBeef or I.Steak or I.RawPorkchop or I.CookedPorkchop or I.RawChicken or I.CookedChicken or I.RawMutton or I.CookedMutton or I.RottenFlesh or I.RawRabbit or I.CookedRabbit;
    public override Animal CreateChild()=>new Wolf(World){Tamed=Tamed,Owner=Owner,MaxHealth=Tamed?20:8,Health=Tamed?20:8};
    public override void Tame(Player p) { base.Tame(p); MaxHealth=Health=20; Sitting=true; }
    public override bool Interact(Player p)
    {
        if(InteractLead(p)) return true;
        var s=p.Inventory.Selected;
        if(!Tamed&&s?.Id==I.Bone&&Target==null) { ConsumeHeld(p); if(World.Rand.Next(3)==0) Tame(p); else World.SpawnParticles(ParticleKind.Smoke,X,Y+Height,Z,7); return true; }
        if(Tamed) {
            if(s!=null&&IsMeat(s.Id)&&Health<MaxHealth) { Heal(s.Item?.Hunger??2); ConsumeHeld(p); return true; }
            int dye=Dyes.ColorOf(s);
            if(dye>=0&&dye!=Collar) { Collar=dye; ConsumeHeld(p); return true; }
            if(p.Name==Owner && !IsBreedingItem(s)) { Sitting=!Sitting; Target=null; Nav.Stop(); return true; }
        }
        return base.Interact(p);
    }
    protected override void Think()
    {
        if(LastAttacker is LivingEntity attacker&&!attacker.Dead&&attacker!=OwnerPlayer) { Target=attacker; Sitting=false; }
        if(Tamed&&OwnerPlayer?.LastAttacker is LivingEntity danger&&!danger.Dead&&danger is not Creeper) Target=danger;
        if(Target!=null&&!Target.Dead) {
            if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed);
            if(--_attack<=0&&DistanceSq(Target)<2.25) { _attack=20; Target.Attack(DamageSource.Mob(this),Tamed?4:2); }
            return;
        }
        Target=null; if(FollowOwner()) return; base.Think();
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Collar); }
    public override void Load(BinaryReader b) { base.Load(b); Collar=b.ReadInt32(); MaxHealth=Tamed?20:8; }
}

public sealed class Ocelot : Tameable
{
    public int CatType;
    public Ocelot(World.World w):base(w) { Width=.6f; Height=.7f; MaxHealth=Health=10; MoveSpeed=.3; }
    public override string TypeKey=>"ocelot";
    public override string DisplayName=>Tamed?"Kot":"Ocelot";
    public override string LivingSound=>Tamed?"mob.cat.meow":null;
    public override string HurtSound=>"mob.cat.hitt";
    public override string DeathSound=>"mob.cat.hitt";
    public override bool IsBreedingItem(ItemStack s)=>s?.Id==I.RawFish;
    public override Animal CreateChild()=>new Ocelot(World){Tamed=Tamed,Owner=Owner,CatType=CatType};
    public override bool Interact(Player p)
    {
        if(InteractLead(p)) return true;
        if(!Tamed&&p.Inventory.Selected?.Id==I.RawFish&&DistanceSq(p)<9&&!p.Sprinting) {
            ConsumeHeld(p); if(World.Rand.Next(3)==0) { Tame(p); CatType=1+World.Rand.Next(3); } return true;
        }
        if(Tamed&&p.Name==Owner&&!IsBreedingItem(p.Inventory.Selected)) { Sitting=!Sitting; return true; }
        return base.Interact(p);
    }
    protected override void Think()
    {
        if(FollowOwner()) return;
        if(!Tamed) {
            var p=World.Players.FirstOrDefault(p=>!p.Spectator&&DistanceSq(p)<256&&p.Inventory.Selected?.Id!=I.RawFish);
            if(p!=null) { if(Age%10==0) Nav.MoveTo(X+(X-p.X)*2,Y,Z+(Z-p.Z)*2,MoveSpeed*1.33); return; }
        }
        base.Think();
    }
    public override bool CanSpawnHere()
    { int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z); return y>=63&&HasSpawnSpace()&&(World.GetBlock(x,y-1,z)==B.Grass||Block.Get(World.GetBlock(x,y-1,z)).Key.Contains("leaves")); }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(CatType); }
    public override void Load(BinaryReader b) { base.Load(b); CatType=b.ReadInt32(); }
}

public sealed class Rabbit : Animal
{
    public int RabbitType; private int _hop,_raid;
    public Rabbit(World.World w):base(w) { Width=.6f; Height=.7f; MaxHealth=Health=10; MoveSpeed=.3; RabbitType=w.Rand.Next(6); }
    public override string TypeKey=>"rabbit";
    public override string DisplayName=>RabbitType==99?"Zabójczy królik":"Królik";
    public override string LivingSound=>"mob.rabbit.idle";
    public override string HurtSound=>"mob.rabbit.hurt";
    public override string DeathSound=>"mob.rabbit.death";
    public override bool IsBreedingItem(ItemStack s)=>s?.Id is I.Carrot or I.GoldenCarrot or B.Dandelion;
    public override Animal CreateChild()=>new Rabbit(World){RabbitType=World.Rand.Next(20)==0?World.Rand.Next(6):World.Rand.NextBool()?RabbitType:(Mate as Rabbit)?.RabbitType??RabbitType};
    public override void Jump() { MotionY=CollidedH?.5:.3; OnGround=false; World.PlaySound("mob.rabbit.hop",X,Y,Z); }
    protected override void Think()
    {
        if(RabbitType==99) {
            Target=NearestPlayer(16,true);
            if(Target!=null) { if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed*1.4); if(Age%20==0&&DistanceSq(Target)<4) Target.Attack(DamageSource.Mob(this),8); }
        } else {
            var danger=World.GetEntitiesInBox(BoundingBox.Grow(8),this).FirstOrDefault(e=>e is Wolf || e is Player p&&!p.Spectator&&p.Inventory.Selected?.Id!=I.Carrot);
            if(danger!=null) { if(Age%10==0) Nav.MoveTo(X+(X-danger.X)*2,Y,Z+(Z-danger.Z)*2,MoveSpeed*2.2); }
            else base.Think();
        }
        if(OnGround&&Nav.HasPath&&--_hop<=0) { IsJumping=true; _hop=PanicTicks>0?1:10; }
        if(_raid>0) _raid--;
        if(_raid==0&&Age%20==0) {
            int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
            if(World.GetBlock(x,y,z)==B.Carrots&&World.GetMeta(x,y,z)==7) { World.SetMeta(x,y,z,0); _raid=40; }
        }
    }
    protected override void DropLoot(bool player,bool burning) { if(IsBaby) return; Drop(I.RabbitHide,World.Rand.Next(2)+World.Rand.Next(Looting+1)); Drop(burning?I.CookedRabbit:I.RawRabbit,World.Rand.Next(2)+World.Rand.Next(Looting+1)); if(player&&World.Rand.NextFloat()<.025+.01*Looting) Drop(I.RabbitFoot,1); }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(RabbitType); }
    public override void Load(BinaryReader b) { base.Load(b); RabbitType=b.ReadInt32(); }
}

public sealed class Bat : Mob
{
    public bool Hanging=true;
    public Bat(World.World w):base(w) { Width=.5f; Height=.9f; MaxHealth=Health=6; NoGravity=true; }
    public override string TypeKey=>"bat";
    public override string DisplayName=>"Nietoperz";
    public override string LivingSound=>"mob.bat.idle";
    public override string HurtSound=>"mob.bat.hurt";
    public override string DeathSound=>"mob.bat.death";
    protected override void OnFall(float distance) { }
    public override bool CanSpawnHere()=>Y<63&&HasSpawnSpace()&&World.GetLight(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Y),MathUtil.FloorToInt(Z))<=World.Rand.Next(4);
    protected override void Think()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        bool ceiling=Block.Get(World.GetBlock(x,y+1,z)).Solid;
        if(Hanging) { MotionX=MotionY=MotionZ=0; if(!ceiling||World.Players.Any(p=>DistanceSq(p)<16)) Hanging=false; }
        else {
            if(Age%30==0||CollidedH) { Yaw=World.Rand.NextFloat()*360; MotionX=-Math.Sin(Yaw*MathUtil.Deg2Rad)*.15; MotionZ=Math.Cos(Yaw*MathUtil.Deg2Rad)*.15; MotionY=(World.Rand.NextFloat()-.5)*.2; }
            if(OnGround) MotionY=.2;
            if(ceiling&&World.Rand.Next(100)==0) Hanging=true;
        }
    }
    public override void Travel(float strafe,float forward)=>MoveWithCollision(MotionX,MotionY,MotionZ);
}

public sealed class Squid : Mob
{
    public Squid(World.World w):base(w) { Width=Height=.95f; MaxHealth=Health=10; }
    public override string TypeKey=>"squid";
    public override string DisplayName=>"Kałamarnica";
    public override bool CanBreatheUnderwater=>true;
    public override string HurtSound=>null;
    public override string DeathSound=>null;
    public override int XpValue=>1+World.Rand.Next(3);
    public override bool CanSpawnHere()=>Y>45&&Y<63&&World.IsAnyLiquid(BoundingBox,false)&&HasSpawnSpace(true);
    protected override void Think()
    {
        if(InWater) { Air=300; if(Age%50==0||CollidedH||MotionX==0) { Yaw=World.Rand.NextFloat()*360; MotionX=-Math.Sin(Yaw*MathUtil.Deg2Rad)*.2; MotionZ=Math.Cos(Yaw*MathUtil.Deg2Rad)*.2; MotionY=(World.Rand.NextFloat()-.5)*.1; } }
        else { MotionX=MotionZ=0; MotionY-=.08; if(--Air<=-20) { Air=0; Attack(DamageSource.Drown,2); } }
    }
    public override void Travel(float strafe,float forward)=>MoveWithCollision(MotionX,MotionY,MotionZ);
    protected override void DropLoot(bool player,bool burning)=>Drop(I.Dye,1+World.Rand.Next(3)+World.Rand.Next(Looting+1),0);
}

public sealed class Mooshroom : Cow
{
    public Mooshroom(World.World w):base(w) { }
    public override string TypeKey=>"mooshroom";
    public override string DisplayName=>"Grzybowa krowa";
    public override Animal CreateChild()=>new Mooshroom(World);
    public override bool CanSpawnHere()=>HasSpawnSpace()&&World.GetBlock(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Y)-1,MathUtil.FloorToInt(Z))==B.Mycelium&&World.GetRawLight(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Y),MathUtil.FloorToInt(Z))>8;
    public override bool Interact(Player p)
    {
        if(!IsBaby&&p.Inventory.Selected?.Id==I.Bowl) { ConsumeHeld(p); var soup=new ItemStack(I.MushroomStew); if(!p.Inventory.Add(soup)) p.DropStack(soup,false); return true; }
        if(!IsBaby&&p.Inventory.Selected?.Id==I.Shears) { Removed=true; var cow=new Cow(World){Health=Health,GrowingAge=GrowingAge,CustomName=CustomName}; cow.SetPositionAndPrev(X,Y,Z); World.SpawnEntity(cow); Drop(B.RedMushroom,5); p.DamageHeld(1); World.PlaySound("mob.sheep.shear",X,Y,Z); return true; }
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
    public override bool Interact(Player p)=>InteractLead(p);
    protected override void OnFall(float distance) { }
    protected override void Think()
    {
        if(AttackTicks>0) AttackTicks--;
        if(RoseTicks>0) RoseTicks--;
        if(LastAttacker is LivingEntity attacker&&!attacker.Dead&&(!PlayerCreated||attacker is not Player)) Target=attacker;
        if(Target==null||Target.Dead) Target=World.GetEntitiesInBox(BoundingBox.Grow(16),this).OfType<Monster>().FirstOrDefault(e=>e is not Creeper&&!e.Dead);
        if(Target!=null) { if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed); if(AttackTicks==0&&DistanceSq(Target)<4) { AttackTicks=10; Target.Attack(DamageSource.Mob(this),7+World.Rand.Next(15)); Target.MotionY+=.4; World.PlaySound("mob.irongolem.throw",X,Y,Z); } }
        else { if(World.Rand.Next(8000)==0) RoseTicks=400; Wander(MoveSpeed); LookAtPlayersRandomly(); }
    }
    protected override void DropLoot(bool player,bool burning) { Drop(I.IronIngot,3+World.Rand.Next(3)); Drop(B.Poppy,World.Rand.Next(3)); }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(PlayerCreated); }
    public override void Load(BinaryReader b) { base.Load(b); PlayerCreated=b.ReadBoolean(); }
}

public sealed class SnowGolem : Mob
{
    public SnowGolem(World.World w):base(w) { Width=.7f; Height=1.9f; MaxHealth=Health=4; MoveSpeed=.2; }
    public override string TypeKey=>"snow_golem";
    public override string DisplayName=>"Śnieżny golem";
    public override bool Persistent=>true;
    public override bool CanSpawnHere()=>HasSpawnSpace();
    public override bool Interact(Player p)=>InteractLead(p);
    protected override void Think()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        if(InWater || World.GetBiome(x,z)==2 || WorldHooks.IsRainingAt?.Invoke(World,x,y,z)==true) Attack(DamageSource.Drown,1);
        if(World.GetBlock(x,y,z)==B.Air&&Block.Get(World.GetBlock(x,y-1,z)).Opaque&&World.GetBiome(x,z)!=2) World.SetBlock(x,y,z,B.SnowLayer);
        Target=World.GetEntitiesInBox(BoundingBox.Grow(10),this).OfType<Monster>().FirstOrDefault(e=>!e.Dead);
        if(Target!=null&&CanSee(Target)) { LookTarget=Target; LookTime=10; if(Age%20==0) { var ball=new GolemSnowball(World); ball.Throw(this,Target); World.SpawnEntity(ball); World.PlaySound("random.bow",X,Y,Z); } }
        else Wander(MoveSpeed);
    }
    protected override void DropLoot(bool player,bool burning)=>Drop(I.Snowball,World.Rand.Next(16));
}

public sealed class GolemSnowball : Entity
{
    public LivingEntity Shooter;
    public GolemSnowball(World.World w):base(w) { Width=Height=.25f; }
    public override string TypeKey=>"golem_snowball";
    public void Throw(LivingEntity shooter,LivingEntity target) { Shooter=shooter; SetPositionAndPrev(shooter.X,shooter.Y+shooter.EyeHeight-.1,shooter.Z); double dx=target.X-X,dz=target.Z-Z,dy=target.Y+target.EyeHeight-Y+Math.Sqrt(dx*dx+dz*dz)*.2,len=Math.Sqrt(dx*dx+dy*dy+dz*dz); MotionX=dx/len*1.6; MotionY=dy/len*1.6; MotionZ=dz/len*1.6; }
    public override void Tick() { base.Tick(); MoveWithCollision(MotionX,MotionY,MotionZ); MotionY-=.03; foreach(var e in World.GetEntitiesInBox(BoundingBox.Grow(.2),this).OfType<LivingEntity>()) if(e!=Shooter) { e.Attack(new DamageSource("thrown",this,Shooter),e.TypeKey=="blaze"?3:0); Removed=true; } if(CollidedH||CollidedV||Age>100) Removed=true; }
}

public sealed partial class Pig
{
    public override bool Interact(Player p) { if(InteractLead(p)) return true; if(!IsBaby&&!Saddled&&p.Inventory.Selected?.Id==I.Saddle) { Saddled=true; ConsumeHeld(p); return true; } if(Saddled&&!IsBreedingItem(p.Inventory.Selected)) return Mount(p); return base.Interact(p); }
}

public sealed class LeashKnot : Entity
{
    public LeashKnot(World.World w):base(w) { Width=.375f; Height=.5f; NoGravity=true; }
    public override string TypeKey=>"leash_knot";
    public override bool Persistent=>true;
    public override bool CanBeAttacked=>true;
    public override bool Interact(Player p) { foreach(var mob in World.Entities.OfType<Mob>().Where(m=>m.LeashHolder==this)) { mob.LeashHolder=null; if(!p.Creative) World.DropItem(mob.X,mob.Y,mob.Z,new ItemStack(I.Lead)); } Removed=true; return true; }
    public override void Tick() { base.Tick(); if(!Block.Get(World.GetBlock(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Y),MathUtil.FloorToInt(Z))).Key.Contains("fence")) Removed=true; }
}
