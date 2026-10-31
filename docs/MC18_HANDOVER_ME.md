# Przekazanie pracy — ME, 2026-10-31

Użytkownik poprosił o zmianę agenta. Dotychczasowy agent zapisuje stan i kończy pracę.
Plan źródłowy: `.modus/progress/Agent-1.md`, zarządzany przez `modus_plan/modus_step`.
Gałąź: `feature/mc18`. Nie ruszać cudzej lokalnej zmiany `.gitignore`.

## Stan potwierdzony

- Funkcjonalna integracja OW jest zakończona do commita `170f627` i wypchnięta.
- Obejmuje teren/biomy/struktury, mieszkańców i łupy, pogodę, pioruny, naturalny
  spawn struktur, poprawiony cykl spawnu 1.8 i oryginalne tekstury opadów.
- Ostatni pełny selftest przed WIP ME: **569 OK, 0 FAIL**.
- `--autotest --ow` i pełny autotest przeszły; zrzuty obejrzane.
- Test dwóch rzeczywistych okien przez relay przeszedł, łącznie z pogodą,
  piorunem, jednym głosem deszczu, kontenerami, handlem i ponownym połączeniem.
  Dowody: `.modus/weather-relay-e2e-20261031-2`.
- Audio ma poprawkę `ad53e2c` przeciw przesterowaniu/nawarstwianiu; zachować ją.
- Raport integracji i ograniczenia: `docs/MC18_RAPORT_INTEGRACJI.md`.
- Raport problemów orkiestracji: `.modus/PROBLEMY_ORKIESTRACJI.md`.

## Rozpoczęty krok 29 — nie jest gotowy

Przeczytano raport Goro `.modus/raporty/20261030-114753-Goro.md`.
Jego moduł obejmował efekty i warzenie; zaklinanie/kowadło oraz większość
przedmiotów użytkowych nie zostały wtedy wdrożone. Poprawki warzenia są już scalone.

Nowy plik `Items/Mechanics/EnchantingRules.cs` jest **WIP**. Zawiera metadane
25 zaklęć 1.8, enchantability materiałów, zgodność zaklęć, rozróżnienie stołu
i kowadła, koszty/oferty z Java Random, generowanie list i liczenie biblioteczek.
Jest odizolowany: brak wywołań z gry, GUI, bloku stołu, rozliczania XP/lazurytu,
zapisu XPSeed gracza, zaklętych książek i komunikatów sieciowych.

WIP **tylko skompilowano**: Release 0 błędów, 4 stare ostrzeżenia
(`.modus/me-enchant-rules-build.log`). Nie dodano jeszcze testów reguł i nie
potwierdzono ich zgodności z oracle MC. Wynik 569/0 nie waliduje tego WIP.

## Najbliższy krok dla następnego agenta

1. Przejrzeć WIP i porównać oferty z MC 1.8, zwłaszcza kolejność kandydatów
   HashMap Java 8 (w kodzie założono kubełki 16/32/64), progi mocy i clue.
2. Dodać testy reguł i biblioteczek; plik testowy jeszcze nie powstał.
3. Wdrożyć działający stół: blok/receptura/GUI, XPSeed z zapisem gracza,
   koszty 1–3 poziomy i lazuryt, wymagany poziom oferty, książki i host autorytatywny.
4. Potem kowadło i pozostałe ME; nie odhaczać całego kroku 29 po samych regułach.

Referencje MCP są w `%TEMP%/blockcraft-mc18-reference/enchanting/`:
EnchantmentHelper, Enchantment i klasy zaklęć, ContainerEnchantment,
Item/ItemStack/ItemBook/ItemHoe/ItemShears/ItemFishingRod. Oficjalny server.jar
i wcześniejsze narzędzia oracle są w nadrzędnym katalogu referencji.
`JRandom` już istnieje w `World/Overworld/McNoise.cs`.

## Pozostała integracja

Krok 30: m.in. dragon_part i pasażerowie w sieci, końcowe testy, README,
CHECKLISTA, raport i wydanie. `BlockCraft_Gra` nadal jest z `00d3ee0`, bez OW.
Main nie zmieniano; merge wymaga końcowej akceptacji użytkownika według planu.
W logach są 4 stare braki tekstur: item_baked_potato, item_poisonous_potato,
item_pumpkin_seeds, item_melon_seeds — do naprawy przy ME/integracji.
