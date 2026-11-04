using System.Diagnostics;
using System.Runtime.Versioning;

namespace Arcadia.ControllerSupport;

/// <summary>
/// "Windows neu starten" (#382, D2): Windows' own <c>shutdown.exe</c> from System32 with fixed arguments, so nothing
/// from the user or a file reaches the process start. Needs no administrator rights: an interactive user may restart
/// the PC. Without <c>/f</c> Windows asks programs to close, and one with unsaved work may still hold the restart up.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class WindowsRestart
{
    /// <summary><c>shutdown.exe</c> only hands the request to Windows and ends at once; this is a generous bound.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>Restarts Windows now; throws when Windows refused (policy, a restart already under way) or didn't answer.</summary>
    public static async Task RequestAsync(CancellationToken cancellationToken)
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        // Restart, no delay, reason "Application: Installation (Planned)" for Windows' event log.
        foreach (var argument in new[] { "/r", "/t", "0", "/d", "p:4:2" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("shutdown.exe startet nicht");
        using var timer = new CancellationTokenSource(Timeout);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested)
        {
            throw new TimeoutException($"shutdown.exe hat nicht innerhalb von {Timeout.TotalSeconds:0} Sekunden geantwortet");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"shutdown.exe endete mit {process.ExitCode}");
        }
    }
}
