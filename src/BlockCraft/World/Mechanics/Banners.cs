using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using B = BlockCraft.World.B;

namespace BlockCraft.World
{
    /// <summary>EnumBannerPattern 1.8: plik tekstury, kod NBT, wzór w siatce 3x3 albo przedmiot (id, meta).</summary>
    public sealed record BannerPattern(string File, string Code, string[] Layers = null, int ItemId = 0, int ItemMeta = 0)
    {
        public static readonly BannerPattern[] All =
        {
            new("base", "b"),
            new("square_bottom_left", "bl", new[] { "   ", "   ", "#  " }), new("square_bottom_right", "br", new[] { "   ", "   ", "  #" }),
            new("square_top_left", "tl", new[] { "#  ", "   ", "   " }), new("square_top_right", "tr", new[] { "  #", "   ", "   " }),
            new("stripe_bottom", "bs", new[] { "   ", "   ", "###" }), new("stripe_top", "ts", new[] { "###", "   ", "   " }),
            new("stripe_left", "ls", new[] { "#  ", "#  ", "#  " }), new("stripe_right", "rs", new[] { "  #", "  #", "  #" }),
            new("stripe_center", "cs", new[] { " # ", " # ", " # " }), new("stripe_middle", "ms", new[] { "   ", "###", "   " }),
            new("stripe_downright", "drs", new[] { "#  ", " # ", "  #" }), new("stripe_downleft", "dls", new[] { "  #", " # ", "#  " }),
            new("small_stripes", "ss", new[] { "# #", "# #", "   " }), new("cross", "cr", new[] { "# #", " # ", "# #" }),
            new("straight_cross", "sc", new[] { " # ", "###", " # " }), new("triangle_bottom", "bt", new[] { "   ", " # ", "# #" }),
            new("triangle_top", "tt", new[] { "# #", " # ", "   " }), new("triangles_bottom", "bts", new[] { "   ", "# #", " # " }),
            new("triangles_top", "tts", new[] { " # ", "# #", "   " }), new("diagonal_left", "ld", new[] { "## ", "#  ", "   " }),
            new("diagonal_up_right", "rd", new[] { "   ", "  #", " ##" }), new("diagonal_up_left", "lud", new[] { "   ", "#  ", "## " }),
            new("diagonal_right", "rud", new[] { " ##", "  #", "   " }), new("circle", "mc", new[] { "   ", " # ", "   " }),
            new("rhombus", "mr", new[] { " # ", "# #", " # " }), new("half_vertical", "vh", new[] { "## ", "## ", "## " }),
            new("half_horizontal", "hh", new[] { "###", "###", "   " }), new("half_vertical_right", "vhr", new[] { " ##", " ##", " ##" }),
            new("half_horizontal_bottom", "hhb", new[] { "   ", "###", "###" }), new("border", "bo", new[] { "###", "# #", "###" }),
            new("curly_border", "cbo", null, B.Vine, 0), new("creeper", "cre", null, I.SkullItem, 4),
            new("gradient", "gra", new[] { "# #", " # ", " # " }), new("gradient_up", "gru", new[] { " # ", " # ", "# #" }),
            new("bricks", "bri", null, B.Bricks, 0), new("skull", "sku", null, I.SkullItem, 1),
            new("flower", "flo", null, B.Poppy, 8), new("mojang", "moj", null, I.GoldenApple, 1),
        };
        public static BannerPattern ByCode(string c) => All.FirstOrDefault(p => p.Code == c);
    }

    /// <summary>TileEntityBanner: kolor tła (meta barwnika: 0 czarny ... 15 biały) i do 6 wzorów (kod, meta barwnika).</summary>
    public sealed class BannerTile : TileEntity
    {
        public override int TypeId => 63;
        public int Base = 15;
        public readonly List<(string code, int color)> Patterns = new();

        public void SetFromItem(ItemStack s)
        {
            Base = Banners.BaseOf(s); Patterns.Clear();
            Patterns.AddRange(Banners.PatternsOf(s));
        }
        public ItemStack ToItem() => Banners.MakeItem(Base, Patterns);
        /// <summary>Klucz tekstury (identyczne wzory dzielą teksturę).</summary>
        public string Design => Banners.Design(Base, Patterns);

        public override void Save(BinaryWriter bw)
        {
            bw.Write((byte)Base); bw.Write((byte)Patterns.Count);
            foreach (var (c, col) in Patterns) { bw.Write(c); bw.Write((byte)col); }
        }
        public override void Load(BinaryReader br)
        {
            Base = br.ReadByte(); Patterns.Clear();
            int n = br.ReadByte();
            for (int i = 0; i < n; i++) Patterns.Add((br.ReadString(), br.ReadByte()));
        }
    }

    public static class Banners
    {
        public const int MaxPatterns = 6;
        public static bool IsBanner(int id) => id is B.StandingBanner or B.WallBanner;
        public static int BaseOf(ItemStack s)
        {
            var tag = s?.Tag?.GetCompound("BlockEntityTag");
            return (tag != null && tag.ContainsKey("Base") ? tag.GetInt("Base") : s?.Damage ?? 15) & 15;
        }

        public static List<(string code, int color)> PatternsOf(ItemStack s)
        {
            var list = new List<(string, int)>();
            var pats = s?.Tag?.GetCompound("BlockEntityTag")?.GetList("Patterns");
            if (pats == null) return list;
            foreach (var o in pats) if (o is Nbt n && n.GetString("Pattern") is { } code) list.Add((code, n.GetInt("Color") & 15));
            return list;
        }

        public static ItemStack MakeItem(int baseColor, IEnumerable<(string code, int color)> patterns)
        {
            var s = new ItemStack(I.Banner, 1, baseColor & 15);
            var list = patterns.Select(p => (object)new Nbt { ["Pattern"] = p.code, ["Color"] = p.color }).ToList();
            if (list.Count > 0) s.GetOrCreateTag().GetOrCreateCompound("BlockEntityTag")["Patterns"] = list;
            return s;
        }

        public static string Design(int baseColor, IEnumerable<(string code, int color)> patterns)
            => "b" + baseColor + string.Concat(patterns.Select(p => ";" + p.code + p.color));

        /// <summary>ItemBanner.getItemStackDisplayName: kolor + "sztandar".</summary>
        public static string Name(ItemStack s) => MascName[15 - BaseOf(s)] + " sztandar";
        private static readonly string[] MascName = { "Biały", "Pomarańczowy", "Karmazynowy", "Jasnoniebieski", "Żółty", "Limonkowy", "Różowy", "Szary",
            "Jasnoszary", "Błękitny", "Fioletowy", "Niebieski", "Brązowy", "Zielony", "Czerwony", "Czarny" };

        /// <summary>Kolor RGB warstwy: EnumDyeColor.byDyeDamage(meta).getMapColor().colorValue.</summary>
        public static uint LayerRgb(int dyeMeta) => MapColors.Base[MapColors.Dye[Dyes.ColorOf(new ItemStack(I.Dye, 1, dyeMeta & 15))]];

        /// <summary>ItemBanner.onItemUse 1.8.</summary>
        public static bool Place(Player p, ItemStack held, int x, int y, int z, int face)
        {
            var w = p.World;
            if (face == Dir.Down || !Block.Get(w.GetBlock(x, y, z)).Solid) return false;
            x += Dir.X[face]; y += Dir.Y[face]; z += Dir.Z[face];
            var at = Block.Get(w.GetBlock(x, y, z));
            if (at.Id != 0 && !at.Replaceable) return false;
            if (y < 0 || y >= Chunk.Height) return false;
            if (w.IsRemote) return true;
            if (face == Dir.Up) w.SetBlock(x, y, z, B.StandingBanner, MathUtil.FloorToInt((p.Yaw + 180f) * 16f / 360f + .5f) & 15);
            else w.SetBlock(x, y, z, B.WallBanner, face);
            if (w.GetTileEntity(x, y, z) is BannerTile te) te.SetFromItem(held);
            if (!p.Creative && --held.Count <= 0) p.Inventory.Selected = null;
            p.Inventory.Dirty = true;
            return true;
        }

        private static bool IsDye(ItemStack s) => Dyes.ColorOf(s) >= 0;
        private static int DyeMeta(ItemStack s) => Dyes.DyeMetaFromColor(Dyes.ColorOf(s));

        /// <summary>RecipeAddPattern 1.8: jeden sztandar (mniej niż 6 wzorów) + barwniki w kształcie wzoru albo przedmiot wzoru (+ najwyżej jeden barwnik).</summary>
        public static ItemStack AddPatternRecipe(ItemStack[] grid, int gw, int gh)
        {
            ItemStack banner = null;
            foreach (var s in grid)
                if (s?.Id == I.Banner) { if (banner != null || PatternsOf(s).Count >= MaxPatterns) return null; banner = s; }
            if (banner == null) return null;
            var pattern = MatchPattern(grid);
            if (pattern == null) return null;
            int color = 0;
            foreach (var s in grid) if (s != null && IsDye(s)) { color = DyeMeta(s); break; }
            var pats = PatternsOf(banner); pats.Add((pattern.Code, color));
            var r = banner.CopyWith(1);
            r.GetOrCreateTag().GetOrCreateCompound("BlockEntityTag")["Patterns"] =
                pats.Select(p => (object)new Nbt { ["Pattern"] = p.code, ["Color"] = p.color }).ToList();
            return r;
        }

        private static BannerPattern MatchPattern(ItemStack[] grid)
        {
            foreach (var pat in BannerPattern.All)
            {
                if (pat.Layers == null && pat.ItemId == 0) continue;
                bool ok = true;
                if (pat.ItemId != 0)
                {
                    bool item = false, dye = false;
                    foreach (var s in grid)
                    {
                        if (s == null || s.Id == I.Banner) continue;
                        if (IsDye(s)) { if (dye) { ok = false; break; } dye = true; }
                        else if (item || s.Id != pat.ItemId || s.Damage != pat.ItemMeta) { ok = false; break; }
                        else item = true;
                    }
                    if (!item) ok = false;
                }
                else if (grid.Length == 9)
                {
                    int color = -1;
                    for (int k = 0; k < 9 && ok; k++)
                    {
                        var s = grid[k]; bool hash = pat.Layers[k / 3][k % 3] != ' ';
                        if (s != null && s.Id != I.Banner)
                        {
                            if (!IsDye(s) || (color != -1 && color != DyeMeta(s)) || !hash) { ok = false; break; }
                            color = DyeMeta(s);
                        }
                        else if (hash) ok = false;
                    }
                }
                else ok = false;
                if (ok) return pat;
            }
            return null;
        }

        /// <summary>RecipeDuplicatePattern: sztandar ze wzorami + czysty tego samego koloru = kopia; wzorzec zostaje w siatce.</summary>
        public static ItemStack DuplicateRecipe(ItemStack[] grid, int gw, int gh)
        {
            ItemStack withPatterns = null, blank = null; int n = 0;
            foreach (var s in grid)
            {
                if (s == null) continue;
                if (s.Id != I.Banner) return null;
                n++;
                if (PatternsOf(s).Count > 0) { if (withPatterns != null || PatternsOf(s).Count > MaxPatterns) return null; withPatterns = s; }
                else { if (blank != null) return null; blank = s; }
            }
            if (n != 2 || withPatterns == null || blank == null || (withPatterns.Damage & 15) != (blank.Damage & 15)) return null;
            return withPatterns.CopyWith(1);
        }

        /// <summary>Slot siatki, który nie jest zużywany (wzorzec przy kopiowaniu sztandaru), albo -1.</summary>
        public static int KeepSlot(ItemStack[] grid, ItemStack result)
        {
            if (result?.Id != I.Banner || DuplicateRecipe(grid, 3, 3) == null) return -1;
            for (int i = 0; i < grid.Length; i++) if (grid[i]?.Id == I.Banner && PatternsOf(grid[i]).Count > 0) return i;
            return -1;
        }

        /// <summary>BlockCauldron: sztandar ze wzorami traci ostatni wzór, poziom wody -1.</summary>
        public static bool Wash(World w, int x, int y, int z, Player p, ItemStack stack)
        {
            int level = w.GetMeta(x, y, z);
            var pats = PatternsOf(stack);
            if (level <= 0 || pats.Count == 0) return false;
            pats.RemoveAt(pats.Count - 1);
            var washed = stack.CopyWith(1);
            if (pats.Count == 0)
            {
                washed.Tag?.Remove("BlockEntityTag");
                if (washed.Tag?.Count == 0) washed.Tag = null;
            }
            else washed.GetOrCreateTag().GetOrCreateCompound("BlockEntityTag")["Patterns"] =
                pats.Select(p => (object)new Nbt { ["Pattern"] = p.code, ["Color"] = p.color }).ToList();
            if (stack.Count <= 1 && !p.Creative) p.Inventory.Selected = washed;
            else
            {
                if (!p.Inventory.Add(washed)) p.DropStack(washed, false);
                if (!p.Creative) stack.Count--;
            }
            if (!p.Creative) w.SetMeta(x, y, z, level - 1);
            p.Inventory.Dirty = true;
            return true;
        }
    }

    public abstract class BannerBlock : Block
    {
        protected BannerBlock(byte id, string key) : base(id, key, "Sztandar")
        {
            T("planks_oak"); H(1); Tool_(ToolType.Axe); Snd(SoundType.Wood);
            Opaque = Solid = FullCube = false; LightOpacity = 0; Render = RenderType.None; InCreative = false;
            HasTileEntity = true;
        }
        public override ItemStack PickStack(int meta) => new(I.Banner, 1, 15);
        public override void OnAdded(World w, int x, int y, int z) { if (w.GetTileEntity(x, y, z) is not BannerTile) w.SetTileEntity(x, y, z, new BannerTile()); }
        public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) { }
        public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
            => drops.Add(w.GetTileEntity(x, y, z) is BannerTile te ? te.ToItem() : new ItemStack(I.Banner, 1, 15));
        protected abstract bool Supported(World w, int x, int y, int z, int meta);
        public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
        {
            if (w.IsRemote || Supported(w, x, y, z, w.GetMeta(x, y, z))) return;
            w.BreakBlock(x, y, z, true);
        }
    }

    /// <summary>BlockBannerStanding: meta = obrót 0-15, podłoże z litego materiału.</summary>
    public sealed class StandingBannerBlock : BannerBlock
    {
        public StandingBannerBlock() : base(B.StandingBanner, "standing_banner") { }
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes) => boxes.Add(new AABB(.25, 0, .25, .75, 1, .75));
        protected override bool Supported(World w, int x, int y, int z, int meta) => Block.Get(w.GetBlock(x, y - 1, z)).Solid;
    }

    /// <summary>BlockBannerHanging: meta = ściana 2-5, blok za plecami.</summary>
    public sealed class WallBannerBlock : BannerBlock
    {
        public WallBannerBlock() : base(B.WallBanner, "wall_banner") { }
        public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> boxes)
        {
            const double h = .78125, d = .125;
            boxes.Add(meta switch
            {
                2 => new AABB(0, 0, 1 - d, 1, h, 1),
                3 => new AABB(0, 0, 0, 1, h, d),
                4 => new AABB(1 - d, 0, 0, 1, h, 1),
                _ => new AABB(0, 0, 0, d, h, 1),
            });
        }
        protected override bool Supported(World w, int x, int y, int z, int meta)
        {
            int f = meta is >= 2 and <= 5 ? meta : 2, o = Dir.Opp[f];
            return Block.Get(w.GetBlock(x + Dir.X[o], y + Dir.Y[o], z + Dir.Z[o])).Solid;
        }
    }
}

namespace BlockCraft.Items
{
    public static partial class ItemRegistry
    {
        internal static void ItemsBanners()
        {
            var banner = Simple(I.Banner, "banner", "Sztandar", World.Blocks.TabDecoration, 16);
            banner.Variants = Enumerable.Range(0, 16).Select(i => 15 - i).ToArray();
            banner.NameFn = World.Banners.Name;
            banner.ColorFn = s => 0xFF000000 | Swap(World.Banners.LayerRgb(World.Banners.BaseOf(s)));
            banner.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => World.Banners.Place(p, s, x, y, z, face);
        }
        private static uint Swap(uint rgb) => (rgb & 0xFF) << 16 | (rgb & 0xFF00) | (rgb >> 16 & 0xFF);
    }

    public static partial class Recipes
    {
        /// <summary>Slot siatki niezużywany przy odbiorze wyniku (getRemainingItems), -1 = wszystkie zużyte.</summary>
        public static Func<ItemStack[], ItemStack, int> KeepSlotHook;

        internal static void RecipesBanners()
        {
            for (int c = 0; c < 16; c++)
                Shaped(new ItemStack(I.Banner, 1, Dyes.DyeMetaFromColor(c)), "###", "###", " | ", '#', ((int)B.Wool, c), '|', I.Stick);
            Special.Add(World.Banners.AddPatternRecipe);
            Special.Add(World.Banners.DuplicateRecipe);
            KeepSlotHook += World.Banners.KeepSlot;
        }
    }
}
