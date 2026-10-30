using System.Collections.Concurrent;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.World;

public enum ParticleKind { BlockBreak, Smoke, Explosion, Flame, Splash, Bubble, Heart, Portal, Crit, RedDust, LargeSmoke, Angry, Note, Poof, Spell }

public interface IWorldListener
{
    void OnBlockChanged(int x, int y, int z, byte id, int meta);
    void PlaySound(string name, double x, double y, double z, float volume, float pitch);
    void SpawnParticles(ParticleKind kind, double x, double y, double z, int count, int data);
    void OnEntityAdded(Entity e);
    void OnEntityRemoved(Entity e);
}

public sealed class GameRules
{
    public bool DoDaylightCycle = true, KeepInventory = false, DoMobSpawning = true, MobGriefing = true, DoFireTick = true, NaturalRegeneration = true;
    public bool Set(string name, string value, out string error)
    {
        error = null;
        if (!bool.TryParse(value, out bool v)) { error = "Wartość musi być true/false"; return false; }
        switch (name.ToLowerInvariant())
        {
            case "dodaylightcycle": DoDaylightCycle = v; return true;
            case "keepinventory": KeepInventory = v; return true;
            case "domobspawning": DoMobSpawning = v; return true;
            case "mobgriefing": MobGriefing = v; return true;
            case "naturalregeneration": NaturalRegeneration = v; return true;
            default: error = "Nieznana reguła: " + name; return false;
        }
    }
    public static readonly string[] Names = { "doDaylightCycle", "keepInventory", "doMobSpawning", "mobGriefing", "naturalRegeneration" };
}

public enum Difficulty { Peaceful = 0, Easy = 1, Normal = 2, Hard = 3 }

public sealed partial class World : IBlockAccess
{
    public readonly long Seed;
    public readonly WorldType Type;
    public readonly WorldGenerator Generator;
    public readonly bool IsRemote;
    public readonly ConcurrentDictionary<long, Chunk> Chunks = new();
    public readonly LightEngine Light;
    public readonly Rng Rand = new(Environment.TickCount64);
    public readonly List<IWorldListener> Listeners = new();
    public readonly GameRules Rules = new();
    public Difficulty Difficulty = Difficulty.Normal;
    public long TotalTime;
    public long DayTime = 1000;
    public int SpawnX, SpawnY = 80, SpawnZ;
    public string Name = "Świat";
    public bool AllowCheats = true;
    public bool Hardcore;

    public readonly List<Entity> Entities = new();
    public readonly Dictionary<int, Entity> EntityById = new();
    public readonly List<Player> Players = new();
    private readonly List<Entity> _toAdd = new();
    private int _nextEntityId = 1;
    public ChunkManager ChunkManager;
    public WorldStorage Storage;
    public MobSpawner Spawner;

    public World(long seed, WorldType type, bool remote)
    {
        Seed = seed; Type = type; IsRemote = remote;
        Generator = new WorldGenerator(seed, type);
        Light = new LightEngine(this);
        Spawner = new MobSpawner(this);
    }

    // ---------- dostęp do chunków ----------
    public Chunk GetChunk(int cx, int cz) => Chunks.TryGetValue(MathUtil.ChunkKey(cx, cz), out var c) ? c : null;
    public Chunk GetChunkIfLit(int cx, int cz) => Chunks.TryGetValue(MathUtil.ChunkKey(cx, cz), out var c) && c.Lit ? c : null;
    public bool IsChunkLoaded(int cx, int cz) => Chunks.ContainsKey(MathUtil.ChunkKey(cx, cz));
    public bool IsLoadedAt(int x, int z) => IsChunkLoaded(x >> 4, z >> 4);
    public bool IsAreaLoaded(int x, int z, int r)
    {
        for (int cx = (x - r) >> 4; cx <= (x + r) >> 4; cx++)
            for (int cz = (z - r) >> 4; cz <= (z + r) >> 4; cz++)
                if (!IsChunkLoaded(cx, cz)) return false;
        return true;
    }

    public byte GetBlock(int x, int y, int z)
    {
        if ((uint)y >= Chunk.Height) return 0;
        if (!Chunks.TryGetValue(MathUtil.ChunkKey(x >> 4, z >> 4), out var c)) return 0;
        return c.Blocks[(y << 8) | ((z & 15) << 4) | (x & 15)];
    }
    public int GetMeta(int x, int y, int z)
    {
        if ((uint)y >= Chunk.Height) return 0;
        if (!Chunks.TryGetValue(MathUtil.ChunkKey(x >> 4, z >> 4), out var c)) return 0;
        return c.Meta[(y << 8) | ((z & 15) << 4) | (x & 15)];
    }
    public int GetSkyLight(int x, int y, int z)
    {
        if (y >= Chunk.Height) return 15;
        if (y < 0) return 0;
        var c = GetChunk(x >> 4, z >> 4); if (c == null) return 15;
        return c.Light[Chunk.Index(x & 15, y, z & 15)] >> 4;
    }
    public int GetBlockLight(int x, int y, int z)
    {
        if ((uint)y >= Chunk.Height) return 0;
        var c = GetChunk(x >> 4, z >> 4); if (c == null) return 0;
        return c.Light[Chunk.Index(x & 15, y, z & 15)] & 15;
    }
    /// <summary>Surowe światło (max nieba i bloku, bez uwzględnienia pory dnia).</summary>
    public int GetRawLight(int x, int y, int z) => Math.Max(GetSkyLight(x, y, z), GetBlockLight(x, y, z));
    /// <summary>Światło z uwzględnieniem pory dnia (jak MC getLightFromNeighbors).</summary>
    public int GetLight(int x, int y, int z) => Math.Max(GetSkyLight(x, y, z) - SkyLightSubtracted, GetBlockLight(x, y, z));
    public int GetHeight(int x, int z) { var c = GetChunk(x >> 4, z >> 4); return c?.GetHeight(x & 15, z & 15) ?? 0; }
    public bool CanSeeSky(int x, int y, int z) => y >= GetHeight(x, z);
    public byte GetBiome(int x, int z) { var c = GetChunk(x >> 4, z >> 4); return c?.Biomes[((z & 15) << 4) | (x & 15)] ?? Generator.BiomeAt(x, z); }

    // ---------- modyfikacja ----------
    private int _notifyDepth;

    public bool SetBlock(int x, int y, int z, byte id, int meta = 0, bool notify = true)
    {
        if ((uint)y >= Chunk.Height) return false;
        var c = GetChunk(x >> 4, z >> 4);
        if (c == null) return false;
        int lx = x & 15, lz = z & 15;
        int i = Chunk.Index(lx, y, lz);
        byte old = c.Blocks[i]; int oldMeta = c.Meta[i];
        if (old == id && oldMeta == meta) return false;
        var ob = Block.ById[old]; var nb = Block.ById[id];
        c.SetBlockRaw(lx, y, lz, id, meta);
        c.Modified = true;
        if (ob.LightOpacity != nb.LightOpacity)
        {
            int h = c.GetHeight(lx, lz);
            if (nb.LightOpacity > 0 && y >= h) c.HeightMap[(lz << 4) | lx] = (short)(y + 1);
            else if (nb.LightOpacity == 0 && y == h - 1) c.RecalcHeight(lx, lz);
        }
        Light.OnBlockChanged(x, y, z, ob, nb);
        MarkDirty(x, y, z);
        foreach (var l in Listeners) l.OnBlockChanged(x, y, z, id, meta);
        if (!IsRemote)
        {
            if (old != id)
            {
                ob.OnRemoved(this, x, y, z, oldMeta);
                if (GetBlock(x, y, z) == id) nb.OnAdded(this, x, y, z);
            }
            if (notify) NotifyNeighbors(x, y, z, id);
        }
        return true;
    }

    public void SetMeta(int x, int y, int z, int meta, bool notify = true)
    {
        if ((uint)y >= Chunk.Height) return;
        var c = GetChunk(x >> 4, z >> 4);
        if (c == null) return;
        int i = Chunk.Index(x & 15, y, z & 15);
        if (c.Meta[i] == meta) return;
        c.Meta[i] = (byte)meta;
        c.Modified = true;
        MarkDirty(x, y, z);
        foreach (var l in Listeners) l.OnBlockChanged(x, y, z, c.Blocks[i], meta);
        if (notify && !IsRemote) NotifyNeighbors(x, y, z, c.Blocks[i]);
    }

    /// <summary>Ustawienie bez logiki (sieć – klient).</summary>
    public void SetBlockRemote(int x, int y, int z, byte id, int meta)
    {
        if ((uint)y >= Chunk.Height) return;
        var c = GetChunk(x >> 4, z >> 4);
        if (c == null) return;
        int lx = x & 15, lz = z & 15, i = Chunk.Index(lx, y, lz);
        byte old = c.Blocks[i];
        if (old == id && c.Meta[i] == meta) return;
        var ob = Block.ById[old]; var nb = Block.ById[id];
        c.SetBlockRaw(lx, y, lz, id, meta);
        if (ob.LightOpacity != nb.LightOpacity) c.RecalcHeight(lx, lz);
        Light.OnBlockChanged(x, y, z, ob, nb);
        if (old != id && ob.HasTileEntity && !nb.HasTileEntity) RemoveTileEntity(x, y, z);
        if (nb.HasTileEntity && GetTileEntity(x, y, z) == null) nb.OnAdded(this, x, y, z);
        MarkDirty(x, y, z);
    }

    public void NotifyNeighbors(int x, int y, int z, int sourceId, int exceptFace = -1, bool skipWires = false)
    {
        if (IsRemote) return;
        for (int f = 0; f < 6; f++)
        {
            if (f == exceptFace) continue;
            int nx = x + Dir.X[f], ny = y + Dir.Y[f], nz = z + Dir.Z[f];
            if (skipWires && GetBlock(nx, ny, nz) == B.RedstoneWire) continue;
            NotifyBlock(nx, ny, nz, sourceId);
        }
    }

    public void NotifyBlock(int x, int y, int z, int sourceId)
    {
        if (IsRemote || (uint)y >= Chunk.Height) return;
        int id = GetBlock(x, y, z);
        if (id == 0) return;
        if (_notifyDepth > 64) { ScheduleTick(x, y, z, (byte)id, 1, true); return; }
        _notifyDepth++;
        try { Block.ById[id].OnNeighborChanged(this, x, y, z, sourceId); }
        finally { _notifyDepth--; }
    }

    public void BreakBlock(int x, int y, int z, bool drop, Player player = null, ItemStack tool = null)
    {
        byte id = GetBlock(x, y, z);
        if (id == 0) return;
        int meta = GetMeta(x, y, z);
        var b = Block.ById[id];
        PlayEffectBreak(x, y, z, id, meta);
        if (drop && !IsRemote && !b.IsLiquid)
        {
            var list = new List<ItemStack>();
            b.GetDrops(this, x, y, z, meta, Rand, tool, list);
            foreach (var s in list) if (s != null && s.Count > 0) DropItem(x + 0.5, y + 0.5, z + 0.5, s, true);
        }
        SetBlock(x, y, z, B.Air);
    }

    public void PlayEffectBreak(int x, int y, int z, int id, int meta)
    {
        var b = Block.ById[id];
        SpawnParticles(ParticleKind.BlockBreak, x + 0.5, y + 0.5, z + 0.5, 1, id | (meta << 8));
        PlaySound("dig_" + b.Sound.ToString().ToLowerInvariant(), x + 0.5, y + 0.5, z + 0.5, 1f, 0.8f);
    }

    public void MarkDirty(int x, int y, int z)
    {
        int sx0 = (x - 1) >> 4, sx1 = (x + 1) >> 4, sz0 = (z - 1) >> 4, sz1 = (z + 1) >> 4;
        int sy0 = Math.Max(0, (y - 1) >> 4), sy1 = Math.Min(Chunk.Sections - 1, (y + 1) >> 4);
        for (int cx = sx0; cx <= sx1; cx++)
            for (int cz = sz0; cz <= sz1; cz++)
            {
                var c = GetChunk(cx, cz);
                if (c == null) continue;
                for (int sy = sy0; sy <= sy1; sy++) c.DirtySections |= 1 << sy;
            }
    }

    public void MarkLightDirty(Chunk c, int x, int y, int z)
    {
        int lx = x & 15, lz = z & 15, ly = y & 15;
        int sy = y >> 4;
        c.DirtySections |= 1 << sy;
        if (ly == 0 && sy > 0) c.DirtySections |= 1 << (sy - 1);
        if (ly == 15 && sy < Chunk.Sections - 1) c.DirtySections |= 1 << (sy + 1);
        if (lx == 0 || lx == 15 || lz == 0 || lz == 15) MarkDirty(x, y, z);
    }

    // ---------- tile entities ----------
    public TileEntity GetTileEntity(int x, int y, int z)
    {
        var c = GetChunk(x >> 4, z >> 4);
        if (c == null || (uint)y >= Chunk.Height) return null;
        return c.TileEntities.TryGetValue(Chunk.Index(x & 15, y, z & 15), out var te) ? te : null;
    }
    public void SetTileEntity(int x, int y, int z, TileEntity te)
    {
        var c = GetChunk(x >> 4, z >> 4);
        if (c == null) return;
        te.X = x; te.Y = y; te.Z = z;
        c.TileEntities[Chunk.Index(x & 15, y, z & 15)] = te;
        c.Modified = true;
    }
    public void RemoveTileEntity(int x, int y, int z)
    {
        var c = GetChunk(x >> 4, z >> 4);
        c?.TileEntities.Remove(Chunk.Index(x & 15, y, z & 15));
    }

    // ---------- zaplanowane ticki ----------
    private readonly PriorityQueue<(int x, int y, int z, byte id), long> _ticks = new();
    private readonly HashSet<(int, int, int, byte)> _tickSet = new();

    public void ScheduleTick(int x, int y, int z, byte id, int delay, bool force = false)
    {
        if (IsRemote) return;
        var key = (x, y, z, id);
        if (!force && _tickSet.Contains(key)) return;
        _tickSet.Add(key);
        _ticks.Enqueue((x, y, z, id), TotalTime + Math.Max(1, delay));
    }

    private void ProcessScheduledTicks()
    {
        int n = 0;
        while (n < 2000 && _ticks.TryPeek(out var t, out long due) && due <= TotalTime)
        {
            _ticks.Dequeue(); n++;
            _tickSet.Remove(t);
            if (!IsLoadedAt(t.x, t.z)) continue;
            if (GetBlock(t.x, t.y, t.z) != t.id) continue;
            Block.ById[t.id].OnScheduledTick(this, t.x, t.y, t.z, Rand);
        }
    }

    public List<(int x, int y, int z, byte id, long due)> PendingTicksSnapshot()
    {
        var l = new List<(int, int, int, byte, long)>();
        foreach (var (e, p) in _ticks.UnorderedItems) l.Add((e.x, e.y, e.z, e.id, p - TotalTime));
        return l;
    }

    // ---------- czas ----------
    public float CelestialAngle(float partial)
    {
        int j = (int)(DayTime % 24000L);
        float f = (j + partial) / 24000f - 0.25f;
        if (f < 0) f += 1; if (f > 1) f -= 1;
        float f1 = f;
        f = 1f - (float)((Math.Cos(f * Math.PI) + 1.0) / 2.0);
        return f1 + (f - f1) / 3f;
    }
    public float SkyDarken(float partial)
    {
        float a = CelestialAngle(partial);
        float f = 1f - (MathF.Cos(a * MathF.PI * 2f) * 2f + 0.5f);
        return Math.Clamp(f, 0f, 1f) * 11f;
    }
    public int SkyLightSubtracted => (int)SkyDarken(0);
    public float SunBrightness(float partial)
    {
        float a = CelestialAngle(partial);
        float f = MathF.Cos(a * MathF.PI * 2f) * 2f + 0.5f;
        return Math.Clamp(f, 0f, 1f);
    }
    public bool IsDaytime() => SkyLightSubtracted < 4;
    public bool IsNight() { long t = DayTime % 24000; return t >= 12541 && t <= 23458; }
    public int MoonPhase => (int)(DayTime / 24000L % 8L + 8L) % 8;

    // ---------- encje ----------
    public void SpawnEntity(Entity e)
    {
        if (e.Id == 0) e.Id = _nextEntityId++;
        else if (e.Id >= _nextEntityId) _nextEntityId = e.Id + 1;
        _toAdd.Add(e);
    }
    public int NextEntityId() => _nextEntityId++;

    private void FlushAdds()
    {
        if (_toAdd.Count == 0) return;
        var list = _toAdd.ToArray(); _toAdd.Clear();
        foreach (var e in list)
        {
            if (EntityById.ContainsKey(e.Id)) continue;
            Entities.Add(e); EntityById[e.Id] = e;
            if (e is Player p && !Players.Contains(p)) Players.Add(p);
            foreach (var l in Listeners) l.OnEntityAdded(e);
        }
    }

    public void AddEntityNow(Entity e) { SpawnEntity(e); FlushAdds(); }

    public void RemoveEntity(Entity e)
    {
        e.Removed = true;
        if (Entities.Remove(e))
        {
            EntityById.Remove(e.Id);
            if (e is Player p) Players.Remove(p);
            foreach (var l in Listeners) l.OnEntityRemoved(e);
        }
    }

    public List<Entity> GetEntitiesInBox(AABB box, Entity except = null)
    {
        var res = new List<Entity>();
        foreach (var e in Entities)
            if (e != except && !e.Removed && e.BoundingBox.Intersects(box)) res.Add(e);
        return res;
    }

    public void DropItem(double x, double y, double z, ItemStack s, bool scatter = true)
    {
        if (IsRemote || s == null || s.Count <= 0) return;
        var e = new ItemEntity(this, x, y, z, s.Copy());
        if (scatter)
        {
            e.MotionX = Rand.NextDouble() * 0.2 - 0.1; e.MotionY = 0.2; e.MotionZ = Rand.NextDouble() * 0.2 - 0.1;
            e.X += Rand.NextDouble() * 0.5 - 0.25; e.Z += Rand.NextDouble() * 0.5 - 0.25; e.Y -= 0.125;
        }
        e.PickupDelay = 10;
        SpawnEntity(e);
    }

    public void SpawnXp(double x, double y, double z, int amount)
    {
        if (IsRemote) return;
        while (amount > 0)
        {
            int v = amount >= 17 ? 17 : amount >= 7 ? 7 : amount >= 3 ? 3 : 1;
            amount -= v;
            var o = new XpOrbEntity(this, x, y, z, v);
            o.MotionX = Rand.NextDouble() * 0.2 - 0.1; o.MotionY = Rand.NextDouble() * 0.2; o.MotionZ = Rand.NextDouble() * 0.2 - 0.1;
            SpawnEntity(o);
        }
    }

    public bool HasMonstersNear(int x, int y, int z, double r)
    {
        foreach (var e in Entities)
            if (e is Monster && Math.Abs(e.X - x) < r && Math.Abs(e.Y - y) < 5 && Math.Abs(e.Z - z) < r) return true;
        return false;
    }

    // ---------- efekty ----------
    public void PlaySound(string name, double x, double y, double z, float volume = 1f, float pitch = 1f)
    {
        foreach (var l in Listeners) l.PlaySound(name, x, y, z, volume, pitch);
    }
    public void SpawnParticles(ParticleKind k, double x, double y, double z, int count, int data = 0)
    {
        foreach (var l in Listeners) l.SpawnParticles(k, x, y, z, count, data);
    }

    public void BroadcastMessage(string msg)
    {
        foreach (var p in Players) p.SendMessage(msg);
    }

    // ---------- tick ----------
    public void Tick()
    {
        TotalTime++;
        if (Rules.DoDaylightCycle) DayTime++;
        FlushAdds();
        if (!IsRemote)
        {
            ProcessScheduledTicks();
            RandomTicks();
            TickTileEntities();
            if (Rules.DoMobSpawning) Spawner.Tick();
            CheckSleeping();
        }
        for (int i = 0; i < Entities.Count; i++)
        {
            var e = Entities[i];
            if (e.Removed) continue;
            if (!IsLoadedAt(MathUtil.FloorToInt(e.X), MathUtil.FloorToInt(e.Z)) && e is not Player) continue;
            e.PrevTick();
            e.Tick();
        }
        for (int i = Entities.Count - 1; i >= 0; i--)
        {
            var e = Entities[i];
            if (e.Removed || (e.Dead && e is not Player))
            {
                Entities.RemoveAt(i); EntityById.Remove(e.Id);
                if (e is Player p) Players.Remove(p);
                e.Removed = true;
                foreach (var l in Listeners) l.OnEntityRemoved(e);
            }
        }
        FlushAdds();
    }

    private void CheckSleeping()
    {
        if (Players.Count == 0) return;
        bool all = true;
        foreach (var p in Players) if (!p.Sleeping || p.SleepTimer < 100) all = false;
        if (all)
        {
            DayTime = (DayTime / 24000 + 1) * 24000;
            foreach (var p in Players) p.WakeUp();
        }
    }

    private void RandomTicks()
    {
        foreach (var c in Chunks.Values)
        {
            if (!c.Merged) continue;
            bool near = false;
            foreach (var p in Players)
            {
                int pcx = MathUtil.FloorToInt(p.X) >> 4, pcz = MathUtil.FloorToInt(p.Z) >> 4;
                if (Math.Abs(pcx - c.Cx) <= 7 && Math.Abs(pcz - c.Cz) <= 7) { near = true; break; }
            }
            if (!near) continue;
            for (int s = 0; s < Chunk.Sections; s++)
            {
                if (c.SectionCounts[s] == 0) continue;
                for (int k = 0; k < 3; k++)
                {
                    int r = Rand.Next(4096);
                    int lx = r & 15, lz = (r >> 4) & 15, ly = (r >> 8) + s * 16;
                    byte id = c.Blocks[Chunk.Index(lx, ly, lz)];
                    var b = Block.ById[id];
                    if (b.RandomTicks) b.OnRandomTick(this, c.Cx * 16 + lx, ly, c.Cz * 16 + lz, Rand);
                }
            }
        }
    }

    private readonly List<TileEntity> _teTmp = new();
    private void TickTileEntities()
    {
        _teTmp.Clear();
        foreach (var c in Chunks.Values) foreach (var te in c.TileEntities.Values) _teTmp.Add(te);
        foreach (var te in _teTmp) te.Tick(this);
    }

    // ---------- kolizje ----------
    private readonly List<AABB> _boxTmp = new();
    public List<AABB> GetCollisionBoxes(AABB box, List<AABB> res)
    {
        res.Clear();
        int x0 = MathUtil.FloorToInt(box.MinX), x1 = MathUtil.FloorToInt(box.MaxX);
        int y0 = MathUtil.FloorToInt(box.MinY) - 1, y1 = MathUtil.FloorToInt(box.MaxY);
        int z0 = MathUtil.FloorToInt(box.MinZ), z1 = MathUtil.FloorToInt(box.MaxZ);
        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
            {
                if (!IsLoadedAt(x, z)) { res.Add(new AABB(x, -64, z, x + 1, 256, z + 1)); continue; }
                for (int y = y0; y <= y1; y++)
                {
                    if (y < 0) { continue; }
                    if (y >= Chunk.Height) continue;
                    byte id = GetBlock(x, y, z);
                    if (id == 0) continue;
                    var b = Block.ById[id];
                    if (!b.Solid) continue;
                    _boxTmp.Clear();
                    b.GetCollisionBoxes(this, x, y, z, GetMeta(x, y, z), _boxTmp);
                    foreach (var bb in _boxTmp)
                    {
                        var wb = bb.Offset(x, y, z);
                        if (wb.Intersects(box)) res.Add(wb);
                    }
                }
            }
        return res;
    }

    public bool IsAnyLiquid(AABB box, bool lava)
    {
        int x0 = MathUtil.FloorToInt(box.MinX), x1 = MathUtil.FloorToInt(box.MaxX);
        int y0 = MathUtil.FloorToInt(box.MinY), y1 = MathUtil.FloorToInt(box.MaxY);
        int z0 = MathUtil.FloorToInt(box.MinZ), z1 = MathUtil.FloorToInt(box.MaxZ);
        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                for (int z = z0; z <= z1; z++)
                {
                    int id = GetBlock(x, y, z);
                    if (id == (lava ? B.Lava : B.Water))
                    {
                        double top = y + FluidBlock.HeightOf(GetMeta(x, y, z));
                        if (box.MinY < top) return true;
                    }
                }
        return false;
    }

    // ---------- raycast ----------
    public struct HitResult
    {
        public bool Hit; public int X, Y, Z, Face; public double HitX, HitY, HitZ, Distance;
    }

    public HitResult RayCast(double ox, double oy, double oz, double dx, double dy, double dz, double maxDist, bool hitLiquids = false)
    {
        var res = new HitResult();
        int x = MathUtil.FloorToInt(ox), y = MathUtil.FloorToInt(oy), z = MathUtil.FloorToInt(oz);
        int stepX = Math.Sign(dx), stepY = Math.Sign(dy), stepZ = Math.Sign(dz);
        double tMaxX = dx != 0 ? ((stepX > 0 ? x + 1 - ox : ox - x) / Math.Abs(dx)) : double.MaxValue;
        double tMaxY = dy != 0 ? ((stepY > 0 ? y + 1 - oy : oy - y) / Math.Abs(dy)) : double.MaxValue;
        double tMaxZ = dz != 0 ? ((stepZ > 0 ? z + 1 - oz : oz - z) / Math.Abs(dz)) : double.MaxValue;
        double tdX = dx != 0 ? 1 / Math.Abs(dx) : double.MaxValue, tdY = dy != 0 ? 1 / Math.Abs(dy) : double.MaxValue, tdZ = dz != 0 ? 1 / Math.Abs(dz) : double.MaxValue;
        var boxes = new List<AABB>();
        for (int i = 0; i < 200; i++)
        {
            if (y >= 0 && y < Chunk.Height)
            {
                byte id = GetBlock(x, y, z);
                if (id != 0)
                {
                    var b = Block.ById[id];
                    boxes.Clear();
                    int meta = GetMeta(x, y, z);
                    if (b.IsLiquid)
                    {
                        if (hitLiquids && meta == 0) boxes.Add(new AABB(0, 0, 0, 1, FluidBlock.HeightOf(meta), 1));
                    }
                    else b.GetSelectionBoxes(this, x, y, z, meta, boxes);
                    double best = double.MaxValue; int bf = -1;
                    foreach (var bb in boxes)
                    {
                        if (bb.Offset(x, y, z).RayIntersect(ox, oy, oz, dx, dy, dz, out double t, out int f) && t < best && f >= 0) { best = t; bf = f; }
                    }
                    if (bf >= 0 && best <= maxDist)
                    {
                        res.Hit = true; res.X = x; res.Y = y; res.Z = z; res.Face = bf; res.Distance = best;
                        res.HitX = ox + dx * best; res.HitY = oy + dy * best; res.HitZ = oz + dz * best;
                        return res;
                    }
                }
            }
            double tNext = Math.Min(tMaxX, Math.Min(tMaxY, tMaxZ));
            if (tNext > maxDist) break;
            if (tMaxX < tMaxY && tMaxX < tMaxZ) { x += stepX; tMaxX += tdX; }
            else if (tMaxY < tMaxZ) { y += stepY; tMaxY += tdY; }
            else { z += stepZ; tMaxZ += tdZ; }
        }
        return res;
    }
}
