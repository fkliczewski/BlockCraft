using BlockCraft.Gui;

namespace BlockCraft.Audio;

/// <summary>Ekran "Muzyka i dźwięki..." jak w MC 1.8: suwak Master + suwaki per kategoria.</summary>
public sealed class SoundOptionsScreen : Screen
{
    private readonly Screen _parent;
    public SoundOptionsScreen(Screen p) { _parent = p; }
    public override bool PausesGame => true;

    static readonly (string key, string label)[] Cats =
    {
        ("music", "Muzyka"), ("record", "Płyty i nuty"), ("weather", "Pogoda"), ("block", "Bloki"),
        ("hostile", "Wrogie stworzenia"), ("neutral", "Przyjazne stworzenia"), ("player", "Gracze"), ("ambient", "Otoczenie"),
    };

    static string Pct(float v) => v <= 0.001f ? "WYŁ." : (int)Math.Round(v * 100) + "%";

    protected override void Init()
    {
        int y = H / 6 - 4;
        Add(new Slider(W / 2 - 155, y, 310, 20, Sound.CategoryVolume("master"), v => "Głośność główna: " + Pct(v), v => Sound.SetCategoryVolume("master", v)));
        for (int i = 0; i < Cats.Length; i++)
        {
            var (key, label) = Cats[i];
            int x = W / 2 - 155 + (i % 2) * 160;
            int yy = y + 28 + (i / 2) * 24;
            Add(new Slider(x, yy, 150, 20, Sound.CategoryVolume(key), v => label + ": " + Pct(v), v => Sound.SetCategoryVolume(key, v)));
        }
        Add(new Button(W / 2 - 100, H / 6 + 168, 200, 20, "Gotowe", Done));
    }
    void Done() { G.Settings.Save(); G.SetScreen(_parent); }
    protected override void Escape() => Done();
    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        G.Sb.TextCentered("Muzyka i dźwięki", W / 2f, 15, 0xFFFFFFFF);
        string st = AssetStore.Status;
        if (!string.IsNullOrEmpty(st)) G.Sb.TextCentered(st, W / 2f, H / 6 + 150, 0xFFA0A0A0);
        base.Render(mx, my, pt);
    }
}
