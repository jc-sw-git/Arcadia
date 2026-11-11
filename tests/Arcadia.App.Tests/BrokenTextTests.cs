using System.Globalization;
using System.Resources;
using Arcadia.App.Input;
using Arcadia.Launchers;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #480: a text broken by a later change (here a translation that renamed a placeholder) shows what is missing, and
/// the page around it keeps working, without a toast. The texts are swapped for the whole App, so the class runs alone.
/// </summary>
[Collection(BrokenTextTests.Name)]
public sealed class BrokenTextTests
{
    public const string Name = "Broken UI texts";

    [AvaloniaFact]
    [Trait("TestCase", "T480-8")]
    public async Task A_text_without_its_value_shows_the_placeholder_and_the_page_keeps_working()
    {
        var texts = AppTexts.Resources;
        AppTexts.Resources = new OneTextReplaced(texts, nameof(AppTexts.EmptyTabNoGamesTitle), "Keine Spiele in {launcher}");
        try
        {
            IReadOnlyList<LauncherSpec> launchers = [new(KnownLaunchers.Gog, 31), new(KnownLaunchers.Xbox, 0)];
            using var arcadia = await ArcadiaDriver.StartAsync(launchers);

            arcadia.Press(UiAction.NextTab);
            var empty = arcadia.Main.Library.EmptyTab!;
            Assert.Equal("Keine Spiele in {launcher}", empty.Title);
            Assert.Null(arcadia.Main.Toast.Text);
            Assert.Contains("UI-Text EmptyTabNoGamesTitle: kein Wert für {launcher}", ArcadiaDriver.Log, StringComparison.Ordinal);

            arcadia.Press(UiAction.Right).Press(UiAction.Accept);
            Assert.True(empty.IsRescanFocused);
            await arcadia.WaitUntilAsync(() => arcadia.Main.Toast.Text == AppTexts.ToastStillNoGames("Xbox"), "der Toast nach dem Einlesen");
        }
        finally
        {
            AppTexts.Resources = texts;
        }
    }

    private sealed class OneTextReplaced(ResourceManager texts, string key, string text) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => name == key ? text : texts.GetString(name, culture);
    }
}

[CollectionDefinition(BrokenTextTests.Name, DisableParallelization = true)]
public sealed class BrokenTextCollection;
