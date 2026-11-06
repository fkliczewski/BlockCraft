using BlockCraft.Core;
using BlockCraft.Gui;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft;

/// <summary>Zadanie B: pad bez sprzętu — martwa strefa, zbocza przycisków, przypisania, klawiatura ekranowa, przeskok krzyżakiem.</summary>
public static partial class SelfTest
{
    private static void TestPad()
    {
        Console.WriteLine("[Pad i klawiatura ekranowa]");
        // martwa strefa promieniowa z przeskalowaniem
        Check(Gamepad.ApplyDeadzone(0.1f, 0.05f, 0.15f) == (0f, 0f), "Martwa strefa: małe wychylenie = 0");
        var (fx, fy) = Gamepad.ApplyDeadzone(1f, 0f, 0.15f);
        Check(Math.Abs(fx - 1f) < 1e-4f && fy == 0, "Martwa strefa: pełne wychylenie = 1", $"{fx},{fy}");
        var (hx, _) = Gamepad.ApplyDeadzone(0.5f, 0f, 0.15f);
        Check(Math.Abs(hx - 0.35f / 0.85f) < 1e-4f, "Martwa strefa: liniowo za strefą", hx.ToString("0.000"));
        var (dx, dy) = Gamepad.ApplyDeadzone(0.7071f, 0.7071f, 0.15f);
        Check(Math.Abs(dx - dy) < 1e-4f && dx > 0.69f, "Martwa strefa: kierunek po skosie bez zmian", $"{dx},{dy}");

        // zbocza przycisków z podstawionego stanu
        var pad = new Gamepad();
        var s = new PadState(); s[PadButton.A] = true;
        pad.Inject(s); pad.Poll();
        bool first = pad.Pressed(PadButton.A);
        pad.Poll();
        Check(first && !pad.Pressed(PadButton.A) && pad.Down(PadButton.A), "Zbocze: A wciśnięte tylko w pierwszej klatce");
        pad.Inject(new PadState()); pad.Poll();
        Check(pad.Released(PadButton.A) && !pad.Active, "Zbocze: puszczenie A, pad bezczynny");
        s = new PadState { LX = 0.05f, RY = 0.9f }; pad.Inject(s); pad.Poll();
        Check(pad.Active && pad.LeftStick == (0f, 0f), "Aktywność: prawa gałka tak, drgnięcie lewej nie");

        // przypisania: zamiana, zapis tylko różnic, odczyt, nieznane nazwy
        var b = new PadBindings();
        Check(b[PadAction.Jump] == PadButton.A && b[PadAction.Attack] == PadButton.RightTrigger, "Przypisania domyślne (A skok, RT atak)");
        b.Assign(PadAction.Jump, PadButton.B);
        Check(b[PadAction.Jump] == PadButton.B && b[PadAction.Sneak] == PadButton.A, "Zajęty przycisk zamienia się z poprzednim");
        var saved = b.ToSettings();
        Check(saved.Count == 2 && saved["Jump"] == "B", "Zapis tylko zmienionych", string.Join(",", saved));
        var loaded = PadBindings.FromSettings(new Dictionary<string, string>(saved) { ["Nieznana"] = "A", ["Drop"] = "Xyz" });
        Check(loaded[PadAction.Jump] == PadButton.B && loaded[PadAction.Sneak] == PadButton.A && loaded[PadAction.Drop] == PadButton.X,
            "Odczyt przypisań (nieznane pomijane)");
        var all = Enum.GetValues<PadAction>().Select(a => loaded[a]).ToList();
        Check(all.Distinct().Count() == all.Count, "Każda akcja ma inny przycisk");
        loaded.Reset();
        Check(loaded.ToSettings().Count == 0, "Domyślne po resecie");

        // klawiatura ekranowa
        var osk = new OnScreenKeyboard();
        string Type(params string[] labels)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in labels)
            {
                if (l == "Shift") { osk.ToggleShift(); continue; }
                if (l == "#+=") { osk.ToggleSymbols(); continue; }
                Check(osk.Select(l), "OSK: klawisz " + l);
                var o = osk.Press();
                if (o is { Kind: OnScreenKeyboard.OutKind.Char } c) sb.Append(c.Char);
                else if (o is { Kind: OnScreenKeyboard.OutKind.Backspace } && sb.Length > 0) sb.Length--;
            }
            return sb.ToString();
        }
        Check(Type("Shift", "z", "a", "ż", "ó", "ł", "ć", "Spacja", "1") == "Zażółć 1", "OSK: Shift jednorazowy, polskie znaki, spacja", Type());
        osk.Reset();
        Check(Type("Shift", "Shift", "a", "b", "Shift", "c") == "ABc", "OSK: dwa razy Shift = stałe wielkie");
        osk.Reset();
        Check(Type("#+=", "@", "#", "#+=", "x", "<-", "y") == "@#y", "OSK: symbole i Backspace");
        osk.Reset();
        osk.Select("q"); osk.Move(0, 1);
        Check(osk.Selected.Label == "a", "OSK: w dół z q na a", osk.Selected.Label);
        osk.Select("q"); osk.Move(-1, 0);
        Check(osk.Selected.Label == "p", "OSK: w lewo z q zawija na p", osk.Selected.Label);
        osk.Select("ń"); osk.Move(0, 1);
        Check(osk.Selected.Special == OnScreenKeyboard.Special.Space, "OSK: pod środkiem liter jest spacja", osk.Selected.Label);
        osk.Move(0, 1);
        Check(osk.Selected.Label is "5" or "6" or "7", "OSK: ze spacji w dół zawija na cyfry", osk.Selected.Label);
        Check(osk.Select("Enter") && osk.Press() is { Kind: OnScreenKeyboard.OutKind.Enter }, "OSK: Enter");

        // przeskok krzyżakiem
        var targets = new List<(int, int)> { (100, 100), (150, 100), (100, 150), (300, 110), (60, 40) };
        Check(PadNavigation.Next(targets, 100, 100, 1, 0) == (150, 100), "Krzyżak: w prawo najbliższy w rzędzie");
        Check(PadNavigation.Next(targets, 100, 100, 0, 1) == (100, 150), "Krzyżak: w dół");
        Check(PadNavigation.Next(targets, 150, 100, 1, 0) == (300, 110), "Krzyżak: dalej w prawo, lekko niżej");
        Check(PadNavigation.Next(targets, 300, 110, 1, 0) == null, "Krzyżak: nic dalej w prawo");
        Check(PadNavigation.Next(targets, 100, 150, 0, -1) == (100, 100), "Krzyżak: w górę");

        TestPadMenu();
        TestPadRumble();
    }

    /// <summary>Menu i klawiatura ekranowa bez okna gry: podstawiony host zapisuje zdarzenia.</summary>
    private static void TestPadMenu()
    {
        var host = new FakePadHost();
        var menu = new PadMenuController(host);
        var pad = new Gamepad();
        var screen = new FakePadScreen();
        void Frame(params PadButton[] down)
        {
            var st = new PadState(); foreach (var b in down) st[b] = true;
            pad.Inject(st); pad.Poll(); menu.Update(pad, screen, 1 / 60.0);
        }
        Check(menu.OnScreenChange(screen, true) == (10, 10), "Menu: kursor na pierwszym celu ekranu");
        host.CursorX = host.CursorY = 10;
        Frame(PadButton.DPadRight);
        Check((host.CursorX, host.CursorY) == (60, 10), "Menu: krzyżak przeskakuje do celu", $"{host.CursorX},{host.CursorY}");
        Frame(); Frame(PadButton.A); Frame(PadButton.A); Frame();
        Check(string.Join(" ", host.Events) == "down0 up0", "Menu: A trzymane = klik z przeciąganiem", string.Join(" ", host.Events));
        host.Events.Clear(); Frame(PadButton.Y); Frame();
        Check(host.Events.SequenceEqual(new[] { "shift" }), "Menu: Y = Shift+klik", string.Join(" ", host.Events));

        screen.Text = true; host.Events.Clear(); Frame();
        Check(menu.KeyboardOpen, "Klawiatura: otwiera się przy polu tekstowym");
        Frame(PadButton.X); Frame(PadButton.Y); Frame();
        Check(!menu.KeyboardOpen && host.Events.SequenceEqual(new[] { "char: " }), "Klawiatura: X spacja, Y chowa", string.Join(" ", host.Events));
        Frame(PadButton.Back); Frame();
        Check(menu.KeyboardOpen, "Klawiatura: Back pokazuje znowu");
        host.Events.Clear(); Frame(PadButton.Start);
        Check(host.Events.SequenceEqual(new[] { "key:Enter" }), "Klawiatura: Start = Enter", string.Join(" ", host.Events));
    }

    private static void TestPadRumble()
    {
        var rumble = new FakeRumble();
        var triggers = new PadRumbleTriggers(rumble);
        var ctrl = new object();
        triggers.Tick(true, 0, ctrl, 0);
        triggers.Tick(true, 10, ctrl, 1);
        Check(rumble.Count == 2, "Wibracje: obrażenia i zniszczony blok", rumble.Count.ToString());
        triggers.Tick(true, 9, new object(), 0);
        Check(rumble.Count == 2, "Wibracje: nowy kontroler (wymiar) nie wibruje");
        triggers.Tick(false, 10, ctrl, 5);
        Check(rumble.Count == 2, "Wibracje: wyłączone");
    }

    private sealed class FakeRumble : IPadRumble
    {
        public int Count;
        public void Rumble(float strength, int ms) => Count++;
        public void Update() { }
    }

    private sealed class FakePadScreen : Screen
    {
        public bool Text;
        public override bool WantsText => Text;
        public override IEnumerable<(int x, int y)> PadTargets() => new[] { (10, 10), (60, 10) };
    }

    private sealed class FakePadHost : IPadMenuHost
    {
        public readonly List<string> Events = new();
        public int GuiW => 320;
        public int GuiH => 240;
        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public double Seconds => 0;
        public void MoveCursor(int x, int y) { CursorX = x; CursorY = y; }
        public void MouseButton(int x, int y, int button, bool down) => Events.Add((down ? "down" : "up") + button);
        public void MouseWheel(int x, int y, float step) => Events.Add("wheel");
        public void ShiftClick(int x, int y) => Events.Add("shift");
        public void ScreenKey(Keys key, bool ctrl) => Events.Add("key:" + key);
        public void ScreenChar(char c) => Events.Add("char:" + c);
        public void Defer(Action action) => action();
        public bool CanReturnToGame(Screen s) => false;
        public void EscapeToGame() { }
        public void PlayUiClick() { }
    }
}
