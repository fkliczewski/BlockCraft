using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Tekstury map 128x128 w jednej tablicy: warstwa 0 tło (map_background), 1 ikony (map_icons), dalej mapy (LRU).</summary>
public sealed class MapTextureCache : IDisposable
{
    public const int Background = 0, Icons = 1, FirstSlot = 2, Slots = 48;
    public readonly TextureArray Array;
    private readonly Dictionary<int, (int slot, int version)> _byMap = new();
    private readonly LinkedList<int> _lru = new();

    public MapTextureCache()
    {
        var layers = new List<uint[]> { LoadOr("map/map_background.png", ProceduralBackground), LoadOr("map/map_icons.png", ProceduralIcons) };
        for (int i = 0; i < Slots; i++) layers.Add(new uint[128 * 128]);
        Array = new TextureArray(128, layers, false);
    }

    private static uint[] LoadOr(string path, Func<uint[]> fallback)
    {
        var px = ClientAssets.TryReadTexture(path, out int w, out int h);
        if (px == null || w == 0 || h == 0) return fallback();
        var dst = new uint[128 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) dst[y * 128 + x] = px[(y * h / 128) * w + x * w / 128];
        return dst;
    }

    private static uint[] ProceduralBackground()
    {
        var p = new uint[128 * 128]; var r = new Rng(128);
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                int edge = Math.Min(Math.Min(x, y), Math.Min(127 - x, 127 - y));
                uint c = edge < 3 ? 0x6B5A3Au : 0xD6C29Au;
                p[y * 128 + x] = TextureGen.WithA(TextureGen.Mul(TextureGen.Hex(c), .92f + r.NextFloat() * .1f), 255);
            }
        return p;
    }

    /// <summary>Siatka 4x4 ikon 32x32: 0 gracz (biała strzałka), 1 ramka (zielona), 2 czerwona, 3 niebieska, 6 kropka poza mapą.</summary>
    private static uint[] ProceduralIcons()
    {
        var p = new uint[128 * 128];
        uint[] colors = { 0xFFFFFF, 0x2EC22E, 0xE03030, 0x3050E0, 0xF0F0F0, 0xC0C0C0, 0xFFFFFF, 0x202020 };
        for (int t = 0; t < 8; t++)
        {
            int ox = t % 4 * 32, oy = t / 4 * 32;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    bool on = t == 6 ? (x - 15.5) * (x - 15.5) + (y - 15.5) * (y - 15.5) < 30 : y >= 4 && y <= 28 && Math.Abs(x - 15.5) <= (y - 4) * .45 && !(y > 20 && Math.Abs(x - 15.5) < (y - 20) * .9);
                    if (on) p[(oy + y) * 128 + ox + x] = TextureGen.Hex(colors[t]);
                }
        }
        return p;
    }

    /// <summary>Warstwa z aktualnymi pikselami mapy (wgrywa ponownie po zmianie wersji).</summary>
    public int LayerFor(MapData m)
    {
        if (_byMap.TryGetValue(m.Id, out var e))
        {
            _lru.Remove(m.Id); _lru.AddFirst(m.Id);
            if (e.version != m.Version) { Upload(e.slot, m); _byMap[m.Id] = (e.slot, m.Version); }
            return e.slot;
        }
        int slot;
        if (_byMap.Count < Slots) slot = FirstSlot + _byMap.Count;
        else { int old = _lru.Last.Value; _lru.RemoveLast(); slot = _byMap[old].slot; _byMap.Remove(old); }
        Upload(slot, m);
        _byMap[m.Id] = (slot, m.Version); _lru.AddFirst(m.Id);
        return slot;
    }

    private readonly uint[] _buf = new uint[128 * 128];
    private void Upload(int slot, MapData m)
    {
        for (int i = 0; i < _buf.Length; i++) _buf[i] = MapColors.Rgba(m.Colors[i]);
        Array.UpdateLayer(slot, _buf);
    }

    public void Dispose() => Array.Dispose();
}

public sealed partial class EntityRenderer
{
    public readonly ModelBatch MapBatch = new();
    private MapTextureCache _mapTex;

    public void FlushMaps()
    {
        if (MapBatch.Verts == 0) return;
        _mapTex ??= new MapTextureCache();
        Flush(MapBatch, _mapTex.Array);
    }

    /// <summary>MapItemRenderer.renderMap: mapa w przestrzeni 0..128 (y w dół), toSpace zamienia (x, y, z) mapy na pozycję docelową.</summary>
    public void RenderMapQuads(MapData m, Func<float, float, float, Vector3> toSpace, bool inFrame, bool background)
    {
        _mapTex ??= new MapTextureCache();
        var b = MapBatch;
        b.Sky = Skin.Sky; b.Blk = Skin.Blk; b.Color = Vector4.One; b.Overlay = Vector4.Zero;
        if (background)
        {
            b.Layer = MapTextureCache.Background;
            b.V(toSpace(-7, 135, 0), 0, 1, 1); b.V(toSpace(135, 135, 0), 1, 1, 1); b.V(toSpace(135, -7, 0), 1, 0, 1); b.V(toSpace(-7, -7, 0), 0, 0, 1);
        }
        if (m == null) return;
        b.Layer = _mapTex.LayerFor(m);
        b.V(toSpace(0, 128, -.01f), 0, 1, 1); b.V(toSpace(128, 128, -.01f), 1, 1, 1); b.V(toSpace(128, 0, -.01f), 1, 0, 1); b.V(toSpace(0, 0, -.01f), 0, 0, 1);
        b.Layer = MapTextureCache.Icons;
        int k = 0;
        foreach (var d in m.Decorations.Values)
        {
            if (inFrame && d.Type != 1) continue;
            float u0 = d.Type % 4 / 4f, v0 = d.Type / 4 / 4f, u1 = u0 + .25f, v1 = v0 + .25f;
            float a = d.Rot * 360f / 16f * MathF.PI / 180f, cs = MathF.Cos(a), sn = MathF.Sin(a);
            float cx = d.X / 2f + 64, cz = d.Z / 2f + 64, z = -.02f + k * -.001f * 3;
            Vector3 P(float x, float y)
            {
                float px = (x - .125f) * 4, py = (y + .125f) * 4;
                return toSpace(cx + px * cs - py * sn, cz + px * sn + py * cs, z);
            }
            b.V(P(-1, 1), u0, v0, 1); b.V(P(1, 1), u1, v0, 1); b.V(P(1, -1), u1, v1, 1); b.V(P(-1, -1), u0, v1, 1);
            k++;
        }
    }

    private static float MapAngle(float pitch)
    {
        float f = 1f - pitch / 45f + .1f;
        f = Math.Clamp(f, 0, 1);
        return -MathF.Cos(f * MathF.PI) * .5f + .5f;
    }

    /// <summary>ItemRenderer.renderItemMap 1.8: obie ręce i mapa przed kamerą, kąt zależny od pochylenia głowy.</summary>
    private void RenderFirstPersonMap(Player p, float pt, float equip, float swing, ItemStack held)
    {
        var gs = new GlStack();
        float pitch = p.PrevPitch + (p.Pitch - p.PrevPitch) * pt;
        float f = -.4f * MathF.Sin(MathF.Sqrt(swing) * MathF.PI), f1 = .2f * MathF.Sin(MathF.Sqrt(swing) * MathF.PI * 2f), f2 = -.2f * MathF.Sin(swing * MathF.PI);
        gs.Translate(f, f1, f2);
        float f3 = MapAngle(pitch);
        gs.Translate(0, .04f, -.72f);
        gs.Translate(0, equip * -1.2f, 0);
        gs.Translate(0, f3 * -.5f, 0);
        gs.Rotate(90, 0, 1, 0);
        gs.Rotate(f3 * -85, 0, 0, 1);
        // renderPlayerArms
        Skin.Layer = SkinGen.Get("steve");
        var saved = gs.M;
        gs.Rotate(54, 0, 1, 0); gs.Rotate(64, 1, 0, 0); gs.Rotate(-62, 0, 0, 1); gs.Translate(.25f, -.85f, .75f);
        _rArm.Reset(-5, 2, 0); _rArm.RZ = .1f; RenderPart(Skin, _rArm, gs.M);
        gs.M = saved;
        gs.Rotate(41, 0, 1, 0); gs.Rotate(-55, 1, 0, 0); gs.Translate(.3f, -1.1f, .45f); gs.Rotate(25, 0, 0, 1);
        _lArm.Reset(5, 2, 0); _lArm.RZ = -.1f; RenderPart(Skin, _lArm, gs.M);
        gs.M = saved;
        float f4 = MathF.Sin(swing * swing * MathF.PI), f5 = MathF.Sin(MathF.Sqrt(swing) * MathF.PI);
        gs.Rotate(f4 * -20, 0, 1, 0); gs.Rotate(f5 * -20, 0, 0, 1); gs.Rotate(f5 * -80, 1, 0, 0);
        gs.Scale(.38f, .38f, .38f);
        gs.Rotate(90, 0, 1, 0); gs.Rotate(180, 0, 0, 1);
        gs.Translate(-1, -1, 0);
        gs.Scale(.015625f, .015625f, .015625f);
        var m = gs.M;
        var data = MapStore.Of(p.World).Get(held.Damage);
        RenderMapQuads(data, (x, y, z) => Vector3.TransformPosition(new Vector3(x, y, z), m), false, true);
    }

    /// <summary>Mapa w ramce: cały blok, obrót co 90 stopni (rotation % 4 * 2 * 45), tylko znaczniki ramek.</summary>
    public void RenderFrameMap(ItemFrameEntity f, Vector3 pos)
    {
        var data = MapStore.Of(f.World).Get(f.Item.Damage);
        int c = HangingEntity.Ccw(f.Facing);
        var r = new Vector3(Dir.HorizX[c], 0, Dir.HorizZ[c]); var n = new Vector3(Dir.HorizX[f.Facing], 0, Dir.HorizZ[f.Facing]);
        var o = pos - n * (1 / 32f) + n * (1 / 16f + 1 / 128f);
        float ang = f.Rotation % 4 * MathF.PI / 2, cs = MathF.Cos(ang), sn = MathF.Sin(ang);
        Skin.Sky = MapBatch.Sky;
        RenderMapQuads(data, (x, y, z) =>
        {
            float a = x / 128f - .5f, bUp = .5f - y / 128f;
            float a2 = a * cs + bUp * sn, b2 = -a * sn + bUp * cs;
            return o + r * a2 + Vector3.UnitY * b2 - n * (z / 128f);
        }, true, false);
    }
}

public static partial class TextureGen
{
    private static void TexMaps()
    {
        var empty = New(); var filled = New(); var noise = TileNoise(55, 8);
        for (int y = 2; y < 14; y++)
            for (int x = 1; x < 15; x++)
            {
                bool edge = x == 1 || x == 14 || y == 2 || y == 13;
                uint paper = Mul(Hex(0xE3D7B0), .9f + noise[y * 16 + x] * .15f);
                empty[y * 16 + x] = edge ? Hex(0x8C7A55) : paper;
                uint land = (x * 3 + y * 5) % 7 < 3 ? Hex(0x6F9A45) : (x + y) % 5 == 0 ? Hex(0x4A6FD0) : Hex(0xB89A62);
                filled[y * 16 + x] = edge ? Hex(0x8C7A55) : x > 2 && x < 13 && y > 3 && y < 12 ? land : paper;
            }
        Add("item_map_empty", empty); Add("item_map_filled", filled);
    }
}
