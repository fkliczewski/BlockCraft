using BlockCraft.Core;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

/// <summary>Ustawienia → Sterowanie → „Kontroler...”: pad wł./wył., czułość kamery, martwa strefa, oś Y, wibracje,
/// przypisania przycisków i podgląd stanu pada na żywo.</summary>
public sealed class ControllerScreen : Screen
{
    private readonly Screen _parent;
    public ControllerScreen(Screen parent) { _parent = parent; }

    protected override void Init()
    {
        var s = G.Settings;
        int y = 34;
        Add(new Button(W / 2 - 155, y, 150, 20, () => "Pad: " + (s.PadEnabled ? "WŁĄCZONY" : "WYŁĄCZONY"), () => s.PadEnabled = !s.PadEnabled));
        Add(new Button(W / 2 + 5, y, 150, 20, () => "Wibracje: " + (s.PadVibration ? "TAK" : "NIE"), () => s.PadVibration = !s.PadVibration));
        Add(new Slider(W / 2 - 155, y + 24, 150, 20, s.PadLookSensitivity, v => "Czułość kamery: " + (int)(v * 200) + "%", v => s.PadLookSensitivity = v, 20));
        Add(new Slider(W / 2 + 5, y + 24, 150, 20, s.PadDeadzone / 0.5f, v => "Martwa strefa: " + (int)MathF.Round(v * 50) + "%", v => s.PadDeadzone = v * 0.5f, 25));
        Add(new Button(W / 2 - 155, y + 48, 150, 20, () => "Odwróć oś Y: " + (s.PadInvertY ? "TAK" : "NIE"), () => s.PadInvertY = !s.PadInvertY));
        Add(new Button(W / 2 + 5, y + 48, 150, 20, "Przyciski...", () => G.SetScreen(new PadBindingsScreen(this))));
        Add(new Button(W / 2 - 100, H - 28, 200, 20, "Gotowe", Close));
    }

    private void Close() { G.Settings.Save(); G.SetScreen(_parent); }
    protected override void Escape() => Close();

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Kontroler", W / 2f, 8, 0xFFFFFFFF);
        string status = G.Pad.Connected ? "§a" + G.Pad.Name : "§7Nie wykryto pada (podłącz go i naciśnij dowolny przycisk)";
        sb.TextCentered(status, W / 2f, 20, 0xFFFFFFFF);
        int top = 34 + 72 + 4;
        if (H - 32 - top >= 56) DrawPadPreview(W / 2, top + (H - 32 - top) / 2 - 26);
        base.Render(mx, my, pt);
    }

    /// <summary>Podgląd pada: gałki z pozycją (kółko = martwa strefa), spusty, przyciski podświetlone po wciśnięciu.</summary>
    private void DrawPadPreview(int cx, int y)
    {
        var sb = G.Sb; var p = G.Pad; var st = p.Current;
        sb.Fill(cx - 110, y - 4, 220, 60, 0x80000000);
        void Stick(int x, int sy, float ax, float ay, bool pressed)
        {
            sb.Fill(x - 13, sy - 13, 26, 26, pressed ? 0xFF606060 : 0xFF303030);
            int dz = (int)(G.Settings.PadDeadzone * 12);
            sb.Rect(x - dz, sy - dz, dz * 2 + 1, dz * 2 + 1, 0xFF808080);
            sb.Fill(x + (int)(ax * 11) - 2, sy + (int)(ay * 11) - 2, 5, 5, 0xFFFFFFFF);
        }
        void Trigger(int x, float v, PadButton b)
        {
            PadIcons.Draw(G, b, x, y, st[b]);
            sb.Fill(x + 14, y + 1, 30, 7, 0xFF303030);
            sb.Fill(x + 14, y + 1, 30 * Math.Clamp(v, 0, 1), 7, 0xFF60C060);
        }
        Trigger(cx - 104, st.LT, PadButton.LeftTrigger);
        Trigger(cx + 58, st.RT, PadButton.RightTrigger);
        PadIcons.Draw(G, PadButton.LeftBumper, cx - 104, y + 12, st[PadButton.LeftBumper]);
        PadIcons.Draw(G, PadButton.RightBumper, cx + 90, y + 12, st[PadButton.RightBumper]);
        Stick(cx - 70, y + 30, st.LX, st.LY, st[PadButton.LeftStick]);
        Stick(cx + 30, y + 40, st.RX, st.RY, st[PadButton.RightStick]);
        // krzyżak
        int dx = cx - 30, dy = y + 40;
        sb.Fill(dx - 3, dy - 10, 7, 21, 0xFF303030); sb.Fill(dx - 10, dy - 3, 21, 7, 0xFF303030);
        if (st[PadButton.DPadUp]) sb.Fill(dx - 3, dy - 10, 7, 7, 0xFFFFFFFF);
        if (st[PadButton.DPadDown]) sb.Fill(dx - 3, dy + 4, 7, 7, 0xFFFFFFFF);
        if (st[PadButton.DPadLeft]) sb.Fill(dx - 10, dy - 3, 7, 7, 0xFFFFFFFF);
        if (st[PadButton.DPadRight]) sb.Fill(dx + 4, dy - 3, 7, 7, 0xFFFFFFFF);
        // Back / Start i A B X Y w karo
        PadIcons.Draw(G, PadButton.Back, cx - 26, y + 14, st[PadButton.Back]);
        PadIcons.Draw(G, PadButton.Start, cx + 2, y + 14, st[PadButton.Start]);
        int bx = cx + 76, by = y + 34;
        PadIcons.Draw(G, PadButton.Y, bx, by - 11, st[PadButton.Y]);
        PadIcons.Draw(G, PadButton.X, bx - 11, by, st[PadButton.X]);
        PadIcons.Draw(G, PadButton.B, bx + 11, by, st[PadButton.B]);
        PadIcons.Draw(G, PadButton.A, bx, by + 11, st[PadButton.A]);
    }
}

/// <summary>Przypisania przycisków pada: kliknij akcję i naciśnij przycisk na padzie (zajęty przycisk zamienia się miejscami).</summary>
public sealed class PadBindingsScreen : Screen, IPadCapture
{
    private readonly Screen _parent;
    private PadAction? _waiting;
    private long _waitUntil;
    public PadBindingsScreen(Screen parent) { _parent = parent; }

    protected override void Init()
    {
        var actions = Enum.GetValues<PadAction>();
        int rows = (actions.Length + 1) / 2;
        for (int i = 0; i < actions.Length; i++)
        {
            var a = actions[i];
            int x = i < rows ? W / 2 - 155 : W / 2 + 5, y = 28 + (i % rows) * 22;
            Add(new Button(x, y, 150, 20, () => PadBindings.ActionNames[a] + ": " +
                (_waiting == a ? "§e> naciśnij <" : PadBindings.ButtonName(G.PadBinds[a])), () => { _waiting = a; _waitUntil = Environment.TickCount64 + 5000; }));
        }
        Add(new Button(W / 2 - 155, H - 28, 150, 20, "Domyślne", () => { G.PadBinds.Reset(); Save(); }));
        Add(new Button(W / 2 + 5, H - 28, 150, 20, "Gotowe", () => { Save(); G.SetScreen(_parent); }));
    }

    private void Save() { G.Settings.PadBindings = G.PadBinds.ToSettings(); G.Settings.Save(); }

    public bool CapturesPad => _waiting != null;
    public void PadButtonPressed(PadButton b)
    {
        if (_waiting is not { } a || b == PadButton.Guide) return;
        G.PadBinds.Assign(a, b);
        _waiting = null;
        G.PlayUiClick();
        Save();
    }

    public override void Tick() { if (_waiting != null && Environment.TickCount64 > _waitUntil) _waiting = null; }

    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (_waiting != null) { _waiting = null; return; }
        base.KeyDown(k, ctrl, shift);
    }

    protected override void Escape() { Save(); G.SetScreen(_parent); }

    public override void Render(int mx, int my, float pt)
    {
        if (G.World == null) DrawDirtBackground(); else DrawDim();
        G.Sb.TextCentered("Przyciski pada", W / 2f, 10, 0xFFFFFFFF);
        G.Sb.TextCentered("§7Ruch i kamera: gałki. W menu: A wybierz, B wstecz, X prawy klik, Y szybkie przeniesienie.", W / 2f, H - 40, 0xFFFFFFFF);
        base.Render(mx, my, pt);
    }
}
