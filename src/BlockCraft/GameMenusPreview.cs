using BlockCraft.Core;
using BlockCraft.Gui;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Podgląd menu: BlockCraft.exe --showcase --menus-preview. Zrzuty menu głównego, wszystkich kategorii ustawień
/// (z kursorem na opcji, żeby było widać opis), tworzenia świata i menu gry do folderu showcase/.</summary>
public sealed partial class Game
{
    private void SetupMenusPreview()
    {
        Pad.Inject(new PadState());
        _shotDir = Path.Combine(AppContext.BaseDirectory, "showcase");
        if (Directory.Exists(_shotDir)) Directory.Delete(_shotDir, true);
        Directory.CreateDirectory(_shotDir);
        void HoverWidget(int row)
        {
            // kursor na środku wiersza panelu ustawień (kolumna po prawej) albo dowolnego przycisku
            var targets = _screen?.PadTargets().ToList();
            if (targets == null || targets.Count == 0) return;
            var t = targets[Math.Min(row, targets.Count - 1)];
            _mouseGuiX = t.x; _mouseGuiY = t.y;
        }
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(5, () => { SetScreen(new TitleScreen()); HoverWidget(0); });
        Step(5, () => Snap("menu_title"));
        foreach (OptionsScreen.Section tab in Enum.GetValues<OptionsScreen.Section>())
        {
            var current = tab;
            // cele krzyżaka: najpierw 6 kategorii, potem opcje panelu
            Step(3, () => { SetScreen(new OptionsScreen(new TitleScreen(), current)); HoverWidget(5); });
            Step(4, () => Snap("menu_options_" + current.ToString().ToLowerInvariant()));
        }
        for (int i = 0; i < 3; i++)
        {
            int tab = i;
            // kategorie: Świat, Efekty, Wydajność (3 cele kategorii, potem opcje)
            Step(3, () => { SetScreen(new GraphicsCustomScreen(new OptionsScreen(new TitleScreen()), tab)); HoverWidget(4); });
            Step(4, () => Snap("menu_graphics_custom_" + tab));
        }
        Step(3, () => { SetScreen(new CreateWorldScreen(new TitleScreen())); HoverWidget(2); });
        Step(4, () => Snap("menu_create_world"));
        Step(5, () => CreateWorld("Menu " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        Step(10, () => { _chat.Clear(); SetScreen(new PauseScreen()); HoverWidget(0); });
        Step(5, () => Snap("menu_pause"));
        Step(3, () => { SetScreen(new OptionsScreen(new PauseScreen(), OptionsScreen.Section.Graphics)); HoverWidget(10); });
        Step(4, () => Snap("menu_options_ingame"));
        Step(5, () => { Log.Info("MENUS PREVIEW DONE"); Close(); });
    }
}
