using BlockCraft.World;

namespace BlockCraft.Items;

public interface IInventory
{
    int Size { get; }
    ItemStack Get(int i);
    void Set(int i, ItemStack s);
    void Changed();
}

public sealed class FurnaceTileEntity : TileEntity, IInventory
{
    public static bool Swapping;
    public readonly ItemStack[] Slots = new ItemStack[3];
    public int BurnTime, CurrentItemBurnTime, CookTime;
    public float StoredXp;
    public const int CookTotal = 200;
    public override int TypeId => 1;
    public int Size => 3;
    public ItemStack Get(int i) => Slots[i];
    public void Set(int i, ItemStack s) { Slots[i] = s != null && s.Count <= 0 ? null : s; }
    public void Changed() { Dirty = true; }
    public bool Dirty;

    public bool IsBurning => BurnTime > 0;

    private bool CanSmelt()
    {
        var input = Slots[0];
        if (input == null) return false;
        var res = Smelting.Result(input);
        if (res == null) return false;
        var o = Slots[2];
        if (o == null) return true;
        if (!o.CanStackWith(res)) return false;
        return o.Count + res.Count <= o.MaxStack;
    }

    public override void Tick(World.World w)
    {
        bool was = BurnTime > 0;
        bool changed = false;
        if (BurnTime > 0) BurnTime--;
        if (w.IsRemote) return;
        if (BurnTime == 0 && CanSmelt())
        {
            CurrentItemBurnTime = BurnTime = Smelting.BurnTime(Slots[1]);
            if (BurnTime > 0)
            {
                changed = true;
                if (Slots[1].Id == I.LavaBucket) Slots[1] = new ItemStack(I.Bucket);
                else { Slots[1].Count--; if (Slots[1].Count <= 0) Slots[1] = null; }
            }
        }
        if (BurnTime > 0 && CanSmelt())
        {
            CookTime++;
            if (CookTime >= CookTotal)
            {
                CookTime = 0;
                var res = Smelting.Result(Slots[0]);
                StoredXp += Smelting.Xp(Slots[0]);
                if (Slots[2] == null) Slots[2] = res.Copy(); else Slots[2].Count += res.Count;
                Slots[0].Count--; if (Slots[0].Count <= 0) Slots[0] = null;
                changed = true;
            }
        }
        else if (CookTime > 0) CookTime = Math.Max(0, CookTime - 2);
        if (was != BurnTime > 0)
        {
            changed = true;
            int meta = w.GetMeta(X, Y, Z);
            Swapping = true;
            try { w.SetBlock(X, Y, Z, BurnTime > 0 ? B.FurnaceLit : B.Furnace, meta); }
            finally { Swapping = false; }
            w.SetTileEntity(X, Y, Z, this);
        }
        if (changed) { Dirty = true; var c = w.GetChunk(X >> 4, Z >> 4); if (c != null) c.Modified = true; }
    }

    public override void Save(BinaryWriter bw)
    {
        foreach (var s in Slots) bw.WriteStack(s);
        bw.Write(BurnTime); bw.Write(CurrentItemBurnTime); bw.Write(CookTime); bw.Write(StoredXp);
    }
    public override void Load(BinaryReader br)
    {
        for (int i = 0; i < 3; i++) Slots[i] = br.ReadStack();
        BurnTime = br.ReadInt32(); CurrentItemBurnTime = br.ReadInt32(); CookTime = br.ReadInt32(); StoredXp = br.ReadSingle();
    }
}

public sealed class ChestTileEntity : TileEntity, IInventory
{
    public readonly ItemStack[] Slots = new ItemStack[27];
    public override int TypeId => 2;
    public int Size => 27;
    public ItemStack Get(int i) => Slots[i];
    public void Set(int i, ItemStack s) { Slots[i] = s != null && s.Count <= 0 ? null : s; }
    public void Changed() { Dirty = true; }
    public bool Dirty;
    public int Viewers;

    public override void Save(BinaryWriter bw) { foreach (var s in Slots) bw.WriteStack(s); }
    public override void Load(BinaryReader br) { for (int i = 0; i < 27; i++) Slots[i] = br.ReadStack(); }
}
