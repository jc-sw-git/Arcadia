using System.Net;
using System.Text.Json;
using Arcadia.ControllerSupport;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Steam;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;

namespace Arcadia.App.Tests;

/// <summary>
/// Windows with Steam and SISR as a test sets them: Steam runs or not, starts when asked or not, logs in or hangs at
/// "Nutzerdaten werden geladen"; SISR is installed or not and emulates a Steam Controller while it runs. Read from the
/// thread pool by the controller support, so every value is volatile.
/// </summary>
internal sealed class FakeSteam : IControllerSupportPlatform
{
    private volatile bool _isWindows = true;
    private volatile bool _isSteamInstalled = true;
    private volatile bool _isSteamRunning;
    private volatile bool _isLoggedIn = true;
    private volatile bool _startsWhenAsked = true;
    private volatile bool _isUsbipReady = true;
    private volatile bool _isCefDebuggingEnabled = true;
    private volatile bool _hasMarker = true;
    private int _startRequests;

    public FakeSteam()
    {
        Sisr = new FakeSisr(this);
    }

    public FakeSisr Sisr { get; }

    /// <summary>False: Linux or macOS, where SISR doesn't exist.</summary>
    public bool IsWindows { get => _isWindows; set => _isWindows = value; }

    public bool IsSteamInstalled { get => _isSteamInstalled; set => _isSteamInstalled = value; }

    public bool IsSteamRunning { get => _isSteamRunning; set => _isSteamRunning = value; }

    /// <summary>False: Steam runs, but hangs at "Nutzerdaten werden geladen".</summary>
    public bool IsLoggedIn { get => _isLoggedIn; set => _isLoggedIn = value; }

    /// <summary>False: asking Steam to start does nothing, e.g. its folder was renamed.</summary>
    public bool StartsWhenAsked { get => _startsWhenAsked; set => _startsWhenAsked = value; }

    public int StartRequests => Volatile.Read(ref _startRequests);

    public bool IsUsbipReady { get => _isUsbipReady; set => _isUsbipReady = value; }

    public bool IsCefDebuggingEnabled { get => _isCefDebuggingEnabled; set => _isCefDebuggingEnabled = value; }

    /// <summary>"SISR Marker" is in the Steam library.</summary>
    public bool HasMarker { get => _hasMarker; set => _hasMarker = value; }

    public bool IsSupported => IsWindows;

    public bool IsSteamReady => IsSteamRunning && IsLoggedIn;

    public bool IsSteamUp => IsSteamRunning;

    public bool EnsureSisrMarker(string sisrExecutable, IReadOnlyCollection<uint>? accountIds = null)
    {
        if (IsSteamRunning)
        {
            return false;
        }

        HasMarker = true;
        return true;
    }

    public IReadOnlyList<MarkerAccount> SisrMarkerAccounts(string sisrExecutable) => [];

    public bool HasSisrMarker(string sisrExecutable) => HasMarker;

    public void EnableCefDebugging() => IsCefDebuggingEnabled = true;

    public string? RunningSteamGame() => null;

    public int? RunningSteamAppId() => null;

    public SteamInputSetting? SteamInputFor(int appId) => null;

    public SteamInputSetting? SteamInputDefaults() => null;

    public Task<bool> StopSteamAsync(TimeProvider time, CancellationToken cancellationToken)
    {
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

    public bool IsPanReady { get; set; } = true;

    public bool IsPanUpdateAvailable => false;

    public Task<bool> InstallDriversAsync(HttpClient http, CancellationToken cancellationToken)
    {
        IsUsbipReady = true;
        IsPanReady = true;
        return Task.FromResult(true);
    }

    public Task<bool> RestoreControllersAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public IReadOnlyList<HealthIssue> CheckDrivers(AppSettings settings, DateTimeOffset now) =>
        IsPanReady ? [] : [new("pan-missing", HealthSeverity.Warning, "Wächterdienst Pan fehlt", "Ohne Pan kein Xbox Controller.", HealthFix.InstallDrivers, "Installieren")];

    public void ResetSteamInputLayout()
    {
    }

    public bool AnyRunningFrom(string directory) => false;

    public IReadOnlyList<HealthIssue> CheckDependencies(AppSettings settings, DateTimeOffset now) => [];
}

/// <summary>SISR as the test sets it; started, it emulates the one Steam Controller of the PC through <see cref="FakeNetwork"/>.</summary>
internal sealed class FakeSisr(FakeSteam steam) : ISisrHost
{
    private volatile bool _isInstalled = true;
    private volatile bool _isRunning;
    private volatile Release? _latestRelease;
    private int _starts;
    private int _installs;

    public SisrApiClient Api { get; private set; } = null!;

    public bool IsInstalled { get => _isInstalled; set => _isInstalled = value; }

    public string ExecutablePath => "sisr.exe";

    public Version? InstalledVersion => IsInstalled ? new Version(1, 0, 0) : null;

    public bool IsRunning => _isRunning;

    /// <summary>What GitHub names as SISR's newest release; newer than 1.0.0, it is an update.</summary>
    public Release? LatestRelease { get => _latestRelease; set => _latestRelease = value; }

    public int Starts => Volatile.Read(ref _starts);

    public int Installs => Volatile.Read(ref _installs);

    public FakeSteam Steam => steam;

    public void UseNetwork(HttpClient http) => Api = new SisrApiClient(http, FakeNetwork.SisrPort);

    public IReadOnlyList<int> ExternalInstances() => [];

    public void StopExternalInstances()
    {
    }

    public Task<Release?> GetLatestReleaseAsync(CancellationToken cancellationToken = default) => Task.FromResult(LatestRelease);

    public Task InstallAsync(Release release, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _installs);
        IsInstalled = true;
        return Task.CompletedTask;
    }

    public void Start()
    {
        Interlocked.Increment(ref _starts);
        _isRunning = true;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isRunning = false;
        return Task.CompletedTask;
    }

    public void Kill() => _isRunning = false;

    public void MarkInitialSetupDone()
    {
    }

    public Task<int> CleanUpViiperAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

/// <summary>
/// The network of the tests: GitHub's list of Arcadia releases and the API of a running <see cref="FakeSisr"/>. Anything
/// else is not found, so no test reaches the real internet.
/// </summary>
internal sealed class FakeNetwork(FakeSisr? sisr, FakeReleases? releases) : HttpMessageHandler
{
    public const int SisrPort = 1;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        object? answer = uri switch
        {
            { Host: "api.github.com", AbsolutePath: "/repos/jc-sw-git/Arcadia/releases" } when releases is not null => releases.GitHubList(),
            { Host: "127.0.0.1", Port: SisrPort } when sisr is { IsRunning: true } => SisrAnswer(uri.AbsolutePath),
            _ => null,
        };
        return Task.FromResult(answer is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(answer)) });
    }

    private static object? SisrAnswer(string path) => path switch
    {
        "/api/v1/version/info" => new { version = "1.0.0" },
        "/api/v1/steam/status" => new { steam_running = true, cef_debug_enabled = true, cef_debug_reachable = true },
        "/api/v1/viiper/ping" => new { address = "127.0.0.1:3242", status = new { server = "viiper", version = "1.0.0" } },
        "/api/v1/devices" => new[]
        {
            new { real_device = new { name = "Steam Controller", path = @"\\?\HID#VID_28DE&PID_1304" }, viiper_device = new { type = "xbox360" } },
        },
        _ => null,
    };
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
