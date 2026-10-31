using BlockCraft.Core;

namespace BlockCraft.World;

public static class OwFeatures
{
    public static void Ore(IGenAccess g, JRandom r, int x, int y, int z, int id, int meta, int size, bool infested = false)
    {
        float angle = r.NextFloat() * MathF.PI;
        double x0 = (float)(x + 8) + McMath.Sin(angle) * size / 8f, x1 = (float)(x + 8) - McMath.Sin(angle) * size / 8f;
        double z0 = (float)(z + 8) + McMath.Cos(angle) * size / 8f, z1 = (float)(z + 8) - McMath.Cos(angle) * size / 8f;
        double y0 = y + r.NextInt(3) - 2, y1 = y + r.NextInt(3) - 2;
        for (int i = 0; i < size; i++)
        {
            float t = (float)i / size;
            double px = x0 + (x1 - x0) * t, py = y0 + (y1 - y0) * t, pz = z0 + (z1 - z0) * t;
            double noise = r.NextDouble() * size / 16.0, diameter = (McMath.Sin(MathF.PI * t) + 1f) * noise + 1;
            double radius = diameter / 2;
            for (int xx = (int)Math.Floor(px - radius); xx <= (int)Math.Floor(px + radius); xx++)
                for (int yy = (int)Math.Floor(py - radius); yy <= (int)Math.Floor(py + radius); yy++)
                    for (int zz = (int)Math.Floor(pz - radius); zz <= (int)Math.Floor(pz + radius); zz++)
                    {
                        double dx = (xx + 0.5 - px) / radius, dy = (yy + 0.5 - py) / radius, dz = (zz + 0.5 - pz) / radius;
                        if (dx * dx + dy * dy + dz * dz < 1 && g.Get(xx, yy, zz) == B.Stone && (!infested || g.GetMeta(xx, yy, zz) == 0)) g.Set(xx, yy, zz, id, meta);
                    }
        }
    }

    public static bool Lake(OwGenRegion g, JRandom r, int x, int y, int z, int liquid)
    {
        x -= 8; z -= 8;
        while (y > 5 && g.Get(x, y, z) == B.Air) y--;
        if (y <= 4) return false;
        y -= 4;
        var shape = new bool[2048];
        int count = r.NextInt(4) + 4;
        for (int i = 0; i < count; i++)
        {
            double dx = r.NextDouble() * 6 + 3, dy = r.NextDouble() * 4 + 2, dz = r.NextDouble() * 6 + 3;
            double px = r.NextDouble() * (16 - dx - 2) + 1 + dx / 2, py = r.NextDouble() * (8 - dy - 4) + 2 + dy / 2, pz = r.NextDouble() * (16 - dz - 2) + 1 + dz / 2;
            for (int xx = 1; xx < 15; xx++) for (int zz = 1; zz < 15; zz++) for (int yy = 1; yy < 7; yy++)
            {
                double a = (xx - px) / (dx / 2), b = (yy - py) / (dy / 2), c = (zz - pz) / (dz / 2);
                if (a * a + b * b + c * c < 1) shape[(xx * 16 + zz) * 8 + yy] = true;
            }
        }
        bool Edge(int xx, int yy, int zz)
        {
            int i = (xx * 16 + zz) * 8 + yy;
            return !shape[i] && (xx < 15 && shape[i + 128] || xx > 0 && shape[i - 128] || zz < 15 && shape[i + 8] || zz > 0 && shape[i - 8] || yy < 7 && shape[i + 1] || yy > 0 && shape[i - 1]);
        }
        for (int xx = 0; xx < 16; xx++) for (int zz = 0; zz < 16; zz++) for (int yy = 0; yy < 8; yy++)
        {
            if (!Edge(xx, yy, zz)) continue;
            int id = g.Get(x + xx, y + yy, z + zz);
            if (yy >= 4 && Block.Get(id).IsLiquid || yy < 4 && !Block.Get(id).Solid && id != liquid) return false;
        }
        for (int xx = 0; xx < 16; xx++) for (int zz = 0; zz < 16; zz++) for (int yy = 0; yy < 8; yy++)
            if (shape[(xx * 16 + zz) * 8 + yy]) g.Set(x + xx, y + yy, z + zz, yy >= 4 ? B.Air : liquid);
        for (int xx = 0; xx < 16; xx++) for (int zz = 0; zz < 16; zz++) for (int yy = 4; yy < 8; yy++)
            if (shape[(xx * 16 + zz) * 8 + yy] && g.Get(x + xx, y + yy - 1, z + zz) == B.Dirt && g.Height(x + xx, z + zz) <= y + yy)
                g.Set(x + xx, y + yy - 1, z + zz, Biome.Get(g.Generator.Biomes.BiomeAt(x + xx, z + zz)).Top == B.Mycelium ? B.Mycelium : B.Grass);
        if (liquid == B.Lava)
            for (int xx = 0; xx < 16; xx++) for (int zz = 0; zz < 16; zz++) for (int yy = 0; yy < 8; yy++)
                if (Edge(xx, yy, zz) && (yy < 4 || r.NextInt(2) != 0) && g.Solid(x + xx, y + yy, z + zz)) g.Set(x + xx, y + yy, z + zz, B.Stone);
        if (liquid == B.Water)
            for (int xx = 0; xx < 16; xx++) for (int zz = 0; zz < 16; zz++)
                if (g.Get(x + xx, y + 4, z + zz) == B.Water && Biome.IsSnowyAt(g.Generator.Biomes.BiomeAt(x + xx, z + zz), x + xx, y + 4, z + zz)) g.Set(x + xx, y + 4, z + zz, B.Ice);
        return true;
    }

    public static bool Patch(OwGenRegion g, JRandom r, int x, int y, int z, int id, int meta = 0)
    {
        bool placed = false;
        bool grass = id == B.TallGrass, bush = id == B.DeadBush;
        if (grass || bush) while (y > 0 && (g.Get(x, y, z) == B.Air || OwTrees.IsLeaves(g.Get(x, y, z)))) y--;
        int tries = grass ? 128 : bush ? 4 : id == B.LilyPad || id == B.Cactus ? 10 : id == B.SugarCane ? 20 : 64;
        for (int i = 0; i < tries; i++)
        {
            int spread = id == B.SugarCane ? 4 : 8;
            int xx = x + r.NextInt(spread) - r.NextInt(spread);
            int yy = id == B.SugarCane ? y : y + r.NextInt(4) - r.NextInt(4);
            int zz = z + r.NextInt(spread) - r.NextInt(spread);
            if (yy < 1 || yy > (id == B.DoublePlant ? 254 : 255) || g.Get(xx, yy, zz) != B.Air) continue;
            int below = g.Get(xx, yy - 1, zz);
            bool soil = below is B.Grass or B.Dirt or B.Farmland;
            if (id == B.SugarCane)
            {
                if (!(soil || below == B.Sand) || !Enumerable.Range(0, 4).Any(d => g.Get(xx + Dir.HorizX[d], yy - 1, zz + Dir.HorizZ[d]) == B.Water)) continue;
                int h = 2 + r.NextInt(r.NextInt(3) + 1);
                for (int j = 0; j < h && g.Get(xx, yy + j, zz) == B.Air; j++) g.Set(xx, yy + j, zz, id);
            }
            else if (id == B.Cactus)
            {
                int h = 1 + r.NextInt(r.NextInt(3) + 1);
                for (int j = 0; j < h; j++)
                {
                    int under = g.Get(xx, yy + j - 1, zz);
                    if (under is not (B.Sand or B.Cactus) || Enumerable.Range(0, 4).Any(d => g.Solid(xx + Dir.HorizX[d], yy + j, zz + Dir.HorizZ[d]))) break;
                    g.Set(xx, yy + j, zz, id);
                }
            }
            else if (id == B.LilyPad) { if (below == B.Water && g.GetMeta(xx, yy - 1, zz) == 0) g.Set(xx, yy, zz, id); }
            else if (id is B.BrownMushroom or B.RedMushroom)
            {
                bool shade = g.Height(xx, zz) > yy;
                if (below == B.Mycelium || below == B.Dirt && g.GetMeta(xx, yy - 1, zz) == 2 || shade && g.Solid(xx, yy - 1, zz)) g.Set(xx, yy, zz, id);
            }
            else if (bush) { if (below is B.Sand or B.Dirt or B.HardenedClay or B.StainedClay) g.Set(xx, yy, zz, id); }
            else if (id is B.Pumpkin or B.Melon) { if (below == B.Grass) g.Set(xx, yy, zz, id, id == B.Pumpkin ? r.NextInt(4) : 0); }
            else if (id == B.DoublePlant)
            {
                if (soil && g.Get(xx, yy + 1, zz) == B.Air) { g.Set(xx, yy, zz, id, meta); g.Set(xx, yy + 1, zz, id, 8); placed = true; }
            }
            else if (soil && g.Height(xx, zz) <= yy) { g.Set(xx, yy, zz, id, meta); placed = true; }
        }
        return placed;
    }

    public static void Sediment(OwGenRegion g, JRandom r, int x, int y, int z, int id, int size)
    {
        if (g.Get(x, y, z) != B.Water) return;
        int radius = r.NextInt(size - 2) + 2, depth = id == B.Clay ? 1 : 2;
        for (int xx = x - radius; xx <= x + radius; xx++) for (int zz = z - radius; zz <= z + radius; zz++)
        {
            if ((xx - x) * (xx - x) + (zz - z) * (zz - z) > radius * radius) continue;
            for (int yy = y - depth; yy <= y + depth; yy++)
            {
                int old = g.Get(xx, yy, zz);
                if (id == B.Clay ? old is B.Dirt or B.Clay : old is B.Dirt or B.Grass) g.Set(xx, yy, zz, id);
            }
        }
    }

    public static void Spring(OwGenRegion g, int x, int y, int z, int id)
    {
        if (g.Get(x, y + 1, z) != B.Stone || g.Get(x, y - 1, z) != B.Stone || g.Get(x, y, z) is not (B.Stone or B.Air)) return;
        int stone = 0, air = 0;
        for (int i = 0; i < 4; i++) { int b = g.Get(x + Dir.HorizX[i], y, z + Dir.HorizZ[i]); if (b == B.Stone) stone++; if (b == B.Air) air++; }
        if (stone == 3 && air == 1) g.Set(x, y, z, id);
    }
}
