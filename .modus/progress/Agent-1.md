<!-- Generowany przez Modus Operandi z kontrolera prac. Nie edytuj: użyj modus_plan i modus_step. -->
# BlockCraft: zawartość MC 1.8 (Java) 1:1 + dźwięki + wymiary + multiplayer

Stan: w toku · gałąź feature/mc18

Prośba Filipa: doimplementować jak najwięcej z MC 1.8 Java 1:1 (bloki, itemy, mechaniki, redstone z unikalnymi mechanizmami, wszystkie moby, wszystkie biomy, działające portale Nether/End), dźwięki + muzyka MC (pobierane z serwera assetów Mojang, nie commitowane), multiplayer bez serwera przez Tailcat.Link (derp.fimson.pl) z prostym dołączaniem kodem. Poprawki: napis .NET/C# EDITION, odwrócone ikony. Gałąź feature/mc18 (master). Fundament (ID wszystkich bloków/itemów z właścicielami modułów, partial hooki, NBT, efekty, enchanty API, wysokość 256) robi master; moduły robią pionki w osobnych worktree, master scala. Testy: build + --autotest + zrzuty ekranu.

## Kroki

- [x] 1. Poprawki: odwrócone ikony bloków, napis .NET/C# EDITION, .NET 10: Ikony z FBO rysowane z odwróconym v, napis .NET/C# EDITION, net10.0 (Tailcat wymaga). Zweryfikowane zrzutem. Commit na feature/mc18.
- [ ] 2. Fundament: wysokość świata 256, ID wszystkich bloków/itemów 1.8 z przypisaniem modułów, partial hooki rejestracji (bloki, tekstury, itemy, receptury, encje, renderery), NBT w ItemStack, API efektów i enchantów, warianty meta w kreatywnym/ikonach (w toku)
- [ ] 3. Moduł A (pionek): dźwięki i muzyka MC 1.8 (assety Mojang, NVorbis, mikser, 3D) - CZEKA na pionka Mizu
- [ ] 4. Moduł B (pionek): biomy 1.8 + bloki naturalne/drewna/budulce + pogoda + struktury overworldu (wioski, świątynie, kopalnie, lochy, monumenty, twierdze)
- [ ] 5. Moduł C (pionek): redstone 1.8 kompletny (komparator, lej, podajnik, dozownik, czujnik światła, płyty, tripwire, blok nut, lepki blok) + tory i wagoniki, mechanika 1:1 (QC, BUD, 0-tick, limit 12)
- [ ] 6. Moduł D (pionek): Nether i End - wymiary, portale, bloki, generatory, forteca, smok, moby Netheru/Endu, Wither
- [ ] 7. Moduł E (pionek): wszystkie brakujące moby overworldu 1.8 + jajka spawnu, smycze, siodła, konie, handel z wieśniakami
- [ ] 8. Moduł F (pionek): mechaniki itemów - enchanting, kowadło, warzenie/mikstury/efekty, wędka, mapy, kompas, zegar, tabliczki, ramki, obrazy, stojaki, sztandary, fajerwerki, łódki, płyty, osiągnięcia, zakładki kreatywne
- [ ] 9. Scalenie fali 1 + build + autotest + zrzuty
- [ ] 10. Moduł G: multiplayer Tailcat.Link (host w grze, dołączanie kodem, synchronizacja chunków/encji/wymiarów)
- [ ] 11. Scalenie, testy końcowe, README/CHECKLISTA, merge po akceptacji Filipa
