using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items;

/// <summary>Punkt rozszerzeń przedmiotów używanych na bloku (wywołuje PlayerController).</summary>
public interface IBlockUseItem
{
    bool UseOnBlock(World.World w, Player p, ItemStack held, int x, int y, int z, int face);
}

public sealed class MinecartItem : Item, IBlockUseItem
{
    public readonly MinecartKind Kind;
    public MinecartItem(int id, string key, string name, MinecartKind kind) : base(id, key, name)
    {
        Kind = kind; MaxStack = 1; CreativeTab = Blocks.TabTransport; Sprite = TextureGen.Layer("item_" + key);
        DispenserBehaviors.ById[id] = Dispense;
    }
    public bool UseOnBlock(World.World w, Player p, ItemStack held, int x, int y, int z, int face)
    {
        int id = w.GetBlock(x, y, z); if (!RailBlock.IsRail(id)) return false;
        if (w.IsRemote) return true;
        double rise = RailBlock.Shape(id, w.GetMeta(x, y, z)) is >= 2 and <= 5 ? 0.5 : 0;
        Spawn(w, held, x + 0.5, y + 0.0625 + rise, z + 0.5);
        if (!p.Creative && --held.Count <= 0) p.Inventory.Main[p.Inventory.SelectedSlot] = null;
        p.Inventory.Dirty = true;
        return true;
    }
    private void Spawn(World.World w, ItemStack held, double x, double y, double z)
    {
        var cart = MinecartEntity.Create(w, Kind); cart.CustomName = held.CustomName; cart.SetPositionAndPrev(x, y, z);
        w.SpawnEntity(cart);
    }
    private ItemStack Dispense(DispenseSource src, ItemStack stack)
    {
        var (x, y, z) = src.Front;
        int id = src.W.GetBlock(x, y, z); double offset;
        if (RailBlock.IsRail(id)) offset = RailBlock.Shape(id, src.W.GetMeta(x, y, z)) is >= 2 and <= 5 ? 0.6 : 0.1;
        else
        {
            int below = src.W.GetBlock(x, y - 1, z);
            if (id != B.Air || !RailBlock.IsRail(below)) return DispenserBehaviors.Default(src, stack);
            offset = src.Facing != Core.Dir.Down && RailBlock.Shape(below, src.W.GetMeta(x, y - 1, z)) is >= 2 and <= 5 ? -0.4 : -0.9;
        }
        Spawn(src.W, stack, src.CX + Core.Dir.X[src.Facing] * 1.125, src.Y + Core.Dir.Y[src.Facing] + offset, src.CZ + Core.Dir.Z[src.Facing] * 1.125);
        stack.Count--; DispenserBehaviors.Click(src); DispenserBehaviors.Smoke(src); return stack;
    }
}
