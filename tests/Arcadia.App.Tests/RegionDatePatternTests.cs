using System.Globalization;
using Arcadia.App.Services;
using Avalonia.Headless.XUnit;

namespace Arcadia.App.Tests;

/// <summary>
/// #592: dates cut from the region's patterns (the header date, a running game's day, a news entry's date) work in every
/// region .NET knows and in the patterns Windows gives, quoted words like Spanish "'de'" included; a pattern Arcadia
/// still can't use shows the region's standard date instead of ending Arcadia.
/// </summary>
[Collection(LocalizationSettingsTests.Name)]
public sealed class RegionDatePatternTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastWeek = new(2026, 11, 6, 21, 58, 0, TimeSpan.Zero);

    public void Dispose() => Formatting.UseRegion(TestSystem.GermanRegion);

    public static TheoryData<string> EveryRegion()
    {
        var regions = new TheoryData<string>();
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
        {
            if (culture.Name.Length > 0)
            {
                regions.Add(culture.Name);
            }
        }

        return regions;
    }

    [Theory]
    [Trait("TestCase", "T592-1")]
    [MemberData(nameof(EveryRegion))]
    public void Every_region_gives_dates_without_the_year_and_without_an_error(string region)
    {
        var culture = CultureInfo.GetCultureInfo(region);
        Formatting.UseRegion(culture);

        AssertCutDates(culture);
    }

    [Theory]
    [Trait("TestCase", "T592-2")]
    [InlineData("es-ES", "dddd, d' de 'MMMM' de 'yyyy", "dd/MM/yyyy")]
    [InlineData("es-MX", "dddd, d' de 'MMMM' de 'yyyy", "dd/MM/yyyy")]
    [InlineData("pt-BR", "dddd, d' de 'MMMM' de 'yyyy", "dd/MM/yyyy")]
    [InlineData("pt-PT", "dddd, d' de 'MMMM' de 'yyyy", "dd/MM/yyyy")]
    [InlineData("ca-ES", "dddd, d' / 'MMMM' / 'yyyy", "dd/MM/yyyy")]
    [InlineData("ru-RU", "d MMMM yyyy 'г.'", "dd.MM.yyyy")]
    [InlineData("bg-BG", "dd MMMM yyyy 'г.', dddd", "d.M.yyyy 'г.'")]
    [InlineData("zh-CN", "yyyy'年'M'月'd'日', dddd", "yyyy/M/d")]
    [InlineData("hu-HU", "yyyy. MMMM d., dddd", "yyyy. MM. dd.")]
    [InlineData("lt-LT", "yyyy 'm'. MMMM d 'd'., dddd", "yyyy-MM-dd")]
    [InlineData("de-DE", "dddd, d. MMMM yyyy", "dd.MM.yyyy")]
    [InlineData("en-US", "dddd, MMMM d, yyyy", "M/d/yyyy")]
    [InlineData("de-DE", @"dddd, d\. MMMM \'yy", @"dd\.MM\.yyyy")]
    public void The_patterns_windows_gives_work_too(string region, string longDate, string shortDate)
    {
        var culture = WithPatterns(region, longDate, shortDate);
        Formatting.UseRegion(culture);

        AssertCutDates(culture);
    }

    [Theory]
    [Trait("TestCase", "T592-2")]
    [InlineData("es-ES", "dddd, d' de 'MMMM' de 'yyyy", "sáb, 14 de nov")]
    [InlineData("es-ES", "dddd, d 'de' MMMM 'de' yyyy", "sáb, 14 de nov")]
    [InlineData("pt-BR", "dddd, d' de 'MMMM' de 'yyyy", "sáb, 14 de nov")]
    [InlineData("ru-RU", "dddd, d MMMM yyyy 'г.'", "sáb, 14 nov")]
    [InlineData("zh-CN", "yyyy'年'M'月'd'日', dddd", "11月14日, sáb")]
    public void The_header_date_keeps_the_regions_words_and_drops_the_years(string region, string longDate, string expected)
    {
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("es-ES");
        try
        {
            Formatting.UseRegion(WithPatterns(region, longDate, "dd/MM/yyyy"));

            Assert.Equal(expected, Formatting.ShortDate(Now).Replace(".", string.Empty, StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Theory]
    [Trait("TestCase", "T592-2")]
    [InlineData("ja-JP", "yyyy年M月d日dddd", "2026年11月6日")]
    [InlineData("zh-CN", "yyyy'年'M'月'd'日' dddd", "2026年11月6日")]
    [InlineData("ko-KR", "yyyy'년' M'월' d'일' dddd", "2026년 11월 6일")]
    [InlineData("hu-HU", "yyyy. MM. d., dddd", "2026. 11. 6.")]
    [InlineData("lt-LT", "yyyy 'm'. MM 'mėn'. d 'd'., dddd", "2026 m. 11 mėn. 6 d.")]
    [InlineData("de-DE", "dddd, d. MM. yyyy", "6. 11. 2026")]
    public void A_news_entrys_date_drops_only_the_weekday_and_its_separator(string region, string longDate, string expected)
    {
        Formatting.UseRegion(WithPatterns(region, longDate, "dd/MM/yyyy"));

        Assert.Equal(expected, Formatting.LongDate(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    [Trait("TestCase", "T592-2")]
    [InlineData("th-TH", "dddd'ที่ 'd MMMM g yyyy", "d/M/yyyy")]
    [InlineData("ar-SA", "dd/MMMM/yyyy g", "dd/MM/yy g")]
    [InlineData("ja-JP", "gg y'年'M'月'd'日'", "gg y/M/d")]
    public void The_era_goes_with_the_year(string region, string longDate, string shortDate)
    {
        var culture = WithPatterns(region, longDate, shortDate);
        Formatting.UseRegion(culture);
        var era = Now.ToString("gg", Formatting.Region);

        Assert.DoesNotContain(era, Formatting.ShortDate(Now), StringComparison.Ordinal);
        Assert.False(DatePattern.Has(Formatting.DayAndMonthPattern(), 'g'));
    }

    [Theory]
    [Trait("TestCase", "T592-2")]
    [InlineData("MM yyyy", "MM")]
    [InlineData("M yyyy", "%M")]
    [InlineData("yyyy-d", "%d")]
    public void A_cut_down_to_one_letter_stays_a_field_not_a_standard_format(string pattern, string expected) =>
        Assert.Equal(expected, DatePattern.WithoutYear(pattern));

    [Theory]
    [Trait("TestCase", "T592-3")]
    [InlineData("dddd, d 'de MMMM 'de' yyyy", "dd/MM/yyyy", null)]
    [InlineData("dddd, d \"de MMMM yyyy", "dd/MM/yyyy", null)]
    [InlineData(@"dddd, d MMMM yyyy \", @"dd/MM/yyyy \", null)]
    [InlineData("'dddd, d MMMM yyyy", "'dd/MM/yyyy", "d 'de MMMM")]
    [InlineData("dddd, d 'de MMMM", "dd 'de MM", "%")]
    public void A_pattern_arcadia_cant_use_gives_a_readable_date_instead_of_an_error(string longDate, string shortDate, string? monthDay)
    {
        var culture = WithPatterns("es-ES", longDate, shortDate);
        if (monthDay is not null)
        {
            culture.DateTimeFormat.MonthDayPattern = monthDay;
        }

        Formatting.UseRegion(culture);

        Assert.False(string.IsNullOrWhiteSpace(Formatting.ShortDate(Now)));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.TimeOfDay(LastWeek, Now)));
        Assert.False(string.IsNullOrWhiteSpace(Formatting.LongDate(LastWeek)));
    }

    [AvaloniaTheory]
    [Trait("TestCase", "T592-4")]
    [InlineData("es-ES")]
    [InlineData("es-MX")]
    [InlineData("pt-BR")]
    public async Task Arcadia_starts_with_a_spanish_or_portuguese_region_and_shows_the_header_date(string region)
    {
        Formatting.UseRegion(CultureInfo.GetCultureInfo(region));

        using var arcadia = await ArcadiaDriver.StartAsync();

        Assert.False(string.IsNullOrWhiteSpace(arcadia.Main.Status.Date));
        Assert.DoesNotContain("'", arcadia.Main.Status.Date, StringComparison.Ordinal);
    }

    private static void AssertCutDates(CultureInfo culture)
    {
        var year = Now.ToString("yyyy", culture);
        var shortDate = Formatting.ShortDate(Now);
        var dayAndMonth = Formatting.TimeOfDay(LastWeek, Now);
        var longDate = Formatting.LongDate(LastWeek);

        Assert.False(string.IsNullOrWhiteSpace(shortDate));
        Assert.DoesNotContain(year, shortDate, StringComparison.Ordinal);
        Assert.DoesNotContain(LastWeek.ToString("yyyy", culture), dayAndMonth, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(longDate));
        Assert.DoesNotContain(LastWeek.ToLocalTime().ToString("dddd", Formatting.Region), longDate, StringComparison.Ordinal);
    }

    private static CultureInfo WithPatterns(string region, string longDate, string shortDate)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(region).Clone();
        culture.DateTimeFormat.Calendar = new GregorianCalendar();
        culture.DateTimeFormat.LongDatePattern = longDate;
        culture.DateTimeFormat.ShortDatePattern = shortDate;
        return culture;
    }
}
