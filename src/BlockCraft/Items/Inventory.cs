using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items;

public sealed class PlayerInventory : IInventory
{
    public readonly ItemStack[] Main = new ItemStack[36];
    public readonly ItemStack[] Armor = new ItemStack[4]; // 0 hełm .. 3 buty
    public int SelectedSlot;
    public bool Dirty = true;
    public int Size => 40;
    public ItemStack Get(int i) => i < 36 ? Main[i] : Armor[i - 36];
    public void Set(int i, ItemStack s)
    {
        if (s != null && s.Count <= 0) s = null;
        if (i < 36) Main[i] = s; else Armor[i - 36] = s;
        Dirty = true;
    }
    public void Changed() { Dirty = true; }

    public ItemStack Selected
    {
        get => Main[SelectedSlot];
        set { Main[SelectedSlot] = value != null && value.Count <= 0 ? null : value; Dirty = true; }
    }

    /// <summary>Dodaje przedmiot; zmniejsza s.Count o dodaną ilość. Zwraca true gdy wszystko dodano.</summary>
    public bool Add(ItemStack s)
    {
        if (s == null || s.Count <= 0) return true;
        // dokładanie do istniejących stosów
        for (int pass = 0; pass < 2 && s.Count > 0; pass++)
            for (int i = 0; i < 36 && s.Count > 0; i++)
            {
                int idx = pass == 0 ? i : i; // hotbar najpierw (0-8 to hotbar)
                var m = Main[idx];
                if (pass == 0 && m != null && m.CanStackWith(s) && m.Count < m.MaxStack)
                {
                    int n = Math.Min(s.Count, m.MaxStack - m.Count);
                    m.Count += n; s.Count -= n; Dirty = true;
                }
                else if (pass == 1 && m == null)
                {
                    int n = Math.Min(s.Count, s.MaxStack);
                    Main[idx] = s.CopyWith(n); s.Count -= n; Dirty = true;
                }
            }
        return s.Count <= 0;
    }

    public int CountOf(int id) { int c = 0; foreach (var s in Main) if (s != null && s.Id == id) c += s.Count; return c; }

    public bool Consume(int id, int count = 1)
    {
        if (CountOf(id) < count) return false;
        for (int i = 0; i < 36 && count > 0; i++)
        {
            var s = Main[i];
            if (s == null || s.Id != id) continue;
            int n = Math.Min(count, s.Count);
            s.Count -= n; count -= n;
            if (s.Count <= 0) Main[i] = null;
        }
        Dirty = true;
        return true;
    }

    public int ArmorValue { get { int a = 0; foreach (var s in Armor) if (s?.Item != null) a += s.Item.ArmorPoints; return a; } }

    public void DamageArmor(float damage)
    {
        int d = Math.Max(1, (int)(damage / 4));
        for (int i = 0; i < 4; i++)
            if (Armor[i] != null && Armor[i].Item?.ArmorSlot >= 0)
                if (Armor[i].DamageItem(d) || Armor[i].Count <= 0) Armor[i] = null;
        Dirty = true;
    }

    public void Clear() { Array.Clear(Main); Array.Clear(Armor); Dirty = true; }

    public int FindSlot(int id, int meta = -1)
    {
        for (int i = 0; i < 36; i++) if (Main[i] != null && Main[i].Id == id && (meta < 0 || Main[i].Damage == meta)) return i;
        return -1;
    }
    public int FirstEmptyHotbar() { for (int i = 0; i < 9; i++) if (Main[i] == null) return i; return -1; }
}

/// <summary>Prosty inwentarz (np. siatka craftingu).</summary>
public sealed class SimpleInventory : IInventory
{
    public readonly ItemStack[] Slots;
    public Action OnChanged;
    public SimpleInventory(int n) { Slots = new ItemStack[n]; }
    public int Size => Slots.Length;
    public ItemStack Get(int i) => Slots[i];
    public void Set(int i, ItemStack s) { Slots[i] = s != null && s.Count <= 0 ? null : s; }
    public void Changed() => OnChanged?.Invoke();
}

public class Slot
{
    public readonly IInventory Inv;
    public readonly int Index;
    public int X, Y;
    public int ArmorType = -1;   // slot zbroi
    public bool IsOutput;
    public Slot(IInventory inv, int index, int x, int y) { Inv = inv; Index = index; X = x; Y = y; }
    public ItemStack Stack { get => Inv.Get(Index); set { Inv.Set(Index, value); Inv.Changed(); } }
    public virtual bool CanPlace(ItemStack s)
    {
        if (IsOutput) return false;
        if (ArmorType >= 0) return s?.Item != null && s.Item.ArmorSlot == ArmorType;
        return true;
    }
    public virtual int MaxStack(ItemStack s) => ArmorType >= 0 ? 1 : s?.MaxStack ?? 64;
    public virtual void OnTake(Player p, ItemStack taken) { }
    public ItemStack Take(int n)
    {
        var s = Stack; if (s == null) return null;
        n = Math.Min(n, s.Count);
        var res = s.CopyWith(n);
        s.Count -= n;
        Stack = s.Count <= 0 ? null : s;
        return res;
    }
}

public sealed class CraftResultSlot : Slot
{
    private readonly Container _c;
    public CraftResultSlot(Container c, IInventory result, int x, int y) : base(result, 0, x, y) { _c = c; IsOutput = true; }
    public override void OnTake(Player p, ItemStack taken)
    {
        // zużyj składniki
        var grid = _c.CraftGrid;
        for (int i = 0; i < grid.Size; i++)
        {
            var s = grid.Get(i);
            if (s == null) continue;
            int id = s.Id;
            s.Count--;
            if (s.Count <= 0) grid.Set(i, null);
            // wiadra zwracają puste wiadro
            if (id == I.MilkBucket || id == I.WaterBucket || id == I.LavaBucket) grid.Set(i, new ItemStack(I.Bucket));
        }
        p?.OnCrafted(taken);
        _c.UpdateCrafting();
    }
}

public sealed class FurnaceOutputSlot : Slot
{
    public FurnaceOutputSlot(IInventory inv, int x, int y) : base(inv, 2, x, y) { IsOutput = true; }
    public override void OnTake(Player p, ItemStack taken)
    {
        if (Inv is FurnaceTileEntity f && p != null && f.StoredXp > 0)
        {
            int xp = (int)f.StoredXp; if (p.World.Rand.NextFloat() < f.StoredXp - xp) xp++;
            f.StoredXp = 0;
            if (xp > 0) p.AddXp(xp);
        }
    }
}

public sealed class FurnaceFuelSlot : Slot
{
    public FurnaceFuelSlot(IInventory inv, int x, int y) : base(inv, 1, x, y) { }
    public override bool CanPlace(ItemStack s) => Smelting.BurnTime(s) > 0 || s?.Id == I.Bucket;
}

public enum ClickType { Pickup, QuickMove, Swap, Throw, PickupAll, Clone }

public abstract class Container
{
    public readonly List<Slot> Slots = new();
    public SimpleInventory CraftGrid;
    public SimpleInventory CraftResult;
    public int GridW, GridH;
    public int Width = 176, Height = 166;
    public string Title = "";
    protected PlayerInventory PInv;
    public int FirstPlayerSlot; // pierwszy slot ekwipunku gracza (27 głównych + 9 hotbar)

    protected void AddPlayerSlots(PlayerInventory inv, int y0, int hotbarY)
    {
        PInv = inv;
        FirstPlayerSlot = Slots.Count;
        for (int r = 0; r < 3; r++) for (int c = 0; c < 9; c++) Slots.Add(new Slot(inv, 9 + r * 9 + c, 8 + c * 18, y0 + r * 18));
        for (int c = 0; c < 9; c++) Slots.Add(new Slot(inv, c, 8 + c * 18, hotbarY));
    }

    public void UpdateCrafting()
    {
        if (CraftGrid == null) return;
        var res = Recipes.Find(CraftGrid.Slots, GridW, GridH);
        CraftResult.Set(0, res);
    }

    /// <summary>Kliknięcie w slot (logika jak MC). slot = -999 poza oknem.</summary>
    /// <summary>Sieć: klient przekazuje kliknięcia hostowi (lokalnie wykonuje je dalej jako przewidywanie).</summary>
    public static Action<Container, int, int, ClickType, Player> NetClickHook;
    public static Action<Container, List<int>, int, Player> NetDistributeHook;

    public void Click(int slotIdx, int button, ClickType type, Player p)
    {
        NetClickHook?.Invoke(this, slotIdx, button, type, p);
        if (type == ClickType.QuickMove && slotIdx >= 0)
        {
            var slot = Slots[slotIdx];
            if (slot.IsOutput && slot is CraftResultSlot)
            {
                // craftuj ile się da
                var first = slot.Stack;
                if (first == null) return;
                for (int guard = 0; guard < 64; guard++)
                {
                    var s = slot.Stack;
                    if (s == null || s.Id != first.Id) break;
                    var copy = s.Copy();
                    if (!MergeIntoPlayer(copy, true)) break;
                    MergeIntoPlayer(s.Copy(), false);
                    slot.Inv.Set(0, null);
                    slot.OnTake(p, copy);
                }
            }
            else TransferStack(p, slotIdx);
            return;
        }
        if (type == ClickType.Throw)
        {
            if (slotIdx < 0) return;
            var slot = Slots[slotIdx];
            var s = slot.Stack; if (s == null || p.Cursor != null) return;
            var taken = slot.Take(button == 1 ? s.Count : 1);
            p.DropStack(taken, false);
            slot.OnTake(p, taken);
            return;
        }
        if (type == ClickType.Swap)
        {
            if (slotIdx < 0) return;
            var slot = Slots[slotIdx];
            var hot = PInv.Main[button];
            var s = slot.Stack;
            if (slot.IsOutput)
            {
                if (s != null && hot == null) { PInv.Main[button] = s.Copy(); slot.Inv.Set(slot.Index, null); slot.OnTake(p, s); PInv.Dirty = true; }
                return;
            }
            if (hot != null && !slot.CanPlace(hot)) return;
            PInv.Main[button] = s; slot.Stack = hot; PInv.Dirty = true;
            OnSlotChanged(slot);
            return;
        }
        if (type == ClickType.PickupAll)
        {
            var cur = p.Cursor;
            if (cur == null) return;
            for (int pass = 0; pass < 2; pass++)
                foreach (var sl in Slots)
                {
                    if (cur.Count >= cur.MaxStack) break;
                    var s = sl.Stack;
                    if (s == null || !s.CanStackWith(cur) || sl.IsOutput) continue;
                    if (pass == 0 && s.Count == s.MaxStack) continue;
                    int n = Math.Min(s.Count, cur.MaxStack - cur.Count);
                    sl.Take(n); cur.Count += n;
                    OnSlotChanged(sl);
                }
            return;
        }
        if (type == ClickType.Clone)
        {
            if (slotIdx < 0 || !p.Creative || p.Cursor != null) return;
            var s = Slots[slotIdx].Stack;
            if (s != null) p.Cursor = s.CopyWith(s.MaxStack);
            return;
        }
        // Pickup
        if (slotIdx == -999)
        {
            if (p.Cursor != null)
            {
                if (button == 0) { p.DropStack(p.Cursor, true); p.Cursor = null; }
                else { p.DropStack(p.Cursor.CopyWith(1), true); p.Cursor.Count--; if (p.Cursor.Count <= 0) p.Cursor = null; }
            }
            return;
        }
        if (slotIdx < 0) return;
        {
            var slot = Slots[slotIdx];
            var s = slot.Stack;
            var cur = p.Cursor;
            if (s == null)
            {
                if (cur != null && slot.CanPlace(cur))
                {
                    int n = button == 0 ? cur.Count : 1;
                    n = Math.Min(n, slot.MaxStack(cur));
                    slot.Stack = cur.CopyWith(n);
                    cur.Count -= n;
                    if (cur.Count <= 0) p.Cursor = null;
                }
            }
            else if (cur == null)
            {
                int n = button == 0 ? s.Count : (s.Count + 1) / 2;
                var taken = slot.Take(n);
                p.Cursor = taken;
                slot.OnTake(p, taken);
            }
            else if (slot.CanPlace(cur))
            {
                if (s.CanStackWith(cur))
                {
                    int n = button == 0 ? cur.Count : 1;
                    n = Math.Min(n, Math.Min(slot.MaxStack(cur), s.MaxStack) - s.Count);
                    if (n > 0) { s.Count += n; cur.Count -= n; slot.Stack = s; if (cur.Count <= 0) p.Cursor = null; }
                }
                else if (cur.Count <= slot.MaxStack(cur))
                {
                    slot.Stack = cur; p.Cursor = s;
                }
            }
            else if (slot.IsOutput && s.CanStackWith(cur))
            {
                if (s.Count + cur.Count <= cur.MaxStack)
                {
                    cur.Count += s.Count;
                    var taken = s.Copy();
                    slot.Inv.Set(slot.Index, null);
                    slot.OnTake(p, taken);
                }
            }
            OnSlotChanged(slot);
        }
    }

    /// <summary>Rozkładanie przeciąganiem (lewy: równo, prawy: po 1).</summary>
    public void Distribute(List<int> slotIdxs, int button, Player p)
    {
        NetDistributeHook?.Invoke(this, slotIdxs, button, p);
        var cur = p.Cursor;
        if (cur == null || slotIdxs.Count == 0) return;
        var valid = slotIdxs.Where(i => { var sl = Slots[i]; var s = sl.Stack; return sl.CanPlace(cur) && (s == null || s.CanStackWith(cur)); }).ToList();
        if (valid.Count == 0) return;
        int per = button == 0 ? Math.Max(1, cur.Count / valid.Count) : 1;
        foreach (var i in valid)
        {
            if (cur.Count <= 0) break;
            var sl = Slots[i]; var s = sl.Stack;
            int have = s?.Count ?? 0;
            int n = Math.Min(per, Math.Min(cur.Count, Math.Min(sl.MaxStack(cur), cur.MaxStack) - have));
            if (n <= 0) continue;
            if (s == null) sl.Stack = cur.CopyWith(n); else { s.Count += n; sl.Stack = s; }
            cur.Count -= n;
            OnSlotChanged(sl);
        }
        if (cur.Count <= 0) p.Cursor = null;
    }

    protected virtual void OnSlotChanged(Slot s)
    {
        if (CraftGrid != null && s.Inv == CraftGrid) UpdateCrafting();
    }

    /// <summary>Scal stos do ekwipunku gracza (hotbar od końca jak MC). simulate: tylko sprawdzenie.</summary>
    protected bool MergeIntoPlayer(ItemStack s, bool simulate)
    {
        int need = s.Count;
        var tmpCounts = new int[36];
        for (int i = 0; i < 36; i++) tmpCounts[i] = PInv.Main[i]?.Count ?? 0;
        int[] order = Enumerable.Range(0, 9).Reverse().Concat(Enumerable.Range(9, 27)).ToArray();
        foreach (var i in order)
        {
            var m = PInv.Main[i];
            if (m != null && m.CanStackWith(s)) { int n = Math.Min(need, m.MaxStack - tmpCounts[i]); if (n > 0) { need -= n; if (!simulate) m.Count += n; } }
            if (need <= 0) break;
        }
        if (need > 0)
            foreach (var i in order)
                if (PInv.Main[i] == null) { if (!simulate) PInv.Main[i] = s.CopyWith(need); need = 0; break; }
        PInv.Dirty = true;
        return need <= 0;
    }

    /// <summary>Przenosi stos do zakresu slotów [from,to). Zwraca true jeśli cokolwiek przeniesiono.</summary>
    protected bool MergeStack(ItemStack s, int from, int to, bool reverse)
    {
        bool moved = false;
        if (s.MaxStack > 1)
        {
            for (int k = 0; k < to - from && s.Count > 0; k++)
            {
                int i = reverse ? to - 1 - k : from + k;
                var sl = Slots[i]; var t = sl.Stack;
                if (t != null && t.CanStackWith(s) && sl.CanPlace(s))
                {
                    int n = Math.Min(s.Count, Math.Min(t.MaxStack, sl.MaxStack(s)) - t.Count);
                    if (n > 0) { t.Count += n; s.Count -= n; sl.Stack = t; moved = true; }
                }
            }
        }
        for (int k = 0; k < to - from && s.Count > 0; k++)
        {
            int i = reverse ? to - 1 - k : from + k;
            var sl = Slots[i];
            if (sl.Stack == null && sl.CanPlace(s))
            {
                int n = Math.Min(s.Count, sl.MaxStack(s));
                sl.Stack = s.CopyWith(n); s.Count -= n; moved = true;
            }
        }
        return moved;
    }

    public virtual void TransferStack(Player p, int idx)
    {
        var slot = Slots[idx];
        var s = slot.Stack;
        if (s == null) return;
        int ps = FirstPlayerSlot, pe = FirstPlayerSlot + 36;
        var copy = s.Copy();
        if (idx >= ps)
        {
            // z gracza do kontenera, albo między główny/hotbar
            if (!TransferFromPlayer(copy))
            {
                if (idx < ps + 27) MergeStack(copy, ps + 27, pe, false);
                else MergeStack(copy, ps, ps + 27, false);
            }
        }
        else MergeStack(copy, ps, pe, true);
        if (copy.Count != s.Count)
        {
            int taken = s.Count - copy.Count;
            var t = slot.Take(taken);
            slot.OnTake(p, t);
            OnSlotChanged(slot);
        }
    }

    /// <summary>Próbuje przenieść z ekwipunku gracza do slotów kontenera.</summary>
    protected virtual bool TransferFromPlayer(ItemStack s) => false;

    public virtual void OnClose(Player p)
    {
        if (CraftGrid != null)
        {
            for (int i = 0; i < CraftGrid.Size; i++)
            {
                var s = CraftGrid.Get(i);
                if (s == null) continue;
                CraftGrid.Set(i, null);
                if (!p.Inventory.Add(s)) p.DropStack(s, false);
            }
            CraftResult.Set(0, null);
        }
        if (p.Cursor != null)
        {
            if (!p.Inventory.Add(p.Cursor)) p.DropStack(p.Cursor, false);
            p.Cursor = null;
        }
    }

    public virtual void Tick() { }
}

public sealed class PlayerContainer : Container
{
    public PlayerContainer(PlayerInventory inv)
    {
        Title = "Wytwarzanie";
        CraftGrid = new SimpleInventory(4); CraftResult = new SimpleInventory(1);
        GridW = 2; GridH = 2;
        Slots.Add(new CraftResultSlot(this, CraftResult, 154, 28));
        for (int r = 0; r < 2; r++) for (int c = 0; c < 2; c++) Slots.Add(new Slot(CraftGrid, r * 2 + c, 98 + c * 18, 18 + r * 18));
        for (int a = 0; a < 4; a++) Slots.Add(new Slot(inv, 36 + a, 8, 8 + a * 18) { ArmorType = a });
        AddPlayerSlots(inv, 84, 142);
    }
    protected override bool TransferFromPlayer(ItemStack s)
    {
        var it = s.Item;
        if (it != null && it.ArmorSlot >= 0)
        {
            var sl = Slots[5 + it.ArmorSlot];
            if (sl.Stack == null) { sl.Stack = s.CopyWith(1); s.Count--; return true; }
        }
        return false;
    }
}

public sealed class WorkbenchContainer : Container
{
    public readonly int Bx, By, Bz;
    public WorkbenchContainer(PlayerInventory inv, int x, int y, int z)
    {
        Bx = x; By = y; Bz = z;
        Title = "Wytwarzanie";
        CraftGrid = new SimpleInventory(9); CraftResult = new SimpleInventory(1);
        GridW = 3; GridH = 3;
        Slots.Add(new CraftResultSlot(this, CraftResult, 124, 35));
        for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) Slots.Add(new Slot(CraftGrid, r * 3 + c, 30 + c * 18, 17 + r * 18));
        AddPlayerSlots(inv, 84, 142);
    }
    protected override bool TransferFromPlayer(ItemStack s) => false;
}

public sealed class FurnaceContainer : Container
{
    public readonly FurnaceTileEntity Furnace;
    public FurnaceContainer(PlayerInventory inv, FurnaceTileEntity f)
    {
        Furnace = f; Title = "Piec";
        Slots.Add(new Slot(f, 0, 56, 17));
        Slots.Add(new FurnaceFuelSlot(f, 56, 53));
        Slots.Add(new FurnaceOutputSlot(f, 116, 35));
        AddPlayerSlots(inv, 84, 142);
    }
    protected override bool TransferFromPlayer(ItemStack s)
    {
        if (Smelting.Result(s) != null) return MergeStack(s, 0, 1, false);
        if (Smelting.BurnTime(s) > 0) return MergeStack(s, 1, 2, false);
        return false;
    }
}

public sealed class ChestContainer : Container
{
    public readonly IInventory Chest;
    public ChestContainer(PlayerInventory inv, IInventory chest, string title)
    {
        Chest = chest; Title = title;
        Height = 114 + 3 * 18;
        for (int r = 0; r < 3; r++) for (int c = 0; c < 9; c++) Slots.Add(new Slot(chest, r * 9 + c, 8 + c * 18, 18 + r * 18));
        AddPlayerSlots(inv, 85, 143);
    }
    protected override bool TransferFromPlayer(ItemStack s) => MergeStack(s, 0, 27, false);
}
