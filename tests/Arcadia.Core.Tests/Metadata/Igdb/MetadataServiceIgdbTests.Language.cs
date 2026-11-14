using System.Globalization;
using Arcadia.Core.Metadata;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Tests.Metadata.Igdb;

/// <summary>
/// #585: IGDB's descriptions in every UI language, through the whole run (store, IGDB, cache, restart): the UI language
/// first, else IGDB's English, never another language, and after a change of "Sprache" like Steam's.
/// </summary>
public sealed partial class MetadataServiceIgdbTests
{
    private const string GermanText = "Du baust eine Stadt und die Bürger sind froh, wenn der Winter nicht zu lang ist.";
    private const string EnglishText = "You build a city and the people are happy with the winter that is not too long.";
    private const string FrenchText = "Vous construisez une ville et les habitants sont heureux dans cette ville très calme.";
    private const string SpanishText = "Construyes una ciudad y los habitantes están felices con el invierno, pero es muy largo.";

    private static string Key100 => Game.KeyOf(Steamish.Id, "100");

    private static string TextIn(string language) => language switch
    {
        "de" => GermanText,
        "fr" => FrenchText,
        "es" => SpanishText,
        _ => EnglishText,
    };

    private void RestartIn(string culture)
    {
        _service.Dispose();
        _language = CultureInfo.GetCultureInfo(culture);
        _service = NewService();
        _service.Open();
    }

    private async Task ScanSteamGameAsync(string? storeText, string igdbText)
    {
        await GrantAccessAsync();
        _store.Serve("100", storeText ?? "-");
        _igdb.ServeSteam("100", FakeIgdb.Game(1, "Nordwind", extra: Summary(igdbText)));
        Scan(steam: [GameOf(Steamish, "100", "Nordwind")]);
        await RunToIdleAsync();
    }

    [Theory]
    [Trait("TestCase", "T585-1")]
    [InlineData("de-DE", false)]
    [InlineData("en-US", false)]
    [InlineData("fr-FR", false)]
    [InlineData("es-ES", false)]
    [InlineData("de-DE", true)]
    [InlineData("fr-FR", true)]
    [InlineData("es-ES", true)]
    public async Task A_description_in_the_ui_language_beats_igdbs_english_in_either_order(string culture, bool igdbFirst)
    {
        RestartIn(culture);
        var language = _language.TwoLetterISOLanguageName;
        if (igdbFirst)
        {
            _settings = _settings with { Order = [MetadataSourceKind.Igdb, MetadataSourceKind.Launcher] };
        }

        await ScanSteamGameAsync(TextIn(language), EnglishText);

        Assert.EndsWith("?l=" + language, _store.Requests[0].Request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        var description = _service.Of(Key100).Description!;
        Assert.Equal(TextIn(language), description.Value);
        Assert.Equal(language == "en" && igdbFirst ? "IGDB" : "Made-up Steam", description.Source);
    }

    [Theory]
    [Trait("TestCase", "T585-1")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    public async Task Without_one_in_the_ui_language_igdbs_english_description_shows(string culture)
    {
        RestartIn(culture);

        await ScanSteamGameAsync(null, EnglishText);

        Assert.Equal((EnglishText, "IGDB"), (_service.Of(Key100).Description!.Value, _service.Of(Key100).Description!.Source));
    }

    [Theory]
    [Trait("TestCase", "T585-1")]
    [InlineData("fr-FR", GermanText)]
    [InlineData("es-ES", GermanText)]
    [InlineData("en-US", FrenchText)]
    [InlineData("de-DE", SpanishText)]
    public async Task An_igdb_text_in_neither_the_ui_language_nor_english_is_never_shown(string culture, string igdbText)
    {
        RestartIn(culture);

        await ScanSteamGameAsync(null, igdbText);

        Assert.Null(_service.Of(Key100).Description);
    }

    [Theory]
    [Trait("TestCase", "T585-2")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    [InlineData("en-US")]
    public async Task After_a_change_of_the_language_igdbs_english_beats_a_stale_german_text_until_a_reload_brings_the_new_one(string culture)
    {
        await ScanSteamGameAsync(GermanText, EnglishText);
        Assert.Equal(GermanText, _service.Of(Key100).Description!.Value);
        var asked = _store.RequestCount + _igdb.ApiRequests;

        // Arcadia restarts in the new language: nothing is asked; IGDB's English, which fits, replaces the German text.
        RestartIn(culture);
        Scan(steam: [GameOf(Steamish, "100", "Nordwind")]);
        await RunToIdleAsync();
        Assert.Equal(asked, _store.RequestCount + _igdb.ApiRequests);
        Assert.Equal((EnglishText, "IGDB"), (_service.Of(Key100).Description!.Value, _service.Of(Key100).Description!.Source));

        // "Alle Metadaten neu laden" asks the store in the new language, and its text comes first again.
        var language = _language.TwoLetterISOLanguageName;
        _store.Serve("100", TextIn(language));
        Assert.True(_service.ReloadAll());
        await RunToIdleAsync();
        Assert.EndsWith("?l=" + language, _store.Requests[^1].Request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(TextIn(language), _service.Of(Key100).Description!.Value);
    }
}
