using System.Collections.Concurrent;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>Gra po stronie klienta (okno gry albo atrapa w testach).</summary>
public interface INetClientHost
{
    World.World World { get; }
    Player Player { get; }
    Screen CurrentScreen { get; }
    void SetScreen(Screen s);
    void AddChat(string s);
    void NetDisconnected(string reason);
    void NetChangeWorld(World.World next);
}

/// <summary>Dane z S2C.LoginOk.</summary>
public sealed class LoginInfo
{
    public int EntityId; public string Name; public int GameMode;
    public double X, Y, Z; public float Yaw, Pitch;
    public string WorldName; public int Dim;
    public long TotalTime, DayTime; public int SpawnX, SpawnY, SpawnZ;
    public string HostName;
}

/// <summary>
/// Klient gry sieciowej. Świat jest zdalny (World.IsRemote): bloki, encje i stan gracza przychodzą od hosta.
/// Lokalny gracz porusza się sam (jak w MC) i wysyła co tick ruch oraz akcje; kopanie, stawianie i kliknięcia w GUI
/// są przewidywane lokalnie, a host i tak odsyła prawdziwy stan.
/// </summary>
public sealed class NetClient
{
    public readonly INetConnection Conn;
    private LinkClientService _link;
    public readonly PacketBuffer Out = new();
    public LoginInfo Login;
    public string Error;
    public INetClientHost G;
    /// <summary>Ostatnia tabliczka otwarta do edycji przez hosta (testy).</summary>
    public (int x, int y, int z)? SignEditorOpened;
    private World.World W => G?.World;
    private Player P => G?.Player;
    private readonly Queue<(byte type, byte[] frame, int off, int len)> _packets = new();
    private int _teleportAck;
    private byte _windowId; private Container _window;
    private bool _invScreenOpen;
    private int _dim, _gen;
    private readonly ConcurrentQueue<(long key, int gen, Chunk c)> _decoded = new();
    private readonly HashSet<long> _decoding = new();
    private readonly Dictionary<long, List<Action>> _deferred = new();
    private readonly long[] _serverInv = new long[40];
    public List<(string name, int ping)> Players = new();
    public long BytesReceived => Conn is QueuedConnection q ? q.BytesReceived : 0;
    public int ChunksReceived;

    private NetClient(INetConnection c) { Conn = c; Array.Fill(_serverInv, -1); Items.MapStore.Client = new Items.MapStore(null); }

    /// <summary>Połączenie już otwarte (np. pętla w pamięci w testach): wysyła logowanie.</summary>
    public static NetClient Start(INetConnection conn, string nick, int view)
    {
        var c = new NetClient(conn);
        var w = c.Out.Begin(C2S.Login); w.Write(Protocol.Version); w.Write(nick ?? "Steve"); w.Write((byte)Math.Clamp(view, 2, 32)); c.Out.End();
        c.Out.FlushTo(conn);
        return c;
    }

    /// <summary>Dołączenie przez Tailcat.Link (kod albo null = ostatni host).</summary>
    public static async Task<NetClient> JoinAsync(string code, string nick, int view, Tailcat.Link.LinkOptions options, CancellationToken ct)
    {
        var link = await LinkClientService.JoinAsync(code, nick, options, ct);
        var c = Start(link.Connection, nick, view);
        c._link = link;
        return c;
    }

    public void Disconnect(string reason = "Rozłączono")
    {
        Conn.Close(reason);
        if (_link != null) { var l = _link; _link = null; _ = Task.Run(async () => await l.DisposeAsync()); }
        if (Container.NetClickHook == ClickHook) Container.NetClickHook = null;
        if (Container.NetDistributeHook == DistributeHook) Container.NetDistributeHook = null;
    }

    // ================= odbiór =================

    private void Pump()
    {
        while (Conn.TryReceive(out var f))
        {
            int pos = 0;
            while (pos + 5 <= f.Length)
            {
                int len = BitConverter.ToInt32(f, pos);
                if (len < 0 || pos + 5 + len > f.Length) { Error = "Uszkodzone dane od hosta"; Conn.Close(Error); return; }
                _packets.Enqueue((f[pos + 4], f, pos + 5, len));
                pos += 5 + len;
            }
        }
    }

    /// <summary>Przed startem sesji: czeka na LoginOk. Zwraca true, gdy przyszło.</summary>
    public bool PollLogin()
    {
        Pump();
        while (_packets.Count > 0 && Login == null)
        {
            var (type, f, off, len) = _packets.Dequeue();
            var r = new BinaryReader(new MemoryStream(f, off, len, false));
            if (type == (byte)S2C.Disconnect) { Error = r.ReadString(); Conn.Close(Error); return false; }
            if (type == (byte)S2C.LoginOk) Login = ReadLogin(r);
        }
        if (Login == null && Conn.IsClosed && Error == null) Error = Conn.CloseReason ?? "Połączenie zamknięte";
        return Login != null;
    }

    private static LoginInfo ReadLogin(BinaryReader r) => new()
    {
        EntityId = r.ReadInt32(), Name = r.ReadString(), GameMode = r.ReadInt32(),
        X = r.ReadDouble(), Y = r.ReadDouble(), Z = r.ReadDouble(), Yaw = r.ReadSingle(), Pitch = r.ReadSingle(),
        WorldName = r.ReadString(), Dim = r.ReadSByte(), TotalTime = r.ReadInt64(), DayTime = r.ReadInt64(),
        SpawnX = r.ReadInt32(), SpawnY = r.ReadInt32(), SpawnZ = r.ReadInt32(), HostName = r.ReadString()
    };

    /// <summary>Podpięcie do gry po utworzeniu świata zdalnego.</summary>
    public void Attach(INetClientHost g)
    {
        G = g;
        _dim = Login.Dim;
        Container.NetClickHook = ClickHook;
        Container.NetDistributeHook = DistributeHook;
        P.NetDropHook = s => { if (P.Creative && G.CurrentScreen is CreativeScreen) { var w = Out.Begin(C2S.CreativeDrop); w.WriteStack(s); Out.End(); } };
        P.NetShootHook = _ => { }; // strzałę wystrzeli host
    }

    /// <summary>Co tick (także w trakcie wczytywania): chunki z wątków, pakiety, wykrywanie zamknięcia okien.</summary>
    public void Tick()
    {
        IntegrateChunks();
        Pump();
        int n = 0;
        while (_packets.Count > 0 && n++ < 5000)
        {
            var (type, f, off, len) = _packets.Dequeue();
            try { Handle((S2C)type, new BinaryReader(new MemoryStream(f, off, len, false))); }
            catch (Exception ex) { Log.Error($"Sieć: pakiet {(S2C)type}: {ex}"); }
            if (G == null) return; // rozłączono w trakcie
        }
        if (P == null) return;
        // zamknięcie okna przez gracza -> host
        if (_window != null && P.OpenContainer != _window)
        {
            var w = Out.Begin(C2S.CloseWindow); w.Write(_windowId); Out.End();
            _window = null;
        }
        bool inv = G.CurrentScreen is CreativeScreen || G.CurrentScreen is ContainerScreen && P.OpenContainer == null;
        if (_invScreenOpen && !inv) { var w = Out.Begin(C2S.CloseWindow); w.Write((byte)0); Out.End(); }
        _invScreenOpen = inv;
        if (P.Creative) SyncCreative();
    }

    private void SyncCreative()
    {
        for (int i = 0; i < 40; i++)
        {
            var s = P.Inventory.Get(i);
            long h = StackUtil.Hash(s);
            if (h == _serverInv[i]) continue;
            _serverInv[i] = h;
            var w = Out.Begin(C2S.CreativeSlot); w.Write((short)i); w.WriteStack(s); Out.End();
        }
    }

    public void Flush() => Out.FlushTo(Conn);

    // ================= wysyłka =================

    public void SendInput(NetInput i)
    {
        i.TeleportAck = _teleportAck;
        var w = Out.Begin(C2S.Input); i.Write(w); Out.End();
    }

    public void SendChat(string s) { var w = Out.Begin(C2S.Chat); w.Write(s); Out.End(); }
    public void SendRespawn() { Out.Begin(C2S.Respawn); Out.End(); }
    public void SelectRecipe(int index)
    {
        if (_window is not MerchantContainer) return;
        var writer = Out.Begin(C2S.SelectRecipe); writer.Write(_windowId); writer.Write(index); Out.End();
    }
    public void SetBeacon(Effect primary,Effect secondary)
    {
        if(_window is not BeaconContainer)return;
        var writer=Out.Begin(C2S.Beacon);writer.Write(_windowId);writer.Write((int)primary);writer.Write((int)secondary);Out.End();
    }
    public void EnchantItem(int button)
    {
        if (_window is not EnchantingContainer) return;
        var writer = Out.Begin(C2S.EnchantItem); writer.Write(_windowId); writer.Write((byte)button); Out.End();
    }
    public void UpdateSign(int x, int y, int z, string[] lines)
    {
        var writer = Out.Begin(C2S.UpdateSign); writer.Write(x); writer.Write(y); writer.Write(z);
        for (int i = 0; i < 4; i++) writer.Write(i < lines.Length ? lines[i] ?? "" : "");
        Out.End();
    }
    public void AnvilName(string name)
    {
        if (_window is not AnvilContainer) return;
        var writer = Out.Begin(C2S.AnvilName); writer.Write(_windowId); writer.Write(name ?? ""); Out.End();
    }
    public void SendDrop(bool all) { var w = Out.Begin(C2S.DropSelected); w.Write(all); Out.End(); }

    private void ClickHook(Container c, int slot, int button, ClickType type, Player p)
    {
        if (G == null || p != P || G.CurrentScreen is CreativeScreen) return;
        int win = c == P.InventoryContainer ? 0 : c == _window ? _windowId : -1;
        if (win < 0) return;
        var w = Out.Begin(C2S.Click); w.Write((byte)win); w.Write((short)slot); w.Write((byte)button); w.Write((byte)type); Out.End();
    }

    private void DistributeHook(Container c, List<int> slots, int button, Player p)
    {
        if (G == null || p != P || G.CurrentScreen is CreativeScreen) return;
        int win = c == P.InventoryContainer ? 0 : c == _window ? _windowId : -1;
        if (win < 0) return;
        var w = Out.Begin(C2S.Distribute); w.Write((byte)win); w.Write((byte)button); w.Write((byte)Math.Min(255, slots.Count));
        foreach (var s in slots.Take(255)) w.Write((short)s);
        Out.End();
    }

    // ================= chunki =================

    private void IntegrateChunks()
    {
        int n = 0;
        while (n < 16 && _decoded.TryDequeue(out var d))
        {
            _decoding.Remove(d.key);
            if (d.gen != _gen || d.c == null) continue;
            n++;
            var w = W;
            if (w.Chunks.TryRemove(d.key, out var old)) old.Unloaded = true;
            w.Chunks[d.key] = d.c;
            w.Light.MergeChunk(d.c);
            d.c.MarkAllDirty();
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var nb = w.GetChunk(d.c.Cx + dx, d.c.Cz + dz);
                    if (nb != null) nb.DirtySections = (1 << Chunk.Sections) - 1;
                }
            ChunksReceived++;
            if (_deferred.Remove(d.key, out var acts)) foreach (var a in acts) a();
        }
    }

    private bool Defer(int x, int z, Action a)
    {
        long k = MathUtil.ChunkKey(x >> 4, z >> 4);
        if (!_decoding.Contains(k)) return false;
        if (!_deferred.TryGetValue(k, out var l)) _deferred[k] = l = new List<Action>();
        l.Add(a);
        return true;
    }

    // ================= obsługa pakietów =================

    private void Handle(S2C type, BinaryReader r)
    {
        var w = W; var p = P;
        switch (type)
        {
            case S2C.Disconnect: G.NetDisconnected(r.ReadString()); return;
            case S2C.KeepAlive: { long t = r.ReadInt64(); var o = Out.Begin(C2S.KeepAlive); o.Write(t); Out.End(); break; }
            case S2C.ChunkData:
                {
                    int dim = r.ReadSByte(), cx = r.ReadInt32(), cz = r.ReadInt32();
                    var data = r.ReadBytes(r.ReadInt32());
                    if (dim != _dim) break;
                    long key = MathUtil.ChunkKey(cx, cz);
                    int gen = _gen;
                    _decoding.Add(key);
                    Task.Run(() =>
                    {
                        Chunk c = null;
                        try { c = ChunkCodec.Decode(cx, cz, data); LightEngine.InitChunkLocal(c); }
                        catch (Exception ex) { Log.Error($"Sieć: chunk {cx},{cz}: {ex.Message}"); }
                        _decoded.Enqueue((key, gen, c));
                    });
                    break;
                }
            case S2C.UnloadChunk:
                {
                    int dim = r.ReadSByte(), cx = r.ReadInt32(), cz = r.ReadInt32();
                    if (dim != _dim) break;
                    long key = MathUtil.ChunkKey(cx, cz);
                    _deferred.Remove(key);
                    if (_decoding.Contains(key)) _deferred[key] = new List<Action> { () => UnloadChunk(key) };
                    else UnloadChunk(key);
                    break;
                }
            case S2C.BlockChanges:
                {
                    int dim = r.ReadSByte(), n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        int x = r.ReadInt32(), y = r.ReadInt16(), z = r.ReadInt32(); byte id = r.ReadByte(), meta = r.ReadByte();
                        if (dim != _dim) continue;
                        if (!Defer(x, z, () => W.SetBlockRemote(x, y, z, id, meta))) w.SetBlockRemote(x, y, z, id, meta);
                    }
                    break;
                }
            case S2C.TileEntity:
                {
                    int dim = r.ReadSByte(), x = r.ReadInt32(), y = r.ReadInt32(), z = r.ReadInt32(), t = r.ReadInt32();
                    var data = r.ReadBytes(r.ReadInt32());
                    if (dim != _dim) break;
                    if (!Defer(x, z, () => ApplyTile(x, y, z, t, data))) ApplyTile(x, y, z, t, data);
                    break;
                }
            case S2C.SpawnEntity:
                {
                    int id = r.ReadInt32(); string key = r.ReadString(), name = r.ReadString(); int nd = r.ReadInt32();
                    var data = r.ReadBytes(r.ReadInt32());
                    Spawn(id, key, name, nd, data);
                    break;
                }
            case S2C.EntityMoves:
                {
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        int id = r.ReadInt32(), qx = r.ReadInt32(), qy = r.ReadInt32(), qz = r.ReadInt32();
                        byte yaw = r.ReadByte(), pitch = r.ReadByte(), flags = r.ReadByte();
                        if (id == p.Id || !w.EntityById.TryGetValue(id, out var e)) continue;
                        e.NetX = qx / 32.0; e.NetY = qy / 32.0; e.NetZ = qz / 32.0;
                        e.NetYaw = NetServer.UnAng(yaw); e.NetPitch = NetServer.UnAng(pitch); e.NetSteps = 3;
                        ApplyFlags(e, flags);
                    }
                    break;
                }
            case S2C.EntityData:
                {
                    int id = r.ReadInt32(), nd = r.ReadInt32();
                    var data = r.ReadBytes(r.ReadInt32());
                    if (w.EntityById.TryGetValue(id, out var e) && e != p) LoadKeepingPosition(e, data, nd);
                    break;
                }
            case S2C.EntityEvent:
                {
                    int id = r.ReadInt32(); var kind = (EntityEventKind)r.ReadByte(); float arg = r.ReadSingle();
                    if (!w.EntityById.TryGetValue(id, out var e) || e is not LivingEntity le) break;
                    if (kind == EntityEventKind.Hurt) { le.HurtTime = le.MaxHurtTime; le.AttackedAtYaw = arg; }
                    else if (kind == EntityEventKind.Swing) le.SwingArm();
                    else if (kind == EntityEventKind.Death) { le.Dead = true; le.Health = 0; }
                    break;
                }
            case S2C.EntityEquip:
                {
                    int id = r.ReadInt32();
                    var held = r.ReadStack();
                    var armor = new ItemStack[4];
                    for (int i = 0; i < 4; i++) armor[i] = r.ReadStack();
                    if (!w.EntityById.TryGetValue(id, out var e) || e == p) break;
                    if (e is Player pl) { pl.Inventory.SelectedSlot = 0; pl.Inventory.Main[0] = held; for (int i = 0; i < 4; i++) pl.Inventory.Armor[i] = armor[i]; }
                    else if (e is LivingEntity le) le.HeldItem = held;
                    break;
                }
            case S2C.RemoveEntities:
                {
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++) { int id = r.ReadInt32(); if (id != p.Id && w.EntityById.TryGetValue(id, out var e)) w.RemoveEntity(e); }
                    break;
                }
            case S2C.Sound:
                {
                    string name = r.ReadString(); double x = r.ReadDouble(), y = r.ReadDouble(), z = r.ReadDouble(); float v = r.ReadSingle(), pi = r.ReadSingle();
                    w.PlaySound(name, x, y, z, v, pi);
                    break;
                }
            case S2C.Particles:
                {
                    var k = (ParticleKind)r.ReadByte(); double x = r.ReadDouble(), y = r.ReadDouble(), z = r.ReadDouble(); int cnt = r.ReadInt32(), d = r.ReadInt32();
                    w.SpawnParticles(k, x, y, z, cnt, d);
                    break;
                }
            case S2C.Time: w.TotalTime = r.ReadInt64(); w.DayTime = r.ReadInt64(); w.Rules.DoDaylightCycle = r.ReadBoolean(); break;
            case S2C.Settings:
                {
                    w.Difficulty = (Difficulty)r.ReadByte(); w.Hardcore = r.ReadBoolean(); w.AllowCheats = r.ReadBoolean();
                    int n = r.ReadByte();
                    for (int i = 0; i < n; i++) w.Rules.Set(r.ReadString(), r.ReadBoolean().ToString(), out _);
                    break;
                }
            case S2C.Chat: G.AddChat(r.ReadString()); break;
            case S2C.Stats: ApplyStats(r); break;
            case S2C.InventorySlots:
                {
                    int n = r.ReadByte();
                    for (int i = 0; i < n; i++)
                    {
                        int slot = r.ReadByte(); var s = r.ReadStack();
                        if (slot >= 40) continue;
                        p.Inventory.Set(slot, s);
                        _serverInv[slot] = StackUtil.Hash(p.Inventory.Get(slot));
                    }
                    break;
                }
            case S2C.Cursor: { var s = r.ReadStack(); if (G.CurrentScreen is not CreativeScreen) p.Cursor = s; break; }
            case S2C.OpenWindow: OpenWindow(r); break;
            case S2C.MapData: MapSync.Read(r); break;
            case S2C.OpenSignEditor:
                {
                    int sx = r.ReadInt32(), sy = r.ReadInt32(), sz = r.ReadInt32();
                    SignEditorOpened = (sx, sy, sz);
                    G.SetScreen(new SignEditScreen(sx, sy, sz));
                    break;
                }
            case S2C.MerchantOffers:
                {
                    byte id = r.ReadByte(); int index = r.ReadInt32(), n = r.ReadInt32();
                    if (n < 0 || n > 4096) throw new InvalidDataException("Za dużo ofert handlu");
                    var offers = new List<MerchantRecipe>();
                    for (int i = 0; i < n; i++) offers.Add(MerchantRecipe.Load(r));
                    if (id == _windowId && _window is MerchantContainer merchant)
                    {
                        merchant.Villager.Recipes.Clear(); merchant.Villager.Recipes.AddRange(offers);
                        merchant.Index = index;
                    }
                    break;
                }
            case S2C.WindowSlots:
                {
                    byte id = r.ReadByte(); int n = r.ReadInt16();
                    for (int i = 0; i < n; i++)
                    {
                        int slot = r.ReadInt16(); var s = r.ReadStack();
                        if (id != _windowId || _window == null || slot < 0 || slot >= _window.Slots.Count) continue;
                        var sl = _window.Slots[slot];
                        if (sl.Inv is not PlayerInventory) sl.Inv.Set(sl.Index, s);
                    }
                    break;
                }
            case S2C.CloseWindow:
                {
                    byte id = r.ReadByte();
                    if (id == _windowId && _window != null && P.OpenContainer == _window)
                    {
                        var closed = _window;
                        _window = null; // bez odsyłania CloseWindow
                        G.SetScreen(null);
                        if (P.OpenContainer == closed) P.CloseContainer();
                    }
                    break;
                }
            case S2C.Teleport:
                {
                    _teleportAck = r.ReadInt32();
                    p.SetPositionAndPrev(r.ReadDouble(), r.ReadDouble(), r.ReadDouble());
                    p.Yaw = p.PrevYaw = r.ReadSingle(); p.Pitch = p.PrevPitch = r.ReadSingle();
                    p.MotionX = p.MotionY = p.MotionZ = 0; p.FallDistance = 0;
                    break;
                }
            case S2C.Velocity: p.MotionX = r.ReadDouble(); p.MotionY = r.ReadDouble(); p.MotionZ = r.ReadDouble(); break;
            case S2C.Death: p.NetSetDeath(r.ReadString()); break;
            case S2C.PlayerList:
                {
                    int n = r.ReadByte();
                    var l = new List<(string, int)>();
                    for (int i = 0; i < n; i++) l.Add((r.ReadString(), r.ReadInt32()));
                    Players = l;
                    break;
                }
            case S2C.WorldState:
                {
                    string key = r.ReadString(); var d = r.ReadBytes(r.ReadInt32());
                    if (NetHooks.WorldState.TryGetValue(key, out var h)) { try { h.Read(w, d); } catch (Exception ex) { Log.Error("Sieć: stan " + key + ": " + ex.Message); } }
                    break;
                }
            case S2C.ChangeDimension: ChangeDimension(r.ReadSByte(), r.ReadInt32()); break;
        }
    }

    private void UnloadChunk(long key)
    {
        if (!W.Chunks.TryRemove(key, out var c)) return;
        c.Unloaded = true;
        foreach (var e in W.Entities.ToArray())
            if (e != P && (MathUtil.FloorToInt(e.X) >> 4) == c.Cx && (MathUtil.FloorToInt(e.Z) >> 4) == c.Cz) W.RemoveEntity(e);
    }

    private void ApplyTile(int x, int y, int z, int type, byte[] data)
    {
        var w = W;
        if (w.GetChunk(x >> 4, z >> 4) == null) return;
        try
        {
            var cur = w.GetTileEntity(x, y, z);
            if (cur != null && cur.TypeId == type) { cur.Load(new BinaryReader(new MemoryStream(data))); cur.X = x; cur.Y = y; cur.Z = z; }
            else { var te = ChunkCodec.LoadTile(type, x, y, z, data); if (te != null) w.SetTileEntity(x, y, z, te); }
        }
        catch (Exception ex) { Log.Error("Sieć: tile entity " + type + ": " + ex.Message); }
    }

    private void Spawn(int id, string key, string name, int netData, byte[] data)
    {
        var w = W;
        if (id == P.Id) return;
        if (w.EntityById.TryGetValue(id, out var old)) w.RemoveEntity(old);
        Entity e = key == "player" ? new Player(w) { Name = string.IsNullOrEmpty(name) ? "?" : name } : EntitySerializer.Create(key, w);
        if (e == null) { Log.Error("Sieć: nieznany typ encji '" + key + "' (brak rejestracji w Registries?)"); return; }
        e.Id = id;
        try { if (data.Length > 0) e.Load(new BinaryReader(new MemoryStream(data))); e.SetNetData(netData); }
        catch (Exception ex) { Log.Error("Sieć: encja " + key + ": " + ex.Message); }
        e.IsRemoteControlled = true;
        e.NetX = e.X; e.NetY = e.Y; e.NetZ = e.Z; e.NetYaw = e.Yaw; e.NetPitch = e.Pitch;
        w.AddEntityNow(e);
    }

    private static void LoadKeepingPosition(Entity e, byte[] data, int netData)
    {
        double x = e.X, y = e.Y, z = e.Z, px = e.PrevX, py = e.PrevY, pz = e.PrevZ;
        float yaw = e.Yaw, pitch = e.Pitch, pyaw = e.PrevYaw, ppitch = e.PrevPitch;
        float by = 0, pby = 0, hy = 0, phy = 0; bool dead = false; int deathTime = 0, hurt = 0;
        if (e is LivingEntity l0) { by = l0.BodyYaw; pby = l0.PrevBodyYaw; hy = l0.HeadYaw; phy = l0.PrevHeadYaw; dead = l0.Dead; deathTime = l0.DeathTime; hurt = l0.HurtTime; }
        int age = e.Age;
        try { e.Load(new BinaryReader(new MemoryStream(data))); e.SetNetData(netData); }
        catch (Exception ex) { Log.Error("Sieć: dane encji " + e.TypeKey + ": " + ex.Message); }
        e.X = x; e.Y = y; e.Z = z; e.PrevX = px; e.PrevY = py; e.PrevZ = pz;
        e.Yaw = yaw; e.Pitch = pitch; e.PrevYaw = pyaw; e.PrevPitch = ppitch; e.Age = age;
        if (e is LivingEntity l) { l.BodyYaw = by; l.PrevBodyYaw = pby; l.HeadYaw = hy; l.PrevHeadYaw = phy; l.Dead |= dead; l.DeathTime = deathTime; l.HurtTime = hurt; }
    }

    private static void ApplyFlags(Entity e, byte f)
    {
        if (e is LivingEntity le) { le.Sneaking = (f & 1) != 0; le.Sprinting = (f & 2) != 0; }
        e.FireTicks = (f & 4) != 0 ? Math.Max(e.FireTicks, 1) : 0;
        e.OnGround = (f & 32) != 0;
        if (e is Player p)
        {
            p.Sleeping = (f & 8) != 0;
            p.ItemInUse = (f & 16) != 0 ? p.Inventory.Selected : null;
        }
    }

    private void ApplyStats(BinaryReader r)
    {
        var p = P;
        p.Health = r.ReadSingle(); p.MaxHealth = r.ReadSingle(); p.FoodLevel = r.ReadInt32(); p.Saturation = r.ReadSingle();
        p.XpLevel = r.ReadInt32(); p.XpProgress = r.ReadSingle(); p.XpTotal = r.ReadInt32(); p.Air = r.ReadInt32(); p.EnchantmentSeed = r.ReadInt32();
        int gm = r.ReadByte(); bool fire = r.ReadBoolean(), dead = r.ReadBoolean(), sleeping = r.ReadBoolean();
        p.HasSpawn = r.ReadBoolean(); p.SpawnX = r.ReadInt32(); p.SpawnY = r.ReadInt32(); p.SpawnZ = r.ReadInt32();
        int ne = r.ReadByte();
        var effects = new Dictionary<Effect, EffectInstance>();
        for (int i = 0; i < ne; i++) { var id = (Effect)r.ReadInt32(); int dur = r.ReadInt32() * 20, amp = r.ReadInt32(); effects[id] = new EffectInstance(id, dur, amp); }
        p.ReceiveAchievements(r.ReadUInt64());
        foreach (var id in p.Effects.Keys.ToList()) if (!effects.ContainsKey(id)) p.RemoveEffect(id);
        foreach (var (id, ef) in effects)
        {
            if (p.Effects.TryGetValue(id, out var cur) && cur.Amplifier == ef.Amplifier) cur.Duration = ef.Duration;
            else { p.Effects.Remove(id); p.AddEffect(ef); }
        }
        if (gm != p.GameMode) { bool fl = p.Flying; p.SetGameMode(gm); if (p.AllowFlying && gm != 3) p.Flying = fl; }
        p.FireTicks = fire ? Math.Max(p.FireTicks, 1) : 0;
        if (!sleeping && p.Sleeping) p.WakeUp();
        if (dead && !p.Dead) { p.Dead = true; p.Health = 0; }
        else if (!dead && p.Dead)
        {
            p.Dead = false; p.DeathTime = 0; p.Removed = false; p.HurtTime = 0;
            if (!W.Entities.Contains(p)) W.AddEntityNow(p);
            if (G.CurrentScreen is DeathScreen) G.SetScreen(null);
        }
    }

    private void OpenWindow(BinaryReader r)
    {
        byte id = r.ReadByte(); int kind = r.ReadInt32(), x = r.ReadInt32(), y = r.ReadInt32(), z = r.ReadInt32();
        string title = r.ReadString(); int width = r.ReadInt16(), height = r.ReadInt16(), n = r.ReadInt16();
        var layout = new List<(bool player, int index, int sx, int sy, int flags)>();
        for (int i = 0; i < n; i++) layout.Add((r.ReadByte() == 0, r.ReadInt16(), r.ReadInt16(), r.ReadInt16(), r.ReadByte()));
        Container c = kind >= 0 ? ContainerFactory.Create(W, P, (GuiKind)kind, x, y, z) : null;
        if (kind == -2)
        {
            var horse = new Horse(W); horse.Load(r); c = new HorseContainer(P.Inventory, horse);
        }
        else if (kind == -3)
        {
            var villager = new Villager(W); villager.Load(r);
            var merchant = new MerchantContainer(P.Inventory, villager) { Index = r.ReadInt32() };
            merchant.Inv.OnChanged = null; // wynik i zapłata przychodzą wyłącznie od hosta
            c = merchant;
        }
        else if (kind == -4)
        {
            var rsLayout = (RsContainer.Layout)r.ReadByte();
            c = new RsContainer(P.Inventory, new SimpleInventory(rsLayout == RsContainer.Layout.Hopper ? 5 : rsLayout == RsContainer.Layout.Grid3 ? 9 : 27), title, rsLayout);
        }
        if (c == null || c.Slots.Count != n) c = new NetContainer(P.Inventory, title, width, height, layout);
        c.Title = title;
        if (G.CurrentScreen != null) G.SetScreen(null);
        if (_window != null) { var o = Out.Begin(C2S.CloseWindow); o.Write(_windowId); Out.End(); }
        _windowId = id; _window = c;
        P.OpenContainer = c;
        G.SetScreen(ContainerFactory.ScreenFor(P, c));
    }

    private void ChangeDimension(int dim, int entityId)
    {
        _dim = dim; _gen++;
        _decoding.Clear(); _deferred.Clear();
        W.RemoveEntity(P);
        P.Id = entityId;
        P.Removed = false;
        G.NetChangeWorld(NetHooks.CreateRemoteWorld(dim));
    }
}

/// <summary>Kontener zbudowany z układu slotów przysłanego przez hosta (GUI modułu bez fabryki po stronie klienta).</summary>
public sealed class NetContainer : Container
{
    public NetContainer(PlayerInventory inv, string title, int width, int height, List<(bool player, int index, int sx, int sy, int flags)> layout)
    {
        Title = title; Width = width; Height = height;
        PInv = inv;
        int other = layout.Count(l => !l.player);
        var local = new SimpleInventory(Math.Max(1, layout.Where(l => !l.player).Select(l => l.index + 1).DefaultIfEmpty(1).Max()));
        FirstPlayerSlot = layout.FindIndex(l => l.player && l.index >= 9 && l.index < 36);
        if (FirstPlayerSlot < 0) FirstPlayerSlot = layout.Count;
        foreach (var l in layout)
            Slots.Add(new Slot(l.player ? inv : local, l.index, l.sx, l.sy) { IsOutput = (l.flags & 1) != 0, ArmorType = (l.flags >> 1) - 1 });
    }
}
