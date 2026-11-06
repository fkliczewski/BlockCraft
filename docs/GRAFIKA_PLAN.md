# Plan: ładniejsza grafika („Cacy”)

Cel: świat ma wyglądać lepiej niż w MC, a nie wiernie jak w MC. Każdy efekt da się wyłączyć, a preset
**Klasyczna** to dokładnie dotychczasowa ścieżka renderu (słabe komputery nic nie tracą).

## Jak to robią inni

- **Shader packi Java (Iris/OptiFine: Complementary Reimagined, BSL)**: stały łańcuch przebiegów
  `shadow → gbuffers → deferred → composite → final`, każdy efekt z przełącznikiem, presety Low–Ultra.
- **Vibrant Visuals (Bedrock, 2025)**: deferred + PBR, cienie kierunkowe, wolumetryczna mgła, odbicia w wodzie,
  tonemapping i auto-ekspozycja; fallback do klasycznego wyglądu.
- **Silniki AAA (Unreal, Unity HDRP)**: wszystko w HDR, na końcu tonemapping + color grading + AA; presety
  dobierane do GPU, każda opcja do nadpisania.

Zasady przyjęte u nas:
1. Najpierw infrastruktura (HDR FBO z teksturą głębi + łańcuch post-processu), potem efekty.
2. Świat liczony w HDR (RGBA16F), tonemapping dopiero na końcu.
3. Drogie efekty w obniżonej rozdzielczości i z osobnym zasięgiem (cienie, chmury, odbicia).
4. Presety + pojedyncze przełączniki; `GpuProfile` dobiera preset do karty.

## Architektura

Forward z dodatkowymi buforami (nie pełne deferred): obecne shadery zostają, świat rysuje się do HDR FBO,
potem pełnoekranowe przebiegi, na końcu GUI w pełnej rozdzielczości.

```
[shadow pass] → [świat → HDR FBO: kolor + (później) normal/materiał + głębia]
→ [woda/przezroczyste] → [bloom, god rays, mgła] → [tonemap + grading] → [FXAA] → ekran → GUI
```

## Ustawienia

- `GraphicsPreset`: Klasyczna / Ładna / Cacy / Ultra / Własna (zmiana pojedynczej opcji = Własna).
- Osobne przełączniki w ekranie „Efekty graficzne…” (Ustawienia graficzne), wszystkie dostępne z pada.
- Profil słabego GPU: zintegrowana → Ładna (bez drogich efektów), programowa → Klasyczna.

## Etapy

Każdy etap: implementacja → zrzuty `--showcase --gfx-preview` → sprawdzenie przez użytkownika → commit.

| # | Etap | Status |
|---|------|--------|
| 1 | HDR FBO + tonemapping (ACES) + color grading (żywe kolory, winieta) + FXAA + system presetów | do sprawdzenia |
| 2 | Falujące liście, trawa, kwiaty i woda (vertex shader, flaga materiału w wierzchołku) | do sprawdzenia |
| 3 | Lepsze niebo: rozpraszanie atmosferyczne, poświata słońca, gwiazdy, mgła zależna od wysokości | — |
| 4 | Cienie od słońca/księżyca: 2–3 kaskady, miękkie PCF, cienie encji; światło nieba zostaje dla jaskiń | — |
| 5 | Woda: fresnel, animowane normalne, odbicie nieba → SSR; widok pod wodą (kaustyki, dystorsja) | — |
| 6 | Bloom + god rays + auto-ekspozycja; emisyjne bloki (lawa, pochodnie, portale) świecą, barwione światło | — |
| 7 | Dopieszczenie: SSAO/GTAO, kolorowe światło bloków, chmury wolumetryczne, TAA, PBR dla paczek HD | — |

### Etap 1 — szczegóły

- `Render/PostFx.cs`: bufor HDR (RGBA16F + tekstura głębi Depth24Stencil8), przebieg końcowy rysujący na ekran.
- Tonemapping ACES (Narkowicz) w przestrzeni liniowej z ekspozycją; grading: nasycenie/vibrance, kontrast,
  lekko ciepły balans, delikatna winieta.
- FXAA (wersja uproszczona 3.11) liczone na wartościach po tonemappingu w tym samym przebiegu.
- Skala renderu 3D działa jak dotąd (bufor w mniejszej rozdzielczości, przebieg końcowy skaluje na ekran).
- Ekran Ustawienia → Wyświetlanie → „Efekty: …”: preset i pojedyncze efekty (z pada jak inne przyciski).
- Gdy shader albo bufor HDR się nie utworzy, gra przechodzi na render klasyczny (log: „Efekty graficzne wyłączone”).
- Podgląd: `--showcase --gfx-preview` — ta sama scena w każdym presecie; self-test `--selftest --gfx-only`.

### Etap 2 — szczegóły

- `Render/ChunkMesherWave.cs`: rodzaj falowania bloku (liście i pnącza / rośliny / woda) w bitach 2-3 flag wierzchołka;
  roślina ma nieruchomy spód, górna połowa podwójnej rośliny rusza się cała.
- Shader chunków przesuwa wierzchołki funkcją pozycji w świecie i czasu: wspólne wierzchołki sąsiadów ruszają się
  razem (bez szczelin). Woda tylko opada (fala w dół do ~0,1 bloku), nie odsłania brzegu.
- Wiatr mocniejszy w deszczu i burzy. Wyłączone = wierzchołki bez zmian (obraz jak dotąd).
- Bez falowania: trzcina (słupek bloków), pajęczyna, ogień, grzyby.
- Opcja „Falujące rośliny” w „Efekty: …”; w presetach Ładna/Cacy/Ultra włączona, w Klasycznej wyłączona.

### Poprawka — wybór karty graficznej (laptopy z dwiema kartami)

- Windows daje grom OpenGL kartę oszczędną (np. Radeon 610M zamiast RTX). Przy starcie gra porównuje GL_RENDERER
  z kartami z DXGI (`Core/GpuSwitch.cs`); jest mocniejsza → ekran pytania (`Gui/GpuChoiceScreen.cs`, mysz i pad).
- „Użyj” zapisuje preferencję Windows dla pliku gry (`HKCU\Software\Microsoft\DirectX\UserGpuPreferences`,
  jak Ustawienia → Grafika) i restartuje grę raz; na mocnej karcie profil „laptop” jest cofany (skala 100%, Cacy).
- Restart bez zmiany karty (np. tryb GPU „Eco”) → wyjaśnienie zamiast pętli. „Zostań” jest zapamiętywane.
- Zmiana później: Własne ustawienia → Wydajność → „Karta graficzna: …” (także powrót na zintegrowaną).
- VSync w laptopie hybrydowym: karta renderująca bez ekranu (DXGI EnumOutputs) → sterownik OpenGL ignoruje interwał
  i obraz się rwie; wtedy interwał 0 + `DwmFlush` po każdej klatce (czekanie na złożenie obrazu przez Windows).

## Kosztowność (orientacyjnie)

| Efekt | Praca | FPS |
|-------|-------|-----|
| HDR + tonemap + grading + FXAA | mała | ~0 |
| Falująca roślinność | mała | ~0 |
| Niebo | mała–średnia | ~0 |
| Cienie | duża | −15–25% |
| Woda SSR | średnia–duża | −5–15% |
| Bloom / god rays | mała | niski |
| Chmury wolumetryczne | duża | −5–10% |
