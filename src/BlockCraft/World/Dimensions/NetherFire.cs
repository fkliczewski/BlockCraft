using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

/// <summary>Ogień jak BlockFire z MC 1.8: wiek w meta (0-15), rozprzestrzenianie według zachęty i palności, gaszenie deszczem.</summary>
public sealed partial class NetherFireBlock
{
    public const int TickRate = 30;
    /// <summary>Zachęta (szansa zajęcia się sąsiedniego powietrza) i palność (szansa spalenia bloku), wartości z BlockFire.init().</summary>
    public static readonly int[] Encouragement = new int[256], Flammability = new int[256];

    public static void SetFireInfo(byte id, int encouragement, int flammability) { Encouragement[id] = encouragement; Flammability[id] = flammability; }

    static NetherFireBlock()
    {
        foreach (var id in new[] { B.OakPlanks, B.BirchPlanks, B.SprucePlanks, B.JunglePlanks, B.AcaciaPlanks, B.DarkOakPlanks,
                     B.OakSlab, B.BirchSlab, B.SpruceSlab, B.JungleSlab, B.AcaciaSlab, B.DarkOakSlab,
                     B.FenceGate, B.SpruceFenceGate, B.BirchFenceGate, B.JungleFenceGate, B.AcaciaFenceGate, B.DarkOakFenceGate,
                     B.Fence, B.SpruceFence, B.BirchFence, B.JungleFence, B.AcaciaFence, B.DarkOakFence,
                     B.OakStairs, B.BirchStairs, B.SpruceStairs, B.JungleStairs, B.AcaciaStairs, B.DarkOakStairs })
            SetFireInfo(id, 5, 20);
        foreach (var id in new[] { B.OakLog, B.BirchLog, B.SpruceLog, B.JungleLog, B.AcaciaLog, B.DarkOakLog }) SetFireInfo(id, 5, 5);
        foreach (var id in new[] { B.OakLeaves, B.BirchLeaves, B.SpruceLeaves, B.JungleLeaves, B.AcaciaLeaves, B.DarkOakLeaves }) SetFireInfo(id, 30, 60);
        SetFireInfo(B.Bookshelf, 30, 20);
        SetFireInfo(B.Tnt, 15, 100);
        foreach (var id in new[] { B.TallGrass, B.DoublePlant, B.Dandelion, B.Poppy, B.DeadBush }) SetFireInfo(id, 60, 100);
        SetFireInfo(B.Wool, 30, 60);
        SetFireInfo(B.Vine, 15, 100);
        SetFireInfo(B.CoalBlock, 5, 5);
        SetFireInfo(B.HayBale, 60, 20);
        SetFireInfo(B.Carpet, 60, 20);
    }

    private static bool CanCatchFire(IBlockAccess w, int x, int y, int z) => Encouragement[w.GetBlock(x, y, z)] > 0;

    private static bool SolidTop(World w, int x, int y, int z) { var b = Block.Get(w.GetBlock(x, y, z)); return b.Solid && b.FullCube; }

    private static bool CanNeighborCatchFire(World w, int x, int y, int z)
    {
        for (int f = 0; f < 6; f++) if (CanCatchFire(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f])) return true;
        return false;
    }

    private static int NeighborEncouragement(World w, int x, int y, int z)
    {
        if (w.GetBlock(x, y, z) != B.Air) return 0;
        int best = 0;
        for (int f = 0; f < 6; f++) best = Math.Max(best, Encouragement[w.GetBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f])]);
        return best;
    }

    private static bool Raining(World w, int x, int y, int z) => WorldHooks.IsRainingAt?.Invoke(w, x, y, z) == true;

    /// <summary>MC 1.8 canDie: deszcz pada na ogień albo na któregoś poziomego sąsiada.</summary>
    private static bool CanDie(World w, int x, int y, int z)
        => Raining(w, x, y, z) || Raining(w, x - 1, y, z) || Raining(w, x + 1, y, z) || Raining(w, x, y, z - 1) || Raining(w, x, y, z + 1);

    /// <summary>Wieczny ogień: na netherracku, a w Endzie także na bedrocku (filary).</summary>
    private static bool Eternal(World w, int below) => below == B.Netherrack || (below == B.Bedrock && w.Dimension == DimensionId.End);

    public override bool CanPlaceAt(World w, int x, int y, int z) => SolidTop(w, x, y - 1, z) || CanNeighborCatchFire(w, x, y, z);

    // MC 1.8 BlockFire.onBlockAdded: ogień na obsydianie próbuje zapalić ramę portalu (poza Endem).
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.Dimension != DimensionId.End && w.GetBlock(x, y - 1, z) == B.Obsidian && DimensionPortals.Ignite(w, x, y, z)) return;
        if (!CanPlaceAt(w, x, y, z)) { w.SetBlock(x, y, z, B.Air); return; }
        w.ScheduleTick(x, y, z, Id, TickRate + w.Rand.Next(10));
    }

    public override void OnNeighborChanged(World w, int x, int y, int z, int neighborId)
    {
        if (!SolidTop(w, x, y - 1, z) && !CanNeighborCatchFire(w, x, y, z)) w.SetBlock(x, y, z, B.Air);
    }

    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (w.GetBlock(x, y, z) != Id || !w.Rules.DoFireTick) return;
        if (!CanPlaceAt(w, x, y, z)) { w.SetBlock(x, y, z, B.Air); return; }
        bool eternal = Eternal(w, w.GetBlock(x, y - 1, z));
        if (!eternal && CanDie(w, x, y, z)) { w.SetBlock(x, y, z, B.Air); return; }
        int age = w.GetMeta(x, y, z);
        if (age < 15) w.SetMeta(x, y, z, Math.Min(15, age + r.Next(3) / 2));
        w.ScheduleTick(x, y, z, Id, TickRate + r.Next(10), true);
        if (!eternal)
        {
            if (!CanNeighborCatchFire(w, x, y, z))
            {
                if (!SolidTop(w, x, y - 1, z) || age > 3) w.SetBlock(x, y, z, B.Air);
                return;
            }
            if (!CanCatchFire(w, x, y - 1, z) && age == 15 && r.Next(4) == 0) { w.SetBlock(x, y, z, B.Air); return; }
        }
        bool humid = IsHumid(w, x, z);
        int h = humid ? -50 : 0;
        TryCatchFire(w, x + 1, y, z, 300 + h, r, age);
        TryCatchFire(w, x - 1, y, z, 300 + h, r, age);
        TryCatchFire(w, x, y - 1, z, 250 + h, r, age);
        TryCatchFire(w, x, y + 1, z, 250 + h, r, age);
        TryCatchFire(w, x, y, z - 1, 300 + h, r, age);
        TryCatchFire(w, x, y, z + 1, 300 + h, r, age);
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) for (int dy = -1; dy <= 4; dy++)
        {
            if (dx == 0 && dy == 0 && dz == 0) continue;
            int chance = 100 + (dy > 1 ? (dy - 1) * 100 : 0);
            int nx = x + dx, ny = y + dy, nz = z + dz;
            if ((uint)ny >= Chunk.Height) continue;
            int enc = NeighborEncouragement(w, nx, ny, nz);
            if (enc <= 0) continue;
            int spread = (enc + 40 + (int)w.Difficulty * 7) / (age + 30);
            if (humid) spread /= 2;
            if (spread > 0 && r.Next(chance) <= spread && !CanDie(w, nx, ny, nz))
                w.SetBlock(nx, ny, nz, Id, Math.Min(15, age + r.Next(5) / 4));
        }
    }

    private static void TryCatchFire(World w, int x, int y, int z, int chance, Rng r, int age)
    {
        byte id = w.GetBlock(x, y, z);
        if (r.Next(chance) >= Flammability[id]) return;
        if (r.Next(age + 10) < 5 && !Raining(w, x, y, z)) w.SetBlock(x, y, z, B.Fire, Math.Min(15, age + r.Next(5) / 4));
        else w.SetBlock(x, y, z, B.Air);
        if (id == B.Tnt) w.SpawnEntity(new TntEntity(w, x + 0.5, y, z + 0.5, 80));
    }

    /// <summary>MC 1.8 isBlockinHighHumidity: biomy o wilgotności powyżej 0.85 (dżungla, bagna, grzybowa wyspa).</summary>
    private static bool IsHumid(World w, int x, int z) => w.GetBiome(x, z) is 6 or 21 or 22 or 23 or 134 or 149 or 151 or 14 or 15;

    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        // MC gra fire.fire z randomDisplayTick (1/24 na klatkę); tick losowy jest rzadszy, więc bez dodatkowego losowania
        w.PlaySound("fire.fire", x + .5, y + .5, z + .5, 1 + r.NextFloat(), r.NextFloat() * .7f + .3f);
    }
}
