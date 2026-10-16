using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

public static class Redstone
{
    [ThreadStatic] private static bool _ignoreWires;

    /// <summary>Moc "silna" w bloku (sumarycznie od sąsiadów), używana dla pełnych bloków.</summary>
    public static int StrongPowerInto(IBlockAccess w, int x, int y, int z)
    {
        int max = 0;
        for (int f = 0; f < 6; f++)
        {
            int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
            int id = w.GetBlock(nx, ny, nz);
            if (id == 0) continue;
            if (_ignoreWires && id == B.RedstoneWire) continue;
            var b = Block.ById[id];
            // side = kierunek od sąsiada do nas
            int p = b.GetStrongPower(w, nx, ny, nz, w.GetMeta(nx, ny, nz), Dir.Opp[f]);
            if (p > max) { max = p; if (max >= 15) return 15; }
        }
        return max;
    }

    /// <summary>Moc docierająca do (x,y,z) od sąsiada w kierunku f.</summary>
    public static int PowerFromSide(IBlockAccess w, int x, int y, int z, int f)
    {
        int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
        int id = w.GetBlock(nx, ny, nz);
        if (id == 0) return 0;
        if (_ignoreWires && id == B.RedstoneWire) return 0;
        var b = Block.ById[id];
        if (b.Opaque && !b.IsPowerSource) return StrongPowerInto(w, nx, ny, nz);
        return b.GetWeakPower(w, nx, ny, nz, w.GetMeta(nx, ny, nz), Dir.Opp[f]);
    }

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

    public static bool IsBlockPowered(IBlockAccess w, int x, int y, int z) => InputPower(w, x, y, z) > 0;

    /// <summary>Powiadom sąsiadów i sąsiadów sąsiadów (dla przekazywania mocy przez bloki).</summary>
    public static void NotifyAround(World w, int x, int y, int z, int sourceId)
    {
        w.NotifyNeighbors(x, y, z, sourceId);
        for (int f = 0; f < 6; f++)
        {
            int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
            if (Block.Get(w.GetBlock(nx, ny, nz)).Opaque) w.NotifyNeighbors(nx, ny, nz, sourceId, Dir.Opp[f]);
        }
    }

    // ---------- Sieć przewodów ----------
    private static bool _updatingWires;

    public static bool WireConnects(IBlockAccess w, int x, int y, int z, int h, out int ty)
    {
        // h = kierunek poziomy (0..3), zwraca y celu jeśli jest tam przewód lub komponent
        int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
        int id = w.GetBlock(nx, y, nz);
        ty = y;
        if (id == B.RedstoneWire) return true;
        var b = Block.Get(id);
        if (b.ConnectsToRedstone(w.GetMeta(nx, y, nz), Dir.Opp[Dir.HorizToFace(h)])) return true;
        if (!b.Opaque && w.GetBlock(nx, y - 1, nz) == B.RedstoneWire) { ty = y - 1; return true; }
        if (!Block.Get(w.GetBlock(x, y + 1, z)).Opaque && w.GetBlock(nx, y + 1, nz) == B.RedstoneWire) { ty = y + 1; return true; }
        return false;
    }

    public static void UpdateWireNetwork(World w, int sx, int sy, int sz)
    {
        if (_updatingWires || w.IsRemote) return;
        _updatingWires = true;
        try
        {
            var nodes = new Dictionary<long, (int x, int y, int z)>();
            var q = new Queue<(int, int, int)>();
            q.Enqueue((sx, sy, sz)); nodes[Key(sx, sy, sz)] = (sx, sy, sz);
            while (q.Count > 0 && nodes.Count < 2048)
            {
                var (x, y, z) = q.Dequeue();
                for (int h = 0; h < 4; h++)
                {
                    if (WireConnects(w, x, y, z, h, out int ty))
                    {
                        int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
                        if (w.GetBlock(nx, ty, nz) == B.RedstoneWire && !nodes.ContainsKey(Key(nx, ty, nz)))
                        { nodes[Key(nx, ty, nz)] = (nx, ty, nz); q.Enqueue((nx, ty, nz)); }
                    }
                }
            }
            // moc zewnętrzna
            var power = new Dictionary<long, int>();
            _ignoreWires = true;
            var pq = new PriorityQueue<long, int>();
            foreach (var kv in nodes)
            {
                var (x, y, z) = kv.Value;
                int p = 0;
                for (int f = 0; f < 6; f++)
                {
                    int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
                    int id = w.GetBlock(nx, ny, nz);
                    if (id == B.RedstoneWire || id == 0) continue;
                    int pp = PowerFromSide(w, x, y, z, f);
                    if (pp > p) p = pp;
                }
                power[kv.Key] = p;
                if (p > 0) pq.Enqueue(kv.Key, -p);
            }
            _ignoreWires = false;
            while (pq.TryDequeue(out long k, out int negP))
            {
                int p = -negP;
                if (power[k] != p) continue;
                if (p <= 1) continue;
                var (x, y, z) = nodes[k];
                for (int h = 0; h < 4; h++)
                {
                    if (!WireConnects(w, x, y, z, h, out int ty)) continue;
                    int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
                    long nk = Key(nx, ty, nz);
                    if (!nodes.ContainsKey(nk)) continue;
                    if (power[nk] < p - 1) { power[nk] = p - 1; pq.Enqueue(nk, -(p - 1)); }
                }
            }
            var changed = new List<(int, int, int)>();
            foreach (var kv in nodes)
            {
                var (x, y, z) = kv.Value;
                int np = power[kv.Key];
                if (w.GetMeta(x, y, z) != np) { w.SetMeta(x, y, z, np, false); changed.Add((x, y, z)); }
            }
            foreach (var (x, y, z) in changed)
            {
                for (int f = 0; f < 6; f++)
                {
                    int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
                    int id = w.GetBlock(nx, ny, nz);
                    if (id == B.RedstoneWire) continue;
                    w.NotifyBlock(nx, ny, nz, B.RedstoneWire);
                    if (Block.Get(id).Opaque) w.NotifyNeighbors(nx, ny, nz, B.RedstoneWire, -1, true);
                }
            }
        }
        finally { _updatingWires = false; _ignoreWires = false; }
    }

    private static long Key(int x, int y, int z) => ((long)(x & 0x3FFFFFF) << 38) | ((long)(z & 0x3FFFFFF) << 12) | (uint)(y & 0xFFF);
}

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

    public override void OnAdded(World w, int x, int y, int z) { Redstone.UpdateWireNetwork(w, x, y, z); Redstone.NotifyAround(w, x, y, z, Id); }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        for (int f = 0; f < 6; f++)
        {
            int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
            if (w.GetBlock(nx, ny, nz) == B.RedstoneWire) Redstone.UpdateWireNetwork(w, nx, ny, nz);
            for (int h = 0; h < 4; h++)
            {
                int dx = nx + Dir.HorizX[h], dz = nz + Dir.HorizZ[h];
                if (w.GetBlock(dx, ny, dz) == B.RedstoneWire) Redstone.UpdateWireNetwork(w, dx, ny, dz);
            }
        }
        Redstone.NotifyAround(w, x, y, z, Id);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (!CanStay(w, x, y, z)) { w.BreakBlock(x, y, z, true); return; }
        Redstone.UpdateWireNetwork(w, x, y, z);
    }

    /// <summary>Maska połączeń: bit h = połączenie w kierunku h; zwraca też czy w górę.</summary>
    public static int Connections(IBlockAccess w, int x, int y, int z, out int upMask)
    {
        int mask = 0; upMask = 0;
        for (int h = 0; h < 4; h++)
            if (Redstone.WireConnects(w, x, y, z, h, out int ty)) { mask |= 1 << h; if (ty > y) upMask |= 1 << h; }
        return mask;
    }

    private static bool PointsTo(IBlockAccess w, int x, int y, int z, int h)
    {
        int mask = Connections(w, x, y, z, out _);
        if (mask == 0) return true; // kropka – zasila wszystkie strony
        int opp = (h + 2) & 3;
        bool straight = (mask & ~((1 << h) | (1 << opp))) == 0;
        return (mask & (1 << h)) != 0 && straight || (mask == (1 << opp));
    }

    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side)
    {
        if (meta == 0) return 0;
        if (side == Dir.Down) return meta;
        if (side == Dir.Up) return 0;
        int h = Dir.FaceToHoriz(side);
        // przewód w kierunku h zasila jeśli tam jest komponent / blok
        int mask = Connections(w, x, y, z, out _);
        if ((mask & (1 << h)) != 0) return meta;
        return PointsTo(w, x, y, z, h) ? meta : 0;
    }
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side)
    {
        if (side == Dir.Up) return 0;
        if (side == Dir.Down) return meta;
        int h = Dir.FaceToHoriz(side);
        int nx = x + Dir.HorizX[h], nz = z + Dir.HorizZ[h];
        if (!Block.Get(w.GetBlock(nx, y, nz)).Opaque) return 0;
        return PointsTo(w, x, y, z, h) ? meta : 0;
    }
}

public class RedstoneTorchBlock : TorchBlock
{
    public readonly bool On;
    public RedstoneTorchBlock(byte id, string k, string n, bool on) : base(id, k, n, on ? "redstone_torch_on" : "redstone_torch_off", on ? 7 : 0)
    { On = on; IsPowerSource = true; }
    public override int DropId(int meta, Rng r) => B.RedstoneTorch;
    public override ItemStack PickStack(int meta) => new(B.RedstoneTorch, 1);
    public static (int, int, int) Attached(int x, int y, int z, int meta) => meta switch
    {
        1 => (x - 1, y, z), 2 => (x + 1, y, z), 3 => (x, y, z - 1), 4 => (x, y, z + 1), _ => (x, y - 1, z)
    };
    private static int AttachedFace(int meta) => meta switch { 1 => Dir.West, 2 => Dir.East, 3 => Dir.North, 4 => Dir.South, _ => Dir.Down };
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side)
        => On && side != AttachedFace(meta) ? 15 : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => On && side == Dir.Up ? 15 : 0;
    public override bool ConnectsToRedstone(int meta, int side) => true;

    private bool ShouldBeOff(World w, int x, int y, int z)
    {
        var (ax, ay, az) = Attached(x, y, z, w.GetMeta(x, y, z));
        return Redstone.StrongPowerInto(w, ax, ay, az) > 0;
    }
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (On) Redstone.NotifyAround(w, x, y, z, Id);
        w.ScheduleTick(x, y, z, Id, 2);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta) { if (On) Redstone.NotifyAround(w, x, y, z, Id); }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (!CanStay(w, x, y, z)) { w.BreakBlock(x, y, z, true); return; }
        w.ScheduleTick(x, y, z, Id, 2);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        bool off = ShouldBeOff(w, x, y, z);
        int meta = w.GetMeta(x, y, z);
        if (On && off) { w.SetBlock(x, y, z, B.RedstoneTorchOff, meta, false); Redstone.NotifyAround(w, x, y, z, Id); }
        else if (!On && !off) { w.SetBlock(x, y, z, B.RedstoneTorch, meta, false); Redstone.NotifyAround(w, x, y, z, Id); }
    }
}

public static partial class RedstoneExt { }

public class LeverBlock : Block
{
    public LeverBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("lever"); Opaque = false; Solid = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0.5f; BlastResistance = 0.5f; NeedsSupport = true; IsPowerSource = true; Sound = SoundType.Wood;
    }
    // meta: bity 0-2 = ściana do której przyczepiona (face id 0..5 bloku-nośnika względem dźwigni), bit 3 = włączona
    public static int AttachFace(int meta) => meta & 7;
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        int attach = Dir.Opp[face];
        if (Block.Get(w.GetBlock(x + Dir.X[attach], y + Dir.Y[attach], z + Dir.Z[attach])).Opaque) return attach;
        for (int f = 0; f < 6; f++) if (Block.Get(w.GetBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f])).Opaque) return f;
        return 0;
    }
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        for (int f = 0; f < 6; f++) if (Block.Get(w.GetBlock(x + Dir.X[f], y + Dir.Y[f], z + Dir.Z[f])).Opaque) return true;
        return false;
    }
    public override bool CanStay(World w, int x, int y, int z)
    {
        int a = AttachFace(w.GetMeta(x, y, z));
        return Block.Get(w.GetBlock(x + Dir.X[a], y + Dir.Y[a], z + Dir.Z[a])).Opaque;
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z) ^ 8;
        w.SetMeta(x, y, z, m, false);
        w.PlaySound("click", x + 0.5, y + 0.5, z + 0.5, 0.3f, (m & 8) != 0 ? 0.6f : 0.5f);
        Redstone.NotifyAround(w, x, y, z, Id);
        int a = AttachFace(m);
        Redstone.NotifyAround(w, x + Dir.X[a], y + Dir.Y[a], z + Dir.Z[a], Id);
        return true;
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if ((oldMeta & 8) != 0) { Redstone.NotifyAround(w, x, y, z, Id); int a = AttachFace(oldMeta); Redstone.NotifyAround(w, x + Dir.X[a], y + Dir.Y[a], z + Dir.Z[a], Id); }
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
        // rączka: przesunięta w zależności od stanu
        float off = on ? 3 : -3;
        float ox = 0, oy = 0, oz = 0;
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

public class ButtonBlock : LeverBlock
{
    public ButtonBlock(byte id, string k, string n) : base(id, k, n) { T("stone"); Sound = SoundType.Stone; }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 8) != 0) return true;
        w.SetMeta(x, y, z, m | 8, false);
        w.PlaySound("click", x + 0.5, y + 0.5, z + 0.5, 0.3f, 0.6f);
        Redstone.NotifyAround(w, x, y, z, Id);
        int a = AttachFace(m); Redstone.NotifyAround(w, x + Dir.X[a], y + Dir.Y[a], z + Dir.Z[a], Id);
        w.ScheduleTick(x, y, z, Id, 20);
        return true;
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        int m = w.GetMeta(x, y, z);
        if ((m & 8) == 0) return;
        w.SetMeta(x, y, z, m & ~8, false);
        w.PlaySound("click", x + 0.5, y + 0.5, z + 0.5, 0.3f, 0.5f);
        Redstone.NotifyAround(w, x, y, z, Id);
        int a = AttachFace(m); Redstone.NotifyAround(w, x + Dir.X[a], y + Dir.Y[a], z + Dir.Z[a], Id);
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        var b = AttachBox(AttachFace(meta), 3, 2, (meta & 8) != 0 ? 2.67f : 5.34f);
        list.Add(new AABB(b.X0, b.Y0, b.Z0, b.X1, b.Y1, b.Z1));
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
        => boxes.Add(AttachBox(AttachFace(meta), 3, 2, (meta & 8) != 0 ? 2.67f : 5.34f, Tex));
}

public class RedstoneBlockBlock : Block
{
    public RedstoneBlockBlock(byte id, string k, string n) : base(id, k, n) { IsPowerSource = true; }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => 15;
    public override void OnAdded(World w, int x, int y, int z) => Redstone.NotifyAround(w, x, y, z, Id);
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta) => Redstone.NotifyAround(w, x, y, z, Id);
}

public class LampBlock : Block
{
    private readonly bool _on;
    public LampBlock(byte id, string k, string n, bool on) : base(id, k, n)
    {
        _on = on; T(on ? "redstone_lamp_on" : "redstone_lamp_off"); H(0.3f); Sound = SoundType.Glass; if (on) LightEmit = 15;
    }
    public override int DropId(int meta, Rng r) => B.RedstoneLamp;
    public override ItemStack PickStack(int meta) => new(B.RedstoneLamp, 1);
    public override void OnAdded(World w, int x, int y, int z) => Check(w, x, y, z);
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) => Check(w, x, y, z);
    private void Check(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        bool p = Redstone.IsBlockPowered(w, x, y, z);
        if (!_on && p) w.SetBlock(x, y, z, B.RedstoneLampOn, 0, false);
        else if (_on && !p) w.ScheduleTick(x, y, z, Id, 4);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (_on && !Redstone.IsBlockPowered(w, x, y, z)) w.SetBlock(x, y, z, B.RedstoneLamp, 0, false);
    }
}

public class RepeaterBlock : Block
{
    private readonly bool _on;
    public RepeaterBlock(byte id, string k, string n, bool on) : base(id, k, n)
    {
        _on = on; T(on ? "repeater_on" : "repeater_off");
        Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0; Hardness = 0; BlastResistance = 0;
        IsPowerSource = true; NeedsSupport = true; Sound = SoundType.Wood;
        if (on) LightEmit = 0;
    }
    // meta: bity 0-1 kierunek WYJŚCIA (0=+Z,1=-X,2=-Z,3=+X), bity 2-3 opóźnienie (0..3)
    public override bool CanPlaceAt(World w, int x, int y, int z) => TorchBlock.SolidTop(w, x, y - 1, z);
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => Dir.HorizFromYaw(p.Yaw);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 0.125, 1));
    public override int DropId(int meta, Rng r) => Items.I.RepeaterItem;
    public override ItemStack PickStack(int meta) => new(Items.I.RepeaterItem, 1);
    public override bool ConnectsToRedstone(int meta, int side)
    {
        int d = meta & 3; int outFace = Dir.HorizToFace(d);
        return side == outFace || side == Dir.Opp[outFace];
    }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => _on && side == Dir.HorizToFace(meta & 3) ? 15 : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => GetWeakPower(w, x, y, z, meta, side);
    private bool Input(World w, int x, int y, int z, int meta)
    {
        int inFace = Dir.Opp[Dir.HorizToFace(meta & 3)];
        return Redstone.PowerFromSide(w, x, y, z, inFace) > 0;
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (!CanStay(w, x, y, z)) { w.BreakBlock(x, y, z, true); return; }
        int meta = w.GetMeta(x, y, z);
        bool inp = Input(w, x, y, z, meta);
        if (inp != _on) w.ScheduleTick(x, y, z, Id, ((meta >> 2) & 3) * 2 + 2);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        int meta = w.GetMeta(x, y, z);
        bool inp = Input(w, x, y, z, meta);
        if (_on && !inp) Switch(w, x, y, z, B.Repeater, meta);
        else if (!_on)
        {
            Switch(w, x, y, z, B.RepeaterOn, meta);
            if (!inp) w.ScheduleTick(x, y, z, B.RepeaterOn, ((meta >> 2) & 3) * 2 + 2);
        }
    }
    private static void Switch(World w, int x, int y, int z, byte id, int meta)
    {
        w.SetBlock(x, y, z, id, meta, false);
        int f = Dir.HorizToFace(meta & 3);
        int fx = x + Dir.X[f], fz = z + Dir.Z[f];
        w.NotifyBlock(fx, y, fz, id);
        Redstone.NotifyAround(w, fx, y, fz, id);
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        int m = w.GetMeta(x, y, z);
        int d = ((m >> 2) + 1) & 3;
        w.SetMeta(x, y, z, (m & 3) | (d << 2), false);
        w.PlaySound("click", x + 0.5, y + 0.5, z + 0.5, 0.3f, 0.55f);
        return true;
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        int f = Dir.HorizToFace(oldMeta & 3);
        Redstone.NotifyAround(w, x + Dir.X[f], y, z + Dir.Z[f], Id);
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int slabSide = TextureGen.Layer("stone_slab_top");
        var t = new int[] { slabSide, Tex[1], slabSide, slabSide, slabSide, slabSide };
        var b = ModelBox.Px(0, 0, 0, 16, 2, 16, t);
        b.UvRot = new int[6]; b.UvRot[1] = meta & 3;
        boxes.Add(b);
        int torch = TextureGen.Layer(_on ? "redstone_torch_on" : "redstone_torch_off");
        int d = meta & 3; int delay = (meta >> 2) & 3;
        // pochodnie: przednia (stała) i tylna (ruchoma w zależności od opóźnienia)
        float[] posFront = { 0, 5 }; // przesunięcia wzdłuż osi od środka w pikselach
        void Torch(float along)
        {
            float cx = 8 + Dir.HorizX[d] * along, cz = 8 + Dir.HorizZ[d] * along;
            var tb = ModelBox.Px(cx - 1, 2, cz - 1, cx + 1, 7, cz + 1, ModelBox.All(torch));
            tb.Uv = new float[24];
            for (int f = 0; f < 6; f++) { tb.Uv[f * 4] = 7 / 16f; tb.Uv[f * 4 + 1] = 6 / 16f; tb.Uv[f * 4 + 2] = 9 / 16f; tb.Uv[f * 4 + 3] = 11 / 16f; }
            tb.Uv[4] = 7 / 16f; tb.Uv[5] = 6 / 16f; tb.Uv[6] = 9 / 16f; tb.Uv[7] = 8 / 16f;
            boxes.Add(tb);
        }
        Torch(5);
        Torch(-1 - delay * 2);
    }
}

public class PistonBlock : Block
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
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        double ex = p.X, ey = p.Y + p.EyeHeight, ez = p.Z;
        if (Math.Abs(ex - (x + 0.5)) < 2 && Math.Abs(ez - (z + 0.5)) < 2)
        {
            if (ey - y > 2) return Dir.Up;
            if (y - ey > 0) return Dir.Down;
        }
        int h = Dir.HorizFromYaw(p.Yaw);
        return Dir.Opp[Dir.HorizToFace(h)];
    }
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

    public override void OnAdded(World w, int x, int y, int z) => Check(w, x, y, z);
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid) => Check(w, x, y, z);

    private static bool _busy;
    private void Check(World w, int x, int y, int z)
    {
        if (w.IsRemote || _busy) return;
        int meta = w.GetMeta(x, y, z);
        int f = Facing(meta);
        bool powered = Redstone.InputPower(w, x, y, z, f) > 0;
        bool ext = (meta & 8) != 0;
        _busy = true;
        try
        {
            if (powered && !ext) { if (TryExtend(w, x, y, z, f)) { w.SetMeta(x, y, z, meta | 8, false); w.PlaySound("piston_out", x + 0.5, y + 0.5, z + 0.5); } }
            else if (!powered && ext) { Retract(w, x, y, z, f); w.SetMeta(x, y, z, meta & 7, false); w.PlaySound("piston_in", x + 0.5, y + 0.5, z + 0.5); }
        }
        finally { _busy = false; }
    }

    public static bool CanPush(int id, int meta)
    {
        if (id == B.Bedrock || id == B.Obsidian || id == B.PistonHead || id == B.Chest || id == B.Furnace || id == B.FurnaceLit) return false;
        if ((id == B.Piston || id == B.StickyPiston) && (meta & 8) != 0) return false;
        return true;
    }
    private static bool Breaks(int id)
    {
        var b = Block.Get(id);
        return b.Replaceable || b.NeedsSupport || b.IsLiquid || !b.Solid && id != B.Air;
    }

    private bool TryExtend(World w, int x, int y, int z, int f)
    {
        var line = new List<(int, int, int)>();
        int cx = x, cy = y, cz = z;
        for (int i = 0; i <= 12; i++)
        {
            cx += Dir.X[f]; cy += Dir.Y[f]; cz += Dir.Z[f];
            if (cy < 0 || cy >= Chunk.Height) return false;
            int id = w.GetBlock(cx, cy, cz);
            if (id == B.Air || Breaks(id)) break;
            if (!CanPush(id, w.GetMeta(cx, cy, cz))) return false;
            if (i == 12) return false;
            line.Add((cx, cy, cz));
        }
        // blok końcowy (powietrze lub łamliwy)
        int endId = w.GetBlock(cx, cy, cz);
        if (endId != B.Air) w.BreakBlock(cx, cy, cz, true);
        for (int i = line.Count - 1; i >= 0; i--)
        {
            var (bx, by, bz) = line[i];
            byte id = w.GetBlock(bx, by, bz); int m = w.GetMeta(bx, by, bz);
            w.SetBlock(bx + Dir.X[f], by + Dir.Y[f], bz + Dir.Z[f], id, m);
        }
        int hx = x + Dir.X[f], hy = y + Dir.Y[f], hz = z + Dir.Z[f];
        w.SetBlock(hx, hy, hz, B.PistonHead, f | (Sticky ? 8 : 0));
        // przesuń encje
        foreach (var e in w.GetEntitiesInBox(new AABB(hx, hy, hz, hx + 1, hy + 1, hz + 1).Grow(0.01)))
            e.MoveWithCollision(Dir.X[f] * 0.6, Dir.Y[f] * 0.6, Dir.Z[f] * 0.6);
        return true;
    }

    private void Retract(World w, int x, int y, int z, int f)
    {
        int hx = x + Dir.X[f], hy = y + Dir.Y[f], hz = z + Dir.Z[f];
        PistonHeadBlock.PistonRetracting = true;
        try { if (w.GetBlock(hx, hy, hz) == B.PistonHead) w.SetBlock(hx, hy, hz, B.Air, 0, false); }
        finally { PistonHeadBlock.PistonRetracting = false; }
        if (Sticky)
        {
            int px = hx + Dir.X[f], py = hy + Dir.Y[f], pz = hz + Dir.Z[f];
            byte id = w.GetBlock(px, py, pz); int m = w.GetMeta(px, py, pz);
            if (id != B.Air && !Breaks(id) && CanPush(id, m))
            {
                w.SetBlock(px, py, pz, B.Air);
                w.SetBlock(hx, hy, hz, id, m);
                return;
            }
        }
        w.NotifyNeighbors(hx, hy, hz, B.Air);
    }
}

public class PistonHeadBlock : Block
{
    public PistonHeadBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("piston_top"); Opaque = false; FullCube = false; Render = RenderType.Model; LightOpacity = 0; H(0.5f);
    }
    public override int DropId(int meta, Rng r) => 0;
    public override ItemStack PickStack(int meta) => null;
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        int f = meta & 7;
        const double p = 0.25;
        list.Add(f switch
        {
            Dir.Down => new AABB(0, 0, 0, 1, p, 1), Dir.Up => new AABB(0, 1 - p, 0, 1, 1, 1),
            Dir.North => new AABB(0, 0, 0, 1, 1, p), Dir.South => new AABB(0, 0, 1 - p, 1, 1, 1),
            Dir.West => new AABB(0, 0, 0, p, 1, 1), _ => new AABB(1 - p, 0, 0, 1, 1, 1)
        });
        // drążek
        list.Add(f switch
        {
            Dir.Down or Dir.Up => new AABB(0.375, 0, 0.375, 0.625, 1, 0.625),
            Dir.North or Dir.South => new AABB(0.375, 0.375, 0, 0.625, 0.625, 1),
            _ => new AABB(0, 0.375, 0.375, 1, 0.625, 0.625)
        });
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int f = meta & 7; bool sticky = (meta & 8) != 0;
        var pb = (PistonBlock)Block.Get(sticky ? B.StickyPiston : B.Piston);
        var l = new List<AABB>(); GetCollisionBoxes(w, x, y, z, meta, l);
        var t = new int[6]; for (int i = 0; i < 6; i++) t[i] = i == f ? pb.TopTex : i == Dir.Opp[f] ? pb.TopTex : pb.SideTex;
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
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        int f = oldMeta & 7;
        int bx = x - Dir.X[f], by = y - Dir.Y[f], bz = z - Dir.Z[f];
        int id = w.GetBlock(bx, by, bz);
        if ((id == B.Piston || id == B.StickyPiston) && (w.GetMeta(bx, by, bz) & 8) != 0)
        {
            // gracz zniszczył głowicę -> zniszcz tłok
            if (!PistonRetracting) w.BreakBlock(bx, by, bz, true);
        }
    }
    public static bool PistonRetracting;
}
