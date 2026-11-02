using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Autotest dźwięku: BlockCraft.exe --autotest --snd. Chodzenie, sprint, kopanie i moby w odmierzonych tickach,
/// w logu znaczniki SND-TEST. Z BLOCKCRAFT_SOUND_TRACE=1 widać każdy start głosu (tools/dev/snd-report.js).</summary>
public sealed partial class Game
{
    private void SetupAutotestSound()
    {
        long until = 0;
        void Phase(string name, int ticks, Action start)
        {
            Step(1, () => { start(); until = _tickCount + ticks; Log.Info($"SND-TEST faza {name} start tick={_tickCount} fps={_fps}"); });
            WaitUntil(() => _tickCount >= until);
            Step(1, () => Log.Info($"SND-TEST faza {name} koniec tick={_tickCount} pos={Player.X:F1},{Player.Y:F1},{Player.Z:F1}"));
        }
        int t1 = 0;
        Step(5, () => CreateWorld("SndTest " + DateTime.Now.ToString("HHmmss"), 4242, 0, false, WorldType.Superflat, true));
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 3000);
        Step(5, () =>
        {
            World.DayTime = 6000; World.Rules.DoMobSpawning = false;
            foreach (var e in World.Entities.ToList()) if (e is not BlockCraft.Entities.Player) e.Kill();
            Player.Inventory.Main[0] = new ItemStack(I.IronPickaxe);
            Player.Inventory.SelectedSlot = 0; Player.Yaw = 0; Player.Pitch = 0;
        });
        Phase("cisza", 40, () => { });
        Phase("chodzenie", 100, () => _autoInput = (true, false, false, false));
        Phase("sprint", 100, () => _autoInput = (true, false, false, true));
        Phase("stop", 20, () => _autoInput = null);
        int mx = 0, my = 0, mz = 0;
        Phase("kopanie", 100, () =>
        {
            Player.Pitch = 70; Ctrl.UpdateTarget(1f);
            mx = Ctrl.Hit.X; my = Ctrl.Hit.Y; mz = Ctrl.Hit.Z;
            Log.Info($"SND-TEST cel {mx} {my} {mz} {Block.Get(World.GetBlock(mx, my, mz)).Key}");
            _leftDown = true; _leftPressed = true;
        });
        Step(1, () => { _leftDown = false; Player.Pitch = 0; });
        var cows = new List<Cow>(); long lastT = 0;
        Step(1, () =>
        {
            for (int i = 0; i < 4; i++)
            {
                var c = new Cow(World); c.SetPositionAndPrev(Player.X + 3 + i * 2, Player.Y, Player.Z + 2); World.SpawnEntity(c); cows.Add(c);
            }
            until = _tickCount + 200; Log.Info($"SND-TEST faza moby start tick={_tickCount}");
        });
        WaitUntil(() =>
        {
            if (_tickCount != lastT) { lastT = _tickCount; var c = cows[0]; Log.Info($"SND-TEST krowa t={_tickCount} x={c.X:F3} z={c.Z:F3} mx={c.MotionX:F3} mz={c.MotionZ:F3} fwd={c.MoveForward:F2} yaw={c.Yaw:F0} ground={c.OnGround} path={c.Nav.HasPath}"); }
            return _tickCount >= until;
        });
        Step(1, () => { Log.Info($"SND-TEST KONIEC voices={Sound.ActiveVoices}"); Close(); });
    }
}
