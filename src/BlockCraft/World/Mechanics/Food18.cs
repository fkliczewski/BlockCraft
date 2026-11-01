using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>BlockCake 1.8 (aug): 7 kęsów, każdy +2 głodu i 0.4 nasycenia.</summary>
public sealed class CakeBlock : Block, IComparatorSource
{
    public CakeBlock() : base(B.Cake,"cake","Ciasto")
    {
        T("cake_top","cake_bottom","cake_side"); H(.5f); Snd(SoundType.Wool);
        Opaque = FullCube = false; LightOpacity=0; Render=RenderType.Model; InCreative=false;
    }
    public static AABB Shape(int bites) => new((1+Math.Clamp(bites,0,6)*2)/16.0,0,1/16.0,15/16.0,.5,15/16.0);
    public override int GetPlacementMeta(World w,int x,int y,int z,int face,double hx,double hy,double hz,Player p,int itemMeta)=>0;
    public override bool CanPlaceAt(World w,int x,int y,int z)=>y>0&&Block.Get(w.GetBlock(x,y-1,z)).Solid;
    public override void GetCollisionBoxes(IBlockAccess w,int x,int y,int z,int meta,List<AABB> boxes)=>boxes.Add(Shape(meta));
    public override void GetModel(IBlockAccess w,int x,int y,int z,int meta,List<ModelBox> boxes)
    {
        var b=Shape(meta);var tex=(int[])Tex.Clone();
        if(meta>0)tex[Dir.West]=BlockCraft.Render.TextureGen.Layer("cake_inner");
        var uv=new float[24];
        for(int f=0;f<6;f++)
        {
            float u0=f is 2 or 3 or 0 or 1?(float)b.MinX:1/16f,u1=15/16f;
            uv[f*4]=u0;uv[f*4+1]=f<2?1/16f:.5f;uv[f*4+2]=u1;uv[f*4+3]=f<2?15/16f:1;
        }
        boxes.Add(new ModelBox((float)b.MinX,0,(float)b.MinZ,(float)b.MaxX,.5f,(float)b.MaxZ,tex){Uv=uv});
    }
    private void Eat(World w,int x,int y,int z,Player p)
    {
        if(w.IsRemote||!p.CanEat(false))return;
        p.FoodLevel=Math.Min(20,p.FoodLevel+2);p.Saturation=Math.Min(p.FoodLevel,p.Saturation+.4f);
        p.Inventory.Dirty=true;
        int bites=w.GetMeta(x,y,z);
        if(bites<6)w.SetMeta(x,y,z,bites+1);else w.SetBlock(x,y,z,B.Air);
    }
    public override bool OnActivated(World w,int x,int y,int z,Player p){Eat(w,x,y,z,p);return true;}
    public override void OnClicked(World w,int x,int y,int z,Player p)=>Eat(w,x,y,z,p);
    public override void OnNeighborChanged(World w,int x,int y,int z,int neighborId)
    {if(!w.IsRemote&&!CanPlaceAt(w,x,y,z))w.SetBlock(x,y,z,B.Air);}
    public override int DropId(int meta,Rng r)=>0;
    public override ItemStack PickStack(int meta)=>new(I.CakeItem);
    public int GetComparatorOutput(World w,int x,int y,int z)=>(7-w.GetMeta(x,y,z))*2;
}
