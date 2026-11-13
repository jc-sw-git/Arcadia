using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Core.Settings;
using Arcadia.Core.Tests.Metadata;
using Arcadia.Core.Tests.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>
/// #494, scenario 12: the IGDB access set up with the controller alone, from the library to the order of the metadata
/// sources and back, every step a button press (D-pad, A, B, L3, ≡); the client id and secret typed key by key on the
/// on-screen keyboard. Made-up launcher, values and fake Twitch/IGDB; no window.
/// </summary>
public sealed class IgdbControllerFlowTests
{
    private static readonly LauncherInfo Shop = new("shop", "Shop", "shop", null);

    [AvaloniaFact]
    [Trait("TestCase", "T494-31")]
    public async Task The_igdb_access_and_the_order_are_set_up_by_controller_alone_and_b_goes_back()
    {
        var game = new Game { Id = "k1", LauncherId = Shop.Id, Title = "K?ln", Launch = new LaunchTarget.ProtocolUri("shop://run/k1") };
        using var arcadia = await ArcadiaDriver.StartAsync(
            [LauncherSpec.With(Shop, [game]) with { Metadata = new FakeMetadataSource { Store = null } }],
            new AppSettings { PanEnabled = true, ActiveLaunchers = [Shop.Id] });
        arcadia.Igdb.ServeName("K ln", FakeIgdb.Game(1, "Köln"));

        // Library → settings → "Datenquellen" with the shoulder buttons.
        arcadia.Press(UiAction.Settings);
        for (var i = 0; i < 6 && arcadia.Main.Settings!.Selected is not DataSourcesSettingsViewModel; i++)
        {
            arcadia.Press(UiAction.NextTab);
        }

        Assert.IsType<DataSourcesSettingsViewModel>(arcadia.Main.Settings!.Selected);

        await TypeOnKeyboardAsync(arcadia, IgdbAccessRows.ClientIdRowKey, FakeIgdb.ClientId);
        await TypeOnKeyboardAsync(arcadia, IgdbAccessRows.SecretRowKey, FakeIgdb.Secret);
        Assert.Equal(AppTexts.DataSourcesKeyConnected, Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle);

        // "Verbindung testen" with A.
        var tokens = arcadia.Igdb.TokenRequests;
        arcadia.FocusRow("igdb-test");
        arcadia.Press(UiAction.Accept);
        await arcadia.WaitUntilAsync(() => Row(arcadia, "igdb-test").Subtitle == AppTexts.DataSourcesIgdbTestOk(arcadia.Time.GetLocalNow()), "Verbindung getestet");
        Assert.Equal(tokens + 1, arcadia.Igdb.TokenRequests);

        // IGDB lifted to first with L3 and ▲, dropped, then lifted back down.
        arcadia.FocusRow(MetadataSourceRows.SourceKey(MetadataSourceKind.Igdb));
        arcadia.Press(UiAction.ViewOptions).Press(UiAction.Up).Press(UiAction.ViewOptions);
        Assert.Equal([MetadataSourceKind.Igdb, MetadataSourceKind.Launcher], arcadia.Settings.Current.Metadata.Order);
        arcadia.Press(UiAction.ViewOptions).Press(UiAction.Down).Press(UiAction.ViewOptions);
        Assert.Equal(MetadataSettings.DefaultOrder, arcadia.Settings.Current.Metadata.Order);

        // B leaves the settings for the library.
        arcadia.Press(UiAction.Back);
        Assert.False(arcadia.Main.IsSettingsOpen);
    }

    private static SettingsRowViewModel Row(ArcadiaDriver arcadia, string key) => arcadia.Main.Settings!.Selected.Rows.Single(row => row.Key == key);

    /// <summary>A on the row, the value typed key by key with the D-pad and A on the on-screen keyboard, ≡ takes it.</summary>
    private static async Task TypeOnKeyboardAsync(ArcadiaDriver arcadia, string rowKey, string value)
    {
        arcadia.FocusRow(rowKey);
        arcadia.Press(UiAction.Accept);
        var editor = arcadia.Main.Settings!.Selected.Editor;
        Assert.NotNull(editor);
        var keyboard = editor.Keyboard;
        foreach (var character in value)
        {
            var target = keyboard.Keys.Where(k => !k.IsSkipped && k.IsCharacter && k.Label == character.ToString()).OrderBy(k => k.Key.Left).First();
            foreach (var direction in OnScreenKeys.PathTo(keyboard, target))
            {
                arcadia.Press(direction);
            }

            Assert.Same(target, keyboard.FocusedKey);
            arcadia.Press(UiAction.Accept);
        }

        Assert.Equal(value, editor.Text);
        arcadia.Press(UiAction.Settings);
        await arcadia.WaitUntilAsync(() => Row(arcadia, IgdbAccessRows.ClientIdRowKey).Subtitle != AppTexts.DataSourcesKeyChecking, "Zugang geprüft");
        Dispatcher.UIThread.RunJobs();
    }
}
