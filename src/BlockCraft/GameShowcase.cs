using System.Globalization;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;

namespace BlockCraft;

/// <summary>Nagrywanie scen do README: BlockCraft.exe --showcase (klatki PNG do folderu showcase/&lt;scena&gt;).</summary>
public sealed partial class Game
{
    private const int ShowcaseFrameWidth = 640;
    private readonly bool _showcase;
    private string _recScene;
    private int _recTicks, _recEvery, _recNext, _recIndex;
    private long _recStart, _recSeen;
    private Action<int> _recPerTick;

    private bool Scripted => _autotest || _showcase;

    /// <summary>Nagrywa scenę przez <paramref name="ticks"/> ticków, klatka co <paramref name="every"/> ticków.</summary>
    private void Record(string scene, int ticks, int every, Action start = null, Action<int> perTick = null)
    {
        Step(1, () =>
        {
            start?.Invoke();
            _recScene = scene; _recTicks = ticks; _recEvery = every; _recNext = 0; _recIndex = 0;
            _recStart = _recSeen = _tickCount; _recPerTick = perTick;
            Directory.CreateDirectory(Path.Combine(_shotDir, scene));
        });
        WaitUntil(() => _recScene == null);
    }

    private double? _holdY;
    private readonly List<(int x, int z)> _usedStages = new();

    private void ShowcaseFrame()
    {
        if (_holdY is double hy && Player != null) { Player.Y = Player.PrevY = hy; Player.MotionY = 0; Player.Flying = true; }
        if (_recScene != null)
        {
            while (_recSeen < _tickCount && _recSeen - _recStart < _recTicks)
            {
                _recSeen++;
                try { _recPerTick?.Invoke((int)(_recSeen - _recStart)); } catch (Exception ex) { Log.Error("Showcase: " + ex); }
            }
            if (_recSeen - _recStart >= _recNext) { CaptureFrame(); _recNext += _recEvery; }
            if (_recSeen - _recStart >= _recTicks) { Log.Info($"SHOWCASE {_recScene}: {_recIndex} klatek"); _recScene = null; }
            return;
        }
        AutotestStep();
    }

    /// <summary>Zrzut bieżącej klatki pomniejszony (uśrednianie bloków) do szerokości ShowcaseFrameWidth.</summary>
    private void CaptureFrame()
    {
        int w = FramebufferSize.X, h = FramebufferSize.Y;
        var px = new byte[w * h * 4];
        GL.ReadBuffer(ReadBufferMode.Back);
        GL.ReadPixels(0, 0, w, h, PixelFormat.Rgba, PixelType.UnsignedByte, px);
        int f = Math.Max(1, w / ShowcaseFrameWidth), ow = w / f, oh = h / f;
        var outPx = new byte[ow * oh * 4];
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                int r = 0, g = 0, b = 0;
                for (int sy = 0; sy < f; sy++)
                    for (int sx = 0; sx < f; sx++)
                    {
                        int i = (((h - 1 - (y * f + sy)) * w) + x * f + sx) * 4;
                        r += px[i]; g += px[i + 1]; b += px[i + 2];
                    }
                int n = f * f, o = (y * ow + x) * 4;
                outPx[o] = (byte)(r / n); outPx[o + 1] = (byte)(g / n); outPx[o + 2] = (byte)(b / n); outPx[o + 3] = 255;
            }
        string path = Path.Combine(_shotDir, _recScene, (_recIndex++).ToString("D4") + ".png");
        using var fs = File.Create(path);
        new StbImageWriteSharp.ImageWriter().WritePng(outPx, ow, oh, StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha, fs);
    }

    private void Snap(string name) { _chat.Clear(); Shot(name); _chat.Clear(); }

    private int GroundY(int x, int z)
    {
        int y = Chunk.Height - 1;
        while (y > 0 && !Block.Get(World.GetBlock(x, y, z)).Solid) y--;
        return y;
    }

    private void Summon(string mob, double x, double y, double z) =>
        Cmds.Execute(Player, $"/summon {mob} {x.ToString(CultureInfo.InvariantCulture)} {y.ToString(CultureInfo.InvariantCulture)} {z.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Szuka płaskiego, trawiastego terenu (bez wody) w pobliżu (x0,z0), z dala od wcześniej użytych scen.</summary>
    private (int x, int z) FindFlat(int x0, int z0, int radius = 8, int search = 48)
    {
        (int x, int z) best = (x0, z0); int bestScore = int.MaxValue;
        for (int dx = -search; dx <= search; dx += 4)
            for (int dz = -search; dz <= search; dz += 4)
            {
                int x = x0 + dx, z = z0 + dz;
                if (_usedStages.Any(u => Math.Abs(u.x - x) < 28 && Math.Abs(u.z - z) < 28)) continue;
                if (World.GetChunk(x >> 4, z >> 4) == null) continue;
                int g = GroundY(x, z);
                if (World.GetBlock(x, g, z) != B.Grass) continue;
                int score = 0;
                for (int ox = -radius; ox <= radius && score < bestScore; ox += 2)
                    for (int oz = -radius; oz <= radius; oz += 2)
                    {
                        int gy = GroundY(x + ox, z + oz);
                        byte top = World.GetBlock(x + ox, gy, z + oz), above = World.GetBlock(x + ox, gy + 1, z + oz);
                        score += Math.Abs(gy - g) * 4 + (Block.Get(top).IsLiquid || Block.Get(above).IsLiquid ? 50 : 0) + (top == B.OakLeaves || top == B.BirchLeaves ? 6 : 0);
                    }
                score += (Math.Abs(dx) + Math.Abs(dz)) / 8;
                if (score < bestScore) { bestScore = score; best = (x, z); }
            }
        _usedStages.Add(best);
        return best;
    }

    /// <summary>Kierunek "przód" gracza zaokrąglony do osi świata oraz "prawo".</summary>
    private (int fx, int fz, int rx, int rz) Axes()
    {
        var (lx, _, lz) = Player.Look();
        int fx = 0, fz = 0;
        if (Math.Abs(lx) > Math.Abs(lz)) fx = Math.Sign(lx); else fz = Math.Sign(lz);
        return (fx, fz, -fz, fx);
    }

    /// <summary>Ustawia gracza na ziemi w (x,z) patrzącego wzdłuż osi (yaw), z wyczyszczonym terenem przed nim.</summary>
    private (int x, int y, int z) Stage(int x, int z, float yaw, float pitch, int clear = 0, double lift = 0)
    {
        (x, z) = FindFlat(x, z, Math.Max(clear, 6));
        int y = GroundY(x, z) + 1;
        Player.Flying = false;
        _holdY = lift > 0 ? y + lift : null;
        foreach (var e in World.Entities.ToList()) if (e is not Entities.Player) e.Kill();
        Player.SetPositionAndPrev(x + 0.5, y, z + 0.5);
        Player.Yaw = Player.PrevYaw = yaw; Player.Pitch = Player.PrevPitch = pitch;
        if (clear > 0)
            for (int dx = -clear; dx <= clear; dx++)
                for (int dz = -clear; dz <= clear; dz++)
                {
                    World.SetBlock(x + dx, y - 1, z + dz, B.Grass);
                    for (int dy = 0; dy < 8; dy++) World.SetBlock(x + dx, y + dy, z + dz, B.Air);
                    for (int dy = 2; dy < 6; dy++) World.SetBlock(x + dx, y - dy, z + dz, B.Dirt);
                }
        return (x, y, z);
    }

    private void WaitForMeshes(int maxFrames = 400)
    {
        int t = 0;
        WaitUntil(() => ++t > maxFrames || t > 60 && Renderer.PendingMeshes < 4);
    }

    private void FaceEast()
    {
        float best = 0; double bestX = -2;
        for (int yaw = 0; yaw < 360; yaw += 90)
        {
            Player.Yaw = yaw;
            var (lx, _, _) = Player.Look();
            if (lx > bestX) { bestX = lx; best = yaw; }
        }
        Player.Yaw = Player.PrevYaw = best;
    }

    private void SetupShowcase()
    {
        _shotDir = Path.Combine(AppContext.BaseDirectory, "showcase");
        if (Directory.Exists(_shotDir)) Directory.Delete(_shotDir, true);
        Directory.CreateDirectory(_shotDir);
        bool showFps = Settings.ShowFps;
        int t0 = 0;
        WaitUntil(() => HasMenuWorld && Renderer.PendingMeshes < 20 || ++t0 > 600);
        Step(30, () => { });
        Record("title", 120, 2);
        Step(5, () => SetScreen(new CreateWorldScreen(new TitleScreen())));
        Step(10, () => Snap("screen_create"));
        Step(5, () => SetScreen(new VideoScreen(new TitleScreen())));
        Step(10, () => Snap("screen_video"));
        Step(5, () => CreateWorld("Showcase " + DateTime.Now.ToString("HHmmss"), 12345, 1, false, WorldType.Default, true));
        int t1 = 0;
        WaitUntil(() => _sessionStarted && Renderer.PendingMeshes < 10 || ++t1 > 1500);
        int sx = 0, sz = 0;
        Step(30, () =>
        {
            World.Rules.DoDaylightCycle = false; World.Rules.DoMobSpawning = false; World.DayTime = 6000;
            HideGui = true; Player.Invulnerable = true; Settings.ShowFps = false;
            sx = MathUtil.FloorToInt(Player.X); sz = MathUtil.FloorToInt(Player.Z);
        });

        // przelot nad terenem
        Step(5, () =>
        {
            Player.SetPositionAndPrev(sx + 0.5, GroundY(sx, sz) + 24, sz + 0.5);
            _holdY = Player.Y;
            Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 24;
        });
        WaitForMeshes();
        Record("flyover", 160, 2,
            () => _autoInput = (true, false, false, false),
            t => Player.Yaw += 0.35f);
        Step(5, () => _autoInput = null);

        // dzień i noc – widok na wschód z wysokości
        Step(5, () => { Stage(sx, sz - 30, 0, -6, 0, 12); FaceEast(); });
        WaitForMeshes();
        Record("daycycle", 240, 2, () => World.DayTime = 22000, t => World.DayTime = 22000 + t * 100L);
        Step(5, () => World.DayTime = 6000);

        // Steve – kamera z przodu, obrót dookoła
        Step(5, () =>
        {
            Stage(sx + 30, sz, 0, 12, 5);
            Player.Inventory.Main[0] = new ItemStack(I.DiamondSword); Player.Inventory.SelectedSlot = 0;
            Player.Inventory.Armor[1] = new ItemStack(I.IronChest);
            CameraMode = 2;
        });
        WaitForMeshes();
        Record("steve", 120, 2, null, t => Player.Yaw += 3f);
        Step(5, () => { CameraMode = 0; Player.Inventory.Armor[1] = null; });

        // kopanie i budowanie (przetrwanie, z HUD)
        Step(5, () =>
        {
            Cmds.Execute(Player, "/gamemode survival");
            Stage(sx - 30, sz, 0, 60, 6);
            Player.Inventory.Clear();
            Player.Inventory.Main[0] = new ItemStack(I.IronShovel); Player.Inventory.Main[1] = new ItemStack(B.OakPlanks, 64);
            Player.Inventory.Main[2] = new ItemStack(B.Torch, 16); Player.Inventory.Main[3] = new ItemStack(I.DiamondPickaxe);
            Player.Inventory.Main[4] = new ItemStack(B.Cobblestone, 64); Player.Inventory.Main[5] = new ItemStack(I.Bread, 12);
            Player.Inventory.SelectedSlot = 0;
            HideGui = false; _chat.Clear();
        });
        WaitForMeshes();
        Record("mining", 220, 2, null, t =>
        {
            if (t < 120) { _leftDown = true; Player.Yaw += t % 40 < 20 ? 1.2f : -1.2f; Player.Pitch = 60 - t * 0.12f; }
            else if (t == 120) { _leftDown = false; Player.Inventory.SelectedSlot = 1; }
            else if (t > 130) { _rightDown = t % 12 < 2; Player.Yaw -= 0.9f; Player.Pitch = 45 - (t - 130) * 0.15f; }
        });
        Step(5, () => _leftDown = _rightDown = false);

        // zrzuty ekranu: HUD, ekwipunek, crafting, piec, jaskinia
        Step(5, () =>
        {
            Stage(sx + 60, sz - 10, 0, 8, 3);
            FaceEast();
            Player.Inventory.SelectedSlot = 3;
            Player.Inventory.Armor[0] = new ItemStack(I.IronHelmet); Player.Inventory.Armor[1] = new ItemStack(I.IronChest);
            Player.Inventory.Main[6] = new ItemStack(I.DiamondSword); Player.Inventory.Main[7] = new ItemStack(B.CraftingTable);
            Player.Inventory.Main[8] = new ItemStack(B.Furnace); Player.Inventory.Main[12] = new ItemStack(B.OakLog, 24);
            Player.Inventory.Main[15] = new ItemStack(I.Apple, 7); Player.Inventory.Main[20] = new ItemStack(B.Tnt, 5);
            Player.Inventory.Main[23] = new ItemStack(I.IronPickaxe);
            Player.Health = 17; Player.FoodLevel = 15;
        });
        WaitForMeshes();
        Step(20, () => Snap("screen_hud"));
        Step(5, () => SetScreen(new ContainerScreen(Player, Player.InventoryContainer)));
        Step(10, () => Snap("screen_inventory"));
        Step(5, () =>
        {
            SetScreen(null);
            int x = MathUtil.FloorToInt(Player.X) - 2, z = MathUtil.FloorToInt(Player.Z), y = MathUtil.FloorToInt(Player.Y);
            World.SetBlock(x, y, z, B.CraftingTable, 0);
            var wb = new WorkbenchContainer(Player.Inventory, x, y, z);
            wb.CraftGrid.Set(0, new ItemStack(I.Diamond)); wb.CraftGrid.Set(1, new ItemStack(I.Diamond)); wb.CraftGrid.Set(2, new ItemStack(I.Diamond));
            wb.CraftGrid.Set(4, new ItemStack(I.Stick)); wb.CraftGrid.Set(7, new ItemStack(I.Stick)); wb.UpdateCrafting();
            Player.OpenContainer = wb; SetScreen(new ContainerScreen(Player, wb));
        });
        Step(10, () => Snap("screen_crafting"));
        Step(5, () =>
        {
            SetScreen(null);
            int x = MathUtil.FloorToInt(Player.X) + 2, z = MathUtil.FloorToInt(Player.Z), y = MathUtil.FloorToInt(Player.Y);
            World.SetBlock(x, y, z, B.Furnace, 0);
            OpenBlockGui(GuiKind.Furnace, x, y, z);
        });
        Step(10, () => Snap("screen_furnace"));
        Step(5, () =>
        {
            SetScreen(null);
            int x = MathUtil.FloorToInt(Player.X), z = MathUtil.FloorToInt(Player.Z);
            for (int dx = -4; dx <= 4; dx++) for (int dz = -4; dz <= 4; dz++) for (int dy = 0; dy < 5; dy++)
                World.SetBlock(x + dx, 20 + dy, z + dz, dy == 0 ? B.Stone : B.Air);
            World.SetBlock(x + 2, 21, z - 2, B.Torch, 0); World.SetBlock(x - 3, 21, z + 1, B.Torch, 0);
            byte[] ores = { B.CoalOre, B.DiamondOre, B.IronOre, B.GoldOre, B.RedstoneOre, B.LapisOre, B.DiamondOre };
            for (int i = 0; i < ores.Length; i++) World.SetBlock(x - 3 + i, 21 + i % 2, z - 4, ores[i]);
            World.SetBlock(x + 1, 21, z - 3, B.Lava, 0);
            Player.FallDistance = 0; Player.SetPositionAndPrev(x + 0.5, 21, z + 2.5);
            Player.Yaw = Player.PrevYaw = 180; Player.Pitch = Player.PrevPitch = 12;
            Player.Inventory.SelectedSlot = 3;
        });
        WaitForMeshes(200);
        Step(20, () => Snap("screen_cave"));
        Step(5, () =>
        {
            Cmds.Execute(Player, "/gamemode creative");
            Stage(sx + 60, sz + 20, 0, 10, 3);
            FaceEast();
        });
        WaitForMeshes();
        Step(10, () => { _chat.Clear(); SetScreen(new CreativeScreen(Player)); });
        Step(10, () => Snap("screen_creative"));
        Step(5, () => { SetScreen(null); ShowDebug = true; });
        Step(10, () => Snap("screen_debug"));
        Step(5, () =>
        {
            ShowDebug = false;
            AddChat("§eWitaj w świecie Showcase! Wpisz /help aby zobaczyć komendy.");
            AddChat("<Steve> Zażółć gęślą jaźń!");
            Cmds.Execute(Player, "/time set noon");
            SetScreen(new ChatScreen("/summon creeper"));
        });
        Step(10, () => { Shot("screen_chat"); _chat.Clear(); });
        Step(5, () => { SetScreen(null); HideGui = true; _chat.Clear(); World.DayTime = 6000; });

        // TNT
        var tnt = new List<(int x, int y, int z)>();
        Step(5, () =>
        {
            var (x, y, z) = Stage(sx, sz + 40, 0, 22, 12, 4);
            var (fx, fz, rx, rz) = Axes();
            tnt.Clear();
            for (int f = 9; f <= 11; f++)
                for (int r = -1; r <= 1; r++)
                    for (int dy = 0; dy < 2; dy++)
                    {
                        var p = (x + fx * f + rx * r, y + dy, z + fz * f + rz * r);
                        World.SetBlock(p.Item1, p.Item2, p.Item3, B.Tnt); tnt.Add(p);
                    }
        });
        WaitForMeshes();
        Record("tnt", 130, 2, null, t =>
        {
            if (t != 10) return;
            foreach (var (bx, by, bz) in tnt)
                if (World.GetBlock(bx, by, bz) == B.Tnt) TntBlock.Ignite(World, bx, by, bz, 30 + World.Rand.Next(20));
        });

        // woda spływająca z platformy
        (int x, int y, int z) spring = default;
        Step(5, () =>
        {
            var (x, y, z) = Stage(sx + 40, sz + 40, 0, 28, 10, 6);
            var (fx, fz, rx, rz) = Axes();
            int cx = x + fx * 10, cz = z + fz * 10, py = y + 3;
            for (int f = -2; f <= 2; f++)
                for (int r = -2; r <= 2; r++)
                    World.SetBlock(cx + fx * f + rx * r, py, cz + fz * f + rz * r, B.Stone);
            for (int dy = 0; dy < 3; dy++) World.SetBlock(cx, y + dy, cz, B.Stone);
            spring = (cx, py + 1, cz);
        });
        WaitForMeshes();
        Record("water", 160, 2, null, t => { if (t == 6) World.SetBlock(spring.x, spring.y, spring.z, B.Water, 0); });

        // redstone: dźwignia -> pył -> lampa + lepki tłok
        (int x, int y, int z) lever = default;
        Step(5, () =>
        {
            var (x, y, z) = Stage(sx - 40, sz + 40, 0, 38, 9, 3.5);
            var (fx, fz, rx, rz) = Axes();
            void Set(int f, int r, int dy, byte id, int meta = 0) => World.SetBlock(x + fx * f + rx * r, y + dy, z + fz * f + rz * r, id, meta);
            for (int f = 3; f <= 9; f++) for (int r = -4; r <= 4; r++) Set(f, r, -1, B.StoneBricks);
            Set(5, -3, 0, B.Lever, Dir.Down);
            for (int r = -2; r <= 1; r++) Set(5, r, 0, B.RedstoneWire);
            Set(5, 2, 0, B.RedstoneLamp);
            Set(6, 0, 0, B.RedstoneWire); Set(7, 0, 0, B.RedstoneWire);
            Set(8, 0, 0, B.StickyPiston, Dir.Up); Set(8, 0, 1, B.GoldBlock);
            lever = (x + fx * 5 - rx * 3, y, z + fz * 5 - rz * 3);
        });
        WaitForMeshes();
        Record("redstone", 160, 2, null, t =>
        {
            if (t % 40 == 10) Block.Get(B.Lever).OnActivated(World, lever.x, lever.y, lever.z, Player);
        });
        Step(5, () =>
        {
            if ((World.GetMeta(lever.x, lever.y, lever.z) & 8) == 0) Block.Get(B.Lever).OnActivated(World, lever.x, lever.y, lever.z, Player);
            World.DayTime = 18000;
        });
        Step(20, () => Snap("screen_night_redstone"));
        Step(5, () => World.DayTime = 6000);

        // zwierzęta
        Step(5, () =>
        {
            Stage(sx + 40, sz - 40, 0, 22, 10, 1.6);
            var (fx, fz, rx, rz) = Axes();
            string[] mobs = { "cow", "pig", "sheep", "chicken", "cow", "sheep", "pig", "chicken" };
            for (int i = 0; i < mobs.Length; i++)
            {
                double f = 5 + (i / 4) * 3, r = (i % 4 - 1.5) * 2.2;
                Summon(mobs[i], Player.X + fx * f + rx * r, GroundY(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z)) + 1, Player.Z + fz * f + rz * r);
            }
        });
        WaitForMeshes(100);
        Record("animals", 140, 2);

        // potwory płonące w słońcu
        Step(5, () =>
        {
            Stage(sx - 40, sz - 40, 0, 15, 10, 2);
            var (fx, fz, rx, rz) = Axes();
            string[] mobs = { "zombie", "skeleton", "creeper", "spider", "enderman", "zombie", "skeleton" };
            for (int i = 0; i < mobs.Length; i++)
            {
                double f = 7 + (i % 2) * 2.5, r = (i - 3) * 1.9;
                Summon(mobs[i], Player.X + fx * f + rx * r, GroundY(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z)) + 1, Player.Z + fz * f + rz * r);
            }
        });
        WaitForMeshes(100);
        Record("monsters", 120, 2);

        // zrzuty: zachód słońca, pustynia
        Step(5, () =>
        {
            foreach (var e in World.Entities.ToList()) if (e is not Entities.Player) e.Kill();
            Stage(sx - 10, sz + 70, 0, -4, 0, 10);
            FaceEast(); Player.Yaw = Player.PrevYaw = Player.Yaw + 180;
            World.DayTime = 12300;
        });
        WaitForMeshes();
        Step(20, () => Snap("screen_sunset"));
        Step(5, () =>
        {
            World.DayTime = 6000;
            var g = World.Generator; bool found = false;
            for (int r = 32; r < 600 && !found; r += 32)
                for (int a = 0; a < 16 && !found; a++)
                {
                    int x = sx + (int)(Math.Cos(a * Math.PI / 8) * r), z = sz + (int)(Math.Sin(a * Math.PI / 8) * r);
                    if (g.BiomeAt(x, z) == Biome.Desert && g.BiomeAt(x + 40, z) == Biome.Desert && g.BiomeAt(x, z + 40) == Biome.Desert)
                    {
                        Player.SetPositionAndPrev(x + 0.5, 100, z + 0.5); _holdY = 100; found = true;
                    }
                }
            Player.Yaw = Player.PrevYaw = 0; Player.Pitch = Player.PrevPitch = 22;
        });
        int t2 = 0;
        WaitUntil(() => ++t2 > 400 || World.GetChunk(MathUtil.FloorToInt(Player.X) >> 4, MathUtil.FloorToInt(Player.Z) >> 4)?.Merged == true && Renderer.PendingMeshes < 4 && t2 > 120);
        Step(20, () =>
        {
            int gy = GroundY(MathUtil.FloorToInt(Player.X), MathUtil.FloorToInt(Player.Z));
            _holdY = gy + 14; Player.SetPositionAndPrev(Player.X, gy + 14, Player.Z);
        });
        WaitForMeshes(200);
        Step(20, () => Snap("screen_desert"));
        Step(5, () =>
        {
            _holdY = null; Settings.ShowFps = showFps;
            foreach (var e in World.Entities.ToList()) if (e is not Entities.Player) e.Kill();
        });
        Step(5, () => { Log.Info("SHOWCASE DONE"); Close(); });
    }
}
