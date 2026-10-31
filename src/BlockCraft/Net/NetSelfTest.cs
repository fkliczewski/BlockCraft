using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Gui;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.World;
using Tailcat.Link.Storage;
using Tailcat.TestSupport;

namespace BlockCraft;

/// <summary>Testy sieci (moduł G) w --selftest: protokół, host+klient w pamięci, Tailcat przez przekaźnik w pamięci.</summary>
public static partial class SelfTest
{
    private sealed class TestClientHost : INetClientHost
    {
        public World.World World { get; set; }
        public Player Player { get; set; }
        public Screen CurrentScreen { get; set; }
        public readonly List<string> Chat = new();
        public string Disconnected;
        public void SetScreen(Screen s) => CurrentScreen = s;
        public void AddChat(string s) => Chat.Add(s);
        public void NetDisconnected(string reason) => Disconnected = reason;
        public void NetChangeWorld(World.World next) { World = next; Player.World = next; }
    }

    private static void TestNet()
    {
        Console.WriteLine("-- Sieć: protokół");
        TestNetProtocol();
        Console.WriteLine("-- Sieć: host i klient w pamięci");
        TestNetLoopback();
        Console.WriteLine("-- Sieć: Tailcat.Link przez przekaźnik w pamięci");
        TestNetTailcat().GetAwaiter().GetResult();
    }

    private static void TestNetProtocol()
    {
        var i = new NetInput { X = 1.5, Y = 70.25, Z = -3.125, Yaw = 91, Pitch = -12, OnGround = true, Sneaking = true, Forward = 1, Strafe = -1, Selected = 4,
            LeftDown = true, RightPressed = true, HitKind = 1, HitX = 5, HitY = 64, HitZ = -7, HitFace = 1, HitFx = 0.5f, HitFy = 1f, HitFz = 0.25f, TeleportAck = 3 };
        var buf = new PacketBuffer();
        var w = buf.Begin(C2S.Input); i.Write(w); buf.End();
        w = buf.Begin(C2S.Chat); w.Write("zażółć gęślą jaźń"); buf.End();
        w = buf.Begin(S2C.Cursor); w.WriteStack(new ItemStack(I.Diamond, 7)); buf.End();
        var frame = buf.Take();
        var packets = PacketReader.Read(frame).ToList();
        Check(packets.Count == 3, "Sieć: ramka z 3 pakietami");
        var i2 = NetInput.Read(packets[0].r);
        Check(i2.X == i.X && i2.Y == i.Y && i2.Z == i.Z && i2.Yaw == i.Yaw && i2.Sneaking && i2.OnGround && !i2.Sprinting && i2.Forward == 1 && i2.Strafe == -1
              && i2.Selected == 4 && i2.LeftDown && i2.RightPressed && !i2.RightDown && i2.HitKind == 1 && i2.HitX == 5 && i2.HitFace == 1 && i2.HitFz == 0.25f && i2.TeleportAck == 3,
              "Sieć: NetInput zapis/odczyt");
        Check(packets[1].r.ReadString() == "zażółć gęślą jaźń", "Sieć: tekst UTF-8");
        var st = packets[2].r.ReadStack();
        Check(st != null && st.Id == I.Diamond && st.Count == 7, "Sieć: stack");
        Check(buf.Take() == null, "Sieć: bufor pusty po Take");

        var world = MakeWorld(4242, WorldType.Default, 0);
        var c = world.GetChunk(0, 0);
        int top = c.TopSolidY(3, 3);
        world.SetBlock(3, top + 1, 3, B.Chest);
        if (world.GetTileEntity(3, top + 1, 3) is ChestTileEntity chest) chest.Slots[5] = new ItemStack(I.IronIngot, 12);
        var data = ChunkCodec.Encode(c);
        var d = ChunkCodec.Decode(0, 0, data);
        Check(d.Blocks.AsSpan().SequenceEqual(c.Blocks) && d.Meta.AsSpan().SequenceEqual(c.Meta) && d.Biomes.AsSpan().SequenceEqual(c.Biomes), $"Sieć: chunk kodowanie ({data.Length} B)");
        Check(d.TileEntities.Values.OfType<ChestTileEntity>().FirstOrDefault()?.Slots[5]?.Count == 12, "Sieć: tile entity w chunku");
        Check(NetServer.UnAng(NetServer.Ang(90)) is > 88 and < 92 && NetServer.UnAng(NetServer.Ang(-170)) is > -172 and < -168, "Sieć: kąty w bajcie");
        var map = RelayGateway.Map(RelayOptions.Default);
        Check(map.Regions.Count == 1 && map.Regions.TryGetValue(304, out var reg) && reg.Nodes.All(n => n.HostName == "gry.fimson.pl" && n.DERPPort == 443),
            "Sieć: mapa DERP tylko z gry.fimson.pl (region 304)");
    }

    /// <summary>Host i klient połączeni pętlą w pamięci, pełna wymiana przez NetServer/NetClient.</summary>
    private static void TestNetLoopback()
    {
        var hw = MakeWorld(777, WorldType.Default, 3);
        using var dimensions = new DimensionSet(hw);
        var hc = hw.GetChunk(0, 0);
        // płaska kamienna platforma na wysokości 70 (teren testu niezależny od generatora)
        int gy = 70;
        for (int x = 0; x < 16; x++) for (int z = 0; z < 16; z++) { hw.SetBlock(x, gy, z, B.Stone); for (int y = gy + 1; y < gy + 8; y++) hw.SetBlock(x, y, z, B.Air); }
        hw.SpawnX = 8; hw.SpawnY = gy + 1; hw.SpawnZ = 8;
        var server = new NetServer(hw) { ViewDistance = 2 };
        var (a, b) = LoopbackConnection.Pair("Host", "Tester");
        server.Accept(a);
        var client = NetClient.Start(b, "Tester", 2);
        var host = new TestClientHost();
        bool started = false;

        void Step(int n)
        {
            for (int k = 0; k < n; k++)
            {
                server.Tick(); dimensions.Tick(); server.AfterTick();
                Thread.Sleep(3);
                if (!started && client.PollLogin())
                {
                    var L = client.Login;
                    var cw = new World.World(0, WorldType.Default, true);
                    var p = new Player(cw) { Name = L.Name, IsLocal = true, Id = L.EntityId };
                    p.SetGameMode(L.GameMode); p.SetPositionAndPrev(L.X, L.Y, L.Z);
                    host.World = cw; host.Player = p;
                    client.Attach(host);
                    started = true;
                }
                if (started && !host.World.EntityById.ContainsKey(host.Player.Id))
                {
                    // jak Game.FinishLoading: gracz wchodzi do świata, gdy teren wokół jest gotowy
                    int pcx = MathUtil.FloorToInt(host.Player.X) >> 4, pcz = MathUtil.FloorToInt(host.Player.Z) >> 4, ready = 0;
                    for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++) if (host.World.GetChunk(pcx + dx, pcz + dz)?.Merged == true) ready++;
                    if (ready == 9) host.World.AddEntityNow(host.Player);
                }
                if (started)
                {
                    client.Tick();
                    host.World.Tick();
                    var p = host.Player;
                    client.SendInput(new NetInput { X = p.X, Y = p.Y, Z = p.Z, Yaw = p.Yaw, Pitch = p.Pitch, OnGround = p.OnGround });
                }
                client.Flush();
                Thread.Sleep(2);
            }
        }

        Step(5);
        Check(started && client.Login.Name == "Tester", "Sieć: logowanie", client.Error ?? "");
        if (!started) return;
        var puppet = server.Clients.FirstOrDefault()?.P;
        Check(puppet != null && puppet.Id == client.Login.EntityId && hw.Players.Contains(puppet), "Sieć: zdalny gracz w świecie hosta");
        Step(40);
        var cw = host.World;
        var cc = cw.GetChunk(0, 0);
        Check(cw.Chunks.Count >= 9, $"Sieć: klient dostał chunki ({cw.Chunks.Count})");
        Check(cc != null && cc.Blocks.AsSpan().SequenceEqual(hc.Blocks), "Sieć: chunk klienta = chunk hosta");
        Check(host.Player.Y > gy - 1 && host.Player.Y < gy + 3, $"Sieć: gracz klienta stoi na ziemi (y={host.Player.Y:F2}, grunt {gy})");
        Check(Math.Abs(puppet.Y - host.Player.Y) < 0.01, "Sieć: host zna pozycję klienta");

        hw.SetBlock(8, gy + 4, 8, B.Stone);
        Step(3);
        Check(cw.GetBlock(8, gy + 4, 8) == B.Stone, "Sieć: zmiana bloku dociera do klienta");

        var cow = new Cow(hw); cow.SetPositionAndPrev(10.5, gy + 1, 8.5); hw.SpawnEntity(cow);
        Step(4);
        var ccow = cw.Entities.FirstOrDefault(e => e.TypeKey == "cow");
        Check(ccow != null && ccow.Id == cow.Id && Math.Abs(ccow.X - cow.X) < 0.5, "Sieć: krowa widoczna u klienta");
        cow.Kill();
        Step(3);
        Check(!cw.Entities.Any(e => e.TypeKey == "cow"), "Sieć: usunięcie encji");

        client.SendChat("hej");
        Step(3);
        Check(host.Chat.Any(s => s.Contains("<Tester> hej")), "Sieć: czat przez hosta", string.Join(" | ", host.Chat));
        client.SendChat("/time set night");
        Step(3);
        Check(host.Chat.Any(s => s.Contains("Tylko host")), "Sieć: komenda bez uprawnień odrzucona");
        server.SetOp(server.Clients[0].Conn.PeerKey, true);
        client.SendChat("/time set 13000");
        Step(25);
        Check(cw.DayTime % 24000 >= 13000 && cw.DayTime % 24000 < 13100, $"Sieć: komenda operatora + czas (pora {cw.DayTime % 24000})");

        puppet.Inventory.Main[0] = new ItemStack(B.Stone, 10);
        puppet.Inventory.Main[1] = new ItemStack(I.Diamond, 5);
        Step(3);
        Check(host.Player.Inventory.Main[1]?.Id == I.Diamond && host.Player.Inventory.Main[1].Count == 5, "Sieć: ekwipunek od hosta");

        // stawianie bloku przez klienta (akcja wykonywana na hoście)
        int tx = 11, tz = 8, ty = gy;
        hw.SetBlock(tx, ty + 1, tz, B.Air); hw.SetBlock(tx, ty + 2, tz, B.Air);
        var p0 = host.Player;
        client.SendInput(new NetInput { X = p0.X, Y = p0.Y, Z = p0.Z, OnGround = true, Selected = 0, RightPressed = true, HitKind = 1, HitX = tx, HitY = ty, HitZ = tz, HitFace = Dir.Up, HitFx = 0.5f, HitFy = 1f, HitFz = 0.5f });
        Step(3);
        Check(hw.GetBlock(tx, ty + 1, tz) == B.Stone && cw.GetBlock(tx, ty + 1, tz) == B.Stone, "Sieć: klient stawia blok (host i klient)", $"host {hw.GetBlock(tx, ty + 1, tz)}/{hw.GetBlock(tx, ty, tz)} klient {cw.GetBlock(tx, ty + 1, tz)} gracz {puppet.X:F1},{puppet.Y:F1},{puppet.Z:F1} ty={ty}");
        Check(host.Player.Inventory.Main[0]?.Count == 9, "Sieć: zużycie bloku w ekwipunku klienta");

        // otwarcie skrzyni przez klienta: okno od hosta, kliknięcie wykonywane na hoście
        int sx = 6, sz = 8, sy = gy + 1;
        hw.SetBlock(sx, sy, sz, B.Chest);
        ((ChestTileEntity)hw.GetTileEntity(sx, sy, sz)).Slots[0] = new ItemStack(I.Apple, 3);
        Step(6);
        client.SendInput(new NetInput { X = p0.X, Y = p0.Y, Z = p0.Z, OnGround = true, Selected = 0, RightPressed = true, HitKind = 1, HitX = sx, HitY = sy, HitZ = sz, HitFace = Dir.Up, HitFx = 0.5f, HitFy = 0.8f, HitFz = 0.5f });
        Step(3);
        Check(host.CurrentScreen is ContainerScreen && host.Player.OpenContainer is ChestContainer, "Sieć: okno skrzyni otwarte przez hosta");
        Check(host.Player.OpenContainer?.Slots[0].Stack?.Id == I.Apple, "Sieć: zawartość skrzyni u klienta");
        if (host.Player.OpenContainer is ChestContainer ccont)
        {
            ccont.Click(0, 0, ClickType.QuickMove, host.Player);
            client.Flush();
            Step(3);
            Check(((ChestTileEntity)hw.GetTileEntity(sx, sy, sz)).Slots[0] == null && puppet.Inventory.CountOf(I.Apple) == 3, "Sieć: shift-klik w skrzyni wykonany na hoście");
            Check(host.Player.Inventory.CountOf(I.Apple) == 3, "Sieć: jabłka u klienta po synchronizacji");
        }

        // teleport przez hosta
        puppet.SetPositionAndPrev(20.5, gy + 30, 20.5);
        Step(2);
        Check(Math.Abs(host.Player.X - 20.5) < 0.01 && Math.Abs(host.Player.Z - 20.5) < 0.01, "Sieć: teleport od hosta");

        // obrażenia i śmierć liczy host
        puppet.Attack(DamageSource.Generic, 100);
        Step(2);
        Check(host.Player.Dead && host.Player.Health <= 0, "Sieć: śmierć z hosta");
        client.SendRespawn();
        Step(3);
        Check(!host.Player.Dead && host.Player.Health == 20 && !puppet.Dead, "Sieć: odrodzenie");

        foreach (var (block, kind, type) in new[] {
            (B.BrewingStand, GuiKind.Brewing, typeof(BrewingContainer)),
            (B.Hopper, GuiKind.Hopper, typeof(RsContainer)),
            (B.Dispenser, GuiKind.Dispenser, typeof(RsContainer)),
            (B.TrappedChest, GuiKind.TrappedChest, typeof(RsContainer)) })
        {
            hw.SetBlock(6, gy + 1, 8, block);
            Step(5);
            puppet.OpenBlockGui(kind, 6, gy + 1, 8);
            Step(4);
            Check(puppet.OpenContainer?.GetType() == type && host.Player.OpenContainer?.GetType() == type,
                "Sieć: GUI modułu " + kind);
            if (kind == GuiKind.TrappedChest)
                Check(((ChestTileEntity)hw.GetTileEntity(6, gy + 1, 8)).Viewers == 1, "Sieć: skrzynia-pułapka liczy zdalnego widza");
            host.Player.CloseContainer(); Step(4);
        }

        // Kontenery encji: ekran klienta, akcje i rozliczenie po stronie hosta.
        var horse = new Horse(hw) { Tamed = true };
        horse.SetPositionAndPrev(puppet.X + 1, puppet.Y, puppet.Z); hw.AddEntityNow(horse);
        puppet.Sneaking = true; horse.Interact(puppet); puppet.Sneaking = false;
        puppet.Inventory.Main[0] = new ItemStack(I.Saddle); Step(5);
        Check(host.CurrentScreen is HorseScreen && host.Player.OpenContainer is HorseContainer,
            "Sieć: ekran ekwipunku konia u gościa");
        if (host.Player.OpenContainer is HorseContainer remoteHorse)
        {
            remoteHorse.Click(remoteHorse.FirstPlayerSlot + 27, 0, ClickType.QuickMove, host.Player); Step(4);
            Check(horse.Inv.Get(0)?.Id == I.Saddle && remoteHorse.Horse.Inv.Get(0)?.Id == I.Saddle
                && puppet.Inventory.Main[0] == null, "Sieć: siodło przeniesione do konia przez hosta");
        }
        host.Player.CloseContainer(); Step(3);

        var cart = new ChestMinecartEntity(hw);
        cart.SetPositionAndPrev(puppet.X + 1, puppet.Y, puppet.Z); hw.AddEntityNow(cart);
        cart.Set(0, new ItemStack(I.GoldIngot, 3)); cart.Interact(puppet); Step(4);
        Check(host.CurrentScreen is RsContainerScreen && host.Player.OpenContainer is RsContainer,
            "Sieć: ekran wagonika ze skrzynią u gościa");
        if (host.Player.OpenContainer is RsContainer remoteCart)
        {
            remoteCart.Click(0, 0, ClickType.QuickMove, host.Player); Step(4);
            Check(cart.Get(0) == null && puppet.Inventory.CountOf(I.GoldIngot) == 3
                && host.Player.Inventory.CountOf(I.GoldIngot) == 3, "Sieć: ładunek wagonika przeniesiony bez powielania");
        }
        host.Player.CloseContainer(); Step(3);

        var villager = new Villager(hw, 0);
        villager.SetPositionAndPrev(puppet.X + 1, puppet.Y, puppet.Z); hw.AddEntityNow(villager);
        villager.Recipes.Clear();
        villager.Recipes.Add(new(new ItemStack(I.Wheat, 4), null, new ItemStack(I.Emerald)));
        villager.Recipes.Add(new(new ItemStack(I.Wheat, 8), null, new ItemStack(I.Emerald, 2)) { MaxUses = 1 });
        villager.Interact(puppet); Step(5);
        Check(host.CurrentScreen is MerchantScreen && host.Player.OpenContainer is MerchantContainer,
            "Sieć: ekran handlu z ofertami u gościa");
        if (host.Player.OpenContainer is MerchantContainer remoteTrade && puppet.OpenContainer is MerchantContainer trade)
        {
            client.SelectRecipe(1); Step(3);
            Check(trade.Index == 1 && remoteTrade.Index == 1 && remoteTrade.Current.Sell.Count == 2,
                "Sieć: wybór oferty potwierdzony przez hosta");
            client.SelectRecipe(999); Step(2);
            Check(trade.Index == 1, "Sieć: nieprawidłowy indeks oferty jest odrzucany");
            puppet.Cursor = new ItemStack(I.Wheat, 8); Step(3);
            remoteTrade.Click(0, 0, ClickType.Pickup, host.Player); Step(3);
            Check(remoteTrade.Inv.Get(2)?.Count == 2, "Sieć: wynik handlu dociera od hosta");
            remoteTrade.Click(2, 0, ClickType.Pickup, host.Player); Step(4);
            Check(trade.Inv.Get(0) == null && puppet.Cursor?.Id == I.Emerald && puppet.Cursor.Count == 2
                && host.Player.Cursor?.Count == 2 && trade.Current.Uses == 1 && remoteTrade.Current.Disabled,
                "Sieć: zapłata, wynik i wyczerpanie oferty rozliczone raz");
            var cursor = host.Player.Cursor; int emeralds = host.Player.Inventory.CountOf(I.Emerald);
            remoteTrade.OnClose(host.Player);
            Check(host.Player.Cursor == cursor && host.Player.Inventory.CountOf(I.Emerald) == emeralds,
                "Sieć: lokalne zamknięcie GUI nie przenosi kursora ponownie do ekwipunku");
            puppet.CloseContainer(); client.SelectRecipe(0); Step(4);
            Check(trade.Index == 1 && host.Player.OpenContainer == null, "Sieć: spóźniony wybór zamkniętego okna jest odrzucany");
        }
        puppet.SetGameMode(1);
        var nether = dimensions.Get(DimensionId.Nether);
        // Zajmij ID w nowym wymiarze, aby przetestować zmianę ID gracza w protokole.
        for (int n = 0; n < 3; n++) nether.AddEntityNow(new Cow(nether));
        dimensions.Transfer(puppet, DimensionId.Nether, 8, 110, 8);
        Step(25);
        Check(host.World.Dimension == DimensionId.Nether && host.Player.Id == puppet.Id,
            "Sieć: zmiana wymiaru i ID gracza");
        Check(host.World.Chunks.Count > 0 && server.Clients[0].Ctrl.W == nether, "Sieć: teren i sterowanie w Netherze");
        dimensions.Transfer(puppet, DimensionId.Overworld, 8, gy + 1, 8);
        Step(10);
        Check(host.World.Dimension == DimensionId.Overworld && host.Player.Id == puppet.Id,
            "Sieć: powrót do Overworldu");

        client.Disconnect("koniec testu");
        Step(4);
        Check(server.Clients.Count == 0 && !hw.Players.Contains(puppet), "Sieć: rozłączenie usuwa gracza");
    }

    /// <summary>Prawdziwy Tailcat.Link (parowanie kodem, kanały) przez przekaźnik DERP w pamięci.</summary>
    private static async Task TestNetTailcat()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var relay = new FakeDerpRelay();
        var gw = new FakeRelayGatewayFactory(relay, null);
        await using var host = await LinkHostService.StartAsync(LinkSetup.Options("host", gw, new InMemoryLinkStore()), cts.Token);
        string code = await host.InviteAsync(cts.Token);
        Check(!string.IsNullOrEmpty(code), "Tailcat: kod zaproszenia", code);
        await using var join = await LinkClientService.JoinAsync(code, "Tester", LinkSetup.Options("client", gw, new InMemoryLinkStore()), cts.Token);
        join.Connection.Send(new byte[] { 1, 2, 3, 4, 5 });
        LinkConnection hc = null;
        while (!cts.IsCancellationRequested && !host.NewConnections.TryDequeue(out hc)) await Task.Delay(10, cts.Token);
        Check(hc != null && hc.PeerName == "Tester", "Tailcat: host przyjął gracza z nickiem", hc?.PeerName ?? "brak");
        if (hc == null) return;
        byte[] f = null;
        while (!cts.IsCancellationRequested && !hc.TryReceive(out f)) await Task.Delay(10, cts.Token);
        Check(f != null && f.Length == 5 && f[4] == 5, "Tailcat: ramka klient -> host");
        var big = new byte[200_000]; new Random(1).NextBytes(big);
        hc.Send(big);
        byte[] g = null;
        while (!cts.IsCancellationRequested && !join.Connection.TryReceive(out g)) await Task.Delay(10, cts.Token);
        Check(g != null && g.AsSpan().SequenceEqual(big), "Tailcat: duża ramka host -> klient (200 KB)");

        // logowanie do gry przez Tailcat
        var hw = MakeWorld(99, WorldType.Superflat, 1);
        hw.SpawnX = 0; hw.SpawnY = 5; hw.SpawnZ = 0;
        var server = new NetServer(hw) { ViewDistance = 2 };
        server.Accept(hc);
        var client = NetClient.Start(join.Connection, "Tester", 2);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!client.PollLogin() && sw.ElapsedMilliseconds < 15000 && client.Error == null)
        {
            server.Tick(); hw.Tick(); server.AfterTick();
            await Task.Delay(20);
        }
        Check(client.Login != null && server.Clients.Count == 1, "Tailcat: logowanie do gry", client.Error ?? "");
        server.Shutdown("koniec testu");
    }
}
