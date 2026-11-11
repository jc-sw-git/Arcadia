using Arcadia.App.Input;
using Arcadia.App.Settings;
using Arcadia.Core.Settings;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>A launcher's own logo and its folder picked by hand, through the system's dialogs (#138).</summary>
public sealed class LauncherLogoAndFolderTests
{
    private const string Picture = """<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 24 24"><rect width="24" height="24" fill="#3a7"/></svg>""";

    private static readonly AppSettings GogAndAmazon = new() { ActiveLaunchers = ["gog", "amazon"] };

    [AvaloniaFact]
    [Trait("TestCase", "T138-2")]
    public async Task An_uploaded_logo_replaces_the_launchers_own_and_restoring_it_deletes_the_copy_with_the_focus_back_on_the_logo()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var dialogs = UseDialogs(arcadia);
        var settings = arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);
        Assert.Equal("gog", settings.PageLauncherId);
        Assert.DoesNotContain("launcher-logo-default", Keys(settings));

        arcadia.FocusRow("launcher-logo");
        arcadia.Press(UiAction.Accept);
        Assert.Equal(["Logo für GOG Galaxy wählen"], dialogs.Opened);
        dialogs.Answer(PictureFile(arcadia, "logo.svg", Picture));
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.PreferencesOf("gog").LogoFile is not null, "das eigene Logo");

        var copy = Path.Combine(arcadia.Folder, "logos", arcadia.Settings.Current.PreferencesOf("gog").LogoFile!);
        Assert.True(File.Exists(copy));
        Assert.Contains("launcher-logo-default", Keys(settings));
        Assert.Equal(AppTexts.LauncherLogoOwnFitted, settings.Rows.Single(row => row.Key == "launcher-logo").Subtitle);
        Assert.NotNull(arcadia.Main.LauncherTabs.Items.Single(tab => tab.Name == "GOG Galaxy").Badge.Picture);

        arcadia.FocusRow("launcher-logo-default");
        arcadia.Press(UiAction.Accept);
        Assert.Null(arcadia.Settings.Current.PreferencesOf("gog").LogoFile);
        Assert.False(File.Exists(copy));
        Assert.DoesNotContain("launcher-logo-default", Keys(settings));
        Assert.Equal("launcher-logo", settings.FocusedRow?.Key);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T138-2")]
    public async Task A_file_that_is_no_picture_keeps_the_logo_and_says_why_and_a_cancelled_dialog_changes_nothing()
    {
        using var arcadia = await ArcadiaDriver.StartAsync();
        var dialogs = UseDialogs(arcadia);
        arcadia.OpenLauncherSettings();
        arcadia.Press(UiAction.Accept);

        arcadia.FocusRow("launcher-logo");
        arcadia.Press(UiAction.Accept);
        dialogs.Answer(null);
        LetTheDialogClose();
        Assert.Null(arcadia.Main.Toast.Text);

        arcadia.Press(UiAction.Accept);
        dialogs.Answer(PictureFile(arcadia, "logo.png", "no picture"));
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text is not null, "der Toast zur abgelehnten Datei");
        Assert.StartsWith("Logo nicht übernommen: ", arcadia.Main.Toast.Text, StringComparison.Ordinal);
        Assert.Null(arcadia.Settings.Current.PreferencesOf("gog").LogoFile);
        Assert.Equal(2, dialogs.Opened.Count);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T138-2")]
    [Trait("TestCase", "T138-3")]
    public async Task While_a_dialog_is_open_a_second_A_on_the_logo_or_the_folder_opens_no_other()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(NotFound(out _), GogAndAmazon);
        var dialogs = UseDialogs(arcadia);
        var settings = arcadia.OpenLauncherSettings();
        OpenPageOfXbox(settings);

        arcadia.FocusRow("launcher-logo");
        arcadia.Press(UiAction.Accept).Press(UiAction.Accept);
        arcadia.FocusRow("launcher-folder");
        arcadia.Press(UiAction.Accept);
        Assert.Equal(["Logo für Xbox wählen"], dialogs.Opened);

        // Once it is closed, A opens a dialog again.
        dialogs.Answer(null);
        LetTheDialogClose();
        arcadia.Press(UiAction.Accept);
        Assert.Equal(["Logo für Xbox wählen", "Ordner von Xbox wählen"], dialogs.Opened);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T138-3")]
    public async Task A_folder_without_the_launcher_is_not_taken_and_the_toast_says_what_Arcadia_looks_for()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(NotFound(out _), GogAndAmazon);
        var dialogs = UseDialogs(arcadia);
        var settings = arcadia.OpenLauncherSettings();
        OpenPageOfXbox(settings);

        arcadia.FocusRow("launcher-folder");
        arcadia.Press(UiAction.Accept);
        dialogs.Answer(Path.Combine(arcadia.Folder, "elsewhere"));
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text is not null, "der Toast zum falschen Ordner");

        Assert.Equal("Dort liegt kein Xbox: Arcadia sucht test.exe.", arcadia.Main.Toast.Text);
        Assert.Null(arcadia.Settings.Current.PreferencesOf("xbox").Folder);
        Assert.Equal(["gog", "amazon"], arcadia.Settings.Current.ActiveLaunchers);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T138-3")]
    public async Task The_launchers_folder_switches_an_inactive_launcher_on_at_the_end_of_the_active_ones()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(NotFound(out var folder), GogAndAmazon);
        var dialogs = UseDialogs(arcadia);
        var settings = arcadia.OpenLauncherSettings();
        OpenPageOfXbox(settings);

        arcadia.FocusRow("launcher-folder");
        arcadia.Press(UiAction.Accept);
        dialogs.Answer(folder);
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.ActiveLaunchers?.Contains("xbox") == true, "Xbox eingeschaltet");

        Assert.Equal(folder, arcadia.Settings.Current.PreferencesOf("xbox").Folder);
        Assert.Equal(["gog", "amazon", "xbox"], arcadia.Settings.Current.ActiveLaunchers);
        await arcadia.WaitUntilAsync(() => arcadia.TabNames().Contains("Xbox"), "der Tab von Xbox");
        Assert.Equal(["GOG Galaxy", "Amazon Games", "Xbox"], arcadia.TabNames());
    }

    [AvaloniaFact]
    [Trait("TestCase", "T138-3")]
    public async Task The_folder_of_an_active_launcher_not_found_is_taken_and_the_launcher_read_again()
    {
        var specs = NotFound(out var folder);
        using var arcadia = await ArcadiaDriver.StartAsync(specs, new AppSettings { ActiveLaunchers = ["gog", "xbox"] });
        var dialogs = UseDialogs(arcadia);
        var settings = arcadia.OpenLauncherSettings();
        OpenPageOfXbox(settings);

        arcadia.FocusRow("launcher-folder");
        arcadia.Press(UiAction.Accept);
        dialogs.Answer(folder);
        await arcadia.WaitUntilAsync(() => arcadia.Settings.Current.PreferencesOf("xbox").Folder == folder, "der gewählte Ordner");
        await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == "3 Spiele in Xbox gefunden.", "der Toast nach dem Einlesen");

        Assert.Equal(["gog", "xbox"], arcadia.Settings.Current.ActiveLaunchers);
    }

    /// <summary>GOG and Amazon active; Xbox not installed, but found in <paramref name="folder"/> once it is picked.</summary>
    private static IReadOnlyList<LauncherSpec> NotFound(out string folder)
    {
        folder = Path.Combine(Path.GetTempPath(), "arcadia-test-xbox");
        return
        [
            new(KnownLaunchers.Gog, 2),
            new(KnownLaunchers.Xbox, 3, IsInstalled: false, FolderChoice: new TestFolderChoice(folder)),
            new(KnownLaunchers.Amazon, 1),
        ];
    }

    /// <summary>Xbox's page, with "Ordner" since Arcadia doesn't find it.</summary>
    private static void OpenPageOfXbox(LauncherSettingsViewModel settings)
    {
        settings.ShowPage("xbox", "launcher-name");
        Assert.Equal("xbox", settings.PageLauncherId);
        Assert.Contains("launcher-folder", Keys(settings));
    }

    /// <summary>What Arcadia does once the dialog answers runs on the UI thread, after the answer.</summary>
    private static void LetTheDialogClose() => Dispatcher.UIThread.RunJobs();

    private static FakeSystemDialogs UseDialogs(ArcadiaDriver arcadia)
    {
        var dialogs = new FakeSystemDialogs();
        arcadia.Main.Dialogs = dialogs;
        return dialogs;
    }

    private static string PictureFile(ArcadiaDriver arcadia, string name, string content)
    {
        var path = Path.Combine(arcadia.Folder, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static IReadOnlyList<string> Keys(LauncherSettingsViewModel settings) => [.. settings.Rows.Select(row => row.Key)];
}
