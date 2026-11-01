using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>Arcadia's browser service (#83) without the web view: waits, timeouts, one page at a time, script results.</summary>
public sealed class WebPagesTests
{
    private static readonly Uri Page = new("https://store.steampowered.com/explore/?secret=1");

    private static readonly IReadOnlyList<string> Steam = ["steampowered.com", "steamcommunity.com"];

    private readonly FakeTimeProvider _time = new();
    private readonly Host _host = new();

    [Fact]
    public async Task A_page_waits_for_the_window_and_returns_what_its_script_gave()
    {
        var pages = new WebPages(_time, action => action());
        var read = pages.ReadAsync(Page, Steam, "script", _ => true, CancellationToken.None);
        Assert.False(read.IsCompleted);

        pages.Attach(_host);
        await OpenedAsync(1);

        var page = Assert.Single(_host.Opened);
        Assert.False(page.IsShown);
        page.Complete(WebPageResult.Done("text"));

        Assert.Equal("text", (await read).Text);
        Assert.Null(_host.Open);
    }

    [Fact]
    public async Task An_unseen_page_that_does_not_answer_fails_after_the_timeout_and_closes()
    {
        var pages = Attached();
        var read = pages.ReadAsync(Page, Steam, "script", _ => true, CancellationToken.None);

        _time.Advance(WebPages.ReadTimeout);
        var result = await read;

        Assert.NotNull(result.Error);
        Assert.Null(_host.Open);
    }

    [Fact]
    public async Task Without_a_window_a_page_fails_instead_of_waiting_forever()
    {
        var pages = new WebPages(_time, action => action());
        var read = pages.ReadAsync(Page, Steam, "script", _ => true, CancellationToken.None);

        _time.Advance(WebPages.HostTimeout);

        Assert.NotNull((await read).Error);
    }

    [Fact]
    public async Task A_shown_page_has_no_timeout_and_a_cancel_closes_it()
    {
        var pages = Attached();
        using var cancel = new CancellationTokenSource();
        var shown = pages.ShowAsync("Bei Steam anmelden", Page, Steam, "script", _ => false, cancel.Token);

        _time.Advance(TimeSpan.FromHours(1));
        Assert.False(shown.IsCompleted);
        Assert.True(_host.Open!.IsShown);

        cancel.Cancel();

        Assert.True((await shown).IsCancelled);
        Assert.Null(_host.Open);
    }

    [Fact]
    public async Task Pages_open_one_after_the_other()
    {
        var pages = Attached();
        var first = pages.ReadAsync(Page, Steam, "a", _ => true, CancellationToken.None);
        var second = pages.ForgetAsync(["steampowered.com"], CancellationToken.None);
        await OpenedAsync(1);
        Assert.Single(_host.Opened);

        _host.Opened[0].Complete(WebPageResult.Done("a"));
        await first;
        await OpenedAsync(2);
        var forget = _host.Opened[1];
        Assert.Equal(["steampowered.com"], forget!.ForgetDomains);
        forget.Complete(WebPageResult.Done(string.Empty));

        Assert.Equal(string.Empty, (await second).Text);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("null", "")]
    [InlineData("undefined", "")]
    [InlineData("\"{\\\"user\\\":\\\"x\\\"}\"", "{\"user\":\"x\"}")]
    [InlineData("\"\"", "")]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("\"kaputt", "\"kaputt")]
    public void Script_results_are_read_as_the_browser_hands_them_over(string? raw, string text) =>
        Assert.Equal(text, WebPageViewModel.ScriptText(raw));

    [Fact]
    public void The_address_shows_scheme_and_host_only()
    {
        Assert.Equal("https://store.steampowered.com", WebPageViewModel.AddressOf(Page));
        Assert.Equal("https://store.steampowered.com", WebPageViewModel.SignIn("t", Page, Steam, "s", _ => true).Address);
    }

    [Theory]
    [InlineData("https://store.steampowered.com/login/")]
    [InlineData("https://login.steampowered.com/jwt/finalizelogin")]
    [InlineData("https://steamcommunity.com/login/home/")]
    [InlineData("https://STORE.SteamPowered.com/explore/")]
    public void A_page_opens_and_reads_steams_sites_and_their_subdomains(string address)
    {
        var page = WebPageViewModel.SignIn("t", Page, Steam, "s", _ => true);

        Assert.True(page.MayOpen(new Uri(address)));
        Assert.True(page.MayRead(new Uri(address)));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://steampowered.com.example.com/login/")]
    [InlineData("https://notsteampowered.com/login/")]
    [InlineData("https://xn--steampwered-6ib.com/")]
    [InlineData("http://store.steampowered.com/login/")]
    [InlineData("file:///C:/Users/someone/fake-login.html")]
    [InlineData("javascript:alert(1)")]
    public void A_page_neither_opens_nor_reads_any_other_site(string address)
    {
        var page = WebPageViewModel.SignIn("t", Page, Steam, "s", _ => true);

        Assert.False(page.MayOpen(new Uri(address)));
        Assert.False(page.MayRead(new Uri(address)));
    }

    [Fact]
    public void A_blank_page_opens_but_is_never_read()
    {
        var page = WebPageViewModel.Read(Page, Steam, "s", _ => true);

        Assert.True(page.MayOpen(new Uri("about:blank")));
        Assert.False(page.MayRead(new Uri("about:blank")));
        Assert.False(page.MayRead(null));
    }

    [Fact]
    public async Task A_page_off_the_launchers_sites_fails_without_opening()
    {
        var pages = Attached();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var shown = await pages.ShowAsync("Bei Steam anmelden", new Uri("https://example.com/login"), Steam, "s", _ => true, stop.Token);
        var read = await pages.ReadAsync(Page, [], "s", _ => true, stop.Token);

        Assert.NotNull(shown.Error);
        Assert.NotNull(read.Error);
        Assert.Empty(_host.Opened);
    }

    /// <summary>Pages open on another thread once the one before is done.</summary>
    private async Task OpenedAsync(int count)
    {
        for (var i = 0; i < 2000 && _host.Opened.Count < count; i++)
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }

        Assert.Equal(count, _host.Opened.Count);
    }

    private WebPages Attached()
    {
        var pages = new WebPages(_time, action => action());
        pages.Attach(_host);
        return pages;
    }

    private sealed class Host : IWebPageHost
    {
        private readonly List<WebPageViewModel> _opened = [];

        public IReadOnlyList<WebPageViewModel> Opened
        {
            get
            {
                lock (_opened)
                {
                    return [.. _opened];
                }
            }
        }

        public WebPageViewModel? Open { get; private set; }

        void IWebPageHost.Open(WebPageViewModel page)
        {
            lock (_opened)
            {
                _opened.Add(page);
            }

            Open = page;
        }

        void IWebPageHost.Close(WebPageViewModel page)
        {
            if (Open == page)
            {
                Open = null;
            }
        }
    }
}
