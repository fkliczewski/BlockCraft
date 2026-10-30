# Rozbudowa BlockCraft do MC 1.8 - zasady dla modułów

Cel: odwzorować Minecraft 1.8 (Java Edition) 1:1 - wartości (twardość, odporność na wybuchy, dropy, czasy, obrażenia,
HP mobów, receptury, szanse) i mechaniki jak w kodzie MC 1.8 (MCP). Wątpliwości: Minecraft Wiki (stan dla 1.8) i własna
wiedza o kodzie MC 1.8. Tekstury, skórki mobów i ikony generujemy proceduralnie w kodzie (styl jak istniejące, 16x16).

## Gałęzie
- `feature/mc18` - gałąź mastera. Moduły pracują na własnych gałęziach `mc18/<moduł>` w worktree i NIE scalają same.
- Commit po każdym działającym kroku, push na `origin` (prywatne konto fkliczewski). W commitach nigdy `Co-Authored-By`.
- Teksty i komentarze po polsku. ZAKAZ długich myślników (U+2010..U+2015, U+2212) wszędzie - tylko ASCII `-`.

## Moduły i własność
| Kod | Moduł | Gałąź |
|---|---|---|
| A | Dźwięki i muzyka | mc18/audio |
| OW | Overworld: biomy, drewna, rośliny, budulce, pogoda, struktury | mc18/overworld |
| RS | Redstone i transport (tory, wagoniki) | mc18/redstone |
| NE | Nether, End, portale, wymiary, moby i bossowie Netheru/Endu | mc18/netherend |
| MO | Moby overworldu, jajka spawnu, smycze, siodła, konie, handel | mc18/mobs |
| ME | Mechaniki itemów: enchanty, kowadło, mikstury, efekty, wędka, mapy, tabliczki, ramki... | mc18/mechanics |
| G | Multiplayer (Tailcat.Link) - później | mc18/net |

Każdy blok i item 1.8 ma już stałą w `World/BlockIds.cs` (B) i `Items/Item.cs` (I) z przypisanym właścicielem.
Rejestruje, teksturuje i implementuje go tylko właściciel. Inne moduły mogą używać stałych (receptury, generacja, dropy);
dopóki właściciel nie zarejestruje bloku, `Block.ById[id]` to powietrze, a `Item.ById[id]` to null - kod musi to znosić
(np. receptura z nieistniejącym itemem po prostu nie zadziała). Brakujące ID dopisuj na końcu swojej sekcji.

## Punkty zaczepienia (nie edytuj wspólnych list - implementuj metody częściowe we WŁASNYCH plikach)
Deklaracje w `Modules.cs`. Każdy moduł tworzy swoje pliki, np. `World/Overworld/*.cs`, i implementuje:
- tekstury: `static partial void TexOverworld()` w `public static partial class TextureGen` (użyj `Add(name, px)`, `New()`, `TileNoise`...)
- bloki: `static partial void BlocksOverworld()` w `public static partial class Blocks`
- itemy: `static partial void ItemsOverworld()` w `public static partial class ItemRegistry` (itemy-bloki tworzą się same z bloków)
- receptury i przetapianie: `static partial void RecipesOverworld()` w `public static partial class Recipes`
- encje, tile entity, renderery: `static partial void EntitiesOverworld()` w `public static partial class Registries`
  (`Registries.Mob(key, w => new X(w))`, `Registries.Entity(...)`, `Registries.Tile(typeId, () => new Te())`,
  `EntityRenderer.Custom[typeof(X)] = ...`, `EntityRenderer.CustomLiving[typeof(X)] = ...`)
- skórki mobów 64x64: `static partial void SkinsOverworld()` w `public static partial class SkinGen`
Nazwy metod: Overworld, Redstone, NetherEnd, Mobs, Mechanics.

Klasy częściowe (możesz dopisywać członków we własnym pliku): Game, World, Blocks, TextureGen, ItemRegistry, Recipes,
Registries, SkinGen, EntityRenderer, WorldRenderer, ChunkMesher, Player, Mob, PlayerController, EffectLogic.

TypeId tile entity: 1 piec, 2 skrzynia, 10-29 RS, 30-49 NE, 50-59 MO, 60-89 ME, 90-99 OW.

## Wspólne API z fundamentu
- Wysokość świata 256 (`Chunk.Height`), poziom morza 63, zapis chunków w wersji 3.
- Warianty meta: `Block.Vars(n)` / `Block.Variants`, `Item.Variants` - kreatywny i ikony biorą je automatycznie.
- Item zależny od stacka: `Item.SpriteFn`, `Item.ColorFn`, `Item.OverlayFn`, `Item.NameFn`.
- NBT: `ItemStack.Tag` (`Core/Nbt.cs`), zapisywane w skrzyniach i ekwipunku. `ItemStack.CustomName`.
- Efekty: `LivingEntity.AddEffect(Effect.Poison, 100, 0)`, `HasEffect`, `EffectLevel`; działanie efektów robi ME w `EffectLogic`.
- Zaklęcia: `EnchantHelper.GetLevel(stack, Ench.Looting)`; działanie robi ME.
- Barwniki: `Dyes.ColorOf(stack)`, `Dyes.DyeStack(color)`, kolejność kolorów jak EnumDyeColor (meta wełny = kolor).
- `/summon` i `/give` widzą wszystko z rejestrów.
- Zakładki kreatywne: `Blocks.TabTransport` (10), `Blocks.TabBrewing` (11) - ME dodaje je do GUI.
- Dźwięki: `World.PlaySound(nazwaZdarzeniaMc18, x, y, z, vol, pitch)` - używaj nazw zdarzeń MC 1.8 (np. `dig.stone`,
  `mob.blaze.hit`, `random.click`), moduł A mapuje je na pliki.

## Testy
- `dotnet build -c Release` w `src/BlockCraft` - 0 błędów.
- `bin/Release/net10.0/BlockCraft.exe --selftest` (logika) i `--autotest` (zrzuty w `bin/Release/net10.0/autotest`).
  Dopisz własne kroki autotestu w osobnym pliku (`partial class Game`) i oglądaj zrzuty (Read na PNG).
