using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public static partial class Blocks
{
    static partial void BlocksMobs()
    {
        new MobSpawnerBlock().T("mob_spawner").H(5,5).Tool_(ToolType.Pickaxe,0).Transparent();
        new MonsterEggBlock().T("stone").H(.75f,.75f).Vars(6);
    }
}

public sealed class MobSpawnerBlock : Block
{
    public MobSpawnerBlock():base(B.MobSpawner,"mob_spawner","Spawner") { HasTileEntity=true; DropOverride=0; }
    public override void OnAdded(World w,int x,int y,int z) => w.SetTileEntity(x,y,z,new MobSpawnerTile());
    public override void OnDestroyedByPlayer(World w,int x,int y,int z,int meta,Player p)
    { if(CanHarvest(p.Inventory.Selected)) w.SpawnXp(x+.5,y+.5,z+.5,15+w.Rand.Next(15)+w.Rand.Next(15)); }
}

public sealed class MonsterEggBlock : Block
{
    public MonsterEggBlock():base(B.MonsterEgg,"monster_egg","Zarażony blok") { DropOverride=0; }
    public static readonly int[] Hosts = { B.Stone,B.Cobblestone,B.StoneBricks,B.StoneBricks,B.StoneBricks,B.StoneBricks };
    public override int GetTexture(int face,int meta) => Block.Get(Hosts[Math.Clamp(meta,0,5)]).GetTexture(face,Math.Max(0,meta-2));
    public override int DropMeta(int meta)=>meta;
    public override ItemStack SilkTouchStack(int meta) => null; // Jedwabny dotyk daje blok-gospodarza (GetDrops)
    public override void GetDrops(World w,int x,int y,int z,int meta,Rng r,ItemStack tool,List<ItemStack> drops)
    {
        if(EnchantHelper.GetLevel(tool,Ench.SilkTouch)>0) { drops.Add(new ItemStack(Hosts[Math.Clamp(meta,0,5)],1,Math.Max(0,meta-2))); return; }
        Release(w,x,y,z);
    }
    public static void Release(World w,int x,int y,int z)
    {
        if(w.IsRemote || w.Difficulty==Difficulty.Peaceful) return;
        if(MobSpawner.Create("silverfish",w) is not Mob m) return;
        m.SetPositionAndPrev(x+.5,y,z+.5); w.SpawnEntity(m);
    }
}

public sealed class MobSpawnerTile : TileEntity
{
    public override int TypeId=>50;
    public string EntityId="pig";
    public int Delay=20, MinDelay=200, MaxDelay=800, SpawnCount=4, MaxNearby=6, PlayerRange=16, SpawnRange=4;
    public float Rotation,PrevRotation;
    public override void SetData(string key,string value)
    {
        if(key=="EntityId") EntityId=value.Replace("minecraft:","").ToLowerInvariant() switch { "cavespider"=>"cave_spider","pigzombie"=>"zombie_pigman","entityhorse"=>"horse","villagergolem"=>"iron_golem",var s=>s };
        else if(key=="Delay" && int.TryParse(value,out int d)) Delay=d;
    }
    public override void Tick(World w)
    {
        if(!w.Players.Any(p=>!p.Spectator && !p.Dead && p.DistanceSq(X+.5,Y+.5,Z+.5)<PlayerRange*PlayerRange)) return;
        PrevRotation=Rotation; Rotation=(Rotation+1000f/(Delay+200))%360;
        if(w.IsRemote) return;
        if(Delay<0) Reset(w);
        if(Delay>0) { Delay--; return; }
        bool spawned=false;
        for(int i=0;i<SpawnCount;i++) {
            if(MobSpawner.Create(EntityId,w) is not Mob mob) { Reset(w); return; }
            if(w.GetEntitiesInBox(new AABB(X,Y,Z,X+1,Y+1,Z+1).Grow(SpawnRange)).Count(e=>e.GetType()==mob.GetType())>=MaxNearby) { Reset(w); return; }
            mob.SetPositionAndPrev(X+.5+(w.Rand.NextDouble()-w.Rand.NextDouble())*SpawnRange,Y+w.Rand.Next(3)-1,Z+.5+(w.Rand.NextDouble()-w.Rand.NextDouble())*SpawnRange);
            if(!mob.CanSpawnHere()) continue;
            mob.Yaw=mob.PrevYaw=w.Rand.NextFloat()*360; w.SpawnEntity(mob); spawned=true;
            w.SpawnParticles(ParticleKind.Smoke,X+.5,Y+.5,Z+.5,8);
        }
        if(spawned) Reset(w);
    }
    private void Reset(World w)=>Delay=MinDelay+w.Rand.Next(Math.Max(1,MaxDelay-MinDelay));
    public override void Save(BinaryWriter b) { b.Write(EntityId); b.Write(Delay); b.Write(MinDelay); b.Write(MaxDelay); b.Write(SpawnCount); b.Write(MaxNearby); b.Write(PlayerRange); b.Write(SpawnRange); }
    public override void Load(BinaryReader b) { EntityId=b.ReadString(); Delay=b.ReadInt32(); MinDelay=b.ReadInt32(); MaxDelay=b.ReadInt32(); SpawnCount=b.ReadInt32(); MaxNearby=b.ReadInt32(); PlayerRange=b.ReadInt32(); SpawnRange=b.ReadInt32(); }
}
