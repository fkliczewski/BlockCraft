using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>Płyny jak w MC: meta 0 = źródło, 1..7 = poziom płynięcia, bit 8 = spadający.</summary>
public class FluidBlock : Block
{
    public readonly bool IsLava;
    public int TickRate => IsLava ? 30 : 5;
    public int DropOff => IsLava ? 2 : 1;

    public FluidBlock(byte id, string k, string n, bool lava) : base(id, k, n)
    {
        IsLava = lava;
        T(lava ? "lava_still" : "water_still");
        Opaque = false; Solid = false; FullCube = false; IsLiquid = true; Replaceable = true;
        Render = RenderType.Liquid; Layer = lava ? RenderLayer.Solid : RenderLayer.Translucent;
        LightOpacity = lava ? 0 : 2; LightEmit = lava ? 15 : 0; Hardness = 100; BlastResistance = 100; InCreative = false;
    }

    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) { }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops) { }
    public override ItemStack PickStack(int meta) => null;

    public static float HeightOf(int meta) => (meta & 8) != 0 ? 1f : (8 - Math.Min(meta & 7, 7)) / 9f;

    public bool Same(int id) => id == Id;

    public override void OnAdded(World w, int x, int y, int z)
    {
        if (!CheckMix(w, x, y, z)) w.ScheduleTick(x, y, z, Id, TickRate);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (!CheckMix(w, x, y, z)) w.ScheduleTick(x, y, z, Id, TickRate);
    }

    /// <summary>Lawa + woda = obsydian / bruk.</summary>
    private bool CheckMix(World w, int x, int y, int z)
    {
        if (!IsLava || w.IsRemote) return false;
        for (int f = 1; f < 6; f++)
        {
            if (w.GetBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f]) == B.Water)
            {
                int m = w.GetMeta(x, y, z);
                w.SetBlock(x, y, z, m == 0 ? B.Obsidian : B.Cobblestone);
                w.PlaySound("fizz", x + 0.5, y + 0.5, z + 0.5);
                w.SpawnParticles(ParticleKind.Smoke, x + 0.5, y + 1, z + 0.5, 8);
                return true;
            }
        }
        return false;
    }

    private int LevelAt(World w, int x, int y, int z)
    {
        if (w.GetBlock(x, y, z) != Id) return -1;
        int m = w.GetMeta(x, y, z);
        return (m & 8) != 0 ? 0 : m;
    }

    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote) return;
        if (w.GetBlock(x, y, z) != Id) return;
        int meta = w.GetMeta(x, y, z);
        int level = meta;
        int drop = DropOff;

        if (meta != 0)
        {
            // wyznacz nowy poziom
            int min = 100; int sources = 0;
            for (int h = 0; h < 4; h++)
            {
                int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
                int l = LevelAt(w, nx, y, nz);
                if (l >= 0)
                {
                    if (l == 0 && w.GetMeta(nx, y, nz) == 0) sources++;
                    if (l < min) min = l;
                }
            }
            int newLevel = min + drop;
            if (newLevel >= 8 || min == 100) newLevel = -1;
            if (w.GetBlock(x, y + 1, z) == Id) newLevel = 8;
            if (!IsLava && sources >= 2)
            {
                int below = w.GetBlock(x, y - 1, z);
                if (Block.Get(below).Solid || (below == Id && w.GetMeta(x, y - 1, z) == 0)) newLevel = 0;
            }
            if (newLevel != meta)
            {
                if (newLevel < 0) { w.SetBlock(x, y, z, B.Air); return; }
                meta = newLevel;
                w.SetMeta(x, y, z, meta);
                w.ScheduleTick(x, y, z, Id, TickRate);
                w.NotifyNeighbors(x, y, z, Id);
            }
        }

        // rozlewanie
        if (y > 0 && CanFlowInto(w, x, y - 1, z))
        {
            if (IsLava && w.GetBlock(x, y - 1, z) == B.Water) { w.SetBlock(x, y - 1, z, B.Stone); w.PlaySound("fizz", x + 0.5, y - 0.5, z + 0.5); return; }
            FlowInto(w, x, y - 1, z, 8);
            return;
        }
        int lv = (meta & 8) != 0 ? 0 : meta;
        int next = lv + drop;
        if (next >= 8) return;
        if (meta != 0 && !IsBlocking(w, x, y - 1, z)) return;
        var dirs = FlowDirections(w, x, y, z);
        for (int h = 0; h < 4; h++)
            if (dirs[h]) FlowInto(w, x + Dir.HorizX[h], y, z + Dir.HorizZ[h], next);
    }

    private bool IsBlocking(World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z);
        var b = Block.Get(id);
        if (id == B.OakDoor || id == B.Ladder || id == B.SugarCane) return true;
        return b.Solid && !b.IsLiquid;
    }

    private bool CanFlowInto(World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z);
        if (id == Id) { return (w.GetMeta(x, y, z) & 8) == 0 && w.GetMeta(x, y, z) != 0; }
        var b = Block.Get(id);
        if (b.IsLiquid) return IsLava ? id == B.Water : false;
        return !IsBlocking(w, x, y, z);
    }

    private void FlowInto(World w, int x, int y, int z, int meta)
    {
        int id = w.GetBlock(x, y, z);
        if (id == Id)
        {
            int m = w.GetMeta(x, y, z);
            if (m == 0) return;
            if ((meta & 8) != 0 || ((m & 8) == 0 && meta < m)) w.SetBlock(x, y, z, Id, meta);
            return;
        }
        var b = Block.Get(id);
        if (b.IsLiquid)
        {
            if (IsLava && id == B.Water) { w.SetBlock(x, y, z, B.Stone); w.PlaySound("fizz", x + 0.5, y + 0.5, z + 0.5); }
            else if (!IsLava && id == B.Lava) { w.SetBlock(x, y, z, w.GetMeta(x, y, z) == 0 ? B.Obsidian : B.Cobblestone); }
            return;
        }
        if (id != B.Air)
        {
            if (IsLava) w.PlaySound("fizz", x + 0.5, y + 0.5, z + 0.5);
            else w.BreakBlock(x, y, z, true);
        }
        w.SetBlock(x, y, z, Id, meta);
    }

    private readonly bool[] _dirs = new bool[4];
    private readonly int[] _cost = new int[4];
    private bool[] FlowDirections(World w, int x, int y, int z)
    {
        for (int h = 0; h < 4; h++)
        {
            _cost[h] = int.MaxValue;
            int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
            if (IsBlocking(w, nx, y, nz) || (w.GetBlock(nx, y, nz) == Id && w.GetMeta(nx, y, nz) == 0)) continue;
            if (!IsBlocking(w, nx, y - 1, nz)) _cost[h] = 0;
            else _cost[h] = Cost(w, nx, y, nz, 1, (h + 2) & 3);
        }
        int min = _cost.Min();
        for (int h = 0; h < 4; h++) _dirs[h] = _cost[h] == min && _cost[h] != int.MaxValue;
        return _dirs;
    }
    private int Cost(World w, int x, int y, int z, int depth, int from)
    {
        int best = 1000;
        for (int h = 0; h < 4; h++)
        {
            if (h == from) continue;
            int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
            if (IsBlocking(w, nx, y, nz) || (w.GetBlock(nx, y, nz) == Id && w.GetMeta(nx, y, nz) == 0)) continue;
            if (!IsBlocking(w, nx, y - 1, nz)) return depth;
            if (depth < 4) { int c = Cost(w, nx, y, nz, depth + 1, (h + 2) & 3); if (c < best) best = c; }
        }
        return best;
    }

    /// <summary>Wektor przepływu (do pchania encji).</summary>
    public (double, double) FlowVector(World w, int x, int y, int z)
    {
        int meta = w.GetMeta(x, y, z);
        int lv = (meta & 8) != 0 ? 0 : meta;
        double fx = 0, fz = 0;
        for (int h = 0; h < 4; h++)
        {
            int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
            int nl = LevelAt(w, nx, y, nz);
            if (nl < 0)
            {
                if (!IsBlocking(w, nx, y, nz))
                {
                    nl = LevelAt(w, nx, y - 1, nz);
                    if (nl >= 0) { int d = nl - (lv - 8); fx += Dir.HorizX[h] * d; fz += Dir.HorizZ[h] * d; }
                }
            }
            else { int d = nl - lv; fx += Dir.HorizX[h] * d; fz += Dir.HorizZ[h] * d; }
        }
        double len = Math.Sqrt(fx * fx + fz * fz);
        if (len > 0) { fx /= len; fz /= len; }
        return (fx, fz);
    }
}
