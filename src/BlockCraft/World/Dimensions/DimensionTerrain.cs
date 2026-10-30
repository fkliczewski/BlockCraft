using BlockCraft.Core;
using BlockCraft.Entities;

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
        if (_id==DimensionId.Nether) { Nether(c,r); NetherFortress.Generate(_seed,c); } else End(c,r);
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
    // ---------- End (ChunkProviderEnd + BiomeEndDecorator z MC 1.8) ----------
    /// <summary>Gęstość w punkcie siatki (co 8 bloków poziomo, co 4 w pionie): wyspa o promieniu ok. 100 bloków,
    /// zanik do -3000 nad 14. warstwą i do -30 pod 8. (initializeNoiseField z ChunkProviderEnd).</summary>
    private double EndGrid(int gx, int gy, int gz)
    {
        double dist = Math.Sqrt((double)gx * gx + (double)gz * gz) * 8;
        double d = _density.Sample(gx * .25, gy * .125, gz * .25) * 96 - 8 + Math.Clamp(100 - dist, -100, 80);
        if (gy > 14) { double t = Math.Clamp((gy - 14) / 64.0, 0, 1); d = d * (1 - t) - 3000 * t; }
        if (gy < 8) { double t = (8 - gy) / 7.0; d = d * (1 - t) - 30 * t; }
        return d;
    }

    /// <summary>Gęstość kolumny (x, z) dla y 0..127 - ta sama interpolacja co przy generowaniu chunków.</summary>
    private double[] EndColumn(int wx, int wz)
    {
        int gx = wx >> 3, gz = wz >> 3; double fx = (wx & 7) / 8.0, fz = (wz & 7) / 8.0;
        var g = new double[33];
        for (int gy = 0; gy < 33; gy++)
            g[gy] = (EndGrid(gx, gy, gz) * (1 - fx) + EndGrid(gx + 1, gy, gz) * fx) * (1 - fz) + (EndGrid(gx, gy, gz + 1) * (1 - fx) + EndGrid(gx + 1, gy, gz + 1) * fx) * fz;
        var col = new double[128];
        for (int y = 0; y < 128; y++) col[y] = g[y >> 2] + (g[(y >> 2) + 1] - g[y >> 2]) * (y & 3) / 4.0;
        return col;
    }

    private void End(Chunk c, Rng r)
    {
        // daleko od wyspy gęstość nie może być dodatnia
        double near = Math.Sqrt(Math.Pow(Math.Max(0, Math.Abs(c.Cx * 16 + 8) - 8), 2) + Math.Pow(Math.Max(0, Math.Abs(c.Cz * 16 + 8) - 8), 2));
        if (near < 230)
        {
            var g = new double[3, 33, 3];
            for (int i = 0; i < 3; i++) for (int k = 0; k < 3; k++) for (int j = 0; j < 33; j++) g[i, j, k] = EndGrid(c.Cx * 2 + i, j, c.Cz * 2 + k);
            for (int x = 0; x < 16; x++) for (int z = 0; z < 16; z++)
            {
                int gx = x >> 3, gz = z >> 3; double fx = (x & 7) / 8.0, fz = (z & 7) / 8.0;
                for (int y = 0; y < 128; y++)
                {
                    int gy = y >> 2; double fy = (y & 3) / 4.0, d = 0;
                    for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++) for (int dz = 0; dz < 2; dz++)
                        d += g[gx + dx, gy + dy, gz + dz] * (dx == 0 ? 1 - fx : fx) * (dy == 0 ? 1 - fy : fy) * (dz == 0 ? 1 - fz : fz);
                    if (d > 0) c.SetBlockRaw(x, y, z, B.EndStone, 0);
                }
            }
        }
        var ents = new List<Entity>();
        // WorldGenSpikes: w co piątym chunku (z przesunięciem +8) filar obsydianu wys. 6-37, promień 1-4, bedrock i kryształ na szczycie
        for (int scx = c.Cx - 1; scx <= c.Cx; scx++) for (int scz = c.Cz - 1; scz <= c.Cz; scz++)
        {
            var sr = new Rng(_seed ^ (long)scx * 341873128712L ^ (long)scz * 132897987541L ^ 0x5350494BL);
            if (sr.Next(5) != 0) continue;
            int sx = scx * 16 + sr.Next(16) + 8, sz = scz * 16 + sr.Next(16) + 8, height = sr.Next(32) + 6, radius = sr.Next(4) + 1;
            if ((double)sx * sx + (double)sz * sz > 240 * 240) continue;
            if (sx + radius < c.Cx * 16 || sx - radius > c.Cx * 16 + 15 || sz + radius < c.Cz * 16 || sz - radius > c.Cz * 16 + 15) continue;
            var col = EndColumn(sx, sz); int baseY = -1;
            for (int y = 127; y >= 0; y--) if (col[y] > 0) { baseY = y + 1; break; }
            if (baseY <= 0) continue;
            bool ok = true;
            for (int dx = -radius; dx <= radius && ok; dx++) for (int dz = -radius; dz <= radius && ok; dz++)
                if (dx * dx + dz * dz <= radius * radius + 1 && EndColumn(sx + dx, sz + dz)[baseY - 1] <= 0) ok = false;
            if (!ok) continue;
            for (int dx = -radius; dx <= radius; dx++) for (int dz = -radius; dz <= radius; dz++)
            {
                int lx = sx + dx - c.Cx * 16, lz = sz + dz - c.Cz * 16;
                if (dx * dx + dz * dz > radius * radius + 1 || lx < 0 || lx > 15 || lz < 0 || lz > 15) continue;
                for (int y = baseY; y < baseY + height && y < Chunk.Height; y++) c.SetBlockRaw(lx, y, lz, B.Obsidian, 0);
            }
            int cxl = sx - c.Cx * 16, czl = sz - c.Cz * 16;
            if (cxl is >= 0 and < 16 && czl is >= 0 and < 16)
            {
                c.SetBlockRaw(cxl, baseY + height, czl, B.Bedrock, 0);
                var crystal = new EnderCrystal(null) { Yaw = sr.NextFloat() * 360 };
                crystal.SetPositionAndPrev(sx + .5, baseY + height + 1, sz + .5);
                ents.Add(crystal);
            }
        }
        // BiomeEndDecorator: smok pojawia się raz, przy generowaniu chunku (0, 0)
        if (c.Cx == 0 && c.Cz == 0)
        {
            var dragon = new EnderDragon(null) { Yaw = r.NextFloat() * 360 };
            dragon.SetPositionAndPrev(0, 128, 0);
            ents.Add(dragon);
        }
        if (ents.Count > 0) c.PendingEntityData = EntitySerializer.SaveEntities(ents);
    }
}
