using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
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

        TestRedstone18Components(w, p);
    }

    private static void TestRedstone18Components(World.World w, Player p)
    {
        // komparator czyta skrzynię (calcRedstoneFromInventory) i reaguje na zmianę zawartości
        w.SetBlock(20, 4, 40, B.Chest);
        w.SetBlock(21, 4, 40, B.Comparator, Dir.FaceToHoriz(Dir.East));
        w.SetBlock(22, 4, 40, B.RedstoneWire);
        var chest = (ChestTileEntity)w.GetTileEntity(20, 4, 40);
        chest.Set(0, new ItemStack(B.Cobblestone, 64));
        Ticks(w, 4);
        Check(w.GetMeta(22, 4, 40) == 1, "Komparator: jeden stos w skrzyni = 1", w.GetMeta(22, 4, 40).ToString());
        for (int i = 1; i < 14; i++) chest.Set(i, new ItemStack(B.Cobblestone, 64));
        Ticks(w, 4);
        Check(w.GetMeta(22, 4, 40) == 8, "Komparator: 14/27 stosów = 8", w.GetMeta(22, 4, 40).ToString());
        for (int i = 0; i < 27; i++) chest.Set(i, null);
        Ticks(w, 4);
        Check(w.GetMeta(22, 4, 40) == 0 && (w.GetMeta(21, 4, 40) & 8) == 0, "Komparator: pusta skrzynia = 0", w.GetMeta(22, 4, 40).ToString());

        // odejmowanie: wejście 15, bok 12 -> 3; porównanie: 15 >= 12 -> 15
        w.SetBlock(30, 4, 50, B.RedstoneBlock);
        w.SetBlock(31, 4, 50, B.Comparator, Dir.FaceToHoriz(Dir.East) | 4);
        w.SetBlock(32, 4, 50, B.RedstoneWire);
        for (int z = 51; z <= 54; z++) w.SetBlock(31, 4, z, B.RedstoneWire);
        w.SetBlock(31, 4, 55, B.RedstoneBlock);
        Ticks(w, 6);
        Check(w.GetMeta(31, 4, 51) == 12 && w.GetMeta(32, 4, 50) == 3, "Komparator odejmuje (15 - 12 = 3)", $"bok={w.GetMeta(31, 4, 51)} wyj={w.GetMeta(32, 4, 50)}");
        Block.Get(B.Comparator).OnActivated(w, 31, 4, 50, p);
        Ticks(w, 4);
        Check(w.GetMeta(32, 4, 50) == 15, "Komparator porównuje (15 >= 12 -> 15)", w.GetMeta(32, 4, 50).ToString());

        // płyty naciskowe: drewniana od przedmiotu, kamienna nie; ważona lekka liczy encje
        w.SetBlock(40, 4, 40, B.WoodPressurePlate);
        w.SetBlock(42, 4, 40, B.StonePressurePlate);
        w.SetBlock(44, 4, 40, B.LightWeightedPlate);
        w.DropItem(40.5, 4.1, 40.5, new ItemStack(B.Dirt, 1), false);
        w.DropItem(42.5, 4.1, 42.5 - 2, new ItemStack(B.Dirt, 1), false);
        for (int i = 0; i < 3; i++) w.DropItem(44.5, 4.1, 40.5, new ItemStack(B.Stone + i, 1), false);
        Ticks(w, 10);
        Check(w.GetMeta(40, 4, 40) == 1, "Drewniana płyta wciśnięta przez przedmiot");
        Check(w.GetMeta(42, 4, 40) == 0, "Kamienna płyta ignoruje przedmiot");
        Check(w.GetMeta(44, 4, 40) == 3, "Płyta ważona: 3 przedmioty = 3", w.GetMeta(44, 4, 40).ToString());
        foreach (var e in w.GetEntitiesInBox(new AABB(39, 3, 39, 46, 6, 42))) if (e is ItemEntity) e.Kill();
        Ticks(w, 25);
        Check(w.GetMeta(40, 4, 40) == 0 && w.GetMeta(44, 4, 40) == 0, "Płyty puszczają po zejściu przedmiotów");

        // hak + linka: dwa haki, trzy odcinki linki, przedmiot na lince zasila haki i blok za hakiem
        w.SetBlock(49, 4, 60, B.Stone); w.SetBlock(55, 4, 60, B.Stone); w.SetBlock(48, 4, 60, B.RedstoneLamp);
        w.SetBlock(50, 4, 60, B.TripwireHook, Dir.FaceToHoriz(Dir.East)); Block.Get(B.TripwireHook).OnPlaced(w, 50, 4, 60, p);
        for (int x = 51; x <= 53; x++) w.SetBlock(x, 4, 60, B.Tripwire);
        w.SetBlock(54, 4, 60, B.TripwireHook, Dir.FaceToHoriz(Dir.West)); Block.Get(B.TripwireHook).OnPlaced(w, 54, 4, 60, p);
        Ticks(w, 2);
        Check((w.GetMeta(50, 4, 60) & 4) != 0 && (w.GetMeta(54, 4, 60) & 4) != 0 && (w.GetMeta(52, 4, 60) & 4) != 0, "Haki podłączone linką",
            $"{w.GetMeta(50, 4, 60)} {w.GetMeta(52, 4, 60)} {w.GetMeta(54, 4, 60)}");
        w.DropItem(52.5, 4.05, 60.5, new ItemStack(B.Dirt, 1), false);
        Ticks(w, 6);
        Check((w.GetMeta(50, 4, 60) & 8) != 0 && w.GetBlock(48, 4, 60) == B.RedstoneLampOn, "Przedmiot na lince zasila hak i blok za nim", $"{w.GetMeta(50, 4, 60)} {w.GetMeta(52, 4, 60)}");
        foreach (var e in w.GetEntitiesInBox(new AABB(51, 3, 59, 54, 6, 62))) if (e is ItemEntity) e.Kill();
        Ticks(w, 20);
        Check((w.GetMeta(50, 4, 60) & 8) == 0, "Hak gaśnie po zejściu z linki");
        // nożyce rozbrajają: haki odłączają się bez impulsu
        p.Inventory.Main[p.Inventory.SelectedSlot] = new ItemStack(I.Shears, 1);
        Block.Get(B.Tripwire).OnDestroyedByPlayer(w, 52, 4, 60, w.GetMeta(52, 4, 60), p);
        w.BreakBlock(52, 4, 60, true);
        bool pulse = (w.GetMeta(50, 4, 60) & 8) != 0;
        Ticks(w, 12);
        Check(!pulse && (w.GetMeta(50, 4, 60) & 12) == 0 && (w.GetMeta(54, 4, 60) & 4) == 0, "Linka przecięta nożycami odłącza haki bez sygnału", $"{w.GetMeta(50, 4, 60)}");
        // zerwanie linki bez nożyc daje impuls (jak w MC 1.8)
        p.Inventory.Main[p.Inventory.SelectedSlot] = null;
        w.SetBlock(51, 4, 62, B.Stone); w.SetBlock(57, 4, 62, B.Stone);
        w.SetBlock(52, 4, 62, B.TripwireHook, Dir.FaceToHoriz(Dir.East)); Block.Get(B.TripwireHook).OnPlaced(w, 52, 4, 62, p);
        for (int x = 53; x <= 55; x++) w.SetBlock(x, 4, 62, B.Tripwire);
        w.SetBlock(56, 4, 62, B.TripwireHook, Dir.FaceToHoriz(Dir.West)); Block.Get(B.TripwireHook).OnPlaced(w, 56, 4, 62, p);
        Ticks(w, 2);
        w.BreakBlock(54, 4, 62, true);
        pulse = (w.GetMeta(52, 4, 62) & 8) != 0;
        Ticks(w, 12);
        Check(pulse && (w.GetMeta(52, 4, 62) & 12) == 0, "Zerwana linka daje impuls i odłącza haki", $"{pulse} {w.GetMeta(52, 4, 62)}");

        // czujnik światła: południe -> moc, noc -> 0, odwrócony odwrotnie
        w.SetBlock(60, 4, 40, B.DaylightSensor);
        w.DayTime = 6000; Ticks(w, 21);
        int noon = w.GetMeta(60, 4, 40);
        w.DayTime = 18000; Ticks(w, 21);
        int night = w.GetMeta(60, 4, 40);
        Check(noon >= 14 && night == 0, "Czujnik światła: dzień/noc", $"{noon}/{night}");
        Block.Get(B.DaylightSensor).OnActivated(w, 60, 4, 40, p);
        Check(w.GetBlock(60, 4, 40) == B.DaylightSensorInverted && w.GetMeta(60, 4, 40) == 15, "Odwrócony czujnik w nocy = 15", w.GetMeta(60, 4, 40).ToString());
        w.DayTime = 6000;

        // klapy i drzwi żelazne: ręka nie otwiera, sygnał tak
        w.SetBlock(70, 5, 40, B.Stone);
        w.SetBlock(71, 5, 40, B.IronTrapdoor, 3);
        w.SetBlock(70, 5, 42, B.Stone);
        w.SetBlock(71, 5, 42, B.Trapdoor, 3);
        Block.Get(B.IronTrapdoor).OnActivated(w, 71, 5, 40, p);
        Block.Get(B.Trapdoor).OnActivated(w, 71, 5, 42, p);
        Check((w.GetMeta(71, 5, 40) & 4) == 0 && (w.GetMeta(71, 5, 42) & 4) != 0, "Żelazna klapa nie otwiera się ręką, drewniana tak");
        w.SetBlock(72, 5, 40, B.RedstoneBlock);
        Check((w.GetMeta(71, 5, 40) & 4) != 0, "Żelazna klapa otwiera się sygnałem");
        w.SetBlock(80, 4, 40, B.IronDoor, 0, false);
        w.SetBlock(80, 5, 40, B.IronDoor, 8, true);
        Block.Get(B.IronDoor).OnActivated(w, 80, 4, 40, p);
        Check((w.GetMeta(80, 4, 40) & 4) == 0, "Żelazne drzwi nie otwierają się ręką");
        w.SetBlock(81, 4, 40, B.Lever, Dir.Down);
        Block.Get(B.Lever).OnActivated(w, 81, 4, 40, p);
        Check((w.GetMeta(80, 4, 40) & 4) != 0, "Żelazne drzwi otwiera dźwignia");

        // świecąca ruda
        w.SetBlock(90, 4, 40, B.RedstoneOre);
        Block.Get(B.RedstoneOre).OnClicked(w, 90, 4, 40, p);
        Check(w.GetBlock(90, 4, 40) == B.RedstoneOreLit, "Ruda czerwonego kamienia świeci po dotknięciu");
    }
}
