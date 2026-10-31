# Moduł G: multiplayer Tailcat.Link

Aktualizacja po scaleniu: protokół 2, podłączone DimensionSet i kontenery
redstone/alchemii. Test sieciowej zmiany wymiaru i ID gracza przechodzi.
Pełny stan i ograniczenia: [raport integracji](MC18_RAPORT_INTEGRACJI.md).
Wyniki poniżej opisują wcześniejszy test samej gałęzi `mc18/net`.

Stan 2026-10-30, gałąź `mc18/net`. Gra używa Tailcat.Link 0.5.4 i własnego
przekaźnika gier `gry.fimson.pl:443` (region 304, kod `fimson-gry`). Nie wymaga przekierowania portów,
Hamachi ani osobnego procesu serwera. `--server` pozostaje starszą atrapą;
obsługiwany tryb to host w oknie gry.

Przekaźnik (od 2026-10-31): gra używa osobnego przekaźnika gier `gry.fimson.pl`.
`derp.fimson.pl` jest zarezerwowany dla Centrum Domowego i Modus Operandi, gra
nie może z niego korzystać. Kod zaproszenia niesie tylko numer regionu, a nazwę
hosta bierze z mapy DERP w grze, dlatego `RelayGateway` ignoruje zapisany region
(stare profile mają 303) i każdy peer jest łączony przez region 304. Starsze
wydania gry (przed przełączeniem) łączą się przez `derp.fimson.pl` i nie
spotkają się z nowymi: host i gracze muszą mieć wydanie z `gry.fimson.pl`.
Zapamiętane parowania z kluczami działają dalej po aktualizacji.

## Uruchomienie

Host otwiera świat, wybiera Esc -> Otwórz w sieci -> Otwórz świat w sieci.
Ekran pokazuje pełny kod zaproszenia, Kopiuj kod, Nowy kod, Zamknij sieć oraz
listę graczy z pingiem, nadawaniem operatora i wyrzucaniem. Kod Tailcat jest
wielokrotny, ważny godzinę; bywa długi, dlatego jest zawijany na ekranie.

Klient wybiera Tryb wieloosobowy, wkleja kod, ustawia nick i wybiera Dołącz.
Dołącz ponownie (ostatni host) korzysta z zapisanego parowania bez kodu.
Host musi nadal mieć otwarty świat w sieci. Obie strony powinny używać tego
samego wydania gry z tymi samymi rejestrami bloków, itemów i encji.

Klucze i parowania: `%LOCALAPPDATA%\BlockCraft\net\host` lub `client`.
`--net-profile nazwa` wybiera foldery `nazwa-host` / `nazwa-client`, dzięki czemu
dwie instancje na jednym komputerze mają osobne tożsamości. Nazwa: do 48 znaków
ASCII (litery, cyfry, `_`, `-`). Dane świata i graczy pozostają w zwykłym zapisie
świata w `%APPDATA%\.blockcraft\saves`.

## Podział odpowiedzialności

| Plik | Zadanie |
|---|---|
| `Net/Relay.cs` | Własna mapa DERP i `INodeGatewayFactory`, bez publicznych przekaźników Tailscale |
| `Net/Transport.cs` | Połączenie kolejkowane, host `HostManyAsync`, klient, parowanie i profile |
| `Net/Protocol.cs` | Wersja protokołu, typy pakietów, ramki, input i kompresja chunków |
| `Net/NetServer.cs` | Logowanie, uprawnienia, śledzenie encji, słuchacze świata i zapis graczy |
| `Net/RemoteClient.cs` | Zdalny gracz hosta, jego kontroler, handler, teren, ekwipunek i okna |
| `Net/NetClient.cs` | Odbiór pakietów, świat zdalny, interpolacja i wysyłanie akcji |
| `Net/GameNet.cs` | Integracja z pętlą gry, host/klient, rozłączanie i nicki nad głowami |
| `Net/NetScreens.cs` | Ekrany sieciowe |
| `Net/NetHooks.cs` | Integracja wymiarów i stanu świata innych modułów |

Host wykonuje logikę bloków, mobów, obrażeń, kontenerów i poleceń. Zdalny gracz
na hoście ma `Player.NetPuppet = true`, własny `PlayerController(world, player)`
i handler `RemoteClient`. Klient przewiduje swój ruch i akcje; host odsyła
stan bloków, slotów i teleporty z potwierdzeniem. Obecna kontrola ruchu
sprawdza skończone współrzędne i duże skoki; nie jest pełną symulacją ruchu
ani systemem wykrywania oszustw.

`NetBeforeTick` odbiera akcje, po nim działa `World.Tick`, a `NetAfterTick`
wysyła stan. Sieć pracuje również podczas ekranu ładowania. Menu pauzy nie
zatrzymuje otwartego hosta ani klienta.

Nick jest przypisany do klucza publicznego Tailcat w
`players/net_names.json` w zapisie świata. Inny klucz otrzymuje inny dostępny
nick. Dane gracza zapisują się po nicku; po powrocie z tym samym kluczem i
nickiem wraca jego ekwipunek i postęp. Operatorzy są zapisywani w tym samym
pliku. Polecenia zmieniające świat wymagają operatora; lista pomocniczych
poleceń bez operatora jest w `NetServer.FreeCommands`.

## Protokół 1

Własny protokół BlockCraft, nie protokół serwerów Minecraft Java. Aplikacja
Tailcat: `blockcraft`, maksymalnie 8 sparowanych peerów. Oddzielny uporządkowany
kanał na kierunek: `blockcraft-c2s` i `blockcraft-s2c`. Jedna ramka kanału
zawiera kolejne pakiety:

```text
[int32 długość samej treści][byte typ][treść]
```

Liczby zapisuje `BinaryWriter` (little-endian), tekst przez `Write(string)`
(UTF-8 z prefiksem długości .NET). Dokładny układ pól i wartości typów są
w `Protocol.cs` oraz metodach zapisu i odczytu. Pierwszy pakiet to
`C2S.Login`: wersja, nick, zasięg widzenia. Host odpowiada `LoginOk` lub
`Disconnect` z powodem po polsku. Niezgodna wersja odrzuca logowanie.

| Kierunek | Grupy pakietów |
|---|---|
| C2S | Login, Input, Chat, Click, Distribute, CloseWindow, CreativeSlot, CreativeDrop, DropSelected, Respawn, KeepAlive |
| S2C | LoginOk, Disconnect, ChunkData, UnloadChunk, BlockChanges, TileEntity |
| S2C | SpawnEntity, EntityMoves, EntityData, EntityEvent, EntityEquip, RemoveEntities |
| S2C | Sound, Particles, Time, Settings, Chat, Stats, InventorySlots, Cursor |
| S2C | OpenWindow, WindowSlots, CloseWindow, Teleport, Velocity, Death, PlayerList |
| S2C | WorldState, ChangeDimension, KeepAlive |

`NetInput` niesie pozycję i orientację, flagi ruchu, wybrany slot, przyciski,
trafiony blok/encję i potwierdzenie teleportu. Ruch pozostałych encji jest
kwantowany do 1/32 bloku, kąty do bajtu; klient interpoluje go.
`EntityEvent` obsługuje m.in. uderzenie, zamach i śmierć. Wyposażenie niesie
przedmiot w ręce i cztery sloty zbroi. Stack zachowuje także swój NBT.

Chunk jest przesyłany z kompresją Deflate (bloki, meta, biomy i tile entity),
świat klienta ma `IsRemote = true` i nie generuje terenu. Zmiany bloków lecą
oddzielnie, a tile entity są sprawdzane co 4 ticki. Dane encji pochodzą
z `Save`/`Load`; pozycja i animacje mają także osobne pakiety. Kontenery
przesyłają opis układu slotów, zawartość i kursor, a klik wykonuje host.

## Jak dodać zawartość innego modułu

- **Blok/item:** zarejestruj jak w grze lokalnej. Synchronizacja używa ID i meta
  oraz pełnego `ItemStack`; nie trzeba dopisywać typu pakietu.
- **Encja:** zarejestruj klucz przez `Registries.Mob` / `Registries.Entity`,
  dodaj renderer i symetryczne `Save`/`Load` z wywołaniem bazowej implementacji.
  Dane potrzebne klientowi muszą być w zapisie lub `NetData`.
  AI, spawn i skutki interakcji wykonuj wyłącznie po stronie hosta
  (`World.IsRemote` / `Entity.IsRemoteControlled`).
- **Tile entity:** zarejestruj unikalny `TypeId` przez `Registries.Tile`, zgodny
  z zakresem modułu z `MC18_MODULY.md`, i implementuj `Save`/`Load`.
  Sieć stosuje zapis przez `Load`. `TileEntity.SetData(string, string)` to
  osobny hak konfiguracji zawartości przez inne moduły, a nie pakiet sieciowy.
- **Kontener:** zarejestruj fabrykę w `ContainerFactory.Containers[GuiKind]`.
  Korzystaj z `IPlayerHandler.OpenBlockGui`, aby host tworzył ten sam kontener
  co gra lokalna. Opcjonalny ekran zarejestruj w `ContainerFactory.Screens`.
  Gdy klient nie utworzy konkretnego typu, dostanie `NetContainer` z układem
  slotów od hosta. Dodatkowe pola GUI poza slotami (np. wskaźniki maszyn)
  wymagają stanu w TE lub osobnego rozszerzenia; same sloty ich nie opisują.
- **Dźwięki/cząsteczki:** wywołuj `World.PlaySound` / `World.SpawnParticles`.
  Słuchacz hosta rozsyła je, pomijając aktora, który przewidział własny efekt.

Przykład rejestracji GUI w inicjalizacji modułu:

```csharp
ContainerFactory.Containers[(GuiKind)20] = (w, p, x, y, z) =>
    w.GetTileEntity(x, y, z) is MyTile te
        ? new MyContainer(p.Inventory, te) : null;
```

### Wymiary NE i pogoda OW

Przy scalaniu NE ustaw `NetHooks.DimOf`, `WorldsOf` i `CreateRemoteWorld` na
implementacje `DimensionSet`. Domyślnie są to: wymiar 0, tylko bieżący świat
hosta i pusty zdalny Overworld. `WorldsOf` musi zwracać wszystkie światy,
w których mogą przebywać zdalni gracze. Serwer śledzi zmianę `Player.World`
i wysyła `ChangeDimension`, czyści śledzone chunki/encje, a potem teleportuje.
Pakiety terenu i TE mają pole wymiaru typu `sbyte` (0, -1, 1). Pakiety encji
dotyczą bieżącego świata klienta określonego przez `ChangeDimension`.
Sama obecność haków nie uruchamia wymiarów - potrzebne jest podłączenie NE.

Stan pogody lub innego modułu zarejestruj w
`NetHooks.WorldState["ow.weather"] = (Write, Read)`, gdzie `Write(World)` zwraca
`byte[]`, a `Read(World, byte[])` stosuje go na kliencie. Host sprawdza zmianę
około raz na sekundę. Nie wysyłaj tak dużego stanu jak teren.
`OnPlayerChangedWorld` pozostaje zarezerwowanym hakiem, bez wywołania w obecnej
pętli; zmiana świata jest wykrywana bezpośrednio z `Player.World`.
Pogoda czeka na scalenie i rejestrację OW.

## Weryfikacja

```powershell
dotnet build src/BlockCraft -c Release
dotnet src/BlockCraft/bin/Release/net10.0/BlockCraft.dll --selftest --selftest-output selftest.log
powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/Test-Net.ps1
```

Skrypt Windows uruchamia dwa prawdziwe okna z osobnymi profilami, używa
`gry.fimson.pl`, zapisuje PNG, logi i `host.result` / `join.result` w nowym
folderze `.modus/net-e2e-*`. Nie wymaga ręcznego przekazywania kodu.
Można wskazać nowy folder przez `-OutputDirectory`; istniejący jest odrzucany,
aby stary sygnał nie zaliczył kolejnego testu. Procesy mają limit czasu.
Wynik skryptu zależy od obu plików wynikowych. Test tworzy nowy zapis świata,
zachowuje profile parowania i przywraca wcześniejsze ustawienia przy wyjściu.

Sprawdzone 2026-10-30: 36 testów sieci w `--selftest` przechodzi (ramki,
kompresja, bloki, encje, ekwipunek, kontener, czat i uprawnienia, teleport,
śmierć/respawn, rozłączenie, FakeDerpRelay i ramka 200 KB). Pełny przebieg:
148 OK / 0 FAIL. Powtórka: 147 OK / 1 FAIL w rozmnażaniu krów. Na bazowym
commicie `b2fb2f2`, sprzed sieci, uzyskano 112 OK / 0 FAIL oraz 110 OK / 2 FAIL
w rozmnażaniu i młodych krów - to istniejąca niestabilność testu mobów.

Rzeczywisty test `.modus/net-e2e-reviewed`: host i klient OK, obejrzane zrzuty
obu modeli z nickami i tego samego złotego bloku. Zweryfikowano logowanie kodem,
chunki, czat w obie strony, 3 diamenty w ekwipunku, nadanie operatora,
wyrzucenie, powrót bez kodu z zachowanym ekwipunkiem i zamknięcie sieci hosta.
Test dotyczy dwóch procesów na jednym komputerze przez prawdziwy przekaźnik;
nie zastępuje próby na dwóch osobnych komputerach.

Zwykły `--autotest` także zakończył się `AUTOTEST DONE`; kopanie i stawianie
mają wynik OK, obejrzano zrzut świata gry jednoosobowej.

Pozostałe ograniczenia: pęknięcia bloku kopanego przez innego gracza nie są
rozsyłane, a renderer gracza nie rysuje zbroi mimo synchronizacji jej slotów.
Integrację NE/OW i specjalnych wskaźników nowych GUI trzeba sprawdzić po
scaleniu modułów. Nie ma deklaracji zgodności różnych zestawów modułów
o tej samej wersji protokołu.
