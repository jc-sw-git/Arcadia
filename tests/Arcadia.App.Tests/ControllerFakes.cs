using System.Net;
using System.Text.Json;
using Arcadia.ControllerSupport;
using Arcadia.Tests.Sisr;
using Arcadia.Core.Settings;
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
internal sealed class FakeSteam : IControllerSupportPlatform
{
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

    public int? RunningSteamAppId() => null;

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

    /// <summary>How often "Pan entfernen" ran Pan's <c>--uninstall</c> (#297).</summary>
    public int PanUninstalls => Volatile.Read(ref _panUninstalls);

    private int _panUninstalls;

    /// <summary>What the Windows prompt of the next update or uninstall answers: true confirmed, false declined.</summary>
    public bool PromptConfirmed { get; set; } = true;

    /// <summary>Set, the next update fails with this message (Pan's exit code, a timeout).</summary>
    public string? PanUpdateError { get; set; }

    /// <summary>Set, the next update's Windows prompt stays up until the test completes it (#297: presses while it is up).</summary>
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

        if (PromptConfirmed)
        {
            PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.Compatible;
        }

        return PromptConfirmed;
    }

    public Task<bool> UninstallPanAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _panUninstalls);
        if (PromptConfirmed)
        {
            PanCompatibility = Arcadia.ControllerSupport.Pan.PanCompatibility.NotInstalled;
            IsPanReady = false;
        }

        return Task.FromResult(PromptConfirmed);
    }

    /// <summary>HidHide's driver version (#243 B2): a supported one unless a test sets another or null (missing).</summary>
    public Version? HidHideVersion { get; set; } = Arcadia.Pan.HidHideRelease.Version;

    /// <summary>HidHide was uninstalled and Windows removes it at the next restart; its version still reads (owner's B2 test).</summary>
    public bool HidHideRemovalPending { get; set; }

    /// <summary>Pan is installed but its last run ended with an error (a data folder it refuses); its install repairs it.</summary>
    public bool PanFailing { get; set; }

    /// <summary>The usbip driver was updated and Windows wants a restart: the platform's "reboot-pending" row.</summary>
    public bool UsbipRebootPending { get; set; }

    /// <summary>How often "Treiber installieren" ran, i.e. asked Windows for administrator rights.</summary>
    public int DriverInstalls => Volatile.Read(ref _driverInstalls);

    private int _driverInstalls;

    /// <summary>Set, "Installieren" fails with this message (e.g. the bundled Pan.exe is missing).</summary>
    public string? InstallError { get; set; }

    /// <summary>Set, the install sets up usbip-win2 and Pan but not HidHide and fails with this text (Pan's exit code 6, #243 B2).</summary>
    public string? HidHideInstallError { get; set; }

    public bool InstallsHidHide => ControllerSupportPlatform.HidHideNeedsInstaller(HidHideVersion) && !HidHideRemovalPending;

    /// <summary>HidHide's installer ran, and with it the puck's restart, while Steam ran (#296): never should.</summary>
    public int PuckRestartsWhileSteamRuns => Volatile.Read(ref _puckRestartsWhileSteamRuns);

    private int _puckRestartsWhileSteamRuns;

    public Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _driverInstalls);
        if (IsSteamRunning && InstallsHidHide)
        {
            Interlocked.Increment(ref _puckRestartsWhileSteamRuns);
        }

        if (InstallError is { } error)
        {
            throw new InvalidOperationException(error);
        }

        if (HidHideInstallError is { } hidHideError)
        {
            // Pan's exit code 6: Pan and the usbip driver are set up, HidHide is not (PanSetup throws after them).
            IsUsbipReady = true;
            IsPanReady = true;
            throw new InvalidOperationException(hidHideError);
        }

        IsUsbipReady = true;
        IsPanReady = true;
        PanFailing = false;
        if (InstallsHidHide)
        {
            HidHideVersion = Arcadia.Pan.HidHideRelease.Version;
        }

        return Task.FromResult(true);
    }

    /// <summary>How often "Neu installieren" ran HidHide's installer over the installed one (#242).</summary>
    public int HidHideReinstalls => Volatile.Read(ref _hidHideReinstalls);

    private int _hidHideReinstalls;

    /// <summary>Called by "Neu installieren", as the real reinstall repairs the driver Pan talks to.</summary>
    public Action? HidHideReinstalled { get; set; }

    public Task<bool> ReinstallHidHideAsync(HttpClient http, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _hidHideReinstalls);
        if (IsSteamRunning)
        {
            Interlocked.Increment(ref _puckRestartsWhileSteamRuns);
        }

        HidHideReinstalled?.Invoke();
        return Task.FromResult(true);
    }

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    /// <summary>The rows the real platform shows for usbip-win2, Pan and HidHide, as far as this fake sets them.</summary>
    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) =>
    [
        .. IsUsbipReady ? Array.Empty<HealthIssue>() : [new("usbip-missing", HealthSeverity.Warning, "usbip-Treiber fehlt", "Arcadia erzeugt den virtuellen Controller über diesen Treiber.", HealthFix.InstallDrivers, "Installieren")],
        .. PanFailing ? [EmulationParts.PanFailing] : Array.Empty<HealthIssue>(),
        .. _panInstalled ? Array.Empty<HealthIssue>() : [new("pan-missing", HealthSeverity.Warning, "Wächterdienst Pan fehlt", "Ohne Pan kein Xbox Controller.", HealthFix.InstallDrivers, "Installieren")],
        .. UsbipRebootPending ? [EmulationParts.RebootPending] : Array.Empty<HealthIssue>(),
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
    public UpdateManager Manager(string folder)
    {
        var packages = Directory.CreateDirectory(Path.Combine(folder, "packages")).FullName;
        var locator = new TestVelopackLocator("Arcadia", installed, packages, null);
        return new UpdateManager(new FeedSource(Feed()), locator: locator);
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

    private sealed class FeedSource(VelopackAssetFeed feed) : IUpdateSource
    {
        public Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null) =>
            Task.FromResult(feed);

        public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken = default) =>
            throw new InvalidOperationException("Die Abnahmetests laden kein Update.");
    }
}
