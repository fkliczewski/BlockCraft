using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Podgląd paczek zasobów do ręcznego porównania: BlockCraft.exe --showcase --packs-preview [--resourcepack NAZWA].
/// Zrzuty (bez paczki i z paczką) do folderu showcase/: ściana wszystkich bloków, ekwipunek kreatywny, ekran paczek.</summary>
public sealed partial class Game
{
    private void SetupPacksPreview()
    {
        _shotDir = Path.Combine(AppContext.BaseDirectory, "showcase");
        if (Directory.Exists(_shotDir)) Directory.Delete(_shotDir, true);
        Directory.CreateDirectory(_shotDir);
        var packs = _packOverride is { Count: > 0 } ? _packOverride.ToList() : new List<string> { ResourcePacks.BuiltinId };
        _packOverride = new List<string>();
        _reloadPacks = true;
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(10, () => SetScreen(new ResourcePackScreen(new TitleScreen(), packs)));
        Step(10, () => Snap("packs_screen"));
        Step(5, () => CreateWorld("Paczki " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        int bx = 0, by = 0, bz = 0;
        Step(30, () =>
        {
            World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false; World.DayTime = 6000;
            Player.Invulnerable = true; Settings.ShowFps = false;
            (bx, by, bz) = Stage(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z), 0, 50, 20, 9);
            // ściana wszystkich bloków: rzędy po 16, co drugi blok, na trawie przed graczem (+Z)
            int n = 0;
            for (int id = 1; id < 256; id++)
            {
                var b = Block.ById[id];
                if (b == null || b.Id == 0 || b.IsLiquid) continue;
                int x = bx - 15 + n % 16 * 2, z = bz + 3 + n / 16 * 2;
                World.SetBlock(x, by, z, (byte)id);
                n++;
            }
            HideGui = true;
        });
        foreach (bool withPack in new[] { false, true })
        {
            string tag = withPack ? "pack" : "base";
            Step(5, () => { if (withPack) { _packOverride = packs; _reloadPacks = true; } });
            WaitForMeshes(200);
            Step(20, () => Snap("blocks_" + tag));
            Step(5, () => { Player.Pitch = Player.PrevPitch = 20; Player.Yaw = Player.PrevYaw = 0; });
            Step(20, () => Snap("blocks_low_" + tag));
            Step(5, () => { Player.Pitch = Player.PrevPitch = 50; HideGui = false; Cmds.Execute(Player, "/gamemode creative"); });
            foreach (int tab in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 10, 11 })
            {
                int t = tab;
                Step(8, () => { _chat.Clear(); SetScreen(new CreativeScreen(Player, t)); });
                Step(8, () => Snap($"creative_{t:D2}_{tag}"));
            }
            Step(5, () => { SetScreen(null); HideGui = true; });
        }
        Step(5, () => { Log.Info("PACKS PREVIEW DONE"); Close(); });
    }
}
