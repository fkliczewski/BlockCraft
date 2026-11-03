using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;

namespace BlockCraft;

/// <summary>Test płynności w oknie: BlockCraft.exe --stress tnt N [--lit] [--fps 1000].
/// Superpłaski świat, kostka N TNT przed graczem (łańcuch od środka albo --lit: wszystkie zapalone),
/// pomiar czasu klatek aż do ostatniego wybuchu. Wynik w logu jako linia "STRESS".</summary>
public sealed partial class Game
{
    private bool _stress;
    private bool _stressMeasure;
    private readonly List<double> _stressFrames = new();
    private readonly Stopwatch _stressClock = new();
    private long _stressTick0;
    private readonly List<double> _stressCpu = new();
    private long _stressSections;
    private Action _stressPerFrame;

    private void StressFrame()
    {
        if (_stressMeasure)
        {
            _stressFrames.Add(_stressClock.Elapsed.TotalMilliseconds);
            _stressCpu.Add((_clock.Elapsed.TotalSeconds - _frameStart) * 1000);
            _stressSections += Renderer.RenderedSections;
            _stressClock.Restart();
        }
        if (_holdY is double hy && Player != null) { Player.Y = Player.PrevY = hy; Player.MotionY = 0; }
        _stressPerFrame?.Invoke();
        AutotestStep();
    }

    private void SetupStress()
    {
        int i = Array.IndexOf(_args, "--stress");
        if (i + 1 < _args.Length && _args[i + 1] == "render") { SetupStressRender(); return; }
        int n = i + 2 < _args.Length && int.TryParse(_args[i + 2], out var nn) ? nn : 10000;
        bool lit = _args.Contains("--lit");
        int side = Math.Max(1, (int)Math.Round(Math.Cbrt(n)));
        n = side * side * side;
        int t0 = 0;
        WaitUntil(() => HasMenuWorld || ++t0 > 600);
        Step(5, () => CreateWorld("Stress " + DateTime.Now.ToString("HHmmss"), 1234, 1, false, WorldType.Superflat, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        int x0 = 0, y0 = 4, z0 = 0;
        Step(30, () =>
        {
            World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false; World.DayTime = 6000;
            Player.Invulnerable = true; Player.Flying = true;
            int px = MathUtil.FloorToInt(Player.X), pz = MathUtil.FloorToInt(Player.Z);
            x0 = px - side / 2; z0 = pz + 24; y0 = 4;
            Player.SetPositionAndPrev(px + 0.5, y0 + side + 12, pz + 0.5);
            Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 30;
        });
        Step(1, () =>
        {
            var w = World;
            for (int y = 0; y < side; y++)
                for (int z = 0; z < side; z++)
                    for (int x = 0; x < side; x++)
                    {
                        if (lit) w.SpawnEntity(new TntEntity(w, x0 + x + 0.5, y0 + y, z0 + z + 0.5, 80));
                        else w.SetBlock(x0 + x, y0 + y, z0 + z, B.Tnt, 0, false);
                    }
            if (!lit)
            {
                int cx = x0 + side / 2, cy = y0 + side / 2, cz = z0 + side / 2;
                w.SetBlock(cx, cy, cz, B.Air, 0, false);
                w.SpawnEntity(new TntEntity(w, cx + 0.5, cy, cz + 0.5, 1));
            }
            Log.Info($"STRESS start: N={n} lit={lit}");
            _stressFrames.Clear(); _stressClock.Restart(); _stressMeasure = true; _stressTick0 = _tickCount;
        });
        var total = new Stopwatch();
        Step(1, () => total.Start());
        WaitUntil(() =>
        {
            if (_tickCount - _stressTick0 < 100 && total.Elapsed.TotalSeconds < 120) return false;
            // TNT odrzucone poza wczytany teren nie tickuje (jak w MC), więc się nie liczy
            foreach (var e in World.Entities)
                if (e is TntEntity && !e.Removed && World.IsLoadedAt(MathUtil.FloorToInt(e.X), MathUtil.FloorToInt(e.Z))) return total.Elapsed.TotalSeconds > 120;
            return true;
        });
        Step(1, () =>
        {
            _stressMeasure = false;
            var f = _stressFrames.ToArray(); Array.Sort(f);
            double sum = 0; foreach (var v in f) sum += v;
            double P(double q) => f.Length == 0 ? 0 : f[Math.Min(f.Length - 1, (int)(f.Length * q))];
            Log.Info($"STRESS wynik: N={n} lit={lit} klatki={f.Length} czas={sum / 1000:F2} s srednio={f.Length / Math.Max(0.001, sum / 1000):F0} FPS " +
                     $"mediana={P(0.5):F1} ms p99={P(0.99):F1} ms max={(f.Length > 0 ? f[^1] : 0):F1} ms ticki={_tickCount - _stressTick0}");
            Close();
        });
    }

    /// <summary>--stress render [--rd N]: świat domyślny (seed 12345), po wczytaniu wszystkiego 6 s postoju
    /// patrząc na horyzont i 6 s obrotu kamery dookoła. Wynik: FPS, 1% najgorszych klatek, czas CPU klatki, sekcje.</summary>
    private void SetupStressRender()
    {
        int ri = Array.IndexOf(_args, "--rd");
        int rd = ri >= 0 && ri + 1 < _args.Length && int.TryParse(_args[ri + 1], out var r) ? r : Settings.RenderDistance;
        Settings.RenderDistance = rd; ApplySettings();
        int si = Array.IndexOf(_args, "--scale");
        if (si >= 0 && si + 1 < _args.Length && int.TryParse(_args[si + 1], out var scl)) Settings.RenderScale = scl;
        int t0 = 0;
        WaitUntil(() => HasMenuWorld || ++t0 > 600);
        Step(5, () => CreateWorld("Stress " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted || ++t1 > 3000);
        Step(5, () =>
        {
            World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false; World.DayTime = 6000;
            Player.Invulnerable = true; Player.Flying = true; HideGui = true;
            Player.SetPositionAndPrev(Player.X, 110, Player.Z);
            Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 15;
        });
        // wczytanie i zmeshowanie całego zasięgu, potem chwila na ustabilizowanie
        var wait = Stopwatch.StartNew(); int calm = 0;
        WaitUntil(() =>
        {
            // pozycja ustawiana do skutku (wczytywanie świata potrafi przestawić gracza na spawn)
            if (Math.Abs(Player.Y - 110) > 0.5) { Player.SetPositionAndPrev(Player.X, 110, Player.Z); Player.Flying = true; _holdY = 110; calm = 0; }
            if (wait.Elapsed.TotalSeconds > 45) return true;
            calm = Renderer.PendingMeshes == 0 ? calm + 1 : 0;
            return calm > 300 && wait.Elapsed.TotalSeconds > 8;
        });
        _shotDir = Path.Combine(AppContext.BaseDirectory, "stress"); Directory.CreateDirectory(_shotDir);
        string tag = _args.Contains("--tag") ? _args[Array.IndexOf(_args, "--tag") + 1] : "";
        Step(1, () => { Log.Info($"STRESS render kamera {Player.X:F1} {Player.Y:F1} {Player.Z:F1} yaw {Player.Yaw:F0} pitch {Player.Pitch:F0}"); Shot($"render_rd{rd}{tag}"); });
        var phase = new Stopwatch();
        void Phase(string name, bool spin)
        {
            Step(1, () =>
            {
                _stressFrames.Clear(); _stressCpu.Clear(); _stressSections = 0; StressGpuReset(); Array.Clear(BlockCraft.Render.WorldRenderer.CpuMs); _stressClock.Restart(); _stressMeasure = true;
                phase.Restart();
                float yaw0 = Player.Yaw;
                _stressPerFrame = spin ? () => { Player.Yaw = Player.PrevYaw = yaw0 + (float)(phase.Elapsed.TotalSeconds * 60.0); } : null;
            });
            WaitUntil(() => phase.Elapsed.TotalSeconds >= 6);
            Step(1, () =>
            {
                _stressMeasure = false; _stressPerFrame = null;
                var f = _stressFrames.ToArray(); Array.Sort(f);
                var c = _stressCpu.ToArray(); Array.Sort(c);
                double sum = 0; foreach (var v in f) sum += v;
                double csum = 0; foreach (var v in c) csum += v;
                int worst = Math.Max(1, f.Length / 100); double wsum = 0; for (int k = f.Length - worst; k < f.Length; k++) wsum += f[k];
                Log.Info($"STRESS render {name}: RD={rd} skala={Settings.RenderScale}% klatki={f.Length} srednio={f.Length / Math.Max(0.001, sum / 1000):F0} FPS " +
                         $"1%low={1000 / Math.Max(0.001, wsum / worst):F0} FPS cpu={csum / Math.Max(1, c.Length):F3} ms gpu={StressGpuAvgMs:F3} ms sekcje={_stressSections / Math.Max(1, f.Length)} " +
                         $"GL={GL.GetString(OpenTK.Graphics.OpenGL4.StringName.Renderer)}");
                var pm = BlockCraft.Render.WorldRenderer.CpuMs; int fn = Math.Max(1, f.Length);
                Log.Info($"STRESS render {name} cpu/klatke: update={pm[0] / fn:F3} niebo={pm[1] / fn:F3} teren={pm[2] / fn:F3} encje={pm[3] / fn:F3} przezr={pm[4] / fn:F3} chmury={pm[5] / fn:F3} ms [encje {pm[6] / fn:F3} tile {pm[7] / fn:F3} flush {pm[8] / fn:F3}]");
            });
        }
        Phase("postoj", false);
        Phase("obrot", true);
        Step(1, () => Close());
    }
}
