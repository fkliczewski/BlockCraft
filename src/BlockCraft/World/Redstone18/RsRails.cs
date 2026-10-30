using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>Znacznik wagonika dla toru detektorowego.</summary>
public interface IRailVehicle { }

/// <summary>Tory 1.8. Kształty meta: NS, EW, podjazdy E/W/N/S, łuki SE/SW/NW/NE.</summary>
public sealed class RailBlock : Block, IComparatorSource
{
    public readonly bool StraightOnly;
    private readonly string _texture;
    public RailBlock(byte id, string key, string name, string texture) : base(id, key, name)
    {
        StraightOnly = id != B.Rail; _texture = texture;
        T(texture); Hardness = 0.7f; BlastResistance = 0.7f; Sound = SoundType.Metal;
        Opaque = Solid = FullCube = false; LightOpacity = 0; Render = RenderType.Model; Layer = RenderLayer.Cutout;
        IsPowerSource = id == B.DetectorRail; CreativeTab = Blocks.TabTransport;
    }
    public static bool IsRail(int id) => id is B.Rail or B.PoweredRail or B.DetectorRail or B.ActivatorRail;
    public static int Shape(int id, int meta) => Math.Clamp(id == B.Rail ? meta : meta & 7, 0, id == B.Rail ? 9 : 5);
    public override bool CanPlaceAt(World w, int x, int y, int z) => TorchBlock.SolidTop(w, x, y - 1, z);
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
        => list.Add(new AABB(0, 0, 0, 1, Shape(Id, meta) is >= 2 and <= 5 ? 0.625 : 0.125, 1));
    public override int GetTexture(int face, int meta)
        => TextureGen.Layer(Id == B.Rail && meta >= 6 ? "rail_turn_" + meta : _texture + (StraightOnly && (meta & 8) != 0 ? "_powered" : ""));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int s = Shape(Id, meta); float baseY = 1 / 16f;
        if (s is 3 or 4) baseY += 1;
        var rot = new int[6]; if (s is 1 or 2 or 3) rot[0] = rot[1] = 1;
        boxes.Add(new ModelBox(0, baseY, 0, 1, baseY, 1, ModelBox.All(GetTexture(1, meta)))
        { NoCull = true, UvRot = rot, SlopeX = s == 2 ? 1 : s == 3 ? -1 : 0, SlopeZ = s == 5 ? 1 : s == 4 ? -1 : 0 });
    }
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        new RailConnections(w, (x, y, z)).Place(Redstone.IsBlockPowered(w, x, y, z), true);
        if (StraightOnly) OnNeighborChanged(w, x, y, z, Id);
        if (Id == B.DetectorRail) UpdateDetector(w, x, y, z);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int neighborId)
    {
        if (w.IsRemote) return;
        int meta = w.GetMeta(x, y, z), s = Shape(Id, meta);
        int support = s switch { 2 => Dir.East, 3 => Dir.West, 4 => Dir.North, 5 => Dir.South, _ => -1 };
        if (!CanPlaceAt(w, x, y, z) || support >= 0 && !TorchBlock.SolidTop(w, x + Dir.X[support], y, z + Dir.Z[support]))
        { w.BreakBlock(x, y, z, true); return; }
        if (Id == B.Rail)
        {
            var c = new RailConnections(w, (x, y, z));
            if ((neighborId == B.Air || Block.Get(neighborId).IsPowerSource) && c.AdjacentCount() == 3) c.Place(Redstone.IsBlockPowered(w, x, y, z), false);
        }
        else if (Id is B.PoweredRail or B.ActivatorRail)
        {
            bool on = Redstone.IsBlockPowered(w, x, y, z) || FindPower(w, x, y, z, meta, true, 0) || FindPower(w, x, y, z, meta, false, 0);
            if (on != ((meta & 8) != 0))
            {
                w.SetMeta(x, y, z, (meta & 7) | (on ? 8 : 0));
                w.NotifyNeighbors(x, y - 1, z, Id);
                if (s is >= 2 and <= 5) w.NotifyNeighbors(x, y + 1, z, Id);
            }
        }
    }
    // Sygnał przechodzi przez najwyżej osiem zgodnie ułożonych torów tego samego rodzaju.
    private bool FindPower(World w, int x, int y, int z, int meta, bool back, int depth)
    {
        if (depth >= 8) return false;
        int s = Shape(Id, meta); bool below = true;
        switch (s)
        {
            case 0: z += back ? 1 : -1; break;
            case 1: x += back ? -1 : 1; break;
            case 2: x += back ? -1 : 1; if (!back) { y++; below = false; } s = 1; break;
            case 3: x += back ? -1 : 1; if (back) { y++; below = false; } s = 1; break;
            case 4: z += back ? 1 : -1; if (!back) { y++; below = false; } s = 0; break;
            case 5: z += back ? 1 : -1; if (back) { y++; below = false; } s = 0; break;
        }
        bool At(int yy)
        {
            if (w.GetBlock(x, yy, z) != Id) return false;
            int m = w.GetMeta(x, yy, z), next = Shape(Id, m);
            if ((m & 8) == 0 || s == 1 && next is 0 or 4 or 5 || s == 0 && next is 1 or 2 or 3) return false;
            return Redstone.IsBlockPowered(w, x, yy, z) || FindPower(w, x, yy, z, m, back, depth + 1);
        }
        return At(y) || below && At(y - 1);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (w.IsRemote) return;
        if (Shape(Id, oldMeta) is >= 2 and <= 5) w.NotifyNeighbors(x, y + 1, z, Id);
        if (StraightOnly) { w.NotifyNeighbors(x, y, z, Id); w.NotifyNeighbors(x, y - 1, z, Id); }
    }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e)
    { if (!w.IsRemote && Id == B.DetectorRail && (w.GetMeta(x, y, z) & 8) == 0) UpdateDetector(w, x, y, z); }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    { if (Id == B.DetectorRail) UpdateDetector(w, x, y, z); }
    private static List<Entity> Carts(World w, int x, int y, int z)
        => RsUtil.EntitiesIn(w, new AABB(x + 0.125, y, z + 0.125, x + 0.875, y + 0.8, z + 0.875)).Where(e => e is IRailVehicle).ToList();
    private void UpdateDetector(World w, int x, int y, int z)
    {
        int meta = w.GetMeta(x, y, z); bool on = Carts(w, x, y, z).Count > 0;
        if (on != ((meta & 8) != 0))
        { w.SetMeta(x, y, z, (meta & 7) | (on ? 8 : 0)); w.NotifyNeighbors(x, y, z, Id); w.NotifyNeighbors(x, y - 1, z, Id); }
        if (on) w.ScheduleTick(x, y, z, Id, 20);
        // Fundament nie ma osobnego powiadomienia komparatorów; ich TE odczytuje stan co tick.
    }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => Id == B.DetectorRail && (meta & 8) != 0 ? 15 : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => side == Dir.Down ? GetWeakPower(w, x, y, z, meta, side) : 0;
    public int GetComparatorOutput(World w, int x, int y, int z)
        => Id == B.DetectorRail && (w.GetMeta(x, y, z) & 8) != 0 ? ComparatorRead.FromInventory(Carts(w, x, y, z).OfType<IInventory>().FirstOrDefault()) : 0;

    /// <summary>Połączenia jak BlockRailBase.Rail: najpierw usuń pozorne końce, potem dobierz kształt i powiadom połączone tory.</summary>
    private sealed class RailConnections
    {
        private readonly World _w;
        private readonly (int x, int y, int z) _p;
        private readonly RailBlock _block;
        private readonly List<(int x, int y, int z)> _ends = new();
        public RailConnections(World w, (int x, int y, int z) p)
        { _w = w; _p = p; _block = (RailBlock)Block.Get(w.GetBlock(p.x, p.y, p.z)); Ends(Shape(_block.Id, w.GetMeta(p.x, p.y, p.z))); }
        private void Ends(int s)
        {
            _ends.Clear(); var (x, y, z) = _p;
            switch (s)
            {
                case 0: _ends.Add((x, y, z - 1)); _ends.Add((x, y, z + 1)); break;
                case 1: _ends.Add((x - 1, y, z)); _ends.Add((x + 1, y, z)); break;
                case 2: _ends.Add((x - 1, y, z)); _ends.Add((x + 1, y + 1, z)); break;
                case 3: _ends.Add((x - 1, y + 1, z)); _ends.Add((x + 1, y, z)); break;
                case 4: _ends.Add((x, y + 1, z - 1)); _ends.Add((x, y, z + 1)); break;
                case 5: _ends.Add((x, y, z - 1)); _ends.Add((x, y + 1, z + 1)); break;
                case 6: _ends.Add((x + 1, y, z)); _ends.Add((x, y, z + 1)); break;
                case 7: _ends.Add((x - 1, y, z)); _ends.Add((x, y, z + 1)); break;
                case 8: _ends.Add((x - 1, y, z)); _ends.Add((x, y, z - 1)); break;
                case 9: _ends.Add((x + 1, y, z)); _ends.Add((x, y, z - 1)); break;
            }
        }
        private bool Connects((int x, int y, int z) p) => _ends.Any(e => e.x == p.x && e.z == p.z);
        private RailConnections Find((int x, int y, int z) p)
        {
            foreach (int dy in new[] { 0, 1, -1 }) if (IsRail(_w.GetBlock(p.x, p.y + dy, p.z))) return new RailConnections(_w, (p.x, p.y + dy, p.z));
            return null;
        }
        private void Clean()
        {
            for (int i = 0; i < _ends.Count; i++)
            { var c = Find(_ends[i]); if (c != null && c.Connects(_p)) _ends[i] = c._p; else _ends.RemoveAt(i--); }
        }
        private bool Accepts(RailConnections c) => Connects(c._p) || _ends.Count != 2;
        private bool Available(int dx, int dz)
        { var c = Find((_p.x + dx, _p.y, _p.z + dz)); if (c == null) return false; c.Clean(); return c.Accepts(this); }
        public int AdjacentCount() => new[] { (0, -1), (0, 1), (-1, 0), (1, 0) }.Count(d => Find((_p.x + d.Item1, _p.y, _p.z + d.Item2)) != null);
        private int Slope(int s)
        {
            var (x, y, z) = _p;
            if (s == 0) { if (IsRail(_w.GetBlock(x, y + 1, z - 1))) s = 4; if (IsRail(_w.GetBlock(x, y + 1, z + 1))) s = 5; }
            if (s == 1) { if (IsRail(_w.GetBlock(x + 1, y + 1, z))) s = 2; if (IsRail(_w.GetBlock(x - 1, y + 1, z))) s = 3; }
            return s;
        }
        private void Write(int s)
        {
            var (x, y, z) = _p; int m = _w.GetMeta(x, y, z);
            _w.SetMeta(x, y, z, s | (_block.StraightOnly ? m & 8 : 0));
        }
        private void Connect(RailConnections c)
        {
            _ends.Add(c._p);
            bool n = Connects((_p.x, _p.y, _p.z - 1)), s = Connects((_p.x, _p.y, _p.z + 1));
            bool w = Connects((_p.x - 1, _p.y, _p.z)), e = Connects((_p.x + 1, _p.y, _p.z));
            int shape = n || s ? 0 : -1; if (w || e) shape = 1;
            if (!_block.StraightOnly)
            {
                if (s && e && !n && !w) shape = 6; if (s && w && !n && !e) shape = 7;
                if (n && w && !s && !e) shape = 8; if (n && e && !s && !w) shape = 9;
            }
            Write(Slope(Math.Max(0, shape)));
        }
        public void Place(bool power, bool force)
        {
            bool n = Available(0, -1), s = Available(0, 1), w = Available(-1, 0), e = Available(1, 0);
            int shape = -1;
            if ((n || s) && !w && !e) shape = 0; if ((w || e) && !n && !s) shape = 1;
            if (!_block.StraightOnly)
            {
                if (s && e && !n && !w) shape = 6; if (s && w && !n && !e) shape = 7;
                if (n && w && !s && !e) shape = 8; if (n && e && !s && !w) shape = 9;
            }
            if (shape < 0)
            {
                if (n || s) shape = 0; if (w || e) shape = 1;
                if (!_block.StraightOnly)
                {
                    if (power) { if (s && e) shape = 6; if (w && s) shape = 7; if (e && n) shape = 9; if (n && w) shape = 8; }
                    else { if (n && w) shape = 8; if (e && n) shape = 9; if (w && s) shape = 7; if (s && e) shape = 6; }
                }
            }
            shape = Slope(Math.Max(0, shape)); Ends(shape);
            if (force || Shape(_block.Id, _w.GetMeta(_p.x, _p.y, _p.z)) != shape)
            {
                Write(shape);
                foreach (var end in _ends.ToArray()) { var c = Find(end); if (c == null) continue; c.Clean(); if (c.Accepts(this)) c.Connect(this); }
            }
        }
    }
}
