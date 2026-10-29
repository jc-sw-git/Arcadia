using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>Arcadia's browser service (#83) without the web view: waits, timeouts, one page at a time, script results.</summary>
public sealed class WebPagesTests
{
    private static readonly Uri Page = new("https://store.steampowered.com/explore/?secret=1");

    private readonly FakeTimeProvider _time = new();
    private readonly Host _host = new();

    [Fact]
    public async Task A_page_waits_for_the_window_and_returns_what_its_script_gave()
    {
        var pages = new WebPages(_time, action => action());
        var read = pages.ReadAsync(Page, "script", _ => true, CancellationToken.None);
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
        var read = pages.ReadAsync(Page, "script", _ => true, CancellationToken.None);

        _time.Advance(WebPages.ReadTimeout);
        var result = await read;

        Assert.NotNull(result.Error);
        Assert.Null(_host.Open);
    }

    [Fact]
    public async Task Without_a_window_a_page_fails_instead_of_waiting_forever()
    {
        var pages = new WebPages(_time, action => action());
        var read = pages.ReadAsync(Page, "script", _ => true, CancellationToken.None);

        _time.Advance(WebPages.HostTimeout);

        Assert.NotNull((await read).Error);
    }

    [Fact]
    public async Task A_shown_page_has_no_timeout_and_a_cancel_closes_it()
    {
        var pages = Attached();
        using var cancel = new CancellationTokenSource();
        var shown = pages.ShowAsync("Bei Steam anmelden", Page, "script", _ => false, cancel.Token);

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
        var first = pages.ReadAsync(Page, "a", _ => true, CancellationToken.None);
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
        Assert.Equal("https://store.steampowered.com", WebPageViewModel.SignIn("t", Page, "s", _ => true).Address);
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
