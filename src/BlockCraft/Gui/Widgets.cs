using BlockCraft.Render;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

public abstract class Widget
{
    public int X, Y, W, H;
    public bool Visible = true, Enabled = true;
    /// <summary>Jedno zdanie opisu pokazywane pod panelem ustawień, gdy kursor (albo pad) jest na widżecie.</summary>
    public string Hint;
    public bool Hover(int mx, int my) => Visible && mx >= X && my >= Y && mx < X + W && my < Y + H;
    public abstract void Draw(Game g, int mx, int my);
    public virtual bool MouseDown(Game g, int mx, int my, int btn) => false;
    public virtual void MouseUp(Game g, int mx, int my, int btn) { }
    public virtual void MouseDrag(Game g, int mx, int my) { }
    public virtual bool Key(Game g, Keys k, bool ctrl, bool shift) => false;
    public virtual bool Char(Game g, char c) => false;
}

public sealed class Button : Widget
{
    public Func<string> Text;
    public Action OnClick;
    public Button(int x, int y, int w, int h, string text, Action click) { X = x; Y = y; W = w; H = h; Text = () => text; OnClick = click; }
    public Button(int x, int y, int w, int h, Func<string> text, Action click) { X = x; Y = y; W = w; H = h; Text = text; OnClick = click; }

    public static void DrawButton(Game g, int x, int y, int w, int h, int state)
    {
        var sb = g.Sb; var t = g.Gui.Widgets;
        int ty = GuiTextures.BtnY + state * 20;
        int half = w / 2;
        sb.DrawPx(t, x, y, 0, ty, half, h / 2);
        sb.DrawPx(t, x + half, y, 200 - (w - half), ty, w - half, h / 2);
        sb.DrawPx(t, x, y + h / 2, 0, ty + 20 - (h - h / 2), half, h - h / 2);
        sb.DrawPx(t, x + half, y + h / 2, 200 - (w - half), ty + 20 - (h - h / 2), w - half, h - h / 2);
    }

    public override void Draw(Game g, int mx, int my)
    {
        if (!Visible) return;
        int st = !Enabled ? 0 : Hover(mx, my) ? 2 : 1;
        DrawButton(g, X, Y, W, H, st);
        uint col = !Enabled ? 0xFFA0A0A0 : Hover(mx, my) ? 0xFFA0FFFF : 0xFFE0E0E0;
        g.Sb.TextCentered(Text(), X + W / 2f, Y + (H - 8) / 2f, col);
    }
    public override bool MouseDown(Game g, int mx, int my, int btn)
    {
        if (btn != 0 || !Enabled || !Hover(mx, my)) return false;
        g.PlayUiClick();
        OnClick?.Invoke();
        return true;
    }
}

/// <summary>Kategoria w kolumnie po lewej (ustawienia): wybrana ma żółty napis i pasek łączący ją z panelem.</summary>
public sealed class TabButton : Widget
{
    public readonly string Text;
    public readonly bool Selected;
    private readonly Action _click;
    public TabButton(int x, int y, int w, int h, string text, bool selected, Action click) { X = x; Y = y; W = w; H = h; Text = text; Selected = selected; _click = click; }

    public override void Draw(Game g, int mx, int my)
    {
        bool hover = Hover(mx, my);
        Button.DrawButton(g, X, Y, W, H, hover && !Selected ? 2 : 1);
        if (Selected)
        {
            g.Sb.Fill(X + 2, Y + 2, 2, H - 4, 0xFF55FFFF);
            g.Sb.Fill(X + W, Y + 2, 8, H - 4, OptionsPanel.Background); // łącznik z panelem opcji
        }
        uint col = Selected ? 0xFF55FFFF : hover ? 0xFFA0FFFF : 0xFFE0E0E0;
        g.Sb.TextCentered(Text, X + W / 2f + (Selected ? 1 : 0), Y + (H - 8) / 2f, col);
    }

    public override bool MouseDown(Game g, int mx, int my, int btn)
    {
        if (btn != 0 || !Hover(mx, my) || Selected) return false;
        g.PlayUiClick();
        _click();
        return true;
    }
}

/// <summary>Wspólny wygląd panelu ustawień: tło, ramka i opis opcji pod kursorem (zawijany do szerokości).</summary>
public static class OptionsPanel
{
    public const uint Background = 0xB0000000, Border = 0xFF5A5A5A, HintColor = 0xFFB8B8B8;

    public static void Draw(Game g, int x, int y, int w, int h)
    {
        g.Sb.Fill(x, y, w, h, Background);
        g.Sb.Rect(x, y, w, h, Border);
    }

    /// <summary>Opis wycentrowany pod panelem, najwyżej dwa wiersze.</summary>
    public static void Hint(Game g, string text, float cx, int y, int width)
    {
        if (string.IsNullOrEmpty(text)) return;
        var sb = g.Sb;
        var lines = new List<string>();
        string line = "";
        foreach (var word in text.Split(' '))
        {
            string next = line.Length == 0 ? word : line + " " + word;
            if (sb.Measure(next) > width && line.Length > 0) { lines.Add(line); line = word; }
            else line = next;
        }
        if (line.Length > 0) lines.Add(line);
        for (int i = 0; i < Math.Min(2, lines.Count); i++) sb.TextCentered(lines[i], cx, y + i * 10, HintColor);
    }
}

public sealed class Slider : Widget
{
    public float Value; // 0..1
    public Func<float, string> Label;
    public Action<float> OnChange;
    public int Steps; // 0 = ciągły
    private bool _drag;
    public Slider(int x, int y, int w, int h, float value, Func<float, string> label, Action<float> change, int steps = 0)
    { X = x; Y = y; W = w; H = h; Value = value; Label = label; OnChange = change; Steps = steps; }
    public override void Draw(Game g, int mx, int my)
    {
        Button.DrawButton(g, X, Y, W, H, 0);
        int hx = X + (int)(Value * (W - 8));
        Button.DrawButton(g, hx, Y, 8, H, Hover(mx, my) || _drag ? 2 : 1);
        g.Sb.TextCentered(Label(Value), X + W / 2f, Y + (H - 8) / 2f, Hover(mx, my) ? 0xFFA0FFFF : 0xFFE0E0E0);
    }
    private void Set(int mx)
    {
        float v = Math.Clamp((mx - X - 4) / (float)(W - 8), 0, 1);
        if (Steps > 0) v = MathF.Round(v * Steps) / Steps;
        if (v != Value) { Value = v; OnChange?.Invoke(v); }
    }
    public override bool MouseDown(Game g, int mx, int my, int btn)
    {
        if (btn != 0 || !Hover(mx, my)) return false;
        _drag = true; Set(mx); return true;
    }
    public override void MouseDrag(Game g, int mx, int my) { if (_drag) Set(mx); }
    public override void MouseUp(Game g, int mx, int my, int btn) { if (_drag) { _drag = false; g.PlayUiClick(); } }
    /// <summary>Krok w lewo/prawo (krzyżak pada): o jedną podziałkę albo 5%.</summary>
    public void Nudge(int dir)
    {
        float v = Math.Clamp(Value + dir * (Steps > 0 ? 1f / Steps : 0.05f), 0, 1);
        if (v != Value) { Value = v; OnChange?.Invoke(v); }
    }
}

public sealed class TextField : Widget
{
    public string Text = "";
    public bool Focused;
    public int MaxLength = 64;
    public string Placeholder = "";
    public Action OnEnter;
    public TextField(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
    public override void Draw(Game g, int mx, int my)
    {
        var sb = g.Sb;
        sb.Fill(X - 1, Y - 1, W + 2, H + 2, Focused ? 0xFFFFFFFF : 0xFFA0A0A0);
        sb.Fill(X, Y, W, H, 0xFF000000);
        string t = Text;
        while (sb.Measure(t) > W - 8 && t.Length > 0) t = t[1..];
        if (Text.Length == 0 && !Focused) sb.Text(Placeholder, X + 4, Y + (H - 8) / 2f, 0xFF707070, false);
        sb.Text(t, X + 4, Y + (H - 8) / 2f, 0xFFE0E0E0);
        // 1.8 GuiTextField: cursorCounter / 6 % 2 w tickach, czyli 300 ms (wcześniej liczone w klatkach: przy 500 Hz miga 8x za szybko)
        if (Focused && Environment.TickCount64 / 300 % 2 == 0) sb.Text("_", X + 4 + sb.Measure(t), Y + (H - 8) / 2f, 0xFFE0E0E0);
    }
    public override bool MouseDown(Game g, int mx, int my, int btn) { Focused = Hover(mx, my); return Focused; }
    public override bool Char(Game g, char c)
    {
        if (!Focused || Text.Length >= MaxLength || c < 32) return false;
        Text += c; return true;
    }
    public override bool Key(Game g, Keys k, bool ctrl, bool shift)
    {
        if (!Focused) return false;
        if (k == Keys.Backspace && Text.Length > 0) { Text = ctrl ? "" : Text[..^1]; return true; }
        if (k == Keys.V && ctrl) { var c = g.GetClipboard(); if (c != null) Text = (Text + c.Replace("\n", "").Replace("\r", "")); if (Text.Length > MaxLength) Text = Text[..MaxLength]; return true; }
        if (k is Keys.Enter or Keys.KeyPadEnter) { OnEnter?.Invoke(); return true; }
        return false;
    }
}

public abstract class Screen
{
    public Game G;
    public int W, H;
    protected readonly List<Widget> Widgets = new();
    public virtual bool PausesGame => true;
    public virtual bool ShowWorld => true;

    public void Open(Game g, int w, int h) { G = g; W = w; H = h; Widgets.Clear(); Init(); }
    protected virtual void Init() { }
    public virtual void Tick() { }
    public virtual void OnClose() { }

    protected T Add<T>(T w) where T : Widget { Widgets.Add(w); return w; }

    public virtual void Render(int mx, int my, float pt)
    {
        foreach (var w in Widgets) if (w.Visible) w.Draw(G, mx, my);
    }

    public virtual void MouseDown(int mx, int my, int btn)
    {
        foreach (var w in Widgets.ToArray()) if (w.Visible && w.MouseDown(G, mx, my, btn)) { if (w is not TextField) foreach (var o in Widgets) if (o is TextField tf && o != w) tf.Focused = false; return; }
        foreach (var o in Widgets) if (o is TextField tf && !tf.Hover(mx, my)) tf.Focused = false;
    }
    public virtual void MouseUp(int mx, int my, int btn) { foreach (var w in Widgets) w.MouseUp(G, mx, my, btn); }
    public virtual void MouseMove(int mx, int my) { foreach (var w in Widgets) w.MouseDrag(G, mx, my); }
    public virtual void Scroll(int mx, int my, float delta) { }
    public virtual void KeyDown(Keys k, bool ctrl, bool shift)
    {
        foreach (var w in Widgets) if (w.Key(G, k, ctrl, shift)) return;
        if (k == Keys.Escape) Escape();
        if (k == Keys.Tab)
        {
            var fields = Widgets.OfType<TextField>().ToList();
            if (fields.Count > 1) { int i = fields.FindIndex(f => f.Focused); foreach (var f in fields) f.Focused = false; fields[(i + 1) % fields.Count].Focused = true; }
        }
    }
    public virtual void CharTyped(char c) { foreach (var w in Widgets) if (w.Char(G, c)) return; }
    protected virtual void Escape() => G.SetScreen(null);

    // ===== pad =====
    /// <summary>Punkty, do których krzyżak przeskakuje kursorem (domyślnie środki widocznych, aktywnych widżetów).</summary>
    public virtual IEnumerable<(int x, int y)> PadTargets() =>
        Widgets.Where(w => w.Visible && w.Enabled).Select(w => (w.X + w.W / 2, w.Y + w.H / 2));
    /// <summary>Gdzie stanąć kursorem po otwarciu ekranu z pada (null = bez zmiany).</summary>
    public virtual (int x, int y)? PadStart() { foreach (var t in PadTargets()) return t; return null; }
    public Widget WidgetAt(int mx, int my) => Widgets.LastOrDefault(w => w.Visible && w.Hover(mx, my));
    /// <summary>Czy ekran czeka na tekst (pokazać klawiaturę ekranową).</summary>
    public virtual bool WantsText => Widgets.Any(w => w is TextField { Visible: true, Focused: true });
    /// <summary>Wysokość pola tekstowego: klawiatura ekranowa staje po drugiej stronie ekranu.</summary>
    public virtual int TextY => Widgets.OfType<TextField>().FirstOrDefault(f => f.Visible && f.Focused)?.Y ?? 0;

    protected void DrawDirtBackground()
    {
        var sb = G.Sb;
        float s = 32;
        sb.Draw(G.Gui.Dirt, 0, 0, W, H, 0, 0, W / s, H / s, 0xFF404040);
    }
    protected void DrawDim()
    {
        G.Sb.Gradient(0, 0, W, H, 0xC0101010, 0xD0101010);
    }
}

