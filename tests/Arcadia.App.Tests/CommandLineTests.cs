namespace Arcadia.App.Tests;

/// <summary>What Arcadia hands the Arcadia it starts again: after an update (#94, #396) and after "Sprache anwenden" (#554).</summary>
public sealed class CommandLineTests
{
    [Theory]
    [Trait("TestCase", "T634-2")]
    [InlineData(new[] { "--demo", "--data-folder", "/tmp/measure", "--demo-games", "5000" }, "/tmp/measure")]
    [InlineData(new[] { "--demo" }, null)]
    [InlineData(new[] { "--demo", "--data-folder" }, null)]
    [InlineData(new[] { "--data-folder", " " }, null)]
    public void The_data_folder_is_the_argument_after_the_option(string[] args, string? expected)
    {
        Assert.Equal(expected, CommandLine.DataFolder(args));
    }

    [Fact]
    [Trait("TestCase", "T634-2")]
    public void A_restart_keeps_the_data_folder()
    {
        var args = new[] { "--demo", "--data-folder", "/tmp/measure" };

        Assert.Equal(args, CommandLine.Restart(args, inFront: true, []));
    }

    [Theory]
    [Trait("TestCase", "T554-7")]
    [InlineData(true, new[] { "--demo", "--fullscreen", "--show-language-setting" })]
    [InlineData(false, new[] { "--demo", "--show-language-setting" })]
    public void Apply_language_keeps_the_start_arguments_and_the_window_mode_and_opens_on_language(bool fullscreen, string[] expected)
    {
        var kept = CommandLine.Restart(["--demo", "--fullscreen", "--first-run"], inFront: true, []);

        Assert.Equal(expected, CommandLine.LanguageRestart(kept, fullscreen));
    }

    [Fact]
    [Trait("TestCase", "T554-7")]
    public void A_window_started_without_fullscreen_but_now_in_it_comes_back_in_fullscreen()
    {
        Assert.Equal(["--demo", "--fullscreen", "--show-language-setting"], CommandLine.LanguageRestart(["--demo"], fullscreen: true));
    }

    [Fact]
    [Trait("TestCase", "T554-7")]
    public void The_start_after_an_update_does_not_open_the_settings_again()
    {
        var args = new[] { "--demo", "--show-language-setting", "--background", "--started-game", "gog:alpha" };

        Assert.Equal(["--demo", "--background", "--started-game", "gog:bravo"], CommandLine.Restart(args, inFront: false, ["gog:bravo"]));
    }
}
