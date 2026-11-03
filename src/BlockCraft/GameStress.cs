using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

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

    private void StressFrame()
    {
        if (_stressMeasure)
        {
            _stressFrames.Add(_stressClock.Elapsed.TotalMilliseconds);
            _stressClock.Restart();
        }
        AutotestStep();
    }

    private void SetupStress()
    {
        int i = Array.IndexOf(_args, "--stress");
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
}
