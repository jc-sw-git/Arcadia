using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;
using Arcadia.Launchers.Steam.Account;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>Owned games the scanner adds as not installed (#83), and the small parts of the Steam account.</summary>
public sealed class SteamOwnedGamesScanTests : IDisposable
{
    private const uint Account = 100_000_001;

    private readonly string _root = Directory.CreateTempSubdirectory("arcadia-steam-owned-").FullName;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeSteamWeb _web;
    private readonly HttpClient _http;
    private readonly SteamOnlineAccount _account;

    public SteamOwnedGamesScanTests()
    {
        _web = new FakeSteamWeb(_time.GetUtcNow());
        _http = new HttpClient(_web);
        _account = new SteamOnlineAccount(new SteamWebApi(_http), new SteamAccountStore(Path.Combine(_root, "Arcadia"), d => d, d => d), _time);
    }

    private string SteamRoot => Path.Combine(_root, "Steam");

    [Fact]
    public async Task Owned_games_join_the_installed_ones_as_not_installed()
    {
        WriteSteam();
        _web.OwnedGames.AddRange(
        [
            (10, "Counter-Strike", 999, 1_700_000_000),
            (20, "Team Fortress Classic", 125, 1_600_000_000),
            (30, "Hidden Game", 0, 0),
            (40, "Half-Life Soundtrack", 0, 0),
            (50, "Some DLC", 0, 0),
        ]);

        // Only the store lists these: a tool, DLC, and an app appinfo.vdf doesn't know (so nothing names or types it).
        _web.StoreApps.AddRange([60u, 70u, 80u]);
        WriteFile(Path.Combine(SteamRoot, "appcache", "appinfo.vdf"), AppInfoFile.Build(AppInfoFile.V29,
        [
            (10, "Counter-Strike", "Game"),
            (40, "Half-Life Soundtrack", "Music"),
            (50, "Some DLC", "DLC"),
            (60, "Dedicated Server", "Tool"),
            (80, "Store DLC", "DLC"),
        ]));
        await SignInAsync();

        var games = (await new SteamScanner(() => new SteamInstallation(SteamRoot, Account), _account).ScanAsync()).OrderBy(g => g.Id).ToList();

        Assert.Equal(["10", "20", "40", "60"], games.Select(g => g.Id));
        var installed = games[0];
        Assert.True(installed.IsInstalled);
        // Installed: the account's localconfig.vdf counts, not the web API.
        Assert.Equal(TimeSpan.FromMinutes(60), installed.Playtime);

        var uninstalled = games[1];
        Assert.False(uninstalled.IsInstalled);
        Assert.Equal("Team Fortress Classic", uninstalled.Title);
        Assert.Equal(GameType.Game, uninstalled.Type);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://install/20"), uninstalled.Install);
        Assert.Equal(new LaunchTarget.ProtocolUri("steam://nav/games/details/20"), uninstalled.OpenInLauncher);
        Assert.Equal(TimeSpan.FromMinutes(125), uninstalled.Playtime);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_600_000_000), uninstalled.LastPlayedAt);
        Assert.Null(uninstalled.InstallDirectory);

        Assert.Equal(GameType.Soundtrack, games[2].Type);
        Assert.Equal(GameType.Tool, games[3].Type);
        Assert.Equal("Dedicated Server", games[3].Title);
        Assert.False(games[3].IsInstalled);
        Assert.All(games, game => Assert.Null(game.Notice));
    }

    [Fact]
    public async Task Another_account_in_the_running_Steam_keeps_the_games_but_blocks_installs_and_says_so()
    {
        WriteSteam();
        _web.OwnedGames.Add((20, "Team Fortress Classic", 0, 0));
        await SignInAsync();

        var games = (await new SteamScanner(() => new SteamInstallation(SteamRoot, Account + 1), _account).ScanAsync()).OrderBy(g => g.Id).ToList();

        Assert.Equal(["10", "20"], games.Select(g => g.Id));
        Assert.All(games, game => Assert.Equal(SteamScanner.OtherAccountNotice, game.Notice));
        Assert.Null(games[0].InstallBlockedReason);
        Assert.Equal(SteamScanner.OtherAccountInstall, games[1].InstallBlockedReason);
        Assert.Equal(SteamOnlineAccount.OtherAccountNotice, _account.State.Notice);
    }

    [Fact]
    public async Task A_closed_Steam_compares_with_no_account()
    {
        WriteSteam();
        _web.OwnedGames.Add((20, "Team Fortress Classic", 0, 0));
        await SignInAsync();

        // Steam writes ActiveUser 0 when it closes; it starts with whichever account the user picks then.
        var games = await new SteamScanner(() => new SteamInstallation(SteamRoot, 0), _account).ScanAsync();

        Assert.Equal(2, games.Count);
        Assert.All(games, game => Assert.Null(game.Notice));
        Assert.All(games, game => Assert.Null(game.InstallBlockedReason));
    }

    [Fact]
    public void Hidden_games_are_read_from_Steams_collections()
    {
        var hidden = SteamHiddenGames.Parse("""
            [
              ["user-collections.favorite", {"key":"user-collections.favorite","value":"{\"id\":\"favorite\",\"added\":[1]}"}],
              ["user-collections.hidden", {"key":"user-collections.hidden","timestamp":1756884645,"value":"{\"id\":\"hidden\",\"name\":\"Versteckt\",\"added\":[544730,30],\"removed\":[]}","version":"2172"}]
            ]
            """);

        Assert.Equal([30u, 544730u], hidden.Order());
        Assert.Empty(SteamHiddenGames.Parse("not json"));
        Assert.Empty(SteamHiddenGames.Parse("""[["user-collections.hidden", {"is_deleted":true,"value":"{\"added\":[5]}"}]]"""));
    }

    [Fact]
    public void Protobuf_messages_round_trip_and_a_cut_message_is_refused()
    {
        var bytes = new ProtoWriter().UInt(1, ulong.MaxValue).String(2, "hällo").Bytes(3, [9, 8]).Fixed64(4, 76561197960265729).Int32(5, -500).ToArray();

        var message = ProtoMessage.Parse(bytes);

        Assert.Equal(ulong.MaxValue, message.UInt(1));
        Assert.Equal("hällo", message.String(2));
        Assert.Equal([9, 8], message.Bytes(3));
        Assert.Equal(76561197960265729UL, message.UInt(4));
        Assert.Equal(-500, unchecked((int)(long)message.UInt(5)!.Value));
        Assert.Null(message.UInt(7));
        Assert.Throws<FormatException>(() => ProtoMessage.Parse(bytes.AsSpan(0, bytes.Length - 3)));
    }

    [Fact]
    public void Token_tells_its_account_and_expiry()
    {
        var expires = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var token = FakeSteamWeb.Token(FakeSteamWeb.SteamId, expires, "access");

        Assert.Equal(FakeSteamWeb.SteamId, SteamToken.SteamIdOf(token));
        Assert.Equal(expires, SteamToken.ExpiresAt(token));
        Assert.Null(SteamToken.SteamIdOf("no.token"));
        Assert.Null(SteamToken.ExpiresAt("a.!!!.c"));
    }

    public void Dispose()
    {
        _account.Dispose();
        _http.Dispose();
        _web.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private async Task SignInAsync()
    {
        _web.IsApproved = true;
        _account.BeginSignIn();
        for (var i = 0; i < 2000 && _account.State.OwnedGames is null; i++)
        {
            await Task.Delay(1);
            _time.Advance(TimeSpan.FromMilliseconds(250));
        }

        Assert.NotNull(_account.State.OwnedGames);
    }

    /// <summary>Steam with Counter-Strike installed and one hidden game in the account's collections.</summary>
    private void WriteSteam()
    {
        WriteText(Path.Combine(SteamRoot, "steamapps", "appmanifest_10.acf"), """
            "AppState" { "appid" "10" "name" "Counter-Strike" "StateFlags" "4" "installdir" "Counter-Strike" "buildid" "1" "LastOwner" "76561197960265728" }
            """);
        var config = Path.Combine(SteamRoot, "userdata", Account.ToString(System.Globalization.CultureInfo.InvariantCulture), "config");
        WriteText(Path.Combine(config, "localconfig.vdf"), """
            "UserLocalConfigStore" { "Software" { "Valve" { "Steam" { "apps" { "10" { "Playtime" "60" } } } } } }
            """);
        WriteText(Path.Combine(config, "cloudstorage", "cloud-storage-namespace-1.json"), """
            [["user-collections.hidden", {"key":"user-collections.hidden","value":"{\"id\":\"hidden\",\"added\":[30]}"}]]
            """);
    }

    private static void WriteText(string path, string text) => WriteFile(path, System.Text.Encoding.UTF8.GetBytes(text));

    private static void WriteFile(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }
}
