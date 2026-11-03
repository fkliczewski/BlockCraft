using System.Diagnostics;
using BlockCraft.Core;
using BlockCraft.Entities;
using BlockCraft.World;

namespace BlockCraft;

/// <summary>Benchmarki bez okna: --bench tnt N [--lit] [--limit S]. Wynik: czasy ticków i suma kontrolna świata
/// (bloki w obszarze + encje), żeby porównać optymalizację ze starą ścieżką bit w bit.</summary>
public static class Bench
{
    public static int Run(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }
        int i = Array.IndexOf(args, "--bench");
        string kind = i + 1 < args.Length ? args[i + 1] : "tnt";
        int n = i + 2 < args.Length && int.TryParse(args[i + 2], out var nn) ? nn : 1000;
        int li = Array.IndexOf(args, "--limit");
        double limit = li >= 0 && li + 1 < args.Length ? double.Parse(args[li + 1], System.Globalization.CultureInfo.InvariantCulture) : 120;
        switch (kind)
        {
            case "tnt":
                {
                    int si = Array.IndexOf(args, "--slice");
                    double slice = si >= 0 && si + 1 < args.Length ? double.Parse(args[si + 1], System.Globalization.CultureInfo.InvariantCulture) : 0;
                    return Tnt(n, args.Contains("--lit"), limit, slice);
                }
            default: Console.WriteLine("Nieznany benchmark: " + kind); return 1;
        }
    }

    /// <summary>Kostka N bloków TNT na superpłaskim świecie zapalona w środku (reakcja łańcuchowa)
    /// albo (--lit) N zapalonych TNT naraz w tej samej kostce. --slice MS: tick cięty na kawałki z budżetem MS
    /// (jak w grze między klatkami); suma kontrolna musi wyjść ta sama, max kawałek = najdłuższy przestój klatki.</summary>
    private static int Tnt(int n, bool lit, double limitSec, double sliceMs)
    {
        int side = Math.Max(1, (int)Math.Round(Math.Cbrt(n)));
        n = side * side * side;
        var w = SelfTest.MakeWorld(1234, WorldType.Superflat, 8);
        int x0 = -side / 2, z0 = -side / 2, y0 = 4;
        var setup = Stopwatch.StartNew();
        for (int y = 0; y < side; y++)
            for (int z = 0; z < side; z++)
                for (int x = 0; x < side; x++)
                {
                    if (lit) w.SpawnEntity(new TntEntity(w, x0 + x + 0.5, y0 + y, z0 + z + 0.5, 80));
                    else w.SetBlock(x0 + x, y0 + y, z0 + z, B.Tnt, 0, false);
                }
        if (!lit)
        {
            int cx = x0 + side / 2, cy = y0 + side / 2, cz = z0 + side / 2;
            w.SetBlock(cx, cy, cz, B.Air, 0, false);
            w.SpawnEntity(new TntEntity(w, cx + 0.5, cy, cz + 0.5, 1));
        }
        w.Tick(); // FlushAdds
        Console.WriteLine($"bench tnt: N={n} ({side}^3) lit={lit} setup={setup.ElapsedMilliseconds} ms");
        Prof.On = true; Prof.Reset();
        var total = Stopwatch.StartNew();
        var tick = new Stopwatch();
        double maxMs = 0, sumMs = 0, maxSliceMs = 0; int ticks = 0, maxTnt = 0, slices = 0;
        var sl = new Stopwatch();
        while (true)
        {
            int tnt = 0;
            foreach (var e in w.Entities) if (e is TntEntity && !e.Removed) tnt++;
            maxTnt = Math.Max(maxTnt, tnt);
            if (tnt == 0 && ticks > 0) break;
            tick.Restart();
            if (sliceMs <= 0) w.Tick();
            else
            {
                w.TickBegin();
                while (true)
                {
                    sl.Restart(); slices++;
                    bool done = w.TickSlice(Stopwatch.GetTimestamp() + (long)(sliceMs / 1000 * Stopwatch.Frequency));
                    double sms = sl.Elapsed.TotalMilliseconds; maxSliceMs = Math.Max(maxSliceMs, sms);
                    if (sms > 15) Console.WriteLine($"  dlugi kawalek {sms:F1} ms w ticku {ticks}, gc0={GC.CollectionCount(0)} gc2={GC.CollectionCount(2)} done={done}");
                    if (done) break;
                }
            }
            double ms = tick.Elapsed.TotalMilliseconds;
            sumMs += ms; maxMs = Math.Max(maxMs, ms); ticks++;
            if (ticks % 20 == 0) Console.WriteLine($"  tick {ticks}: {ms:F1} ms, TNT {tnt}, encji {w.Entities.Count}");
            if (total.Elapsed.TotalSeconds > limitSec) { Console.WriteLine($"  PRZERWANO po {limitSec} s"); break; }
            if (ticks > 20000) break;
        }
        Console.WriteLine($"wynik: ticki={ticks} sredni={sumMs / Math.Max(1, ticks):F2} ms max={maxMs:F1} ms suma={total.Elapsed.TotalSeconds:F2} s maxTNT={maxTnt}");
        if (sliceMs > 0) Console.WriteLine($"kawalki: {slices} (budzet {sliceMs} ms), najdluzszy {maxSliceMs:F1} ms");
        Prof.Print();
        Console.WriteLine($"suma kontrolna: {Checksum(w, x0 - 40, z0 - 40, side + 80):X16} encji={w.Entities.Count}");
        return 0;
    }

    /// <summary>FNV-1a po blokach i meta w kwadracie oraz po pozycjach encji (zaokrąglonych do 1e-6).</summary>
    public static ulong Checksum(World.World w, int x0, int z0, int size)
    {
        ulong h = 14695981039346656037UL;
        void Mix(long v) { h ^= (ulong)v; h *= 1099511628211UL; }
        for (int x = x0; x < x0 + size; x++)
            for (int z = z0; z < z0 + size; z++)
                for (int y = 0; y < 64; y++) { Mix(w.GetBlock(x, y, z)); Mix(w.GetMeta(x, y, z)); }
        foreach (var e in w.Entities)
        {
            if (e.Removed) continue;
            foreach (char ch in e.TypeKey) Mix(ch);
            Mix((long)Math.Round(e.X * 1e6)); Mix((long)Math.Round(e.Y * 1e6)); Mix((long)Math.Round(e.Z * 1e6));
        }
        return h;
    }
}
