using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft.Items
{
    /// <summary>Książka z piórem i napisana książka 1.8 (ItemWritableBook, ItemEditableBook, RecipeBookCloning, GuiScreenBook).</summary>
    public static class Books
    {
        public const int MaxPages = 50, MaxPageChars = 256, MaxTitle = 16, MaxGeneration = 2;
        public static readonly string[] Generations = { "Oryginał", "Kopia oryginału", "Kopia kopii", "Zniszczona" };

        public static List<string> Pages(ItemStack s) => s?.Tag?.GetList("pages")?.Select(o => o as string ?? "").ToList() ?? new();
        public static string Title(ItemStack s) => s?.Tag?.GetString("title");
        public static string Author(ItemStack s) => s?.Tag?.GetString("author");
        public static int Generation(ItemStack s) => Math.Clamp(s?.Tag?.GetInt("generation") ?? 0, 0, 3);

        /// <summary>ChatAllowedCharacters: bez kodów formatowania i znaków sterujących (poza nową linią w stronie).</summary>
        public static string FilterPage(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var t = new string(s.Where(c => c == '\n' || c >= ' ' && c != '§' && c != 127).ToArray());
            return t.Length > MaxPageChars ? t[..MaxPageChars] : t;
        }
        public static string FilterTitle(string s)
        {
            var t = new string((s ?? "").Where(c => c >= ' ' && c != '§' && c != 127).ToArray()).Trim();
            return t.Length > MaxTitle ? t[..MaxTitle] : t;
        }

        /// <summary>ItemEditableBook.validBookTagContents: tytuł do 32 znaków i autor.</summary>
        public static bool ValidWritten(ItemStack s) => s?.Tag != null && Title(s) is { Length: <= 32 } && Author(s) != null && s.Tag.GetList("pages") != null;

        /// <summary>Host: zapis stron (MC|BEdit) albo podpis (MC|BSign) trzymanej książki z piórem.</summary>
        public static bool Apply(Player p, IList<string> pages, bool sign, string title)
        {
            var held = p.Inventory.Selected;
            if (held?.Id != I.WritableBook || pages == null) return false;
            var list = pages.Take(MaxPages).Select(FilterPage).ToList();
            // GuiScreenBook.sendBookToServer: puste strony na końcu są odcinane (zostaje co najmniej jedna)
            while (list.Count > 1 && list[^1].Length == 0) list.RemoveAt(list.Count - 1);
            if (list.Count == 0) list.Add("");
            if (sign)
            {
                string t = FilterTitle(title);
                if (t.Length == 0) return false;
                var book = new ItemStack(I.WrittenBook, 1, 0) { Tag = held.Tag?.Copy() ?? new Nbt() };
                book.Tag["author"] = p.Name ?? "";
                book.Tag["title"] = t;
                book.Tag["pages"] = list.Cast<object>().ToList();
                book.Tag["resolved"] = 1;
                p.Inventory.Selected = book;
            }
            else held.GetOrCreateTag()["pages"] = list.Cast<object>().ToList();
            p.Inventory.Dirty = true;
            return true;
        }

        /// <summary>RecipeBookCloning: napisana książka (generacja 0-1) + 1-8 książek z piórem = kopie o generacji +1.</summary>
        public static ItemStack CloneRecipe(ItemStack[] grid, int gw, int gh)
        {
            ItemStack original = null; int blanks = 0;
            foreach (var s in grid)
            {
                if (s == null) continue;
                if (s.Id == I.WrittenBook) { if (original != null) return null; original = s; }
                else if (s.Id == I.WritableBook) blanks++;
                else return null;
            }
            if (original == null || blanks < 1 || Generation(original) >= MaxGeneration || original.Tag == null) return null;
            var copy = new ItemStack(I.WrittenBook, blanks, 0) { Tag = original.Tag.Copy() };
            copy.Tag["generation"] = Generation(original) + 1;
            return copy;
        }

        /// <summary>getRemainingItems: oryginał zostaje w siatce.</summary>
        public static int KeepSlot(ItemStack[] grid, ItemStack result)
        {
            if (result?.Id != I.WrittenBook || CloneRecipe(grid, 3, 3) == null) return -1;
            for (int i = 0; i < grid.Length; i++) if (grid[i]?.Id == I.WrittenBook) return i;
            return -1;
        }

        public static string Name(ItemStack s) => Title(s) is { Length: > 0 } t ? t : "Napisana książka";
        public static string Lore(ItemStack s)
        {
            if (s?.Tag == null) return null;
            string a = Author(s);
            string lore = a != null ? "autor: " + a : "";
            return lore + (lore.Length > 0 ? "\n§7" : "") + Generations[Generation(s)];
        }

        /// <summary>Otwiera ekran książki u lokalnego gracza (na hoście dla gościa nic nie robi: ekran otwiera jego klient).</summary>
        public static bool Open(Player p, ItemStack s, bool editable)
        {
            if (p.Handler is Game g) g.SetScreen(new Gui.BookScreen(p, s, editable));
            return true;
        }
    }

    public static partial class ItemRegistry
    {
        static partial void ItemsBl2()
        {
            var pot = Simple(I.FlowerPotItem, "flower_pot", "Doniczka", Blocks.TabDecoration);
            pot.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => FlowerPots.Place(p, s, x, y, z, face);
            var writable = Simple(I.WritableBook, "writable_book", "Książka z piórem", Blocks.TabMisc, 1);
            writable.UseInAir = (p, s) => Books.Open(p, s, true);
            // ItemEditableBook 1.8: stos 16 (kopiowanie daje do 8 sztuk naraz)
            var written = Simple(I.WrittenBook, "written_book", "Napisana książka", Blocks.TabMisc, 16);
            written.InCreative = false; written.Glint = true; written.NameFn = Books.Name; written.LoreFn = Books.Lore;
            written.UseInAir = (p, s) => Books.Open(p, s, false);
            if (Item.ById[B.Barrier] is { } barrier) { barrier.Sprite = TextureGen.Layer("item_barrier"); barrier.InCreative = false; }
        }
    }

    public static partial class Recipes
    {
        static partial void RecipesBl2()
        {
            Shaped(new ItemStack(B.EnderChest), "###", "#E#", "###", '#', (int)B.Obsidian, 'E', I.EyeOfEnder);
            Shaped(new ItemStack(I.FlowerPotItem), "# #", " # ", '#', I.Brick);
            All.Add(new Recipe { Shapeless = true, Ingredients = new() { (I.Book, -1), (I.Dye, 0), (I.Feather, -1) }, Result = new ItemStack(I.WritableBook) });
            Special.Add(Books.CloneRecipe);
            var previous = KeepSlotHook;
            KeepSlotHook = (grid, result) => { int k = Books.KeepSlot(grid, result); return k >= 0 ? k : previous?.Invoke(grid, result) ?? -1; };
            OnCraftedHook += Bl2Stats.Crafted;
        }
    }
}

namespace BlockCraft.Entities
{
    public static partial class Registries
    {
        static partial void EntitiesBl2()
        {
            Tile(Bl2.EnderChestTileId, () => new EnderChestTile());
            Tile(Bl2.FlowerPotTileId, () => new FlowerPotTile());
            Gui.ContainerFactory.Containers[Bl2.EnderChestGui] = EnderChestTile.CreateContainer;
            WorldRenderer.TileRenderHook += Bl2Render.DrawTiles;
        }
    }
}
