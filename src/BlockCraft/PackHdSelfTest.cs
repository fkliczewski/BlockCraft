using BlockCraft.Render;

namespace BlockCraft;

/// <summary>Etap 2 paczek: atlas terenu w rozdzielczości paczki HD (limit, powiększanie tekstur gry, animacje, skrzynie).</summary>
public static partial class SelfTest
{
    private static void TestPackHd()
    {
        Console.WriteLine("[Paczki zasobów: HD]");
        const string T = TextureGen.TexturesRoot;
        byte[] J(string s) => System.Text.Encoding.UTF8.GetBytes(s);
        var files = new Dictionary<string, byte[]>
        {
            [T + "blocks/stone.png"] = Png(32, 32, (x, y) => (x + y) % 2 == 0 ? 0xFF0000FFu : 0xFF00FF00u),       // szachownica 1 px
            [T + "blocks/water_still.png"] = Png(32, 64, (x, y) => y < 32 ? 0xFFFF0000u : 0xFF00FFFFu),
            [T + "blocks/water_still.png.mcmeta"] = J("{\"animation\":{}}"),
            [T + "blocks/rail_normal_turned.png"] = Png(32, 32, (x, y) => x == 0 && y == 0 ? 0xFFFFFFFFu : 0u),
            [T + "entity/chest/normal.png"] = Png(128, 128, (x, y) => 0xFF808080u),
        };
        int maxBefore = TextureGen.MaxAtlasSize;
        try
        {
            TextureGen.MaxAtlasSize = 64;
            TextureGen.ApplyPacks(p => files.GetValueOrDefault(p));
            Check(TextureGen.AtlasSize == 32, "Atlas w rozdzielczości paczki (32x)", TextureGen.AtlasSize.ToString());
            var layers = TextureGen.AtlasLayers();
            var stone = layers[TextureGen.Layer("stone")];
            Check(stone.Length == 32 * 32 && stone[0] == 0xFF0000FF && stone[1] == 0xFF00FF00, "Kamień HD: piksele 1:1 bez uśredniania");
            Check((TextureGen.Get("stone")[0] & 0xFF) > 0, "Atlas 16x16 dla gry dalej jest (zmniejszony)");
            var cobble = layers[TextureGen.Layer("cobblestone")];
            var cobble16 = TextureGen.Get("cobblestone");
            Check(cobble.Length == 32 * 32 && cobble[0] == cobble16[0] && cobble[1] == cobble16[0] && cobble[32 * 2 + 2] == cobble16[17], "Tekstura gry powiększona 2x bez wygładzania");
            Check(TextureGen.AtlasAnimations.TryGetValue("water_still", out var w) && w.Frames[0].Length == 32 * 32, "Animacja wody w rozdzielczości atlasu");
            var rail = layers[TextureGen.Layer("rail_turn_8")];
            Check(rail[32 * 32 - 1] == 0xFFFFFFFF, "Obrót szyn w HD");
            Check(layers[TextureGen.Layer("chest_front")].Length == 32 * 32, "Skrzynia HD składana z modelu 128x128");
            Check(TextureGen.ToAtlas(TextureGen.Animations["lava_still"][0]).Length == 32 * 32, "Wygenerowane klatki lawy powiększane do atlasu");

            TextureGen.MaxAtlasSize = 16;
            TextureGen.ApplyPacks(p => files.GetValueOrDefault(p));
            Check(TextureGen.AtlasSize == 16 && TextureGen.AtlasLayers()[TextureGen.Layer("stone")].Length == 256, "Limit 16: paczka HD zmniejszona jak dotąd");
        }
        finally
        {
            TextureGen.MaxAtlasSize = maxBefore;
            TextureGen.ApplyPacks((Func<string, byte[]>)null);
        }
        Check(TextureGen.AtlasSize == 16, "Bez paczek: atlas 16x16");
    }
}
