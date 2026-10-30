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

            new DoorBlock(B.IronDoor, "iron_door", "Żelazne drzwi", "door_iron", Items.I.IronDoorItem, true).NotCreative();
            new TrapdoorBlock(B.Trapdoor, "trapdoor", "Klapa", false).Tab(TabRedstone);
            new TrapdoorBlock(B.IronTrapdoor, "iron_trapdoor", "Żelazna klapa", true).Tab(TabRedstone);
            new PressurePlateBlock(B.StonePressurePlate, "stone_pressure_plate", "Kamienna płyta naciskowa", "stone", PressurePlateBlock.Sense.Mobs).Tab(TabRedstone);
            new PressurePlateBlock(B.WoodPressurePlate, "wooden_pressure_plate", "Drewniana płyta naciskowa", "planks_oak", PressurePlateBlock.Sense.Everything).Tab(TabRedstone);
            new PressurePlateBlock(B.LightWeightedPlate, "light_weighted_pressure_plate", "Płyta naciskowa (lekka)", "gold_block", PressurePlateBlock.Sense.Light).Tab(TabRedstone);
            new PressurePlateBlock(B.HeavyWeightedPlate, "heavy_weighted_pressure_plate", "Płyta naciskowa (ciężka)", "iron_block", PressurePlateBlock.Sense.Heavy).Tab(TabRedstone);
            new TripwireHookBlock(B.TripwireHook, "tripwire_hook", "Hak na linkę").Tab(TabRedstone);
            new TripwireBlock(B.Tripwire, "tripwire", "Linka");
            new DaylightSensorBlock(B.DaylightSensor, "daylight_detector", "Czujnik światła dziennego", false).Tab(TabRedstone);
            new DaylightSensorBlock(B.DaylightSensorInverted, "daylight_detector_inverted", "Czujnik światła dziennego", true).NotCreative();
            new ComparatorBlock(B.Comparator, "unpowered_comparator", "Komparator", false).NotCreative();
            new ComparatorBlock(B.ComparatorOn, "powered_comparator", "Komparator", true).NotCreative();
            new RedstoneOreBlock(B.RedstoneOre, "redstone_ore", "Ruda czerwonego kamienia", false);
            new RedstoneOreBlock(B.RedstoneOreLit, "lit_redstone_ore", "Ruda czerwonego kamienia", true);
            new HopperBlock(B.Hopper, "hopper", "Lej").Tab(TabRedstone);
            new DispenserBlock(B.Dropper, "dropper", "Podajnik", true).Tab(TabRedstone);
            new DispenserBlock(B.Dispenser, "dispenser", "Dozownik", false).Tab(TabRedstone);
            new NoteBlockBlock(B.NoteBlock, "noteblock", "Blok nut").Tab(TabRedstone);
            new TrappedChestBlock(B.TrappedChest, "trapped_chest", "Skrzynia-pułapka").H(2.5f).Tool_(ToolType.Axe).Snd(SoundType.Wood).Tab(TabRedstone);
        }
    }
}

namespace BlockCraft.Items
{
    public static partial class ItemRegistry
    {
        static partial void ItemsRedstone()
        {
            const int Red = 2;
            var door = Simple(I.IronDoorItem, "iron_door", "Żelazne drzwi", Red, 64); door.BlockId = B.IronDoor;
            var cmp = Simple(I.ComparatorItem, "comparator", "Komparator", Red); cmp.BlockId = B.Comparator;
            // nić stawia linkę (MC ItemReed(Blocks.tripwire))
            Item.ById[I.String].BlockId = B.Tripwire;
        }
    }
}

namespace BlockCraft.Entities
{
    public static partial class Registries
    {
        static partial void EntitiesRedstone()
        {
            Tile(10, () => new ComparatorTileEntity());
            Tile(11, () => new PistonTileEntity());
            Tile(12, () => new DaylightSensorTileEntity());
            Tile(13, () => new HopperTileEntity());
            Tile(14, () => new DispenserTileEntity(false));
            Tile(15, () => new DispenserTileEntity(true));
            Tile(16, () => new NoteTileEntity());
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
