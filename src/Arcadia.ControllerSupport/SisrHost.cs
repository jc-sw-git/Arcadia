using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Arcadia.Launchers;

namespace Arcadia.ControllerSupport;

/// <summary>The SISR process as <see cref="ControllerSupportService"/> uses it; replaced in tests.</summary>
public interface ISisrHost
{
    SisrApiClient Api { get; }

    bool IsInstalled { get; }

    Version? InstalledVersion { get; }

    /// <summary>True if Arcadia's SISR process runs.</summary>
    bool IsRunning { get; }

    /// <summary>SISR processes that are not Arcadia's copy, e.g. a separately installed SISR.</summary>
    IReadOnlyList<int> ExternalInstances();

    void StopExternalInstances();

    Task<Release?> GetLatestReleaseAsync(CancellationToken cancellationToken = default);

    Task InstallAsync(Release release, CancellationToken cancellationToken = default);

    void Start();

    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// SISR (GPL-3.0, https://github.com/Alia5/SISR), run by Arcadia: started hidden, controlled
/// through its HTTP API and stopped again. It lives where SISR's official installer puts it, so
/// an existing installation — and the Steam marker shortcut SISR created for it — is reused
/// instead of adding a second one (SISR picks the marker that points to its own executable).
/// </summary>
public sealed class SisrHost(HttpClient http, string installDirectory, string logDirectory) : ISisrHost
{
    /// <summary>Fixed so Arcadia can reach the API; SISR's own default is a random port.</summary>
    public const int ApiPort = 47640;

    private const string VersionFile = "arcadia-sisr-version.txt";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\SISR";

    public SisrApiClient Api { get; } = new(http, ApiPort);

    public string InstallDirectory { get; } = installDirectory;

    public string ExecutablePath => Path.Combine(InstallDirectory, "SISR.exe");

    public static SisrHost CreateDefault(HttpClient http) => new(
        http,
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SISR"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "logs"));

    public bool IsInstalled => File.Exists(ExecutablePath);

    public Version? InstalledVersion
    {
        get
        {
            var file = Path.Combine(InstallDirectory, VersionFile);
            if (File.Exists(file))
            {
                return GitHubReleaseClient.ParseVersion(File.ReadAllText(file));
            }

            // Installed by SISR's own installer: it records the version in its uninstall entry.
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstallKey);
                return GitHubReleaseClient.ParseVersion(key?.GetValue("DisplayVersion") as string);
            }

            return null;
        }
    }

    /// <summary>True if Arcadia's SISR process runs.</summary>
    public bool IsRunning => FindProcesses(own: true).Count > 0;

    /// <summary>SISR processes that are not Arcadia's copy, e.g. a separately installed SISR.</summary>
    public IReadOnlyList<int> ExternalInstances() => FindProcesses(own: false);

    public Task<Release?> GetLatestReleaseAsync(CancellationToken cancellationToken = default) =>
        new GitHubReleaseClient(http).GetLatestAsync("Alia5", "SISR", cancellationToken);

    /// <summary>Installs or updates SISR from a release. Stops a running copy first.</summary>
    public async Task InstallAsync(Release release, CancellationToken cancellationToken = default)
    {
        var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var asset = release.FindAsset(n => n.Equals($"SISR-windows_{arch}-Release.zip", StringComparison.OrdinalIgnoreCase))
            ?? release.FindAsset(n => n.Equals($"SISR-windows_{arch}.zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"SISR {release.Tag} enthält kein Paket für Windows {arch}.");

        var staging = InstallDirectory + ".new";
        var archive = Path.Combine(Path.GetTempPath(), "Arcadia", asset.Name);
        await VerifiedDownload.DownloadAsync(http, asset, archive, cancellationToken);
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            ZipFile.ExtractToDirectory(archive, staging);

            // Some archives wrap everything in a top-level folder; flatten that.
            if (!File.Exists(Path.Combine(staging, "SISR.exe"))
                && Directory.GetDirectories(staging) is [var inner]
                && File.Exists(Path.Combine(inner, "SISR.exe")))
            {
                var flattened = staging + ".flat";
                Directory.Move(inner, flattened);
                Directory.Delete(staging, recursive: true);
                Directory.Move(flattened, staging);
            }

            if (!File.Exists(Path.Combine(staging, "SISR.exe")))
            {
                throw new InvalidOperationException($"Das SISR-Paket {asset.Name} enthält keine SISR.exe.");
            }

            // Arcadia handles setup and updates itself; skip SISR's first-run wizard.
            await File.WriteAllTextAsync(Path.Combine(staging, ".initial_setup_done"), string.Empty, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(staging, VersionFile), release.Tag, cancellationToken);
        }
        finally
        {
            File.Delete(archive);
        }

        await StopAsync(cancellationToken);

        // Copy over the existing installation instead of replacing the folder, so files of
        // SISR's own installer (uninstall script, first-run marker) stay.
        foreach (var source in Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(InstallDirectory, Path.GetRelativePath(staging, source));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }

        Directory.Delete(staging, recursive: true);

        if (OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstallKey, writable: true);
            key?.SetValue("DisplayVersion", release.Tag);
        }
    }

    /// <summary>
    /// Removes the separate copy earlier Arcadia builds installed to <c>%LOCALAPPDATA%\Arcadia\sisr</c>.
    /// </summary>
    public static void RemoveLegacyCopy()
    {
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Arcadia", "sisr");
        if (!Directory.Exists(legacy))
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName("SISR").Concat(Process.GetProcessesByName("viiper")))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } path && path.StartsWith(legacy, StringComparison.OrdinalIgnoreCase))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(3000);
                    }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                }
            }
        }

        try
        {
            Directory.Delete(legacy, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Try again next start.
        }
    }

    public void Start()
    {
        if (!IsInstalled || IsRunning)
        {
            return;
        }

        Directory.CreateDirectory(logDirectory);
        var info = new ProcessStartInfo(ExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = InstallDirectory,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("--api.listen-address");
        info.ArgumentList.Add($"localhost:{ApiPort}");
        info.ArgumentList.Add("--update-notify");
        info.ArgumentList.Add("none");
        info.ArgumentList.Add("--log.file");
        info.ArgumentList.Add(Path.Combine(logDirectory, "sisr.log"));
        Process.Start(info)?.Dispose();
    }

    /// <summary>Asks SISR to quit so it can remove its virtual controllers, then kills it if needed.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return;
        }

        await Api.QuitAsync(cancellationToken);
        if (await ProcessWatch.WaitUntilAsync(() => !IsRunning, TimeSpan.FromSeconds(5), TimeProvider.System, cancellationToken))
        {
            return;
        }

        foreach (var id in FindProcesses(own: true))
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
                // Already gone.
            }
        }
    }

    /// <summary>Stops SISR instances that are not Arcadia's copy.</summary>
    public void StopExternalInstances()
    {
        foreach (var id in ExternalInstances())
        {
            try
            {
                using var process = Process.GetProcessById(id);
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
            {
            }
        }
    }

    private List<int> FindProcesses(bool own)
    {
        var ownPath = Path.GetFullPath(ExecutablePath);
        var result = new List<int>();
        foreach (var process in Process.GetProcessesByName("SISR"))
        {
            using (process)
            {
                string? path;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    path = null;
                }

                var isOwn = path is not null && string.Equals(Path.GetFullPath(path), ownPath, StringComparison.OrdinalIgnoreCase);
                if (isOwn == own)
                {
                    result.Add(process.Id);
                }
            }
        }

        return result;
    }
}
