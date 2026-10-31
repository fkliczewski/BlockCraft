using BlockCraft.Core;

namespace BlockCraft.Render;

public struct BoxDef
{
    public float X, Y, Z, W, H, D, Inflate; public int U, V; public bool Mirror;
}

/// <summary>Część modelu jak ModelRenderer w MC (piksele, oś Y w dół).</summary>
public sealed class ModelPart
{
    public float PX, PY, PZ, RX, RY, RZ;
    public bool Visible = true;
    public readonly List<BoxDef> Boxes = new();
    public readonly List<ModelPart> Children = new();
    public ModelPart(float px = 0, float py = 0, float pz = 0) { PX = px; PY = py; PZ = pz; }
    public ModelPart Box(int u, int v, float x, float y, float z, float w, float h, float d, float inflate = 0, bool mirror = false)
    {
        Boxes.Add(new BoxDef { U = u, V = v, X = x, Y = y, Z = z, W = w, H = h, D = d, Inflate = inflate, Mirror = mirror });
        return this;
    }
    public void Reset(float px, float py, float pz) { PX = px; PY = py; PZ = pz; RX = RY = RZ = 0; }
}

/// <summary>Skórki mobów rysowane proceduralnie w układzie UV jak w MC (64x32).</summary>
public static partial class SkinGen
{
    public const int W = 64, H = 64;
    public static readonly List<uint[]> Skins = new();
    public static readonly Dictionary<string, int> Index = new();

    private sealed class Canvas
    {
        public readonly uint[] P = new uint[W * H];
        public readonly Rng R;
        public Canvas(int seed) { R = new Rng(seed); }
        public void Set(int x, int y, uint c) { if (x >= 0 && x < W && y >= 0 && y < H) P[y * W + x] = c; }
        public void Rect(int x, int y, int w, int h, uint c) { for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(x + i, y + j, c); }
        public void Noise(int x, int y, int w, int h, uint c, float amt)
        {
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) Set(x + i, y + j, TextureGen.Mul(c, 1 - amt / 2 + R.NextFloat() * amt));
        }
        /// <summary>Wypełnij całe UV boxa (wszystkie ściany) funkcją (ściana, x, y, szerokość, wysokość).</summary>
        public void Box(int u, int v, int w, int h, int d, Func<int, int, int, int, int, uint> f)
        {
            void Face(int face, int fx, int fy, int fw, int fh)
            {
                for (int j = 0; j < fh; j++) for (int i = 0; i < fw; i++) { uint c = f(face, i, j, fw, fh); if (c != 1) Set(fx + i, fy + j, c); }
            }
            Face(0, u + d, v, w, d);          // góra
            Face(1, u + d + w, v, w, d);      // dół
            Face(2, u, v + d, d, h);          // prawa
            Face(3, u + d, v + d, w, h);      // przód
            Face(4, u + d + w, v + d, d, h);  // lewa
            Face(5, u + d + w + d, v + d, w, h); // tył
        }
    }

    private static uint Hx(uint c) => TextureGen.Hex(c);
    private static void Add(string name, Canvas c) { Index[name] = Skins.Count; Skins.Add(c.P); }
    public static int Get(string name) { Ensure(); return Index.TryGetValue(name, out int i) ? i : 0; }

    private static bool _init;
    public static void Ensure()
    {
        if (_init) return; _init = true;
        Steve(); Zombie(); Skeleton(); Creeper(); Spider(); Enderman(); Cow(); Pig(); Sheep(); SheepFur(); Chicken();
        SkinsOverworld(); SkinsRedstone(); SkinsNetherEnd(); SkinsMobs(); SkinsMechanics();
    }
    // skórki modułów (64x64; większe tekstury moduł trzyma we własnej tablicy)
    static partial void SkinsOverworld();
    static partial void SkinsRedstone();
    static partial void SkinsNetherEnd();
    static partial void SkinsMobs();
    static partial void SkinsMechanics();

    private static uint V(Rng r, long c, float a = 0.12f) => TextureGen.Mul(Hx((uint)c), 1 - a / 2 + r.NextFloat() * a);

    // ---------- humanoidy ----------
    private static void Humanoid(Canvas c, Func<int, int, int, int, int, uint> head, Func<int, int, int, int, int, uint> body,
        Func<int, int, int, int, int, uint> arm, Func<int, int, int, int, int, uint> leg, int armW = 4, int armD = 4)
    {
        c.Box(0, 0, 8, 8, 8, head);
        c.Box(16, 16, 8, 12, 4, body);
        c.Box(40, 16, armW, 12, armD, arm);
        c.Box(0, 16, armW, 12, armD, leg);
    }

    private static void Steve()
    {
        var c = new Canvas(1); var r = c.R;
        uint skin = 0xB4846D, skinD = 0x9C6E57, hair = 0x3B2213, hairD = 0x2B1A0D;
        Humanoid(c,
            (f, x, y, w, h) =>
            {
                if (f == 0) return V(r, hair, 0.15f);
                if (f == 1) return V(r, skin);
                if (f == 5) return V(r, y < 6 ? hair : hairD, 0.15f);
                if (f == 3)
                {
                    if (y < 2) return V(r, hair, 0.15f);
                    if (y == 2 && (x == 0 || x == 7)) return V(r, hair);
                    if (y == 4 && (x == 1 || x == 6)) return Hx(0xFFFFFF);
                    if (y == 4 && (x == 2 || x == 5)) return Hx(0x523D89);
                    if (y == 3 && (x >= 1 && x <= 2 || x >= 5 && x <= 6)) return Hx(0x2B1A0D);
                    if (y == 5 && (x == 3 || x == 4)) return Hx(0x8D5E4B);
                    if (y == 6 && x >= 2 && x <= 5) return Hx(0x6A3E30);
                    return V(r, skin, 0.08f);
                }
                return V(r, y < 3 ? hair : (y < 4 && (x == 0 || x == 7) ? hair : skin), 0.1f);
            },
            (f, x, y, w, h) => f == 0 || f == 1 ? V(r, 0x00AFAF) : V(r, y >= 10 ? 0x3C3C9B : (y < 1 && f == 3 && x >= 3 && x <= 4 ? skin : 0x00AFAF), 0.12f),
            (f, x, y, w, h) => V(r, (f == 0 || y < 4) && f != 1 ? 0x00AFAF : (f == 1 ? skinD : skin), 0.1f),
            (f, x, y, w, h) => V(r, y >= 9 ? 0x535353 : 0x3C3C9B, 0.12f));
        Add("steve", c);
    }

    private static void Zombie()
    {
        var c = new Canvas(2); var r = c.R;
        uint skin = 0x4A7A3A, skinD = 0x3C6630;
        Humanoid(c,
            (f, x, y, w, h) =>
            {
                if (f == 3)
                {
                    if (y == 3 && (x == 1 || x == 2 || x == 5 || x == 6)) return Hx(0x111F11);
                    if (y == 4 && (x == 1 || x == 2 || x == 5 || x == 6)) return Hx(0x1E3A1E);
                    if (y == 6 && x >= 2 && x <= 5) return Hx(0x2A4A22);
                    if (y == 5 && (x == 3 || x == 4)) return Hx(0x355E2A);
                }
                if (f == 0 || f == 5 && y < 3) return V(r, skinD, 0.2f);
                return V(r, skin, 0.18f);
            },
            (f, x, y, w, h) => V(r, y >= 10 ? 0x46469A : 0x00A0A0, 0.15f),
            (f, x, y, w, h) => V(r, y < 4 && f != 1 ? 0x00A0A0 : skin, 0.15f),
            (f, x, y, w, h) => V(r, y >= 10 ? 0x3A3A3A : 0x46469A, 0.15f));
        Add("zombie", c);
    }

    private static void Skeleton()
    {
        var c = new Canvas(3); var r = c.R;
        uint bone = 0xC8C8C8, dark = 0x7A7A7A;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y >= 3 && y <= 4 && (x == 1 || x == 2 || x == 5 || x == 6)) return Hx(0x141414);
                if (y == 5 && (x == 3 || x == 4)) return Hx(0x303030);
                if (y == 6 && x >= 1 && x <= 6) return (x % 2 == 0) ? Hx(0x303030) : V(r, bone);
            }
            return V(r, bone, 0.15f);
        });
        c.Box(16, 16, 8, 12, 4, (f, x, y, w, h) =>
        {
            if (f == 3 || f == 5)
            {
                if (x == 3 || x == 4) return V(r, bone);
                if (y % 3 == 1 && y < 9) return V(r, bone);
                if (y >= 10) return V(r, dark);
                return 0; // przezroczyste żebra
            }
            return y % 3 == 1 ? V(r, bone) : 0;
        });
        c.Box(40, 16, 2, 12, 2, (f, x, y, w, h) => V(r, bone, 0.15f));
        c.Box(0, 16, 2, 12, 2, (f, x, y, w, h) => V(r, bone, 0.15f));
        Add("skeleton", c);
    }

    private static void Creeper()
    {
        var c = new Canvas(4); var r = c.R;
        uint[] pal = { 0x0DA70D, 0x59CC50, 0x3B8F35, 0x0F7A0F, 0x8AD080, 0x0DA70D };
        uint Cam() => TextureGen.Hex(pal[r.Next(pal.Length)]);
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                bool eye = y >= 2 && y <= 3 && (x is 1 or 2 or 5 or 6);
                bool mouth = (y == 4 && (x == 3 || x == 4)) || (y == 5 && x >= 2 && x <= 5) || (y == 6 && (x == 2 || x == 5)) || (y == 7 && (x == 2 || x == 5));
                if (eye || mouth) return Hx(0x0A0A0A);
            }
            return Cam();
        });
        c.Box(16, 16, 8, 12, 4, (f, x, y, w, h) => Cam());
        c.Box(0, 16, 4, 6, 4, (f, x, y, w, h) => f == 1 ? Hx(0x1E3A1E) : Cam());
        Add("creeper", c);
    }

    private static void Spider()
    {
        var c = new Canvas(5); var r = c.R;
        uint a = 0x3D3530, b = 0x2A2420;
        c.Box(32, 4, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if ((y == 2 || y == 3) && (x == 1 || x == 6)) return Hx(0xCC0000);
                if (y == 4 && (x == 2 || x == 3 || x == 4 || x == 5)) return Hx(0xFF2020);
                if (y == 3 && (x == 3 || x == 4)) return Hx(0x990000);
            }
            return V(r, r.NextBool() ? a : b, 0.2f);
        });
        c.Box(0, 0, 6, 6, 6, (f, x, y, w, h) => V(r, b, 0.2f));
        c.Box(0, 12, 10, 8, 12, (f, x, y, w, h) => V(r, r.Next(3) == 0 ? 0x4A3F38 : a, 0.2f));
        c.Box(18, 0, 16, 2, 2, (f, x, y, w, h) => V(r, b, 0.2f));
        Add("spider", c);
    }

    private static void Enderman()
    {
        var c = new Canvas(6); var r = c.R;
        uint bk = 0x161616, bk2 = 0x0A0A0A;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3 && y == 4 && x >= 0 && x <= 7 && x != 3 && x != 4) return (x == 1 || x == 6) ? Hx(0xE079FA) : Hx(0xCC00FA);
            return V(r, r.NextBool() ? bk : bk2, 0.3f);
        });
        c.Box(32, 16, 8, 12, 4, (f, x, y, w, h) => V(r, bk, 0.3f));
        c.Box(56, 0, 2, 30, 2, (f, x, y, w, h) => V(r, bk2, 0.3f));
        Add("enderman", c);
    }

    private static void Cow()
    {
        var c = new Canvas(7); var r = c.R;
        uint br = 0x4A3424, wh = 0xD8D8D8;
        var spots = TextureGen.TileNoise(77, 4);
        c.Box(0, 0, 8, 8, 6, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 3 && (x == 1 || x == 6)) return Hx(0x111111);
                if (y == 3 && (x == 2 || x == 5)) return Hx(0xFFFFFF);
                if (y >= 5 && x >= 2 && x <= 5) return y == 6 && (x == 2 || x == 5) ? Hx(0x3A2A2A) : V(r, 0xB49A8A);
                if (x >= 2 && x <= 5 && y <= 4) return V(r, wh);
            }
            return V(r, br, 0.15f);
        });
        c.Box(22, 0, 1, 3, 1, (f, x, y, w, h) => V(r, 0xA0A0A0));
        c.Box(18, 4, 12, 18, 10, (f, x, y, w, h) =>
        {
            float n = spots[((y * 3 + f * 5) & 15) * 16 + ((x * 3 + f * 7) & 15)];
            return V(r, n > 0.62f ? wh : br, 0.12f);
        });
        c.Box(52, 0, 4, 6, 1, (f, x, y, w, h) => V(r, 0xE0A0A0));
        c.Box(0, 16, 4, 12, 4, (f, x, y, w, h) => V(r, y > 9 ? 0x2A1E14 : (y < 4 && r.Next(3) == 0 ? wh : br), 0.12f));
        Add("cow", c);
    }

    private static void Pig()
    {
        var c = new Canvas(8); var r = c.R;
        uint pk = 0xF0A0A0, pkD = 0xE08A8A;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 3 && (x == 1 || x == 6)) return Hx(0xFFFFFF);
                if (y == 3 && (x == 2 || x == 5)) return Hx(0x111111);
            }
            return V(r, pk, 0.08f);
        });
        c.Box(16, 16, 4, 3, 1, (f, x, y, w, h) => f == 3 && y == 1 && (x == 0 || x == 3) ? Hx(0x8A4A4A) : V(r, 0xD47878, 0.05f));
        c.Box(28, 8, 10, 16, 8, (f, x, y, w, h) => V(r, pk, 0.08f));
        c.Box(0, 16, 4, 6, 4, (f, x, y, w, h) => V(r, y > 4 ? 0x9A6060 : pkD, 0.08f));
        Add("pig", c);
    }

    private static void Sheep()
    {
        var c = new Canvas(9); var r = c.R;
        uint skin = 0xD9C6B4, body = 0xC8B0A0;
        c.Box(0, 0, 6, 6, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 2 && (x == 0 || x == 5)) return Hx(0xFFFFFF);
                if (y == 2 && (x == 1 || x == 4)) return Hx(0x111111);
                if (y == 4 && (x == 2 || x == 3)) return Hx(0xB08080);
            }
            return V(r, skin, 0.08f);
        });
        c.Box(28, 8, 8, 16, 6, (f, x, y, w, h) => V(r, body, 0.1f));
        c.Box(0, 16, 4, 12, 4, (f, x, y, w, h) => V(r, y > 9 ? 0x3A3030 : skin, 0.08f));
        Add("sheep", c);
    }

    private static void SheepFur()
    {
        var c = new Canvas(10); var r = c.R;
        uint wool = 0xEAEAEA;
        c.Box(0, 0, 6, 6, 6, (f, x, y, w, h) => f == 3 ? 0 : V(r, wool, 0.15f));
        c.Box(28, 8, 8, 16, 6, (f, x, y, w, h) => V(r, wool, 0.15f));
        c.Box(0, 16, 4, 6, 4, (f, x, y, w, h) => V(r, wool, 0.15f));
        Add("sheep_fur", c);
    }

    private static void Chicken()
    {
        var c = new Canvas(11); var r = c.R;
        uint wh = 0xFFFFFF;
        c.Box(0, 0, 4, 6, 3, (f, x, y, w, h) =>
        {
            if (f == 3 && y == 2 && (x == 0 || x == 3)) return Hx(0x111111);
            return V(r, wh, 0.06f);
        });
        c.Box(14, 0, 4, 2, 2, (f, x, y, w, h) => V(r, 0xF0B020, 0.08f));
        c.Box(14, 4, 2, 2, 2, (f, x, y, w, h) => V(r, 0xD02020, 0.08f));
        c.Box(0, 9, 6, 8, 6, (f, x, y, w, h) => V(r, wh, 0.07f));
        c.Box(26, 0, 3, 5, 3, (f, x, y, w, h) => V(r, 0xE0A020, 0.08f));
        c.Box(24, 13, 1, 4, 6, (f, x, y, w, h) => V(r, 0xEEEEEE, 0.07f));
        Add("chicken", c);
    }
}
