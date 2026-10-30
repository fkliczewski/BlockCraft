using BlockCraft.Audio;
using BlockCraft.Entities;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

public partial class Game
{
    public DimensionSet Dimensions { get; private set; }
    private void StartDimensions(World.World overworld)
    {
        Dimensions = new DimensionSet(overworld);
        Dimensions.EntityTransferred += (entity, old, next) =>
        {
            if (entity is Player) next.ChunkManager ??= new ChunkManager(next) { Radius = Settings.RenderDistance };
            if (entity != Player) return;
            Player.OpenContainer = null;
            next.ChunkManager ??= new ChunkManager(next) { Radius = Settings.RenderDistance };
            World = next; Ctrl = new PlayerController(this); Cmds = new Commands(next);
            // renderer odrzuca stare siatki, więc chunki świata, do którego wracamy, trzeba przebudować
            foreach (var c in next.Chunks.Values) c.MarkAllDirty();
            Renderer.SetWorld(next);
            // MC 1.8: wyjście przez portal Endu pokazuje napisy końcowe
            SetScreen(old.Dimension == DimensionId.End && next.Dimension == DimensionId.Overworld && !Player.Dead ? new CreditsScreen() : null);
            next.PlaySound("portal.travel",Player.X,Player.Y,Player.Z,.8f);
        };
    }

    private void UpdateDimensionChunks()
    {
        if (Dimensions == null) { World.ChunkManager?.Update(ChunkCenters(), 5); return; }
        foreach (var w in Dimensions.Loaded.Values)
        {
            var centers = w.Players.Where(p => !p.Removed)
                .Select(p => (Core.MathUtil.FloorToInt(p.X) >> 4, Core.MathUtil.FloorToInt(p.Z) >> 4)).ToArray();
            if (centers.Length == 0) continue;
            w.ChunkManager ??= new ChunkManager(w) { Radius = Settings.RenderDistance };
            w.ChunkManager.Update(centers, 5);
        }
    }

    /// <summary>MC 1.8 Minecraft.getAmbientMusicType: muzyka Netheru, Endu (z bossem: EndBoss), w Overworldzie gra albo kreatywny.</summary>
    private void DimensionMusicTick()
    {
        BossStatus.Tick();
        if (Player == null || World == null || Music.Context == MusicType.Credits) return;
        var t = World.Dimension switch
        {
            DimensionId.Nether => MusicType.Nether,
            DimensionId.End => BossStatus.Visible ? MusicType.EndBoss : MusicType.End,
            _ => Player.Creative ? MusicType.Creative : MusicType.Game
        };
        if (t != Music.Context) Music.SetContext(t);
    }

    /// <summary>GuiIngame.renderBossHealth (MC 1.8): pasek 182x5 u góry ekranu i nazwa bossa nad nim.</summary>
    private void DrawBossBar()
    {
        if (!BossStatus.Visible) return;
        var sb = Sb; int cx = GuiW / 2, x = cx - 91, y = 12, fill = (int)(BossStatus.HealthScale * 183);
        sb.Fill(x - 1, y - 1, 184, 7, SpriteBatch.Col(0, 0, 0, 200));
        sb.Fill(x, y, 182, 5, SpriteBatch.Col(74, 18, 74));
        if (fill > 0) sb.Fill(x, y, Math.Min(182, fill), 5, SpriteBatch.Col(236, 62, 232));
        if (fill > 0) sb.Fill(x, y, Math.Min(182, fill), 1, SpriteBatch.Col(255, 140, 250));
        sb.TextCentered(BossStatus.BossName, cx, y - 10, 0xFFFFFFFF);
    }
}

/// <summary>Napisy końcowe po wyjściu z Endu (GuiWinGame): przewijany tekst, Esc albo koniec przewijania zamyka.</summary>
public sealed class CreditsScreen : Gui.Screen
{
    private static readonly string[] Lines =
    {
        "§eBLOCKCRAFT", "", "", "§7Smok Endu pokonany.", "",
        "Wyspa w pustce ucichła, a portal z bedrocku", "otworzył drogę z powrotem do domu.", "",
        "Jajo smoka zostało na kolumnie -", "może kiedyś ktoś po nie wróci.", "", "",
        "§eŚwiat", "Overworld, Nether i End", "", "§eModuły", "OW - Overworld i struktury", "NE - Nether, End i bossowie",
        "ME - mechanika i przedmioty", "MO - moby i spawnery", "", "§eGra wzorowana na", "Minecraft 1.8 (Mojang)", "", "",
        "§7Dziękujemy za grę.", "", "", ""
    };
    private float _scroll, _prevScroll;
    public override bool ShowWorld => false;
    /// <summary>Przewija napisy tak, by pierwsza linia była u góry ekranu (zrzut w autoteście).</summary>
    public void SkipIntro() { _scroll = _prevScroll = H; }
    protected override void Init() { Audio.Music.SetContext(Audio.MusicType.Credits); }
    public override void OnClose() { Audio.Music.SetContext(G.Player?.Creative == true ? Audio.MusicType.Creative : Audio.MusicType.Game); }
    public override void Tick()
    {
        _prevScroll = _scroll; _scroll += .5f;
        if (_scroll > Lines.Length * 12 + H + 40) G.SetScreen(null);
    }
    public override void Render(int mx, int my, float pt)
    {
        G.Sb.Fill(0, 0, W, H, 0xFF000000);
        float s = _prevScroll + (_scroll - _prevScroll) * pt, y0 = H + 20 - s;
        for (int i = 0; i < Lines.Length; i++)
        {
            float y = y0 + i * 12;
            if (y < -12 || y > H) continue;
            bool title = i == 0;
            G.Sb.TextCentered(Lines[i], W / 2f, y, 0xFFFFFFFF, true, title ? 2 : 1);
        }
        base.Render(mx, my, pt);
    }
}
