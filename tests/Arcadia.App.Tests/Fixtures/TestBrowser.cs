using System.ComponentModel;
using Arcadia.App.ViewModels;
using Arcadia.Core.Tests;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Tests.Fixtures;

/// <summary>
/// Arcadia's browser view (<c>WebPageHost</c>) as the tests have it (#83): it can't run headless, so this answers every
/// page the window opens from <see cref="FakeSteamWeb"/>. Everything up to the view is real: the launcher asks
/// <see cref="Services.WebPages"/>, the page opens over the window and B on the gamepad cancels it.
/// </summary>
internal sealed class TestBrowser
{
    private readonly MainViewModel _main;
    private readonly FakeSteamWeb _steam;

    public TestBrowser(MainViewModel main, FakeSteamWeb steam)
    {
        _main = main;
        _steam = steam;
        main.PropertyChanged += OnChanged;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.WebPage) && _main.WebPage is { } page)
        {
            _ = Task.Run(() => AnswerAsync(page));
        }
    }

    private async Task AnswerAsync(WebPageViewModel page)
    {
        // Closed from Arcadia's side (B, ✕): the page in the fake browser goes too.
        var closed = new CancellationTokenSource();
        _ = page.Result.ContinueWith(_ => closed.Cancel(), TaskScheduler.Default);
        try
        {
            var result = page.ForgetDomains.Count > 0
                ? await _steam.ForgetAsync(page.ForgetDomains, closed.Token)
                : page.IsShown
                    ? await _steam.ShowAsync(page.Title, page.Page, page.Script, page.Accepts, closed.Token)
                    : await _steam.ReadAsync(page.Page, page.Script, page.Accepts, closed.Token);
            page.Complete(result);
        }
        catch (Exception ex)
        {
            // An assert of the fake failed: the page says so, and the test's expectations fail on it.
            page.Complete(WebPageResult.Failed(ex.Message));
        }
    }
}
