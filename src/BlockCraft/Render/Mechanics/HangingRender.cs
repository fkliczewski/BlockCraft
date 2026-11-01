using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Render obrazów (RenderPainting), ramek (RenderItemFrame) i stojaków na zbroję (ModelArmorStand) z 1.8.</summary>
public static class HangingRender
{
    /// <summary>Oś "w prawo" (rotateYCCW) i normalna od ściany w świecie.</summary>
    private static (Vector3 right, Vector3 normal) Axes(int facing)
    {
        int c = HangingEntity.Ccw(facing);
        return (new Vector3(Dir.HorizX[c], 0, Dir.HorizZ[c]), new Vector3(Dir.HorizX[facing], 0, Dir.HorizZ[facing]));
    }

    /// <summary>Prostopadłościan w osiach ściany: [r0,r1] w prawo, [y0,y1] w górę, [n0,n1] od ściany; przód (n1) z osobną teksturą.</summary>
    private static void WallBox(ModelBatch b, Vector3 o, Vector3 r, Vector3 n, float r0, float r1, float y0, float y1, float n0, float n1,
        int frontLayer, Vector4 frontUv, int sideLayer, Vector4 sideUv, int backLayer = -1, Vector4 backUv = default)
    {
        Vector3 P(float a, float y, float c) => o + r * a + Vector3.UnitY * y + n * c;
        void Q(Vector3 a, Vector3 bb, Vector3 c, Vector3 d, int layer, Vector4 uv, Vector3 normal)
        {
            b.Layer = layer;
            b.Quad(a, bb, c, d, uv.X, uv.Y, uv.Z, uv.W, b.ShadeFor(normal));
        }
        // przód: lewa-góra, lewa-dół, prawa-dół, prawa-góra
        Q(P(r0, y1, n1), P(r0, y0, n1), P(r1, y0, n1), P(r1, y1, n1), frontLayer, frontUv, n);
        Q(P(r1, y1, n0), P(r1, y0, n0), P(r0, y0, n0), P(r0, y1, n0), backLayer < 0 ? sideLayer : backLayer, backLayer < 0 ? sideUv : backUv, -n);
        Q(P(r0, y1, n0), P(r0, y1, n1), P(r1, y1, n1), P(r1, y1, n0), sideLayer, sideUv, Vector3.UnitY);
        Q(P(r0, y0, n1), P(r0, y0, n0), P(r1, y0, n0), P(r1, y0, n1), sideLayer, sideUv, -Vector3.UnitY);
        Q(P(r0, y1, n0), P(r0, y0, n0), P(r0, y0, n1), P(r0, y1, n1), sideLayer, sideUv, -r);
        Q(P(r1, y1, n1), P(r1, y0, n1), P(r1, y0, n0), P(r1, y1, n0), sideLayer, sideUv, r);
    }

    public static void Painting(EntityRenderer er, Entity e, float pt, Vector3 pos, Vector3 cam)
    {
        var p = (PaintingEntity)e;
        var art = PaintingArt.All[p.Art];
        var (r, n) = Axes(p.Facing);
        var b = er.Skin;
        float hw = art.W / 32f, hh = art.H / 32f, t = 1 / 32f;
        int front = SkinGen.Get("painting_" + art.Name), back = SkinGen.Get("painting_back");
        // tył i krawędzie: kafel 16x16 z tyłu arkusza (RenderPainting: u 0.75-0.8125)
        WallBox(b, pos, r, n, -hw, hw, -hh, hh, -t, t, front, new Vector4(0, 0, art.W / 64f, art.H / 64f), back, new Vector4(0, 0, 1 / 64f, 1), back, new Vector4(0, 0, 1, 1));
    }

    public static void ItemFrame(EntityRenderer er, Entity e, float pt, Vector3 pos, Vector3 cam)
    {
        var f = (ItemFrameEntity)e;
        var (r, n) = Axes(f.Facing);
        var b = er.Terrain;
        int planks = TextureGen.Layer("planks_birch"), bg = TextureGen.Layer("itemframe_background");
        var full = new Vector4(0, 0, 1, 1);
        // model item_frame.json: ramka 12x12 z listew 1 px (z 15-16), tło 10x10 (z 15.5-16); środek encji leży 1/32 od ściany
        Vector3 o = pos - n * (1 / 32f);
        float px = 1 / 16f;
        WallBox(b, o, r, n, -5 * px, 5 * px, -5 * px, 5 * px, 0, .5f * px, bg, new Vector4(3 / 16f, 3 / 16f, 13 / 16f, 13 / 16f), planks, full, planks, full);
        WallBox(b, o, r, n, -6 * px, 6 * px, 5 * px, 6 * px, 0, px, planks, new Vector4(2 / 16f, 2 / 16f, 14 / 16f, 3 / 16f), planks, full);
        WallBox(b, o, r, n, -6 * px, 6 * px, -6 * px, -5 * px, 0, px, planks, new Vector4(2 / 16f, 13 / 16f, 14 / 16f, 14 / 16f), planks, full);
        WallBox(b, o, r, n, -6 * px, -5 * px, -5 * px, 5 * px, 0, px, planks, new Vector4(2 / 16f, 3 / 16f, 3 / 16f, 13 / 16f), planks, full);
        WallBox(b, o, r, n, 5 * px, 6 * px, -5 * px, 5 * px, 0, px, planks, new Vector4(13 / 16f, 3 / 16f, 14 / 16f, 13 / 16f), planks, full);
        if (f.Item?.Item == null) return;
        // RenderItemFrame: przedmiot 0.4375 od tylnej ściany ramki, skala 0.5, obrót co 45 stopni wokół osi ramki
        float yaw = f.Facing * 90f;
        var m = Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(-f.Rotation * 45f));
        bool block = EntityRenderer.IsBlockModel(f.Item.Item);
        m = Matrix4.CreateScale(block ? .25f : .5f) * (block ? Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180)) : Matrix4.Identity) * m
            * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180 - yaw)) * Matrix4.CreateTranslation(o + n * (block ? .125f + 1 / 32f : 1 / 32f + .02f));
        er.RenderItem(f.Item, m);
    }

    // ModelArmorStand 1.8 (tekstura 64x64)
    private static readonly ModelPart Head = new ModelPart(0, 1, 0).Box(0, 0, -1, -7, -1, 2, 7, 2);
    private static readonly ModelPart Body = new ModelPart(0, 0, 0).Box(0, 26, -6, 0, -1.5f, 12, 3, 3);
    private static readonly ModelPart RArm = new ModelPart(-5, 2, 0).Box(24, 0, -2, -2, -1, 2, 12, 2);
    private static readonly ModelPart LArm = new ModelPart(5, 2, 0).Box(32, 16, 0, -2, -1, 2, 12, 2, 0, true);
    private static readonly ModelPart RLeg = new ModelPart(-1.9f, 12, 0).Box(8, 0, -1, 0, -1, 2, 11, 2);
    private static readonly ModelPart LLeg = new ModelPart(1.9f, 12, 0).Box(40, 16, -1, 0, -1, 2, 11, 2, 0, true);
    private static readonly ModelPart RSide = new ModelPart(0, 0, 0).Box(16, 0, -3, 3, -1, 2, 7, 2);
    private static readonly ModelPart LSide = new ModelPart(0, 0, 0).Box(48, 16, 1, 3, -1, 2, 7, 2);
    private static readonly ModelPart Waist = new ModelPart(0, 0, 0).Box(0, 48, -4, 10, -1, 8, 2, 2);
    private static readonly ModelPart Base = new ModelPart(0, 12, 0).Box(0, 32, -6, 11, -6, 12, 1, 12);
    // biped do zbroi (stojak nosi ją jak gracz)
    private static readonly ModelPart BHead = new ModelPart(0, 1, 0).Box(0, 0, -4, -8, -4, 8, 8, 8), BBody = new ModelPart().Box(16, 16, -4, 0, -2, 8, 12, 4);
    private static readonly ModelPart BRArm = new ModelPart(-5, 2, 0).Box(40, 16, -3, -2, -2, 4, 12, 4), BLArm = new ModelPart(5, 2, 0).Box(40, 16, -1, -2, -2, 4, 12, 4, 0, true);
    private static readonly ModelPart BRLeg = new ModelPart(-1.9f, 12, 0).Box(0, 16, -2, 0, -2, 4, 12, 4), BLLeg = new ModelPart(1.9f, 12, 0).Box(0, 16, -2, 0, -2, 4, 12, 4, 0, true);

    private static void Pose(ModelPart p, Vector3 deg) { p.RX = MathHelper.DegreesToRadians(deg.X); p.RY = MathHelper.DegreesToRadians(deg.Y); p.RZ = MathHelper.DegreesToRadians(deg.Z); }

    public static void ArmorStand(EntityRenderer er, Entity e, float pt, Vector3 pos, Vector3 cam)
    {
        var s = (ArmorStandEntity)e;
        float yaw = s.PrevYaw + MathUtil.WrapDegrees(s.Yaw - s.PrevYaw) * pt;
        // RenderArmorStand.rotateCorpse: chwianie po uderzeniu przez 5 ticków
        float since = s.World.TotalTime - s.PunchTime + pt;
        if (since < 5) yaw += MathF.Sin(since / 1.5f * MathF.PI) * 3f;
        var root = EntityRenderer.EntityRoot(pos, yaw, s.Small ? .5f : 1f);
        var b = er.Skin;
        b.Layer = SkinGen.Get("armorstand");
        Pose(Head, s.HeadPose); Pose(Body, s.BodyPose); Pose(RArm, s.RightArmPose); Pose(LArm, s.LeftArmPose); Pose(RLeg, s.RightLegPose); Pose(LLeg, s.LeftLegPose);
        Pose(RSide, s.BodyPose); Pose(LSide, s.BodyPose); Pose(Waist, s.BodyPose);
        foreach (var part in new[] { Head, Body, RLeg, LLeg, RSide, LSide, Waist }) er.RenderPart(b, part, root);
        if (s.ShowArms) { er.RenderPart(b, RArm, root); er.RenderPart(b, LArm, root); }
        if (!s.NoBasePlate) er.RenderPart(b, Base, root);
        // zbroja na bipedzie z tymi samymi kątami
        Pose(BHead, s.HeadPose); Pose(BBody, s.BodyPose); Pose(BRArm, s.RightArmPose); Pose(BLArm, s.LeftArmPose); Pose(BRLeg, s.RightLegPose); Pose(BLLeg, s.LeftLegPose);
        er.RenderArmorLayers(s.Armor, BHead, BBody, BRArm, BLArm, BRLeg, BLLeg, root);
        if (s.ShowArms && s.Held != null) er.RenderHeldPublic(s.Held, RArm, root);
    }
}

public sealed partial class EntityRenderer
{
    public void RenderHeldPublic(ItemStack s, ModelPart arm, Matrix4 root) => RenderHeld(s, arm, root);

    private static readonly ModelPart[] ArmorOuter = MakeArmor(1f), ArmorInner = MakeArmor(.5f);
    /// <summary>ModelBiped(1.0) i (0.5) z LayerBipedArmor: głowa, nakładka (hełm), tułów, ręce, nogi.</summary>
    private static ModelPart[] MakeArmor(float inf) => new[]
    {
        new ModelPart().Box(0, 0, -4, -8, -4, 8, 8, 8, inf), new ModelPart().Box(32, 0, -4, -8, -4, 8, 8, 8, inf + .5f),
        new ModelPart().Box(16, 16, -4, 0, -2, 8, 12, 4, inf),
        new ModelPart().Box(40, 16, -3, -2, -2, 4, 12, 4, inf), new ModelPart().Box(40, 16, -1, -2, -2, 4, 12, 4, inf, true),
        new ModelPart().Box(0, 16, -2, 0, -2, 4, 12, 4, inf), new ModelPart().Box(0, 16, -2, 0, -2, 4, 12, 4, inf, true),
    };
    private static readonly string[] ArmorMat = { "leather", "iron", "gold", "diamond", "chainmail" };

    private static void Copy(ModelPart dst, ModelPart src) { dst.PX = src.PX; dst.PY = src.PY; dst.PZ = src.PZ; dst.RX = src.RX; dst.RY = src.RY; dst.RZ = src.RZ; }

    /// <summary>LayerBipedArmor 1.8: hełm, napierśnik i buty z warstwy 1 (powiększenie 1.0), spodnie z warstwy 2 (0.5); skóra barwiona 0xA06540.</summary>
    public void RenderArmorLayers(ItemStack[] armor, ModelPart head, ModelPart body, ModelPart rArm, ModelPart lArm, ModelPart rLeg, ModelPart lLeg, Matrix4 root)
    {
        if (armor == null) return;
        var b = Skin; var color = b.Color;
        for (int slot = 0; slot < 4; slot++)
        {
            var it = armor[slot]?.Item;
            if (it == null || it.ArmorMaterial < 0 || it.ArmorMaterial >= ArmorMat.Length) continue;
            var parts = slot == 2 ? ArmorInner : ArmorOuter;
            Copy(parts[0], head); Copy(parts[1], head); Copy(parts[2], body); Copy(parts[3], rArm); Copy(parts[4], lArm); Copy(parts[5], rLeg); Copy(parts[6], lLeg);
            var show = slot switch { 0 => new[] { 0, 1 }, 1 => new[] { 2, 3, 4 }, 2 => new[] { 2, 5, 6 }, _ => new[] { 5, 6 } };
            string mat = ArmorMat[it.ArmorMaterial], layer = slot == 2 ? "_2" : "_1";
            bool leather = it.ArmorMaterial == 0;
            b.Layer = SkinGen.Get("armor_" + mat + layer);
            if (leather) b.Color = new Vector4(0xA0 / 255f, 0x65 / 255f, 0x40 / 255f, 1);
            foreach (int i in show) RenderPart(b, parts[i], root);
            b.Color = color;
            if (leather) { b.Layer = SkinGen.Get("armor_leather" + layer + "_overlay"); foreach (int i in show) RenderPart(b, parts[i], root); }
        }
    }
}

public static partial class SkinGen
{
    static partial void SkinsMechanics()
    {
        // obrazy: oryginalny arkusz z client.jar 1.8, gdy jest w cache; inaczej proceduralne płótna
        var sheet = ClientAssets.TryReadTexture("painting/paintings_kristoffer_zetterstrand.png", out int sw, out int sh);
        if (sheet != null && (sw != 256 || sh != 256)) sheet = null;
        foreach (var art in PaintingArt.All)
        {
            var c = new Cv(art.Name.GetHashCode() & 0xFFFF);
            if (sheet != null) { for (int y = 0; y < art.H; y++) for (int x = 0; x < art.W; x++) c.Set(x, y, sheet[(art.V + y) * 256 + art.U + x] | 0xFF000000); }
            else ProceduralPainting(c, art);
            Add("painting_" + art.Name, c);
        }
        var back = new Cv(4242);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            back.Set(x, y, sheet != null ? sheet[(y / 4) * 256 + 192 + x / 4] | 0xFF000000 : V(back.R, (y / 4) % 4 == 0 ? 0x6B5233 : 0x8C6D45, .1f));
        Add("painting_back", back);

        // stojak na zbroję: entity/armorstand/wood.png
        Add("armorstand", FromClientOr("entity/armorstand/wood.png", 64, 64, cv =>
        {
            foreach (var (u, v, w, h, d) in new[] { (0, 0, 2, 7, 2), (0, 26, 12, 3, 3), (24, 0, 2, 12, 2), (32, 16, 2, 12, 2), (8, 0, 2, 11, 2), (40, 16, 2, 11, 2), (16, 0, 2, 7, 2), (48, 16, 2, 7, 2), (0, 48, 8, 2, 2) })
                cv.Fill(u, v, w, h, d, 0xA0814E);
            cv.Fill(0, 32, 12, 1, 12, 0x8B8B8B);
        }));

        // warstwy zbroi models/armor/<materiał>_layer_1/2.png (64x32 w górnej połowie płótna)
        foreach (var (mat, color) in new[] { ("leather", 0xC8C8C8u), ("iron", 0xD8D8D8u), ("gold", 0xEAC33Au), ("diamond", 0x5DE0D2u), ("chainmail", 0x8E8E8Eu) })
            foreach (string layer in new[] { "_1", "_2" })
            {
                Add("armor_" + mat + layer, FromClientOr("models/armor/" + mat + "_layer" + layer + ".png", 64, 32, cv => ProceduralArmor(cv, color, layer == "_2", mat == "chainmail")));
                if (mat == "leather") Add("armor_leather" + layer + "_overlay", FromClientOr("models/armor/leather_layer" + layer + "_overlay.png", 64, 32, cv => { }));
            }
    }

    private static Cv FromClientOr(string path, int w, int h, Action<Cv> fallback)
    {
        var c = new Cv(path.GetHashCode() & 0xFFFF);
        var px = ClientAssets.TryReadTexture(path, out int iw, out int ih);
        if (px != null && iw == w && ih == h) { for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) c.Set(x, y, px[y * w + x]); }
        else fallback(c);
        return c;
    }

    private static void ProceduralArmor(Cv c, uint color, bool legs, bool chain)
    {
        uint Px(int x, int y) => chain && (x + y) % 3 == 0 ? 0 : V(c.R, color, .15f);
        if (legs)
        {
            c.Box(16, 16, 8, 12, 4, (f, x, y, w, h) => f == 3 || f == 5 ? (y < 5 ? Px(x, y) : 1) : Px(x, y));
            c.Box(0, 16, 4, 12, 4, (f, x, y, w, h) => Px(x, y));
            return;
        }
        c.Box(0, 0, 8, 8, 8, (f, x, y, w, h) => f == 3 && y >= 3 && y <= 5 && x >= 1 && x <= 6 ? 0 : Px(x, y));
        c.Box(16, 16, 8, 12, 4, (f, x, y, w, h) => Px(x, y));
        c.Box(40, 16, 4, 12, 4, (f, x, y, w, h) => y < 8 ? Px(x, y) : 1);
        c.Box(0, 16, 4, 12, 4, (f, x, y, w, h) => y >= 9 || f == 1 ? Px(x, y) : 1);
    }

    private static void ProceduralPainting(Cv c, PaintingArt art)
    {
        var r = c.R;
        uint[] pal = { 0x2B4A6F, 0xC9A253, 0x7A3B2E, 0x3E6B3A, 0xD8C8A0, 0x5A4A78, 0xA85A3A, 0x1E2A22 };
        uint bg = Hx(pal[art.Name.Length % pal.Length]), fg = Hx(pal[(art.Name.Length * 3 + art.W) % pal.Length]);
        for (int y = 0; y < art.H; y++)
            for (int x = 0; x < art.W; x++)
            {
                bool edge = x == 0 || y == 0 || x == art.W - 1 || y == art.H - 1;
                float wave = MathF.Sin(x * .35f + art.U) + MathF.Cos(y * .3f + art.V);
                c.Set(x, y, edge ? Hx(0x5C3D1E) : V(r, wave > .6f ? fg : bg, .2f));
            }
    }
}

public static partial class TextureGen
{
    /// <summary>Ikony obrazu, ramki i stojaka oraz tło ramki (itemframe_background).</summary>
    private static void TexHanging()
    {
        var bgTile = New(); var noise = TileNoise(77, 8);
        for (int i = 0; i < 256; i++) bgTile[i] = Mul(Hex(0x8E5A34), .8f + noise[i] * .35f);
        Add("itemframe_background", bgTile);

        var frame = New(); var painting = New(); var stand = New();
        for (int y = 1; y < 15; y++)
            for (int x = 1; x < 15; x++)
            {
                bool border = x < 3 || y < 3 || x > 12 || y > 12;
                frame[y * 16 + x] = border ? Mul(Hex(0xC8B077), .85f + noise[y * 16 + x] * .3f) : Mul(Hex(0x8E5A34), .8f + noise[y * 16 + x] * .3f);
                painting[y * 16 + x] = border ? Hex(x == 1 || y == 1 ? 0x7A5230u : 0x5C3D1Eu)
                    : y > 9 ? Hex(0x3E6B3A) : (x - 9) * (x - 9) + (y - 5) * (y - 5) < 5 ? Hex(0xE8C84A) : Hex(0x5D8FCB);
            }
        for (int y = 1; y < 15; y++) { stand[y * 16 + 7] = Hex(0xA0814E); stand[y * 16 + 8] = Hex(0x7A6038); }
        for (int x = 3; x < 13; x++) { stand[3 * 16 + x] = Hex(0xA0814E); stand[8 * 16 + x] = Hex(0x8C7044); stand[14 * 16 + x] = Hex(0x8B8B8B); stand[15 * 16 + x] = Hex(0x6B6B6B); }
        for (int y = 0; y < 3; y++) for (int x = 6; x < 10; x++) stand[y * 16 + x] = Hex(0xA0814E);
        Add("item_item_frame", frame); Add("item_painting", painting); Add("item_armor_stand", stand);
    }
}
