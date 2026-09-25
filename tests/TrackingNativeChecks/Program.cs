using System.Reflection;
using System.IO;

internal static class Program
{
    // Run interactively on the desktop. Console/sandbox desktops do not expose
    // the user's foreground window and cannot validate real caret providers.
    [STAThread]
    private static int Main()
    {
        using var log = new StreamWriter(Path.Combine(AppContext.BaseDirectory, "tracking-native.log")) { AutoFlush = true };
        Console.SetOut(log);
        try
        {
            TrackingChecks.RunNative(Assembly.Load("QuickZoom"));
            return 0;
        }
        catch (Exception ex)
        {
            log.WriteLine(ex);
            return 1;
        }
    }
}
