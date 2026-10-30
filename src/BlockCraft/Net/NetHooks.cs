using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>
/// Punkty zaczepienia sieci dla innych modułów (bez zależności kompilacyjnej).
/// - Wymiary (NE): DimOf, WorldsOf i CreateRemoteWorld; gracz w innym World niż wcześniej dostaje S2C.ChangeDimension.
/// - Stan świata modułu (OW: pogoda itp.): WorldState["klucz"] = (zapis na hoście, odczyt na kliencie); wysyłane, gdy bajty się zmienią.
/// Encje, tile entity, bloki i kontenery synchronizują się same (klucz rejestru + Save/Load, TypeId + Save/Load, id+meta, sloty).
/// </summary>
public static class NetHooks
{
    /// <summary>Numer wymiaru świata (0 Overworld, -1 Nether, 1 End).</summary>
    public static Func<World.World, int> DimOf = _ => 0;

    /// <summary>Wszystkie światy hosta, w których mogą być gracze (domyślnie tylko świat gracza hosta).</summary>
    public static Func<World.World, IEnumerable<World.World>> WorldsOf = w => new[] { w };

    /// <summary>Świat zdalny na kliencie dla wymiaru.</summary>
    public static Func<int, World.World> CreateRemoteWorld = dim => new World.World(0, WorldType.Default, true);

    public static readonly Dictionary<string, (Func<World.World, byte[]> Write, Action<World.World, byte[]> Read)> WorldState = new();

    /// <summary>Host: przeniesienie zdalnego gracza do innego świata (NE wywołuje swoją logikę portalu; tu tylko powiadomienie sieci).</summary>
    public static Action<Player, World.World> OnPlayerChangedWorld;
}
