using BlockCraft.Core;
using BlockCraft.World;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

public sealed class TitleScreen : Screen
{
    private static readonly string[] Splashes =
    {
        "Teraz w C#!", "100% proceduralne!", "Kopiesz w dół!", "Nie kop w dół!", "Creeper? Aww man", "Kwadratowe słońce!", "Zrobione w .NET!",
        "Diamenty!", "Pieczony schab!", "Pszenica rośnie!", "Także spróbuj Terrarii!", "Ssss...", "Polska wersja!", "Teraz z redstone!", "Bloki!", "Endermany!"
    };
    private string _splash;
    public override bool PausesGame => false;
    protected override void Init()
    {
        _splash ??= Splashes[new Random().Next(Splashes.Length)];
        if (!_recentChecked)
        {
            _recentChecked = true;
            try { var w = WorldStorage.ListWorlds().FirstOrDefault(); if (w.dir != null) _recent = (w.dir, w.data.Name); }
            catch (Exception ex) { Log.Error("Ostatni świat: " + ex.Message); }
        }
        // jedna główna akcja na górze (ostatni świat), pod nią wybór świata i sieć, rzadkie opcje w małym rzędzie na dole
        int x = W / 2 - 100, y = H / 4 + 48;
        if (_recent is { } r)
        {
            Add(new Button(x, y, 200, 20, "Graj dalej: " + Fit(r.name, 200 - 16 - G.Sb.Measure("Graj dalej: ")), () => G.LoadWorld(r.dir)));
            y += 24;
        }
        Add(new Button(x, y, 200, 20, _recent == null ? "Zagraj..." : "Światy...", () => G.SetScreen(new WorldSelectScreen())));
        Add(new Button(x, y + 24, 200, 20, "Gra w sieci...", () => G.SetScreen(new Net.MultiplayerScreen(this))));
        y += 24 + 24 + 12;
        Add(new Button(x, y, 64, 20, "Ustawienia", () => G.SetScreen(new OptionsScreen(this))));
        Add(new Button(x + 68, y, 64, 20, "Pomoc", () => G.SetScreen(new HelpScreen(this))));
        Add(new Button(x + 136, y, 64, 20, "Wyjdź", () => G.Quit()));
    }

    /// <summary>Ostatnio grany świat (do „Graj dalej”); sprawdzany raz na otwarcie menu.</summary>
    private (string dir, string name)? _recent;
    private bool _recentChecked;

    /// <summary>Tekst skrócony wielokropkiem do szerokości.</summary>
    private string Fit(string text, int width)
    {
        if (G.Sb.Measure(text) <= width) return text;
        while (text.Length > 1 && G.Sb.Measure(text + "...") > width) text = text[..^1];
        return text + "...";
    }
    protected override void Escape() { }
    public override void Render(int mx, int my, float pt)
    {
        var sb = G.Sb;
        if (!G.HasMenuWorld) DrawDirtBackground();
        sb.Gradient(0, 0, W, H, 0x40000000, 0x80000000);
        float lw = GuiTextures.LogoW, lh = GuiTextures.LogoH;
        float scale = Math.Min(1f, W * 0.72f / lw);
        float lx = W / 2f - lw * scale / 2, ly = 30;
        sb.Draw(G.Gui.Logo, lx, ly, lw * scale, lh * scale);
        sb.TextCentered(".NET/C# EDITION", W / 2f, ly + lh * scale + 2, 0xFFE0E0E0);
        float t = (float)(Environment.TickCount64 % 1000) / 1000f * MathF.PI * 2;
        float s = 1.8f - MathF.Abs(MathF.Sin(t)) * 0.1f;
        s = s * 100f / (sb.Measure(_splash) + 32);
        sb.PushTransform(lx + lw * scale - 20, ly + lh * scale - 6, -20, s);
        sb.TextCentered(_splash, 0, -8, 0xFF00FFFF);
        sb.PopTransform();
        sb.Text("BlockCraft " + GameVersion.Name + " (.NET)", 2, H - 10, 0xFFFFFFFF);
        var ast = BlockCraft.Audio.AssetStore.Status;
        if (!string.IsNullOrEmpty(ast)) sb.Text(ast + (BlockCraft.Audio.AssetStore.Done ? "" : " (" + (int)(BlockCraft.Audio.AssetStore.Progress * 100) + "%)"), 2, H - 20, 0xFFA0A0A0);
        string c = "Nie jest powiązane z Mojang AB.";
        sb.Text(c, W - sb.Measure(c) - 2, H - 10, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }
}

public sealed class HelpScreen : Screen
{
    private readonly Screen _parent;
    public HelpScreen(Screen p) { _parent = p; }
    public override bool PausesGame => false;
    protected override void Init() { Add(new Button(W / 2 - 100, H - 30, 200, 20, "Gotowe", () => G.SetScreen(_parent))); }
    protected override void Escape() => G.SetScreen(_parent);
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        sb.TextCentered("BlockCraft - sterowanie", W / 2f, 15, 0xFFFFFFFF);
        string[] lines =
        {
            "WASD - ruch, Spacja - skok (2x w kreatywnym: latanie)", "Shift - kucanie (trzymanie krawędzi), Ctrl lub 2x W - sprint",
            "LPM - kopanie / atak, PPM - stawianie / używanie, ŚPM - wybierz blok", "1-9 / kółko - pasek szybkiego wyboru, Q - wyrzuć przedmiot",
            "E - ekwipunek, T - czat, / - komenda, Esc - pauza", "F1 - ukryj HUD, F2 - zrzut ekranu, F3 - debug, F5 - kamera, F11 - pełny ekran",
            "", "Komendy: /help, /time set day, /gamemode creative, /give @p diamond 64,", "/tp x y z, /summon zombie, /kill, /gamerule keepInventory true",
        };
        for (int i = 0; i < lines.Length; i++) sb.TextCentered(lines[i], W / 2f, 40 + i * 12, 0xFFE0E0E0);
        base.Render(mx, my, pt);
    }
}

public sealed class WorldSelectScreen : Screen, IPadLists
{
    private List<(string dir, LevelData data)> _worlds = new();
    private int _sel = -1; private float _scroll;
    private Button _play, _del;
    private bool _confirmDelete;
    public override bool PausesGame => false;
    protected override void Init()
    {
        _worlds = WorldStorage.ListWorlds();
        if (_sel >= _worlds.Count) _sel = -1;
        _play = Add(new Button(W / 2 - 154, H - 52, 150, 20, "Graj w wybranym świecie", () => { if (_sel >= 0) G.LoadWorld(_worlds[_sel].dir); }));
        Add(new Button(W / 2 + 4, H - 52, 150, 20, "Stwórz nowy świat", () => G.SetScreen(new CreateWorldScreen(this))));
        _del = Add(new Button(W / 2 - 154, H - 28, 72, 20, () => _confirmDelete ? "Na pewno?" : "Usuń", () =>
        {
            if (_sel < 0) return;
            if (!_confirmDelete) { _confirmDelete = true; return; }
            try { Directory.Delete(_worlds[_sel].dir, true); } catch (Exception ex) { Log.Error(ex.Message); }
            _sel = -1; _confirmDelete = false; G.SetScreen(this);
        }));
        Add(new Button(W / 2 + 82, H - 28, 72, 20, "Anuluj", () => G.SetScreen(new TitleScreen())));
    }
    protected override void Escape() => G.SetScreen(new TitleScreen());
    private int ListTop => 32; private int ListBottom => H - 64;
    private const int RowOffset = 4, RowH = 36, ItemH = 30;
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        sb.Fill(0, ListTop, W, ListBottom - ListTop, 0xC0000000);
        sb.TextCentered("Wybierz świat", W / 2f, 12, 0xFFFFFFFF);
        _play.Enabled = _sel >= 0; _del.Enabled = _sel >= 0;
        if (_worlds.Count == 0) sb.TextCentered("Brak światów - stwórz nowy!", W / 2f, ListTop + 20, 0xFFA0A0A0);
        for (int i = 0; i < _worlds.Count; i++)
        {
            if (!RowVisible(i)) continue; // tylko całe wiersze: przewinięte nie nachodzą na tytuł i przyciski
            int x = W / 2 - 110, y = RowY(i);
            var d = _worlds[i].data;
            if (i == _sel) { sb.Fill(x - 2, y - 2, 224, 34, 0xFF808080); sb.Fill(x - 1, y - 1, 222, 32, 0xFF000000); }
            string mode = d.Hardcore ? "§cHardkor" : d.GameMode == 1 ? "Kreatywny" : "Przetrwanie";
            sb.Text(d.Name, x + 2, y + 1, 0xFFFFFFFF);
            sb.Text($"{Path.GetFileName(_worlds[i].dir)} ({d.LastPlayed:dd.MM.yyyy HH:mm})", x + 2, y + 12, 0xFF808080);
            sb.Text(mode + (d.Type == 1 ? ", Superpłaski" : "") + (d.AllowCheats ? ", Komendy" : ""), x + 2, y + 22, 0xFF808080);
        }
        base.Render(mx, my, pt);
    }
    public override void MouseDown(int mx, int my, int btn)
    {
        if (my > ListTop && my < ListBottom && mx > W / 2 - 112 && mx < W / 2 + 112)
        {
            int i = (int)((my - ListTop - RowOffset + _scroll) / RowH);
            if (i >= 0 && i < _worlds.Count && RowVisible(i))
            {
                if (i == _sel && btn == 0 && (Environment.TickCount64 - _lastClick) < 400) { G.LoadWorld(_worlds[i].dir); return; }
                _sel = i; _confirmDelete = false; _lastClick = Environment.TickCount64; return;
            }
        }
        base.MouseDown(mx, my, btn);
    }
    private long _lastClick;
    public override void Scroll(int mx, int my, float d) { _scroll = Math.Clamp(_scroll - d * 20, 0, MaxScroll); }
    private float MaxScroll => Math.Max(0, _worlds.Count * RowH - (ListBottom - ListTop) + 2 * RowOffset);
    private bool RowVisible(int i) => RowY(i) >= ListTop && RowY(i) + ItemH <= ListBottom;
    private int RowY(int i) => ListTop + RowOffset + i * RowH - (int)_scroll;
    public override IEnumerable<(int x, int y)> PadTargets() => this.ListTargets().Concat(base.PadTargets());

    public IEnumerable<PadList> PadLists()
    {
        yield return new PadList
        {
            Left = W / 2 - 112, Width = 224, Top = ListTop, Bottom = ListBottom, Offset = RowOffset, RowH = RowH, ItemH = ItemH, Count = _worlds.Count,
            MaxScroll = MaxScroll, GetScroll = () => _scroll, SetScroll = v => _scroll = v,
        };
    }
}

public sealed class CreateWorldScreen : Screen
{
    private readonly Screen _parent;
    private TextField _name, _seed;
    private int _mode; // 0 przetrwanie, 1 kreatywny, 2 hardkor
    private int _type;
    private bool _cheats;
    public CreateWorldScreen(Screen p) { _parent = p; }
    public override bool PausesGame => false;
    protected override void Init()
    {
        string oldName = _name?.Text ?? "Nowy świat", oldSeed = _seed?.Text ?? "";
        _name = Add(new TextField(W / 2 - 100, 50, 200, 20) { Text = oldName, Focused = true, MaxLength = 32 });
        _seed = Add(new TextField(W / 2 - 100, 92, 200, 20) { Text = oldSeed, Placeholder = "(losowe)", MaxLength = 32 });
        Add(new Button(W / 2 - 155, 124, 150, 20, () => "Tryb gry: " + (_mode switch { 0 => "Przetrwanie", 1 => "Kreatywny", _ => "Hardkor" }),
            () => { _mode = (_mode + 1) % 3; _cheats = _mode == 1; }));
        Add(new Button(W / 2 + 5, 124, 150, 20, () => "Typ świata: " + (_type switch { 0 => "Domyślny", 1 => "Superpłaski", 2 => "Duże biomy", _ => "Amplified" }), () => _type = (_type + 1) % 4));
        Add(new Button(W / 2 - 155, 148, 150, 20, () => "Komendy: " + (_cheats ? "TAK" : "NIE"), () => _cheats = !_cheats) );
        Add(new Button(W / 2 - 155, H - 28, 150, 20, "Stwórz nowy świat", Create));
        Add(new Button(W / 2 + 5, H - 28, 150, 20, "Anuluj", () => G.SetScreen(_parent)));
        _name.OnEnter = Create;
    }
    protected override void Escape() => G.SetScreen(_parent);
    private void Create()
    {
        long seed;
        string s = _seed.Text.Trim();
        if (s.Length == 0) seed = Random.Shared.NextInt64();
        else if (!long.TryParse(s, out seed)) { seed = 0; foreach (char c in s) seed = seed * 31 + c; }
        G.CreateWorld(string.IsNullOrWhiteSpace(_name.Text) ? "Nowy świat" : _name.Text.Trim(), seed, _mode == 1 ? 1 : 0, _mode == 2, (WorldType)_type, _cheats);
    }
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        sb.TextCentered("Stwórz nowy świat", W / 2f, 15, 0xFFFFFFFF);
        sb.Text("Nazwa świata", W / 2 - 100, 38, 0xFFA0A0A0);
        sb.Text("Ziarno generatora świata", W / 2 - 100, 80, 0xFFA0A0A0);
        string desc = _mode switch { 0 => "Szukaj surowców, buduj, walcz i przetrwaj.", 1 => "Nieograniczone zasoby, latanie, natychmiastowe niszczenie.", _ => "Jak przetrwanie, ale masz tylko jedno życie!" };
        sb.TextCentered(desc, W / 2f, 176, 0xFFA0A0A0);
        if (_type == 1) sb.TextCentered("Całkowicie płaski świat: skała, 2x ziemia, trawa.", W / 2f, 188, 0xFFA0A0A0);
        base.Render(mx, my, pt);
    }
}

/// <summary>Ekran ustawień z kategoriami: kolumna kategorii po lewej, w panelu najwyżej 6 opcji jednej kategorii
/// w jednej kolumnie i opis opcji pod kursorem. Na padzie kategorie przełączają LB/RB.</summary>
public abstract class TabbedOptionsScreen : Screen, IPadTabs
{
    protected readonly Screen Parent;
    protected int Tab;
    private readonly string _title;
    private readonly string[] _tabNames;
    /// <summary>Limit rozdzielczości tekstur paczek przy otwarciu ekranu (zmiana = przeładowanie atlasu).</summary>
    private int? _packTexSize;

    private const int RailW = 96, Gap = 8, PanelW = 216, RowH = 24, Rows = 6;
    private int Left => W / 2 - (RailW + Gap + PanelW) / 2;
    private int PanelX => Left + RailW + Gap;
    private int Top => Math.Max(30, (H - 240) / 2 + 30);
    private int PanelH => Rows * RowH + 4;
    private int _rowY;

    protected TabbedOptionsScreen(Screen parent, string title, string[] tabNames, int tab)
    {
        Parent = parent; _title = title; _tabNames = tabNames; Tab = Math.Clamp(tab, 0, tabNames.Length - 1);
    }
    public override bool PausesGame => true;

    protected override void Init()
    {
        _packTexSize ??= G.Settings.MaxPackTextureSize;
        for (int i = 0; i < _tabNames.Length; i++)
        {
            int t = i;
            Add(new TabButton(Left, Top + i * RowH, RailW, 20, _tabNames[i], t == Tab, () => Select(t)));
        }
        _rowY = Top + 4;
        BuildTab(Tab);
        Add(new Button(W / 2 - 100, H - 28, 200, 20, "Gotowe", Done));
    }

    protected virtual string Title => _title;

    /// <summary>Opcje wybranej kategorii (przez Row, OptionButton, OptionSlider).</summary>
    protected abstract void BuildTab(int tab);
    protected virtual void OnTabSelected(int tab) { }

    protected T Row<T>(T widget, string hint) where T : Widget
    {
        widget.X = PanelX + 8; widget.Y = _rowY; widget.W = PanelW - 16; widget.H = 20; widget.Hint = hint;
        _rowY += RowH;
        return Add(widget);
    }
    protected Button OptionButton(Func<string> text, Action click, string hint) => Row(new Button(0, 0, 0, 0, text, click), hint);
    protected Slider OptionSlider(float value, Func<float, string> label, Action<float> change, string hint, int steps = 0) =>
        Row(new Slider(0, 0, 0, 0, value, label, change, steps), hint);
    protected static string YesNo(bool v) => v ? "TAK" : "NIE";

    private void Select(int t) { Tab = t; OnTabSelected(t); Rebuild(); }
    protected void Rebuild() { Widgets.Clear(); Init(); }

    public void PadTab(int dir)
    {
        G.PlayUiClick();
        Select((Tab + dir + _tabNames.Length) % _tabNames.Length);
    }

    /// <summary>Pad: kursor na wybranej kategorii.</summary>
    public override (int x, int y)? PadStart() => (Left + RailW / 2, Top + Tab * RowH + 10);

    protected void Done()
    {
        G.Settings.Save(); G.ApplySettings();
        RequestPackReloadIfLimitChanged();
        G.SetScreen(Parent);
    }

    /// <summary>Limit rozdzielczości paczek zmieniony od otwarcia ekranu: przeładowanie atlasu przed następną klatką.</summary>
    protected void RequestPackReloadIfLimitChanged()
    {
        if (_packTexSize != G.Settings.MaxPackTextureSize) G.RequestPackReload();
        _packTexSize = G.Settings.MaxPackTextureSize;
    }

    protected override void Escape() => Done();

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        G.Sb.TextCentered(Title, W / 2f, Top - 18, 0xFFFFFFFF);
        OptionsPanel.Draw(G, PanelX, Top - 2, PanelW, PanelH);
        base.Render(mx, my, pt);
        OptionsPanel.Hint(G, WidgetAt(mx, my)?.Hint, W / 2f, Top + PanelH + 4, RailW + Gap + PanelW);
    }
}

/// <summary>Ustawienia: Gra, Grafika (profil + jasność + własne), Dźwięk, Sterowanie, Paczki i język.</summary>
public sealed class OptionsScreen : TabbedOptionsScreen
{
    public enum Section { Game, Graphics, Sound, Controls, Packs }
    private static readonly string[] Names = { "Gra", "Grafika", "Dźwięk", "Sterowanie", "Paczki i język" };
    private static Section _last = Section.Game;

    public OptionsScreen(Screen p, Section? tab = null) : base(p, "Ustawienia", Names, (int)(tab ?? _last)) { }
    protected override void OnTabSelected(int tab) => _last = (Section)tab;

    protected override void BuildTab(int tab)
    {
        var s = G.Settings;
        switch ((Section)tab)
        {
            case Section.Game:
                OptionButton(() => "Poziom trudności: " + DiffName(G.CurrentDifficulty), () => { G.CurrentDifficulty = (G.CurrentDifficulty + 1) % 4; s.Difficulty = G.CurrentDifficulty; },
                    "Pokojowy: bez potworów. Trudny: silniejsze potwory, a głód może zabić.").Enabled = (G.World == null || !G.World.Hardcore) && G.Client == null; // gość: trudność ustala host
                OptionSlider((s.Fov - 30) / 80f, v => "Pole widzenia: " + (Math.Abs(30 + v * 80 - 70) < 0.5f ? "Normalne" : ((int)(30 + v * 80)).ToString()), v => s.Fov = 30 + v * 80,
                    "Jak szeroko widzisz świat. Normalne to 70 stopni.");
                OptionButton(() => "Kołysanie widoku: " + YesNo(s.ViewBobbing), () => s.ViewBobbing = !s.ViewBobbing, "Kamera kołysze się przy chodzeniu.");
                OptionButton(() => "Skala menu: " + (s.GuiScale == 0 ? "Auto" : s.GuiScale.ToString()), () => { s.GuiScale = (s.GuiScale + 1) % 5; G.ApplySettings(); },
                    "Wielkość menu, napisów i paska przedmiotów.");
                OptionButton(() => "Licznik FPS: " + YesNo(s.ShowFps), () => s.ShowFps = !s.ShowFps, "Liczba klatek na sekundę w rogu ekranu.");
                break;
            case Section.Graphics:
                Button profile = null;
                profile = OptionButton(() => "Profil grafiki: " + GraphicsQuality.Name(GraphicsQuality.Match(s)), () =>
                {
                    GraphicsQuality.Apply(s, GraphicsQuality.Next(GraphicsQuality.Match(s)));
                    G.ApplySettings();
                    profile.Hint = GraphicsQuality.Description(GraphicsQuality.Match(s));
                }, GraphicsQuality.Description(GraphicsQuality.Match(s)));
                OptionSlider(s.Brightness, v => "Jasność: " + (v < 0.01f ? "Ciemno" : v > 0.99f ? "Jasno" : "+" + (int)(v * 100) + "%"), v => s.Brightness = v,
                    "Rozjaśnia ciemne miejsca na ekranie; światło w świecie się nie zmienia.");
                OptionButton(() => "Własne ustawienia...", () => G.SetScreen(new GraphicsCustomScreen(this)),
                    "Wszystkie opcje grafiki osobno: jakość świata, efekty i wydajność.");
                break;
            case Section.Sound:
                OptionSlider(Sound.CategoryVolume("master"), v => "Głośność główna: " + Pct(v), v => Sound.SetCategoryVolume("master", v), "Wszystkie dźwięki i muzyka.");
                OptionSlider(Sound.CategoryVolume("music"), v => "Muzyka: " + Pct(v), v => Sound.SetCategoryVolume("music", v), "Muzyka w tle.");
                OptionButton(() => "Pozostałe głośności...", () => G.SetScreen(new BlockCraft.Audio.SoundOptionsScreen(this)), "Osobno: pogoda, bloki, stworzenia, gracze i otoczenie.");
                break;
            case Section.Controls:
                OptionSlider(s.Sensitivity, v => "Czułość myszy: " + (int)(v * 200) + "%", v => s.Sensitivity = v, "Jak szybko obraca się kamera przy ruchu myszą.");
                OptionButton(() => "Odwróć mysz: " + YesNo(s.InvertMouse), () => s.InvertMouse = !s.InvertMouse, "Gdy TAK: ruch myszą w górę patrzy w dół.");
                OptionButton(() => "Pad...", () => G.SetScreen(new ControllerScreen(this)), "Czułość, martwa strefa, wibracje, ikony i przypisania przycisków pada.");
                OptionButton(() => "Klawisze...", () => G.SetScreen(new ControlsScreen(this)), "Lista klawiszy klawiatury i myszy.");
                break;
            case Section.Packs:
                OptionButton(() => "Paczki zasobów...", () => G.SetScreen(new ResourcePackScreen(this, G.ActivePackIds)), "Tekstury, dźwięki i modele z paczek Minecrafta 1.8.");
                OptionButton(() => "Język nazw przedmiotów...", () => G.SetScreen(new LanguageScreen(this)), "Nazwy przedmiotów z plików językowych 1.8; menu zostaje po polsku.");
                break;
        }
    }

    private static string Pct(float v) => v <= 0.001f ? "WYŁ." : (int)Math.Round(v * 100) + "%";
    public static string DiffName(int d) => d switch { 0 => "Pokojowy", 1 => "Łatwy", 2 => "Normalny", _ => "Trudny" };
}

/// <summary>Ustawienia → Grafika → „Własne ustawienia...”: wszystkie opcje grafiki osobno (profil zmienia się wtedy na Własna).</summary>
public sealed class GraphicsCustomScreen : TabbedOptionsScreen
{
    private static readonly string[] Names = { "Świat", "Efekty", "Wydajność" };
    private static int _last;

    public GraphicsCustomScreen(Screen parent, int? tab = null) : base(parent, "Własne ustawienia grafiki", Names, tab ?? _last) { }
    protected override void OnTabSelected(int tab) => _last = tab;

    protected override void BuildTab(int tab)
    {
        var s = G.Settings;
        switch (tab)
        {
            case 0:
                OptionButton(() => "Jakość: " + (s.FancyGraphics ? "Dokładna" : "Szybka"), () => s.FancyGraphics = !s.FancyGraphics,
                    "Szybka: liście bez prześwitów i prostsze efekty, więcej klatek.");
                OptionButton(() => "Wygładzone światło: " + YesNo(s.SmoothLighting), () => s.SmoothLighting = !s.SmoothLighting, "Miękkie cienie w narożnikach bloków.");
                OptionButton(() => "Chmury: " + YesNo(s.Clouds), () => s.Clouds = !s.Clouds, "Chmury na niebie.");
                OptionSlider((s.RenderDistance - 2) / 14f, v => "Zasięg widzenia: " + (int)Math.Round(2 + v * 14) + " chunków", v => s.RenderDistance = (int)Math.Round(2 + v * 14),
                    "Jak daleko widać świat. Mniejszy zasięg to więcej klatek.", 14);
                OptionButton(() => "Dalekie postacie: " + YesNo(s.FarEntities), () => s.FarEntities = !s.FarEntities, "Zwierzęta i potwory widoczne aż do granicy mgły.");
                break;
            case 1:
                OptionButton(() => "Kolory HDR: " + YesNo(s.Tonemapping), () => { s.Tonemapping = !s.Tonemapping; EffectsChanged(); },
                    "Jasne niebo i światło bez przepalonych, płaskich plam (tonemapping).");
                OptionButton(() => "Żywe kolory: " + YesNo(s.VividColors), () => { s.VividColors = !s.VividColors; EffectsChanged(); },
                    "Mocniejsze kolory, ciepłe światło i lekko przyciemnione rogi ekranu.");
                OptionButton(() => "Wygładzanie krawędzi: " + YesNo(s.Fxaa), () => { s.Fxaa = !s.Fxaa; EffectsChanged(); }, "Mniej postrzępione krawędzie bloków (FXAA).");
                OptionButton(() => "Falujące rośliny: " + YesNo(s.WavingPlants), () => { s.WavingPlants = !s.WavingPlants; EffectsChanged(); }, "Liście, trawa i woda poruszają się na wietrze.");
                break;
            case 2:
                if (G.GpuPerformanceAlternative is { } performanceGpu)
                    OptionButton(() => "GPU: wydajność...", () => G.SetScreen(new GpuChoiceScreen(this, G.GlRenderer, performanceGpu)),
                        "Przełącz na " + GpuChoiceScreen.Short(performanceGpu.Name) + ": więcej klatek (jeden restart gry).");
                if (G.GpuPowerSavingAlternative is { } powerSavingGpu)
                    OptionButton(() => "GPU: oszczędzanie baterii...", () => G.SetScreen(new GpuChoiceScreen(this, G.GlRenderer, powerSavingGpu)),
                        "Przełącz na " + GpuChoiceScreen.Short(powerSavingGpu.Name) + ": mniejsze zużycie energii (jeden restart gry).");
                OptionSlider((s.MaxFps - 30) / 230f, v => "Maks. FPS: " + (v >= 0.99f ? "Bez limitu" : ((int)(30 + v * 230)).ToString()), v => s.MaxFps = v >= 0.99f ? 1000 : (int)(30 + v * 230),
                    "Górny limit klatek na sekundę; niższy oszczędza baterię.");
                OptionButton(() => "Synchronizacja V: " + YesNo(s.VSync), () => { s.VSync = !s.VSync; G.ApplySettings(); }, "Klatki równo z odświeżaniem monitora, bez rozrywania obrazu.");
                OptionSlider((s.RenderScale - 25) / 75f, v => "Skala renderu 3D: " + (25 + (int)Math.Round(v * 15) * 5) + "%", v => s.RenderScale = 25 + (int)Math.Round(v * 15) * 5,
                    "Niższa rozdzielczość samego świata dla słabszych kart; menu zostaje ostre.", 15);
                OptionButton(() => "Tekstury paczek: do " + s.MaxPackTextureSize + "x", () => s.MaxPackTextureSize = s.MaxPackTextureSize >= 128 ? 16 : Math.Max(16, s.MaxPackTextureSize * 2),
                    "Największa rozdzielczość tekstur z paczek; większe są zmniejszane.");
                break;
        }
    }

    /// <summary>Efekty widać od razu w tle; preset efektów dopasowany do zmian (albo Własna).</summary>
    private void EffectsChanged() { G.Settings.GraphicsPreset = GraphicsPresets.Match(G.Settings); G.ApplySettings(); }

    protected override string Title => "Własne ustawienia grafiki (profil: " + GraphicsQuality.Name(GraphicsQuality.Match(G.Settings)) + ")";
}

public sealed class ControlsScreen : Screen
{
    private readonly Screen _parent;
    public ControlsScreen(Screen p) { _parent = p; }
    protected override void Init()
    {
        var s = G.Settings;
        Add(new Slider(W / 2 - 155, 30, 150, 20, s.Sensitivity, v => "Czułość myszy: " + (int)(v * 200) + "%", v => s.Sensitivity = v));
        Add(new Button(W / 2 + 5, 30, 150, 20, () => "Odwróć mysz: " + (s.InvertMouse ? "TAK" : "NIE"), () => s.InvertMouse = !s.InvertMouse));
        Add(new Button(W / 2 - 155, H - 28, 150, 20, "Kontroler...", () => G.SetScreen(new ControllerScreen(this))));
        Add(new Button(W / 2 + 5, H - 28, 150, 20, "Gotowe", () => { s.Save(); G.SetScreen(_parent); }));
    }
    protected override void Escape() { G.Settings.Save(); G.SetScreen(_parent); }
    private static readonly (string, string)[] Keys_ =
    {
        ("Ruch do przodu / tyłu", "W / S"), ("Ruch w lewo / prawo", "A / D"), ("Skok / latanie w górę", "Spacja"), ("Kucanie / w dół", "Lewy Shift"),
        ("Sprint", "Lewy Ctrl"), ("Ekwipunek", "E"), ("Wyrzuć przedmiot", "Q"), ("Czat / komenda", "T / /"), ("Atak / niszczenie", "LPM"),
        ("Użyj / postaw", "PPM"), ("Wybierz blok", "ŚPM"), ("Perspektywa", "F5"), ("Ukryj HUD", "F1"), ("Debug", "F3"), ("Zrzut ekranu", "F2"),
    };
    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Sterowanie", W / 2f, 12, 0xFFFFFFFF);
        for (int i = 0; i < Keys_.Length; i++)
        {
            int y = 60 + i * 11;
            if (y > H - 40) break;
            sb.Text(Keys_[i].Item1, W / 2 - 150, y, 0xFFE0E0E0);
            sb.Text(Keys_[i].Item2, W / 2 + 60, y, 0xFFFFFF55);
        }
        base.Render(mx, my, pt);
    }
}

public sealed class PauseScreen : Screen
{
    protected override void Init()
    {
        // powrót do gry na górze; rzadkie ekrany w małym rzędzie; wyjście oddzielone odstępem, żeby nie trafić w nie przypadkiem
        int x = W / 2 - 100, y = H / 4 + 16;
        Add(new Button(x, y, 200, 20, "Wróć do gry", () => G.SetScreen(null)));
        y += 24;
        Add(new Button(x, y, 200, 20, "Ustawienia...", () => G.SetScreen(new OptionsScreen(this))));
        y += 24;
        if (G.Client == null)
        {
            Add(new Button(x, y, 200, 20, () => G.HostLink != null ? "Gra w sieci (otwarta)" : "Otwórz w sieci...", () => G.SetScreen(new Net.NetHostScreen(this))));
            y += 24;
        }
        Add(new Button(x, y, 64, 20, "Osiągnięcia", () => G.SetScreen(new AchievementsScreen(this))));
        Add(new Button(x + 68, y, 64, 20, "Statystyki", () => G.SetScreen(new StatsScreen(this))));
        Add(new Button(x + 136, y, 64, 20, "Pomoc", () => G.SetScreen(new HelpScreen(this))));
        y += 24 + 16;
        Add(new Button(x, y, 200, 20, G.Client != null ? "Rozłącz się" : "Zapisz i wyjdź do menu", () => G.QuitToTitle()));
    }
    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        G.Sb.TextCentered("Menu gry", W / 2f, 40, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }
}

public sealed class DeathScreen : Screen
{
    private readonly string _msg;
    private int _ticks;
    public DeathScreen(string msg) { _msg = msg; }
    public override bool PausesGame => false;
    protected override void Init()
    {
        bool hc = G.World?.Hardcore == true;
        Add(new Button(W / 2 - 100, H / 4 + 72, 200, 20, hc ? "Obserwuj świat" : "Odrodź się", () => G.Respawn()) { Enabled = false });
        Add(new Button(W / 2 - 100, H / 4 + 96, 200, 20, "Menu główne", () => G.QuitToTitle()) { Enabled = false });
    }
    protected override void Escape() { }
    public override void Tick() { if (++_ticks == 20) foreach (var w in Widgets) w.Enabled = true; }
    public override void Render(int mx, int my, float pt)
    {
        G.Sb.Gradient(0, 0, W, H, 0x60000050, 0xA0303080);
        G.Sb.TextCentered(G.World?.Hardcore == true ? "Koniec gry!" : "Umarłeś!", W / 2f, 60, 0xFFFFFFFF, true, 2);
        G.Sb.TextCentered(_msg ?? "", W / 2f, 85, 0xFFFFFFFF);
        G.Sb.TextCentered("Wynik: §e" + (G.Player?.XpTotal ?? 0), W / 2f, 100, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }
}

public sealed class LoadingScreen : Screen
{
    public string Title = "Generowanie terenu", Sub = "";
    public float Progress;
    public override bool PausesGame => false;
    public override bool ShowWorld => false;
    protected override void Escape() { }
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        sb.TextCentered(Title, W / 2f, H / 2f - 30, 0xFFFFFFFF);
        sb.TextCentered(Sub, W / 2f, H / 2f - 14, 0xFFA0A0A0);
        int bw = 100;
        sb.Fill(W / 2f - bw / 2f, H / 2f + 4, bw, 2, 0xFF808080);
        sb.Fill(W / 2f - bw / 2f, H / 2f + 4, bw * Math.Clamp(Progress, 0, 1), 2, 0xFF80FF80);
    }
}
