using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.World;

namespace BlockCraft;

public sealed partial class Game
{
    private void SetupAutotestCommands()
    {
        int wait = 0, x = 0, z = 0;
        void Run(string line)
        { if (Commands.Run(new PlayerSender(Player), line) == 0) throw new InvalidOperationException("CMD-TEST FAIL /" + line); }
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++wait > 600);
        Step(5, () => CreateWorld("AutoTestCMD " + DateTime.Now.ToString("HHmmss"), 1818, 1, false, WorldType.Superflat, true));
        int loaded = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++loaded > 1500);
        Step(30, () =>
        {
            x = MathUtil.FloorToInt(Player.X); z = MathUtil.FloorToInt(Player.Z);
            World.Rules.DoMobSpawning = false; World.DayTime = 6000; HideGui = false;
            Player.SetGameMode(1); Player.Flying = true;
            Player.SetPositionAndPrev(x + .5, 6, z - 6.5); Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 12;
            Run("scoreboard objectives add punkty dummy §eWyniki testu CMD");
            Run("scoreboard objectives setdisplay sidebar punkty");
            Run("scoreboard players set Alicja punkty 12"); Run("scoreboard players set Bartek punkty 8"); Run("scoreboard players set Celina punkty 5");
            Run("scoreboard teams add zieloni"); Run("scoreboard teams option zieloni color green"); Run("scoreboard teams join zieloni Alicja");
            World.SetBlock(x, 4, z, B.CommandBlock);
            ((CommandBlockTile)World.GetTileEntity(x, 4, z)).Logic.Command = "scoreboard players add Alicja punkty 1";
        });
        Step(20, () => Shot("cmd_01_sidebar"));
        Step(5, () =>
        {
            Run("title @p times 10 160 20"); Run("title @p subtitle {\"text\":\"Selektory, scoreboard i blok poleceń\",\"color\":\"yellow\"}");
            Run("title @p title {\"text\":\"Minecraft 1.8\",\"color\":\"gold\"}");
        });
        Step(45, () => Shot("cmd_02_title"));
        Step(5, () =>
        {
            Run("title @p clear"); Run($"worldborder center {x}.5 {z}.5"); Run("worldborder set 32");
            Player.SetPositionAndPrev(x + 12.5, 6, z - 5.5); Player.Yaw = Player.PrevYaw = -55; Player.Pitch = Player.PrevPitch = 8;
        });
        Step(25, () => Shot("cmd_03_granica"));
        Step(5, () =>
        {
            Run("worldborder set 60000000"); Player.SetPositionAndPrev(x + .5, 4, z - 3.5);
            Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 5;
            if (!CommandBlocks.Open(Player, x, 4, z)) throw new InvalidOperationException("CMD-TEST FAIL edytor");
        });
        Step(15, () => Shot("cmd_04_edytor"));
        Step(5, () =>
        {
            SetScreen(null); World.SetBlock(x + 1, 4, z, B.RedstoneBlock);
        });
        Step(15, () =>
        {
            bool ok = ((CommandBlockTile)World.GetTileEntity(x, 4, z)).Logic.SuccessCount == 1;
            Log.Info("CMD-TEST " + (ok ? "OK" : "FAIL") + " impuls redstone");
            Shot("cmd_05_impuls");
        });
        Step(5, () => { Log.Info("CMD-TEST KONIEC"); Log.Info("AUTOTEST DONE"); Close(); });
    }
}
