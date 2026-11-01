using BlockCraft.Core;
using BlockCraft.Items;

namespace BlockCraft.Render;

public static partial class TextureGen
{
    private static void TexFood18()
    {
        void Native(string name,string path,Func<int,int,uint> fallback)
        {
            var src=ClientAssets.TryReadTexture(path,out int w,out int h);var px=New();
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)px[y*16+x]=src!=null&&w>0&&h>0?src[(y*h/16)*w+x*w/16]:fallback(x,y);
            Add(name,px);
        }
        foreach(string key in FishFood.RawTextures.Concat(FishFood.CookedTextures))
            Native("item_"+key,"items/"+key+".png",(x,y)=>Math.Abs(y-8)<3&&x>1&&x<14?Hex(key.Contains("salmon")?0xC27867u:0xA2AA7Cu):0);
        Native("item_cake","items/cake.png",(x,y)=>x>1&&x<14&&y>3&&y<13?Hex(y<7?0xFFF4DCu:0x986B43u):0);
        foreach(string key in new[]{"cake_top","cake_bottom","cake_side","cake_inner"})
            Native(key,"blocks/"+key+".png",(x,y)=>Hex(key=="cake_bottom"?0x986B43u:key=="cake_top"&&(x+y*3)%13==0?0xB02424u:0xF3D5ACu));
    }
}
