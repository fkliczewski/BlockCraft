using BlockCraft.Render;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

public abstract class Widget
{
    public int X, Y, W, H;
    public bool Visible = true, Enabled = true;
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
}

public sealed class TextField : Widget
{
    public string Text = "";
    public bool Focused;
    public int MaxLength = 64;
    public string Placeholder = "";
    private int _cursorBlink;
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
        _cursorBlink++;
        if (Focused && (_cursorBlink / 30) % 2 == 0) sb.Text("_", X + 4 + sb.Measure(t), Y + (H - 8) / 2f, 0xFFE0E0E0);
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
