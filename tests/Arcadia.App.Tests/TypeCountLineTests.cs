using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using static Arcadia.App.Tests.TestGames;

namespace Arcadia.App.Tests;

/// <summary>The count line beside a tab's name and on a launcher's page (#141), and how the types are named.</summary>
public sealed class TypeCountLineTests
{
    private static readonly LauncherInfo Launcher = KnownLaunchers.Steam;

    private static Game Entry(string title, GameType type, bool installed = true) =>
        (installed ? Installed(Launcher, title, ArcadiaDriver.Now) : NotInstalled(Launcher, title)) with { Type = type };

    [Fact]
    [Trait("TestCase", "T141-7")]
    public void One_part_per_type_in_order_with_the_installed_ones_in_brackets_unless_all_are()
    {
        var line = TypeCountLine.Of(
        [
            Entry("Soundtrack", GameType.Soundtrack),
            Entry("Blender", GameType.Tool),
            Entry("Hades", GameType.Game),
            Entry("Celeste", GameType.Game, installed: false),
            Entry("SDK", GameType.Tool),
        ]);

        Assert.Equal(
            [
                new TypeCountPart(GameType.Game, "2", "1", ShowsInstalled: true, HasDivider: false),
                new TypeCountPart(GameType.Tool, "2", "2", ShowsInstalled: false, HasDivider: true),
                new TypeCountPart(GameType.Soundtrack, "1", "1", ShowsInstalled: false, HasDivider: true),
            ],
            line.Parts);
        Assert.Equal("2 Spiele, davon 1 installiert · 2 Werkzeuge · 1 Soundtrack", line.Tip);
        Assert.Equal(["IconTypeGame", "IconTypeTool", "IconTypeSoundtrack"], line.Parts.Select(part => part.IconKey));
    }

    [Fact]
    [Trait("TestCase", "T141-7")]
    public void Large_numbers_get_a_thousands_separator_and_an_empty_library_has_no_parts()
    {
        var games = Enumerable.Range(0, 1234).Select(i => Entry($"Spiel {i}", GameType.Game, installed: i == 0)).ToList();

        var line = TypeCountLine.Of(games);

        Assert.Equal(("1.234", "1"), (line.Parts[0].TotalText, line.Parts[0].InstalledText));
        Assert.Equal("1.234 Spiele, davon 1 installiert", line.Tip);
        Assert.False(TypeCountLine.Of(Array.Empty<Game>()).HasParts);
    }

    [Theory]
    [Trait("TestCase", "T141-5")]
    [InlineData(GameType.Game, 1, "1 Spiel", "Spiel", "Spielen")]
    [InlineData(GameType.Game, 2, "2 Spiele", "Spiel", "Spielen")]
    [InlineData(GameType.Software, 1, "1 Software", "Software", "Tüfteln")]
    [InlineData(GameType.Software, 3, "3 Software", "Software", "Tüfteln")]
    [InlineData(GameType.Tool, 1, "1 Werkzeug", "Werkzeug", "Tüfteln")]
    [InlineData(GameType.Tool, 2, "2 Werkzeuge", "Werkzeug", "Tüfteln")]
    [InlineData(GameType.Soundtrack, 1, "1 Soundtrack", "Soundtrack", "Abrocken")]
    [InlineData(GameType.Soundtrack, 2, "2 Soundtracks", "Soundtrack", "Abrocken")]
    [InlineData((GameType)99, 2, "2 Software", "Software", "Tüfteln")]
    public void Each_type_has_its_name_count_and_what_A_does(GameType type, int count, string counted, string name, string action)
    {
        Assert.Equal(counted, GameTypeTexts.Count(type, count));
        Assert.Equal(name, GameTypeTexts.Name(type));
        Assert.Equal(action, GameTypeTexts.PrimaryAction(type));
    }
}
