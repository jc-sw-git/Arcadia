using Arcadia.App.Input;
using Arcadia.App.Keyboard;
using Arcadia.App.ViewModels;
using Arcadia.Core.Launchers;
using Arcadia.Core.UserData;
using Arcadia.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #533: the info box of "Tags" explains own tags and lists the game's, "Titel ändern" speaks of the original title, and a
/// new tag's name says when it is at its most characters.
/// </summary>
public sealed class OwnTagsAndTitleTextsTests
{
    private static readonly TimeSpan PastHoldTime = TimeSpan.FromMilliseconds(1100);

    private static readonly Game Alpha = TestGames.Installed(KnownLaunchers.Gog, "Alpha", new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private static IReadOnlyList<LauncherSpec> Library() => [new LauncherSpec(KnownLaunchers.Gog, 1, Library: [Alpha])];

    [AvaloniaFact]
    [Trait("TestCase", "T533-1")]
    [Trait("TestCase", "T533-2")]
    public async Task The_info_box_of_Tags_explains_them_and_lists_the_games_own_tags()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library());
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        var row = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Tags);
        Assert.Equal(
            "Weise deinen Spielen eigene Tags zu, um besser nach ihnen filtern zu können. Du kannst deine Tags auch unter Einstellungen → Datenquellen verwalten.",
            row.InfoText);
        Assert.Equal((true, false), (row.HasNoInfoTags, row.HasInfoTags));
        Assert.Same(row, arcadia.Detail!.Current.InfoRow);

        // Two tags made in the window: the info box lists them alphabetically as soon as it closes.
        arcadia.Press(UiAction.Accept);
        var window = arcadia.Main.TagWindow!;
        foreach (var name in new[] { "Zu zweit", "Couch-Koop" })
        {
            arcadia.Press(UiAction.Up).Press(UiAction.Accept);
            window.Editor!.Type(name);
            arcadia.Press(UiAction.Settings);
        }

        arcadia.Press(UiAction.Back);
        row = arcadia.Detail.Current.Row(GameActionKind.Tags);
        Assert.Equal(["Couch-Koop", "Zu zweit"], row.InfoTags);
        Assert.Equal((false, true), (row.HasNoInfoTags, row.HasInfoTags));

        // Rows that aren't about tags list none.
        Assert.Null(arcadia.Detail.Current.Row(GameActionKind.ChangeTitle).InfoTags);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T533-3")]
    public async Task Titel_aendern_speaks_of_the_original_title()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library());
        arcadia.Hold(UiAction.Accept, PastHoldTime);

        var change = arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.ChangeTitle);
        Assert.EndsWith("Ein leerer Titel stellt den ursprünglichen wieder her.", change.InfoText);
        Assert.Equal("Zurück zum ursprünglichen Titel", arcadia.Detail!.Current.Row(GameActionKind.ResetTitle).Subtitle);

        arcadia.Press(UiAction.Accept);
        Assert.Equal("Leer lassen oder „Alpha“ eingeben stellt den ursprünglichen Titel wieder her.", arcadia.Main.ChangeTitle!.Note);
        Assert.DoesNotContain("automatischen", AppTexts.DataSourcesUserDataInfo);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T533-4")]
    public async Task A_new_tags_name_at_its_most_characters_says_so_and_can_still_be_taken()
    {
        using var arcadia = await ArcadiaDriver.StartAsync(Library());
        arcadia.Hold(UiAction.Accept, PastHoldTime);
        arcadia.FocusDetailRow(DetailAreaViewModel.ManageKey, GameActionKind.Tags);
        arcadia.Press(UiAction.Accept);
        var window = arcadia.Main.TagWindow!;
        arcadia.Press(UiAction.Accept);
        var editor = window.Editor!;

        // One below the limit: no notice; the last character from the on-screen keyboard reaches it.
        editor.Type(new string('a', UserDataStore.MaxTagLength - 1));
        Assert.Equal((false, null), (editor.HasLimitNotice, editor.LimitNotice));
        ((IKeyboardTarget)editor).Type('b');
        Assert.True(editor.HasLimitNotice);
        Assert.Equal("Maximale Länge erreicht: höchstens 40 Zeichen", editor.LimitNotice);

        // More from the real keyboard is refused, the notice stays; erasing one takes it away, typing one brings it back.
        editor.Type("cd");
        Assert.Equal(UserDataStore.MaxTagLength, editor.Text.Length);
        Assert.True(editor.HasLimitNotice);
        editor.HandleKey(Avalonia.Input.Key.Back);
        Assert.False(editor.HasLimitNotice);
        editor.Type("e");
        Assert.True(editor.HasLimitNotice);

        // A notice, not a problem: ≡ takes the full name.
        Assert.False(editor.HasProblem);
        var name = editor.Text;
        arcadia.Press(UiAction.Settings);
        Assert.Null(window.Editor);
        Assert.Contains(window.Entries, entry => entry.Name == name && entry.IsTicked);
    }
}
