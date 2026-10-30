# src/BlockCraft/World/ChunkManager.cs

- ChunkManager · class · L9-L234 — public sealed class ChunkManager : IDisposable
- LoadedCount · variable · L21-L21 — public int LoadedCount => _w.Chunks.Count;
- PendingCount · variable · L22-L22 — public int PendingCount { get { lock (_lock) return _requests.Count + _pending.Count; } }
- ChunkManager · method · L27-L39 — public ChunkManager(World w)
- Dispose · method · L41-L47 — public void Dispose()
- WorkerLoop · method · L49-L85 — private void WorkerLoop()
- SaverLoop · method · L87-L99 — private void SaverLoop()
- Update · method · L104-L132 — public void Update(IReadOnlyList<(int cx, int cz)> centers, double budgetMs = 6)
- InRange · method · L134-L139 — private bool InRange(long k, int r)
- Integrate · method · L141-L164 — private void Integrate(double budgetMs)
- UnloadFar · method · L166-L188 — private void UnloadFar()
- QueueSave · method · L190-L196 — private void QueueSave(Chunk c, byte[] ents)
- SaveAll · method · L199-L221 — public void SaveAll()
- IsAreaReady · method · L224-L233 — public bool IsAreaReady(int cx, int cz, int r)
