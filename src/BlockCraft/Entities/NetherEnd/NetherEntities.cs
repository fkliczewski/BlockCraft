using BlockCraft.World;

namespace BlockCraft.Entities;

public static partial class Registries
{
    static partial void EntitiesNetherEnd()
    {
        WorldHooks.OnEntityInPortal = DimensionPortals.ContactPortal;
    }
}
