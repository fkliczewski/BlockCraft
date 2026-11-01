using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public abstract partial class Mob
{
    public string CustomName;
    public bool PersistenceRequired;
    public Entity LeashHolder;
    public Entity Vehicle;
    public Player Rider;
    public bool Saddled;
    public virtual bool CanOpenDoors => false;
    public override bool Persistent => PersistenceRequired || !string.IsNullOrEmpty(CustomName);
    public int Looting => LastAttacker is Player p ? EnchantHelper.GetLevel(p.Inventory.Selected, Ench.Looting) : 0;
    private int _doorX, _doorY, _doorZ, _doorTicks = -1;

    public virtual bool CanSpawnHere()
    {
        int x=MathUtil.FloorToInt(X),y=MathUtil.FloorToInt(Y),z=MathUtil.FloorToInt(Z);
        if (!HasSpawnSpace() || !Block.Get(World.GetBlock(x,y-1,z)).Opaque) return false;
        if (this is Monster) return World.Difficulty != Difficulty.Peaceful && World.GetSkyLight(x,y,z)<=World.Rand.Next(32) && World.GetLight(x,y,z)<=World.Rand.Next(8);
        return World.GetBlock(x,y-1,z)==B.Grass && World.GetRawLight(x,y,z)>8;
    }
    public bool HasSpawnSpace(bool aquatic=false) => World.GetCollisionBoxes(BoundingBox,new List<AABB>()).Count==0
        && !World.IsAnyLiquid(BoundingBox,true) && (aquatic || !World.IsAnyLiquid(BoundingBox,false))
        && !World.GetEntitiesInBox(BoundingBox,this).Any(e=>e is LivingEntity && !e.Dead);
    public static void ConsumeHeld(Player p)
    {
        if(p.Creative) return;
        if(p.Inventory.Selected is { } s && --s.Count<=0) p.Inventory.Selected=null;
        p.Inventory.Dirty=true;
    }
    /// <summary>EntityAIOpenDoor: otwiera drzwi na ścieżce i zamyka je po 20 tickach.</summary>
    public void OpenDoorAt(int x,int y,int z)
    {
        if(World.GetBlock(x,y-1,z)==World.GetBlock(x,y,z)) y--;
        int meta=World.GetMeta(x,y,z);
        if((meta&8)!=0) return;
        if((meta&4)==0) { World.SetMeta(x,y,z,meta|4); World.PlaySound("random.door_open",x+.5,y+.5,z+.5,1f,World.Rand.NextFloat()*.1f+.9f); }
        _doorX=x; _doorY=y; _doorZ=z; _doorTicks=20;
    }
    private void TickDoor()
    {
        if(_doorTicks<0 || --_doorTicks>0) return;
        if(DistanceSq(_doorX+.5,Y,_doorZ+.5)<2.25) { _doorTicks=5; return; }
        _doorTicks=-1;
        if(!PathFinder.IsWoodDoor(World.GetBlock(_doorX,_doorY,_doorZ))) return;
        int meta=World.GetMeta(_doorX,_doorY,_doorZ);
        if((meta&4)!=0) { World.SetMeta(_doorX,_doorY,_doorZ,meta&~4); World.PlaySound("random.door_close",_doorX+.5,_doorY+.5,_doorZ+.5,1f,World.Rand.NextFloat()*.1f+.9f); }
    }
    private void SaveMobData(BinaryWriter b)
    {
        b.Write(0x4D4F3138); b.Write(CustomName??""); b.Write(PersistenceRequired); b.Write(Saddled);
    }
    private void LoadMobData(BinaryReader b)
    {
        long pos=b.BaseStream.Position;
        if(b.BaseStream.Length-pos<4) return;
        if(b.ReadInt32()!=0x4D4F3138) { b.BaseStream.Position=pos; return; }
        CustomName=b.ReadString(); PersistenceRequired=b.ReadBoolean(); Saddled=b.ReadBoolean();
        if(CustomName.Length==0) CustomName=null;
    }
    private bool TickMobControl()
    {
        TickDoor();
        if(Rider!=null && !Rider.Dead) {
            Nav.Stop();
            if(this is Pig pig && Saddled && Rider.Inventory.Selected?.Id==I.CarrotOnAStick) { pig.SteerByRider(Rider); return true; }
            if(this is Horse h && h.Tamed && Saddled) {
                Yaw=Rider.Yaw; Pitch=Rider.Pitch*.5f; BodyYaw=Yaw;
                AiMoveSpeed=(float)MoveSpeed; MoveForward=Rider.MoveForward; MoveStrafe=Rider.MoveStrafe*.5f; return true;
            }
            if(this is Horse untamed && !untamed.Tamed && !untamed.Undead) {
                if(World.Rand.Next(50)==0) {
                    if(World.Rand.Next(100)<untamed.Temper) untamed.Tame(Rider);
                    else { untamed.Temper=Math.Min(100,untamed.Temper+5); Dismount(); untamed.MakeRear(); }
                }
                else if(World.Rand.Next(20)==0) { AiMoveSpeed=(float)MoveSpeed*1.2f; MoveForward=1; Yaw+=World.Rand.NextFloat()*40-20; }
                return true;
            }
        }
        if(Vehicle!=null) {
            if(Vehicle.Dead||Vehicle.Removed) Vehicle=null;
            else { Nav.Stop(); SetPosition(Vehicle.X,RideY,Vehicle.Z); MotionX=MotionY=MotionZ=0; return Vehicle is not Mob; }
        }
        if(LeashHolder!=null) {
            double d=Distance(LeashHolder);
            if(LeashHolder.Removed||LeashHolder.Dead||d>10) { if(!World.IsRemote) Drop(I.Lead,1); LeashHolder=null; }
            else if(d>6) { MotionX+=(LeashHolder.X-X)/d*.4*(d-6)/4; MotionY+=Math.Max(0,(LeashHolder.Y-Y)/d*.4); MotionZ+=(LeashHolder.Z-Z)/d*.4*(d-6)/4; }
            else if(d>2 && Age%10==0) Nav.MoveTo(LeashHolder.X,LeashHolder.Y,LeashHolder.Z,MoveSpeed*1.2);
        }
        return false;
    }
    public override void Tick()
    {
        base.Tick();
        if(Vehicle!=null&&!Vehicle.Removed) { SetPosition(Vehicle.X,RideY,Vehicle.Z); MotionX=MotionY=MotionZ=0; FallDistance=0; }
        if(Rider!=null) {
            if(Dead||Removed||Rider.Dead||Rider.Removed||Rider.Sneaking) Dismount();
            else { Rider.SetPosition(X,Y+MountedYOffset,Z); Rider.MotionX=Rider.MotionY=Rider.MotionZ=0; Rider.FallDistance=0; }
        }
    }
    /// <summary>Wysokość stóp moba-jeźdźca: posY pojazdu + getMountedYOffset (pająk i kurczak połowa wysokości, reszta 3/4) + getYOffset jeźdźca (dorosły szkielet i zombie -0.35).</summary>
    private double RideY => Vehicle.Y + (Vehicle is Spider or Chicken ? Vehicle.Height*.5 : Vehicle.Height*.75) + (this is Skeleton || this is Zombie { Baby: false } ? -.35 : 0);
    /// <summary>getMountedYOffset z MC: wysokość siodła nad stopami moba.</summary>
    public virtual double MountedYOffset => Height*.75 - .2;
    public bool Mount(Player p)
    {
        // null wybiera to przeciążenie również dla moba wysiadającego z wagonika.
        if (p == null) { base.Mount(null); Dismount(); return true; }
        if(Rider!=null || World.IsRemote) return false; p.RidingMob?.Dismount(); Rider=p; p.RidingMob=this; PersistenceRequired=true; Nav.Stop(); return true;
    }
    public void Dismount()
    { if(Rider==null) return; var p=Rider; Rider=null; p.RidingMob=null; p.SetPositionAndPrev(X,Y+Height,Z); p.FallDistance=0; }
    public bool InteractLead(Player p)
    {
        if(LeashHolder==p) { LeashHolder=null; if(!p.Creative) Drop(I.Lead,1); return true; }
        if(p.Inventory.Selected?.Id!=I.Lead || !CanBeLeashed || LeashHolder!=null) return false;
        LeashHolder=p; PersistenceRequired=true; ConsumeHeld(p); return true;
    }
    /// <summary>allowLeashing z MC: potwory i nietoperze nie, golemy i zwierzęta tak.</summary>
    public virtual bool CanBeLeashed => this is not Monster && this is not Bat && this is not Squid;
    public override void OnDeath(DamageSource src)
    {
        if(Dead) return;
        if(!World.IsRemote && src.Attacker is Creeper c && c.Charged && c.HeadsDropped==0 && this!=c && this is Zombie or Skeleton or WitherSkeleton or Creeper)
        {
            c.HeadsDropped++;Drop(I.SkullItem,1,this is WitherSkeleton?1:this is Skeleton?0:this is Zombie?2:4);
        }
        if(LeashHolder!=null) { Drop(I.Lead,1); LeashHolder=null; }
        if(Saddled) Drop(I.Saddle,1);
        Saddled=false;
        Dismount();
        base.OnDeath(src);
    }
}

public sealed partial class Pig
{
    private float _steerSpeed; private int _boost,_boostMax;
    /// <summary>EntityAIControlledByPlayer z 1.8: świnia skręca za wzrokiem gracza i rozpędza się do 0.3.</summary>
    public void SteerByRider(Player rider)
    {
        float turn=Math.Clamp(MathUtil.WrapDegrees(rider.Yaw-Yaw)*.5f,-5f,5f);
        Yaw=MathUtil.WrapDegrees(Yaw+turn); BodyYaw=Yaw; Pitch=0;
        if(_steerSpeed<.3f) _steerSpeed+=(.3f-_steerSpeed)*.01f;
        float speed=_steerSpeed;
        if(_boost>0) { speed+=speed*1.15f*MathF.Sin((float)_boost++/_boostMax*MathF.PI); if(_boost>_boostMax) _boost=0; }
        AiMoveSpeed=speed; MoveForward=1; MoveStrafe=0;
        if(CollidedH && OnGround) IsJumping=true;
    }
    /// <summary>Prawy klik marchewką na patyku podczas jazdy: przyspieszenie, -7 trwałości.</summary>
    public bool Boost(Player p)
    {
        if(_boost>0) return false;
        _boost=1; _boostMax=World.Rand.Next(841)+140;
        if(!p.Creative && p.Inventory.Selected is { } s) {
            s.Damage+=7;
            if(s.Damage>=(s.Item?.MaxDamage??25)) p.Inventory.Selected=new ItemStack(I.FishingRod);
            p.Inventory.Dirty=true;
        }
        return true;
    }
    public override bool Interact(Player p)
    {
        if(InteractLead(p)) return true;
        if(!IsBaby&&!Saddled&&p.Inventory.Selected?.Id==I.Saddle) { Saddled=true; ConsumeHeld(p); World.PlaySound("mob.horse.leather",X,Y,Z,.5f,1f); return true; }
        if(Saddled&&!IsBreedingItem(p.Inventory.Selected)) return Mount(p);
        return base.Interact(p);
    }
    public override void OnStruckByLightning(Entity bolt)
    {
        if(World.IsRemote||Dead) return;
        var zp=MobSpawner.Create("zombie_pigman",World) as Mob;
        if(zp==null) { base.OnStruckByLightning(bolt); return; }
        zp.SetPositionAndPrev(X,Y,Z); zp.Yaw=Yaw; zp.CustomName=CustomName; World.SpawnEntity(zp); Removed=true;
    }
}

public sealed partial class Player
{
    public Mob RidingMob;
    private bool TravelMounted(float strafe,float forward)
    {
        if(RidingMob==null) return false;
        if(RidingMob.Dead||RidingMob.Removed) { RidingMob.Dismount(); return false; }
        MotionX=MotionY=MotionZ=0; FallDistance=0;
        SetPosition(RidingMob.X,RidingMob.Y+RidingMob.MountedYOffset,RidingMob.Z); return true;
    }
    /// <summary>Otwiera kontener encji (koń, wieśniak) w GUI gracza lokalnego.</summary>
    public void OpenEntityContainer(Container c)
    {
        if(OpenContainer!=null) CloseContainer();
        OpenContainer=c;
        if(Handler is Game g) g.OpenEntityScreen(this,c);
    }
}

public static partial class Registries
{
    static partial void EntitiesMobs()
    {
        Tile(50,()=>new MobSpawnerTile());
        RegisterOverworldMobs();
        RegisterMobRenderers();
    }
    static partial void RegisterOverworldMobs();
    static partial void RegisterMobRenderers();
}
