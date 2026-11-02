using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Obrazy, ramki na przedmioty i stojaki na zbroję (moduł ME-g).</summary>
public static partial class SelfTest
{
    private static void TestHanging()
    {
        var w = MakeWorld(17, WorldType.Superflat, 2);
        w.Rules.DoMobSpawning = false;
        var p = SpawnPlayer(w, 0.5, 4, 0.5);
        foreach (var e in w.Entities.Where(e => e != p).ToList()) e.Kill();
        w.FlushAdds();

        var grid = new ItemStack[9];
        for (int i = 0; i < 9; i++) grid[i] = new ItemStack(I.Stick);
        grid[4] = new ItemStack(B.Wool, 1, 5);
        Check(Recipes.Find(grid, 3, 3)?.Id == I.Painting, "Obraz: receptura (patyki + wełna dowolnego koloru)");
        grid[4] = new ItemStack(I.Leather);
        Check(Recipes.Find(grid, 3, 3)?.Id == I.ItemFrame, "Ramka: receptura (patyki + skóra)");
        var stand = new ItemStack[9]; stand[0] = stand[1] = stand[2] = stand[4] = stand[6] = stand[8] = new ItemStack(I.Stick); stand[7] = new ItemStack(B.StoneSlab);
        Check(Recipes.Find(stand, 3, 3)?.Id == I.ArmorStand, "Stojak na zbroję: receptura");
        Check(PaintingArt.All.Length == 26, "Obraz: 26 motywów 1.8");

        // ściana z kamienia 4x4 od z=10 (front na północ, czyli od strony -Z)
        for (int x = 0; x < 4; x++) for (int y = 4; y < 8; y++) w.SetBlock(x, y, 10, B.Stone);
        var held = new ItemStack(I.Painting, 3); p.Inventory.Selected = held;
        Check(Item.ById[I.Painting].UseOnBlock(p, held, 1, 5, 10, Dir.Up, .5, 1, .5) == false, "Obraz: nie wisi na górnej ścianie bloku");
        Item.ById[I.Painting].UseOnBlock(p, held, 1, 5, 10, Dir.North, .5, .5, 0);
        w.FlushAdds();
        var painting = w.Entities.OfType<PaintingEntity>().FirstOrDefault();
        Check(painting != null && held.Count == 2 && painting.Facing == 2 && painting.HangX == 1 && painting.HangZ == 9,
            "Obraz: zawieszony przed ścianą, zwrócony na północ, zużywa przedmiot", painting == null ? "brak" : $"f{painting.Facing} {painting.HangX},{painting.HangZ} n{held.Count}");
        if (painting != null)
        {
            var art = PaintingArt.All[painting.Art];
            Check(art.W <= 64 && art.H <= 64 && painting.OnValidSurface(), "Obraz: wylosowany motyw mieści się na ścianie 4x4", art.Name);
            var bb = painting.BoundingBox;
            Check(Math.Abs(bb.MaxZ - 10) < 1e-6 && Math.Abs(bb.MaxZ - bb.MinZ - 1 / 16.0) < 1e-6, "Obraz: grubość 1/16 bloku, przylega do ściany", $"{bb.MinZ:F4}-{bb.MaxZ:F4}");
            // drugi obraz w tym samym miejscu się nie zmieści
            var again = PaintingEntity.Create(w, 1, 5, 9, 2);
            again.Art = painting.Art; again.SetFacing(2);
            Check(!again.OnValidSurface(), "Obraz: nie nachodzi na inny obraz");
            // usunięcie ściany: po 100 tickach odpada jako przedmiot
            foreach (var (x, y, z) in painting.SupportBlocks()) w.SetBlock(x, y, z, B.Air);
            for (int i = 0; i < 102; i++) w.Tick();
            Check(painting.Removed && w.Entities.OfType<ItemEntity>().Any(i => i.Stack.Id == I.Painting), "Obraz: bez ściany odpada po 100 tickach i wypada przedmiot");
        }
        // mały obraz: na pojedynczym bloku wybierane są tylko motywy 16x16
        w.SetBlock(10, 5, 10, B.Stone);
        var small = PaintingEntity.Create(w, 10, 5, 9, 2);
        Check(PaintingArt.All[small.Art].W == 16 && PaintingArt.All[small.Art].H == 16, "Obraz: na jednym bloku tylko motyw 16x16", PaintingArt.All[small.Art].Name);
        w.AddEntityNow(small);
        small.Attack(DamageSource.PlayerAttack(p), 1);
        Check(small.Removed, "Obraz: cios gracza go zdejmuje");

        // ramka + komparator przez blok: kamień (20,4,40), komparator (21) na wschód, ramka na zachodniej ścianie kamienia
        w.SetBlock(20, 4, 40, B.Stone);
        w.SetBlock(21, 4, 40, B.Comparator, Dir.FaceToHoriz(Dir.East));
        w.SetBlock(22, 4, 40, B.RedstoneWire);
        var fheld = new ItemStack(I.ItemFrame, 1); p.Inventory.Selected = fheld;
        Item.ById[I.ItemFrame].UseOnBlock(p, fheld, 20, 4, 40, Dir.West, 0, .5, .5);
        w.FlushAdds();
        var frame = w.Entities.OfType<ItemFrameEntity>().FirstOrDefault();
        Check(frame != null && p.Inventory.Selected == null && frame.FacingFace == Dir.West, "Ramka: zawieszona na zachodniej ścianie");
        if (frame != null)
        {
            p.Inventory.Selected = new ItemStack(I.Diamond, 5);
            frame.Interact(p);
            Ticks(w, 6);
            Check(frame.Item?.Id == I.Diamond && frame.Item.Count == 1 && p.Inventory.Selected.Count == 4, "Ramka: PPM wkłada jeden przedmiot");
            Check(w.GetMeta(22, 4, 40) == 1, "Ramka: komparator za blokiem czyta 1 (obrót 0)", w.GetMeta(22, 4, 40).ToString());
            for (int i = 0; i < 3; i++) frame.Interact(p);
            Ticks(w, 6);
            Check(frame.Rotation == 3 && w.GetMeta(22, 4, 40) == 4, "Ramka: każdy PPM obraca o 45 stopni, komparator = obrót + 1", $"rot {frame.Rotation} sig {w.GetMeta(22, 4, 40)}");
            for (int i = 0; i < 5; i++) frame.Interact(p);
            Check(frame.Rotation == 0, "Ramka: 8 obrotów wraca do zera");
            int drops = w.Entities.OfType<ItemEntity>().Count(i => i.Stack.Id == I.Diamond);
            frame.Attack(DamageSource.PlayerAttack(p), 1);
            Ticks(w, 6);
            Check(!frame.Removed && frame.Item == null && w.Entities.OfType<ItemEntity>().Count(i => i.Stack.Id == I.Diamond) == drops + 1,
                "Ramka: pierwszy cios zdejmuje przedmiot, ramka zostaje");
            Check(w.GetMeta(22, 4, 40) == 0, "Ramka: pusta ramka = sygnał 0", w.GetMeta(22, 4, 40).ToString());
            frame.Attack(DamageSource.PlayerAttack(p), 1);
            w.FlushAdds();
            Check(frame.Removed && w.Entities.OfType<ItemEntity>().Any(i => i.Stack.Id == I.ItemFrame), "Ramka: drugi cios zdejmuje ramkę jako przedmiot");
        }

        // stojak na zbroję
        var sheld = new ItemStack(I.ArmorStand, 2); p.Inventory.Selected = sheld; p.Yaw = 10;
        Item.ById[I.ArmorStand].UseOnBlock(p, sheld, 30, 3, 30, Dir.Up, .5, 1, .5);
        w.FlushAdds();
        var st = w.Entities.OfType<ArmorStandEntity>().FirstOrDefault();
        Check(st != null && sheld.Count == 1 && Math.Abs(st.X - 30.5) < 1e-6 && Math.Abs(st.Y - 4) < 1e-6 && st.Yaw == -180,
            "Stojak: postawiony na bloku, obrót co 45 stopni przodem do gracza", st == null ? "brak" : $"{st.X},{st.Y},{st.Z} yaw {st.Yaw}");
        if (st != null)
        {
            p.SetPositionAndPrev(30.5, 4, 27.5); p.Yaw = 0; p.Pitch = 0;
            p.Inventory.Selected = new ItemStack(I.IronChest);
            st.Interact(p);
            Check(st.Armor[1]?.Id == I.IronChest && p.Inventory.Selected == null, "Stojak: PPM napierśnikiem zakłada go");
            p.Inventory.Selected = new ItemStack(I.DiamondHelmet);
            st.Interact(p);
            Check(st.Armor[0]?.Id == I.DiamondHelmet, "Stojak: hełm trafia na głowę niezależnie od miejsca kliknięcia");
            p.Inventory.Selected = new ItemStack(I.Stick);
            st.Interact(p);
            Check(p.Inventory.Selected?.Id == I.Stick && st.Held == null, "Stojak: bez rąk nie przyjmuje przedmiotu do ręki");
            // pusta ręka, celowanie w tułów (wysokość ok. 1.3 nad stopami) zdejmuje napierśnik
            p.Inventory.Selected = null; p.SetPositionAndPrev(30.5, 4 + 1.3 - p.EyeHeight, 27.5);
            st.Interact(p);
            Check(p.Inventory.Selected?.Id == I.IronChest && st.Armor[1] == null, "Stojak: pusta ręka zdejmuje część, w którą się celuje");
            long t0 = w.TotalTime;
            st.Attack(DamageSource.PlayerAttack(p), 1);
            Check(!st.Removed && st.PunchTime == t0, "Stojak: pierwszy cios tylko go chwieje");
            st.Attack(DamageSource.PlayerAttack(p), 1);
            w.FlushAdds();
            Check(st.Removed && w.Entities.OfType<ItemEntity>().Any(i => i.Stack.Id == I.ArmorStand) && w.Entities.OfType<ItemEntity>().Any(i => i.Stack.Id == I.DiamondHelmet),
                "Stojak: drugi cios w ciągu 5 ticków niszczy go, wypada stojak i zbroja");
        }

        // zapis i odczyt (także dane sieci)
        var f2 = ItemFrameEntity.Create(w, 5, 5, 5, 3); f2.Item = new ItemStack(I.Compass); f2.Rotation = 6;
        var ms = new MemoryStream(); f2.Save(new BinaryWriter(ms)); ms.Position = 0;
        var f3 = new ItemFrameEntity(w); f3.Load(new BinaryReader(ms));
        Check(f3.Item?.Id == I.Compass && f3.Rotation == 6 && f3.Facing == 3 && f3.HangX == 5 && Math.Abs(f3.BoundingBox.MinX - f2.BoundingBox.MinX) < 1e-9,
            "Ramka: zapis zachowuje przedmiot, obrót i położenie");
        var pa = PaintingEntity.Create(w, 1, 5, 9, 2); pa.Art = PaintingArt.IndexOf("Wither"); pa.SetFacing(2);
        ms = new MemoryStream(); pa.Save(new BinaryWriter(ms)); ms.Position = 0;
        var pb = new PaintingEntity(w); pb.Load(new BinaryReader(ms));
        Check(PaintingArt.All[pb.Art].Name == "Wither" && pb.Facing == 2, "Obraz: zapis zachowuje motyw");
    }
}
