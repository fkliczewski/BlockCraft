using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft.Entities
{
    public static partial class Registries
    {
        static partial void RegisterMobRenderers() => Render.EntityRenderer.RegisterMobModels();
    }
}

namespace BlockCraft.Render
{
    /// <summary>Modele mobów modułu MO: boxy, UV i animacje jak ModelBase w MC 1.8.</summary>
    public sealed partial class EntityRenderer
    {
        public static void RegisterMobModels()
        {
            CustomLiving[typeof(Wolf)] = (r, e, p) => r.RenderWolf((Wolf)e, p);
            CustomLiving[typeof(Ocelot)] = (r, e, p) => r.RenderOcelot((Ocelot)e, p);
            CustomLiving[typeof(Rabbit)] = (r, e, p) => r.RenderRabbit((Rabbit)e, p);
            CustomLiving[typeof(Bat)] = (r, e, p) => r.RenderBat((Bat)e, p);
            CustomLiving[typeof(Squid)] = (r, e, p) => r.RenderSquid((Squid)e, p);
            CustomLiving[typeof(Mooshroom)] = (r, e, p) => r.RenderMooshroom((Mooshroom)e, p);
            CustomLiving[typeof(IronGolem)] = (r, e, p) => r.RenderIronGolem((IronGolem)e, p);
            CustomLiving[typeof(SnowGolem)] = (r, e, p) => r.RenderSnowGolem((SnowGolem)e, p);
            CustomLiving[typeof(Horse)] = (r, e, p) => r.RenderHorse((Horse)e, p);
            CustomLiving[typeof(Villager)] = (r, e, p) => r.RenderVillager(e, p, VillagerSkin(((Villager)e).Profession), false);
            CustomLiving[typeof(Witch)] = (r, e, p) => r.RenderVillager(e, p, "witch", true);
            CustomLiving[typeof(Guardian)] = (r, e, p) => r.RenderGuardian((Guardian)e, p);
            CustomLiving[typeof(Slime)] = (r, e, p) => r.RenderSlime((Slime)e, p);
            CustomLiving[typeof(Silverfish)] = (r, e, p) => r.RenderSilverfish(e, p);
            CustomLiving[typeof(CaveSpider)] = (r, e, p) => r.RenderSpider(e, p, "cave_spider", .7f);
            CustomLiving[typeof(Giant)] = (r, e, p) => r.RenderZombie(e, p, 6f, false, false);
            CustomLiving[typeof(Zombie)] = (r, e, p) => { var z = (Zombie)e; r.RenderZombie(e, p, 1f, z.Baby, z.Villager); };
            CustomLiving[typeof(Creeper)] = (r, e, p) => r.RenderCreeper((Creeper)e, p);
            Custom[typeof(LeashKnot)] = (r, e, pt, pos, cam) => r.RenderLeashKnot(pos);
            Custom[typeof(GolemSnowball)] = (r, e, pt, pos, cam) => r.RenderBillboardSprite(TextureGen.Layer("item_snowball"), pos + new Vector3(0, .1f, 0), .25f, Vector4.One, cam);
            Custom[typeof(WitchPotion)] = (r, e, pt, pos, cam) => r.RenderBillboardSprite(TextureGen.Layer("item_potion"), pos + new Vector3(0, .1f, 0), .25f, new Vector4(.9f, .4f, .9f, 1), cam);
        }

        private static string VillagerSkin(int prof) => prof switch { 1 => "villager_librarian", 2 => "villager_priest", 3 => "villager_smith", 4 => "villager_butcher", _ => "villager_farmer" };
        private static float Rad(float deg) => MathHelper.DegreesToRadians(deg);

        /// <summary>Jak RendererLivingEntity: T(0,-1.5) S(-s) [przesunięcie] [obrót śmierci] RotY T(pos).</summary>
        private static Matrix4 MobRoot(LivingEntity e, LivingPose p, float sx, float sy, float sz, float dy = 0, float deathRot = 90f)
        {
            var m = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-sx, -sy, sz);
            if (dy != 0) m *= Matrix4.CreateTranslation(0, dy, 0);
            if (e.DeathTime > 0)
            {
                float f = MathF.Sqrt(Math.Max(0, (e.DeathTime + p.Pt - 1) / 20f * 1.6f));
                m *= Matrix4.CreateRotationZ(Rad(Math.Min(1, f) * deathRot));
            }
            return m * Matrix4.CreateRotationY(Rad(180 - p.BodyYaw)) * Matrix4.CreateTranslation(p.Pos);
        }
        private static Matrix4 MobRoot(LivingEntity e, LivingPose p, float s) => MobRoot(e, p, s, s, s);
        private void Parts(Matrix4 root, int tw, int th, params ModelPart[] parts) { foreach (var x in parts) RenderPart(Skin, x, root, tw, th); }
        private void Parts(Matrix4 root, params ModelPart[] parts) => Parts(root, 64, 64, parts);

        /// <summary>EntitySheep.DYE_TO_RGB z 1.8 (owce i obroże wilków).</summary>
        public static readonly Vector3[] FleeceRgb =
        {
            new(1f,1f,1f), new(.85f,.5f,.2f), new(.7f,.3f,.85f), new(.4f,.6f,.85f), new(.9f,.9f,.2f), new(.5f,.8f,.1f), new(.95f,.5f,.65f), new(.3f,.3f,.3f),
            new(.6f,.6f,.6f), new(.3f,.5f,.6f), new(.5f,.25f,.7f), new(.2f,.3f,.7f), new(.4f,.3f,.2f), new(.4f,.5f,.2f), new(.6f,.2f,.2f), new(.1f,.1f,.1f)
        };
        private static Vector4 Fleece(int c) { var v = FleeceRgb[c & 15]; return new Vector4(v.X, v.Y, v.Z, 1); }

        /// <summary>Drugi przebieg z przezroczystością (szlam, ładunek creepera): w świecie osobny flush z mieszaniem kolorów.</summary>
        private void Translucent(LivingPose p, Action draw)
        {
            if (p.ForGui) { draw(); return; }
            Flush(Skin, SkinTex);
            GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); GL.DepthMask(false);
            draw(); Flush(Skin, SkinTex);
            GL.DepthMask(true); GL.Disable(EnableCap.Blend);
        }

        // ===================== wilk (ModelWolf) =====================
        private readonly ModelPart _wHead = P(-1, 13.5f, -7).Box(0, 0, -3, -3, -2, 6, 6, 4).Box(16, 14, -3, -5, 0, 2, 2, 1).Box(16, 14, 1, -5, 0, 2, 2, 1).Box(0, 10, -1.5f, 0, -5, 3, 3, 4);
        private readonly ModelPart _wBody = P(0, 14, 2).Box(18, 14, -4, -2, -3, 6, 9, 6);
        private readonly ModelPart _wMane = P(-1, 14, 2).Box(21, 0, -4, -3, -3, 8, 6, 7);
        private readonly ModelPart _wCollar = P(-1, 14, 2).Box(21, 0, -4, -3, -3, 8, 6, 7, .05f);
        private readonly ModelPart _wTail = P(-1, 12, 8).Box(9, 18, 0, 0, -1, 2, 8, 2);
        private readonly ModelPart[] _wLegs = { P(-2.5f, 16, 7).Box(0, 18, 0, 0, -1, 2, 8, 2), P(.5f, 16, 7).Box(0, 18, 0, 0, -1, 2, 8, 2), P(-2.5f, 16, -4).Box(0, 18, 0, 0, -1, 2, 8, 2), P(.5f, 16, -4).Box(0, 18, 0, 0, -1, 2, 8, 2) };

        private void RenderWolf(Wolf w, LivingPose p)
        {
            var root = MobRoot(w, p, w.IsBaby ? .5f : 1f);
            Skin.Layer = SkinGen.Get(w.Angry ? "wolf_angry" : w.Tamed ? "wolf_tame" : "wolf");
            _wTail.RY = w.Angry ? 0 : MathF.Cos(p.Limb * .6662f) * 1.4f * p.LimbAmt;
            if (w.Sitting)
            {
                _wMane.Reset(-1, 16, -3); _wMane.RX = MathF.PI * 2 / 5; _wBody.Reset(0, 18, 0); _wBody.RX = MathF.PI / 4; _wTail.PX = -1; _wTail.PY = 21; _wTail.PZ = 6;
                _wLegs[0].Reset(-2.5f, 22, 2); _wLegs[0].RX = MathF.PI * 1.5f; _wLegs[1].Reset(.5f, 22, 2); _wLegs[1].RX = MathF.PI * 1.5f;
                _wLegs[2].Reset(-2.49f, 17, -4); _wLegs[2].RX = 5.811947f; _wLegs[3].Reset(.51f, 17, -4); _wLegs[3].RX = 5.811947f;
            }
            else
            {
                _wBody.Reset(0, 14, 2); _wBody.RX = MathF.PI / 2; _wMane.Reset(-1, 14, -3); _wMane.RX = _wBody.RX; _wTail.PX = -1; _wTail.PY = 12; _wTail.PZ = 8;
                float[] lx = { -2.5f, .5f, -2.5f, .5f }, lz = { 7, 7, -4, -4 };
                for (int i = 0; i < 4; i++) { _wLegs[i].Reset(lx[i], 16, lz[i]); _wLegs[i].RX = MathF.Cos(p.Limb * .6662f + (i is 1 or 2 ? MathF.PI : 0)) * 1.4f * p.LimbAmt; }
            }
            _wHead.RX = Rad(p.Pitch); _wHead.RY = Rad(p.HeadYaw); _wHead.RZ = w.Begging ? .15f * MathF.PI : 0;
            _wTail.RX = w.TailRotation;
            Parts(root, _wHead, _wBody, _wMane, _wTail, _wLegs[0], _wLegs[1], _wLegs[2], _wLegs[3]);
            if (w.Tamed)
            {
                _wCollar.PX = _wMane.PX; _wCollar.PY = _wMane.PY; _wCollar.PZ = _wMane.PZ; _wCollar.RX = _wMane.RX;
                Skin.Layer = SkinGen.Get("wolf_collar"); Skin.Color = Fleece(w.Collar);
                Parts(root, _wCollar); Skin.Color = Vector4.One;
            }
        }

        // ===================== ocelot (ModelOcelot) =====================
        private readonly ModelPart _oHead = P(0, 15, -9).Box(0, 0, -2.5f, -2, -3, 5, 4, 5).Box(0, 24, -1.5f, 0, -4, 3, 2, 2).Box(0, 10, -2, -3, 0, 1, 1, 2).Box(6, 10, 1, -3, 0, 1, 1, 2);
        private readonly ModelPart _oBody = P(0, 12, -10).Box(20, 0, -2, 3, -8, 4, 16, 6);
        private readonly ModelPart _oTail = P(0, 15, 8).Box(0, 15, -.5f, 0, 0, 1, 8, 1), _oTail2 = P(0, 20, 14).Box(4, 15, -.5f, 0, 0, 1, 8, 1);
        private readonly ModelPart _oBL = P(1.1f, 18, 5).Box(8, 13, -1, 0, 1, 2, 6, 2), _oBR = P(-1.1f, 18, 5).Box(8, 13, -1, 0, 1, 2, 6, 2);
        private readonly ModelPart _oFL = P(1.2f, 13.8f, -5).Box(40, 0, -1, 0, 0, 2, 10, 2), _oFR = P(-1.2f, 13.8f, -5).Box(40, 0, -1, 0, 0, 2, 10, 2);

        private void RenderOcelot(Ocelot o, LivingPose p)
        {
            float s = (o.IsBaby ? .5f : 1f) * (o.Tamed ? .8f : 1f);
            var root = MobRoot(o, p, s);
            Skin.Layer = SkinGen.Get(o.CatType switch { 1 => "cat_black", 2 => "cat_red", 3 => "cat_siamese", _ => "ocelot" });
            _oBody.Reset(0, 12, -10); _oBody.RX = MathF.PI / 2; _oHead.Reset(0, 15, -9);
            _oTail.Reset(0, 15, 8); _oTail.RX = .9f; _oTail2.Reset(0, 20, 14);
            _oFL.Reset(1.2f, 13.8f, -5); _oFR.Reset(-1.2f, 13.8f, -5); _oBL.Reset(1.1f, 18, 5); _oBR.Reset(-1.1f, 18, 5);
            if (o.Sitting)
            {
                _oBody.RX = MathF.PI / 4; _oBody.PY += -4; _oBody.PZ += 5; _oHead.PY += -3.3f; _oHead.PZ += 1;
                _oTail.PY += 8; _oTail.PZ += -2; _oTail2.PY += 2; _oTail2.PZ += -.8f; _oTail.RX = 1.7278761f; _oTail2.RX = 2.670354f;
                _oFL.RX = _oFR.RX = -.15707964f; _oFL.PY = _oFR.PY = 15.8f; _oFL.PZ = _oFR.PZ = -7;
                _oBL.RX = _oBR.RX = -MathF.PI / 2; _oBL.PY = _oBR.PY = 21; _oBL.PZ = _oBR.PZ = 1;
            }
            else
            {
                float l = p.Limb * .6662f;
                _oBL.RX = MathF.Cos(l) * p.LimbAmt; _oBR.RX = MathF.Cos(l + MathF.PI) * p.LimbAmt;
                _oFL.RX = MathF.Cos(l + MathF.PI) * p.LimbAmt; _oFR.RX = MathF.Cos(l) * p.LimbAmt;
                _oTail2.RX = 1.7278761f + MathF.PI / 4 * MathF.Cos(p.Limb) * p.LimbAmt;
            }
            _oHead.RX = Rad(p.Pitch); _oHead.RY = Rad(p.HeadYaw);
            Parts(root, _oHead, _oBody, _oTail, _oTail2, _oBL, _oBR, _oFL, _oFR);
        }

        // ===================== królik (ModelRabbit) =====================
        private readonly ModelPart _rLFoot = P(3, 17.5f, 3.7f).Box(26, 24, -1, 5.5f, -3.7f, 2, 1, 7, 0, true), _rRFoot = P(-3, 17.5f, 3.7f).Box(8, 24, -1, 5.5f, -3.7f, 2, 1, 7);
        private readonly ModelPart _rLThigh = P(3, 17.5f, 3.7f).Box(30, 15, -1, 0, 0, 2, 4, 5, 0, true), _rRThigh = P(-3, 17.5f, 3.7f).Box(16, 15, -1, 0, 0, 2, 4, 5);
        private readonly ModelPart _rBody = P(0, 19, 8).Box(0, 0, -3, -2, -10, 6, 5, 10);
        private readonly ModelPart _rLArm = P(3, 17, -1).Box(8, 15, -1, 0, -1, 2, 7, 2, 0, true), _rRArm = P(-3, 17, -1).Box(0, 15, -1, 0, -1, 2, 7, 2);
        private readonly ModelPart _rHead = P(0, 16, -1).Box(32, 0, -2.5f, -4, -5, 5, 4, 5).Box(32, 9, -.5f, -2.5f, -5.5f, 1, 1, 1);
        private readonly ModelPart _rREar = P(0, 16, -1).Box(52, 0, -2.5f, -9, -1, 2, 5, 1), _rLEar = P(0, 16, -1).Box(58, 0, .5f, -9, -1, 2, 5, 1, 0, true);
        private readonly ModelPart _rTail = P(0, 20, 7).Box(52, 6, -1.5f, -1.5f, 0, 3, 3, 2);

        private void RenderRabbit(Rabbit rb, LivingPose p)
        {
            Skin.Layer = SkinGen.Get(rb.RabbitType switch { 1 => "rabbit_white", 2 => "rabbit_black", 3 => "rabbit_black_white", 4 => "rabbit_gold", 5 => "rabbit_salt", 99 => "rabbit_caerbannog", _ => "rabbit_brown" });
            float hy = Rad(p.HeadYaw), hp = Rad(p.Pitch);
            _rHead.RX = _rREar.RX = _rLEar.RX = hp; _rHead.RY = hy; _rREar.RY = hy - .2617994f; _rLEar.RY = hy + .2617994f;
            _rBody.RX = -.34906584f; _rTail.RX = -.3490659f;
            float j = MathF.Sin(rb.JumpAnim * MathF.PI);
            _rLThigh.RX = _rRThigh.RX = Rad(j * 50 - 21); _rLFoot.RX = _rRFoot.RX = Rad(j * 50); _rLArm.RX = _rRArm.RX = Rad(j * -40 - 11);
            var b = MobRoot(rb, p, 1f);
            if (rb.IsBaby)
            {
                Parts(Matrix4.CreateTranslation(0, 5 / 16f, 2 / 16f) * Matrix4.CreateScale(.5f * 1.4f) * b, _rHead, _rREar, _rLEar);
                Parts(Matrix4.CreateTranslation(0, 24 / 16f, 0) * Matrix4.CreateScale(.5f) * b, _rLFoot, _rRFoot, _rLThigh, _rRThigh, _rBody, _rLArm, _rRArm, _rTail);
            }
            else Parts(Matrix4.CreateTranslation(0, 1f, 0) * Matrix4.CreateScale(.6f) * b, _rLFoot, _rRFoot, _rLThigh, _rRThigh, _rBody, _rLArm, _rRArm, _rHead, _rREar, _rLEar, _rTail);
        }

        // ===================== nietoperz (ModelBat) =====================
        private ModelPart _bHead, _bBody, _bRWing, _bLWing, _bRTip, _bLTip;
        private void InitBat()
        {
            if (_bHead != null) return;
            _bHead = P(0, 0, 0).Box(0, 0, -3, -3, -3, 6, 6, 6);
            _bHead.Children.Add(P(0, 0, 0).Box(24, 0, -4, -6, -2, 3, 4, 1)); _bHead.Children.Add(P(0, 0, 0).Box(24, 0, 1, -6, -2, 3, 4, 1, 0, true));
            _bBody = P(0, 0, 0).Box(0, 16, -3, 4, -3, 6, 12, 6).Box(0, 34, -5, 16, 0, 10, 6, 1);
            _bRWing = P(0, 0, 0).Box(42, 0, -12, 1, 1.5f, 10, 16, 1); _bRTip = P(-12, 1, 1.5f).Box(24, 16, -8, 1, 0, 8, 12, 1);
            _bLWing = P(0, 0, 0).Box(42, 0, 2, 1, 1.5f, 10, 16, 1, 0, true); _bLTip = P(12, 1, 1.5f).Box(24, 16, 0, 1, 0, 8, 12, 1, 0, true);
            _bRWing.Children.Add(_bRTip); _bLWing.Children.Add(_bLTip); _bBody.Children.Add(_bRWing); _bBody.Children.Add(_bLWing);
        }
        private void RenderBat(Bat b, LivingPose p)
        {
            InitBat();
            Skin.Layer = SkinGen.Get("bat");
            float hy = Rad(p.HeadYaw), hp = Rad(p.Pitch);
            if (b.Hanging)
            {
                _bHead.RX = hp; _bHead.RY = MathF.PI - hy; _bHead.RZ = MathF.PI; _bHead.Reset(0, -2, 0); _bHead.RX = hp; _bHead.RY = MathF.PI - hy; _bHead.RZ = MathF.PI;
                _bRWing.Reset(-3, 0, 3); _bLWing.Reset(3, 0, 3);
                _bBody.RX = MathF.PI; _bBody.RY = 0;
                _bRWing.RX = -MathF.PI * .05f; _bRWing.RY = -MathF.PI * .4f; _bRTip.RY = -MathF.PI * .55f;
                _bLWing.RX = _bRWing.RX; _bLWing.RY = -_bRWing.RY; _bLTip.RY = -_bRTip.RY;
            }
            else
            {
                _bHead.Reset(0, 0, 0); _bHead.RX = hp; _bHead.RY = hy;
                _bRWing.Reset(0, 0, 0); _bLWing.Reset(0, 0, 0);
                _bBody.RX = MathF.PI / 4 + MathF.Cos(p.Age * .1f) * .15f; _bBody.RY = 0;
                _bRWing.RY = MathF.Cos(p.Age * 1.3f) * MathF.PI * .25f; _bLWing.RY = -_bRWing.RY;
                _bRTip.RY = _bRWing.RY * .5f; _bLTip.RY = -_bRWing.RY * .5f;
            }
            float dy = b.Hanging ? -.1f : MathF.Cos(p.Age * .3f) * .1f;
            Parts(MobRoot(b, p, .35f, .35f, .35f, dy), _bHead, _bBody);
        }

        // ===================== kałamarnica (ModelSquid) =====================
        private readonly ModelPart _sqBody = P(0, 8, 0).Box(0, 0, -6, -8, -6, 12, 16, 12);
        private ModelPart[] _sqLegs;
        private void RenderSquid(Squid s, LivingPose p)
        {
            if (_sqLegs == null)
            {
                _sqLegs = new ModelPart[8];
                for (int j = 0; j < 8; j++)
                {
                    double a = j * Math.PI * 2 / 8;
                    _sqLegs[j] = P((float)Math.Cos(a) * 5, 15, (float)Math.Sin(a) * 5).Box(48, 0, -1, 0, -1, 2, 18, 2);
                    _sqLegs[j].RY = (float)(j * Math.PI * -2 / 8 + Math.PI / 2);
                }
            }
            Skin.Layer = SkinGen.Get("squid");
            float t = MathHelper.Lerp(s.PrevTentacle, s.Tentacle, p.Pt);
            foreach (var l in _sqLegs) l.RX = t;
            float pitch = MathHelper.Lerp(s.PrevBodyPitch, s.BodyPitch, p.Pt), roll = MathHelper.Lerp(s.PrevBodyRoll, s.BodyRoll, p.Pt);
            var m = Matrix4.CreateTranslation(0, -1.5078125f, 0) * Matrix4.CreateScale(-1, -1, 1) * Matrix4.CreateTranslation(0, -1.2f, 0)
                    * Matrix4.CreateRotationY(roll) * Matrix4.CreateRotationX(Rad(pitch)) * Matrix4.CreateTranslation(0, .5f, 0);
            if (s.DeathTime > 0) m *= Matrix4.CreateRotationZ(Rad(Math.Min(1, MathF.Sqrt(Math.Max(0, (s.DeathTime + p.Pt - 1) / 20f * 1.6f))) * 90));
            m *= Matrix4.CreateRotationY(Rad(180 - p.BodyYaw)) * Matrix4.CreateTranslation(p.Pos);
            RenderPart(Skin, _sqBody, m); foreach (var l in _sqLegs) RenderPart(Skin, l, m);
        }

        // ===================== grzybowa krowa =====================
        private void RenderMooshroom(Mooshroom c, LivingPose p)
        {
            float sc = c.IsBaby ? .5f : 1f;
            var root = MobRoot(c, p, sc);
            Skin.Layer = SkinGen.Get("mooshroom");
            _cowHead.RY = Rad(p.HeadYaw); _cowHead.RX = Rad(p.Pitch); _cowBody.RX = MathF.PI / 2; Quad4(_cowLegs, p.Limb, p.LimbAmt);
            RenderPart(Skin, _cowHead, root); RenderPart(Skin, _cowBody, root); foreach (var l in _cowLegs) RenderPart(Skin, l, root);
            if (c.IsBaby || Block.Get(B.RedMushroom).Id != B.RedMushroom) return;
            // LayerMooshroomMushroom: dwa grzyby na grzbiecie i jeden na głowie (pełne modele bloków)
            var flip = Matrix4.CreateTranslation(0, .5f, 0) * Matrix4.CreateScale(1, -1, 1);
            RenderBlockAt(B.RedMushroom, 0, flip * Matrix4.CreateRotationY(Rad(42)) * Matrix4.CreateTranslation(.2f, 2 / 16f, .4f) * root);
            RenderBlockAt(B.RedMushroom, 0, flip * Matrix4.CreateRotationY(Rad(84)) * Matrix4.CreateTranslation(-.15f, 2 / 16f, -.2f) * root);
            RenderBlockAt(B.RedMushroom, 0, flip * Matrix4.CreateRotationY(Rad(12)) * Matrix4.CreateTranslation(0, -4 / 16f, -3 / 16f) * PartMatrix(_cowHead, root));
        }

        // ===================== golem żelazny (ModelIronGolem, 128x128) =====================
        private readonly ModelPart _igHead = P(0, -7, -2).Box(24, 0, -1, -5.5f, -7.5f, 2, 4, 2).Box(0, 0, -4, -12, -5.5f, 8, 10, 8);
        private readonly ModelPart _igBody = P(0, -7, 0).Box(0, 40, -9, -2, -6, 18, 12, 11).Box(0, 70, -4.5f, 10, -3, 9, 5, 6, .5f);
        private readonly ModelPart _igRArm = P(0, -7, 0).Box(60, 21, -13, -2.5f, -3, 4, 30, 6), _igLArm = P(0, -7, 0).Box(60, 58, 9, -2.5f, -3, 4, 30, 6);
        private readonly ModelPart _igRLeg = P(-4, 11, 0).Box(37, 0, -3.5f, -3, -3, 6, 16, 5), _igLLeg = P(5, 11, 0).Box(60, 0, -3.5f, -3, -3, 6, 16, 5, 0, true);
        private static float Tri(float a, float b) => (MathF.Abs(a % b - b * .5f) - b * .25f) / (b * .25f);
        private void RenderIronGolem(IronGolem g, LivingPose p)
        {
            Skin.Layer = SkinGen.Get("iron_golem");
            _igHead.RY = Rad(p.HeadYaw); _igHead.RX = Rad(p.Pitch);
            _igRLeg.RX = -1.5f * Tri(p.Limb, 13) * p.LimbAmt; _igLLeg.RX = 1.5f * Tri(p.Limb, 13) * p.LimbAmt;
            if (g.AttackTicks > 0) { _igRArm.RX = _igLArm.RX = -2 + 1.5f * Tri(g.AttackTicks - p.Pt, 10); }
            else if (g.RoseTicks > 0) { _igRArm.RX = -.8f + .025f * Tri(g.RoseTicks, 70); _igLArm.RX = 0; }
            else { _igRArm.RX = (-.2f + 1.5f * Tri(p.Limb, 13)) * p.LimbAmt; _igLArm.RX = (-.2f - 1.5f * Tri(p.Limb, 13)) * p.LimbAmt; }
            // RenderIronGolem.rotateCorpse: kołysanie przy chodzie
            float sway = (MathF.Abs((p.Limb + 6) % 13 - 6.5f) - 3.25f) / 3.25f;
            var root = MobRoot(g, p, 1f) ;
            if (p.LimbAmt > .01f) root = Matrix4.CreateRotationZ(Rad(6.5f * sway * p.LimbAmt)) * root;
            Parts(root, 128, 128, _igHead, _igBody, _igRArm, _igLArm, _igRLeg, _igLLeg);
            if (g.RoseTicks > 0 && Block.Get(B.Poppy).Id == B.Poppy)
                RenderBlockAt(B.Poppy, 0, Matrix4.CreateScale(.5f) * Matrix4.CreateTranslation(-11 / 16f, 1.2f, -.3f) * Matrix4.CreateScale(1, -1, 1) * PartMatrix(_igRArm, root));
        }

        // ===================== golem śnieżny (ModelSnowMan) =====================
        private readonly ModelPart _snBody = P(0, 13, 0).Box(0, 16, -5, -10, -5, 10, 10, 10, -.5f), _snBottom = P(0, 24, 0).Box(0, 36, -6, -12, -6, 12, 12, 12, -.5f);
        private readonly ModelPart _snHead = P(0, 4, 0).Box(0, 0, -4, -8, -4, 8, 8, 8, -.5f);
        private readonly ModelPart _snRHand = P(0, 6, 0).Box(32, 0, -1, 0, -1, 12, 2, 2, -.5f), _snLHand = P(0, 6, 0).Box(32, 0, -1, 0, -1, 12, 2, 2, -.5f);
        private void RenderSnowGolem(SnowGolem g, LivingPose p)
        {
            Skin.Layer = SkinGen.Get("snow_golem");
            _snHead.RY = Rad(p.HeadYaw); _snHead.RX = Rad(p.Pitch);
            _snBody.RY = Rad(p.HeadYaw) * .25f;
            float s = MathF.Sin(_snBody.RY), c = MathF.Cos(_snBody.RY);
            _snRHand.RZ = 1; _snLHand.RZ = -1; _snRHand.RY = _snBody.RY; _snLHand.RY = MathF.PI + _snBody.RY;
            _snRHand.PX = c * 5; _snRHand.PZ = -s * 5; _snLHand.PX = -c * 5; _snLHand.PZ = s * 5;
            Parts(MobRoot(g, p, 1f), _snBody, _snBottom, _snHead, _snRHand, _snLHand);
        }

        // ===================== wieśniak i wiedźma (ModelVillager, ModelWitch) =====================
        private readonly ModelPart _vHead = P(0, 0, 0).Box(0, 0, -4, -10, -4, 8, 10, 8), _vNose = P(0, -2, 0).Box(24, 0, -1, -1, -6, 2, 4, 2);
        private readonly ModelPart _vBody = P(0, 0, 0).Box(16, 20, -4, 0, -3, 8, 12, 6).Box(0, 38, -4, 0, -3, 8, 18, 6, .5f);
        private readonly ModelPart _vArms = P(0, 2, 0).Box(44, 22, -8, -2, -2, 4, 8, 4).Box(44, 22, 4, -2, -2, 4, 8, 4, 0, true).Box(40, 38, -4, 2, -2, 8, 4, 4);
        private readonly ModelPart _vRLeg = P(-2, 12, 0).Box(0, 22, -2, 0, -2, 4, 12, 4), _vLLeg = P(2, 12, 0).Box(0, 22, -2, 0, -2, 4, 12, 4, 0, true);
        private ModelPart _witchHat, _witchMole;
        private void RenderVillager(LivingEntity e, LivingPose p, string skin, bool witch)
        {
            if (_vHead.Children.Count == 0) _vHead.Children.Add(_vNose);
            if (_witchHat == null)
            {
                _witchHat = P(-5, -10.03125f, -5).Box(0, 64, 0, 0, 0, 10, 2, 10);
                var h2 = P(1.75f, -4, 2).Box(0, 76, 0, 0, 0, 7, 4, 7); h2.RX = -.05235988f; h2.RZ = .02617994f;
                var h3 = P(1.75f, -4, 2).Box(0, 87, 0, 0, 0, 4, 4, 4); h3.RX = -.10471976f; h3.RZ = .05235988f;
                var h4 = P(1.75f, -2, 2).Box(0, 95, 0, 0, 0, 1, 2, 1, .25f); h4.RX = -.20943952f; h4.RZ = .10471976f;
                _witchHat.Children.Add(h2); h2.Children.Add(h3); h3.Children.Add(h4);
                _witchMole = P(0, -2, 0).Box(0, 0, 0, 3, -6.75f, 1, 1, 1, -.25f);
            }
            _vHead.Children.Remove(_witchHat); _vNose.Children.Remove(_witchMole);
            if (witch) { _vHead.Children.Add(_witchHat); _vNose.Children.Add(_witchMole); }
            Skin.Layer = SkinGen.Get(skin);
            _vHead.RY = Rad(p.HeadYaw); _vHead.RX = Rad(p.Pitch);
            _vHead.RZ = e is Villager v && v.HeadShaking ? .3f * MathF.Sin(.45f * p.Age) : 0;
            _vNose.Reset(0, -2, 0);
            if (e is Witch wi && wi.Drinking) { _vNose.PY = -2 + MathF.Sin(p.Age * .3f) * 1.2f; _vNose.RX = -.9f; }
            _vArms.Reset(0, 3, -1); _vArms.RX = -.75f;
            _vRLeg.RX = MathF.Cos(p.Limb * .6662f) * 1.4f * p.LimbAmt * .5f; _vLLeg.RX = MathF.Cos(p.Limb * .6662f + MathF.PI) * 1.4f * p.LimbAmt * .5f;
            float s = .9375f * (e is Animal { IsBaby: true } ? .5f : 1f);
            int tw = witch ? 128 : 64;
            Parts(MobRoot(e, p, s), tw, tw, _vHead, _vBody, _vArms, _vRLeg, _vLLeg);
        }

        // ===================== strażnik (ModelGuardian) =====================
        private ModelPart _gBody, _gEye, _gTail0, _gTail1, _gTail2; private ModelPart[] _gSpines;
        private static readonly float[] GSx = { 1.75f, .25f, 0, 0, .5f, .5f, .5f, .5f, 1.25f, .75f, 0, 0 }, GSy = { 0, 0, 0, 0, .25f, 1.75f, 1.25f, .75f, 0, 0, 0, 0 },
            GSz = { 0, 0, .25f, 1.75f, 0, 0, 0, 0, 0, 0, .75f, 1.25f }, GPx = { 0, 0, 8, -8, -8, 8, 8, -8, 0, 0, 8, -8 }, GPy = { -8, -8, -8, -8, 0, 0, 0, 0, 8, 8, 8, 8 }, GPz = { 8, -8, 0, 0, -8, -8, 8, 8, 8, -8, 0, 0 };
        private void RenderGuardian(Guardian g, LivingPose p)
        {
            if (_gBody == null)
            {
                _gBody = P(0, 0, 0).Box(0, 0, -6, 10, -8, 12, 12, 16).Box(0, 28, -8, 10, -6, 2, 12, 12).Box(0, 28, 6, 10, -6, 2, 12, 12, 0, true).Box(16, 40, -6, 8, -6, 12, 2, 12).Box(16, 40, -6, 22, -6, 12, 2, 12);
                _gSpines = new ModelPart[12];
                for (int i = 0; i < 12; i++) { _gSpines[i] = P(0, 0, 0).Box(0, 0, -1, -4.5f, -1, 2, 9, 2); _gBody.Children.Add(_gSpines[i]); }
                _gEye = P(0, 0, -8.25f).Box(8, 0, -1, 15, 0, 2, 2, 1); _gBody.Children.Add(_gEye);
                _gTail0 = P(0, 0, 0).Box(40, 0, -2, 14, 7, 4, 4, 8); _gTail1 = P(-1.5f, .5f, 14).Box(0, 54, 0, 14, 0, 3, 3, 7);
                _gTail2 = P(.5f, .5f, 6).Box(41, 32, 0, 14, 0, 2, 2, 6).Box(25, 19, 1, 10.5f, 3, 1, 9, 9);
                _gBody.Children.Add(_gTail0); _gTail0.Children.Add(_gTail1); _gTail1.Children.Add(_gTail2);
            }
            Skin.Layer = SkinGen.Get(g.Elder ? "elder_guardian" : "guardian");
            float spikes = g.Nav.HasPath ? .2f : 1f, f7 = (1 - spikes) * .55f;
            for (int i = 0; i < 12; i++)
            {
                var s = _gSpines[i]; s.RX = MathF.PI * GSx[i]; s.RY = MathF.PI * GSy[i]; s.RZ = MathF.PI * GSz[i];
                float k = 1 + MathF.Cos(p.Age * 1.5f + i) * .01f - f7;
                s.PX = GPx[i] * k; s.PY = 16 + GPy[i] * k; s.PZ = GPz[i] * k;
            }
            _gBody.RY = Rad(p.HeadYaw); _gBody.RX = Rad(p.Pitch);
            _gEye.PZ = -8.25f; _gEye.PX = 0; _gEye.PY = 0;
            float tail = p.Age * .1f;
            _gTail0.RY = MathF.Sin(tail) * MathF.PI * .05f; _gTail1.RY = MathF.Sin(tail) * MathF.PI * .1f; _gTail2.RY = MathF.Sin(tail) * MathF.PI * .15f;
            float sc = g.Elder ? 2.35f : 1f;
            var root = MobRoot(g, p, sc);
            Parts(root, _gBody);
            if (g.BeamTicks > 0 && g.Target is { } t && !t.Dead) RenderBeam(g, t, p, root);
        }
        private readonly ModelPart _beam = P(0, 0, 0).Box(0, 0, -.5f, -.5f, 0, 1, 1, 16);
        /// <summary>Laser strażnika: cienki box od oka do celu, coraz jaśniejszy w miarę ładowania.</summary>
        private void RenderBeam(Guardian g, LivingEntity t, LivingPose p, Matrix4 root)
        {
            var eye = new Vector3(p.Pos.X, p.Pos.Y + g.EyeHeight, p.Pos.Z);
            var tp = new Vector3((float)(t.IX(p.Pt) - g.IX(p.Pt)), (float)(t.IY(p.Pt) - g.IY(p.Pt) + t.Height * .5f), (float)(t.IZ(p.Pt) - g.IZ(p.Pt))) + p.Pos;
            var d = tp - eye; float len = d.Length; if (len < .1f) return;
            float yaw = MathF.Atan2(d.X, d.Z), pitch = -MathF.Asin(d.Y / len);
            Skin.Layer = SkinGen.Get("guardian_beam");
            float charge = Math.Clamp(g.BeamTicks / (g.Elder ? 60f : 80f), 0, 1);
            Skin.Color = new Vector4(.4f + .6f * charge, .6f + .4f * charge, 1f, 1);
            var m = Matrix4.CreateScale(2f, 2f, len) * Matrix4.CreateRotationX(pitch) * Matrix4.CreateRotationY(yaw) * Matrix4.CreateTranslation(eye);
            RenderPart(Skin, _beam, m);
            Skin.Color = Vector4.One;
        }

        // ===================== szlam (ModelSlime) =====================
        private readonly ModelPart _slInner = P(0, 0, 0).Box(0, 16, -3, 17, -3, 6, 6, 6).Box(32, 0, -3.25f, 18, -3.5f, 2, 2, 2).Box(32, 4, 1.25f, 18, -3.5f, 2, 2, 2).Box(32, 8, 0, 21, -3.5f, 1, 1, 1);
        private readonly ModelPart _slOuter = P(0, 0, 0).Box(0, 0, -4, 16, -4, 8, 8, 8);
        private void RenderSlime(Slime s, LivingPose p)
        {
            Skin.Layer = SkinGen.Get("slime");
            float size = s.Size, squish = s.OnGround ? 0 : Math.Clamp((float)-s.MotionY * 2f, -.5f, .5f);
            float f2 = squish / (size * .5f + 1), f3 = 1 / (f2 + 1);
            var root = MobRoot(s, p, f3 * size, 1 / f3 * size, f3 * size);
            Parts(root, _slInner);
            Translucent(p, () => { Skin.Layer = SkinGen.Get("slime"); Parts(root, _slOuter); });
        }

        // ===================== rybik cukrowy (ModelSilverfish) =====================
        private ModelPart[] _sfParts, _sfWings; private float[] _sfZ;
        private void RenderSilverfish(LivingEntity e, LivingPose p)
        {
            if (_sfParts == null)
            {
                int[][] len = { new[] { 3, 2, 2 }, new[] { 4, 3, 2 }, new[] { 6, 4, 3 }, new[] { 3, 3, 3 }, new[] { 2, 2, 3 }, new[] { 2, 1, 2 }, new[] { 1, 1, 2 } };
                int[][] tex = { new[] { 0, 0 }, new[] { 0, 4 }, new[] { 0, 9 }, new[] { 0, 16 }, new[] { 0, 22 }, new[] { 11, 0 }, new[] { 13, 4 } };
                _sfParts = new ModelPart[7]; _sfZ = new float[7]; float f = -3.5f;
                for (int i = 0; i < 7; i++)
                {
                    _sfParts[i] = P(0, 24 - len[i][1], f).Box(tex[i][0], tex[i][1], len[i][0] * -.5f, 0, len[i][2] * -.5f, len[i][0], len[i][1], len[i][2]);
                    _sfZ[i] = f; if (i < 6) f += (len[i][2] + len[i + 1][2]) * .5f;
                }
                _sfWings = new[]
                {
                    P(0, 16, _sfZ[2]).Box(20, 0, -5, 0, len[2][2] * -.5f, 10, 8, len[2][2]),
                    P(0, 20, _sfZ[4]).Box(20, 11, -3, 0, len[4][2] * -.5f, 6, 4, len[4][2]),
                    P(0, 19, _sfZ[1]).Box(20, 18, -3, 0, len[4][2] * -.5f, 6, 5, len[1][2])
                };
            }
            Skin.Layer = SkinGen.Get("silverfish");
            for (int i = 0; i < 7; i++)
            {
                _sfParts[i].RY = MathF.Cos(p.Age * .9f + i * .15f * MathF.PI) * MathF.PI * .05f * (1 + Math.Abs(i - 2));
                _sfParts[i].PX = MathF.Sin(p.Age * .9f + i * .15f * MathF.PI) * MathF.PI * .2f * Math.Abs(i - 2);
            }
            _sfWings[0].RY = _sfParts[2].RY; _sfWings[1].RY = _sfParts[4].RY; _sfWings[1].PX = _sfParts[4].PX; _sfWings[2].RY = _sfParts[1].RY; _sfWings[2].PX = _sfParts[1].PX;
            var root = MobRoot(e, p, 1f, 1f, 1f, 0, 180f);
            Parts(root, _sfParts); Parts(root, _sfWings);
        }

        // ===================== pająk (do jaskiniowego) =====================
        private void RenderSpider(LivingEntity e, LivingPose p, string skin, float scale)
        {
            InitSpider();
            Skin.Layer = SkinGen.Get(skin);
            _spHead.RY = Rad(p.HeadYaw); _spHead.RX = Rad(p.Pitch);
            float f = MathF.PI / 4f, f2 = MathF.PI / 8f, s = p.Limb * .6662f, amt = p.LimbAmt;
            float[] rz = { -f, f, -f * .74f, f * .74f, -f * .74f, f * .74f, -f, f };
            float[] ry = { f2 * 2, -f2 * 2, f2, -f2, -f2, f2, -f2 * 2, f2 * 2 };
            float[] w3 = { -(MathF.Cos(s * 2) * .4f) * amt, -(MathF.Cos(s * 2 + MathF.PI) * .4f) * amt, -(MathF.Cos(s * 2 + MathF.PI / 2) * .4f) * amt, -(MathF.Cos(s * 2 + MathF.PI * 1.5f) * .4f) * amt };
            float[] w7 = { MathF.Abs(MathF.Sin(s) * .4f) * amt, MathF.Abs(MathF.Sin(s + MathF.PI) * .4f) * amt, MathF.Abs(MathF.Sin(s + MathF.PI / 2) * .4f) * amt, MathF.Abs(MathF.Sin(s + MathF.PI * 1.5f) * .4f) * amt };
            for (int i = 0; i < 8; i++) { int k = i / 2; float sg = i % 2 == 0 ? 1 : -1; _spLegs[i].RZ = rz[i] + sg * w7[k]; _spLegs[i].RY = ry[i] + sg * w3[k]; }
            var root = MobRoot(e, p, scale, scale, scale, 0, 180f);
            Parts(root, _spHead, _spNeck, _spBody); Parts(root, _spLegs);
        }

        // ===================== zombie: dorosły, dziecko, wieśniak, gigant =====================
        private readonly ModelPart _zvHead = P(0, 0, 0).Box(0, 32, -4, -10, -4, 8, 10, 8).Box(24, 32, -1, -3, -6, 2, 4, 2);
        private void RenderZombie(LivingEntity e, LivingPose p, float scale, bool baby, bool villager)
        {
            Skin.Layer = SkinGen.Get(villager ? "zombie_villager" : "zombie");
            Biped(e, p.Pt, p.Limb, p.LimbAmt, p.Age, p.HeadYaw, p.Pitch, _head, _body, _rArm, _lArm, _rLeg, _lLeg, true, 4);
            var head = _head;
            if (villager) { _zvHead.Reset(_head.PX, _head.PY, _head.PZ); _zvHead.RX = _head.RX; _zvHead.RY = _head.RY; head = _zvHead; }
            var root = MobRoot(e, p, scale);
            if (baby)
            {
                Parts(Matrix4.CreateTranslation(0, 1f, 0) * Matrix4.CreateScale(.75f) * root, head);
                Parts(Matrix4.CreateTranslation(0, 1.5f, 0) * Matrix4.CreateScale(.5f) * root, _body, _rArm, _lArm, _rLeg, _lLeg);
            }
            else Parts(root, head, _body, _rArm, _lArm, _rLeg, _lLeg);
            if (e.HeldItem != null) RenderHeld(e.HeldItem, _rArm, baby ? Matrix4.CreateTranslation(0, 1.5f, 0) * Matrix4.CreateScale(.5f) * root : root);
        }

        // ===================== creeper (z ładunkiem) =====================
        private readonly ModelPart _ccHead = P(0, 6, 0).Box(0, 0, -4, -8, -4, 8, 8, 8, 2f), _ccBody = P(0, 6, 0).Box(16, 16, -4, 0, -2, 8, 12, 4, 2f);
        private readonly ModelPart[] _ccLegs = { P(-2, 18, 4).Box(0, 16, -2, 0, -2, 4, 6, 4, 2f), P(2, 18, 4).Box(0, 16, -2, 0, -2, 4, 6, 4, 2f), P(-2, 18, -4).Box(0, 16, -2, 0, -2, 4, 6, 4, 2f), P(2, 18, -4).Box(0, 16, -2, 0, -2, 4, 6, 4, 2f) };
        private void RenderCreeper(Creeper cr, LivingPose p)
        {
            Skin.Layer = SkinGen.Get("creeper");
            float fuse = (cr.PrevFuse + (cr.Fuse - cr.PrevFuse) * p.Pt) / (Creeper.MaxFuse - 2);
            var croot = p.Root;
            if (fuse > 0)
            {
                float f1 = 1f + MathF.Sin(fuse * 100f) * fuse * .01f;
                fuse = Math.Clamp(fuse, 0, 1); fuse *= fuse; fuse *= fuse;
                float sxz = (1f + fuse * .4f) * f1, sy = (1f + fuse * .1f) / f1;
                croot = MobRoot(cr, p, sxz, sy, sxz);
                if ((int)(fuse * 10f) % 2 != 0 || cr.Fuse > 20 && (cr.Fuse / 2) % 2 == 0) Skin.Overlay = new Vector4(1, 1, 1, Math.Clamp(fuse * .2f + .3f, 0, .8f));
            }
            _cHead.RY = Rad(p.HeadYaw); _cHead.RX = Rad(p.Pitch);
            Quad4(_cLegs, p.Limb, p.LimbAmt);
            Parts(croot, _cHead, _cBody); Parts(croot, _cLegs);
            if (!cr.Charged) return;
            var ov = Skin.Overlay; Skin.Overlay = Vector4.Zero;
            _ccHead.RY = _cHead.RY; _ccHead.RX = _cHead.RX;
            for (int i = 0; i < 4; i++) _ccLegs[i].RX = _cLegs[i].RX;
            Translucent(p, () =>
            {
                Skin.Layer = SkinGen.Get("creeper_charge");
                Parts(croot, _ccHead, _ccBody); Parts(croot, _ccLegs);
            });
            Skin.Overlay = ov;
        }

        // ===================== koń (ModelHorse, 128x128) =====================
        private ModelPart _hHead, _hHorseEars, _hMuleEars, _hFaceMetal, _hReins, _hBody, _hTailA, _hTailB, _hTailC, _hChestL, _hChestR, _hSaddle, _hStirL, _hStirR;
        private ModelPart[] _hLegs, _hShins;
        private void InitHorse()
        {
            if (_hHead != null) return;
            _hHead = P(0, 4, -10).Box(0, 0, -2.5f, -10, -1.5f, 5, 5, 7).Box(24, 18, -2, -10, -7, 4, 3, 6).Box(24, 27, -2, -7, -6.5f, 4, 2, 5)
                .Box(0, 12, -2.05f, -9.8f, -2, 4, 14, 8).Box(58, 0, -1, -11.5f, 5, 2, 16, 4);
            _hHorseEars = P(0, 0, 0).Box(0, 0, .45f, -12, 4, 2, 3, 1).Box(0, 0, -2.45f, -12, 4, 2, 3, 1);
            var muleL = P(0, 0, 0).Box(0, 12, -2, -16, 4, 2, 7, 1); muleL.RZ = .2617994f;
            var muleR = P(0, 0, 0).Box(0, 12, 0, -16, 4, 2, 7, 1); muleR.RZ = -.2617994f;
            _hMuleEars = P(0, 0, 0); _hMuleEars.Children.Add(muleL); _hMuleEars.Children.Add(muleR);
            _hFaceMetal = P(0, 0, 0).Box(74, 13, 1.5f, -8, -4, 1, 2, 2).Box(74, 13, -2.5f, -8, -4, 1, 2, 2);
            _hReins = P(0, 0, 0).Box(44, 10, 2.6f, -6, -6, 0, 3, 16).Box(44, 5, -2.6f, -6, -6, 0, 3, 16);
            _hHead.Children.Add(_hHorseEars); _hHead.Children.Add(_hMuleEars); _hHead.Children.Add(_hFaceMetal); _hHead.Children.Add(_hReins);
            _hBody = P(0, 11, 9).Box(0, 34, -5, -8, -19, 10, 10, 24);
            _hTailA = P(0, 3, 14).Box(44, 0, -1, -1, 0, 2, 2, 3); _hTailB = P(0, 3, 14).Box(38, 7, -1.5f, -2, 3, 3, 4, 7); _hTailC = P(0, 3, 14).Box(24, 3, -1.5f, -4.5f, 9, 3, 4, 7);
            (float x, float z, int u, float bx, float bz, int w, int h, int d, float sx, float sz, float hx, float hz)[] legs =
            {
                (4, 11, 78, -2.5f, -2.5f, 4, 9, 5, -2, -1.5f, -2.5f, -2), (-4, 11, 96, -1.5f, -2.5f, 4, 9, 5, -1, -1.5f, -1.5f, -2),
                (4, -8, 44, -1.9f, -2.1f, 3, 8, 4, -1.9f, -1.6f, -2.4f, -2.1f), (-4, -8, 60, -1.1f, -2.1f, 3, 8, 4, -1.1f, -1.6f, -1.6f, -2.1f)
            };
            _hLegs = new ModelPart[4]; _hShins = new ModelPart[4];
            for (int i = 0; i < 4; i++)
            {
                var l = legs[i]; bool back = i < 2;
                _hLegs[i] = P(l.x, 9, l.z).Box(l.u, 29, l.bx, back ? -2 : -1, l.bz, l.w, l.h, l.d);
                _hShins[i] = P(0, 7, 0).Box(l.u, back ? 43 : 41, l.sx, 0, l.sz, 3, 5, 3).Box(l.u, 51, l.hx, 5.1f, l.hz, 4, 3, 4);
                _hLegs[i].Children.Add(_hShins[i]);
            }
            _hChestL = P(-7.5f, 3, 10).Box(0, 34, -3, 0, 0, 8, 8, 3); _hChestL.RY = MathF.PI / 2;
            _hChestR = P(4.5f, 3, 10).Box(0, 47, -3, 0, 0, 8, 8, 3); _hChestR.RY = MathF.PI / 2;
            _hSaddle = P(0, 2, 2).Box(80, 0, -5, 0, -3, 10, 1, 8).Box(106, 9, -1.5f, -1, -3, 3, 1, 2).Box(80, 9, -4, -1, 3, 8, 1, 2);
            _hStirL = P(5, 3, 2).Box(70, 0, -.5f, 0, -.5f, 1, 6, 1).Box(74, 0, -.5f, 6, -1, 1, 2, 2);
            _hStirR = P(-5, 3, 2).Box(70, 0, -.5f, 0, -.5f, 1, 6, 1).Box(74, 4, -.5f, 6, -1, 1, 2, 2);
        }
        private static readonly string[] HorseColorSkins = { "horse_white", "horse_creamy", "horse_chestnut", "horse_brown", "horse_black", "horse_gray", "horse_darkbrown" };
        private void RenderHorse(Horse h, LivingPose p)
        {
            InitHorse();
            float typeScale = h.HorseType == 1 ? .87f : h.HorseType == 2 ? .92f : 1f, sc = typeScale * (h.IsBaby ? .5f : 1f);
            var root = MobRoot(h, p, sc);
            bool longEars = h.HorseType is 1 or 2;
            _hHorseEars.Visible = !longEars; _hMuleEars.Visible = longEars;
            _hFaceMetal.Visible = h.Saddled; _hReins.Visible = h.Saddled && h.Rider != null;
            float eat = h.EatTicks > 0 ? 1f : 0f, rear = h.Rearing ? 1f : 0f;
            _hHead.Reset(0, 4, -10);
            _hHead.RX = (.5235988f + Rad(Math.Clamp(p.Pitch, -40, 40))) * (1 - eat) + 2.1816616f * eat; _hHead.RY = Rad(Math.Clamp(p.HeadYaw, -20, 20)) * (1 - eat);
            if (eat > 0) { _hHead.PY = 4 + 7 * eat; _hHead.PZ = -10 - 2 * eat; }
            float l = p.Limb * .6662f, amt = p.LimbAmt;
            float swingB = MathF.Cos(l) * .8f * amt, swingF = MathF.Cos(l + MathF.PI) * .8f * amt;
            _hLegs[0].RX = swingB; _hLegs[1].RX = -swingB; _hLegs[2].RX = swingF; _hLegs[3].RX = -swingF;
            for (int i = 0; i < 4; i++) _hShins[i].RX = Math.Max(0, i < 2 ? -_hLegs[i].RX : _hLegs[i].RX) * .6f;
            float tailSwing = MathF.Cos(p.Age * .1f) * .05f + MathF.Cos(l) * .2f * amt;
            _hTailA.RX = _hTailB.RX = -1.134464f + amt * .3f; _hTailC.RX = -1.40215f + amt * .3f;
            _hTailA.RY = _hTailB.RY = _hTailC.RY = tailSwing;
            _hChestL.Visible = _hChestR.Visible = h.Chested && h.CanCarryChest;
            bool saddle = h.Saddled; _hSaddle.Visible = _hStirL.Visible = _hStirR.Visible = saddle;
            // stawanie dęba: tułów obraca się wokół tylnych nóg
            var front = root;
            if (rear > 0)
            {
                front = Matrix4.CreateTranslation(0, -9 / 16f, -11 / 16f) * Matrix4.CreateRotationX(-MathF.PI / 4 * rear) * Matrix4.CreateTranslation(0, 9 / 16f, 11 / 16f) * root;
                _hLegs[2].RX = _hLegs[3].RX = -.6f; _hHead.RX -= .3f;
            }
            ModelPart[] main = { _hHead, _hBody, _hTailA, _hTailB, _hTailC, _hLegs[2], _hLegs[3], _hChestL, _hChestR, _hSaddle, _hStirL, _hStirR };
            void Draw(Matrix4 r0, Matrix4 rf) { Parts(rf, 128, 128, main); Parts(r0, 128, 128, _hLegs[0], _hLegs[1]); }
            string baseSkin = h.HorseType switch { 1 => "horse_donkey", 2 => "horse_mule", 3 => "horse_zombie", 4 => "horse_skeleton", _ => HorseColorSkins[Math.Clamp(h.Variant & 255, 0, 6)] };
            Skin.Layer = SkinGen.Get(baseSkin);
            Draw(root, front);
            var grow = Matrix4.CreateScale(1.012f);
            int marks = (h.Variant >> 8) & 255;
            if (h.HorseType == 0 && marks is >= 1 and <= 4)
            {
                Skin.Layer = SkinGen.Get("horse_markings_" + marks);
                _hSaddle.Visible = _hStirL.Visible = _hStirR.Visible = _hChestL.Visible = _hChestR.Visible = false;
                Draw(grow * root, grow * front);
            }
            int armor = h.ArmorItem;
            if (h.CanWearArmor && armor != 0)
            {
                Skin.Layer = SkinGen.Get(armor == I.IronHorseArmor ? "horse_armor_iron" : armor == I.GoldHorseArmor ? "horse_armor_gold" : "horse_armor_diamond");
                _hSaddle.Visible = _hStirL.Visible = _hStirR.Visible = _hChestL.Visible = _hChestR.Visible = false;
                var g2 = Matrix4.CreateScale(1.025f);
                Draw(g2 * root, g2 * front);
            }
        }

        // ===================== węzeł smyczy i sama smycz =====================
        private readonly ModelPart _knot = P(0, 0, 0).Box(0, 0, -3, -6, -3, 6, 8, 6);
        private void RenderLeashKnot(Vector3 pos)
        {
            Skin.Layer = SkinGen.Get("horse_brown");
            RenderPart(Skin, _knot, Matrix4.CreateScale(-1, -1, 1) * Matrix4.CreateTranslation(pos + new Vector3(0, .5f, 0)), 32, 32);
        }
        private readonly ModelPart _rope = P(0, 0, 0).Box(78, 51, -.5f, -.5f, 0, 1, 1, 16);
        /// <summary>RenderLiving.renderLeash: lina zwisająca łukiem między mobem a trzymającym.</summary>
        public void RenderLeash(Mob m, float pt, Vector3 mobPos, Vector3 cam)
        {
            var holder = m.LeashHolder; if (holder == null) return;
            Vector3 a = mobPos + new Vector3(0, m.Height * .6f, 0), b;
            if (holder is Player pl)
            {
                float yaw = Rad(MathUtil.LerpAngle(pl.PrevYaw, pl.Yaw, pt));
                b = new Vector3((float)(pl.IX(pt) - cam.X) - MathF.Cos(yaw) * .35f, (float)(pl.IY(pt) - cam.Y) + pl.EyeHeight * .7f, (float)(pl.IZ(pt) - cam.Z) - MathF.Sin(yaw) * .35f);
            }
            else b = new Vector3((float)(holder.IX(pt) - cam.X), (float)(holder.IY(pt) - cam.Y) + .3f, (float)(holder.IZ(pt) - cam.Z));
            Skin.Layer = SkinGen.Get("horse_brown"); Skin.Color = new Vector4(.55f, .42f, .25f, 1);
            const int N = 16;
            var prev = a;
            for (int i = 1; i <= N; i++)
            {
                float t = i / (float)N;
                var cur = Vector3.Lerp(a, b, t); cur.Y = a.Y + (b.Y - a.Y) * (t * t + t) * .5f;
                var d = cur - prev; float len = d.Length;
                if (len > 1e-4f)
                {
                    var mm = Matrix4.CreateScale(.6f, .6f, len) * Matrix4.CreateRotationX(-MathF.Asin(Math.Clamp(d.Y / len, -1, 1))) * Matrix4.CreateRotationY(MathF.Atan2(d.X, d.Z)) * Matrix4.CreateTranslation(prev);
                    RenderPart(Skin, _rope, mm, 128, 128);
                }
                prev = cur;
            }
            Skin.Color = Vector4.One;
        }
    }
}
