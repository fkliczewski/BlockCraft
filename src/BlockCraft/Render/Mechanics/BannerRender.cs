using BlockCraft.Core;
using BlockCraft.World;
using BlockCraft.Items;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Tekstury sztandarów 64x64 składane z warstw jak LayeredColorMaskTexture 1.8 (tło banner_base, maski wzorów, kolory MapColor barwników).</summary>
public sealed class BannerTextureCache : IDisposable
{
    public const int Slots = 64;
    public readonly TextureArray Array;
    private readonly Dictionary<string, int> _byDesign = new();
    private readonly LinkedList<string> _lru = new();
    private static uint[] _base;
    private static readonly Dictionary<string, uint[]> Masks = new();

    public BannerTextureCache()
    {
        var layers = new List<uint[]>();
        for (int i = 0; i < Slots; i++) layers.Add(new uint[64 * 64]);
        Array = new TextureArray(64, layers, false);
    }

    public int LayerFor(int baseColor, IReadOnlyList<(string code, int color)> patterns)
    {
        string key = Banners.Design(baseColor, patterns);
        if (_byDesign.TryGetValue(key, out int slot)) { _lru.Remove(key); _lru.AddFirst(key); return slot; }
        if (_byDesign.Count < Slots) slot = _byDesign.Count;
        else { string old = _lru.Last.Value; _lru.RemoveLast(); slot = _byDesign[old]; _byDesign.Remove(old); }
        Array.UpdateLayer(slot, Composite(baseColor, patterns));
        _byDesign[key] = slot; _lru.AddFirst(key);
        return slot;
    }

    public static uint[] Composite(int baseColor, IReadOnlyList<(string code, int color)> patterns)
    {
        _base ??= ClientAssets.TryReadTexture("entity/banner_base.png", out int w, out int h) is { } px && w == 64 && h == 64 ? px : ProceduralBase();
        var img = (uint[])_base.Clone();
        var layers = new List<(string code, int color)> { ("b", baseColor) };
        layers.AddRange(patterns);
        foreach (var (code, color) in layers.Take(17))
        {
            var mask = MaskOf(code);
            if (mask == null) continue;
            uint rgb = Banners.LayerRgb(color);
            int cr = (int)(rgb >> 16 & 255), cg = (int)(rgb >> 8 & 255), cb = (int)(rgb & 255);
            for (int i = 0; i < img.Length; i++)
            {
                uint m = mask[i];
                if (m >> 24 == 0) continue;
                float a = (m & 255) / 255f;
                uint b = _base[i];
                float sr = (b & 255) * cr / 255f, sg = (b >> 8 & 255) * cg / 255f, sb = (b >> 16 & 255) * cb / 255f;
                uint d = img[i];
                float da = (d >> 24) / 255f, oa = a + da * (1 - a);
                if (oa <= 0) continue;
                float r = (sr * a + (d & 255) * da * (1 - a)) / oa, g = (sg * a + (d >> 8 & 255) * da * (1 - a)) / oa, bl = (sb * a + (d >> 16 & 255) * da * (1 - a)) / oa;
                img[i] = Rgb.Pack((int)r, (int)g, (int)bl, (int)(oa * 255));
            }
        }
        return img;
    }

    private static uint[] MaskOf(string code)
    {
        if (Masks.TryGetValue(code, out var m)) return m;
        var pat = BannerPattern.ByCode(code);
        if (pat == null) return Masks[code] = null;
        m = ClientAssets.TryReadTexture("entity/banner/" + pat.File + ".png", out int w, out int h) is { } px && w == 64 && h == 64 ? px : ProceduralMask(code);
        return Masks[code] = m;
    }

    private static uint[] ProceduralBase()
    {
        var p = new uint[64 * 64]; var r = new Rng(64);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                bool cloth = y < 41 && x < 43, wood = x >= 44 && x < 52 && y < 44 || y >= 42 && y < 46 && x < 44;
                if (cloth) p[y * 64 + x] = TextureGen.Gray(.86f + r.NextFloat() * .1f);
                else if (wood) p[y * 64 + x] = TextureGen.Mul(TextureGen.Hex(0x9C7A48), .85f + r.NextFloat() * .2f);
            }
        return p;
    }

    /// <summary>Zastępcza maska wzoru na przedniej (1,1) i tylnej (22,1) ścianie płachty 20x40, gdy brak oryginałów z client.jar.</summary>
    private static uint[] ProceduralMask(string code)
    {
        var p = new uint[64 * 64];
        for (int y = 0; y < 40; y++)
            for (int x = 0; x < 20; x++)
            {
                float u = (x + .5f) / 20f, v = (y + .5f) / 40f;
                float a = code switch
                {
                    "b" => 1,
                    "bl" => u < .5f && v > .667f ? 1 : 0, "br" => u >= .5f && v > .667f ? 1 : 0, "tl" => u < .5f && v < .333f ? 1 : 0, "tr" => u >= .5f && v < .333f ? 1 : 0,
                    "bs" => v >= .667f ? 1 : 0, "ts" => v < .333f ? 1 : 0, "ls" => u < .333f ? 1 : 0, "rs" => u >= .667f ? 1 : 0,
                    "cs" => u >= .333f && u < .667f ? 1 : 0, "ms" => v >= .4f && v < .6f ? 1 : 0,
                    "drs" => MathF.Abs(u - v) < .15f ? 1 : 0, "dls" => MathF.Abs(u - (1 - v)) < .15f ? 1 : 0,
                    "ss" => x % 5 < 2 && v < .8f ? 1 : 0, "cr" => MathF.Abs(u - v) < .15f || MathF.Abs(u - (1 - v)) < .15f ? 1 : 0,
                    "sc" => u >= .4f && u < .6f || v >= .45f && v < .55f ? 1 : 0,
                    "bt" => v >= 1 - (.5f - MathF.Abs(u - .5f)) ? 1 : 0, "tt" => v <= .5f - MathF.Abs(u - .5f) ? 1 : 0,
                    "bts" => v >= .85f + .15f * MathF.Abs((u * 4 % 1) * 2 - 1) ? 1 : 0, "tts" => v <= .15f - .15f * MathF.Abs((u * 4 % 1) * 2 - 1) ? 1 : 0,
                    "ld" => u + v < 1 ? 1 : 0, "rd" => u + v >= 1 ? 1 : 0, "lud" => u < v ? 1 : 0, "rud" => u >= v ? 1 : 0,
                    "mc" => (x - 9.5f) * (x - 9.5f) + (y - 19.5f) * (y - 19.5f) < 25 ? 1 : 0, "mr" => MathF.Abs(x - 9.5f) / 7f + MathF.Abs(y - 19.5f) / 14f < 1 ? 1 : 0,
                    "vh" => u < .5f ? 1 : 0, "hh" => v < .5f ? 1 : 0, "vhr" => u >= .5f ? 1 : 0, "hhb" => v >= .5f ? 1 : 0,
                    "bo" => x < 2 || x > 17 || y < 2 || y > 37 ? 1 : 0, "cbo" => (x < 3 || x > 16 || y < 3 || y > 36) && (x + y) % 3 != 0 ? 1 : 0,
                    "gra" => 1 - v, "gru" => v,
                    "bri" => y % 4 == 0 || (x + (y / 4 % 2) * 2) % 4 == 0 ? 1 : 0,
                    "cre" => (y >= 12 && y < 16 && (x >= 4 && x < 8 || x >= 12 && x < 16)) || (y >= 16 && y < 24 && x >= 8 && x < 12) || (y >= 18 && y < 26 && (x >= 6 && x < 8 || x >= 12 && x < 14)) ? 1 : 0,
                    "sku" => ((x - 9.5f) * (x - 9.5f) / 36 + (y - 16f) * (y - 16f) / 49 < 1 && !((y >= 15 && y < 18) && (x >= 6 && x < 9 || x >= 11 && x < 14))) || (MathF.Abs(MathF.Abs(x - 9.5f) - (y - 24) * .8f) < 1.5f && y > 24 && y < 34) ? 1 : 0,
                    "flo" => (x - 9.5f) * (x - 9.5f) + (y - 19.5f) * (y - 19.5f) is var d2 && (d2 < 9 || d2 < 36 && (MathF.Atan2(y - 19.5f, x - 9.5f) * 8 / MathF.PI % 2 + 2) % 2 < 1) ? 1 : 0,
                    "moj" => MathF.Abs(x - 9.5f) < 6 && MathF.Abs(y - 19.5f) < 6 && !(MathF.Abs(x - 9.5f) < 3 && y > 17) ? 1 : 0,
                    _ => 0
                };
                if (a <= 0) continue;
                uint c = Rgb.Pack((int)(a * 255), (int)(a * 255), (int)(a * 255), 255);
                p[(y + 1) * 64 + x + 1] = c;
                p[(y + 1) * 64 + 22 + (19 - x)] = c;
            }
        return p;
    }

    public void Dispose() => Array.Dispose();
}

public sealed partial class EntityRenderer
{
    public void RenderBannerItem(ItemStack stack, Matrix4 transform)
    {
        FlushBanners();
        BannerBatch.Layer = BannerTextures.LayerFor(Banners.BaseOf(stack), Banners.PatternsOf(stack));
        BannerBatch.Sky = Terrain.Sky; BannerBatch.Blk = Terrain.Blk;
        BannerBatch.Color = Vector4.One; BannerBatch.Overlay = Vector4.Zero;
        BannerSlate.RX = 0;
        var root = Matrix4.CreateScale(.4f, -.4f, -.4f) * Matrix4.CreateTranslation(0, -.25f, 0) * transform;
        RenderPart(BannerBatch, BannerSlate, root);
        RenderPart(BannerBatch, BannerStand, root);
        RenderPart(BannerBatch, BannerTop, root);
    }

    internal static void DrawBanners(EntityRenderer r, World.World w, float pt, Vector3d cam)
    {
        int cx = MathUtil.FloorToInt(cam.X) >> 4, cz = MathUtil.FloorToInt(cam.Z) >> 4;
        for (int x = cx - 4; x <= cx + 4; x++)
            for (int z = cz - 4; z <= cz + 4; z++)
            {
                var chunk = w.GetChunk(x, z);
                if (chunk == null) continue;
                foreach (var te in chunk.TileEntities.Values)
                {
                    if (te is not BannerTile banner) continue;
                    var pos = new Vector3((float)(te.X - cam.X), (float)(te.Y - cam.Y), (float)(te.Z - cam.Z));
                    if (pos.LengthSquared > 64 * 64) continue;
                    int id = w.GetBlock(te.X, te.Y, te.Z);
                    if (!Banners.IsBanner(id)) continue;
                    // Flush before the LRU can reuse a layer referenced by queued vertices.
                    r.FlushBanners();
                    r.BannerBatch.Sky = w.GetSkyLight(te.X, te.Y, te.Z);
                    r.BannerBatch.Blk = w.GetBlockLight(te.X, te.Y, te.Z);
                    r.RenderBanner(banner, id, w.GetMeta(te.X, te.Y, te.Z), pos, w.TotalTime, pt);
                }
            }
    }

    public readonly ModelBatch BannerBatch = new();
    private BannerTextureCache _bannerTex;
    internal BannerTextureCache BannerTextures => _bannerTex ??= new BannerTextureCache();

    public void FlushBanners()
    {
        if (BannerBatch.Verts == 0 || _bannerTex == null) return;
        Flush(BannerBatch, _bannerTex.Array);
    }

    // ModelBanner 1.8 (64x64)
    private static readonly ModelPart BannerSlate = new ModelPart(0, -32, 0).Box(0, 0, -10, 0, -2, 20, 40, 1);
    private static readonly ModelPart BannerStand = new ModelPart().Box(44, 0, -1, -30, -1, 2, 42, 2);
    private static readonly ModelPart BannerTop = new ModelPart().Box(0, 42, -10, -32, -1, 20, 2, 2);

    /// <summary>TileEntityBannerRenderer 1.8: stojący (obrót co 22.5 stopnia, z drzewcem) albo ścienny, płachta faluje z czasem świata.</summary>
    public void RenderBanner(BannerTile te, int id, int meta, Vector3 rel, long worldTime, float pt)
    {
        const float f = .6666667f;
        bool standing = id == B.StandingBanner;
        Matrix4 root;
        if (standing)
            root = Matrix4.CreateScale(f, -f, -f) * Matrix4.CreateRotationY(-(meta & 15) * 360f / 16f * MathUtil.Deg2Rad) * Matrix4.CreateTranslation(rel + new Vector3(.5f, .75f * f, .5f));
        else
        {
            float a = meta switch { 2 => 180f, 4 => 90f, 5 => -90f, _ => 0f };
            root = Matrix4.CreateScale(f, -f, -f) * Matrix4.CreateTranslation(0, -.3125f, -.4375f) * Matrix4.CreateRotationY(-a * MathUtil.Deg2Rad) * Matrix4.CreateTranslation(rel + new Vector3(.5f, -.25f * f, .5f));
        }
        float t = te.X * 7 + te.Y * 9 + te.Z * 13 + worldTime + pt;
        BannerSlate.RX = (-.0125f + .01f * MathF.Cos(t * MathF.PI * .02f)) * MathF.PI;
        var b = BannerBatch;
        b.Layer = BannerTextures.LayerFor(te.Base, te.Patterns);
        RenderPart(b, BannerSlate, root);
        if (standing) RenderPart(b, BannerStand, root);
        RenderPart(b, BannerTop, root);
    }
}

public static partial class TextureGen
{
    private static void TexBanners()
    {
        var px = New();
        for (int y = 1; y < 15; y++) { px[y * 16 + 3] = px[y * 16 + 4] = Hex(0x8A6A40); }
        for (int x = 3; x < 14; x++) px[1 * 16 + x] = px[2 * 16 + x] = Hex(0x8A6A40);
        for (int y = 3; y < 13; y++) for (int x = 5; x < 13; x++) px[y * 16 + x] = Gray(y > 10 && (x % 2 == 0) ? .8f : .95f);
        Add("item_banner", px);
    }
}
