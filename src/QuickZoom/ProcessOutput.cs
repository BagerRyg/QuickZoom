using System.Diagnostics;

namespace QuickZoom;

internal static class ProcessOutput
{
    internal static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            // The caller owns the child. In particular, an elevated installer
            // must not be terminated in the middle of replacing a startup task.
            return false;
        }
    }

    internal static bool TryRead(Process process, int timeoutMilliseconds, out string output, out string error)
    {
        output = error = string.Empty;
        using var deadline = new CancellationTokenSource(timeoutMilliseconds);
        // Drain both pipes while the child runs. Waiting for exit first deadlocks
        // once either redirected pipe fills (for example, a large task XML).
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        Task<string> stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        try
        {
            Task.WhenAll(stdout, stderr, process.WaitForExitAsync(deadline.Token)).GetAwaiter().GetResult();
            output = stdout.GetAwaiter().GetResult();
            error = stderr.GetAwaiter().GetResult();
            return true;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* The child already exited. */ }
            catch (System.ComponentModel.Win32Exception) { /* The child may be exiting. */ }
            return false;
        }
    }
}
