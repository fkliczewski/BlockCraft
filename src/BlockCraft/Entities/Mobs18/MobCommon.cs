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
    }
    private bool TickMobControl()
    {
        if(Vehicle!=null) {
            if(Vehicle.Dead||Vehicle.Removed) Vehicle=null;
            else { Nav.Stop(); SetPosition(Vehicle.X,Vehicle.Y+Vehicle.Height*.75,Vehicle.Z); MotionX=MotionY=MotionZ=0; return true; }
        }
        if(LeashHolder!=null) {
            double d=Distance(LeashHolder);
            if(LeashHolder.Removed||LeashHolder.Dead||d>10) { Drop(I.Lead,1); LeashHolder=null; }
            else if(d>6) { MotionX+=(LeashHolder.X-X)/d*.4; MotionY+=(LeashHolder.Y-Y)/d*.4; MotionZ+=(LeashHolder.Z-Z)/d*.4; }
            else if(d>2 && Age%10==0) Nav.MoveTo(LeashHolder.X,LeashHolder.Y,LeashHolder.Z,MoveSpeed);
        }
        return false;
    }
    public override void OnDeath(DamageSource src)
    {
        if(Dead) return;
        if(!World.IsRemote && src.Attacker is Creeper c && c.Charged && c.HeadsDropped==0 && this!=c && this is Zombie or Skeleton or Creeper)
        { c.HeadsDropped++; Drop(I.SkullItem,1,this is Skeleton?0:this is Zombie?2:4); }
        if(LeashHolder!=null) { Drop(I.Lead,1); LeashHolder=null; }
        if(Saddled) Drop(I.Saddle,1);
        base.OnDeath(src);
    }
}

public static partial class Registries
{
    static partial void EntitiesMobs()
    {
        Tile(50,()=>new MobSpawnerTile());
        RegisterOverworldMobs();
    }
    static partial void RegisterOverworldMobs();
}
