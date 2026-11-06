using BlockCraft.Core;
using BlockCraft.Render;

namespace BlockCraft;

/// <summary>Czcionka Unicode 1.8: szerokości z glyph_sizes.bin jak FontRenderer i odczyt z client.jar (bez GL).</summary>
public static partial class SelfTest
{
    private static void TestUnicodeFont()
    {
        Console.WriteLine("[Czcionka Unicode]");
        Check(UnicodeFont.Columns(0) == null && UnicodeFont.CharWidth(0) == 0, "Bajt 0 = brak znaku");
        // kolumny 2..6 (0x26): (6 + 1 - 2) / 2 = 2 jak getCharWidth
        Check(UnicodeFont.Columns(0x26) == (2, 7) && UnicodeFont.CharWidth(0x26) == 2, "Szerokość ze starszych i młodszych 4 bitów", UnicodeFont.CharWidth(0x26).ToString());
        Check(UnicodeFont.PagePath(0x04) == "assets/minecraft/textures/font/unicode_page_04.png", "Ścieżka strony (szesnastkowo)");
        // młodsze bity > 7: getCharWidth 1.8 bierze kolumny 0..15, czyli 16 / 2 = 8 (+1 odstępu)
        Check(UnicodeFont.CharWidth(0x0E) == 8 && UnicodeFont.CharWidth(0x3F) == 8 && UnicodeFont.CharWidth(0x07) == 4,
            "Szerokość dla młodszych bitów > 7 jak getCharWidth 1.8", $"{UnicodeFont.CharWidth(0x0E)} {UnicodeFont.CharWidth(0x3F)} {UnicodeFont.CharWidth(0x07)}");
        var fake = new byte[65536]; fake['Ж'] = 0x0E; fake['ə'] = 0x15;
        var font = new UnicodeFont(p => p == UnicodeFont.SizesPath ? fake : null);
        Check(font.Available && font.Width('Ж') == 8 && font.Width('ə') == 2 && font.Width('ㅎ') == null, "Szerokości z podanego glyph_sizes.bin",
            $"{font.Width('Ж')} {font.Width('ə')}");
        var none = new UnicodeFont(_ => null);
        Check(!none.Available && none.Width('Ж') == null, "Bez plików: brak znaków (zostaje ?)");
        bool jarReady = false; var packSizes = new byte[65536]; packSizes['Ж'] = 0x07;
        using (var provider = new UnicodeFontProvider(p => p == UnicodeFont.SizesPath ? (jarReady ? fake : null) : null, () => jarReady))
        {
            var before = provider.Font;
            Check(!before.Available && provider.Font == before, "Dostawca: czcionka tworzona raz, zanim client.jar gotowy");
            jarReady = true;
            var after = provider.Font;
            Check(after != before && after.Width('Ж') == 8 && provider.Font == after, "Dostawca: po pobraniu client.jar tworzona raz na nowo");
        }
        using (var provider = new UnicodeFontProvider(p => p == UnicodeFont.SizesPath ? packSizes : null, () => true))
            Check(provider.Font.Width('Ж') == 4, "Dostawca: szerokości z podanego źródła");
        if (ClientAssets.TryReadFile(UnicodeFont.SizesPath) is { } jarSizes)
        {
            var jar = new UnicodeFont(ClientAssets.TryReadFile);
            Check(jarSizes.Length == 65536 && jar.Width('Ж') > 0 && jar.Width('ə') > 0 && jar.Width('中') > 0, "client.jar: cyrylica, ə i chiński mają szerokości",
                $"{jar.Width('Ж')} {jar.Width('ə')} {jar.Width('中')}");
            Check("Жə中".All(c => jar.Width(c) + 1 == GetCharWidth18(jarSizes[c])), "client.jar: szerokości jak getCharWidth 1.8",
                string.Join(" ", "Жə中".Select(c => $"{jar.Width(c)}+1/{GetCharWidth18(jarSizes[c])}")));
            var page = ClientAssets.TryReadFile(UnicodeFont.PagePath(0x04));
            var img = page == null ? null : RawImage.Decode(page);
            Check(img is { W: 256, H: 256 }, "client.jar: strona unicode_page_04.png 256x256", img == null ? "brak" : $"{img.W}x{img.H}");
        }
        else Console.WriteLine("  (brak client.jar — pominięto test plików 1.8)");
    }

    /// <summary>Wzorzec: FontRenderer.getCharWidth 1.8 dla znaku Unicode, przepisany wprost z Javy (z odstępem).</summary>
    private static int GetCharWidth18(byte size)
    {
        int j = size >> 4, k = size & 15;
        if (k > 7) { k = 15; j = 0; }
        return (k + 1 - j) / 2 + 1;
    }
}
