using BlockCraft.Entities;
using OpenTK.Mathematics;

namespace BlockCraft;

public partial class Game
{
    private void ApplyEffectAtmosphere(Player player, float pt)
    {
        if (player.HasEffect(Effect.NightVision)) { Renderer.Gamma = 1; Renderer.SunBrightness = 1; }
        else Renderer.Gamma = Settings.Brightness;
        if (player.Effects.TryGetValue(Effect.Blindness, out var blind))
        {
            float distance = 5 + Math.Max(0, 1 - blind.Duration / 20f) * (Renderer.RenderDistance * 16 - 5);
            Renderer.FogMode = 0; Renderer.FogStart = distance * .25f; Renderer.FogEnd = distance; Renderer.FogColor = Vector3.Zero;
        }
    }
    private void DrawEffectHearts(Player player, int x, int y)
    {
        int bonus = (int)Math.Ceiling(Math.Max(0, player.MaxHealth - 20) / 2);
        int gold = (int)Math.Ceiling(player.Absorption / 2);
        for (int i = 0; i < bonus + gold; i++)
        {
            int hx = x + i % 10 * 8, hy = y - 10 - i / 10 * 10;
            uint color = i >= bonus ? 0xFFFFCF40 : player.Health > 20 + i * 2 ? 0xFFFF3030 : 0xFF502020;
            Sb.Fill(hx + 1, hy + 1, 2, 2, color); Sb.Fill(hx + 5, hy + 1, 2, 2, color);
            Sb.Fill(hx, hy + 3, 8, 2, color); Sb.Fill(hx + 1, hy + 5, 6, 1, color); Sb.Fill(hx + 2, hy + 6, 4, 1, color); Sb.Fill(hx + 3, hy + 7, 2, 1, color);
        }
    }
}
