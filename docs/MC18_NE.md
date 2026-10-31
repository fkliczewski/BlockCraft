# Moduł NE

Wymiary mają identyfikatory `Overworld = 0`, `Nether = -1`, `End = 1`.
Plan integracji: `DimensionSet` jest właścicielem leniwie tworzonych instancji `World`.
Każdy świat ma własne chunki, encje, graczy, generator, oświetlenie i `ChunkManager`.
Zapis chunków Netheru trafia do `DIM-1`, Endu do `DIM1`; dane gracza pozostają w katalogu sesji.
Przeniesienie encji usuwa ją z indeksów starego świata i rejestruje w docelowym świecie.
Host multiplayer powinien posiadać jeden `DimensionSet` na sesję i aktualizować centra
ładowania osobno dla graczy każdego wymiaru. Renderowanie klienta obserwuje wyłącznie aktywny świat.

Zmiany zawartości są w osobnych plikach częściowych modułu NE. Identyfikatory należą
do zakresów zadeklarowanych w `BlockIds.cs` i `Item.cs`.

Weryfikacja: `dotnet build -c Release`, `BlockCraft.exe --selftest`, `BlockCraft.exe --autotest`.
Status implementacji i znane różnice zostaną uzupełnione po testach.
