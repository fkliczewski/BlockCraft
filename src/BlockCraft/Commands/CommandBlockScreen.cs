using BlockCraft.Cmd;
using BlockCraft.Net;
using BlockCraft.Render;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft.Gui;

public sealed class CommandBlockScreen : Screen
{
    private readonly int _x, _y, _z, _entityId;
    private string _command, _output;
    private bool _track;
    public override bool PausesGame => false;
    public override bool WantsText => true;
    public CommandBlockScreen(int x, int y, int z, int entityId, string command, bool track, string output)
    { _x = x; _y = y; _z = z; _entityId = entityId; _command = command; _track = track; _output = output; }
    protected override void Init()
    {
        Add(new Button(W / 2 - 154, H / 2 + 60, 150, 20, "Gotowe", Save));
        Add(new Button(W / 2 + 4, H / 2 + 60, 150, 20, "Anuluj", () => G.SetScreen(null)));
        Add(new Button(W / 2 - 154, H / 2 + 30, 308, 20, "Zapisuj wynik: " + (_track ? "tak" : "nie"), () => { _track = !_track; Widgets.Clear(); Init(); }));
    }
    private void Save()
    {
        if (G.Client != null) G.Client.UpdateCommands(_x, _y, _z, _entityId, _command, _track);
        else CommandBlocks.Update(G.Player, _x, _y, _z, _entityId, _command, _track);
        G.SetScreen(null);
    }
    public override void KeyDown(Keys k, bool ctrl, bool shift)
    {
        if (k == Keys.Enter) { Save(); return; }
        if (k == Keys.Backspace && _command.Length > 0) { _command = _command[..^1]; return; }
        if (ctrl && k == Keys.V) { string value = G.ClipboardString ?? ""; _command = (_command + value.Replace("\r", "").Replace("\n", "")); if (_command.Length > 32767) _command = _command[..32767]; return; }
        base.KeyDown(k, ctrl, shift);
    }
    public override void CharTyped(char c) { if (c >= ' ' && c != 127 && c != '§' && _command.Length < 32767) _command += c; }
    public override void Render(int mx, int my, float pt)
    {
        DrawDim(); var sb = G.Sb; int left = W / 2 - 154;
        sb.TextCentered("Blok poleceń", W / 2f, H / 2 - 85, 0xFFFFFFFF);
        sb.Text("Komenda konsoli", left, H / 2 - 60, 0xFFAAAAAA);
        sb.Fill(left - 1, H / 2 - 43, 310, 22, SpriteBatch.Hex(0xA0A0A0));
        sb.Fill(left, H / 2 - 42, 308, 20, SpriteBatch.Hex(0x000000));
        string text = _command; while (text.Length > 0 && sb.Measure(text + "_") > 300) text = text[1..];
        sb.Text(text + "_", left + 4, H / 2 - 36, 0xFFFFFFFF);
        sb.Text("@p najbliższy gracz, @a gracze, @e encje", left, H / 2 - 14, 0xFFAAAAAA);
        string output = _track ? _output : "-"; while (output.Length > 0 && sb.Measure(output) > 300) output = output[..^1];
        sb.Text(output, left, H / 2 + 6, 0xFFAAAAAA);
        base.Render(mx, my, pt);
    }
}
