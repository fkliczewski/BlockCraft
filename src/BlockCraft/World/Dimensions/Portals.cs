using System.Runtime.CompilerServices;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public readonly record struct PortalRectangle(int X,int Y,int Z,int Width,int Height,int Axis)
{
    public int Dx => Axis==1?1:0;
    public int Dz => Axis==2?1:0;
}

public static class DimensionPortals
{
    private sealed class Contact { public int Time,Cooldown; public bool Touched; }
    private static readonly ConditionalWeakTable<Entity,Contact> Contacts=new();
    private static bool Interior(int id)=>id is B.Air or B.Fire or B.Portal;
    public static bool FindFrame(World w,int x,int y,int z,int axis,out PortalRectangle frame)
    {
        frame=default; int dx=axis==1?1:0,dz=axis==2?1:0;
        if(!Interior(w.GetBlock(x,y,z)))return false;
        int bottom=y;
        while(bottom>0&&y-bottom<21&&Interior(w.GetBlock(x,bottom-1,z)))bottom--;
        if(w.GetBlock(x,bottom-1,z)!=B.Obsidian)return false;
        int left=0;
        while(left<21&&Interior(w.GetBlock(x-dx*(left+1),bottom,z-dz*(left+1))))left++;
        x-=left*dx;z-=left*dz;
        if(w.GetBlock(x-dx,bottom,z-dz)!=B.Obsidian)return false;
        int width=0;
        while(width<22&&Interior(w.GetBlock(x+dx*width,bottom,z+dz*width)))width++;
        if(width is <2 or >21 || w.GetBlock(x+dx*width,bottom,z+dz*width)!=B.Obsidian)return false;
        for(int i=0;i<width;i++)if(w.GetBlock(x+dx*i,bottom-1,z+dz*i)!=B.Obsidian)return false;
        int height=0;
        for(;height<=21;height++)
        {
            bool roof=true;
            for(int i=0;i<width;i++)if(w.GetBlock(x+dx*i,bottom+height,z+dz*i)!=B.Obsidian){roof=false;break;}
            if(roof)break;
            if(w.GetBlock(x-dx,bottom+height,z-dz)!=B.Obsidian||w.GetBlock(x+dx*width,bottom+height,z+dz*width)!=B.Obsidian)return false;
            for(int i=0;i<width;i++)if(!Interior(w.GetBlock(x+dx*i,bottom+height,z+dz*i)))return false;
        }
        if(height is <3 or >21)return false;
        frame=new(x,bottom,z,width,height,axis);return true;
    }
    public static bool Ignite(World w,int x,int y,int z)
    {
        if(w.IsRemote||w.Dimension==DimensionId.End)return false;
        for(int axis=1;axis<=2;axis++) if(FindFrame(w,x,y,z,axis,out var f))
        {
            for(int i=0;i<f.Width;i++)for(int j=0;j<f.Height;j++)w.SetBlock(f.X+i*f.Dx,f.Y+j,f.Z+i*f.Dz,B.Portal,axis,false);
            return true;
        }
        return false;
    }
    public static void ContactPortal(Entity entity,int block)
    {
        if(entity.World.IsRemote||entity.World.Dimensions==null||entity.Dead)return;
        var c=Contacts.GetOrCreateValue(entity);c.Touched=true;
        // MC 1.8: getPortalCooldown() = 10 dla gracza, 300 dla innych encji; stanie w portalu odnawia odliczanie
        int cooldown=entity is Player?10:300;
        if(c.Cooldown>0){c.Cooldown=cooldown;return;}
        if(block==B.Portal && ++c.Time < (entity is Player p&&!p.Creative?80:1))
        { if(c.Time==1)entity.World.PlaySound("portal.trigger",entity.X,entity.Y,entity.Z);return; }
        c.Time=0;c.Cooldown=cooldown;
        var set=entity.World.Dimensions;
        if(block==B.EndPortal)
        {
            if(entity.World.Dimension==DimensionId.End)
            {
                var home=set.Get(DimensionId.Overworld);
                set.QueueTransfer(entity,DimensionId.Overworld,home.SpawnX+.5,home.SpawnY,home.SpawnZ+.5);
            }
            else
            {
                var end=set.Get(DimensionId.End);end.EnsureDimensionArea(100,0,32);
                for(int dx=-2;dx<=2;dx++)for(int dz=-2;dz<=2;dz++)for(int dy=-1;dy<3;dy++)end.SetBlock(100+dx,49+dy,dz,dy<0?B.Obsidian:B.Air);
                set.QueueTransfer(entity,DimensionId.End,100.5,49,.5);
            }
            return;
        }
        var target=entity.World.Dimension==DimensionId.Nether?DimensionId.Overworld:DimensionId.Nether;
        double scale=target==DimensionId.Nether?.125:8;
        var world=set.Get(target);
        var destination=LocateOrBuild(world,Math.Clamp(entity.X*scale,-29999872,29999872),entity.Y,Math.Clamp(entity.Z*scale,-29999872,29999872));
        set.QueueTransfer(entity,target,destination.x,destination.y,destination.z);
    }
    public static (double x,double y,double z) LocateOrBuild(World w,double x,double y,double z)
    {
        int bx=MathUtil.FloorToInt(x),bz=MathUtil.FloorToInt(z);double nearest=double.MaxValue; (double x,double y,double z) result=default;
        for(int cx=(bx-128)>>4;cx<=(bx+128)>>4;cx++)for(int cz=(bz-128)>>4;cz<=(bz+128)>>4;cz++)
        {
            var chunk=w.GetChunk(cx,cz)??w.Storage?.LoadChunk(cx,cz);if(chunk==null)continue;
            for(int lx=0;lx<16;lx++)for(int lz=0;lz<16;lz++)
            {
                int px=cx*16+lx,pz=cz*16+lz;if(Math.Abs(px-bx)>128||Math.Abs(pz-bz)>128)continue;
                for(int py=0;py<Chunk.Height;py++)if(chunk.GetBlock(lx,py,lz)==B.Portal&&(py==0||chunk.GetBlock(lx,py-1,lz)!=B.Portal))
                {
                    double d=(px+.5-x)*(px+.5-x)+(py-y)*(py-y)+(pz+.5-z)*(pz+.5-z);
                    if(d<nearest){nearest=d;result=(px+.5,py,pz+.5);}
                }
            }
        }
        if(nearest<double.MaxValue){w.EnsureDimensionArea((int)result.x,(int)result.z,32);return result;}
        w.EnsureDimensionArea(bx,bz,32); int by=Math.Clamp((int)y,70,w.Dimension==DimensionId.Nether?118:245);
        double best=double.MaxValue;
        for(int dx=-16;dx<=16;dx++)for(int dz=-16;dz<=16;dz++)for(int py=4;py<(w.Dimension==DimensionId.Nether?123:250);py++)
        {
            bool clear=true;
            for(int a=-1;a<=2&&clear;a++)for(int b=-1;b<=1&&clear;b++)
            { if(!Block.Get(w.GetBlock(bx+dx+a,py-1,bz+dz+b)).Solid){clear=false;break;}for(int h=0;h<4;h++)if(w.GetBlock(bx+dx+a,py+h,bz+dz+b)!=B.Air){clear=false;break;} }
            double d=dx*dx+dz*dz+(py-y)*(py-y);if(clear&&d<best){best=d;result=(bx+dx,py,bz+dz);}
        }
        if(best<double.MaxValue){bx=(int)result.x;by=(int)result.y;bz=(int)result.z;}
        for(int dx=-1;dx<=2;dx++)for(int dz=-1;dz<=1;dz++)for(int h=-1;h<=3;h++)w.SetBlock(bx+dx,by+h,bz+dz,h<0?B.Obsidian:B.Air,0,false);
        BuildFrame(w,bx,by,bz,1);Ignite(w,bx,by,bz);return(bx+.5,by,bz+.5);
    }
    public static void BuildFrame(World w,int x,int y,int z,int axis,int width=2,int height=3)
    {
        int dx=axis==1?1:0,dz=axis==2?1:0;
        for(int i=-1;i<=width;i++)for(int j=-1;j<=height;j++)if(i is -1||i==width||j is -1||j==height)w.SetBlock(x+i*dx,y+j,z+i*dz,B.Obsidian,0,false);
    }
    public static void TickContacts(World w)
    {
        foreach(var e in w.Entities)
        {
            var c=Contacts.GetOrCreateValue(e);if(c.Cooldown>0)c.Cooldown--;c.Touched=false;
            var box=e.BoundingBox;bool portal=false;
            for(int x=MathUtil.FloorToInt(box.MinX+.001);x<=MathUtil.FloorToInt(box.MaxX-.001);x++)
            for(int y=MathUtil.FloorToInt(box.MinY-.001);y<=MathUtil.FloorToInt(box.MaxY-.001);y++)
            for(int z=MathUtil.FloorToInt(box.MinZ+.001);z<=MathUtil.FloorToInt(box.MaxZ-.001);z++)
            {
                byte id=w.GetBlock(x,y,z);
                if(id is B.Portal or B.EndPortal){if(!portal)WorldHooks.OnEntityInPortal?.Invoke(e,id);portal=true;}
                else if(id is B.SoulSand or B.Fire)Block.ById[id].OnEntityCollide(w,x,y,z,e);
            }
            if(!c.Touched)c.Time=Math.Max(0,c.Time-4);
        }
    }
    public static bool ActivateEnd(World w,int x,int y,int z)
    {
        for(int cx=x-2;cx<=x+2;cx++)for(int cz=z-2;cz<=z+2;cz++)
        {
            bool valid=true;
            bool Eye(int px,int pz,int facing)=>w.GetBlock(px,y,pz)==B.EndPortalFrame&&w.GetMeta(px,y,pz)==(facing|4);
            for(int i=-1;i<=1;i++)if(!Eye(cx+i,cz-2,0)||!Eye(cx+i,cz+2,2)||!Eye(cx-2,cz+i,3)||!Eye(cx+2,cz+i,1)){valid=false;break;}
            if(!valid)continue;
            for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)w.SetBlock(cx+dx,y,cz+dz,B.EndPortal);
            return true;
        }
        return false;
    }
}

public sealed partial class DimensionPortalBlock
{
    public override void OnNeighborChanged(World w,int x,int y,int z,int neighborId)
    {
        if(Id==B.Portal&&!DimensionPortals.FindFrame(w,x,y,z,w.GetMeta(x,y,z),out _))w.SetBlock(x,y,z,B.Air);
    }
    public override void OnRandomTick(World w,int x,int y,int z,Rng r)
    { w.SpawnParticles(ParticleKind.Portal,x+.5,y+.5,z+.5,4);if(r.Next(100)==0)w.PlaySound("portal.portal",x+.5,y+.5,z+.5,.5f); }
}

public sealed partial class EndFrameBlock
{
    public override bool OnActivated(World w,int x,int y,int z,Player p)
    {
        var held=p.Inventory.Selected;int meta=w.GetMeta(x,y,z);
        if(held?.Id!=I.EyeOfEnder||(meta&4)!=0)return false;
        if(w.IsRemote)return true;
        w.SetMeta(x,y,z,meta|4);if(!p.Creative&&--held.Count<=0)p.Inventory.Selected=null;p.Inventory.Dirty=true;
        w.PlaySound("random.orb",x,y,z,.5f,.8f);w.SpawnParticles(ParticleKind.Smoke,x+.5,y+.8,z+.5,16);
        DimensionPortals.ActivateEnd(w,x,y,z);return true;
    }
}
