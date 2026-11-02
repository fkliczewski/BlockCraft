using BlockCraft.Core;

namespace BlockCraft.World;

/// <summary>BiomeDecorator i dekoracje klas biomów, z parametrami ChunkProviderSettings 1.8.</summary>
public static class OwDecorator
{
    public static void Decorate(OwGenRegion g, JRandom r, BiomeDef b, int x, int z)
    {
        int flowers = b.Flowers, grass = b.Grass;
        (int x, int z) Pos() => (x + r.NextInt(16) + 8, z + r.NextInt(16) + 8);
        bool Plant(int id, int meta = 0, bool extraHeight = true)
        {
            var p = Pos(); int max = extraHeight ? g.Height(p.x, p.z) + 32 : g.Height(p.x, p.z) * 2;
            return max > 0 && OwFeatures.Patch(g, r, p.x, r.NextInt(max), p.z, id, meta);
        }
        if (b.Kind == BiomeKind.Forest)
        {
            if (b.Variant == 3)
                for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++)
                {
                    int tx = x + i * 4 + 9 + r.NextInt(3), tz = z + j * 4 + 9 + r.NextInt(3), ty = g.Height(tx, tz);
                    if (r.NextInt(20) == 0) OwTrees.HugeMushroom(g, r, tx, ty, tz, r.NextInt(2));
                    else Tree(g, r, b, tx, ty, tz);
                }
            int count = r.NextInt(5) - 3 + (b.Variant == 1 ? 2 : 0);
            for (int i = 0; i < count; i++)
            {
                int type = r.NextInt(3) switch { 0 => 1, 1 => 4, _ => 5 };
                for (int j = 0; j < 5; j++) if (Plant(B.DoublePlant, type)) break;
            }
        }
        if (b.Kind == BiomeKind.Plains)
        {
            if (Biome.GrassColorNoise((x + 8) / 200.0, (z + 8) / 200.0) < -0.8) { flowers = 15; grass = 5; }
            else { flowers = 4; grass = 10; for (int i = 0; i < 7; i++) Plant(B.DoublePlant, 2); }
            if (b.Variant == 1) for (int i = 0; i < 10; i++) Plant(B.DoublePlant, 0);
        }
        if (b.Kind == BiomeKind.Taiga)
        {
            if (b.Variant > 0)
            {
                int count = r.NextInt(3);
                for (int i = 0; i < count; i++) { var p = Pos(); Boulder(g, r, p.x, g.Height(p.x, p.z), p.z); }
            }
            for (int i = 0; i < 7; i++) Plant(B.DoublePlant, 3);
        }
        if (b.Kind == BiomeKind.Savanna && !b.IsMutation) for (int i = 0; i < 7; i++) Plant(B.DoublePlant, 2);
        if (b.Kind == BiomeKind.Snow && b.Variant == 1)
        {
            for (int i = 0; i < 3; i++) { var p = Pos(); IceSpike(g, r, p.x, g.Height(p.x, p.z), p.z); }
            for (int i = 0; i < 2; i++) { var p = Pos(); IcePath(g, r, p.x, g.Height(p.x, p.z), p.z); }
        }
        Ores(g, r, x, z);
        void Sediment(int count, int id, int size)
        {
            for (int i = 0; i < count; i++) { var p = Pos(); OwFeatures.Sediment(g, r, p.x, g.TopSolid(p.x, p.z), p.z, id, size); }
        }
        Sediment(b.SandPatches, B.Sand, 7); Sediment(b.Clay, B.Clay, 4); Sediment(b.GravelPatches, B.Gravel, 6);
        int trees = b.Trees + (r.NextInt(10) == 0 ? 1 : 0);
        for (int i = 0; i < trees; i++) { var p = Pos(); Tree(g, r, b, p.x, g.Height(p.x, p.z), p.z); }
        for (int i = 0; i < b.BigMushrooms; i++) { var p = Pos(); OwTrees.HugeMushroom(g, r, p.x, g.Height(p.x, p.z), p.z, r.NextInt(2)); }
        for (int i = 0; i < flowers; i++)
        {
            var p = Pos(); int y = r.NextInt(Math.Max(1, g.Height(p.x, p.z) + 32));
            var f = OwFlora.PickFlower(b.Id, r, p.x, p.z);
            OwFeatures.Patch(g, r, p.x, y, p.z, f.id, f.meta);
        }
        for (int i = 0; i < grass; i++)
        {
            var p = Pos(); int max = g.Height(p.x, p.z) * 2;
            if (max <= 0) continue;
            int y = r.NextInt(max);
            int meta = b.Kind == BiomeKind.Taiga ? r.NextInt(5) > 0 ? 2 : 1 : b.Kind == BiomeKind.Jungle && r.NextInt(4) == 0 ? 2 : 1;
            OwFeatures.Patch(g, r, p.x, y, p.z, B.TallGrass, meta);
        }
        for (int i = 0; i < b.DeadBush; i++) Plant(B.DeadBush, 0, false);
        for (int i = 0; i < b.Lilies; i++)
        {
            var p = Pos(); int y = r.NextInt(Math.Max(1, g.Height(p.x, p.z) * 2));
            while (y > 0 && g.Get(p.x, y - 1, p.z) == B.Air) y--;
            OwFeatures.Patch(g, r, p.x, y, p.z, B.LilyPad);
        }
        for (int i = 0; i < b.Mushrooms; i++)
        {
            if (r.NextInt(4) == 0) { var p = Pos(); OwFeatures.Patch(g, r, p.x, g.Height(p.x, p.z), p.z, B.BrownMushroom); }
            if (r.NextInt(8) == 0) Plant(B.RedMushroom, 0, false);
        }
        if (r.NextInt(4) == 0) Plant(B.BrownMushroom, 0, false);
        if (r.NextInt(8) == 0) Plant(B.RedMushroom, 0, false);
        for (int i = 0; i < b.Reeds + 10; i++) Plant(B.SugarCane, 0, false);
        if (r.NextInt(32) == 0) Plant(B.Pumpkin, 0, false);
        for (int i = 0; i < b.Cacti; i++) Plant(B.Cactus, 0, false);
        if (b.Lakes)
        {
            for (int i = 0; i < 50; i++) { var p = Pos(); int y = r.NextInt(r.NextInt(248) + 8); OwFeatures.Spring(g, p.x, y, p.z, B.Water); }
            for (int i = 0; i < 20; i++) { var p = Pos(); int y = r.NextInt(r.NextInt(r.NextInt(240) + 8) + 8); OwFeatures.Spring(g, p.x, y, p.z, B.Lava); }
        }
        if (b.Kind == BiomeKind.Hills)
        {
            int count = 3 + r.NextInt(6);
            for (int i = 0; i < count; i++)
            {
                int xx = x + r.NextInt(16), yy = r.NextInt(28) + 4, zz = z + r.NextInt(16);
                if (g.Get(xx, yy, zz) == B.Stone) g.Set(xx, yy, zz, B.EmeraldOre);
            }
            for (int i = 0; i < 7; i++) OwFeatures.Ore(g, r, x + r.NextInt(16), r.NextInt(64), z + r.NextInt(16), B.MonsterEgg, 0, 9, true);
        }
        if (b.Kind == BiomeKind.Jungle)
        {
            Plant(B.Melon, 0, false);
            for (int i = 0; i < 50; i++)
            {
                var p = Pos(); int vx = p.x, vz = p.z;
                for (int y = 128; y < 256; y++)
                {
                    if (g.Get(vx, y, vz) == B.Air)
                    {
                        for (int d = 0; d < 4; d++)
                            if (g.Solid(vx + Dir.HorizX[d], y, vz + Dir.HorizZ[d])) { g.Set(vx, y, vz, B.Vine, d switch { 0 => 4, 1 => 8, 2 => 1, _ => 2 }); break; }
                    }
                    else { vx += r.NextInt(4) - r.NextInt(4); vz += r.NextInt(4) - r.NextInt(4); }
                }
            }
        }
    }

    private static void Ores(OwGenRegion g, JRandom r, int x, int z)
    {
        void Ore(int id, int meta, int size, int count, int max)
        {
            for (int i = 0; i < count; i++) OwFeatures.Ore(g, r, x + r.NextInt(16), r.NextInt(max), z + r.NextInt(16), id, meta, size);
        }
        Ore(B.Dirt, 0, 33, 10, 256); Ore(B.Gravel, 0, 33, 8, 256);
        Ore(B.Stone, 3, 33, 10, 80); Ore(B.Stone, 1, 33, 10, 80); Ore(B.Stone, 5, 33, 10, 80);
        Ore(B.CoalOre, 0, 17, 20, 128); Ore(B.IronOre, 0, 9, 20, 64); Ore(B.GoldOre, 0, 9, 2, 32);
        Ore(B.RedstoneOre, 0, 8, 8, 16); Ore(B.DiamondOre, 0, 8, 1, 16);
        OwFeatures.Ore(g, r, x + r.NextInt(16), r.NextInt(16) + r.NextInt(16), z + r.NextInt(16), B.LapisOre, 0, 7);
    }

    private static bool Tree(OwGenRegion g, JRandom r, BiomeDef b, int x, int y, int z)
    {
        switch (b.Kind)
        {
            case BiomeKind.Forest:
                if (b.Variant == 3 && r.NextInt(3) > 0) return OwTrees.Canopy(g, r, x, y, z);
                if (b.Variant == 2 || r.NextInt(5) == 0) return OwTrees.BirchTree(g, r, x, y, z, b.IsMutation && r.NextBool());
                break;
            case BiomeKind.Taiga:
                if (b.Variant > 0 && r.NextInt(3) == 0) return OwTrees.MegaPine(g, r, x, y, z, b.Variant == 2 || r.NextInt(13) == 0, true);
                return r.NextInt(3) == 0 ? OwTrees.Taiga1(g, r, x, y, z) : OwTrees.Taiga2(g, r, x, y, z);
            case BiomeKind.Snow: return OwTrees.Taiga2(g, r, x, y, z);
            case BiomeKind.Swamp: return OwTrees.Swamp(g, r, x, y, z);
            case BiomeKind.Jungle:
                if (r.NextInt(10) == 0) return OwTrees.BigTree(g, r, x, y, z);
                if (r.NextInt(2) == 0) return OwTrees.Shrub(g, r, x, y, z);
                if (b.Variant == 0 && r.NextInt(3) == 0) return OwTrees.MegaJungle(g, r, x, y, z);
                return OwTrees.SmallTree(g, r, x, y, z, 4 + r.NextInt(7), OwTrees.Jungle, OwTrees.Jungle, true);
            case BiomeKind.Savanna:
                if (r.NextInt(5) > 0) return OwTrees.Savanna(g, r, x, y, z);
                return OwTrees.SmallTree(g, r, x, y, z, 4, OwTrees.Oak, OwTrees.Oak, false);
            case BiomeKind.Hills:
                if (r.NextInt(3) > 0) return OwTrees.Taiga2(g, r, x, y, z);
                break;
        }
        if (r.NextInt(10) == 0) return OwTrees.BigTree(g, r, x, y, z);
        return OwTrees.SmallTree(g, r, x, y, z, 4, OwTrees.Oak, OwTrees.Oak, false);
    }

    private static void Boulder(OwGenRegion g, JRandom r, int x, int y, int z)
    {
        while (y > 3 && g.Get(x, y - 1, z) is not (B.Grass or B.Dirt or B.Stone)) y--;
        if (y <= 3) return;
        for (int i = 0; i < 3; i++)
        {
            int a = r.NextInt(2), b = r.NextInt(2), c = r.NextInt(2); float radius = (a + b + c) / 3f + 0.5f;
            for (int xx = -a; xx <= a; xx++) for (int yy = -b; yy <= b; yy++) for (int zz = -c; zz <= c; zz++)
                if (xx * xx + yy * yy + zz * zz <= radius * radius) g.Set(x + xx, y + yy, z + zz, B.MossyCobblestone);
            x += r.NextInt(2) - 1; y -= r.NextInt(2); z += r.NextInt(2) - 1;
        }
    }

    private static void IcePath(OwGenRegion g, JRandom r, int x, int y, int z)
    {
        while (y > 2 && g.Get(x, y, z) == B.Air) y--;
        if (g.Get(x, y, z) != B.Snow) return;
        int radius = r.NextInt(2) + 2;
        for (int xx = -radius; xx <= radius; xx++) for (int zz = -radius; zz <= radius; zz++)
            if (xx * xx + zz * zz <= radius * radius)
                for (int yy = -1; yy <= 1; yy++) if (g.Get(x + xx, y + yy, z + zz) is B.Dirt or B.Snow or B.Ice) g.Set(x + xx, y + yy, z + zz, B.PackedIce);
    }

    private static void IceSpike(OwGenRegion g, JRandom r, int x, int y, int z)
    {
        while (y > 2 && g.Get(x, y, z) == B.Air) y--;
        if (g.Get(x, y, z) != B.Snow) return;
        y += r.NextInt(4); int height = r.NextInt(4) + 7, width = height / 4 + r.NextInt(2);
        if (width > 1 && r.NextInt(60) == 0) y += 10 + r.NextInt(30);
        for (int yy = 0; yy < height; yy++)
        {
            float radius = (1f - (float)yy / height) * width; int bound = (int)Math.Ceiling(radius);
            for (int xx = -bound; xx <= bound; xx++) for (int zz = -bound; zz <= bound; zz++)
            {
                float dx = Math.Abs(xx) - 0.25f, dz = Math.Abs(zz) - 0.25f;
                if ((xx != 0 || zz != 0) && dx * dx + dz * dz > radius * radius || (xx == -bound || xx == bound || zz == -bound || zz == bound) && r.NextFloat() > 0.75f) continue;
                void Ice(int py) { if (g.Get(x + xx, py, z + zz) is B.Air or B.Dirt or B.Snow or B.Ice) g.Set(x + xx, py, z + zz, B.PackedIce); }
                Ice(y + yy); if (yy != 0 && bound > 1) Ice(y - yy);
            }
        }
        int root = Math.Clamp(width - 1, 0, 1);
        for (int xx = -root; xx <= root; xx++) for (int zz = -root; zz <= root; zz++)
        {
            int py = y - 1, remaining = Math.Abs(xx) == 1 && Math.Abs(zz) == 1 ? r.NextInt(5) : 50;
            while (py > 50 && g.Get(x + xx, py, z + zz) is B.Air or B.Dirt or B.Snow or B.Ice or B.PackedIce)
            {
                g.Set(x + xx, py--, z + zz, B.PackedIce);
                if (--remaining <= 0) { py -= r.NextInt(5) + 1; remaining = r.NextInt(5); }
            }
        }
    }
}
