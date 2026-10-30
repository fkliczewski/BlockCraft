# src/BlockCraft/World/Lighting.cs

- LightEngine · class · L6-L262 — public sealed class LightEngine
- LightEngine · method · L15-L15 — public LightEngine(World w) { _w = w; InitTables(); }
- InitTables · method · L17-L22 — public static void InitTables()
- InitChunkLocal · method · L25-L72 — public static void InitChunkLocal(Chunk c)
- SpreadLocal · method · L74-L99 — private static void SpreadLocal(Chunk c, Queue<int> q, bool sky)
- GetL · method · L102-L110 — private int GetL(int x, int y, int z, bool sky, out Chunk c)
- SetL · method · L111-L117 — private void SetL(Chunk c, int x, int y, int z, bool sky, int lvl)
- Push · method · L120-L124 — private void Push(int x, int y, int z, int l)
- PushR · method · L125-L129 — private void PushR(int x, int y, int z, int l)
- RunAdd · method · L131-L151 — private void RunAdd(bool sky)
- RunRemove · method · L153-L174 — private void RunRemove(bool sky)
- MergeChunk · method · L177-L210 — public void MergeChunk(Chunk c)
- OnBlockChanged · method · L213-L219 — public void OnBlockChanged(int x, int y, int z, Block oldB, Block newB)
- UpdateChannel · method · L221-L261 — private void UpdateChannel(int x, int y, int z, bool sky, Block newB)
