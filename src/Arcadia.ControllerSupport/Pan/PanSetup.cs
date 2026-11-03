using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.ServiceProcess;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// Installs, updates, removes and restores with Pan.exe (#243). Arcadia ships Pan in <c>Pan\Pan.exe</c> beside its
/// own exe (the user's folder, writable by anyone who can write there); every command runs the bundled exe elevated,
/// with one UAC prompt, and <c>--install</c> gets the SHA-256 Arcadia computed so the copy in Program Files is checked
/// against it after the copy. A driver install's prompt comes first, its installers after it (#413, <see cref="StartInstallAsync"/>).
/// </summary>
[SupportedOSPlatform("windows")]
public static class PanSetup
{
    private const int ErrorCancelled = 1223;

    public static string BundledExecutable => Path.Combine(AppContext.BaseDirectory, "Pan", "Pan.exe");

    public static bool IsBundled => File.Exists(BundledExecutable);

    /// <summary>The service exists and its exe is in place.</summary>
    public static bool IsInstalled => PanPipeTransport.ServiceExists() && File.Exists(PanPaths.InstalledExecutable);

    /// <summary>
    /// Whether the installed Pan is exactly the bundled one (#297); a service without its exe counts as installed, so it
    /// is replaced. Reads both exes, so off the UI thread or once at start.
    /// </summary>
    public static PanCompatibility Compatibility =>
        PanCompatibilityCheck.Judge(
            PanPipeTransport.ServiceExists() || File.Exists(PanPaths.InstalledExecutable),
            IsBundled,
            () => PanCompatibilityCheck.HashOf(BundledExecutable),
            () => PanCompatibilityCheck.HashOf(PanPaths.InstalledExecutable));

    /// <summary>
    /// The Windows prompt of a driver install (#413, D7): <c>Pan.exe --install --run …</c> elevated, which waits for the
    /// installers until <see cref="IPanInstallRun.HandOverAsync"/>; nothing is downloaded or closed before. Connected
    /// right away, so a Pan that ends early (exit 4, 8, 9) ends the run before Arcadia touches VIIPER or Steam.
    /// </summary>
    /// <returns>Null if the user declined the prompt.</returns>
    public static async Task<IPanInstallRun?> StartInstallAsync(PanInstallPlan plan, CancellationToken cancellationToken)
    {
        if (!IsBundled)
        {
            throw new InvalidOperationException(MissingBundle);
        }

        var id = InstallRun.NewId();
        var run = new InstallRun(id, Environment.ProcessId, Path.Combine(Path.GetTempPath(), "Arcadia", id), plan.Usbip, plan.HidHide, plan.HidHideRepair);
        Directory.CreateDirectory(run.InstallersFolder);
        var process = StartElevated(["--install", "--expect-hash", HashOf(BundledExecutable), .. run.ToArguments()]);
        if (process is null)
        {
            Directory.Delete(run.InstallersFolder, recursive: true);
            return null;
        }

        try
        {
            return await PanInstallRun.ConnectAsync(process, run, cancellationToken);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Pan's update (#297, #326): <c>Pan.exe --install</c> elevated with the bundled exe only, no installers, so nothing
    /// to hand over.
    /// </summary>
    /// <returns>False if the user declined the UAC prompt.</returns>
    public static Task<bool> UpdateAsync(CancellationToken cancellationToken) =>
        RunElevatedAsync(["--install", "--expect-hash", HashOf(BundledExecutable)], PanTimeouts.Install, cancellationToken);

    /// <summary>
    /// <c>Pan.exe --uninstall</c> elevated: stops the service (its stop tears every emulation down), removes it and Pan's
    /// files; with <paramref name="removeDrivers"/> HidHide and usbip-win2 too, after Pan undid its own entries.
    /// </summary>
    public static Task<bool> UninstallAsync(bool removeDrivers, CancellationToken cancellationToken) =>
        RunElevatedAsync(removeDrivers ? ["--uninstall", "--remove-drivers"] : ["--uninstall"], PanTimeouts.Uninstall, cancellationToken);

    /// <summary><c>Pan.exe --restore</c> elevated: the manual way out when the service hangs; undoes what its journal lists.</summary>
    public static Task<bool> RestoreAsync(CancellationToken cancellationToken) => RunElevatedAsync(["--restore"], PanTimeouts.Restore, cancellationToken);

    private static async Task<bool> RunElevatedAsync(IReadOnlyList<string> arguments, TimeSpan wait, CancellationToken cancellationToken)
    {
        using var process = StartElevated(arguments);
        if (process is null)
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(wait);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new System.TimeoutException($"Pan {arguments[0]} hat nach {wait.TotalMinutes:0} Minuten nicht geantwortet.");
        }

        if (process.ExitCode != 0)
        {
            throw PanExitCodes.ErrorFor(process.ExitCode, arguments[0]);
        }

        return true;
    }

    /// <summary>The bundled Pan started elevated with one UAC prompt; null if the user declined it.</summary>
    private static Process? StartElevated(IReadOnlyList<string> arguments)
    {
        var exe = BundledExecutable;
        if (!File.Exists(exe))
        {
            throw new InvalidOperationException(MissingBundle);
        }

        var info = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas", CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            return Process.Start(info) ?? throw new InvalidOperationException("Pan konnte nicht gestartet werden.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return null;
        }
    }

    /// <summary>A Debug build gets its Pan.exe from the App project's build (see Arcadia.App.csproj); the installer always has it.</summary>
    private static string MissingBundle =>
        $"Pan fehlt in Arcadias Programmordner ({BundledExecutable}) – Arcadia neu installieren behebt das.";

    private static string HashOf(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
