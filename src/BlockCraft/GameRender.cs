using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft;

public sealed partial class Game
{
    private Matrix4 _proj;

    private void RenderFrame(float pt)
    {
        int fw = FramebufferSize.X, fh = FramebufferSize.Y;
        GL.Viewport(0, 0, fw, fh);
        GL.ClearColor(0, 0, 0, 1);
        GL.DepthMask(true);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        bool worldVisible = (_screen == null || _screen.ShowWorld) && Renderer.World != null;
        if (worldVisible && World != null && Player != null && _sessionStarted) RenderGameWorld(pt, fw, fh);
        else if (_menuWorld != null && Renderer.World == _menuWorld) RenderMenuWorld(pt, fw, fh);

        // GUI
        Sb.Begin(fw, fh, GuiScale);
        if (World != null && Player != null && _sessionStarted && !HideGui) RenderHud(pt);
        if (_screen != null)
        {
            try { _screen.Render(MouseGuiX, MouseGuiY, pt); } catch (Exception ex) { Log.Error("Screen: " + ex); }
        }
        Sb.End();
    }

    private void RenderMenuWorld(float pt, int fw, int fh)
    {
        var w = _menuWorld;
        _menuYaw += 0.03f;
        int top = 0;
        for (int dx = -3; dx <= 3; dx++) for (int dz = -3; dz <= 3; dz++) top = Math.Max(top, w.GetHeight(w.SpawnX + dx, w.SpawnZ + dz));
        var cam = new Vector3d(w.SpawnX + 0.5, Math.Max(w.SpawnY + 4, top + 2.5), w.SpawnZ + 0.5);
        Renderer.Update(cam);
        var gs = new GlStack();
        gs.Rotate(12, 1, 0, 0);
        gs.Rotate(_menuYaw + 180, 0, 1, 0);
        var view = gs.M;
        _proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(85), fw / (float)Math.Max(1, fh), 0.05f, Math.Max(256f, Renderer.RenderDistance * 32f));
        var look = -new Vector3(view.M13, view.M23, view.M33);
        Renderer.ComputeAtmosphere(cam, look, pt, 0);
        GL.ClearColor(Renderer.FogColor.X, Renderer.FogColor.Y, Renderer.FogColor.Z, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        Renderer.Render(cam, view, _proj, pt, null, true, null, -1);
    }

    private void RenderGameWorld(float pt, int fw, int fh)
    {
        var p = Player; var w = World;
        Ctrl.UpdateTarget(pt);
        double ex = p.IX(pt), ey = p.IY(pt) + p.EyeHeight, ez = p.IZ(pt);
        if (p.Sleeping) ey = p.IY(pt) + 0.3;
        float yaw = p.Yaw, pitch = p.Pitch;
        // macierz efektów kamery (obrażenia, kołysanie)
        var fx = new GlStack();
        HurtAndBob(fx, pt, true);
        var gs = new GlStack();
        HurtAndBob(gs, pt, Settings.ViewBobbing);
        var cam = new Vector3d(ex, ey, ez);
        bool third = CameraMode != 0;
        if (third)
        {
            double dist = 4.0;
            var (lx, ly, lz) = p.Look(1f);
            double sgn = CameraMode == 1 ? -1 : 1;
            // kolizja kamery z blokami
            for (int i = 0; i < 8; i++)
            {
                double ox = ((i & 1) * 2 - 1) * 0.1, oy = (((i >> 1) & 1) * 2 - 1) * 0.1, oz = (((i >> 2) & 1) * 2 - 1) * 0.1;
                var h = w.RayCast(ex + ox, ey + oy, ez + oz, lx * sgn, ly * sgn, lz * sgn, dist);
                if (h.Hit && h.Distance < dist) dist = h.Distance;
            }
            dist = Math.Max(0.3, dist - 0.1);
            cam = new Vector3d(ex + lx * sgn * dist, ey + ly * sgn * dist, ez + lz * sgn * dist);
            if (CameraMode == 2) { yaw += 180; pitch = -pitch; }
        }
        gs.Rotate(pitch, 1, 0, 0);
        gs.Rotate(yaw + 180, 0, 1, 0);
        var view = gs.M;
        float fov = Settings.Fov * MathHelper.Lerp(_prevFovMod, _fovMod, pt);
        int headFluid = 0;
        if (!third)
        {
            int bx = MathUtil.FloorToInt(ex), by = MathUtil.FloorToInt(ey), bz = MathUtil.FloorToInt(ez);
            int id = w.GetBlock(bx, by, bz);
            if (id == B.Water && ey < by + FluidBlock.HeightOf(w.GetMeta(bx, by, bz))) { headFluid = 1; fov *= 60f / 70f; }
            if (id == B.Lava) headFluid = 2;
        }
        if (p.HasEffect(Effect.Nausea)) { float wave = MathF.Sin((p.Age + pt) * .08f); view *= Matrix4.CreateRotationZ(wave * .035f) * Matrix4.CreateScale(1 + wave * .06f, 1, 1); }
        _proj = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(Math.Clamp(fov, 10, 150)), fw / (float)Math.Max(1, fh), 0.05f, Math.Max(256f, Renderer.RenderDistance * 32f));
        Renderer.Update(cam);
        var look = -new Vector3(view.M13, view.M23, view.M33);
        Renderer.ComputeAtmosphere(cam, look, pt, headFluid);
        ApplyEffectAtmosphere(p, pt);
        GL.ClearColor(Renderer.FogColor.X, Renderer.FogColor.Y, Renderer.FogColor.Z, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        World.World.HitResult? sel = Ctrl.Hit.Hit && !p.Spectator && !HideGui ? Ctrl.Hit : null;
        Renderer.Render(cam, view, _proj, pt, p, third, sel, Ctrl.BreakStage);
        NetCaptureCamera(cam, view);

        // ręka
        if (!third && !p.Spectator && !p.Sleeping && !HideGui)
        {
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend); GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            var hp = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(70), fw / (float)Math.Max(1, fh), 0.05f, 10f);
            var er = Renderer.Entities;
            var hv = new GlStack(); HurtAndBob(hv, pt, Settings.ViewBobbing);
            er.Shader.Use(); er.Shader.Set("uViewProj", hv.M * hp);
            er.Shader.Set("uFogMode", 0); er.Shader.Set("uFogStart", 1000f); er.Shader.Set("uFogEnd", 2000f);
            er.Shader.Set("uSun", Renderer.SunBrightness); er.Shader.Set("uGamma", Settings.Brightness);
            int bx = MathUtil.FloorToInt(p.X), by = MathUtil.FloorToInt(p.Y + p.EyeHeight), bz = MathUtil.FloorToInt(p.Z);
            float equip = 1f - MathHelper.Lerp(_prevEquip, _equip, pt);
            er.RenderFirstPerson(p, pt, equip, _equipped, w.GetSkyLight(bx, by, bz), w.GetBlockLight(bx, by, bz));
            er.Flush(er.Skin, er.SkinTex);
            er.Flush(er.Terrain, Renderer.Terrain);
        }
        GL.Disable(EnableCap.DepthTest);
    }

    private void HurtAndBob(GlStack gs, float pt, bool bob)
    {
        var p = Player;
        float ht = p.HurtTime - pt;
        if (p.Health <= 0) { float dt = p.DeathTime + pt; gs.Rotate(40f - 8000f / (dt + 200f), 0, 0, 1); }
        if (ht > 0)
        {
            float f = ht / p.MaxHurtTime;
            f = MathF.Sin(f * f * f * f * MathF.PI);
            gs.Rotate(-p.AttackedAtYaw, 0, 1, 0);
            gs.Rotate(-f * 14f, 0, 0, 1);
            gs.Rotate(p.AttackedAtYaw, 0, 1, 0);
        }
        if (bob && CameraMode == 0)
        {
            float f = (float)(p.DistWalked - p.PrevDistWalked);
            float f1 = -(float)(p.DistWalked + f * pt);
            float f2 = (float)(p.PrevCameraYawBob + (p.CameraYawBob - p.PrevCameraYawBob) * pt);
            float f3 = p.PrevCameraPitchBob + (p.CameraPitchBob - p.PrevCameraPitchBob) * pt;
            gs.Translate(MathF.Sin(f1 * MathF.PI) * f2 * 0.5f, -MathF.Abs(MathF.Cos(f1 * MathF.PI) * f2), 0);
            gs.Rotate(MathF.Sin(f1 * MathF.PI) * f2 * 3f, 0, 0, 1);
            gs.Rotate(MathF.Abs(MathF.Cos(f1 * MathF.PI - 0.2f) * f2) * 5f, 1, 0, 0);
            gs.Rotate(f3, 1, 0, 0);
        }
    }

    // ================= przedmioty w GUI =================
    /// <summary>Połysk zaklęcia: fioletowa, pulsująca kopia kształtu przedmiotu (przybliżenie RenderItem.renderEffect).</summary>
    /// <summary>Item.hasEffect z 1.8: zaklęcia, zaklęta książka, zaklęte złote jabłko, mikstury z efektami, flaga Glint.</summary>
    public static bool HasGlint(ItemStack s) => s.Item.Glint || s.Id == I.EnchantedBook || EnchantHelper.IsEnchanted(s)
        || s.Id == I.GoldenApple && s.Damage > 0 || s.Id == I.Potion && Potions.Effects(s).Any();

    private void DrawGlint(ItemStack s, float x, float y)
    {
        // addytywnie (GL_SRC_ALPHA, GL_ONE) jak RenderItem.renderEffect: rozjaśnia, nie brudzi koloru przedmiotu
        float t = (Environment.TickCount64 % 3000) / 3000f;
        int a = (int)(90 + 45 * Math.Sin(t * Math.PI * 2 + x * .05));
        uint c = SpriteBatch.Col(128, 64, 204, a);
        Sb.Flush();
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        if (Icons.TryGet(s, out int layer)) Sb.DrawLayer(Icons.Array, layer, x, y, 16, 16, c, 0, 1, 1, 0);
        else Sb.DrawLayer(Renderer.Terrain, s.Item.GetSprite(s), x, y, 16, 16, c);
        Sb.Flush();
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
    }

    public void DrawItem(ItemStack s, float x, float y, bool count = true)
    {
        if (s == null || s.Item == null) return;
        if (Icons.TryGet(s, out int layer)) Sb.DrawLayer(Icons.Array, layer, x, y, 16, 16, 0xFFFFFFFF, 0, 1, 1, 0); // FBO ma oś Y od dołu
        else
        {
            var it = s.Item;
            int sp = it.GetSprite(s);
            uint col = it.GetColor(s, 0);
            Sb.DrawLayer(Renderer.Terrain, sp, x, y, 16, 16, col);
            if (it.OverlayFn != null) { var (ol, oc) = it.OverlayFn(s); if (ol > 0) Sb.DrawLayer(Renderer.Terrain, ol, x, y, 16, 16, oc); }
        }
        if (HasGlint(s)) DrawGlint(s, x, y);
        if (count && s.Count > 1)
        {
            string t = s.Count.ToString();
            Sb.Text(t, x + 17 - Sb.Measure(t), y + 9, 0xFFFFFFFF);
        }
        var item = s.Item;
        if (item.MaxDamage > 0 && s.Damage > 0)
        {
            float f = 1f - (float)s.Damage / item.MaxDamage;
            int w = (int)Math.Round(13 * f);
            int r = (int)(255 * (1 - f)), g = (int)(255 * f);
            Sb.Fill(x + 2, y + 13, 13, 2, 0xFF000000);
            Sb.Fill(x + 2, y + 13, w, 1, SpriteBatch.Col(r, g, 0));
        }
    }

    /// <summary>Model gracza w GUI (ekwipunek).</summary>
    public void DrawPlayerPreview(int cx, int cy, float size, float lookX, float lookY)
    {
        var p = Player;
        if (p == null) return;
        Sb.Flush();
        int fw = FramebufferSize.X, fh = FramebufferSize.Y;
        var proj = Matrix4.CreateOrthographicOffCenter(0, fw, fh, 0, -500, 500);
        GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.Enable(EnableCap.DepthTest); GL.Disable(EnableCap.CullFace);
        var er = Renderer.Entities;
        er.Shader.Use(); er.Shader.Set("uViewProj", proj);
        er.Shader.Set("uFogMode", 0); er.Shader.Set("uFogStart", 1000f); er.Shader.Set("uFogEnd", 2000f); er.Shader.Set("uSun", 1f); er.Shader.Set("uGamma", 0.5f);
        float yawS = p.Yaw, pyS = p.PrevYaw, by = p.BodyYaw, pby = p.PrevBodyYaw, pi = p.Pitch, ppi = p.PrevPitch;
        float body = MathF.Atan(lookX / 40f) * 20f, head = MathF.Atan(lookX / 40f) * 40f, pit = -MathF.Atan(lookY / 40f) * 20f;
        p.BodyYaw = p.PrevBodyYaw = 180 + body; p.Yaw = p.PrevYaw = 180 + head; p.Pitch = p.PrevPitch = pit;
        float la = p.LimbSwingAmount, pla = p.PrevLimbSwingAmount; p.LimbSwingAmount = p.PrevLimbSwingAmount = 0;
        int hurt = p.HurtTime; p.HurtTime = 0;
        er.Skin.Sky = 15; er.Skin.Blk = 15; er.Terrain.Sky = 15; er.Terrain.Blk = 15;
        er.Skin.Overlay = Vector4.Zero;
        // budujemy w przestrzeni świata, potem transformujemy do pikseli ekranu
        int start = er.Skin.Verts, tstart = er.Terrain.Verts;
        er.RenderLiving(p, 1f, Vector3.Zero);
        float s = size * GuiScale;
        var m = Matrix4.CreateScale(s, -s, s) * Matrix4.CreateTranslation(cx * GuiScale, cy * GuiScale, 0);
        TransformBatch(er.Skin, start, m); TransformBatch(er.Terrain, tstart, m);
        er.Flush(er.Skin, er.SkinTex); er.Flush(er.Terrain, Renderer.Terrain);
        p.Yaw = yawS; p.PrevYaw = pyS; p.BodyYaw = by; p.PrevBodyYaw = pby; p.Pitch = pi; p.PrevPitch = ppi; p.LimbSwingAmount = la; p.PrevLimbSwingAmount = pla; p.HurtTime = hurt;
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
    }

    private static void TransformBatch(ModelBatch b, int start, Matrix4 m)
    {
        for (int v = start; v < b.Verts; v++)
        {
            int o = v * ModelBatch.F;
            var p = Vector3.TransformPosition(new Vector3(b.D[o], b.D[o + 1], b.D[o + 2]), m);
            b.D[o] = p.X; b.D[o + 1] = p.Y; b.D[o + 2] = -p.Z;
        }
    }

    // ================= HUD =================
    private void RenderHud(float pt)
    {
        var sb = Sb; var p = Player; var w = World;
        int W = GuiW, H = GuiH;
        // pod wodą: niebieska nakładka; w lawie
        if (CameraMode == 0)
        {
            int bx = MathUtil.FloorToInt(p.X), by = MathUtil.FloorToInt(p.Y + p.EyeHeight), bz = MathUtil.FloorToInt(p.Z);
            if (p.IsHeadInWater()) sb.Fill(0, 0, W, H, 0x40802010);
            if (w.GetBlock(bx, by, bz) == B.Lava) sb.Fill(0, 0, W, H, 0xA00050FF);
            if (p.IsInsideOpaqueBlock() && !p.Spectator) sb.Fill(0, 0, W, H, 0xC0101010);
        }
        NetDrawNameTags(pt);
        // winieta
        sb.Gradient(0, 0, W, H / 6f, 0x30000000, 0x00000000);
        sb.Gradient(0, H - H / 6f, W, H / 6f, 0x00000000, 0x30000000);
        if (p.SleepTimer > 0)
        {
            float a = Math.Min(1f, p.SleepTimer / 100f);
            if (p.SleepTimer > 100) a = 1f - (p.SleepTimer - 100) / 10f;
            sb.Fill(0, 0, W, H, SpriteBatch.Col(16, 16, 32, (int)(a * 204)));
        }
        if (p.Dead) return;
        // celownik (odwrócenie kolorów)
        if (CameraMode == 0 && !p.Spectator)
        {
            sb.Flush();
            GL.BlendFunc(BlendingFactor.OneMinusDstColor, BlendingFactor.OneMinusSrcColor);
            sb.DrawPx(Gui.Icons, W / 2 - 7, H / 2 - 7, 0, 0, 15, 15);
            sb.Flush();
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
        }
        DrawBossBar();
        if (ShowDebug) DrawDebug();
        if (p.Spectator) { DrawChat(); return; }
        // hotbar
        int hx = W / 2 - 91, hy = H - 22;
        sb.DrawPx(Gui.Widgets, hx, hy, 0, 0, 182, 22);
        sb.DrawPx(Gui.Widgets, hx - 1 + p.Inventory.SelectedSlot * 20, hy - 1, 0, 22, 24, 24);
        for (int i = 0; i < 9; i++) DrawItem(p.Inventory.Main[i], hx + 3 + i * 20, hy + 3);
        if (!p.Creative)
        {
            // XP
            int xy = H - 29;
            sb.DrawPx(Gui.Icons, hx, xy, 0, 64, 182, 5);
            int fill = (int)(p.XpProgress * 183);
            if (fill > 0) sb.DrawPx(Gui.Icons, hx, xy, 0, 69, fill, 5);
            if (p.XpLevel > 0)
            {
                string lv = p.XpLevel.ToString();
                float lx = W / 2f - sb.Measure(lv) / 2f, ly = xy - 6;
                sb.Text(lv, lx + 1, ly, 0xFF000000, false); sb.Text(lv, lx - 1, ly, 0xFF000000, false); sb.Text(lv, lx, ly + 1, 0xFF000000, false); sb.Text(lv, lx, ly - 1, 0xFF000000, false);
                sb.Text(lv, lx, ly, 0xFF20FF80, false);
            }
            // serca
            int hp = (int)Math.Ceiling(p.Health);
            bool blink = p.HurtResistantTime > 10 && (p.HurtResistantTime / 3) % 2 == 1;
            var r = new Rng(_tickCount * 312871);
            int heartY = H - 39;
            for (int i = 0; i < 10; i++)
            {
                int x = hx + i * 8, y = heartY;
                if (hp <= 4) y += r.Next(2);
                if (p.RegenEffect > 0 && (int)(_tickCount % 25) == i) y -= 2;
                sb.DrawPx(Gui.Icons, x, y, blink ? GuiTextures.HeartBlinkEmpty : GuiTextures.HeartEmpty, 0, 9, 9);
                if (i * 2 + 1 < hp) sb.DrawPx(Gui.Icons, x, y, GuiTextures.HeartFull, 0, 9, 9);
                else if (i * 2 + 1 == hp) sb.DrawPx(Gui.Icons, x, y, GuiTextures.HeartHalf, 0, 9, 9);
            }
            DrawEffectHearts(p, hx, heartY);
            // zbroja
            int armor = p.ArmorValue;
            if (armor > 0)
                for (int i = 0; i < 10; i++)
                {
                    int x = hx + i * 8, y = heartY - 10;
                    int u = i * 2 + 1 < armor ? 34 : i * 2 + 1 == armor ? 25 : 16;
                    sb.DrawPx(Gui.Icons, x, y, u, 9, 9, 9);
                }
            // głód
            bool hunger = p.HungerEffect > 0;
            for (int i = 0; i < 10; i++)
            {
                int x = W / 2 + 91 - i * 8 - 9, y = heartY;
                if (p.Saturation <= 0 && _tickCount % (p.FoodLevel * 3 + 1) == 0) y += r.Next(3) - 1;
                sb.DrawPx(Gui.Icons, x, y, 16, 27, 9, 9);
                int fu = hunger ? 88 : 52;
                if (i * 2 + 1 < p.FoodLevel) sb.DrawPx(Gui.Icons, x, y, fu, 27, 9, 9);
                else if (i * 2 + 1 == p.FoodLevel) sb.DrawPx(Gui.Icons, x, y, fu + 9, 27, 9, 9);
            }
            // bąbelki
            if (p.IsHeadInWater() || p.Air < 300)
            {
                int full = (int)Math.Ceiling((p.Air - 2) * 10.0 / 300.0);
                int pop = (int)Math.Ceiling(p.Air * 10.0 / 300.0) - full;
                for (int i = 0; i < full + pop; i++)
                    sb.DrawPx(Gui.Icons, W / 2 + 91 - i * 8 - 9, heartY - 10, i < full ? 16 : 25, 18, 9, 9);
            }
        }
        // nazwa przedmiotu
        if (_itemNameTicks > 0 && !string.IsNullOrEmpty(_itemName))
        {
            float a = Math.Min(1f, _itemNameTicks * 256f / 10f / 255f);
            sb.TextCentered(_itemName, W / 2f, H - (p.Creative ? 45 : 59), SpriteBatch.Alpha(0xFFFFFFFF, a));
        }
        DrawChat();
        if (Settings.ShowFps && !ShowDebug) sb.Text(_fps + " fps", 2, 2, 0xFFFFFFFF);
    }

    private void DrawChat()
    {
        bool open = _screen is ChatScreen;
        int H = GuiH;
        int y = H - 48;
        int shown = 0;
        for (int i = _chat.Count - 1; i >= 0 && shown < (open ? 20 : 10); i--)
        {
            var (text, time) = _chat[i];
            long age = _tickCount - time;
            if (!open && age > 200) break;
            float a = open ? 1f : Math.Clamp((200 - age) / 20f, 0, 1);
            Sb.Fill(2, y - 1, 320, 10, SpriteBatch.Col(0, 0, 0, (int)(128 * a)));
            Sb.Text(text, 4, y, SpriteBatch.Alpha(0xFFFFFFFF, a));
            y -= 10; shown++;
        }
    }

    private void DrawDebug()
    {
        var p = Player; var w = World;
        int bx = MathUtil.FloorToInt(p.X), by = MathUtil.FloorToInt(p.Y), bz = MathUtil.FloorToInt(p.Z);
        string[] facing = { "południe (+Z)", "zachód (-X)", "północ (-Z)", "wschód (+X)" };
        var lines = new List<string>
        {
            $"BlockCraft 1.0 ({_fps} fps, {Renderer.PendingMeshes} oczek. mesh)",
            $"C: {Renderer.RenderedSections}/{Renderer.TotalSections} sekcji, E: {w.Entities.Count}, P: {Renderer.Particles.Count}",
            $"Chunki: {w.Chunks.Count} załad., {w.ChunkManager?.PendingCount ?? 0} w kolejce",
            "",
            $"XYZ: {p.X:F3} / {p.Y:F5} / {p.Z:F3}",
            $"Blok: {bx} {by} {bz}   Chunk: {bx & 15} {by & 15} {bz & 15} w {bx >> 4} {by >> 4} {bz >> 4}",
            $"Kierunek: {facing[Dir.HorizFromYaw(p.Yaw)]} ({MathUtil.WrapDegrees(p.Yaw):F1} / {p.Pitch:F1})",
            $"Biom: {Biome.Name(w.GetBiome(bx, bz))}",
            $"Światło: {w.GetLight(bx, by, bz)} (niebo: {w.GetSkyLight(bx, by, bz)}, blok: {w.GetBlockLight(bx, by, bz)})",
            $"Dzień {w.DayTime / 24000}, czas {w.DayTime % 24000}",
            $"Ziarno: {w.Seed}",
        };
        if (Ctrl.Hit.Hit)
        {
            var h = Ctrl.Hit;
            lines.Add($"Cel: {h.X} {h.Y} {h.Z} {Block.Get(w.GetBlock(h.X, h.Y, h.Z)).Key} [{w.GetMeta(h.X, h.Y, h.Z)}]");
        }
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0) continue;
            Sb.Fill(1, 1 + i * 9, Sb.Measure(lines[i]) + 2, 9, 0x90505050);
            Sb.Text(lines[i], 2, 2 + i * 9, 0xFFE0E0E0, false);
        }
    }

    // ================= zrzuty ekranu =================
    public string TakeScreenshot(string path)
    {
        int w = FramebufferSize.X, h = FramebufferSize.Y;
        var px = new byte[w * h * 4];
        GL.ReadBuffer(ReadBufferMode.Back);
        GL.ReadPixels(0, 0, w, h, PixelFormat.Rgba, PixelType.UnsignedByte, px);
        var flip = new byte[px.Length];
        for (int y = 0; y < h; y++) System.Buffer.BlockCopy(px, (h - 1 - y) * w * 4, flip, y * w * 4, w * 4);
        for (int i = 3; i < flip.Length; i += 4) flip[i] = 255;
        if (path == null)
        {
            var dir = Path.Combine(WorldStorage.Root, "screenshots"); Directory.CreateDirectory(dir);
            path = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss") + ".png");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var fs = File.Create(path))
            new StbImageWriteSharp.ImageWriter().WritePng(flip, w, h, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
        if (World != null) AddChat("Zapisano zrzut ekranu jako " + Path.GetFileName(path));
        return path;
    }
}
