# src/BlockCraft/Gui/GuiTextures.cs

- GuiTextures · class · L7-L244 — public sealed class GuiTextures : IDisposable
- C · method · L16-L16 — private static uint C(uint hex, int a = 255) => TextureGen.Hex(hex, a);
- GuiTextures · method · L18-L27 — public GuiTextures()
- Fill · method · L29-L29 — private static void Fill(uint[] p, int x, int y, int w, int h, uint c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) if (x + i < 256 && y + j < 256) p[(y + j) * 256 + x + i] = c; }
- MakeWidgets · method · L31-L86 — private static uint[] MakeWidgets()
- Sprite9 · method · L88-L92 — private static void Sprite9(uint[] p, int ox, int oy, string[] rows, Dictionary<char, uint> pal)
- MakeIcons · method · L94-L161 — private static uint[] MakeIcons()
- IsInside · method · L163-L167 — private static bool IsInside(string[] rows, int x, int y)
- MakeLogo · method · L182-L241 — private static Texture2D MakeLogo()
- Dispose · method · L243-L243 — public void Dispose() { Widgets.Dispose(); Icons.Dispose(); Logo.Dispose(); Dirt.Dispose(); }
