using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items;

public sealed class BrewingStandTile : TileEntity, IInventory
{
    public override int TypeId => 60;
    public readonly ItemStack[] Items = new ItemStack[4];
    public int BrewTime;
    private int _ingredient;
    public int Size => 4;
    public ItemStack Get(int i) => Items[i];
    public void Set(int i, ItemStack stack) => Items[i] = stack is { Count: > 0 } ? stack : null;
    public void Changed() { }
    private bool CanBrew() => Potions.IsIngredient(Items[3]) && Items.Take(3).Any(s => s?.Id == I.Potion && Potions.Brew(s.Damage, Items[3]) != s.Damage);
    public override void Tick(World.World w)
    {
        if (w.IsRemote) return;
        bool can = CanBrew();
        if (BrewTime > 0)
        {
            if (!can || Items[3].Id != _ingredient) BrewTime = 0;
            else if (--BrewTime == 0)
            {
                foreach (var s in Items.Take(3)) if (s?.Id == I.Potion) s.Damage = Potions.Brew(s.Damage, Items[3]);
                if (--Items[3].Count <= 0) Items[3] = null;
            }
        }
        else if (can) { BrewTime = 400; _ingredient = Items[3].Id; }
        int meta = 0; for (int i = 0; i < 3; i++) if (Items[i] != null) meta |= 1 << i;
        if (w.GetMeta(X, Y, Z) != meta) w.SetMeta(X, Y, Z, meta, false);
        var chunk = w.GetChunk(X >> 4, Z >> 4); if (chunk != null) chunk.Modified = true;
    }
    public override void Save(BinaryWriter writer) { foreach (var s in Items) writer.WriteStack(s); writer.Write(BrewTime); writer.Write(_ingredient); }
    public override void Load(BinaryReader reader) { for (int i = 0; i < 4; i++) Items[i] = reader.ReadStack(); BrewTime = reader.ReadInt32(); _ingredient = reader.ReadInt32(); }
}

public sealed class FilterSlot : Slot
{
    private readonly Func<ItemStack, bool> _filter;
    private readonly int _limit;
    public FilterSlot(IInventory inv, int index, int x, int y, Func<ItemStack, bool> filter, int limit = 64) : base(inv, index, x, y) { _filter = filter; _limit = limit; }
    public override bool CanPlace(ItemStack s) => s != null && _filter(s);
    public override int MaxStack(ItemStack s) => Math.Min(_limit, base.MaxStack(s));
}

public sealed class BrewingContainer : Container
{
    public readonly BrewingStandTile Stand;
    public BrewingContainer(PlayerInventory inv, BrewingStandTile stand)
    {
        Stand = stand; Title = "Statyw alchemiczny";
        Slots.Add(new FilterSlot(stand, 0, 56, 51, Bottle, 1)); Slots.Add(new FilterSlot(stand, 1, 79, 58, Bottle, 1)); Slots.Add(new FilterSlot(stand, 2, 102, 51, Bottle, 1));
        Slots.Add(new FilterSlot(stand, 3, 79, 17, Potions.IsIngredient)); AddPlayerSlots(inv, 84, 142);
    }
    private static bool Bottle(ItemStack s) => s.Id is I.Potion or I.GlassBottle;
    protected override bool TransferFromPlayer(ItemStack s) => Potions.IsIngredient(s) ? MergeStack(s, 3, 4, false) : Bottle(s) && MergeStack(s, 0, 3, false);
}
