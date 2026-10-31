# Minecraft 1.8: raport integracji, 2026-10-30

## Odzyskany Overworld, 2026-10-31

Zabezpieczono niecommitowaną pracę Momo na mc18/overworld jako f55f692
(wioski, twierdze, monumenty, lokatory i wzorce). Osobna gałąź przeszła
167 testów; następnie scalono ją wraz z wcześniejszym terenem, dekoratorami,
jaskiniami, kopalniami, lochami i świątyniami do feature/mc18.

Wykryto brak wykonania planów encji struktur: istniały w cache generatora,
ale znikały przed integracją chunka. Dodano jednorazowe tworzenie na wątku
świata, przekazanie profesji wieśniaka, wariantu starszego strażnika oraz
niezależnej kopii łupu wagonika. Testy sprawdzają brak powielania i zapis.
Wszystkie testy scalonej gry: **499 OK / 0 FAIL**,
`.modus/ow-population-selftest.log`; Release kompiluje się bez błędów.

Dodano `--autotest --ow`. Przebieg kończy się `OW-TEST KONIEC: OK` i robi
zrzuty wioski, chatki/świątyni i monumentu. Obejrzano wszystkie trzy: wioska
i chatka są widoczne, ale kamera monumentu pokazuje głównie wodę. Ujęcie
wymaga poprawy; sam znacznik OK nie jest dowodem weryfikacji wyglądu monumentu.
Nadal do zrobienia: pogoda i jej zapis/sieć/render/dźwięki, spawn zwykłych
strażników w monumentach i pełny autotest OW. Nie oznaczono całego OW jako
ukończonego. Wydania BlockCraft_Gra jeszcze nie odświeżono o te zmiany.

## Kontynuacja integracji, 2026-10-31

Goście multiplayera mają teraz właściwe ekrany ekwipunku konia, kontenerów
wagoników i handlu. Host wysyła opis kontenera i stan encji/ofert, odbiera
wybór oferty z identyfikatorem okna i rozlicza zapłatę, wynik oraz limit użyć.
Klient nie wylicza własnego wyniku handlu i nie zwraca sam przedmiotów przy
zamykaniu GUI. Stanowiło to błąd przy zmianie okna z równoczesnym nadejściem
nowej zawartości kursora; pierwszy zrzut testowy wykazał powielenie pszenicy.
Poprawiono wspólny Container.OnClose i powtórzono test z asercją braku kopii.
Host odrzuca błędne i spóźnione wybory, zamyka oddalone kontenery encji.
Protokół podniesiono do **3**; host i goście potrzebują tego samego wydania.

Weryfikacja: **467 OK / 0 FAIL** w `.modus/entity-windows-verified.log`.
Test dwóch procesów przez `gry.fimson.pl` zakończony OK dla hosta i gościa:
`.modus/entity-windows-e2e-fixed`. Sprawdza siodłanie przez GUI, odbiór
ładunku wagonika, zmianę oferty, zapłatę i wyczerpanie oferty bez powielenia,
a następnie wyrzucenie, powrót i zamknięcie sieci. Obejrzano zrzuty konia,
wagonika i handlu; ostatni zrzut nie zawiera już powielonej pszenicy.
Build/publish Release przechodzą, wydanie `BlockCraft_Gra` odświeżone.

Testy układów redstone mają wyłączony naturalny spawn od początku scenariusza,
aby obce moby nie popychały wagoników. Dopisano diagnostykę pozycji i meta
w razie błędu odblokowania leja. To izolacja testu; nie zmieniono fizyki gry
i nie uznano przypuszczenia o źródle wcześniejszej niestabilności za dowiedzione.
Do dalszej integracji pozostają m.in. części smoka w protokole, jazda
pojazdami przez sieć, OW i niedokończone mechaniki ME. Historyczne braki GUI
w raportach poniżej zastępuje ta aktualizacja.

## Priorytet audio, 2026-10-31

Zgłoszenie: odgłosy nakładały się i robiły bardzo głośne. Mikser sumował
do 32 głosów, a przepełnienie obcinał niezależnie w kanałach do -1/+1,
co powodowało przesterowanie. Nie ograniczał duplikatów zdarzenia.

Poprawiono mikser: 6 dB zapasu na wyjściu, wspólny limiter stereo do 0.65
z natychmiastowym atakiem i powrotem 150 ms zamiast obcinania fali.
Zduplikowane zgłoszenia tego samego efektu i tonu w tym samym miejscu
w ciągu 33 ms są odrzucane; maksymalnie dwie kopie efektu jednego źródła.
Osobne źródła i różne efekty pozostają równoczesne, nuty są wyłączone
z tego ograniczenia. Zachowano suwaki użytkownika; nie zmieniano options.json.

Dźwięki poza zasięgiem nie zajmują głosów. Zmiana świata od razu usuwa jego
odgłosy, zostawiając muzykę. Świat w tle menu nie emituje efektów do miksera.
Iloczyn głośności zdarzenia i wariantu jest ograniczany do 1.

Weryfikacja: 13 testów audio, w tym 32 jednoczesne źródła, panorama stereo,
powrót limitera, cisza, duplikaty, nuty, cykl życia i granice buforów.
Pełny selftest: **456 OK / 0 FAIL**, `.modus/audio-final-selftest.log`.
Uruchomiona gra: audio enabled=True, odtwarzanie testowych efektów potwierdzone
aktywnymi głosami, pełny autotest zakończony `AUTOTEST DONE`.
To testy cyfrowego sygnału i działania gry; nie wykonano odsłuchu głośników.
Zaktualizowano wydanie `BlockCraft_Gra`.

Wstępna weryfikacja sprzed poprawek wykazała niestabilność wcześniejszego testu
toru aktywacyjnego (`relay-wave1-selftest.log`, 442 OK/1 FAIL). Ostatni przebieg
przeszedł, ale problem pozostaje do osobnej diagnozy po priorytecie audio.

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

1. MO: uzupełnić oferty zależne
   od przedmiotów ME i sprawdzić zgodność AI. Modele, spawn według biomu,
   wieśniacy, handel i ekwipunek konia już scalone - aktualizacja Tory poniżej.
   GUI konia, handlu i wagoników podłączono do sieci 2026-10-31.
2. NE: ukończyć podłączenie przywoływania Withera do prawdziwych czaszek ME;
   sprawdzić zgodność generatorów i układu fortec z oryginałem. Smok, kryształy,
   Wither i napisy są już scalone - patrz aktualizacja Hiro poniżej.
3. RS: dalsza weryfikacja kolejności aktualizacji, złożonych konstrukcji i
   transportu w multiplayerze. Tory i wagoniki już scalone - aktualizacja Nami
   poniżej. Testy nie dowodzą zgodności każdego układu z MC 1.8.
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

## Aktualizacja po raporcie Hiro, 12:59

Przeczytano `.modus/raporty/20261030-125922-Hiro.md`. Scalono nowe commity
1c82e2e i 5bb8463 z `mc18/netherend` do `feature/mc18` (merge 39e4763).
Obejmują rozszerzoną generację Endu i filary, kryształy, smoka z częściami
ciała, leczenie i śmierć, portal wyjściowy z jajem, Withera, pasek bossa
i napisy końcowe. Nie deklarujemy na podstawie raportu pełnej zgodności 1:1.

Sprawdzono na scalonym kodzie: Release OK, selftest 318/0 (w tym testy sieci),
`--autotest --ne` zakończony `NE-TEST KONIEC, błędów: 0` i `AUTOTEST DONE`.
Obejrzano ne_13_dragon i ne_20_wither_armor. W tym przebiegu nie testowano
walki z bossami przez sieć. Szczególnie DragonPart wymaga integracji z protokołem
(brak rejestracji dragon_part, lokalne ID części klienta nie są ID hosta).
Przywołanie Withera w autoteście używa zamiennika czaszek - nie dowodzi
działania przywoływania za pomocą przedmiotu czaszki w zwykłej grze.

Naprawy integracyjne po tym scaleniu: uniknięto pokazania napisów końcowych
przy odrodzeniu po śmierci w Endzie; przemianowano pomocniczą metodę Withera,
która zasłaniała właściwość LivingEntity.IsUndead. Pełna lista braków z innych
modułów pozostaje aktualna. Napisy używają własnego tekstu BlockCraft.

Powtórka testów gotowego wydania ujawniła wcześniejszą niestabilność krów:
316 OK / 2 FAIL (liczba krów spadła z 2 do 0). Test rozmnażania współdzielił
świat z wcześniejszymi walkami i włączonym naturalnym spawnem potworów.
Odizolowano go w osobnym świecie bez naturalnego spawnu; reguły rozmnażania
w grze nie zostały zmienione. Log pierwszej nieudanej próby pozostawiono
w `.modus/hiro-published-selftest.log`, wyniki po poprawce w
`.modus/hiro-published-fixed.log` i `.modus/hiro-repeat-*.log`.

## Aktualizacja po raporcie Tory, 16:48

Przeczytano `.modus/raporty/20261030-164854-Tora.md`. Scalono `mc18/mobs2`
do 4f9ac95 przez fast-forward. Usunięto tymczasowy Horse.cs na rzecz
Horses18.cs. Dodano pełniejszą obsługę koni i wariantów, zbroi, skrzyń,
dziedziczenia, skoku ładowanego i GUI; wieśniaków z karierami i handlem;
oswajanie zwierząt, golemy budowane z bloków, smycze oraz modele i skórki.
MobSpawner ma cztery kategorie i listy biomów; skórki są w tablicy 128x128
z przeskalowaniem dotychczasowych warstw 64x64.

Własna weryfikacja po scaleniu: Release OK, **379 OK / 0 FAIL** w selftest
(`.modus/tora-integrated-selftest.log`), autotest `--autotest --mo` zakończony
`MO-TEST KONIEC: OK` oraz `AUTOTEST DONE`. Obejrzano mo_06_konie,
mo_17_gui_kon i mo_19_gui_handel. Zrzuty zachowane w
`.modus/tora-integrated-autotest`; odświeżono wydanie `BlockCraft_Gra`.
Pięć udanych powtórek z raportu Tory jest wynikiem jego testów, a nie pięcioma
dodatkowymi przebiegami wykonanymi podczas integracji.

Ograniczenia: brakujące przedmioty ME powodują pomijanie zależnych od nich
ofert; spawn strażników przy monumentach wymaga integracji ze strukturami OW.
Przegląd kodu ujawnił też, że Player.OpenEntityContainer otwiera ekran wyłącznie
przy Handler typu Game. GUI konia i handlu nie jest jeszcze podłączone do
RemoteClient, a wybór oferty wymaga komunikatu klient-host. Nie deklarować
pełnej obsługi tych mechanik dla gościa sieciowego. Zgodność całości 1:1 nadal
wymaga dalszych testów.

## Aktualizacja po końcowym raporcie Nami, 18:43

Przeczytano oba raporty `.modus/raporty/20261030-184310-Nami.md` i
`20261030-184341-Nami.md`. Pierwszy wstrzymywał scalanie do zakończenia zapisu
pasażerów. Po drugim scalono `mc18/rs` do 23827fc: cztery typy torów, dziesięć
kształtów, zakręty, podjazdy, zwrotnice, propagację zasilania, pięć typów
wagoników, kontenery, pasażerów, piec, lej, TNT, dozowanie oraz receptury.
Zachowano obie zakładki Transport i Alchemia, wszystkie rodzaje drewna
w recepturach oraz haki używania przedmiotów i odbijania kul ognia.

Pierwszy selftest po scaleniu ujawnił rzeczywisty błąd integracji MO/RS:
przeciążenie Mob.Mount(Player) przejmowało wywołanie cow.Mount(null),
powodując NullReferenceException. Dodano obsługę wysiadania przez bazowe
Entity.Mount(null). Log nieudanej próby zachowany jako
`.modus/nami-integrated-selftest.log` (396 OK, 1 FAIL, dalsza część testów
RS przerwana wyjątkiem). Po poprawce **442 OK, 0 FAIL** w
`.modus/nami-integrated-fixed.log`; Release ma 0 błędów i 4 ostrzeżenia.

Autotest `BC_AUTOTEST=rs` zakończony `AUTOTEST DONE`, warunki transportu OK.
Obejrzano rs_10_tory, rs_11_wagoniki i rs_15_transport. Zrzuty zachowane
w `.modus/nami-integrated-autotest`; zaktualizowano `BlockCraft_Gra`.
Sam raport Nami podaje 221 testów na jej gałęzi; 442 to wynik scalonego kodu.

Nie testowano jazdy i odtwarzania relacji pasażera przez sieć. GUI kontenerów
wagonika używa RsGui.Open (wymaga lokalnego Game), więc udane testy GUI
jednoosobowego nie oznaczają obsługi kontenerów wagonika dla gościa sieciowego.

## Kontynuacja OW po przekazaniu sesji, 2026-10-31

Commit 272253a poprawia podwodną kamerę autotestu monumentu i weryfikuje
gotowość wszystkich chunków jego obszaru. Przebieg `--autotest --ow`
zakończył się OK: 12873 bloki prismarynu, 116 latarni, 8 bloków złota.
Obejrzano zrzut monumentu: widoczna schodkowa bryła i szczyt pod wodą.
Pierwsza próba kamery przy zewnętrznym narożniku była zbyt odległa i ciemna;
ostateczna kamera jest bliżej szczytu. Nie zmieniano mgły ani geometrii gry.
Dowody: `.modus/ow-monument-autotest.log`, `.modus/ow-monument-final/`.

Dodano podstawę pogody według `World.updateWeather` z referencji 1.8:
oddzielne zegary deszczu, burzy i wymuszonej dobrej pogody, płynne natężenie,
przyciemnienie światła nieba, reset po śnie, komendę `/weather` z czasem
w sekundach oraz zapis w opcjonalnym polu `level.json` (starsze zapisy działają).
Stan przechodzi przez `NetHooks.WorldState`, klient wygładza opady samodzielnie,
bez przełączania zegarów hosta. Podłączono hak opadów dla ognia i kotła;
deszcz gasi płonące encje i uwzględnia dach, biom oraz temperaturę.

Release: 0 błędów, 4 istniejące ostrzeżenia. Selftest: **516 OK, 0 FAIL**,
w tym 15 nowych testów podstawy pogody i 2 testy komend pogody przez transport
host–klient (`.modus/ow-weather-selftest.log`). Nie wykonano jeszcze testu
pogody w dwóch graficznych oknach przez relay.

To nie jest ukończenie OW: pozostały grafika i dźwięk opadów, pioruny,
akumulacja śniegu/lód, dalsze interakcje z pogodą oraz naturalny spawn
strażników w monumentach i wiedźm w chatkach. Wydanie `BlockCraft_Gra`
nie zostało odświeżone o częściową implementację pogody. Pełna zgodność 1:1
nie została potwierdzona. Dalsze braki ME i końcowa integracja pozostają otwarte.

### OW: opady i interakcje powierzchniowe, dalsza kontynuacja 2026-10-31

Dodano ruchome kolumny deszczu/śniegu przy kamerze, z odcięciem na powierzchni
terenu i dachów, wyborem opadów według temperatury biomu i brakiem opadów na
pustyni, w Netherze i Endzie. Pogoda odbarwia niebo i mgłę, przygasza słońce,
księżyc i gwiazdy. Tekstury opadów są proceduralne 16×16, zgodnie z aktualnym
atlasem BlockCraft; nie są oryginalnymi teksturami MC. Pełna zgodność wizualna
1:1 nadal wymaga dopracowania.

Deszcz odtwarza asset `ambient.weather.rain` przez jeden zapętlony głos
w kategorii `weather`. Aktualizacja głośności nie dodaje kopii próbki; limit
wejściowy wynosi 0.25, typowy cel 0.2 przed dotychczasowym zapasem 6 dB.
Głośność łagodnie maleje pod dachem i przy końcu deszczu; głęboka jaskinia,
śnieg i suchy biom są ciche. Wyjście ze świata czyści ambient. Dekodowanie
próbki odbywa się poza blokadą miksera. Nie wykonano odsłuchu głośników.

Dodano próby powierzchniowe w aktywnych chunkach: zamarzanie źródeł wody od
brzegu, pojedyncza warstwa śniegu podczas opadów (bez piętrzenia), bez śniegu
na lodzie. Zamarzanie nie wymaga opadów. Deszcz nawadnia pole uprawne;
dach odcina to nawadnianie.

Release: 0 błędów, 4 istniejące ostrzeżenia. Selftest: **529 OK, 0 FAIL**,
w tym test 1000 aktualizacji pojedynczego ambientu, suwaka kategorii, końca
opadów i zmiany świata oraz 9 testów śniegu/lodu/pól. Autotest `--ow`:
równiny (316 kolumn deszczu, 1 głos), śnieżny biom (316 kolumn śniegu,
0 głosów), pustynia i clear (0 kolumn, 0 głosów); `OW-TEST KONIEC: OK`.
Scena pogody jest kontrolowanym fixture, nie dowodem naturalnej generacji
biomów. Zrzuty deszczu/śniegu obejrzano i zmniejszono zbyt dużą gęstość
pierwszej wersji. Dowody: `.modus/ow-precipitation-*.log`
i `.modus/ow-precipitation-final/`.

Pozostały pioruny (encja, efekty, dźwięk i sieć), naturalny spawn guardianów
i wiedźm w strukturach, dokładniejsze odwzorowanie grafiki pogody i test
pogody w dwóch oknach przez relay. Krok OW pozostaje w toku; wydanie
BlockCraft_Gra jeszcze nie zostało odświeżone.

### OW: pioruny, kontynuacja 2026-10-31

Dodano encję `lightning_bolt` i alias `/summon LightningBolt`, naturalne
uderzenia w aktywnych chunkach podczas burzy, kolejne błyski, ogień oraz
obszar rażenia. Działają dotychczasowe reakcje MO: naładowanie creepera,
świnia → zombie pigman i wieśniak → wiedźma. Steruje nimi wyłącznie host.
Stan, liczba pozostałych błysków i ziarno geometrii przechodzą przez zapis
encji i istniejący protokół. Klient nie losuje błysków ani nie nalicza obrażeń.
Piorun jest chwilową encją, nie trafia do trwałego zapisu chunków.

Cykl, obszar rażenia i cztery otoczki geometrii z dwiema gałęziami oparto na
[EntityLightningBolt 1.8.9](https://github.com/Marcelektro/MavenMCP-1.8.9/blob/master/src/main/java/net/minecraft/entity/effect/EntityLightningBolt.java)
i [RenderLightningBolt 1.8.9](https://github.com/Marcelektro/MavenMCP-1.8.9/blob/master/src/main/java/net/minecraft/client/renderer/entity/RenderLightningBolt.java).
Zachowano Java Random dla geometrii. Sky/fog reagują na aktywny błysk.
Świadoma różnica audio ze względu na priorytet użytkownika: grzmot ma zasięg
64 bloków zamiast wartości głośności 10000 z referencji. Grzmot i cichy trzask
grają raz na piorun, bez ponawiania przy kolejnych błyskach; obowiązuje
dotychczasowy limiter miksera. Odsłuch głośników nadal niewykonany.

Naprawiono brak `doFireTick` w parserze reguł, liście reguł, zapisie sesji
i komunikatach ustawień hosta. Wyłączenie reguły blokuje rozpalanie bloków;
nie wyłącza obrażeń ani podpalenia encji od uderzenia.

Release: 0 błędów, 4 istniejące ostrzeżenia. Selftest **543 OK, 0 FAIL**:
11 nowych testów logiki oraz 3 sieciowe. `--autotest --ow` zakończony OK;
obejrzano `ow_lightning.png` z widocznym pniem i gałęziami. Dowody w
`.modus/ow-lightning-selftest.log`, `.modus/ow-lightning-autotest.log`
i `.modus/ow-lightning-final/`. Jeden test początkowo zawiódł, bo creeper
w scenie bez gracza znikał przed uderzeniem; nadano mu trwałość w fixture,
bez zmiany zasad despawnu. Początkowy błąd kompilacji testu wynikał z użycia
CustomName przez typ LivingEntity zamiast Mob i został poprawiony.

OW pozostaje otwarty: naturalny spawn guardianów w monumentach i wiedźm
w chatkach, dokładniejsze tekstury opadów i test pogody w dwóch graficznych
oknach przez relay. Wydanie BlockCraft_Gra nie jest jeszcze odświeżone.

### OW: spawn w strukturach, kontynuacja 2026-10-31

Podłączono `ChunkProviderGenerate.getPossibleCreatures` do naturalnego spawnera:
wewnątrz monumentu lista potworów zawiera zwykłego guardiana (waga 1, grupa
2–4), a w chatce wyłącznie wiedźmę (waga 1, grupa 1–1). Kategorie zwierząt,
wodnych i nietoperzy zachowują listy biomów. Guardian należy do limitu
potworów, mimo że jego umieszczenie wymaga wody, a nie podłoża lądowego.
Istniejące zasady światła, kolizji, odległości od graczy i limitów pozostają.

Do planów dodano odrębne obszary spawnu. Monument używa oryginalnego pudła
58×23×58 (Y 39–61); rozszerzony obszar fundamentów i karczowania nie zwiększa
zasięgu spawnu. Chatka pomija podpory poniżej bazowego Y. Lista jest ponownie
sprawdzana przy każdym przesunięciu grupy, z tożsamością SpawnListEntry
(wiedźma ze struktury nie przenosi wagi 1 do zwykłej listy biomu). Superflat
i inne wymiary nie dostają tych reguł. Obszary są cache'owane osobno;
próba spawnu nie wyszukuje kopalń, twierdz ani wiosek.

Release: 0 błędów, 4 istniejące ostrzeżenia. Selftest **556 OK, 0 FAIL**,
w tym 13 nowych sprawdzeń granic, list, kategorii i pełnego przebiegu
`World.Tick → MobSpawner → utworzenie moba`. Te ostatnie używają kontrolowanych
komór w rzeczywistych obszarach struktur seeda 12345, bez ręcznego spawnowania
guardianów/wiedźm. Pierwsza komora wiedźm miała otwarte boki i zbyt dużo
światła w południe (0 mobów); dodano ściany do fixture, nie zmieniano reguł
oświetlenia. Log: `.modus/ow-structure-spawns-selftest.log`.

Nie wykonano pomiaru zgodności tempa spawnu 1:1. Dotychczasowy spawner
wciąż ogranicza próby potworów do 1/4 ze względów wydajnościowych.
Pozostałe zadania OW: dokładniejsze tekstury opadów i test pogody w dwóch
graficznych oknach przez relay. Końcowe wydanie i integracja pozostają otwarte.

### OW: cykl i limity naturalnego spawnu, kontynuacja 2026-10-31

Porównano MobSpawner z lokalną referencją
[SpawnerAnimals 1.8.9](https://github.com/Marcelektro/MavenMCP-1.8.9/blob/master/src/main/java/net/minecraft/world/SpawnerAnimals.java)
i `World.countEntities/isAnyPlayerWithinRangeAt`. Usunięto wcześniejsze
pomijanie 3/4 prób potworów. Dla pojedynczego gracza licznik limitu obejmuje
17×17 chunków (289), natomiast próby odbywają się w wewnętrznych 15×15 (225).
Zachowano nietypową regułę 1.8: pierścień nie trafia do zbioru prób, więc dla
dwóch graczy w tym samym miejscu licznik rośnie do 353, choć obszar prób
pozostaje 225. Limit przestaje zależeć od liczby wczytanych chunków.

Zgodnie z referencją próby są dozwolone przy liczbie mobów równej limitowi
(`<=`), zatem cała grupa może go przekroczyć. Nazwane i oznaczone jako trwałe
moby nie zajmują limitu; zwykłe zwierzęta nadal są liczone. Widzowie nie
tworzą regionów i nie blokują prób w odległości 24 bloków. Odległość od spawnu
świata uwzględnia Y. Nowy mob trafia od razu do świata, więc następny z grupy
widzi jego kolizję. Pusta lista kończy próbę grupy, zamiast kontynuować losowanie.

Release: 0 błędów, 4 istniejące ostrzeżenia. Selftest **569 OK, 0 FAIL**:
11 nowych sprawdzeń regionów/liczenia/odległości oraz 2 testy pełnego spawnera
przy dokładnie 70 potworach i po przekroczeniu limitu. Nadal przechodzą testy
rzeczywistego naturalnego spawnu guardianów i wiedźm. Pełny `--autotest`
zakończony `AUTOTEST DONE`; obejrzano ekran debug (54 FPS, 131 encji,
97 wczytanych chunków w pojedynczym ujęciu — to nie jest benchmark wydajności).
Log uruchomienia nadal zawiera 4 wcześniejsze braki tekstur przedmiotów:
baked_potato, poisonous_potato, pumpkin_seeds i melon_seeds. Nie wynikają
ze zmiany spawnera; pozostają do integracji itemów/ME.

Dowody: `.modus/ow-spawn-cycle-selftest.log`, `.modus/ow-spawn-cycle-autotest.log`
i `.modus/ow-spawn-cycle-final/`. Ograniczenia: próby nie wymuszają generacji
niewczytanych chunków, RNG gry i kolejność HashSet nie są zgodne bitowo z JVM,
nie porównano wszystkich warunków początkowego wyposażenia/grup. Nie deklaruje
się całej symulacji mobów jako 1:1. Nadal otwarte są dokładniejsze tekstury opadów,
test pogody w dwóch oknach przez relay, ME i końcowa integracja/wydanie.
