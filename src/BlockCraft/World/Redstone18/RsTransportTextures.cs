using BlockCraft.Core;

namespace BlockCraft.Render;

public static partial class TextureGen
{
    private static void TexRedstoneTransport()
    {
        foreach (string kind in new[] { "normal", "golden", "detector", "activator" })
        {
            Add("rail_" + kind, RailTexture(kind, false));
            if (kind != "normal") Add("rail_" + kind + "_powered", RailTexture(kind, true));
        }
        for (int s = 6; s < 10; s++) Add("rail_turn_" + s, RailCurve(s));
    }
    private static uint[] RailTexture(string kind, bool on)
    {
        var p = New();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool tie = y % 4 is 1 or 2;
                if (tie && x >= 1 && x <= 14) p[y * 16 + x] = Hex(y % 4 == 1 ? 0x76522Fu : 0x513821u);
                if (x is 2 or 3 or 12 or 13) p[y * 16 + x] = Hex(kind == "golden" ? (x is 3 or 12 ? 0xF7D65Cu : 0xB08B23u) : (x is 3 or 12 ? 0xC8C8C8u : 0x747474u));
                if (kind is "golden" or "activator" && x is 6 or 9) p[y * 16 + x] = Hex(on ? 0xFF3C22u : 0x641B13u);
                if (kind == "activator" && y % 4 == 0 && x >= 6 && x <= 9) p[y * 16 + x] = Hex(on ? 0xFF6B38u : 0xA18767u);
                if (kind == "detector" && x >= 5 && x <= 10 && y >= 4 && y <= 11) p[y * 16 + x] = Hex(x is 5 or 10 || y is 4 or 11 ? 0x696969u : on ? 0xCB5546u : 0xB7B7B7u);
            }
        return p;
    }
    private static uint[] RailCurve(int s)
    {
        var p = New();
        // Ćwiartka okręgu o środku w narożniku SE; pozostałe łuki są odbiciami.
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                double dx = 15.5 - x, dz = 15.5 - y, r = Math.Sqrt(dx * dx + dz * dz);
                double a = Math.Atan2(dz, dx);
                uint c = 0;
                if (r >= 1 && r <= 15 && (int)(a * 10) % 3 == 1) c = Hex(0x76522Fu);
                if (r is >= 2 and < 4 or >= 12 and < 14) c = Hex(r is < 3 or >= 13 ? 0x747474u : 0xC8C8C8u);
                int xx = s is 7 or 8 ? 15 - x : x, yy = s is 8 or 9 ? 15 - y : y;
                p[yy * 16 + xx] = c;
            }
        return p;
    }
}
