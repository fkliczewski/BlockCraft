using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Testy sieci NET2 (gość robi to samo co gracz jednoosobowy): pęknięcia kopania, jazda, smycze, smok,
/// wygląd innych graczy, GUI, kontakt z blokami, odporność i podróże portalami. Host i dwóch gości w pamięci.</summary>
public static partial class SelfTest
{
    /// <summary>Gość podłączony pętlą w pamięci: świat zdalny, gracz i wejście wysyłane co tick jak w Game.BuildInput.</summary>
    private sealed class NetGuest
    {
        public string Nick;
        public LoopbackConnection HostSide;
        public NetClient Client;
        public readonly TestClientHost Host = new();
        public bool Started, Paused;
        /// <summary>Modyfikacja wejścia w każdym ticku (np. trzymany LPM) i jednorazowa (kliknięcie).</summary>
        public Func<NetInput, NetInput> Hold, Once;
        public Player P => Host.Player;
        public World.World W => Host.World;
        public void CloseWindow() { P.CloseContainer(); Host.SetScreen(null); }
        public T Find<T>(int id) where T : Entity => W != null && W.EntityById.TryGetValue(id, out var e) ? e as T : null;
    }

    /// <summary>Host z platformą, basenem i torami oraz dowolna liczba gości.</summary>
    private sealed class NetRig : IDisposable
    {
        public readonly World.World Hw;
        public readonly DimensionSet Dims;
        public readonly NetServer Server;
        public readonly int Gy = 70;
        public readonly List<NetGuest> Guests = new();
        public Action BeforeTick;
        public long Ticks;
        private readonly string _dir;

        public NetRig(long seed)
        {
            Hw = MakeWorld(seed, WorldType.Default, 3);
            _dir = Path.Combine(Path.GetTempPath(), "blockcraft-net2-" + Guid.NewGuid().ToString("N"));
            Hw.Storage = new WorldStorage(_dir);
            Dims = new DimensionSet(Hw);
            for (int x = -4; x < 32; x++)
                for (int z = -4; z < 32; z++)
                {
                    Hw.SetBlock(x, Gy - 1, z, B.Stone); Hw.SetBlock(x, Gy, z, B.Stone);
                    for (int y = Gy + 1; y < Gy + 14; y++) Hw.SetBlock(x, y, z, B.Air);
                }
            // basen dla łódki (źródła wody, więc nie ma przepływu) i tory wzdłuż osi X
            for (int x = 2; x <= 26; x++) for (int z = 20; z <= 23; z++) Hw.SetBlock(x, Gy, z, B.Water);
            for (int x = 2; x <= 26; x++) Hw.SetBlock(x, Gy + 1, 12, B.Rail, 1);
            Hw.SpawnX = 8; Hw.SpawnY = Gy + 1; Hw.SpawnZ = 8;
            Server = new NetServer(Hw) { ViewDistance = 3 };
        }

        public NetGuest Join(string nick)
        {
            var (a, b) = LoopbackConnection.Pair("Host", nick);
            Server.Accept(a);
            var g = new NetGuest { Nick = nick, HostSide = a, Client = NetClient.Start(b, nick, 3) };
            Guests.Add(g);
            return g;
        }

        public RemoteClient Rc(NetGuest g) => Server.Clients.FirstOrDefault(c => c.Conn == g.HostSide);
        public Player Puppet(NetGuest g) => Rc(g)?.P;

        public static NetInput Base(Player p) => new()
        {
            X = p.X, Y = p.Y, Z = p.Z, Yaw = p.Yaw, Pitch = p.Pitch, OnGround = p.OnGround, Sneaking = p.Sneaking, Sprinting = p.Sprinting,
            Jumping = p.IsJumping, Flying = p.Flying, Forward = (sbyte)Math.Sign(p.MoveForward), Strafe = (sbyte)Math.Sign(p.MoveStrafe),
            Selected = (byte)p.Inventory.SelectedSlot,
        };

        public void Step(int n = 1)
        {
            for (int k = 0; k < n; k++)
            {
                Ticks++;
                BeforeTick?.Invoke();
                Server.Tick(); Dims.Tick(); Server.AfterTick();
                Thread.Sleep(2);
                foreach (var g in Guests)
                {
                    if (g.Paused || g.Client.Conn.IsClosed && !g.Started) continue;
                    if (!g.Started && g.Client.PollLogin())
                    {
                        var L = g.Client.Login;
                        var cw = NetHooks.CreateRemoteWorld(L.Dim);
                        var p = new Player(cw) { Name = L.Name, IsLocal = true, Id = L.EntityId };
                        p.SetGameMode(L.GameMode); p.SetPositionAndPrev(L.X, L.Y, L.Z);
                        g.Host.World = cw; g.Host.Player = p;
                        g.Client.Attach(g.Host);
                        g.Started = true;
                    }
                    if (!g.Started || g.Client.G == null) continue;
                    if (!g.W.EntityById.ContainsKey(g.P.Id))
                    {
                        int pcx = MathUtil.FloorToInt(g.P.X) >> 4, pcz = MathUtil.FloorToInt(g.P.Z) >> 4, ready = 0;
                        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (g.W.GetChunk(pcx + dx, pcz + dz)?.Merged == true) ready++;
                        if (ready == 9) g.W.AddEntityNow(g.P);
                    }
                    g.Client.Tick();
                    if (g.Client.G == null) continue;
                    g.W.Tick();
                    var i = Base(g.P);
                    if (g.Hold != null) i = g.Hold(i);
                    if (g.Once != null) { i = g.Once(i); g.Once = null; }
                    g.Client.SendInput(i);
                    g.Client.Flush();
                }
                Thread.Sleep(2);
            }
        }

        /// <summary>Ustawia gracza gościa (przewidywany ruch klienta dotrze do hosta).</summary>
        public void Move(NetGuest g, double x, double y, double z, float yaw = 0, float pitch = 0)
        {
            g.P.SetPositionAndPrev(x, y, z); g.P.Yaw = g.P.PrevYaw = yaw; g.P.Pitch = g.P.PrevPitch = pitch;
            g.P.MotionX = g.P.MotionY = g.P.MotionZ = 0;
            Step(4);
        }

        public void Dispose()
        {
            foreach (var g in Guests) try { g.Client.Disconnect("koniec testu"); } catch { }
            try { Server.Shutdown("koniec testu"); } catch { }
            Dims.Dispose();
            try { Directory.Delete(_dir, true); } catch { }
        }
    }

    private static NetInput Click(NetInput i, Entity e) { i.RightPressed = true; i.HitKind = 2; i.HitEntity = e.Id; return i; }
    private static NetInput HitBlock(NetInput i, int x, int y, int z, int face = Dir.Up)
    {
        i.HitKind = 1; i.HitX = x; i.HitY = y; i.HitZ = z; i.HitFace = face; i.HitFx = .5f; i.HitFy = face == Dir.Up ? 1f : .5f; i.HitFz = .5f;
        return i;
    }

    private static void TestNet2()
    {
        using var rig = new NetRig(4321);
        int gy = rig.Gy;
        var A = rig.Join("Alfa");
        var Bg = rig.Join("Beta");
        rig.Step(40);
        Check(A.Started && Bg.Started && rig.Server.Clients.Count == 2 && A.W.EntityById.ContainsKey(A.P.Id), "Sieć2: dwóch gości w świecie hosta",
            $"{A.Client.Error} {Bg.Client.Error} klienci={rig.Server.Clients.Count}");
        if (!A.Started || !Bg.Started) return;
        var pa = rig.Puppet(A); var pb = rig.Puppet(Bg);
        rig.Move(A, 8.5, gy + 1, 8.5); rig.Move(Bg, 6.5, gy + 1, 6.5);

        TestNet2Breaking(rig, A, Bg, pa);
        TestNet2Riding(rig, A, Bg, pa);
        TestNet2Dragon(rig, A, pa);
        TestNet2Looks(rig, A, Bg, pa);
        TestNet2Gui(rig, A, pa);
        TestNet2Robust(rig, A, Bg);
        TestNet2Portals(rig, A);
    }

    // ---------- 1. pęknięcia kopania, zamach, trafienie ----------
    private static void TestNet2Breaking(NetRig rig, NetGuest A, NetGuest Bg, Player pa)
    {
        Console.WriteLine("-- Sieć2: pęknięcia kopania i animacje");
        int gy = rig.Gy;
        A.Hold = i => { i = HitBlock(i, 9, gy, 10); i.LeftDown = true; return i; };
        bool swingSeen = false;
        for (int k = 0; k < 40; k++)
        {
            rig.Step();
            if (Bg.Find<Player>(pa.Id) is { } seen && (seen.IsSwinging || seen.SwingProgress > 0)) swingSeen = true;
        }
        Check(Bg.W.DestroyProgress.TryGetValue(pa.Id, out var seenCrack) && seenCrack.X == 9 && seenCrack.Y == gy && seenCrack.Z == 10 && seenCrack.Stage >= 1,
            "Sieć2: gość B widzi pęknięcie bloku kopanego przez gościa A", $"{seenCrack}");
        Check(rig.Hw.DestroyProgress.TryGetValue(pa.Id, out var hostCrack) && hostCrack.Stage == seenCrack.Stage, "Sieć2: host widzi pęknięcie gościa");
        Check(!A.W.DestroyProgress.ContainsKey(pa.Id), "Sieć2: własne pęknięcie nie wraca do kopiącego");
        Check(swingSeen, "Sieć2: gość B widzi zamach ręki gościa A");
        A.Hold = null; rig.Step(4);
        Check(!Bg.W.DestroyProgress.ContainsKey(pa.Id) && !rig.Hw.DestroyProgress.ContainsKey(pa.Id), "Sieć2: przerwanie kopania usuwa pęknięcie u innych");

        // gracz hosta kopie: goście widzą jego pęknięcia
        var hp = new Player(rig.Hw) { Name = "Host", IsLocal = true };
        hp.SetPositionAndPrev(10.5, gy + 1, 6.5); rig.Hw.AddEntityNow(hp);
        var hctrl = new PlayerController(rig.Hw, hp);
        rig.Server.HostPlayer = () => hp; rig.Server.HostCtrl = () => hctrl;
        bool mining = true;
        rig.BeforeTick = () =>
        {
            if (!mining) { hctrl.ResetBreak(); return; }
            hctrl.Hit = new World.World.HitResult { Hit = true, X = 11, Y = gy, Z = 6, Face = Dir.Up, HitX = 11.5, HitY = gy + 1, HitZ = 6.5, Distance = 2 };
            hctrl.TickLeft(false);
        };
        rig.Step(40);
        Check(A.W.DestroyProgress.TryGetValue(hp.Id, out var hc) && hc.X == 11 && hc.Stage >= 1 && Bg.W.DestroyProgress.ContainsKey(hp.Id),
            "Sieć2: goście widzą pęknięcie bloku kopanego przez hosta");
        Check(!rig.Hw.DestroyProgress.ContainsKey(hp.Id), "Sieć2: host nie dubluje własnego pęknięcia");
        mining = false; rig.Step(3);
        Check(!A.W.DestroyProgress.ContainsKey(hp.Id), "Sieć2: koniec kopania hosta czyści pęknięcie u gości");
        rig.BeforeTick = null;
        rig.Hw.RemoveEntity(hp); rig.Server.HostPlayer = () => null; rig.Server.HostCtrl = () => null;
        rig.Step(3);

        // trafienie gościa: czerwony błysk i pochylenie kamery u niego samego, błysk u innych
        pa.HurtResistantTime = 0;
        pa.Attack(DamageSource.Generic, 1);
        rig.Step(2);
        Check(A.P.HurtTime > 0 && Bg.Find<Player>(pa.Id)?.HurtTime > 0, "Sieć2: trafiony gość widzi własne trafienie, inni też",
            $"A {A.P.HurtTime} B {Bg.Find<Player>(pa.Id)?.HurtTime}");
        pa.Health = 20; rig.Step(2);
    }

    // ---------- 2. jazda: łódka, wagonik, koń, świnia, smycz ----------
    private static void TestNet2Riding(NetRig rig, NetGuest A, NetGuest Bg, Player pa)
    {
        Console.WriteLine("-- Sieć2: jazda gościa");
        int gy = rig.Gy;
        var rc = rig.Rc(A);
        pa.Inventory.SelectedSlot = 8; A.P.Inventory.SelectedSlot = 8;

        // łódka
        var boat = new BoatEntity(rig.Hw);
        boat.SetPositionAndPrev(5.5, gy + .6, 21.5); boat.Yaw = boat.PrevYaw = -90;
        rig.Hw.AddEntityNow(boat);
        rig.Move(A, 5.5, gy + 1, 18.5, -90);
        rig.Step(10);
        A.Once = i => Click(i, boat);
        rig.Step(4);
        Check(pa.RidingEntity == boat && A.P.RidingEntity?.Id == boat.Id && Bg.Find<Player>(pa.Id)?.RidingEntity?.Id == boat.Id,
            "Sieć2: gość wsiada do łódki (host, on sam i inny gość)", $"host={pa.RidingEntity?.TypeKey} A={A.P.RidingEntity?.Id} boat={boat.Id}");
        int tp0 = rc.TeleportId; double bx0 = boat.X, maxGap = 0;
        A.P.MoveForward = 1; A.P.Yaw = -90;
        for (int k = 0; k < 70; k++)
        {
            rig.Step();
            if (A.Find<BoatEntity>(boat.Id) is { } cb) maxGap = Math.Max(maxGap, Math.Sqrt(A.P.DistanceSq(cb.X, A.P.Y, cb.Z)));
        }
        Check(boat.X - bx0 > 1.5, "Sieć2: gość steruje łódką (host liczy ruch z wejścia gościa)", $"przesunięcie {boat.X - bx0:F2}");
        Check(maxGap < 1.2 && rc.TeleportId == tp0, "Sieć2: gość siedzi w łódce bez teleportów", $"odstęp {maxGap:F2}, teleporty {rc.TeleportId - tp0}");
        A.P.MoveForward = 0; A.P.Sneaking = true; rig.Step(3); A.P.Sneaking = false; rig.Step(6);
        Check(pa.RidingEntity == null && boat.Passenger == null && A.P.RidingEntity == null && Bg.Find<Player>(pa.Id)?.RidingEntity == null,
            "Sieć2: wysiadanie z łódki po obu stronach");
        Check(Math.Abs(A.P.X - pa.X) < .1 && Math.Abs(A.P.Z - pa.Z) < .1, "Sieć2: pozycja po wysiadaniu zgodna z hostem",
            $"klient {A.P.X:F2},{A.P.Z:F2} host {pa.X:F2},{pa.Z:F2}");
        boat.Kill(); rig.Step(2);

        // wagonik na torach
        var cart = new MinecartEntity(rig.Hw);
        cart.SetPositionAndPrev(4.5, gy + 1.0625, 12.5); rig.Hw.AddEntityNow(cart);
        rig.Move(A, 4.5, gy + 1, 10.5, -90);
        A.Once = i => Click(i, cart);
        rig.Step(4);
        Check(pa.RidingEntity == cart && A.P.RidingEntity?.Id == cart.Id, "Sieć2: gość wsiada do wagonika");
        double cx0 = cart.X;
        A.P.MoveForward = 1; A.P.Yaw = -90;
        rig.Step(40);
        Check(cart.X - cx0 > .5 && Math.Abs(A.P.X - (A.Find<MinecartEntity>(cart.Id)?.X ?? 0)) < 1.2, "Sieć2: gość popycha wagonik i jedzie w nim",
            $"przesunięcie {cart.X - cx0:F2}");
        A.P.MoveForward = 0; A.P.Sneaking = true; rig.Step(3); A.P.Sneaking = false; rig.Step(6);
        Check(pa.RidingEntity == null && A.P.RidingEntity == null, "Sieć2: wysiadanie z wagonika");
        cart.Kill(); rig.Step(2);

        // koń z siodłem
        var horse = new Horse(rig.Hw) { Tamed = true };
        horse.Inv.Set(0, new ItemStack(I.Saddle)); horse.Saddled = true;
        horse.SetPositionAndPrev(12.5, gy + 1, 4.5); rig.Hw.AddEntityNow(horse);
        rig.Move(A, 10.5, gy + 1, 4.5);
        rig.Step(30);
        A.Once = i => Click(i, horse);
        rig.Step(4);
        Check(pa.RidingMob == horse && horse.Rider == pa && A.P.RidingMob?.Id == horse.Id && Bg.Find<Player>(pa.Id)?.RidingMob?.Id == horse.Id,
            "Sieć2: gość wsiada na konia (host, on sam i inny gość)", $"host={pa.RidingMob?.Id} A={A.P.RidingMob?.Id} koń={horse.Id} siodło={horse.Saddled}");
        double hz0 = horse.Z; tp0 = rc.TeleportId;
        A.P.MoveForward = 1; A.P.Yaw = 0;
        rig.Step(30);
        var ch = A.Find<Horse>(horse.Id);
        Check(horse.Z - hz0 > 1.5 && ch != null && Math.Abs(A.P.Z - ch.Z) < 1, "Sieć2: gość kieruje koniem", $"przesunięcie {horse.Z - hz0:F2}, gość fwd {pa.MoveForward} koń fwd {horse.MoveForward} speed {horse.AiMoveSpeed}/{horse.MoveSpeed} siodło {horse.Saddled} oswojony {horse.Tamed} ziemia {horse.OnGround} rider {horse.Rider?.Name} rear {horse.RearTicks}");
        Check(rc.TeleportId == tp0, "Sieć2: jazda konno bez teleportów", (rc.TeleportId - tp0).ToString());
        A.P.MoveForward = 0; A.P.Sneaking = true; rig.Step(3); A.P.Sneaking = false; rig.Step(6);
        Check(pa.RidingMob == null && horse.Rider == null && A.P.RidingMob == null && ch?.Rider == null, "Sieć2: zsiadanie z konia");
        horse.Kill(); rig.Step(2);

        // świnia z siodłem i marchewka na kiju
        var pig = new Pig(rig.Hw) { Saddled = true };
        pig.SetPositionAndPrev(16.5, gy + 1, 4.5); rig.Hw.AddEntityNow(pig);
        pa.Inventory.Main[7] = new ItemStack(I.CarrotOnAStick); pa.Inventory.SelectedSlot = 7; A.P.Inventory.SelectedSlot = 7;
        rig.Move(A, 14.5, gy + 1, 4.5);
        A.Once = i => Click(i, pig);
        rig.Step(4);
        Check(pa.RidingMob == pig && A.P.RidingMob?.Id == pig.Id, "Sieć2: gość wsiada na świnię");
        double pz0 = pig.Z; A.P.Yaw = 0;
        rig.Step(50);
        Check(pig.Z - pz0 > 1, "Sieć2: świnia idzie za marchewką na kiju gościa", $"przesunięcie {pig.Z - pz0:F2}");
        A.Once = i => { i.RightPressed = true; return i; };
        rig.Step(4);
        Check(pa.Inventory.Main[7]?.Damage == 7 && A.P.Inventory.Main[7]?.Damage == 7, "Sieć2: przyspieszenie świni zużywa marchewkę na kiju (host)",
            $"{pa.Inventory.Main[7]?.Damage}/{A.P.Inventory.Main[7]?.Damage}");
        A.P.Sneaking = true; rig.Step(3); A.P.Sneaking = false; rig.Step(6);
        Check(pa.RidingMob == null && A.P.RidingMob == null, "Sieć2: zsiadanie ze świni");
        pig.Kill(); pa.Inventory.Main[7] = null; rig.Step(2);

        // smycz
        var cow = new Cow(rig.Hw);
        cow.SetPositionAndPrev(6.5, gy + 1, 4.5); rig.Hw.AddEntityNow(cow);
        pa.Inventory.Main[6] = new ItemStack(I.Lead); pa.Inventory.SelectedSlot = 6; A.P.Inventory.SelectedSlot = 6;
        rig.Move(A, 6.5, gy + 1, 6.5, 180);
        A.Once = i => Click(i, cow);
        rig.Step(4);
        Check(cow.LeashHolder == pa && A.Find<Cow>(cow.Id)?.LeashHolder == A.P && Bg.Find<Cow>(cow.Id)?.LeashHolder?.Id == pa.Id,
            "Sieć2: smycz gościa widoczna u niego i u innych");
        A.Once = i => Click(i, cow);
        rig.Step(4);
        Check(cow.LeashHolder == null && A.Find<Cow>(cow.Id)?.LeashHolder == null && Bg.Find<Cow>(cow.Id)?.LeashHolder == null, "Sieć2: zdjęcie smyczy po obu stronach");
        cow.Kill();
        foreach (var e in rig.Hw.Entities.OfType<ItemEntity>().ToList()) e.Kill();
        pa.Inventory.Main[6] = null;
        rig.Step(3);
    }

    // ---------- 3. smok Endu: części w protokole ----------
    private static void TestNet2Dragon(NetRig rig, NetGuest A, Player pa)
    {
        Console.WriteLine("-- Sieć2: smok Endu");
        int gy = rig.Gy;
        rig.Move(A, 8.5, gy + 1, 5.5);
        var dragon = new EnderDragon(rig.Hw) { NoAI = true };
        dragon.SetPositionAndPrev(8.5, gy + 3, 14); dragon.Yaw = dragon.PrevYaw = 180;
        rig.Hw.AddEntityNow(dragon);
        rig.Step(6);
        var cd = A.Find<EnderDragon>(dragon.Id);
        Check(cd != null && cd.Parts != null && cd.Parts.All(p => p.Id < 0 && A.W.EntityById.TryGetValue(p.Id, out var q) && q == p),
            "Sieć2: klient tworzy części smoka z ID wyliczonym z ID smoka");
        Check(!A.W.Entities.Any(e => e is DragonPart && e.Id > 0), "Sieć2: host nie wysyła swoich części smoka");
        float hp0 = dragon.Health;
        A.Once = i => { i.LeftPressed = true; i.HitKind = 2; i.HitEntity = NetLinks.DragonPartId(dragon.Id, 1); return i; };
        rig.Step(3);
        Check(dragon.Health < hp0, "Sieć2: gość trafia smoka w część ciała", $"{hp0} -> {dragon.Health}");
        BossStatus.BossName = null; A.Client.Tick();
        Check(BossStatus.BossName == dragon.DisplayName, "Sieć2: pasek zdrowia smoka u gościa");
        rig.Hw.RemoveEntity(dragon);
        rig.Step(4);
        Check(!A.W.Entities.OfType<DragonPart>().Any() && !A.W.Entities.OfType<EnderDragon>().Any(), "Sieć2: usunięcie smoka usuwa jego części u gościa");
        pa.Health = 20; BossStatus.BossName = null;
    }

    // ---------- 4. wygląd gościa u innych ----------
    private static void TestNet2Looks(NetRig rig, NetGuest A, NetGuest Bg, Player pa)
    {
        Console.WriteLine("-- Sieć2: wygląd innych graczy");
        int gy = rig.Gy;
        rig.Move(A, 8.5, gy + 1, 8.5, 90);
        var inv = pa.Inventory;
        inv.Main[0] = new ItemStack(I.DiamondSword); inv.Main[1] = new ItemStack(I.Bow); inv.Main[2] = new ItemStack(I.Arrow, 16);
        inv.Main[3] = new ItemStack(I.FishingRod); inv.Main[4] = new ItemStack(I.Bread, 4);
        inv.Armor[0] = new ItemStack(I.IronHelmet);
        inv.SelectedSlot = 0; A.P.Inventory.SelectedSlot = 0;
        rig.Step(4);
        Player Seen() => Bg.Find<Player>(pa.Id);
        Check(Seen()?.Inventory.Selected?.Id == I.DiamondSword && Seen()?.Inventory.Armor[0]?.Id == I.IronHelmet, "Sieć2: inni widzą przedmiot w ręce i zbroję gościa");
        // blokowania mieczem nie ma w grze jednoosobowej (żaden przedmiot nie ma UseAction.Block); renderer ma już pozę na ten przypadek
        A.P.Sneaking = true; rig.Step(3);
        Check(Seen()?.Sneaking == true && pa.Sneaking, "Sieć2: skradanie widoczne u innych");
        A.P.Sneaking = false; rig.Step(3);
        Check(Seen()?.Sneaking == false, "Sieć2: koniec skradania");

        A.P.Inventory.SelectedSlot = 1; A.Hold = i => { i.RightDown = true; return i; };
        rig.Step(15);
        Check(Seen()?.ItemInUse?.Id == I.Bow, "Sieć2: naciąganie łuku widoczne u innych");
        A.Hold = null; rig.Step(4);
        Check(rig.Hw.Entities.OfType<ArrowEntity>().Any() && Bg.W.Entities.OfType<ArrowEntity>().Any(), "Sieć2: strzała gościa wystrzelona przez hosta i widoczna u innych");
        foreach (var e in rig.Hw.Entities.OfType<ArrowEntity>().ToList()) e.Kill();

        A.P.Inventory.SelectedSlot = 3; rig.Step(2);
        A.Once = i => { i.RightPressed = true; return i; };
        rig.Step(4);
        Check(pa.FishHook != null && Seen()?.FishHook is { Removed: false } h && h.Angler == Seen(), "Sieć2: zarzucona wędka gościa widoczna u innych (spławik i żyłka)");
        A.Once = i => { i.RightPressed = true; return i; };
        rig.Step(4);
        Check(Seen()?.FishHook == null, "Sieć2: zwinięta wędka u innych");

        pa.FoodLevel = 10; A.P.Inventory.SelectedSlot = 4; rig.Step(2);
        A.Hold = i => { i.RightDown = true; return i; };
        rig.Step(6);
        Check(Seen()?.ItemInUse?.Id == I.Bread, "Sieć2: jedzenie widoczne u innych");
        rig.Step(30);
        A.Hold = null; rig.Step(2);
        Check(pa.FoodLevel > 10 && inv.Main[4]?.Count == 3, "Sieć2: gość zjada chleb (host rozlicza)", $"głód {pa.FoodLevel}, chleb {inv.Main[4]?.Count}");

        pa.Sleeping = true; rig.Step(3);
        Check(Seen()?.Sleeping == true, "Sieć2: sen w łóżku widoczny u innych");
        pa.Sleeping = false; rig.Step(3);
        A.P.Inventory.SelectedSlot = 8; inv.Armor[0] = null;
        for (int s = 0; s < 5; s++) inv.Main[s] = null;
        rig.Step(3);
    }

    // ---------- 5. GUI i interakcje z blokami ----------
    private static void TestNet2Gui(NetRig rig, NetGuest A, Player pa)
    {
        Console.WriteLine("-- Sieć2: GUI i bloki u gościa");
        int gy = rig.Gy;
        A.Client.ActivateHooks(); // dwóch klientów w jednym procesie: kliknięcia okien idą od gościa A
        rig.Move(A, 8.5, gy + 1, 8.5);
        // piec: paliwo, postęp i wynik
        rig.Hw.SetBlock(10, gy + 1, 8, B.Furnace);
        var fte = (FurnaceTileEntity)rig.Hw.GetTileEntity(10, gy + 1, 8);
        fte.Slots[0] = new ItemStack(B.IronOre); fte.Slots[1] = new ItemStack(I.Coal);
        rig.Step(6);
        pa.OpenBlockGui(GuiKind.Furnace, 10, gy + 1, 8);
        rig.Step(40);
        var cf = A.P.OpenContainer as FurnaceContainer;
        Check(cf != null && cf.Furnace.BurnTime > 0 && cf.Furnace.CookTime > 0, "Sieć2: wskaźniki paliwa i postępu pieca u gościa",
            $"{A.P.OpenContainer?.GetType().Name} spalanie {cf?.Furnace.BurnTime} postęp {cf?.Furnace.CookTime}");
        rig.Step(170);
        Check(cf?.Slots[2].Stack?.Id == I.IronIngot, "Sieć2: wynik pieca u gościa");
        if (cf != null) { cf.Click(2, 0, ClickType.QuickMove, A.P); rig.Step(4); }
        Check(pa.Inventory.CountOf(I.IronIngot) == 1 && A.P.Inventory.CountOf(I.IronIngot) == 1 && fte.Slots[2] == null, "Sieć2: odbiór z pieca rozliczony przez hosta",
            $"host {pa.Inventory.CountOf(I.IronIngot)} gość {A.P.Inventory.CountOf(I.IronIngot)} wyjście {fte.Slots[2]?.Count} okno {pa.OpenContainer?.GetType().Name}/{A.P.OpenContainer?.GetType().Name} kursor {pa.Cursor?.Id} zajęte {pa.Inventory.Main.Count(x => x != null)}/{A.P.Inventory.Main.Count(x => x != null)} [{string.Join(",", pa.Inventory.Main.Select(x => x?.Id ?? 0))}]");
        A.CloseWindow(); rig.Step(3);

        // statyw alchemiczny: czas warzenia
        rig.Hw.SetBlock(10, gy + 1, 9, B.BrewingStand);
        var bte = (BrewingStandTile)rig.Hw.GetTileEntity(10, gy + 1, 9);
        bte.Items[0] = new ItemStack(I.Potion); bte.Items[3] = new ItemStack(I.NetherWartItem);
        rig.Step(6);
        pa.OpenBlockGui(GuiKind.Brewing, 10, gy + 1, 9);
        rig.Step(10);
        var cbt = A.W.GetTileEntity(10, gy + 1, 9) as BrewingStandTile;
        Check(A.P.OpenContainer is BrewingContainer && bte.BrewTime is > 0 and < 400 && cbt?.BrewTime is > 0 and < 400, "Sieć2: czas warzenia u gościa",
            $"host {bte.BrewTime} gość {cbt?.BrewTime}");
        A.CloseWindow(); rig.Step(3);

        // stół rzemieślniczy: siatka i wynik liczy host
        rig.Hw.SetBlock(11, gy + 1, 8, B.CraftingTable);
        pa.OpenBlockGui(GuiKind.Crafting, 11, gy + 1, 8);
        pa.Cursor = new ItemStack(B.OakLog, 1);
        rig.Step(4);
        if (A.P.OpenContainer is WorkbenchContainer wb)
        {
            wb.Click(1, 0, ClickType.Pickup, A.P); rig.Step(4);
            var hwb = pa.OpenContainer as WorkbenchContainer;
            Check(wb.Slots[0].Stack?.Id == B.OakPlanks && wb.Slots[0].Stack.Count == 4, "Sieć2: wynik wytwarzania u gościa",
                $"gość {wb.Slots[0].Stack?.Id}x{wb.Slots[0].Stack?.Count} siatka {wb.Slots[1].Stack?.Id}; host {hwb?.Slots[0].Stack?.Id} siatka {hwb?.Slots[1].Stack?.Id} kursor {pa.Cursor?.Id}/{A.P.Cursor?.Id} okno {pa.OpenContainer?.GetType().Name}/{A.P.OpenContainer?.GetType().Name} [{string.Join(",", pa.Inventory.Main.Select(x => x?.Id ?? 0))}]");
            wb.Click(0, 0, ClickType.QuickMove, A.P); rig.Step(4);
            Check(pa.Inventory.CountOf(B.OakPlanks) == 4 && A.P.Inventory.CountOf(B.OakPlanks) == 4, "Sieć2: wytwarzanie gościa rozliczone przez hosta");
        }
        else Check(false, "Sieć2: GUI stołu rzemieślniczego u gościa", A.P.OpenContainer?.GetType().Name);
        A.CloseWindow(); rig.Step(3);

        // podajnik
        rig.Hw.SetBlock(12, gy + 1, 8, B.Dropper);
        rig.Step(4);
        pa.OpenBlockGui(GuiKind.Dispenser, 12, gy + 1, 8);
        rig.Step(4);
        Check(A.P.OpenContainer is RsContainer { Title: "Podajnik" } && A.P.OpenContainer.Slots.Count == 45, "Sieć2: GUI podajnika u gościa", A.P.OpenContainer?.Title);
        A.CloseWindow(); rig.Step(3);

        // mapa: gość tworzy mapę, host rysuje i przesyła piksele
        pa.Inventory.Main[5] = new ItemStack(I.EmptyMap); pa.Inventory.SelectedSlot = 5; A.P.Inventory.SelectedSlot = 5;
        rig.Step(3);
        A.Once = i => { i.RightPressed = true; return i; };
        rig.Step(20);
        var map = pa.Inventory.Main[5];
        var md = map?.Id == I.FilledMap ? MapStore.Client.Get(map.Damage) : null;
        Check(map?.Id == I.FilledMap && md != null && md.Colors.Any(c => c != 0), "Sieć2: mapa gościa wypełniona przez hosta i przesłana");
        pa.Inventory.Main[5] = null; A.P.Inventory.SelectedSlot = 8;

        // płyta naciskowa: gość naciska ją swoim ruchem (kontakt z blokami na hoście)
        rig.Hw.SetBlock(14, gy + 1, 8, B.StonePressurePlate);
        rig.Step(3);
        rig.Move(A, 14.5, gy + 1, 8.5);
        rig.Step(3);
        Check((rig.Hw.GetMeta(14, gy + 1, 8) & 1) != 0 && (A.W.GetMeta(14, gy + 1, 8) & 1) != 0, "Sieć2: gość naciska płytę naciskową");
        rig.Move(A, 8.5, gy + 1, 8.5);
        rig.Step(30);
        Check((rig.Hw.GetMeta(14, gy + 1, 8) & 1) == 0, "Sieć2: płyta zwolniona po zejściu gościa");
        foreach (var (x, z) in new[] { (10, 8), (10, 9), (11, 8), (12, 8), (14, 8) }) rig.Hw.SetBlock(x, gy + 1, z, B.Air);
        foreach (var e in rig.Hw.Entities.OfType<ItemEntity>().ToList()) e.Kill();
        rig.Step(3);
    }

    // ---------- 6. odporność ----------
    private static void TestNet2Robust(NetRig rig, NetGuest A, NetGuest Bg)
    {
        Console.WriteLine("-- Sieć2: odporność");
        int gy = rig.Gy;
        // dużo encji: 600 przedmiotów
        var items = new List<ItemEntity>();
        for (int x = 0; x < 24; x++)
            for (int z = 0; z < 25; z++)
            {
                // siatka 24 x 8 x 3 piętra co 1 blok: przedmioty się nie łączą i nikt ich nie podnosi
                var it = new ItemEntity(rig.Hw, x + .5, gy + 1 + (z / 8) * 1.5, 24.5 + (z % 8), new ItemStack(z / 8 == 0 ? B.Stone : z / 8 == 1 ? B.Dirt : B.Cobblestone, 1));
                it.SetPositionAndPrev(x + .5, gy + 1 + (z / 8) * 1.5, 24.5 + (z % 8));
                it.PickupDelay = 32767;
                rig.Hw.AddEntityNow(it); items.Add(it);
            }
        var swAfter = new Stopwatch();
        long bytes0 = rig.Server.BytesSent;
        double worst = 0;
        for (int k = 0; k < 30; k++)
        {
            rig.Server.Tick(); rig.Dims.Tick();
            swAfter.Restart(); rig.Server.AfterTick(); swAfter.Stop();
            worst = Math.Max(worst, swAfter.Elapsed.TotalMilliseconds);
            rig.Step(1);
        }
        int seen = A.W.Entities.OfType<ItemEntity>().Count(), onHost = rig.Hw.Entities.OfType<ItemEntity>().Count();
        long kb = (rig.Server.BytesSent - bytes0) / 1024;
        Console.WriteLine($"    600 przedmiotów: najdłuższy AfterTick {worst:F1} ms, {kb} KB do 2 gości w 60 tickach, gość widzi {seen} z {onHost} u hosta");
        Check(onHost > 500 && seen == onHost, "Sieć2: gość dostaje wszystkie z ok. 600 encji", $"{seen}/{onHost}");
        Check(worst < 40, "Sieć2: wysyłka 600 encji mieści się w ticku", $"{worst:F1} ms");
        foreach (var it in rig.Hw.Entities.OfType<ItemEntity>().ToList()) it.Kill();
        rig.Step(4);
        Check(!A.W.Entities.OfType<ItemEntity>().Any(), "Sieć2: usunięcie 600 encji u gościa");

        // zator: gość nie odbiera i nie wysyła przez 3 s gry, potem dostaje wszystko naraz
        var pa = rig.Puppet(A);
        A.Paused = true;
        pa.Inventory.Main[1] = new ItemStack(I.Diamond, 7);
        rig.Hw.SetBlock(9, gy + 1, 9, B.Stone);
        rig.Step(60);
        A.Paused = false;
        rig.Step(10);
        Check(rig.Rc(A) != null && A.P.Inventory.Main[1]?.Count == 7 && A.W.GetBlock(9, gy + 1, 9) == B.Stone, "Sieć2: po zatorze gość nadrabia stan");
        Check(Math.Abs(A.P.X - pa.X) < .05 && Math.Abs(A.P.Z - pa.Z) < .05, "Sieć2: po zatorze pozycja zgodna");
        rig.Hw.SetBlock(9, gy + 1, 9, B.Air);

        // paczka 90 wejść naraz (przekaźnik oddał zaległe ramki)
        for (int k = 0; k < 90; k++) A.Client.SendInput(NetRig.Base(A.P));
        A.Client.Flush();
        rig.Step(6);
        Check(rig.Rc(A) != null && rig.Rc(A).PendingInputs <= 3, "Sieć2: zaległe wejścia nadrobione bez rozłączenia", rig.Rc(A)?.PendingInputs.ToString());

        // uszkodzona ramka od obcego połączenia nie psuje gry innym
        var (a3, b3) = LoopbackConnection.Pair("Host", "Zly");
        rig.Server.Accept(a3);
        b3.Send(new byte[] { 50, 0, 0, 0, 1, 2, 3 });
        rig.Step(4);
        Check(a3.IsClosed && rig.Server.Clients.Count == 2, "Sieć2: uszkodzone dane rozłączają tylko nadawcę");

        // rozłączenie w łódce i powrót: ekwipunek, pozycja i miejsce w łódce
        var boat = new BoatEntity(rig.Hw);
        boat.SetPositionAndPrev(8.5, gy + .6, 21.5); rig.Hw.AddEntityNow(boat);
        rig.Move(A, 8.5, gy + 1, 18.5);
        A.Once = i => Click(i, boat);
        rig.Step(4);
        Check(pa.RidingEntity == boat, "Sieć2: gość w łódce przed rozłączeniem");
        A.Client.Disconnect("test");
        rig.Step(4);
        Check(rig.Server.Clients.Count == 1 && boat.Passenger == null, "Sieć2: rozłączenie zwalnia łódkę");
        rig.Guests.Remove(A);
        var A2 = rig.Join("Alfa");
        rig.Step(30);
        var pa2 = rig.Puppet(A2);
        Check(A2.Started && A2.P.Name == "Alfa" && pa2 != null && pa2.Inventory.Main[1]?.Count == 7 && A2.P.Inventory.Main[1]?.Count == 7,
            "Sieć2: powrót gościa z tym samym kluczem przywraca ekwipunek");
        Check(pa2?.RidingEntity == boat && A2.P.RidingEntity?.Id == boat.Id, "Sieć2: po powrocie gość siedzi w tej samej łódce",
            $"host {pa2?.RidingEntity?.Id} gość {A2.P.RidingEntity?.Id} łódka {boat.Id}");
        A2.P.Sneaking = true; rig.Step(3); A2.P.Sneaking = false; rig.Step(4);
        boat.Kill();
        rig.Step(2);
        _net2Guest = A2; // dalsze testy działają na nowym połączeniu
    }

    private static NetGuest _net2Guest;

    // ---------- 7. portale Netheru i Endu ----------
    private static void TestNet2Portals(NetRig rig, NetGuest _)
    {
        Console.WriteLine("-- Sieć2: portale gościa");
        var A = _net2Guest;
        if (A == null) { Check(false, "Sieć2: brak gościa do testu portali"); return; }
        int gy = rig.Gy;
        var pa = rig.Puppet(A);
        pa.SetGameMode(0);
        rig.Step(3);
        // portal Endu i powrót przez portal w Endzie
        rig.Hw.SetBlock(24, gy + 1, 4, B.EndPortal);
        rig.Step(2);
        rig.Move(A, 24.5, gy + 1, 4.5);
        for (int k = 0; k < 40 && A.W.Dimension != DimensionId.End; k++) rig.Step();
        Check(A.W.Dimension == DimensionId.End && pa.World.Dimension == DimensionId.End, "Sieć2: gość przechodzi do Endu",
            $"gość {A.W.Dimension} host {pa.World.Dimension} {pa.X:F2},{pa.Y:F2},{pa.Z:F2} blok {rig.Hw.GetBlock(24, gy + 1, 4)}");
        rig.Step(30);
        var end = pa.World;
        int ex = MathUtil.FloorToInt(pa.X) + 1, ey = MathUtil.FloorToInt(pa.Y), ez = MathUtil.FloorToInt(pa.Z);
        end.SetBlock(ex, ey, ez, B.EndPortal);
        rig.Step(2);
        rig.Move(A, ex + .5, ey, ez + .5);
        for (int k = 0; k < 40 && A.W.Dimension != DimensionId.Overworld; k++) rig.Step();
        rig.Step(30);
        Check(A.W.Dimension == DimensionId.Overworld && pa.World == rig.Hw && Math.Abs(pa.X - (rig.Hw.SpawnX + .5)) < 3,
            "Sieć2: powrót z Endu do punktu odrodzenia w Overworldzie", $"{A.W.Dimension} {pa.X:F1},{pa.Z:F1}");
        // portal Netheru w ramie z obsydianu; gość stoi w nim 80 ticków jak w trybie przetrwania
        DimensionPortals.BuildFrame(rig.Hw, 20, gy + 1, 4, 1);
        bool lit = DimensionPortals.Ignite(rig.Hw, 20, gy + 1, 4);
        rig.Step(3);
        rig.Move(A, 20.5, gy + 1, 4.5);
        for (int k = 0; k < 120 && A.W.Dimension != DimensionId.Nether; k++) rig.Step();
        Check(lit && A.W.Dimension == DimensionId.Nether && pa.World.Dimension == DimensionId.Nether && A.P.Id == pa.Id,
            "Sieć2: gość przechodzi portalem do Netheru", $"zapalony {lit}, gość {A.W.Dimension}, host {pa.World.Dimension}");
        rig.Step(30);
        Check(A.W.EntityById.ContainsKey(A.P.Id) && A.W.Chunks.Count >= 9 && Math.Abs(A.P.Y - pa.Y) < .1, "Sieć2: gość stoi w Netherze na terenie od hosta",
            $"chunki {A.W.Chunks.Count}, y {A.P.Y:F2}/{pa.Y:F2}");
        // powrót: wyjście z portalu i ponowne wejście
        var nether = pa.World;
        (int x, int y, int z)? portal = null;
        int px = MathUtil.FloorToInt(pa.X), py = MathUtil.FloorToInt(pa.Y), pz = MathUtil.FloorToInt(pa.Z);
        for (int dx = -2; dx <= 2 && portal == null; dx++) for (int dz = -2; dz <= 2 && portal == null; dz++) for (int dy = -1; dy <= 2 && portal == null; dy++)
            if (nether.GetBlock(px + dx, py + dy, pz + dz) == B.Portal) portal = (px + dx, py + dy, pz + dz);
        if (portal is { } pp)
        {
            bool alongX = nether.GetBlock(pp.x + 1, pp.y, pp.z) == B.Portal || nether.GetBlock(pp.x - 1, pp.y, pp.z) == B.Portal;
            double ox = alongX ? 0 : 1.2, oz = alongX ? 1.2 : 0;
            while (nether.GetBlock(pp.x, pp.y - 1, pp.z) == B.Portal) pp = (pp.x, pp.y - 1, pp.z);
            rig.Move(A, pp.x + .5 + ox, pp.y, pp.z + .5 + oz);
            rig.Step(20);
            rig.Move(A, pp.x + .5, pp.y, pp.z + .5);
            for (int k = 0; k < 120 && A.W.Dimension != DimensionId.Overworld; k++) rig.Step();
        }
        Check(portal != null && A.W.Dimension == DimensionId.Overworld && pa.World == rig.Hw, "Sieć2: gość wraca portalem do Overworldu", $"portal {portal}, gość {A.W.Dimension}");
        rig.Step(20);
        // po podróżach gość nadal stawia bloki
        rig.Hw.SetBlock(24, gy + 1, 4, B.Air);
        pa.Inventory.Main[0] = new ItemStack(B.Stone, 2); pa.Inventory.SelectedSlot = 0; A.P.Inventory.SelectedSlot = 0;
        rig.Move(A, rig.Hw.SpawnX + .5, gy + 1, rig.Hw.SpawnZ + .5);
        rig.Step(6);
        A.Once = i => { i = HitBlock(i, 10, gy, 9); i.RightPressed = true; return i; };
        rig.Step(4);
        Check(rig.Hw.GetBlock(10, gy + 1, 9) == B.Stone && A.W.GetBlock(10, gy + 1, 9) == B.Stone, "Sieć2: po podróżach gość stawia blok w Overworldzie");
    }
}
