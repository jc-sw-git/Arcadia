using System.Collections.Concurrent;
using System.Globalization;
using System.Resources;
using Arcadia.Core.Tests.Texts;
using Arcadia.Launchers.Abstractions.Texts;

namespace Arcadia.Core.Tests;

/// <summary>
/// The fill helper every UI text goes through (#480): a broken text shows what is missing instead of ending Arcadia,
/// and is logged once with its key, never with the values. <see cref="UiText.Log"/> is static, so the class runs alone.
/// </summary>
[Collection(UiTextTests.Name)]
public sealed class UiTextTests : IDisposable
{
    public const string Name = "UiText.Log";

    private readonly ConcurrentQueue<string> _logged = new();

    private readonly Action<string>? _previousLog = UiText.Log;

    public UiTextTests() => UiText.Log = _logged.Enqueue;

    public void Dispose() => UiText.Log = _previousLog;

    // Keys are unique per test: a broken text is logged once per run of Arcadia, here once per test process.
    private static FakeTexts Texts(string key, string text) => new(new Dictionary<string, string> { [key] = text });

    [Fact]
    [Trait("TestCase", "T480-1")]
    public void A_text_filled_with_all_its_values_shows_them_and_logs_nothing()
    {
        Assert.Equal("„Hollow Crown“ wird in Steam geöffnet …", FallbackTestTexts.GameOpening("Hollow Crown", "Steam"));
        Assert.Empty(_logged);
    }

    [Fact]
    [Trait("TestCase", "T480-2")]
    public void A_placeholder_without_a_value_stays_visible_and_is_logged_with_the_key()
    {
        var texts = Texts("T2Opening", "„{game}“ wird in {launcher} geöffnet …");

        var shown = UiText.Fill(texts, "T2Opening", ("launcher", "Steam"));

        Assert.Equal("„{game}“ wird in Steam geöffnet …", shown);
        Assert.Equal("UI-Text T2Opening: kein Wert für {game}", Assert.Single(_logged));
    }

    [Fact]
    [Trait("TestCase", "T480-3")]
    public void A_missing_text_shows_its_key_and_is_logged()
    {
        Assert.Equal("T3Missing", UiText.Get(FallbackTestTexts.Resources, "T3Missing"));
        Assert.Equal("T3Missing", UiText.Fill(FallbackTestTexts.Resources, "T3Missing", ("game", "Hollow Crown")));

        Assert.Equal("UI-Text fehlt: T3Missing", Assert.Single(_logged));
    }

    [Fact]
    [Trait("TestCase", "T480-4")]
    public async Task A_broken_text_is_logged_once_however_often_and_from_however_many_threads_it_is_filled()
    {
        var texts = new FakeTexts(new Dictionary<string, string> { ["T4First"] = "{game} startet", ["T4Second"] = "{launcher} öffnet" });

        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                UiText.Fill(texts, "T4First");
            }
        })));
        UiText.Fill(texts, "T4Second");

        Assert.Equal(["UI-Text T4First: kein Wert für {game}", "UI-Text T4Second: kein Wert für {launcher}"], _logged);
    }

    [Theory]
    [Trait("TestCase", "T480-5")]
    [InlineData("{launcher} Edition")]
    [InlineData("Foo {count}")]
    [InlineData("{{game}}")]
    public void A_value_with_braces_is_shown_as_it_is(string title)
    {
        Assert.Equal($"„{title}“ wird in Steam geöffnet …", FallbackTestTexts.GameOpening(title, "Steam"));
        Assert.Empty(_logged);
    }

    [Fact]
    [Trait("TestCase", "T480-6")]
    public void A_value_the_text_does_not_use_is_ignored()
    {
        var texts = Texts("T6Started", "{game} läuft");

        Assert.Equal("Hollow Crown läuft", UiText.Fill(texts, "T6Started", ("game", "Hollow Crown"), ("launcher", "Steam")));
        Assert.Empty(_logged);
    }

    [Fact]
    [Trait("TestCase", "T480-7")]
    public void The_log_line_holds_the_key_and_the_placeholder_but_none_of_the_values()
    {
        var texts = Texts("T7Path", "{game} liegt in {path} von {account}");

        UiText.Fill(texts, "T7Path", ("game", "Neon Drift"), ("account", "player-one"));
        UiText.Get(texts, "T7Missing");

        Assert.Equal(["UI-Text T7Path: kein Wert für {path}", "UI-Text fehlt: T7Missing"], _logged);
        Assert.DoesNotContain(_logged, line => line.Contains("Neon Drift", StringComparison.Ordinal) || line.Contains("player-one", StringComparison.Ordinal));
    }

    [Fact]
    public void A_failing_log_does_not_break_the_text()
    {
        UiText.Log = _ => throw new IOException("log file locked");

        Assert.Equal("{game} fehlt", UiText.Fill(Texts("LogFails", "{game} fehlt"), "LogFails"));
    }

    [Fact]
    public void A_resource_manager_that_throws_counts_as_a_missing_text()
    {
        Assert.Equal("Thrown", UiText.Get(new ThrowingTexts(), "Thrown"));
        Assert.Equal("UI-Text fehlt: Thrown", Assert.Single(_logged));
    }

    [Theory]
    [Trait("TestCase", "T480-10")]
    [InlineData("de-DE", "12,5 GB · 1.234 Dateien · seit 09:05 Uhr")]
    [InlineData("en-US", "12.5 GB · 1,234 Dateien · seit 09:05 Uhr")]
    public void Numbers_and_times_are_formatted_as_string_interpolation_does(string culture, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var (size, count, time) = (12.5, 1234, new DateTime(2026, 1, 2, 9, 5, 0, DateTimeKind.Local));
            Assert.Equal($"{size:0.0} GB · {count:N0} Dateien · seit {time:HH:mm} Uhr", FallbackTestTexts.Size(size, count, time));
            Assert.Equal(expected, FallbackTestTexts.Size(size, count, time));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [Trait("TestCase", "T480-11")]
    [InlineData("en-US")]
    [InlineData("fr-FR")]
    [InlineData("")]
    public void Every_ui_culture_gets_the_German_texts(string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            Assert.Equal("Abbrechen", FallbackTestTexts.Plain);
            Assert.Equal("Steam-Konto", Arcadia.Launchers.Steam.SteamTexts.SteamAccountTitle);
            Assert.Equal("Der Name darf nicht leer sein.", Arcadia.Core.CoreTexts.NameEmpty);
            Assert.Empty(_logged);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private sealed class FakeTexts(Dictionary<string, string> texts) : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => texts.GetValueOrDefault(name);
    }

    private sealed class ThrowingTexts : ResourceManager
    {
        public override string? GetString(string name, CultureInfo? culture) => throw new MissingManifestResourceException("no resources");
    }
}

[CollectionDefinition(UiTextTests.Name, DisableParallelization = true)]
public sealed class UiTextCollection;
