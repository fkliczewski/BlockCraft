using BlockCraft.Core;

namespace BlockCraft.Gui;

/// <summary>Wybór karty graficznej (laptop z dwiema kartami): przy starcie, gdy gra działa na zintegrowanej, a jest mocniejsza,
/// albo z Ustawień (także powrót na zintegrowaną dla baterii). Wybór zapisuje preferencję Windows i restartuje grę.</summary>
public sealed class GpuChoiceScreen : Screen
{
    private readonly Screen _parent;
    private readonly string _current;
    private readonly GpuSwitch.Adapter _target;
    private readonly bool _failed;
    private string _error;
    private bool HighPerformance => _target != null && GpuSwitch.PreferenceFor(_target, G.GpuAdapters) == GpuSwitch.PrefHighPerformance;

    /// <param name="target">karta do przełączenia (null przy <paramref name="failed"/>)</param>
    /// <param name="failed">restart nie zmienił karty: tylko wyjaśnienie</param>
    public GpuChoiceScreen(Screen parent, string current, GpuSwitch.Adapter target, bool failed = false)
    {
        _parent = parent; _current = current; _target = target; _failed = failed;
    }

    public override bool PausesGame => true;

    protected override void Init()
    {
        int y = H / 2 + 24;
        if (_failed || _target == null)
        {
            Add(new Button(W / 2 - 100, y, 200, 20, "OK", Keep));
            return;
        }
        string use = "Użyj: " + Short(_target.Name) + (HighPerformance ? " (zalecane)" : "");
        Add(new Button(W / 2 - 150, y, 300, 20, use, Switch));
        Add(new Button(W / 2 - 150, y + 24, 300, 20, "Zostań przy obecnej", Keep));
    }

    private void Switch()
    {
        G.Settings.GpuTarget = _target.Name;
        G.Settings.GpuChoice = HighPerformance ? "high" : "saving";
        if (!G.SwitchGpu(GpuSwitch.PreferenceFor(_target, G.GpuAdapters)))
            _error = "Nie udało się zapisać ustawienia Windows. Ustaw kartę ręcznie: Ustawienia → System → Ekran → Grafika.";
    }

    private void Keep()
    {
        if (!_failed) { G.Settings.GpuChoice = "keep"; G.Settings.Save(); }
        G.SetScreen(_parent);
    }

    protected override void Escape() => Keep();

    /// <summary>Krótka nazwa na przycisk: "NVIDIA GeForce RTX 5070 Ti Laptop GPU/PCIe/SSE2" → "RTX 5070 Ti",
    /// "AMD Radeon(TM) 610M" → "Radeon 610M" (GL_RENDERER ma dopiski po "/", nazwa producenta jest zbędna).</summary>
    public static string Short(string name)
    {
        string s = (name ?? "").Split('/')[0];
        foreach (var x in new[] { "Laptop GPU", "(TM)", "(R)", "NVIDIA", "GeForce", "AMD", "Intel" }) s = s.Replace(x, "");
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        float y = H / 2 - 70;
        void Line(string s, uint c) { G.Sb.TextCentered(s, W / 2f, y, c); y += 12; }
        if (_failed)
        {
            Line("Nie udało się przełączyć karty graficznej", 0xFFFFFF55);
            y += 6;
            Line("Gra nadal działa na: " + Short(_current), 0xFFFFFFFF);
            Line("System lub sterownik nie zastosował wybranego trybu karty.", 0xFFAAAAAA);
            Line("ASUS: Armoury Crate → Tryb GPU → Standard lub Ultimate.", 0xFFAAAAAA);
            Line("Albo: Ustawienia Windows → System → Ekran → Grafika.", 0xFFAAAAAA);
        }
        else if (HighPerformance)
        {
            Line("Wykryto mocniejszą kartę graficzną", 0xFFFFFF55);
            y += 6;
            Line("Gra działa teraz na: " + Short(_current), 0xFFFFFFFF);
            Line("Dostępna: " + Short(_target.Name) + " — płynniejsza gra i ładniejsza grafika.", 0xFFFFFFFF);
            Line("Na baterii laptop wytrzyma krócej. Gra uruchomi się ponownie.", 0xFFAAAAAA);
        }
        else if (_target != null)
        {
            Line("Karta graficzna", 0xFFFFFF55);
            y += 6;
            Line("Gra działa teraz na: " + Short(_current), 0xFFFFFFFF);
            Line("Karta " + Short(_target.Name) + " oszczędza baterię, ale gra będzie wolniejsza.", 0xFFAAAAAA);
            Line("Gra uruchomi się ponownie.", 0xFFAAAAAA);
        }
        if (_error != null) G.Sb.TextCentered(_error, W / 2f, H / 2 + 76, 0xFFFF5555);
        base.Render(mx, my, pt);
    }
}
