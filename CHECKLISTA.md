# BlockCraft – checklista implementacji (klon Minecrafta 1.8 w .NET)

## Narzędzia / technologie
- .NET 8 (C#), jeden projekt gry `BlockCraft` + projekt testów `BlockCraft.Tests` (xUnit)
- **OpenTK 4.x** – okno (GLFW), wejście (klawiatura/mysz), OpenGL 3.3 core
- **StbImageWriteSharp** – zapis screenshotów PNG (F2 + autotesty wizualne)
- **NAudio** (opcjonalnie) – proceduralnie syntezowane dźwięki
- Wszystkie tekstury generowane proceduralnie w kodzie (16x16 pixel-art w stylu MC), font pixelowy ręcznie zdefiniowany (z polskimi znakami)
- Sieć: Tailcat.Link 0.5.4 przez derp.fimson.pl:443, host w grze ("Otwórz w sieci") + dołączanie kodem lub zapamiętanym parowaniem; `--server` pozostaje atrapą
- Testy: xUnit (logika) + tryb `--autotest` (uruchomienie gry, skrypt akcji, screenshoty do weryfikacji)

## Stan prac (2026-10-16)
Legenda: `[x]` zrobione, `[~]` częściowo / stub, `[ ]` nie rozpoczęte.

- Kod: ~17,2 tys. linii C# w `src/BlockCraft`; `dotnet build -c Release` → 0 błędów, 4 ostrzeżenia (m.in. CA2014 – `stackalloc` w pętli w `ChunkMesher.cs`)
- Zbudowana gra w `BlockCraft_Gra/` (poza repo) uruchamia się – GL 3.3, Intel Iris Xe
- **Następne kroki:**
  1. Multiplayer - moduł G działa przez Tailcat.Link; po scaleniu NE/OW podłączyć wymiary i pogodę przez `NetHooks` (opis: `docs/MC18_NET.md`)
  2. Dźwięki proceduralne – synteza NAudio w `Audio/`, podpiąć pod `Sound.Play`
  3. Projekt `BlockCraft.Tests` (xUnit) – przenieść przypadki z `SelfTest.cs`
  4. Drobne: ostrzeżenie CA2014, `/weather` i `/locate` to atrapy

## Etap 1 – Fundament
- [x] Projekt, okno, pętla gry (20 TPS logika + interpolowany render)
- [x] Shadery, atlas tekstur, SpriteBatch 2D, font pixelowy
- [x] Generator tekstur bloków / itemów / GUI / mobów

## Etap 2 – Świat
- [x] Rejestr bloków (id + metadata), chunki 16x128x16, sekcje 16^3
- [x] Generator terenu: szum (Perlin/Simplex), biomy las + pustynia, plaże, woda na poziomie 63, jaskinie, rudy (węgiel, żelazo, złoto, redstone, lapis, diament), bedrock
- [x] Drzewa (dąb, brzoza) jak w MC, kwiaty, trawa, kaktusy, martwe krzaki, trzcina
- [x] Superpłaski świat
- [x] Oświetlenie: sky light + block light (BFS), smooth lighting + AO
- [x] Mesher sekcji w tle, frustum culling, mgła
- [x] Niebo, słońce, księżyc, gwiazdy, cykl dnia i nocy, chmury 3D
- [x] Woda i lawa (płynięcie, źródła), animowane tekstury
- [x] Zapis/odczyt świata

## Etap 3 – Gracz
- [x] Fizyka MC (grawitacja, tarcie, skok, AABB), sprint (Ctrl / 2x W), kucanie (Shift + trzymanie krawędzi)
- [x] Pływanie, drabiny, latanie w kreatywnym (2x spacja)
- [x] Kamera F5 (1. os., 3. os. z tyłu, z przodu), bobbing, ręka Steve'a + trzymany item
- [x] Niszczenie (czas wg twardości/narzędzia, pęknięcia, cząsteczki), stawianie bloków
- [x] HP, głód, nasycenie, wyczerpanie (sprint/skok), regeneracja, obrażenia od upadku/topienia/lawy/kaktusa, śmierć + respawn
- [x] Jedzenie (jabłka, mięso surowe/pieczone, chleb...) z animacją

## Etap 4 – Itemy i GUI
- [x] Ekwipunek (36 slotów + zbroja), hotbar, klik L/P, shift-klik, przeciąganie, klawisze 1-9, Q
- [x] Crafting 2x2 w eq, stół 3x3, receptury shaped/shapeless
- [x] Piec (paliwo, przetapianie, pieczenie), skrzynia
- [x] Narzędzia drewno → kamień → żelazo → diament (+złoto), miecze, motyki, nożyce, wiadra, zbroje
- [x] Menu kreatywne z zakładkami i wszystkimi itemami
- [x] HUD: celownik, hotbar, serca, głód, zbroja, bąbelki, XP, F3 debug
- [x] Menu główne (panorama świata), wybór świata, tworzenie świata (tryb, typ, seed, komendy), ustawienia, sterowanie, pauza, ekran śmierci
- [x] Czat + komendy: /time /gamemode /tp /give /kill /summon /setblock /fill /difficulty /gamerule /clear /seed /help /spawnpoint /say

## Etap 5 – Moby
- [x] Renderer modeli z boxów (UV jak skiny MC), tekstury mobów
- [x] AI + pathfinding A*, spawnowanie (światło ≤ 7, noc/jaskinie), despawn
- [x] Zombie, szkielet (łuk, strzały), creeper (eksplozja), pająk (wspinanie), enderman (teleport, patrzenie)
- [x] Spalanie zombie/szkieletów w dzień
- [x] Krowa (mleko, skóra), owca (wełna, nożyce, odrastanie), świnia, kurczak (jajka) + rozmnażanie i młode
- [x] Dropy, łuk gracza, TNT

## Etap 6 – Extra
- [x] Redstone: pył, pochodnia, dźwignia, przycisk, blok, lampa, repeater, TNT, tłok i lepki tłok, drzwi
- [x] Multiplayer Tailcat.Link: host w grze, kod i zapamiętane parowanie, klient, chunki/bloki/TE/encje, modele i nicki graczy, kontenery, czat, ekwipunek, śmierć i respawn; test dwóch procesów przez derp.fimson.pl i PNG z obu okien. Wymiary/pogoda wymagają integracji po scaleniu NE/OW; zbroja modelu i pęknięcia kopania innych graczy pozostają do dodania. Szczegóły: `docs/MC18_NET.md`.
- [~] Dźwięki proceduralne  *(wywołania `PlaySound` są w całej grze, ale `Sound.Play` w `GameRender.cs` to pusty stub; folder `Audio/` pusty)*
- [~] Testy jednostkowe + autotest wizualny, publish do pojedynczego folderu  *(zamiast xUnit: `--selftest` (`SelfTest.cs`) + `--autotest` (`GameAutotest.cs`); publish do `BlockCraft_Gra/` działa; brak projektu `BlockCraft.Tests`)*

## Weryfikacja modułu G (2026-10-30)

- [x] Build Release: 0 błędów (4 istniejące ostrzeżenia).
- [x] 36 testów sieci, w tym FakeDerpRelay i ramka 200 KB. Pełny selftest: 148 OK / 0 FAIL; niestabilny test krów odtworzony również przed zmianami sieciowymi (`b2fb2f2`).
- [x] `tools/dev/Test-Net.ps1`: logowanie kodem, teren, wspólny złoty blok, modele graczy, czat, 3 diamenty, operator, wyrzucenie, powrót bez kodu z ekwipunkiem, zamknięcie sieci przez hosta. Wyniki i obejrzane zrzuty: `.modus/net-e2e-reviewed`.
- [ ] Próba na dwóch fizycznych komputerach oraz próby wymiarów/pogody po scaleniu modułów NE/OW.
