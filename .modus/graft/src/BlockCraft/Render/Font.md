# src/BlockCraft/Render/Font.cs

- FontData · class · L4-L140 — public static class FontData
- G · method · L9-L9 — private static void G(char c, params string[] rows) => Glyphs[c] = (rows, 0);
- GA · method · L10-L10 — private static void GA(char c, int start, params string[] rows) => Glyphs[c] = (rows, start);
- FontData · method · L12-L137 — static FontData()
- Width · method · L139-L139 — public static int Width(char c) => Glyphs.TryGetValue(c, out var g) ? g.rows.Max(r => r.Length) : 5;
