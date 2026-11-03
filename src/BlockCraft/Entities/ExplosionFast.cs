using System.Runtime.CompilerServices;
using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Entities;

/// <summary>Szybka eksplozja z wynikiem identycznym jak ExplodeReference (te same liczby losowe w tej samej kolejności,
/// te same bloki w tej samej kolejności, ten sam odrzut). Zyski:
/// - bloki czytane z tablic chunków przez lokalną siatkę referencji (bez słownika na każdy krok promienia),
/// - kroki promienia poza obszarem niepustych bloków bez odczytu (powietrze nic nie zmienia),
/// - komórki powietrza nie trafiają do listy niszczenia (i tak były pomijane), deduplikacja bitsetem zamiast HashSet,
/// - ekspozycja: test "samo powietrze między wybuchem a encją" z zakresów kolumn; raycasty tylko gdy coś jest po drodze,
/// - encje z siatki przestrzennej bez alokacji.</summary>
public static partial class Explosion
{
    /// <summary>Testy porównawcze: true wymusza starą ścieżkę.</summary>
    public static bool UseReference;

    public static void Explode(World.World w, Entity source, double x, double y, double z, float power, bool breakBlocks)
    {
        if (UseReference || source is IExplosionBlockRules) { ExplodeReference(w, source, x, y, z, power, breakBlocks); return; }
        var s = Scratch.Rent();
        try { ExplodeFast(w, source, x, y, z, power, breakBlocks, s); }
        finally { Scratch.Return(s); }
    }

    private static void ExplodeFast(World.World w, Entity source, double x, double y, double z, float power, bool breakBlocks, Scratch s)
    {
        w.PlaySound("explode", x, y, z, 4f, (1f + (w.Rand.NextFloat() - w.Rand.NextFloat()) * 0.2f) * 0.7f);
        w.SpawnParticles(power >= 2 ? ParticleKind.Explosion : ParticleKind.LargeSmoke, x, y, z, 1, (int)(power * 10));
        s.Affected.Clear();
        long pt = Prof.Begin();
        if (breakBlocks && (w.Rules.MobGriefing || source is TntEntity)) Rays(w, s, x, y, z, power);
        Prof.End(Prof.ExpRays, pt); pt = Prof.Begin();

        // encje (jak w ExplodeReference)
        float r2 = power * 2f;
        var box = new AABB(x - r2 - 1, y - r2 - 1, z - r2 - 1, x + r2 + 1, y + r2 + 1, z + r2 + 1);
        var ents = s.Ents; ents.Clear();
        w.Grid.Query(box, null, ents, sorted: false);
        // Kolejność ma znaczenie tylko dla encji z efektami ubocznymi (Attack: losowość, dropy, zagnieżdżone wybuchy).
        // Same "czyste" encje (TNT, przedmioty, kule XP, bloki spadające) można liczyć w dowolnej kolejności.
        for (int n = 0; n < ents.Count; n++) if (ents[n].CanBeAttacked) { EntityGrid.SortBySeq(ents); break; }
        s.ExposureReady = false;
        for (int n = 0; n < ents.Count; n++)
        {
            var e = ents[n];
            double dist = Math.Sqrt(e.DistanceSq(x, y, z)) / r2;
            if (dist > 1) continue;
            double ex = e.X - x, ey = e.Y + e.EyeHeight - y, ez = e.Z - z;
            double d = Math.Sqrt(ex * ex + ey * ey + ez * ez);
            if (d == 0) continue;
            ex /= d; ey /= d; ez /= d;
            var bb = e.BoundingBox;
            double exposure;
            if (HullIsAir(w, s, x, y, z, bb, r2)) exposure = 1.0;
            else { long pe = Prof.Begin(); exposure = ExposureFast(w, s, x, y, z, bb); Prof.End(Prof.ExpExposureRef, pe); }
            double impact = (1 - dist) * exposure;
            float dmg = (float)((impact * impact + impact) / 2 * 8 * r2 + 1);
            if (e.CanBeAttacked) e.Attack(DamageSource.Explosion(source is TntEntity t ? (t.Igniter ?? source) : source), dmg);
            else if (e is ItemEntity || e is XpOrbEntity) { e.Kill(); continue; }
            double push = EnchantEffects.BlastKnockback(e, impact);
            e.MotionX += ex * push; e.MotionY += ey * push; e.MotionZ += ez * push;
        }
        ents.Clear();
        Prof.End(Prof.ExpEntities, pt); pt = Prof.Begin();

        // niszczenie bloków (kolejność jak w HashSet oryginału, bez komórek powietrza)
        var drops = s.Drops;
        var aff = s.Affected;
        for (int n = 0; n < aff.Count; n++)
        {
            var (bx, by, bz) = aff[n];
            int id = w.GetBlock(bx, by, bz);
            if (id == 0) continue;
            var b = Block.ById[id];
            if (id == B.Tnt) { w.SetBlock(bx, by, bz, B.Air); var t = new TntEntity(w, bx + 0.5, by, bz + 0.5, w.Rand.Next(20) + 10); w.SpawnEntity(t); continue; }
            if (b.IsLiquid) continue;
            int meta = w.GetMeta(bx, by, bz);
            if (w.Rand.NextFloat() < 1f / power)
            {
                drops.Clear();
                b.GetDrops(w, bx, by, bz, meta, w.Rand, null, drops);
                foreach (var st in drops) w.DropItem(bx + 0.5, by + 0.5, bz + 0.5, st);
            }
            w.SetBlock(bx, by, bz, B.Air);
        }
        aff.Clear();
        Prof.End(Prof.ExpDestroy, pt);
    }

    // ---------------- promienie ----------------
    private static void Rays(World.World w, Scratch s, double x, double y, double z, float power)
    {
        // zasięg: f < 1.3*power, co krok f maleje o >= 0.225, krok 0.3 bloku
        int steps = (int)Math.Ceiling(1.3 * power / 0.225) + 1;
        double reach = 0.3 * steps + 1;
        int x0 = MathUtil.FloorToInt(x - reach), x1 = MathUtil.FloorToInt(x + reach);
        int y0 = MathUtil.FloorToInt(y - reach), y1 = MathUtil.FloorToInt(y + reach);
        int z0 = MathUtil.FloorToInt(z - reach), z1 = MathUtil.FloorToInt(z + reach);
        var area = s.Area;
        area.Load(w, x0, z0, x1, z1);
        // ramka niepustych bloków w zasięgu (z zakresów kolumn)
        int mnx = int.MaxValue, mxx = int.MinValue, mny = int.MaxValue, mxy = int.MinValue, mnz = int.MaxValue, mxz = int.MinValue;
        for (int bx = x0; bx <= x1; bx++)
            for (int bz = z0; bz <= z1; bz++)
            {
                var c = area.Chunk(bx, bz);
                if (c == null) continue;
                int ci = ((bz & 15) << 4) | (bx & 15);
                int top = c.ColTop[ci] - 1, bot = c.ColBot[ci];
                if (top < bot || bot > y1 || top < y0) continue;
                int lo = Math.Max(bot, y0), hi = Math.Min(top, y1);
                if (bx < mnx) mnx = bx; if (bx > mxx) mxx = bx;
                if (bz < mnz) mnz = bz; if (bz > mxz) mxz = bz;
                if (lo < mny) mny = lo; if (hi > mxy) mxy = hi;
            }
        bool any = mnx != int.MaxValue;
        if (any) s.BeginSeen(x0, y0, z0, x1 - x0 + 1, y1 - y0 + 1, z1 - z0 + 1);
        var rand = w.Rand;
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
                for (int k = 0; k < 16; k++)
                {
                    if (i != 0 && i != 15 && j != 0 && j != 15 && k != 0 && k != 15) continue;
                    double dx = i / 15.0 * 2 - 1, dy = j / 15.0 * 2 - 1, dz = k / 15.0 * 2 - 1;
                    double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    dx /= len; dy /= len; dz /= len;
                    float f = power * (0.7f + rand.NextFloat() * 0.6f);
                    if (!any) continue; // w zasięgu samo powietrze: promień niczego nie zmienia
                    double px = x, py = y, pz = z;
                    double sx = dx * 0.3, sy = dy * 0.3, sz = dz * 0.3;
                    for (; f > 0; f -= 0.225f)
                    {
                        int bx = MathUtil.FloorToInt(px), by = MathUtil.FloorToInt(py), bz = MathUtil.FloorToInt(pz);
                        if (bx >= mnx && bx <= mxx && by >= mny && by <= mxy && bz >= mnz && bz <= mxz)
                        {
                            int id = area.Get(bx, by, bz);
                            if (id != 0)
                            {
                                var b = Block.ById[id];
                                float res = b.IsLiquid ? 100 : b.BlastResistance;
                                f -= (res + 0.3f) * 0.3f;
                                if (f > 0 && by >= 0 && by < Chunk.Height) s.AddAffected(bx, by, bz);
                            }
                        }
                        else
                        {
                            // poza ramką bloków: jeśli się oddala albo nie zdąży jej osiągnąć, reszta promienia to samo powietrze
                            if ((bx > mxx && sx >= 0) || (bx < mnx && sx <= 0) || (by > mxy && sy >= 0) || (by < mny && sy <= 0) || (bz > mxz && sz >= 0) || (bz < mnz && sz <= 0)) break;
                            double gap = Math.Max(Math.Max(bx > mxx ? px - (mxx + 1) : bx < mnx ? mnx - px : 0, by > mxy ? py - (mxy + 1) : by < mny ? mny - py : 0), bz > mxz ? pz - (mxz + 1) : bz < mnz ? mnz - pz : 0);
                            if (gap > 0.3 * (f / 0.225 + 1) + 0.01) break;
                        }
                        px += sx; py += sy; pz += sz;
                    }
                }
    }

    // ---------------- ekspozycja ----------------
    /// <summary>True, gdy w prostopadłościanie obejmującym środek wybuchu i ramkę encji (z zapasem na krok DDA na granicy)
    /// nie ma żadnego niepustego bloku: wtedy wszystkie 27 promieni ExposureReference przechodzi, wynik = 1.</summary>
    private static bool HullIsAir(World.World w, Scratch s, double x, double y, double z, AABB bb, float r2)
    {
        const double Eps = 1e-6;
        int hx0 = MathUtil.FloorToInt(Math.Min(x, bb.MinX) - Eps), hx1 = MathUtil.FloorToInt(Math.Max(x, bb.MaxX) + Eps);
        int hy0 = MathUtil.FloorToInt(Math.Min(y, bb.MinY) - Eps), hy1 = MathUtil.FloorToInt(Math.Max(y, bb.MaxY) + Eps);
        int hz0 = MathUtil.FloorToInt(Math.Min(z, bb.MinZ) - Eps), hz1 = MathUtil.FloorToInt(Math.Max(z, bb.MaxZ) + Eps);
        if (!s.ExposureReady)
        {
            // kolumny w obszarze zapytania encji z zapasem na ramki
            double m = r2 + 1 + EntityGrid.MarginUp + 1;
            s.ExposureReady = true;
            s.ExpX0 = MathUtil.FloorToInt(x - m); s.ExpZ0 = MathUtil.FloorToInt(z - m);
            int ex1 = MathUtil.FloorToInt(x + m), ez1 = MathUtil.FloorToInt(z + m);
            s.ExpW = ex1 - s.ExpX0 + 1; s.ExpD = ez1 - s.ExpZ0 + 1;
            var area = s.ExpArea;
            area.Load(w, s.ExpX0, s.ExpZ0, ex1, ez1);
            int n = s.ExpW * s.ExpD;
            if (s.ExpTop.Length < n) { s.ExpTop = new short[n]; s.ExpBot = new short[n]; }
            int slabLo = int.MaxValue, slabHi = int.MinValue;
            for (int ix = 0; ix < s.ExpW; ix++)
                for (int iz = 0; iz < s.ExpD; iz++)
                {
                    int bx = s.ExpX0 + ix, bz = s.ExpZ0 + iz;
                    var c = area.Chunk(bx, bz);
                    short top = 0, bot = Chunk.Height;
                    if (c != null) { int ci = ((bz & 15) << 4) | (bx & 15); top = c.ColTop[ci]; bot = c.ColBot[ci]; }
                    s.ExpTop[ix * s.ExpD + iz] = top; s.ExpBot[ix * s.ExpD + iz] = bot;
                    if (top > bot) { if (bot < slabLo) slabLo = bot; if (top - 1 > slabHi) slabHi = top - 1; }
                }
            s.SlabLo = slabLo; s.SlabHi = slabHi;
            s.ExpVersion = w.BlockVersion;
        }
        else if (s.ExpVersion != w.BlockVersion) { s.ExposureReady = false; return HullIsAir(w, s, x, y, z, bb, r2); }
        if (s.SlabLo > s.SlabHi || hy1 < s.SlabLo || hy0 > s.SlabHi) return true; // cały obszar pusty albo ramka nad/pod blokami
        int lx0 = hx0 - s.ExpX0, lx1 = hx1 - s.ExpX0, lz0 = hz0 - s.ExpZ0, lz1 = hz1 - s.ExpZ0;
        if (lx0 < 0 || lz0 < 0 || lx1 >= s.ExpW || lz1 >= s.ExpD) return false; // poza tabelą: dokładna ścieżka
        if ((lx1 - lx0 + 1) * (lz1 - lz0 + 1) > 64) return false;
        for (int ix = lx0; ix <= lx1; ix++)
            for (int iz = lz0; iz <= lz1; iz++)
            {
                int ci = ix * s.ExpD + iz;
                int top = s.ExpTop[ci] - 1, bot = s.ExpBot[ci];
                if (top >= bot && bot <= hy1 && top >= hy0) return false;
            }
        return true;
    }

    /// <summary>ExposureReference z odczytem bloków przez ChunkArea (ten sam DDA i te same testy co World.RayCast).</summary>
    private static double ExposureFast(World.World w, Scratch s, double x, double y, double z, AABB bb)
    {
        int hx0 = MathUtil.FloorToInt(Math.Min(x, bb.MinX)) - 2, hx1 = MathUtil.FloorToInt(Math.Max(x, bb.MaxX)) + 2;
        int hz0 = MathUtil.FloorToInt(Math.Min(z, bb.MinZ)) - 2, hz1 = MathUtil.FloorToInt(Math.Max(z, bb.MaxZ)) + 2;
        if (!s.ExposureReady || hx0 < s.ExpX0 || hz0 < s.ExpZ0 || hx1 >= s.ExpX0 + s.ExpW || hz1 >= s.ExpZ0 + s.ExpD)
            return ExposureReference(w, x, y, z, bb);
        var a = s.ExpArea;
        int hit = 0, total = 0;
        for (double fx = 0; fx <= 1; fx += 0.5)
            for (double fy = 0; fy <= 1; fy += 0.5)
                for (double fz = 0; fz <= 1; fz += 0.5)
                {
                    double tx = bb.MinX + (bb.MaxX - bb.MinX) * fx, ty = bb.MinY + (bb.MaxY - bb.MinY) * fy, tz = bb.MinZ + (bb.MaxZ - bb.MinZ) * fz;
                    double dx = tx - x, dy = ty - y, dz = tz - z;
                    double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    total++;
                    if (len < 0.01) { hit++; continue; }
                    if (!RayBlocked(w, a, x, y, z, dx / len, dy / len, dz / len, len)) hit++;
                }
        return (double)hit / total;
    }

    [ThreadStatic] private static List<AABB> _boxes;
    /// <summary>Kopia World.RayCast (hitLiquids = false): true, gdy promień trafia blok i ten blok jest nieprzezroczysty.</summary>
    private static bool RayBlocked(World.World w, ChunkArea a, double ox, double oy, double oz, double dx, double dy, double dz, double maxDist)
    {
        int x = MathUtil.FloorToInt(ox), y = MathUtil.FloorToInt(oy), z = MathUtil.FloorToInt(oz);
        int stepX = Math.Sign(dx), stepY = Math.Sign(dy), stepZ = Math.Sign(dz);
        double tMaxX = dx != 0 ? ((stepX > 0 ? x + 1 - ox : ox - x) / Math.Abs(dx)) : double.MaxValue;
        double tMaxY = dy != 0 ? ((stepY > 0 ? y + 1 - oy : oy - y) / Math.Abs(dy)) : double.MaxValue;
        double tMaxZ = dz != 0 ? ((stepZ > 0 ? z + 1 - oz : oz - z) / Math.Abs(dz)) : double.MaxValue;
        double tdX = dx != 0 ? 1 / Math.Abs(dx) : double.MaxValue, tdY = dy != 0 ? 1 / Math.Abs(dy) : double.MaxValue, tdZ = dz != 0 ? 1 / Math.Abs(dz) : double.MaxValue;
        var boxes = _boxes ??= new List<AABB>();
        for (int i = 0; i < 200; i++)
        {
            if (y >= 0 && y < Chunk.Height)
            {
                byte id = a.Get(x, y, z);
                if (id != 0)
                {
                    var b = Block.ById[id];
                    boxes.Clear();
                    if (!b.IsLiquid) b.GetSelectionBoxes(w, x, y, z, a.GetMeta(x, y, z), boxes);
                    double best = double.MaxValue; int bf = -1;
                    foreach (var bb in boxes)
                    {
                        if (bb.Offset(x, y, z).RayIntersect(ox, oy, oz, dx, dy, dz, out double t, out int f) && t < best && f >= 0) { best = t; bf = f; }
                    }
                    if (bf >= 0 && best <= maxDist) return Block.Get(id).Opaque;
                }
            }
            double tNext = Math.Min(tMaxX, Math.Min(tMaxY, tMaxZ));
            if (tNext > maxDist) break;
            if (tMaxX < tMaxY && tMaxX < tMaxZ) { x += stepX; tMaxX += tdX; }
            else if (tMaxY < tMaxZ) { y += stepY; tMaxY += tdY; }
            else { z += stepZ; tMaxZ += tdZ; }
        }
        return false;
    }

    // ---------------- dane robocze ----------------
    /// <summary>Siatka referencji chunków dla prostokąta bloków: odczyt bez słownika.</summary>
    internal sealed class ChunkArea
    {
        private Chunk[] _c = new Chunk[16];
        private int _cx0, _cz0, _w, _d;
        public void Load(World.World w, int x0, int z0, int x1, int z1)
        {
            _cx0 = x0 >> 4; _cz0 = z0 >> 4;
            _w = (x1 >> 4) - _cx0 + 1; _d = (z1 >> 4) - _cz0 + 1;
            if (_c.Length < _w * _d) _c = new Chunk[_w * _d];
            for (int i = 0; i < _w; i++)
                for (int j = 0; j < _d; j++) _c[i * _d + j] = w.GetChunk(_cx0 + i, _cz0 + j);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Chunk Chunk(int bx, int bz)
        {
            int i = (bx >> 4) - _cx0, j = (bz >> 4) - _cz0;
            if ((uint)i >= (uint)_w || (uint)j >= (uint)_d) return null;
            return _c[i * _d + j];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Get(int bx, int by, int bz)
        {
            if ((uint)by >= World.Chunk.Height) return 0;
            var c = Chunk(bx, bz);
            return c == null ? (byte)0 : c.Blocks[(by << 8) | ((bz & 15) << 4) | (bx & 15)];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetMeta(int bx, int by, int bz)
        {
            if ((uint)by >= World.Chunk.Height) return 0;
            var c = Chunk(bx, bz);
            return c == null ? 0 : c.Meta[(by << 8) | ((bz & 15) << 4) | (bx & 15)];
        }
    }

    private sealed class Scratch
    {
        [ThreadStatic] private static Stack<Scratch> _pool;
        public static Scratch Rent() { var p = _pool ??= new(); return p.Count > 0 ? p.Pop() : new Scratch(); }
        public static void Return(Scratch s) => (_pool ??= new()).Push(s);

        public readonly List<(int, int, int)> Affected = new();
        public readonly List<Entity> Ents = new();
        public readonly List<ItemStack> Drops = new();
        public readonly ChunkArea Area = new(), ExpArea = new();
        private ulong[] _seen = new ulong[256];
        private int _sx, _sy, _sz, _sw, _sh, _sd;

        public bool ExposureReady;
        public int ExpX0, ExpZ0, ExpW, ExpD, SlabLo, SlabHi;
        public long ExpVersion;
        public short[] ExpTop = new short[0], ExpBot = new short[0];

        public void BeginSeen(int x0, int y0, int z0, int w, int h, int d)
        {
            _sx = x0; _sy = y0; _sz = z0; _sw = w; _sh = h; _sd = d;
            int words = (w * h * d + 63) >> 6;
            if (_seen.Length < words) _seen = new ulong[words];
            else Array.Clear(_seen, 0, words);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddAffected(int x, int y, int z)
        {
            int i = ((x - _sx) * _sd + (z - _sz)) * _sh + (y - _sy);
            ulong bit = 1UL << (i & 63);
            ref ulong word = ref _seen[i >> 6];
            if ((word & bit) != 0) return;
            word |= bit;
            Affected.Add((x, y, z));
        }
    }
}
