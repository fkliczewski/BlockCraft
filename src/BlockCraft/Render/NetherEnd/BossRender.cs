using BlockCraft.Core;
using BlockCraft.Entities;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Modele bossów i kryształu z MC 1.8: ModelDragon (tekstura 256x256 we własnej tablicy), ModelEnderCrystal, ModelWither.</summary>
public sealed partial class EntityRenderer
{
    private const int DragonTexSize = 256;
    private TextureArray _bossTex;
    private readonly ModelBatch _bossBatch = new();
    private ModelPart _dHead, _dJaw, _dSpine, _dBody, _dWing, _dWingTip, _dFrontLeg, _dFrontLegTip, _dFrontFoot, _dRearLeg, _dRearLegTip, _dRearFoot;
    private ModelPart _cGlass, _cCube, _cBase;
    private ModelPart[] _wBody, _wHeads, _wBodyArmor, _wHeadsArmor;
    private ModelPart _skullHead;

    private static Matrix4 Rx(float deg) => Matrix4.CreateRotationX(MathHelper.DegreesToRadians(deg));
    private static Matrix4 Ry(float deg) => Matrix4.CreateRotationY(MathHelper.DegreesToRadians(deg));
    private static Matrix4 Rz(float deg) => Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(deg));
    private static Matrix4 Tr(float x, float y, float z) => Matrix4.CreateTranslation(x, y, z);

    private void InitBossModels()
    {
        if (_dHead != null) return;
        // ModelDragon
        _dHead = P(0, 0, 0).Box(176, 44, -6, -1, -24, 12, 5, 16).Box(112, 30, -8, -8, -10, 16, 16, 16)
            .Box(0, 0, -5, -12, -4, 2, 4, 6, 0, true).Box(112, 0, -5, -3, -22, 2, 2, 4, 0, true)
            .Box(0, 0, 3, -12, -4, 2, 4, 6).Box(112, 0, 3, -3, -22, 2, 2, 4);
        _dJaw = P(0, 4, -8).Box(176, 65, -6, 0, -16, 12, 4, 16);
        _dHead.Children.Add(_dJaw);
        _dSpine = P(0, 0, 0).Box(192, 104, -5, -5, -5, 10, 10, 10).Box(48, 0, -1, -9, -3, 2, 4, 6);
        _dBody = P(0, 4, 8).Box(0, 0, -12, 0, -16, 24, 24, 64).Box(220, 53, -1, -6, -10, 2, 6, 12).Box(220, 53, -1, -6, 10, 2, 6, 12).Box(220, 53, -1, -6, 30, 2, 6, 12);
        _dWing = P(-12, 5, 2).Box(112, 88, -56, -4, -4, 56, 8, 8).Box(-56, 88, -56, 0, 2, 56, 0, 56);
        _dWingTip = P(-56, 0, 0).Box(112, 136, -56, -2, -2, 56, 4, 4).Box(-56, 144, -56, 0, 2, 56, 0, 56);
        _dWing.Children.Add(_dWingTip);
        _dFrontLeg = P(-12, 20, 2).Box(112, 104, -4, -4, -4, 8, 24, 8);
        _dFrontLegTip = P(0, 20, -1).Box(226, 138, -3, -1, -3, 6, 24, 6);
        _dFrontFoot = P(0, 23, 0).Box(144, 104, -4, 0, -12, 8, 4, 16);
        _dFrontLeg.Children.Add(_dFrontLegTip); _dFrontLegTip.Children.Add(_dFrontFoot);
        _dRearLeg = P(-16, 16, 42).Box(0, 0, -8, -4, -8, 16, 32, 16);
        _dRearLegTip = P(0, 32, -4).Box(196, 0, -6, -2, 0, 12, 32, 12);
        _dRearFoot = P(0, 31, 4).Box(112, 0, -9, 0, -20, 18, 6, 24);
        _dRearLeg.Children.Add(_dRearLegTip); _dRearLegTip.Children.Add(_dRearFoot);
        // ModelEnderCrystal (64x32)
        _cGlass = P(0, 0, 0).Box(0, 0, -4, -4, -4, 8, 8, 8);
        _cCube = P(0, 0, 0).Box(32, 0, -4, -4, -4, 8, 8, 8);
        _cBase = P(0, 0, 0).Box(0, 16, -6, 0, -6, 12, 4, 12);
        // ModelWither (64x64) i jego wersja powiększona o 0.5 dla tarczy (LayerWitherAura)
        _wBody = WitherBody(0); _wHeads = WitherHeads(0);
        _wBodyArmor = WitherBody(.5f); _wHeadsArmor = WitherHeads(.5f);
        // ModelSkeletonHead dla czaszki Withera
        _skullHead = P(0, 0, 0).Box(0, 0, -4, -8, -4, 8, 8, 8);
    }

    private static ModelPart[] WitherBody(float inf) => new[]
    {
        P(0, 0, 0).Box(0, 16, -10, 3.9f, -.5f, 20, 3, 3, inf),
        P(-2, 6.9f, -.5f).Box(0, 22, 0, 0, 0, 3, 10, 3, inf).Box(24, 22, -4, 1.5f, .5f, 11, 2, 2, inf).Box(24, 22, -4, 4, .5f, 11, 2, 2, inf).Box(24, 22, -4, 6.5f, .5f, 11, 2, 2, inf),
        P(0, 0, 0).Box(12, 22, 0, 0, 0, 3, 6, 3, inf)
    };
    private static ModelPart[] WitherHeads(float inf) => new[]
    {
        P(0, 0, 0).Box(0, 0, -4, -4, -4, 8, 8, 8, inf),
        P(-8, 4, 0).Box(32, 0, -4, -4, -4, 6, 6, 6, inf),
        P(10, 4, 0).Box(32, 0, -4, -4, -4, 6, 6, 6, inf)
    };

    private void EnsureBossTex()
    {
        _bossTex ??= new TextureArray(DragonTexSize, new[] { DragonTexture.Make() }, false);
    }

    private void FlushBoss() => Flush(_bossBatch, _bossTex);

    // ================= smok =================
    private static float Wrap(double d) => MathUtil.WrapDegrees((float)d);

    private void RenderDragon(EnderDragon d, LivingPose pose)
    {
        InitBossModels(); EnsureBossTex();
        var b = _bossBatch; b.Layer = 0; b.Sky = Skin.Sky; b.Blk = Math.Max(Skin.Blk, 8); b.Color = Vector4.One;
        b.Overlay = d.HurtTime > 0 ? new Vector4(1, 0, 0, .3f) : Vector4.Zero;
        float pt = pose.Pt;
        float f = d.PrevAnimTime + (d.AnimTime - d.PrevAnimTime) * pt;
        _dJaw.RX = (MathF.Sin(f * MathF.PI * 2) + 1) * .2f;
        float f1 = MathF.Sin(f * MathF.PI * 2 - 1) + 1; f1 = (f1 * f1 + f1 * 2) * .05f;
        // RenderDragon.rotateCorpse + RendererLivingEntity: scale(-1,-1,1), translate(0,-1.5078125,0)
        float corpseYaw = (float)d.MovementOffsets(7, pt).yaw, corpsePitch = (float)(d.MovementOffsets(5, pt).y - d.MovementOffsets(10, pt).y);
        var root = Tr(0, -1.5078125f, 0) * Matrix4.CreateScale(-1, -1, 1) * Tr(0, 0, 1) * Rx(corpsePitch * 10) * Ry(-corpseYaw) * Matrix4.CreateTranslation(pose.Pos);
        var m = Rx(f1 * 2) * Tr(0, f1 - 2, -3) * root;
        const float f5 = 1.5f;
        var a6 = d.MovementOffsets(6, pt);
        float f6 = Wrap(d.MovementOffsets(5, pt).yaw - d.MovementOffsets(10, pt).yaw);
        float f7 = Wrap(d.MovementOffsets(5, pt).yaw + f6 / 2f);
        float f8 = f * MathF.PI * 2, py = 20, pz = -12, px = 0;
        for (int i = 0; i < 5; i++)
        {
            var a1 = d.MovementOffsets(5 - i, pt);
            float f10 = MathF.Cos(i * .45f + f8) * .15f;
            _dSpine.RY = Wrap(a1.yaw - a6.yaw) * MathF.PI / 180f * f5;
            _dSpine.RX = f10 + (float)(a1.y - a6.y) * MathF.PI / 180f * f5 * 5f;
            _dSpine.RZ = -Wrap(a1.yaw - f7) * MathF.PI / 180f * f5;
            _dSpine.PY = py; _dSpine.PZ = pz; _dSpine.PX = px;
            py += MathF.Sin(_dSpine.RX) * 10; pz -= MathF.Cos(_dSpine.RY) * MathF.Cos(_dSpine.RX) * 10; px -= MathF.Sin(_dSpine.RY) * MathF.Cos(_dSpine.RX) * 10;
            RenderPart(b, _dSpine, m, DragonTexSize, DragonTexSize);
        }
        _dHead.PY = py; _dHead.PZ = pz; _dHead.PX = px;
        var a0 = d.MovementOffsets(0, pt);
        _dHead.RY = Wrap(a0.yaw - a6.yaw) * MathF.PI / 180f;
        _dHead.RZ = -Wrap(a0.yaw - f7) * MathF.PI / 180f;
        RenderPart(b, _dHead, m, DragonTexSize, DragonTexSize);
        var mb = Tr(0, -1, 0) * Rz(-f6 * f5) * Tr(0, 1, 0) * m;
        _dBody.RZ = 0;
        RenderPart(b, _dBody, mb, DragonTexSize, DragonTexSize);
        for (int j = 0; j < 2; j++)
        {
            float f12 = f * MathF.PI * 2;
            _dWing.RX = .125f - MathF.Cos(f12) * .2f; _dWing.RY = .25f; _dWing.RZ = (MathF.Sin(f12) + .125f) * .8f;
            _dWingTip.RZ = -(MathF.Sin(f12 + 2) + .5f) * .75f;
            _dRearLeg.RX = 1 + f1 * .1f; _dRearLegTip.RX = .5f + f1 * .1f; _dRearFoot.RX = .75f + f1 * .1f;
            _dFrontLeg.RX = 1.3f + f1 * .1f; _dFrontLegTip.RX = -.5f - f1 * .1f; _dFrontFoot.RX = .75f + f1 * .1f;
            RenderPart(b, _dWing, mb, DragonTexSize, DragonTexSize);
            RenderPart(b, _dFrontLeg, mb, DragonTexSize, DragonTexSize);
            RenderPart(b, _dRearLeg, mb, DragonTexSize, DragonTexSize);
            mb = Matrix4.CreateScale(-1, 1, 1) * mb;
        }
        float f11 = 0; py = 10; pz = 60; px = 0;
        var a11 = d.MovementOffsets(11, pt);
        for (int k = 0; k < 12; k++)
        {
            var a2 = d.MovementOffsets(12 + k, pt);
            f11 += MathF.Sin(k * .45f + f8) * .05f;
            _dSpine.RY = (Wrap(a2.yaw - a11.yaw) * f5 + 180f) * MathF.PI / 180f;
            _dSpine.RX = f11 + (float)(a2.y - a11.y) * MathF.PI / 180f * f5 * 5f;
            _dSpine.RZ = Wrap(a2.yaw - f7) * MathF.PI / 180f * f5;
            _dSpine.PY = py; _dSpine.PZ = pz; _dSpine.PX = px;
            py += MathF.Sin(_dSpine.RX) * 10; pz -= MathF.Cos(_dSpine.RY) * MathF.Cos(_dSpine.RX) * 10; px -= MathF.Sin(_dSpine.RY) * MathF.Cos(_dSpine.RX) * 10;
            RenderPart(b, _dSpine, m, DragonTexSize, DragonTexSize);
        }
        if (d.DeathTicks > 0) DragonDeathRays(d, pt, root);
        if (d.HealingCrystal is { Dead: false } c) CrystalBeam(d, c, pose);
        b.Overlay = Vector4.Zero;
        FlushBoss();
    }

    /// <summary>LayerEnderDragonDeath: rosnący pęk promieni z ziarna 432, ostatnie 20% wygasa.</summary>
    private void DragonDeathRays(EnderDragon d, float pt, Matrix4 root)
    {
        var b = _bossBatch;
        float f = (d.DeathTicks + pt) / 200f, f1 = f > .8f ? (f - .8f) / .2f : 0;
        var rnd = new JavaRandom(432);
        var m = Tr(0, -1, -2) * root;
        float u = 4f / DragonTexSize, v = 252f / DragonTexSize;
        b.Blk = b.Sky = 15; b.Overlay = Vector4.Zero;
        for (int i = 0; i < (f + f * f) / 2f * 60f; i++)
        {
            // kolejność losowań jak w GL: X, Y, Z, X, Y, Z (+f*90)
            var a = new float[6]; for (int k = 0; k < 6; k++) a[k] = rnd.NextFloat() * 360;
            m = Rz(a[5] + f * 90) * Ry(a[4]) * Rx(a[3]) * Rz(a[2]) * Ry(a[1]) * Rx(a[0]) * m;
            float len = rnd.NextFloat() * 20 + 5 + f1 * 10, w = rnd.NextFloat() * 2 + 1 + f1 * 2;
            var c = Vector3.TransformPosition(Vector3.Zero, m);
            var p1 = Vector3.TransformPosition(new Vector3(-.866f * w, len, -.5f * w), m);
            var p2 = Vector3.TransformPosition(new Vector3(.866f * w, len, -.5f * w), m);
            var p3 = Vector3.TransformPosition(new Vector3(0, len, w), m);
            var white = new Vector4(1, 1, 1, 1 - f1); var purple = new Vector4(1, 0, 1, .2f * (1 - f1));
            foreach (var (pa, pb) in new[] { (p1, p2), (p2, p3), (p3, p1) })
            {
                b.Color = white; b.V(c, u, v, 1); b.V(c, u, v, 1);
                b.Color = purple; b.V(pa, u, v, 1); b.V(pb, u, v, 1);
            }
        }
        b.Color = Vector4.One;
    }

    /// <summary>Promień leczenia od kryształu do smoka (RenderDragon.doRender): stożek z 8 ścian, ciemny przy smoku, jasny przy krysztale.</summary>
    private void CrystalBeam(EnderDragon d, EnderCrystal c, LivingPose pose)
    {
        var b = _bossBatch;
        float pt = pose.Pt, rot = c.InnerRotation + pt, bob = MathF.Sin(rot * .2f) / 2 + .5f;
        bob = (bob * bob + bob) * .2f;
        var start = pose.Pos + new Vector3(0, 2, 0);
        var end = pose.Pos + new Vector3((float)(c.X - d.IX(pt)), (float)(c.Y - d.IY(pt)) + 1.2f + bob, (float)(c.Z - d.IZ(pt)));
        var axis = end - start; if (axis.LengthSquared < 1e-4f) return;
        var dir = Vector3.Normalize(axis);
        var side = Vector3.Normalize(Vector3.Cross(dir, MathF.Abs(dir.Y) > .9f ? Vector3.UnitX : Vector3.UnitY)); var up = Vector3.Cross(side, dir);
        float u = 4f / DragonTexSize, v = 252f / DragonTexSize;
        b.Blk = b.Sky = 15; b.Overlay = Vector4.Zero;
        for (int i = 0; i < 8; i++)
        {
            float a0 = i * MathF.PI * 2 / 8, a1 = (i + 1) * MathF.PI * 2 / 8;
            Vector3 o0 = side * MathF.Sin(a0) + up * MathF.Cos(a0), o1 = side * MathF.Sin(a1) + up * MathF.Cos(a1);
            b.Color = new Vector4(.35f, .1f, .45f, 1); b.V(start + o0 * .15f, u, v, 1); b.V(start + o1 * .15f, u, v, 1);
            b.Color = new Vector4(1, .85f, 1, 1); b.V(end + o1 * .75f, u, v, 1); b.V(end + o0 * .75f, u, v, 1);
        }
        b.Color = Vector4.One;
    }

    // ================= kryształ =================
    private void RenderEnderCrystal(EnderCrystal c, float pt, Vector3 pos)
    {
        InitBossModels();
        Skin.Layer = SkinGen.Get("ender_crystal"); Skin.Blk = 15;
        float f = c.InnerRotation + pt, bob = MathF.Sin(f * .2f) / 2 + .5f;
        bob = bob * bob + bob;
        // RenderEnderCrystal + ModelEnderCrystal.render; +1 w pionie, bo podstawa leży u stóp encji (na bedrocku)
        var diag = Matrix4.CreateFromAxisAngle(new Vector3(.70710677f, 0, .70710677f), MathHelper.DegreesToRadians(60));
        var spin = Ry(f * 3);
        var baseM = Tr(0, -.5f, 0) * Matrix4.CreateScale(2) * Matrix4.CreateTranslation(pos + new Vector3(0, 1, 0));
        RenderPart(Skin, _cBase, baseM);
        var m1 = diag * Tr(0, .8f + bob * .2f, 0) * spin * baseM;
        RenderPart(Skin, _cGlass, m1);
        var m2 = spin * diag * Matrix4.CreateScale(.875f) * m1;
        RenderPart(Skin, _cGlass, m2);
        var m3 = spin * diag * Matrix4.CreateScale(.875f) * m2;
        RenderPart(Skin, _cCube, m3);
    }

    // ================= Wither =================
    private void RenderWither(Wither w, LivingPose pose)
    {
        InitBossModels();
        float s = 2f;
        if (w.InvulTime > 0) s -= (w.InvulTime - pose.Pt) / 220f * .5f;
        var root = ScaledRoot(w, pose, s, s, s);
        bool flash = w.InvulTime > 0 && w.InvulTime / 5 % 2 == 1;
        Skin.Layer = SkinGen.Get(flash ? "wither_invulnerable" : "wither");
        PoseWither(w, pose, _wBody, _wHeads);
        foreach (var p in _wBody) RenderPart(Skin, p, root);
        foreach (var p in _wHeads) RenderPart(Skin, p, root);
        if (w.Armored)
        {
            // LayerWitherAura: przesuwana tekstura tarczy - tu 4 klatki
            Skin.Layer = SkinGen.Get("wither_armor_" + (int)(pose.Age / 3) % 4);
            Skin.Blk = 15;
            PoseWither(w, pose, _wBodyArmor, _wHeadsArmor);
            foreach (var p in _wBodyArmor) RenderPart(Skin, p, root);
            foreach (var p in _wHeadsArmor) RenderPart(Skin, p, root);
        }
    }

    private static void PoseWither(Wither w, LivingPose pose, ModelPart[] body, ModelPart[] heads)
    {
        float f6 = MathF.Cos(pose.Age * .1f);
        body[1].RX = (.065f + .05f * f6) * MathF.PI;
        body[2].Reset(-2, 6.9f + MathF.Cos(body[1].RX) * 10, -.5f + MathF.Sin(body[1].RX) * 10);
        body[2].RX = (.265f + .1f * f6) * MathF.PI;
        heads[0].RY = MathHelper.DegreesToRadians(pose.HeadYaw); heads[0].RX = MathHelper.DegreesToRadians(pose.Pitch);
        for (int i = 1; i < 3; i++)
        {
            float yaw = MathUtil.LerpAngle(w.PrevHeadYawAbs[i - 1], w.HeadYawAbs[i - 1], pose.Pt), pitch = MathHelper.Lerp(w.PrevHeadPitch[i - 1], w.HeadPitch[i - 1], pose.Pt);
            heads[i].RY = MathHelper.DegreesToRadians(MathUtil.WrapDegrees(yaw - pose.BodyYaw));
            heads[i].RX = MathHelper.DegreesToRadians(pitch);
        }
    }

    private void RenderWitherSkull(WitherSkullEntity s, float pt, Vector3 pos)
    {
        InitBossModels();
        Skin.Layer = SkinGen.Get(s.Blue ? "wither_invulnerable" : "wither"); Skin.Blk = 15;
        float yaw = (float)(Math.Atan2(-s.MotionX, s.MotionZ) * MathUtil.Rad2Deg);
        float pitch = (float)(-Math.Atan2(s.MotionY, Math.Sqrt(s.MotionX * s.MotionX + s.MotionZ * s.MotionZ)) * MathUtil.Rad2Deg);
        _skullHead.RY = MathHelper.DegreesToRadians(yaw); _skullHead.RX = MathHelper.DegreesToRadians(pitch);
        RenderPart(Skin, _skullHead, Matrix4.CreateScale(-1, -1, 1) * Matrix4.CreateTranslation(pos));
    }

    public static void RegisterBossRenderers()
    {
        CustomLiving[typeof(EnderDragon)] = (r, e, p) => r.RenderDragon((EnderDragon)e, p);
        CustomLiving[typeof(Wither)] = (r, e, p) => r.RenderWither((Wither)e, p);
        Custom[typeof(DragonPart)] = (r, e, pt, pos, cam) => { };
        Custom[typeof(EnderCrystal)] = (r, e, pt, pos, cam) => r.RenderEnderCrystal((EnderCrystal)e, pt, pos);
        Custom[typeof(WitherSkullEntity)] = (r, e, pt, pos, cam) => r.RenderWitherSkull((WitherSkullEntity)e, pt, pos);
    }
}

/// <summary>Proceduralna tekstura smoka 256x256 w układzie UV z ModelDragon (MC 1.8).</summary>
public static class DragonTexture
{
    private const int S = 256;
    public static uint[] Make()
    {
        var p = new uint[S * S]; var r = new Rng(203);
        void Set(int x, int y, uint c) { if (x >= 0 && x < S && y >= 0 && y < S) p[y * S + x] = c; }
        uint V(uint c, float a = .14f) => TextureGen.Mul(TextureGen.Hex(c), 1 - a / 2 + r.NextFloat() * a);
        void Box(int u, int v, int w, int h, int d, Func<int, int, int, int, int, uint> f)
        {
            void Face(int face, int fx, int fy, int fw, int fh) { for (int j = 0; j < fh; j++) for (int i = 0; i < fw; i++) Set(fx + i, fy + j, f(face, i, j, fw, fh)); }
            Face(0, u + d, v, w, d); Face(1, u + d + w, v, w, d); Face(2, u, v + d, d, h);
            Face(3, u + d, v + d, w, h); Face(4, u + d + w, v + d, d, h); Face(5, u + d + w + d, v + d, w, h);
        }
        // łuski: ciemny grafit z jaśniejszym rąbkiem co kilka pikseli
        uint Scales(int face, int x, int y, int w, int h)
        {
            bool rim = (y + (x / 4 % 2) * 2) % 4 == 0;
            if (face == 1) return V(0x262626, .1f);
            return V(rim ? 0x2E2E2Eu : 0x1A1A1Au, .16f);
        }
        uint Ridge(int face, int x, int y, int w, int h) => V(face == 0 ? 0x5A5A5Au : 0x444444u, .14f);
        Box(0, 0, 24, 24, 64, Scales);               // tułów (i udo tylnej łapy z tym samym UV)
        Box(220, 53, 2, 6, 12, Ridge);               // grzebień tułowia
        Box(48, 0, 2, 4, 6, Ridge);                  // grzebień szyi
        Box(0, 0, 2, 4, 6, Ridge);                   // rogi głowy
        Box(192, 104, 10, 10, 10, Scales);           // kręg szyi i ogona
        Box(112, 30, 16, 16, 16, (f, x, y, w, h) =>  // głowa: fioletowe oczy z przodu, nad pyskiem
        {
            if (f == 3 && y >= 3 && y <= 5 && (x >= 1 && x <= 4 || x >= 11 && x <= 14)) return TextureGen.Hex(y == 4 ? 0xE079FAu : 0xCC00FAu);
            if ((f == 2 || f == 4) && y >= 3 && y <= 4 && (f == 2 ? x >= 12 : x <= 3)) return TextureGen.Hex(0xB000E0);
            return Scales(f, x, y, w, h);
        });
        Box(176, 44, 12, 5, 16, (f, x, y, w, h) => f == 1 ? V(0x3A1F1F) : Scales(f, x, y, w, h));   // górna szczęka
        Box(176, 65, 12, 4, 16, (f, x, y, w, h) => f == 0 ? V(0x3A1F1F) : Scales(f, x, y, w, h));   // żuchwa
        Box(112, 0, 18, 6, 24, Scales);              // tylna stopa (i nozdrza)
        Box(196, 0, 12, 32, 12, Scales);             // tylny piszczel
        Box(112, 104, 8, 24, 8, Scales);             // przednie ramię
        Box(226, 138, 6, 24, 6, Scales);             // przednie przedramię
        Box(144, 104, 8, 4, 16, Scales);             // przednia stopa
        Box(112, 88, 56, 8, 8, (f, x, y, w, h) => V(0x303030, .12f));   // kość skrzydła
        Box(112, 136, 56, 4, 4, (f, x, y, w, h) => V(0x303030, .12f));  // kość końcówki
        // błony skrzydeł: szare z ciemniejszymi żyłkami rozchodzącymi się od kości
        for (int y = 88; y < 200; y++) for (int x = 0; x < 112; x++)
        {
            if (y >= 144 && y < 200 || y < 144)
            {
                int lx = x % 56, ly = (y - 88) % 56;
                bool vein = (lx + ly * 2) % 14 == 0 || ly % 18 == 17;
                Set(x, y, V(vein ? 0x2A2A2Au : 0x3E3E3Eu, .1f));
            }
        }
        // biała łata na promienie i wiązkę kryształu
        for (int y = 240; y < 256; y++) for (int x = 0; x < 16; x++) Set(x, y, TextureGen.Hex(0xFFFFFF));
        return p;
    }
}
