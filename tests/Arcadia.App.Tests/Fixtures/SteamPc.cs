using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Steam;

namespace Arcadia.App.Tests.Fixtures;

/// <summary>
/// Steam Controller Support on a Windows PC whose Steam is <paramref name="steam"/>: the real
/// <see cref="ControllerSupportPlatform"/> reads and writes Steam's files there (accounts in <c>loginusers.vdf</c>, the
/// "SISR Marker" in <c>shortcuts.vdf</c>, the CEF flag). Only what needs Windows is pretended: Steam runs until SISR's
/// setup closes it, and comes back logged in; the usbip driver is ready.
/// </summary>
internal sealed class SteamPc : IControllerSupportPlatform
{
    private readonly ControllerSupportPlatform _files;

    public SteamPc(SteamFolder steam)
    {
        _files = new ControllerSupportPlatform(() => Directory.Exists(steam.Root) ? new SteamInstallation(steam.Root, null) : null, () => IsSteamRunning);
        Sisr = new SisrToInstall();
    }

    public SisrToInstall Sisr { get; }

    public bool IsSupported => true;

    public bool IsSteamInstalled => _files.IsSteamInstalled;

    public bool IsSteamRunning { get; private set; } = true;

    public bool IsSteamReady => IsSteamRunning;

    public bool IsSteamUp => IsSteamRunning;

    public bool IsUsbipReady => true;

    public bool IsPanReady => true;

    public Arcadia.ControllerSupport.Pan.PanCompatibility PanCompatibility => Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;

    public Version? HidHideVersion => Arcadia.Pan.HidHideRelease.Version;

    public bool IsCefDebuggingEnabled => _files.IsCefDebuggingEnabled;

    /// <summary>How often Steam was closed for SISR's setup.</summary>
    public int SteamStops { get; private set; }

    public bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null) => _files.EnsureSisrMarker(sisrExecutable, accountIds);

    public IReadOnlyList<MarkerAccount> SisrMarkerAccounts(string sisrExecutable) => _files.SisrMarkerAccounts(sisrExecutable);

    public bool HasSisrMarker(string sisrExecutable) => _files.HasSisrMarker(sisrExecutable);

    public void EnableCefDebugging() => _files.EnableCefDebugging();

    public string? RunningSteamGame() => null;

    public int? RunningSteamAppId() => null;

    public SteamInputSetting? SteamInputFor(int appId) => null;

    public SteamInputSetting? SteamInputDefaults() => null;

    public Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        IsSteamRunning = false;
        SteamStops++;
        return Task.FromResult(true);
    }

    public void StartSteam() => IsSteamRunning = true;

    /// <summary>The user closed Steam.</summary>
    public void CloseSteam() => IsSteamRunning = false;

    public Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) => [];

    public void ResetSteamInputLayout()
    {
    }

    public bool AnyRunningFrom(string directory) => false;

    public IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now) => [];
}

/// <summary>SISR before its setup: not installed; "installing" only records it, and a start is counted, not run.</summary>
internal sealed class SisrToInstall : ISisrHost
{
    private static readonly HttpClient NoNetwork = new();

    // Never asked: this SISR doesn't run.
    public SisrApiClient Api { get; } = new(NoNetwork, 1);

    public bool IsInstalled { get; private set; }

    /// <summary>Under the test's temp folder, so the marker's path names no real PC.</summary>
    public string ExecutablePath { get; } = Path.Combine(Path.GetTempPath(), "arcadia-tests-sisr", "sisr.exe");

    public Version? InstalledVersion => IsInstalled ? new Version(1, 0) : null;

    public bool IsRunning => false;

    public int Starts { get; private set; }

    public IReadOnlyList<int> ExternalInstances() => [];

    public void StopExternalInstances()
    {
    }

    public Task<Release?> GetLatestReleaseAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<Release?>(new Release("v1.0.0", new Version(1, 0), []));

    public Task InstallAsync(Release release, CancellationToken cancellationToken = default)
    {
        IsInstalled = true;
        return Task.CompletedTask;
    }

    public void Start() => Starts++;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Kill()
    {
    }

    public void MarkInitialSetupDone()
    {
    }

    public Task<int> CleanUpViiperAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}
