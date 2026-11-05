using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.Items;
using BlockCraft.Net;
using BlockCraft.Cmd;
using BlockCraft.Render;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Uszkodzone dane zapisu i sieci: błąd gubi tylko jeden element, a gra działa dalej.</summary>
public static partial class SelfTest
{
    private static void TestDataRobustness()
    {
        Console.WriteLine("[Uszkodzone dane]");
        TestNbtBadValues();
        TestCorruptEntityRecords();
        TestCorruptTileEntityRecord();
        TestCorruptScheduledTicks();
        TestUnknownItemId();
        TestMobDataFormats();
        TestFenceLeashRestore();
        TestCorruptChunkPacket();
        TestOddTranslationIndex();
        TestParticleCountLimit();
        TestReceiveQueueLimit();
        TestHostileClientPackets();
    }

    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        using var ms = new MemoryStream();
        using (var bw = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) write(bw);
        return ms.ToArray();
    }

    private static void TestNbtBadValues()
    {
        var item = new Nbt { ["id"] = 999999999999L, ["Health"] = 1.0e40, ["Name"] = "x" };
        bool ok = true;
        for (int tick = 0; tick < 3; tick++)
            ok &= item.GetInt("id", -1) == -1 && item.GetFloat("Health", 5f) == 5f && item.GetInt("Name", 7) == 7;
        Check(ok, "Dane: NBT poza zakresem daje wartość domyślną");
    }

    private static void TestCorruptEntityRecords()
    {
        var w = MakeWorld(7, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        var cow = new Cow(w); cow.SetPositionAndPrev(2.5, 4, 2.5);
        byte[] cowData = Bytes(cow.Save);
        int before = w.Entities.Count + w.PendingEntities.Count;
        int Spawned() => w.Entities.Count + w.PendingEntities.Count - before;

        EntitySerializer.LoadEntities(w, Bytes(b => b.Write(int.MaxValue)));
        EntitySerializer.LoadEntities(w, Bytes(b => { b.Write(1); b.Write("cow"); b.Write(1_000_000_000); }));
        Check(Spawned() == 0, "Dane: fałszywa liczba lub długość rekordu encji");

        EntitySerializer.LoadEntities(w, Bytes(b =>
        {
            b.Write(2);
            b.Write("cow"); b.Write(3); b.Write(new byte[] { 1, 2, 3 });
            b.Write("cow"); b.Write(cowData.Length); b.Write(cowData);
        }));
        Check(Spawned() == 1, "Dane: uszkodzona encja nie blokuje następnej");
    }

    private static void TestCorruptTileEntityRecord()
    {
        var chest = new ChestTileEntity(); chest.Slots[3] = new ItemStack(I.Diamond, 4);
        byte[] chestData = Bytes(chest.Save);
        var data = Bytes(b =>
        {
            b.Write(3); b.Write(new byte[] { 9, 9, 9 });
            b.Write(chestData.Length); b.Write(chestData);
        });
        using var br = new BinaryReader(new MemoryStream(data));
        var broken = BinaryLength.ReadRecord(br, r => TileEntity.LoadRecord(chest.TypeId, r));
        var next = BinaryLength.ReadRecord(br, r => TileEntity.LoadRecord(chest.TypeId, r)) as ChestTileEntity;
        Check(broken == null && next?.Slots[3]?.Count == 4, "Dane: uszkodzony tile entity gubi tylko siebie");
    }

    private static void TestCorruptScheduledTicks()
    {
        string dir = Path.Combine(Path.GetTempPath(), "bc-ticks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new WorldStorage(dir);
            File.WriteAllBytes(Path.Combine(dir, "ticks.dat"), Bytes(b => { b.Write(int.MaxValue); b.Write(1); }));
            bool corruptEmpty = storage.LoadScheduledTicks().Count == 0;
            storage.SaveScheduledTicks(new[] { new ScheduledTickRecord(1, 2, 3, 4, 5, 6) });
            Check(corruptEmpty && storage.LoadScheduledTicks().Single() == new ScheduledTickRecord(1, 2, 3, 4, 5, 6), "Dane: uszkodzony ticks.dat");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    private static void TestUnknownItemId()
    {
        var data = Bytes(b => { b.Write((short)0x3FFF); b.Write((byte)1); b.Write((short)0); b.Write((short)-1); });
        using var br = new BinaryReader(new MemoryStream(data));
        Check(br.ReadStack() == null && br.ReadStack() == null && br.BaseStream.Position == data.Length, "Dane: przedmiot z id spoza rejestru");
    }

    /// <summary>Stary zapis MO18 (bez bajtu smyczy) wczytuje się razem z polami zapisanymi po danych moba.</summary>
    private static void TestMobDataFormats()
    {
        var w = MakeWorld(8, WorldType.Superflat, 1);
        var cow = new Cow(w) { CustomName = "Mu", GrowingAge = -50 };
        byte[] mo19 = Bytes(cow.Save);
        int magic = IndexOf(mo19, BitConverter.GetBytes(0x4D4F3139));
        int leashByte = magic + 4 + 3 + 2; // magic, "Mu", PersistenceRequired, Saddled
        var mo18 = mo19.Take(leashByte).Concat(mo19.Skip(leashByte + 1)).ToArray();
        BitConverter.GetBytes(0x4D4F3138).CopyTo(mo18, magic);
        var old = new Cow(w); old.Load(new BinaryReader(new MemoryStream(mo18)));
        var cur = new Cow(w); cur.Load(new BinaryReader(new MemoryStream(mo19)));
        Check(magic >= 0 && old.CustomName == "Mu" && old.GrowingAge == -50 && cur.CustomName == "Mu" && cur.GrowingAge == -50, "Dane: mob w formacie MO18 i MO19");
    }

    private static void TestFenceLeashRestore()
    {
        var w = MakeWorld(9, WorldType.Superflat, 1); w.Rules.DoMobSpawning = false;
        w.SetBlock(5, 4, 5, B.Fence, 0);
        var cow = new Cow(w); cow.SetPositionAndPrev(6.5, 4, 5.5); w.AddEntityNow(cow);
        cow.LeashHolder = LeashKnot.Spawn(w, 5, 4, 5);
        byte[] saved = EntitySerializer.SaveEntities(new List<Entity> { cow });

        var w2 = MakeWorld(9, WorldType.Superflat, 1); w2.Rules.DoMobSpawning = false;
        w2.SetBlock(5, 4, 5, B.Fence, 0);
        EntitySerializer.LoadEntities(w2, saved);
        Ticks(w2, 2);
        var loaded = w2.Entities.OfType<Cow>().SingleOrDefault();
        Check(loaded?.LeashHolder is LeashKnot k && k.BX == 5 && k.BY == 4 && k.BZ == 5, "Dane: krowa przywiązana do płotu po wczytaniu");
    }

    private static byte[] Deflated(Action<BinaryWriter> write)
    {
        using var ms = new MemoryStream();
        using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Fastest, true))
        using (var bw = new BinaryWriter(ds, System.Text.Encoding.UTF8, true)) write(bw);
        return ms.ToArray();
    }

    private static bool DecodeFails(byte[] packet)
    {
        try { ChunkCodec.Decode(0, 0, packet); return false; }
        catch (IOException) { return true; }
    }

    /// <summary>Pakiet chunka z fałszywą liczbą tile entity albo długością rekordu kończy się błędem danych, bez wielkiej alokacji.</summary>
    private static void TestCorruptChunkPacket()
    {
        void Header(BinaryWriter b) { b.Write((byte)0); b.Write(new byte[256]); }
        bool fakeCount = DecodeFails(Deflated(b => { Header(b); b.Write(int.MaxValue); }));
        bool fakeLength = DecodeFails(Deflated(b => { Header(b); b.Write(1); b.Write(0); b.Write(0); b.Write(0); b.Write(0); b.Write(1_000_000_000); }));
        Check(fakeCount && fakeLength, "Dane: pakiet chunka z fałszywą liczbą lub długością tile entity");
    }

    private static void TestOddTranslationIndex()
    {
        Check(TextComponent.Translate("%99999999999$s|%1$s", new[] { "a" }) == "|a", "Dane: indeks argumentu tłumaczenia poza zakresem int");
    }

    private static void TestParticleCountLimit()
    {
        Check(WorldRenderer.LimitParticleCount(int.MaxValue) <= 4000 && WorldRenderer.LimitParticleCount(-5) == 0, "Dane: liczba cząsteczek z pakietu ograniczona");
    }

    private static bool WaitFor(Func<bool> done, Action step = null)
    {
        for (int i = 0; i < 500 && !done(); i++) { step?.Invoke(); Thread.Sleep(5); }
        return done();
    }

    /// <summary>Druga strona zasypująca ramkami, których nikt nie czyta, zostaje rozłączona.</summary>
    private static void TestReceiveQueueLimit()
    {
        var (a, b) = LoopbackConnection.Pair("A", "B");
        for (int i = 0; i <= QueuedConnection.MaxInFrames; i++) b.Send(new byte[] { 1 });
        Check(WaitFor(() => a.IsClosed), "Dane: przepełniona kolejka odbioru rozłącza");
        b.Close("koniec testu");
    }

    /// <summary>Drugi Login i ściana HitFace spoza 0..5 od klienta nie tworzą drugiego gracza ani nie wyrzucają wyjątku.</summary>
    private static void TestHostileClientPackets()
    {
        var hw = MakeWorld(10, WorldType.Superflat, 1); hw.Rules.DoMobSpawning = false;
        using var dimensions = new DimensionSet(hw);
        var server = new NetServer(hw) { ViewDistance = 2 };
        var (a, b) = LoopbackConnection.Pair("Host", "Zły");
        server.Accept(a);
        var buf = new PacketBuffer();
        for (int i = 0; i < 2; i++) { var w = buf.Begin(C2S.Login); w.Write(Protocol.Version); w.Write("Zły"); w.Write((byte)2); buf.End(); }
        b.Send(buf.Take());
        void Step() { server.Tick(); dimensions.Tick(); server.AfterTick(); while (b.TryReceive(out _)) { } }
        bool loggedIn = WaitFor(() => server.Clients.Count == 1, Step);
        Check(loggedIn && hw.Players.Count == 1, "Dane: drugi Login nie tworzy drugiego gracza");

        var p = hw.Players.FirstOrDefault();
        var input = new NetInput { X = p?.X ?? 0, Y = p?.Y ?? 0, Z = p?.Z ?? 0, OnGround = true, RightPressed = true, HitKind = 1, HitX = MathUtil.FloorToInt(p?.X ?? 0), HitY = MathUtil.FloorToInt(p?.Y ?? 0) - 1, HitZ = MathUtil.FloorToInt(p?.Z ?? 0), HitFace = 200 };
        var iw = buf.Begin(C2S.Input); input.Write(iw); buf.End();
        b.Send(buf.Take());
        for (int i = 0; i < 20; i++) { Step(); Thread.Sleep(2); }
        Check(!a.IsClosed && server.Clients.Count == 1, "Dane: ściana HitFace spoza zakresu nie wyrzuca klienta");
        b.Close("koniec testu");
    }

    private static int IndexOf(byte[] data, byte[] pattern)
    {
        for (int i = 0; i + pattern.Length <= data.Length; i++)
            if (data.AsSpan(i, pattern.Length).SequenceEqual(pattern)) return i;
        return -1;
    }
}
