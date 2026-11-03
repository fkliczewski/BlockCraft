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
        s.ExposureReady = false;
        long pq = Prof.Begin();
        if (w.Grid.TryPureCells(box, s.Cells, out int total))
        {
            // W zasięgu wyłącznie "czyste" encje: zapytanie i odrzut w jednym przebiegu po komórkach siatki,
            // równolegle przy dużej chmurze. Każda encja zmienia tylko własny ruch, więc kolejność nie ma znaczenia.
            if (total > 0)
            {
                HullIsAir(w, s, x, y, z, new AABB(x, y, z, x, y, z), r2); // przygotowanie tabel przed wątkami
                var heavy = s.Heavy; heavy.Clear();
                PushCells(w, s, x, y, z, r2, box, heavy);
                if (heavy.Count >= ParallelMin)
                {
                    int count = heavy.Count, parts = Math.Min(Environment.ProcessorCount, Math.Max(2, count / 8));
                    var job = s.Job; job.W = w; job.S = s; job.X = x; job.Y = y; job.Z = z; job.R2 = r2; job.Count = count; job.Parts = parts;
                    Parallel.For(0, parts, job.Run);
                }
                else for (int n = 0; n < heavy.Count; n++) PushPure(w, s, heavy[n], x, y, z, r2, null);
                heavy.Clear();
            }
            s.Cells.Clear();
            Prof.End(Prof.OtherTick, pq);
        }
        else
        {
        w.Grid.QueryNear(box, x, y, z, r2, ents);
        Prof.End(Prof.OtherTick, pq);
        // Kolejność ma znaczenie tylko dla encji z efektami ubocznymi (Attack: losowość, dropy, zagnieżdżone wybuchy).
        // Same "czyste" encje (TNT, przedmioty, kule XP, bloki spadające) można liczyć w dowolnej kolejności.
        bool impure = false;
        for (int n = 0; n < ents.Count; n++) if (ents[n].CanBeAttacked) { impure = true; EntityGrid.SortBySeq(ents); break; }
        if (!impure)
        {
            // same "czyste" encje: każda zmienia tylko własny ruch, świat jest tylko czytany, więc wynik nie zależy od kolejności.
            // Faza A: tanie (bez przeszkód między wybuchem a encją) od razu; faza B: wymagające raycastów, równolegle gdy jest ich dużo.
            var heavy = s.Heavy; heavy.Clear();
            for (int n = 0; n < ents.Count; n++) PushPure(w, s, ents[n], x, y, z, r2, heavy);
            if (heavy.Count >= ParallelMin)
            {
                int count = heavy.Count, parts = Math.Min(Environment.ProcessorCount, Math.Max(2, count / 8));
                var job = s.Job; job.W = w; job.S = s; job.X = x; job.Y = y; job.Z = z; job.R2 = r2; job.Count = count; job.Parts = parts;
                Parallel.For(0, parts, job.Run);
            }
            else for (int n = 0; n < heavy.Count; n++) PushPure(w, s, heavy[n], x, y, z, r2, null);
            heavy.Clear();
        }
        else
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

    private const int ParallelMin = 24;

    /// <summary>Odrzut jednej czystej encji (bez Attack): identyczne obliczenia jak w pętli sekwencyjnej.</summary>
    private static void PushPure(World.World w, Scratch s, Entity e, double x, double y, double z, float r2, List<Entity> deferHeavy)
    {
        double dist = Math.Sqrt(e.DistanceSq(x, y, z)) / r2;
        if (dist > 1) return;
        double ex = e.X - x, ey = e.Y + e.EyeHeight - y, ez = e.Z - z;
        double d = Math.Sqrt(ex * ex + ey * ey + ez * ez);
        if (d == 0) return;
        ex /= d; ey /= d; ez /= d;
        var bb = e.BoundingBox;
        double exposure;
        if (HullIsAir(w, s, x, y, z, bb, r2)) exposure = 1.0;
        else if (deferHeavy != null) { deferHeavy.Add(e); return; }
        else exposure = ExposureFast(w, s, x, y, z, bb);
        double impact = (1 - dist) * exposure;
        if (e is ItemEntity || e is XpOrbEntity) { e.Kill(); return; }
        double push = EnchantEffects.BlastKnockback(e, impact);
        e.MotionX += ex * push; e.MotionY += ey * push; e.MotionZ += ez * push;
    }

    /// <summary>Przebieg po komórkach siatki (same czyste encje): filtr jak EntityGrid.QueryNear, a potem obliczenia PushPure
    /// na kopii pozycji i rozmiaru z siatki (czyste typy nie nadpisują BoundingBox ani EyeHeight). Encje wymagające raycastów
    /// trafiają do heavy.</summary>
    private static void PushCells(World.World w, Scratch s, double x, double y, double z, float r2, AABB box, List<Entity> heavy)
    {
        double maxSq = (double)r2 * r2 * 1.000001 + 1e-9;
        var cells = s.Cells;
        for (int ci = 0; ci < cells.Count; ci++)
        {
            var c = cells[ci];
            var items = c.Items; var pp = c.P; var ss = c.S;
            for (int i = 0, n = c.Count; i < n; i++)
            {
                double ex = pp[i * 3], ey = pp[i * 3 + 1], ez = pp[i * 3 + 2];
                float hw = ss[i * 2], h = ss[i * 2 + 1];
                if (!(box.MaxX > ex - hw && box.MinX < ex + hw && box.MaxY > ey && box.MinY < ey + h && box.MaxZ > ez - hw && box.MinZ < ez + hw)) continue;
                double qx = ex - x, qy = ey - y, qz = ez - z;
                double dsq = qx * qx + qy * qy + qz * qz;
                if (dsq > maxSq) continue;
                double dist = Math.Sqrt(dsq) / r2;
                if (dist > 1) continue;
                var e = items[i];
                if (e.Removed) continue;
                // jak PushPure: e.Y + e.EyeHeight (Height * 0.85f), ramka jak Entity.BoundingBox
                double dx = qx, dy = ey + h * 0.85f - y, dz = qz;
                double d = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d == 0) continue;
                dx /= d; dy /= d; dz /= d;
                var bb = new AABB(ex - hw, ey, ez - hw, ex + hw, ey + h, ez + hw);
                if (!HullIsAir(w, s, x, y, z, bb, r2)) { heavy.Add(e); continue; }
                double impact = 1 - dist; // ekspozycja 1
                if (e is ItemEntity || e is XpOrbEntity) { e.Kill(); continue; }
                e.MotionX += dx * impact; e.MotionY += dy * impact; e.MotionZ += dz * impact; // BlastKnockback: nie-żywa encja = impact
            }
        }
    }

    private sealed class PushJob
    {
        public World.World W; public Scratch S; public double X, Y, Z; public float R2; public int Count, Parts;
        public readonly Action<int> Run;
        public PushJob() { Run = RunPart; }
        private void RunPart(int p)
        {
            int from = (int)((long)Count * p / Parts), to = (int)((long)Count * (p + 1) / Parts);
            var ents = S.Heavy;
            for (int i = from; i < to; i++) PushPure(W, S, ents[i], X, Y, Z, R2, null);
        }
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
        var rand = w.Rand;
        var fs = s.RayF;
        for (int r = 0; r < RayCount; r++) fs[r] = power * (0.7f + rand.NextFloat() * 0.6f); // te same losowania, w tej samej kolejności
        if (!any) return; // w zasięgu samo powietrze: promienie niczego nie zmieniają
        var job = s.RayJob;
        job.S = s; job.Area = area; job.X = x; job.Y = y; job.Z = z;
        job.Fx0 = mnx; job.Fx1 = (double)mxx + 1; job.Fy0 = mny; job.Fy1 = (double)mxy + 1; job.Fz0 = mnz; job.Fz1 = (double)mxz + 1;
        job.Begin(x0, y0, z0, x1 - x0 + 1, y1 - y0 + 1, z1 - z0 + 1, RayParts);
        Parallel.For(0, RayParts, job.Run);
        // scalanie w kolejności promieni: pierwsze wystąpienie wygrywa, jak w HashSet oryginału
        s.BeginSeen(x0, y0, z0, x1 - x0 + 1, y1 - y0 + 1, z1 - z0 + 1);
        for (int part = 0; part < RayParts; part++)
        {
            var list = job.Out[part];
            for (int n = 0; n < list.Count; n++) { var (bx, by, bz) = list[n]; s.AddAffected(bx, by, bz); }
        }
    }

    private const int RayCount = 1352, RayParts = 8;
    private static readonly double[] RayDx = new double[RayCount], RayDy = new double[RayCount], RayDz = new double[RayCount];
    static Explosion()
    {
        int r = 0;
        for (int i = 0; i < 16; i++)
            for (int j = 0; j < 16; j++)
                for (int k = 0; k < 16; k++)
                {
                    if (i != 0 && i != 15 && j != 0 && j != 15 && k != 0 && k != 15) continue;
                    double dx = i / 15.0 * 2 - 1, dy = j / 15.0 * 2 - 1, dz = k / 15.0 * 2 - 1;
                    double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    dx /= len; dy /= len; dz /= len;
                    RayDx[r] = dx; RayDy[r] = dy; RayDz[r] = dz; r++;
                }
    }

    /// <summary>Promienie z zakresu [part*N/P, (part+1)*N/P): każdy wątek ma własną listę trafionych bloków (w kolejności promieni).</summary>
    private sealed class RayJob
    {
        public Scratch S; public ChunkArea Area; public double X, Y, Z, Fx0, Fx1, Fy0, Fy1, Fz0, Fz1;
        public readonly List<(int, int, int)>[] Out = new List<(int, int, int)>[RayParts];
        private readonly ulong[][] _seen = new ulong[RayParts][];
        private int _sx, _sy, _sz, _sh, _sd, _words, _parts;
        public readonly Action<int> Run;
        public RayJob() { Run = RunPart; for (int i = 0; i < RayParts; i++) { Out[i] = new(); _seen[i] = new ulong[64]; } }
        public void Begin(int x0, int y0, int z0, int w, int h, int d, int parts)
        {
            _sx = x0; _sy = y0; _sz = z0; _sh = h; _sd = d; _parts = parts;
            _words = (w * h * d + 63) >> 6;
        }
        private void RunPart(int part)
        {
            var outList = Out[part]; outList.Clear();
            var seen = _seen[part];
            if (seen.Length < _words) seen = _seen[part] = new ulong[_words]; else Array.Clear(seen, 0, _words);
            var area = Area; var fs = S.RayF;
            double x = X, y = Y, z = Z, fx0 = Fx0, fx1 = Fx1, fy0 = Fy0, fy1 = Fy1, fz0 = Fz0, fz1 = Fz1;
            int from = RayCount * part / _parts, to = RayCount * (part + 1) / _parts;
            for (int r = from; r < to; r++)
            {
                float f = fs[r];
                double px = x, py = y, pz = z;
                double sx = RayDx[r] * 0.3, sy = RayDy[r] * 0.3, sz = RayDz[r] * 0.3;
                // promień, który nie zdąży dolecieć do ramki bloków, niczego nie zmienia
                double gap0 = Math.Max(Math.Max(px >= fx1 ? px - fx1 : px < fx0 ? fx0 - px : 0, py >= fy1 ? py - fy1 : py < fy0 ? fy0 - py : 0), pz >= fz1 ? pz - fz1 : pz < fz0 ? fz0 - pz : 0);
                if (gap0 > 0.3 * (f / 0.225 + 1) + 0.01) continue;
                for (; f > 0; f -= 0.225f)
                {
                    // floor(p) w [mn, mx]  <=>  mn <= p < mx + 1
                    if (px >= fx0 && px < fx1 && py >= fy0 && py < fy1 && pz >= fz0 && pz < fz1)
                    {
                        int bx = MathUtil.FloorToInt(px), by = MathUtil.FloorToInt(py), bz = MathUtil.FloorToInt(pz);
                        int id = area.Get(bx, by, bz);
                        if (id != 0)
                        {
                            var b = Block.ById[id];
                            float res = b.IsLiquid ? 100 : b.BlastResistance;
                            f -= (res + 0.3f) * 0.3f;
                            if (f > 0 && by >= 0 && by < Chunk.Height)
                            {
                                int i = ((bx - _sx) * _sd + (bz - _sz)) * _sh + (by - _sy);
                                ulong bit = 1UL << (i & 63);
                                ref ulong word = ref seen[i >> 6];
                                if ((word & bit) == 0) { word |= bit; outList.Add((bx, by, bz)); }
                            }
                        }
                    }
                    else if ((px >= fx1 && sx >= 0) || (px < fx0 && sx <= 0) || (py >= fy1 && sy >= 0) || (py < fy0 && sy <= 0) || (pz >= fz1 && sz >= 0) || (pz < fz0 && sz <= 0))
                        break; // poza ramką i oddala się: dalej samo powietrze
                    px += sx; py += sy; pz += sz;
                }
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

    /// <summary>Bloki, których box zaznaczenia to zawsze pełny sześcian (brak nadpisań GetSelectionBoxes/GetCollisionBoxes, nie płyn, nie roślina).</summary>
    private static readonly bool[] UnitSelection = BuildUnitSelection();
    private static bool[] BuildUnitSelection()
    {
        var r = new bool[256];
        for (int i = 1; i < 256; i++)
        {
            var b = Block.ById[i];
            if (b == null || b.IsLiquid || b.Render == RenderType.Cross || b.Render == RenderType.Crops) continue;
            var t = b.GetType();
            var sel = t.GetMethod(nameof(Block.GetSelectionBoxes)); var col = t.GetMethod(nameof(Block.GetCollisionBoxes));
            r[i] = sel?.DeclaringType == typeof(Block) && col?.DeclaringType == typeof(Block);
        }
        return r;
    }
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
                    double best = double.MaxValue; int bf = -1;
                    if (UnitSelection[id])
                    {
                        // domyślny box zaznaczenia 0..1 (ta sama arytmetyka co bb.Offset(x, y, z) dla (0,0,0,1,1,1))
                        if (new AABB(x, y, z, x + 1, y + 1, z + 1).RayIntersect(ox, oy, oz, dx, dy, dz, out double t, out int f) && t < best && f >= 0) { best = t; bf = f; }
                    }
                    else
                    {
                        boxes.Clear();
                        if (!b.IsLiquid) b.GetSelectionBoxes(w, x, y, z, a.GetMeta(x, y, z), boxes);
                        foreach (var bb in boxes)
                        {
                            if (bb.Offset(x, y, z).RayIntersect(ox, oy, oz, dx, dy, dz, out double t, out int f) && t < best && f >= 0) { best = t; bf = f; }
                        }
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
        public readonly List<Entity> Ents = new(), Heavy = new();
        public readonly List<ItemStack> Drops = new();
        public readonly ChunkArea Area = new(), ExpArea = new();
        public readonly PushJob Job = new();
        public readonly List<EntityGrid.Cell> Cells = new();
        public readonly float[] RayF = new float[RayCount];
        public readonly RayJob RayJob = new();
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
