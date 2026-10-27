using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests;

/// <summary>Steam's console log (#277), with lines as Steam wrote them on the owner's PC on 2026-10-27.</summary>
public sealed class SteamConsoleLogTests : IDisposable
{
    // Steam writes local time; the tests run in a zone of their own, so the result doesn't depend on the machine's.
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("Test", TimeSpan.FromHours(1), "Test", "Test");

    private const string NoLicense =
        """
        [2026-10-27 18:01:01] GameAction [AppID 214490, ActionID 1] : LaunchApp changed task to RequestingLicense with ""
        [2026-10-27 18:01:02] GameAction [AppID 214490, ActionID 1] : LaunchApp failed with AppError_5 with ""
        [2026-10-27 18:01:02] GameAction [AppID 214490, ActionID 1] : LaunchApp changed task to Failed with ""
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-console-log-").FullName;

    private string LogPath => Path.Combine(_folder, "console_log.txt");

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private SteamConsoleLog CreateLog() => new(() => LogPath, Zone);

    private static IReadOnlyList<RefusedStart> Parse(string lines) => new SteamConsoleLog(() => null, Zone).RefusalsIn(lines.Split('\n'));

    [Fact]
    [Trait("TestCase", "T277-1")]
    public void A_start_steam_refuses_for_a_missing_license_is_named_with_its_reason()
    {
        var refusal = Assert.Single(Parse(NoLicense));

        Assert.Equal(
            new RefusedStart("214490", new DateTimeOffset(2026, 10, 27, 18, 1, 2, TimeSpan.FromHours(1)), "Das angemeldete Steam-Konto besitzt dieses Spiel nicht.", "AppError_5"),
            refusal);
    }

    [Fact]
    [Trait("TestCase", "T277-5")]
    public void An_unknown_code_has_no_reason_and_its_detail_leaves_out_the_path_steam_names()
    {
        var refusal = Assert.Single(Parse(
            """[2026-10-26 21:14:09] GameAction [AppID 550, ActionID 3] : LaunchApp failed with AppError_28 with "F:\SteamLibrary\steamapps\common\Left 4 Dead 2\left4dead2.exe" """));

        Assert.Equal("550", refusal.GameId);
        Assert.Null(refusal.Reason);
        Assert.Equal("AppError_28", refusal.Detail);
    }

    [Fact]
    [Trait("TestCase", "T277-2")]
    public void Steam_waiting_for_the_player_and_a_start_that_completes_are_no_failure()
    {
        Assert.Empty(Parse(
            """
            [2026-10-27 18:05:10] GameAction [AppID 4231820, ActionID 2] : LaunchApp changed task to ShowLaunchOption with ""
            [2026-10-27 18:05:10] GameAction [AppID 4231820, ActionID 2] : LaunchApp waiting for user response to ShowLaunchOption ""
            [2026-10-27 18:05:16] GameAction [AppID 4231820, ActionID 2] : LaunchApp continues with user response "1"
            [2026-10-27 18:05:16] GameAction [AppID 4231820, ActionID 2] : LaunchApp waiting for user response to ShowEula ""
            [2026-10-27 18:05:20] GameAction [AppID 4231820, ActionID 2] : LaunchApp waiting for user response to CreatingProcess ""
            [2026-10-27 18:05:21] GameAction [AppID 4231820, ActionID 2] : LaunchApp changed task to Completed with ""
            """));
    }

    [Fact]
    public void A_line_without_steams_time_stamp_is_left_alone()
    {
        Assert.Empty(Parse("GameAction [AppID 214490, ActionID 1] : LaunchApp failed with AppError_5 with \"\""));
    }

    [Fact]
    [Trait("TestCase", "T277-3")]
    public void What_steam_logged_before_the_first_read_belongs_to_no_start_of_arcadias()
    {
        File.WriteAllText(LogPath, NoLicense + "\n");
        var log = CreateLog();

        Assert.Empty(log.ReadRefusals());
        Assert.Empty(log.ReadRefusals());
    }

    [Fact]
    [Trait("TestCase", "T277-1")]
    public void An_abort_is_reported_once_also_when_steam_writes_its_line_in_two_parts()
    {
        File.WriteAllText(LogPath, "[2026-10-27 17:00:00] older start\n");
        var log = CreateLog();
        log.ReadRefusals();

        var line = NoLicense.Split('\n')[1];
        File.AppendAllText(LogPath, line[..40]);
        Assert.Empty(log.ReadRefusals());

        File.AppendAllText(LogPath, line[40..] + "\r\n");
        Assert.Equal("214490", Assert.Single(log.ReadRefusals()).GameId);
        Assert.Empty(log.ReadRefusals());
    }

    [Fact]
    public void A_new_file_after_steam_started_over_is_read_from_its_start()
    {
        File.WriteAllText(LogPath, new string('x', 4000) + "\n");
        var log = CreateLog();
        log.ReadRefusals();

        File.WriteAllText(LogPath, NoLicense + "\n");

        Assert.Single(log.ReadRefusals());
    }

    [Fact]
    [Trait("TestCase", "T277-4")]
    public void Without_a_readable_log_nothing_is_reported()
    {
        Assert.Empty(CreateLog().ReadRefusals());
        Assert.Empty(new SteamConsoleLog(() => null).ReadRefusals());
    }
}
