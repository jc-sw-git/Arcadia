using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Tests;

/// <summary>
/// #357: the foreground is asked on every gamepad poll, so <see cref="ForegroundOwner"/> looks up a process's path only
/// when the foreground process changed, and no answer outlives a switch.
/// </summary>
public sealed class ForegroundOwnerTests
{
    private const int GameProcess = 4100;
    private const int SteamProcess = 4200;
    private const int BrowserProcess = 4300;

    private readonly Dictionary<int, string> _paths = new()
    {
        [GameProcess] = @"D:\Games\Hollow Peak\HollowPeak.exe",
        [SteamProcess] = "C:/Program Files (x86)/Steam/steam.exe",
        [BrowserProcess] = @"C:\Programs\Browser\browser.exe",
    };

    private readonly List<int> _pathLookups = [];
    private GameFolders _folders = GameFolders.Of([Game(@"D:\Games\Hollow Peak")]);
    private int? _foreground;

    private ForegroundOwner Create() => new(() => _folders, new LaunchedGames(), ["steam.exe", "steamwebhelper.exe"], () => _foreground, id =>
    {
        _pathLookups.Add(id);
        return _paths.GetValueOrDefault(id);
    });

    [Fact]
    [Trait("TestCase", "T357-1")]
    public void A_switch_is_seen_at_once_and_the_path_is_looked_up_once_per_process()
    {
        var owner = Create();
        _foreground = GameProcess;
        Assert.Equal("test:hollow-peak", owner.GameKey());
        Assert.False(owner.IsSteam());

        _foreground = SteamProcess;
        Assert.True(owner.IsSteam());
        Assert.Null(owner.GameKey());

        _foreground = BrowserProcess;
        Assert.False(owner.IsSteam());
        Assert.Null(owner.GameKey());

        Assert.Equal([GameProcess, SteamProcess, BrowserProcess], _pathLookups);
    }

    [Fact]
    [Trait("TestCase", "T357-1")]
    public void No_foreground_window_is_neither_Steam_nor_a_game_and_the_last_process_is_looked_up_again()
    {
        var owner = Create();
        _foreground = GameProcess;
        Assert.NotNull(owner.GameKey());

        _foreground = null;
        Assert.Null(owner.GameKey());
        Assert.False(owner.IsSteam());

        _foreground = GameProcess;
        Assert.NotNull(owner.GameKey());
        Assert.Single(_pathLookups);
    }

    [Fact]
    public void A_new_library_asks_again()
    {
        var owner = Create();
        _foreground = GameProcess;
        Assert.NotNull(owner.GameKey());

        _folders = GameFolders.Empty;
        Assert.Null(owner.GameKey());
        Assert.Equal(2, _pathLookups.Count);
    }

    [Fact]
    public void Arcadia_itself_is_never_a_game()
    {
        _paths[Environment.ProcessId] = @"D:\Games\Hollow Peak\Arcadia.exe";
        _foreground = Environment.ProcessId;

        Assert.Null(Create().GameKey());
    }

    private static Game Game(string installDirectory) => new()
    {
        Id = "hollow-peak",
        LauncherId = "test",
        Title = "Hollow Peak",
        Launch = new LaunchTarget.None(),
        InstallDirectory = installDirectory,
    };
}
