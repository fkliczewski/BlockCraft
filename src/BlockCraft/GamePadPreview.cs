using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Podgląd obsługi pada bez sprzętu: BlockCraft.exe --showcase --pad-preview. Stan pada jest podstawiany
/// (Gamepad.Inject) i przechodzi tę samą drogę co prawdziwy pad; zrzuty w folderze showcase/.</summary>
public sealed partial class Game
{
    private void SetupPadPreview()
    {
        _shotDir = Path.Combine(AppContext.BaseDirectory, "showcase");
        if (Directory.Exists(_shotDir)) Directory.Delete(_shotDir, true);
        Directory.CreateDirectory(_shotDir);
        static PadState Buttons(params PadButton[] b) { var s = new PadState(); foreach (var x in b) s[x] = true; return s; }
        void Tap(params PadButton[] b) { Step(2, () => Pad.Inject(Buttons(b))); Step(3, () => Pad.Inject(new PadState())); }
        void TypeOsk(string text)
        {
            foreach (char ch in text)
            {
                Step(1, () =>
                {
                    if (char.IsUpper(ch) && PadKeyboard.ShiftState == 0) PadKeyboard.ToggleShift();
                    string label = ch == ' ' ? "Spacja" : char.ToLowerInvariant(ch).ToString();
                    if (!PadKeyboard.Select(label)) { PadKeyboard.ToggleSymbols(); PadKeyboard.Select(label); }
                });
                Tap(PadButton.A);
                Step(1, () => { if (PadKeyboard.Symbols) PadKeyboard.ToggleSymbols(); });
            }
        }
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        // menu główne: krzyżak przeskakuje po przyciskach
        Tap(PadButton.DPadDown); Tap(PadButton.DPadDown);
        Step(5, () => Snap("pad_title"));
        // listy: krzyżak chodzi po wierszach i przewija listę (światy, języki, paczki); ikony PlayStation wg nazwy pada
        Step(3, () => SetScreen(new WorldSelectScreen()));
        for (int i = 0; i < 6; i++) Tap(PadButton.DPadDown);
        Step(5, () => Snap("pad_worlds"));
        Step(3, () => { Pad.Inject(new PadState(), "PS5 DualSense Wireless Controller"); SetScreen(new LanguageScreen(new TitleScreen())); });
        for (int i = 0; i < 12; i++) Tap(PadButton.DPadDown);
        Step(5, () => Snap("pad_languages_ps"));
        Step(3, () => _screen?.Scroll(0, 0, -1000)); // koniec listy: cyrylica, greka, chiński z czcionki Unicode
        Step(5, () => Snap("languages_unicode"));
        Step(3, () => SetScreen(new ResourcePackScreen(new TitleScreen(), new List<string> { ResourcePacks.BuiltinId })));
        Tap(PadButton.DPadDown);
        Step(5, () => Snap("pad_packs_ps"));
        Step(3, () => { Pad.Inject(new PadState(), "Pad testowy"); SetScreen(new TitleScreen()); });
        Step(5, () => CreateWorld("Pad " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        Step(10, () => { World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false; World.DayTime = 6000; Player.Invulnerable = true; _chat.Clear(); });
        // w grze: lewa gałka do przodu, prawa obraca kamerę
        double x0 = 0, z0 = 0; float yaw0 = 0;
        Step(2, () => { x0 = Player.X; z0 = Player.Z; yaw0 = Player.Yaw; var s = new PadState { LY = -1f, RX = 0.6f }; Pad.Inject(s); });
        Step(30, () => Pad.Inject(new PadState()));
        Step(5, () =>
        {
            double moved = Math.Sqrt((Player.X - x0) * (Player.X - x0) + (Player.Z - z0) * (Player.Z - z0));
            Log.Info($"PAD PREVIEW: ruch {moved:0.00} bloku, obrót {Player.Yaw - yaw0:0.0} st.");
            Snap("pad_ingame");
        });
        // RB: następny slot, Y: ekwipunek (kreatywny), RB w ekwipunku: zakładka, krzyżak w górę do siatki
        Tap(PadButton.RightBumper);
        Tap(PadButton.Y);
        Step(5, () => Snap("pad_creative"));
        Tap(PadButton.RightBumper);
        Tap(PadButton.DPadUp); Tap(PadButton.DPadUp); Tap(PadButton.DPadRight);
        Step(5, () => Snap("pad_creative_nav"));
        Tap(PadButton.A);
        Step(5, () => Snap("pad_creative_pick"));
        Tap(PadButton.B);
        // Back: czat z klawiaturą ekranową
        Tap(PadButton.Back);
        TypeOsk("Cześć, pad!");
        Step(5, () => Snap("pad_keyboard"));
        Tap(PadButton.Start);
        Step(5, () => Snap("pad_chat_sent"));
        // ustawienia kontrolera z wychylonymi gałkami i wciśniętymi przyciskami (bez A/Y: nie klikają przycisków)
        Step(5, () => SetScreen(new ControllerScreen(new PauseScreen())));
        Step(3, () =>
        {
            var s = Buttons(PadButton.X, PadButton.RightBumper, PadButton.DPadLeft, PadButton.LeftTrigger);
            s.LX = 0.6f; s.LY = 0.3f; s.RX = -0.4f; s.RY = -0.7f; s.LT = 0.8f; s.RT = 0.3f;
            Pad.Inject(s);
        });
        Step(5, () => Snap("pad_settings"));
        Step(3, () => { Pad.Inject(null); SetScreen(new PadBindingsScreen(new PauseScreen())); });
        Step(5, () => Snap("pad_bindings"));
        Step(5, () => { Log.Info("PAD PREVIEW DONE"); Close(); });
    }
}
