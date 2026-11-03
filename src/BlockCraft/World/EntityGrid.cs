using System.Runtime.CompilerServices;
using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

/// <summary>Siatka przestrzenna encji świata (komórki 8x8x8 po pozycji stóp). Zastępuje liniowe przejście po
/// wszystkich encjach w GetEntitiesInBox. Wynik zapytania jest sortowany po kolejności dodania do World.Entities,
/// więc jest identyczny z dawną pętlą po liście (mechanika i odrzut eksplozji bez zmian).
/// Encje z ramką wychodzącą poza zapas zapytania (np. obrazy, smok) trafiają na osobną listę "duże".</summary>
public sealed class EntityGrid
{
    private const int Shift = 3; // komórka 8 bloków
    // zapas zapytania względem pozycji encji: ramka encji mieści się w [X-H, X+H] x [Y-Down, Y+Up]
    public const double MarginH = 2.5, MarginUp = 4.5, MarginDown = 0.5;

    private sealed class Cell { public Entity[] Items = new Entity[8]; public int Count; }
    private readonly Dictionary<long, Cell> _cells = new();
    private readonly List<Entity> _big = new();
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

    private static bool IsBig(Entity e)
    {
        var bb = e.BoundingBox;
        return bb.MinX < e.X - MarginH || bb.MaxX > e.X + MarginH || bb.MinZ < e.Z - MarginH || bb.MaxZ > e.Z + MarginH
            || bb.MinY < e.Y - MarginDown || bb.MaxY > e.Y + MarginUp || double.IsNaN(bb.MinX);
    }

    public void Add(Entity e)
    {
        if (e.GridOwner == this) Remove(e);
        e.GridSeq = ++_seq;
        e.GridOwner = this;
        Place(e);
    }

    private void Place(Entity e)
    {
        if (IsBig(e)) { e.GridBig = true; e.GridIndex = _big.Count; _big.Add(e); return; }
        e.GridBig = false;
        long k = KeyOf(e);
        if (!_cells.TryGetValue(k, out var c)) { c = _free.Count > 0 ? _free.Pop() : new Cell(); _cells[k] = c; }
        if (c.Count == c.Items.Length) Array.Resize(ref c.Items, c.Items.Length * 2);
        e.GridKey = k; e.GridIndex = c.Count;
        c.Items[c.Count++] = e;
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
        if (!_cells.TryGetValue(e.GridKey, out var c)) return;
        int idx = e.GridIndex;
        if (idx < 0 || idx >= c.Count || c.Items[idx] != e) return;
        int l = --c.Count;
        var mv = c.Items[l]; c.Items[idx] = mv; mv.GridIndex = idx; c.Items[l] = null;
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
        if (KeyOf(e) != e.GridKey) { Unplace(e); Place(e); }
    }

    /// <summary>Po ticku encji: zmiana rozmiaru może przenieść ją między siatką a listą dużych.</summary>
    public void Revalidate(Entity e)
    {
        if (e.GridOwner != this) return;
        bool big = IsBig(e);
        if (big != e.GridBig || (!big && KeyOf(e) != e.GridKey)) { Unplace(e); Place(e); }
    }

    private static readonly IComparer<Entity> BySeq = Comparer<Entity>.Create((a, b) => a.GridSeq.CompareTo(b.GridSeq));

    /// <summary>Encje (nieusunięte, różne od except), których ramka przecina box; w kolejności World.Entities.</summary>
    public static void SortBySeq(List<Entity> list) { if (list.Count > 1) list.Sort(BySeq); }

    public void Query(AABB box, Entity except, List<Entity> res, bool sorted = true)
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
                Collect(kv.Value, box, except, res);
            }
        }
        else
        {
            for (int cx = x0; cx <= x1; cx++)
                for (int cz = z0; cz <= z1; cz++)
                    for (int cy = y0; cy <= y1; cy++)
                        if (_cells.TryGetValue(Key(cx, cy, cz), out var c)) Collect(c, box, except, res);
        }
        foreach (var e in _big)
            if (e != except && !e.Removed && e.BoundingBox.Intersects(box)) res.Add(e);
        if (sorted && res.Count - start > 1) res.Sort(start, res.Count - start, BySeq);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Collect(Cell c, AABB box, Entity except, List<Entity> res)
    {
        var items = c.Items;
        for (int i = 0, n = c.Count; i < n; i++)
        {
            var e = items[i];
            if (e == except || e.Removed) continue;
            if (e.BoundingBox.Intersects(box)) res.Add(e);
        }
    }

    public void Clear() { _cells.Clear(); _big.Clear(); }
}
