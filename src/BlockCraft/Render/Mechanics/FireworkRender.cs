using BlockCraft.Core;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Kolejka efektów fajerwerków (logika encji -> renderer świata, tylko wizualnie i lokalnie, jak cząsteczki klienta MC).</summary>
public static class FireworkFx
{
    internal record struct Ev(World.World W, Vector3d Pos, Vector3d Motion, Nbt Fireworks, bool Trail);
    internal static readonly List<Ev> Pending = new();
    /// <summary>Liczba wybuchów (do testów).</summary>
    public static int Explosions;

    public static void Trail(World.World w, double x, double y, double z, double motionY)
    {
        lock (Pending) if (Pending.Count < 2000) Pending.Add(new Ev(w, new Vector3d(x, y, z), new Vector3d(0, motionY, 0), null, true));
    }
    public static void Explode(World.World w, double x, double y, double z, double mx, double my, double mz, Nbt fireworks)
    {
        Explosions++;
        lock (Pending) if (Pending.Count < 4000) Pending.Add(new Ev(w, new Vector3d(x, y, z), new Vector3d(mx, my, mz), fireworks, false));
    }
}

public sealed partial class WorldRenderer
{
    private sealed class Spark
    {
        public Vector3d Pos, Prev, Vel; public int Age, MaxAge; public float R = 1, G = 1, B = 1, A = 1, Scale;
        public bool Trail, Twinkle, HasFade; public float FR, FG, FB;
    }
    private sealed class Starter
    {
        public Vector3d Pos, Motion; public List<Nbt> Explosions; public int Age, MaxAge; public bool Twinkle;
    }
    private sealed class Flash { public Vector3d Pos; public int Age; public float R, G, B; }

    private readonly List<Spark> _sparks = new();
    private readonly List<Starter> _starters = new();
    private readonly List<Flash> _flashes = new();
    private readonly List<(int ticks, string ev, Vector3d pos, float vol, float pitch)> _delayedSounds = new();
    private readonly Rng _fr = new(1717);

    private double Gauss() { double u = 1 - _fr.NextDouble(), v = _fr.NextDouble(); return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v); }

    private Spark AddSpark(Vector3d pos, Vector3d vel)
    {
        if (_sparks.Count > 6000) return null;
        // EntityFX: skala (0.5-1)*2, iskra *0.75; maxAge 48 + rand(12)
        var s = new Spark { Pos = pos, Prev = pos, Vel = vel, MaxAge = 48 + _fr.Next(12), Scale = (_fr.NextFloat() * .5f + .5f) * 2f * .75f };
        _sparks.Add(s);
        return s;
    }

    private void CreateParticle(Vector3d pos, double mx, double my, double mz, int[] colors, int[] fade, bool trail, bool twinkle)
    {
        var s = AddSpark(pos, new Vector3d(mx, my, mz));
        if (s == null) return;
        s.Trail = trail; s.Twinkle = twinkle;
        int c = colors[_fr.Next(colors.Length)];
        s.R = (c >> 16 & 255) / 255f; s.G = (c >> 8 & 255) / 255f; s.B = (c & 255) / 255f;
        if (fade != null && fade.Length > 0)
        {
            int f = fade[_fr.Next(fade.Length)];
            s.HasFade = true; s.FR = (f >> 16 & 255) / 255f; s.FG = (f >> 8 & 255) / 255f; s.FB = (f & 255) / 255f;
        }
    }

    private static readonly double[][] StarShape = { new[] { 0.0, 1.0 }, new[] { .3455, .309 }, new[] { .9511, .309 }, new[] { .3795918367346939, -.12653061224489795 }, new[] { .6122448979591837, -.8040816326530612 }, new[] { 0.0, -.35918367346938773 } };
    private static readonly double[][] CreeperShape = { new[] { 0.0, .2 }, new[] { .2, .2 }, new[] { .2, .6 }, new[] { .6, .6 }, new[] { .6, .2 }, new[] { .2, .2 }, new[] { .2, 0.0 }, new[] { .4, 0.0 }, new[] { .4, -.6 }, new[] { .2, -.6 }, new[] { .2, -.4 }, new[] { 0.0, -.4 } };

    private void CreateBall(Starter st, double speed, int size, int[] c, int[] f, bool trail, bool tw)
    {
        for (int i = -size; i <= size; ++i)
            for (int j = -size; j <= size; ++j)
                for (int k = -size; k <= size; ++k)
                {
                    double d3 = j + (_fr.NextDouble() - _fr.NextDouble()) * .5, d4 = i + (_fr.NextDouble() - _fr.NextDouble()) * .5, d5 = k + (_fr.NextDouble() - _fr.NextDouble()) * .5;
                    double d6 = Math.Sqrt(d3 * d3 + d4 * d4 + d5 * d5) / speed + Gauss() * .05;
                    CreateParticle(st.Pos, d3 / d6, d4 / d6, d5 / d6, c, f, trail, tw);
                    if (i != -size && i != size && j != -size && j != size) k += size * 2 - 1;
                }
    }

    private void CreateShaped(Starter st, double speed, double[][] shape, int[] c, int[] f, bool trail, bool tw, bool creeper)
    {
        double d0 = shape[0][0], d1 = shape[0][1];
        CreateParticle(st.Pos, d0 * speed, d1 * speed, 0, c, f, trail, tw);
        float a = _fr.NextFloat() * MathF.PI;
        double d2 = creeper ? .034 : .34;
        for (int i = 0; i < 3; ++i)
        {
            double d3 = a + i * MathF.PI * d2;
            double d4 = d0, d5 = d1;
            for (int j = 1; j < shape.Length; ++j)
            {
                double d6 = shape[j][0], d7 = shape[j][1];
                for (double d8 = .25; d8 <= 1.0; d8 += .25)
                {
                    double d9 = (d4 + (d6 - d4) * d8) * speed, d10 = (d5 + (d7 - d5) * d8) * speed;
                    double d11 = d9 * Math.Sin(d3);
                    d9 *= Math.Cos(d3);
                    for (double d12 = -1; d12 <= 1; d12 += 2) CreateParticle(st.Pos, d9 * d12, d10, d11 * d12, c, f, trail, tw);
                }
                d4 = d6; d5 = d7;
            }
        }
    }

    private void CreateBurst(Starter st, int[] c, int[] f, bool trail, bool tw)
    {
        double d0 = Gauss() * .05, d1 = Gauss() * .05;
        for (int i = 0; i < 70; ++i)
        {
            double d2 = st.Motion.X * .5 + Gauss() * .15 + d0, d3 = st.Motion.Z * .5 + Gauss() * .15 + d1, d4 = st.Motion.Y * .5 + _fr.NextDouble() * .5;
            CreateParticle(st.Pos, d2, d4, d3, c, f, trail, tw);
        }
    }

    /// <summary>World.playSound z distanceDelay: dalej niż 10 bloków dźwięk opóźniony o odległość / 40 bloków na sekundę.</summary>
    private void PlayDelayed(string ev, Vector3d pos, float vol, float pitch)
    {
        double d = Math.Sqrt((pos - _camPos).LengthSquared);
        int delay = d * d > 100 ? (int)(d / 40.0 * 20.0) : 0;
        if (delay <= 0) Sound.Play(ev, pos.X, pos.Y, pos.Z, vol, pitch);
        else _delayedSounds.Add((delay, ev, pos, vol, pitch));
    }

    private bool Far(Vector3d pos) => (pos - _camPos).LengthSquared >= 256;

    /// <summary>EntityFireworkStarterFX + iskry + błysk: wołane co tick z TickParticles.</summary>
    private void TickFireworks()
    {
        List<FireworkFx.Ev> evs;
        lock (FireworkFx.Pending) { evs = FireworkFx.Pending.ToList(); FireworkFx.Pending.Clear(); }
        foreach (var e in evs)
        {
            if (e.W != World) continue;
            if (e.Trail) { AddSpark(e.Pos - new Vector3d(0, .3, 0), new Vector3d(Gauss() * .05, -e.Motion.Y * .5, Gauss() * .05)); continue; }
            var list = e.Fireworks?.GetList("Explosions")?.OfType<Nbt>().ToList();
            if (list == null || list.Count == 0) continue;
            var st = new Starter { Pos = e.Pos, Motion = e.Motion, Explosions = list, MaxAge = list.Count * 2 - 1 };
            if (list.Any(x => x.GetBool("Flicker"))) { st.Twinkle = true; st.MaxAge += 15; }
            _starters.Add(st);
        }
        for (int i = _delayedSounds.Count - 1; i >= 0; i--)
        {
            var d = _delayedSounds[i];
            if (--d.ticks <= 0) { Sound.Play(d.ev, d.pos.X, d.pos.Y, d.pos.Z, d.vol, d.pitch); _delayedSounds.RemoveAt(i); }
            else _delayedSounds[i] = d;
        }
        for (int i = _starters.Count - 1; i >= 0; i--)
        {
            var st = _starters[i];
            if (st.Age == 0)
            {
                bool large = st.Explosions.Count >= 3 || st.Explosions.Any(x => x.GetInt("Type") == Fireworks.Large);
                PlayDelayed("fireworks." + (large ? "largeBlast" : "blast") + (Far(st.Pos) ? "_far" : ""), st.Pos, 20f, .95f + _fr.NextFloat() * .1f);
            }
            if (st.Age % 2 == 0 && st.Age / 2 < st.Explosions.Count)
            {
                var ex = st.Explosions[st.Age / 2];
                int type = ex.GetInt("Type"); bool trail = ex.GetBool("Trail"), flick = ex.GetBool("Flicker");
                var colors = ex["Colors"] as int[] ?? (ex.TryGetValue("Colors", out var co) && co is List<object> lo ? lo.Select(Convert.ToInt32).ToArray() : null);
                if (colors == null || colors.Length == 0) colors = new[] { Fireworks.DyeColors[0] };
                var fade = ex.TryGetValue("FadeColors", out var fo) ? fo as int[] : null;
                switch (type)
                {
                    case Fireworks.Large: CreateBall(st, .5, 4, colors, fade, trail, flick); break;
                    case Fireworks.Star: CreateShaped(st, .5, StarShape, colors, fade, trail, flick, false); break;
                    case Fireworks.Creeper: CreateShaped(st, .5, CreeperShape, colors, fade, trail, flick, true); break;
                    case Fireworks.Burst: CreateBurst(st, colors, fade, trail, flick); break;
                    default: CreateBall(st, .25, 2, colors, fade, trail, flick); break;
                }
                int c0 = colors[0];
                _flashes.Add(new Flash { Pos = st.Pos, R = (c0 >> 16 & 255) / 255f, G = (c0 >> 8 & 255) / 255f, B = (c0 & 255) / 255f });
            }
            st.Age++;
            if (st.Age > st.MaxAge)
            {
                if (st.Twinkle) PlayDelayed("fireworks." + (Far(st.Pos) ? "twinkle_far" : "twinkle"), st.Pos, 20f, .9f + _fr.NextFloat() * .15f);
                _starters.RemoveAt(i);
            }
        }
        for (int i = _flashes.Count - 1; i >= 0; i--) if (++_flashes[i].Age >= 4) _flashes.RemoveAt(i);
        var w = World;
        int n = _sparks.Count;
        for (int i = 0; i < n; i++)
        {
            var s = _sparks[i];
            s.Prev = s.Pos;
            if (s.Age++ >= s.MaxAge) { s.MaxAge = -1; continue; }
            if (s.Age > s.MaxAge / 2)
            {
                s.A = 1f - (s.Age - s.MaxAge / 2f) / s.MaxAge;
                if (s.HasFade) { s.R += (s.FR - s.R) * .2f; s.G += (s.FG - s.G) * .2f; s.B += (s.FB - s.B) * .2f; }
            }
            s.Vel.Y -= .004;
            var np = s.Pos + s.Vel;
            bool ground = false;
            if (w != null)
            {
                var b = Block.Get(w.GetBlock(MathUtil.FloorToInt(np.X), MathUtil.FloorToInt(np.Y), MathUtil.FloorToInt(np.Z)));
                if (b.Solid && b.FullCube) { if (s.Vel.Y < 0) ground = true; np = new Vector3d(np.X, s.Pos.Y, np.Z); s.Vel.Y = 0; }
            }
            s.Pos = np;
            s.Vel *= .91;
            if (ground) { s.Vel.X *= .7; s.Vel.Z *= .7; }
            if (s.Trail && s.Age < s.MaxAge / 2 && (s.Age + s.MaxAge) % 2 == 0)
            {
                var c = AddSpark(s.Pos, Vector3d.Zero);
                if (c != null)
                {
                    c.A = .99f; c.R = s.R; c.G = s.G; c.B = s.B; c.Age = c.MaxAge / 2;
                    c.HasFade = s.HasFade; c.FR = s.FR; c.FG = s.FG; c.FB = s.FB; c.Twinkle = s.Twinkle;
                }
            }
        }
        _sparks.RemoveAll(s => s.MaxAge < 0);
    }

    private void RenderFireworks(float pt, Vector3d cam)
    {

        if (_sparks.Count == 0 && _flashes.Count == 0) return;
        var b = Entities.Terrain;
        b.Sky = 15; b.Blk = 15; b.Overlay = Vector4.Zero;
        foreach (var s in _sparks)
        {
            if (s.Twinkle && !(s.Age < s.MaxAge / 3 || (s.Age + s.MaxAge) / 3 % 2 == 0)) continue;
            var pos = s.Prev + (s.Pos - s.Prev) * pt;
            var rel = new Vector3((float)(pos.X - cam.X), (float)(pos.Y - cam.Y), (float)(pos.Z - cam.Z));
            if (rel.LengthSquared > 160 * 160) continue;
            int frame = Math.Clamp(7 - s.Age * 8 / Math.Max(1, s.MaxAge), 0, 7);
            b.Layer = TextureGen.Layer("particle_firework_" + frame);
            b.Color = new Vector4(s.R, s.G, s.B, s.A);
            float h = .1f * s.Scale;
            var r = _right * h; var u = _up * h;
            b.Quad(rel - r + u, rel - r - u, rel + r - u, rel + r + u, 0, 0, 1, 1, 1f);
        }
        foreach (var f in _flashes)
        {
            float t = f.Age + pt - 1;
            float size = 7.1f * MathF.Sin(t * .25f * MathF.PI), alpha = .6f - t * .25f * .5f;
            if (size <= 0 || alpha <= 0) continue;
            var rel = new Vector3((float)(f.Pos.X - cam.X), (float)(f.Pos.Y - cam.Y), (float)(f.Pos.Z - cam.Z));
            b.Layer = TextureGen.Layer("particle_firework_flash");
            b.Color = new Vector4(f.R, f.G, f.B, alpha);
            var r = _right * size; var u = _up * size;
            b.Quad(rel - r + u, rel - r - u, rel + r - u, rel + r + u, 0, 0, 1, 1, 1f);
        }
        b.Color = Vector4.One;
    }

    public int FireworkSparkCount => _sparks.Count;
}

public static partial class TextureGen
{
    private static void TexFireworks()
    {
        // iskra: 8 klatek (particles.png 160-167), od dużej gwiazdki do kropki
        for (int f = 0; f < 8; f++)
        {
            var px = New(); float r = 1 + f;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = Math.Abs(x - 7.5f), dy = Math.Abs(y - 7.5f);
                    bool cross = (dx < .6f && dy <= r) || (dy < .6f && dx <= r);
                    bool core = dx + dy <= r * .55f;
                    if (cross || core) px[y * 16 + x] = Hex(0xFFFFFF, core ? 255 : 200);
                }
            Add("particle_firework_" + f, px);
        }
        var flash = New();
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float d = MathF.Sqrt((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f)) / 8f;
                if (d < 1) flash[y * 16 + x] = Hex(0xFFFFFF, (int)(255 * (1 - d) * (1 - d)));
            }
        Add("particle_firework_flash", flash);
        var rocket = New();
        for (int y = 3; y < 15; y++) for (int x = 6; x < 10; x++) rocket[y * 16 + x] = Hex(y < 6 ? 0xC03030u : (y / 2) % 2 == 0 ? 0xE8E0D0u : 0xC03030u);
        for (int y = 0; y < 3; y++) rocket[y * 16 + 7] = rocket[y * 16 + 8] = Hex(0x8A6A40);
        rocket[15 * 16 + 7] = Hex(0x404040);
        Add("item_fireworks", rocket);
        var star = New();
        for (int y = 3; y < 14; y++) for (int x = 3; x < 14; x++) { float d = (x - 8) * (x - 8) + (y - 8.5f) * (y - 8.5f); if (d < 26) star[y * 16 + x] = Hex(d < 10 ? 0xFFFFFFu : 0xD0D0D0u); }
        Add("item_firework_charge", star);
    }
}
