using BlockCraft.Cmd;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft;

public sealed partial class Game
{
    private void DrawCommandHud(float pt)
    {
        var sb = Scoreboard.Of(World); var o = sb.SidebarFor(Player.Name);
        if (o != null)
        {
            var rows = sb.Sorted(o).TakeLast(15).ToList();
            float width = Sb.Measure(o.DisplayName);
            foreach (var row in rows) width = Math.Max(width, Sb.Measure(sb.FormatName(row.Name) + ": " + row.Points));
            float right = GuiW - 3, left = right - width, bottom = GuiH / 2f + rows.Count * 9 / 3f;
            float top = bottom - rows.Count * 9;
            Sb.Fill(left - 2, top - 10, width + 4, 10, 0x60000000);
            Sb.TextCentered(o.DisplayName, (left + right) / 2, top - 9, 0xFFFFFFFF);
            for (int i = 0; i < rows.Count; i++)
            {
                float y = bottom - (i + 1) * 9; string value = "§c" + rows[i].Points;
                Sb.Fill(left - 2, y, width + 4, 9, 0x50000000);
                Sb.Text(sb.FormatName(rows[i].Name), left, y + 1, 0xFFFFFFFF);
                Sb.Text(value, right - Sb.Measure(value), y + 1, 0xFFFFFFFF);
            }
        }
        if (KeyboardState.IsKeyDown(Keys.Tab) && CurrentScreen == null) DrawCommandPlayerList(sb);
        var title = Player.Title; float alpha = title.Alpha(pt);
        if (alpha > 0)
        {
            uint c = SpriteBatch.Col(255, 255, 255, (int)(alpha * 255));
            Sb.TextCentered(title.Title, GuiW / 2f, GuiH / 2f - 20, c, true, 4);
            Sb.TextCentered(title.Subtitle, GuiW / 2f, GuiH / 2f + 15, c, true, 2);
        }
        float warning = Math.Clamp(WorldBorder.Of(World).Warning(World, Player.X, Player.Z), 0, 1);
        if (warning > 0 && !Player.Spectator)
        {
            uint c = SpriteBatch.Col(230, 10, 10, (int)(warning * 140));
            Sb.Gradient(0, 0, GuiW, GuiH / 5f, c, 0);
            Sb.Gradient(0, GuiH * 4 / 5f, GuiW, GuiH / 5f, 0, c);
            for (int i = 0; i < 24; i++)
            {
                uint edge = SpriteBatch.Col(230, 10, 10, (int)(warning * 110 * (1 - i / 24f)));
                Sb.Fill(i, 0, 1, GuiH, edge); Sb.Fill(GuiW - i - 1, 0, 1, GuiH, edge);
            }
        }
    }

    private void DrawCommandPlayerList(Scoreboard sb)
    {
        var players = Client?.Players ?? Commands.AllPlayers(World).Select(p => (name: p.Name, ping: p.Ping)).ToList();
        var rows = players.OrderBy(p => sb.TeamOf(p.name)?.Name ?? "").ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase).Take(80).ToList();
        var o = sb.Display[0]; int columns = Math.Max(1, (rows.Count + 19) / 20), perColumn = (rows.Count + columns - 1) / columns;
        float cell = Math.Min(180, (GuiW - 40f) / columns), left = (GuiW - columns * cell) / 2;
        Sb.Fill(left - 2, 8, cell * columns + 4, perColumn * 10 + 4, 0x90000000);
        for (int i = 0; i < rows.Count; i++)
        {
            float x = left + (i / perColumn) * cell, y = 10 + (i % perColumn) * 10;
            Sb.Text(sb.FormatName(rows[i].name), x + 2, y, 0xFFFFFFFF);
            if (o != null)
            {
                int value = sb.GetScore(rows[i].name, o, false)?.Points ?? 0;
                string text = o.RenderType == "hearts" ? "§c" + value + " ♥" : "§e" + value;
                Sb.Text(text, x + cell - 12 - Sb.Measure(text), y, 0xFFFFFFFF);
            }
            Sb.Text("|", x + cell - 7, y, rows[i].ping < 300 ? 0xFF40DD40u : 0xFF40DDDDu);
        }
    }

    /// <summary>Półprzezroczysta ściana z ukośną siatką, kolor zależy od stanu granicy.</summary>
    private void DrawWorldBorder(Vector3d cam, Matrix4 vp)
    {
        var border = WorldBorder.Of(World); var b = border.Bounds(World);
        if (border.ClosestDistance(World, cam.X, cam.Z) > 32) return;
        int status = border.Status(World.TotalTime);
        uint color = status < 0 ? SpriteBatch.Col(255, 48, 48, 160) : status > 0 ? SpriteBatch.Col(64, 255, 128, 160) : SpriteBatch.Col(64, 160, 255, 160);
        int tex = TextureGen.Layer("world_border");
        float scroll = (float)(World.TotalTime % 200) / 200;
        void Wall(double fixedAxis, double along0, double along1, bool axisX)
        {
            double distance = Math.Abs(fixedAxis - (axisX ? cam.X : cam.Z)); if (distance > 32) return;
            double center = axisX ? cam.Z : cam.X;
            double low = Math.Max(along0, center - 32), high = Math.Min(along1, center + 32); if (high <= low) return;
            // U podąża wzdłuż ściany, V od podłoża ku niebu. Początek blisko kamery zapobiega utracie precyzji.
            Matrix4 frame = axisX
                ? Matrix4.CreateRotationY(-MathF.PI / 2) * Matrix4.CreateTranslation((float)(fixedAxis - cam.X), (float)-cam.Y, (float)(low - cam.Z))
                : Matrix4.CreateTranslation((float)(low - cam.X), (float)-cam.Y, (float)(fixedAxis - cam.Z));
            Sb.Begin3D(frame * vp);
            uint tint = (color & 0xFFFFFF) | ((uint)(160 * Math.Pow(1 - distance / 32, 2)) << 24);
            Sb.DrawLayer(Renderer.Terrain, tex, 0, 0, (float)(high - low), Chunk.Height, tint,
                (float)(low % 2) / 2 + scroll, scroll, (float)(low % 2 + high - low) / 2 + scroll, Chunk.Height / 2f + scroll);
            Sb.End3D();
        }
        Wall(b.MinX, b.MinZ, b.MaxZ, true); Wall(b.MaxX, b.MinZ, b.MaxZ, true);
        Wall(b.MinZ, b.MinX, b.MaxX, false); Wall(b.MaxZ, b.MinX, b.MaxX, false);
        GL.DepthMask(true); GL.Enable(EnableCap.DepthTest);
    }
}
