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
        int y = H / 4 + 48;
        Add(new Button(W / 2 - 100, y, 200, 20, "Tryb jednoosobowy", () => G.SetScreen(new WorldSelectScreen())));
        Add(new Button(W / 2 - 100, y + 24, 200, 20, "Tryb wieloosobowy", () => G.SetScreen(new Net.MultiplayerScreen(this))));
        Add(new Button(W / 2 - 100, y + 48, 200, 20, "Twórcy i pomoc", () => G.SetScreen(new HelpScreen(this))));
        Add(new Button(W / 2 - 100, y + 72 + 12, 98, 20, "Ustawienia...", () => G.SetScreen(new OptionsScreen(this))));
        Add(new Button(W / 2 + 2, y + 72 + 12, 98, 20, "Wyjdź z gry", () => G.Quit()));
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
        sb.Text("BlockCraft 1.0 (.NET)", 2, H - 10, 0xFFFFFFFF);
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
        sb.TextCentered("BlockCraft – sterowanie", W / 2f, 15, 0xFFFFFFFF);
        string[] lines =
        {
            "WASD – ruch, Spacja – skok (2x w kreatywnym: latanie)", "Shift – kucanie (trzymanie krawędzi), Ctrl lub 2x W – sprint",
            "LPM – kopanie / atak, PPM – stawianie / używanie, ŚPM – wybierz blok", "1-9 / kółko – pasek szybkiego wyboru, Q – wyrzuć przedmiot",
            "E – ekwipunek, T – czat, / – komenda, Esc – pauza", "F1 – ukryj HUD, F2 – zrzut ekranu, F3 – debug, F5 – kamera, F11 – pełny ekran",
            "", "Komendy: /help, /time set day, /gamemode creative, /give @p diamond 64,", "/tp x y z, /summon zombie, /kill, /gamerule keepInventory true",
        };
        for (int i = 0; i < lines.Length; i++) sb.TextCentered(lines[i], W / 2f, 40 + i * 12, 0xFFE0E0E0);
        base.Render(mx, my, pt);
    }
}

public sealed class WorldSelectScreen : Screen
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
    public override void Render(int mx, int my, float pt)
    {
        DrawDirtBackground();
        var sb = G.Sb;
        sb.Fill(0, ListTop, W, ListBottom - ListTop, 0xC0000000);
        sb.TextCentered("Wybierz świat", W / 2f, 12, 0xFFFFFFFF);
        _play.Enabled = _sel >= 0; _del.Enabled = _sel >= 0;
        if (_worlds.Count == 0) sb.TextCentered("Brak światów – stwórz nowy!", W / 2f, ListTop + 20, 0xFFA0A0A0);
        for (int i = 0; i < _worlds.Count; i++)
        {
            int y = ListTop + 4 + i * 36 - (int)_scroll;
            if (y < ListTop - 30 || y > ListBottom) continue;
            int x = W / 2 - 110;
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
            int i = (int)((my - ListTop - 4 + _scroll) / 36);
            if (i >= 0 && i < _worlds.Count)
            {
                if (i == _sel && btn == 0 && (Environment.TickCount64 - _lastClick) < 400) { G.LoadWorld(_worlds[i].dir); return; }
                _sel = i; _confirmDelete = false; _lastClick = Environment.TickCount64; return;
            }
        }
        base.MouseDown(mx, my, btn);
    }
    private long _lastClick;
    public override void Scroll(int mx, int my, float d) { _scroll = Math.Clamp(_scroll - d * 20, 0, Math.Max(0, _worlds.Count * 36 - (ListBottom - ListTop) + 8)); }
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

public sealed class OptionsScreen : Screen
{
    private readonly Screen _parent;
    public OptionsScreen(Screen p) { _parent = p; }
    public override bool PausesGame => true;
    protected override void Init()
    {
        var s = G.Settings;
        int y = H / 6;
        Add(new Slider(W / 2 - 155, y, 150, 20, (s.Fov - 30) / 80f, v => "Pole widzenia: " + (Math.Abs(30 + v * 80 - 70) < 0.5f ? "Normalne" : ((int)(30 + v * 80)).ToString()), v => { s.Fov = 30 + v * 80; }));
        Add(new Button(W / 2 + 5, y, 150, 20, () => "Poziom trudności: " + DiffName(G.CurrentDifficulty), () =>
        {
            G.CurrentDifficulty = (G.CurrentDifficulty + 1) % 4; s.Difficulty = G.CurrentDifficulty;
        }) { Enabled = G.World == null || !G.World.Hardcore });
        Add(new Button(W / 2 - 155, y + 48, 150, 20, "Ustawienia graficzne...", () => G.SetScreen(new VideoScreen(this))));
        Add(new Button(W / 2 + 5, y + 48, 150, 20, "Sterowanie...", () => G.SetScreen(new ControlsScreen(this))));
        Add(new Slider(W / 2 - 155, y + 72, 150, 20, s.Brightness, v => "Jasność: " + (v < 0.01f ? "Ciemno" : v > 0.99f ? "Jasno" : "+" + (int)(v * 100) + "%"), v => s.Brightness = v));
        Add(new Button(W / 2 + 5, y + 72, 150, 20, () => "Licznik FPS: " + (s.ShowFps ? "TAK" : "NIE"), () => s.ShowFps = !s.ShowFps));
        Add(new Button(W / 2 - 155, y + 24, 150, 20, "Muzyka i dźwięki...", () => G.SetScreen(new BlockCraft.Audio.SoundOptionsScreen(this))));
        Add(new Button(W / 2 - 100, H / 6 + 168, 200, 20, "Gotowe", Done));
    }
    public static string DiffName(int d) => d switch { 0 => "Pokojowy", 1 => "Łatwy", 2 => "Normalny", _ => "Trudny" };
    private void Done() { G.Settings.Save(); G.ApplySettings(); G.SetScreen(_parent); }
    protected override void Escape() => Done();
    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        G.Sb.TextCentered("Ustawienia", W / 2f, 15, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }
}

public sealed class VideoScreen : Screen
{
    private readonly Screen _parent;
    public VideoScreen(Screen p) { _parent = p; }
    protected override void Init()
    {
        var s = G.Settings;
        int y = H / 6; int l = W / 2 - 155, r = W / 2 + 5;
        Add(new Button(l, y, 150, 20, () => "Grafika: " + (s.FancyGraphics ? "Dokładna" : "Szybka"), () => s.FancyGraphics = !s.FancyGraphics));
        Add(new Slider(r, y, 150, 20, (s.RenderDistance - 2) / 14f, v => "Zasięg renderowania: " + (int)Math.Round(2 + v * 14) + " chunków", v => s.RenderDistance = (int)Math.Round(2 + v * 14), 14));
        Add(new Button(l, y + 24, 150, 20, () => "Wygładzone światło: " + (s.SmoothLighting ? "TAK" : "NIE"), () => s.SmoothLighting = !s.SmoothLighting));
        Add(new Button(r, y + 24, 150, 20, () => "Chmury: " + (s.Clouds ? "TAK" : "NIE"), () => s.Clouds = !s.Clouds));
        Add(new Button(l, y + 48, 150, 20, () => "Kołysanie widoku: " + (s.ViewBobbing ? "TAK" : "NIE"), () => s.ViewBobbing = !s.ViewBobbing));
        Add(new Button(r, y + 48, 150, 20, () => "Skala GUI: " + (s.GuiScale == 0 ? "Auto" : s.GuiScale.ToString()), () => { s.GuiScale = (s.GuiScale + 1) % 5; G.ApplySettings(); G.SetScreen(this); }));
        Add(new Button(l, y + 72, 150, 20, () => "Synchronizacja V: " + (s.VSync ? "TAK" : "NIE"), () => { s.VSync = !s.VSync; G.ApplySettings(); }));
        Add(new Slider(r, y + 72, 150, 20, (s.MaxFps - 30) / 230f, v => "Maks. FPS: " + (v >= 0.99f ? "Bez limitu" : ((int)(30 + v * 230)).ToString()), v => s.MaxFps = v >= 0.99f ? 1000 : (int)(30 + v * 230)));
        Add(new Slider(l, y + 96, 150, 20, s.Brightness, v => "Jasność: " + (int)(v * 100) + "%", v => s.Brightness = v));
        Add(new Button(W / 2 - 100, H / 6 + 168, 200, 20, "Gotowe", Done));
    }
    private void Done() { G.Settings.Save(); G.ApplySettings(); G.SetScreen(_parent); }
    protected override void Escape() => Done();
    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        G.Sb.TextCentered("Ustawienia graficzne", W / 2f, 15, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }
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
        Add(new Button(W / 2 - 100, H - 28, 200, 20, "Gotowe", () => { s.Save(); G.SetScreen(_parent); }));
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
        int y = H / 4 + 8;
        Add(new Button(W / 2 - 100, y + 24 - 16, 200, 20, "Wróć do gry", () => G.SetScreen(null)));
        Add(new Button(W / 2 - 100, y + 96 - 16, 98, 20, "Ustawienia...", () => G.SetScreen(new OptionsScreen(this))));
        Add(new Button(W / 2 + 2, y + 96 - 16, 98, 20, "Pomoc", () => G.SetScreen(new HelpScreen(this))));
        if (G.Client == null) Add(new Button(W / 2 - 100, y + 48 - 16, 200, 20, () => G.HostLink != null ? "Gra w sieci (otwarta)" : "Otwórz w sieci...", () => G.SetScreen(new Net.NetHostScreen(this))));
        Add(new Button(W / 2 - 100, y + 120 - 16, 200, 20, G.Client != null ? "Rozłącz się" : "Zapisz i wyjdź do menu", () => G.QuitToTitle()));
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
