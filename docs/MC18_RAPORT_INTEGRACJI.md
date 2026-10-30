# Minecraft 1.8: raport integracji, 2026-10-30

Gałąź: `feature/mc18`. Kod pozostaje w C#/.NET 10. Ten raport zastępuje nieaktualne
statusy "gotowe do scalenia" w planie poprzedniej sesji. Cały zakres MC 1.8 1:1
NIE jest zakończony. Zakończono scalenie istniejących modułów i naprawy integracji.

## Co połączono

- `mc18/rs` do c351f6f: pył, pochodnie, przekaźniki, tłoki, QC/BUD, szlam,
  komparatory, płyty, linka z hakami, czujnik światła, leje, dozowniki, podajniki,
  blok nutowy i skrzynia-pułapka. Testy obejmują m.in. limit tłoka, blokadę
  przekaźnika, przepalenie pochodni, krótki puls, transfer leja i odczyt komparatora.
- `mc18/netherend` do a20f9ae: zapis wymiarów DIM-1/DIM1, generatory,
  aktywowanie portali i podróż, ogień, ładunki ogniste, oczy Endu, forteca,
  zombie pigman, ghast, płomyk, kostka magmy, witherowy szkielet i endermit.
- `mc18/overworld` do 1bc3724: rejestr 59 biomów, stos GenLayer,
  drzewa, rośliny i budulce. Test biomów porównuje 48 map z zapisanymi
  wynikami MC 1.8.9; nie dowodzi zgodności całego terenu 1:1.
- `mc18/mechanics` do 6c62cef: efekty statusu, żywność, warzenie, mikstury,
  pociski mikstur, kocioł i ekran statywu.
- `mc18/mobs` do 54feb61: odzyskany, częściowy moduł MO. Wciąż WIP,
  mimo obecności klas i jajek w rejestrze. Nie traktować jako kompletnych mobów.
- `mc18/net` do 2aebe0b: host w grze, Tailcat.Link 0.5.4, kod zaproszenia,
  ponowne dołączanie, synchronizacja i czat. Audio było już scalone.

## Naprawy po scaleniu

- Konflikty scalania rozwiązane z zachowaniem testów wszystkich modułów,
  obsługi audio, tykania wymiarów i cyklu życia sieci.
- Brakująca klasa Horse blokowała kompilację MO. Dodano podstawowe atrybuty,
  oswajanie, karmienie, siodłanie, jazdę i zapis. Brakuje pełnego modelu,
  ekwipunku konia, zbroi, skrzyń i dokładnego dziedziczenia.
- Warzenie: operator `&` sprawdza wymagany bit i zeruje wartość przy jego braku.
  Poprzedni parser pomijał ten operator. Poprawiono też błędny wynik oczekiwany
  prochu: 8194 -> 16386, oraz test statywu, który próbował warzyć brodawkę
  w już niezręcznej miksturze. Licznik statywu startuje od 400, po czym odlicza.
  Punkt odniesienia: [PotionHelper 1.8.9](https://github.com/Marcelektro/MavenMCP-1.8.9/blob/master/src/main/java/net/minecraft/potion/PotionHelper.java).
- GUI statywu było utracone przy scaleniu sieci. Lej/dozownik/skrzynia-pułapka
  otwierały się tylko lokalnemu Game. Wszystkie używają teraz ContainerFactory
  i IPlayerHandler; test obejmuje otwarcie u gościa oraz widza skrzyni-pułapki.
- NetHooks zwracają rzeczywisty wymiar i wszystkie światy DimensionSet.
- Zmiana wymiaru odświeża kontroler zdalnego gracza i jego ID na kliencie;
  protokół podniesiono do 2. Wymagane jednakowe wydanie hosta i gościa.
- Centra ładowania chunków aktualizowane są dla graczy każdego wymiaru.
- Wczytanie zdalnego gracza odtwarza zapisany wymiar. Host przechowuje dane
  sieciowe w Overworldzie także wtedy, gdy otwiera sieć z Netheru.
- Odrodzenie z Netheru/Endu przenosi do Overworldu, a transfer zamyka kontener.
- Kreatywny ma dostępne zakładki Transport i Alchemia. Napis .NET/C# EDITION
  i poprawka orientacji ikon zostały zachowane.

## Weryfikacja

- Release: kompilacja przechodzi; pozostają 4 wcześniejsze ostrzeżenia.
- Selftest wydania w `BlockCraft_Gra`: **318 OK, 0 FAIL**; log lokalny
  `.modus/integration-selftest-final.log`.
- Pełny autotest: `AUTOTEST DONE`; wszystkie raportowane warunki redstone OK.
  Obejrzano zrzut zasilonych lamp/tłoków oraz ekwipunku z poprawną orientacją ikon.
- `--autotest --ne`: `NE-TEST KONIEC, błędów: 0`. Sprawdzono zapalanie portalu,
  podróż do Netheru i powrót, ogień/deszcz, oko Endu i przejście do Endu.
  Zrzuty zachowane lokalnie w `.modus/integration-portals`.
- Dwa procesy przez rzeczywisty DERP: `.modus/net-e2e-final/host.result` i
  `join.result` mają OK. Kod, teren, bloki, ekwipunek, czat, operator,
  wyrzucenie i powrót bez kodu sprawdzone. Nie jest to test dwóch komputerów.
- Test podróży sieciowej między wymiarami wykonano przez połączenie w pamięci.
- Nie weryfikowano dźwięku na słuch. Logi i stan audio nie zastępują odsłuchu.

## Pierwsze niezakończone zadania

1. MO: pełne modele i skórki dodatkowych mobów (część klas nie ma renderera),
   naturalny spawn według biomu, wieśniak i handel (RegisterVillagers18 nadal
   niezaimplementowane), ekwipunek konia, smycze i kompletna zgodność AI.
2. NE: smok, kryształy, Wither, wyjście z Endu i napisy; dokładna generacja
   Netheru/Endu zamiast uproszczonych generatorów.
3. RS: tory i wagoniki; dalsza weryfikacja kolejności aktualizacji i złożonych
   konstrukcji. Zestaw testów nie dowodzi zgodności każdego układu z MC 1.8.
4. ME: zaklinanie i działanie zaklęć, kowadło, mapy, wędka, tabliczki, ramki,
   obrazy, stojaki, sztandary, łódki, fajerwerki, osiągnięcia i pozostałe itemy.
5. OW: pogoda oraz brakujące struktury; sprawdzenie zgodności terenu i dekoracji.
6. Sieć: wskaźnik czasu warzenia, renderer zbroi graczy, pęknięcia kopania,
   test dwóch komputerów i podróży przez portal w tym teście.

## Co się wywalało w prowadzeniu pracy

Źródła: pięć raportów wskazanych przez użytkownika i lokalny dziennik
`.modus/PROBLEMY_ORKIESTRACJI.md`. Poniższe problemy historyczne pochodzą z tych
zapisów; nowa sesja nie odtworzyła błędów samej aplikacji Modus Operandi.

- Limity Codex/Claude przerywały pracę, a przekazanie nie zawsze następowało.
  Obecne przejęcie dostało opis limitu Claude 5 h i odzyskało pracę z Git.
- Nowe kafelki pozostawały bez pierwszej wiadomości i planu. Status Working
  bywał wyświetlany mimo braku zmian przez wiele godzin.
- Dialog zaufania do folderu blokował część startów; waitingForUser pozostawał
  pusty. Nie jest potwierdzone, że tłumaczy to wszystkie nieudane starty.
- Reset limitu nie wznawiał zatrzymanych agentów. Master również nie dostawał
  sygnału umożliwiającego szybkie przejęcie.
- Raport częściowy automatycznie oznaczał krok jako gotowy do scalenia, także
  przy dwóch FAIL lub niedokończonych krokach. Z tego powodu sprawdzono kod
  i rzeczywiste testy, zamiast ufać samym znacznikom planu.
- Zamknięcie kafelka nie zwalniało przydziału kroku, wymuszając zastępcze kroki.

W bieżącym przejęciu rzeczywiste błędy repo to brak Horse, konflikty scalania,
parser warzenia i błędy integracji GUI/wymiarów opisane wyżej. Zapisano je
oddzielnie od problemów orkiestracji. Nie naprawiano aplikacji Modus Operandi
ani jej ustawień zaufania - nie są częścią tego repozytorium.
