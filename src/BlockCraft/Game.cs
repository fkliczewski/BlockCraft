using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Render;
using BlockCraft.World;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace BlockCraft;

public sealed partial class Game : GameWindow, IPlayerHandler
{
    public Settings Settings;
    public SpriteBatch Sb;
    public GuiTextures Gui;
    public WorldRenderer Renderer;
    public IconAtlas Icons;
    public World.World World;
    public Player Player;
    public PlayerController Ctrl;
    public Commands Cmds;
    private WorldStorage _storage;
    private LevelData _level;
    private Screen _screen;
    public int GuiW, GuiH; public float GuiScale = 2;
    public int MouseGuiX, MouseGuiY;
    public bool IsShiftDown => KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
    public bool ShowDebug, HideGui;
    public int CameraMode;
    private readonly List<(string text, long time)> _chat = new();
    private double _acc;
    private long _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTime;
    private int _fps, _fpsCount; private double _fpsTimer;
    private bool _leftDown, _rightDown, _middlePressed;
    private bool _leftPressed, _rightPressed;
    private int _lastFwdTap = -100, _lastJumpTap = -100; private long _tickCount;
    private bool _prevFwd, _prevJump;
    private float _equip = 1, _prevEquip = 1; private ItemStack _equipped; private int _equippedSlot;
    private int _itemNameTicks; private string _itemName = "";
    private float _fovMod = 1f, _prevFovMod = 1f;
    private World.World _menuWorld; private ChunkManager _menuCm; private float _menuYaw;
    public bool HasMenuWorld => _menuWorld != null && Renderer.RenderedSections > 20;
    public int CurrentDifficulty { get => World != null ? (int)World.Difficulty : Settings.Difficulty; set { if (World != null) World.Difficulty = (Difficulty)value; Settings.Difficulty = value; } }
    private readonly bool _autotest;
    private readonly string[] _args;

    public Game(string[] args) : base(new GameWindowSettings { UpdateFrequency = 0 },
        new NativeWindowSettings { ClientSize = new Vector2i(1280, 720), Title = "BlockCraft", APIVersion = new Version(3, 3), Profile = ContextProfile.Core, Flags = ContextFlags.ForwardCompatible, StartVisible = true })
    {
        _args = args;
        _autotest = args.Contains("--autotest") || args.Contains("--autotest-host") || args.Contains("--autotest-join");
        int profile = Array.IndexOf(args, "--net-profile");
        if (profile >= 0 && profile + 1 < args.Length)
        {
            var name = args[profile + 1];
            if (name.Length > 48 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
                throw new ArgumentException("Profil sieci może zawierać litery ASCII, cyfry, '-' i '_' (do 48 znaków).");
            Net.LinkSetup.Profile = name;
        }
        _showcase = args.Contains("--showcase");
        Settings = Settings.Load();
        VSync = Settings.VSync ? VSyncMode.On : VSyncMode.Off;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        Log.Info("Start BlockCraft, GL: " + GL.GetString(StringName.Renderer) + " / " + GL.GetString(StringName.Version));
        SharedQuadIndices.Init();
        ItemRegistry.Init(); Recipes.Init(); Entities.Registries.Init();
        Sb = new SpriteBatch();
        Gui = new GuiTextures();
        Renderer = new WorldRenderer();
        Icons = new IconAtlas(Renderer.Entities, Renderer.Terrain);
        Sound.Init(Settings);
        Renderer.SoundHook = (n, x, y, z, v, p) => { if (World != null && _sessionStarted) Sound.Play(n, x, y, z, v, p); };
        UpdateGuiScale();
        ApplySettings();
        StartMenuWorld();
        SetScreen(new TitleScreen());
        _lastTime = _clock.Elapsed.TotalSeconds;
        if (ArgsNetAutotest) SetupNetAutotest();
        else if (_autotest) SetupAutotest();
        else if (_showcase) SetupShowcase();
    }

    // ================= ustawienia / GUI =================
    public void ApplySettings()
    {
        VSync = Settings.VSync ? VSyncMode.On : VSyncMode.Off;
        ChunkMesher.SmoothLighting = Settings.SmoothLighting;
        bool fancyChanged = ChunkMesher.FancyLeaves != Settings.FancyGraphics;
        ChunkMesher.FancyLeaves = Settings.FancyGraphics;
        int rdOld = Renderer.RenderDistance;
        Renderer.RenderDistance = Settings.RenderDistance;
        Renderer.CloudsEnabled = Settings.Clouds;
        Renderer.Gamma = Settings.Brightness;
        if (World?.ChunkManager != null) World.ChunkManager.Radius = Settings.RenderDistance;
        if (World != null && Client == null) World.Difficulty = (Difficulty)Settings.Difficulty; // gość: trudność ustala host
        if (fancyChanged || _lastSmooth != Settings.SmoothLighting) MarkAllDirty();
        _lastSmooth = Settings.SmoothLighting;
        UpdateGuiScale();
    }
    private bool _lastSmooth = true;

    private void MarkAllDirty()
    {
        var w = World ?? _menuWorld;
        if (w == null) return;
        foreach (var c in w.Chunks.Values) c.MarkAllDirty();
    }

    private void UpdateGuiScale()
    {
        int fw = Math.Max(1, FramebufferSize.X), fh = Math.Max(1, FramebufferSize.Y);
        int max = 1;
        while (max < 6 && fw / (max + 1) >= 320 && fh / (max + 1) >= 240) max++;
        int s = Settings.GuiScale == 0 ? max : Math.Min(Settings.GuiScale, max);
        GuiScale = s;
        GuiW = (int)Math.Ceiling(fw / GuiScale); GuiH = (int)Math.Ceiling(fh / GuiScale);
        if (_screen != null) _screen.Open(this, GuiW, GuiH);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        UpdateGuiScale();
    }

    public void SetScreen(Screen s)
    {
        var old = _screen;
        _screen = null;
        old?.OnClose();
        if (s == null && World == null) s = new TitleScreen();
        _screen = s;
        s?.Open(this, GuiW, GuiH);
        UpdateCursor();
        _leftDown = _rightDown = false;
        if (Ctrl != null) Ctrl.ResetBreak();
    }
    public Screen CurrentScreen => _screen;

    private void UpdateCursor()
    {
        bool grab = _screen == null && World != null && IsFocused;
        CursorState = grab ? CursorState.Grabbed : CursorState.Normal;
    }

    public void PlayUiClick() => Sound.PlayUi("random.click", 1f, 1f);
    public string GetClipboard() { try { return ClipboardString; } catch { return null; } }
    public void Quit() => Close();

    // ================= świat menu =================
    private void StartMenuWorld()
    {
        try
        {
            _menuWorld = new World.World(9876543210L + DateTime.Now.DayOfYear, WorldType.Default, false);
            var sp = _menuWorld.Generator.FindSpawn();
            _menuWorld.SpawnX = sp.x; _menuWorld.SpawnY = sp.y; _menuWorld.SpawnZ = sp.z;
            _menuWorld.DayTime = 3000;
            _menuWorld.Rules.DoMobSpawning = false;
            _menuCm = new ChunkManager(_menuWorld) { Radius = Math.Min(6, Settings.RenderDistance) };
            _menuWorld.ChunkManager = _menuCm;
            Renderer.SetWorld(_menuWorld);
        }
        catch (Exception ex) { Log.Error("Menu world: " + ex); _menuWorld = null; }
    }
    private void StopMenuWorld()
    {
        if (_menuWorld == null) return;
        _menuCm?.Dispose(); _menuCm = null;
        if (Renderer.World == _menuWorld) Renderer.SetWorld(null);
        _menuWorld = null;
    }

    // ================= sesja gry =================
    public void CreateWorld(string name, long seed, int gameMode, bool hardcore, WorldType type, bool cheats)
    {
        string dir = Path.Combine(WorldStorage.SavesRoot, WorldStorage.SafeFolderName(name));
        var level = new LevelData { Name = name, Seed = seed, Type = (int)type, GameMode = gameMode, Hardcore = hardcore, AllowCheats = cheats, DayTime = 0, Difficulty = hardcore ? 3 : Settings.Difficulty };
        var storage = new WorldStorage(dir);
        storage.SaveLevel(level);
        StartSession(storage, level, true);
    }

    public void LoadWorld(string dir)
    {
        var level = WorldStorage.LoadLevel(dir);
        if (level == null) return;
        StartSession(new WorldStorage(dir), level, false);
    }

    private LoadingScreen _loading;
    private int _loadTicks;

    private void StartSession(WorldStorage storage, LevelData level, bool isNew)
    {
        StopMenuWorld();
        _storage = storage; _level = level;
        var w = new World.World(level.Seed, (WorldType)level.Type, false)
        {
            Name = level.Name, AllowCheats = level.AllowCheats, Hardcore = level.Hardcore, DayTime = level.DayTime, TotalTime = level.Time,
            Difficulty = (Difficulty)level.Difficulty, Storage = storage
        };
        foreach (var kv in level.Rules) w.Rules.Set(kv.Key, kv.Value.ToString(), out _);
        w.RestoreWeather(level.Weather);
        if (isNew || (level.SpawnX == 0 && level.SpawnY == 0 && level.SpawnZ == 0))
        {
            var sp = w.Generator.FindSpawn();
            w.SpawnX = sp.x; w.SpawnY = sp.y; w.SpawnZ = sp.z;
        }
        else { w.SpawnX = level.SpawnX; w.SpawnY = level.SpawnY; w.SpawnZ = level.SpawnZ; }
        StartDimensions(w);
        if (level.Player?.Dimension is -1 or 1) w = Dimensions.Get((DimensionId)level.Player.Dimension);
        var p = new Player(w) { Name = Settings.PlayerName, Handler = this, IsLocal = true, IsLocalOp = level.AllowCheats };
        if (level.Player != null) p.FromData(level.Player);
        else
        {
            p.SetGameMode(level.GameMode);
            p.SetPositionAndPrev(w.SpawnX + 0.5, w.SpawnY + 0.1, w.SpawnZ + 0.5);
        }
        w.ChunkManager = new ChunkManager(w) { Radius = Settings.RenderDistance };
        World = w; Player = p;
        Ctrl = new PlayerController(this);
        Cmds = new Commands(w);
        Renderer.SetWorld(w);
        _chat.Clear();
        _loading = new LoadingScreen { Title = "Wczytywanie świata", Sub = "Generowanie terenu..." };
        _loadTicks = 0;
        SetScreen(_loading);
        _sessionStarted = false;
    }
    private bool _sessionStarted;

    private void FinishLoading()
    {
        var w = World; var p = Player;
        // znajdź bezpieczną wysokość jeśli nowy gracz
        if (_level.Player == null)
        {
            int x = MathUtil.FloorToInt(p.X), z = MathUtil.FloorToInt(p.Z);
            int y = Chunk.Height - 1;
            while (y > 0 && !Block.Get(w.GetBlock(x, y, z)).Solid && !Block.Get(w.GetBlock(x, y, z)).IsLiquid) y--;
            p.SetPositionAndPrev(x + 0.5, y + 1, z + 0.5);
            w.SpawnY = y + 1;
        }
        w.AddEntityNow(p);
        _sessionStarted = true;
        if (p.Health <= 0) { p.Dead = true; OnDeath(p.DeathMessage ?? ""); return; }
        SetScreen(null);
        AddChat("§eWitaj w świecie " + w.Name + "! Wpisz /help aby zobaczyć komendy.");
    }

    public void SaveWorld()
    {
        if (World == null || _storage == null || !_sessionStarted) return;
        Server?.SavePlayers();
        try
        {
            var home = Dimensions?.Get(DimensionId.Overworld) ?? World;
            _level.DayTime = home.DayTime; _level.Time = home.TotalTime;
            _level.Weather = home.Weather;
            _level.SpawnX = home.SpawnX; _level.SpawnY = home.SpawnY; _level.SpawnZ = home.SpawnZ;
            _level.Difficulty = (int)World.Difficulty;
            _level.GameMode = Player.GameMode;
            _level.Player = Player.ToData();
            _level.Rules = new Dictionary<string, bool>
            {
                ["doDaylightCycle"] = World.Rules.DoDaylightCycle, ["keepInventory"] = World.Rules.KeepInventory, ["doMobSpawning"] = World.Rules.DoMobSpawning,
                ["mobGriefing"] = World.Rules.MobGriefing, ["doFireTick"] = World.Rules.DoFireTick, ["naturalRegeneration"] = World.Rules.NaturalRegeneration, ["doEntityDrops"] = World.Rules.DoEntityDrops
            };
            _storage.SaveLevel(_level);
            Dimensions.SaveAll();
        }
        catch (Exception ex) { Log.Error("Zapis świata: " + ex); }
    }

    public void QuitToTitle()
    {
        NetShutdown();
        SaveWorld();
        Dimensions?.Dispose(); Dimensions = null;
        Renderer.SetWorld(null);
        World = null; Player = null; Ctrl = null; _storage = null;
        StartMenuWorld();
        SetScreen(new TitleScreen());
    }

    public void Respawn()
    {
        if (Player == null) return;
        if (Client != null) { Client.SendRespawn(); return; } // odrodzenie robi host
        if (World.Hardcore) { Player.Respawn(); Player.SetGameMode(3); Player.SetPositionAndPrev(Player.X, Player.Y + 2, Player.Z); }
        else Player.Respawn();
        if (!World.Entities.Contains(Player)) World.AddEntityNow(Player);
        SetScreen(null);
    }

    // ================= IPlayerHandler =================
    public void OpenBlockGui(GuiKind kind, int x, int y, int z)
    {
        if (Client != null) return; // klient sieciowy: okno otworzy host (S2C.OpenWindow)
        var p = Player;
        var c = ContainerFactory.Create(World, p, kind, x, y, z);
        if (c == null) return;
        p.OpenContainer = c;
        SetScreen(ContainerFactory.ScreenFor(p, c));

    }
    public void OnMessage(string msg) => AddChat(msg);
    public void OnDeath(string message) { SetScreen(new DeathScreen(message)); }
    public void OnPickup(Entity item, Entity collector) { }

    public void AddChat(string s)
    {
        foreach (var line in s.Split('\n')) _chat.Add((line, _tickCount));
        if (_chat.Count > 100) _chat.RemoveRange(0, _chat.Count - 100);
        Log.Info("[CHAT] " + s);
    }

    public void SendChat(string s)
    {
        if (World == null || Player == null) return;
        if (Client != null) { if (s.StartsWith("/")) AddChat("§7" + s); Client.SendChat(s); return; } // czat i komendy wykonuje host
        if (s.StartsWith("/")) { AddChat("§7" + s); Cmds.Execute(Player, s); }
        else World.BroadcastMessage($"<{Player.Name}> {s}");
    }

    // ================= wejście =================
    protected override void OnFocusedChanged(FocusedChangedEventArgs e)
    {
        base.OnFocusedChanged(e);
        if (!e.IsFocused && World != null && _screen == null && !Scripted) SetScreen(new PauseScreen());
        UpdateCursor();
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        float sx = FramebufferSize.X / (float)Math.Max(1, ClientSize.X), sy = FramebufferSize.Y / (float)Math.Max(1, ClientSize.Y);
        MouseGuiX = (int)(e.X * sx / GuiScale); MouseGuiY = (int)(e.Y * sy / GuiScale);
        _screen?.MouseMove(MouseGuiX, MouseGuiY);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        int btn = e.Button == MouseButton.Left ? 0 : e.Button == MouseButton.Right ? 1 : e.Button == MouseButton.Middle ? 2 : -1;
        if (btn < 0) return;
        if (_screen != null) { _screen.MouseDown(MouseGuiX, MouseGuiY, btn); return; }
        if (btn == 0) { _leftDown = true; _leftPressed = true; }
        if (btn == 1) { _rightDown = true; _rightPressed = true; }
        if (btn == 2) _middlePressed = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        int btn = e.Button == MouseButton.Left ? 0 : e.Button == MouseButton.Right ? 1 : 2;
        if (btn == 0) _leftDown = false;
        if (btn == 1) _rightDown = false;
        _screen?.MouseUp(MouseGuiX, MouseGuiY, btn);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_screen != null) { _screen.Scroll(MouseGuiX, MouseGuiY, e.OffsetY); return; }
        if (Player == null) return;
        int d = e.OffsetY > 0 ? -1 : e.OffsetY < 0 ? 1 : 0;
        if (Player.Spectator) { Player.FlySpeed = Math.Clamp(Player.FlySpeed - d * 0.005f, 0.01f, 0.2f); return; }
        Player.Inventory.SelectedSlot = (Player.Inventory.SelectedSlot + d + 9) % 9;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_ignoreNextChar) { _ignoreNextChar = false; return; }
        foreach (char c in char.ConvertFromUtf32(e.Unicode)) _screen?.CharTyped(c);
    }
    private bool _ignoreNextChar;

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool ctrl = e.Control;
        if (e.Key == Keys.F11) { WindowState = WindowState == WindowState.Fullscreen ? WindowState.Normal : WindowState.Fullscreen; return; }
        if (e.Key == Keys.F2) { _screenshotRequested = true; return; }
        if (_screen != null) { _screen.KeyDown(e.Key, ctrl, e.Shift); return; }
        if (World == null || Player == null) return;
        var p = Player;
        switch (e.Key)
        {
            case Keys.Escape: SetScreen(new PauseScreen()); break;
            case Keys.E:
                if (p.Spectator) break;
                if (p.Creative) SetScreen(new CreativeScreen(p)); else SetScreen(new ContainerScreen(p, p.InventoryContainer));
                break;
            case Keys.T: _ignoreNextChar = true; SetScreen(new ChatScreen("")); break;
            case Keys.Slash: _ignoreNextChar = true; SetScreen(new ChatScreen("/")); break;
            case Keys.F1: HideGui = !HideGui; break;
            case Keys.F3: ShowDebug = !ShowDebug; break;
            case Keys.F5: CameraMode = (CameraMode + 1) % 3; break;
            case Keys.Q:
                {
                    var s = p.Inventory.Selected;
                    if (s != null && !p.Spectator)
                    {
                        Client?.SendDrop(ctrl);
                        var drop = ctrl ? s.Copy() : s.CopyWith(1);
                        s.Count -= drop.Count; if (s.Count <= 0) p.Inventory.Selected = null;
                        p.DropStack(drop, false); p.SwingArm();
                    }
                    break;
                }
            default:
                if (e.Key >= Keys.D1 && e.Key <= Keys.D9) p.Inventory.SelectedSlot = e.Key - Keys.D1;
                break;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_netTestOriginalSettings != null) Settings = _netTestOriginalSettings;
        try { NetShutdown(); Sound.Shutdown(); SaveWorld(); Dimensions?.Dispose(); World?.ChunkManager?.Dispose(); _menuCm?.Dispose(); Settings.Save(); } catch (Exception ex) { Log.Error(ex.ToString()); }

        base.OnClosing(e);
    }

    // ================= pętla =================
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Min(0.25, now - _lastTime); _lastTime = now;
        _fpsTimer += dt; _fpsCount++;
        if (_fpsTimer >= 1) { _fps = _fpsCount; _fpsCount = 0; _fpsTimer -= 1; }
        _acc += dt;
        int n = 0;
        while (_acc >= 0.05 && n < 10) { _acc -= 0.05; n++; try { Tick(); } catch (Exception ex) { Log.Error("Tick: " + ex); } }
        if (n >= 10) _acc = 0;
        float pt = (float)(_acc / 0.05);
        if (World != null && _screen != null && _screen.PausesGame && _sessionStarted) pt = 1f;

        MouseLook();
        try { RenderFrame(pt); } catch (Exception ex) { Log.Error("Render: " + ex); }
        _frames++;
        if (_autotest) AutotestStep();
        else if (_showcase) ShowcaseFrame();
        if (_screenshotRequested) { _screenshotRequested = false; TakeScreenshot(null); }
        SwapBuffers();
        if (!Settings.VSync && Settings.MaxFps < 1000)
        {
            double target = 1.0 / Settings.MaxFps;
            double spent = _clock.Elapsed.TotalSeconds - now;
            if (spent < target) Thread.Sleep((int)((target - spent) * 1000));
        }
    }
    private bool _screenshotRequested;

    private void MouseLook()
    {
        if (Player == null || _screen != null || !IsFocused || CursorState != CursorState.Grabbed) return;
        var d = MouseState.Delta;
        if (_skipMouse > 0) { _skipMouse--; return; }
        float f = Settings.Sensitivity * 0.6f + 0.2f;
        float f1 = f * f * f * 8f;
        float dx = d.X * f1 * 0.15f, dy = d.Y * f1 * 0.15f * (Settings.InvertMouse ? -1 : 1);
        if (Player.Sleeping) return;
        Player.Yaw += dx;
        Player.Pitch = Math.Clamp(Player.Pitch + dy, -90f, 90f);
    }
    private int _skipMouse = 2;

    private void Tick()
    {
        _tickCount++;
        try { AudioTick(); } catch (Exception ex) { Log.Error("Audio: " + ex.Message); }
        _screen?.Tick();
        if (_menuWorld != null)
        {
            _menuWorld.DayTime = 3000 + (long)(_tickCount * 0.5) % 24000 * 0;
            _menuCm.Update(new[] { (_menuWorld.SpawnX >> 4, _menuWorld.SpawnZ >> 4) }, 6);
            Renderer.TickAnimations();
            return;
        }
        if (World == null) return;
        var w = World; var p = Player;
        if (!_sessionStarted)
        {
            NetLoadingTick();
            if (World == null) return;
            w.ChunkManager?.Update(new[] { (MathUtil.FloorToInt(p.X) >> 4, MathUtil.FloorToInt(p.Z) >> 4) }, 12);
            Renderer.Update(new Vector3d(p.X, p.Y, p.Z));
            _loadTicks++;
            int pcx = MathUtil.FloorToInt(p.X) >> 4, pcz = MathUtil.FloorToInt(p.Z) >> 4;
            int need = 2;
            int ready = 0, total = (2 * need + 1) * (2 * need + 1);
            for (int dx = -need; dx <= need; dx++) for (int dz = -need; dz <= need; dz++) { var c = w.GetChunk(pcx + dx, pcz + dz); if (c != null && c.Merged) ready++; }
            _loading.Progress = ready / (float)total;
            _loading.Sub = $"Przygotowywanie obszaru: {(int)(_loading.Progress * 100)}%";
            if (ready == total && (Renderer.PendingMeshes < 8 || _loadTicks > 200)) FinishLoading();
            return;
        }
        bool paused = _screen != null && _screen.PausesGame && Server == null && Client == null; // w sieci gra nie staje
        if (paused) { Renderer.Update(new Vector3d(p.X, p.Y, p.Z)); return; }

        NetBeforeTick();
        if (World == null) return;
        NetCaptureButtons();
        ControlTick();
        if (Dimensions != null) Dimensions.Tick(); else w.Tick();
        DimensionMusicTick();
        w = World;
        NetAfterTick();

        if (!p.Dead && p.Removed) { }
        UpdateDimensionChunks();
        Renderer.TickAnimations();
        Renderer.TickParticles();
        // ekwipowanie
        _prevEquip = _equip;
        var held = p.Inventory.Selected;
        bool same = held == _equipped || (held != null && _equipped != null && held.Id == _equipped.Id && held.Damage == _equipped.Damage) ;
        if (_equippedSlot != p.Inventory.SelectedSlot) same = false;
        float target = same ? 1f : 0f;
        _equip += Math.Clamp(target - _equip, -0.4f, 0.4f);
        if (_equip < 0.1f) { _equipped = held; _equippedSlot = p.Inventory.SelectedSlot; }
        if (held != null && (_lastNameItem == null || _lastNameItem.Id != held.Id || _lastNameSlot != p.Inventory.SelectedSlot)) { _itemName = held.Name; _itemNameTicks = 40; }
        _lastNameItem = held; _lastNameSlot = p.Inventory.SelectedSlot;
        if (_itemNameTicks > 0) _itemNameTicks--;
        // FOV
        _prevFovMod = _fovMod;
        float fovT = 1f;
        if (p.Flying) fovT *= 1.1f;
        fovT *= (p.GetSpeed() / .1f + 1) / 2;
        if (p.ItemInUse?.Id == I.Bow) { float bc = p.BowCharge; fovT *= 1f - bc * bc * 0.15f; }
        _fovMod += (fovT - _fovMod) * 0.5f;
        // autozapis co 5 minut
        if (w.TotalTime % 6000 == 0) SaveWorld();
        if (p.Dead && _screen is not DeathScreen) OnDeath(p.DeathMessage);
        if (p.Y < -100 && p.Spectator) p.SetPositionAndPrev(p.X, 100, p.Z);
    }
    private ItemStack _lastNameItem; private int _lastNameSlot;

    private void ControlTick()
    {
        var p = Player; var kb = KeyboardState;
        bool inGame = _screen == null && IsFocused || Scripted && _screen == null;
        if (p.Dead) { p.MoveForward = p.MoveStrafe = 0; p.IsJumping = false; return; }
        bool fwd = inGame && kb.IsKeyDown(Keys.W), back = inGame && kb.IsKeyDown(Keys.S);
        bool left = inGame && kb.IsKeyDown(Keys.A), right = inGame && kb.IsKeyDown(Keys.D);
        bool jump = inGame && kb.IsKeyDown(Keys.Space), sneak = inGame && (kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
        bool sprintKey = inGame && (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl));
        if (_autoInput.HasValue) { (fwd, jump, sneak, sprintKey) = _autoInput.Value; }
        if (p.Sleeping) { if (sneak) p.WakeUp(); fwd = back = left = right = jump = false; }
        p.MoveForward = (fwd ? 1 : 0) - (back ? 1 : 0);
        p.MoveStrafe = (left ? 1 : 0) - (right ? 1 : 0);
        p.IsJumping = jump;
        p.Sneaking = sneak;
        bool canSprint = p.FoodLevel > 6 || p.AllowFlying;
        // sprint: Ctrl lub podwójne W
        if (fwd && !_prevFwd)
        {
            if (_tickCount - _lastFwdTap <= 7 && canSprint && !sneak) p.Sprinting = true;
            _lastFwdTap = (int)_tickCount;
        }
        if (sprintKey && fwd && canSprint && !sneak && p.ItemInUse == null) p.Sprinting = true;
        if (!fwd) p.Sprinting = false;
        _prevFwd = fwd;
        // latanie: podwójna spacja
        if (jump && !_prevJump && p.AllowFlying && !p.Spectator)
        {
            if (_tickCount - _lastJumpTap <= 7) { p.Flying = !p.Flying; _lastJumpTap = -100; }
            else _lastJumpTap = (int)_tickCount;
        }
        _prevJump = jump;
        if (p.Flying && p.OnGround && !p.Spectator && p.MotionY <= 0 && !jump) p.Flying = false;
        // interakcje
        Ctrl.UpdateTarget(1f);
        if (inGame)
        {
            if (_leftDown || _leftPressed) Ctrl.TickLeft(_leftPressed); else Ctrl.ResetBreak();
            if (_rightDown || _rightPressed) Ctrl.TickRight(_rightPressed);
            else if (p.ItemInUse != null) p.StopUsing();
            if (_middlePressed) Ctrl.PickBlock();
        }
        else if (p.ItemInUse != null && p.ItemInUse.Id == I.Bow) p.StopUsing();
        _leftPressed = _rightPressed = _middlePressed = false;
    }
    private (bool, bool, bool, bool)? _autoInput;
}
