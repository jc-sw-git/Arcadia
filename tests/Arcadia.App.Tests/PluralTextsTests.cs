using Arcadia.App.Services;
using Arcadia.App.Settings;

namespace Arcadia.App.Tests;

/// <summary>
/// The plural and conditional texts read from <c>AppTexts.resx</c> (#480) give the same strings as the interpolations
/// they replaced, which are written out here as the expected value.
/// </summary>
[Trait("TestCase", "T480-9")]
public sealed class PluralTextsTests
{
    public static TheoryData<int> Counts => [0, 1, 2, 1000];

    [Theory]
    [MemberData(nameof(Counts))]
    public void A_game_count_reads_as_before(int count) =>
        Assert.Equal(count == 1 ? "1 Spiel" : $"{count} Spiele", LauncherTexts.GameCount(count));

    [Theory]
    [MemberData(nameof(Counts))]
    public void The_found_games_toast_reads_as_before(int count) =>
        Assert.Equal(
            count == 1 ? "1 Spiel in Steam gefunden." : $"{count} Spiele in Steam gefunden.",
            count == 1 ? AppTexts.ToastFoundOneGame("Steam") : AppTexts.ToastFoundGames(count, "Steam"));

    [Theory]
    [MemberData(nameof(Counts))]
    public void The_installed_games_toast_reads_as_before(int count) =>
        Assert.Equal(
            count == 1 ? "„Demo Quest“ ist jetzt installiert." : $"{count} Spiele sind jetzt installiert.",
            count == 1 ? AppTexts.ToastGameInstalled("Demo Quest") : AppTexts.ToastGamesInstalled(count));

    [Theory]
    [MemberData(nameof(Counts))]
    public void The_search_title_reads_as_before(int launchers) =>
        Assert.Equal(
            $"12 Treffer in {launchers} {(launchers == 1 ? "Launcher" : "Launchern")}",
            launchers == 1 ? AppTexts.SearchHitsInOneLauncher(12) : AppTexts.SearchHitsInLaunchers(12, launchers));

    [Theory]
    [MemberData(nameof(Counts))]
    public void The_filter_hides_all_note_reads_as_before(int count) =>
        Assert.Equal(
            $"Steam hat {(count == 1 ? "1 Eintrag" : $"{Formatting.Count(count)} Einträge")}, der Filter blendet gerade alle aus.",
            count == 1 ? AppTexts.LibraryFilterHidesTheOne("Steam") : AppTexts.LibraryFilterHidesAll("Steam", Formatting.Count(count)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_failed_scan_reads_as_before(int launchers)
    {
        var names = new[] { "Steam", "Epic Games", "GOG" }[..launchers];
        var expected = $"{string.Join(" und ", names)} konnte{(names.Length == 1 ? string.Empty : "n")}";

        var who = names.Length == 1 ? AppTexts.LauncherCould(names[0]) : AppTexts.LaunchersCould(string.Join(AppTexts.JoinAnd, names));

        Assert.Equal(expected, who);
    }
}
