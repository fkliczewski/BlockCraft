using BlockCraft.Entities;
using BlockCraft.Items;
using OpenTK.Mathematics;

namespace BlockCraft.Render;

/// <summary>Modele mobów Netheru i Endu odwzorowane z klas Model* z MC 1.8 (współrzędne w pikselach modelu).</summary>
public sealed partial class EntityRenderer
{
    private ModelPart _ghBody; private ModelPart[] _ghTentacles;
    private ModelPart _blHead; private ModelPart[] _blRods;
    private ModelPart[] _mcSegments; private ModelPart _mcCore;
    private ModelPart[] _emParts;
    private static readonly int[][] EmSize = { new[] { 4, 3, 2 }, new[] { 6, 4, 5 }, new[] { 3, 3, 1 }, new[] { 1, 2, 1 } };
    private static readonly int[][] EmUv = { new[] { 0, 0 }, new[] { 0, 5 }, new[] { 0, 14 }, new[] { 0, 18 } };

    /// <summary>java.util.Random (LCG 48-bit) - długości macek ghasta z ziarna 1660 jak w ModelGhast.</summary>
    private sealed class JavaRandom
    {
        private long _seed;
        public JavaRandom(long seed) { _seed = (seed ^ 0x5DEECE66DL) & ((1L << 48) - 1); }
        private int Next(int bits) { _seed = (_seed * 0x5DEECE66DL + 0xBL) & ((1L << 48) - 1); return (int)((ulong)_seed >> (48 - bits)); }
        public int NextInt(int bound)
        {
            if ((bound & -bound) == bound) return (int)((bound * (long)Next(31)) >> 31);
            int bits, val;
            do { bits = Next(31); val = bits % bound; } while (bits - val + (bound - 1) < 0);
            return val;
        }
    }

    private void InitNetherModels()
    {
        if (_ghBody != null) return;
        // ModelGhast: korpus 16x16x16, 9 macek 2xNx2 (N = 8..14)
        _ghBody = P(0, 8, 0).Box(0, 0, -8, -8, -8, 16, 16, 16);
        var rand = new JavaRandom(1660);
        _ghTentacles = new ModelPart[9];
        for (int i = 0; i < 9; i++)
        {
            float x = ((i % 3 - i / 3 % 2 * .5f + .25f) / 2f * 2f - 1f) * 5f, z = (i / 3 / 2f * 2f - 1f) * 5f;
            int len = rand.NextInt(7) + 8;
            _ghTentacles[i] = P(x, 15, z).Box(0, 0, -1, 0, -1, 2, len, 2);
        }
        // ModelBlaze: głowa 8x8x8, 12 prętów 2x8x2
        _blHead = P(0, 0, 0).Box(0, 0, -4, -4, -4, 8, 8, 8);
        _blRods = new ModelPart[12];
        for (int i = 0; i < 12; i++) _blRods[i] = P(0, 0, 0).Box(0, 16, 0, 0, 0, 2, 8, 2);
        // ModelMagmaCube: 8 plasterków 8x1x8 i rdzeń 4x4x4
        _mcSegments = new ModelPart[8];
        for (int i = 0; i < 8; i++)
        {
            int u = 0, v = i;
            if (i == 2) { u = 24; v = 10; } else if (i == 3) { u = 24; v = 19; }
            _mcSegments[i] = P(0, 0, 0).Box(u, v, -4, 16 + i, -4, 8, 1, 8);
        }
        _mcCore = P(0, 0, 0).Box(0, 16, -2, 18, -2, 4, 4, 4);
        // ModelEnderMite: 4 segmenty
        _emParts = new ModelPart[4];
        float pz = -3.5f;
        for (int i = 0; i < 4; i++)
        {
            _emParts[i] = P(0, 24 - EmSize[i][1], pz).Box(EmUv[i][0], EmUv[i][1], EmSize[i][0] * -.5f, 0, EmSize[i][2] * -.5f, EmSize[i][0], EmSize[i][1], EmSize[i][2]);
            if (i < 3) pz += (EmSize[i][2] + EmSize[i + 1][2]) * .5f;
        }
    }

    /// <summary>Korzeń modelu ze skalą preRenderCallback (jak RendererLivingEntity: przewrót po śmierci, skala, -1.5078125).</summary>
    public static Matrix4 ScaledRoot(LivingEntity e, LivingPose pose, float sx, float sy, float sz)
    {
        var m = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(sx, sy, sz) * Matrix4.CreateScale(-1, -1, 1);
        if (e.DeathTime > 0)
        {
            float f = MathF.Sqrt(Math.Max(0, (e.DeathTime + pose.Pt - 1) / 20f * 1.6f));
            m *= Matrix4.CreateRotationZ(MathHelper.DegreesToRadians(Math.Min(1, f) * 90f));
        }
        return m * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(180f - pose.BodyYaw)) * Matrix4.CreateTranslation(pose.Pos);
    }

    private void RenderGhast(Ghast g, LivingPose pose)
    {
        InitNetherModels();
        Skin.Layer = SkinGen.Get(g.Attacking ? "ghast_shooting" : "ghast");
        Skin.Blk = Math.Max(Skin.Blk, 15);
        // ModelGhast.render przesuwa model o 0.6 w górę układu modelu
        var root = Matrix4.CreateTranslation(0, .6f, 0) * ScaledRoot(g, pose, 4.5f, 4.5f, 4.5f);
        RenderPart(Skin, _ghBody, root);
        for (int i = 0; i < 9; i++) { _ghTentacles[i].RX = .2f * MathF.Sin(pose.Age * .3f + i) + .4f; RenderPart(Skin, _ghTentacles[i], root); }
    }

    private void RenderBlaze(Blaze b, LivingPose pose)
    {
        InitNetherModels();
        Skin.Layer = SkinGen.Get("blaze");
        Skin.Blk = 15;
        float age = pose.Age, f = age * MathF.PI * -.1f;
        for (int i = 0; i < 4; i++) { _blRods[i].Reset(MathF.Cos(f) * 9f, -2f + MathF.Cos((i * 2 + age) * .25f), MathF.Sin(f) * 9f); f++; }
        f = MathF.PI / 4f + age * MathF.PI * .03f;
        for (int i = 4; i < 8; i++) { _blRods[i].Reset(MathF.Cos(f) * 7f, 2f + MathF.Cos((i * 2 + age) * .25f), MathF.Sin(f) * 7f); f++; }
        f = .47123894f + age * MathF.PI * -.05f;
        for (int i = 8; i < 12; i++) { _blRods[i].Reset(MathF.Cos(f) * 5f, 11f + MathF.Cos((i * 1.5f + age) * .5f), MathF.Sin(f) * 5f); f++; }
        _blHead.RY = MathHelper.DegreesToRadians(pose.HeadYaw); _blHead.RX = MathHelper.DegreesToRadians(pose.Pitch);
        RenderPart(Skin, _blHead, pose.Root);
        foreach (var r in _blRods) RenderPart(Skin, r, pose.Root);
    }

    private void RenderMagmaCube(MagmaCube m, LivingPose pose)
    {
        InitNetherModels();
        Skin.Layer = SkinGen.Get("magma_cube");
        Skin.Blk = 15;
        float squish = m.PrevSquishFactor + (m.SquishFactor - m.PrevSquishFactor) * pose.Pt;
        // RenderMagmaCube.preRenderCallback
        float f1 = squish / (m.Size * .5f + 1f), f2 = 1f / (f1 + 1f);
        var root = ScaledRoot(m, pose, f2 * m.Size, 1f / f2 * m.Size, f2 * m.Size);
        float s = Math.Max(0, squish);
        for (int i = 0; i < 8; i++) { _mcSegments[i].PY = -(4 - i) * s * 1.7f; RenderPart(Skin, _mcSegments[i], root); }
        RenderPart(Skin, _mcCore, root);
    }

    private void RenderPigman(ZombiePigman z, LivingPose pose)
    {
        Skin.Layer = SkinGen.Get("zombie_pigman");
        Biped(z, pose.Pt, pose.Limb, pose.LimbAmt, pose.Age, pose.HeadYaw, pose.Pitch, _head, _body, _rArm, _lArm, _rLeg, _lLeg, true, 4);
        foreach (var part in new[] { _head, _body, _rArm, _lArm, _rLeg, _lLeg }) RenderPart(Skin, part, pose.Root);
        if (z.HeldItem != null) RenderHeld(z.HeldItem, _rArm, pose.Root);
    }

    private void RenderWitherSkeleton(WitherSkeleton w, LivingPose pose)
    {
        Skin.Layer = SkinGen.Get("wither_skeleton");
        var root = ScaledRoot(w, pose, 1.2f, 1.2f, 1.2f);
        Biped(w, pose.Pt, pose.Limb, pose.LimbAmt, pose.Age, pose.HeadYaw, pose.Pitch, _head, _body, _sRArm, _sLArm, _sRLeg, _sLLeg, false, 2);
        foreach (var part in new[] { _head, _body, _sRArm, _sLArm, _sRLeg, _sLLeg }) RenderPart(Skin, part, root);
        if (w.HeldItem != null) RenderHeld(w.HeldItem, _sRArm, root);
    }

    private void RenderEndermite(Endermite e, LivingPose pose)
    {
        InitNetherModels();
        Skin.Layer = SkinGen.Get("endermite");
        for (int i = 0; i < 4; i++)
        {
            float a = pose.Age * .9f + i * .15f * MathF.PI;
            _emParts[i].RY = MathF.Cos(a) * MathF.PI * .01f * (1 + Math.Abs(i - 2));
            _emParts[i].PX = MathF.Sin(a) * MathF.PI * .1f * Math.Abs(i - 2);
            RenderPart(Skin, _emParts[i], pose.Root);
        }
    }

    public static void RegisterNetherRenderers()
    {
        CustomLiving[typeof(Ghast)] = (r, e, p) => r.RenderGhast((Ghast)e, p);
        CustomLiving[typeof(Blaze)] = (r, e, p) => r.RenderBlaze((Blaze)e, p);
        CustomLiving[typeof(MagmaCube)] = (r, e, p) => r.RenderMagmaCube((MagmaCube)e, p);
        CustomLiving[typeof(ZombiePigman)] = (r, e, p) => r.RenderPigman((ZombiePigman)e, p);
        CustomLiving[typeof(WitherSkeleton)] = (r, e, p) => r.RenderWitherSkeleton((WitherSkeleton)e, p);
        CustomLiving[typeof(Endermite)] = (r, e, p) => r.RenderEndermite((Endermite)e, p);
        Custom[typeof(LargeFireballEntity)] = (r, e, pt, pos, cam) => { r.Skin.Blk = r.Terrain.Blk = 15; r.RenderBillboardSprite(TextureGen.Layer("item_fire_charge"), pos + new Vector3(0, .5f, 0), 1f, Vector4.One, cam); };
        Custom[typeof(SmallFireballEntity)] = (r, e, pt, pos, cam) => { r.Skin.Blk = r.Terrain.Blk = 15; r.RenderBillboardSprite(TextureGen.Layer("item_fire_charge"), pos + new Vector3(0, .15f, 0), .5f, Vector4.One, cam); };
    }
}
