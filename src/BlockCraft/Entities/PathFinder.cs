using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>A* po siatce bloków dla mobów.</summary>
public static class PathFinder
{
    private sealed class Node
    {
        public int X, Y, Z; public float G, F; public Node Parent;
    }

    private static readonly List<AABB> Tmp = new();

    public static bool IsPassable(World.World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z);
        if (id == 0) return true;
        var b = Block.ById[id];
        if (id == B.OakDoor) return (w.GetMeta(x, y, z) & 4) != 0 || (w.GetMeta(x, y - 1, z) & 4) != 0;
        if (b.IsLiquid) return id == B.Water;
        if (id == B.Cactus || id == B.Fence || id == B.FenceGate) return false;
        return !b.Solid;
    }

    private static bool IsDanger(World.World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z);
        return id == B.Lava || id == B.Cactus || id == B.Fire;
    }

    /// <summary>Czy mob może stać w (x,y,z): miejsce wolne na height bloków i grunt pod spodem.</summary>
    private static bool CanStand(World.World w, int x, int y, int z, int h, bool allowWater, bool doors = false)
    {
        for (int i = 0; i < h; i++) if (!(IsPassable(w, x, y + i, z) || doors && w.GetBlock(x, y + i, z) == B.OakDoor) || IsDanger(w, x, y + i, z)) return false;
        int below = w.GetBlock(x, y - 1, z);
        if (below == B.Water) return allowWater;
        var bb = Block.Get(below);
        if (below == B.Fence || below == B.Lava || below == B.Cactus) return false;
        if (w.GetBlock(x, y, z) == B.Water) return allowWater;
        return bb.Solid;
    }

    public static List<(int x, int y, int z)> Find(World.World w, Entity e, int tx, int ty, int tz, int maxNodes = 600, float maxDist = 32)
    {
        int sx = MathUtil.FloorToInt(e.X), sy = MathUtil.FloorToInt(e.Y + 0.01), sz = MathUtil.FloorToInt(e.Z);
        int h = (int)Math.Ceiling(e.Height); bool doors = e is Mob m && m.CanOpenDoors;
        if (!w.IsLoadedAt(tx, tz)) return null;
        var open = new PriorityQueue<Node, float>();
        var all = new Dictionary<long, Node>();
        var start = new Node { X = sx, Y = sy, Z = sz, G = 0 };
        start.F = H(sx, sy, sz, tx, ty, tz);
        open.Enqueue(start, start.F);
        all[Key(sx, sy, sz)] = start;
        Node best = start; float bestH = start.F;
        int processed = 0;
        var closed = new HashSet<long>();
        while (open.Count > 0 && processed < maxNodes)
        {
            var n = open.Dequeue();
            long nk = Key(n.X, n.Y, n.Z);
            if (!closed.Add(nk)) continue;
            processed++;
            float hh = H(n.X, n.Y, n.Z, tx, ty, tz);
            if (hh < bestH) { bestH = hh; best = n; }
            if (n.X == tx && n.Z == tz && Math.Abs(n.Y - ty) <= 1) { best = n; break; }
            for (int d = 0; d < 4; d++)
            {
                int nx = n.X + Dir.HorizX[d], nz = n.Z + Dir.HorizZ[d];
                int ny = -999;
                if (CanStand(w, nx, n.Y, nz, h, true, doors)) ny = n.Y;
                else if (CanStand(w, nx, n.Y + 1, nz, h, true, doors) && IsPassable(w, n.X, n.Y + h, n.Z)) ny = n.Y + 1;
                else
                {
                    for (int dy = 1; dy <= 3; dy++)
                    {
                        bool free = true;
                        for (int i = 0; i < h; i++) if (!IsPassable(w, nx, n.Y - dy + 1 + i, nz)) { free = false; break; }
                        if (!free) break;
                        if (CanStand(w, nx, n.Y - dy, nz, h, true, doors)) { ny = n.Y - dy; break; }
                    }
                }
                if (ny == -999) continue;
                long k = Key(nx, ny, nz);
                if (closed.Contains(k)) continue;
                float cost = 1f + (ny != n.Y ? 0.5f : 0f) + (w.GetBlock(nx, ny, nz) == B.Water ? 2f : 0f);
                float g = n.G + cost;
                if (all.TryGetValue(k, out var ex) && ex.G <= g) continue;
                if (Math.Abs(nx - sx) > maxDist || Math.Abs(nz - sz) > maxDist) continue;
                var nn = new Node { X = nx, Y = ny, Z = nz, G = g, Parent = n };
                nn.F = g + H(nx, ny, nz, tx, ty, tz) * 1.2f;
                all[k] = nn;
                open.Enqueue(nn, nn.F);
            }
        }
        if (best == start) return null;
        var path = new List<(int, int, int)>();
        for (var p = best; p != null && p != start; p = p.Parent) path.Add((p.X, p.Y, p.Z));
        path.Reverse();
        return path;
    }

    private static float H(int x, int y, int z, int tx, int ty, int tz) => MathF.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty) * 1.5f + (z - tz) * (z - tz));
    private static long Key(int x, int y, int z) => ((long)(x & 0xFFFFFF) << 32) | ((long)(z & 0xFFFFFF) << 8) | (uint)(y & 0xFF);
}

public sealed class Navigator
{
    private readonly Mob _m;
    private List<(int x, int y, int z)> _path;
    private int _idx;
    private double _speed;
    private int _stuckTicks; private double _lastDist = double.MaxValue;
    public bool HasPath => _path != null && _idx < _path.Count;
    public (int x, int y, int z) Target => _path != null && _path.Count > 0 ? _path[^1] : default;

    public Navigator(Mob m) { _m = m; }

    public bool MoveTo(double x, double y, double z, double speed)
    {
        var p = PathFinder.Find(_m.World, _m, MathUtil.FloorToInt(x), MathUtil.FloorToInt(y), MathUtil.FloorToInt(z), 500, 40);
        if (p == null || p.Count == 0) { _path = null; return false; }
        _path = p; _idx = 0; _speed = speed; _stuckTicks = 0; _lastDist = double.MaxValue;
        return true;
    }
    public void SetSpeed(double s) => _speed = s;
    public void Stop() { _path = null; _m.MoveForward = 0; }

    public void Tick()
    {
        if (!HasPath) return;
        var (x, y, z) = _path[_idx];
        if (_m.CanOpenDoors && _m.World.GetBlock(x,y,z)==B.OakDoor) { int meta=_m.World.GetMeta(x,y,z); if ((meta&8)==0 && (meta&4)==0) { _m.World.SetMeta(x,y,z,meta|4); _m.World.PlaySound("random.door_open",x,y,z); } }
        double tx = x + 0.5, tz = z + 0.5;
        double dx = tx - _m.X, dz = tz - _m.Z;
        double dist = Math.Sqrt(dx * dx + dz * dz);
        double reach = Math.Max(0.35, _m.Width / 2);
        if (dist < reach && Math.Abs(_m.Y - y) < 1.5)
        {
            _idx++;
            _stuckTicks = 0; _lastDist = double.MaxValue;
            if (_idx >= _path.Count) { Stop(); return; }
            (x, y, z) = _path[_idx]; tx = x + 0.5; tz = z + 0.5; dx = tx - _m.X; dz = tz - _m.Z; dist = Math.Sqrt(dx * dx + dz * dz);
        }
        if (dist < _lastDist - 0.01) { _lastDist = dist; _stuckTicks = 0; }
        else if (++_stuckTicks > 60) { Stop(); return; }
        float targetYaw = (float)(Math.Atan2(dz, dx) * MathUtil.Rad2Deg) - 90f;
        _m.Yaw = _m.Yaw + Math.Clamp(MathUtil.WrapDegrees(targetYaw - _m.Yaw), -30f, 30f);
        _m.SetMoveSpeed((float)_speed);
        if ((y > _m.Y + 0.5 && dist < 1.5) || (_m.CollidedH && _m.OnGround)) _m.IsJumping = true;
        else if (!_m.InWater) _m.IsJumping = false;
        if (_m.InWater) _m.IsJumping = _m.World.Rand.NextFloat() < 0.8f;
    }
}
