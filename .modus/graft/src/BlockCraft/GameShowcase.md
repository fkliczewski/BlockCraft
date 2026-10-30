# src/BlockCraft/GameShowcase.cs

- Game · class · L12-L459 — public sealed partial class Game
- Scripted · variable · L21-L21 — private bool Scripted => _autotest || _showcase;
- Record · method · L24-L34 — private void Record(string scene, int ticks, int every, Action start = null, Action<int> perTick = null)
- ShowcaseFrame · method · L39-L54 — private void ShowcaseFrame()
- CaptureFrame · method · L57-L81 — private void CaptureFrame()
- Snap · method · L83-L83 — private void Snap(string name) { _chat.Clear(); Shot(name); _chat.Clear(); }
- GroundY · method · L85-L90 — private int GroundY(int x, int z)
- Summon · method · L92-L93 — private void Summon(string mob, double x, double y, double z) =>
- FindFlat · method · L96-L120 — private (int x, int z) FindFlat(int x0, int z0, int radius = 8, int search = 48)
- Axes · method · L123-L129 — private (int fx, int fz, int rx, int rz) Axes()
- Stage · method · L132-L150 — private (int x, int y, int z) Stage(int x, int z, float yaw, float pitch, int clear = 0, double lift = 0)
- WaitForMeshes · method · L152-L156 — private void WaitForMeshes(int maxFrames = 400)
- FaceEast · method · L158-L168 — private void FaceEast()
- SetupShowcase · method · L170-L458 — private void SetupShowcase()
