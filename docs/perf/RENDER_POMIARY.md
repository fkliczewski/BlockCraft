# Pomiary renderu (`BlockCraft.exe --stress render --rd N --fps 1000 [--scale P] [--laptop] [--no-cave]`)

Scena: świat domyślny, seed 12345, kamera na Y=110 nad spawnem, pitch 15, okno 1280x720.
Po wczytaniu całego zasięgu 6 s postoju i 6 s obrotu kamery (60 stopni/s). Wynik w `blockcraft.log`
(linie `STRESS render`), zrzut w `BlockCraft_Gra/stress/`. Na RTX pomiar bywa dwumodalny (+-20% między
uruchomieniami tej samej wersji, stan sterownika), więc porównania robione kilka razy.

## RTX 5070 (Filip-PC), postój

| Etap | RD 8 | RD 16 | CPU klatki RD 16 |
|---|---|---|---|
| Przed zmianami (jeden draw call na sekcję) | 2479 FPS | 842 FPS | 1,09 ms |
| Regiony 8x8 chunków + MultiDrawElementsBaseVertex | 3218 | 1011 | 0,95 ms |
| + zasięg rysowania encji jak MC 1.8 (`isInRangeToRenderDist`) | 3150 | 2055 | 0,39 ms |
| + kubełki kierunków ścian, test frustum kolumny | 4000-5000 | 2159 | 0,37 ms |
| + cave culling (VisGraph + BFS jak MC 1.8) | 4000-5000 | ~2000 | 0,40 ms |

Cave culling przy RD 16: sekcje 1143 -> 576, czas GPU 0,149 -> 0,123 ms. Na RTX klatkę ogranicza CPU,
więc FPS stoi; zysk widać na słabszym GPU (niżej).

Obraz po każdym kroku porównany ze zrzutem sprzed zmiany: teren identyczny.

## Bez karty graficznej: Mesa llvmpipe (cały render na CPU Ryzen 9 5900X), RD 6

| Wariant | FPS (postój) | CPU klatki |
|---|---|---|
| Ustawienia domyślne (dokładna grafika, chmury) | 61 | 13,4 ms |
| Profil laptop (`--laptop`: szybkie liście, bez chmur, RD 6, skala 75%) | 110 | 8,2 ms |
| Profil laptop bez cave cullingu | 88 | 10,5 ms |
| Profil laptop, skala 100% | 111 | 6,5 ms |

Na llvmpipe ogranicza przetwarzanie wierzchołków, nie piksele, więc skala renderu nic tu nie daje.
Na zintegrowanych kartach (Intel Iris Xe, UHD) liczy się też wypełnianie pikseli, tam skala 75% pomaga.
Test na laptopie dell-f (Iris Xe) czeka na Filipa: brak zdalnego dostępu do tego komputera.

## Limiter FPS

`timeBeginPeriod(1)` + harmonogram klatek: `--fps 120` daje 120, `--fps 144` daje 144, `--fps 60` daje 60
(wcześniej `Thread.Sleep` z rozdzielczością 15,6 ms dawał przy limicie 120 około 64 FPS).

### Jak zmierzyć na laptopie

Skopiować folder `BlockCraft_Gra` na laptop i uruchomić w nim (gra sama się zamknie po ~1 minucie):

```
BlockCraft.exe --stress render --laptop --fps 1000
BlockCraft.exe --stress render --rd 8 --fps 1000
```

Wyniki są w `blockcraft.log` w tym samym folderze (linie `STRESS render`). Świat testowy "Stress ..."
zostaje w `%APPDATA%\.blockcraft\saves` i można go usunąć.
