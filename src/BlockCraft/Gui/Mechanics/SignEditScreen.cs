using BlockCraft.World;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

/// <summary>GuiEditSign z 1.8: cztery linie po 90 pikseli, strzałki i Enter zmieniają linię, "Gotowe" zapisuje.</summary>
public sealed class SignEditScreen : Screen
{
    private readonly int _x, _y, _z;
    private readonly string[] _lines = { "", "", "", "" };
    private int _line, _ticks;
    public override bool PausesGame => false;

    public SignEditScreen(int x, int y, int z) { _x = x; _y = y; _z = z; }

    protected override void Init()
    {
        if (G.World?.GetTileEntity(_x, _y, _z) is SignTile t) for (int i = 0; i < 4; i++) _lines[i] = t.Lines[i] ?? "";
        Add(new Button(W / 2 - 100, H / 4 + 120, 200, 20, "Gotowe", () => G.SetScreen(null)));
    }

    public override void Tick() { _ticks++; }

    /// <summary>onGuiClosed: zapis lokalnie albo pakiet do hosta.</summary>
    public override void OnClose()
    {
        var lines = _lines.Select(Signs.Filter).ToArray();
        if (G.Client != null) { G.Client.UpdateSign(_x, _y, _z, lines); return; }
        if (G.World?.GetTileEntity(_x, _y, _z) is SignTile t) for (int i = 0; i < 4; i++) t.Lines[i] = lines[i];
    }

    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (k == Keys.Escape) { G.SetScreen(null); return; }
        if (k == Keys.Up) { _line = (_line + 3) & 3; return; }
        if (k is Keys.Down or Keys.Enter or Keys.KeyPadEnter) { _line = (_line + 1) & 3; return; }
        if (k == Keys.Backspace && _lines[_line].Length > 0) { _lines[_line] = _lines[_line][..^1]; return; }
        base.KeyDown(k, ctrl, shift);
    }

    public override void CharTyped(char c)
    {
        if (c < ' ' || c == '§' || c == 127) return;
        string s = _lines[_line] + c;
        if (G.Sb.Measure(s) <= Signs.MaxLineWidth) _lines[_line] = s;
    }

    public override void Render(int mx, int my, float pt)
    {
        DrawDim();
        var sb = G.Sb;
        sb.TextCentered("Edytuj treść tabliczki:", W / 2f, 40, 0xFFFFFFFF);
        // deska 24x12 pikseli modelu w skali tekstu (96 px czcionki na blok * 2/3)
        int bw = 96, bh = 48, bx = W / 2 - bw / 2 - 2, by = 62;
        sb.Fill(bx - 1, by - 1, bw + 6, bh + 6, BlockCraft.Render.SpriteBatch.Hex(0x4A3219));
        sb.Fill(bx, by, bw + 4, bh + 4, BlockCraft.Render.SpriteBatch.Hex(0xB08F5A));
        sb.Fill(W / 2 - 3, by + bh + 5, 6, 40, BlockCraft.Render.SpriteBatch.Hex(0x5E4A26));
        for (int i = 0; i < 4; i++)
        {
            string s = _lines[i];
            if (i == _line && _ticks / 6 % 2 == 0) s = "> " + s + " <";
            sb.TextCentered(s, W / 2f, by + 4 + i * 10 + 2, 0xFF000000, false);
        }
        base.Render(mx, my, pt);
    }
}
