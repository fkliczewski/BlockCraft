# src/BlockCraft/Items/Recipes.cs

- Recipe · class · L5-L13 — public sealed class Recipe
- Recipes · class · L15-L210 — public static class Recipes
- Matches · method · L20-L28 — private static bool Matches(int want, int wantMeta, ItemStack s)
- Shaped · method · L30-L52 — public static void Shaped(ItemStack result, params object[] def)
- Shapeless · method · L54-L57 — public static void Shapeless(ItemStack result, params int[] ids)
- Find · method · L60-L112 — public static ItemStack Find(ItemStack[] grid, int gw, int gh)
- S · method · L114-L114 — private static ItemStack S(int id, int c = 1, int m = 0) => new(id, c, m);
- Init · method · L116-L209 — public static void Init()
- Smelting · class · L212-L261 — public static class Smelting
- Init · method · L216-L238 — public static void Init()
- Result · method · L240-L240 — public static ItemStack Result(ItemStack input) => input != null && Table.TryGetValue(input.Id, out var r) ? r.result : null;
- Xp · method · L241-L241 — public static float Xp(ItemStack input) => input != null && Table.TryGetValue(input.Id, out var r) ? r.xp : 0;
- BurnTime · method · L243-L260 — public static int BurnTime(ItemStack s)
