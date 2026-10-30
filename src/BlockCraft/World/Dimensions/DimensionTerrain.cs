using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>Deterministyczne generowanie terenu Netheru i Endu, bez dostępu do aktywnego gracza.</summary>
public sealed class DimensionTerrain
{
    private readonly long _seed;
    private readonly DimensionId _id;
    private readonly OctaveNoise _density, _surface, _detail;
    public DimensionTerrain(long seed, DimensionId id)
    { _seed=seed; _id=id; var r=new Rng(seed); _density=new(r,8); _surface=new(r,4); _detail=new(r,4); }
    public Chunk Generate(int cx,int cz)
    {
        var c=new Chunk(cx,cz); var r=new Rng(_seed ^ (long)cx*341873128712L ^ (long)cz*132897987541L);
        Array.Fill(c.Biomes,(byte)(_id==DimensionId.Nether ? 8 : 9));
        if (_id==DimensionId.Nether) Nether(c,r); else End(c,r);
        c.RecountSections(); c.RecalcHeightMap(); c.Modified=true;
        return c;
    }
    private void Nether(Chunk c,Rng r)
    {
        // Siatka 5x17x5 i interpolacja ograniczają koszt próbkowania szumu przestrzennego.
        var grid=new double[5,17,5];
        for(int gx=0;gx<5;gx++) for(int gy=0;gy<17;gy++) for(int gz=0;gz<5;gz++)
        {
            double x=c.Cx*16+gx*4,z=c.Cz*16+gz*4,y=gy*8;
            grid[gx,gy,gz]=_density.Sample(x/64,y/32,z/64)*2.4 + Math.Cos(y*Math.PI/32)*.32 + Math.Max(0,(16-y)/8) + Math.Max(0,(y-108)/8) -.12;
        }
        for(int x=0;x<16;x++) for(int z=0;z<16;z++) for(int y=0;y<128;y++)
        {
            double d=0; int gx=x/4,gy=y/8,gz=z/4; double fx=x%4/4.0,fy=y%8/8.0,fz=z%4/4.0;
            for(int dx=0;dx<2;dx++) for(int dy=0;dy<2;dy++) for(int dz=0;dz<2;dz++)
                d+=grid[gx+dx,gy+dy,gz+dz]*(dx==0?1-fx:fx)*(dy==0?1-fy:fy)*(dz==0?1-fz:fz);
            byte b=y<=r.Next(5)||y>=127-r.Next(5)?B.Bedrock:d>0?B.Netherrack:y<32?B.Lava:B.Air;
            c.SetBlockRaw(x,y,z,b,0);
        }
        for(int x=0;x<16;x++) for(int z=0;z<16;z++)
        {
            int wx=c.Cx*16+x,wz=c.Cz*16+z;
            for(int y=115;y>30;y--)
            {
                if(c.GetBlock(x,y,z)!=B.Netherrack||c.GetBlock(x,y+1,z)!=B.Air)continue;
                if(y<65 && _surface.Sample(wx/32.0,wz/32.0)>.12) c.SetBlockRaw(x,y,z,B.SoulSand,0);
                else if(y<65 && _detail.Sample(wx/32.0,wz/32.0)>.2)c.SetBlockRaw(x,y,z,B.Gravel,0);
                if(c.GetBlock(x,y,z)==B.Netherrack&&r.Next(55)==0)c.SetBlockRaw(x,y+1,z,B.Fire,0);
                else if(r.Next(120)==0)c.SetBlockRaw(x,y+1,z,r.Next(2)==0?B.BrownMushroom:B.RedMushroom,0);
            }
        }
        for(int n=0;n<16;n++)
        {
            int x=r.Next(16),y=r.Next(10,118),z=r.Next(16);
            for(int k=0;k<14;k++) { if(c.GetBlock(x,y,z)==B.Netherrack)c.SetBlockRaw(x,y,z,B.QuartzOre,0); x=Math.Clamp(x+r.Next(3)-1,0,15);y=Math.Clamp(y+r.Next(3)-1,10,117);z=Math.Clamp(z+r.Next(3)-1,0,15); }
        }
        for(int n=0;n<10;n++)
        {
            int x=r.Next(16),z=r.Next(16);
            for(int y=r.Next(60,115);y>35;y--) if(c.GetBlock(x,y,z)==B.Air&&c.GetBlock(x,y+1,z)==B.Netherrack)
            { for(int k=0;k<r.Next(2,6);k++)c.SetBlockRaw(x,y-k,z,B.Glowstone,0);break; }
        }
    }
    private void End(Chunk c,Rng r)
    {
        for(int x=0;x<16;x++) for(int z=0;z<16;z++)
        {
            int wx=c.Cx*16+x,wz=c.Cz*16+z; double distance=Math.Sqrt((double)wx*wx+(double)wz*wz);
            double shape=1-distance/100+_surface.Sample(wx/32.0,wz/32.0)*.22;
            if(shape<=0)continue;
            int top=64+(int)(_detail.Sample(wx/48.0,wz/48.0)*9),bottom=top-(int)(shape*35);
            for(int y=bottom;y<=top;y++)c.SetBlockRaw(x,y,z,B.EndStone,0);
        }
        // Filary są deterministyczne także na granicach chunków.
        var spikes=new Rng(_seed^0x454E44);
        for(int i=0;i<10;i++)
        {
            double angle=i*Math.PI/5; int px=(int)(Math.Cos(angle)*42),pz=(int)(Math.Sin(angle)*42),radius=2+spikes.Next(3),top=76+spikes.Next(28);
            for(int x=0;x<16;x++) for(int z=0;z<16;z++)
            {
                int dx=c.Cx*16+x-px,dz=c.Cz*16+z-pz;
                if(dx*dx+dz*dz>radius*radius+1)continue;
                for(int y=45;y<top;y++)c.SetBlockRaw(x,y,z,B.Obsidian,0);
                if(dx==0&&dz==0)c.SetBlockRaw(x,top,z,B.Bedrock,0);
            }
        }
    }
}
