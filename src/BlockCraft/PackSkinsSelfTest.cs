using BlockCraft.Core;
using BlockCraft.Render;

namespace BlockCraft;

/// <summary>Etap 4 paczek: skórki encji (64x32, 64x64, 128x128, HD) i ściany skrzyń z entity/chest.</summary>
public static partial class SelfTest
{
    private static void TestPackSkins()
    {
        Console.WriteLine("[Paczki zasobów: encje]");
        RawImage Solid(int w, int h, uint c) { var p = new uint[w * h]; Array.Fill(p, c); return new RawImage { W = w, H = h, Px = p, Name = "test" }; }
        var images = new Dictionary<string, RawImage>
        {
            ["entity/skeleton/skeleton.png"] = Solid(64, 32, 0xFF0000FF),
            ["entity/zombie/zombie.png"] = Solid(128, 128, 0xFF00FF00),       // HD 2x
            ["entity/horse/horse_white.png"] = Solid(128, 128, 0xFFFF0000),
            ["entity/cow/cow.png"] = Solid(64, 20, 0xFFFFFFFF),               // zły rozmiar
            ["entity/witch.png"] = Solid(64, 128, 0xFFFF00FF),                // 64x128 jak w 1.8
        };
        try
        {
            SkinGen.Rebuild(null);
            var baseLayers = SkinGen.Layers();
            int sk = SkinGen.Get("skeleton"), zo = SkinGen.Get("zombie"), ho = SkinGen.Get("horse_white"), cow = SkinGen.Get("cow"), wi = SkinGen.Get("witch");
            SkinGen.Rebuild(p => images.GetValueOrDefault(p));
            var layers = SkinGen.Layers();
            int T = SkinGen.TexSize;
            Check(layers[sk][10 * T + 10] == 0xFF0000FF && layers[sk][63 * T + 127] == 0xFF0000FF, "Szkielet 64x32: górna połowa płótna z paczki");
            Check(layers[sk][100 * T + 10] == baseLayers[sk][100 * T + 10], "Szkielet: dolna połowa zostaje z gry");
            Check(layers[zo][127 * T + 127] == 0xFF00FF00, "Zombie HD 128x128: całe płótno");
            Check(layers[ho][127 * T + 127] == 0xFFFF0000, "Koń 128x128 na płótnie 128");
            Check(layers[cow].AsSpan().SequenceEqual(baseLayers[cow]), "Zły rozmiar tekstury: skórka gry");
            Check(layers[wi][(T - 1) * T + T / 2 - 1] == 0xFFFF00FF, "Wiedźma 64x128 na płótnie 128");
            SkinGen.Rebuild(null);
            Check(SkinGen.Layers()[sk].AsSpan().SequenceEqual(baseLayers[sk]), "Bez paczki: skórki gry");
            var unknown = SkinGen.PackFiles.Keys.Where(k => !SkinGen.Index.ContainsKey(k)).ToList();
            Check(unknown.Count == 0, "Przypisania skórek: same znane skórki", string.Join(",", unknown));
        }
        finally { SkinGen.Rebuild(null); }

        // skrzynia: wieko (u 14.., v 14..18) zielone, podstawa (v 33..) niebieska, zamek (1..2, 1..4) biały
        var chest = Png(64, 64, (x, y) => x >= 1 && x <= 2 && y >= 1 && y <= 4 ? 0xFFFFFFFF : y >= 14 && y < 19 ? 0xFF00FF00 : y >= 33 && y < 43 ? 0xFFFF0000 : 0xFF808080);
        var files = new Dictionary<string, byte[]> { [TextureGen.TexturesRoot + "entity/chest/normal.png"] = chest };
        try
        {
            TextureGen.ApplyPacks(p => files.GetValueOrDefault(p));
            var front = TextureGen.Get("chest_front");
            Check(front[0] == 0xFF00FF00 && front[15 * 16] == 0xFFFF0000, "Przód skrzyni: wieko u góry, podstawa niżej");
            Check(front[5 * 16 + 7] == 0xFFFFFFFF, "Przód skrzyni: zamek z tekstury modelu");
            Check(!TextureGen.PackOverridden.Contains("ender_chest_front"), "Skrzynia Endu bez pliku: tekstura gry");
        }
        finally { TextureGen.ApplyPacks((Func<string, byte[]>)null); }
    }
}
