using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

public enum DimensionId { Nether = -1, Overworld = 0, End = 1 }

/// <summary>Cechy wymiaru, wspólne dla serwera, światła, renderera i mechanik przedmiotów.</summary>
public sealed class DimensionProvider
{
    public DimensionId Id { get; }
    public bool HasNoSky => Id != DimensionId.Overworld;
    public bool HasDaylightCycle => Id == DimensionId.Overworld;
    public bool WaterVaporizes => Id == DimensionId.Nether;
    public bool CanRespawn => Id == DimensionId.Overworld;
    public bool NaturalNavigation => Id == DimensionId.Overworld;
    public string SaveFolder => Id == DimensionId.Nether ? "DIM-1" : Id == DimensionId.End ? "DIM1" : "";
    public float[] LightBrightnessTable { get; } = new float[16];
    public DimensionProvider(DimensionId id)
    {
        if (id is not (DimensionId.Nether or DimensionId.Overworld or DimensionId.End)) throw new ArgumentOutOfRangeException(nameof(id));
        Id = id;
        float ambient = id == DimensionId.Nether ? .1f : 0;
        for (int i = 0; i < 16; i++) { float darkness = 1 - i / 15f; LightBrightnessTable[i] = (1 - darkness) / (darkness * 3 + 1) * (1 - ambient) + ambient; }
    }
}

public sealed partial class World
{
    public DimensionProvider Provider { get; private set; }
    public DimensionId Dimension => Provider.Id;
    public DimensionSet Dimensions { get; internal set; }
    private DimensionTerrain _dimensionTerrain;
    public Chunk GenerateChunk(int cx, int cz) => _dimensionTerrain?.Generate(cx,cz) ?? Generator.Generate(cx,cz);
    public void EnsureDimensionArea(int x, int z, int radius)
    {
        // Wywołuje wątek hosta. Integracja gotowych chunków pomija już dodane klucze.
        var added = new List<Chunk>();
        for (int cx = (x-radius)>>4; cx <= (x+radius)>>4; cx++) for (int cz = (z-radius)>>4; cz <= (z+radius)>>4; cz++)
        {
            if (GetChunk(cx,cz) != null) continue;
            var c = Storage?.LoadChunk(cx,cz) ?? GenerateChunk(cx,cz);
            LightEngine.InitChunkLocal(c,Provider.HasNoSky);
            Chunks[MathUtil.ChunkKey(cx,cz)] = c; added.Add(c);
        }
        foreach (var c in added)
        {
            Light.MergeChunk(c); c.MarkAllDirty();
            if (c.PendingEntityData != null) { EntitySerializer.LoadEntities(this,c.PendingEntityData); c.PendingEntityData = null; }
        }
    }
}

/// <summary>Zbiór światów hosta. Nie zależy od Game ani od renderera; może obsługiwać wielu graczy.</summary>
public sealed class DimensionSet : IDisposable
{
    private readonly Dictionary<DimensionId,World> _worlds = new();
    private readonly World _overworld;
    public IReadOnlyDictionary<DimensionId,World> Loaded => _worlds;
    public event Action<Entity,World,World> EntityTransferred;
    private readonly Queue<(Entity entity,DimensionId target,double x,double y,double z)> _transfers = new();
    public DimensionSet(World overworld)
    {
        if (overworld.Dimension != DimensionId.Overworld) throw new ArgumentException("Wymagany Overworld");
        _overworld = overworld; _worlds[DimensionId.Overworld] = overworld; overworld.Dimensions = this;
    }
    public World Get(DimensionId id)
    {
        if (_worlds.TryGetValue(id,out var w)) return w;
        w = new World(_overworld.Seed,_overworld.Type,_overworld.IsRemote,id)
        {
            Dimensions = this, Name = _overworld.Name, AllowCheats = _overworld.AllowCheats,
            Hardcore = _overworld.Hardcore, Difficulty = _overworld.Difficulty, TotalTime = _overworld.TotalTime,
            SpawnX = id == DimensionId.End ? 100 : 0, SpawnY = id == DimensionId.End ? 49 : 64, SpawnZ = 0
        };
        if (_overworld.Storage != null) w.Storage = new WorldStorage(Path.Combine(_overworld.Storage.Dir,w.Provider.SaveFolder));
        CopyRules(w);
        _worlds.Add(id,w);
        return w;
    }
    private void CopyRules(World w)
    {
        // Nether i End mają własny spawner (DimensionSpawner), więc ogólny MobSpawner świata jest w nich wyłączony
        w.Rules.DoDaylightCycle = _overworld.Rules.DoDaylightCycle; w.Rules.DoMobSpawning = w == _overworld && _overworld.Rules.DoMobSpawning;
        w.Rules.KeepInventory = _overworld.Rules.KeepInventory; w.Rules.DoFireTick = _overworld.Rules.DoFireTick;
        w.Rules.MobGriefing = _overworld.Rules.MobGriefing; w.Rules.NaturalRegeneration = _overworld.Rules.NaturalRegeneration;
    }
    public void QueueTransfer(Entity entity, DimensionId target, double x, double y, double z)
    {
        if (entity.World.IsRemote || entity.Dead || _transfers.Any(t=>t.entity == entity)) return;
        _transfers.Enqueue((entity,target,x,y,z));
    }
    public void FlushTransfers()
    {
        while (_transfers.TryDequeue(out var t)) Transfer(t.entity,t.target,t.x,t.y,t.z);
    }
    public void Transfer(Entity entity, DimensionId target, double x, double y, double z)
    {
        var old = entity.World; var next = Get(target);
        if (old == next) return;
        next.EnsureDimensionArea(MathUtil.FloorToInt(x),MathUtil.FloorToInt(z),32);
        old.RemoveEntity(entity); entity.World = next; entity.Id = 0; entity.Removed = false;
        entity.MotionX = entity.MotionY = entity.MotionZ = 0; entity.FallDistance = 0;
        entity.SetPositionAndPrev(x,y,z); next.AddEntityNow(entity);
        EntityTransferred?.Invoke(entity,old,next);
    }
    public void Tick()
    {
        foreach (var w in _worlds.Values.ToArray())
        {
            CopyRules(w); w.Tick();
            if (w.IsRemote) continue;
            DimensionPortals.TickContacts(w);
            if (w != _overworld && _overworld.Rules.DoMobSpawning) DimensionSpawner.Tick(w);
        }
        FlushTransfers();
    }
    public void SaveAll() { foreach (var w in _worlds.Values) w.ChunkManager?.SaveAll(); }
    public void Dispose() { foreach (var w in _worlds.Values) w.ChunkManager?.Dispose(); }
}
