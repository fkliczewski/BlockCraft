# Pomiary TNT (headless, `BlockCraft.exe --bench tnt N [--lit] [--limit S]`)

Filip-PC (Ryzen 9 5900X), Release, superpłaski świat, seed 1234, promień 8 chunków.
Bez `--lit`: kostka N bloków TNT zapalona w środku (reakcja łańcuchowa). Z `--lit`: N zapalonych TNT naraz (lont 80).
Suma kontrolna = bloki y 0-63 w obszarze kostki + 40 i pozycje encji; ta sama suma = identyczna mechanika.

## Punkt wyjścia (przed optymalizacją, commit c69e7fc + bench)

| Scenariusz | Ticki | Średni tick | Max tick | Razem | Suma kontrolna |
|---|---|---|---|---|---|
| 1000 łańcuch | 42 | 21,2 ms | 186 ms | 0,9 s | 869C7787761D3080 |
| 10 648 łańcuch | 53 | 155 ms | 511 ms | 8,2 s | 28296578BE42B1CF |
| 1000 zapalone | 80 | 19,4 ms | 1112 ms | 1,6 s | 1D1DA393612864E6 |
| 10 648 zapalone | 80 | 252 ms | **19 572 ms** | 20,2 s | 83513F34CA5F2BD3 |
| 97 336 łańcuch | przerwane po 90 s (38 ticków) | 2474 ms | 9323 ms | > 90 s | - |

Cel: 100k bez spadku poniżej 20 TPS w praktyce (tick < 50 ms), render niezależny od ticku.

## Po E1.1-E1.4 (siatka encji, szybka eksplozja, ruch w powietrzu, render TNT)

| Scenariusz | Ticki | Średni tick | Max tick | Razem | Suma kontrolna |
|---|---|---|---|---|---|
| 1000 łańcuch | 42 | 6,0 ms | 34 ms | 0,27 s | 869C7787761D3080 |
| 10 648 łańcuch | 53 | 33 ms | 72 ms | 1,8 s | 28296578BE42B1CF |
| 1000 zapalone | 80 | 6,1 ms | 300 ms | 0,51 s | 1D1DA393612864E6 |
| 10 648 zapalone | 80 | 30 ms | 1676 ms | 2,5 s | 83513F34CA5F2BD3 |
| 97 336 łańcuch | 62 | 89 ms | 275 ms | 5,5 s | FC2B0D07ED895D5C |

## E1.5: tick cięty na kawałki między klatkami

Gra daje tickom 20 ms na klatkę; ciężki tick dokańcza się w następnych klatkach (zegar sprawdzany co 16 encji
i po każdym wybuchu). Między kawałkami tylko rysuje, więc wynik jest ten sam (`--bench ... --slice MS`
daje identyczną sumę kontrolną). `--no-slice` przywraca stary rytm do porównań.

Bench, najdłuższy kawałek (= najdłuższy przestój klatki z powodu ticku):

| Scenariusz | Max tick (cały) | Najdłuższy kawałek | Suma kontrolna |
|---|---|---|---|
| 10 648 zapalone, `--slice 4` | 1676 ms | 21 ms | 83513F34CA5F2BD3 |
| 97 336 łańcuch, `--slice 16` | 597 ms | 29 ms | FC2B0D07ED895D5C |

Gra w oknie (`BlockCraft.exe --stress tnt N [--lit] --fps 1000`, RTX 5070, od zapalenia do ostatniego wybuchu):

| Scenariusz | Najdłuższa klatka bez cięcia | Najdłuższa klatka z cięciem | Średnio FPS |
|---|---|---|---|
| 10 648 zapalone | 1860 ms | 39 ms | 632 |
| 97 336 łańcuch | 1296 ms | 53 ms | 891 |

Usuwanie martwych encji z listy było O(N^2) (`RemoveAt` po kolei); teraz ściskanie listy w O(N), skutki
uboczne w tej samej kolejności.
