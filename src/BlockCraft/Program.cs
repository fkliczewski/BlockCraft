using System.Runtime.InteropServices;
using BlockCraft.Core;

namespace BlockCraft;

public static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    public static void ShowError(string text)
    {
        try { if (OperatingSystem.IsWindows()) MessageBoxW(IntPtr.Zero, text, "BlockCraft", 0x10); } catch { }
    }

    public static void EnsureConsole()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { if (!AttachConsole(-1)) AllocConsole(); } catch { }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error("Nieobsłużony wyjątek: " + e.ExceptionObject);
        try
        {
            if (args.Contains("--selftest")) { EnsureConsole(); return SelfTest.Run(args); }
            if (args.Contains("--server")) { EnsureConsole(); return Net.DedicatedServer.Run(args); }
            using var game = new Game(args);
            game.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error("Krytyczny błąd: " + ex);
            ShowError("Wystąpił błąd krytyczny:\n" + ex.Message + "\n\nSzczegóły w pliku blockcraft.log");
            return 1;
        }
    }
}
