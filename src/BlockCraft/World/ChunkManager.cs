using System.Collections.Concurrent;
using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World;

/// <summary>Ładowanie/generowanie chunków w wątkach roboczych + integracja w wątku głównym.</summary>
public sealed class ChunkManager : IDisposable
{
    private readonly World _w;
    private readonly object _lock = new();
    private readonly List<long> _requests = new();
    private readonly HashSet<long> _pending = new();
    private readonly ConcurrentQueue<Chunk> _ready = new();
    private readonly Thread[] _workers;
    private readonly AutoResetEvent _signal = new(false);
    private volatile bool _running = true;
    private (int cx, int cz)[] _centers = { (0, 0) };
    public int Radius = 8;
    public int LoadedCount => _w.Chunks.Count;
    public int PendingCount { get { lock (_lock) return _requests.Count + _pending.Count; } }
    private readonly ConcurrentDictionary<long, Chunk> _savingChunks = new();
    private readonly BlockingCollection<(Chunk, byte[])> _saveQueue = new();
    private readonly Thread _saver;

    public ChunkManager(World w)
    {
        _w = w;
        int n = Math.Clamp(Environment.ProcessorCount - 2, 1, 4);
        _workers = new Thread[n];
        for (int i = 0; i < n; i++)
        {
            _workers[i] = new Thread(WorkerLoop) { IsBackground = true, Name = "ChunkGen" + i, Priority = ThreadPriority.BelowNormal };
            _workers[i].Start();
        }
        _saver = new Thread(SaverLoop) { IsBackground = true, Name = "ChunkSaver" };
        _saver.Start();
    }

    public void Dispose()
    {
        _running = false;
        for (int i = 0; i < _workers.Length; i++) _signal.Set();
        _saveQueue.CompleteAdding();
        _saver.Join(5000);
    }

    private void WorkerLoop()
    {
        while (_running)
        {
            long key = 0; bool got = false;
            lock (_lock)
            {
                if (_requests.Count > 0)
                {
                    int best = -1; long bestD = long.MaxValue;
                    for (int i = 0; i < _requests.Count; i++)
                    {
                        long k = _requests[i];
                        int cx = (int)(k >> 32), cz = (int)k;
                        long d = long.MaxValue;
                        foreach (var c in _centers) { long dd = (long)(cx - c.cx) * (cx - c.cx) + (long)(cz - c.cz) * (cz - c.cz); if (dd < d) d = dd; }
                        if (d < bestD) { bestD = d; best = i; }
                    }
                    key = _requests[best];
                    _requests[best] = _requests[^1]; _requests.RemoveAt(_requests.Count - 1);
                    got = true;
                }
            }
            if (!got) { _signal.WaitOne(50); continue; }
            try
            {
                int cx = (int)(key >> 32), cz = (int)key;
                Chunk c = null;
                if (_savingChunks.TryGetValue(key, out var sc)) { c = sc; c.Unloaded = false; }
                if (c == null && _w.Storage != null) c = _w.Storage.LoadChunk(cx, cz);
                if (c == null) c = _w.GenerateChunk(cx, cz);
                LightEngine.InitChunkLocal(c, _w.Provider.HasNoSky);
                _ready.Enqueue(c);
            }
            catch (Exception ex) { Log.Error("Chunk gen error: " + ex); lock (_lock) _pending.Remove(key); }
        }
    }

    private void SaverLoop()
    {
        try
        {
            foreach (var (c, ents) in _saveQueue.GetConsumingEnumerable())
            {
                try { _w.Storage?.SaveChunk(c, ents); }
                catch (Exception ex) { Log.Error("Save chunk error: " + ex.Message); }
                _savingChunks.TryRemove(MathUtil.ChunkKey(c.Cx, c.Cz), out _);
            }
        }
        catch (Exception) { }
    }

    private int _lastCenterHash = int.MinValue;

    /// <summary>Wywoływane co klatkę/tick w wątku głównym.</summary>
    public void Update(IReadOnlyList<(int cx, int cz)> centers, double budgetMs = 6)
    {
        if (centers.Count == 0) return;
        int hash = Radius;
        foreach (var c in centers) hash = hash * 31 + c.cx * 7919 + c.cz;
        if (hash != _lastCenterHash)
        {
            _lastCenterHash = hash;
            lock (_lock)
            {
                _centers = centers.ToArray();
                foreach (var old in _requests) _pending.Remove(old);
                _requests.Clear();
                int r = Radius + 1;
                foreach (var cc in centers)
                    for (int dx = -r; dx <= r; dx++)
                        for (int dz = -r; dz <= r; dz++)
                        {
                            if (dx * dx + dz * dz > (r + 0.5) * (r + 0.5)) continue;
                            long k = MathUtil.ChunkKey(cc.cx + dx, cc.cz + dz);
                            if (_w.Chunks.ContainsKey(k) || _pending.Contains(k)) continue;
                            _pending.Add(k); _requests.Add(k);
                        }
            }
            for (int i = 0; i < _workers.Length; i++) _signal.Set();
            UnloadFar();
        }
        Integrate(budgetMs);
    }

    private bool InRange(long k, int r)
    {
        int cx = (int)(k >> 32), cz = (int)k;
        foreach (var c in _centers) if (Math.Abs(cx - c.cx) <= r && Math.Abs(cz - c.cz) <= r) return true;
        return false;
    }

    private void Integrate(double budgetMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < budgetMs && _ready.TryDequeue(out var c))
        {
            long k = MathUtil.ChunkKey(c.Cx, c.Cz);
            lock (_lock) _pending.Remove(k);
            if (!InRange(k, Radius + 3)) { if (c.Modified) QueueSave(c, null); continue; }
            if (_w.Chunks.ContainsKey(k)) continue;
            _w.Chunks[k] = c;
            _w.Light.MergeChunk(c);
            c.MarkAllDirty();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var n = _w.GetChunk(c.Cx + dx, c.Cz + dz);
                    if (n != null) n.DirtySections = (1 << Chunk.Sections) - 1;
                }
            foreach (var te in c.TileEntities.Values) { te.X += 0; }
            if (c.PendingEntityData != null) { EntitySerializer.LoadEntities(_w, c.PendingEntityData); c.PendingEntityData = null; }
            if (!c.Populated) { c.Populated = true; _w.Spawner.PopulateChunk(c); }
            // zaplanuj ticki płynów na krawędziach (np. wczytane źródła)
        }
    }

    private void UnloadFar()
    {
        var toRemove = new List<long>();
        foreach (var kv in _w.Chunks)
            if (!InRange(kv.Key, Radius + 3)) toRemove.Add(kv.Key);
        foreach (var k in toRemove)
        {
            if (!_w.Chunks.TryRemove(k, out var c)) continue;
            c.Unloaded = true;
            // encje w chunku
            var ents = new List<Entity>();
            foreach (var e in _w.Entities)
            {
                if (e is Player) continue;
                if ((MathUtil.FloorToInt(e.X) >> 4) == c.Cx && (MathUtil.FloorToInt(e.Z) >> 4) == c.Cz) ents.Add(e);
            }
            byte[] data = null;
            var persist = ents.Where(e => e.Persistent).ToList();
            if (persist.Count > 0) { data = EntitySerializer.SaveEntities(persist); c.Modified = true; }
            foreach (var e in ents) _w.RemoveEntity(e);
            if (c.Modified && _w.Storage != null) QueueSave(c, data);
        }
    }

    private void QueueSave(Chunk c, byte[] ents)
    {
        if (_w.Storage == null) return;
        _savingChunks[MathUtil.ChunkKey(c.Cx, c.Cz)] = c;
        c.PendingEntityData = ents;
        _saveQueue.Add((c, ents));
    }

    /// <summary>Zapis wszystkich zmienionych chunków (synchronicznie).</summary>
    public void SaveAll()
    {
        if (_w.Storage == null) return;
        var byChunk = new Dictionary<long, List<Entity>>();
        foreach (var e in _w.Entities)
        {
            if (e is Player || !e.Persistent) continue;
            long k = MathUtil.ChunkKey(MathUtil.FloorToInt(e.X) >> 4, MathUtil.FloorToInt(e.Z) >> 4);
            if (!byChunk.TryGetValue(k, out var l)) byChunk[k] = l = new List<Entity>();
            l.Add(e);
        }
        foreach (var kv in _w.Chunks)
        {
            var c = kv.Value;
            byChunk.TryGetValue(kv.Key, out var ents);
            if (!c.Modified && ents == null) continue;
            try { _w.Storage.SaveChunk(c, ents != null ? EntitySerializer.SaveEntities(ents) : null); }
            catch (Exception ex) { Log.Error("SaveAll: " + ex.Message); }
        }
        // poczekaj na kolejkę
        var sw = Stopwatch.StartNew();
        while (_saveQueue.Count > 0 && sw.ElapsedMilliseconds < 5000) Thread.Sleep(10);
    }

    /// <summary>Synchroniczne załadowanie (np. przy starcie dla obszaru spawnu).</summary>
    public bool IsAreaReady(int cx, int cz, int r)
    {
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                var c = _w.GetChunk(cx + dx, cz + dz);
                if (c == null || !c.Merged) return false;
            }
        return true;
    }
}
