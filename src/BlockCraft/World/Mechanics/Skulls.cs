using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public sealed class SkullTile : TileEntity
{
    public override int TypeId=>64;
    public int SkullType, Rotation;
    public Nbt Owner;
    public void SetFromItem(ItemStack stack,int rotation)
    {
        SkullType=Math.Clamp(stack.Damage,0,4);Rotation=rotation&15;Owner=null;
        if(SkullType!=3)return;
        var tag=stack.Tag;
        if(tag?.GetCompound("SkullOwner") is {} owner)Owner=owner.Copy();
        else if(tag?.GetString("SkullOwner") is {Length:>0} name)Owner=new Nbt{["Name"]=name};
    }
    public ItemStack ToItem()
    {
        var s=new ItemStack(I.SkullItem,1,SkullType);
        if(SkullType==3&&Owner!=null)s.GetOrCreateTag()["SkullOwner"]=Owner.Copy();
        return s;
    }
    public override void Save(BinaryWriter bw)
    {bw.Write((byte)SkullType);bw.Write((byte)Rotation);bw.Write(Owner!=null);Owner?.Write(bw);}
    public override void Load(BinaryReader br)
    {SkullType=Math.Clamp((int)br.ReadByte(),0,4);Rotation=br.ReadByte()&15;Owner=br.ReadBoolean()?Nbt.Read(br):null;}
}

public static class Skulls
{
    public static readonly string[] Names={"Czaszka szkieleta","Czaszka Witherowego szkieleta","Głowa zombie","Głowa gracza","Głowa creepera"};
    public static readonly string[] Icons={"skull_skeleton","skull_wither","skull_zombie","skull_steve","skull_creeper"};
    public static int Type(ItemStack s)=>Math.Clamp(s?.Damage??0,0,4);
    public static string Name(ItemStack s)
    {
        string name=s?.Tag?.GetCompound("SkullOwner")?.GetString("Name")??s?.Tag?.GetString("SkullOwner");
        return Type(s)==3&&!string.IsNullOrEmpty(name)?"Głowa gracza "+name:Names[Type(s)];
    }
    public static bool IsWither(World w,int x,int y,int z)=>w.GetBlock(x,y,z)==B.Skull&&w.GetTileEntity(x,y,z) is SkullTile{SkullType:1};
    public static bool Place(Player p,ItemStack s,int x,int y,int z,int face)
    {
        if(face==Dir.Down)return false;
        var w=p.World;var clicked=Block.Get(w.GetBlock(x,y,z));
        if(!clicked.Replaceable)
        {
            if(!clicked.Solid)return false;
            x+=Dir.X[face];y+=Dir.Y[face];z+=Dir.Z[face];
        }
        if(y<0||y>=Chunk.Height)return false;
        var at=Block.Get(w.GetBlock(x,y,z));
        if(at.Id!=B.Air&&!at.Replaceable)return false;
        if(w.IsRemote)return true;
        if(!w.SetBlock(x,y,z,B.Skull,face))return false;
        var tile=w.GetTileEntity(x,y,z) as SkullTile;
        tile?.SetFromItem(s,face==Dir.Up?MathUtil.FloorToInt(p.Yaw*16f/360f+.5f)&15:0);
        if(!p.Creative&&--s.Count<=0)p.Inventory.Selected=null;
        p.Inventory.Dirty=true;
        if(tile?.SkullType==1)Wither.TrySummon(w,x,y,z);
        return true;
    }
}

public sealed class SkullBlock : Block
{
    public SkullBlock():base(B.Skull,"skull","Czaszka")
    {
        T("soul_sand");H(1);Opaque=FullCube=false;LightOpacity=0;Render=RenderType.None;InCreative=false;HasTileEntity=true;
    }
    public override void OnAdded(World w,int x,int y,int z)
    {if(w.GetTileEntity(x,y,z) is not SkullTile)w.SetTileEntity(x,y,z,new SkullTile());}
    public override void OnRemoved(World w,int x,int y,int z,int oldMeta)=>w.RemoveTileEntity(x,y,z);
    public static AABB Shape(int meta)=>(meta&7) switch
    {
        2=>new(.25,.25,.5,.75,.75,1),3=>new(.25,.25,0,.75,.75,.5),
        4=>new(.5,.25,.25,1,.75,.75),5=>new(0,.25,.25,.5,.75,.75),
        _=>new(.25,0,.25,.75,.5,.75)
    };
    public override void GetCollisionBoxes(IBlockAccess w,int x,int y,int z,int meta,List<AABB> boxes)=>boxes.Add(Shape(meta));
    public override ItemStack PickStack(int meta)=>new(I.SkullItem);
    public override void GetDrops(World w,int x,int y,int z,int meta,Rng r,ItemStack tool,List<ItemStack> drops)
    {if((meta&8)==0)drops.Add(w.GetTileEntity(x,y,z) is SkullTile t?t.ToItem():new ItemStack(I.SkullItem));}
}
