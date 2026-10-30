using BlockCraft.Audio;
using BlockCraft.Entities;
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
            SetScreen(null);
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
}
