using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

public abstract partial class Mob
{
    public string CustomName;
    public bool PersistenceRequired;
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
