using BlockCraft.Core;

namespace BlockCraft.Render;

/// <summary>Skórki mobów modułu MO. Tablica skórek ma 128x128: skórki 64x64 są skalowane 2x (UV są względne), duże (koń, golem, wiedźma) mają własne 128x128.</summary>
public static partial class SkinGen
{
    public const int TexSize = 128;
    private static readonly Dictionary<int, uint[]> BigSkins = new();

    /// <summary>Warstwy tablicy tekstur: duże skórki wprost, pozostałe powiększone 2x.</summary>
    public static List<uint[]> Layers()
    {
        Ensure();
        var list = new List<uint[]>(Skins.Count);
        for (int i = 0; i < Skins.Count; i++)
        {
            if (BigSkins.TryGetValue(i, out var big)) { list.Add(big); continue; }
            var s = Skins[i]; var p = new uint[TexSize * TexSize];
            for (int y = 0; y < TexSize; y++) for (int x = 0; x < TexSize; x++) p[y * TexSize + x] = s[(y >> 1) * W + (x >> 1)];
            list.Add(p);
        }
        return list;
    }

    /// <summary>Płótno o dowolnym rozmiarze (64 albo 128) z tym samym układem ścian co Canvas.</summary>
    private sealed class Cv
    {
        public readonly int N; public readonly uint[] P; public readonly Rng R;
        public Cv(int seed, int n = 64) { N = n; P = new uint[n * n]; R = new Rng(seed); }
        public void Set(int x, int y, uint c) { if (x >= 0 && x < N && y >= 0 && y < N) P[y * N + x] = c; }
        public uint Get(int x, int y) => x >= 0 && x < N && y >= 0 && y < N ? P[y * N + x] : 0;
        public void Box(int u, int v, int w, int h, int d, Func<int, int, int, int, int, uint> f)
        {
            void Face(int face, int fx, int fy, int fw, int fh)
            {
                for (int j = 0; j < fh; j++) for (int i = 0; i < fw; i++) { uint c = f(face, i, j, fw, fh); if (c != 1) Set(fx + i, fy + j, c); }
            }
            Face(0, u + d, v, w, d); Face(1, u + d + w, v, w, d); Face(2, u, v + d, d, h);
            Face(3, u + d, v + d, w, h); Face(4, u + d + w, v + d, d, h); Face(5, u + d + w + d, v + d, w, h);
        }
        /// <summary>Jednolity kolor z szumem na całym boxie.</summary>
        public void Fill(int u, int v, int w, int h, int d, uint c, float a = .12f) => Box(u, v, w, h, d, (f, x, y, fw, fh) => V(R, c, a));
        public Cv Copy(int seed) { var c = new Cv(seed, N); Array.Copy(P, c.P, P.Length); return c; }
    }
    private static void Add(string name, Cv c)
    {
        Index[name] = Skins.Count;
        if (c.N == W) Skins.Add(c.P);
        else { BigSkins[Skins.Count] = c.P; Skins.Add(new uint[W * H]); }
    }
    private static uint Rgb(float r, float g, float b) => TextureGen.Rgba((int)(r * 255), (int)(g * 255), (int)(b * 255), 255);
    private static uint Shade(uint c, float f) => TextureGen.Mul(c, f);

    static partial void SkinsMobs()
    {
        CaveSpiderSkin(); SlimeSkin(); SilverfishSkin(); WitchSkin(); GuardianSkins(); ZombieVillagerSkin();
        WolfSkins(); OcelotSkins(); RabbitSkins(); BatSkin(); SquidSkin(); MooshroomSkin(); PigSaddleSkin(); LeashSkins(); IronGolemSkin(); SnowGolemSkin();
        VillagerSkins(); HorseSkins(); CreeperChargeSkin(); BeamSkin();
    }

    // ---------- potwory ----------
    private static void CaveSpiderSkin()
    {
        var c = new Cv(101); var r = c.R;
        uint a = 0x0E3C42, b = 0x0A2A30;
        c.Box(32, 4, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f == 3) { if ((y == 2 || y == 3) && (x == 1 || x == 6)) return Hx(0xCC0000); if (y == 4 && x >= 2 && x <= 5) return Hx(0xFF2020); }
            return V(r, r.NextBool() ? a : b, .25f);
        });
        c.Fill(0, 0, 6, 6, 6, b, .2f);
        c.Box(0, 12, 10, 8, 12, (f, x, y, w, h) => V(r, f == 0 && (x + y) % 5 == 0 ? 0x2A6E73 : a, .25f));
        c.Fill(18, 0, 16, 2, 2, b, .2f);
        Add("cave_spider", c);
    }

    private static void SlimeSkin()
    {
        var c = new Cv(102); var r = c.R;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) => (x == 0 || y == 0 || x == w - 1 || y == h - 1) ? TextureGen.Hex(0x80C870, 200) : TextureGen.Hex(0x6CBF5C, 120));
        c.Fill(0, 16, 6, 6, 6, 0x5DA04E, .15f);
        c.Fill(32, 0, 2, 2, 2, 0x111A11, .1f); c.Fill(32, 4, 2, 2, 2, 0x111A11, .1f); c.Fill(32, 8, 1, 1, 1, 0x1E3A1E, .1f);
        Add("slime", c);
    }

    private static void SilverfishSkin()
    {
        var c = new Cv(103); var r = c.R;
        int[][] len = { new[] { 3, 2, 2 }, new[] { 4, 3, 2 }, new[] { 6, 4, 3 }, new[] { 3, 3, 3 }, new[] { 2, 2, 3 }, new[] { 2, 1, 2 }, new[] { 1, 1, 2 } };
        int[][] tex = { new[] { 0, 0 }, new[] { 0, 4 }, new[] { 0, 9 }, new[] { 0, 16 }, new[] { 0, 22 }, new[] { 11, 0 }, new[] { 13, 4 } };
        for (int i = 0; i < 7; i++) c.Box(tex[i][0], tex[i][1], len[i][0], len[i][1], len[i][2], (f, x, y, w, h) => V(r, f == 0 ? 0x8A8A8A : 0x6E6E6E, .25f));
        c.Box(0, 0, 3, 2, 2, (f, x, y, w, h) => f == 3 && y == 0 && (x == 0 || x == 2) ? Hx(0x101010) : V(r, 0x7A7A7A, .2f));
        c.Fill(20, 0, 10, 8, 3, 0x575757, .3f); c.Fill(20, 11, 6, 4, 3, 0x5E5E5E, .3f); c.Fill(20, 18, 6, 5, 2, 0x5E5E5E, .3f);
        Add("silverfish", c);
    }

    private static void VillagerBase(Cv c, uint skin, uint robe, uint robe2, Func<int, int, int, uint> extra = null)
    {
        var r = c.R;
        c.Box(0, 0, 8, 10, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 4 && (x == 1 || x == 6)) return Hx(0xFFFFFF);
                if (y == 4 && (x == 2 || x == 5)) return Hx(0x2E8B3E);
                if (y == 3 && x >= 1 && x <= 6) return V(r, Shade(skin, .55f), .1f);
                if (y >= 7 && y <= 8 && x >= 2 && x <= 5) return V(r, Shade(skin, .8f), .1f);
            }
            return V(r, f == 0 ? Shade(skin, .9f) : skin, .08f);
        });
        c.Fill(24, 0, 2, 4, 2, Shade(skin, .92f), .08f);
        c.Box(16, 20, 8, 12, 6, (f, x, y, w, h) => V(r, extra?.Invoke(f, x, y) is uint e && e != 0 ? e : robe, .12f));
        c.Box(0, 38, 8, 18, 6, (f, x, y, w, h) => y > 15 ? V(r, Shade(robe2, .8f), .12f) : V(r, extra?.Invoke(f + 10, x, y) is uint e && e != 0 ? e : robe2, .12f));
        c.Box(44, 22, 4, 8, 4, (f, x, y, w, h) => y >= 5 || f == 1 ? V(r, skin, .08f) : V(r, robe, .12f));
        c.Box(40, 38, 8, 4, 4, (f, x, y, w, h) => V(r, robe, .12f));
        c.Box(0, 22, 4, 12, 4, (f, x, y, w, h) => V(r, y > 9 ? 0x3B2A1E : Shade(robe2, .7f), .12f));
    }

    private static void VillagerSkins()
    {
        uint skin = 0xB98266;
        var farmer = new Cv(110); VillagerBase(farmer, skin, 0x6B4A2A, 0x5A3E22, (f, x, y) => f == 13 && y < 6 && x is 1 or 6 ? 0x2E2414u : 0); Add("villager_farmer", farmer);
        var lib = new Cv(111); VillagerBase(lib, skin, 0xE6E6E6, 0xE0E0E0, (f, x, y) => f is 3 or 13 && x is 3 or 4 && y < 10 ? 0xB0B0B0u : 0); Add("villager_librarian", lib);
        var priest = new Cv(112); VillagerBase(priest, skin, 0x7A3E8C, 0x6B3279, (f, x, y) => f is 3 && y == 0 && x is >= 2 and <= 5 ? 0xE0C040u : 0); Add("villager_priest", priest);
        var smith = new Cv(113); VillagerBase(smith, skin, 0x3B3B3B, 0x2E2E2E, (f, x, y) => f is 3 or 13 && x >= 1 && x <= 6 && y >= 1 ? 0x1E1E1Eu : 0); Add("villager_smith", smith);
        var butcher = new Cv(114); VillagerBase(butcher, skin, 0xE8E8E8, 0xE4E4E4, (f, x, y) => f is 3 or 13 && (x + y * 3) % 11 == 0 ? 0xA02020u : 0); Add("villager_butcher", butcher);
    }

    private static void ZombieVillagerSkin()
    {
        var c = new Cv(104); var r = c.R;
        var z = Skins[Index["zombie"]]; Array.Copy(z, c.P, z.Length);
        for (int y = 32; y < 64; y++) for (int x = 0; x < 64; x++) c.Set(x, y, 0);
        uint skin = 0x4E7E40;
        c.Box(0, 32, 8, 10, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 4 && (x == 1 || x == 2 || x == 5 || x == 6)) return Hx(0x151F12);
                if (y == 3 && x >= 1 && x <= 6) return V(r, 0x2E4A26, .1f);
                if (y >= 7 && y <= 8 && x >= 2 && x <= 5) return V(r, 0x3A5E30, .1f);
            }
            return V(r, skin, .18f);
        });
        c.Fill(24, 32, 2, 4, 2, 0x467238, .15f);
        Add("zombie_villager", c);
    }

    private static void WitchSkin()
    {
        var c = new Cv(105, 128); var r = c.R;
        uint skin = 0xA6866A, robe = 0x3A2A4A, hat = 0x2E2A22;
        c.Box(0, 0, 8, 10, 8, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 4 && (x == 1 || x == 6)) return Hx(0xFFFFFF);
                if (y == 4 && (x == 2 || x == 5)) return Hx(0x6E2C8C);
                if (y == 3 && x >= 1 && x <= 6) return V(r, 0x5A4232, .1f);
            }
            return V(r, f == 0 ? hat : skin, .1f);
        });
        c.Fill(24, 0, 2, 4, 2, 0x957A60, .1f);
        c.Box(16, 20, 8, 12, 6, (f, x, y, w, h) => V(r, robe, .15f));
        c.Box(0, 38, 8, 18, 6, (f, x, y, w, h) => V(r, y > 15 ? 0x241A2E : robe, .15f));
        c.Box(44, 22, 4, 8, 4, (f, x, y, w, h) => y >= 5 || f == 1 ? V(r, skin, .08f) : V(r, robe, .15f));
        c.Fill(40, 38, 8, 4, 4, robe, .15f);
        c.Box(0, 22, 4, 12, 4, (f, x, y, w, h) => V(r, y > 9 ? 0x1E1A16 : 0x2A2034, .12f));
        c.Fill(0, 64, 10, 2, 10, hat, .12f);
        c.Box(0, 76, 7, 4, 7, (f, x, y, w, h) => V(r, y == 3 && f != 0 && f != 1 ? 0x5E8C2E : hat, .12f));
        c.Fill(0, 87, 4, 4, 4, hat, .12f); c.Fill(0, 95, 1, 2, 1, hat, .12f);
        c.Box(0, 0, 1, 1, 1, (f, x, y, w, h) => 1); // znamię dzieli UV z głową jak w 1.8
        Add("witch", c);
    }

    private static void GuardianSkins()
    {
        foreach (var elder in new[] { false, true })
        {
            var c = new Cv(elder ? 107 : 106); var r = c.R;
            uint body = elder ? 0xC8C2B0u : 0x5E8C78u, belly = elder ? 0xA8A090u : 0xD08850u, spot = elder ? 0x8A7E70u : 0x3E6A58u;
            c.Box(0, 0, 12, 12, 16, (f, x, y, w, h) => V(r, f == 1 ? belly : (x * 7 + y * 3 + f) % 9 == 0 ? spot : body, .15f));
            c.Fill(0, 28, 2, 12, 12, Shade(body, .9f), .15f);
            c.Fill(16, 40, 12, 2, 12, Shade(body, .85f), .15f);
            c.Box(0, 0, 2, 9, 2, (f, x, y, w, h) => 1);
            c.Box(8, 0, 2, 2, 1, (f, x, y, w, h) => f == 3 ? (x == 0 ? Hx(0x101010) : Hx(0xE8E8E8)) : V(r, body, .1f));
            c.Fill(40, 0, 4, 4, 8, body, .15f); c.Fill(0, 54, 3, 3, 7, body, .15f); c.Fill(41, 32, 2, 2, 6, body, .15f);
            c.Box(25, 19, 1, 9, 9, (f, x, y, w, h) => V(r, belly, .15f));
            // kolce (box 2x9x2 przy (0,0) dzieli UV z ciałem jak w MC; dorysowujemy jasną końcówkę w rogu)
            for (int y = 0; y < 2; y++) for (int x = 0; x < 8; x++) c.Set(x, 11 + y, V(r, 0xE6D6B0, .1f));
            Add(elder ? "elder_guardian" : "guardian", c);
        }
    }

    private static void BeamSkin()
    {
        var c = new Cv(108); var r = c.R;
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) c.Set(x, y, Hx(((x + y) & 3) == 0 ? 0xFFFFFFu : 0xB0E8F8u));
        Add("guardian_beam", c);
    }

    private static void CreeperChargeSkin()
    {
        var c = new Cv(109);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            if ((x + y * 2) % 8 < 3) c.Set(x, y, TextureGen.Hex(0x7FC8FF, 160));
        Add("creeper_charge", c);
    }

    // ---------- zwierzęta ----------
    private static void WolfBase(Cv c, uint fur, uint fur2, uint eye)
    {
        var r = c.R;
        c.Box(0, 0, 6, 6, 4, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 2 && (x == 1 || x == 4)) return Hx(eye);
                if (y == 2 && (x == 0 || x == 5)) return Hx(0xF0F0F0);
                if (y >= 4) return V(r, fur2, .08f);
            }
            return V(r, fur, .1f);
        });
        c.Fill(16, 14, 2, 2, 1, Shade(fur, .8f), .1f);
        c.Box(0, 10, 3, 3, 4, (f, x, y, w, h) => f == 3 && y == 0 && x == 1 ? Hx(0x1A1A1A) : V(r, fur2, .1f));
        c.Box(18, 14, 6, 9, 6, (f, x, y, w, h) => V(r, f == 1 ? fur2 : fur, .12f));
        c.Box(21, 0, 8, 6, 7, (f, x, y, w, h) => V(r, Shade(fur, .95f), .15f));
        c.Box(0, 18, 2, 8, 2, (f, x, y, w, h) => V(r, y > 6 ? fur2 : fur, .1f));
        c.Box(9, 18, 2, 8, 2, (f, x, y, w, h) => V(r, y > 5 ? fur2 : fur, .1f));
    }
    private static void WolfSkins()
    {
        var wild = new Cv(120); WolfBase(wild, 0xD8D4D0, 0xC0B4A4, 0x2A2A2A); Add("wolf", wild);
        var tame = new Cv(121); WolfBase(tame, 0xE2DEDA, 0xC8BCAC, 0x2A2A2A); Add("wolf_tame", tame);
        var angry = new Cv(122); WolfBase(angry, 0xD8D4D0, 0xC0B4A4, 0xD01010); Add("wolf_angry", angry);
        var collar = new Cv(123);
        collar.Box(21, 0, 8, 6, 7, (f, x, y, w, h) => (f is 2 or 4 && x == 0) || (f is 3 && y == 0) || (f == 0 && y == 0) ? Hx(0xFFFFFF) : 0);
        Add("wolf_collar", collar);
    }

    private static void OcelotBase(Cv c, uint fur, uint spots, uint belly, bool siamese = false)
    {
        var r = c.R;
        uint Furry(int f, int x, int y) => (x * 5 + y * 3 + f) % 7 == 0 ? spots : fur;
        c.Box(0, 0, 5, 4, 5, (f, x, y, w, h) =>
        {
            if (f == 3) { if (y == 1 && (x == 1 || x == 3)) return Hx(0x2EAA2E); if (y == 3) return V(r, belly, .08f); }
            return V(r, siamese && f == 3 ? 0x4A3428 : Furry(f, x, y), .1f);
        });
        c.Fill(0, 24, 3, 2, 2, siamese ? 0x3A2A20u : 0xF0C0A0u, .08f);
        c.Fill(0, 10, 1, 1, 2, siamese ? 0x3A2A20 : fur, .1f); c.Fill(6, 10, 1, 1, 2, siamese ? 0x3A2A20 : fur, .1f);
        c.Box(20, 0, 4, 16, 6, (f, x, y, w, h) => V(r, f == 1 ? belly : Furry(f, x, y), .12f));
        c.Box(0, 15, 1, 8, 1, (f, x, y, w, h) => V(r, siamese ? 0x3A2A20 : y % 3 == 0 ? spots : fur, .1f));
        c.Box(4, 15, 1, 8, 1, (f, x, y, w, h) => V(r, siamese ? 0x3A2A20 : y % 3 == 0 ? spots : fur, .1f));
        c.Box(8, 13, 2, 6, 2, (f, x, y, w, h) => V(r, siamese && y > 3 ? 0x3A2A20 : Furry(f, x, y), .1f));
        c.Box(40, 0, 2, 10, 2, (f, x, y, w, h) => V(r, siamese && y > 6 ? 0x3A2A20 : Furry(f, x, y), .1f));
    }
    private static void OcelotSkins()
    {
        var o = new Cv(130); OcelotBase(o, 0xE8C860, 0x6A4A20, 0xF2E2B0); Add("ocelot", o);
        var b = new Cv(131); OcelotBase(b, 0x1E1E28, 0x14141C, 0xE8E8E8); Add("cat_black", b);
        var g = new Cv(132); OcelotBase(g, 0xE88A30, 0xC06A20, 0xF6D0A0); Add("cat_red", g);
        var s = new Cv(133); OcelotBase(s, 0xF0E6D0, 0xE0D4BC, 0xF4ECDC, true); Add("cat_siamese", s);
    }

    private static void RabbitSkins()
    {
        (string n, uint fur, uint fur2, uint eye)[] types =
        {
            ("rabbit_brown", 0x8A6440, 0x6E4E30, 0x111111), ("rabbit_white", 0xF2F2F2, 0xDDDDDD, 0xC03050), ("rabbit_black", 0x2A2A2A, 0x1C1C1C, 0x8A1010),
            ("rabbit_black_white", 0xF0F0F0, 0x262626, 0x111111), ("rabbit_gold", 0xE8C870, 0xD0A850, 0x111111), ("rabbit_salt", 0xA8968A, 0x7E6E64, 0x111111),
            ("rabbit_caerbannog", 0xF4F4F4, 0xE0E0E0, 0xD01010)
        };
        int seed = 140;
        foreach (var t in types)
        {
            var c = new Cv(seed++); var r = c.R;
            uint Coat(int f, int x, int y) => t.n == "rabbit_black_white" ? ((x + y + f) % 4 < 2 ? t.fur : t.fur2) : t.n == "rabbit_salt" && (x * 3 + y) % 5 == 0 ? 0xD8D0C8 : t.fur;
            c.Box(32, 0, 5, 4, 5, (f, x, y, w, h) =>
            {
                if (f == 3 && y == 1 && (x == 0 || x == 4)) return Hx(t.eye);
                if (t.n == "rabbit_caerbannog" && f == 3 && y == 3 && x is 1 or 3) return Hx(0xC01010);
                return V(r, Coat(f, x, y), .1f);
            });
            c.Fill(52, 0, 2, 5, 1, t.fur, .1f); c.Fill(58, 0, 2, 5, 1, t.fur, .1f);
            for (int y = 2; y < 5; y++) { c.Set(53, y + 1, Hx(0xE8A0A0)); c.Set(59, y + 1, Hx(0xE8A0A0)); }
            c.Fill(32, 9, 1, 1, 1, 0xE88A9A, .05f);
            c.Box(0, 0, 6, 5, 10, (f, x, y, w, h) => V(r, f == 1 ? t.fur2 : Coat(f, x, y), .12f));
            c.Fill(52, 6, 3, 3, 2, 0xF0F0F0, .06f);
            c.Box(26, 24, 2, 1, 7, (f, x, y, w, h) => V(r, t.fur2, .1f)); c.Box(8, 24, 2, 1, 7, (f, x, y, w, h) => V(r, t.fur2, .1f));
            c.Box(30, 15, 2, 4, 5, (f, x, y, w, h) => V(r, Coat(f, x, y), .12f)); c.Box(16, 15, 2, 4, 5, (f, x, y, w, h) => V(r, Coat(f, x, y), .12f));
            c.Box(8, 15, 2, 7, 2, (f, x, y, w, h) => V(r, Coat(f, x, y), .1f)); c.Box(0, 15, 2, 7, 2, (f, x, y, w, h) => V(r, Coat(f, x, y), .1f));
            Add(t.n, c);
        }
    }

    private static void BatSkin()
    {
        var c = new Cv(150); var r = c.R;
        uint fur = 0x4C3E30, wing = 0x2A2018;
        c.Box(0, 0, 6, 6, 6, (f, x, y, w, h) => f == 3 && y == 2 && (x == 1 || x == 4) ? Hx(0x111111) : V(r, fur, .15f));
        c.Fill(24, 0, 3, 4, 1, Shade(fur, .8f), .15f);
        c.Fill(0, 16, 6, 12, 6, fur, .18f); c.Fill(0, 34, 10, 6, 1, Shade(fur, .8f), .15f);
        c.Fill(42, 0, 10, 16, 1, wing, .2f); c.Fill(24, 16, 8, 12, 1, wing, .2f);
        Add("bat", c);
    }

    private static void SquidSkin()
    {
        var c = new Cv(151); var r = c.R;
        uint body = 0x223B4D, light = 0x5A7A8E;
        c.Box(0, 0, 12, 16, 12, (f, x, y, w, h) =>
        {
            if (f is 3 or 5 && y == 10 && (x == 2 || x == 9)) return Hx(0x0A0A0A);
            if (f is 3 or 5 && y == 10 && (x == 3 || x == 8)) return Hx(0xE8E8E8);
            return V(r, (x * 3 + y * 5 + f) % 11 == 0 ? light : body, .15f);
        });
        c.Box(48, 0, 2, 18, 2, (f, x, y, w, h) => V(r, y % 4 == 3 ? light : Shade(body, .9f), .15f));
        Add("squid", c);
    }

    private static void MooshroomSkin()
    {
        var c = new Cv(152); var r = c.R;
        uint red = 0xA01010, wh = 0xCFCFCF;
        var spots = TextureGen.TileNoise(78, 4);
        c.Box(0, 0, 8, 8, 6, (f, x, y, w, h) =>
        {
            if (f == 3)
            {
                if (y == 3 && (x == 1 || x == 6)) return Hx(0x111111);
                if (y >= 5 && x >= 2 && x <= 5) return y == 6 && (x == 2 || x == 5) ? Hx(0x3A2A2A) : V(r, 0xB49A8A);
            }
            return V(r, red, .15f);
        });
        c.Fill(22, 0, 1, 3, 1, 0xA0A0A0, .1f);
        c.Box(18, 4, 12, 18, 10, (f, x, y, w, h) => V(r, spots[((y * 3 + f * 5) & 15) * 16 + ((x * 3 + f * 7) & 15)] > .6f ? wh : red, .12f));
        c.Fill(52, 0, 4, 6, 1, 0xE0A0A0, .08f);
        c.Box(0, 16, 4, 12, 4, (f, x, y, w, h) => V(r, y > 9 ? 0x2A1E14 : y > 6 ? wh : red, .12f));
        Add("mooshroom", c);
    }

    /// <summary>pig_saddle.png: skórzane siodło na grzbiecie (przednia ściana obróconego tułowia) i popręg po bokach, reszta przezroczysta.</summary>
    private static void PigSaddleSkin()
    {
        var c = new Cv(154); var r = c.R;
        c.Box(28, 8, 10, 16, 8, (f, x, y, w, h) =>
        {
            bool seat = y >= 4 && y <= 11;
            if (f == 3 && seat) return x == 0 || x == w - 1 || y == 4 || y == 11 ? V(r, 0x4A2C14, .1f) : V(r, 0x7A4A24, .12f);
            if (f is 2 or 4 && y >= 6 && y <= 8) return V(r, 0x3A2412, .1f);
            if (f == 1 && y >= 6 && y <= 8) return V(r, 0x3A2412, .1f);
            return 1;
        });
        Add("pig_saddle", c);
    }

    /// <summary>lead_knot.png i lina smyczy: skręcony sznur w odcieniach brązu.</summary>
    private static void LeashSkins()
    {
        var k = new Cv(155); var r = k.R;
        k.Box(0, 0, 6, 8, 6, (f, x, y, w, h) => V(r, (x + y) % 3 == 0 ? 0x5C4024 : (x + y) % 3 == 1 ? 0x8A6A3E : 0x7A5A32, .1f));
        Add("leash_knot", k);
        var l = new Cv(156); r = l.R;
        l.Box(0, 0, 1, 1, 16, (f, x, y, w, h) => V(r, (x + y) % 2 == 0 ? 0x806040 : 0x5E4428, .08f));
        Add("leash", l);
    }

    private static void IronGolemSkin()
    {
        var c = new Cv(153, 128); var r = c.R;
        uint iron = 0xC8C2B8, dark = 0x9A948A, vine = 0x3E6A20;
        uint Plate(int f, int x, int y) => (x * 7 + y * 13 + f * 5) % 23 == 0 ? vine : (x + y) % 9 == 0 ? dark : iron;
        c.Box(0, 0, 8, 10, 8, (f, x, y, w, h) =>
        {
            if (f == 3) { if (y == 4 && (x == 2 || x == 5)) return Hx(0xA01010); if (y == 3 && x >= 1 && x <= 6) return V(r, dark, .1f); }
            return V(r, Plate(f, x, y), .1f);
        });
        c.Fill(24, 0, 2, 4, 2, dark, .1f);
        c.Box(0, 40, 18, 12, 11, (f, x, y, w, h) => V(r, Plate(f, x, y), .12f));
        c.Fill(0, 70, 9, 5, 6, dark, .12f);
        c.Box(60, 21, 4, 30, 6, (f, x, y, w, h) => V(r, Plate(f, x, y), .12f));
        c.Box(60, 58, 4, 30, 6, (f, x, y, w, h) => V(r, Plate(f, x, y), .12f));
        c.Box(37, 0, 6, 16, 5, (f, x, y, w, h) => V(r, Plate(f, x, y), .12f));
        c.Box(60, 0, 6, 16, 5, (f, x, y, w, h) => V(r, Plate(f, x, y), .12f));
        Add("iron_golem", c);
    }

    private static void SnowGolemSkin()
    {
        var c = new Cv(154); var r = c.R;
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) =>
        {
            if (f != 3) return V(r, 0xE07A10, .1f);
            if (y >= 2 && y <= 3 && (x is 1 or 2 or 5 or 6)) return Hx(0x3A1E04);
            if (y >= 5 && y <= 6 && x >= 1 && x <= 6 && !(y == 5 && x is 3 or 4)) return Hx(0x3A1E04);
            return V(r, 0xE88820, .1f);
        });
        c.Fill(0, 16, 10, 10, 10, 0xF4FAFA, .06f);
        c.Fill(0, 36, 12, 12, 12, 0xF0F8F8, .06f);
        c.Fill(32, 0, 12, 2, 2, 0x6A4A2A, .15f);
        Add("snow_golem", c);
    }

    // ---------- konie (128x128 jak w 1.8) ----------
    private static readonly (string n, uint c, uint mane)[] HorseColors =
    {
        ("horse_white", 0xEDEDED, 0xD8D8D8), ("horse_creamy", 0xC6A577, 0x9E7C50), ("horse_chestnut", 0x9E5A2E, 0x6E3A1A), ("horse_brown", 0x6A4426, 0x3A2412),
        ("horse_black", 0x262626, 0x141414), ("horse_gray", 0x6A6A6A, 0x3E3E3E), ("horse_darkbrown", 0x3E2A1E, 0x221610)
    };
    /// <summary>Wszystkie boxy konia z ModelHorse 1.8 (u, v, w, h, d) i ich rola: 0 sierść, 1 grzywa/ogon, 2 kopyto, 3 siodło, 4 metal, 5 lejce, 6 skrzynia, 7 pysk.</summary>
    public static readonly (int u, int v, int w, int h, int d, int kind)[] HorseBoxes =
    {
        (0,0,5,5,7,0), (24,18,4,3,6,7), (24,27,4,2,5,7), (0,0,2,3,1,0), (0,12,2,7,1,0), (0,12,4,14,8,0), (58,0,2,16,4,1),
        (0,34,10,10,24,0), (44,0,2,2,3,1), (38,7,3,4,7,1), (24,3,3,4,7,1),
        (78,29,4,9,5,0), (78,43,3,5,3,0), (78,51,4,3,4,2), (96,29,4,9,5,0), (96,43,3,5,3,0), (96,51,4,3,4,2),
        (44,29,3,8,4,0), (44,41,3,5,3,0), (44,51,4,3,4,2), (60,29,3,8,4,0), (60,41,3,5,3,0), (60,51,4,3,4,2),
        (0,34,8,8,3,6), (0,47,8,8,3,6), (80,0,10,1,8,3), (106,9,3,1,2,3), (80,9,8,1,2,3), (74,0,1,2,2,4), (74,4,1,2,2,4), (70,0,1,6,1,5), (74,13,1,2,2,4), (44,10,0,3,16,5), (44,5,0,3,16,5)
    };
    private static void HorseCoat(Cv c, uint coat, uint mane, uint hoof, bool donkey, bool undead)
    {
        var r = c.R;
        foreach (var b in HorseBoxes)
        {
            uint col = b.kind switch { 1 => mane, 2 => hoof, 3 => 0x5A3418, 4 => 0xB8B8B8, 5 => 0x3A2614, 6 => 0x8A5E2C, 7 => donkey ? 0xB8AC9C : Shade(coat, .85f), _ => coat };
            if (b.kind == 0 && b.v == 12 && b.h == 7 && !donkey) continue;
            c.Box(b.u, b.v, b.w, b.h, b.d, (f, x, y, w, h) =>
            {
                if (b.kind == 6 && f == 3 && y is 3 or 4 && x is 3 or 4) return Hx(0xC8C8C8);
                if (b.kind == 0 && b.u == 0 && b.v == 0 && b.d == 7 && (f == 2 && x == w - 2 || f == 4 && x == 1) && y == 1) return Hx(undead ? 0x6A1010u : 0x101010u);
                if (undead && b.kind is 0 or 1 && (x * 5 + y * 7 + f) % 6 == 0) return V(r, Shade(coat, .6f), .2f);
                return V(r, col, b.kind == 0 ? .1f : .16f);
            });
        }
    }
    private static void HorseSkins()
    {
        int seed = 160;
        foreach (var hc in HorseColors) { var c = new Cv(seed++, 128); HorseCoat(c, hc.c, hc.mane, 0x4A4A4A, false, false); Add(hc.n, c); }
        var donkey = new Cv(seed++, 128); HorseCoat(donkey, 0x857565, 0x4A3E34, 0x3A3A3A, true, false); Add("horse_donkey", donkey);
        var mule = new Cv(seed++, 128); HorseCoat(mule, 0x5E3E26, 0x2E1E12, 0x2A2A2A, true, false); Add("horse_mule", mule);
        var zombie = new Cv(seed++, 128); HorseCoat(zombie, 0x5E8E52, 0x3E6236, 0x3A4A34, false, true); Add("horse_zombie", zombie);
        var skel = new Cv(seed++, 128); HorseCoat(skel, 0xD8D8CC, 0xB8B8AC, 0x9A9A90, false, true); Add("horse_skeleton", skel);
        // znaczenia (nakładki z przezroczystością): białe skarpety i strzałka, białe pole, białe kropki, czarne kropki
        for (int m = 1; m <= 4; m++)
        {
            var c = new Cv(seed++, 128);
            uint col = m == 4 ? 0x1A1A1Au : 0xF4F4F4u;
            foreach (var b in HorseBoxes)
            {
                if (b.kind != 0) continue;
                bool leg = b.u >= 44 && b.v >= 29;
                c.Box(b.u, b.v, b.w, b.h, b.d, (f, x, y, w, h) =>
                {
                    bool on = m switch
                    {
                        1 => leg && (b.v >= 41 || y > h / 2) || b.u == 0 && b.v == 0 && f == 3 && x >= 1 && x <= 3,
                        2 => !leg && ((x * 3 + y * 2 + f * 7) % 17 < 8) && b.v == 34 || leg && b.v >= 41,
                        _ => (x * 7 + y * 11 + f * 3 + b.u) % 13 == 0
                    };
                    return on ? Hx(col) : 0;
                });
            }
            Add("horse_markings_" + m, c);
        }
        foreach (var (name, metal) in new[] { ("horse_armor_iron", 0xC8C8C8u), ("horse_armor_gold", 0xE8C830u), ("horse_armor_diamond", 0x5ED8D0u) })
        {
            var c = new Cv(seed++, 128); var r = c.R;
            foreach (var b in HorseBoxes)
            {
                bool body = b.u == 0 && b.v == 34 && b.d == 24, neck = b.u == 0 && b.v == 12 && b.d == 8, head = b.u == 0 && b.v == 0 && b.d == 7, leg = b.kind == 0 && b.u >= 44 && b.v == 29;
                if (!(body || neck || head || leg)) continue;
                c.Box(b.u, b.v, b.w, b.h, b.d, (f, x, y, w, h) =>
                {
                    if (leg && y > 3) return 0;
                    if (body && f is 2 or 4 && x > 10) return 0;
                    if (body && f == 1) return 0;
                    return V(r, (x + y) % 5 == 0 ? Shade(metal, .75f) : metal, .1f);
                });
            }
            Add(name, c);
        }
    }
}
