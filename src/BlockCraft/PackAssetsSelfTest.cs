using BlockCraft.Core;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Etap 3 paczek: arkusz cząsteczek, mapy kolorów biomów, operacje na obrazach GUI.</summary>
public static partial class SelfTest
{
    private static void TestPackAssets()
    {
        Console.WriteLine("[Paczki zasobów: GUI, cząsteczki, kolory]");
        // arkusz 128x128: komórka = 8x8, kolor = numer komórki
        var sheet = Png(128, 128, (x, y) => 0xFF000000 | (uint)(y / 8 * 16 + x / 8));
        var files = new Dictionary<string, byte[]> { [TextureGen.TexturesRoot + "particle/particles.png"] = sheet };
        try
        {
            TextureGen.ApplyPacks(p => files.GetValueOrDefault(p));
            Check((TextureGen.Get("particle_heart")[0] & 0xFF) == 80, "Serce z komórki 80 arkusza cząsteczek");
            Check((TextureGen.Get("particle_smoke_0")[0] & 0xFF) == 7 && (TextureGen.Get("particle_smoke_7")[0] & 0xFF) == 0, "Dym: klatka 0 = największa (komórka 7)");
            Check((TextureGen.Get("particle_firework_3")[5 * 16 + 5] & 0xFF) == 163, "Iskry fajerwerków: komórki 160+");
        }
        finally { TextureGen.ApplyPacks((Func<string, byte[]>)null); }

        uint plainsBefore = Biome.GrassColor(Biome.Plains);
        try
        {
            var map = new uint[256 * 256];
            for (int i = 0; i < map.Length; i++) map[i] = 0xFF000000 | (uint)(i & 255) << 16 | (uint)(i >> 8);
            Biome.GrassMap = map;
            var d = Biome.Get(Biome.Plains);
            int x = (int)((1 - d.Temp) * 255), y = (int)((1 - d.Rain * d.Temp) * 255);
            uint c = Biome.GrassColor(Biome.Plains);
            Check(c == (uint)((y << 16) | (x & 255)), "Trawa z colormap/grass.png wg temperatury i wilgotności", c.ToString("X6"));
            Check(Biome.GrassColor(Biome.Swampland) == 0x6A7039, "Bagno: stały kolor jak w 1.8");
        }
        finally { Biome.GrassMap = null; }
        Check(Biome.GrassColor(Biome.Plains) == plainsBefore, "Bez mapy kolorów: tabela gry");

        var img = new RawImage { W = 2, H = 2, Px = new uint[] { 1, 2, 3, 4 } };
        var up = img.Upscale(2);
        Check(up.W == 4 && up.Px[0] == 1 && up.Px[3] == 2 && up.Px[15] == 4, "Powiększenie obrazu GUI bez wygładzania");
        var dst = new RawImage { W = 4, H = 4, Px = new uint[16] };
        dst.Paste(up, 2, 0, 2, 2);
        Check(dst.Px[2] == 2 && dst.Px[0] == 0, "Wklejanie obszaru z paczki");
    }
}
