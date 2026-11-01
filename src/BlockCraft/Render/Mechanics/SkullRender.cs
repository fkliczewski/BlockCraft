using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed partial class EntityRenderer
{
    private static readonly ModelPart SkullHead=new ModelPart().Box(0,0,-4,-8,-4,8,8,8);
    private static readonly ModelPart SkullHat=new ModelPart().Box(32,0,-4,-8,-4,8,8,8,.25f);
    private static readonly ModelPart WornSkull=new ModelPart().Box(0,0,-4,-8,-4,8,8,8,.5f);
    public void RenderSkullItem(ItemStack item,Matrix4 transform)
    {
        Skin.Layer=SkinGen.Get("skull_"+Skulls.Type(item));Skin.Sky=Terrain.Sky;Skin.Blk=Terrain.Blk;Skin.Color=Vector4.One;
        var root=Matrix4.CreateScale(1,-1,-1)*Matrix4.CreateTranslation(0,-.25f,0)*transform;
        RenderPart(Skin,SkullHead,root);
        if(Skulls.Type(item)==3)RenderPart(Skin,SkullHat,root);
    }
    internal static void DrawSkulls(EntityRenderer r,World.World w,float pt,Vector3d cam)
    {
        int cx=MathUtil.FloorToInt(cam.X)>>4,cz=MathUtil.FloorToInt(cam.Z)>>4;
        for(int x=cx-4;x<=cx+4;x++)for(int z=cz-4;z<=cz+4;z++)
        {
            var chunk=w.GetChunk(x,z);if(chunk==null)continue;
            foreach(var te in chunk.TileEntities.Values)
            {
                if(te is not SkullTile skull||w.GetBlock(te.X,te.Y,te.Z)!=B.Skull)continue;
                var pos=new Vector3((float)(te.X-cam.X),(float)(te.Y-cam.Y),(float)(te.Z-cam.Z));
                if(pos.LengthSquared>64*64)continue;
                int face=w.GetMeta(te.X,te.Y,te.Z)&7;
                float angle=skull.Rotation*22.5f;
                var center=new Vector3(.5f,0,.5f);
                switch(face)
                {
                    case 2:center=new(.5f,.25f,.75f);angle=180;break;
                    case 3:center=new(.5f,.25f,.25f);angle=0;break;
                    case 4:center=new(.75f,.25f,.5f);angle=90;break;
                    case 5:center=new(.25f,.25f,.5f);angle=270;break;
                }
                var root=Matrix4.CreateScale(1,-1,-1)*Matrix4.CreateRotationY(-angle*MathUtil.Deg2Rad)*Matrix4.CreateTranslation(pos+center);
                r.Skin.Sky=w.GetSkyLight(te.X,te.Y,te.Z);r.Skin.Blk=w.GetBlockLight(te.X,te.Y,te.Z);
                r.Skin.Color=Vector4.One;r.Skin.Overlay=Vector4.Zero;r.Skin.Layer=SkinGen.Get("skull_"+skull.SkullType);
                r.RenderPart(r.Skin,SkullHead,root);
                if(skull.SkullType==3)r.RenderPart(r.Skin,SkullHat,root);
            }
        }
    }
}

public static partial class SkinGen
{
    private static void SkinsSkulls()
    {
        var paths=new[]{("entity/skeleton/skeleton.png",32),("entity/skeleton/wither_skeleton.png",32),("entity/zombie/zombie.png",64),("entity/steve.png",64),("entity/creeper/creeper.png",32)};
        uint[] colors={0xCCCCCC,0x353535,0x638744,0xB38268,0x4B9B35};
        for(int i=0;i<5;i++)
        {
            int type=i;
            Add("skull_"+i,FromClientOr(paths[i].Item1,64,paths[i].Item2,c=>
            {
                c.Fill(0,0,8,8,8,colors[type]);
                for(int y=10;y<13;y++)for(int x=9;x<15;x++)if(x<11||x>12)c.Set(x,y,Hx(0x202020));
            }));
        }
    }
}

public static partial class TextureGen
{
    private static void TexSkulls()
    {
        for(int i=0;i<Skulls.Icons.Length;i++)
        {
            var src=ClientAssets.TryReadTexture("items/"+Skulls.Icons[i]+".png",out int w,out int h);var px=New();
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)px[y*16+x]=src!=null&&w>0&&h>0?src[y*h/16*w+x*w/16]:x>2&&x<13&&y>2&&y<13?Hex(y>6&&y<9&&(x<6||x>9)?0x202020u:0xB0B0B0u):0;
            Add("item_"+Skulls.Icons[i],px);
        }
    }
}
