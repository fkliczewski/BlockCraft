using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;

// Punkty zaczepienia modułów rozbudowy do MC 1.8. Każdy moduł implementuje swoje metody częściowe (static partial void ...)
// we własnych plikach, więc nie edytuje wspólnych list. Kolejność wywołań: tekstury -> bloki -> itemy -> receptury -> encje.
// Moduły: Overworld (OW), Redstone (RS), NetherEnd (NE), Mobs (MO), Mechanics (ME).

namespace BlockCraft.Render
{
    public static partial class TextureGen
    {
        static partial void TexOverworld();
        static partial void TexRedstone();
        static partial void TexNetherEnd();
        static partial void TexMobs();
        static partial void TexMechanics();
        private static void ModuleTextures() { TexOverworld(); TexRedstone(); TexNetherEnd(); TexMobs(); TexMechanics(); }
    }
}

namespace BlockCraft.World
{
    public static partial class Blocks
    {
        static partial void BlocksOverworld();
        static partial void BlocksRedstone();
        static partial void BlocksNetherEnd();
        static partial void BlocksMobs();
        static partial void BlocksMechanics();
        private static void ModuleBlocks() { BlocksOverworld(); BlocksRedstone(); BlocksNetherEnd(); BlocksMobs(); BlocksMechanics(); }
    }
}

namespace BlockCraft.Items
{
    public static partial class ItemRegistry
    {
        static partial void ItemsOverworld();
        static partial void ItemsRedstone();
        static partial void ItemsNetherEnd();
        static partial void ItemsMobs();
        static partial void ItemsMechanics();
        private static void ModuleItems() { ItemsOverworld(); ItemsRedstone(); ItemsNetherEnd(); ItemsMobs(); ItemsMechanics(); }
    }

    public static partial class Recipes
    {
        /// <summary>Receptury rzemiosła i przetapiania modułów (Smelting.Table jest już zainicjowana).</summary>
        static partial void RecipesOverworld();
        static partial void RecipesRedstone();
        static partial void RecipesNetherEnd();
        static partial void RecipesMobs();
        static partial void RecipesMechanics();
        private static void ModuleRecipes() { RecipesOverworld(); RecipesRedstone(); RecipesNetherEnd(); RecipesMobs(); RecipesMechanics(); }
    }
}

namespace BlockCraft.Entities
{
    /// <summary>Rejestracja encji (EntitySerializer.Types), typów tile entity (TileEntity.Types) i rendererów (EntityRenderer.Custom*).</summary>
    public static partial class Registries
    {
        private static bool _init;
        static partial void EntitiesOverworld();
        static partial void EntitiesRedstone();
        static partial void EntitiesNetherEnd();
        static partial void EntitiesMobs();
        static partial void EntitiesMechanics();

        public static void Init()
        {
            if (_init) return; _init = true;
            EntitiesOverworld(); EntitiesRedstone(); EntitiesNetherEnd(); EntitiesMobs(); EntitiesMechanics();
        }

        /// <summary>Klucze encji, które są mobami (do /summon, jajek spawnu i spawnera).</summary>
        public static readonly List<string> MobKeys = new() { "zombie", "skeleton", "creeper", "spider", "enderman", "cow", "pig", "sheep", "chicken" };

        public static void Entity(string key, Func<World.World, Entity> factory) => EntitySerializer.Types[key] = factory;
        public static void Mob(string key, Func<World.World, Entity> factory) { EntitySerializer.Types[key] = factory; if (!MobKeys.Contains(key)) MobKeys.Add(key); }
        public static void Tile(int typeId, Func<TileEntity> factory) => TileEntity.Types[typeId] = factory;
    }
}
