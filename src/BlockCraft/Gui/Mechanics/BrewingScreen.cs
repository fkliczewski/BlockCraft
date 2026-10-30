using BlockCraft.Entities;
using BlockCraft.Items;

namespace BlockCraft.Gui;

public sealed class BrewingScreen : ContainerScreen
{
    public BrewingScreen(Player player, BrewingContainer container) : base(player, container) { }
    protected override void DrawBackground(int mx, int my)
    {
        base.DrawBackground(mx, my);
        G.Sb.TextCentered(C.Title, Left + C.Width / 2, Top + 6, 0xFF404040, false);
        int time = ((BrewingContainer)C).Stand.BrewTime;
        G.Sb.Fill(Left + 97, Top + 17, 5, 27, 0xFF555555);
        if (time > 0) G.Sb.Fill(Left + 97, Top + 17, 5, (400 - time) * 27 / 400, 0xFFFFFFFF);
        if (time > 0) G.Sb.Text($"{(time + 19) / 20}s", Left + 111, Top + 22, 0xFF404040, false);
    }
}
