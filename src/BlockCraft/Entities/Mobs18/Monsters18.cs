using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public static partial class Registries
{
    static partial void RegisterOverworldMobs()
    {
        Mob("cave_spider",w=>new CaveSpider(w)); Mob("slime",w=>new Slime(w));
        Mob("silverfish",w=>new Silverfish(w)); Mob("witch",w=>new Witch(w));
        Mob("guardian",w=>new Guardian(w)); Mob("elder_guardian",w=>new Guardian(w,true));
        Mob("giant",w=>new Giant(w)); Mob("zombie_villager",w=>new Zombie(w){Villager=true});
        Mob("baby_zombie",w=>new Zombie(w){Baby=true});
        Mob("spider_jockey",w=>new SpiderJockey(w)); Mob("chicken_jockey",w=>new ChickenJockey(w));
        Entity("witch_potion",w=>new WitchPotion(w));
        RegisterAnimals18();
    }
    static partial void RegisterAnimals18();
}

public sealed class CaveSpider : Spider
{
    public CaveSpider(World.World w):base(w) { Width=.7f; Height=.5f; MaxHealth=Health=12; }
    public override string TypeKey=>"cave_spider";
    public override string DisplayName=>"Pająk jaskiniowy";
    public override float EyeHeight=>.45f;
    public override bool CanHaveEffect(Effect id)=>id!=Effect.Poison && base.CanHaveEffect(id);
    protected override void OnMeleeHit(LivingEntity target)
    { if(World.Difficulty is Difficulty.Normal or Difficulty.Hard) target.AddEffect(Effect.Poison,World.Difficulty==Difficulty.Hard?300:140); }
}

public sealed class Slime : Monster
{
    public int Size { get; private set; }
    private int _jumpDelay;
    public Slime(World.World w):base(w) { SetSize(1<<w.Rand.Next(3)); }
    public override string TypeKey=>"slime";
    public override string DisplayName=>"Szlam";
    public override string HurtSound=>Size>1?"mob.slime.big":"mob.slime.small";
    public override int XpValue=>Size;
    public void SetSize(int size,bool heal=true) { Size=Math.Clamp(size,1,16); Width=Height=.51000005f*Size; MaxHealth=Size*Size; if(heal) Health=MaxHealth; MoveSpeed=.2+.1*Size; AttackDamage=Size; }
    protected override void Think()
    {
        Target=NearestPlayer(16,true);
        if(Target!=null) { FaceTowards(Target.X,Target.Z); LookTarget=Target; LookTime=10; }
        if(OnGround && --_jumpDelay<=0) { _jumpDelay=(10+World.Rand.Next(20))/(Target!=null?3:1); IsJumping=true; SetMoveSpeed((float)MoveSpeed); World.PlaySound(HurtSound,X,Y,Z); }
        else if(!OnGround) SetMoveSpeed((float)MoveSpeed);
        if(Size>1 && Target!=null && DistanceSq(Target)<.36*Size*Size && CanSee(Target)) Target.Attack(DamageSource.Mob(this),Size);
    }
    public override void OnDeath(DamageSource source)
    {
        if(Dead) return;
        if(!World.IsRemote && Size>1) for(int i=0,n=2+World.Rand.Next(3);i<n;i++) {
            var child=new Slime(World){CustomName=CustomName,PersistenceRequired=PersistenceRequired}; child.SetSize(Size/2);
            child.SetPositionAndPrev(X+(i%2-.5)*Size/2,Y+.5,Z+(i/2-.5)*Size/2); World.SpawnEntity(child);
        }
        base.OnDeath(source);
    }
    protected override void DropLoot(bool player,bool burning) { if(Size==1) Drop(I.Slimeball,World.Rand.Next(3)+World.Rand.Next(Looting+1)); }
    public static bool IsSlimeChunk(long seed,int x,int z)
    {
        // java.util.Random, wraz z przepełnieniem int w składnikach współrzędnych.
        long s=unchecked(seed+(int)(x*x*4987142)+(int)(x*5947611)+(long)(int)(z*z)*4392871+(int)(z*389711))^987234911L;
        long state=(s^0x5DEECE66DL)&((1L<<48)-1); int bits,val;
        do { state=(state*0x5DEECE66DL+11)&((1L<<48)-1); bits=(int)(state>>17); val=bits%10; } while(unchecked(bits-val+9)<0);
        return val==0;
    }
    public override bool CanSpawnHere()
    {
        if(World.Difficulty==Difficulty.Peaceful || !HasSpawnSpace()) return false;
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        if(World.GetBiome(x,z)==6 && y>50 && y<70) {
            double[] moon={1,.75,.5,.25,0,.25,.5,.75};
            if(World.Rand.NextFloat()<.5 && World.Rand.NextFloat()<moon[(World.TotalTime/24000)%8] && World.GetLight(x,y,z)<=World.Rand.Next(8)) return true;
        }
        return y<40 && World.Rand.Next(10)==0 && IsSlimeChunk(World.Seed,x>>4,z>>4);
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Size); }
    public override void Load(BinaryReader b) { base.Load(b); SetSize(b.ReadInt32(),false); }
    public override int NetData=>Size;
    public override void SetNetData(int d)=>SetSize(d,false);
}

public sealed class Silverfish : Monster
{
    private int _alert;
    public Silverfish(World.World w):base(w) { Width=.4f; Height=.3f; MaxHealth=Health=8; MoveSpeed=.25; AttackDamage=1; }
    public override string TypeKey=>"silverfish";
    public override string DisplayName=>"Rybik cukrowy";
    public override string LivingSound=>"mob.silverfish.say";
    public override string HurtSound=>"mob.silverfish.hit";
    public override string DeathSound=>"mob.silverfish.kill";
    protected override void OnHurt(DamageSource src) { base.OnHurt(src); if(_alert==0 && src.Attacker is LivingEntity) _alert=20; }
    protected override void Think()
    {
        if(_alert>0 && --_alert==0) {
            int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
            for(int dy=-5;dy<=5;dy++) for(int dx=-10;dx<=10;dx++) for(int dz=-10;dz<=10;dz++)
                if(World.GetBlock(x+dx,y+dy,z+dz)==B.MonsterEgg) { World.BreakBlock(x+dx,y+dy,z+dz,true); if(World.Rand.NextBool()) return; }
        }
        AcquireTarget();
        if(LastAttacker is LivingEntity le && !le.Dead) Target=le;
        if(Target!=null) { MeleeChase(MoveSpeed); return; }
        if(!Nav.HasPath && World.Rand.Next(10)==0) {
            int d=World.Rand.Next(6),x=MathUtil.FloorToInt(X)+Dir.X[d],y=MathUtil.FloorToInt(Y)+Dir.Y[d],z=MathUtil.FloorToInt(Z)+Dir.Z[d];
            int id=World.GetBlock(x,y,z),meta=World.GetMeta(x,y,z);
            int inf=id==B.Stone && meta==0?0:id==B.Cobblestone?1:id==B.StoneBricks?2+meta:-1;
            if(inf is >=0 and <6) { World.SetBlock(x,y,z,B.MonsterEgg,inf); Removed=true; return; }
        }
        Wander(MoveSpeed); LookAtPlayersRandomly();
    }
    public override bool CanSpawnHere()=>base.CanSpawnHere() && !World.Players.Any(p=>!p.Spectator && p.DistanceSq(this)<25);
}

public sealed class Witch : Monster
{
    private int _drink,_throw; private Effect _drinkingEffect;
    public bool Drinking=>_drink>0;
    public Witch(World.World w):base(w) { Height=1.95f; MaxHealth=Health=26; MoveSpeed=.25; }
    public override string TypeKey=>"witch";
    public override string DisplayName=>"Wiedźma";
    public override string HurtSound=>null;
    public override string DeathSound=>null;
    public override bool Attack(DamageSource src,float amount)=>base.Attack(src,src.Attacker==this?0:src.Type=="magic"?amount*.15f:amount);
    protected override void Think()
    {
        AcquireTarget();
        if(_drink>0) { Nav.Stop(); if(--_drink==0) { if(_drinkingEffect==Effect.InstantHealth) Heal(4); else AddEffect(_drinkingEffect,3600); HeldItem=null; } return; }
        Effect? effect=null;
        if(InWater&&!HasEffect(Effect.WaterBreathing)&&World.Rand.NextFloat()<.15) effect=Effect.WaterBreathing;
        else if(FireTicks>0&&!HasEffect(Effect.FireResistance)&&World.Rand.NextFloat()<.15) effect=Effect.FireResistance;
        else if(Health<MaxHealth&&World.Rand.NextFloat()<.05) effect=Effect.InstantHealth;
        else if(Target!=null&&DistanceSq(Target)>121&&!HasEffect(Effect.Speed)&&World.Rand.NextFloat()<.25) effect=Effect.Speed;
        if(effect.HasValue) { _drinkingEffect=effect.Value; _drink=32; HeldItem=new ItemStack(I.Potion); return; }
        if(Target==null) { Wander(MoveSpeed); LookAtPlayersRandomly(); return; }
        LookTarget=Target; LookTime=10;
        if(DistanceSq(Target)>100||!CanSee(Target)) { if(Age%10==0) Nav.MoveTo(Target.X,Target.Y,Target.Z,MoveSpeed); }
        else { Nav.Stop(); if(--_throw<=0) { _throw=60; var p=new WitchPotion(World); p.Throw(this,Target); World.SpawnEntity(p); } }
    }
    protected override void DropLoot(bool player,bool burning)
    {
        int[] ids={I.GlowstoneDust,I.Sugar,I.Redstone,I.SpiderEye,I.GlassBottle,I.Gunpowder,I.Stick,I.Stick};
        for(int i=0,n=1+World.Rand.Next(3);i<n;i++) Drop(ids[World.Rand.Next(ids.Length)],World.Rand.Next(3)+World.Rand.Next(Looting+1));
    }
}

public sealed class WitchPotion : Entity
{
    public LivingEntity Shooter;
    public Effect PotionEffect=Effect.InstantDamage;
    public WitchPotion(World.World w):base(w) { Width=Height=.25f; }
    public override string TypeKey=>"witch_potion";
    public void Throw(LivingEntity shooter,LivingEntity target)
    {
        Shooter=shooter; SetPositionAndPrev(shooter.X,shooter.Y+shooter.EyeHeight-.1,shooter.Z);
        double dx=target.X-X,dz=target.Z-Z,h=Math.Sqrt(dx*dx+dz*dz),dy=target.Y+target.EyeHeight-1.1-Y+h*.2;
        double len=Math.Sqrt(dx*dx+dy*dy+dz*dz); MotionX=dx/len*.75; MotionY=dy/len*.75; MotionZ=dz/len*.75;
        PotionEffect=h>=8&&!target.HasEffect(Effect.Slowness)?Effect.Slowness:target.Health>=8&&!target.HasEffect(Effect.Poison)?Effect.Poison:h<=3&&!target.HasEffect(Effect.Weakness)&&World.Rand.NextFloat()<.25?Effect.Weakness:Effect.InstantDamage;
    }
    public override void Tick()
    {
        base.Tick(); if(World.IsRemote) return;
        double speed=Math.Sqrt(MotionX*MotionX+MotionY*MotionY+MotionZ*MotionZ);
        var hit=World.RayCast(X,Y,Z,MotionX/speed,MotionY/speed,MotionZ/speed,speed);
        MoveWithCollision(MotionX,MotionY,MotionZ); MotionX*=.99; MotionZ*=.99; MotionY=MotionY*.99-.05;
        var direct=World.GetEntitiesInBox(BoundingBox.Grow(.2),this).OfType<LivingEntity>().FirstOrDefault(e=>e!=Shooter);
        if(!hit.Hit&&!CollidedH&&!CollidedV&&direct==null&&Age<200) return;
        foreach(var e in World.GetEntitiesInBox(BoundingBox.Grow(4,2,4),this).OfType<LivingEntity>()) {
            double d=Distance(e); if(d>=4) continue; float strength=e==direct?1:(float)(1-d/4);
            if(PotionEffect==Effect.InstantDamage) e.Attack(new DamageSource("magic",this,Shooter){BypassArmor=true},MathF.Floor(6*strength+.5f));
            else { int ticks=(int)((PotionEffect==Effect.Poison?900:PotionEffect==Effect.Slowness?1800:1800)*strength+.5f); if(ticks>20) e.AddEffect(PotionEffect,ticks); }
        }
        World.PlaySound("game.potion.smash",X,Y,Z); Removed=true;
    }
}

public sealed class Guardian : Monster
{
    public bool Elder; public int BeamTicks;
    public Guardian(World.World w,bool elder=false):base(w) { Elder=elder; Width=Height=elder?1.9975f:.85f; MaxHealth=Health=elder?80:30; MoveSpeed=elder?.3:.5; AttackDamage=elder?8:6; PersistenceRequired=elder; }
    public override string TypeKey=>Elder?"elder_guardian":"guardian";
    public override string DisplayName=>Elder?"Prastrażnik":"Strażnik";
    public override bool CanBreatheUnderwater=>true;
    public override string LivingSound=>Elder?"mob.guardian.elder.idle":"mob.guardian.idle";
    public override string HurtSound=>Elder?"mob.guardian.elder.hit":"mob.guardian.hit";
    public override string DeathSound=>Elder?"mob.guardian.elder.death":"mob.guardian.death";
    public override bool CanSpawnHere()=>World.Difficulty!=Difficulty.Peaceful && World.GetBlock(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Y),MathUtil.FloorToInt(Z))==B.Water && HasSpawnSpace(true) && (World.Rand.Next(20)==0 || !World.CanSeeSky(MathUtil.FloorToInt(X),MathUtil.FloorToInt(Y),MathUtil.FloorToInt(Z)));
    public override bool Attack(DamageSource src,float amount)
    {
        if(src.Attacker is LivingEntity e && !src.IsExplosion && src.Type!="thorns" && !Nav.HasPath) e.Attack(new DamageSource("thorns",this),2);
        return base.Attack(src,amount);
    }
    protected override void Think()
    {
        if(Elder && Age%1200==0) foreach(var p in World.Players)
            if(!p.Creative&&!p.Spectator&&p.DistanceSq(this)<2500 && (p.EffectLevel(Effect.MiningFatigue)<2 || p.Effects[Effect.MiningFatigue].Duration<1200)) p.AddEffect(Effect.MiningFatigue,6000,2);
        if(!InWater) { if(OnGround) { MotionY=.5; MotionX+=(World.Rand.NextFloat()-.5)*.8; MotionZ+=(World.Rand.NextFloat()-.5)*.8; } BeamTicks=0; return; }
        AcquireTarget();
        Target??=World.GetEntitiesInBox(BoundingBox.Grow(16),this).OfType<LivingEntity>().FirstOrDefault(e=>e.TypeKey=="squid"&&DistanceSq(e)>9);
        if(Target==null) { BeamTicks=0; MotionY+=Math.Sin(Age*.1)*.003; return; }
        if(!CanSee(Target)||(!Elder&&DistanceSq(Target)<9)) { Target=null; BeamTicks=0; return; }
        LookTarget=Target; LookTime=10; FaceTowards(Target.X,Target.Z); Nav.Stop();
        if(++BeamTicks==1) World.PlaySound("mob.guardian.attack",X,Y,Z);
        if(BeamTicks>=(Elder?60:80)) {
            Target.Attack(new DamageSource("indirectMagic",this){BypassArmor=true},1+(World.Difficulty==Difficulty.Hard?2:0)+(Elder?2:0));
            Target.HurtResistantTime=0; Target.Attack(DamageSource.Mob(this),AttackDamage); BeamTicks=0; Target=null;
        }
    }
    public override void Travel(float strafe,float forward)
    { if(InWater) { MoveFlying(strafe,forward,.1f); MoveWithCollision(MotionX,MotionY,MotionZ); MotionX*=.9; MotionY*=.9; MotionZ*=.9; } else base.Travel(strafe,forward); }
    protected override void DropLoot(bool player,bool burning)
    { Drop(I.PrismarineShard,World.Rand.Next(3)+World.Rand.Next(Looting+1)); if(World.Rand.Next(3)==0) Drop(I.RawFish,1); else if(World.Rand.Next(3)==0) Drop(I.PrismarineCrystals,1); if(Elder&&player) Drop(B.Sponge,1,1); }
}

public sealed class Giant : Monster
{
    public Giant(World.World w):base(w) { Width=3.6f; Height=10.8f; MaxHealth=Health=100; MoveSpeed=.5; AttackDamage=50; }
    public override string TypeKey=>"giant";
    public override string DisplayName=>"Gigant";
    public override bool CanSpawnHere()=>false;
    protected override void Think() { }
}

public sealed class SpiderJockey : Spider
{
    public SpiderJockey(World.World w):base(w) { }
    public override string TypeKey=>"spider_jockey";
    public override void Tick() { base.Tick(); if(Age==1&&!World.IsRemote) { var s=new Skeleton(World){Vehicle=this}; s.SetPositionAndPrev(X,Y+Height*.75,Z); World.SpawnEntity(s); } }
}
public sealed class ChickenJockey : Zombie
{
    public ChickenJockey(World.World w):base(w) { Baby=true; }
    public override string TypeKey=>"chicken_jockey";
    public override void Tick() { base.Tick(); if(Age==1&&!World.IsRemote) { var c=new Chicken(World); c.SetPositionAndPrev(X,Y,Z); World.SpawnEntity(c); Vehicle=c; } }
}

public partial class Zombie
{
    private bool _baby;
    public bool Baby { get=>_baby; set { _baby=value; Width=value?.3f:.6f; Height=value?.975f:1.95f; MoveSpeed=value?.345:.23; } }
    public bool Villager;
    public int ConversionTicks=-1,DoorTicks;
    private bool TickZombie()
    {
        if(ConversionTicks>=0 && --ConversionTicks<=0) {
            if(MobSpawner.Create("villager",World) is Mob v) { v.SetPositionAndPrev(X,Y,Z); v.CustomName=CustomName; v.PersistenceRequired=true; World.SpawnEntity(v); Removed=true; World.PlaySound("mob.zombie.unfect",X,Y,Z); }
            return true;
        }
        if(Target==null) Target=World.GetEntitiesInBox(BoundingBox.Grow(16),this).OfType<LivingEntity>().FirstOrDefault(e=>e.TypeKey=="villager");
        if(CollidedH && Target!=null && World.Difficulty==Difficulty.Hard) {
            int x=MathUtil.FloorToInt(X-Math.Sin(Yaw*MathUtil.Deg2Rad)*.8),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z+Math.Cos(Yaw*MathUtil.Deg2Rad)*.8);
            if(World.GetBlock(x,y,z)==B.OakDoor) { if(++DoorTicks%20==0) World.PlaySound("mob.zombie.wood",X,Y,Z); if(DoorTicks>=240) { World.BreakBlock(x,y,z,true); DoorTicks=0; } return true; }
        }
        DoorTicks=0; return false;
    }
    public override bool Interact(Player p)
    {
        if(Villager&&ConversionTicks<0&&HasEffect(Effect.Weakness)&&p.Inventory.Selected is {Id:I.GoldenApple,Damage:0}) {
            ConsumeHeld(p); RemoveEffect(Effect.Weakness); ConversionTicks=3600+World.Rand.Next(2401); AddEffect(Effect.Strength,ConversionTicks,Math.Max(0,(int)World.Difficulty-1)); PersistenceRequired=true; World.PlaySound("mob.zombie.remedy",X,Y,Z); return true;
        }
        return base.Interact(p);
    }
    protected override void OnMeleeHit(LivingEntity target)
    {
        if(target.Dead && target.TypeKey=="villager" && World.Difficulty!=Difficulty.Easy && (World.Difficulty==Difficulty.Hard || World.Rand.NextBool())) {
            target.Removed=true; var z=new Zombie(World){Villager=true,CustomName=(target as Mob)?.CustomName}; z.SetPositionAndPrev(target.X,target.Y,target.Z); World.SpawnEntity(z); World.PlaySound("mob.zombie.infect",X,Y,Z);
        }
    }
    protected override void OnHurt(DamageSource src)
    {
        base.OnHurt(src);
        if(World.Difficulty!=Difficulty.Hard || World.Rand.NextFloat()>=.05) return;
        for(int i=0;i<50;i++) {
            var z=new Zombie(World){Target=src.Attacker as LivingEntity};
            z.SetPositionAndPrev(X+(7+World.Rand.Next(34))*(World.Rand.NextBool()?1:-1),Y+World.Rand.Next(7)-3,Z+(7+World.Rand.Next(34))*(World.Rand.NextBool()?1:-1));
            if(z.CanSpawnHere()&&!World.Players.Any(p=>p.DistanceSq(z)<49)) { World.SpawnEntity(z); break; }
        }
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Baby); b.Write(Villager); b.Write(ConversionTicks); }
    public override void Load(BinaryReader b) { base.Load(b); if(b.BaseStream.Position<b.BaseStream.Length) { Baby=b.ReadBoolean(); Villager=b.ReadBoolean(); ConversionTicks=b.ReadInt32(); } }
}

public sealed partial class Creeper
{
    public bool Charged; public int HeadsDropped;
    public override void OnStruckByLightning(Entity bolt) { base.OnStruckByLightning(bolt); Charged=true; }
    private bool AvoidCats()
    {
        var cat=World.GetEntitiesInBox(BoundingBox.Grow(6),this).FirstOrDefault(e=>e.TypeKey=="ocelot");
        if(cat==null) return false;
        if(Age%10==0||!Nav.HasPath) Nav.MoveTo(X+(X-cat.X)*2,Y,Z+(Z-cat.Z)*2,MoveSpeed*1.2);
        return true;
    }
    public override void Save(BinaryWriter b) { base.Save(b); b.Write(Charged); b.Write(HeadsDropped); }
    public override void Load(BinaryReader b) { base.Load(b); if(b.BaseStream.Position<b.BaseStream.Length) { Charged=b.ReadBoolean(); HeadsDropped=b.ReadInt32(); } }
}
