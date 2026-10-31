using BlockCraft.Core;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Game
{
    private void SetupAutotestOverworld()
    {
        int ticks = 0, failures = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++ticks > 900);
        Step(1, () => CreateWorld("AutoTestOW " + DateTime.Now.ToString("HHmmss"), 12345, 3, false, WorldType.Default, true));
        ticks = 0;
        WaitUntil(() => _sessionStarted || ++ticks > 1800);
        Step(1, () =>
        {
            World.Rules.DoMobSpawning = false; World.Rules.DoDaylightCycle = false; World.DayTime = 6000;
            Renderer.RenderDistance = 4; World.ChunkManager.Radius = 4;
            Player.Flying = true;
        });
        foreach (string structure in new[] { "Village", "Temple", "Monument" })
        {
            var name = structure; OwStructurePlan plan = null; int wait = 0;
            Step(1, () =>
            {
                var gen = World.Generator.Overworld;
                var location = gen.Locate(name, 0, 0);
                if (location == null) { failures++; Log.Error("OW-TEST brak " + name); return; }
                int cx = location.Value.x >> 4, cz = location.Value.z >> 4;
                plan = name switch { "Village" => gen.Village(cx, cz), "Temple" => gen.Temple(cx, cz), _ => gen.Monument(cx, cz) };
                if (plan == null) { failures++; return; }
                var box = plan.Box;
                double radius = Math.Max(box.SizeX, box.SizeZ) * .55 + 12;
                Player.SetPositionAndPrev((box.X0 + box.X1) / 2.0 + radius, Math.Max(70, box.Y1) + radius * .9, (box.Z0 + box.Z1) / 2.0 + radius);
                Player.Yaw = Player.PrevYaw = 135; Player.Pitch = Player.PrevPitch = 32;
                Log.Info($"OW-TEST {name} chunk={cx},{cz} bbox={box.X0},{box.Y0},{box.Z0}/{box.X1},{box.Y1},{box.Z1}");
            });
            WaitUntil(() => plan == null || ++wait > 1800 || wait > 60 && Renderer.PendingMeshes < 3
                && World.GetChunk((plan.Box.X0 + plan.Box.X1) / 2 >> 4, (plan.Box.Z0 + plan.Box.Z1) / 2 >> 4)?.Merged == true);
            Step(20, () =>
            {
                if (plan == null) return;
                var c = World.GetChunk((plan.Box.X0 + plan.Box.X1) / 2 >> 4, (plan.Box.Z0 + plan.Box.Z1) / 2 >> 4);
                if (c?.Merged != true) { failures++; Log.Error("OW-TEST nie wczytano " + name); }
                Shot("ow_" + name.ToLowerInvariant());
            });
        }
        Step(1, () => { Log.Info($"OW-TEST KONIEC: {(failures == 0 ? "OK" : failures + " FAIL")}"); Log.Info("AUTOTEST DONE"); Close(); });
    }
}
