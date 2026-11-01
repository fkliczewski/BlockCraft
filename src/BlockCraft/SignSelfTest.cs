using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Tabliczki (ItemSign, BlockStandingSign, BlockWallSign 1.8).</summary>
public static partial class SelfTest
{
    private static void TestSigns()
    {
        var grid = new ItemStack[9];
        for (int i = 0; i < 6; i++) grid[i] = new ItemStack(B.OakPlanks); grid[7] = new ItemStack(I.Stick);
        var res = Recipes.Find(grid, 3, 3);
        Check(res?.Id == I.SignItem && res.Count == 3 && Item.ById[I.SignItem].MaxStack == 16, "Tabliczka: receptura 3 sztuki, stos 16");

        var w = MakeWorld(17, WorldType.Superflat, 1);
        var p = new Player(w); p.SetPositionAndPrev(8.5, 4, 12.5); w.AddEntityNow(p);
        int gy = 3;
        p.Yaw = 180; // patrzy na północ
        var s = new ItemStack(I.SignItem, 2); p.Inventory.Selected = s;
        Check(Item.ById[I.SignItem].UseOnBlock(p, s, 8, gy, 9, Dir.Up, .5, 1, .5) && w.GetBlock(8, gy + 1, 9) == B.StandingSign && w.GetMeta(8, gy + 1, 9) == 0 && s.Count == 1,
            "Tabliczka: stojąca na ziemi, obrót 0 przy patrzeniu na północ");
        p.Yaw = 225;
        Item.ById[I.SignItem].UseOnBlock(p, new ItemStack(I.SignItem), 6, gy, 9, Dir.Up, .5, 1, .5);
        Check(w.GetMeta(6, gy + 1, 9) == 2, "Tabliczka: obrót co 22,5 stopnia (225 -> 2)", w.GetMeta(6, gy + 1, 9).ToString());
        w.SetBlock(10, gy + 1, 6, B.Stone);
        Check(Item.ById[I.SignItem].UseOnBlock(p, new ItemStack(I.SignItem), 10, gy + 1, 6, Dir.South, .5, .5, 1) && w.GetBlock(10, gy + 1, 7) == B.WallSign && w.GetMeta(10, gy + 1, 7) == Dir.South,
            "Tabliczka: ścienna na południowej ścianie bloku");
        Check(!Item.ById[I.SignItem].UseOnBlock(p, new ItemStack(I.SignItem), 10, gy + 1, 6, Dir.Down, .5, 0, .5), "Tabliczka: nie da się powiesić pod spodem bloku");
        if (w.GetTileEntity(10, gy + 1, 7) is SignTile t) { t.Lines[0] = "Witaj"; }
        w.SetBlock(10, gy + 1, 6, B.Air);
        w.Tick();
        Check(w.GetBlock(10, gy + 1, 7) == 0 && w.Entities.OfType<ItemEntity>().Any(e => e.Stack.Id == I.SignItem), "Tabliczka: bez podparcia odpada jako przedmiot");
        Check(Signs.Filter("§cA\u0001b") == "cAb" && Signs.Filter(new string('x', 99)).Length == Signs.MaxLineChars, "Tabliczka: filtr znaków i długości");
        var data = new SignTile(); data.Lines[1] = "zażółć"; using var ms = new MemoryStream(); using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) data.Save(bw);
        ms.Position = 0; var back = new SignTile(); using (var br = new BinaryReader(ms)) back.Load(br);
        Check(back.Lines[1] == "zażółć" && back.Lines[0] == "", "Tabliczka: zapis i odczyt linii");
    }
}
