using System.Text.Json;
using BlockCraft.Core;

namespace BlockCraft.Render;

/// <summary>Tekstury bloków i przedmiotów z paczek zasobów 1.8 nakładane na atlas generowany w kodzie.
/// Warstwy atlasu się nie zmieniają (siatki chunków zostają), podmieniane są tylko piksele.</summary>
public static partial class TextureGen
{
    /// <summary>Krok animacji z .mcmeta: numer klatki i czas jej wyświetlania w tickach.</summary>
    public readonly record struct AnimStep(int Frame, int Time);

    /// <summary>Animacja z paczki: klatki 16x16 i kroki cyklu (z .mcmeta).</summary>
    public sealed record PackAnim(List<uint[]> Frames, AnimStep[] Steps)
    {
        private readonly long _cycleLength = Steps.Sum(s => (long)s.Time);

        /// <summary>Numer klatki wyświetlanej w danym ticku.</summary>
        public int FrameAt(long tick)
        {
            long t = tick % _cycleLength;
            foreach (var step in Steps)
            {
                if (t < step.Time) return step.Frame;
                t -= step.Time;
            }
            return Steps[^1].Frame;
        }
    }

    /// <summary>Animacje z aktywnych paczek (zastępują wygenerowane animacje o tej samej nazwie).</summary>
    public static readonly Dictionary<string, PackAnim> PackAnimations = new();
    /// <summary>Nazwy tekstur, które dała paczka (wtedy wygenerowana animacja jest wyłączona).</summary>
    public static readonly HashSet<string> PackOverridden = new();
    /// <summary>Kopia tekstur wygenerowanych w kodzie (do przywrócenia po wyłączeniu paczki).</summary>
    private static uint[][] _generated;

    public const string TexturesRoot = "assets/minecraft/textures/";
    /// <summary>Kolory w kolejności meta wełny (0 = biały ... 15 = czarny), jak w nazwach plików 1.8.</summary>
    public static readonly string[] DyeColorNames = { "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray", "silver", "cyan", "purple", "blue", "brown", "green", "red", "black" };

    /// <summary>Nakłada tekstury z jednego źródła (read: ścieżka w paczce -> bajty albo null). Null = same tekstury gry.</summary>
    public static List<int> ApplyPacks(Func<string, byte[]> read) => ApplyPacks(read == null ? null : new[] { read });

    /// <summary>Nakłada tekstury z paczek (pierwsza ma pierwszeństwo). Null albo pusta lista = same tekstury gry.
    /// Zwraca indeksy warstw, których piksele się zmieniły.</summary>
    public static List<int> ApplyPacks(IReadOnlyList<Func<string, byte[]>> packs) => ApplyPacks(packs, null);

    /// <summary>Jak wyżej; extraPaths: tekstury z paczek (np. "blocks/grass_side_overlay"), których używają modele bloków
    /// z paczek, a gra nie ma ich w atlasie — dostają osobne warstwy „pack:ścieżka” (do następnego nakładania).</summary>
    public static List<int> ApplyPacks(IReadOnlyList<Func<string, byte[]>> packs, IEnumerable<string> extraPaths,
        IReadOnlyList<Func<string, byte[]>> modelPacks = null)
    {
        if (packs is { Count: 0 }) packs = null;
        Ensure();
        lock (Lock)
        {
            _generated ??= _tex.Select(p => (uint[])p.Clone()).ToArray();
            ResetModelTextureLayers(packs != null ? extraPaths : null);
            var sources = new PackTextureSources(packs, modelPacks);
            var changed = new List<int>();
            PackAnimations.Clear(); PackOverridden.Clear();
            foreach (var (name, i) in _index)
            {
                uint[] px = null; PackAnim anim = null;
                if (packs != null)
                    try { px = sources.Resolve(name, out anim); }
                    catch (Exception ex) { Log.Error("Paczka zasobów: tekstura " + name + ": " + ex.Message); }
                if (px != null) { PackOverridden.Add(name); if (anim != null) PackAnimations[name] = anim; }
                var target = px ?? (i < _generated.Length ? _generated[i] : _generated[0]); // brak pliku: tekstura „missing”
                if (!_tex[i].AsSpan().SequenceEqual(target)) changed.Add(i);
                _tex[i] = target;
            }
            if (PackOverridden.Count > 0) Log.Info($"Paczki zasobów: {PackOverridden.Count} tekstur z paczek, {PackAnimations.Count} animacji");
            BuildAtlas(packs, sources.MaxNative, modelPacks);
            return changed;
        }
    }

    private static void ResetModelTextureLayers(IEnumerable<string> extraPaths)
    {
        // warstwy z poprzedniego nakładania („pack:...”) usuwamy, potem dokładamy potrzebne teraz
        if (_tex.Count > _generated.Length)
        {
            _tex.RemoveRange(_generated.Length, _tex.Count - _generated.Length);
            foreach (var k in _index.Where(kv => kv.Value >= _generated.Length).Select(kv => kv.Key).ToList()) _index.Remove(k);
        }
        if (extraPaths != null)
            foreach (var path in extraPaths.Distinct().OrderBy(p => p, StringComparer.Ordinal))
                if (LayerForPackPath(path) < 0) { _index[PackLayerPrefix + path] = _tex.Count; _tex.Add(_tex[0]); }
    }

    private sealed class PackTextureSources
    {
        private readonly PackSource _regular;
        private readonly PackSource _models;

        public PackTextureSources(IReadOnlyList<Func<string, byte[]>> packs,
            IReadOnlyList<Func<string, byte[]>> modelPacks, int size = 16)
        {
            _regular = new PackSource(packs, size);
            _models = new PackSource(modelPacks ?? packs, size);
        }

        public int MaxNative => Math.Max(_regular.MaxNative, _models.MaxNative);

        public uint[] Resolve(string name, out PackAnim animation) =>
            ResolvePackTexture(name, name.StartsWith(PackLayerPrefix) ? _models : _regular, out animation);
    }


    // ================= atlas HD =================

    /// <summary>Największy bok atlasu terenu (ustawienie graficzne „Tekstury paczek”); 16 = jak w 1.8 bez HD.</summary>
    public static int MaxAtlasSize = 64;
    /// <summary>Bok warstwy atlasu terenu na GPU: 16 albo rozdzielczość paczek HD (potęga dwójki, najwyżej MaxAtlasSize).</summary>
    public static int AtlasSize { get; private set; } = 16;
    /// <summary>Warstwy HD z paczek (indeks warstwy -> piksele AtlasSize²); pozostałe są powiększane z 16x16.</summary>
    private static readonly Dictionary<int, uint[]> _atlasHd = new();
    /// <summary>Animacje z paczek w rozmiarze atlasu (przy 16 te same co PackAnimations).</summary>
    public static readonly Dictionary<string, PackAnim> AtlasAnimations = new();

    /// <summary>Drugi przebieg nakładania w rozdzielczości paczek HD (gdy większa niż 16).</summary>
    private static void BuildAtlas(IReadOnlyList<Func<string, byte[]>> packs, int maxNative,
        IReadOnlyList<Func<string, byte[]>> modelPacks)
    {
        _atlasHd.Clear(); AtlasAnimations.Clear();
        int cap = 16;
        while (cap * 2 <= MaxAtlasSize && cap < 256) cap *= 2; // potęga dwójki 16..256
        int size = 16;
        while (size < maxNative && size < cap) size *= 2;
        AtlasSize = size;
        if (size == 16 || packs == null)
        {
            AtlasSize = 16;
            foreach (var (k, v) in PackAnimations) AtlasAnimations[k] = v;
            return;
        }
        var sources = new PackTextureSources(packs, modelPacks, size);
        foreach (var name in PackOverridden)
        {
            uint[] px = null; PackAnim anim = null;
            try { px = sources.Resolve(name, out anim); }
            catch (Exception ex) { Log.Error("Paczka zasobów: tekstura HD " + name + ": " + ex.Message); }
            if (px == null || px.Length != size * size) continue;
            _atlasHd[_index[name]] = px;
            if (anim != null) AtlasAnimations[name] = anim;
        }
        // animacje bez wersji HD: klatki 16x16 powiększone
        foreach (var (k, v) in PackAnimations)
            if (!AtlasAnimations.ContainsKey(k)) AtlasAnimations[k] = new PackAnim(v.Frames.Select(ToAtlas).ToList(), v.Steps);
        Log.Info($"Paczki zasobów: atlas terenu {size}x{size}, {_atlasHd.Count} tekstur HD (limit {MaxAtlasSize})");
    }

    /// <summary>Warstwy atlasu terenu w rozmiarze AtlasSize.</summary>
    public static List<uint[]> AtlasLayers()
    {
        Ensure();
        lock (Lock) return _tex.Select((t, i) => _atlasHd.TryGetValue(i, out var hd) ? hd : ToAtlas(t)).ToList();
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<uint[], uint[]> _upscaledCache = new();

    /// <summary>Tekstura 16x16 powiększona bez wygładzania do rozmiaru atlasu (np. wygenerowane klatki wody przy atlasie HD).</summary>
    public static uint[] ToAtlas(uint[] px16)
    {
        int n = AtlasSize;
        if (n == 16 || px16.Length != 256) return px16;
        if (_upscaledCache.TryGetValue(px16, out var cached) && cached.Length == n * n) return cached;
        int k = n / 16; var d = new uint[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) d[y * n + x] = px16[(y / k) * 16 + x / k];
        _upscaledCache.AddOrUpdate(px16, d);
        return d;
    }

    /// <summary>Prefiks warstw z teksturami dokładanymi dla modeli z paczek.</summary>
    public const string PackLayerPrefix = "pack:";
    private static Dictionary<string, string> _nameByPath;

    /// <summary>Warstwa atlasu dla ścieżki tekstury 1.8 ("blocks/stone"): tekstura gry przypisana 1:1 albo warstwa „pack:”;
    /// -1 = brak. Tekstury składane przez grę (np. bok trawy z nakładką) nie są przypisane — model dostaje oryginalny plik.</summary>
    public static int LayerForPackPath(string path)
    {
        Ensure();
        lock (Lock)
        {
            if (_nameByPath == null)
            {
                var d = new Dictionary<string, string>(StringComparer.Ordinal);
                var rules = PackRules;
                foreach (var name in _index.Keys.Where(n => !n.StartsWith(PackLayerPrefix)))
                {
                    if (rules.Custom.ContainsKey(name) || rules.Transform.ContainsKey(name) || rules.Skip.Contains(name)) continue;
                    if (rules.Alias.TryGetValue(name, out var alias)) d.TryAdd(alias, name);
                    else if (name.StartsWith("item_")) d.TryAdd("items/" + name[5..], name);
                    else d.TryAdd("blocks/" + name, name);
                }
                _nameByPath = d;
            }
            if (_nameByPath.TryGetValue(path, out var n) && _index.TryGetValue(n, out int layer)) return layer;
            return _index.TryGetValue(PackLayerPrefix + path, out int pl) ? pl : -1;
        }
    }

    private static uint[] ResolvePackTexture(string name, PackSource src, out PackAnim anim)
    {
        anim = null;
        if (name.StartsWith(PackLayerPrefix)) return Pick(src.Img(name[PackLayerPrefix.Length..]), out anim);
        var rules = PackRules;
        if (rules.Transform.TryGetValue(name, out var tr)) return Transformed(src.Img(tr.File), src, tr.Frame, out anim);
        if (rules.Custom.TryGetValue(name, out var fn)) return fn(src);
        if (rules.Alias.TryGetValue(name, out var alias)) return Pick(src.Img(alias), out anim);
        if (rules.Skip.Contains(name)) return null;
        if (name.StartsWith("item_"))
            return Pick(src.Img("items/" + name[5..]), out anim) ?? Pick(src.Img("blocks/" + name[5..]), out anim);
        return Pick(src.Img("blocks/" + name), out anim) ?? Pick(src.Img("items/" + name), out anim);
    }

    private static uint[] Pick(PackImage img, out PackAnim anim)
    {
        anim = img?.Anim;
        return img == null ? null : (uint[])img.Still.Clone();
    }

    /// <summary>Każda klatka obrazu przekształcona osobno; animacja zachowuje sekwencję i czasy z .mcmeta.</summary>
    private static uint[] Transformed(PackImage img, PackSource src, Func<PackSource, uint[], uint[]> frame, out PackAnim anim)
    {
        anim = null;
        if (img == null) return null;
        var frames = img.Frames.Select(f => frame(src, (uint[])f.Clone())).ToList();
        if (img.Anim != null) anim = new PackAnim(frames, img.Anim.Steps);
        return (uint[])frames[img.Frames.IndexOf(img.Still)].Clone();
    }

    // ================= przypisania: nazwa w BlockCraft -> plik 1.8 =================

    public sealed class PackRuleSet
    {
        /// <summary>Nazwa tekstury -> plik 1.8 bez rozszerzenia ("blocks/torch_on"); animacje z .mcmeta działają.</summary>
        public readonly Dictionary<string, string> Alias = new();
        /// <summary>Tekstury przekształcane z jednego pliku 1.8 klatka po klatce (animacje z .mcmeta działają).</summary>
        public readonly Dictionary<string, (string File, Func<PackSource, uint[], uint[]> Frame)> Transform = new();
        /// <summary>Tekstury wybierane z kilku klatek paska (bez animacji czasowej).</summary>
        public readonly Dictionary<string, Func<PackSource, uint[]>> Custom = new();
        /// <summary>Tekstury, których nie bierzemy z paczek w tym etapie (inny układ w MC: encje, cząsteczki, GUI).</summary>
        public readonly HashSet<string> Skip = new();
    }

    private static PackRuleSet _rules;
    public static PackRuleSet PackRules => _rules ??= BuildPackRules();

    private static PackRuleSet BuildPackRules()
    {
        var r = new PackRuleSet();
        var a = r.Alias;
        // bloki o innych nazwach niż w 1.8
        a["sandstone_side"] = "blocks/sandstone_normal";
        a["flower_poppy"] = "blocks/flower_rose";
        a["torch"] = "blocks/torch_on";
        a["piston_top"] = "blocks/piston_top_normal";
        a["fire"] = "blocks/fire_layer_0";
        a["quartz_block"] = "blocks/quartz_block_side";
        a["quartz_pillar"] = "blocks/quartz_block_lines";
        a["quartz_chiseled"] = "blocks/quartz_block_chiseled";
        a["end_portal_frame"] = "blocks/endframe_side";
        a["me_enchant_top"] = "blocks/enchanting_table_top";
        a["me_enchant_side"] = "blocks/enchanting_table_side";
        a["me_anvil_base"] = "blocks/anvil_base";
        a["me_jukebox_side"] = "blocks/jukebox_side";
        a["me_jukebox_top"] = "blocks/jukebox_top";
        for (int i = 0; i < 3; i++) a["me_anvil_top_" + i] = "blocks/anvil_top_damaged_" + i;
        for (int i = 0; i < 8; i++) a["wheat_" + i] = "blocks/wheat_stage_" + i;
        for (int i = 0; i < 10; i++) a["destroy_" + i] = "blocks/destroy_stage_" + i;
        for (int i = 0; i < 3; i++) a["nether_wart_" + i] = "blocks/nether_wart_stage_" + i;
        // pierwsze wełny gry (biała, jasnoszara, szara, czarna, brązowa, różowa)
        string[] oldWool = { "white", "silver", "gray", "black", "brown", "pink" };
        for (int i = 0; i < oldWool.Length; i++) a["wool_" + i] = "blocks/wool_colored_" + oldWool[i];
        for (int i = 0; i < 16; i++)
        {
            a["wool_colored_" + i] = "blocks/wool_colored_" + DyeColorNames[i];
            a["hardened_clay_stained_" + i] = "blocks/hardened_clay_stained_" + DyeColorNames[i];
            a["glass_" + i] = "blocks/glass_" + DyeColorNames[i];
            a["item_dye_" + i] = "items/dye_powder_" + DyeColorNames[15 - i]; // barwnik: meta 0 = czarny ... 15 = biały
        }

        // przedmioty o innych nazwach niż w 1.8
        foreach (var t in new[] { "pickaxe", "axe", "shovel", "sword", "hoe" })
        {
            a["item_wooden_" + t] = "items/wood_" + t;
            a["item_golden_" + t] = "items/gold_" + t;
        }
        foreach (var t in new[] { "helmet", "chestplate", "leggings", "boots" }) a["item_golden_" + t] = "items/gold_" + t;
        foreach (var (ours, mc) in new (string, string)[]
        {
            ("lapis_lazuli", "dye_powder_blue"), ("bone_meal", "dye_powder_white"), ("redstone", "redstone_dust"),
            ("bucket", "bucket_empty"), ("water_bucket", "bucket_water"), ("lava_bucket", "bucket_lava"), ("milk_bucket", "bucket_milk"),
            ("bow", "bow_standby"), ("golden_apple", "apple_golden"),
            ("porkchop", "porkchop_raw"), ("cooked_porkchop", "porkchop_cooked"), ("beef", "beef_raw"), ("cooked_beef", "beef_cooked"),
            ("mutton", "mutton_raw"), ("cooked_mutton", "mutton_cooked"), ("chicken", "chicken_raw"), ("cooked_chicken", "chicken_cooked"),
            ("rabbit", "rabbit_raw"), ("cooked_rabbit", "rabbit_cooked"),
            ("wheat_seeds", "seeds_wheat"), ("pumpkin_seeds", "seeds_pumpkin"), ("melon_seeds", "seeds_melon"),
            ("book", "book_normal"), ("enchanted_book", "book_enchanted"), ("writable_book", "book_writable"), ("written_book", "book_written"),
            ("wooden_door", "door_wood"), ("spruce_door", "door_spruce"), ("birch_door", "door_birch"), ("jungle_door", "door_jungle"),
            ("acacia_door", "door_acacia"), ("dark_oak_door", "door_dark_oak"), ("iron_door", "door_iron"),
            ("minecart", "minecart_normal"), ("chest_minecart", "minecart_chest"), ("furnace_minecart", "minecart_furnace"),
            ("tnt_minecart", "minecart_tnt"), ("hopper_minecart", "minecart_hopper"),
            ("fire_charge", "fireball"), ("golden_horse_armor", "gold_horse_armor"), ("slime_ball", "slimeball"),
            ("glass_bottle", "potion_bottle_empty"), ("potion", "potion_bottle_drinkable"), ("splash_potion", "potion_bottle_splash"),
            ("baked_potato", "potato_baked"), ("poisonous_potato", "potato_poisonous"),
            ("fermented_spider_eye", "spider_eye_fermented"), ("speckled_melon", "melon_speckled"), ("golden_carrot", "carrot_golden"),
            ("armor_stand", "wooden_armorstand"), ("fishing_rod", "fishing_rod_uncast"), ("firework_charge", "fireworks_charge"),
            ("spawn_egg_spots", "spawn_egg_overlay"),
        }) a["item_" + ours] = "items/" + mc;
        a["me_potion_liquid"] = "items/potion_overlay";

        var c = r.Custom;
        var tr = r.Transform;
        // trawa: bok z nakładką barwioną wg biomu (alfa < 252 = piksel do zabarwienia, patrz shader chunków)
        tr["grass_side"] = ("blocks/grass_side", GrassSide);
        tr["grass_side_item"] = ("blocks/grass_side", (s, b) => TintMarked(GrassSide(s, b), 0x79C05A));
        tr["grass_top_item"] = ("blocks/grass_top", (_, t) => Tinted(t, 0x79C05A));
        // liście na grafice „Szybka”: ta sama tekstura bez przezroczystości
        foreach (var l in new[] { "oak", "birch", "spruce", "jungle", "acacia", "big_oak" })
        {
            tr["leaves_" + l + "_opaque"] = ("blocks/leaves_" + l, (_, t) => Opaque(t));
        }
        // skórzana zbroja: w 1.8 szara, barwiona domyślnym brązem, z nakładką
        foreach (var piece in new[] { "helmet", "chestplate", "leggings", "boots" })
        {
            string file = "items/leather_" + piece;
            tr["item_leather_" + piece] = (file, (s, b) =>
            {
                b = Tinted(b, 0xA06540);
                if (s.Still(file + "_overlay") is { } o) Over(b, o);
                return b;
            });
        }
        // zegar i kompas: klatki z paska items/clock.png (64) i items/compass.png (32), bez animacji czasowej
        c["item_clock"] = s => s.Frame("items/clock", 0);
        c["item_compass"] = s => s.Frame("items/compass", 0);
        for (int i = 0; i < 64; i++) { int f = i; c["item_clock_" + i] = s => s.Frame("items/clock", f); }
        for (int i = 0; i < 32; i++) { int f = i; c["item_compass_" + i] = s => s.Frame("items/compass", f); }
        // zakręty szyn: jedna tekstura w 1.8 obracana wg meta (6..9)
        for (int m = 6; m < 10; m++) { int rot = m - 6; tr["rail_turn_" + m] = ("blocks/rail_normal_turned", (_, t) => Rotate(t, rot)); }

        // cząsteczki: komórki 8x8 z particle/particles.png (indeksy jak setParticleTextureIndex w 1.8)
        for (int i = 0; i < 8; i++) { int cell = 7 - i; c["particle_smoke_" + i] = s => s.Cell("particle/particles", 16, cell); } // 0 = największy dym
        for (int i = 0; i < 8; i++) { int cell = 160 + i; c["particle_firework_" + i] = s => s.Cell("particle/particles", 16, cell); }
        foreach (var (name, cell) in new[] { ("particle_bubble", 32), ("particle_flame", 48), ("particle_crit", 65), ("particle_heart", 80),
                     ("particle_angry", 81), ("particle_splash", 19), ("particle_portal", 3), ("particle_white", 0) })
            c[name] = s => s.Cell("particle/particles", 16, cell);
        for (int i = 0; i < 4; i++) { int cell = i * 5; c["particle_explosion_" + i] = s => s.Cell("entity/explosion", 4, cell); }

        // skrzynie: ściany składane z tekstur modelu (entity/chest/*.png, 64x64 jak ModelChest 1.8)
        c["chest_front"] = s => ChestFace(s.Raw("entity/chest/normal", 64), ChestPart.Front, s.Size);
        c["chest_side"] = s => ChestFace(s.Raw("entity/chest/normal", 64), ChestPart.Side, s.Size);
        c["chest_top"] = s => ChestFace(s.Raw("entity/chest/normal", 64), ChestPart.Top, s.Size);
        c["trapped_chest_front"] = s => ChestFace(s.Raw("entity/chest/trapped", 64), ChestPart.Front, s.Size);
        c["trapped_chest_side"] = s => ChestFace(s.Raw("entity/chest/trapped", 64), ChestPart.Side, s.Size);
        c["trapped_chest_top"] = s => ChestFace(s.Raw("entity/chest/trapped", 64), ChestPart.Top, s.Size);
        foreach (var part in new[] { ChestPart.Front, ChestPart.Side, ChestPart.Top, ChestPart.Lock })
            c["ender_chest_" + part.ToString().ToLowerInvariant()] = s => EnderChestTile(s.Raw("entity/chest/ender", 64), part, s.Size);

        // inny układ niż blok 16x16 albo inna paczka plików: kolejne etapy (encje, cząsteczki, GUI, modele)
        foreach (var n in new[] { "minecart_side", "minecart_inside", "end_sky", "end_portal", "world_border", "beacon_beam", "fishing_hook", "me_brewing", "me_cauldron",
                     "weather_rain", "weather_snow", "weather_lightning", "item_banner", "missing" })
            r.Skip.Add(n);
        return r;
    }

    // ================= skrzynie =================

    public enum ChestPart { Front, Side, Top, Lock }

    /// <summary>Tekstura modelu skrzyni 1.8 (64x64 albo wielokrotność) jako próbnik: piksel UV (u,v) plus część piksela
    /// (su,sv w 0..1) dla tekstur HD; null = zły rozmiar albo brak.</summary>
    private static Func<int, int, float, float, uint> ChestSampler(RawImage src)
    {
        if (src?.RequireSize(i => i.W == i.H && i.W >= 64 && i.W % 64 == 0, "kwadrat 64x64 albo wielokrotność") == null) return null;
        int k = src.W / 64;
        return (u, v, su, sv) => src.At(u * k + Math.Min(k - 1, (int)(su * k)), v * k + Math.Min(k - 1, (int)(sv * k)));
    }

    /// <summary>Kafel size x size: ściana modelPx x modelPx rozciągnięta na piksele atlasu; pixel(u, v, su, sv)
    /// daje kolor dla pozycji w modelu (u,v) i części piksela (su, sv w 0..1).</summary>
    private static uint[] ChestTile(int size, int modelPx, Func<int, int, float, float, uint> pixel)
    {
        var p = new uint[size * size];
        for (int Y = 0; Y < size; Y++)
            for (int X = 0; X < size; X++)
            {
                float fu = (X + .5f) * modelPx / size, fv = (Y + .5f) * modelPx / size;
                int u = (int)fu, v = (int)fv;
                p[Y * size + X] = pixel(u, v, fu - u, fv - v);
            }
        return p;
    }

    /// <summary>Ściana zwykłej skrzyni (model gry: jeden prostopadłościan 14x14x14 z pełnym UV na ścianę):
    /// 5 wierszy wieka i 9 wierszy podstawy jak w ModelChest, na przodzie zamek 2x4; 14x14 rozciągnięte do size x size.</summary>
    private static uint[] ChestFace(RawImage src, ChestPart part, int size)
    {
        var S = ChestSampler(src); if (S == null) return null;
        int u0 = part == ChestPart.Side ? 0 : 14;
        return ChestTile(size, 14, (x, y, su, sv) => part == ChestPart.Front && x is 6 or 7 && y is >= 3 and <= 6 ? S(1 + x - 6, 1 + y - 3, su, sv) // zamek: przód 2x4 od (1,1)
            : part == ChestPart.Top ? S(14 + x, y, su, sv)
            : y < 5 ? S(u0 + x, 14 + y, su, sv)        // wieko: ściana 14x5 od (u0,14)
            : S(u0 + x, 33 + y - 4, su, sv));          // podstawa: ściana 14x10 od (u0,33), wiersze 1-9 pod wiekiem
    }

    /// <summary>Kafle skrzyni Endu w układzie modelu gry (UV wg pozycji, jak EnderChestTextures), size x size.</summary>
    private static uint[] EnderChestTile(RawImage src, ChestPart part, int size)
    {
        var S = ChestSampler(src); if (S == null) return null;
        return ChestTile(size, 16, (x, y, su, sv) =>
        {
            int cx = Math.Clamp(x - 1, 0, 13);
            float lx = (x + su) * 2 / 16, ly = (y + sv) * 4 / 16;
            return part switch
            {
                ChestPart.Front => y <= 6 ? S(14 + cx, 14 + Math.Clamp(y - 2, 0, 4), su, sv) : S(14 + cx, 33 + Math.Clamp(y - 6, 0, 9), su, sv),
                ChestPart.Side => y <= 6 ? S(cx, 14 + Math.Clamp(y - 2, 0, 4), su, sv) : S(cx, 33 + Math.Clamp(y - 6, 0, 9), su, sv),
                ChestPart.Top => S(14 + Math.Clamp(x - 1, 0, 13), Math.Clamp(y - 1, 0, 13), su, sv),
                _ => S(1 + Math.Clamp((int)lx, 0, 1), 1 + Math.Clamp((int)ly, 0, 3), lx - (int)lx, ly - (int)ly),
            };
        });
    }

    // ================= przekształcenia =================

    private static uint[] GrassSide(PackSource s, uint[] b)
    {
        if (s.Still("blocks/grass_side_overlay") is { } o)
            for (int i = 0; i < Math.Min(b.Length, o.Length); i++) if (A(o[i]) >= 128) b[i] = WithA(o[i], 250);
        return b;
    }
    /// <summary>Barwi piksele oznaczone do zabarwienia (alfa &lt; 252) i robi je nieprzezroczystymi.</summary>
    private static uint[] TintMarked(uint[] p, uint rgb)
    {
        if (p == null) return null;
        for (int i = 0; i < p.Length; i++) if (A(p[i]) < 252 && A(p[i]) > 0) p[i] = WithA(Tinted(p[i], rgb), 255);
        return p;
    }
    private static uint Tinted(uint c, uint rgb) => Rgba(R(c) * (int)(rgb >> 16 & 255) / 255, G(c) * (int)(rgb >> 8 & 255) / 255, Bl(c) * (int)(rgb & 255) / 255, A(c));
    private static uint[] Tinted(uint[] p, uint rgb) { for (int i = 0; i < p.Length; i++) p[i] = Tinted(p[i], rgb); return p; }
    private static uint[] Opaque(uint[] p) { for (int i = 0; i < p.Length; i++) p[i] = WithA(p[i], 255); return p; }
    /// <summary>Nakłada src na dst (zwykłe mieszanie alfa).</summary>
    private static void Over(uint[] dst, uint[] src)
    {
        for (int i = 0; i < dst.Length; i++)
        {
            int sa = A(src[i]); if (sa == 0) continue;
            float t = sa / 255f;
            dst[i] = Rgba((int)(R(dst[i]) + (R(src[i]) - R(dst[i])) * t), (int)(G(dst[i]) + (G(src[i]) - G(dst[i])) * t),
                (int)(Bl(dst[i]) + (Bl(src[i]) - Bl(dst[i])) * t), Math.Max(A(dst[i]), sa));
        }
    }
    /// <summary>Obrót kwadratowej tekstury (16x16 albo HD) o rot * 90 stopni.</summary>
    public static uint[] Rotate(uint[] s, int rot)
    {
        int n = (int)Math.Sqrt(s.Length);
        var d = new uint[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int sx = x, sy = y;
                for (int k = 0; k < rot; k++) { int t = sx; sx = sy; sy = n - 1 - t; }
                d[y * n + x] = s[sy * n + sx];
            }
        return d;
    }

    // ================= odczyt PNG i .mcmeta =================

    public sealed class PackImage
    {
        public List<uint[]> Frames;
        public uint[] Still;
        public PackAnim Anim;
        /// <summary>Szerokość klatki w pliku (16 dla 1.8, 32/64/... dla paczek HD).</summary>
        public int NativeSize;
    }

    /// <summary>Obrazy z paczek z pamięcią podręczną na czas jednego nakładania.</summary>
    public sealed class PackSource
    {
        private readonly IReadOnlyList<Func<string, byte[]>> _packs;
        private readonly Dictionary<string, PackImage> _cache = new();
        /// <summary>Bok tekstur wynikowych: 16 (atlas gry) albo rozmiar atlasu HD.</summary>
        public readonly int Size;
        /// <summary>Największa rozdzielczość tekstury z paczki w przeliczeniu na blok (np. 32 dla paczki 32x).</summary>
        public int MaxNative { get; private set; } = 16;
        public PackSource(IReadOnlyList<Func<string, byte[]>> packs, int size = 16) { _packs = packs; Size = size; }
        private void Native(int perBlock) => MaxNative = Math.Max(MaxNative, perBlock);

        /// <summary>Obraz "blocks/stone" albo null, gdy żadna paczka go nie ma.</summary>
        public PackImage Img(string path)
        {
            if (_packs == null) return null;
            if (_cache.TryGetValue(path, out var r)) return r;
            _cache[path] = r = ReadFromFirstPackWith(path);
            return r;
        }

        /// <summary>PNG z pierwszej paczki, która go ma, razem z .mcmeta z tej samej paczki.</summary>
        private PackImage ReadFromFirstPackWith(string path)
        {
            foreach (var read in _packs)
            {
                var png = read(TexturesRoot + path + ".png");
                if (png != null)
                {
                    var img = DecodePackImage(png, read(TexturesRoot + path + ".png.mcmeta"), path, Size);
                    if (img != null) Native(img.NativeSize);
                    return img;
                }
            }
            return null;
        }
        private readonly Dictionary<string, RawImage> _raw = new();
        /// <summary>Cały obraz bez dzielenia na klatki (np. arkusz cząsteczek), z pierwszej paczki, która go ma.
        /// vanillaW: szerokość w 1.8 (do wyliczenia rozdzielczości HD); 0 = nie liczy się do rozmiaru atlasu.</summary>
        public RawImage Raw(string path, int vanillaW = 0)
        {
            if (_packs == null) return null;
            if (!_raw.TryGetValue(path, out var r))
            {
                foreach (var read in _packs)
                    if (read(TexturesRoot + path + ".png") is { } png) { r = RawImage.Decode(png, path); break; }
                _raw[path] = r;
            }
            if (r != null && vanillaW > 0) Native(r.W * 16 / vanillaW);
            return r;
        }
        private readonly Dictionary<string, RawImage> _sheets = new();
        /// <summary>Arkusz grid x grid komórek; złe wymiary: jeden wpis w logu i null (cząsteczki jak bez paczki).</summary>
        private RawImage Sheet(string path, int grid)
        {
            if (_sheets.TryGetValue(path, out var s)) return s;
            return _sheets[path] = Raw(path)?.RequireSize(i => i.W == i.H && i.W % grid == 0, $"kwadrat podzielny na {grid}x{grid} komórek");
        }
        /// <summary>Komórka index z arkusza grid x grid (np. particle/particles: 16x16 komórek), przeskalowana do 16x16.</summary>
        public uint[] Cell(string path, int grid, int index)
        {
            var img = Sheet(path, grid);
            if (img == null) return null;
            int cs = img.W / grid;
            var cell = img.Crop(index % grid * cs, index / grid * cs, cs, cs);
            return ScaleTo(cell.Px, cs, 0, Size);
        }
        public uint[] Still(string path) => Img(path)?.Still is { } s ? (uint[])s.Clone() : null;
        public uint[] Frame(string path, int i)
        {
            var img = Img(path); if (img == null) return null;
            int n = img.Frames.Count;
            return (uint[])img.Frames[(i % n + n) % n].Clone();
        }
    }

    /// <summary>PNG z paczki -> klatki size x size (domyślnie 16). Tekstura N razy wyższa niż szeroka to pasek N klatek (animacja z .mcmeta).</summary>
    public static PackImage DecodePackImage(byte[] png, byte[] mcmeta, string path, int size = 16)
    {
        StbImageSharp.ImageResult img;
        try { img = StbImageSharp.ImageResult.FromMemory(png, StbImageSharp.ColorComponents.RedGreenBlueAlpha); }
        catch (Exception ex) { Log.Error("Paczka zasobów: " + path + ": " + ex.Message); return null; }
        int w = img.Width, h = img.Height;
        if (w <= 0 || h < w || h % w != 0) { Log.Error($"Paczka zasobów: {path}: nieobsługiwany rozmiar {w}x{h}"); return null; }
        var px = new uint[w * h];
        Buffer.BlockCopy(img.Data, 0, px, 0, Math.Min(img.Data.Length, px.Length * 4));
        int n = h / w;
        var frames = new List<uint[]>(n);
        for (int f = 0; f < n; f++) frames.Add(ScaleTo(px, w, f * w * w, size));
        AnimStep[] steps;
        try { steps = AnimationSequence(mcmeta, n); }
        catch (JsonException ex) { Log.Error("Paczka zasobów: " + path + ".mcmeta: " + ex.Message); return null; }
        var r = new PackImage { Frames = frames, Still = frames[steps?[0].Frame ?? 0], NativeSize = w };
        if (steps != null && steps.Select(st => st.Frame).Distinct().Skip(1).Any()) r.Anim = new PackAnim(frames, steps);
        return r;
    }

    /// <summary>Kroki animacji z sekcji "animation" w .mcmeta (frametime, frames, {index,time}).
    /// Pusta lub brakująca lista frames = wszystkie klatki po kolei. Null = obraz statyczny.
    /// Uszkodzony .mcmeta, błędny indeks klatki albo pasek klatek bez sekcji "animation"
    /// rzuca JsonException, żeby tekstura została generowana.</summary>
    public static AnimStep[] AnimationSequence(byte[] mcmeta, int frameCount)
    {
        var an = AnimationSection(mcmeta);
        if (an == null)
        {
            if (frameCount > 1) throw new JsonException("pasek klatek bez sekcji \"animation\" w .mcmeta");
            return null;
        }
        int frametime = an.Value.TryGetProperty("frametime", out var ft) && ft.TryGetInt32(out int fti) ? Math.Max(fti, 1) : 1;
        var seq = new List<AnimStep>();
        void AddFrame(int index, int time)
        {
            if (index < 0 || index >= frameCount) throw new JsonException($"indeks klatki {index} poza zakresem 0..{frameCount - 1}");
            seq.Add(new AnimStep(index, Math.Max(time, 1)));
        }
        bool hasFrames = an.Value.TryGetProperty("frames", out var fr);
        if (hasFrames && fr.ValueKind != JsonValueKind.Array) throw new JsonException("\"frames\" nie jest tablicą");
        if (hasFrames)
        {
            foreach (var e in fr.EnumerateArray())
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out int idx)) AddFrame(idx, frametime);
                else if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("index", out var ie) && ie.TryGetInt32(out int idx2))
                    AddFrame(idx2, e.TryGetProperty("time", out var te) && te.TryGetInt32(out int tt) ? tt : frametime);
                else throw new JsonException("nieprawidłowy wpis w \"frames\"");
            }
        }
        if (seq.Count == 0) for (int i = 0; i < frameCount; i++) AddFrame(i, frametime);
        return seq.ToArray();
    }

    /// <summary>Sekcja "animation" z .mcmeta albo null, gdy pliku lub sekcji nie ma.</summary>
    private static JsonElement? AnimationSection(byte[] mcmeta)
    {
        if (mcmeta == null) return null;
        using var doc = JsonDocument.Parse(mcmeta, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (!doc.RootElement.TryGetProperty("animation", out var an)) return null;
        if (an.ValueKind != JsonValueKind.Object) throw new JsonException("\"animation\" nie jest obiektem");
        return an.Clone();
    }

    /// <summary>Klatka w×w (od przesunięcia off) do 16×16.</summary>
    public static uint[] ScaleTo16(uint[] src, int w, int off = 0) => ScaleTo(src, w, off, 16);

    /// <summary>Klatka w×w (od przesunięcia off) do size×size: większe uśredniane blokami (tekstury wycinane
    /// alfą zostają ostre), mniejsze powiększane bez wygładzania.</summary>
    public static uint[] ScaleTo(uint[] src, int w, int off, int size)
    {
        var d = new uint[size * size];
        if (w == size) { Array.Copy(src, off, d, 0, size * size); return d; }
        if (w < size || w % size != 0)
        {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) d[y * size + x] = src[off + y * w / size * w + x * w / size];
            return d;
        }
        int k = w / size;
        bool binary = true;
        for (int i = 0; i < w * w && binary; i++) { int a = (int)(src[off + i] >> 24); binary = a == 0 || a == 255; }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                long r = 0, g = 0, b = 0, aw = 0; int opaque = 0;
                for (int j = 0; j < k; j++)
                    for (int i = 0; i < k; i++)
                    {
                        uint c = src[off + (y * k + j) * w + x * k + i];
                        int a = A(c); if (a > 0) opaque++;
                        r += R(c) * a; g += G(c) * a; b += Bl(c) * a; aw += a;
                    }
                int n = k * k;
                int outA = binary ? (opaque * 2 >= n ? 255 : 0) : (int)(aw / n);
                d[y * size + x] = aw == 0 ? 0 : Rgba((int)(r / aw), (int)(g / aw), (int)(b / aw), outA);
            }
        return d;
    }
}
