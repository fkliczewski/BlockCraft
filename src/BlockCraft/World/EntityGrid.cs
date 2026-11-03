using System.Runtime.CompilerServices;
using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

/// <summary>Siatka przestrzenna encji świata (komórki 8x8x8 po pozycji stóp). Zastępuje liniowe przejście po
/// wszystkich encjach w GetEntitiesInBox. Wynik zapytania jest sortowany po kolejności dodania do World.Entities,
/// więc jest identyczny z dawną pętlą po liście (mechanika i odrzut eksplozji bez zmian).
/// Komórka trzyma kopię pozycji i rozmiaru encji (tablice obok siebie), więc filtr ramek nie dotyka obiektów encji.
/// Encje z własną ramką (BoundingBox nadpisany) albo dużą trafiają na osobną listę "duże".</summary>
public sealed class EntityGrid
{
    private const int Shift = 3; // komórka 8 bloków
    // zapas zapytania względem pozycji encji: ramka encji mieści się w [X-H, X+H] x [Y-Down, Y+Up]
    public const double MarginH = 2.5, MarginUp = 4.5, MarginDown = 0.5;

    internal sealed class Cell
    {
        public Entity[] Items = new Entity[8];
        public double[] P = new double[24]; // x, y, z
        public float[] S = new float[16];   // Width / 2, Height
        public int Count, Impure; // Impure: encje spoza listy "czystych" (mogą mieć efekty uboczne przy Attack)
        public void Grow()
        {
            int n = Items.Length * 2;
            Array.Resize(ref Items, n); Array.Resize(ref P, n * 3); Array.Resize(ref S, n * 2);
        }
    }
    private readonly Dictionary<long, Cell> _cells = new();
    private readonly List<Entity> _big = new();
    /// <summary>Encje bez efektów ubocznych w eksplozji (CanBeAttacked = false na stałe, zmieniają tylko własny ruch).</summary>
    private static bool IsPureType(Entity e) => e is TntEntity || e is ItemEntity || e is XpOrbEntity || e is FallingBlockEntity;
    private readonly Stack<Cell> _free = new();
    private long _seq;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CellCoord(double v)
    {
        if (!(v > -1e9 && v < 1e9)) return v > 0 ? int.MaxValue >> Shift : int.MinValue >> Shift; // NaN i nieskończoności
        return MathUtil.FloorToInt(v) >> Shift;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CellY(double v) => Math.Clamp(CellCoord(v), -64, 64);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long Key(int cx, int cy, int cz) => ((long)(cx & 0x3FFFFFF) << 38) | ((long)(cz & 0x3FFFFFF) << 12) | (long)(cy & 0xFFF);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long KeyOf(Entity e) => Key(CellCoord(e.X), CellY(e.Y), CellCoord(e.Z));

    private static readonly Dictionary<Type, bool> _customBox = new();
    private static bool HasCustomBox(Entity e)
    {
        var t = e.GetType();
        lock (_customBox)
        {
            if (!_customBox.TryGetValue(t, out bool v))
            {
                var p = t.GetProperty(nameof(Entity.BoundingBox));
                v = p != null && p.GetMethod?.DeclaringType != typeof(Entity);
                _customBox[t] = v;
            }
            return v;
        }
    }

    private static bool IsBig(Entity e)
    {
        if (e.GridCustomBox) return true;
        var bb = e.BoundingBox;
        return bb.MinX < e.X - MarginH || bb.MaxX > e.X + MarginH || bb.MinZ < e.Z - MarginH || bb.MaxZ > e.Z + MarginH
            || bb.MinY < e.Y - MarginDown || bb.MaxY > e.Y + MarginUp || double.IsNaN(bb.MinX);
    }

    public void Add(Entity e)
    {
        if (e.GridOwner == this) Remove(e);
        e.GridSeq = ++_seq;
        e.GridCustomBox = HasCustomBox(e);
        e.GridPure = IsPureType(e);
        e.GridOwner = this;
        Place(e);
    }

    private void Place(Entity e)
    {
        if (IsBig(e)) { e.GridBig = true; e.GridIndex = _big.Count; _big.Add(e); return; }
        e.GridBig = false;
        long k = KeyOf(e);
        if (!_cells.TryGetValue(k, out var c)) { c = _free.Count > 0 ? _free.Pop() : new Cell(); _cells[k] = c; }
        if (c.Count == c.Items.Length) c.Grow();
        int i = c.Count++;
        e.GridKey = k; e.GridIndex = i; e.GridCell = c;
        c.Items[i] = e;
        if (!e.GridPure) c.Impure++;
        c.P[i * 3] = e.X; c.P[i * 3 + 1] = e.Y; c.P[i * 3 + 2] = e.Z;
        c.S[i * 2] = e.Width / 2; c.S[i * 2 + 1] = e.Height;
    }

    private void Unplace(Entity e)
    {
        if (e.GridBig)
        {
            int i = e.GridIndex, last = _big.Count - 1;
            if (i < 0 || i > last || _big[i] != e) { i = _big.IndexOf(e); if (i < 0) return; }
            var m = _big[last]; _big[i] = m; m.GridIndex = i; _big.RemoveAt(last);
            return;
        }
        var c = e.GridCell;
        if (c == null) return;
        int idx = e.GridIndex;
        if (idx < 0 || idx >= c.Count || c.Items[idx] != e) return;
        int l = --c.Count;
        if (!e.GridPure) c.Impure--;
        if (idx != l)
        {
            var mv = c.Items[l]; c.Items[idx] = mv; mv.GridIndex = idx;
            c.P[idx * 3] = c.P[l * 3]; c.P[idx * 3 + 1] = c.P[l * 3 + 1]; c.P[idx * 3 + 2] = c.P[l * 3 + 2];
            c.S[idx * 2] = c.S[l * 2]; c.S[idx * 2 + 1] = c.S[l * 2 + 1];
        }
        c.Items[l] = null;
        e.GridCell = null;
        if (c.Count == 0) { _cells.Remove(e.GridKey); _free.Push(c); }
    }

    public void Remove(Entity e)
    {
        if (e.GridOwner != this) return;
        Unplace(e);
        e.GridOwner = null;
    }

    /// <summary>Wołane przy zmianie pozycji encji (setter X/Y/Z).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Moved(Entity e)
    {
        if (e.GridBig) return;
        if (KeyOf(e) != e.GridKey) { Unplace(e); Place(e); return; }
        var c = e.GridCell;
        int i = e.GridIndex * 3;
        c.P[i] = e.X; c.P[i + 1] = e.Y; c.P[i + 2] = e.Z;
    }

    /// <summary>Po ticku encji: zmiana rozmiaru może przenieść ją między siatką a listą dużych.</summary>
    public void Revalidate(Entity e)
    {
        if (e.GridOwner != this) return;
        bool big = IsBig(e);
        if (big != e.GridBig || (!big && KeyOf(e) != e.GridKey)) { Unplace(e); Place(e); return; }
        if (!big)
        {
            var c = e.GridCell;
            int i = e.GridIndex;
            c.S[i * 2] = e.Width / 2; c.S[i * 2 + 1] = e.Height;
            c.P[i * 3] = e.X; c.P[i * 3 + 1] = e.Y; c.P[i * 3 + 2] = e.Z;
        }
    }

    private static readonly IComparer<Entity> BySeq = Comparer<Entity>.Create((a, b) => a.GridSeq.CompareTo(b.GridSeq));
    public static void SortBySeq(List<Entity> list) { if (list.Count > 1) list.Sort(BySeq); }

    /// <summary>Encje (nieusunięte, różne od except), których ramka przecina box; domyślnie w kolejności World.Entities.</summary>
    public void Query(AABB box, Entity except, List<Entity> res, bool sorted = true)
        => QueryCore(box, except, res, sorted, false, 0, 0, 0, 0);

    /// <summary>Jak Query (bez sortowania), ale od razu odrzuca encje, których stopy są wyraźnie dalej niż maxDist od (x,y,z).
    /// Odrzucone na pewno nie przejdą testu odległości eksplozji, więc wynik eksplozji się nie zmienia.</summary>
    public void QueryNear(AABB box, double x, double y, double z, double maxDist, List<Entity> res)
        => QueryCore(box, null, res, false, true, x, y, z, maxDist * maxDist * 1.000001 + 1e-9);

    private void QueryCore(AABB box, Entity except, List<Entity> res, bool sorted, bool near, double x, double y, double z, double maxSq)
    {
        int x0 = CellCoord(box.MinX - MarginH), x1 = CellCoord(box.MaxX + MarginH);
        int z0 = CellCoord(box.MinZ - MarginH), z1 = CellCoord(box.MaxZ + MarginH);
        int y0 = CellY(box.MinY - MarginUp), y1 = CellY(box.MaxY + MarginDown);
        int start = res.Count;
        long span = (long)(x1 - x0 + 1) * (z1 - z0 + 1) * (y1 - y0 + 1);
        if (span > _cells.Count * 2L)
        {
            // bardzo duży obszar: taniej przejść po zajętych komórkach
            foreach (var kv in _cells)
            {
                long k = kv.Key;
                int cy = (int)(k << 52 >> 52), cz = (int)(k << 26 >> 38), cx = (int)(k >> 38);
                if (cx < x0 || cx > x1 || cz < z0 || cz > z1 || cy < y0 || cy > y1) continue;
                Collect(kv.Value, box, except, res, near, x, y, z, maxSq);
            }
        }
        else
        {
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                    for (int cy = y0; cy <= y1; cy++)
                        if (_cells.TryGetValue(Key(cx, cy, cz), out var c)) Collect(c, box, except, res, near, x, y, z, maxSq);
        }
        foreach (var e in _big)
            if (e != except && !e.Removed && e.BoundingBox.Intersects(box)
                && (!near || e.DistanceSq(x, y, z) <= maxSq)) res.Add(e);
        if (sorted && res.Count - start > 1) res.Sort(start, res.Count - start, BySeq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Collect(Cell c, AABB box, Entity except, List<Entity> res, bool near, double x, double y, double z, double maxSq)
    {
        var items = c.Items; var p = c.P; var s = c.S;
        for (int i = 0, n = c.Count; i < n; i++)
        {
            double ex = p[i * 3], ey = p[i * 3 + 1], ez = p[i * 3 + 2];
            float hw = s[i * 2], h = s[i * 2 + 1];
            // ta sama arytmetyka co Entity.BoundingBox i AABB.Intersects
            if (!(box.MaxX > ex - hw && box.MinX < ex + hw && box.MaxY > ey && box.MinY < ey + h && box.MaxZ > ez - hw && box.MinZ < ez + hw)) continue;
            if (near)
            {
                double dx = ex - x, dy = ey - y, dz = ez - z;
                if (dx * dx + dy * dy + dz * dz > maxSq) continue;
            }
            var e = items[i];
            if (e == except || e.Removed) continue;
            res.Add(e);
        }
    }

    public void Clear() { _cells.Clear(); _big.Clear(); }

    /// <summary>Komórki mogące zawierać encje przecinające box, o ile w zasięgu są wyłącznie encje "czyste"
    /// (i żadna duża encja nie przecina boxa). False: trzeba użyć zwykłej ścieżki z sortowaniem.</summary>
    internal bool TryPureCells(AABB box, List<Cell> cells, out int total)
    {
        cells.Clear(); total = 0;
        int x0 = CellCoord(box.MinX - MarginH), x1 = CellCoord(box.MaxX + MarginH);
        int z0 = CellCoord(box.MinZ - MarginH), z1 = CellCoord(box.MaxZ + MarginH);
        int y0 = CellY(box.MinY - MarginUp), y1 = CellY(box.MaxY + MarginDown);
        if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) * (y1 - y0 + 1) > 512) return false;
        foreach (var e in _big) if (!e.Removed && e.BoundingBox.Intersects(box)) return false;
        for (int cx = x0; cx <= x1; cx++)
            for (int cz = z0; cz <= z1; cz++)
                for (int cy = y0; cy <= y1; cy++)
                    if (_cells.TryGetValue(Key(cx, cy, cz), out var c))
                    {
                        if (c.Impure > 0) return false;
                        cells.Add(c); total += c.Count;
                    }
        return true;
    }
}
