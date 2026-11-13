using System.Globalization;
using Arcadia.Core.Localization;

namespace Arcadia.Core.Tests.Localization;

/// <summary>#554: which UI language a choice in the row "Sprache" gives, and when two of them count as the same.</summary>
public sealed class UiLanguagesTests
{
    [Theory]
    [Trait("TestCase", "T554-2")]
    [Trait("TestCase", "T554-3")]
    [InlineData("system", "de-DE", "de-DE")]
    [InlineData("system", "de-AT", "de-AT")]
    [InlineData("system", "en-US", "en-US")]
    [InlineData("system", "en-GB", "en-GB")]
    [InlineData("system", "it-IT", "en")]
    [InlineData("system", "", "en")]
    [InlineData("en", "de-DE", "en")]
    [InlineData("de", "it-IT", "de")]
    public void A_choice_gives_its_language_and_the_system_one_falls_back_to_English(string choice, string system, string expected) =>
        Assert.Equal(expected, UiLanguages.Resolve(choice, CultureInfo.GetCultureInfo(system)).Name);

    [Theory]
    [Trait("TestCase", "T554-13")]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("EN")]
    [InlineData("fr")]
    public void An_unknown_choice_counts_as_the_system_language(string? choice)
    {
        Assert.False(UiLanguages.IsChoice(choice));
        Assert.Equal("de-DE", UiLanguages.Resolve(choice, CultureInfo.GetCultureInfo("de-DE")).Name);
    }

    [Theory]
    [Trait("TestCase", "T554-6")]
    [InlineData("de-DE", "de", true)]
    [InlineData("en-US", "en", true)]
    [InlineData("de-DE", "en", false)]
    public void Two_cultures_of_one_language_count_as_the_same(string inUse, string chosen, bool same) =>
        Assert.Equal(same, UiLanguages.SameLanguage(CultureInfo.GetCultureInfo(inUse), CultureInfo.GetCultureInfo(chosen)));

    [Fact]
    [Trait("TestCase", "T554-1")]
    public void The_row_offers_the_system_language_English_and_German_each_named_in_its_own_language()
    {
        Assert.Equal(["system", "en", "de"], UiLanguages.Choices);
        Assert.Equal(["English", "Deutsch"], UiLanguages.Choices.Skip(1).Select(UiLanguages.OwnName));
    }
}
