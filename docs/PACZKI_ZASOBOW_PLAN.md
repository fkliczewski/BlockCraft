# Paczki zasobów Minecraft 1.8 — plan

Cel: BlockCraft wczytuje oficjalne paczki zasobów 1.8 (`.zip` albo folder z `pack.mcmeta`
i `assets/minecraft/...`) — tak jak menu „Paczki zasobów” w MC 1.8.
Mody Java (Forge, LiteLoader, Bukkit) są poza zakresem: wymagałyby JVM i odtworzenia API Minecrafta.

Zasada ogólna: każda tekstura/dźwięk ma trzy źródła, od najważniejszego:
1. aktywne paczki (kolejność z listy, górna wygrywa),
2. *(opcjonalnie)* wbudowana paczka „Minecraft 1.8 (oryginalne)” = pobrany `client.jar`,
3. obecne zasoby generowane w kodzie (zawsze dostępne, gra działa bez paczek).

Po każdym etapie: kompilacja, selftest, ręczne sprawdzenie w grze, **stop i odbiór przez użytkownika**.

---

## Etap 1 — Paczki, ekran i tekstury bloków/przedmiotów  *(łączy pkt 1–3 z analizy)*

- `resourcepacks/` w katalogu gry (`%APPDATA%\.blockcraft\resourcepacks`): paczki `.zip` i foldery.
- Odczyt `pack.mcmeta` (`pack_format`, `description`) i `pack.png`; ostrzeżenie, gdy `pack_format` ≠ 1.
- Wbudowana pozycja „Minecraft 1.8 (oryginalne)” — czyta `client.jar`, który gra już pobiera.
- Ekran „Paczki zasobów...” w Ustawieniach: dwie listy (dostępne / aktywne), dodawanie, usuwanie,
  zmiana kolejności, „Otwórz folder paczek”, „Gotowe” = zastosuj bez restartu.
- Ustawienie `ResourcePacks` (lista nazw) w `options.json`; parametr `--resourcepack <nazwa>` do testów.
- Tabela przypisań: nazwa tekstury BlockCraft → plik 1.8 (`blocks/…`, `items/…`) + przekształcenia
  (klatka z paska animacji, barwienie skórzanej zbroi, nakładki itp.). ~380 nazw zgodnych automatycznie,
  reszta jawnie w tabeli; nazwy bez odpowiednika zostają generowane.
- Animacje z `.mcmeta` (woda, lawa, ogień, portal, …): klatki, `frametime`, `frames`.
- Tekstury większe niż 16×16 (HD) w tym etapie są zmniejszane do 16×16 (pełne HD w etapie 2).
- Przeładowanie bez restartu: podmiana warstw atlasu, odbudowa ikon bloków i tła menu.
- Selftest: parsowanie mcmeta, kolejność paczek, przypisania, klatki animacji, paczka uszkodzona/pusta.

## Etap 1b — Źródła paczek: wiele folderów i repozytoria git  *(zrobiony)*

Zrobione: `Core/ContentSources.cs`, ekran „Źródła...” (`Gui/PackSourcesScreen.cs`), `PackSources` w `options.json`,
`--pack-source`, pobieranie repo w tle przy starcie (git: płytki klon, potem fetch + reset do stanu zdalnego;
bez gita zip z GitHuba z ETag), obserwowanie folderów lokalnych i przeładowanie po zapisie, selftest
(`ContentSourcesSelfTest.cs`, w tym prawdziwe repo git przez file://). Przykład w repo `blockcraft-plugins`:
`resourcepacks/przyklad`. Zaufanie do źródeł z kodem (mody) — razem z modami.

Cel: paczki zasobów (a później mody) nie tylko z `resourcepacks/`, ale z dowolnych miejsc,
w tym z repozytorium git, np. https://github.com/b-y-t-e/blockcraft-plugins
(lokalnie `D:\work\sources\blockcraft-plugins`). Zmiany w repo są pobierane automatycznie przy starcie gry.

- **Rodzaje źródeł:**
  - folder lokalny: każdy podfolder z `pack.mcmeta` i każdy `.zip` to paczka; folderów może być wiele;
  - repozytorium git (URL + opcjonalnie gałąź/tag): klon w `%APPDATA%\.blockcraft\sources\<nazwa>`, przy starcie
    `git pull --ff-only` w tle z limitem czasu; bez sieci gra używa ostatniej pobranej wersji;
  - lokalny checkout repo (do testów, np. `D:\work\sources\blockcraft-plugins`): czytany wprost, bez pobierania.
- **Konfiguracja na trzy sposoby:**
  - w grze: ekran „Paczki zasobów” → „Źródła...” (dodaj folder lub URL, usuń, „Odśwież teraz”);
  - w pliku: lista `PackSources` w `options.json` (albo osobny `sources.json` obok);
  - w parametrze: `--pack-source <ścieżka|url>` (można powtórzyć, tylko na tę sesję), np.
    `python run.py -- --pack-source D:\work\sources\blockcraft-plugins`.
- **Proponowany układ repo** (do uzgodnienia): `resourcepacks/<nazwa>/pack.mcmeta + assets/...`,
  później `mods/<nazwa>/mod.json + ...`; opcjonalny manifest `blockcraft-source.json` (nazwa, opis, wersja gry).
- **Pobieranie bez gita:** gdy na komputerze nie ma `git`, gra ściąga archiwum zip z GitHuba
  (`/archive/refs/heads/<gałąź>.zip`) i sprawdza ETag, żeby nie pobierać, gdy nic się nie zmieniło.
- **Nazwy:** identyfikator paczki z prefiksem źródła (`blockcraft-plugins:moja-paczka`), żeby uniknąć kolizji;
  na liście w grze widać, skąd paczka pochodzi.
- **Wygoda przy tworzeniu paczek:** dla folderów lokalnych obserwator plików (FileSystemWatcher) i przeładowanie
  tekstur w trakcie gry po zapisaniu pliku (to samo przeładowanie co po „Gotowe” w ekranie paczek).
- **Bezpieczeństwo (mody):** paczki zasobów to same dane, więc ich pobieranie jest bezpieczne. Mody z kodem
  z repozytorium oznaczają uruchamianie cudzego kodu: każde źródło modów trzeba wyraźnie zaufać (potwierdzenie
  w grze), z opcją przypięcia do konkretnego commita.
- **Selftest:** wiele folderów, kolejność źródeł, kolizje nazw, repo bez sieci (ostatnia kopia), uszkodzone archiwum.

## Etap 2 — Tekstury HD (32×, 64×, 128×)

- Atlas terenu w rozdzielczości największej paczki (limit 128 / pamięć GPU), tekstury generowane
  powiększane bez wygładzania.
- Poprawki miejsc zakładających 16×16 (wyciągane przedmioty 3D, cząsteczki z bloków, ikony).
- Mipmapy i opcja „Poziom mipmap” jak w 1.8.

**Etap 2 — zrobione:** drugi przebieg nakładania w rozdzielczości paczki (`TextureGen.BuildAtlas`), atlas terenu
w rozmiarze największej tekstury bloków/przedmiotów (potęga dwójki, najwyżej „Tekstury paczek: do N x” w ustawieniach
graficznych, domyślnie 64, profil słabego GPU 32, programowy 16), tekstury bez wersji HD i klatki wygenerowane
(woda, lawa) powiększane bez wygładzania, przekształcenia (trawa, szyny, skrzynie, cząsteczki) w HD, mipmapy do 1x1.
Gra dalej ma atlas 16x16 w pamięci (ikony 3D, kolizje kształtów); na GPU idzie atlas HD.

## Etap 3 — GUI, czcionka, cząsteczki, niebo, kolory biomów

- `gui/widgets.png`, `gui/icons.png`, okna kontenerów (`gui/container/*.png`), `gui/options_background.png`,
  logo `gui/title/minecraft.png`.
- Czcionka `font/ascii.png` (szerokości znaków jak w MC), polskie znaki z `font/unicode_page_*`.
- `particle/particles.png`, `environment/` (słońce, księżyc, chmury), `misc/` (winieta, nakładki).
- `colormap/grass.png`, `colormap/foliage.png` — kolory trawy i liści zależne od biomu.

**Etap 3 — zrobione:** `gui/widgets.png` i `gui/icons.png` (obszary 1.8 wklejane na teksturę gry, HD w rozdzielczości paczki),
`gui/options_background.png`, okna ekwipunku, stołu, pieca (z płomieniem i strzałką postępu) i skrzyni z `gui/container/`,
czcionka `font/ascii.png` (szerokości jak w 1.8, HD; polskie litery z czcionki gry), cząsteczki z `particle/particles.png`
i wybuch z `entity/explosion.png`, słońce, fazy księżyca, chmury, deszcz i śnieg z `environment/`, mapy kolorów trawy
i liści z `colormap/` (przebudowa chunków po zmianie).
Świadomie bez zmian: logo (gra nazywa się BlockCraft, `gui/title/minecraft.png` ma napis Minecraft), zakładki i tło trybu
kreatywnego, okna statywu, stołu do zaklęć, kowadła, latarni, konia, wieśniaka, lejka i dozownika (własne układy — do zrobienia
przy okazji), strony Unicode czcionki (`font/glyph_*.png`).

## Etap 4 — Encje

- Moby, gracz (domyślne skórki Steve/Alex), skrzynie, zbroje na postaci, obrazy, banery, czaszki, łódka, wózki.
- Tekstury encji mają własne wymiary (64×32, 64×64…) — mapowanie na nasze modele, kontrola UV.

**Etap 4 — zrobione:** skórki mobów z `entity/` (około 80 przypisań w `Render/PackSkins.cs`, UV modeli gry jak w 1.8,
tekstury 64x32/64x64/128x128 i HD; poza obszarem tekstury zostaje skórka gry), czaszki z głów mobów, smok
(`entity/enderdragon/dragon.png`), obrazy, warstwy zbroi i stojak z paczki, ściany skrzyni, skrzyni-pułapki i skrzyni Endu
z `entity/chest/`. Podgląd `--packs-preview` ma rzędy mobów i postać w zbroi.
Bez zmian: model dużej skrzyni w świecie (dalej dwie małe), łódka, wózki, strzały, spławik, promień latarni, kryształ i pancerz
withera (animowane/osobne tekstury), skórki graczy z sieci (osobny temat).

## Etap 5 — Dźwięki, muzyka i język

- `sounds.json` (zastępowanie i dopisywanie dźwięków), pliki `.ogg`, muzyka i płyty.
- `lang/pl_PL.lang` i inne: nazwy przedmiotów/bloków przez klucze tłumaczeń (`tile.stone.name`),
  wybór języka w ustawieniach.

**Etap 5 — zrobione:**
- Dźwięki: `sounds.json` z paczek nakładany na sounds.json 1.8 od najmniej ważnej paczki (`"replace": true` zastępuje,
  inaczej dopisuje), pliki `.ogg` (efekty, muzyka, płyty) najpierw z paczek, potem z pobranych zasobów 1.8.
- Język: ekran Ustawienia → „Język...”, `Settings.Language`, `--language KOD` na sesję. Pliki `.lang` 1.8: en_US z client.jar,
  pozostałe języki pobierane z zasobami 1.8 (około 7 MB), nadpisywane przez paczki; języki dodane w `pack.mcmeta` ("language").
  Tłumaczone są nazwy przedmiotów i bloków: tabela 515 przypisań `Items/LangKeysTable.cs` (zgodność z oficjalnym pl_PL,
  angielskie nazwy, kolory, ręczne uzupełnienia; każdy klucz sprawdzony w en_US.lang), mikstury i jajka składane jak w 1.8.
- Bez zmian: teksty menu i komunikaty gry (są po polsku w kodzie), mikstury bez efektu (niezręczna, gęsta) — nazwa gry,
  znaki spoza czcionki gry (np. ç, ə) jako „?” do czasu stron Unicode czcionki.

## Etap 6 — Modele bloków z JSON  *(największy, opcjonalny)*

- `blockstates/*.json` + `models/block/*.json`: elementy, obroty, UV, `cull`, `tintindex`.
- Mesher korzysta z modelu z paczki, gdy go podano; w przeciwnym razie z obecnego kodu.
- Modele przedmiotów `models/item/*.json` (pozycja w ręce, na GUI).

---

## Stan

| Etap | Stan |
|---|---|
| 1 | zrobiony — do odbioru |
| 1b | zrobiony |
| 3 | zrobiony |
| 4 | zrobiony |
| 5 | zrobiony |
| 2 | zrobiony — do odbioru |
| 6 | do zrobienia |

### Etap 1 — co jest i jak sprawdzić

- Kod: `Core/ResourcePacks.cs` (paczki), `Render/PackTextures.cs` (przypisania, .mcmeta, skalowanie),
  `Gui/ResourcePackScreen.cs` (ekran), `GameResourcePacks.cs` (przeładowanie), `ResourcePackSelfTest.cs`.
- Gra: Ustawienia → „Paczki zasobów...”. Paczki `.zip` do `%APPDATA%\.blockcraft\resourcepacks`.
- Uruchomienie z paczką na jedną sesję: `python run.py -- --resourcepack minecraft-1.8`.
- Zrzuty porównawcze (bez paczki / z paczką): `BlockCraft.exe --showcase --packs-preview [--resourcepack NAZWA]`
  → `bin/Release/net10.0/showcase/`.
- Selftest: `--selftest --packs-only`. Oryginalne tekstury 1.8 pokrywają 662 z 723 tekstur gry.

Świadomie pominięte w etapie 1 (zostają tekstury gry):
- skrzynie, skrzynia Endu, wózki (w 1.8 to tekstury encji) → etap 4;
- cząsteczki, pogoda, niebo Endu, granica świata, promień latarni, spławik → etapy 3–4;
- statyw alchemiczny i kocioł (inny model niż w 1.8) → etap 6;
- rama portalu Endu: ten sam obraz na wszystkich ścianach (bok) → etap 6;
- `interpolate` w animacjach .mcmeta (klatki przełączane bez płynnego przejścia).

---

# Inne zadania (spoza paczek zasobów)

## A. Wyszukiwanie przedmiotów po nazwie  *(zrobione — do odbioru)*

Zrobione (`Items/ItemSearch.cs`, zakładka „Szukaj” w `CreativeScreen`): bez wielkości liter i polskich znaków, słowa w dowolnej
kolejności, po nazwie (także polskiej nazwie gry przy innym języku), kluczu (`iron_pickaxe`) i zaklęciach; wyniki zaczynające
się od zapytania pierwsze; pisanie liter/cyfr w innej zakładce od razu szuka (T = puste wyszukiwanie, E dalej zamyka),
prawy przycisk czyści pole, ostatnie zapytanie zostaje, „Brak wyników”, podpowiedź w pustym polu. Selftest `--search-only`.
Nie zrobione: podświetlanie w ekwipunku przetrwania i skrzyniach.

Pierwotny plan:

Stan: w trybie kreatywnym jest zakładka „Szukaj” (ikona książki, prawa górna), filtruje po nazwie i kluczu.
Do zrobienia:
- pisanie w dowolnej zakładce od razu przełącza na „Szukaj” i wpisuje znak (jak w MC 1.8);
- wyszukiwanie bez polskich znaków i wielkości liter („zelazny” znajduje „Żelazny kilof”), po kilku słowach w dowolnej kolejności;
- szukanie także po angielskim kluczu (`iron_pickaxe`), po zaklęciach i opisie z podpowiedzi;
- pole wyszukiwania widoczne od razu (np. podpowiedź „Szukaj...” przy zakładkach), czyszczenie prawym przyciskiem jak w MC;
- wyszukiwarka w ekwipunku przetrwania: podświetlanie pasujących przedmiotów w otwartych skrzyniach i ekwipunku (opcjonalnie).

## B. Kontrolery (pady) i klawiatura ekranowa

- Obsługa padów przez GLFW (OpenTK `JoystickState` / mapowania gamepad SDL z GLFW): Xbox, PlayStation, ogólne.
- Sterowanie w grze: lewa gałka ruch, prawa kamera (czułość, martwa strefa, odwrócenie osi), spusty kopanie/stawianie,
  przyciski: skok, skradanie, ekwipunek, upuszczanie, zmiana slotu (bumpery), pauza, sprint (wciśnięcie gałki).
- Nawigacja po menu i ekranach: kursor sterowany gałką z przyciąganiem do przycisków i slotów, A = klik, B = wstecz,
  X/Y = akcje slotów (połowa stosu, szybkie przeniesienie), przewijanie list.
- Klawiatura ekranowa przy polach tekstowych (czat, komendy, nazwa świata, nick, kod zaproszenia, wyszukiwanie, tabliczki,
  źródła paczek): siatka liter z polskimi znakami, Shift/Caps, Backspace, Enter, spacja, wklejanie ze schowka;
  wybór gałką/krzyżakiem, otwiera się sama, gdy pole dostaje fokus z pada.
- Ekran ustawień „Kontroler...”: przypisania przycisków, czułość, wibracje (jeśli dostępne), podgląd stanu pada.
- Podpowiedzi przycisków na ekranie (ikony A/B/X/Y) zamiast klawiszy, gdy ostatnio używano pada.
- Wiele padów w sieci lokalnej nie jest w planie (jeden gracz na instancję gry).
