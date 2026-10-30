using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

public static partial class SelfTest
{
    private static void TestRedstone18Rails(World.World w)
    {
        const int x = -70, y = 4, z = 80;
        for (int i = 0; i < 12; i++) w.SetBlock(x + i, y, z, B.PoweredRail);
        w.SetBlock(x - 1, y, z, B.RedstoneBlock);
        Check(Enumerable.Range(0, 9).All(i => (w.GetMeta(x + i, y, z) & 8) != 0) && (w.GetMeta(x + 9, y, z) & 8) == 0,
            "Zasilane tory: sygnał do 8 torów od źródła", string.Join(",", Enumerable.Range(0, 12).Select(i => w.GetMeta(x + i, y, z))));
        w.SetBlock(x - 1, y, z, B.Air);
        Check(Enumerable.Range(0, 12).All(i => (w.GetMeta(x + i, y, z) & 8) == 0), "Zasilane tory gasną po odłączeniu");
        for (int i = 0; i < 4; i++)
        {
            int xx = x + i * 5, zz = z + 5;
            int dx = i is 0 or 3 ? 1 : -1, dz = i < 2 ? 1 : -1;
            w.SetBlock(xx, y, zz, B.Rail);
            w.SetBlock(xx + dx, y, zz, B.Rail);
            w.SetBlock(xx, y, zz + dz, B.Rail);
            Check(w.GetMeta(xx, y, zz) == 6 + i, "Tory: łuk " + (6 + i), w.GetMeta(xx, y, zz).ToString());
        }
        w.SetBlock(x, y, z + 10, B.Rail);
        w.SetBlock(x + 1, y, z + 10, B.Stone);
        w.SetBlock(x + 1, y + 1, z + 10, B.Rail);
        Check(w.GetMeta(x, y, z + 10) == 2, "Tory łączą podjazd ze szczytem", w.GetMeta(x, y, z + 10).ToString());
        var boxes = new List<ModelBox>();
        Block.Get(B.Rail).GetModel(w, x, y, z + 10, 2, boxes);
        Check(boxes.Count == 1 && boxes[0].SlopeX == 1, "Model podjazdu ma ciągłe nachylenie");
        w.SetBlock(x + 1, y, z + 10, B.Air);
        Check(w.GetBlock(x, y, z + 10) == B.Air && w.GetBlock(x + 1, y + 1, z + 10) == B.Air, "Tory wypadają po zabraniu podpory podjazdu");

        // Zwrotnica T: trzy wolne końce, tor dokładany w środku.
        int sx = x + 25, sz = z + 6;
        w.SetBlock(sx, y, sz - 1, B.Rail); w.SetBlock(sx, y, sz + 1, B.Rail); w.SetBlock(sx + 1, y, sz, B.Rail);
        w.SetBlock(sx, y, sz, B.Rail);
        int unpowered = w.GetMeta(sx, y, sz);
        w.SetBlock(sx - 1, y, sz, B.RedstoneBlock);
        int powered = w.GetMeta(sx, y, sz);
        Check(unpowered == 6 && powered == 9, "Zwrotnica przełącza odnogę po zasileniu", $"{unpowered}/{powered}");
        w.SetBlock(sx - 1, y, sz, B.Air);
        Check(w.GetMeta(sx, y, sz) == 6, "Zwrotnica wraca po odłączeniu sygnału");
    }
}
