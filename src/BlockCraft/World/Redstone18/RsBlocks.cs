using BlockCraft.Entities;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.World
{
    public static partial class Blocks
    {
        static partial void BlocksRedstone()
        {
            // bloki "powiązane" dzielą zaplanowany tick (MC Block.isAssociatedBlock)
            World.TickGroup[B.RepeaterOn] = B.Repeater;
            World.TickGroup[B.RedstoneTorchOff] = B.RedstoneTorch;
            World.TickGroup[B.ComparatorOn] = B.Comparator;

            new PistonExtensionBlock(B.PistonExtension, "piston_extension", "Blok w ruchu");
            new ButtonBlock(B.WoodButton, "wooden_button", "Drewniany przycisk", true).Tab(TabRedstone);
            new SlimeBlockBlock(B.SlimeBlock, "slime", "Blok szlamu").Tab(TabRedstone);
        }
    }
}

namespace BlockCraft.Entities
{
    public static partial class Registries
    {
        static partial void EntitiesRedstone()
        {
            Tile(11, () => new PistonTileEntity());
            Render.EntityRenderer.Custom[typeof(PistonMovingVisual)] = Render.EntityRenderer.RenderMovingBlock;
        }
    }
}

namespace BlockCraft.Render
{
    public sealed partial class EntityRenderer
    {
        /// <summary>Rysuje blok w ruchu tłoka (MC TileEntityPistonRenderer).</summary>
        public static void RenderMovingBlock(EntityRenderer r, Entity e, float pt, Vector3 pos, Vector3 cam)
        {
            var te = ((PistonMovingVisual)e).Te;
            if (te.Finished) return;
            var (ox, oy, oz) = te.Offset(pt);
            var center = pos + new Vector3(0, 0.5f, 0);
            var moved = center + new Vector3(ox, oy, oz);
            if (te.HeadRendered && !te.Extending && (te.BlockId == B.Piston || te.BlockId == B.StickyPiston))
            {
                // cofanie: baza stoi (wysunięta), głowica wjeżdża
                int f = te.BlockMeta & 7;
                r.RenderBlockAt(te.BlockId, f | 8, Matrix4.CreateTranslation(center));
                r.RenderBlockAt(B.PistonHead, f | (te.BlockId == B.StickyPiston ? 8 : 0), Matrix4.CreateTranslation(moved));
                return;
            }
            r.RenderBlockAt(te.BlockId, te.BlockMeta, Matrix4.CreateTranslation(moved));
        }
    }
}
