using System.Globalization;
using Arcadia.Core.Metadata;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam;

namespace Arcadia.Core.Tests.Metadata;

/// <summary>
/// #557: metadata in the UI language: the language rule for descriptions (T557-3), Steam's tags (T557-2), genres by key
/// (T557-5) and the first age rating by region (T557-6, T557-7).
/// </summary>
public sealed class MetadataLanguageTests
{
    private const string German = "Du baust eine Stadt und die Bürger sind froh, wenn der Winter nicht zu lang ist.";
    private const string English = "You build a city and the people are happy with the winter that is not too long.";
    private const string French = "Vous construisez une ville et les habitants sont heureux dans cette ville très calme.";
    private const string Spanish = "Construyes una ciudad y los habitantes están felices con el invierno, pero es muy largo.";
    private const string Italian = "Costruisci una città e gli abitanti sono felici quando l'inverno non è troppo lungo.";
    private const string Japanese = "あなたは街を作り、住民は冬が長すぎないときに幸せです。";
    private const string BrokenGerman = "Du baust eine Stadt in K?ln und die B?rger sind froh, wenn der Winter nicht lang ist.";

    private static ProviderMetadata Provider(string source, string? description, string? language = null) =>
        new(source, MetadataCheck.Checked(new GameMetadata { Description = description }), language);

    private static string? DescriptionOf(string uiLanguage, params ProviderMetadata[] providers) =>
        MergedMetadata.Of(providers, uiLanguage, AgeRatingBoard.Usk).Description?.Value;

    [Theory]
    [Trait("TestCase", "T557-3")]
    [InlineData(German, "de")]
    [InlineData(English, "en")]
    [InlineData(French, "fr")]
    [InlineData(Spanish, "es")]
    [InlineData(Italian, "it")]
    [InlineData(Japanese, null)]
    [InlineData("Roguelike", null)]
    public void Each_ui_language_is_recognised_by_its_own_words(string text, string? expected)
    {
        Assert.Equal(expected, TextLanguage.LanguageOf(text));
    }

    [Theory]
    [Trait("TestCase", "T557-3")]
    [InlineData("de", English, German, German)]
    [InlineData("en", German, English, English)]
    [InlineData("fr", English, French, French)]
    [InlineData("es", Spanish, English, Spanish)]
    [InlineData("fr", Spanish, English, English)]
    [InlineData("de", French, English, English)]
    public void The_ui_language_comes_first_then_english_whatever_the_providers_order(string uiLanguage, string first, string second, string expected)
    {
        Assert.Equal(expected, DescriptionOf(uiLanguage, Provider("Steam", first, uiLanguage), Provider("IGDB", second, "en")));
    }

    [Theory]
    [Trait("TestCase", "T557-3")]
    [InlineData("de", French)]
    [InlineData("en", Spanish)]
    [InlineData("fr", Italian)]
    [InlineData("es", Japanese)]
    [InlineData("de", Japanese)]
    public void A_text_in_neither_the_ui_language_nor_english_is_left_out(string uiLanguage, string text)
    {
        Assert.Null(DescriptionOf(uiLanguage, Provider("Steam", text, uiLanguage)));
    }

    [Fact]
    [Trait("TestCase", "T557-3")]
    public void A_broken_text_counts_only_without_a_whole_one_in_a_fitting_language()
    {
        Assert.Equal(English, DescriptionOf("de", Provider("Epic", BrokenGerman), Provider("IGDB", English, "en")));
        Assert.Equal(BrokenGerman, DescriptionOf("de", Provider("Epic", BrokenGerman)));
        Assert.Null(DescriptionOf("fr", Provider("Epic", BrokenGerman)));
    }

    [Fact]
    [Trait("TestCase", "T557-3")]
    public void A_latin_text_too_short_to_tell_counts_as_the_language_it_was_asked_in()
    {
        Assert.Equal("Roguelike", DescriptionOf("fr", Provider("Steam", "Roguelike", "fr")));
        Assert.Equal("Roguelike", DescriptionOf("fr", Provider("Demo", "Roguelike")));
    }

    [Fact]
    [Trait("TestCase", "T557-4")]
    public void A_text_loaded_in_the_language_before_stays_until_one_in_the_ui_language_or_english_is_there()
    {
        // Steam's answer is from before the change to English, IGDB has none: the German text stays.
        Assert.Equal(German, DescriptionOf("en", Provider("Steam", German, "de")));

        // Once a provider has an English one, it wins; a German text asked in English never counts.
        Assert.Equal(English, DescriptionOf("en", Provider("Steam", German, "de"), Provider("IGDB", English, "en")));
        Assert.Null(DescriptionOf("en", Provider("Steam", German, "en")));
    }

    [Theory]
    [Trait("TestCase", "T557-2")]
    [InlineData("de-DE", "Indie", "Abenteuer")]
    [InlineData("en-US", "Indie", "Adventure")]
    [InlineData("fr-FR", "Indépendant", "Aventure")]
    [InlineData("es-ES", "Indie", "Aventura")]
    [InlineData("it-IT", "Indie", "Adventure")]
    public void Steams_tags_are_named_in_the_ui_language_and_in_english_for_any_other(string uiLanguage, string indie, string adventure)
    {
        Assert.Equal([indie, adventure], SteamTags.NamesOf(["492", "21", "999999"], CultureInfo.GetCultureInfo(uiLanguage)));
    }

    [Fact]
    [Trait("TestCase", "T557-2")]
    public void Every_tag_list_names_the_same_tags()
    {
        var english = IdsOf("english");

        Assert.True(english.Count > 400, $"{english.Count} English tags");
        foreach (var language in SteamLanguage.Known)
        {
            Assert.Equal(english, IdsOf(language));
        }
    }

    private static SortedSet<int> IdsOf(string steamLanguage)
    {
        using var stream = typeof(SteamTags).Assembly.GetManifestResourceStream(SteamTags.ResourceOf(steamLanguage))!;
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        return [.. document.RootElement.EnumerateArray().Select(tag => tag.GetProperty("tagid").GetInt32())];
    }

    [Fact]
    [Trait("TestCase", "T557-5")]
    public void Genres_are_kept_by_key_and_named_in_the_ui_language()
    {
        var before = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Role-playing", GenreNames.Of(Genre.RolePlaying));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("Rollenspiel", GenreNames.Of(Genre.RolePlaying));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Theory]
    [Trait("TestCase", "T557-7")]
    [InlineData("DE", AgeRatingBoard.Usk)]
    [InlineData("AT", AgeRatingBoard.Usk)]
    [InlineData("at", AgeRatingBoard.Usk)]
    [InlineData("US", AgeRatingBoard.Esrb)]
    [InlineData("CA", AgeRatingBoard.Esrb)]
    [InlineData("CH", AgeRatingBoard.Pegi)]
    [InlineData("FR", AgeRatingBoard.Pegi)]
    [InlineData("GB", AgeRatingBoard.Pegi)]
    [InlineData("JP", AgeRatingBoard.Pegi)]
    [InlineData(null, AgeRatingBoard.Pegi)]
    public void The_region_names_the_first_board(string? region, AgeRatingBoard expected)
    {
        Assert.Equal(expected, AgeRatingRegion.FirstBoardOf(region));
    }

    [Theory]
    [Trait("TestCase", "T557-6")]
    [InlineData(AgeRatingBoard.Usk, new[] { AgeRatingBoard.Usk, AgeRatingBoard.Pegi, AgeRatingBoard.Esrb })]
    [InlineData(AgeRatingBoard.Pegi, new[] { AgeRatingBoard.Pegi, AgeRatingBoard.Usk, AgeRatingBoard.Esrb })]
    [InlineData(AgeRatingBoard.Esrb, new[] { AgeRatingBoard.Esrb, AgeRatingBoard.Usk, AgeRatingBoard.Pegi })]
    public void The_regions_rating_comes_first_and_the_others_follow_in_their_order(AgeRatingBoard first, AgeRatingBoard[] expected)
    {
        var merged = MergedMetadata.Of(
            [
                new ProviderMetadata("Steam", new GameMetadata { AgeRatings = [new AgeRating(AgeRatingBoard.Usk, "16")] }),
                new ProviderMetadata("IGDB", new GameMetadata { AgeRatings = [new AgeRating(AgeRatingBoard.Esrb, "M"), new AgeRating(AgeRatingBoard.Pegi, "18")] }),
            ],
            "de",
            first);

        Assert.Equal(expected, merged.AgeRatings.Select(rating => rating.Value.Board));
    }

    [Fact]
    [Trait("TestCase", "T557-6")]
    public void Without_the_regions_board_the_first_rating_is_the_next_in_order()
    {
        var merged = MergedMetadata.Of(
            [new ProviderMetadata("Steam", new GameMetadata { AgeRatings = [new AgeRating(AgeRatingBoard.Pegi, "12"), new AgeRating(AgeRatingBoard.Usk, "12")] })],
            "en",
            AgeRatingBoard.Esrb);

        Assert.Equal([AgeRatingBoard.Usk, AgeRatingBoard.Pegi], merged.AgeRatings.Select(rating => rating.Value.Board));
    }
}
