using System.Net;
using System.Text.Json;
using Arcadia.ControllerSupport;
using Arcadia.Tests.Pan;
using Arcadia.Tests.Sisr;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;

namespace Arcadia.App.Tests;

/// <summary>
/// Windows with Steam as a test sets it: Steam runs or not, starts when asked or not, logs in or hangs at "Nutzerdaten
/// werden geladen"; what an older Arcadia left of SISR is <see cref="Sisr"/> (#214). Read from the thread pool by the
/// controller support, so every value is volatile.
/// </summary>
internal sealed class FakeSteam : IControllerSupportPlatform, ILauncherProgram
{
    // Steam the program, as the controller support asks it.
    string? ILauncherProgram.Folder => null;

    bool ILauncherProgram.IsRunning => IsSteamRunning;

    bool ILauncherProgram.IsReady => IsSteamReady;

    RunningLauncherGame? ILauncherProgram.RunningGame() => RunningSteamGame() is { } name ? new("0", name) : null;

    Task<bool> ILauncherProgram.CloseAsync(TimeProvider time, CancellationToken cancellationToken) => StopSteamAsync(time, cancellationToken);

    void ILauncherProgram.Start() => StartSteam();

    private volatile bool _isWindows = true;
    private volatile bool _isSteamInstalled = true;
    private volatile bool _isSteamRunning;
    private volatile bool _isLoggedIn = true;
    private volatile bool _startsWhenAsked = true;
    private volatile bool _isUsbipReady = true;
    private int _startRequests;

    /// <summary>SISR's files, its marker in Steam and its processes; nothing of it unless the test puts it there.</summary>
    public FakeSisrPc Sisr { get; } = new();

    /// <summary>Arcadia's own VIIPER next to Arcadia.exe (#242 part B); there unless the test takes it away.</summary>
    public FakeViiperFile Viiper { get; } = new();

    /// <summary>False: Linux, where the controller emulation doesn't exist.</summary>
    public bool IsWindows { get => _isWindows; set => _isWindows = value; }

    public bool IsSteamInstalled { get => _isSteamInstalled; set => _isSteamInstalled = value; }

    public bool IsSteamRunning { get => _isSteamRunning; set => _isSteamRunning = value; }

    /// <summary>False: Steam runs, but hangs at "Nutzerdaten werden geladen".</summary>
    public bool IsLoggedIn { get => _isLoggedIn; set => _isLoggedIn = value; }

    /// <summary>False: asking Steam to start does nothing, e.g. its folder was renamed.</summary>
    public bool StartsWhenAsked { get => _startsWhenAsked; set => _startsWhenAsked = value; }

    public int StartRequests => Volatile.Read(ref _startRequests);

    public bool IsUsbipReady { get => _isUsbipReady; set => _isUsbipReady = value; }

    public bool IsSupported => IsWindows;

    public bool IsSteamReady => IsSteamRunning && IsLoggedIn;

    private volatile string? _steamGame;

    /// <summary>The Steam game that runs, or null.</summary>
    public string? SteamGame { get => _steamGame; set => _steamGame = value; }

    public string? RunningSteamGame() => IsSteamRunning ? SteamGame : null;

    private int _steamStops;

    public int SteamStops => Volatile.Read(ref _steamStops);

    /// <summary>True: Steam ignores the request to close, as with a dialog of its own open.</summary>
    public bool RefusesToExit { get; set; }

    public Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken)
    {
        if (RefusesToExit)
        {
            return Task.FromResult(false);
        }

        Interlocked.Increment(ref _steamStops);
        IsSteamRunning = false;
        return Task.FromResult(true);
    }

    public void StartSteam()
    {
        Interlocked.Increment(ref _startRequests);
        if (StartsWhenAsked)
        {
            IsSteamRunning = true;
        }
    }

    private bool _panInstalled = true;

    /// <summary>Installed and not failing, as on the real platform.</summary>
    public bool IsPanReady
    {
        get => _panInstalled && !PanFailing;
        set => _panInstalled = value;
    }

    /// <summary>Whether the installed Pan is this Arcadia's (#297): the bundled one unless a test sets another.</summary>
    public Arcadia.ControllerSupport.Pan.PanCompatibility PanCompatibility { get; set; } = Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;

    /// <summary>How often the update at start ran Pan's <c>--install</c> alone (#297), i.e. asked Windows once.</summary>
    public int PanUpdates => Volatile.Read(ref _panUpdates);

    private int _panUpdates;

    /// <summary>What the Windows prompt of the next update or restore answers: true confirmed, false declined.</summary>
    public bool PromptConfirmed { get; set; } = true;

    /// <summary>Set, the next update fails with this message (Pan's exit code, a timeout).</summary>
    public string? PanUpdateError { get; set; }

    /// <summary>Set, the next update throws this, as a bug in Arcadia would (not one of Pan's own failures).</summary>
    public Exception? PanUpdateBug { get; set; }

    /// <summary>Set, the Windows prompt of the next update or restore stays up until the test completes it (#297, #326: presses while it is up).</summary>
    public TaskCompletionSource? PanPromptUp { get; set; }

    public async Task<bool> UpdatePanAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _panUpdates);
        if (PanPromptUp is { } prompt)
        {
            await prompt.Task.WaitAsync(cancellationToken);
        }

        if (PanUpdateError is { } error)
        {
            throw new InvalidOperationException(error);
        }

        if (PanUpdateBug is { } bug)
        {
            throw bug;
        }

        if (PromptConfirmed)
        {
            PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;
        }

        return PromptConfirmed;
    }

    /// <summary>
    /// The Pan service runs (#326): an old Pan left alone ends itself, so false by default; true keeps it running, as an
    /// old Pan that couldn't give the controllers back does.
    /// </summary>
    public bool IsPanRunning { get; set; }

    /// <summary>HidHide's driver version (#243 B2): a supported one unless a test sets another or null (missing).</summary>
    public Version? HidHideVersion { get; set; } = Arcadia.Pan.HidHideRelease.Version;

    /// <summary>HidHide was uninstalled and Windows removes it at the next restart; its version still reads (owner's B2 test).</summary>
    public bool HidHideRemovalPending { get; set; }

    /// <summary>Pan is installed but its last run ended with an error (a data folder it refuses); its install repairs it.</summary>
    public bool PanFailing { get; set; }

    /// <summary>usbip-win2 was installed or updated since Windows started: the restart is recommended, Pan keeps running (#382, S6).</summary>
    public bool UsbipRebootPending { get; set; }

    /// <summary>How often "Treiber installieren" ran, i.e. asked Windows for administrator rights.</summary>
    public int DriverInstalls => Volatile.Read(ref _driverInstalls);

    private int _driverInstalls;

    /// <summary>Set, "Installieren" fails with this message (e.g. the bundled Pan.exe is missing).</summary>
    public string? InstallError { get; set; }

    /// <summary>Set, the install sets up usbip-win2 and Pan but not HidHide: Pan's exit code 6 (#243 B2), with its own text.</summary>
    public string? HidHideInstallError { get; set; }

    public bool InstallsHidHide => ControllerSupportPlatform.HidHideNeedsInstaller(HidHideVersion) && !HidHideRemovalPending;

    /// <summary>usbip-win2 was uninstalled and Windows removes it at the next restart (#382); Pan pauses, as for HidHide.</summary>
    public bool UsbipRemovalPending { get; set; }

    /// <summary>
    /// Set, the pending-restart check throws a new exception from this (a registry read that fails, #382's failure case):
    /// the tick and the settings' refresh may check at once, and one instance thrown on two threads keeps one stack trace.
    /// </summary>
    public Func<Exception>? PendingRestartError { get; set; }

    public PendingRestart CheckPendingRestart(AppSettings settings, DateTimeOffset now) =>
        PendingRestartError is { } error ? throw error()
        : UsbipRemovalPending || HidHideRemovalPending ? PendingRestart.Required
        : UsbipRebootPending ? PendingRestart.Recommended
        : PendingRestart.None;

    /// <summary>How often Windows was asked to restart ("PC neu starten", #382).</summary>
    public int WindowsRestarts => Volatile.Read(ref _windowsRestarts);

    private int _windowsRestarts;

    /// <summary>Set, Windows refuses the restart with this (policy, a blocking program, no answer in time).</summary>
    public Exception? WindowsRestartError { get; set; }

    public Task RestartWindowsAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _windowsRestarts);
        return WindowsRestartError is { } error ? Task.FromException(error) : Task.CompletedTask;
    }

    /// <summary>A controller's firmware is flashed now (#407).</summary>
    public bool FirmwareUpdateRuns { get; set; }

    /// <summary>A Steam Controller or its puck is plugged in (#407).</summary>
    public bool SteamControllerPlugged { get; set; }

    /// <summary>HidHide's installer ran, and with it the puck's restart, while Steam ran (#296): never should.</summary>
    public int PuckRestartsWhileSteamRuns => Volatile.Read(ref _puckRestartsWhileSteamRuns);

    private int _puckRestartsWhileSteamRuns;

    /// <summary>
    /// Elevated Pan of the driver installs (#413): the prompt, the hand-over, the report and the exit code. Set up so a
    /// confirmed prompt installs what the run brings, as today's tests expect; a test can hold or fail any step.
    /// </summary>
    public FakeElevatedPan Pan { get; }

    public FakeSteam()
    {
        Pan = new FakeElevatedPan
        {
            OnHandOver = plan =>
            {
                if (plan.HidHideRepair)
                {
                    Interlocked.Increment(ref _hidHideReinstalls);
                    HidHideReinstalled?.Invoke();
                }
                else
                {
                    Interlocked.Increment(ref _driverInstalls);
                }

                if (IsSteamRunning && plan.HidHide)
                {
                    Interlocked.Increment(ref _puckRestartsWhileSteamRuns);
                }
            },
            OnExit = (plan, exitCode) =>
            {
                if (plan.HidHideRepair || exitCode is not (0 or 6))
                {
                    return;
                }

                // Exit 6: Pan and the usbip driver are set up, HidHide is not.
                IsUsbipReady = true;
                IsPanReady = true;
                PanFailing = false;
                if (exitCode == 0 && plan.HidHide)
                {
                    HidHideVersion = Arcadia.Pan.HidHideRelease.Version;
                }
            },
        };
    }

    public Task<Arcadia.ControllerSupport.Pan.IPanInstallRun?> StartPanInstallAsync(Arcadia.ControllerSupport.Pan.PanInstallPlan plan, CancellationToken cancellationToken)
    {
        if (InstallError is { } error)
        {
            // E.g. the bundled Pan.exe is missing: nothing starts, no prompt.
            throw new InvalidOperationException(error);
        }

        if (HidHideInstallError is not null)
        {
            Pan.ExitCode = Arcadia.ControllerSupport.Pan.PanExitCodes.HidHideFailed;
        }

        return Pan.StartAsync(plan, cancellationToken);
    }

    public Task<string> DownloadUsbipInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) =>
        Pan.DownloadAsync("usbip-win2", folder, progress, cancellationToken);

    public Task<string> DownloadHidHideInstallerAsync(HttpClient http, string folder, IProgress<DownloadProgress> progress, CancellationToken cancellationToken) =>
        Pan.DownloadAsync("HidHide", folder, progress, cancellationToken);

    /// <summary>How often "Neu installieren" ran HidHide's installer over the installed one (#242).</summary>
    public int HidHideReinstalls => Volatile.Read(ref _hidHideReinstalls);

    private int _hidHideReinstalls;

    /// <summary>Called by "Neu installieren", as the real reinstall repairs the driver Pan talks to.</summary>
    public Action? HidHideReinstalled { get; set; }

    /// <summary>How often Pan's <c>--restore</c> ran ("Controller wiederherstellen").</summary>
    public int Restores => Volatile.Read(ref _restores);

    private int _restores;

    /// <summary>Set, the next restore fails with this message (Pan's exit code 5).</summary>
    public string? RestoreError { get; set; }

    /// <summary>Set, Pan's restore runs after the accepted prompt until the test completes it (#471: Arcadia has let go meanwhile).</summary>
    public TaskCompletionSource? RestoreRunning { get; set; }

    public async Task<bool> RestoreControllersAsync(Func<CancellationToken, Task> afterPrompt, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _restores);
        if (PanPromptUp is { } prompt)
        {
            await prompt.Task.WaitAsync(cancellationToken);
        }

        if (!PromptConfirmed)
        {
            return false;
        }

        await afterPrompt(cancellationToken);
        if (RestoreRunning is { } running)
        {
            await running.Task.WaitAsync(cancellationToken);
        }

        if (RestoreError is { } error)
        {
            throw new InvalidOperationException(error);
        }

        return true;
    }

    /// <summary>The rows the real platform shows for usbip-win2, Pan and HidHide, as far as this fake sets them.</summary>
    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) =>
    [
        .. IsUsbipReady ? Array.Empty<HealthIssue>() : [new("usbip-missing", HealthSeverity.Warning, "usbip-Treiber fehlt", "Arcadia erzeugt den virtuellen Controller über diesen Treiber.", HealthFix.InstallDrivers, "Installieren")],
        .. PanFailing ? [EmulationParts.PanFailing] : Array.Empty<HealthIssue>(),
        .. _panInstalled ? Array.Empty<HealthIssue>() : [new("pan-missing", HealthSeverity.Warning, "Controller-Wächter (Pan) fehlt", "Ohne Pan kein Xbox Controller.", HealthFix.InstallDrivers, "Installieren")],
        .. ControllerSupportPlatform.CheckHidHide(HidHideVersion, HidHideRemovalPending) is { } hidHide ? [hidHide] : Array.Empty<HealthIssue>(),
    ];
}

/// <summary>Arcadia's VIIPER as a test sets it: installed or not; "Installieren" brings it without a Windows prompt.</summary>
internal sealed class FakeViiperFile : IViiperInstallation
{
    private volatile bool _isInstalled = true;
    private int _installs;

    public bool IsInstalled { get => _isInstalled; set => _isInstalled = value; }

    public int Installs => Volatile.Read(ref _installs);

    public Task InstallAsync(HttpClient http, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _installs);
        IsInstalled = true;
        return Task.CompletedTask;
    }
}

/// <summary>
/// The network of the tests: GitHub's list of Arcadia releases. Anything
/// else is not found, so no test reaches the real internet.
/// </summary>
internal sealed class FakeNetwork(FakeReleases? releases) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        object? answer = uri switch
        {
            { Host: "api.github.com", AbsolutePath: "/repos/jc-sw-git/Arcadia/releases" } when releases is not null => releases.GitHubList(),
            _ => null,
        };
        return Task.FromResult(answer is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(answer)) });
    }
}

/// <summary>
/// Arcadia installed in <paramref name="installed"/> while GitHub lists <paramref name="published"/> (version and bullet
/// points), newest first or not; the newest is the update Velopack offers.
/// </summary>
internal sealed class FakeReleases(string installed, params (string Version, string[] Changes)[] published)
{
    /// <summary>Set, a download reports 40 % and waits for it before it fails, so a test sees it running.</summary>
    public TaskCompletionSource? HeldDownload { get; init; }

    /// <summary>Set, a download reports 40 % and then gets no further until it is cancelled.</summary>
    public bool StallsDownload { get; init; }

    public UpdateManager Manager(string folder)
    {
        var packages = Directory.CreateDirectory(Path.Combine(folder, "packages")).FullName;
        var locator = new TestVelopackLocator("Arcadia", installed, packages, null);
        return new UpdateManager(new FeedSource(Feed(), HeldDownload, StallsDownload), locator: locator);
    }

    public object GitHubList() => published.Select(release => new
    {
        tag_name = $"v{release.Version}",
        body = string.Join('\n', release.Changes.Select(change => $"* {change}")),
        published_at = ArcadiaDriver.Now,
        prerelease = false,
        draft = false,
    }).ToArray();

    private VelopackAssetFeed Feed() => new()
    {
        Assets = [.. published.Select(release => new VelopackAsset
        {
            PackageId = "Arcadia",
            Version = SemanticVersion.Parse(release.Version),
            Type = VelopackAssetType.Full,
            FileName = $"Arcadia-{release.Version}-full.nupkg",
            SHA1 = new string('0', 40),
            Size = 1,
            NotesMarkdown = string.Join('\n', release.Changes.Select(change => $"* {change}")),
        })],
    };

    private sealed class FeedSource(VelopackAssetFeed feed, TaskCompletionSource? held, bool stalls) : IUpdateSource
    {
        public Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null) =>
            Task.FromResult(feed);

        public async Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken = default)
        {
            if (held is not null)
            {
                progress(40);
                await held.Task;
            }

            if (stalls)
            {
                progress(40);
                await Task.Delay(Timeout.Infinite, cancelToken);
            }

            throw new InvalidOperationException("Die Abnahmetests laden kein Update.");
        }
    }
}
