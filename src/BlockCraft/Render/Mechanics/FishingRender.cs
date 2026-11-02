using BlockCraft.Core;
using BlockCraft.Entities;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public sealed partial class EntityRenderer
{
    internal static void DrawFishingHook(EntityRenderer r,Entity entity,float pt,Vector3 pos,Vector3 cam)
    {
        var hook=(FishingHook)entity;hook.ResolveAngler();
        r.RenderBillboardSprite(TextureGen.Layer("fishing_hook"),pos,.25f,Vector4.One,cam);
        var p=hook.Angler;if(p==null)return;
        float yaw=MathUtil.LerpAngle(p.PrevYaw,p.Yaw,pt)*MathUtil.Deg2Rad;
        var hand=new Vector3((float)(p.IX(pt)-cam.X)-MathF.Cos(yaw)*.35f,(float)(p.IY(pt)-cam.Y)+p.EyeHeight-.3f,(float)(p.IZ(pt)-cam.Z)-MathF.Sin(yaw)*.35f);
        var b=r.Terrain;var color=b.Color;b.Color=new Vector4(.03f,.03f,.03f,1);b.Layer=TextureGen.Layer("wool_0");
        var from=pos+new Vector3(0,.125f,0);var previous=from;
        for(int i=1;i<=16;i++)
        {
            float t=i/16f;var next=Vector3.Lerp(from,hand,t);next.Y=from.Y+(hand.Y-from.Y)*(t*t+t)*.5f;
            var side=new Vector3(.003f,0,.003f);
            b.Quad(previous-side,previous+side,next+side,next-side,0,0,.01f,.01f,1);
            var up=new Vector3(0,.003f,0);b.Quad(previous-up,previous+up,next+up,next-up,0,0,.01f,.01f,1);
            previous=next;
        }
        b.Color=color;
    }
}

public static partial class TextureGen
{
    private static void TexFishing()
    {
        var p=New();
        for(int y=5;y<13;y++)for(int x=5;x<11;x++)
            if(y<10||x==5||x==10||y==12)p[y*16+x]=Hex(y<7?0xCA3020u:y<10?0xEEEEEEu:0x606060u);
        Add("fishing_hook",p);
        var source=ClientAssets.TryReadTexture("items/bowl.png",out int w,out int h);var bowl=New();
        for(int y=0;y<16;y++)for(int x=0;x<16;x++)bowl[y*16+x]=source!=null?source[(y*h/16)*w+x*w/16]:y>=8&&y<13&&Math.Abs(x-8)<14-y?Hex(0x8A633Fu):0;
        Add("item_bowl",bowl);
        foreach(string state in new[]{"uncast","cast"})
        {
            var rod=ClientAssets.TryReadTexture("items/fishing_rod_"+state+".png",out w,out h);
            if(rod==null||w<=0||h<=0)continue;
            var px=New();for(int y=0;y<16;y++)for(int x=0;x<16;x++)px[y*16+x]=rod[(y*h/16)*w+x*w/16];
            Add(state=="uncast"?"item_fishing_rod":"item_fishing_rod_cast",px);
        }
    }
}
