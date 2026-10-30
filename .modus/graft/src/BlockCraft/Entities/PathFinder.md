# src/BlockCraft/Entities/PathFinder.cs

- PathFinder · class · L7-L106 — public static class PathFinder
- Node · class · L9-L12 — private sealed class Node
- IsPassable · method · L16-L25 — public static bool IsPassable(World.World w, int x, int y, int z)
- IsDanger · method · L27-L31 — private static bool IsDanger(World.World w, int x, int y, int z)
- CanStand · method · L34-L43 — private static bool CanStand(World.World w, int x, int y, int z, int h, bool allowWater)
- Find · method · L45-L102 — public static List<(int x, int y, int z)> Find(World.World w, Entity e, int tx, int ty, int tz, int maxNodes = 600, float maxDist = 32)
- H · method · L104-L104 — private static float H(int x, int y, int z, int tx, int ty, int tz) => MathF.Sqrt((x - tx) * (x - tx) + (y - ty) * (y - ty) * 1.5f + (z - tz) * (z - tz));
- Key · method · L105-L105 — private static long Key(int x, int y, int z) => ((long)(x & 0xFFFFFF) << 32) | ((long)(z & 0xFFFFFF) << 8) | (uint)(y & 0xFF);
- Navigator · class · L108-L154 — public sealed class Navigator
- HasPath · variable · L115-L115 — public bool HasPath => _path != null && _idx < _path.Count;
- Target · variable · L116-L116 — public (int x, int y, int z) Target => _path != null && _path.Count > 0 ? _path[^1] : default;
- Navigator · method · L118-L118 — public Navigator(Mob m) { _m = m; }
- MoveTo · method · L120-L126 — public bool MoveTo(double x, double y, double z, double speed)
- SetSpeed · method · L127-L127 — public void SetSpeed(double s) => _speed = s;
- Stop · method · L128-L128 — public void Stop() { _path = null; _m.MoveForward = 0; }
- Tick · method · L130-L153 — public void Tick()
