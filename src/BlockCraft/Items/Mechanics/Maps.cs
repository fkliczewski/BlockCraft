using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft.Items
{
    /// <summary>MapColor z 1.8: 36 kolorów bazowych, piksel mapy = indeks * 4 + jasność (0: 180/255, 1: 220/255, 2: 1, 3: 135/255).</summary>
    public static class MapColors
    {
        public static readonly uint[] Base =
        {
            0x000000, 0x7FB238, 0xF7E9A3, 0xC7C7C7, 0xFF0000, 0xA0A0FF, 0xA7A7A7, 0x007C00, 0xFFFFFF, 0xA4A8B8, 0x976D4D, 0x707070,
            0x4040FF, 0x8F7748, 0xFFFCF5, 0xD87F33, 0xB24CD8, 0x6699D8, 0xE5E533, 0x7FCC19, 0xF27FA5, 0x4C4C4C, 0x999999, 0x4C7F99,
            0x7F3FB2, 0x334CB2, 0x664C33, 0x667F33, 0x993333, 0x191919, 0xFAEE4D, 0x5CDBD5, 0x4A80FF, 0x00D93A, 0x815631, 0x700200,
        };
        public const int Air = 0, Grass = 1, Sand = 2, Cloth = 3, Tnt = 4, Ice = 5, Iron = 6, Foliage = 7, Snow = 8, Clay = 9, Dirt = 10, Stone = 11,
            Water = 12, Wood = 13, Quartz = 14, Adobe = 15, Magenta = 16, LightBlue = 17, Yellow = 18, Lime = 19, Pink = 20, Gray = 21, Silver = 22,
            Cyan = 23, Purple = 24, Blue = 25, Brown = 26, Green = 27, Red = 28, Black = 29, Gold = 30, Diamond = 31, Lapis = 32, Emerald = 33,
            Obsidian = 34, Netherrack = 35;
        private static readonly int[] Brightness = { 180, 220, 255, 135 };
        /// <summary>EnumDyeColor.getMapColor (meta wełny: 0 biały ... 15 czarny).</summary>
        public static readonly int[] Dye = { Snow, Adobe, Magenta, LightBlue, Yellow, Lime, Pink, Gray, Silver, Cyan, Purple, Blue, Brown, Green, Red, Black };
        /// <summary>BlockPlanks.EnumType.getMapColor: dąb, świerk, brzoza, dżungla, akacja, ciemny dąb.</summary>
        private static readonly int[] WoodKinds = { Wood, Obsidian, Sand, Dirt, Adobe, Brown };

        /// <summary>Kolor RGBA (TextureGen.Rgba) piksela mapy; 0 = przezroczysty.</summary>
        public static uint Rgba(byte pixel)
        {
            int idx = pixel >> 2;
            if (idx == 0 || idx >= Base.Length) return 0;
            int m = Brightness[pixel & 3]; uint c = Base[idx];
            return Core.Rgb.Pack((int)(c >> 16 & 255) * m / 255, (int)(c >> 8 & 255) * m / 255, (int)(c & 255) * m / 255);
        }

        private static int WoodKind(string key)
            => key.Contains("spruce") ? 1 : key.Contains("birch") ? 2 : key.Contains("jungle") ? 3 : key.Contains("acacia") ? 4 : key.Contains("dark_oak") ? 5 : 0;

        private static bool Any(string k, params string[] parts) { foreach (var p in parts) if (k.Contains(p)) return true; return false; }

        /// <summary>Block.getMapColor 1.8 (według materiału i wariantu).</summary>
        public static int Of(int id, int meta)
        {
            if (id == B.Air) return Air;
            var b = Block.Get(id);
            string k = b.Key ?? "";
            if (b.IsLiquid) return k.Contains("lava") ? Tnt : Water;
            if (Any(k, "stained_glass", "wool", "carpet", "stained_hardened_clay")) return Dye[meta & 15];
            if (Any(k, "glass", "torch", "redstone_wire", "rail", "lever", "button", "tripwire", "repeater", "comparator", "ladder", "flower_pot",
                "cake", "portal", "fire", "skull")) return Air;
            if (k == "grass" || k == "slime") return Grass;
            if (k == "mycelium") return Purple;
            if (k == "dirt") return (meta & 3) == 2 ? Obsidian : Dirt;
            if (k == "farmland" || k == "brown_mushroom_block") return Dirt;
            if (k == "clay") return Clay;
            if (k == "gravel" || k == "bedrock") return Stone;
            if (k == "sand") return (meta & 1) == 1 ? Adobe : Sand;
            if (k.StartsWith("red_sandstone") || k == "hardened_clay" || Any(k, "pumpkin")) return Adobe;
            if (k.StartsWith("sandstone") || k == "end_stone" || k == "glowstone") return Sand;
            if (k == "soul_sand") return Brown;
            if (k == "netherrack" || k.StartsWith("nether_brick") || k == "quartz_ore") return Netherrack;
            if (k.StartsWith("quartz") || k == "sea_lantern") return Quartz;
            if (k == "obsidian" || k == "coal_block") return Black;
            if (k.StartsWith("brick") || k == "red_mushroom_block") return Red;
            if (k == "prismarine") return Cyan;
            if (k == "sponge" || k == "hay_block") return Yellow;
            if (k == "melon_block") return Lime;
            if (k == "tnt" || k == "redstone_block") return Tnt;
            if (k == "lapis_block") return Lapis;
            if (k == "gold_block") return Gold;
            if (k == "diamond_block") return Diamond;
            if (k == "emerald_block") return Emerald;
            if (Any(k, "iron", "anvil", "cauldron", "brewing_stand", "hopper", "weighted_pressure_plate")) return Iron;
            if (k == "ice" || k == "packed_ice") return Ice;
            if (k == "snow" || k == "snow_layer") return Snow;
            if (k == "web" || k == "bed") return Cloth;
            if (Any(k, "leaves", "sapling", "flower", "tallgrass", "tall_grass", "deadbush", "dead_bush", "vine", "wheat", "carrots", "potatoes",
                "reeds", "waterlily", "stem", "nether_wart", "cocoa", "double_plant", "poppy", "dandelion", "cactus", "mushroom")) return Foliage;
            if (Any(k, "planks", "log", "_slab", "_stairs") && Any(k, "oak", "spruce", "birch", "jungle", "acacia", "dark_oak", "planks", "log"))
                return Any(k, "planks", "log", "oak", "spruce", "birch", "jungle", "acacia") && !Any(k, "stone", "cobble", "brick", "quartz", "sandstone")
                    ? WoodKinds[WoodKind(k)] : Stone;
            if (Any(k, "chest", "crafting", "bookshelf", "fence", "door", "trapdoor", "sign", "jukebox", "noteblock", "pressure_plate", "daylight",
                "banner", "wood")) return k.Contains("nether_brick") ? Netherrack : k.Contains("stone") ? Stone : Wood;
            return b.Solid ? Stone : Air;
        }
    }

    public struct MapDecoration { public byte Type; public sbyte X, Z; public byte Rot; }

    /// <summary>MapData 1.8: środek, skala 0-4, wymiar, piksele 128x128, znaczniki (gracze, ramki).</summary>
    public sealed class MapData
    {
        public int Id; public int XCenter, ZCenter; public int Dimension; public int Scale;
        public readonly byte[] Colors = new byte[128 * 128];
        public readonly Dictionary<string, MapDecoration> Decorations = new();
        /// <summary>Licznik zmian pikseli i lista prostokątów (wersja, x0, z0, x1, z1) do wysyłki przyrostowej.</summary>
        public int Version, DecoVersion;
        public readonly List<(int ver, int x0, int z0, int x1, int z1)> Changes = new();
        public bool Dirty;
        internal readonly Dictionary<Player, int> Steps = new();

        public void RecordChange(int x0, int z0, int x1, int z1)
        {
            Version++; Dirty = true;
            Changes.Add((Version, x0, z0, x1, z1));
            if (Changes.Count > 64) Changes.RemoveAt(0);
        }

        public void SetDecoration(string key, MapDecoration d)
        {
            if (Decorations.TryGetValue(key, out var old) && old.Type == d.Type && old.X == d.X && old.Z == d.Z && old.Rot == d.Rot) return;
            Decorations[key] = d; DecoVersion++;
        }
        public void RemoveDecoration(string key) { if (Decorations.Remove(key)) DecoVersion++; }

        /// <summary>MapData.updateDecorations 1.8: pozycja w pół-pikselach, obrót w 16 krokach, poza mapą kropka (typ 6) do 320 pikseli.</summary>
        public void UpdateDecoration(int type, World.World w, string key, double wx, double wz, double rotation)
        {
            int i = 1 << Scale;
            float f = (float)(wx - XCenter) / i, f1 = (float)(wz - ZCenter) / i;
            sbyte b0 = (sbyte)(int)(f * 2.0 + .5), b1 = (sbyte)(int)(f1 * 2.0 + .5);
            const int j = 63; byte b2;
            if (f >= -j && f1 >= -j && f <= j && f1 <= j)
            {
                rotation += rotation < 0 ? -8.0 : 8.0;
                b2 = (byte)((int)(rotation * 16.0 / 360.0) & 15);
                if (Dimension < 0)
                {
                    int k = (int)(w.DayTime / 10L);
                    b2 = (byte)(k * k * 34187121 + k * 121 >> 15 & 15);
                }
            }
            else
            {
                if (Math.Abs(f) >= 320 || Math.Abs(f1) >= 320) { RemoveDecoration(key); return; }
                type = 6; b2 = 0;
                if (f <= -j) b0 = unchecked((sbyte)(int)(j * 2 + 2.5));
                if (f1 <= -j) b1 = unchecked((sbyte)(int)(j * 2 + 2.5));
                if (f >= j) b0 = j * 2 + 1;
                if (f1 >= j) b1 = j * 2 + 1;
            }
            SetDecoration(key, new MapDecoration { Type = (byte)type, X = b0, Z = b1, Rot = b2 });
        }

        public void Write(BinaryWriter bw)
        {
            bw.Write((byte)1); bw.Write(XCenter); bw.Write(ZCenter); bw.Write((sbyte)Dimension); bw.Write((byte)Scale); bw.Write(Colors);
        }
        public static MapData Read(BinaryReader br, int id)
        {
            br.ReadByte();
            var m = new MapData { Id = id, XCenter = br.ReadInt32(), ZCenter = br.ReadInt32(), Dimension = br.ReadSByte(), Scale = br.ReadByte() };
            br.Read(m.Colors, 0, m.Colors.Length);
            return m;
        }
    }

    /// <summary>Mapy jednego zapisu świata (MapStorage z MC): data/map_N.dat i data/idcounts.txt. Gość ma własny magazyn w pamięci.</summary>
    public sealed class MapStore
    {
        private readonly string _dir;
        public readonly Dictionary<int, MapData> Maps = new();
        private int _next = -1;
        public MapStore(string worldDir) { _dir = worldDir == null ? null : Path.Combine(worldDir, "data"); }

        /// <summary>Magazyn gościa (dane przychodzą od hosta).</summary>
        public static MapStore Client = new(null);

        public static MapStore Of(World.World w)
        {
            if (w.IsRemote) return Client;
            var home = w.Dimensions?.Get(DimensionId.Overworld) ?? w;
            return home.MapStoreField ??= new MapStore(home.Storage?.Dir);
        }

        public MapData Get(int id)
        {
            if (Maps.TryGetValue(id, out var m)) return m;
            if (_dir == null) return null;
            string p = Path.Combine(_dir, "map_" + id + ".dat");
            if (!File.Exists(p)) return null;
            try { using var f = File.OpenRead(p); m = MapData.Read(new BinaryReader(f), id); Maps[id] = m; return m; }
            catch (Exception ex) { Log.Error("Mapa " + id + ": " + ex.Message); return null; }
        }

        /// <summary>World.getUniqueDataId("map"): kolejne numery od 0.</summary>
        public int NextId()
        {
            if (_next < 0)
            {
                _next = 0;
                string p = _dir == null ? null : Path.Combine(_dir, "idcounts.txt");
                if (p != null && File.Exists(p) && int.TryParse(File.ReadAllText(p).Trim(), out int last)) _next = last + 1;
                foreach (int k in Maps.Keys) _next = Math.Max(_next, k + 1);
            }
            int id = _next++;
            if (_dir != null) { Directory.CreateDirectory(_dir); File.WriteAllText(Path.Combine(_dir, "idcounts.txt"), id.ToString()); }
            return id;
        }

        public MapData Create(int scale, double x, double z, int dimension)
        {
            var m = new MapData { Id = NextId(), Scale = Math.Clamp(scale, 0, 4), Dimension = dimension, Dirty = true };
            (m.XCenter, m.ZCenter) = MapLogic.Center(x, z, m.Scale);
            Maps[m.Id] = m;
            return m;
        }

        public void SaveAll()
        {
            if (_dir == null) return;
            foreach (var m in Maps.Values)
            {
                if (!m.Dirty) continue;
                try
                {
                    Directory.CreateDirectory(_dir);
                    string p = Path.Combine(_dir, "map_" + m.Id + ".dat"), tmp = p + ".tmp";
                    using (var f = File.Create(tmp)) m.Write(new BinaryWriter(f));
                    File.Move(tmp, p, true);
                    m.Dirty = false;
                }
                catch (Exception ex) { Log.Error("Zapis mapy " + m.Id + ": " + ex.Message); }
            }
        }
    }

    /// <summary>ItemEmptyMap i ItemMap 1.8.</summary>
    public static class MapLogic
    {
        /// <summary>ItemEmptyMap: środek mapy w siatce 128 * 2^skala przesuniętej o 64.</summary>
        public static (int x, int z) Center(double x, double z, int scale)
        {
            int i = 128 * (1 << scale);
            int j = MathUtil.FloorToInt((x + 64.0) / i), k = MathUtil.FloorToInt((z + 64.0) / i);
            return (j * i + i / 2 - 64, k * i + i / 2 - 64);
        }

        public static MapData DataFor(World.World w, ItemStack s)
        {
            var store = MapStore.Of(w);
            var m = store.Get(s.Damage);
            if (m == null && !w.IsRemote)
            {
                // ItemMap.getMapData: brakujące dane - nowa mapa skali 3 wokół punktu odrodzenia
                m = store.Create(3, w.SpawnX, w.SpawnZ, (int)w.Dimension);
                s.Damage = m.Id;
            }
            return m;
        }

        /// <summary>ItemEmptyMap.onItemRightClick: nowa mapa skali 0 wyśrodkowana na graczu.</summary>
        public static bool UseEmpty(Player p, ItemStack held)
        {
            var w = p.World;
            if (w.IsRemote) return true;
            var m = MapStore.Of(w).Create(0, p.X, p.Z, (int)w.Dimension);
            var map = new ItemStack(I.FilledMap, 1, m.Id);
            if (!p.Creative) held.Count--;
            if (held.Count <= 0) p.Inventory.Selected = map;
            else if (!p.Inventory.Add(map)) p.DropStack(map, false);
            p.Inventory.Dirty = true;
            return true;
        }

        private static bool HasMap(Player p, int id)
        {
            foreach (var s in p.Inventory.Main) if (s != null && s.Id == I.FilledMap && s.Damage == id) return true;
            return false;
        }

        /// <summary>Host, co tick: ItemMap.onUpdate dla każdej mapy w ekwipunku (updateVisiblePlayers, a dla trzymanej updateMapData).</summary>
        public static void TickPlayer(Player p)
        {
            var w = p.World;
            if (w.IsRemote || p.Dead) return;
            var inv = p.Inventory.Main;
            for (int i = 0; i < inv.Length; i++)
            {
                var s = inv[i];
                if (s == null || s.Id != I.FilledMap) continue;
                var m = DataFor(w, s);
                if (m == null) continue;
                UpdateVisiblePlayers(m, p, s.Damage);
                if (i == p.Inventory.SelectedSlot) UpdateMapData(w, p, m);
            }
        }

        public static void UpdateVisiblePlayers(MapData m, Player p, int id)
        {
            if (!m.Steps.ContainsKey(p)) m.Steps[p] = 0;
            foreach (var other in m.Steps.Keys.ToList())
            {
                if (!other.Removed && !other.Dead && HasMap(other, id))
                {
                    if ((int)other.World.Dimension == m.Dimension) m.UpdateDecoration(0, other.World, other.Name, other.X, other.Z, other.Yaw);
                    else m.RemoveDecoration(other.Name);
                }
                else { m.Steps.Remove(other); m.RemoveDecoration(other.Name); }
            }
        }

        /// <summary>Znacznik ramki (typ 1, "frame-id") jak w EntityTrackerEntry dla ramki z mapą.</summary>
        public static void UpdateFrame(MapData m, ItemFrameEntity f)
            => m.SetDecoration("frame-" + f.Id, MakeFrameDecoration(m, f));

        private static MapDecoration MakeFrameDecoration(MapData m, ItemFrameEntity f)
        {
            int i = 1 << m.Scale;
            float fx = (float)(f.HangX - m.XCenter) / i, fz = (float)(f.HangZ - m.ZCenter) / i;
            double rot = f.Facing * 90; rot += rot < 0 ? -8 : 8;
            return new MapDecoration { Type = 1, X = (sbyte)Math.Clamp((int)(fx * 2 + .5), -128, 127), Z = (sbyte)Math.Clamp((int)(fz * 2 + .5), -128, 127), Rot = (byte)((int)(rot * 16 / 360) & 15) };
        }

        /// <summary>ItemMap.updateMapData 1.8 (port 1:1): co tick 1/16 kolumn w okręgu wokół gracza, kolor dominujący, cieniowanie od wysokości.</summary>
        public static void UpdateMapData(World.World w, Entity viewer, MapData data)
        {
            if ((int)w.Dimension != data.Dimension || viewer is not Player player) return;
            int i = 1 << data.Scale;
            int j = data.XCenter, k = data.ZCenter;
            int l = MathUtil.FloorToInt(viewer.X - j) / i + 64;
            int i1 = MathUtil.FloorToInt(viewer.Z - k) / i + 64;
            int j1 = 128 / i;
            bool noSky = w.Provider.HasNoSky;
            if (noSky) j1 /= 2;
            int step = data.Steps.TryGetValue(player, out int s0) ? s0 + 1 : 1;
            data.Steps[player] = step;
            int minX = 128, minZ = 128, maxX = -1, maxZ = -1;
            bool flag = false;
            var counts = new int[MapColors.Base.Length];
            for (int k1 = l - j1 + 1; k1 < l + j1; ++k1)
            {
                if ((k1 & 15) != (step & 15) && !flag) continue;
                flag = false;
                double d0 = 0;
                for (int l1 = i1 - j1 - 1; l1 < i1 + j1; ++l1)
                {
                    if (k1 < 0 || l1 < -1 || k1 >= 128 || l1 >= 128) continue;
                    int i2 = k1 - l, j2 = l1 - i1;
                    bool flag1 = i2 * i2 + j2 * j2 > (j1 - 2) * (j1 - 2);
                    int k2 = (j / i + k1 - 64) * i, l2 = (k / i + l1 - 64) * i;
                    var chunk = w.GetChunk(k2 >> 4, l2 >> 4);
                    if (chunk == null) continue;
                    Array.Clear(counts);
                    int i3 = k2 & 15, j3 = l2 & 15, k3 = 0;
                    double d1 = 0;
                    if (noSky)
                    {
                        int l3 = k2 + l2 * 231871;
                        l3 = l3 * l3 * 31287121 + l3 * 11;
                        if ((l3 >> 20 & 1) == 0) counts[MapColors.Dirt] += 10; else counts[MapColors.Stone] += 100;
                        d1 = 100;
                    }
                    else
                    {
                        for (int i4 = 0; i4 < i; ++i4)
                            for (int j4 = 0; j4 < i; ++j4)
                            {
                                int cx = i4 + i3, cz = j4 + j3;
                                if (cx > 15 || cz > 15) continue;
                                int k4 = chunk.GetHeight(cx, cz) + 1;
                                int color = MapColors.Air;
                                if (k4 > 1)
                                {
                                    int id;
                                    do
                                    {
                                        --k4;
                                        id = k4 < 256 ? chunk.GetBlock(cx, k4, cz) : 0;
                                        color = MapColors.Of(id, k4 < 256 ? chunk.GetMeta(cx, k4, cz) : 0);
                                    }
                                    while (color == MapColors.Air && k4 > 0);
                                    if (k4 > 0 && Block.Get(id).IsLiquid)
                                    {
                                        int l4 = k4 - 1;
                                        while (true)
                                        {
                                            int below = l4 >= 0 ? chunk.GetBlock(cx, l4, cz) : 0; l4--;
                                            ++k3;
                                            if (l4 <= 0 || !Block.Get(below).IsLiquid) break;
                                        }
                                    }
                                }
                                d1 += (double)k4 / (i * i);
                                counts[color]++;
                            }
                    }
                    k3 /= i * i;
                    double d2 = (d1 - d0) * 4.0 / (i + 4) + (((k1 + l1) & 1) - .5) * .4;
                    int i5 = 1;
                    if (d2 > .6) i5 = 2;
                    if (d2 < -.6) i5 = 0;
                    // Multisets.copyHighestCountFirst: najczęstszy kolor; przy remisie pierwszy dodany (kolejność iteracji HashMultiset ~ indeks)
                    int best = MapColors.Air, bestN = 0;
                    for (int c = 0; c < counts.Length; c++) if (counts[c] > bestN) { bestN = counts[c]; best = c; }
                    if (best == MapColors.Water)
                    {
                        d2 = k3 * .1 + ((k1 + l1) & 1) * .2;
                        i5 = 1;
                        if (d2 < .5) i5 = 2;
                        if (d2 > .9) i5 = 0;
                    }
                    d0 = d1;
                    if (l1 >= 0 && i2 * i2 + j2 * j2 < j1 * j1 && (!flag1 || ((k1 + l1) & 1) != 0))
                    {
                        byte b0 = data.Colors[k1 + l1 * 128], b1 = (byte)(best * 4 + i5);
                        if (b0 != b1)
                        {
                            data.Colors[k1 + l1 * 128] = b1;
                            minX = Math.Min(minX, k1); maxX = Math.Max(maxX, k1); minZ = Math.Min(minZ, l1); maxZ = Math.Max(maxZ, l1);
                            flag = true;
                        }
                    }
                }
            }
            if (maxX >= 0) data.RecordChange(minX, minZ, maxX, maxZ);
        }

        /// <summary>Gotowy przepis specjalny: RecipesMapExtending (mapa + 8 papieru, skala &lt; 4) i RecipesMapCloning (mapa + puste mapy).</summary>
        public static ItemStack SpecialRecipe(ItemStack[] grid, int gw, int gh)
        {
            int maps = 0, empty = 0, paper = 0, other = 0; ItemStack map = null;
            foreach (var s in grid)
            {
                if (s == null) continue;
                if (s.Id == I.FilledMap) { maps++; map = s; }
                else if (s.Id == I.EmptyMap) empty++;
                else if (s.Id == I.Paper) paper++;
                else other++;
            }
            if (maps != 1 || other > 0) return null;
            if (gw == 3 && gh == 3 && paper == 8 && empty == 0 && grid[4]?.Id == I.FilledMap)
            {
                var r = map.CopyWith(1);
                r.GetOrCreateTag()["map_is_scaling"] = true;
                return r;
            }
            if (paper == 0 && empty > 0) return map.CopyWith(empty + 1);
            return null;
        }

        /// <summary>ItemMap.onCreated: mapa ze znacznikiem powiększania dostaje nowe dane o skali +1 i tym samym środku.</summary>
        public static void OnCrafted(Player p, ItemStack s)
        {
            if (s == null || s.Id != I.FilledMap || s.Tag == null || !s.Tag.GetBool("map_is_scaling")) return;
            s.Tag.Remove("map_is_scaling");
            if (s.Tag.Count == 0) s.Tag = null;
            var w = p.World;
            if (w.IsRemote) return;
            var store = MapStore.Of(w);
            var old = store.Get(s.Damage);
            if (old == null) return;
            var m = new MapData { Id = store.NextId(), Scale = Math.Min(4, old.Scale + 1), XCenter = old.XCenter, ZCenter = old.ZCenter, Dimension = old.Dimension, Dirty = true };
            store.Maps[m.Id] = m;
            s.Damage = m.Id;
        }
    }

    public static partial class ItemRegistry
    {
        internal static void ItemsMaps()
        {
            var empty = Simple(I.EmptyMap, "map_empty", "Pusta mapa", Blocks.TabMisc);
            empty.UseInAir = MapLogic.UseEmpty;
            empty.UseOnBlock = (p, s, x, y, z, face, hx, hy, hz) => MapLogic.UseEmpty(p, s);
            var filled = Simple(I.FilledMap, "map_filled", "Mapa", Blocks.TabMisc);
            filled.InCreative = false;
        }
    }

    public static partial class Recipes
    {
        /// <summary>Przepisy liczone z całej siatki (mapy, fajerwerki, sztandary). Sprawdzane przed zwykłymi.</summary>
        public static readonly List<Func<ItemStack[], int, int, ItemStack>> Special = new();
        /// <summary>Po wyjęciu wyniku z siatki (ItemStack.onCrafting / Item.onCreated).</summary>
        public static Action<Player, ItemStack> OnCraftedHook;

        internal static void RecipesMaps()
        {
            Shaped(new ItemStack(I.EmptyMap), "###", "#X#", "###", '#', I.Paper, 'X', I.Compass);
            Special.Add(MapLogic.SpecialRecipe);
            OnCraftedHook += MapLogic.OnCrafted;
        }
    }
}

namespace BlockCraft.Core
{
    public static class Rgb
    {
        public static uint Pack(int r, int g, int b, int a = 255) => (uint)(r & 255) | (uint)(g & 255) << 8 | (uint)(b & 255) << 16 | (uint)(a & 255) << 24;
    }
}
