using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Tests.Fixtures;

/// <summary>
/// The controller emulation on a Windows PC whose Steam is <paramref name="steam"/>. Only what needs Windows is pretended:
/// Steam runs until a driver fix closes it and comes back logged in; every part of the emulation is ready.
/// </summary>
internal sealed class SteamPc(SteamFolder steam) : IControllerSupportPlatform, ILauncherProgram
{
    public bool IsSupported => true;

    public string? Folder => Directory.Exists(steam.Root) ? steam.Root : null;

    public bool IsRunning => IsSteamRunning;

    public bool IsReady => IsSteamRunning;

    public bool IsSteamRunning { get; private set; } = true;

    public bool IsUsbipReady => true;

    public bool IsPanReady => true;

    public Arcadia.ControllerSupport.Pan.PanCompatibility PanCompatibility => Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;

    public Version? HidHideVersion => Arcadia.Pan.HidHideRelease.Version;

    /// <summary>How often Steam was closed for a driver fix.</summary>
    public int SteamStops { get; private set; }

    public RunningLauncherGame? RunningGame() => null;

    public Task<bool> CloseAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        IsSteamRunning = false;
        SteamStops++;
        return Task.FromResult(true);
    }

    public void Start() => IsSteamRunning = true;

    /// <summary>The user closed Steam.</summary>
    public void CloseSteam() => IsSteamRunning = false;

    public Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) => [];
}
