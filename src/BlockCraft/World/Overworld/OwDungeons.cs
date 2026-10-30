using BlockCraft.Core;

namespace BlockCraft.World;

public sealed partial class OwGenerator
{
    partial void PopulateStructures(OwGenRegion g, JRandom r, int x, int z)
    {
        for (int i = 0; i < 8; i++) OwDungeons.Generate(g, r, x + r.NextInt(16) + 8, r.NextInt(256), z + r.NextInt(16) + 8);
    }
}

public static class OwDungeons
{
    public static bool Generate(OwGenRegion g, JRandom r, int x, int y, int z)
    {
        int rx = r.NextInt(2) + 2, rz = r.NextInt(2) + 2, entrances = 0;
        for (int dx = -rx - 1; dx <= rx + 1; dx++) for (int dy = -1; dy <= 4; dy++) for (int dz = -rz - 1; dz <= rz + 1; dz++)
        {
            bool solid = g.Solid(x + dx, y + dy, z + dz);
            if ((dy == -1 || dy == 4) && !solid) return false;
            if ((dx == -rx - 1 || dx == rx + 1 || dz == -rz - 1 || dz == rz + 1) && dy == 0 && g.Get(x + dx, y, z + dz) == B.Air && g.Get(x + dx, y + 1, z + dz) == B.Air) entrances++;
        }
        if (entrances < 1 || entrances > 5) return false;
        for (int dx = -rx - 1; dx <= rx + 1; dx++) for (int dy = 3; dy >= -1; dy--) for (int dz = -rz - 1; dz <= rz + 1; dz++)
        {
            int xx = x + dx, yy = y + dy, zz = z + dz;
            if (dx != -rx - 1 && dx != rx + 1 && dz != -rz - 1 && dz != rz + 1 && dy != -1)
            { if (g.Get(xx, yy, zz) != B.Chest) g.Set(xx, yy, zz, B.Air); }
            else if (yy >= 0 && !g.Solid(xx, yy - 1, zz)) g.Set(xx, yy, zz, B.Air);
            else if (g.Solid(xx, yy, zz) && g.Get(xx, yy, zz) != B.Chest) g.Set(xx, yy, zz, dy == -1 && r.NextInt(4) != 0 ? B.MossyCobblestone : B.Cobblestone);
        }
        for (int i = 0; i < 2; i++) for (int attempt = 0; attempt < 3; attempt++)
        {
            int xx = x + r.NextInt(rx * 2 + 1) - rx, zz = z + r.NextInt(rz * 2 + 1) - rz;
            if (g.Get(xx, y, zz) != B.Air) continue;
            int solid = 0, facing = 2;
            for (int d = 0; d < 4; d++) if (g.Solid(xx + Dir.HorizX[d], y, zz + Dir.HorizZ[d])) { solid++; facing = d switch { 0 => 2, 1 => 5, 2 => 3, _ => 4 }; }
            if (solid != 1) continue;
            OwLoot.Chest(g, r, xx, y, zz, OwLoot.Dungeon, 8, facing);
            break;
        }
        g.Set(x, y, z, B.MobSpawner);
        g.Tile(x, y, z, new OwTilePlan { BlockId = B.MobSpawner, TypeId = 50, EntityId = r.NextInt(4) switch { 0 => "skeleton", 3 => "spider", _ => "zombie" } });
        return true;
    }
}
