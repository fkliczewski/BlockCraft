using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;

namespace BlockCraft.World;

/// <summary>Wspólne pomocnicze RS: które encje wykrywają płyty, linka i tory detektorowe (MC EntitySelectors.NOT_SPECTATING).</summary>
public static class RsUtil
{
    public static bool Detectable(Entity e) => !e.Removed && e is not PistonMovingVisual && e is not Player { Spectator: true };
    public static List<Entity> EntitiesIn(World w, AABB box) => w.GetEntitiesInBox(box).Where(Detectable).ToList();
}

/// <summary>Odczyt komparatora z bloku (MC hasComparatorInputOverride / Container.calcRedstoneFromInventory).</summary>
public static class ComparatorRead
{
    /// <summary>Bloki z tile entity IInventory, których komparator nie czyta (MC: magiczna latarnia). Moduły mogą dopisywać.</summary>
    public static readonly bool[] NoInventoryRead = new bool[256];
    static ComparatorRead() { NoInventoryRead[B.Beacon] = true; }

    public static bool HasOverride(World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z);
        if (Block.ById[id] is IComparatorSource) return true;
        return !NoInventoryRead[id] && w.GetTileEntity(x, y, z) is IInventory;
    }
    public static int Read(World w, int x, int y, int z)
    {
        int id = w.GetBlock(x, y, z);
        if (Block.ById[id] is IComparatorSource s) return s.GetComparatorOutput(w, x, y, z);
        return w.GetTileEntity(x, y, z) is IInventory inv ? FromInventory(inv) : 0;
    }
    /// <summary>MC Container.calcRedstoneFromInventory.</summary>
    public static int FromInventory(IInventory inv)
    {
        if (inv == null || inv.Size == 0) return 0;
        int n = 0; float f = 0;
        for (int i = 0; i < inv.Size; i++)
        {
            var s = inv.Get(i);
            if (s == null || s.Count <= 0) continue;
            int max = Math.Min(64, s.Item?.MaxStack ?? 64);
            f += s.Count / (float)Math.Max(1, max);
            n++;
        }
        f /= inv.Size;
        return (int)MathF.Floor(f * 14f) + (n > 0 ? 1 : 0);
    }
}

/// <summary>
/// Komparator jak BlockRedstoneComparator 1.8. meta: bity 0-1 wyjście (jak DiodeBlock), bit 2 = odejmowanie, bit 3 = zasilony.
/// 175 (powered_comparator) to blok zgodności: przy ticku zamienia się w 174 z bitem 3.
/// </summary>
public sealed class ComparatorBlock : DiodeBlock
{
    private readonly bool _legacyOn;
    private readonly int _texOff, _texOn;
    public ComparatorBlock(byte id, string k, string n, bool legacyOn) : base(id, k, n)
    {
        _legacyOn = legacyOn; HasTileEntity = true;
        _texOff = TextureGen.Layer("comparator_off"); _texOn = TextureGen.Layer("comparator_on");
        T("comparator_off");
    }
    public static bool Subtract(int meta) => (meta & 4) != 0;
    public override int DropId(int meta, Rng r) => I.ComparatorItem;
    public override ItemStack PickStack(int meta) => new(I.ComparatorItem, 1);
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
        => Dir.HorizFromYaw(p.Yaw);

    protected override bool IsPowered(int meta) => _legacyOn || (meta & 8) != 0;
    protected override int Delay(int meta) => 2;
    protected override bool CanPowerSide(int id) => Block.Get(id).IsPowerSource;
    protected override int ActiveSignal(IBlockAccess w, int x, int y, int z, int meta)
        => w is World ww && ww.GetTileEntity(x, y, z) is ComparatorTileEntity te ? te.OutputSignal : 0;

    /// <summary>MC calculateInputStrength komparatora: kontener / blok z odczytem za wejściem, także przez blok pełny, i ramka.</summary>
    protected override int InputStrength(World w, int x, int y, int z, int meta)
    {
        int i = base.InputStrength(w, x, y, z, meta);
        int f = InFace(meta);
        int bx = x + Dir.X[f], bz = z + Dir.Z[f];
        if (ComparatorRead.HasOverride(w, bx, y, bz)) return ComparatorRead.Read(w, bx, y, bz);
        if (i < 15 && Redstone.IsNormalCube(w.GetBlock(bx, y, bz)))
        {
            bx += Dir.X[f]; bz += Dir.Z[f];
            if (ComparatorRead.HasOverride(w, bx, y, bz)) i = ComparatorRead.Read(w, bx, y, bz);
            else if (w.GetBlock(bx, y, bz) == B.Air)
            {
                var frames = w.GetEntitiesInBox(new AABB(bx, y, bz, bx + 1, y + 1, bz + 1))
                    .Where(e => e is IComparatorFrame fr && fr.FacingFace == f).ToList();
                if (frames.Count == 1) i = ((IComparatorFrame)frames[0]).ComparatorOutput;
            }
        }
        return i;
    }
    /// <summary>Odczyt kontenera za wejściem (-1 gdy go nie ma) - do wykrywania zmian zawartości (zamiast markDirty w MC).</summary>
    internal int ContainerInput(World w, int x, int y, int z, int meta)
    {
        int f = InFace(meta);
        int bx = x + Dir.X[f], bz = z + Dir.Z[f];
        if (ComparatorRead.HasOverride(w, bx, y, bz)) return ComparatorRead.Read(w, bx, y, bz);
        if (!Redstone.IsNormalCube(w.GetBlock(bx, y, bz))) return -1;
        bx += Dir.X[f]; bz += Dir.Z[f];
        return ComparatorRead.HasOverride(w, bx, y, bz) ? ComparatorRead.Read(w, bx, y, bz) : -1;
    }

    private int CalculateOutput(World w, int x, int y, int z, int meta)
        => Subtract(meta) ? Math.Max(InputStrength(w, x, y, z, meta) - PowerOnSides(w, x, y, z, meta), 0) : InputStrength(w, x, y, z, meta);

    protected override bool ShouldBePowered(World w, int x, int y, int z, int meta)
    {
        int i = InputStrength(w, x, y, z, meta);
        if (i >= 15) return true;
        if (i == 0) return false;
        int j = PowerOnSides(w, x, y, z, meta);
        return j == 0 || i >= j;
    }

    protected override void UpdateState(World w, int x, int y, int z, int meta)
    {
        if (w.IsBlockTickPending(x, y, z, Id)) return;
        int i = CalculateOutput(w, x, y, z, meta);
        int j = w.GetTileEntity(x, y, z) is ComparatorTileEntity te ? te.OutputSignal : 0;
        if (i != j || IsPowered(meta) != ShouldBePowered(w, x, y, z, meta))
            w.ScheduleTickPriority(x, y, z, Id, 2, FacingTowardsDiode(w, x, y, z, meta) ? -1 : 0);
    }

    private void OnStateChange(World w, int x, int y, int z, int meta)
    {
        int i = CalculateOutput(w, x, y, z, meta);
        int j = 0;
        if (w.GetTileEntity(x, y, z) is ComparatorTileEntity te) { j = te.OutputSignal; te.OutputSignal = i; }
        if (j != i || !Subtract(meta))
        {
            bool should = ShouldBePowered(w, x, y, z, meta), on = IsPowered(meta);
            if (on && !should) { meta &= ~8; w.SetMeta(x, y, z, meta, false); }
            else if (!on && should) { meta |= 8; w.SetMeta(x, y, z, meta, false); }
            NotifyOutput(w, x, y, z, meta);
        }
    }

    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        int meta = w.GetMeta(x, y, z);
        if (_legacyOn)
        {
            w.SetBlock(x, y, z, B.Comparator, meta | 8, false);
            if (Block.ById[B.Comparator] is ComparatorBlock c) c.OnStateChange(w, x, y, z, meta | 8);
            return;
        }
        OnStateChange(w, x, y, z, meta);
    }

    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        if (w.IsRemote) return true;
        int meta = w.GetMeta(x, y, z) ^ 4;
        w.PlaySound("random.click", x + 0.5, y + 0.5, z + 0.5, 0.3f, Subtract(meta) ? 0.55f : 0.5f);
        w.SetMeta(x, y, z, meta, false);
        OnStateChange(w, x, y, z, meta);
        return true;
    }

    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        NotifyOutput(w, x, y, z, w.GetMeta(x, y, z));
        if (w.GetTileEntity(x, y, z) is not ComparatorTileEntity) w.SetTileEntity(x, y, z, new ComparatorTileEntity());
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        w.RemoveTileEntity(x, y, z);
        if (!w.IsRemote) NotifyOutput(w, x, y, z, oldMeta);
    }

    /// <summary>Sprawdzenie zmiany zawartości kontenera za wejściem (wywoływane z tile entity co tick).</summary>
    internal void PollContainer(World w, ComparatorTileEntity te)
    {
        int meta = w.GetMeta(te.X, te.Y, te.Z);
        int v = ContainerInput(w, te.X, te.Y, te.Z, meta);
        if (v == te.LastContainer) return;
        te.LastContainer = v;
        UpdateState(w, te.X, te.Y, te.Z, meta);
    }

    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        bool on = IsPowered(meta);
        AddSlab(boxes, on ? _texOn : _texOff, meta);
        AddTorch(boxes, meta, -5, 3, on);
        AddTorch(boxes, meta, -5, -3, on);
        AddTorch(boxes, meta, 5, 0, Subtract(meta), Subtract(meta) ? 5 : 4);
    }
}

/// <summary>Tile entity komparatora (MC TileEntityComparator): zapamiętany sygnał wyjściowy.</summary>
public sealed class ComparatorTileEntity : TileEntity
{
    public int OutputSignal;
    internal int LastContainer = int.MinValue;
    public override int TypeId => 10;
    public override void Tick(World w)
    {
        if (w.IsRemote) return;
        if (Block.ById[w.GetBlock(X, Y, Z)] is ComparatorBlock c) c.PollContainer(w, this);
    }
    public override void Save(BinaryWriter bw) => bw.Write(OutputSignal);
    public override void Load(BinaryReader br) => OutputSignal = br.ReadInt32();
}

/// <summary>Czujnik światła dziennego jak BlockDaylightDetector 1.8 (meta = moc 0-15, PPM odwraca).</summary>
public sealed class DaylightSensorBlock : Block
{
    public readonly bool Inverted;
    public DaylightSensorBlock(byte id, string k, string n, bool inverted) : base(id, k, n)
    {
        Inverted = inverted;
        T(inverted ? "daylight_detector_inverted_top" : "daylight_detector_top", "daylight_detector_side", "daylight_detector_side");
        Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0.2f; BlastResistance = 0.2f; Sound = SoundType.Wood; IsPowerSource = true; HasTileEntity = true; Flammable = true;
    }
    public override int DropId(int meta, Rng r) => B.DaylightSensor;
    public override ItemStack PickStack(int meta) => new(B.DaylightSensor, 1);
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(new AABB(0, 0, 0, 1, 0.375, 1));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes) => boxes.Add(ModelBox.Px(0, 0, 0, 16, 6, 16, Tex));
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => meta & 15;

    /// <summary>MC updatePower.</summary>
    public void UpdatePower(World w, int x, int y, int z)
    {
        int i = w.GetSkyLight(x, y, z) - w.SkyLightSubtracted;
        float f = w.CelestialAngle(1f) * MathF.PI * 2f;
        float f1 = f < MathF.PI ? 0f : MathF.PI * 2f;
        f += (f1 - f) * 0.2f;
        i = (int)MathF.Round(i * MathF.Cos(f), MidpointRounding.AwayFromZero);
        i = Math.Clamp(i, 0, 15);
        if (Inverted) i = 15 - i;
        if ((w.GetMeta(x, y, z) & 15) != i) w.SetMeta(x, y, z, i, true);
    }
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        if (w.IsRemote) return true;
        int meta = w.GetMeta(x, y, z);
        w.SetBlock(x, y, z, Inverted ? B.DaylightSensor : B.DaylightSensorInverted, meta, false);
        ((DaylightSensorBlock)Block.ById[w.GetBlock(x, y, z)]).UpdatePower(w, x, y, z);
        return true;
    }
    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.GetTileEntity(x, y, z) is not DaylightSensorTileEntity) w.SetTileEntity(x, y, z, new DaylightSensorTileEntity());
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta) => w.RemoveTileEntity(x, y, z);
}

/// <summary>MC TileEntityDaylightDetector: co 20 ticków przelicza moc.</summary>
public sealed class DaylightSensorTileEntity : TileEntity
{
    public override int TypeId => 12;
    public override void Tick(World w)
    {
        if (w.IsRemote || w.TotalTime % 20L != 0) return;
        if (Block.ById[w.GetBlock(X, Y, Z)] is DaylightSensorBlock d) d.UpdatePower(w, X, Y, Z);
    }
    public override void Save(BinaryWriter bw) { }
    public override void Load(BinaryReader br) { }
}

/// <summary>
/// Płyty naciskowe jak BlockBasePressurePlate 1.8: kamienna (moby), drewniana (wszystko), ważone lekka (15) i ciężka (150).
/// meta: zwykłe bit 0 = wciśnięta, ważone = moc 0-15.
/// </summary>
public sealed class PressurePlateBlock : Block
{
    public enum Sense { Everything, Mobs, Light, Heavy }
    public readonly Sense Sensitivity;
    public PressurePlateBlock(byte id, string k, string n, string tex, Sense s) : base(id, k, n)
    {
        Sensitivity = s; T(tex);
        Opaque = false; Solid = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0.5f; BlastResistance = 0.5f; IsPowerSource = true; NeedsSupport = true;
        Sound = s switch { Sense.Everything => SoundType.Wood, Sense.Mobs => SoundType.Stone, _ => SoundType.Metal };
        if (s != Sense.Everything) { Tool = ToolType.Pickaxe; HarvestLevel = 0; }
        if (s == Sense.Everything) Flammable = true;
    }
    private bool Weighted => Sensitivity >= Sense.Light;
    private int TickRate => Weighted ? 10 : 20;
    public int Strength(int meta) => Weighted ? meta & 15 : (meta & 1) != 0 ? 15 : 0;
    private int MetaFor(int strength) => Weighted ? strength : strength > 0 ? 1 : 0;

    public override bool CanPlaceAt(World w, int x, int y, int z) => TorchBlock.SolidTop(w, x, y - 1, z) || Block.Get(w.GetBlock(x, y - 1, z)) is FenceBlock;
    private AABB Bounds(int meta) => Strength(meta) > 0 ? new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 1 / 32.0, 15 / 16.0) : new AABB(1 / 16.0, 0, 1 / 16.0, 15 / 16.0, 1 / 16.0, 15 / 16.0);
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Bounds(meta));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var b = Bounds(meta);
        boxes.Add(new ModelBox((float)b.MinX, 0, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, Tex));
    }

    public override void OnEntityCollide(World w, int x, int y, int z, Entity e)
    {
        if (w.IsRemote) return;
        int s = Strength(w.GetMeta(x, y, z));
        if (s == 0) UpdateState(w, x, y, z, s);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (w.IsRemote) return;
        int s = Strength(w.GetMeta(x, y, z));
        if (s > 0) UpdateState(w, x, y, z, s);
    }
    private void UpdateState(World w, int x, int y, int z, int old)
    {
        int j = Compute(w, x, y, z);
        bool was = old > 0, now = j > 0;
        if (old != j)
        {
            w.SetMeta(x, y, z, MetaFor(j), false);
            NotifyOutputs(w, x, y, z);
        }
        if (!now && was) w.PlaySound("random.click", x + 0.5, y + 0.1, z + 0.5, 0.3f, 0.5f);
        else if (now && !was) w.PlaySound("random.click", x + 0.5, y + 0.1, z + 0.5, 0.3f, 0.6f);
        if (now) w.ScheduleTick(x, y, z, Id, TickRate);
    }
    /// <summary>MC computeRedstoneStrength.</summary>
    private int Compute(World w, int x, int y, int z)
    {
        var list = RsUtil.EntitiesIn(w, new AABB(x + 0.125, y, z + 0.125, x + 0.875, y + 0.25, z + 0.875));
        switch (Sensitivity)
        {
            case Sense.Everything: return list.Count > 0 ? 15 : 0;
            case Sense.Mobs: return list.Any(e => e is LivingEntity) ? 15 : 0;
            default:
                int max = Sensitivity == Sense.Light ? 15 : 150;
                int n = Math.Min(list.Count, max);
                return n > 0 ? (int)MathF.Ceiling(n / (float)max * 15f) : 0;
        }
    }
    private void NotifyOutputs(World w, int x, int y, int z)
    {
        Redstone.NotifyNeighbors(w, x, y, z, Id);
        Redstone.NotifyNeighbors(w, x, y - 1, z, Id);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (!w.IsRemote && Strength(oldMeta) > 0) NotifyOutputs(w, x, y, z);
    }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => Strength(meta);
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side) => side == Dir.Down ? Strength(meta) : 0;
}

/// <summary>
/// Hak na linkę jak BlockTripWireHook 1.8. meta: bity 0-1 kierunek poziomy haka (od ściany), bit 2 = podłączony, bit 3 = zasilony.
/// </summary>
public sealed class TripwireHookBlock : Block
{
    public TripwireHookBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("trip_wire_source");
        Opaque = false; Solid = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0; BlastResistance = 0; IsPowerSource = true; Sound = SoundType.Wood;
    }
    public static int Facing(int meta) => Dir.HorizToFace(meta & 3);
    private static bool Wall(World w, int x, int y, int z, int facing)
        => Redstone.IsNormalCube(w.GetBlock(x - Dir.X[facing], y, z - Dir.Z[facing]));
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        foreach (int f in Redstone.Horizontals) if (Wall(w, x, y, z, f)) return true;
        return false;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        if (face >= 2 && Wall(w, x, y, z, face)) return Dir.FaceToHoriz(face);
        foreach (int f in Redstone.Horizontals) if (Wall(w, x, y, z, f)) return Dir.FaceToHoriz(f);
        return -1;
    }
    public override void OnPlaced(World w, int x, int y, int z, Player p) => Update(w, x, y, z, w.GetMeta(x, y, z), false, false, -1, -1);
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (w.IsRemote || nid == Id) return;
        if (!Wall(w, x, y, z, Facing(w.GetMeta(x, y, z)))) w.BreakBlock(x, y, z, true);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r) => Update(w, x, y, z, w.GetMeta(x, y, z), false, true, -1, -1);

    /// <summary>MC func_176260_a: przelicza linkę między hakami. wireIdx/wireMeta: zmieniany odcinek linki (jego nowy stan).</summary>
    public void Update(World w, int x, int y, int z, int meta, bool removing, bool notify, int wireIdx, int wireMeta)
    {
        int facing = Facing(meta);
        bool wasAttached = (meta & 4) != 0, wasPowered = (meta & 8) != 0;
        bool suspended = !TorchBlock.SolidTop(w, x, y - 1, z);
        bool attached = !removing, powered = false;
        int dist = 0;
        var wires = new int[42];
        for (int j = 1; j < 42; j++)
        {
            int bx = x + Dir.X[facing] * j, bz = z + Dir.Z[facing] * j;
            int id = w.GetBlock(bx, y, bz);
            if (id == Id)
            {
                if (Facing(w.GetMeta(bx, y, bz)) == Dir.Opp[facing]) dist = j;
                break;
            }
            if (id != B.Tripwire && j != wireIdx) { wires[j] = -1; attached = false; }
            else
            {
                int s = j == wireIdx && wireMeta >= 0 ? wireMeta : w.GetMeta(bx, y, bz);
                bool armed = (s & 8) == 0, wPowered = (s & 1) != 0, wSusp = (s & 2) != 0;
                attached &= wSusp == suspended;
                powered |= armed && wPowered;
                wires[j] = s;
                if (j == wireIdx)
                {
                    w.ScheduleTick(x, y, z, Id, 10);
                    attached &= armed;
                }
            }
        }
        attached &= dist > 1;
        powered &= attached;
        int st = (attached ? 4 : 0) | (powered ? 8 : 0);
        if (dist > 0)
        {
            int ox = x + Dir.X[facing] * dist, oz = z + Dir.Z[facing] * dist;
            int opp = Dir.Opp[facing];
            w.SetMeta(ox, y, oz, st | Dir.FaceToHoriz(opp), true);
            NotifyOutputs(w, ox, y, oz, opp);
            Sounds(w, ox, y, oz, attached, powered, wasAttached, wasPowered);
        }
        Sounds(w, x, y, z, attached, powered, wasAttached, wasPowered);
        if (!removing)
        {
            w.SetMeta(x, y, z, st | Dir.FaceToHoriz(facing), true);
            if (notify) NotifyOutputs(w, x, y, z, facing);
        }
        if (wasAttached != attached)
            for (int k = 1; k < dist; k++)
            {
                int bx = x + Dir.X[facing] * k, bz = z + Dir.Z[facing] * k;
                if (wires[k] >= 0 && w.GetBlock(bx, y, bz) != B.Air)
                    w.SetBlock(bx, y, bz, B.Tripwire, attached ? wires[k] | 4 : wires[k] & ~4, true);
            }
    }
    private static void Sounds(World w, int x, int y, int z, bool attached, bool powered, bool wasAttached, bool wasPowered)
    {
        double cx = x + 0.5, cy = y + 0.1, cz = z + 0.5;
        if (powered && !wasPowered) w.PlaySound("random.click", cx, cy, cz, 0.4f, 0.6f);
        else if (!powered && wasPowered) w.PlaySound("random.click", cx, cy, cz, 0.4f, 0.5f);
        else if (attached && !wasAttached) w.PlaySound("random.click", cx, cy, cz, 0.4f, 0.7f);
        else if (!attached && wasAttached) w.PlaySound("random.bowhit", cx, cy, cz, 0.4f, 1.2f / (w.Rand.NextFloat() * 0.2f + 0.9f));
    }
    private void NotifyOutputs(World w, int x, int y, int z, int facing)
    {
        Redstone.NotifyNeighbors(w, x, y, z, Id);
        Redstone.NotifyNeighbors(w, x - Dir.X[facing], y, z - Dir.Z[facing], Id);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (w.IsRemote) return;
        bool attached = (oldMeta & 4) != 0, powered = (oldMeta & 8) != 0;
        if (attached || powered) Update(w, x, y, z, oldMeta, true, false, -1, -1);
        if (powered) NotifyOutputs(w, x, y, z, Facing(oldMeta));
    }
    public override int GetWeakPower(IBlockAccess w, int x, int y, int z, int meta, int side) => (meta & 8) != 0 ? 15 : 0;
    public override int GetStrongPower(IBlockAccess w, int x, int y, int z, int meta, int side)
        => (meta & 8) != 0 && side == Dir.Opp[Facing(meta)] ? 15 : 0;

    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        // MC: 0.1875 grubości, 0.2-0.8 w pionie, 0.3125-0.6875 w poprzek
        const double t = 0.1875;
        list.Add((meta & 3) switch
        {
            0 => new AABB(0.3125, 0.2, 0, 0.6875, 0.8, t),
            1 => new AABB(1 - t, 0.2, 0.3125, 1, 0.8, 0.6875),
            2 => new AABB(0.3125, 0.2, 1 - t, 0.6875, 0.8, 1),
            _ => new AABB(0, 0.2, 0.3125, t, 0.8, 0.6875),
        });
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        int f = Facing(meta);
        bool attached = (meta & 4) != 0;
        int planks = TextureGen.Layer("planks_oak"), iron = TextureGen.Layer("iron_block");
        float drop = attached ? 1.5f : 0;
        // płytka przy ścianie, haczyk wystający w kierunku f i obręcz
        boxes.Add(Local(f, 6, 1, 0, 10, 9, 2, ModelBox.All(planks)));
        boxes.Add(Local(f, 7.5f, 6 - drop, 2, 8.5f, 7 - drop, 7, ModelBox.All(iron)));
        boxes.Add(Local(f, 6.5f, 2.5f - drop, 6, 9.5f, 6.5f - drop, 7, ModelBox.All(Tex[0])));
    }
    /// <summary>Box w pikselach w układzie haka: z rośnie od ściany w stronę f, x w poprzek.</summary>
    private static ModelBox Local(int f, float x0, float y0, float z0, float x1, float y1, float z1, int[] tex) => f switch
    {
        Dir.South => ModelBox.Px(x0, y0, z0, x1, y1, z1, tex),
        Dir.North => ModelBox.Px(16 - x1, y0, 16 - z1, 16 - x0, y1, 16 - z0, tex),
        Dir.East => ModelBox.Px(z0, y0, 16 - x1, z1, y1, 16 - x0, tex),
        _ => ModelBox.Px(16 - z1, y0, x0, 16 - z0, y1, x1, tex),
    };
}

/// <summary>Linka jak BlockTripWire 1.8. meta: bit 0 zasilona, bit 1 zawieszona, bit 2 podłączona, bit 3 rozbrojona.</summary>
public sealed class TripwireBlock : Block
{
    public TripwireBlock(byte id, string k, string n) : base(id, k, n)
    {
        T("trip_wire");
        Opaque = false; Solid = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        Hardness = 0; BlastResistance = 0; InCreative = false;
    }
    public override int DropId(int meta, Rng r) => I.String;
    public override ItemStack PickStack(int meta) => new(I.String, 1);
    private static AABB Bounds(int meta)
    {
        bool attached = (meta & 4) != 0, grounded = (meta & 2) == 0;
        if (!grounded) return new AABB(0, 0, 0, 1, 0.09375, 1);
        if (!attached) return new AABB(0, 0, 0, 1, 0.5, 1);
        return new AABB(0, 0.0625, 0, 1, 0.15625, 1);
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Bounds(meta));

    public override void OnAdded(World w, int x, int y, int z)
    {
        if (w.IsRemote) return;
        int meta = w.GetMeta(x, y, z);
        meta = TorchBlock.SolidTop(w, x, y - 1, z) ? meta & ~2 : meta | 2;
        w.SetMeta(x, y, z, meta, true);
        NotifyHook(w, x, y, z, meta);
    }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        if (!w.IsRemote) NotifyHook(w, x, y, z, oldMeta | 1);
    }
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    {
        if (!w.IsRemote && p.Inventory.Selected?.Id == I.Shears) w.SetMeta(x, y, z, meta | 8, false);
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (w.IsRemote) return;
        bool susp = (w.GetMeta(x, y, z) & 2) != 0;
        bool now = !TorchBlock.SolidTop(w, x, y - 1, z);
        if (susp != now) w.BreakBlock(x, y, z, true);
    }
    public override void OnEntityCollide(World w, int x, int y, int z, Entity e)
    {
        if (!w.IsRemote && (w.GetMeta(x, y, z) & 1) == 0) UpdateState(w, x, y, z);
    }
    public override void OnScheduledTick(World w, int x, int y, int z, Rng r)
    {
        if (!w.IsRemote && (w.GetMeta(x, y, z) & 1) != 0) UpdateState(w, x, y, z);
    }
    private void UpdateState(World w, int x, int y, int z)
    {
        int meta = w.GetMeta(x, y, z);
        bool was = (meta & 1) != 0;
        bool now = RsUtil.EntitiesIn(w, Bounds(meta).Offset(x, y, z)).Count > 0;
        if (now != was)
        {
            meta = now ? meta | 1 : meta & ~1;
            w.SetMeta(x, y, z, meta, true);
            NotifyHook(w, x, y, z, meta);
        }
        if (now) w.ScheduleTick(x, y, z, Id, 10);
    }
    /// <summary>MC notifyHook: szuka haka wzdłuż osi S i W (hak przelicza oba końce).</summary>
    private static void NotifyHook(World w, int x, int y, int z, int meta)
    {
        foreach (int f in new[] { Dir.South, Dir.West })
            for (int i = 1; i < 42; i++)
            {
                int bx = x + Dir.X[f] * i, bz = z + Dir.Z[f] * i;
                int id = w.GetBlock(bx, y, bz);
                if (id == B.TripwireHook)
                {
                    int hm = w.GetMeta(bx, y, bz);
                    if (TripwireHookBlock.Facing(hm) == Dir.Opp[f] && Block.ById[id] is TripwireHookBlock h) h.Update(w, bx, y, bz, hm, false, true, i, meta);
                    break;
                }
                if (id != B.Tripwire) break;
            }
    }
    private static bool ConnectsTo(IBlockAccess w, int x, int y, int z, int f)
    {
        int nx = x + Dir.X[f], nz = z + Dir.Z[f];
        int id = w.GetBlock(nx, y, nz);
        if (id == B.TripwireHook) return TripwireHookBlock.Facing(w.GetMeta(nx, y, nz)) == Dir.Opp[f];
        return id == B.Tripwire;
    }
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        bool n = ConnectsTo(w, x, y, z, Dir.North), s = ConnectsTo(w, x, y, z, Dir.South);
        bool we = ConnectsTo(w, x, y, z, Dir.West), e = ConnectsTo(w, x, y, z, Dir.East);
        if (!n && !s && !we && !e) { n = s = true; }
        float h = (meta & 4) != 0 ? 1.5f : (meta & 2) != 0 ? 0.5f : 1.5f;
        var t = ModelBox.All(Tex[0]);
        void Seg(float x0, float z0, float x1, float z1)
        {
            var b = ModelBox.Px(x0, h, z0, x1, h + 0.25f, z1, t); b.NoCull = true; boxes.Add(b);
        }
        if (n) Seg(7.75f, 0, 8.25f, 8);
        if (s) Seg(7.75f, 8, 8.25f, 16);
        if (we) Seg(0, 7.75f, 8, 8.25f);
        if (e) Seg(8, 7.75f, 16, 8.25f);
    }
}

/// <summary>Ruda czerwonego kamienia jak BlockRedstoneOre 1.8: świeci po dotknięciu, gaśnie losowym tickiem.</summary>
public sealed class RedstoneOreBlock : OreBlock
{
    private readonly bool _lit;
    public RedstoneOreBlock(byte id, string k, string n, bool lit) : base(id, k, n, I.Redstone, 4, 5)
    {
        _lit = lit; T("redstone_ore"); H(3f); Tool_(ToolType.Pickaxe, 2);
        if (lit) { LightEmit = 9; RandomTicks = true; InCreative = false; }
    }
    public override ItemStack PickStack(int meta) => new(B.RedstoneOre, 1);
    private void Activate(World w, int x, int y, int z)
    {
        Particles(w, x, y, z);
        if (Id == B.RedstoneOre) w.SetBlock(x, y, z, B.RedstoneOreLit, 0);
    }
    public override void OnClicked(World w, int x, int y, int z, Player p) => Activate(w, x, y, z);
    public override void OnEntityWalk(World w, int x, int y, int z, Entity e) => Activate(w, x, y, z);
    public override bool OnActivated(World w, int x, int y, int z, Player p) { Activate(w, x, y, z); return false; }
    public override void OnRandomTick(World w, int x, int y, int z, Rng r)
    {
        if (_lit) w.SetBlock(x, y, z, B.RedstoneOre, 0);
    }
    public override void OnDestroyedByPlayer(World w, int x, int y, int z, int meta, Player p)
    {
        if (!p.Creative && !Silked(p)) w.SpawnXp(x + 0.5, y + 0.5, z + 0.5, 1 + w.Rand.Next(5));
    }
    private static void Particles(World w, int x, int y, int z)
    {
        var r = w.Rand;
        for (int f = 0; f < 6; f++)
        {
            int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
            if (Block.Get(w.GetBlock(nx, ny, nz)).Opaque) continue;
            double px = Dir.X[f] != 0 ? 0.5 + 0.5625 * Dir.X[f] : r.NextFloat();
            double py = Dir.Y[f] != 0 ? 0.5 + 0.5625 * Dir.Y[f] : r.NextFloat();
            double pz = Dir.Z[f] != 0 ? 0.5 + 0.5625 * Dir.Z[f] : r.NextFloat();
            w.SpawnParticles(ParticleKind.RedDust, x + px, y + py, z + pz, 1);
        }
    }
}

/// <summary>
/// Klapa jak BlockTrapDoor 1.8 (drewniana i żelazna). meta: bity 0-1 kierunek (0=N,1=S,2=W,3=E), bit 2 = otwarta, bit 3 = górna połowa.
/// Stawiana tylko na boku bloku-podpory; żelazna otwiera się wyłącznie sygnałem.
/// </summary>
public sealed class TrapdoorBlock : Block
{
    public readonly bool Iron;
    public TrapdoorBlock(byte id, string k, string n, bool iron) : base(id, k, n)
    {
        Iron = iron; T(iron ? "iron_trapdoor" : "trapdoor");
        Opaque = false; FullCube = false; Render = RenderType.Model; Layer = RenderLayer.Cutout; LightOpacity = 0;
        if (iron) { Hardness = 5f; BlastResistance = 5f; Tool = ToolType.Pickaxe; HarvestLevel = 0; Sound = SoundType.Metal; }
        else { Hardness = 3f; BlastResistance = 3f; Tool = ToolType.Axe; Sound = SoundType.Wood; Flammable = true; }
    }
    public static int Facing(int meta) => (meta & 3) switch { 0 => Dir.North, 1 => Dir.South, 2 => Dir.West, _ => Dir.East };
    private static int MetaOf(int face) => face switch { Dir.North => 0, Dir.South => 1, Dir.West => 2, _ => 3 };
    /// <summary>MC isValidSupportBlock.</summary>
    private static bool ValidSupport(int id)
    {
        var b = Block.Get(id);
        return b.Opaque && b.FullCube || id == B.Glowstone || b is SlabBlock || b is StairsBlock;
    }
    private static bool Supported(World w, int x, int y, int z, int facing)
        => ValidSupport(w.GetBlock(x - Dir.X[facing], y, z - Dir.Z[facing]));
    public override bool CanPlaceAt(World w, int x, int y, int z)
    {
        foreach (int f in Redstone.Horizontals) if (Supported(w, x, y, z, f)) return true;
        return false;
    }
    public override int GetPlacementMeta(World w, int x, int y, int z, int face, double hx, double hy, double hz, Player p, int itemMeta)
    {
        if (face < 2 || !Supported(w, x, y, z, face)) return -1;
        return MetaOf(face) | (hy > 0.5 ? 8 : 0);
    }
    public static AABB Box(int meta)
    {
        const double t = 0.1875;
        if ((meta & 4) != 0)
            return Facing(meta) switch
            {
                Dir.North => new AABB(0, 0, 1 - t, 1, 1, 1),
                Dir.South => new AABB(0, 0, 0, 1, 1, t),
                Dir.West => new AABB(1 - t, 0, 0, 1, 1, 1),
                _ => new AABB(0, 0, 0, t, 1, 1),
            };
        return (meta & 8) != 0 ? new AABB(0, 1 - t, 0, 1, 1, 1) : new AABB(0, 0, 0, 1, t, 1);
    }
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) => list.Add(Box(meta));
    public override void GetModel(IBlockAccess w, int x, int y, int z, int meta, List<ModelBox> boxes)
    {
        var b = Box(meta);
        boxes.Add(new ModelBox((float)b.MinX, (float)b.MinY, (float)b.MinZ, (float)b.MaxX, (float)b.MaxY, (float)b.MaxZ, Tex));
    }
    private static void DoorSound(World w, int x, int y, int z, bool open)
        => w.PlaySound(open ? "random.door_open" : "random.door_close", x + 0.5, y + 0.5, z + 0.5, 1f, w.Rand.NextFloat() * 0.1f + 0.9f);
    public override bool OnActivated(World w, int x, int y, int z, Player p)
    {
        if (Iron) return true;
        if (w.IsRemote) return true;
        int m = w.GetMeta(x, y, z) ^ 4;
        w.SetMeta(x, y, z, m, false);
        DoorSound(w, x, y, z, (m & 4) != 0);
        return true;
    }
    public override void OnNeighborChanged(World w, int x, int y, int z, int nid)
    {
        if (w.IsRemote) return;
        int m = w.GetMeta(x, y, z);
        if (!Supported(w, x, y, z, Facing(m))) { w.BreakBlock(x, y, z, true); return; }
        bool powered = Redstone.IsBlockPowered(w, x, y, z);
        if (powered || Block.Get(nid).IsPowerSource)
        {
            bool open = (m & 4) != 0;
            if (open != powered)
            {
                w.SetMeta(x, y, z, powered ? m | 4 : m & ~4, false);
                DoorSound(w, x, y, z, powered);
            }
        }
    }
}
