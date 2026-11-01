using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>Zdalny gracz po stronie hosta: połączenie, jego Player (NetPuppet) i wszystko, co klient już dostał.</summary>
public sealed partial class RemoteClient : IPlayerHandler
{
    public readonly NetServer Server;
    public readonly INetConnection Conn;
    public readonly PacketBuffer Out = new();
    public string Name = "?";
    public Player P;
    public PlayerController Ctrl;
    public bool LoggedIn;
    public int Age;
    public long LastPacketTick;
    public int ViewDistance = 8;
    public double LastX, LastY, LastZ;
    public int TeleportId;
    private int _ackTeleport;
    private World.World _world;
    public readonly HashSet<long> SentChunks = new();
    private readonly Dictionary<int, Entity> _tracked = new();
    private readonly Dictionary<long, (int x, int y, int z, byte id, byte meta)> _blocks = new();
    private readonly long[] _invHash = new long[40];
    private long _cursorHash = -1;
    private byte _windowId;
    private Container _window;
    private long[] _winHash = Array.Empty<long>();
    private byte[] _lastStats;
    private long _lastTimeSent = -100, _lastSettingsHash;
    private readonly Dictionary<string, byte[]> _worldState = new();
    private int _ticks;

    public RemoteClient(NetServer s, INetConnection c) { Server = s; Conn = c; Array.Fill(_invHash, -1); }

    private World.World W => P.World;

    public void Kick(string reason)
    {
        if (Conn.IsClosed) return;
        Log.Info("Sieć: rozłączam " + Name + ": " + reason);
        var w = Out.Begin(S2C.Disconnect); w.Write(reason ?? ""); Out.End();
        Out.FlushTo(Conn);
        Conn.Close(reason);
    }

    // ================= logowanie =================

    public void SendLogin()
    {
        _world = W;
        var w = Out.Begin(S2C.LoginOk);
        w.Write(P.Id); w.Write(Name); w.Write(P.GameMode);
        w.Write(P.X); w.Write(P.Y); w.Write(P.Z); w.Write(P.Yaw); w.Write(P.Pitch);
        w.Write(W.Name ?? ""); w.Write((sbyte)NetHooks.DimOf(W));
        w.Write(W.TotalTime); w.Write(W.DayTime);
        w.Write(W.SpawnX); w.Write(W.SpawnY); w.Write(W.SpawnZ);
        w.Write(Server.HostPlayer()?.Name ?? "Host");
        Out.End();
        LastX = P.X; LastY = P.Y; LastZ = P.Z;
        SendSettings();
    }

    private void SendSettings()
    {
        var w = Out.Begin(S2C.Settings);
        w.Write((byte)W.Difficulty); w.Write(W.Hardcore); w.Write(W.AllowCheats);
        var rules = new (string, bool)[] { ("doDaylightCycle", W.Rules.DoDaylightCycle), ("keepInventory", W.Rules.KeepInventory), ("doMobSpawning", W.Rules.DoMobSpawning),
            ("mobGriefing", W.Rules.MobGriefing), ("naturalRegeneration", W.Rules.NaturalRegeneration), ("doFireTick", W.Rules.DoFireTick) };
        w.Write((byte)rules.Length);
        foreach (var (n, v) in rules) { w.Write(n); w.Write(v); }
        Out.End();
        _lastSettingsHash = SettingsHash();
    }

    private long SettingsHash() => (long)W.Difficulty * 1000 + (W.Hardcore ? 100 : 0) + (W.AllowCheats ? 200 : 0)
        + (W.Rules.DoDaylightCycle ? 1 : 0) + (W.Rules.KeepInventory ? 2 : 0) + (W.Rules.DoMobSpawning ? 4 : 0) + (W.Rules.MobGriefing ? 8 : 0) + (W.Rules.NaturalRegeneration ? 16 : 0) + (W.Rules.DoFireTick ? 32 : 0);

    // ================= wejście od klienta =================

    public void ApplyInput(NetInput i)
    {
        var p = P;
        if (p == null) return;
        if (i.TeleportAck > _ackTeleport) _ackTeleport = i.TeleportAck;
        // host przesunął gracza od ostatniego ruchu klienta (komenda, perła, łóżko) - ten ruch jest już nieaktualny
        double hx0 = p.X - LastX, hy0 = p.Y - LastY, hz0 = p.Z - LastZ;
        if (hx0 * hx0 + hy0 * hy0 + hz0 * hz0 > 1e-6) SendTeleport();
        if (i.TeleportAck >= TeleportId && !p.Sleeping && !p.Dead) _inMoves.Enqueue(i);
        p.Sneaking = i.Sneaking; p.Sprinting = i.Sprinting; p.IsJumping = i.Jumping;
        p.Flying = i.Flying && p.AllowFlying;
        p.MoveForward = Math.Clamp((int)i.Forward, -1, 1); p.MoveStrafe = Math.Clamp((int)i.Strafe, -1, 1);
        if (i.Selected < 9) p.Inventory.SelectedSlot = i.Selected;
        if (p.Dead) { Ctrl.ResetBreak(); return; }

        Ctrl.Hit = default; Ctrl.HitEntity = null;
        double ex = p.X, ey = p.Y + p.EyeHeight, ez = p.Z;
        if (i.HitKind == 1)
        {
            double hx = i.HitX + Math.Clamp(i.HitFx, 0f, 1f), hy = i.HitY + Math.Clamp(i.HitFy, 0f, 1f), hz = i.HitZ + Math.Clamp(i.HitFz, 0f, 1f);
            double dist = Math.Sqrt((hx - ex) * (hx - ex) + (hy - ey) * (hy - ey) + (hz - ez) * (hz - ez));
            if (dist <= Ctrl.Reach + 1.5 && i.HitFace is >= 0 and < 6)
                Ctrl.Hit = new World.World.HitResult { Hit = true, X = i.HitX, Y = i.HitY, Z = i.HitZ, Face = i.HitFace, HitX = hx, HitY = hy, HitZ = hz, Distance = dist };
        }
        else if (i.HitKind == 2 && W.EntityById.TryGetValue(i.HitEntity, out var e) && e != p && !e.Removed && e.DistanceSq(ex, ey, ez) < 8 * 8)
            Ctrl.HitEntity = e;

        Server.SetActor(this);
        try
        {
            if (i.LeftDown || i.LeftPressed) Ctrl.TickLeft(i.LeftPressed); else Ctrl.ResetBreak();
            if (i.RightDown || i.RightPressed) Ctrl.TickRight(i.RightPressed);
            else if (p.ItemInUse != null) p.StopUsing();
            if (i.Middle) Ctrl.PickBlock();
        }
        finally { Server.SetActor(null); }

        if (i.RightPressed || i.Middle) ForceSync();
        if (i.HitKind == 1 && (i.LeftDown || i.LeftPressed || i.RightPressed || i.RightDown))
        {
            QueueBlockState(i.HitX, i.HitY, i.HitZ);
            if (i.RightPressed || i.RightDown) QueueBlockState(i.HitX + Dir.X[i.HitFace], i.HitY + Dir.Y[i.HitFace], i.HitZ + Dir.Z[i.HitFace]);
        }
    }

    // Ruchy klienta przychodzą przez przekaźnik paczkami (0, 2, 1, 3 na tick). Host nakłada je w ticku gracza po PrevTick,
    // jeden na tick, z małym buforem: gracz u hosta porusza się płynnie, ma animację chodu i nie przeskakuje.
    private readonly Queue<NetInput> _inMoves = new();
    private readonly Queue<NetInput> _inputs = new();
    private const int MoveBuffer = 2;

    /// <summary>Wejście od klienta czeka w kolejce: host wykonuje je w stałym tempie (jedno na tick), jak klient je wysłał.
    /// Bez tego paczki z przekaźnika dawały serie uderzeń kilofa i nakładające się dźwięki.</summary>
    public int PendingInputs => _inputs.Count;
    public void QueueInput(NetInput i) { if (_inputs.Count < 100) _inputs.Enqueue(i); }

    public void ProcessInputs()
    {
        int n = _inputs.Count > MoveBuffer + 1 ? _inputs.Count - MoveBuffer : Math.Min(1, _inputs.Count);
        for (int k = 0; k < n; k++) ApplyInput(_inputs.Dequeue());
    }

    internal void ApplyQueuedMoves()
    {
        var p = P;
        if (p == null || _inMoves.Count == 0) return;
        int n = _inMoves.Count; // tempo pilnuje już kolejka wejść
        Server.SetActor(this);
        try
        {
            for (int k = 0; k < n && _inMoves.Count > 0; k++)
            {
                var i = _inMoves.Dequeue();
                if (i.TeleportAck < TeleportId || p.Sleeping || p.Dead) continue;
                double hx0 = p.X - LastX, hy0 = p.Y - LastY, hz0 = p.Z - LastZ;
                if (hx0 * hx0 + hy0 * hy0 + hz0 * hz0 > 1e-6) { SendTeleport(); return; }
                double dx = i.X - p.X, dy = i.Y - p.Y, dz = i.Z - p.Z;
                if (!double.IsFinite(i.X + i.Y + i.Z) || dx * dx + dy * dy + dz * dz >= 100 * 100) { SendTeleport(); return; }
                p.NetApplyMove(i.X, i.Y, i.Z, i.Yaw, i.Pitch, i.OnGround);
                LastX = p.X; LastY = p.Y; LastZ = p.Z;
            }
        }
        finally { Server.SetActor(null); }
    }

    private Container WindowById(byte win) => win == 0 ? P.InventoryContainer : win == _windowId && P.OpenContainer == _window ? _window : null;

    public void Click(byte win, int slot, int button, ClickType type)
    {
        var c = WindowById(win);
        ForceSync();
        if (c == null || P.Dead) return;
        if (slot != -999 && (slot < 0 || slot >= c.Slots.Count)) return;
        if (type == ClickType.Swap && (button < 0 || button > 8)) return;
        if (type == ClickType.Clone && !P.Creative) return;
        c.Click(slot, button, type, P);
    }

    public void Distribute(byte win, int button, List<int> slots)
    {
        var c = WindowById(win);
        ForceSync();
        if (c == null || P.Dead || slots.Any(s => s < 0 || s >= c.Slots.Count)) return;
        c.Distribute(slots, button, P);
    }

    public void CloseWindow(byte win)
    {
        if (win == 0) P.InventoryContainer.OnClose(P);
        else if (win == _windowId && P.OpenContainer != null) P.CloseContainer();
        if (P.OpenContainer == null) _window = null;
        ForceSync();
    }

    public void CreativeSlot(int slot, ItemStack s)
    {
        if (!P.Creative || slot < 0 || slot >= 40) { ForceSync(); return; }
        if (s != null && (s.Count <= 0 || s.Count > 64)) s = null;
        P.Inventory.Set(slot, s);
        _invHash[slot] = StackUtil.Hash(P.Inventory.Get(slot));
    }

    public void DropSelected(bool all)
    {
        var s = P.Inventory.Selected;
        ForceSync();
        if (s == null || P.Spectator || P.Dead) return;
        var drop = all ? s.Copy() : s.CopyWith(1);
        s.Count -= drop.Count;
        if (s.Count <= 0) P.Inventory.Selected = null;
        P.DropStack(drop, false);
        P.SwingArm();
    }

    public void Respawn()
    {
        if (!P.Dead) return;
        if (W.Hardcore) { P.Respawn(); P.SetGameMode(3); }
        else P.Respawn();
        if (!W.Entities.Contains(P)) W.AddEntityNow(P);
        ForceSync();
    }

    private void ForceSync()
    {
        Array.Fill(_invHash, -1);
        _cursorHash = -1;
        Array.Fill(_winHash, -1);
    }

    // ================= IPlayerHandler (wywołania logiki gry dla zdalnego gracza) =================

    public void OpenBlockGui(GuiKind kind, int x, int y, int z)
    {
        if (kind == GuiKind.SignEdit)
        {
            _signEdit = (x, y, z);
            var sw = Out.Begin(S2C.OpenSignEditor); sw.Write(x); sw.Write(y); sw.Write(z); Out.End();
            return;
        }
        var c = ContainerFactory.Create(W, P, kind, x, y, z);
        if (c == null) return;
        if (P.OpenContainer != null) P.CloseContainer();
        P.OpenContainer = c;
        OpenWindow((int)kind, x, y, z, c);
    }

    public void OnMessage(string msg)
    {
        var w = Out.Begin(S2C.Chat); w.Write(msg ?? ""); Out.End();
    }

    public void OnDeath(string message)
    {
        var w = Out.Begin(S2C.Death); w.Write(message ?? ""); Out.End();
    }

    public void OnPickup(Entity item, Entity collector) { }

    private void OpenWindow(int kind, int x, int y, int z, Container c)
    {
        if (kind < 0) kind = c is HorseContainer ? -2 : c is MerchantContainer ? -3 : c is RsContainer ? -4 : -1;
        _windowId = (byte)(_windowId % 100 + 1);
        _window = c;
        _winHash = new long[c.Slots.Count];
        Array.Fill(_winHash, -1);
        var w = Out.Begin(S2C.OpenWindow);
        w.Write(_windowId); w.Write(kind); w.Write(x); w.Write(y); w.Write(z); w.Write(c.Title ?? "");
        w.Write((short)c.Width); w.Write((short)c.Height);
        w.Write((short)c.Slots.Count);
        foreach (var s in c.Slots)
        {
            w.Write((byte)(s.Inv is PlayerInventory ? 0 : 1));
            w.Write((short)s.Index); w.Write((short)s.X); w.Write((short)s.Y);
            w.Write((byte)((s.IsOutput ? 1 : 0) | ((s.ArmorType + 1) << 1)));
        }
        if (kind == -2) ((HorseContainer)c).Horse.Save(w);
        else if (kind == -3) { ((MerchantContainer)c).Villager.Save(w); w.Write(((MerchantContainer)c).Index); }
        else if (kind == -4) w.Write((byte)((RsContainer)c).Kind);
        _merchantState = null;
        Out.End();
    }

    public void EnchantItem(byte window, int button)
    {
        if (P.Dead || WindowById(window) is not EnchantingContainer c || !c.StillValid(P)) return;
        if (c.Enchant(P, button)) ForceSync();
    }

    private (int x, int y, int z)? _signEdit;

    /// <summary>C12PacketUpdateSign: tylko tabliczka otwarta temu graczowi, w zasięgu.</summary>
    public void UpdateSign(int x, int y, int z, string[] lines)
    {
        if (P.Dead || _signEdit != (x, y, z) || P.DistanceSq(x + .5, y + .5, z + .5) > 64 * 64) return;
        _signEdit = null;
        if (W.GetTileEntity(x, y, z) is not SignTile t) return;
        for (int i = 0; i < 4; i++) t.Lines[i] = Signs.Filter(i < lines.Length ? lines[i] : "");
    }

    public void AnvilName(byte window, string name)
    {
        if (P.Dead || WindowById(window) is not AnvilContainer c || !c.StillValid(P)) return;
        c.SetName(name); ForceSync();
    }

    public void SelectRecipe(byte window, int index)
    {
        if (P.Dead || WindowById(window) is not MerchantContainer c || !c.StillValid(P)
            || index < 0 || index >= c.Villager.Recipes.Count) return;
        c.SetIndex(index); ForceSync();
    }

    // ================= wysyłka po ticku =================

    public void Update(bool teScan)
    {
        _ticks++;
        if (W != _world) ChangeWorld();
        // teleport (komenda, łóżko, odrodzenie, perła...) - pozycja zmieniona przez hosta
        double ddx = P.X - LastX, ddy = P.Y - LastY, ddz = P.Z - LastZ;
        if (ddx * ddx + ddy * ddy + ddz * ddz > 1e-6) SendTeleport();
        // odrzut: host nie liczy ruchu gracza, więc przekazuje prędkość klientowi
        if (Math.Abs(P.MotionX) >= 0.05 || Math.Abs(P.MotionY) >= 0.05 || Math.Abs(P.MotionZ) >= 0.05)
        {
            var w = Out.Begin(S2C.Velocity); w.Write(P.MotionX); w.Write(P.MotionY); w.Write(P.MotionZ); Out.End();
        }
        P.MotionX = P.MotionY = P.MotionZ = 0;

        StreamChunks();
        FlushBlocks();
        if (teScan) SendTiles();
        TrackEntities();
        SendStats();
        if (_ticks % 100 == 0) ForceSync();
        SyncInventory();
        SyncWindow();
        if (_ticks % 2 == 0) SendMaps();
        if (W.TotalTime - _lastTimeSent >= 20 || W.TotalTime < _lastTimeSent)
        {
            _lastTimeSent = W.TotalTime;
            var w = Out.Begin(S2C.Time); w.Write(W.TotalTime); w.Write(W.DayTime); w.Write(W.Rules.DoDaylightCycle); Out.End();
            if (SettingsHash() != _lastSettingsHash) SendSettings();
            SendWorldState();
        }
    }

    private void ChangeWorld()
    {
        _world = W;
        Ctrl = new PlayerController(W, P);
        SentChunks.Clear(); _tracked.Clear(); _blocks.Clear(); _worldState.Clear();
        var w = Out.Begin(S2C.ChangeDimension); w.Write((sbyte)NetHooks.DimOf(W)); w.Write(P.Id); Out.End();
        SendTeleport();
    }

    private void SendTeleport()
    {
        TeleportId++;
        _inMoves.Clear();
        var w = Out.Begin(S2C.Teleport);
        w.Write(TeleportId); w.Write(P.X); w.Write(P.Y); w.Write(P.Z); w.Write(P.Yaw); w.Write(P.Pitch);
        Out.End();
        LastX = P.X; LastY = P.Y; LastZ = P.Z;
    }

    // ---------- chunki ----------
    private int _lastCcx = int.MinValue, _lastCcz;
    private readonly List<(int cx, int cz, int d)> _cand = new();

    private void StreamChunks()
    {
        int pcx = MathUtil.FloorToInt(P.X) >> 4, pcz = MathUtil.FloorToInt(P.Z) >> 4, r = ViewDistance;
        sbyte dim = (sbyte)NetHooks.DimOf(W);
        if (pcx != _lastCcx || pcz != _lastCcz)
        {
            _lastCcx = pcx; _lastCcz = pcz;
            foreach (var k in SentChunks.ToList())
            {
                int cx = (int)(k >> 32), cz = (int)k;
                if (Math.Abs(cx - pcx) <= r + 1 && Math.Abs(cz - pcz) <= r + 1) continue;
                SentChunks.Remove(k);
                var w = Out.Begin(S2C.UnloadChunk); w.Write(dim); w.Write(cx); w.Write(cz); Out.End();
            }
        }
        if (Conn.PendingBytes > 768 * 1024) return;
        _cand.Clear();
        for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                int d = dx * dx + dz * dz;
                if (d > (r + 0.5) * (r + 0.5)) continue;
                long k = MathUtil.ChunkKey(pcx + dx, pcz + dz);
                if (SentChunks.Contains(k) || !W.Chunks.ContainsKey(k)) continue;
                _cand.Add((pcx + dx, pcz + dz, d));
            }
        if (_cand.Count == 0) return;
        _cand.Sort((a, b) => a.d.CompareTo(b.d));
        int budget = SentChunks.Count < 25 ? 8 : 4;
        foreach (var (cx, cz, _) in _cand)
        {
            if (budget-- <= 0) break;
            var c = W.GetChunk(cx, cz);
            if (c == null) continue;
            var data = ChunkCodec.Encode(c);
            var w = Out.Begin(S2C.ChunkData); w.Write(dim); w.Write(cx); w.Write(cz); w.Write(data.Length); w.Write(data); Out.End();
            SentChunks.Add(MathUtil.ChunkKey(cx, cz));
            Out.FlushTo(Conn);
        }
    }

    // ---------- bloki ----------
    public void QueueBlock(int x, int y, int z, byte id, int meta) => _blocks[NetServer.PosKey(x, y, z)] = (x, y, z, id, (byte)meta);

    private void QueueBlockState(int x, int y, int z)
    {
        if ((uint)y >= Chunk.Height || !SentChunks.Contains(MathUtil.ChunkKey(x >> 4, z >> 4))) return;
        QueueBlock(x, y, z, W.GetBlock(x, y, z), W.GetMeta(x, y, z));
    }

    private void FlushBlocks()
    {
        if (_blocks.Count == 0) return;
        var w = Out.Begin(S2C.BlockChanges);
        w.Write((sbyte)NetHooks.DimOf(W)); w.Write(_blocks.Count);
        foreach (var b in _blocks.Values) { w.Write(b.x); w.Write((short)b.y); w.Write(b.z); w.Write(b.id); w.Write(b.meta); }
        Out.End();
        _blocks.Clear();
    }

    private void SendTiles()
    {
        foreach (var (tw, te, data) in Server.ChangedTiles)
        {
            if (tw != W || !SentChunks.Contains(MathUtil.ChunkKey(te.X >> 4, te.Z >> 4))) continue;
            var w = Out.Begin(S2C.TileEntity);
            w.Write((sbyte)NetHooks.DimOf(W)); w.Write(te.X); w.Write(te.Y); w.Write(te.Z); w.Write(te.TypeId); w.Write(data.Length); w.Write(data);
            Out.End();
        }
    }

    // ---------- encje ----------
    private readonly HashSet<int> _seen = new();
    private readonly List<(Entity e, NetServer.EntState s)> _moves = new();

    private void TrackEntities()
    {
        _seen.Clear(); _moves.Clear();
        double range = Math.Min(ViewDistance * 16, 96);
        foreach (var e in W.Entities)
        {
            if (e == P || e.Removed) continue;
            if (Math.Abs(e.X - P.X) > range || Math.Abs(e.Z - P.Z) > range) continue;
            if (!SentChunks.Contains(MathUtil.ChunkKey(MathUtil.FloorToInt(e.X) >> 4, MathUtil.FloorToInt(e.Z) >> 4))) continue;
            var st = Server.StateOf(e);
            if (st == null) continue;
            _seen.Add(e.Id);
            if (!_tracked.ContainsKey(e.Id))
            {
                _tracked[e.Id] = e;
                Spawn(e, st);
                _moves.Add((e, st));
                continue;
            }
            if (st.Moved) _moves.Add((e, st));
            if (st.EquipChanged) Equip(e);
            if (st.DataChanged && st.Data != null)
            {
                var w = Out.Begin(S2C.EntityData); w.Write(e.Id); w.Write(st.NetData); w.Write(st.Data.Length); w.Write(st.Data); Out.End();
            }
            foreach (var (kind, arg) in st.Events) { var w = Out.Begin(S2C.EntityEvent); w.Write(e.Id); w.Write((byte)kind); w.Write(arg); Out.End(); }
        }
        if (_moves.Count > 0)
        {
            var w = Out.Begin(S2C.EntityMoves);
            w.Write(_moves.Count);
            foreach (var (e, s) in _moves) { w.Write(e.Id); w.Write(s.Qx); w.Write(s.Qy); w.Write(s.Qz); w.Write(s.Yaw); w.Write(s.Pitch); w.Write(s.Flags); }
            Out.End();
        }
        if (_tracked.Count > _seen.Count)
        {
            var gone = _tracked.Keys.Where(id => !_seen.Contains(id)).ToList();
            var w = Out.Begin(S2C.RemoveEntities);
            w.Write(gone.Count);
            foreach (var id in gone) { w.Write(id); _tracked.Remove(id); }
            Out.End();
        }
    }

    private void Spawn(Entity e, NetServer.EntState st)
    {
        var data = st.Data ?? Array.Empty<byte>();
        var w = Out.Begin(S2C.SpawnEntity);
        w.Write(e.Id); w.Write(e.TypeKey ?? ""); w.Write(e is Player pl ? pl.Name : ""); w.Write(st.NetData); w.Write(data.Length); w.Write(data);
        Out.End();
        if (e is LivingEntity) Equip(e);
    }

    private void Equip(Entity e)
    {
        var w = Out.Begin(S2C.EntityEquip);
        w.Write(e.Id);
        if (e is Player p) { w.WriteStack(p.Inventory.Selected); foreach (var a in p.Inventory.Armor) w.WriteStack(a); }
        else { w.WriteStack((e as LivingEntity)?.HeldItem); for (int i = 0; i < 4; i++) w.WriteStack(null); }
        Out.End();
    }

    // ---------- gracz ----------
    private void SendStats()
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(P.Health); bw.Write(P.MaxHealth); bw.Write(P.FoodLevel); bw.Write(P.Saturation);
            bw.Write(P.XpLevel); bw.Write(P.XpProgress); bw.Write(P.XpTotal); bw.Write(P.Air); bw.Write(P.EnchantmentSeed);
            bw.Write((byte)P.GameMode); bw.Write(P.FireTicks > 0); bw.Write(P.Dead); bw.Write(P.Sleeping);
            bw.Write(P.HasSpawn); bw.Write(P.SpawnX); bw.Write(P.SpawnY); bw.Write(P.SpawnZ);
            bw.Write((byte)P.Effects.Count);
            foreach (var ef in P.Effects.Values) { bw.Write((int)ef.Id); bw.Write(ef.Duration / 20); bw.Write(ef.Amplifier); }
        }
        var bytes = ms.ToArray();
        if (_lastStats != null && bytes.AsSpan().SequenceEqual(_lastStats) && _ticks % 40 != 0) return;
        _lastStats = bytes;
        var w = Out.Begin(S2C.Stats); w.Write(bytes); Out.End();
    }

    private void SyncInventory()
    {
        int n = 0;
        for (int i = 0; i < 40; i++) if (StackUtil.Hash(P.Inventory.Get(i)) != _invHash[i]) n++;
        if (n > 0)
        {
            var w = Out.Begin(S2C.InventorySlots);
            w.Write((byte)n);
            for (int i = 0; i < 40; i++)
            {
                var s = P.Inventory.Get(i);
                long h = StackUtil.Hash(s);
                if (h == _invHash[i]) continue;
                _invHash[i] = h;
                w.Write((byte)i); w.WriteStack(s);
            }
            Out.End();
        }
        long ch = StackUtil.Hash(P.Cursor);
        if (ch != _cursorHash)
        {
            _cursorHash = ch;
            var w = Out.Begin(S2C.Cursor); w.WriteStack(P.Cursor); Out.End();
        }
    }

    private byte[] _merchantState;
    private void SyncWindow()
    {
        var current = P.OpenContainer;
        if (current is HorseContainer hc && !hc.StillValid(P)
            || current is MerchantContainer mc && !mc.StillValid(P)
            || current is RsContainer rc && rc.Inv is Entity e && (e.Removed || e.Dead || e.World != W || e.DistanceSq(P) > 64))
            P.CloseContainer();
        if (P.OpenContainer != _window)
        {
            if (P.OpenContainer == null)
            {
                var w = Out.Begin(S2C.CloseWindow); w.Write(_windowId); Out.End();
                _window = null; _winHash = Array.Empty<long>();
            }
            else OpenWindow(-1, 0, 0, 0, P.OpenContainer); // okno otwarte przez kod modułu bez OpenBlockGui: układ ogólny
        }
        var c = _window;
        if (c == null) return;
        if (c is MerchantContainer merchant)
        {
            using var data = new MemoryStream();
            using (var writer = new BinaryWriter(data, System.Text.Encoding.UTF8, true))
            {
                writer.Write(merchant.Index); writer.Write(merchant.Villager.Recipes.Count);
                foreach (var offer in merchant.Villager.Recipes) offer.Save(writer);
            }
            var state = data.ToArray();
            if (_merchantState == null || !state.AsSpan().SequenceEqual(_merchantState))
            {
                _merchantState = state;
                var writer = Out.Begin(S2C.MerchantOffers); writer.Write(_windowId); writer.Write(state); Out.End();
            }
        }
        var changed = new List<int>();
        for (int i = 0; i < c.Slots.Count; i++)
        {
            var sl = c.Slots[i];
            if (sl.Inv is PlayerInventory) continue;
            long h = StackUtil.Hash(sl.Stack);
            if (h != _winHash[i]) { _winHash[i] = h; changed.Add(i); }
        }
        if (changed.Count == 0) return;
        var bw = Out.Begin(S2C.WindowSlots);
        bw.Write(_windowId); bw.Write((short)changed.Count);
        foreach (var i in changed) { bw.Write((short)i); bw.WriteStack(c.Slots[i].Stack); }
        Out.End();
    }

    private void SendWorldState()
    {
        foreach (var (key, (write, _)) in NetHooks.WorldState)
        {
            byte[] d;
            try { d = write(W) ?? Array.Empty<byte>(); } catch (Exception ex) { Log.Error("Sieć: stan " + key + ": " + ex.Message); continue; }
            if (_worldState.TryGetValue(key, out var old) && old.AsSpan().SequenceEqual(d)) continue;
            _worldState[key] = d;
            var w = Out.Begin(S2C.WorldState); w.Write(key); w.Write(d.Length); w.Write(d); Out.End();
        }
    }
}
