using QuickZoom;
using System.Diagnostics;

try
{
    Run(args);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void Run(string[] args)
{
    if (args.Contains("--emit-output"))
    {
        Console.Out.Write(new string('O', 256 * 1024));
        Console.Error.Write(new string('E', 256 * 1024));
        return;
    }
    if (args.Contains("--hang"))
    {
        Thread.Sleep(Timeout.Infinite);
        return;
    }

    // Isolated names ensure tests never activate a user's running QuickZoom window.
    string signalName = @"Local\QuickZoom.UiChecks." + Guid.NewGuid().ToString("N");
    if (SettingsActivation.Request(signalName)) throw new Exception("A missing instance must not accept activation.");
    using var activated = new AutoResetEvent(false);
    using (var listener = new SettingsActivation(() => activated.Set(), signalName))
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            if (!SettingsActivation.Request(signalName) || !activated.WaitOne(TimeSpan.FromSeconds(3)))
                throw new Exception("Settings activation signal was not delivered.");
        }
    }
    if (SettingsActivation.Request(signalName)) throw new Exception("A disposed instance retained its activation signal.");
    Console.WriteLine("PASS: missing instance, repeat activation, and disposal; no live application was signalled.");

    int callbacks = 0;
    using (var listener = new SettingsActivation(() =>
    {
        Interlocked.Increment(ref callbacks);
        activated.Set();
        throw new InvalidOperationException("Deliberate callback failure.");
    }, signalName))
    {
        for (int i = 0; i < 2; i++)
            if (!SettingsActivation.Request(signalName) || !activated.WaitOne(TimeSpan.FromSeconds(3)))
                throw new Exception("Activation did not recover after a callback error.");
        listener.Dispose();
        listener.Dispose();
    }
    if (callbacks != 2 || SettingsActivation.Request(signalName)) throw new Exception("Activation cleanup failed.");
    Console.WriteLine("PASS: callback failures are contained and repeated disposal is safe.");

    using (Process child = StartChild("--emit-output"))
    {
        if (!ProcessOutput.TryRead(child, 5000, out string output, out string error) ||
            child.ExitCode != 0 || output != new string('O', 256 * 1024) || error != new string('E', 256 * 1024))
            throw new Exception("Large redirected output was lost or deadlocked.");
    }
    using (Process child = StartChild("--hang"))
    {
        var watch = Stopwatch.StartNew();
        if (ProcessOutput.TryRead(child, 300, out _, out _) || !child.WaitForExit(2000) || watch.ElapsedMilliseconds > 3000)
            throw new Exception("The subprocess timeout did not terminate the test child promptly.");
    }
    Console.WriteLine("PASS: concurrent pipe draining and bounded subprocess timeout.");

    foreach (float start in new[] { 0.15f, 0.9f, 0.985f, 1f })
    {
        SetupProgressFrame previous = SetupProgressCompletion.GetFrame(start, 0);
        for (int ms = 0; ms <= SetupProgressCompletion.DurationMilliseconds; ms++)
        {
            SetupProgressFrame frame = SetupProgressCompletion.GetFrame(start, ms);
            if (frame.Progress < previous.Progress || frame.Progress > 1f ||
                frame.SuccessBlend < previous.SuccessBlend || frame.SuccessBlend > 1f ||
                frame.SuccessBlend > 0f && frame.Progress != 1f ||
                frame.IsFinished && (frame.Progress != 1f || frame.SuccessBlend != 1f))
                throw new Exception("Startup completion must fill monotonically, then turn green, then finish.");
            if (ms <= SetupProgressCompletion.PauseMilliseconds && frame.Progress != start)
                throw new Exception("Startup completion must pause briefly before moving.");
            if (ms < SetupProgressCompletion.DurationMilliseconds && frame.IsFinished)
                throw new Exception("Startup completion was announced before the green hold ended.");
            previous = frame;
        }
        SetupProgressFrame middle = SetupProgressCompletion.GetFrame(start,
            SetupProgressCompletion.PauseMilliseconds + SetupProgressCompletion.FillMilliseconds / 2);
        if (start < 1f && (middle.Progress <= start || middle.Progress >= 1f))
            throw new Exception("Startup completion jumped to full instead of animating.");
    }
    Console.WriteLine("PASS: smooth startup fill, full-before-green ordering, and delayed completion.");
}

static Process StartChild(string argument)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    start.ArgumentList.Add(argument);
    return Process.Start(start) ?? throw new Exception("Could not start isolated test process.");
}
