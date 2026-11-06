using BlockCraft.Core;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Etap 6 paczek: blockstates i modele JSON (parent, tekstury, obroty, cullface, warianty losowe, nowe tekstury).</summary>
public static partial class SelfTest
{
    private static void TestPackModels()
    {
        Console.WriteLine("[Paczki zasobów: modele JSON]");
        Items.ItemRegistry.Init();
        TestPackElementTransforms();
        TestPackStateMapping();
        byte[] J(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        const string M = "assets/minecraft/models/block/", S = "assets/minecraft/blockstates/";
        var vanilla = new Dictionary<string, byte[]>
        {
            [M + "cube_all.json"] = J("{\"textures\":{\"particle\":\"#all\"},\"elements\":[{\"from\":[0,0,0],\"to\":[16,16,16],\"faces\":{\"down\":{\"texture\":\"#all\",\"cullface\":\"down\"},\"up\":{\"texture\":\"#all\",\"cullface\":\"up\"},\"north\":{\"texture\":\"#all\",\"cullface\":\"north\"},\"south\":{\"texture\":\"#all\",\"cullface\":\"south\"},\"west\":{\"texture\":\"#all\",\"cullface\":\"west\"},\"east\":{\"texture\":\"#all\",\"cullface\":\"east\"}}}]}"),
            [M + "dirt.json"] = J("{\"parent\":\"block/cube_all\",\"textures\":{\"all\":\"blocks/dirt\"}}"),
            [S + "dirt.json"] = J("{\"variants\":{\"normal\":{\"model\":\"dirt\"}}}"),
            [S + "oak_log.json"] = J("{\"variants\":{\"axis=y\":{\"model\":\"oak_log\"},\"axis=x\":{\"model\":\"oak_log\",\"x\":90,\"y\":90},\"axis=z\":{\"model\":\"oak_log\",\"x\":90},\"axis=none\":{\"model\":\"oak_log\"}}}"),
            [M + "oak_log.json"] = J("{\"parent\":\"block/cube_all\",\"textures\":{\"all\":\"blocks/log_oak\"}}"),
        };
        var pack = new Dictionary<string, byte[]>
        {
            // płyta 16x8x16 z nową teksturą; ziemia losuje dwa warianty
            [M + "half.json"] = J("{\"textures\":{\"t\":\"blocks/nowa_tekstura\"},\"elements\":[{\"from\":[0,0,0],\"to\":[16,8,16],\"faces\":{\"up\":{\"texture\":\"#t\"},\"north\":{\"texture\":\"#t\",\"cullface\":\"north\",\"tintindex\":0}}}]}"),
            [S + "dirt.json"] = J("{\"variants\":{\"normal\":[{\"model\":\"half\"},{\"model\":\"half\",\"y\":90,\"weight\":3}]}}"),
            [M + "oak_log.json"] = J("{\"parent\":\"block/cube_all\",\"textures\":{\"all\":\"blocks/log_birch\"},\"elements\":[{\"from\":[0,0,0],\"to\":[16,16,2],\"faces\":{\"north\":{\"texture\":\"#all\",\"cullface\":\"north\"}}}]}"),
            [TextureGen.TexturesRoot + "blocks/nowa_tekstura.png"] = Png(16, 16, (x, y) => 0xFFFF00FFu),
        };
        var sources = new List<PackModels.Source> { new(p => pack.GetValueOrDefault(p), false), new(p => vanilla.GetValueOrDefault(p), true) };
        try
        {
            var models = PackModels.Load(sources);
            var paths = models.TexturePaths;
            Check(paths.Contains("blocks/nowa_tekstura") && paths.Contains("blocks/log_birch"), "Tekstury potrzebne modelom", string.Join(",", paths));
            TextureGen.ApplyPacks(new Func<string, byte[]>[] { p => pack.GetValueOrDefault(p), p => vanilla.GetValueOrDefault(p) }, paths);
            PackModels.Activate(models, p => Math.Max(0, TextureGen.LayerForPackPath(p)));
            Check(PackModels.Any, "Modele z paczki włączone");
            var dirt = PackModels.Get(B.Dirt, 0, 0, 0, 0, false);
            Check(dirt != null && dirt.Quads.Count == 2 && dirt.Quads.Any(q => q.Normal == Dir.Up && Math.Abs(q.P[0].Y - .5f) < 1e-4), "Model płyty: górna ściana na wysokości 0.5");
            int newLayer = TextureGen.LayerForPackPath("blocks/nowa_tekstura");
            Check(newLayer > 0 && dirt.Quads[0].Layer == newLayer && TextureGen.Textures[newLayer][0] == 0xFFFF00FF, "Nowa tekstura z paczki na osobnej warstwie atlasu");
            Check(dirt.Quads.Any(q => q.Tint), "tintindex = barwienie biomu");
            var variants = Enumerable.Range(0, 200).Select(i => PackModels.Get(B.Dirt, 0, i * 7, 64, i * 13, false)).Distinct().Count();
            Check(variants == 2 && PackModels.Get(B.Dirt, 0, 5, 64, 9, false) == PackModels.Get(B.Dirt, 0, 5, 64, 9, false), "Warianty losowane stałe dla pozycji");
            var logX = PackModels.Get(B.OakLog, 1, 0, 0, 0, false);
            Check(logX != null && logX.Quads.Count == 1 && logX.Quads[0].CullFace == Dir.Down, "Obrót wariantu jak ModelRotation 1.8 (x90: północ na dół, y90 dół zostawia)", logX?.Quads[0].CullFace.ToString());
            Check(PackModels.Get(B.Cobblestone, 0, 0, 0, 0, false) == null, "Blok bez plików z paczki: geometria gry");
        }
        finally
        {
            PackModels.Activate(PackModels.Load(null), _ => 0);
            TextureGen.ApplyPacks((Func<string, byte[]>)null);
        }
        Check(!PackModels.Any && TextureGen.LayerForPackPath("blocks/nowa_tekstura") < 0, "Bez paczek: brak modeli i dodatkowych warstw");

        // sama paczka 1.8 (wbudowana) nie zmienia geometrii gry
        PackModels.Activate(PackModels.Load(new List<PackModels.Source> { new(p => vanilla.GetValueOrDefault(p), true) }), _ => 0);
        Check(!PackModels.Any, "Wbudowana paczka 1.8: geometria gry");
    }

    private static void TestPackElementTransforms()
    {
        // Independent landmarks for the top face's first corner relative to origin (4,8,12).
        var cases = new[]
        {
            (Axis: "x", Angle: 45f, Expected: new System.Numerics.Vector3(-.25f, 1.25f, -.25f)),
            (Axis: "y", Angle: 45f, Expected: new System.Numerics.Vector3(-1f, .5f, -.5f)),
            (Axis: "z", Angle: 45f, Expected: new System.Numerics.Vector3(-.75f, .25f, -.75f)),
            (Axis: "x", Angle: -22.5f, Expected: new System.Numerics.Vector3(-.25f, .18933983f, -.95710678f)),
            (Axis: "y", Angle: 22.5f, Expected: new System.Numerics.Vector3(-.56066017f, .5f, -.64644661f)),
            (Axis: "z", Angle: -22.5f, Expected: new System.Numerics.Vector3(-.04289322f, .60355339f, -.75f)),
        };
        foreach (var sample in cases)
            foreach (bool rescale in new[] { false, true })
            {
                var elements = new[] { new
                {
                    from = new[] { 0, 0, 0 }, to = new[] { 16, 16, 16 },
                    rotation = new { origin = new[] { 4, 8, 12 }, axis = sample.Axis, angle = sample.Angle, rescale },
                    faces = new { up = new { texture = "blocks/stone" } }
                } };
                using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(elements));
                var model = new PackModelDefinition { Elements = json.RootElement };
                var baked = new PackModelBaker(new List<string>()).Bake(model, 0, 0);
                var expected = sample.Expected;
                if (!rescale)
                {
                    float cosine = MathF.Cos(sample.Angle * MathF.PI / 180f);
                    expected *= sample.Axis switch
                    {
                        "x" => new System.Numerics.Vector3(1, cosine, cosine),
                        "y" => new System.Numerics.Vector3(cosine, 1, cosine),
                        _ => new System.Numerics.Vector3(cosine, cosine, 1)
                    };
                }
                expected += new System.Numerics.Vector3(.25f, .5f, .75f);
                Check(System.Numerics.Vector3.Distance(baked.Quads[0].P[0], expected) < 1e-5f,
                    $"Obrót elementu {sample.Axis} {sample.Angle}, rescale={rescale}");
            }

        var expectedUvs = new[]
        {
            new System.Numerics.Vector2(.125f, .25f), new System.Numerics.Vector2(.125f, .875f),
            new System.Numerics.Vector2(.75f, .875f), new System.Numerics.Vector2(.75f, .25f)
        };
        foreach (int rotation in new[] { 0, 90, 180, 270 })
        {
            string elements = """[{"from":[0,0,0],"to":[16,16,16],"faces":{"up":{"texture":"blocks/stone","uv":[2,4,12,14],"rotation":ROTATION}}}]""".Replace("ROTATION", rotation.ToString());
            using var json = System.Text.Json.JsonDocument.Parse(elements);
            var baked = new PackModelBaker(new List<string>()).Bake(new PackModelDefinition { Elements = json.RootElement }, 0, 0);
            Check(Enumerable.Range(0, 4).All(i => baked.Quads[0].Uv[i] == expectedUvs[(i + rotation / 90) % 4]),
                $"Obrót UV ściany {rotation}");
        }
    }

    private static void TestPackStateMapping()
    {
        var materials = new Dictionary<string, string[]>
        {
            ["stone"] = new[] { "stone", "granite", "smooth_granite", "diorite", "smooth_diorite", "andesite", "smooth_andesite" },
            ["dirt"] = new[] { "dirt", "coarse_dirt", "podzol" },
            ["sand"] = new[] { "sand", "red_sand" },
            ["sandstone"] = new[] { "sandstone", "chiseled_sandstone", "smooth_sandstone" },
            ["red_sandstone"] = new[] { "red_sandstone", "chiseled_red_sandstone", "smooth_red_sandstone" },
            ["stonebrick"] = new[] { "stonebrick", "mossy_stonebrick", "cracked_stonebrick", "chiseled_stonebrick" },
            ["prismarine"] = new[] { "prismarine", "prismarine_bricks", "dark_prismarine" },
            ["quartz_block"] = new[] { "quartz_block", "chiseled_quartz_block", "quartz_column", "quartz_column", "quartz_column" },
            ["red_flower"] = new[] { "poppy", "blue_orchid", "allium", "houstonia", "red_tulip", "orange_tulip", "white_tulip", "pink_tulip", "oxeye_daisy" },
            ["tallgrass"] = new[] { "tall_grass", "tall_grass", "fern" },
            ["monster_egg"] = new[] { "stone_monster_egg", "cobblestone_monster_egg", "stone_brick_monster_egg", "mossy_brick_monster_egg", "cracked_brick_monster_egg", "chiseled_brick_monster_egg" }
        };
        foreach (var (key, files) in materials)
        {
            var block = Block.ById.First(b => b?.Key == key);
            for (int meta = 0; meta < files.Length; meta++)
                Check(PackModelStates.For(block, meta, false)?.File == files[meta], $"Plik modelu {key}:{meta}");
        }
        string[] colors = { "white", "orange", "magenta", "light_blue", "yellow", "lime", "pink", "gray", "silver", "cyan", "purple", "blue", "brown", "green", "red", "black" };
        foreach (string key in new[] { "wool", "stained_glass", "stained_hardened_clay" })
        {
            var block = Block.ById.First(b => b?.Key == key);
            for (int meta = 0; meta < colors.Length; meta++)
                Check(PackModelStates.For(block, meta, false)?.File == colors[meta] + "_" + key, $"Kolor modelu {key}:{meta}");
        }
        foreach (string key in new[] { "grass", "mycelium", "dirt" })
        {
            var block = Block.ById.First(b => b?.Key == key);
            int meta = key == "dirt" ? 2 : 0;
            foreach (bool snowy in new[] { false, true })
            {
                var state = PackModelStates.For(block, meta, snowy).Value;
                var pack = new Dictionary<string, byte[]>
                {
                    ["assets/minecraft/blockstates/" + state.File + ".json"] = System.Text.Encoding.UTF8.GetBytes(
                        "{\"variants\":{\"snowy=false\":{\"model\":\"dry\"},\"snowy=true\":{\"model\":\"snow\"}}}" )
                };
                foreach (string name in new[] { "dry", "snow" })
                    pack["assets/minecraft/models/block/" + name + ".json"] = System.Text.Encoding.UTF8.GetBytes(
                        "{\"elements\":[{\"from\":[0,0,0],\"to\":[16,16,16],\"faces\":{\"up\":{\"texture\":\"blocks/" + name + "\"}}}]}" );
                var loader = new PackModelLoader(new[] { new PackModels.Source(path => pack.GetValueOrDefault(path), false) });
                var baked = loader.Bake(state.File, state.Props);
                Check(baked?.Single().Quads.Single().Texture == "blocks/" + (snowy ? "snow" : "dry"), $"Wariant snowy {key}: {snowy}");
            }
        }
    }

}

