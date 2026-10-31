namespace BlockCraft.World;

/// <summary>Komponenty kopalni 1.8: sala, korytarz, skrzyżowanie i schody, z limitami głębokości 8 i zasięgu 80.</summary>
public static class OwMineshaft
{
    private sealed class Piece
    {
        public int Kind, Direction, Depth;
        public OwBox Box;
        public bool Rails, Spiders;
        public readonly List<OwBox> Entrances = new();
    }

    public static OwStructurePlan Build(OwGenerator gen, JRandom r, int cx, int cz)
    {
        int startX = cx * 16 + 2, startZ = cz * 16 + 2;
        var root = new Piece { Box = new(startX, 50, startZ, startX + 7 + r.NextInt(6), 54 + r.NextInt(6), startZ + 7 + r.NextInt(6)), Kind = 0 };
        var pieces = new List<Piece> { root };
        Piece Add(int x, int y, int z, int direction, int depth)
        {
            if (depth > 8 || Math.Abs(x - root.Box.X0) > 80 || Math.Abs(z - root.Box.Z0) > 80) return null;
            int roll = r.NextInt(100), kind = roll >= 80 ? 2 : roll >= 70 ? 3 : 1;
            var box = new OwBox(x, kind == 3 ? y - 5 : y, z, x, y + 2, z);
            if (kind == 2)
            {
                if (r.NextInt(4) == 0) box.Y1 += 4;
                switch (direction)
                {
                    case 2: box.X0 = x - 1; box.X1 = x + 3; box.Z0 = z - 4; break;
                    case 3: box.X0 = x - 1; box.X1 = x + 3; box.Z1 = z + 4; break;
                    case 4: box.X0 = x - 4; box.Z0 = z - 1; box.Z1 = z + 3; break;
                    case 5: box.X1 = x + 4; box.Z0 = z - 1; box.Z1 = z + 3; break;
                }
                if (pieces.Any(p => p.Box.Intersects(box))) return null;
            }
            else
            {
                int sections = kind == 1 ? r.NextInt(3) + 2 : 1;
                for (; sections > 0; sections--)
                {
                    int length = kind == 1 ? sections * 5 : 9;
                    switch (direction)
                    {
                        case 2: box.X1 = x + 2; box.Z0 = z - length + 1; break;
                        case 3: box.X1 = x + 2; box.Z1 = z + length - 1; break;
                        case 4: box.X0 = x - length + 1; box.Z1 = z + 2; break;
                        case 5: box.X1 = x + length - 1; box.Z1 = z + 2; break;
                    }
                    if (!pieces.Any(p => p.Box.Intersects(box))) break;
                    if (kind == 3) return null;
                }
                if (sections == 0) return null;
            }
            var result = new Piece { Box = box, Direction = direction, Depth = depth + 1, Kind = kind };
            if (kind == 1) { result.Rails = r.NextInt(3) == 0; result.Spiders = !result.Rails && r.NextInt(23) == 0; }
            pieces.Add(result); Connect(result);
            return result;
        }
        void Connect(Piece p)
        {
            var b = p.Box; int d = p.Depth;
            if (p.Kind == 0)
            {
                int height = Math.Max(1, b.SizeY - 4);
                foreach (int dir in new[] { 2, 3, 4, 5 })
                {
                    int size = dir is 2 or 3 ? b.SizeX : b.SizeZ;
                    for (int k = 0; k < size; k += 4)
                    {
                        k += r.NextInt(size); if (k + 3 > size) break;
                        int y = b.Y0 + r.NextInt(height) + 1;
                        var child = dir switch
                        {
                            2 => Add(b.X0 + k, y, b.Z0 - 1, dir, d), 3 => Add(b.X0 + k, y, b.Z1 + 1, dir, d),
                            4 => Add(b.X0 - 1, y, b.Z0 + k, dir, d), _ => Add(b.X1 + 1, y, b.Z0 + k, dir, d)
                        };
                        if (child == null) continue;
                        var c = child.Box;
                        p.Entrances.Add(dir switch
                        {
                            2 => new(c.X0, c.Y0, b.Z0, c.X1, c.Y1, b.Z0 + 1),
                            3 => new(c.X0, c.Y0, b.Z1 - 1, c.X1, c.Y1, b.Z1),
                            4 => new(b.X0, c.Y0, c.Z0, b.X0 + 1, c.Y1, c.Z1),
                            _ => new(b.X1 - 1, c.Y0, c.Z0, b.X1, c.Y1, c.Z1)
                        });
                    }
                }
            }
            else if (p.Kind == 3)
            {
                switch (p.Direction)
                {
                    case 2: Add(b.X0, b.Y0, b.Z0 - 1, 2, d); break;
                    case 3: Add(b.X0, b.Y0, b.Z1 + 1, 3, d); break;
                    case 4: Add(b.X0 - 1, b.Y0, b.Z0, 4, d); break;
                    case 5: Add(b.X1 + 1, b.Y0, b.Z0, 5, d); break;
                }
            }
            else if (p.Kind == 2)
            {
                void Exit(int dir, int y)
                {
                    switch (dir)
                    {
                        case 2: Add(b.X0 + 1, y, b.Z0 - 1, 2, d); break;
                        case 3: Add(b.X0 + 1, y, b.Z1 + 1, 3, d); break;
                        case 4: Add(b.X0 - 1, y, b.Z0 + 1, 4, d); break;
                        case 5: Add(b.X1 + 1, y, b.Z0 + 1, 5, d); break;
                    }
                }
                foreach (int dir in new[] { 2, 3, 4, 5 }) if (dir != (p.Direction ^ 1)) Exit(dir, b.Y0);
                if (b.SizeY > 3) foreach (int dir in new[] { 2, 4, 5, 3 }) if (r.NextBool()) Exit(dir, b.Y0 + 4);
            }
            else
            {
                int j = r.NextInt(4);
                switch (p.Direction)
                {
                    case 2:
                        if (j <= 1) Add(b.X0, b.Y0 - 1 + r.NextInt(3), b.Z0 - 1, 2, d);
                        else if (j == 2) Add(b.X0 - 1, b.Y0 - 1 + r.NextInt(3), b.Z0, 4, d);
                        else Add(b.X1 + 1, b.Y0 - 1 + r.NextInt(3), b.Z0, 5, d);
                        break;
                    case 3:
                        if (j <= 1) Add(b.X0, b.Y0 - 1 + r.NextInt(3), b.Z1 + 1, 3, d);
                        else if (j == 2) Add(b.X0 - 1, b.Y0 - 1 + r.NextInt(3), b.Z1 - 3, 4, d);
                        else Add(b.X1 + 1, b.Y0 - 1 + r.NextInt(3), b.Z1 - 3, 5, d);
                        break;
                    case 4:
                        if (j <= 1) Add(b.X0 - 1, b.Y0 - 1 + r.NextInt(3), b.Z0, 4, d);
                        else if (j == 2) Add(b.X0, b.Y0 - 1 + r.NextInt(3), b.Z0 - 1, 2, d);
                        else Add(b.X0, b.Y0 - 1 + r.NextInt(3), b.Z1 + 1, 3, d);
                        break;
                    case 5:
                        if (j <= 1) Add(b.X1 + 1, b.Y0 - 1 + r.NextInt(3), b.Z0, 5, d);
                        else if (j == 2) Add(b.X1 - 3, b.Y0 - 1 + r.NextInt(3), b.Z0 - 1, 2, d);
                        else Add(b.X1 - 3, b.Y0 - 1 + r.NextInt(3), b.Z1 + 1, 3, d);
                        break;
                }
                if (d >= 8) return;
                if (p.Direction is 4 or 5)
                    for (int x = b.X0 + 3; x + 3 <= b.X1; x += 5)
                    {
                        int roll = r.NextInt(5);
                        if (roll == 0) Add(x, b.Y0, b.Z0 - 1, 2, d + 1); else if (roll == 1) Add(x, b.Y0, b.Z1 + 1, 3, d + 1);
                    }
                else
                    for (int z = b.Z0 + 3; z + 3 <= b.Z1; z += 5)
                    {
                        int roll = r.NextInt(5);
                        if (roll == 0) Add(b.X0 - 1, b.Y0, z, 4, d + 1); else if (roll == 1) Add(b.X1 + 1, b.Y0, z, 5, d + 1);
                    }
            }
        }
        Connect(root);
        var bounds = new OwBox(pieces.Min(p => p.Box.X0), pieces.Min(p => p.Box.Y0), pieces.Min(p => p.Box.Z0), pieces.Max(p => p.Box.X1), pieces.Max(p => p.Box.Y1), pieces.Max(p => p.Box.Z1));
        int top = bounds.SizeY + 1;
        if (top < 53) top += r.NextInt(53 - top);
        int offset = top - bounds.Y1; bounds.MoveY(offset);
        foreach (var p in pieces) { p.Box.MoveY(offset); foreach (var e in p.Entrances) e.MoveY(offset); }
        var g = new OwGenRegion(gen, true);
        foreach (var p in pieces) Place(g, r, p);
        g.ReleaseReadCache();
        return new("Mineshaft", bounds, g);
    }

    private static void Place(OwGenRegion g, JRandom r, Piece p)
    {
        var b = p.Box;
        for (int x = b.X0 - 1; x <= b.X1 + 1; x++) for (int z = b.Z0 - 1; z <= b.Z1 + 1; z++)
            for (int y = Math.Max(0, b.Y0 - 1); y <= Math.Min(255, b.Y1 + 1); y++)
                if ((x == b.X0 - 1 || x == b.X1 + 1 || z == b.Z0 - 1 || z == b.Z1 + 1 || y == b.Y0 - 1 || y == b.Y1 + 1) && Block.Get(g.Get(x, y, z)).IsLiquid) return;
        void Fill(int x0, int y0, int z0, int x1, int y1, int z1, int id, bool existing = false)
        {
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                if (!existing || g.Get(x, y, z) != B.Air) g.Set(x, y, z, id);
        }
        if (p.Kind == 0)
        {
            Fill(b.X0, b.Y0, b.Z0, b.X1, b.Y0, b.Z1, B.Dirt, true);
            Fill(b.X0, b.Y0 + 1, b.Z0, b.X1, Math.Min(b.Y0 + 3, b.Y1), b.Z1, B.Air);
            foreach (var e in p.Entrances) Fill(e.X0, e.Y1 - 2, e.Z0, e.X1, e.Y1, e.Z1, B.Air);
            float sx = b.SizeX, sy = b.Y1 - (b.Y0 + 4) + 1, sz = b.SizeZ;
            float centerX = b.X0 + sx / 2f, centerZ = b.Z0 + sz / 2f;
            for (int y = b.Y0 + 4; y <= b.Y1; y++) for (int x = b.X0; x <= b.X1; x++) for (int z = b.Z0; z <= b.Z1; z++)
            {
                float dx = (x - centerX) / (sx * 0.5f), dy = (y - (b.Y0 + 4)) / sy, dz = (z - centerZ) / (sz * 0.5f);
                if (dx * dx + dy * dy + dz * dz <= 1.05f) g.Set(x, y, z, B.Air);
            }
            return;
        }
        if (p.Kind == 2)
        {
            void Cross(int y0, int y1)
            {
                Fill(b.X0 + 1, y0, b.Z0, b.X1 - 1, y1, b.Z1, B.Air);
                Fill(b.X0, y0, b.Z0 + 1, b.X1, y1, b.Z1 - 1, B.Air);
            }
            if (b.SizeY > 3)
            {
                Cross(b.Y0, b.Y0 + 2); Cross(b.Y1 - 2, b.Y1);
                Fill(b.X0 + 1, b.Y0 + 3, b.Z0 + 1, b.X1 - 1, b.Y0 + 3, b.Z1 - 1, B.Air);
            }
            else Cross(b.Y0, b.Y1);
            foreach (int x in new[] { b.X0 + 1, b.X1 - 1 }) foreach (int z in new[] { b.Z0 + 1, b.Z1 - 1 }) Fill(x, b.Y0, z, x, b.Y1, z, B.OakPlanks);
            for (int x = b.X0; x <= b.X1; x++) for (int z = b.Z0; z <= b.Z1; z++) if (g.Get(x, b.Y0 - 1, z) == B.Air) g.Set(x, b.Y0 - 1, z, B.OakPlanks);
            return;
        }
        int length = p.Direction is 2 or 3 ? b.SizeZ : b.SizeX;
        var s = new OwStructureWriter(g, r, b.X0, b.Y0, b.Z0, 3, length, p.Direction);
        if (p.Kind == 3)
        {
            s.Air(0, 5, 0, 2, 7, 1); s.Air(0, 0, 7, 2, 2, 8);
            for (int i = 0; i < 5; i++) s.Air(0, 5 - i - (i < 4 ? 1 : 0), 2 + i, 2, 7 - i, 2 + i);
            return;
        }
        void Maybe(float chance, int x, int y, int z, int id, int meta = 0) { if (r.NextFloat() < chance) s.Set(new(id, meta), x, y, z); }
        s.Air(0, 0, 0, 2, 1, length - 1);
        for (int x = 0; x <= 2; x++) for (int z = 0; z < length; z++) Maybe(0.8f, x, 2, z, B.Air);
        if (p.Spiders) for (int y = 0; y <= 1; y++) for (int x = 0; x <= 2; x++) for (int z = 0; z < length; z++) Maybe(0.6f, x, y, z, B.Cobweb);
        bool spawner = false;
        for (int section = 0; section < length / 5; section++)
        {
            int z = 2 + section * 5;
            s.Fill(0, 0, z, 0, 1, z, new(B.Fence), new(B.Air)); s.Fill(2, 0, z, 2, 1, z, new(B.Fence), new(B.Air));
            if (r.NextInt(4) == 0) { s.Set(new(B.OakPlanks), 0, 2, z); s.Set(new(B.OakPlanks), 2, 2, z); }
            else s.Fill(0, 2, z, 2, 2, z, new(B.OakPlanks), new(B.Air));
            foreach (int dz in new[] { -1, 1, -2, 2 }) foreach (int x in new[] { 0, 2 }) Maybe(Math.Abs(dz) == 1 ? 0.1f : 0.05f, x, 2, z + dz, B.Cobweb);
            Maybe(0.05f, 1, 2, z - 1, B.Torch, 5); Maybe(0.05f, 1, 2, z + 1, B.Torch, 5);
            void Cart(int x, int zz)
            {
                if (s.Get(x, 0, zz) != B.Air) return;
                int rail = s.Meta(B.Rail, r.NextBool() ? 1 : 0); s.Set(new(B.Rail, rail), x, 0, zz);
                var point = s.Point(x, 0, zz);
                g.Entities.Add(new("chest_minecart", point.x + 0.5, point.y + 0.5, point.z + 0.5, OwLoot.Generate(r, OwLoot.Mineshaft, 3 + r.NextInt(4))));
            }
            if (r.NextInt(100) == 0) Cart(2, z - 1);
            if (r.NextInt(100) == 0) Cart(0, z + 1);
            if (p.Spiders && !spawner)
            {
                var point = s.Point(1, 0, z - 1 + r.NextInt(3));
                g.Set(point.x, point.y, point.z, B.MobSpawner);
                g.Tile(point.x, point.y, point.z, new OwTilePlan { BlockId = B.MobSpawner, TypeId = 50, EntityId = "cave_spider" }); spawner = true;
            }
        }
        for (int x = 0; x <= 2; x++) for (int z = 0; z < length; z++) if (s.Get(x, -1, z) == B.Air) s.Set(new(B.OakPlanks), x, -1, z);
        if (p.Rails) for (int z = 0; z < length; z++) if (Block.Get(s.Get(1, -1, z)).FullCube) Maybe(0.7f, 1, 0, z, B.Rail, s.Meta(B.Rail, 0));
    }
}
