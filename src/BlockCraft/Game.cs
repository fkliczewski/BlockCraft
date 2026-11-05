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
    /// <summary>Pozycja myszy w GUI; podczas odtwarzania odłożonego zdarzenia - z chwili, gdy przyszło.</summary>
    public int MouseGuiX => _input.Replaying?.MouseX ?? _mouseGuiX;
    public int MouseGuiY => _input.Replaying?.MouseY ?? _mouseGuiY;
    private int _mouseGuiX, _mouseGuiY;
    public bool IsShiftDown => _input.Replaying?.Shift ?? (KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift));
    /// <summary>Czas zdarzenia wejścia w ms (Environment.TickCount64); odłożone zdarzenie zachowuje czas, w którym przyszło (np. dwuklik).</summary>
    public long InputTime => _input.Replaying?.Time ?? Environment.TickCount64;
    public bool ShowDebug, HideGui;
    public int CameraMode;
    private readonly List<(string text, long time)> _chat = new();
    private double _acc;
    private long _frames;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTime;
    private int _fps, _fpsCount; private double _fpsTimer;
    /// <summary>F3: ticki gry i przyrost czasu świata na sekundę (w 1.8 oba po 20).</summary>
    private int _tps, _dayRate; private long _tpsLastTick, _tpsLastDay;
    private bool _leftDown, _rightDown, _middlePressed;
    private bool _leftPressed, _rightPressed;
    private int _lastFwdTap = -100, _lastJumpTap = -100; private long _tickCount;
    private bool _prevFwd, _prevJump;
    private float _equip = 1, _prevEquip = 1; private ItemStack _equipped; private int _equippedSlot;
    private int _itemNameTicks; private string _itemName = "";
    private float _fovMod = 1f, _prevFovMod = 1f;
    private World.World _menuWorld; private ChunkManager _menuCm; private float _menuYaw;
    public bool HasMenuWorld => _menuWorld != null && Renderer.RenderedSections > 20;
    public int CurrentDifficulty
    {
        get => World != null ? (int)World.Difficulty : Settings.Difficulty;
        set
        {
            // jak /difficulty: wszystkie wymiary; hardcore i gość (trudność ustala host) bez zmian w świecie
            if (World != null && Client == null && !World.Hardcore) foreach (var w in Commands.Worlds(World)) w.Difficulty = (Difficulty)value;
            Settings.Difficulty = value;
        }
    }
    private readonly bool _autotest;
    private readonly string[] _args;

    public Game(string[] args) : base(new GameWindowSettings { UpdateFrequency = 0 },
        new NativeWindowSettings { ClientSize = new Vector2i(1280, 720), Title = "BlockCraft", APIVersion = new Version(3, 3), Profile = ContextProfile.Core, Flags = ContextFlags.ForwardCompatible, StartVisible = true })
    {
        _args = args;
        _input = new DeferredInputQueue(IsInputBlocked);
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
        _stress = !_autotest && !_showcase && args.Contains("--stress");
        Settings = Settings.Load();
        ParsePackArgs(args);
        int fps = Array.IndexOf(args, "--fps");
        if (fps >= 0 && fps + 1 < args.Length && int.TryParse(args[fps + 1], out int f) && f > 0) _fpsOverride = f;
        VSync = Settings.VSync && _fpsOverride == 0 ? VSyncMode.On : VSyncMode.Off;
    }
    /// <summary>--fps N (testy): VSync wyłączony i limit N klatek, bez zapisu do ustawień.</summary>
    private readonly int _fpsOverride;

    protected override void OnLoad()
    {
        base.OnLoad();
        string glRenderer = GL.GetString(StringName.Renderer);
        Log.Info("Start BlockCraft, GL: " + glRenderer + " / " + GL.GetString(StringName.Version));
        // Pierwsze uruchomienie na danej karcie: słaba grafika (zintegrowana, programowa) dostaje profil "laptop".
        if (Settings.GpuProfileFor != glRenderer && !_autotest && !_showcase && !_stress)
        {
            var kind = GpuProfile.Detect(glRenderer);
            if (kind != GpuProfile.Kind.Dedicated) { GpuProfile.ApplyLaptop(Settings, kind); Log.Info("Profil grafiki: " + kind + " (zasięg " + Settings.RenderDistance + ", skala " + Settings.RenderScale + "%)"); }
            Settings.GpuProfileFor = glRenderer; Settings.Save();
        }
        SharedQuadIndices.Init();
        ItemRegistry.Init(); Recipes.Init(); Entities.Registries.Init();
        Sb = new SpriteBatch();
        LoadResourcePacksAtStart();
        Gui = new GuiTextures();
        Renderer = new WorldRenderer();
        Icons = new IconAtlas(Renderer.Entities, Renderer.Terrain);
        Sound.Init(Settings);
        Renderer.SoundHook = (n, x, y, z, v, p) =>
        {
            if (World == null || !_sessionStarted) return;
            // szafa grająca: "record:cat" gra płytę w bloku x,y,z, "record:" ją zatrzymuje
            if (n.StartsWith("record:")) { if (n.Length > 7) Sound.PlayRecord(n[7..], x, y, z); else Sound.StopRecord(x, y, z); return; }
            // Limit dźwięków na tick: przy masowych wybuchach mikser i tak gra najwyżej Mixer.MaxVoices głosów,
            // a każde wywołanie kosztuje (dekodowanie, wyszukiwanie duplikatów). Mechanika gry bez zmian.
            if (_soundTick != _tickCount) { _soundTick = _tickCount; _soundsThisTick = 0; }
            if (++_soundsThisTick > 24) return;
            Sound.Play(n, x, y, z, v, p);
        };
        UpdateGuiScale();
        ApplySettings();
        StartMenuWorld();
        SetScreen(new TitleScreen());
        _lastTime = _clock.Elapsed.TotalSeconds;
        SyncSourcesInBackground();
        if (ArgsNetAutotest) SetupNetAutotest();
        else if (_autotest) SetupAutotest();
        else if (_showcase && _args.Contains("--packs-preview")) SetupPacksPreview();
        else if (_showcase) SetupShowcase();
        else if (_stress) SetupStress();
    }

    // ================= ustawienia / GUI =================
    public void ApplySettings()
    {
        VSync = Settings.VSync && _fpsOverride == 0 ? VSyncMode.On : VSyncMode.Off;
        ChunkMesher.SmoothLighting = Settings.SmoothLighting;
        bool fancyChanged = ChunkMesher.FancyLeaves != Settings.FancyGraphics;
        ChunkMesher.FancyLeaves = Settings.FancyGraphics;
        int rdOld = Renderer.RenderDistance;
        Renderer.RenderDistance = Settings.RenderDistance;
        Renderer.CloudsEnabled = Settings.Clouds;
        Renderer.FarEntities = Settings.FarEntities;
        Renderer.Gamma = Settings.Brightness;
        if (World?.ChunkManager != null) World.ChunkManager.Radius = Settings.RenderDistance;
        // trudność świata zmienia tylko przycisk w opcjach (CurrentDifficulty) i /difficulty, nie każde zastosowanie ustawień
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
            Difficulty = (Difficulty)level.Difficulty, Storage = storage, DefaultGameMode = level.GameMode
        };
        foreach (var kv in level.Rules) w.Rules.Set(kv.Key, kv.Value.ToString().ToLowerInvariant(), out _);
        if (level.RuleValues != null) foreach (var kv in level.RuleValues) w.Rules.Set(kv.Key, kv.Value, out _, true);
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
            Items.MapStore.Of(home).SaveAll();
            _level.DayTime = home.DayTime; _level.Time = home.TotalTime;
            _level.Weather = home.Weather;
            _level.SpawnX = home.SpawnX; _level.SpawnY = home.SpawnY; _level.SpawnZ = home.SpawnZ;
            _level.Difficulty = (int)World.Difficulty;
            _level.GameMode = home.DefaultGameMode;
            _level.Player = Player.ToData();
            _level.Rules = new Dictionary<string, bool>
            {
                ["doDaylightCycle"] = World.Rules.DoDaylightCycle, ["keepInventory"] = World.Rules.KeepInventory, ["doMobSpawning"] = World.Rules.DoMobSpawning,
                ["mobGriefing"] = World.Rules.MobGriefing, ["doFireTick"] = World.Rules.DoFireTick, ["naturalRegeneration"] = World.Rules.NaturalRegeneration, ["doEntityDrops"] = World.Rules.DoEntityDrops
            };
            _level.RuleValues = home.Rules.ToStrings();
            Commands.SaveState(home);
            _storage.SaveLevel(_level);
            Dimensions.SaveAll();
        }
        catch (Exception ex) { Log.Error("Zapis świata: " + ex); }
    }

    public void QuitToTitle()
    {
        if (Player != null) Bl2Stats.Add(Player, "stat.leaveGame");
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
        if (kind == GuiKind.SignEdit) { if (Client == null) SetScreen(new Gui.SignEditScreen(x, y, z)); return; }
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
        _mouseGuiX = (int)(e.X * sx / GuiScale); _mouseGuiY = (int)(e.Y * sy / GuiScale);
        int mx = MouseGuiX, my = MouseGuiY;
        RunOrDefer(() => _screen?.MouseMove(mx, my));
    }

    // Wejście w połowie dzielonego ticku czeka na jego koniec: akcje GUI (odrodzenie, komendy z czatu, upuszczanie)
    // dodają i usuwają encje, a to przesuwa indeksy pętli encji przerwanego ticku (encja byłaby pominięta).
    private readonly DeferredInputQueue _input;
    private void RunOrDefer(Action a) => _input.RunOrDefer(a, new InputContext(MouseGuiX, MouseGuiY, IsShiftDown, Environment.TickCount64));
    /// <summary>Kliknięcie w świecie czeka na ControlTick; kolejne zdarzenia zostają w kolejce, żeby nie scalić dwóch kliknięć w jedno.</summary>
    private bool HasUnhandledWorldClick => _screen == null && (_leftPressed || _rightPressed || _middlePressed);
    private bool IsInputBlocked() => _midTick || HasUnhandledWorldClick;
    private void FlushDeferredInput() => _input.Flush(ex => Log.Error("Wejście: " + ex));

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        int btn = e.Button == MouseButton.Left ? 0 : e.Button == MouseButton.Right ? 1 : e.Button == MouseButton.Middle ? 2 : -1;
        if (btn < 0) return;
        int mx = MouseGuiX, my = MouseGuiY;
        RunOrDefer(() => MouseDownNow(mx, my, btn));
    }
    private void MouseDownNow(int mx, int my, int btn)
    {
        if (_screen != null) { _screen.MouseDown(mx, my, btn); return; }
        if (btn == 0) { _leftDown = true; _leftPressed = true; }
        if (btn == 1) { _rightDown = true; _rightPressed = true; }
        if (btn == 2) _middlePressed = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        int btn = e.Button == MouseButton.Left ? 0 : e.Button == MouseButton.Right ? 1 : 2;
        int mx = MouseGuiX, my = MouseGuiY;
        RunOrDefer(() => MouseUpNow(mx, my, btn));
    }
    private void MouseUpNow(int mx, int my, int btn)
    {
        if (btn == 0) _leftDown = false;
        if (btn == 1) _rightDown = false;
        _screen?.MouseUp(mx, my, btn);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        int mx = MouseGuiX, my = MouseGuiY; float dy = e.OffsetY;
        RunOrDefer(() => MouseWheelNow(mx, my, dy));
    }
    private void MouseWheelNow(int mx, int my, float offsetY)
    {
        if (_screen != null) { _screen.Scroll(mx, my, offsetY); return; }
        if (Player == null) return;
        int d = offsetY > 0 ? -1 : offsetY < 0 ? 1 : 0;
        if (Player.Spectator) { Player.FlySpeed = Math.Clamp(Player.FlySpeed - d * 0.005f, 0.01f, 0.2f); return; }
        Player.Inventory.SelectedSlot = (Player.Inventory.SelectedSlot + d + 9) % 9;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        int u = e.Unicode;
        RunOrDefer(() => TextInputNow(u));
    }
    private void TextInputNow(int unicode)
    {
        if (_ignoreNextChar) { _ignoreNextChar = false; return; }
        foreach (char c in char.ConvertFromUtf32(unicode)) _screen?.CharTyped(c);
    }
    private bool _ignoreNextChar;

    /// <summary>Klawisze edycji tekstu, których powtarzanie (przytrzymanie) trafia do ekranu; reszta reaguje tylko na wciśnięcie.</summary>
    private static bool IsEditRepeatKey(Keys k) => k is Keys.Backspace or Keys.Delete or Keys.Left or Keys.Right or Keys.Up or Keys.Down
        or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown;

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        RunOrDefer(() => KeyDownNow(e));
    }
    private void KeyDownNow(KeyboardKeyEventArgs e)
    {
        bool ctrl = e.Control;
        // MC 1.8: w grze bez powtórzeń klawiszy; ekrany tekstowe (czat, tabliczka, kowadło) potrzebują ich do kasowania i strzałek
        if (e.IsRepeat) { if (_screen != null && IsEditRepeatKey(e.Key)) _screen.KeyDown(e.Key, ctrl, e.Shift); return; }
        // znak klawisza, który otworzył czat, przychodzi zaraz po nim; inne wciśnięcie znaczy, że go nie było (np. Ctrl+T)
        _ignoreNextChar = false;
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
                if (Client != null) { Client.Out.Begin(Net.C2S.OpenInventory); Client.Out.End(); }
                else p.AwardAchievement("openInventory");
                if (p.Creative) SetScreen(new CreativeScreen(p)); else SetScreen(new ContainerScreen(p, p.InventoryContainer));
                break;
            case Keys.T: _ignoreNextChar = !ctrl && !e.Alt; SetScreen(new ChatScreen("")); break; // Ctrl/Alt+T nie daje znaku
            case Keys.Slash: _ignoreNextChar = !ctrl && !e.Alt; SetScreen(new ChatScreen("/")); break;
            case Keys.F1: HideGui = !HideGui; break;
            case Keys.F3: ShowDebug = !ShowDebug; break;
            case Keys.F5: CameraMode = (CameraMode + 1) % 3; break;
            case Keys.Q:
                {
                    var s = p.Inventory.Selected;
                    if (s != null && !p.Spectator)
                    {
                        Client?.SendDrop(ctrl);
                        Bl2Stats.Add(p, "stat.drop");
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
        try { NetShutdown(); Sound.Shutdown(); SaveWorld(); Dimensions?.Dispose(); World?.ChunkManager?.Dispose(); _menuCm?.Dispose(); if (!_stress) Settings.Save(); } catch (Exception ex) { Log.Error(ex.ToString()); }

        base.OnClosing(e);
    }

    // ================= pętla =================
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        double now = _clock.Elapsed.TotalSeconds; _frameStart = now;
        double dt = Math.Min(0.25, now - _lastTime); _lastTime = now;
        _fpsTimer += dt; _fpsCount++;
        if (_fpsTimer >= 1)
        {
            _fps = _fpsCount; _fpsCount = 0; _fpsTimer -= 1;
            _tps = (int)(_tickCount - _tpsLastTick); _tpsLastTick = _tickCount;
            long day = World?.DayTime ?? 0; _dayRate = (int)(day - _tpsLastDay); _tpsLastDay = day;
        }
        _acc += dt;
        // Budżet czasu ticków na klatkę: ciężki tick (np. tysiące wybuchów TNT) dzieli się na kawałki
        // i klatka rysuje się między nimi, więc obraz i mysz nie stają. Wynik ticku jest ten sam.
        // Autotest liczy na stary rytm (całe ticki), więc tam bez budżetu.
        long deadline = _autotest || _noSlice ? long.MaxValue : Stopwatch.GetTimestamp() + (long)(TickBudgetSeconds * Stopwatch.Frequency);
        int n = 0;
        if (_midTick) { n++; try { ContinueTick(deadline); } catch (Exception ex) { _midTick = false; Log.Error("Tick: " + ex); } }
        FlushDeferredInput(); // między pełnymi tickami, przed następnym (ControlTick widzi kliknięcia)
        while (!_midTick && _acc >= 0.05 && n < 10 && (n == 0 || Stopwatch.GetTimestamp() < deadline))
        {
            _acc -= 0.05; n++;
            try { Tick(deadline); } catch (Exception ex) { _midTick = false; Log.Error("Tick: " + ex); }
            FlushDeferredInput(); // np. zwolnienie przycisku po kliknięciu obsłużonym w tym ticku, przed nadrabianiem kolejnych
        }
        if (n >= 10 || _acc > 0.5) _acc = 0;
        // w połowie ticku: pt=0, czyli wszystko w pozycji z początku ticku (bez cofania po jego końcu)
        float pt = _midTick ? 0f : (float)(_acc / 0.05);
        if (World != null && _screen != null && _screen.PausesGame && _sessionStarted && Server == null && Client == null) pt = 1f; // w sieci świat dalej tyka

        // zminimalizowane okno: bufor 0x0 (rzutowanie z proporcją 0 rzuca wyjątek), więc bez rysowania i bez kręcenia pętli
        if (FramebufferSize.X <= 0 || FramebufferSize.Y <= 0 || WindowState == WindowState.Minimized)
        {
            Thread.Sleep(10); _nextFrame = _clock.Elapsed.TotalSeconds;
            return;
        }

        MouseLook();
        CheckSourceChanges();
        if (_reloadPacks) { _reloadPacks = false; try { ReloadResourcePacksNow(); } catch (Exception ex) { Log.Error("Paczki zasobów: " + ex); } }
        bool gpuQ = _stressMeasure;
        if (gpuQ) StressGpuBegin();
        try { RenderFrame(pt); } catch (Exception ex) { Log.Error("Render: " + ex); }
        if (gpuQ) StressGpuEnd();
        _frames++;
        if (_autotest) AutotestStep();
        else if (_showcase) ShowcaseFrame();
        else if (_stress) StressFrame();
        if (_screenshotRequested) { _screenshotRequested = false; TakeScreenshot(null); }
        SwapBuffers();
        int maxFps = _fpsOverride > 0 ? _fpsOverride : Settings.MaxFps;
        if ((!Settings.VSync || _fpsOverride > 0) && maxFps < 1000)
        {
            // Limiter z harmonogramem (bez dryfu): sen po 1 ms (timeBeginPeriod), końcówka dokręcana Yield,
            // bo sam Thread.Sleep na Windows ma rozdzielczość do 15,6 ms (limit 120 dawał ~64 FPS).
            double target = 1.0 / maxFps;
            _nextFrame += target;
            double t = _clock.Elapsed.TotalSeconds;
            if (_nextFrame < t - target) _nextFrame = t; // duże opóźnienie: nie nadrabiamy klatek
            while (_nextFrame - t > 0.0015) { Thread.Sleep(1); t = _clock.Elapsed.TotalSeconds; }
            while (_clock.Elapsed.TotalSeconds < _nextFrame) Thread.Yield();
        }
        else _nextFrame = _clock.Elapsed.TotalSeconds;
    }
    private double _nextFrame;
    private bool _screenshotRequested;
    private double _frameStart;
    private long _soundTick; private int _soundsThisTick;

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

    /// <summary>Ile czasu na klatkę mogą zająć ticki, zanim klatka się narysuje (reszta ticku w następnej).</summary>
    private const double TickBudgetSeconds = 0.020;
    /// <summary>--no-slice: stary rytm (całe ticki w klatce), do porównań.</summary>
    private readonly bool _noSlice = Environment.GetCommandLineArgs().Contains("--no-slice");
    private bool _midTick; private DimensionSet _midDims; private World.World _midWorld; private Player _midPlayer;

    /// <summary>Dokańcza przerwany tick świata; gdy świat się zmienił (wyjście do menu), porzuca go.</summary>
    private void ContinueTick(long deadline)
    {
        if (World == null || Player != _midPlayer || Dimensions != _midDims || (_midDims == null && World != _midWorld)) { _midTick = false; return; }
        if (!(_midDims != null ? _midDims.TickSlice(deadline) : _midWorld.TickSlice(deadline))) return;
        _midTick = false;
        TickAfterWorld(_midPlayer);
    }

    private void Tick(long deadline)
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
        _midDims = Dimensions; _midWorld = w; _midPlayer = p;
        bool done;
        if (Dimensions != null) { Dimensions.TickBegin(); done = Dimensions.TickSlice(deadline); }
        else { w.TickBegin(); done = w.TickSlice(deadline); }
        if (!done) { _midTick = true; return; }
        TickAfterWorld(p);
    }

    private void TickAfterWorld(Player p)
    {
        DimensionMusicTick();
        var w = World;
        NetAfterTick();

        if (!p.Dead && p.Removed) { }
        UpdateDimensionChunks();
        Renderer.TickAnimations();
        Items.ItemAnimations.Tick(World, Player);
        Bl2ClientTick();
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
