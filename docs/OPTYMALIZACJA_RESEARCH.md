# BlockCraft - research optymalizacji (2026-11-03)

Cel od Filipa: mechaniki MC 1.8 bez zmian, tysiące FPS, brak lagów przy 100 000 TNT naraz,
dobra gra na laptopach bez karty graficznej (iGPU Intel UHD/Iris Xe, Radeon Vega w APU).

Dokument ma trzy części: (1) co dziś hamuje BlockCraft (z kodu), (2) jakich sztuczek używają
inni (mody MC, silniki voxelowe, .NET), (3) plan wdrożenia z pomiarami. Każdy etap ma być
mierzalny benchmarkiem i nie może zmienić mechaniki (selftest + autotesty muszą przechodzić).

---

## 1. Diagnoza obecnego kodu

### 1.1 TNT i eksplozje (`Entities/MiscEntities.cs`, `World/World.cs`)

| Miejsce | Problem | Koszt przy N TNT |
|---|---|---|
| `World.GetEntitiesInBox` | liniowe przejście po **wszystkich** encjach i nowa `List` przy każdym wywołaniu | każda eksplozja O(N), czyli łącznie **O(N²)**: 100k TNT = 10¹⁰ testów AABB |
| `Explosion.Explode` - promienie | 1352 promienie po ~23 kroki = ~31 000 `w.GetBlock` (każdy to lookup w `ConcurrentDictionary`) + `HashSet<(int,int,int)>` | ~31k lookupów na eksplozję, nawet gdy dookoła jest już samo powietrze |
| `Explosion.Exposure` | 27 raycastów na każdą encję w zasięgu | gęsta chmura TNT: tysiące encji w zasięgu * 27 promieni * N eksplozji |
| niszczenie bloków | `SetBlock` na blok: przeliczenie światła BFS, heightmapa, `MarkDirty` 3x3x3, listenery, `NotifyNeighbors` (6 wywołań wirtualnych) | tysiące pełnych aktualizacji światła na tick |
| `TntEntity.Tick` | `base.Tick` -> `UpdateFluidState` (2x `IsAnyLiquid`), `MoveWithCollision` -> `GetCollisionBoxes` (lista + `AABB` na blok), cząsteczka dymu co tick | 100k * kilka alokacji i kilkadziesiąt lookupów na tick |
| cząsteczki i dźwięki | każda eksplozja = dźwięk + duża cząsteczka, każdy TNT = dym co tick | 100k wywołań `SpawnParticles` na tick |
| render TNT | każdy TNT to osobny model w `ModelBatch` (float, 68 B na wierzchołek, ponowny upload co klatkę) | 100k * 24 wierzchołki * 68 B = **160 MB/klatkę** przez magistralę |
| pętla gry (`Game.OnRenderFrame`) | tick świata w wątku renderu, do 10 ticków na klatkę | gdy tick trwa 500 ms, cała gra stoi (zero klatek, mysz nie reaguje) |

### 1.2 Render świata (`Render/WorldRenderer.cs`, `Render/ChunkMesher.cs`, `Render/GL.cs`)

- **Jeden draw call na sekcję** (`BindVertexArray` + `glUniform3(uOffset)` + `DrawElements`). Przy RD 8
  to ~1000-2500 wywołań na klatkę. Narzut sterownika OpenGL rzędu 1-3 µs na wywołanie daje 2-5 ms CPU:
  sufit ~200-500 FPS niezależnie od GPU.
- **Brak occlusion cullingu**: rysujemy wszystkie jaskinie i całe podziemie w frustum.
- **Brak cullingu po kierunku ścian**: ściany odwrócone od kamery idą do GPU i giną dopiero przy rasteryzacji
  (backface culling), czyli ~50% wierzchołków na darmo przechodzi vertex shader i pasmo.
- **Wierzchołek 28 B** (3x float pozycji + reszta). Sodium ma 20 B, Nvidium 16 B, silniki voxelowe 8 B na quad.
  Na iGPU pasmo pamięci jest głównym ograniczeniem (pamięć współdzielona z CPU).
- Kolejność rysowania = kolejność w `Dictionary`, nie od najbliższych. Brak wykorzystania early-Z,
  duży overdraw (na iGPU kosztowny).
- Lightmapa liczona matematyką **w każdym fragmencie** (`LightmapGlsl`), MC 1.8 bierze ją z tekstury 16x16.
- Mesher: `_opaque[256]` liczone od nowa przy każdej sekcji, kopia danych `Data[..Bytes]` (alokacja),
  `Snapshot` przez `Chunk` (OK), pętla po 4096 blokach bez szybkiego pominięcia pełnych/pustych warstw.
- `Update` co klatkę iteruje po wszystkich chunkach i alokuje listy; `Render` alokuje `visible` co klatkę.
- Limiter FPS przez `Thread.Sleep` (rozdzielczość Windows 1-15,6 ms) i brak `timeBeginPeriod`.

### 1.3 Świat i dane

- `World.GetBlock` = `ConcurrentDictionary.TryGetValue` (~15-30 ns). To najczęściej wołana funkcja w grze
  (kolizje, raycasty, eksplozje, światło, redstone, AI). Pierścieniowa tablica chunków wokół gracza daje ~1-2 ns.
- Światło: aktualizacja natychmiastowa przy każdym `SetBlock`. Przy masowych zmianach (eksplozje, `/fill`)
  ta sama okolica jest przeliczana tysiące razy.
- Encje w jednej `List<Entity>`, brak podziału przestrzennego (sekcje 16³ jak w MC 1.8 `entityLists`).

---

## 2. Co robią inni (research)

### 2.1 Render terenu

| Technika | Kto | Zysk | Dla nas |
|---|---|---|---|
| Kompaktowy wierzchołek (20 B, pozycje w 20 bitach względem sekcji) | [Sodium](https://modrinth.com/mod/sodium) | ~40% mniej VRAM i pasma | tak: 16 B, a w ścieżce GL4 docelowo 8 B na quad |
| Wspólny bufor indeksów quadów | Sodium | do 25% mniej pasma | już mamy `SharedQuadIndices` |
| Regiony (8x4x8 sekcji w jednym buforze) + `glMultiDrawElementsBaseVertex` | Sodium | z tysięcy draw calli robi się kilkadziesiąt | tak, to klucz do "tysięcy FPS"; działa w GL 3.3 |
| Podział geometrii sekcji na 6 kierunków + "nieosiowe", rysowanie tylko kierunków widocznych z kamery | Sodium | ~40-50% mniej wierzchołków | tak, prawie za darmo przy multidraw |
| Cave culling: graf widoczności 15 bitów na sekcję + BFS od kamery | [Tommo, MCPE 0.9](https://tomcc.github.io/2014/08/31/visibility-1.html), [część 2](https://tomcc.github.io/2014/08/31/visibility-2.html), też MC Java i Sodium | odrzuca jaskinie i podziemie; BFS daje kolejność od najbliższych | tak, priorytet (CPU, liniowy czas) |
| GPU-driven culling, mesh shadery | [Nvidium](https://modrinth.com/mod/nvidium) (tylko NVIDIA Turing+) | bardzo duży przy dużym RD | nie teraz: tylko NVIDIA, a iGPU to priorytet |
| Hi-Z occlusion + `glMultiDrawElementsIndirect` | silniki GPU-driven ([przykład](https://daslang.io/doc/reference/tutorials/vulkan/12_gpu_driven.html)) | duży przy RD 32+ | opcjonalnie później (GL 4.3), po cave cullingu |
| Binary greedy meshing: maski bitowe 64-bit na kolumnę, culling ścian przesunięciami, 8 B na quad, vertex pulling | [cgerikj/binary-greedy-meshing](https://github.com/cgerikj/binary-greedy-meshing) | 50-200 µs na chunk 62³ | maski bitowe do szybkiego cullingu ścian tak; greedy merge **tylko** gdy AO i światło na wierzchołkach identyczne (wygląd MC musi zostać) |
| Vertex pulling (wierzchołki liczone w shaderze z danych quada) | binary meshing, [Vercidium](https://github.com/vercidium-patreon/meshing) | 4-6x mniej pamięci | ścieżka GL 4.3 (SSBO), z fallbackiem GL 3.3 |
| LOD dalekiego terenu | [Distant Horizons, Voxy](https://distant-horizons-guide.pages.dev/about) | ogromne RD tanio | poza zakresem (MC 1.8 nie ma), ewentualnie na koniec |

### 2.2 TNT i eksplozje

| Technika | Kto | Uwagi o zgodności |
|---|---|---|
| Cache ekspozycji (density cache) w obrębie ticku, klucz: pozycja eksplozji + AABB encji | [PaperSpigot "Optimize explosions"](https://github.com/PaperMC/Paper-1.7/blob/master/Spigot-Server-Patches/0063-Optimize-explosions.patch) | Paper uważa to za zgodne; my dodatkowo unieważniamy cache, gdy w okolicy zmienił się blok, więc wynik jest **identyczny** |
| Cache bloków dla promieni (sąsiednie promienie przechodzą przez te same bloki) | Paper "Actually optimise explosions" (~5x na eksplozję wg autora) | zgodne |
| Łączenie wielu TNT wybuchających w tym samym miejscu w jedną kalkulację odrzutu | [Carpet optimizedTNT](https://bugs-legacy.mojang.com/browse/MC-260762) | zgodne, o ile kolejność i stan świata się nie różni |
| Scalanie TNT w jedną encję z licznikiem, limity, kolejka | [TNTOptimizer](https://modrinth.com/mod/tntoptimizer), [TNTQueue](https://modrinth.com/mod/tntqueue), TNT Fixer | **zmieniają mechanikę** (autorzy sami ostrzegają o działach TNT). Bierzemy tylko wariant dokładny: "stos" identycznych TNT (ta sama pozycja, ruch, lont) |
| Mniej alokacji i cache bloków przy raycastach ekspozycji | [Lithium](https://modrinth.com/mod/lithium/changelog) | zgodne |
| Limit renderu TNT po stronie klienta | TNTOptimizer | tylko render: instancing zamiast limitu |

Własne pomysły wynikające z analizy (zgodne z 1.8):

1. **Siatka przestrzenna encji** (listy na sekcję 16³ jak `Chunk.entityLists` w 1.8): `GetEntitiesInBox`
   z O(N) na O(encje w okolicy). Wynik w tej samej kolejności co dziś (sortowanie po indeksie dodania),
   żeby odrzut działał identycznie.
2. **Szybka ścieżka "samo powietrze"**: maska zajętości bloków (bit na blok, 512 B na sekcję) i licznik
   niepustych bloków w sekcji (już jest `SectionCounts`). Jeśli sfera zasięgu promieni (max ~7 bloków dla mocy 4)
   jest pusta, promienie nic nie niszczą, więc pomijamy 31k lookupów. Ekspozycja: jeśli AABB
   obejmujący eksplozję i encję jest pusty, ekspozycja = 1 bez raycastów.
   Po kilku pierwszych wybuchach w chmurze TNT dookoła jest tylko powietrze, więc 99% eksplozji idzie tą ścieżką.
3. **Lokalny cache bloków eksplozji**: tablica 32³ (`resistance` + id) wokół środka zamiast lookupów
   w słowniku, bitset zamiast `HashSet<(int,int,int)>`. Liczby losowe pobierane w tej samej kolejności co dziś.
4. **Wsadowe niszczenie bloków**: eksplozja zbiera zmiany, potem jedno przejście: zapis bloków,
   jedna aktualizacja heightmapy na kolumnę, światło w kolejce (wsadowe BFS na koniec ticku, jak Starlight),
   `MarkDirty` raz na sekcję. Powiadomienia sąsiadów zostają (mechanika: piasek, woda, redstone), ale z szybkim
   wyjściem dla powietrza.
5. **Równoległy ruch TNT bez zmiany wyniku**: lonty są znane z góry, więc na początku ticku wiadomo,
   które TNT wybuchną. TNT daleko (z zapasem na prędkość) od wszystkich wybuchających w tym ticku nie mogą
   zostać popchnięte ani zmienione przez eksplozje, więc ich ruch liczymy równolegle na wszystkich rdzeniach.
   Reszta (wybuchające i ich okolica) leci sekwencyjnie w oryginalnej kolejności. Wynik bit w bit jak sekwencyjny.
6. **Cząsteczki jak w MC 1.8**: klient MC odrzuca zwykłe cząsteczki dalej niż 16 bloków od kamery
   (`RenderGlobal.spawnEntityFX`), a ustawienie "mniej cząsteczek" obcina resztę. Robimy to samo, plus limit
   dźwięków eksplozji na tick (MC i tak ma ~28 kanałów).
7. **Render TNT przez instancing**: jeden draw call, na instancję 16 B (pozycja, skala, błysk). 100k kostek
   to ~1,2 mln trójkątów: RTX bez problemu, iGPU z LOD/limitem odległości renderu encji.

### 2.3 Światło, tick, wątki

- [Starlight](https://sourceforge.net/projects/starlight-fabric.mirror/) / ScalableLux: bezstanowy silnik światła,
  zmiany zbierane i liczone wsadowo. Wynik końcowy ten sam (BFS daje jednoznaczny stan), zysk przy masowych zmianach.
- **MC 1.8 sam rozdziela grę na dwa wątki**: zintegrowany serwer tickuje świat w osobnym wątku, klient renderuje
  niezależnie. Dlatego w MC przy lagu serwera kamera nadal chodzi płynnie. U nas tick jest w wątku renderu, więc
  ciężki tick zamraża obraz. Docelowo: tick świata w osobnym wątku + migawka stanu encji do renderu (podwójny bufor).
  Wariant przejściowy: budżet czasu ticków na klatkę, żeby render nigdy nie stał dłużej niż ~1 klatkę.
- C2ME (równoległe generowanie chunków): mamy już wątki `ChunkGen`.

### 2.4 .NET

- Zero alokacji w gorących ścieżkach (`List`, `HashSet`, krotki, LINQ, domknięcia), pule tablic (`ArrayPool`),
  `Span`/`stackalloc`. Doświadczenie osu! ([dyskusja](https://github.com/dotnet/runtime/discussions/115627)):
  nawet przy pulach domyślny GC potrafi przyciąć, więc trzeba **nie alokować**, a nie tylko stroić GC.
- `GCSettings.LatencyMode = SustainedLowLatency` w trakcie gry
  ([dokumentacja](https://learn.microsoft.com/es-es/dotNET/standard/garbage-collection/latency)),
  `GC.TryStartNoGCRegion` nie ma sensu przy grze otwartej.
- TieredPGO już włączone; do sprawdzenia `TieredCompilation` + ReadyToRun dla szybkiego startu.
- `timeBeginPeriod(1)` dla dokładnego limitera FPS, a przy "bez limitu" zero `Sleep`.
- SIMD (`Vector256`) w mesherze i światle tam, gdzie pętle są regularne (maski zajętości, inicjalizacja światła).

### 2.5 iGPU (laptopy bez karty)

Wnioski z [porad Intela dla OpenGL](https://www.digit.in/?p=56959) i własnej analizy:
- iGPU dzieli pamięć z CPU, więc **pasmo** jest wąskim gardłem: mniejszy wierzchołek, mniej wierzchołków
  (culling kierunków, cave culling), mniej overdraw (rysowanie od najbliższych, early-Z).
- Bez synchronizacji CPU-GPU (`glFinish`, `glReadPixels`, `glGet*` w pętli).
- Prostszy fragment shader: lightmapa z tekstury 16x16 (jak MC), mgła liczona w vertex shaderze,
  `discard` tylko w przebiegu cutout (liście, trawa), osobno od pełnych bloków, żeby nie psuć early-Z.
- **Skala renderu 3D** (np. 50-100%, HUD w pełnej rozdzielczości): największa dźwignia przy słabym GPU.
- Automatyczny profil "laptop": wykrycie iGPU po `GL_RENDERER` (Intel UHD/Iris, AMD Radeon Graphics bez numeru),
  ustawia: szybkie liście, mniej cząsteczek, RD 6-8, chmury płaskie, skala renderu 75%.

---

## 3. Plan wdrożenia

Zasada: najpierw pomiar, potem zmiana, potem znowu pomiar. Każdy etap = osobny commit, selftest i autotesty
przechodzą. Mechanika nie może się zmienić; tam gdzie ruszamy kolejność albo losowość, dokładamy test
porównujący wynik nowej i starej ścieżki na tym samym ziarnie.

### Etap 0 - benchmarki (punkt odniesienia)
- `--bench render`: stały seed, ustalona trasa kamery, wynik: średnie FPS, 1% low, czas CPU klatki, liczba draw calli.
- `--bench tnt N`: świat płaski, kostka N bloków TNT zapalona w jednym punkcie (i wariant: N zapalonych TNT naraz),
  wynik: czas ticku (średni, max), czas do końca reakcji, FPS w trakcie.
- `--bench mesh`: czas mesha sekcji (średni) na typowym terenie.
- Wyniki zapisywane do `docs/perf/` (CSV), porównanie przed i po.

### Etap 1 - TNT i tick (największy problem)
1. Siatka przestrzenna encji + `GetEntitiesInBox` bez alokacji (z zachowaniem kolejności).
2. Eksplozja: lokalny cache bloków, bitset zamiast `HashSet`, szybka ścieżka "samo powietrze".
3. Ekspozycja: szybka ścieżka pustego obszaru, cache w obrębie ticku z unieważnianiem przy zmianie bloków.
4. Wsadowe niszczenie bloków i światło w kolejce na koniec ticku.
5. `TntEntity.Tick` i `MoveWithCollision` bez alokacji, szybka ścieżka "w powietrzu, nic nie dotyka".
6. Cząsteczki: odcięcie > 16 bloków jak MC 1.8, limit dźwięków na tick.
7. Instancing TNT i bloków spadających.
8. Równoległy ruch encji daleko od eksplozji (deterministyczny).
9. Budżet ticków na klatkę, potem osobny wątek ticku.

### Etap 2 - render terenu (tysiące FPS)
1. Pierścieniowa tablica chunków (`GetBlock` bez słownika), korzyść dla całej gry.
2. Wierzchołek 16 B.
3. Regiony + jeden VAO + `glMultiDrawElementsBaseVertex`, zero alokacji na klatkę.
4. Geometria w 7 kubełkach kierunków, rysowanie tylko widocznych.
5. Cave culling (graf 15 bitów) + kolejność od najbliższych.
6. Lightmapa z tekstury, mgła w vertex shaderze, osobny przebieg cutout.
7. Encje: trwale zmapowany bufor (`glBufferStorage`, GL 4.4, fallback na orphaning), mniejszy wierzchołek.
8. Limiter FPS z `timeBeginPeriod`, tryb bez limitu bez `Sleep`.

### Etap 3 - iGPU
1. Skala renderu 3D.
2. Wykrywanie iGPU i profil "laptop".
3. Test na Intel Iris Xe (laptop dell-f) i porównanie z RTX 5070.

### Etap 4 - dalsze (jeśli zostanie czasu)
- Ścieżka GL 4.3: vertex pulling 8 B na quad, `MultiDrawIndirect`, Hi-Z.
- SIMD w mesherze i inicjalizacji światła, maski zajętości do cullingu ścian.
- Sieć: wsadowanie i kompresja aktualizacji encji (100k TNT u gościa).

---

## Źródła
- [Sodium (Modrinth, changelog)](https://modrinth.com/mod/sodium/changelog?page=3)
- [Nvidium](https://modrinth.com/mod/nvidium), [format wierzchołków Sodium/Nvidium](https://deepwiki.com/drouarb/nvidium/7-data-compatibility-layer)
- [Tommo: Advanced Cave Culling, cz. 1](https://tomcc.github.io/2014/08/31/visibility-1.html), [cz. 2](https://tomcc.github.io/2014/08/31/visibility-2.html)
- [binary-greedy-meshing](https://github.com/cgerikj/binary-greedy-meshing)
- [glMultiDrawElementsBaseVertex (Khronos)](https://registry.khronos.org/OpenGL-Refpages/gl4/html/glMultiDrawElementsBaseVertex.xhtml)
- [GPU-driven Hi-Z + indirect](https://daslang.io/doc/reference/tutorials/vulkan/12_gpu_driven.html)
- [Paper-1.7 Optimize explosions](https://github.com/PaperMC/Paper-1.7/blob/master/Spigot-Server-Patches/0063-Optimize-explosions.patch)
- [MC-260762 (opis Carpet optimizedTNT)](https://bugs-legacy.mojang.com/browse/MC-260762)
- [TNTOptimizer](https://modrinth.com/mod/tntoptimizer), [TNTQueue](https://modrinth.com/mod/tntqueue), [TNT Fixer](https://builtbybit.com/resources/tnt-fixer-raytraced-explosion-engine.112362/)
- [Lithium changelog](https://modrinth.com/mod/lithium/changelog)
- [Starlight](https://sourceforge.net/projects/starlight-fabric.mirror/), [ScalableLux](https://www.github.com/RelativityMC/ScalableLux)
- [Distant Horizons](https://distant-horizons-guide.pages.dev/about)
- [.NET GC latency modes](https://learn.microsoft.com/es-es/dotNET/standard/garbage-collection/latency), [dyskusja o pauzach GC](https://github.com/dotnet/runtime/discussions/115627)
- [Intel: OpenGL performance tips](https://www.digit.in/?p=56959)
