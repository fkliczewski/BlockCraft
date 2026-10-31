using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

/// <summary>Blok odbierający zdarzenia bloku (MC addBlockEvent / onBlockEventReceived): tłoki, blok nut.</summary>
public interface IBlockEventReceiver
{
    /// <summary>Zwraca true, jeśli zdarzenie się wykonało.</summary>
    bool OnBlockEvent(World w, int x, int y, int z, int eventId, int param);
}

/// <summary>
/// Blok z własnym odczytem komparatora (MC hasComparatorInputOverride / getComparatorInputOverride).
/// Implementują: ciasto, kocioł, szafa grająca, rama portalu Endu itp. Bloki z tile entity typu IInventory nie muszą -
/// komparator liczy wtedy zapełnienie jak Container.calcRedstoneFromInventory.
/// </summary>
public interface IComparatorSource
{
    int GetComparatorOutput(World w, int x, int y, int z);
}

/// <summary>Encja czytana przez komparator przez blok (ramka na przedmiot): wyjście 0-15 i kierunek, w który patrzy (face id).</summary>
public interface IComparatorFrame
{
    int ComparatorOutput { get; }
    /// <summary>Kierunek (Dir face id), w który ramka jest zwrócona - od ściany, na której wisi.</summary>
    int FacingFace { get; }
}

/// <summary>Inventory z dostępem zależnym od strony (MC ISidedInventory). face = strona bloku (Dir), z której wchodzi/wychodzi przedmiot.</summary>
public interface ISidedInventory : IInventory
{
    int[] SlotsForFace(int face);
    bool CanInsert(int slot, ItemStack s, int face);
    bool CanExtract(int slot, ItemStack s, int face);
}

public sealed partial class World
{
    // ---------- zdarzenia bloków (MC WorldServer.addBlockEvent / sendQueuedBlockEvents) ----------
    private readonly List<(int x, int y, int z, byte id, int ev, int param)>[] _blockEvents = { new(), new() };
    private int _blockEventIdx;

    /// <summary>Dodaj zdarzenie bloku (wykonane po tickach zaplanowanych, przed tile entity). Duplikaty są pomijane.</summary>
    public void AddBlockEvent(int x, int y, int z, byte id, int eventId, int param)
    {
        if (IsRemote) return;
        var q = _blockEvents[_blockEventIdx];
        var d = (x, y, z, id, eventId, param);
        foreach (var o in q) if (o == d) return;
        q.Add(d);
    }

    private void ProcessBlockEvents()
    {
        int guard = 0;
        while (_blockEvents[_blockEventIdx].Count > 0 && guard++ < 1000)
        {
            int i = _blockEventIdx;
            _blockEventIdx ^= 1;
            foreach (var d in _blockEvents[i])
            {
                if (GetBlock(d.x, d.y, d.z) != d.id) continue;
                if (Block.ById[d.id] is IBlockEventReceiver r) r.OnBlockEvent(this, d.x, d.y, d.z, d.ev, d.param);
            }
            _blockEvents[i].Clear();
        }
    }
}

/// <summary>Zasady przesuwania bloków tłokiem (MC getMobilityFlag i BlockPistonBase.canPush).</summary>
public static class PistonRules
{
    /// <summary>Nadpisanie mobilności (0 przesuwalny, 1 niszczony, 2 nieruchomy, 255 = automatycznie). Moduły mogą ustawiać.</summary>
    public static readonly byte[] MobilityOverride = Enumerable.Repeat((byte)255, 256).ToArray();

    static PistonRules()
    {
        foreach (int id in new[] { B.OakLeaves, B.BirchLeaves, B.SpruceLeaves, B.JungleLeaves, B.AcaciaLeaves, B.DarkOakLeaves, B.Cactus,
                     B.Pumpkin, B.JackOLantern, B.Melon, B.Cake, B.DragonEgg, B.Carpet, B.SnowLayer, B.Cobweb, B.OakDoor, B.IronDoor,
                     B.SpruceDoor, B.BirchDoor, B.JungleDoor, B.AcaciaDoor, B.DarkOakDoor, B.Bed, B.Ladder, B.Vine, B.LilyPad,
                     B.StonePressurePlate, B.WoodPressurePlate, B.LightWeightedPlate, B.HeavyWeightedPlate, B.Fire,
                     B.RedstoneWire, B.RedstoneTorch, B.RedstoneTorchOff, B.Lever, B.StoneButton, B.WoodButton, B.Repeater, B.RepeaterOn,
                     B.Comparator, B.ComparatorOn, B.TripwireHook, B.Tripwire, B.Rail, B.PoweredRail, B.DetectorRail, B.ActivatorRail })
            MobilityOverride[id] = 1;
        foreach (int id in new[] { B.PistonHead, B.PistonExtension, B.Portal, B.EndPortal, B.Barrier, B.Obsidian })
            MobilityOverride[id] = 2;
    }

    public static int Mobility(int id)
    {
        if (MobilityOverride[id] != 255) return MobilityOverride[id];
        var b = Block.Get(id);
        if (b.Hardness < 0) return 2;
        if (b.IsLiquid || b.Replaceable || b.NeedsSupport || !b.Solid || b.Render is RenderType.Cross or RenderType.Crops) return 1;
        return 0;
    }

    /// <summary>MC BlockPistonBase.canPush.</summary>
    public static bool CanPush(World w, int id, int x, int y, int z, int dir, bool allowDestroy)
    {
        if (id == B.Obsidian) return false;
        if (y < 0 || (dir == Dir.Down && y == 0)) return false;
        if (y > Chunk.Height - 1 || (dir == Dir.Up && y == Chunk.Height - 1)) return false;
        if (id != B.Piston && id != B.StickyPiston)
        {
            var b = Block.Get(id);
            if (b.Hardness < 0) return false;
            int m = Mobility(id);
            if (m == 2) return false;
            if (m == 1) return allowDestroy;
            return !b.HasTileEntity;
        }
        return (w.GetMeta(x, y, z) & 8) == 0;
    }
}

/// <summary>MC BlockPistonStructureHelper 1.8 (limit 12 bloków, ciągnięcie przez blok szlamu).</summary>
public sealed class PistonStructure
{
    private readonly World _w;
    private readonly (int x, int y, int z) _piston, _blockToMove;
    private readonly int _moveDir;
    public readonly List<(int x, int y, int z)> ToMove = new();
    public readonly List<(int x, int y, int z)> ToDestroy = new();

    public PistonStructure(World w, int x, int y, int z, int pistonFacing, bool extending)
    {
        _w = w; _piston = (x, y, z);
        if (extending) { _moveDir = pistonFacing; _blockToMove = (x + Dir.X[pistonFacing], y + Dir.Y[pistonFacing], z + Dir.Z[pistonFacing]); }
        else { _moveDir = Dir.Opp[pistonFacing]; _blockToMove = (x + Dir.X[pistonFacing] * 2, y + Dir.Y[pistonFacing] * 2, z + Dir.Z[pistonFacing] * 2); }
    }

    private int Id((int x, int y, int z) p) => _w.GetBlock(p.x, p.y, p.z);
    private static (int, int, int) Off((int x, int y, int z) p, int dir, int n) => (p.x + Dir.X[dir] * n, p.y + Dir.Y[dir] * n, p.z + Dir.Z[dir] * n);
    private bool CanPush((int x, int y, int z) p, bool allowDestroy) => PistonRules.CanPush(_w, Id(p), p.x, p.y, p.z, _moveDir, allowDestroy);

    public bool CanMove()
    {
        ToMove.Clear(); ToDestroy.Clear();
        int id = Id(_blockToMove);
        if (!CanPush(_blockToMove, false))
        {
            if (PistonRules.Mobility(id) != 1) return false;
            ToDestroy.Add(_blockToMove);
            return true;
        }
        if (!AddBlockLine(_blockToMove)) return false;
        for (int i = 0; i < ToMove.Count; i++)
            if (Id(ToMove[i]) == B.SlimeBlock && !AddBranchingBlocks(ToMove[i])) return false;
        return true;
    }

    private bool AddBlockLine((int x, int y, int z) origin)
    {
        int id = Id(origin);
        if (id == 0) return true;
        if (!CanPush(origin, false)) return true;
        if (origin == _piston) return true;
        if (ToMove.Contains(origin)) return true;
        int i = 1;
        if (i + ToMove.Count > 12) return false;
        while (id == B.SlimeBlock)
        {
            var bp = Off(origin, Dir.Opp[_moveDir], i);
            id = Id(bp);
            if (id == 0 || !CanPush(bp, false) || bp == _piston) break;
            i++;
            if (i + ToMove.Count > 12) return false;
        }
        int i1 = 0;
        for (int j = i - 1; j >= 0; j--) { ToMove.Add(Off(origin, Dir.Opp[_moveDir], j)); i1++; }
        int j1 = 1;
        while (true)
        {
            var bp1 = Off(origin, _moveDir, j1);
            int k = ToMove.IndexOf(bp1);
            if (k > -1)
            {
                Reorder(i1, k);
                for (int l = 0; l <= k + i1; l++)
                {
                    var bp2 = ToMove[l];
                    if (Id(bp2) == B.SlimeBlock && !AddBranchingBlocks(bp2)) return false;
                }
                return true;
            }
            id = Id(bp1);
            if (id == 0) return true;
            if (!PistonRules.CanPush(_w, id, bp1.Item1, bp1.Item2, bp1.Item3, _moveDir, true) || bp1 == _piston) return false;
            if (PistonRules.Mobility(id) == 1) { ToDestroy.Add(bp1); return true; }
            if (ToMove.Count >= 12) return false;
            ToMove.Add(bp1); i1++; j1++;
        }
    }

    private void Reorder(int a, int b)
    {
        var l0 = ToMove.GetRange(0, b);
        var l1 = ToMove.GetRange(ToMove.Count - a, a);
        var l2 = ToMove.GetRange(b, ToMove.Count - a - b);
        ToMove.Clear(); ToMove.AddRange(l0); ToMove.AddRange(l1); ToMove.AddRange(l2);
    }

    private bool AddBranchingBlocks((int x, int y, int z) p)
    {
        for (int f = 0; f < 6; f++)
            if (f >> 1 != _moveDir >> 1 && !AddBlockLine(Off(p, f, 1))) return false;
        return true;
    }
}

/// <summary>Ruchomy blok tłoka (MC TileEntityPiston): postęp 0 -> 0.5 -> 1, potem blok wraca do świata.</summary>
public sealed class PistonTileEntity : TileEntity
{
    public override int TypeId => 11;
    public int BlockId, BlockMeta, Facing;
    public bool Extending, HeadRendered;
    public float Progress, LastProgress;
    private bool _done;

    public static PistonTileEntity Create(World w, int x, int y, int z, int blockId, int meta, int facing, bool extending, bool head)
    {
        var te = new PistonTileEntity { BlockId = blockId, BlockMeta = meta, Facing = facing, Extending = extending, HeadRendered = head, X = x, Y = y, Z = z };
        if (!w.IsRemote) w.SpawnEntity(new PistonMovingVisual(w, te));
        return te;
    }

    public bool Finished => _done;
    public float GetProgress(float pt) { if (pt > 1) pt = 1; return LastProgress + (Progress - LastProgress) * pt; }
    /// <summary>Przesunięcie rysowania (MC getOffsetX/Y/Z).</summary>
    public (float x, float y, float z) Offset(float pt)
    {
        float p = GetProgress(pt);
        float k = Extending ? p - 1f : 1f - p;
        return (k * Dir.X[Facing], k * Dir.Y[Facing], k * Dir.Z[Facing]);
    }

    /// <summary>MC clearPistonTileEntity: natychmiast kończy ruch i stawia blok.</summary>
    public void Clear(World w)
    {
        if (LastProgress >= 1f || _done) return;
        LastProgress = Progress = 1f;
        Finish(w);
    }

    private void Finish(World w)
    {
        _done = true;
        if (w.GetTileEntity(X, Y, Z) == this) w.RemoveTileEntity(X, Y, Z);
        if (w.GetBlock(X, Y, Z) == B.PistonExtension)
        {
            w.SetBlock(X, Y, Z, (byte)BlockId, BlockMeta);
            w.NotifyBlock(X, Y, Z, BlockId);
        }
    }

    public override void Tick(World w)
    {
        if (_done || w.GetTileEntity(X, Y, Z) != this) { _done = true; return; }
        LastProgress = Progress;
        if (LastProgress >= 1f)
        {
            PushEntities(w, 1f, 0.25f);
            Finish(w);
        }
        else
        {
            Progress += 0.5f;
            if (Progress >= 1f) Progress = 1f;
            if (Extending) PushEntities(w, Progress, Progress - LastProgress + 0.0625f);
        }
    }

    /// <summary>MC BlockPistonMoving.getBoundingBox: kolizja przesuwanego bloku "ucięta" o postęp.</summary>
    public void CollisionBoxes(World w, float progress, List<AABB> res)
    {
        if (BlockId == B.PistonExtension || BlockId == 0) return;
        var tmp = new List<AABB>();
        Block.Get(BlockId).GetCollisionBoxes(w, X, Y, Z, BlockMeta, tmp);
        if (tmp.Count == 0) return;
        // MC liczy jeden box (getCollisionBoundingBox) - bierzemy obwiednię
        var bb = tmp[0];
        foreach (var t in tmp) bb = new AABB(Math.Min(bb.MinX, t.MinX), Math.Min(bb.MinY, t.MinY), Math.Min(bb.MinZ, t.MinZ), Math.Max(bb.MaxX, t.MaxX), Math.Max(bb.MaxY, t.MaxY), Math.Max(bb.MaxZ, t.MaxZ));
        int fx = Dir.X[Facing], fy = Dir.Y[Facing], fz = Dir.Z[Facing];
        if (fx < 0) bb.MinX -= fx * progress; else bb.MaxX -= fx * progress;
        if (fy < 0) bb.MinY -= fy * progress; else bb.MaxY -= fy * progress;
        if (fz < 0) bb.MinZ -= fz * progress; else bb.MaxZ -= fz * progress;
        res.Add(bb);
    }

    private void PushEntities(World w, float p, float amount)
    {
        p = Extending ? 1f - p : p - 1f;
        var boxes = new List<AABB>();
        CollisionBoxes(w, p, boxes);
        if (boxes.Count == 0) return;
        var box = boxes[0].Offset(X, Y, Z);
        foreach (var e in w.GetEntitiesInBox(box))
        {
            if (e is PistonMovingVisual) continue;
            if (BlockId == B.SlimeBlock && Extending)
            {
                if (Dir.X[Facing] != 0) e.MotionX = Dir.X[Facing];
                else if (Dir.Y[Facing] != 0) e.MotionY = Dir.Y[Facing];
                else e.MotionZ = Dir.Z[Facing];
            }
            else e.MoveWithCollision(amount * Dir.X[Facing], amount * Dir.Y[Facing], amount * Dir.Z[Facing]);
        }
    }

    public override void Save(BinaryWriter bw)
    {
        bw.Write(BlockId); bw.Write(BlockMeta); bw.Write(Facing); bw.Write(Extending); bw.Write(HeadRendered); bw.Write(Progress);
    }
    public override void Load(BinaryReader br)
    {
        BlockId = br.ReadInt32(); BlockMeta = br.ReadInt32(); Facing = br.ReadInt32(); Extending = br.ReadBoolean(); HeadRendered = br.ReadBoolean();
        Progress = LastProgress = br.ReadSingle();
    }
}

/// <summary>Blok w ruchu (MC piston_extension, id 36 / u nas 186). Rysuje go encja PistonMovingVisual.</summary>
public sealed class PistonExtensionBlock : Block
{
    public PistonExtensionBlock(byte id, string k, string n) : base(id, k, n)
    {
        Opaque = false; FullCube = false; Render = RenderType.None; LightOpacity = 0; Hardness = -1; BlastResistance = 0; HasTileEntity = true; InCreative = false;
    }
    public override int DropId(int meta, Rng r) => 0;
    public override ItemStack PickStack(int meta) => null;
    public override bool CanPlaceAt(World w, int x, int y, int z) => false;
    public override void GetCollisionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list)
    {
        if (w is not World world || world.GetTileEntity(x, y, z) is not PistonTileEntity te) return;
        float f = te.GetProgress(0);
        if (te.Extending) f = 1f - f;
        te.CollisionBoxes(world, f, list);
    }
    public override void GetSelectionBoxes(IBlockAccess w, int x, int y, int z, int meta, List<AABB> list) { }
    public override void OnRemoved(World w, int x, int y, int z, int oldMeta)
    {
        // MC breakBlock -> clearPistonTileEntity; blok już nie jest ruchomy, więc tylko kończymy tile entity
        if (w.GetTileEntity(x, y, z) is PistonTileEntity te) { te.Clear(w); w.RemoveTileEntity(x, y, z); }
    }
    public override void GetDrops(World w, int x, int y, int z, int meta, Rng r, ItemStack tool, List<ItemStack> drops)
    {
        if (w.GetTileEntity(x, y, z) is PistonTileEntity te && te.BlockId != B.PistonHead)
            Block.Get(te.BlockId).GetDrops(w, x, y, z, te.BlockMeta, r, tool, drops);
    }
}

/// <summary>Encja tylko do rysowania bloku w ruchu (nie zapisywana).</summary>
public sealed class PistonMovingVisual : Entity
{
    public readonly PistonTileEntity Te;
    public PistonMovingVisual(World w, PistonTileEntity te) : base(w)
    {
        Te = te; Width = 0.01f; Height = 0.01f; NoClip = true; NoGravity = true;
        SetPositionAndPrev(te.X + 0.5, te.Y, te.Z + 0.5);
    }
    public override string TypeKey => "rs_moving_block";
    public override void Tick()
    {
        Age++;
        if (Te.Finished || Age > 40) Kill();
    }
    public override void Save(BinaryWriter bw) { }
    public override void Load(BinaryReader br) { }
}
