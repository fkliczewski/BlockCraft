using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Podgląd efektów graficznych do porównania presetów: BlockCraft.exe --showcase --gfx-preview.
/// Te same ujęcia (menu, krajobraz w południe, zachód słońca, bliski plan) w każdym presecie; zrzuty w showcase/.
/// Ustawienia gracza nie są zapisywane.</summary>
public sealed partial class Game
{
    private Settings _gfxPreviewOriginalSettings;
    private float? _gfxPreviewMenuYaw;

    private void SetupGfxPreview()
    {
        _shotDir = Path.Combine(AppContext.BaseDirectory, "showcase");
        if (Directory.Exists(_shotDir)) Directory.Delete(_shotDir, true);
        Directory.CreateDirectory(_shotDir);
        // pusty stan pada: GLFW czyta pad we wszystkich oknach, przyciski z gry obok klikałyby w menu podglądu
        Pad.Inject(new PadState());
        // Warianty pojedynczych efektów korzystają ze wspólnej klasycznej bazy.
        var variants = new (string tag, Action<Settings> enable)[]
        {
            ("classic", s => { }),
            ("tonemapping", s => s.Tonemapping = true),
            ("vivid", s => s.VividColors = true),
            ("fxaa", s => s.Fxaa = true),
            ("waving", s => s.WavingPlants = true),
            ("cacy", s => GraphicsPresets.Apply(s, GraphicsPreset.Cacy)),
        };
        void ApplyVariant(Action<Settings> enable)
        {
            GraphicsPresets.Apply(Settings, GraphicsPreset.Classic);
            enable(Settings);
            ApplySettings();
        }
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        // Jeden kierunek kamery dla wszystkich por?wna? efekt?w w menu.
        Step(1, () => _gfxPreviewMenuYaw = _menuYaw);
        foreach (var (tag, enable) in variants)
        {
            Step(5, () => { ApplyVariant(enable); });
            Step(10, () => Snap("gfx_menu_" + tag));
        }
        Step(1, () => _gfxPreviewMenuYaw = null);
        Step(5, () => CreateWorld("Grafika " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        int gx = 0, gz = 0, gy = 0;
        Step(10, () =>
        {
            World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false;
            Player.Invulnerable = true; Settings.ShowFps = false; HideGui = true; _chat.Clear();
            gx = MathUtil.FloorToInt(Player.X); gz = MathUtil.FloorToInt(Player.Z); gy = GroundY(gx, gz);
        });
        // (nazwa, pora dnia, wysokość nad ziemią, yaw, pitch); yaw 90 = zachód, tam zachodzi słońce
        var shots = new (string name, long time, double lift, float yaw, float pitch)[]
        {
            ("noon", 6000, 24, 30, 18),
            ("sunset", 12300, 24, 90, 8),
            ("close", 6000, 2.2, 200, 12),
        };
        foreach (var (name, time, lift, yaw, pitch) in shots)
        {
            Step(5, () =>
            {
                World.DayTime = time;
                _holdY = gy + 1 + lift; Player.SetPositionAndPrev(gx + 0.5, gy + 1 + lift, gz + 0.5);
                Player.Yaw = Player.PrevYaw = yaw; Player.Pitch = Player.PrevPitch = pitch;
            });
            WaitForMeshes(300);
            foreach (var (tag, enable) in variants)
            {
                Step(5, () => { ApplyVariant(enable); });
                Step(10, () => Snap($"gfx_{name}_{tag}"));
            }
        }
        // falowanie (etap 2): dwa zrzuty bliskiego planu w odstępie ~0,7 s; różnica = ruch liści i trawy
        Step(5, () => { GraphicsPresets.Apply(Settings, GraphicsPreset.Cacy); ApplySettings(); });
        Step(10, () => Snap("gfx_wave_a"));
        Step(14, () => Snap("gfx_wave_b"));
        // woda: najbliższa tafla co najmniej 9x9 w promieniu 128 bloków, kamera nisko nad nią
        int wx = int.MinValue, wy = 0, wz = 0;
        Step(5, () =>
        {
            bool Water(int x, int z) => World.GetChunk(x >> 4, z >> 4) != null && World.GetBlock(x, World.GetHeight(x, z) - 1, z) == B.Water;
            for (int r = 4; r <= 128 && wx == int.MinValue; r += 4)
                for (int dx = -r; dx <= r && wx == int.MinValue; dx += 4)
                    foreach (int dz in new[] { -r, r })
                    {
                        int x = gx + dx, z = gz + dz;
                        if (!Water(x, z) || !Water(x - 4, z - 4) || !Water(x + 4, z - 4) || !Water(x - 4, z + 4) || !Water(x + 4, z + 4)) continue;
                        wx = x; wy = World.GetHeight(x, z) - 1; wz = z; break;
                    }
            Log.Info(wx == int.MinValue ? "GFX PREVIEW: brak wody w pobliżu" : $"GFX PREVIEW: woda {wx} {wy} {wz}");
            if (wx == int.MinValue) return;
            World.DayTime = 6000;
            _holdY = wy + 4; Player.SetPositionAndPrev(wx + 0.5, wy + 4, wz - 3.5);
            Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 25;
        });
        WaitForMeshes(300);
        foreach (var (tag, enable) in variants)
        {
            Step(5, () => { ApplyVariant(enable); });
            Step(10, () => { if (wx != int.MinValue) Snap($"gfx_water_{tag}"); });
        }
        Step(5, () => { Log.Info("GFX PREVIEW DONE"); Close(); });
    }
}
