using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>BlockEnchantmentTable z 1.8: blok 12/16 wysokości, twardość 5, odporność 2000, otwiera ContainerEnchantment.</summary>
public sealed class EnchantingTableBlock : Block
{
    public EnchantingTableBlock() : base(B.EnchantingTable, "enchanting_table", "Stół do zaklinania")
    {
        T("me_enchant_top", "obsidian", "me_enchant_side"); H(5, 2000); Tool_(ToolType.Pickaxe, 0); Tab(Blocks.TabDecoration);
        Opaque = FullCube = false; LightOpacity = 0; Render = RenderType.Model;
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes) => boxes.Add(ModelBox.Px(0, 0, 0, 16, 12, 16, Tex));
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) => boxes.Add(new AABB(0, 0, 0, 1, .75, 1));
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) => boxes.Add(new AABB(0, 0, 0, 1, .75, 1));
    public override bool OnActivated(World w, int x, int y, int z, Player p) { p.OpenBlockGui(GuiKind.Enchanting, x, y, z); return true; }
}
