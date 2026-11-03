using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Pomiar prędkości wszystkich mobów w trzech sytuacjach (wędrówka, gracz obok, panika po ciosie).
/// Zwraca najszybsze tempo w oknie 20 ticków (bloki/s, tylko w poziomie, bez teleportów).</summary>
public static partial class SelfTest
{
    private static readonly string[] SkipSurvey = { "ender_dragon", "wither", "giant", "ghast", "bat", "blaze", "squid", "guardian", "elder_guardian" };

    internal static (double wander, double player, double panic) SurveyMob(string key, long seed = 77)
    {
        double Run(int scenario)
        {
            var w = MakeWorld(seed, WorldType.Superflat, 4);
            if (EntitySerializer.Create(key, w) is not Mob m) return -1;
            m.SetPositionAndPrev(0.5, 4, 0.5); w.AddEntityNow(m);
            Player p = null;
            if (scenario > 0) { p = SpawnPlayer(w, 5.5, 4, 0.5); p.GameMode = 0; }
            if (scenario == 2) m.Attack(DamageSource.Mob(p), 0.5f);
            int n = scenario == 0 ? 1200 : 300;
            var hist = new (double x, double z)[n + 1];
            hist[0] = (m.X, m.Z);
            double best = 0;
            for (int t = 1; t <= n; t++)
            {
                if (p != null) { p.Health = p.MaxHealth; p.SetPositionAndPrev(p.X, 4, p.Z); }
                if (m.Dead || m.Removed) break;
                w.Tick();
                hist[t] = (m.X, m.Z);
                double step = Math.Sqrt(Sq(hist[t].x - hist[t - 1].x) + Sq(hist[t].z - hist[t - 1].z));
                if (step > 3) { for (int k = 0; k < t; k++) hist[k] = hist[t]; continue; } // teleport (enderman)
                if (t >= 20) best = Math.Max(best, Math.Sqrt(Sq(hist[t].x - hist[t - 20].x) + Sq(hist[t].z - hist[t - 20].z)));
            }
            return best;
        }
        static double Sq(double v) => v * v;
        return (Run(0), Run(1), Run(2));
    }

    internal static void MobSpeedSurveyReport(string only = null)
    {
        ItemRegistry.Init(); Recipes.Init(); Entities.Registries.Init();
        foreach (var key in EntitySerializer.Types.Keys.OrderBy(k => k))
        {
            if (only != null && !only.StartsWith("--") ? key != only : SkipSurvey.Contains(key)) continue;
            var probe = EntitySerializer.Create(key, MakeWorld(1, WorldType.Superflat, 0));
            if (probe is not Mob) continue;
            foreach (long seed in only != null && !only.StartsWith("--") ? new long[] { 1, 2, 3, 4, 5 } : new long[] { 77 }) { var (a, b, c) = SurveyMob(key, seed);
            Console.WriteLine($"  {key,-18} wedrowka {a:F2}  gracz {b:F2}  panika {c:F2}  (b/s)"); }
        }
    }
}
