using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Gui;

/// <summary>
/// Tworzenie kontenerów GUI bloków i ich ekranów. Używa go gra lokalna (Game.OpenBlockGui) i host sieciowy
/// dla zdalnych graczy, więc nowy rodzaj GUI zarejestrowany tutaj działa od razu także w sieci.
/// Moduły rejestrują własne rodzaje w swojej inicjalizacji: ContainerFactory.Containers[(GuiKind)20] = (w, p, x, y, z) => ...
/// </summary>
public static class ContainerFactory
{
    public static readonly Dictionary<GuiKind, Func<World.World, Player, int, int, int, Container>> Containers = new()
    {
        [GuiKind.Crafting] = (w, p, x, y, z) => new WorkbenchContainer(p.Inventory, x, y, z),
        [GuiKind.Furnace] = (w, p, x, y, z) => w.GetTileEntity(x, y, z) is FurnaceTileEntity f ? new FurnaceContainer(p.Inventory, f) : null,
        [GuiKind.Chest] = (w, p, x, y, z) => w.GetTileEntity(x, y, z) is ChestTileEntity c ? new ChestContainer(p.Inventory, c, "Skrzynia") : null,
        [GuiKind.Brewing] = (w, p, x, y, z) => w.GetTileEntity(x, y, z) is BrewingStandTile b ? new BrewingContainer(p.Inventory, b) : null,
        [GuiKind.Hopper] = (w, p, x, y, z) => w.GetTileEntity(x, y, z) is World.HopperTileEntity h ? new World.RsContainer(p.Inventory, h, "Lej", World.RsContainer.Layout.Hopper) : null,
        [GuiKind.Dispenser] = (w, p, x, y, z) => w.GetTileEntity(x, y, z) is World.DispenserTileEntity d ? new World.RsContainer(p.Inventory, d, w.GetBlock(x,y,z) == World.B.Dropper ? "Podajnik" : "Dozownik", World.RsContainer.Layout.Grid3) : null,
        [GuiKind.TrappedChest] = (w, p, x, y, z) => World.Block.Get(w.GetBlock(x,y,z)) is World.TrappedChestBlock t ? t.CreateContainer(w, p, x, y, z) : null,
    };

    /// <summary>Ekrany dla typów kontenerów; brak wpisu = zwykły ContainerScreen.</summary>
    public static readonly Dictionary<Type, Func<Player, Container, Screen>> Screens = new()
    {
        [typeof(BrewingContainer)] = (p, c) => new BrewingScreen(p, (BrewingContainer)c),
        [typeof(World.RsContainer)] = (p, c) => new RsContainerScreen(p, c),
        [typeof(HorseContainer)] = (p, c) => new HorseScreen(p, (HorseContainer)c),
        [typeof(MerchantContainer)] = (p, c) => new MerchantScreen(p, (MerchantContainer)c),
    };

    public static Container Create(World.World w, Player p, GuiKind kind, int x, int y, int z)
        => Containers.TryGetValue(kind, out var f) ? f(w, p, x, y, z) : null;

    public static Screen ScreenFor(Player p, Container c)
        => Screens.TryGetValue(c.GetType(), out var f) ? f(p, c) : new ContainerScreen(p, c);
}
