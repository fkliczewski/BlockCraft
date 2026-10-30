using System.Runtime.CompilerServices;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>
/// Rdzeń redstone jak w MC 1.8 (World.getRedstonePower, getStrongPower, isBlockPowered, notifyNeighborsOfStateChange).
/// Konwencja parametru 'side' w Block.GetWeakPower/GetStrongPower: kierunek OD dostawcy DO pytającego.
/// Funkcje tej klasy z parametrem 'facing' używają konwencji MC: kierunek OD pytającego DO dostawcy.
/// </summary>
public static class Redstone
{
    /// <summary>MC BlockRedstoneWire.canProvidePower: wyłączane na czas liczenia mocy zewnętrznej przewodu.</summary>
    public static bool WiresProvidePower = true;
    /// <summary>Kolejność World.notifyNeighborsOfStateChange w MC 1.8: W, E, D, U, N, S.</summary>
    public static readonly int[] NotifyOrder = { Dir.West, Dir.East, Dir.Down, Dir.Up, Dir.North, Dir.South };
    /// <summary>EnumFacing.Plane.HORIZONTAL: N, E, S, W.</summary>
    public static readonly int[] Horizontals = { Dir.North, Dir.East, Dir.South, Dir.West };
    /// <summary>Bloki pełne, które w MC nie przewodzą mocy (materiał przezroczysty: jasnogłaz, TNT, lód, tłoki...). Moduły mogą dopisywać.</summary>
    public static readonly bool[] NonConductive = new bool[256];

    static Redstone()
    {
        foreach (int id in new[] { B.Glowstone, B.Tnt, B.Ice, B.Piston, B.StickyPiston, B.SlimeBlock, B.SeaLantern, B.Beacon, B.Glass, B.StainedGlass, B.Barrier })
            NonConductive[id] = true;
    }

    public static int RotY(int f) => f switch { Dir.North => Dir.East, Dir.East => Dir.South, Dir.South => Dir.West, Dir.West => Dir.North, _ => f };
    public static int RotYCCW(int f) => f switch { Dir.North => Dir.West, Dir.West => Dir.South, Dir.South => Dir.East, Dir.East => Dir.North, _ => f };
    public static bool IsHoriz(int f) => f >= 2;

    /// <summary>MC Block.isNormalCube: pełny nieprzezroczysty blok, który nie jest źródłem mocy (przewodzi silną moc).</summary>
    public static bool IsNormalCube(int id)
    {
        var b = Block.ById[id];
        return b != null && b.Opaque && b.Solid && !b.IsPowerSource && !NonConductive[id];
    }
    /// <summary>MC Block.isBlockNormalCube (wygląd przewodu): pełny sześcian z kolizją.</summary>
    public static bool IsSolidCube(int id) { var b = Block.ById[id]; return b != null && b.Opaque && b.Solid; }

    /// <summary>Silna moc dostawcy (x,y,z) dla pytającego, który patrzy na niego w kierunku facing.</summary>
    public static int StrongPower(IBlockAccess w, int x, int y, int z, int facing)
    {
        int id = w.GetBlock(x, y, z);
        return id == 0 ? 0 : Block.ById[id].GetStrongPower(w, x, y, z, w.GetMeta(x, y, z), Dir.Opp[facing]);
    }
    public static int WeakPower(IBlockAccess w, int x, int y, int z, int facing)
    {
        int id = w.GetBlock(x, y, z);
        return id == 0 ? 0 : Block.ById[id].GetWeakPower(w, x, y, z, w.GetMeta(x, y, z), Dir.Opp[facing]);
    }

    /// <summary>MC World.getStrongPower(pos): największa silna moc wchodząca do bloku od sąsiadów.</summary>
    public static int StrongPowerInto(IBlockAccess w, int x, int y, int z)
    {
        int max = 0;
        for (int f = 0; f < 6; f++)
        {
            int p = StrongPower(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], f);
            if (p > max) { max = p; if (max >= 15) return 15; }
        }
        return max;
    }

    /// <summary>MC World.getRedstonePower(pos, facing): blok pełny oddaje moc, którą sam dostał silnie; inny swoją słabą.</summary>
    public static int GetRedstonePower(IBlockAccess w, int x, int y, int z, int facing)
    {
        int id = w.GetBlock(x, y, z);
        if (id == 0) return 0;
        return IsNormalCube(id) ? StrongPowerInto(w, x, y, z) : Block.ById[id].GetWeakPower(w, x, y, z, w.GetMeta(x, y, z), Dir.Opp[facing]);
    }
    public static bool IsSidePowered(IBlockAccess w, int x, int y, int z, int facing) => GetRedstonePower(w, x, y, z, facing) > 0;

    /// <summary>Moc docierająca do (x,y,z) od sąsiada w kierunku f.</summary>
    public static int PowerFromSide(IBlockAccess w, int x, int y, int z, int f) => GetRedstonePower(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], f);

    /// <summary>MC isBlockIndirectlyGettingPowered (największa moc ze wszystkich stron).</summary>
    public static int InputPower(IBlockAccess w, int x, int y, int z, int exceptFace = -1)
    {
        int max = 0;
        for (int f = 0; f < 6; f++)
        {
            if (f == exceptFace) continue;
            int p = PowerFromSide(w, x, y, z, f);
            if (p > max) { max = p; if (max >= 15) return 15; }
        }
        return max;
    }

    /// <summary>MC isBlockPowered.</summary>
    public static bool IsBlockPowered(IBlockAccess w, int x, int y, int z)
    {
        for (int f = 0; f < 6; f++) if (PowerFromSide(w, x, y, z, f) > 0) return true;
        return false;
    }

    /// <summary>MC notifyNeighborsOfStateChange (kolejność W, E, D, U, N, S).</summary>
    public static void NotifyNeighbors(World w, int x, int y, int z, int sourceId)
    {
        foreach (int f in NotifyOrder) w.NotifyBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], sourceId);
    }
    /// <summary>MC notifyNeighborsOfStateChangeExcept.</summary>
    public static void NotifyNeighborsExcept(World w, int x, int y, int z, int sourceId, int skip)
    {
        foreach (int f in NotifyOrder) if (f != skip) w.NotifyBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], sourceId);
    }
    /// <summary>Powiadom sąsiadów każdego z 6 sąsiadów (MC: pochodnia, przycisk przy silnym zasilaniu).</summary>
    public static void NotifyAround(World w, int x, int y, int z, int sourceId)
    {
        for (int f = 0; f < 6; f++) NotifyNeighbors(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], sourceId);
    }

    /// <summary>MC World.updateComparatorOutputLevel: zawartość kontenera się zmieniła, komparatory obok (lub za blokiem) liczą od nowa.</summary>
    public static void UpdateComparatorOutputLevel(World w, int x, int y, int z, int sourceId)
    {
        foreach (int f in Horizontals)
        {
            int nx = x + Dir.X[f], nz = z + Dir.Z[f];
            int id = w.GetBlock(nx, y, nz);
            if (id == B.Comparator || id == B.ComparatorOn) w.NotifyBlock(nx, y, nz, sourceId);
            else if (IsNormalCube(id))
            {
                nx += Dir.X[f]; nz += Dir.Z[f];
                id = w.GetBlock(nx, y, nz);
                if (id == B.Comparator || id == B.ComparatorOn) w.NotifyBlock(nx, y, nz, sourceId);
            }
        }
    }

    // ---- zgodność ze starym API ----
    public static bool WireConnects(IBlockAccess w, int x, int y, int z, int h, out int ty)
    {
        int f = Dir.HorizToFace(h);
        var a = RedstoneWireBlock.Attach(w, x, y, z, f);
        ty = a == 2 ? y + 1 : y;
        if (a == 1 && w.GetBlock(x + Dir.X[f], y, z + Dir.Z[f]) != B.RedstoneWire && w.GetBlock(x + Dir.X[f], y - 1, z + Dir.Z[f]) == B.RedstoneWire) ty = y - 1;
        return a != 0;
    }
}

/// <summary>Czerwony proszek 1:1 z BlockRedstoneWire 1.8 (calculateCurrentChanges, kolejność HashSet BlockPos).</summary>
public class RedstoneWireBlock : Block
{
    private readonly int _cross, _line;
    public RedstoneWireBlock(byte id, string k, string n) : base(id, k, n)
    {
        _cross = TextureGen.Layer("redstone_dust_cross"); _line = TextureGen.Layer("redstone_dust_line");
        Tex = ModelBox.All(_cross); Opaque = false; Solid = false; FullCube = false; Render = RenderType.Wire; Layer = RenderLayer.Cutout;
        LightOpacity = 0; Hardness = 0; BlastResistance = 0; NeedsSupport = true; Tint = TintType.RedstoneWire; IsPowerSource = true;
    }
    public int CrossTex => _cross; public int LineTex => _line;
    public override bool CanPlaceAt(World w, int x, int y, int z) => TorchBlock.SolidTop(w, x, y - 1, z) || w.GetBlock(x, y - 1, z) == B.Glowstone;
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 1 / 16.0, 1));
    public override int DropId(int meta, Rng r) => Items.I.Redstone;
    public override ItemStack PickStack(int meta) => new(Items.I.Redstone, 1);
    public override bool ConnectsToRedstone(int meta, int side) => true;

    // ---------- połączenia ----------
    /// <summary>MC func_176343_a(state, side): czy przewód łączy się z tym blokiem (side = kierunek od przewodu, -1 = tylko przewód).</summary>
    public static bool CanConnectTo(IBlockAccess w, int x, int y, int z, int side)
    {
        int id = w.GetBlock(x, y, z);
        if (id == B.RedstoneWire) return true;
        if (side < 0 || id == 0) return false;
        return Block.ById[id].ConnectsToRedstone(w.GetMeta(x, y, z), Dir.Opp[side]);
    }

    /// <summary>MC getAttachPosition: 0 brak, 1 bok, 2 w górę po ścianie.</summary>
    public static int Attach(IBlockAccess w, int x, int y, int z, int f)
    {
        int nx = x + Dir.X[f], nz = z + Dir.Z[f];
        int nid = w.GetBlock(nx, y, nz);
        bool solid = Redstone.IsSolidCube(nid);
        if (CanConnectTo(w, nx, y, nz, f) || (!solid && w.GetBlock(nx, y - 1, nz) == B.RedstoneWire)) return 1;
        if (!Redstone.IsSolidCube(w.GetBlock(x, y + 1, z)) && solid && w.GetBlock(nx, y + 1, nz) == B.RedstoneWire) return 2;
        return 0;
    }

    /// <summary>Maska połączeń (bit h = kierunek poziomy h: 0=S,1=W,2=N,3=E) i maska połączeń w górę.</summary>
    public static int Connections(IBlockAccess w, int x, int y, int z, out int upMask)
    {
        int mask = 0; upMask = 0;
        for (int h = 0; h < 4; h++)
        {
            int a = Attach(w, x, y, z, Dir.HorizToFace(h));
            if (a != 0) { mask |= 1 << h; if (a == 2) upMask |= 1 << h; }
        }
        return mask;
    }

    /// <summary>MC func_176339_d: czy przewód "wskazuje" w kierunku side (do liczenia, komu daje moc).</summary>
    private static bool PowersToward(IBlockAccess w, int x, int y, int z, int side)
    {
        int nx = x + Dir.X[side], nz = z + Dir.Z[side];
        int nid = w.GetBlock(nx, y, nz);
        bool flag = Redstone.IsNormalCube(nid);
        bool flag1 = Redstone.IsNormalCube(w.GetBlock(x, y + 1, z));
        if (!flag1 && flag && w.GetBlock(nx, y + 1, nz) == B.RedstoneWire) return true;
        if (CanConnectTo(w, nx, y, nz, side)) return true;
        return !flag && w.GetBlock(nx, y - 1, nz) == B.RedstoneWire;
    }

    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side)
    {
        if (!Redstone.WiresProvidePower || meta == 0) return 0;
        int facing = Dir.Opp[side]; // MC: od pytającego do przewodu
        if (facing == Dir.Up) return meta;
        int set = 0;
        foreach (int h in Redstone.Horizontals) if (PowersToward(w, x, y, z, h)) set |= 1 << h;
        if (Redstone.IsHoriz(facing) && set == 0) return meta;
        if ((set & (1 << facing)) != 0 && (set & (1 << Redstone.RotYCCW(facing))) == 0 && (set & (1 << Redstone.RotY(facing))) == 0) return meta;
        return 0;
    }
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side)
        => Redstone.WiresProvidePower ? GetWeakPower(w, x, y, z, meta, side) : 0;

    // ---------- aktualizacja ----------
    private static int WirePower(World w, int x, int y, int z, int strength)
    {
        if (w.GetBlock(x, y, z) != B.RedstoneWire) return strength;
        int p = w.GetMeta(x, y, z);
        return p > strength ? p : strength;
    }

    /// <summary>Kubełek java.util.HashMap (pojemność 16) dla BlockPos.hashCode - kolejność iteracji blocksNeedingUpdate w MC.</summary>
    private static int JavaBucket(int x, int y, int z)
    {
        unchecked
        {
            int h = (y + z * 31) * 31 + x;
            h ^= (int)((uint)h >> 16);
            return h & 15;
        }
    }

    private List<(int x, int y, int z)> CalculateCurrentChanges(World w, int x, int y, int z, int oldPower)
    {
        int i = oldPower;
        int j = WirePower(w, x, y, z, 0);
        Redstone.WiresProvidePower = false;
        int k;
        try { k = Redstone.InputPower(w, x, y, z); }
        finally { Redstone.WiresProvidePower = true; }
        if (k > 0 && k > j - 1) j = k;
        int l = 0;
        bool upNormal = Redstone.IsNormalCube(w.GetBlock(x, y + 1, z));
        foreach (int f in Redstone.Horizontals)
        {
            int nx = x + Dir.X[f], nz = z + Dir.Z[f];
            l = WirePower(w, nx, y, nz, l);
            if (Redstone.IsNormalCube(w.GetBlock(nx, y, nz)) && !upNormal) l = WirePower(w, nx, y + 1, nz, l);
            else if (!Redstone.IsNormalCube(w.GetBlock(nx, y, nz))) l = WirePower(w, nx, y - 1, nz, l);
        }
        if (l > j) j = l - 1;
        else if (j > 0) j--;
        else j = 0;
        if (k > j - 1) j = k;
        if (i == j) return null;
        if (w.GetBlock(x, y, z) == B.RedstoneWire && w.GetMeta(x, y, z) == i) w.SetMeta(x, y, z, j, false);
        // HashSet<BlockPos>: pos + 6 sąsiadów (EnumFacing.values()), iteracja wg kubełków HashMap, w kubełku wg wstawienia
        var list = new List<(int x, int y, int z)>(7) { (x, y, z) };
        for (int f = 0; f < 6; f++) list.Add((x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f]));
        return list.Select((p, idx) => (p, idx)).OrderBy(t => JavaBucket(t.p.x, t.p.y, t.p.z)).ThenBy(t => t.idx).Select(t => t.p).ToList();
    }

    private void UpdateSurroundingRedstone(World w, int x, int y, int z, int power)
    {
        var list = CalculateCurrentChanges(w, x, y, z, power);
        if (list == null) return;
        foreach (var (px, py, pz) in list) Redstone.NotifyNeighbors(w, px, py, pz, Id);
    }

    private void NotifyWireNeighbors(World w, int x, int y, int z)
    {
        if (w.GetBlock(x, y, z) != Id) return;
        Redstone.NotifyNeighbors(w, x, y, z, Id);
        for (int f = 0; f < 6; f++) Redstone.NotifyNeighbors(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], Id);
    }

    private void NotifyWireArea(World w, int x, int y, int z)
    {
        foreach (int f in Redstone.Horizontals) NotifyWireNeighbors(w, x + Dir.X[f], y, z + Dir.Z[f]);
        foreach (int f in Redstone.Horizontals)
        {
            int nx = x + Dir.X[f], nz = z + Dir.Z[f];
            if (Redstone.IsNormalCube(w.GetBlock(nx, y, nz))) NotifyWireNeighbors(w, nx, y + 1, nz);
            else NotifyWireNeighbors(w, nx, y - 1, nz);
        }
    }

    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        UpdateSurroundingRedstone(w, x, y, z, w.GetMeta(x, y, z));
        Redstone.NotifyNeighbors(w, x, y + 1, z, Id);
        Redstone.NotifyNeighbors(w, x, y - 1, z, Id);
        NotifyWireArea(w, x, y, z);
    }

    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (w.IsRemote) return;
        for (int f = 0; f < 6; f++) Redstone.NotifyNeighbors(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], Id);
        UpdateSurroundingRedstone(w, x, y, z, oldMeta);
        NotifyWireArea(w, x, y, z);
    }

    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (w.IsRemote) return;
        if (CanPlaceAt(w, x, y, z)) UpdateSurroundingRedstone(w, x, y, z, w.GetMeta(x, y, z));
        else w.BreakBlock(x, y, z, true);
    }
    // gdy World odłożył powiadomienie (zbyt głęboka rekurencja) - zachowuje się jak zmiana sąsiada
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r) => OnNeighborChanged(w, x, y, z, Id);
}

/// <summary>Pochodnia z czerwonego kamienia jak BlockRedstoneTorch 1.8 (z przepaleniem: 8 przełączeń w 60 tickach).</summary>
public class RedstoneTorchBlock : TorchBlock
{
    public readonly bool On;
    private static readonly ConditionalWeakTable<World, List<(int x, int y, int z, long time)>> Toggles = new();

    public RedstoneTorchBlock(byte id, string k, string n, bool on) : base(id, k, n, on ? "redstone_torch_on" : "redstone_torch_off", on ? 7 : 0)
    { On = on; IsPowerSource = true; }
    public override int DropId(int meta, Rng r) => B.RedstoneTorch;
    public override ItemStack PickStack(int meta) => new(B.RedstoneTorch, 1);
    public static (int, int, int) Attached(int x, int y, int z, int meta) => meta switch
    {
        1 => (x - 1, y, z), 2 => (x + 1, y, z), 3 => (x, y, z - 1), 4 => (x, y, z + 1), _ => (x, y - 1, z)
    };
    /// <summary>Kierunek od pochodni do bloku, na którym wisi.</summary>
    public static int AttachedFace(int meta) => meta switch { 1 => Dir.West, 2 => Dir.East, 3 => Dir.North, 4 => Dir.South, _ => Dir.Down };
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side)
        => On && side != AttachedFace(meta) ? 15 : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => On && side == Dir.Up ? 15 : 0;
    public override bool ConnectsToRedstone(int meta, int side) => true;

    private static bool ShouldBeOff(World w, int x, int y, int z)
    {
        int f = AttachedFace(w.GetMeta(x, y, z));
        return Redstone.IsSidePowered(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], f);
    }

    private static bool IsBurnedOut(World w, int x, int y, int z, bool turnOff)
    {
        var list = Toggles.GetOrCreateValue(w);
        if (turnOff) list.Add((x, y, z, w.TotalTime));
        int i = 0;
        foreach (var t in list)
            if (t.x == x && t.y == y && t.z == z && ++i >= 8) return true;
        return false;
    }

    public override void OnAdded(World w, int x, int y, int z)
    {
        if (On && !w.IsRemote) Redstone.NotifyAround(w, x, y, z, Id);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (On && !w.IsRemote) Redstone.NotifyAround(w, x, y, z, Id);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (!CanStay(w, x, y, z)) { w.BreakBlock(x, y, z, true); return; }
        if (On == ShouldBeOff(w, x, y, z)) w.ScheduleTick(x, y, z, Id, 2);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        bool off = ShouldBeOff(w, x, y, z);
        var list = Toggles.GetOrCreateValue(w);
        while (list.Count > 0 && w.TotalTime - list[0].time > 60) list.RemoveAt(0);
        int meta = w.GetMeta(x, y, z);
        if (On)
        {
            if (off)
            {
                w.SetBlock(x, y, z, B.RedstoneTorchOff, meta);
                if (IsBurnedOut(w, x, y, z, true))
                {
                    w.PlaySound("random.fizz", x + 0.5, y + 0.5, z + 0.5, 0.5f, 2.6f + (w.Rand.NextFloat() - w.Rand.NextFloat()) * 0.8f);
                    w.SpawnParticles(ParticleKind.Smoke, x + 0.5, y + 0.7, z + 0.5, 5);
                    w.ScheduleTick(x, y, z, B.RedstoneTorchOff, 160);
                }
            }
        }
        else if (!off && !IsBurnedOut(w, x, y, z, false)) w.SetBlock(x, y, z, B.RedstoneTorch, meta);
    }
}

public static partial class RedstoneExt { }

/// <summary>Dźwignia jak BlockLever 1.8. meta: bity 0-2 = kierunek do bloku-nośnika, bit 3 = włączona.</summary>
public class LeverBlock : Block
{
    public LeverBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("lever"); Opaque = false; Solid = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0.5f; BlastResistance = 0.5f; NeedsSupport = true; IsPowerSource = true; Sound = SoundType.Wood;
    }
    public static int AttachFace(int meta) => meta & 7;
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        int attach = Dir.Opp[face];
        if (Supports(w, x, y, z, attach)) return attach;
        for (int f = 0; f < 6; f++) if (Supports(w, x, y, z, f)) return f;
        return 0;
    }
    protected static bool Supports(World w, int x, int y, int z, int f)
        => f == Dir.Down ? TorchBlock.SolidTop(w, x, y - 1, z) : Block.Get(w.GetBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f])).Opaque;
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        for (int f = 0; f < 6; f++) if (Supports(w, x, y, z, f)) return true;
        return false;
    }
    public override bool CanStay(World w, int x, int y, int z) => Supports(w, x, y, z, AttachFace(w.GetMeta(x, y, z)));
    protected void NotifyOutputs(World w, int x, int y, int z, int meta)
    {
        Redstone.NotifyNeighbors(w, x, y, z, Id);
        int a = AttachFace(meta);
        Redstone.NotifyNeighbors(w, x + Dir.X[a], y + Dir.Y[a], z + Dir.Z[a], Id);
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        if (w.IsRemote) return true;
        int m = w.GetMeta(x, y, z) ^ 8;
        w.SetMeta(x, y, z, m);
        w.PlaySound("random.click", x + 0.5, y + 0.5, z + 0.5, 0.3f, (m & 8) != 0 ? 0.6f : 0.5f);
        NotifyOutputs(w, x, y, z, m);
        return true;
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if ((oldMeta & 8) != 0 && !w.IsRemote) NotifyOutputs(w, x, y, z, oldMeta);
    }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => (meta & 8) != 0 ? 15 : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => (meta & 8) != 0 && side == AttachFace(meta) ? 15 : 0;
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        var b = AttachBox(AttachFace(meta), 4, 3, 8);
        list.Add(new AABB(b.X0, b.Y0, b.Z0, b.X1, b.Y1, b.Z1).Grow(0.05));
    }
    public static ModelBox AttachBox(int attach, float halfW, float halfH, float depth, int[] tex = null)
    {
        // box przylegający do ściany 'attach', wymiary w pikselach
        float c = 8, d = depth;
        return attach switch
        {
            Dir.Down => ModelBox.Px(c - halfW, 0, c - halfH, c + halfW, d / 2.67f, c + halfH, tex),
            Dir.Up => ModelBox.Px(c - halfW, 16 - d / 2.67f, c - halfH, c + halfW, 16, c + halfH, tex),
            Dir.North => ModelBox.Px(c - halfW, c - halfH, 0, c + halfW, c + halfH, d / 2.67f, tex),
            Dir.South => ModelBox.Px(c - halfW, c - halfH, 16 - d / 2.67f, c + halfW, c + halfH, 16, tex),
            Dir.West => ModelBox.Px(0, c - halfH, c - halfW, d / 2.67f, c + halfH, c + halfW, tex),
            _ => ModelBox.Px(16 - d / 2.67f, c - halfH, c - halfW, 16, c + halfH, c + halfW, tex),
        };
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int a = AttachFace(meta); bool on = (meta & 8) != 0;
        int cob = TextureGen.Layer("cobblestone"); int stick = Tex[0];
        boxes.Add(AttachBox(a, 4, 3, 8, ModelBox.All(cob)));
        float off = on ? 3 : -3;
        float len = 8;
        ModelBox hb;
        switch (a)
        {
            case Dir.Down: hb = ModelBox.Px(7, 3, 7 + off, 9, 3 + len, 9 + off, ModelBox.All(stick)); break;
            case Dir.Up: hb = ModelBox.Px(7, 13 - len, 7 + off, 9, 13, 9 + off, ModelBox.All(stick)); break;
            case Dir.North: hb = ModelBox.Px(7, 7 + off, 3, 9, 9 + off, 3 + len, ModelBox.All(stick)); break;
            case Dir.South: hb = ModelBox.Px(7, 7 + off, 13 - len, 9, 9 + off, 13, ModelBox.All(stick)); break;
            case Dir.West: hb = ModelBox.Px(3, 7 + off, 7, 3 + len, 9 + off, 9, ModelBox.All(stick)); break;
            default: hb = ModelBox.Px(13 - len, 7 + off, 7, 13, 9 + off, 9, ModelBox.All(stick)); break;
        }
        hb.Uv = new float[24];
        for (int f = 0; f < 6; f++) { hb.Uv[f * 4] = 7 / 16f; hb.Uv[f * 4 + 1] = 6 / 16f; hb.Uv[f * 4 + 2] = 9 / 16f; hb.Uv[f * 4 + 3] = 1; }
        boxes.Add(hb);
    }
}

/// <summary>Przycisk jak BlockButton 1.8: kamienny 20 ticków, drewniany 30 ticków i wciskany strzałami.</summary>
public class ButtonBlock : LeverBlock
{
    public readonly bool Wooden;
    public ButtonBlock(byte id, string k, string n, bool wooden = false) : base(id, k, n)
    {
        Wooden = wooden;
        if (wooden) { T("planks_oak"); Sound = SoundType.Wood; } else { T("stone"); Sound = SoundType.Stone; }
    }
    public int TickRate => Wooden ? 30 : 20;
    private static AABB Bounds(int meta)
    {
        var b = AttachBox(AttachFace(meta), 3, 2, (meta & 8) != 0 ? 2.67f : 5.34f);
        return new AABB(b.X0, b.Y0, b.Z0, b.X1, b.Y1, b.Z1);
    }
    private void Press(World w, int x, int y, int z, int m, bool on)
    {
        int nm = on ? m | 8 : m & ~8;
        w.SetMeta(x, y, z, nm);
        w.PlaySound("random.click", x + 0.5, y + 0.5, z + 0.5, 0.3f, on ? 0.6f : 0.5f);
        NotifyOutputs(w, x, y, z, nm);
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        if (w.IsRemote) return true;
        int m = w.GetMeta(x, y, z);
        if ((m & 8) != 0) return true;
        Press(w, x, y, z, m, true);
        w.ScheduleTick(x, y, z, Id, TickRate);
        return true;
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 8) == 0) return;
        if (Wooden) CheckArrows(w, x, y, z, m);
        else Press(w, x, y, z, m, false);
    }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e)
    {
        if (Wooden && !w.IsRemote && (w.GetMeta(x, y, z) & 8) == 0) CheckArrows(w, x, y, z, w.GetMeta(x, y, z));
    }
    private void CheckArrows(World w, int x, int y, int z, int m)
    {
        var bb = Bounds(m).Offset(x, y, z);
        bool arrows = w.GetEntitiesInBox(bb).Any(e => e is ArrowEntity);
        bool on = (m & 8) != 0;
        if (arrows && !on) Press(w, x, y, z, m, true);
        if (!arrows && on) Press(w, x, y, z, m, false);
        if (arrows) w.ScheduleTick(x, y, z, Id, TickRate);
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Bounds(meta));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        => boxes.Add(AttachBox(AttachFace(meta), 3, 2, (meta & 8) != 0 ? 2.67f : 5.34f, Tex));
}

/// <summary>Blok czerwonego kamienia (BlockCompressedPowered): słaba moc 15 na wszystkie strony.</summary>
public class RedstoneBlockBlock : Block
{
    public RedstoneBlockBlock(byte id, string k, string n) : base(id, k, n) { IsPowerSource = true; }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => 15;
}

/// <summary>Lampa jak BlockRedstoneLight 1.8 (włącza od razu, gaśnie po 4 tickach).</summary>
public class LampBlock : Block
{
    private readonly bool _on;
    public LampBlock(byte id, string k, string n, bool on) : base(id, k, n)
    {
        _on = on; T(on ? "redstone_lamp_on" : "redstone_lamp_off"); H(0.3f); Sound = SoundType.Glass; if (on) LightEmit = 15;
    }
    public override int DropId(int meta, Rng r) => B.RedstoneLamp;
    public override ItemStack PickStack(int meta) => new(B.RedstoneLamp, 1);
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        bool p = Redstone.IsBlockPowered(w, x, y, z);
        if (_on && !p) w.SetBlock(x, y, z, B.RedstoneLamp, 0, false);
        else if (!_on && p) w.SetBlock(x, y, z, B.RedstoneLampOn, 0, false);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (w.IsRemote) return;
        bool p = Redstone.IsBlockPowered(w, x, y, z);
        if (_on && !p) w.ScheduleTick(x, y, z, Id, 4);
        else if (!_on && p) w.SetBlock(x, y, z, B.RedstoneLampOn, 0, false);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (_on && !Redstone.IsBlockPowered(w, x, y, z)) w.SetBlock(x, y, z, B.RedstoneLamp, 0, false);
    }
}

/// <summary>
/// Wspólna logika diod (BlockRedstoneDiode 1.8): przekaźnik i komparator.
/// meta: bity 0-1 = kierunek WYJŚCIA (0=+Z,1=-X,2=-Z,3=+X), dalsze bity zależą od bloku.
/// </summary>
public abstract class DiodeBlock : Block
{
    protected DiodeBlock(byte id, string k, string n) : base(id, k, n)
    {
        Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0; Hardness = 0; BlastResistance = 0;
        IsPowerSource = true; NeedsSupport = true; Sound = SoundType.Wood;
    }
    public static int OutFace(int meta) => Dir.HorizToFace(meta & 3);
    /// <summary>MC FACING diody: kierunek do wejścia.</summary>
    public static int InFace(int meta) => Dir.Opp[OutFace(meta)];
    public static bool IsDiode(int id) => id == B.Repeater || id == B.RepeaterOn || id == B.Comparator || id == B.ComparatorOn;
    public static bool IsRepeater(int id) => id == B.Repeater || id == B.RepeaterOn;

    public override bool CanPlaceAt(World w, int x, int y, int z) => TorchBlock.SolidTop(w, x, y - 1, z);
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => Dir.HorizFromYaw(p.Yaw);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 0.125, 1));

    protected abstract bool IsPowered(int meta);
    protected abstract int ActiveSignal(IBlockAccess w, int x, int y, int z, int meta);
    protected abstract int Delay(int meta);
    protected virtual bool IsLocked(IBlockAccess w, int x, int y, int z, int meta) => false;
    protected abstract bool CanPowerSide(int id);

    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side)
        => IsPowered(meta) && side == OutFace(meta) ? ActiveSignal(w, x, y, z, meta) : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => GetWeakPower(w, x, y, z, meta, side);

    /// <summary>MC calculateInputStrength.</summary>
    protected virtual int InputStrength(World w, int x, int y, int z, int meta)
    {
        int f = InFace(meta);
        int ix = x + Dir.X[f], iz = z + Dir.Z[f];
        int i = Redstone.GetRedstonePower(w, ix, y, iz, f);
        if (i >= 15) return i;
        return Math.Max(i, w.GetBlock(ix, y, iz) == B.RedstoneWire ? w.GetMeta(ix, y, iz) : 0);
    }
    /// <summary>MC getPowerOnSides (zamek przekaźnika / odejmowanie komparatora).</summary>
    public int PowerOnSides(IBlockAccess w, int x, int y, int z, int meta)
    {
        int f = InFace(meta);
        int l = Redstone.RotY(f), r = Redstone.RotYCCW(f);
        return Math.Max(PowerOnSide(w, x + Dir.X[l], y, z + Dir.Z[l], l), PowerOnSide(w, x + Dir.X[r], y, z + Dir.Z[r], r));
    }
    private int PowerOnSide(IBlockAccess w, int x, int y, int z, int side)
    {
        int id = w.GetBlock(x, y, z);
        if (!CanPowerSide(id)) return 0;
        if (id == B.RedstoneBlock) return 15;
        if (id == B.RedstoneWire) return w.GetMeta(x, y, z);
        return Redstone.StrongPower(w, x, y, z, side);
    }
    protected bool ShouldBePowered(World w, int x, int y, int z, int meta) => InputStrength(w, x, y, z, meta) > 0;

    /// <summary>MC isFacingTowardsRepeater: przed wyjściem stoi dioda, która nie patrzy wejściem w nas.</summary>
    protected static bool FacingTowardsDiode(World w, int x, int y, int z, int meta)
    {
        int f = OutFace(meta);
        int tx = x + Dir.X[f], tz = z + Dir.Z[f];
        int id = w.GetBlock(tx, y, tz);
        return IsDiode(id) && InFace(w.GetMeta(tx, y, tz)) != f;
    }

    /// <summary>MC notifyNeighbors: blok przed wyjściem i jego sąsiedzi (bez nas).</summary>
    protected void NotifyOutput(World w, int x, int y, int z, int meta)
    {
        int f = OutFace(meta);
        int tx = x + Dir.X[f], tz = z + Dir.Z[f];
        w.NotifyBlock(tx, y, tz, Id);
        Redstone.NotifyNeighborsExcept(w, tx, y, tz, Id, Dir.Opp[f]);
    }

    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (w.IsRemote) return;
        if (!CanStay(w, x, y, z))
        {
            w.BreakBlock(x, y, z, true);
            for (int f = 0; f < 6; f++) Redstone.NotifyNeighbors(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], Id);
            return;
        }
        UpdateState(w, x, y, z, w.GetMeta(x, y, z));
    }
    protected abstract void UpdateState(World w, int x, int y, int z, int meta);

    public override void OnPlaced(World w, int x, int y, int z, Player p)
    {
        int meta = w.GetMeta(x, y, z);
        if (ShouldBePowered(w, x, y, z, meta)) w.ScheduleTick(x, y, z, Id, 1);
    }
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    {
        if (IsPowered(meta)) for (int f = 0; f < 6; f++) Redstone.NotifyNeighbors(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], Id);
    }

    protected void AddSlab(List<ModelBox> boxes, int top, int meta)
    {
        int slabSide = TextureGen.Layer("stone_slab_top");
        var t = new int[] { slabSide, top, slabSide, slabSide, slabSide, slabSide };
        var b = ModelBox.Px(0, 0, 0, 16, 2, 16, t);
        b.UvRot = new int[6]; b.UvRot[1] = meta & 3;
        boxes.Add(b);
    }
    protected static void AddTorch(List<ModelBox> boxes, int meta, float along, float side, bool lit, float height = 7)
    {
        int torch = TextureGen.Layer(lit ? "redstone_torch_on" : "redstone_torch_off");
        int d = meta & 3;
        // oś "along" wzdłuż wyjścia, "side" w poprzek
        float cx = 8 + Dir.HorizX[d] * along - Dir.HorizZ[d] * side, cz = 8 + Dir.HorizZ[d] * along + Dir.HorizX[d] * side;
        var tb = ModelBox.Px(cx - 1, 2, cz - 1, cx + 1, height, cz + 1, ModelBox.All(torch));
        tb.Uv = new float[24];
        for (int f = 0; f < 6; f++) { tb.Uv[f * 4] = 7 / 16f; tb.Uv[f * 4 + 1] = 6 / 16f; tb.Uv[f * 4 + 2] = 9 / 16f; tb.Uv[f * 4 + 3] = (4 + height) / 16f; }
        tb.Uv[4] = 7 / 16f; tb.Uv[5] = 6 / 16f; tb.Uv[6] = 9 / 16f; tb.Uv[7] = 8 / 16f;
        boxes.Add(tb);
    }
}

/// <summary>Przekaźnik jak BlockRedstoneRepeater 1.8. meta: bity 0-1 wyjście, bity 2-3 opóźnienie (0..3 = 1..4 ticki redstone).</summary>
public class RepeaterBlock : DiodeBlock
{
    private readonly bool _on;
    public RepeaterBlock(byte id, string k, string n, bool on) : base(id, k, n)
    {
        _on = on; T(on ? "repeater_on" : "repeater_off");
    }
    public override int DropId(int meta, Rng r) => Items.I.RepeaterItem;
    public override ItemStack PickStack(int meta) => new(Items.I.RepeaterItem, 1);
    public override bool ConnectsToRedstone(int meta, int side)
    {
        int o = OutFace(meta);
        return side == o || side == Dir.Opp[o];
    }
    protected override bool IsPowered(int meta) => _on;
    protected override int ActiveSignal(IBlockAccess w, int x, int y, int z, int meta) => 15;
    protected override int Delay(int meta) => ((meta >> 2) & 3) * 2 + 2;
    protected override bool CanPowerSide(int id) => IsDiode(id);
    protected override bool IsLocked(IBlockAccess w, int x, int y, int z, int meta) => PowerOnSides(w, x, y, z, meta) > 0;

    protected override void UpdateState(World w, int x, int y, int z, int meta)
    {
        if (IsLocked(w, x, y, z, meta)) return;
        bool flag = ShouldBePowered(w, x, y, z, meta);
        if ((_on && !flag || !_on && flag) && !w.IsBlockTickPending(x, y, z, Id))
        {
            int prio = -1;
            if (FacingTowardsDiode(w, x, y, z, meta)) prio = -3;
            else if (_on) prio = -2;
            w.ScheduleTickPriority(x, y, z, Id, Delay(meta), prio);
        }
    }

    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        int meta = w.GetMeta(x, y, z);
        if (IsLocked(w, x, y, z, meta)) return;
        bool flag = ShouldBePowered(w, x, y, z, meta);
        if (_on && !flag) w.SetBlock(x, y, z, B.Repeater, meta, false);
        else if (!_on)
        {
            w.SetBlock(x, y, z, B.RepeaterOn, meta, false);
            if (!flag) w.ScheduleTickPriority(x, y, z, B.RepeaterOn, Delay(meta), -1);
        }
    }

    public override void OnAdded(World w, int x, int y, int z) { if (!w.IsRemote) NotifyOutput(w, x, y, z, w.GetMeta(x, y, z)); }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta) { if (!w.IsRemote) NotifyOutput(w, x, y, z, oldMeta); }

    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z);
        int d = ((m >> 2) + 1) & 3;
        w.SetMeta(x, y, z, (m & 3) | (d << 2));
        return true;
    }

    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        AddSlab(boxes, Tex[1], meta);
        int delay = (meta >> 2) & 3;
        AddTorch(boxes, meta, 5, 0, _on);
        bool locked = IsLockedSafe(w, x, y, z, meta);
        if (locked)
        {
            // zamek: belka z bedrocku w poprzek przekaźnika
            int bed = TextureGen.Layer("bedrock");
            int d = meta & 3; float along = -1 - delay * 2;
            float cx = 8 + Dir.HorizX[d] * along, cz = 8 + Dir.HorizZ[d] * along;
            bool alongX = Dir.HorizX[d] != 0;
            boxes.Add(alongX ? ModelBox.Px(cx - 1, 2, 2, cx + 1, 4, 14, ModelBox.All(bed)) : ModelBox.Px(2, 2, cz - 1, 14, 4, cz + 1, ModelBox.All(bed)));
        }
        else AddTorch(boxes, meta, -1 - delay * 2, 0, _on);
    }
    private bool IsLockedSafe(IBlockAccess w, int x, int y, int z, int meta)
    {
        try { return IsLocked(w, x, y, z, meta); } catch { return false; }
    }
}

/// <summary>Tłok jak BlockPistonBase 1.8 (zdarzenia bloku, quasi-łączność, BlockPistonStructureHelper, ruchomy blok).</summary>
public class PistonBlock : Block, IBlockEventReceiver
{
    public readonly bool Sticky;
    private readonly int _top, _side, _bottom, _inner;
    public PistonBlock(byte id, string k, string n, bool sticky) : base(id, k, n)
    {
        Sticky = sticky;
        _top = TextureGen.Layer(sticky ? "piston_top_sticky" : "piston_top"); _side = TextureGen.Layer("piston_side");
        _bottom = TextureGen.Layer("piston_bottom"); _inner = TextureGen.Layer("piston_inner");
        Tex = new[] { _bottom, _top, _side, _side, _side, _side }; H(0.5f); Sound = SoundType.Stone;
    }
    // meta: bity 0-2 = kierunek (face id), bit 3 = wysunięty
    public static int Facing(int meta) => meta & 7;
    public override bool IsOpaqueCube(int meta) => (meta & 8) == 0;
    public override int GetTexture(int face, int meta)
    {
        int f = Facing(meta);
        if (face == f) return (meta & 8) != 0 ? _inner : _top;
        if (face == Dir.Opp[f]) return _bottom;
        return _side;
    }
    public int SideTex => _side; public int TopTex => _top; public int BottomTex => _bottom;
    /// <summary>MC BlockPistonBase.getFacingFromEntity.</summary>
    public static int FacingFromEntity(int x, int y, int z, LivingEntity p)
    {
        if (Math.Abs((float)p.X - x) < 2f && Math.Abs((float)p.Z - z) < 2f)
        {
            double ey = p.Y + p.EyeHeight;
            if (ey - y > 2) return Dir.Up;
            if (y - ey > 0) return Dir.Down;
        }
        return Dir.Opp[Dir.HorizToFace(Dir.HorizFromYaw(p.Yaw))];
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => FacingFromEntity(x, y, z, p);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        if ((meta & 8) == 0) { list.Add(new AABB(0, 0, 0, 1, 1, 1)); return; }
        int f = Facing(meta); const double s = 0.75;
        list.Add(f switch
        {
            Dir.Down => new AABB(0, 1 - s, 0, 1, 1, 1), Dir.Up => new AABB(0, 0, 0, 1, s, 1),
            Dir.North => new AABB(0, 0, 1 - s, 1, 1, 1), Dir.South => new AABB(0, 0, 0, 1, 1, s),
            Dir.West => new AABB(1 - s, 0, 0, 1, 1, 1), _ => new AABB(0, 0, 0, s, 1, 1)
        });
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var l = new List<AABB>(); GetCollisionBoxes(w, x, y, z, meta, l);
        var b = l[0]; var t = new int[6]; for (int i = 0; i < 6; i++) t[i] = GetTexture(i, meta);
        var mb = new ModelBox((float)b.MinX, (float)b.MinY, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, t);
        mb.UvRot = SideRot(Facing(meta));
        boxes.Add(mb);
    }
    public static int[] SideRot(int f)
    {
        var r = new int[6];
        switch (f)
        {
            case Dir.Down: r[2] = r[3] = r[4] = r[5] = 2; break;
            case Dir.North: r[1] = 2; r[0] = 0; r[4] = 1; r[5] = 3; break;
            case Dir.South: r[1] = 0; r[0] = 2; r[4] = 3; r[5] = 1; break;
            case Dir.West: r[1] = 3; r[0] = 3; r[2] = 3; r[3] = 1; break;
            case Dir.East: r[1] = 1; r[0] = 1; r[2] = 1; r[3] = 3; break;
        }
        return r;
    }

    public override void OnPlaced(World w, int x, int y, int z, Player p) { if (!w.IsRemote) CheckForMove(w, x, y, z); }
    public override void OnAdded(World w, int x, int y, int z) { if (!w.IsRemote && w.GetTileEntity(x, y, z) == null) CheckForMove(w, x, y, z); }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) { if (!w.IsRemote) CheckForMove(w, x, y, z); }

    private void CheckForMove(World w, int x, int y, int z)
    {
        int meta = w.GetMeta(x, y, z);
        int f = Facing(meta);
        bool flag = ShouldBeExtended(w, x, y, z, f);
        bool ext = (meta & 8) != 0;
        if (flag && !ext)
        {
            if (new PistonStructure(w, x, y, z, f, true).CanMove()) w.AddBlockEvent(x, y, z, Id, 0, f);
        }
        else if (!flag && ext)
        {
            w.SetMeta(x, y, z, meta & 7, false);
            w.AddBlockEvent(x, y, z, Id, 1, f);
        }
    }

    /// <summary>MC shouldBeExtended: zasilanie z boków (poza przodem) i quasi-łączność przez blok nad tłokiem.</summary>
    public static bool ShouldBeExtended(World w, int x, int y, int z, int facing)
    {
        for (int f = 0; f < 6; f++)
            if (f != facing && Redstone.IsSidePowered(w, x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f], f)) return true;
        if (Redstone.IsSidePowered(w, x, y, z, Dir.Down)) return true;
        for (int f = 0; f < 6; f++)
            if (f != Dir.Down && Redstone.IsSidePowered(w, x + Dir.X[f], y + 1 + Dir.Y[f], z + Dir.Z[f], f)) return true;
        return false;
    }

    public bool OnBlockEvent(World w, int x, int y, int z, int eventId, int f)
    {
        int meta = w.GetMeta(x, y, z);
        bool flag = ShouldBeExtended(w, x, y, z, f);
        if (flag && eventId == 1) { w.SetMeta(x, y, z, f | 8, false); return false; }
        if (!flag && eventId == 0) return false;
        if (eventId == 0)
        {
            if (!DoMove(w, x, y, z, f, true)) return false;
            w.SetMeta(x, y, z, f | 8, false);
            w.PlaySound("tile.piston.out", x + 0.5, y + 0.5, z + 0.5, 0.5f, w.Rand.NextFloat() * 0.25f + 0.6f);
        }
        else if (eventId == 1)
        {
            int hx = x + Dir.X[f], hy = y + Dir.Y[f], hz = z + Dir.Z[f];
            if (w.GetTileEntity(hx, hy, hz) is PistonTileEntity headTe) headTe.Clear(w);
            w.SetBlock(x, y, z, B.PistonExtension, f | (Sticky ? 8 : 0));
            w.SetTileEntity(x, y, z, PistonTileEntity.Create(w, x, y, z, Id, f, f, false, true));
            if (Sticky)
            {
                int bx = x + Dir.X[f] * 2, by = y + Dir.Y[f] * 2, bz = z + Dir.Z[f] * 2;
                int bid = w.GetBlock(bx, by, bz);
                bool dropped = false;
                if (bid == B.PistonExtension && w.GetTileEntity(bx, by, bz) is PistonTileEntity te && te.Facing == f && te.Extending)
                {
                    te.Clear(w); dropped = true;
                }
                if (!dropped && bid != 0 && PistonRules.CanPush(w, bid, bx, by, bz, Dir.Opp[f], false)
                    && (PistonRules.Mobility(bid) == 0 || bid == B.Piston || bid == B.StickyPiston))
                    DoMove(w, x, y, z, f, false);
            }
            else w.SetBlock(hx, hy, hz, B.Air);
            w.PlaySound("tile.piston.in", x + 0.5, y + 0.5, z + 0.5, 0.5f, w.Rand.NextFloat() * 0.15f + 0.6f);
        }
        return true;
    }

    /// <summary>MC BlockPistonBase.doMove.</summary>
    private bool DoMove(World w, int x, int y, int z, int dir, bool extending)
    {
        if (!extending) w.SetBlock(x + Dir.X[dir], y + Dir.Y[dir], z + Dir.Z[dir], B.Air);
        var helper = new PistonStructure(w, x, y, z, dir, extending);
        if (!helper.CanMove()) return false;
        var toMove = helper.ToMove; var toDestroy = helper.ToDestroy;
        int n = toMove.Count + toDestroy.Count;
        var blocks = new int[n];
        int moveDir = extending ? dir : Dir.Opp[dir];
        int i = n;
        for (int j = toDestroy.Count - 1; j >= 0; j--)
        {
            var (bx, by, bz) = toDestroy[j];
            int id = w.GetBlock(bx, by, bz);
            w.BreakBlock(bx, by, bz, true);
            blocks[--i] = id;
        }
        for (int k = toMove.Count - 1; k >= 0; k--)
        {
            var (bx, by, bz) = toMove[k];
            int id = w.GetBlock(bx, by, bz); int m = w.GetMeta(bx, by, bz);
            w.SetBlock(bx, by, bz, B.Air);
            int tx = bx + Dir.X[moveDir], ty = by + Dir.Y[moveDir], tz = bz + Dir.Z[moveDir];
            w.SetBlock(tx, ty, tz, B.PistonExtension, dir, false);
            w.SetTileEntity(tx, ty, tz, PistonTileEntity.Create(w, tx, ty, tz, id, m, dir, extending, false));
            blocks[--i] = id;
        }
        int hx = x + Dir.X[dir], hy = y + Dir.Y[dir], hz = z + Dir.Z[dir];
        if (extending)
        {
            w.SetBlock(hx, hy, hz, B.PistonExtension, dir | (Sticky ? 8 : 0), false);
            w.SetTileEntity(hx, hy, hz, PistonTileEntity.Create(w, hx, hy, hz, B.PistonHead, dir | (Sticky ? 8 : 0), dir, true, false));
        }
        for (int l = toDestroy.Count - 1; l >= 0; l--) { var (bx, by, bz) = toDestroy[l]; Redstone.NotifyNeighbors(w, bx, by, bz, blocks[i++]); }
        for (int l = toMove.Count - 1; l >= 0; l--) { var (bx, by, bz) = toMove[l]; Redstone.NotifyNeighbors(w, bx, by, bz, blocks[i++]); }
        if (extending) { Redstone.NotifyNeighbors(w, hx, hy, hz, B.PistonHead); Redstone.NotifyNeighbors(w, x, y, z, Id); }
        return true;
    }
}

/// <summary>Głowica tłoka jak BlockPistonExtension 1.8. meta: bity 0-2 kierunek, bit 3 lepka.</summary>
public class PistonHeadBlock : Block
{
    public PistonHeadBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("piston_top"); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; H(0.5f);
    }
    public override int DropId(int meta, Rng r) => 0;
    public override ItemStack PickStack(int meta) => new((meta & 8) != 0 ? B.StickyPiston : B.Piston, 1);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => Boxes(meta & 7, false, list);
    public static void Boxes(int f, bool shortArm, List<AABB> list)
    {
        const double p = 0.25;
        list.Add(f switch
        {
            Dir.Down => new AABB(0, 0, 0, 1, p, 1), Dir.Up => new AABB(0, 1 - p, 0, 1, 1, 1),
            Dir.North => new AABB(0, 0, 0, 1, 1, p), Dir.South => new AABB(0, 0, 1 - p, 1, 1, 1),
            Dir.West => new AABB(0, 0, 0, p, 1, 1), _ => new AABB(1 - p, 0, 0, 1, 1, 1)
        });
        double a = shortArm ? 0.25 : 0, b = shortArm ? 0.75 : 1; // krótkie ramię przy wsuwaniu
        list.Add(f switch
        {
            Dir.Down => new AABB(0.375, p, 0.375, 0.625, 1 + (shortArm ? -0.25 : 0), 0.625),
            Dir.Up => new AABB(0.375, a, 0.375, 0.625, 1 - p, 0.625),
            Dir.North => new AABB(0.375, 0.375, p, 0.625, 0.625, b),
            Dir.South => new AABB(0.375, 0.375, a, 0.625, 0.625, 1 - p),
            Dir.West => new AABB(p, 0.375, 0.375, b, 0.625, 0.625),
            _ => new AABB(a, 0.375, 0.375, 1 - p, 0.625, 0.625)
        });
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes) => Model(meta, false, boxes);
    public static void Model(int meta, bool shortArm, List<ModelBox> boxes)
    {
        int f = meta & 7; bool sticky = (meta & 8) != 0;
        var pb = (PistonBlock)Block.Get(sticky ? B.StickyPiston : B.Piston);
        var l = new List<AABB>(); Boxes(f, shortArm, l);
        var t = new int[6]; for (int i = 0; i < 6; i++) t[i] = i == f ? pb.TopTex : i == Dir.Opp[f] ? TextureGen.Layer("piston_top") : pb.SideTex;
        var a = l[0];
        var plate = new ModelBox((float)a.MinX, (float)a.MinY, (float)a.MinZ, (float)a.MaxX, (float)a.MaxY, (float)a.MaxZ, t);
        plate.UvRot = PistonBlock.SideRot(f);
        boxes.Add(plate);
        var r = l[1];
        var rod = new ModelBox((float)r.MinX, (float)r.MinY, (float)r.MinZ, (float)r.MaxX, (float)r.MaxY, (float)r.MaxZ, ModelBox.All(pb.SideTex));
        rod.UvRot = PistonBlock.SideRot(f);
        boxes.Add(rod);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        int f = w.GetMeta(x, y, z) & 7;
        int bx = x - Dir.X[f], by = y - Dir.Y[f], bz = z - Dir.Z[f];
        int id = w.GetBlock(bx, by, bz);
        if (id != B.Piston && id != B.StickyPiston) w.SetBlock(x, y, z, B.Air);
        else Block.ById[id].OnNeighborChanged(w, bx, by, bz, nid);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        int f = oldMeta & 7;
        int bx = x - Dir.X[f], by = y - Dir.Y[f], bz = z - Dir.Z[f];
        int id = w.GetBlock(bx, by, bz);
        if ((id == B.Piston || id == B.StickyPiston) && (w.GetMeta(bx, by, bz) & 8) != 0) w.BreakBlock(bx, by, bz, true);
    }
}
