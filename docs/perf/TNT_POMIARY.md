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
