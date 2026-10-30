using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Render;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace BlockCraft
{
    public sealed partial class Game
    {
        /// <summary>Otwiera ekran dla kontenera encji (koń, wieśniak).</summary>
        public void OpenEntityScreen(Player p, Container c)
        {
            Gui.ContainerScreen s = c switch
            {
                HorseContainer hc => new Gui.HorseScreen(p, hc),
                MerchantContainer mc => new Gui.MerchantScreen(p, mc),
                _ => new Gui.ContainerScreen(p, c)
            };
            SetScreen(s);
            p.OpenContainer = c;
        }

        /// <summary>Model moba w GUI (jak GuiInventory.drawEntityOnScreen).</summary>
        public void DrawMobPreview(LivingEntity e, int cx, int cy, float size, float lookX, float lookY)
        {
            if (e == null) return;
            Sb.Flush();
            int fw = FramebufferSize.X, fh = FramebufferSize.Y;
            var proj = Matrix4.CreateOrthographicOffCenter(0, fw, fh, 0, -500, 500);
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest); GL.Disable(EnableCap.CullFace);
            var er = Renderer.Entities;
            er.Shader.Use(); er.Shader.Set("uViewProj", proj);
            er.Shader.Set("uFogMode", 0); er.Shader.Set("uFogStart", 1000f); er.Shader.Set("uFogEnd", 2000f); er.Shader.Set("uSun", 1f); er.Shader.Set("uGamma", 0.5f);
            float yawS = e.Yaw, pyS = e.PrevYaw, by = e.BodyYaw, pby = e.PrevBodyYaw, pi = e.Pitch, ppi = e.PrevPitch;
            float body = MathF.Atan(lookX / 40f) * 20f, head = MathF.Atan(lookX / 40f) * 40f, pit = -MathF.Atan(lookY / 40f) * 20f;
            e.BodyYaw = e.PrevBodyYaw = 180 + body + 30; e.Yaw = e.PrevYaw = 180 + head + 30; e.Pitch = e.PrevPitch = pit;
            float la = e.LimbSwingAmount, pla = e.PrevLimbSwingAmount; e.LimbSwingAmount = e.PrevLimbSwingAmount = 0;
            int hurt = e.HurtTime; e.HurtTime = 0;
            er.Skin.Sky = 15; er.Skin.Blk = 15; er.Terrain.Sky = 15; er.Terrain.Blk = 15;
            er.Skin.Overlay = Vector4.Zero; er.Skin.Color = Vector4.One;
            int start = er.Skin.Verts, tstart = er.Terrain.Verts;
            er.RenderLiving(e, 1f, Vector3.Zero, true);
            float s = size * GuiScale;
            var m = Matrix4.CreateScale(s, -s, s) * Matrix4.CreateTranslation(cx * GuiScale, cy * GuiScale, 0);
            for (int v = start; v < er.Skin.Verts; v++) Xf(er.Skin, v, m);
            for (int v = tstart; v < er.Terrain.Verts; v++) Xf(er.Terrain, v, m);
            er.Flush(er.Skin, er.SkinTex); er.Flush(er.Terrain, Renderer.Terrain);
            e.Yaw = yawS; e.PrevYaw = pyS; e.BodyYaw = by; e.PrevBodyYaw = pby; e.Pitch = pi; e.PrevPitch = ppi; e.LimbSwingAmount = la; e.PrevLimbSwingAmount = pla; e.HurtTime = hurt;
            GL.Disable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            static void Xf(ModelBatch b, int v, Matrix4 m)
            {
                int o = v * ModelBatch.F;
                var p = Vector3.TransformPosition(new Vector3(b.D[o], b.D[o + 1], b.D[o + 2]), m);
                b.D[o] = p.X; b.D[o + 1] = p.Y; b.D[o + 2] = -p.Z;
            }
        }
    }
}

namespace BlockCraft.Gui
{
    /// <summary>GuiScreenHorseInventory: siodło, zbroja, skrzynia osła/muła i podgląd konia.</summary>
    public sealed class HorseScreen : ContainerScreen
    {
        private readonly HorseContainer _hc;
        public HorseScreen(Player p, HorseContainer c) : base(p, c) { _hc = c; }
        protected override void DrawBackground(int mx, int my)
        {
            var sb = G.Sb;
            GuiDraw.Panel(G, Left, Top, C.Width, C.Height);
            sb.Text(C.Title, Left + 8, Top + 6, 0xFF404040, false);
            sb.Text("Ekwipunek", Left + 8, Top + C.Height - 96 + 2, 0xFF404040, false);
            foreach (var s in C.Slots) GuiDraw.Slot(G, Left + s.X - 1, Top + s.Y - 1);
            if (_hc.Horse.Inv.Get(0) == null) Ghost("item_saddle", 8, 18);
            if (_hc.Horse.CanWearArmor && _hc.Horse.Inv.Get(1) == null) Ghost("item_iron_horse_armor", 8, 36);
            sb.Fill(Left + 25, Top + 17, 52, 52, 0xFF000000);
            G.DrawMobPreview(_hc.Horse, Left + 51, Top + 60, 17, Left + 51 - mx, Top + 25 - my);
        }
        private void Ghost(string tex, int x, int y) => G.Sb.DrawLayer(G.Renderer.Terrain, TextureGen.Layer(tex), Left + x, Top + y, 16, 16, 0x40000000);
        public override void Tick() { if (!_hc.StillValid(P)) G.SetScreen(null); }
    }

    /// <summary>GuiMerchant: dwa sloty zapłaty, wynik, strzałki wyboru oferty i przekreślenie wyczerpanej.</summary>
    public sealed class MerchantScreen : ContainerScreen
    {
        private readonly MerchantContainer _mc;
        public MerchantScreen(Player p, MerchantContainer c) : base(p, c) { _mc = c; }
        private int Count => _mc.Villager.Recipes.Count;
        private (int x, int y) Prev => (Left + 36 - 19, Top + 19);
        private (int x, int y) Next => (Left + 120 + 27, Top + 19);
        protected override void DrawBackground(int mx, int my)
        {
            var sb = G.Sb;
            GuiDraw.Panel(G, Left, Top, C.Width, C.Height);
            sb.TextCentered(C.Title, Left + C.Width / 2f, Top + 6, 0xFF404040, false);
            sb.Text("Ekwipunek", Left + 8, Top + C.Height - 96 + 2, 0xFF404040, false);
            foreach (var s in C.Slots) GuiDraw.Slot(G, Left + s.X - 1, Top + s.Y - 1, s.IsOutput ? 18 : 18);
            GuiDraw.Slot(G, Left + 115, Top + 48, 26);
            Button(Prev, mx, my, _mc.Index > 0, false);
            Button(Next, mx, my, _mc.Index < Count - 1, true);
            var r = _mc.Current;
            if (r == null) return;
            // podgląd oferty nad slotami
            G.DrawItem(r.Buy1, Left + 36, Top + 24);
            if (r.Buy2 != null) G.DrawItem(r.Buy2, Left + 62, Top + 24);
            G.DrawItem(r.Sell, Left + 120, Top + 24);
            DrawArrow(Left + 83, Top + 51, 0);
            if (r.Disabled)
            {
                for (int i = 0; i < 14; i++) { sb.Fill(Left + 87 + i, Top + 52 + i, 2, 2, 0xFFE02020); sb.Fill(Left + 100 - i, Top + 52 + i, 2, 2, 0xFFE02020); }
                if (In(mx, my, Left + 83, Top + 51, 24, 16)) GuiDraw.Tooltip(G, "Oferta wyczerpana\n§7Handluj innymi, by ją odnowić", mx, my);
            }
            sb.TextCentered($"{_mc.Index + 1}/{Count}", Left + C.Width / 2f, Top + 42, 0xFF404040, false);
        }
        private void Button((int x, int y) p, int mx, int my, bool on, bool right)
        {
            var sb = G.Sb;
            bool hover = on && In(mx, my, p.x, p.y, 12, 19);
            uint face = !on ? 0xFF6E6E6E : hover ? 0xFFA8B8F0 : 0xFFB0B0B0;
            sb.Fill(p.x, p.y, 12, 19, 0xFF202020); sb.Fill(p.x + 1, p.y + 1, 10, 17, face);
            uint arrow = on ? 0xFF303030 : 0xFF505050;
            for (int i = 0; i < 5; i++)
            {
                int w = 5 - i, x = right ? p.x + 4 + i : p.x + 8 - i;
                sb.Fill(right ? p.x + 3 + i : p.x + 9 - i - 1, p.y + 5 + i, 1, 9 - i * 2, arrow);
            }
        }
        private static bool In(int mx, int my, int x, int y, int w, int h) => mx >= x && my >= y && mx < x + w && my < y + h;
        public override void MouseDown(int mx, int my, int btn)
        {
            if (btn == 0 && In(mx, my, Prev.x, Prev.y, 12, 19) && _mc.Index > 0) { _mc.SetIndex(_mc.Index - 1); return; }
            if (btn == 0 && In(mx, my, Next.x, Next.y, 12, 19) && _mc.Index < Count - 1) { _mc.SetIndex(_mc.Index + 1); return; }
            base.MouseDown(mx, my, btn);
        }
        public override void Render(int mx, int my, float pt)
        {
            base.Render(mx, my, pt);
            var r = _mc.Current;
            if (r == null || P.Cursor != null) return;
            if (In(mx, my, Left + 36, Top + 24, 16, 16)) GuiDraw.Tooltip(G, GuiDraw.ItemTooltip(r.Buy1, false), mx, my);
            else if (r.Buy2 != null && In(mx, my, Left + 62, Top + 24, 16, 16)) GuiDraw.Tooltip(G, GuiDraw.ItemTooltip(r.Buy2, false), mx, my);
            else if (In(mx, my, Left + 120, Top + 24, 16, 16)) GuiDraw.Tooltip(G, GuiDraw.ItemTooltip(r.Sell, false) + Enchants(r.Sell), mx, my);
        }
        private static string Enchants(ItemStack s)
        {
            string t = "";
            foreach (var kv in EnchantHelper.GetAll(s)) t += "\n§7" + EnchantHelper.Name(kv.Key) + " " + EnchantHelper.Roman(kv.Value);
            foreach (var kv in EnchantHelper.GetAll(s, "StoredEnchantments")) t += "\n§7" + EnchantHelper.Name(kv.Key) + " " + EnchantHelper.Roman(kv.Value);
            return t;
        }
        public override void Tick() { if (!_mc.StillValid(P)) G.SetScreen(null); }
    }
}
