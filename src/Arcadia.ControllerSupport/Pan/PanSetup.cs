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
/// against it after the copy. The usbip-win2 installer rides along in the same prompt.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PanSetup
{
    private const int ErrorCancelled = 1223;
    private static readonly TimeSpan CommandWait = TimeSpan.FromMinutes(6);

    public static string BundledExecutable => Path.Combine(AppContext.BaseDirectory, "Pan", "Pan.exe");

    public static bool IsBundled => File.Exists(BundledExecutable);

    /// <summary>Pan's exit code when <c>--install</c> couldn't secure the data folder (<c>Program.DataFolderExitCode</c> in Pan).</summary>
    private const int DataFolderExitCode = 4;

    /// <summary>Pan's exit code when <c>--restore</c> or <c>--uninstall</c> could not undo everything (<c>Program.RestoreIncompleteExitCode</c> in Pan).</summary>
    private const int RestoreIncompleteExitCode = 5;

    public static Version? BundledVersion => FileVersion(BundledExecutable);

    public static Version? InstalledVersion => FileVersion(PanPaths.InstalledExecutable);

    /// <summary>The service exists and its exe is in place.</summary>
    public static bool IsInstalled => PanPipeTransport.ServiceExists() && File.Exists(PanPaths.InstalledExecutable);

    /// <summary>Arcadia brings a newer Pan than the one installed (an Arcadia update): the dialog at start offers it.</summary>
    public static bool UpdateAvailable =>
        IsInstalled && BundledVersion is { } bundled && (InstalledVersion is not { } installed || bundled > installed);

    /// <summary>
    /// <c>Pan.exe --install</c> elevated: the usbip-win2 installer first when one is given, then Pan itself.
    /// </summary>
    /// <returns>False if the user declined the UAC prompt.</returns>
    /// <exception cref="InvalidOperationException">Pan reported an error; its log says which (<see cref="PanPaths.LogPath"/>).</exception>
    public static async Task<bool> InstallAsync(string? usbipInstaller, CancellationToken cancellationToken)
    {
        if (!IsBundled)
        {
            throw new InvalidOperationException(MissingBundle);
        }

        var arguments = new List<string> { "--install", "--expect-hash", HashOf(BundledExecutable) };
        if (usbipInstaller is not null)
        {
            arguments.Add("--usbip-installer");
            arguments.Add(usbipInstaller);
        }

        return await RunElevatedAsync(arguments, cancellationToken);
    }

    /// <summary><c>Pan.exe --uninstall</c> elevated: stops the service (its stop tears every emulation down), removes it and Pan's files.</summary>
    public static Task<bool> UninstallAsync(CancellationToken cancellationToken) => RunElevatedAsync(["--uninstall"], cancellationToken);

    /// <summary><c>Pan.exe --restore</c> elevated: the manual way out when the service hangs; undoes what its journal lists.</summary>
    public static Task<bool> RestoreAsync(CancellationToken cancellationToken) => RunElevatedAsync(["--restore"], cancellationToken);

    private static async Task<bool> RunElevatedAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
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
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Pan konnte nicht gestartet werden.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(CommandWait);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new System.TimeoutException($"Pan {arguments[0]} hat nach {CommandWait.TotalMinutes:0} Minuten nicht geantwortet.");
            }

            if (process.ExitCode == DataFolderExitCode)
            {
                // Pan could not log this: nothing of it may be written into a folder it doesn't own.
                throw new InvalidOperationException($"Pan konnte seinen Datenordner {PanPaths.DataDirectory} nicht sichern – bitte den Ordner prüfen oder entfernen und es noch einmal versuchen.");
            }

            if (process.ExitCode == RestoreIncompleteExitCode)
            {
                throw new InvalidOperationException($"Pan konnte nicht alles aufräumen – das Journal bleibt erhalten; Details stehen in {PanPaths.LogPath}. Ein Neustart von Pan oder \"Pan.exe --restore\" versucht es noch einmal.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Pan {arguments[0]} meldete Fehler {process.ExitCode} – Details stehen in {PanPaths.LogPath}.");
            }

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
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

    private static Version? FileVersion(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(file);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
