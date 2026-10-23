using Arcadia.Core.Launchers;
using Arcadia.Core.Settings;

namespace Arcadia.Core.Tests;

public sealed class FirstRunSetupTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("arcadia-first-run-").FullName;

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private SettingsService Settings() => new(new SettingsStore(SettingsPath));

    [Fact]
    public void Shows_while_no_launcher_list_was_saved()
    {
        var setup = new FirstRunSetup(Settings(), demo: false, forced: false);

        Assert.True(setup.IsPending);
        Assert.Null(setup.ChosenLaunchers());
    }

    [Fact]
    public void Shows_after_an_update_from_a_version_without_it()
    {
        File.WriteAllText(SettingsPath, """{ "SteamControllerSupport": true, "LastTabLauncherId": "epic" }""");

        Assert.True(new FirstRunSetup(Settings(), demo: false, forced: false).IsPending);
    }

    [Fact]
    public void Doesnt_show_once_a_list_was_saved_even_an_empty_one()
    {
        Settings().Update(settings => settings with { ActiveLaunchers = [] });

        var setup = new FirstRunSetup(Settings(), demo: false, forced: false);

        Assert.False(setup.IsPending);
        Assert.Empty(setup.ChosenLaunchers()!);
    }

    [Fact]
    public void Done_saves_the_launchers_and_doesnt_show_again_after_a_restart()
    {
        var setup = new FirstRunSetup(Settings(), demo: false, forced: false);

        setup.Complete(["steam", "gog"]);

        Assert.False(setup.IsPending);
        Assert.Equal(["steam", "gog"], setup.ChosenLaunchers());
        var restarted = new FirstRunSetup(Settings(), demo: false, forced: false);
        Assert.False(restarted.IsPending);
        Assert.Equal(["steam", "gog"], restarted.ChosenLaunchers());
    }

    [Fact]
    public void Demo_skips_it_and_saves_nothing()
    {
        var setup = new FirstRunSetup(Settings(), demo: true, forced: false);

        Assert.False(setup.IsPending);
        Assert.Null(setup.ChosenLaunchers());
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void First_run_asks_again_and_reads_every_launcher_found_meanwhile()
    {
        Settings().Update(settings => settings with { ActiveLaunchers = ["epic"] });

        var setup = new FirstRunSetup(Settings(), demo: false, forced: true);

        Assert.True(setup.IsPending);
        Assert.Null(setup.ChosenLaunchers());
        setup.Complete(["steam"]);
        Assert.Equal(["steam"], Settings().Current.ActiveLaunchers);
    }

    [Fact]
    public void Demo_with_first_run_holds_the_answer_for_the_session_only()
    {
        var settings = Settings();
        settings.Update(s => s with { ActiveLaunchers = ["epic"] });
        var setup = new FirstRunSetup(settings, demo: true, forced: true);

        Assert.True(setup.IsPending);
        setup.Complete(["gog", "xbox"]);

        Assert.Equal(["gog", "xbox"], setup.ChosenLaunchers());
        Assert.Equal(["epic"], Settings().Current.ActiveLaunchers);
    }

    [Fact]
    public void In_the_demo_the_launcher_settings_win_once_they_change_the_list()
    {
        var settings = Settings();
        var setup = new FirstRunSetup(settings, demo: true, forced: true);
        setup.Complete(["gog", "xbox"]);

        settings.Update(s => s with { ActiveLaunchers = ["xbox"] });

        Assert.Equal(["xbox"], setup.ChosenLaunchers());
    }

    [Fact]
    public void Done_twice_keeps_the_first_answer()
    {
        var setup = new FirstRunSetup(Settings(), demo: false, forced: false);
        setup.Complete(["steam"]);

        setup.Complete(["epic"]);

        Assert.Equal(["steam"], setup.ChosenLaunchers());
    }

    [Fact]
    public void A_failed_save_still_ends_the_question_for_this_session()
    {
        // A folder where settings.json belongs: the save fails on every platform, as with a file another program holds.
        Directory.CreateDirectory(SettingsPath);
        var setup = new FirstRunSetup(Settings(), demo: false, forced: false);

        var error = Record.Exception(() => setup.Complete(["steam"]));

        Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        Assert.False(setup.IsPending);
        Assert.Equal(["steam"], setup.ChosenLaunchers());
    }

    [Fact]
    public void Preselects_the_launchers_found_in_registered_order()
    {
        var found = new HashSet<string> { "epic", "steam", "ea" };

        Assert.Equal(["steam", "epic", "ea"], FirstRunSetup.Preselected(["steam", "epic", "gog", "ea", "xbox"], found.Contains));
    }
}
