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
    public static List<int> ApplyPacks(IReadOnlyList<Func<string, byte[]>> packs)
    {
        if (packs is { Count: 0 }) packs = null;
        Ensure();
        lock (Lock)
        {
            _generated ??= _tex.Select(p => (uint[])p.Clone()).ToArray();
            var ctx = new PackSource(packs);
            var changed = new List<int>();
            PackAnimations.Clear(); PackOverridden.Clear();
            foreach (var (name, i) in _index)
            {
                uint[] px = null; PackAnim anim = null;
                if (packs != null)
                    try { px = ResolvePackTexture(name, ctx, out anim); }
                    catch (Exception ex) { Log.Error("Paczka zasobów: tekstura " + name + ": " + ex.Message); }
                if (px != null) { PackOverridden.Add(name); if (anim != null) PackAnimations[name] = anim; }
                var target = px ?? _generated[i];
                if (!_tex[i].AsSpan().SequenceEqual(target)) changed.Add(i);
                _tex[i] = target;
            }
            if (PackOverridden.Count > 0) Log.Info($"Paczki zasobów: {PackOverridden.Count} tekstur z paczek, {PackAnimations.Count} animacji");
            return changed;
        }
    }

    private static uint[] ResolvePackTexture(string name, PackSource src, out PackAnim anim)
    {
        anim = null;
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

        // inny układ niż blok 16x16 albo inna paczka plików: kolejne etapy (encje, cząsteczki, GUI, modele)
        foreach (var n in new[] { "chest_top", "chest_side", "chest_front", "trapped_chest_front", "ender_chest_front", "ender_chest_side", "ender_chest_top", "ender_chest_lock",
                     "minecart_side", "minecart_inside", "end_sky", "end_portal", "world_border", "beacon_beam", "fishing_hook", "me_brewing", "me_cauldron",
                     "weather_rain", "weather_snow", "weather_lightning", "item_banner", "missing" })
            r.Skip.Add(n);
        return r;
    }

    // ================= przekształcenia =================

    private static uint[] GrassSide(PackSource s, uint[] b)
    {
        if (s.Still("blocks/grass_side_overlay") is { } o)
            for (int i = 0; i < 256; i++) if (A(o[i]) >= 128) b[i] = WithA(o[i], 250);
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
    /// <summary>Obrót 16x16 o rot * 90 stopni.</summary>
    public static uint[] Rotate(uint[] s, int rot)
    {
        var d = new uint[256];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                int sx = x, sy = y;
                for (int k = 0; k < rot; k++) { int t = sx; sx = sy; sy = 15 - t; }
                d[y * 16 + x] = s[sy * 16 + sx];
            }
        return d;
    }

    // ================= odczyt PNG i .mcmeta =================

    public sealed class PackImage
    {
        public List<uint[]> Frames;
        public uint[] Still;
        public PackAnim Anim;
    }

    /// <summary>Obrazy z paczek z pamięcią podręczną na czas jednego nakładania.</summary>
    public sealed class PackSource
    {
        private readonly IReadOnlyList<Func<string, byte[]>> _packs;
        private readonly Dictionary<string, PackImage> _cache = new();
        public PackSource(IReadOnlyList<Func<string, byte[]>> packs) { _packs = packs; }

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
                if (png != null) return DecodePackImage(png, read(TexturesRoot + path + ".png.mcmeta"), path);
            }
            return null;
        }
        private readonly Dictionary<string, RawImage> _raw = new();
        /// <summary>Cały obraz bez dzielenia na klatki (np. arkusz cząsteczek), z pierwszej paczki, która go ma.</summary>
        public RawImage Raw(string path)
        {
            if (_packs == null) return null;
            if (_raw.TryGetValue(path, out var r)) return r;
            foreach (var read in _packs)
                if (read(TexturesRoot + path + ".png") is { } png) { r = RawImage.Decode(png, path); break; }
            return _raw[path] = r;
        }
        /// <summary>Komórka index z arkusza grid x grid (np. particle/particles: 16x16 komórek), przeskalowana do 16x16.</summary>
        public uint[] Cell(string path, int grid, int index)
        {
            var img = Raw(path);
            if (img == null || img.W != img.H || img.W % grid != 0) return null;
            int cs = img.W / grid;
            var cell = img.Crop(index % grid * cs, index / grid * cs, cs, cs);
            return ScaleTo16(cell.Px, cs);
        }
        public uint[] Still(string path) => Img(path)?.Still is { } s ? (uint[])s.Clone() : null;
        public uint[] Frame(string path, int i)
        {
            var img = Img(path); if (img == null) return null;
            int n = img.Frames.Count;
            return (uint[])img.Frames[(i % n + n) % n].Clone();
        }
    }

    /// <summary>PNG z paczki -> klatki 16x16. Tekstura N razy wyższa niż szeroka to pasek N klatek (animacja z .mcmeta).</summary>
    public static PackImage DecodePackImage(byte[] png, byte[] mcmeta, string path)
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
        for (int f = 0; f < n; f++) frames.Add(ScaleTo16(px, w, f * w * w));
        AnimStep[] steps;
        try { steps = AnimationSequence(mcmeta, n); }
        catch (JsonException ex) { Log.Error("Paczka zasobów: " + path + ".mcmeta: " + ex.Message); return null; }
        var r = new PackImage { Frames = frames, Still = frames[steps?[0].Frame ?? 0] };
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

    /// <summary>Klatka w×w (od przesunięcia off) do 16×16: większe uśredniane blokami (tekstury wycinane
    /// alfą zostają ostre), mniejsze powiększane bez wygładzania.</summary>
    public static uint[] ScaleTo16(uint[] src, int w, int off = 0)
    {
        var d = new uint[256];
        if (w == 16) { Array.Copy(src, off, d, 0, 256); return d; }
        if (w < 16 || w % 16 != 0)
        {
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) d[y * 16 + x] = src[off + y * w / 16 * w + x * w / 16];
            return d;
        }
        int k = w / 16;
        bool binary = true;
        for (int i = 0; i < w * w && binary; i++) { int a = (int)(src[off + i] >> 24); binary = a == 0 || a == 255; }
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
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
                d[y * 16 + x] = aw == 0 ? 0 : Rgba((int)(r / aw), (int)(g / aw), (int)(b / aw), outA);
            }
        return d;
    }
}
