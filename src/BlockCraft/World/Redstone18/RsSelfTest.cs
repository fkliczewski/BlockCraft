using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy modułu RS (redstone 1.8).</summary>
public static partial class SelfTest
{
    private static void TestRedstone18()
    {
        Console.WriteLine("[Redstone 1.8]");
        var w = MakeWorld(1, WorldType.Superflat, 7);
        var p = SpawnPlayer(w, 0, 4, -10);

        // zegar z pochodni: pochodnia na kamieniu, pył wraca i wskazuje w kamień -> przepalenie
        w.SetBlock(0, 4, 40, B.Stone);
        w.SetBlock(0, 4, 41, B.RedstoneTorch, 3);
        foreach (var (x, z) in new[] { (0, 42), (1, 42), (2, 42), (2, 41), (2, 40), (1, 40) }) w.SetBlock(x, 4, z, B.RedstoneWire, 0);
        int toggles = 0; byte last = w.GetBlock(0, 4, 41);
        for (int i = 0; i < 60; i++) { w.Tick(); byte now = w.GetBlock(0, 4, 41); if (now != last) toggles++; last = now; }
        Check(toggles >= 8, "Zegar z pochodni przełącza się", toggles.ToString());
        bool burnt = true;
        for (int i = 0; i < 60; i++) { w.Tick(); if (w.GetBlock(0, 4, 41) != B.RedstoneTorchOff) burnt = false; }
        Check(burnt, "Pochodnia się przepala (8 przełączeń w 60 tickach)");

        // quasi-łączność: zasilenie miejsca nad tłokiem nie budzi tłoka, dopiero aktualizacja (BUD)
        w.SetBlock(0, 4, 50, B.Piston, Dir.East);
        w.SetBlock(0, 6, 50, B.RedstoneBlock);
        Ticks(w, 4);
        Check((w.GetMeta(0, 4, 50) & 8) == 0, "QC: tłok bez aktualizacji stoi (BUD)");
        w.SetBlock(0, 4, 49, B.Stone);
        Ticks(w, 4);
        Check((w.GetMeta(0, 4, 50) & 8) != 0 && w.GetBlock(1, 4, 50) == B.PistonHead, "QC: po aktualizacji tłok się wysuwa", w.GetMeta(0, 4, 50).ToString());

        // limit 12 bloków
        w.SetBlock(0, 4, 60, B.Piston, Dir.East);
        for (int x = 1; x <= 12; x++) w.SetBlock(x, 4, 60, B.Cobblestone);
        w.SetBlock(-1, 4, 60, B.RedstoneBlock);
        Ticks(w, 4);
        Check(w.GetBlock(13, 4, 60) == B.Cobblestone && w.GetBlock(1, 4, 60) == B.PistonHead, "Tłok pcha 12 bloków");
        w.SetBlock(0, 4, 62, B.Piston, Dir.East);
        for (int x = 1; x <= 13; x++) w.SetBlock(x, 4, 62, B.Cobblestone);
        w.SetBlock(-1, 4, 62, B.RedstoneBlock);
        Ticks(w, 4);
        Check(w.GetBlock(14, 4, 62) == 0 && (w.GetMeta(0, 4, 62) & 8) == 0, "Tłok nie pcha 13 bloków");
        // obsydian nieprzesuwalny
        w.SetBlock(0, 4, 64, B.Piston, Dir.East);
        w.SetBlock(1, 4, 64, B.Obsidian);
        w.SetBlock(-1, 4, 64, B.RedstoneBlock);
        Ticks(w, 4);
        Check(w.GetBlock(1, 4, 64) == B.Obsidian && (w.GetMeta(0, 4, 64) & 8) == 0, "Obsydian nieprzesuwalny");

        // krótki puls (1 tick): lepki tłok zostawia blok
        w.SetBlock(0, 4, 70, B.StickyPiston, Dir.East);
        w.SetBlock(1, 4, 70, B.Stone);
        w.SetBlock(-1, 4, 70, B.RedstoneBlock);
        w.Tick();
        w.SetBlock(-1, 4, 70, B.Air);
        Ticks(w, 6);
        Check(w.GetBlock(2, 4, 70) == B.Stone && w.GetBlock(1, 4, 70) == 0 && w.GetBlock(0, 4, 70) == B.StickyPiston, "Lepki tłok upuszcza blok przy krótkim pulsie",
            $"{w.GetBlock(0, 4, 70)} {w.GetBlock(1, 4, 70)} {w.GetBlock(2, 4, 70)}");

        // blok szlamu ciągnie sąsiada (w powietrzu - w MC szlam przykleja też podłoże)
        w.SetBlock(0, 8, 80, B.Piston, Dir.East);
        w.SetBlock(1, 8, 80, B.SlimeBlock);
        w.SetBlock(1, 9, 80, B.Stone);
        w.SetBlock(-1, 8, 80, B.RedstoneBlock);
        Ticks(w, 4);
        Check(w.GetBlock(2, 8, 80) == B.SlimeBlock && w.GetBlock(2, 9, 80) == B.Stone && w.GetBlock(1, 9, 80) == 0, "Szlam przesuwa przyklejony blok", $"{w.GetBlock(1, 8, 80)} {w.GetBlock(2, 8, 80)} {w.GetBlock(1, 9, 80)} {w.GetBlock(2, 9, 80)} m={w.GetMeta(0, 8, 80)}");

        // przycisk kamienny: 20 ticków
        w.SetBlock(0, 4, 90, B.Stone);
        w.SetBlock(1, 4, 90, B.StoneButton, Dir.West);
        w.SetBlock(-1, 4, 90, B.RedstoneLamp);
        Block.Get(B.StoneButton).OnActivated(w, 1, 4, 90, p);
        Ticks(w, 2);
        Check(w.GetBlock(-1, 4, 90) == B.RedstoneLampOn, "Przycisk zasila blok i lampę za nim");
        Ticks(w, 25);
        Check((w.GetMeta(1, 4, 90) & 8) == 0 && w.GetBlock(-1, 4, 90) == B.RedstoneLamp, "Przycisk puszcza po 20 tickach");

        // blokada przekaźnika
        w.SetBlock(0, 4, 100, B.Lever, Dir.Down);
        w.SetBlock(1, 4, 100, B.Repeater, 3);
        w.SetBlock(2, 4, 100, B.RedstoneLamp);
        w.SetBlock(1, 4, 99, B.RepeaterOn, 0); // wyjście +Z w bok przekaźnika
        w.SetBlock(1, 4, 98, B.RedstoneBlock);
        Ticks(w, 4);
        Block.Get(B.Lever).OnActivated(w, 0, 4, 100, p);
        Ticks(w, 6);
        Check(w.GetBlock(2, 4, 100) == B.RedstoneLamp, "Zablokowany przekaźnik nie przekazuje", w.GetBlock(2, 4, 100).ToString());
    }
}
