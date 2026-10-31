using BlockCraft.Entities;

namespace BlockCraft.Gui;

public static class EffectPanel
{
    public static void Draw(Game game, Player player, int left, int top)
    {
        int y = top, spacing = player.Effects.Count > 5 ? 132 / (player.Effects.Count - 1) : 33;
        int x = Math.Max(2, left - 126);
        foreach (var effect in player.Effects.Values.OrderBy(e => e.Id))
        {
            game.Sb.Fill(x, y, 122, 31, 0xB0202020);
            uint color = 0xFF000000 | (uint)EffectLogic.Colors[(int)effect.Id];
            // Ikona proceduralna w siatce 18x18, indywidualna dla każdego efektu.
            for (int r = 0; r < 5; r++) for (int c = 0; c < 5; c++)
                if ((((int)effect.Id * 73 + r * 17) >> (c < 3 ? c : 4 - c) & 1) != 0) game.Sb.Fill(x + 5 + c * 3, y + 7 + r * 3, 3, 3, color);
            string level = effect.Amplifier > 0 ? " " + Items.EnchantHelper.Roman(effect.Amplifier + 1) : "";
            game.Sb.Text(EffectLogic.Name(effect.Id) + level, x + 25, y + 6, 0xFFFFFFFF);
            game.Sb.Text(EffectLogic.Time(effect), x + 25, y + 17, 0xFFB0B0B0);
            y += spacing;
        }
    }
}
