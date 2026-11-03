using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace BlockCraft.Core;

/// <summary>Lekki profiler sekcji (włączany w benchmarkach). Prof.Begin() zwraca znacznik czasu, Prof.End(slot, t) dolicza.</summary>
public static class Prof
{
    public static bool On;
    public const int ExpRays = 0, ExpEntities = 1, ExpExposureRef = 2, ExpDestroy = 3, TntTick = 4, Light = 5, Notify = 6, OtherTick = 7, Count = 8;
    private static readonly string[] Names = { "eksplozja: promienie", "eksplozja: encje", "  w tym ekspozycja raycast", "eksplozja: niszczenie", "tick TNT (ruch)", "światło", "powiadomienia sąsiadów", "tick encji innych" };
    public static readonly long[] Ticks = new long[Count];
    public static readonly long[] Calls = new long[Count];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Begin() => On ? Stopwatch.GetTimestamp() : 0;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void End(int slot, long t) { if (On && t != 0) { Ticks[slot] += Stopwatch.GetTimestamp() - t; Calls[slot]++; } }

    public static void Reset() { Array.Clear(Ticks); Array.Clear(Calls); }
    public static void Print()
    {
        for (int i = 0; i < Count; i++)
            if (Calls[i] > 0) Console.WriteLine($"  {Names[i],-28} {Ticks[i] * 1000.0 / Stopwatch.Frequency,9:F1} ms  ({Calls[i]} wywołań)");
    }
}
