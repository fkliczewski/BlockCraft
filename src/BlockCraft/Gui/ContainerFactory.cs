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
    };

    /// <summary>Ekrany dla typów kontenerów; brak wpisu = zwykły ContainerScreen.</summary>
    public static readonly Dictionary<Type, Func<Player, Container, Screen>> Screens = new();

    public static Container Create(World.World w, Player p, GuiKind kind, int x, int y, int z)
        => Containers.TryGetValue(kind, out var f) ? f(w, p, x, y, z) : null;

    public static Screen ScreenFor(Player p, Container c)
        => Screens.TryGetValue(c.GetType(), out var f) ? f(p, c) : new ContainerScreen(p, c);
}
