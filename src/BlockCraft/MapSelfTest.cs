using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Mapy 1.8 (moduł ME-h 39a).</summary>
public static partial class SelfTest
{
    private static void TestMaps()
    {
        Check(MapLogic.Center(0, 0, 0) == (0, 0) && MapLogic.Center(100, -100, 0) == (128, -128) && MapLogic.Center(-65, 0, 0) == (-128, 0),
            "Mapa: środek w siatce 128 przesuniętej o 64 (calculateMapCenter)", MapLogic.Center(100, -100, 0).ToString());
        Check(MapLogic.Center(300, 300, 2) == (192, 192), "Mapa: środek dla skali 2 (siatka 512)", MapLogic.Center(300, 300, 2).ToString());
        Check(MapColors.Rgba(1 * 4 + 2) == Rgb.Pack(0x7F, 0xB2, 0x38) && MapColors.Rgba(1 * 4 + 0) == Rgb.Pack(0x7F * 180 / 255, 0xB2 * 180 / 255, 0x38 * 180 / 255),
            "Mapa: kolor trawy i jasność 180/255");
        Check(MapColors.Of(B.Grass, 0) == MapColors.Grass && MapColors.Of(B.Water, 0) == MapColors.Water && MapColors.Of(B.Stone, 0) == MapColors.Stone
            && MapColors.Of(B.Wool, 14) == MapColors.Red && MapColors.Of(B.Glass, 0) == MapColors.Air && MapColors.Of(B.Torch, 0) == MapColors.Air
            && MapColors.Of(B.OakLeaves, 0) == MapColors.Foliage && MapColors.Of(B.Sand, 0) == MapColors.Sand && MapColors.Of(B.Lava, 0) == MapColors.Tnt
            && MapColors.Of(B.OakPlanks, 0) == MapColors.Wood && MapColors.Of(B.BirchPlanks, 0) == MapColors.Sand && MapColors.Of(B.Snow, 0) == MapColors.Snow,
            "Mapa: kolory bloków (trawa, woda, kamień, wełna, szkło i pochodnia przezroczyste, liście, piasek, lawa, deski)");

        var w = MakeWorld(19, WorldType.Superflat, 5);
        w.Rules.DoMobSpawning = false;
        var p = SpawnPlayer(w, 10.5, 4, 10.5);
        var grid = new ItemStack[9];
        for (int i = 0; i < 9; i++) grid[i] = new ItemStack(I.Paper);
        grid[4] = new ItemStack(I.Compass);
        Check(Recipes.Find(grid, 3, 3)?.Id == I.EmptyMap, "Mapa: receptura pustej mapy (papier + kompas)");

        p.Inventory.SelectedSlot = 0; p.Inventory.Main[0] = new ItemStack(I.EmptyMap, 2);
        Item.ById[I.EmptyMap].UseInAir(p, p.Inventory.Main[0]);
        var store = MapStore.Of(w);
        var filled = p.Inventory.Main.FirstOrDefault(s => s?.Id == I.FilledMap);
        var m = filled == null ? null : store.Get(filled.Damage);
        Check(p.Inventory.Main[0]?.Count == 1 && m != null && m.Scale == 0 && m.XCenter == 0 && m.ZCenter == 0,
            "Mapa: PPM pustą mapą tworzy mapę skali 0 wyśrodkowaną w siatce", m == null ? "brak" : $"{m.XCenter},{m.ZCenter}");
        if (m == null) return;
        // woda pod graczem: 5x5 na wysokości trawy
        for (int x = 20; x < 25; x++) for (int z = 20; z < 25; z++) w.SetBlock(x, 3, z, B.Water);
        p.Inventory.Main[0] = filled; p.Inventory.Main[1] = null;
        for (int i = 0; i < 40; i++) MapLogic.TickPlayer(p);
        int px = (10 - m.XCenter) + 64, pz = (10 - m.ZCenter) + 64;
        Check(m.Colors[px + pz * 128] >> 2 == MapColors.Grass, "Mapa: trzymana mapa rysuje trawę pod graczem", (m.Colors[px + pz * 128] >> 2).ToString());
        Check(m.Colors[(22 + 64) + (22 + 64) * 128] >> 2 == MapColors.Water, "Mapa: woda ma kolor wody", (m.Colors[86 + 86 * 128] >> 2).ToString());
        Check(m.Colors[0] == 0 || m.Colors[0] >> 2 == MapColors.Grass, "Mapa: piksele poza zasięgiem zostają puste lub z wczytanych chunków");
        int drawn = m.Colors.Count(c => c != 0);
        Check(drawn > 1000 && m.Version > 0 && m.Dirty, "Mapa: po 40 tickach narysowany okrąg wokół gracza", drawn.ToString());
        Check(m.Decorations.TryGetValue(p.Name, out var dec) && dec.Type == 0 && dec.X == 21 && dec.Z == 21,
            "Mapa: znacznik gracza (typ 0) w pół-pikselach", m.Decorations.TryGetValue(p.Name, out var d0) ? $"{d0.Type} {d0.X},{d0.Z}" : "brak");
        p.SetPositionAndPrev(200, 4, 10);
        MapLogic.TickPlayer(p);
        Check(m.Decorations.TryGetValue(p.Name, out var far) && far.Type == 6 && far.X == 127, "Mapa: gracz daleko poza mapą - kropka na krawędzi (typ 6), dalej niż 320 znika");
        p.SetPositionAndPrev(1000, 4, 10); MapLogic.TickPlayer(p);
        bool gone = !m.Decorations.ContainsKey(p.Name);
        p.SetPositionAndPrev(10.5, 4, 10.5);
        Check(gone, "Mapa: znacznik gracza dalej niż 320 pikseli znika");

        // powiększanie i kopiowanie
        for (int i = 0; i < 9; i++) grid[i] = new ItemStack(I.Paper);
        grid[4] = filled.CopyWith(1);
        var bigger = Recipes.Find(grid, 3, 3);
        Check(bigger?.Id == I.FilledMap && bigger.Tag != null && bigger.Tag.GetBool("map_is_scaling"), "Mapa: mapa + 8 papieru = powiększanie");
        if (bigger != null)
        {
            p.OnCrafted(bigger);
            var m2 = store.Get(bigger.Damage);
            Check(m2 != null && m2.Id != m.Id && m2.Scale == 1 && m2.XCenter == m.XCenter && bigger.Tag == null, "Mapa: powiększona ma nowy numer, skalę 1 i ten sam środek");
        }
        var clone = new ItemStack[9]; clone[0] = filled.CopyWith(1); clone[3] = new ItemStack(I.EmptyMap); clone[8] = new ItemStack(I.EmptyMap);
        var copies = Recipes.Find(clone, 3, 3);
        Check(copies?.Id == I.FilledMap && copies.Count == 3 && copies.Damage == m.Id, "Mapa: mapa + 2 puste = 3 kopie tej samej mapy");

        // ramka z mapą: znacznik typ 1
        w.SetBlock(12, 4, 12, B.Stone);
        var f = ItemFrameEntity.Create(w, 12, 4, 11, 2); f.Item = filled.CopyWith(1); w.AddEntityNow(f);
        for (int i = 0; i < 12; i++) f.Tick();
        Check(m.Decorations.Values.Any(d => d.Type == 1), "Mapa: ramka z mapą dodaje zielony znacznik");
        f.Attack(DamageSource.PlayerAttack(p), 1);
        Check(!m.Decorations.Keys.Any(k => k.StartsWith("frame-")), "Mapa: zdjęcie z ramki usuwa znacznik");

        // zapis i odczyt
        string dir = Path.Combine(Path.GetTempPath(), "bc-maps-" + Guid.NewGuid().ToString("N"));
        try
        {
            var disk = new MapStore(dir);
            var dm = disk.Create(2, 300, 300, 0);
            Array.Copy(m.Colors, dm.Colors, 16384);
            disk.SaveAll();
            var again = new MapStore(dir);
            var lm = again.Get(dm.Id);
            Check(lm != null && lm.Scale == 2 && lm.XCenter == 192 && lm.Colors.SequenceEqual(m.Colors) && again.NextId() == dm.Id + 1,
                "Mapa: zapis data/map_N.dat i licznik numerów");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }

        // sieć: pełna mapa, potem tylko zmieniony prostokąt
        var old = MapStore.Client; MapStore.Client = new MapStore(null);
        try
        {
            var sent = new Dictionary<int, (int, int)>();
            var buf = new PacketBuffer();
            MapSync.Send(buf, w, sent, new[] { m.Id });
            void Deliver() { var frame = buf.Take(); if (frame == null) return; foreach (var (type, r) in PacketReader.Read(frame)) if (type == (byte)S2C.MapData) MapSync.Read(r); }
            int fullLen = (int)buf.Length; Deliver();
            var cm = MapStore.Client.Get(m.Id);
            Check(cm != null && cm.Colors.SequenceEqual(m.Colors) && cm.Version == m.Version && cm.Decorations.Count == m.Decorations.Count,
                "Mapa w sieci: gość dostaje piksele, wersję i znaczniki", $"{fullLen} B");
            m.Colors[5 + 6 * 128] = 77; m.RecordChange(5, 6, 5, 6);
            MapSync.Send(buf, w, sent, new[] { m.Id });
            int rectLen = (int)buf.Length; Deliver();
            Check(cm.Colors[5 + 6 * 128] == 77 && rectLen < 100, "Mapa w sieci: zmiana jednego piksela to mały pakiet", $"{rectLen} B");
            MapSync.Send(buf, w, sent, new[] { m.Id });
            Check(buf.Length == 0, "Mapa w sieci: bez zmian nic nie jest wysyłane");
        }
        finally { MapStore.Client = old; }
    }
}
