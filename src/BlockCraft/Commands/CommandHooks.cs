using BlockCraft.Cmd;
using BlockCraft.Core;
using BlockCraft.Entities;

namespace BlockCraft.World
{
    /// <summary>Stan komend w świecie: scoreboard i granica (tylko w Overworldzie hosta), tick kryteriów i obrażeń granicy.</summary>
    public sealed partial class World
    {
        public Scoreboard ScoreboardField;
        public WorldBorder BorderField;
        public int DefaultGameMode;

        /// <summary>Wołane z World.Tick na hoście.</summary>
        private void TickCommands()
        {
            var border = WorldBorder.Of(this);
            // EntityLivingBase.onEntityUpdate: gracz poza granicą (za buforem) dostaje obrażenia inWall
            foreach (var p in Players)
            {
                if (p.Dead || p.Health <= 0 || p.Creative || p.Spectator) continue;
                if (border.Contains(this, p.BoundingBox)) continue;
                double d = border.ClosestDistance(this, p.X, p.Z) + border.DamageBuffer;
                if (d < 0) p.Attack(DamageSource.InWall, Math.Max(1, MathUtil.FloorToInt(-d * border.DamageAmount)));
            }
            if (Commands.Home(this) != this) return;
            var sb = Scoreboard.Of(this);
            if (sb.Objectives.Count == 0) return;
            foreach (var w in Commands.Worlds(this))
                foreach (var p in w.Players)
                {
                    sb.SetByCriteria(p.Name, "health", p.Dead ? 0 : (int)Math.Ceiling(p.Health));
                    foreach (var o in sb.Objectives.Values)
                    {
                        if (o.Criteria.StartsWith("achievement.") && p.Achievements.Contains(o.Criteria[12..]) && !sb.HasScore(p.Name, o))
                            sb.SetScore(p.Name, o, 1);
                    }
                    MirrorStats(sb, p);
                }
        }

        /// <summary>Kryteria stat.*: wynik rośnie o przyrost statystyki gracza (Player.Statistics, jak StatisticsFile.func_150873_a).</summary>
        private static void MirrorStats(Scoreboard sb, Player p)
        {
            foreach (var o in sb.Objectives.Values)
            {
                if (!o.Criteria.StartsWith("stat.")) continue;
                int now = p.Statistics.GetValueOrDefault(o.Criteria);
                string key = p.Name + "\n" + o.Criteria;
                if (!sb.StatSeen.TryGetValue(key, out int seen)) { sb.StatSeen[key] = now; continue; }
                if (now > seen) sb.GetScore(p.Name, o).Points += now - seen;
                if (now != seen) { sb.StatSeen[key] = now; sb.Dirty = true; }
            }
        }

        /// <summary>World.getCollidingBoundingBoxes 1.8: dla ciała wewnątrz granicy bloki za granicą zachowują się jak kamień.</summary>
        private void AddBorderBoxes(AABB box, List<AABB> res)
        {
            if (IsRemote && WorldBorder.Client.EndDiameter >= 59999968) return;
            var border = WorldBorder.Of(this);
            var b = border.Bounds(this);
            double cx = (box.MinX + box.MaxX) / 2, cz = (box.MinZ + box.MaxZ) / 2;
            if (!(cx > b.MinX - 1 && cx < b.MaxX + 1 && cz > b.MinZ - 1 && cz < b.MaxZ + 1)) return;
            if (box.MinX - 1 > b.MinX && box.MaxX + 1 < b.MaxX && box.MinZ - 1 > b.MinZ && box.MaxZ + 1 < b.MaxZ) return;
            int x0 = MathUtil.FloorToInt(box.MinX), x1 = MathUtil.FloorToInt(box.MaxX), z0 = MathUtil.FloorToInt(box.MinZ), z1 = MathUtil.FloorToInt(box.MaxZ);
            if (x1 - x0 > 8 || z1 - z0 > 8) return;
            int y0 = MathUtil.FloorToInt(box.MinY) - 1, y1 = MathUtil.FloorToInt(box.MaxY) + 1;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (border.Contains(this, x, z)) continue;
                    for (int y = y0; y <= y1; y++)
                    {
                        var bb = new AABB(x, y, z, x + 1, y + 1, z + 1);
                        if (bb.Intersects(box)) res.Add(bb);
                    }
                }
        }
    }
}

namespace BlockCraft.Entities
{
    public abstract partial class LivingEntity
    {
        /// <summary>Haki na śmierć istoty (scoreboard: deathCount, playerKillCount, totalKillCount, teamkill, stat.*).</summary>
        public static Action<LivingEntity, DamageSource> DeathHook = ScoreDeaths.OnDeath;
    }

    public partial class Player
    {
        /// <summary>Stan tytułu (/title) wyświetlany na HUD tego gracza.</summary>
        public readonly TitleState Title = new();
    }
}

namespace BlockCraft.Cmd
{
    public sealed class TitleState
    {
        public string Title = "", Subtitle = "";
        public int FadeIn = 10, Stay = 70, FadeOut = 20, Ticks;
        public void Show(int action, string text, int fi, int st, int fo)
        {
            switch (action)
            {
                case 0: Title = text; Ticks = FadeIn + Stay + FadeOut; break;
                case 1: Subtitle = text; break;
                case 2: if (fi >= 0) FadeIn = fi; if (st >= 0) Stay = st; if (fo >= 0) FadeOut = fo; if (Ticks > 0) Ticks = FadeIn + Stay + FadeOut; break;
                case 3: Title = Subtitle = ""; Ticks = 0; break;
                case 4: Title = Subtitle = ""; Ticks = 0; FadeIn = 10; Stay = 70; FadeOut = 20; break;
            }
        }
        public void Tick() { if (Ticks > 0) Ticks--; }
        /// <summary>Krycie (GuiIngame.renderGameOverlay, titlesTimer).</summary>
        public float Alpha(float pt)
        {
            if (Ticks <= 0) return 0;
            float t = Ticks - pt, total = FadeIn + Stay + FadeOut;
            float a = 1;
            if (t > FadeOut + Stay) a = (total - t) / FadeIn;
            if (t <= FadeOut) a = t / FadeOut;
            return Math.Clamp(a, 0, 1);
        }
    }

    /// <summary>Kryteria śmierci i zabójstw scoreboardu (EntityPlayer.onDeath, onKillEntity, func_96646_a).</summary>
    public static class ScoreDeaths
    {
        public static void OnDeath(LivingEntity victim, DamageSource src)
        {
            if (victim.World == null || victim.World.IsRemote) return;
            var sb = Scoreboard.Of(victim.World);
            if (sb.Objectives.Count == 0) return;
            Player killer = src?.Attacker as Player ?? (victim.LastAttacker as Player);
            string killerName = killer?.Name;
            if (victim is Player vp)
            {
                sb.AddByCriteria(vp.Name, "deathCount", 1);
                if (killer != null && killer != vp)
                {
                    sb.AddByCriteria(killerName, "playerKillCount", 1);
                    var kt = sb.TeamOf(killerName); var vt = sb.TeamOf(vp.Name);
                    if (vt != null && vt.Color != 'r') sb.AddByCriteria(killerName, "teamkill." + TextComponent.ColorName(vt.Color), 1);
                    if (kt != null && kt.Color != 'r') sb.AddByCriteria(vp.Name, "killedByTeam." + TextComponent.ColorName(kt.Color), 1);
                }
            }
            if (killer != null && killer != victim)
            {
                sb.AddByCriteria(killerName, "totalKillCount", 1);
            }
        }
    }
}
