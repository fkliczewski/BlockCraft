using System.Text;
using System.Text.Json;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.World;

namespace BlockCraft.Net;

/// <summary>
/// Host gry sieciowej (autorytatywny). Działa w wątku gry: Tick() przed World.Tick (odbiór, ruch i akcje zdalnych graczy),
/// AfterTick() po nim (wysyłka chunków, bloków, encji, dźwięków, statystyk, ekwipunku).
/// Zdalny gracz to zwykły Player w świecie hosta (NetPuppet), z własnym PlayerController i handlerem (RemoteClient).
/// </summary>
public sealed partial class NetServer
{
    public readonly World.World Main;
    public readonly Commands Cmds;
    public readonly List<RemoteClient> Clients = new();
    private readonly List<RemoteClient> _pending = new();
    public Func<Player> HostPlayer = () => null;
    public int ViewDistance = 8;
    public int DefaultGameMode;
    public string WorldName => Main.Name;
    /// <summary>Klucze graczy z uprawnieniami operatora (komendy).</summary>
    public readonly HashSet<string> Ops = new();
    private readonly Dictionary<World.World, WorldTap> _taps = new();
    private readonly Dictionary<Entity, EntState> _ents = new();
    private readonly Dictionary<(World.World, long), long> _teHash = new();
    private RemoteClient _actor; // gracz, którego akcje teraz wykonujemy (on sam przewidział swoje dźwięki i cząsteczki)
    private long _tick;
    public long BytesSent => Clients.Sum(c => c.Conn is QueuedConnection q ? q.BytesSent : 0);

    public NetServer(World.World main)
    {
        Main = main;
        Cmds = new Commands(main);
        LoadNames();
    }

    public void Accept(INetConnection conn)
    {
        Log.Info("Sieć: nowe połączenie od " + conn.PeerName);
        _pending.Add(new RemoteClient(this, conn));
    }

    // ================= tick =================

    public void Tick()
    {
        _tick++;
        foreach (var w in NetHooks.WorldsOf(Main)) if (!_taps.ContainsKey(w)) { var t = new WorldTap(this, w); _taps[w] = t; w.Listeners.Add(t); }
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var c = _pending[i];
            if (c.Conn.IsClosed) { _pending.RemoveAt(i); continue; }
            while (c.Conn.TryReceive(out var f) && !c.LoggedIn) HandleFrame(c, f);
            if (c.LoggedIn) { _pending.RemoveAt(i); Clients.Add(c); }
            else if (c.Age++ > 20 * 30) { c.Kick("Brak logowania"); _pending.RemoveAt(i); }
        }
        foreach (var c in Clients.ToArray())
        {
            if (c.Conn.IsClosed) { Disconnected(c, c.Conn.CloseReason); continue; }
            int n = 0;
            while (n++ < 200 && c.Conn.TryReceive(out var f)) HandleFrame(c, f);
            if (_tick - c.LastPacketTick > 20 * 45) c.Kick("Przekroczono czas oczekiwania");
        }
    }

    public void AfterTick()
    {
        UpdateEntityStates();
        bool teScan = _tick % 4 == 0;
        if (teScan) ScanTileEntities();
        foreach (var c in Clients.ToArray())
        {
            if (c.Conn.IsClosed) continue;
            try { c.Update(teScan); }
            catch (Exception ex) { Log.Error("Sieć: aktualizacja " + c.Name + ": " + ex); c.Kick("Błąd hosta: " + ex.Message); }
            c.Out.FlushTo(c.Conn);
        }
        foreach (var c in _pending) c.Out.FlushTo(c.Conn);
        _actor = null;
        if (_tick % 40 == 0) BroadcastPlayerList();
    }

    public void Shutdown(string reason)
    {
        SavePlayers();
        foreach (var c in Clients.ToArray()) { c.Kick(reason); RemovePuppet(c); }
        foreach (var c in _pending) c.Kick(reason);
        Clients.Clear(); _pending.Clear();
        foreach (var (w, t) in _taps) w.Listeners.Remove(t);
        _taps.Clear();
    }

    public void SavePlayers()
    {
        foreach (var c in Clients) SavePlayer(c);
        SaveNames();
    }

    private void SavePlayer(RemoteClient c)
    {
        if (c.P == null || Main.Storage == null) return;
        try { Main.Storage.SavePlayer(c.P.ToData()); } catch (Exception ex) { Log.Error("Sieć: zapis gracza " + c.Name + ": " + ex.Message); }
    }

    private void Disconnected(RemoteClient c, string reason)
    {
        Log.Info("Sieć: " + c.Name + " rozłączony: " + reason);
        Clients.Remove(c);
        SavePlayer(c);
        RemovePuppet(c);
        Main.BroadcastMessage("§e" + c.Name + " opuścił grę");
    }

    private void RemovePuppet(RemoteClient c)
    {
        if (c.P == null) return;
        try { c.P.CloseContainer(); c.P.InventoryContainer.OnClose(c.P); } catch { }
        c.P.World.RemoveEntity(c.P);
    }

    public void Kick(RemoteClient c, string reason) { c.Kick(reason); }

    // ================= odbiór =================

    private void HandleFrame(RemoteClient c, byte[] frame)
    {
        c.LastPacketTick = _tick;
        try
        {
            foreach (var (type, r) in PacketReader.Read(frame)) Handle(c, (C2S)type, r);
        }
        catch (Exception ex) { Log.Error("Sieć: błędny pakiet od " + c.Name + ": " + ex); c.Kick("Błąd protokołu"); }
    }

    private void Handle(RemoteClient c, C2S type, BinaryReader r)
    {
        if (!c.LoggedIn && type != C2S.Login) return;
        switch (type)
        {
            case C2S.Login: Login(c, r.ReadInt32(), r.ReadString(), r.ReadByte()); break;
            case C2S.Input: c.ApplyInput(NetInput.Read(r)); break;
            case C2S.Chat: Chat(c, r.ReadString()); break;
            case C2S.Click: c.Click(r.ReadByte(), r.ReadInt16(), r.ReadByte(), (ClickType)r.ReadByte()); break;
            case C2S.Distribute:
                {
                    byte win = r.ReadByte(), btn = r.ReadByte(); int n = r.ReadByte();
                    var slots = new List<int>(); for (int i = 0; i < n; i++) slots.Add(r.ReadInt16());
                    c.Distribute(win, btn, slots);
                    break;
                }
            case C2S.CloseWindow: c.CloseWindow(r.ReadByte()); break;
            case C2S.SelectRecipe: c.SelectRecipe(r.ReadByte(), r.ReadInt32()); break;
            case C2S.EnchantItem: c.EnchantItem(r.ReadByte(), r.ReadByte()); break;
            case C2S.CreativeSlot: c.CreativeSlot(r.ReadInt16(), r.ReadStack()); break;
            case C2S.CreativeDrop: { var s = r.ReadStack(); if (c.P.Creative && s != null) c.P.DropStack(s, false); break; }
            case C2S.DropSelected: c.DropSelected(r.ReadBoolean()); break;
            case C2S.Respawn: c.Respawn(); break;
            case C2S.KeepAlive: c.P.Ping = (int)Math.Clamp(Environment.TickCount64 - r.ReadInt64(), 0, 99999); break;
        }
    }

    private void Login(RemoteClient c, int version, string nick, int view)
    {
        if (version != Protocol.Version) { c.Kick($"Inna wersja gry (host: {Protocol.Version}, Ty: {version}). Zaktualizuj BlockCraft."); return; }
        string name = UniqueName(SanitizeName(nick), c.Conn.PeerKey);
        c.Name = name;
        c.ViewDistance = Math.Clamp(view, 2, Math.Max(2, ViewDistance));
        var data = Main.Storage?.LoadPlayer(name);
        var target = data != null && Main.Dimensions != null ? Main.Dimensions.Get((DimensionId)data.Dimension) : Main;
        if (target != Main) target.EnsureDimensionArea(MathUtil.FloorToInt(data.X), MathUtil.FloorToInt(data.Z), 32);
        var p = new Player(target) { Name = name, Handler = c, NetPuppet = true, IsLocal = false };
        p.NetPuppetMove = c.ApplyQueuedMoves;
        if (data != null) p.FromData(data);
        else
        {
            p.SetGameMode(DefaultGameMode);
            p.SetPositionAndPrev(Main.SpawnX + 0.5, Main.SpawnY + 0.1, Main.SpawnZ + 0.5);
        }
        p.IsLocalOp = Ops.Contains(c.Conn.PeerKey);
        c.P = p;
        c.Ctrl = new PlayerController(target, p);
        target.AddEntityNow(p);
        c.LastX = p.X; c.LastY = p.Y; c.LastZ = p.Z;
        c.LoggedIn = true;
        c.SendLogin();
        if (p.Dead || p.Health <= 0) { p.Dead = true; p.Health = 0; c.OnDeath(p.DeathMessage ?? (name + " zginął")); }
        Main.BroadcastMessage("§e" + name + " dołączył do gry");
        Log.Info($"Sieć: zalogowano {name} ({c.Conn.PeerKey})");
    }

    private static readonly HashSet<string> FreeCommands = new() { "help", "me", "say", "list", "seed", "tell", "msg" };

    private void Chat(RemoteClient c, string s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return;
        if (s.Length > 256) s = s[..256];
        if (s.StartsWith("/"))
        {
            string cmd = s[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";
            if (!FreeCommands.Contains(cmd) && !Ops.Contains(c.Conn.PeerKey)) { c.OnMessage("§cTylko host i operatorzy mogą używać tej komendy."); return; }
            c.P.IsLocalOp = Ops.Contains(c.Conn.PeerKey);
            Log.Info($"[CHAT] {c.Name}: {s}");
            _actor = null;
            Cmds.Execute(c.P, s);
        }
        else Main.BroadcastMessage($"<{c.Name}> {s}");
    }

    public void SetOp(string key, bool op)
    {
        if (op) Ops.Add(key); else Ops.Remove(key);
        foreach (var c in Clients) if (c.Conn.PeerKey == key) { c.P.IsLocalOp = op; c.OnMessage(op ? "§aHost nadał Ci uprawnienia operatora." : "§eHost odebrał Ci uprawnienia operatora."); }
        SaveNames();
    }

    private void BroadcastPlayerList()
    {
        var list = new List<(string, int)>();
        var hp = HostPlayer();
        if (hp != null) list.Add((hp.Name, 0));
        foreach (var c in Clients) list.Add((c.Name, c.P?.Ping ?? 0));
        foreach (var c in Clients)
        {
            var w = c.Out.Begin(S2C.PlayerList);
            w.Write((byte)Math.Min(255, list.Count));
            foreach (var (n, ping) in list.Take(255)) { w.Write(n); w.Write(ping); }
            c.Out.End();
            w = c.Out.Begin(S2C.KeepAlive); w.Write(Environment.TickCount64); c.Out.End();
        }
    }

    // ================= nazwy graczy (nick przypisany do klucza publicznego) =================

    private Dictionary<string, string> _names = new(StringComparer.OrdinalIgnoreCase); // nick -> klucz
    private string NamesFile => Main.Storage != null ? Path.Combine(Main.Storage.Dir, "players", "net_names.json") : null;

    private sealed class NamesData { public Dictionary<string, string> Names { get; set; } = new(); public List<string> Ops { get; set; } = new(); }

    private void LoadNames()
    {
        try
        {
            if (NamesFile == null || !File.Exists(NamesFile)) return;
            var d = JsonSerializer.Deserialize<NamesData>(File.ReadAllText(NamesFile));
            if (d == null) return;
            _names = new Dictionary<string, string>(d.Names, StringComparer.OrdinalIgnoreCase);
            foreach (var o in d.Ops) Ops.Add(o);
        }
        catch (Exception ex) { Log.Error("Sieć: net_names.json: " + ex.Message); }
    }

    private void SaveNames()
    {
        try { if (NamesFile != null) File.WriteAllText(NamesFile, JsonSerializer.Serialize(new NamesData { Names = new(_names), Ops = Ops.ToList() })); }
        catch (Exception ex) { Log.Error("Sieć: zapis net_names.json: " + ex.Message); }
    }

    public static string SanitizeName(string n)
    {
        var s = new string((n ?? "").Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        if (s.Length > 16) s = s[..16];
        return s.Length < 2 ? "Gracz" : s;
    }

    /// <summary>Nick zajęty przez innego gracza (inny klucz) albo przez hosta dostaje cyfrę na końcu.</summary>
    private string UniqueName(string name, string key)
    {
        string baseName = name; int i = 1;
        while (true)
        {
            bool takenByHost = string.Equals(HostPlayer()?.Name, name, StringComparison.OrdinalIgnoreCase);
            bool takenOnline = Clients.Any(c => c.LoggedIn && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            bool boundElsewhere = _names.TryGetValue(name, out var k) && k != key;
            if (!takenByHost && !takenOnline && !boundElsewhere) break;
            name = baseName + (++i);
        }
        _names[name] = key;
        SaveNames();
        return name;
    }

    // ================= stan encji (liczony raz na tick, wspólny dla klientów) =================

    internal sealed class EntState
    {
        public int Qx, Qy, Qz; public byte Yaw, Pitch, Flags;
        public long EquipHash = -1; public byte[] Data; public int NetData;
        public bool Dead; public int HurtTime;
        public bool Moved, EquipChanged, DataChanged;
        public readonly List<(EntityEventKind kind, float arg)> Events = new();
    }

    internal EntState StateOf(Entity e) => _ents.TryGetValue(e, out var s) ? s : null;

    private void UpdateEntityStates()
    {
        var alive = new HashSet<Entity>();
        bool dataTick = _tick % 4 == 0;
        foreach (var w in _taps.Keys)
            foreach (var e in w.Entities)
            {
                if (e.Removed) continue;
                alive.Add(e);
                bool fresh = !_ents.TryGetValue(e, out var s);
                if (fresh) _ents[e] = s = new EntState();
                s.Events.Clear();
                int qx = Q(e.X), qy = Q(e.Y), qz = Q(e.Z);
                byte yaw = Ang(e.Yaw), pitch = Ang(e.Pitch), flags = Flags(e);
                s.Moved = qx != s.Qx || qy != s.Qy || qz != s.Qz || yaw != s.Yaw || pitch != s.Pitch || flags != s.Flags;
                s.Qx = qx; s.Qy = qy; s.Qz = qz; s.Yaw = yaw; s.Pitch = pitch; s.Flags = flags;
                long eh = EquipHash(e);
                s.EquipChanged = eh != s.EquipHash && !fresh; s.EquipHash = eh;
                s.DataChanged = false;
                int nd = e.NetData;
                if (fresh || dataTick || nd != s.NetData)
                {
                    var d = SaveEntity(e);
                    if (fresh) s.Data = d;
                    else if (d != null && (s.Data == null || !SameData(d, s.Data)) || nd != s.NetData) { s.Data = d; s.DataChanged = true; }
                    s.NetData = nd;
                }
                if (e is LivingEntity le)
                {
                    if (!fresh && le.HurtTime > s.HurtTime) s.Events.Add((EntityEventKind.Hurt, le.AttackedAtYaw));
                    s.HurtTime = le.HurtTime;
                    if (le.IsSwinging && le.SwingTicks <= 0) s.Events.Add((EntityEventKind.Swing, 0));
                    if (le.Dead && !s.Dead && !fresh) s.Events.Add((EntityEventKind.Death, 0));
                    s.Dead = le.Dead;
                }
            }
        if (_ents.Count > alive.Count) foreach (var k in _ents.Keys.Where(k => !alive.Contains(k)).ToList()) _ents.Remove(k);
    }

    internal static int Q(double v) => (int)Math.Floor(v * 32.0);
    internal static byte Ang(float deg) => (byte)(int)Math.Floor(MathUtil.WrapDegrees(deg) * 256f / 360f);
    internal static float UnAng(byte b) => (sbyte)b * 360f / 256f;

    internal static byte Flags(Entity e)
    {
        int f = 0;
        if (e is LivingEntity le) { if (le.Sneaking) f |= 1; if (le.Sprinting) f |= 2; }
        if (e.FireTicks > 0) f |= 4;
        if (e is Player p) { if (p.Sleeping) f |= 8; if (p.ItemInUse != null) f |= 16; }
        if (e.OnGround) f |= 32;
        return (byte)f;
    }

    internal static long EquipHash(Entity e)
    {
        if (e is Player p)
        {
            long h = StackUtil.Hash(p.Inventory.Selected);
            foreach (var a in p.Inventory.Armor) h = h * 31 + StackUtil.Hash(a);
            return h;
        }
        return e is LivingEntity le ? StackUtil.Hash(le.HeldItem) : 0;
    }

    /// <summary>Bajty Entity.Save (od 68. bajtu w dół to pozycja, ruch, kąty i wiek, które idą osobno).</summary>
    internal static byte[] SaveEntity(Entity e)
    {
        try
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, true)) e.Save(bw);
            return ms.ToArray();
        }
        catch (Exception ex) { Log.Error("Sieć: zapis encji " + e.TypeKey + ": " + ex.Message); return null; }
    }

    private static bool SameData(byte[] a, byte[] b)
    {
        const int skip = 68;
        if (a.Length != b.Length) return false;
        if (a.Length <= skip) return true;
        return a.AsSpan(skip).SequenceEqual(b.AsSpan(skip));
    }

    // ================= tile entity =================

    private readonly List<(World.World w, TileEntity te, byte[] data)> _teChanged = new();
    internal IReadOnlyList<(World.World w, TileEntity te, byte[] data)> ChangedTiles => _teChanged;

    private void ScanTileEntities()
    {
        _teChanged.Clear();
        var seen = new HashSet<(World.World, long)>();
        foreach (var w in _taps.Keys)
            foreach (var c in w.Chunks.Values)
                foreach (var te in c.TileEntities.Values.ToArray())
                {
                    var key = (w, PosKey(te.X, te.Y, te.Z));
                    seen.Add(key);
                    byte[] d;
                    try { d = ChunkCodec.SaveTile(te); } catch { continue; }
                    long h = te.TypeId;
                    foreach (var b in d) h = h * 131 + b;
                    if (_teHash.TryGetValue(key, out var old) && old == h) continue;
                    bool known = _teHash.ContainsKey(key);
                    _teHash[key] = h;
                    if (known || true) _teChanged.Add((w, te, d));
                }
        if (_teHash.Count > seen.Count) foreach (var k in _teHash.Keys.Where(k => !seen.Contains(k)).ToList()) _teHash.Remove(k);
    }

    internal static long PosKey(int x, int y, int z) => ((long)(x & 0x3FFFFFF) << 38) | ((long)(z & 0x3FFFFFF) << 12) | (long)(y & 0xFFF);

    // ================= zdarzenia świata =================

    internal static readonly HashSet<string> OwnerPredicted = new() { "eat", "drink", "burp", "fall_big", "fall_small", "splash", "game.player.swim" };

    private sealed class WorldTap : IWorldListener
    {
        private readonly NetServer _s; private readonly World.World _w;
        public WorldTap(NetServer s, World.World w) { _s = s; _w = w; }
        public void OnBlockChanged(int x, int y, int z, byte id, int meta)
        {
            long ck = MathUtil.ChunkKey(x >> 4, z >> 4);
            foreach (var c in _s.Clients) if (c.P?.World == _w && c.SentChunks.Contains(ck)) c.QueueBlock(x, y, z, id, meta);
        }
        public void PlaySound(string name, double x, double y, double z, float volume, float pitch)
        {
            double r = Math.Max(16, 16 * volume);
            foreach (var c in _s.Clients)
            {
                if (c == _s._actor && !LivingEntity.UnpredictedSound || c.P?.World != _w || c.P.DistanceSq(x, y, z) > r * r) continue;
                // dźwięki gracza, które jego klient odtworzył już sam (jedzenie, upadek, plusk) - inaczej słychać echo z opóźnieniem sieci
                if (OwnerPredicted.Contains(name) && c.P.DistanceSq(x, y, z) < 0.01) continue;
                var bw = c.Out.Begin(S2C.Sound);
                bw.Write(name); bw.Write(x); bw.Write(y); bw.Write(z); bw.Write(volume); bw.Write(pitch);
                c.Out.End();
            }
        }
        public void SpawnParticles(ParticleKind kind, double x, double y, double z, int count, int data)
        {
            foreach (var c in _s.Clients)
            {
                if (c == _s._actor || c.P?.World != _w || c.P.DistanceSq(x, y, z) > 48 * 48) continue;
                var bw = c.Out.Begin(S2C.Particles);
                bw.Write((byte)kind); bw.Write(x); bw.Write(y); bw.Write(z); bw.Write(count); bw.Write(data);
                c.Out.End();
            }
        }
        public void OnEntityAdded(Entity e) { }
        public void OnEntityRemoved(Entity e) { }
    }

    internal void SetActor(RemoteClient c) => _actor = c;
}
