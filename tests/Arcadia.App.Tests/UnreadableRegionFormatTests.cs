using System.Globalization;
using Arcadia.App.Services;
using Arcadia.Core.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// #641: a regional time or date format .NET can't format (e.g. one made in Windows' region settings) shows the invariant
/// format instead of ending Arcadia at start; the header and every other time and date keep working in tricky regions.
/// </summary>
[Collection(LocalizationSettingsTests.Name)]
public sealed class UnreadableRegionFormatTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 21, 58, 0, TimeSpan.Zero);

    public UnreadableRegionFormatTests() => Formatting.ClockFormat = ClockFormat.System;

    public void Dispose()
    {
        Formatting.ClockFormat = ClockFormat.System;
        Formatting.UseRegion(TestSystem.GermanRegion);
    }

    public static TheoryData<string> TrickyRegions() => ["es-ES", "pt-BR", "ar-SA", "th-TH", "fa-IR", "ja-JP", "de-CH"];

    [Theory]
    [Trait("TestCase", "T641-1")]
    [InlineData("HH:mm 'x")]
    [InlineData("HH:mm \"x")]
    [InlineData(@"HH:mm \")]
    public void A_broken_time_pattern_gives_a_time_instead_of_an_error(string timePattern)
    {
        Formatting.UseRegion(WithBrokenPatterns(timePattern, shortDate: "dd/MM/yyyy"));

        foreach (var clock in Enum.GetValues<ClockFormat>())
        {
            Formatting.ClockFormat = clock;
            Assert.False(string.IsNullOrWhiteSpace(Formatting.Clock(Now)));
            Assert.False(string.IsNullOrWhiteSpace(Formatting.ClockWithUnit(Now)));
            Assert.False(string.IsNullOrWhiteSpace(Formatting.TimeOfDay(Now, Now)));
        }
    }

    [Theory]
    [Trait("TestCase", "T641-1")]
    [InlineData("'dd/MM/yyyy")]
    [InlineData("dd/MM/yyyy \"")]
    [InlineData(@"dd/MM/yyyy \")]
    public void A_broken_short_date_pattern_gives_a_date_instead_of_an_error(string shortDate)
    {
        Formatting.UseRegion(WithBrokenPatterns("HH:mm", shortDate));

        Assert.Equal("2026-11-14", Formatting.Date(Now));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.Date(new DateOnly(2026, 11, 14))));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.DateAndTime(Now)));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.ShortDate(Now)));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.LongDate(Now)));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.TimeOfDay(Now.AddDays(-9), Now)));
    }

    [AvaloniaFact]
    [Trait("TestCase", "T641-2")]
    public async Task Arcadia_starts_with_a_broken_time_and_date_pattern_and_shows_the_header()
    {
        Formatting.UseRegion(WithBrokenPatterns("HH:mm 'x", "'dd/MM/yyyy"));

        using var arcadia = await ArcadiaDriver.StartAsync();

        Assert.False(string.IsNullOrWhiteSpace(arcadia.Main.Status.Clock));
        Assert.False(string.IsNullOrWhiteSpace(arcadia.Main.Status.Date));
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T641-2")]
    [MemberData(nameof(TrickyRegions))]
    public async Task Arcadia_starts_in_a_tricky_region_with_every_clock_setting(string region)
    {
        Formatting.UseRegion(CultureInfo.GetCultureInfo(region));

        foreach (var clock in Enum.GetValues<ClockFormat>())
        {
            Formatting.ClockFormat = clock;
            using var arcadia = await ArcadiaDriver.StartAsync();

            Assert.False(string.IsNullOrWhiteSpace(arcadia.Main.Status.Clock));
            Assert.False(string.IsNullOrWhiteSpace(arcadia.Main.Status.Date));
        }
    }

    [AvaloniaFact]
    [Trait("TestCase", "T641-3")]
    public void An_error_at_start_shows_a_window_with_the_path_of_the_log_and_it_closes()
    {
        var window = StartupFailure.Build(@"C:\Users\someone\AppData\Roaming\Arcadia\logs\arcadia.log");
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.Show();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains(AppTexts.StartupFailed, texts);
        Assert.Contains(@"C:\Users\someone\AppData\Roaming\Arcadia\logs\arcadia.log", texts);
        window.GetVisualDescendants().OfType<Button>().Single().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

        Assert.True(closed);
    }

    private static CultureInfo WithBrokenPatterns(string timePattern, string shortDate)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("es-ES").Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        culture.DateTimeFormat.ShortTimePattern = timePattern;
        culture.DateTimeFormat.ShortDatePattern = shortDate;
        return culture;
    }
}
