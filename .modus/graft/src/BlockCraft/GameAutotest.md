# src/BlockCraft/GameAutotest.cs

- Game · class · L10-L190 — public sealed partial class Game
- Shot · method · L16-L16 — private void Shot(string name) => TakeScreenshot(Path.Combine(_shotDir, name + ".png"));
- Step · method · L18-L18 — private void Step(int frames, Action a) => _steps.Enqueue((frames, () => { a(); return true; }));
- WaitUntil · method · L19-L19 — private void WaitUntil(Func<bool> cond) => _steps.Enqueue((1, cond));
- SetupAutotest · method · L21-L178 — private void SetupAutotest()
- AutotestStep · method · L180-L189 — private void AutotestStep()
