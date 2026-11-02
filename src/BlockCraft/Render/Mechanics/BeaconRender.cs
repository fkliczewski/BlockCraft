using BlockCraft.Core;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

public static partial class TextureGen
{
    private static void TexBeacon()
    {
        foreach(var key in new[]{"beacon","beacon_beam"})
        {
            var original=ClientAssets.TryReadTexture(key=="beacon"?"blocks/beacon.png":"entity/beacon_beam.png",out int w,out int h);var px=New();
            for(int y=0;y<16;y++)for(int x=0;x<16;x++)px[y*16+x]=original!=null?original[y*h/16*w+x*w/16]:Hex(key=="beacon"?0x8DF6EDu:0xFFFFFFu);
            Add(key,px);
        }
    }
}
public sealed partial class EntityRenderer
{
    internal static void DrawBeacons(EntityRenderer r,World.World w,float pt,Vector3d cam)
    {
        int cx=MathUtil.FloorToInt(cam.X)>>4,cz=MathUtil.FloorToInt(cam.Z)>>4;
        for(int x=cx-4;x<=cx+4;x++)for(int z=cz-4;z<=cz+4;z++)
        {
            var chunk=w.GetChunk(x,z);if(chunk==null)continue;
            foreach(var te in chunk.TileEntities.Values)
            {
                if(te is not BeaconTile beacon)continue;
                // Clients do not tick tile entities. Rebuild only geometry, never apply gameplay effects.
                if(w.IsRemote&&(beacon.VisualTick<0||w.TotalTime/80!=beacon.VisualTick/80))
                {beacon.Rebuild(w);beacon.VisualTick=w.TotalTime;}
                if(!beacon.Active)continue;
                var b=r.Terrain;b.Layer=TextureGen.Layer("beacon_beam");b.Sky=b.Blk=15;
                float ox=(float)(beacon.X+.5-cam.X),oz=(float)(beacon.Z+.5-cam.Z),time=(w.TotalTime+pt);
                foreach(var s in beacon.Beam)
                {
                    for(int pass=0;pass<2;pass++)
                    {
                        float radius=pass==0?.2f:.25f,angle=pass==0?time*.025f:MathF.PI/4;
                        b.Color=new Vector4(s.R,s.G,s.B,pass==0?1:.125f);b.Overlay=Vector4.Zero;
                        float v0=-time*.2f,v1=v0+(s.Top-s.Bottom)*(pass==0?2.5f:1);
                        for(int i=0;i<4;i++)
                        {
                            float a=angle+i*MathF.PI/2,c=angle+(i+1)*MathF.PI/2;
                            var low0=new Vector3(ox+MathF.Cos(a)*radius,(float)(s.Bottom-cam.Y),oz+MathF.Sin(a)*radius);
                            var low1=new Vector3(ox+MathF.Cos(c)*radius,(float)(s.Bottom-cam.Y),oz+MathF.Sin(c)*radius);
                            var height=new Vector3(0,s.Top-s.Bottom,0);
                            b.Quad(low0,low0+height,low1+height,low1,0,v0,1,v1,1);
                        }
                    }
                }
                b.Color=Vector4.One;
            }
        }
    }
}
