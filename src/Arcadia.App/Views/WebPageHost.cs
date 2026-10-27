using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Diagnostics;
using Arcadia.Launchers.Abstractions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Arcadia.App.Views;

/// <summary>
/// Arcadia's browser (#83): loads <see cref="Page"/> in a web view of the system (WebView2 on Windows, WebKitGTK on
/// Linux) and reports what its script returns. Each page gets a new web view, removed with the page: a browser beside a
/// running game would cost memory for nothing. The profile (cookies) lives in Arcadia's data folder, so a sign-in holds
/// across starts and updates.
/// </summary>
/// <remarks>
/// An unseen page sits in a hidden part of the window: its native view is still made, only not shown. A native view is
/// always drawn over Avalonia's content, so a shown page covers the whole window and nothing is drawn on top of it.
/// </remarks>
public sealed class WebPageHost : Decorator
{
    public static readonly StyledProperty<WebPageViewModel?> PageProperty =
        AvaloniaProperty.Register<WebPageHost, WebPageViewModel?>(nameof(Page));

    /// <summary>A page that passes on by script loads again within this time; a result it gave before counts after it.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(4);

    /// <summary>A web view starts within a few seconds; WebView2 takes longest on its first start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);

    private static readonly string ProfileFolder = Path.Combine(LogLocation.DataFolder, "WebView");

    // Counts loads, so a settle wait knows whether the page went on in the meantime.
    private int _loads;

    // The page in the web view loaded once; reset with every new page.
    private bool _hasLoaded;

    public WebPageViewModel? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty)
        {
            Load(change.GetNewValue<WebPageViewModel?>());
        }
    }

    private void Load(WebPageViewModel? page)
    {
        if (Child is NativeWebView old)
        {
            Child = null;
            (old as IDisposable)?.Dispose();
        }

        if (page is null)
        {
            return;
        }

        _hasLoaded = false;
        var view = new NativeWebView();
        view.EnvironmentRequested += (_, e) =>
        {
            switch (e)
            {
                case WindowsWebView2EnvironmentRequestedEventArgs windows:
                    windows.UserDataFolder = ProfileFolder;
                    break;
                case GtkWebViewEnvironmentRequestedEventArgs gtk:
                    gtk.EphemeralDataManager = false;
                    gtk.BaseDataDirectory = ProfileFolder;
                    gtk.BaseCacheDirectory = Path.Combine(ProfileFolder, "Cache");
                    break;
            }
        };
        view.NavigationStarted += (_, e) =>
        {
            _loads++;
            // The launcher's pages and its sign-in only; a link to anything else stays unopened.
            if (e.Request is { } target && target.Scheme != Uri.UriSchemeHttps && target.Scheme != "about")
            {
                e.Cancel = true;
                return;
            }

            if (e.Request is { } address)
            {
                page.Address = WebPageViewModel.AddressOf(address);
            }
        };
        view.NavigationCompleted += (_, e) => LoadedAsync(page, view, e.IsSuccess).Forget("Webseite lesen");
        var started = false;
        view.AdapterCreated += (_, _) =>
        {
            started = true;
            StartAsync(page, view).Forget("Webseite öffnen");
        };
        Child = view;
        GiveUpIfNotStartedAsync(page, () => started).Forget("Webseite öffnen");
    }

    /// <summary>
    /// Without a browser engine (no WebView2 runtime, no WebKitGTK) the web view never starts: the page fails instead of
    /// covering the window until B.
    /// </summary>
    private static async Task GiveUpIfNotStartedAsync(WebPageViewModel page, Func<bool> started)
    {
        await Task.Delay(StartTimeout);
        if (!started() && !page.Result.IsCompleted)
        {
            AppLog.Write("Browser: Web-Ansicht nicht gestartet (WebView2 bzw. WebKitGTK fehlt?).");
            page.Complete(WebPageResult.Failed("Der Browser startet nicht."));
        }
    }

    private async Task StartAsync(WebPageViewModel page, NativeWebView view)
    {
        if (page.ForgetDomains.Count == 0)
        {
            view.Navigate(page.Page);
            return;
        }

        if (view.TryGetCookieManager() is not { } cookies)
        {
            page.Complete(WebPageResult.Failed("Dieser Browser kann keine Cookies löschen."));
            return;
        }

        var deleted = 0;
        foreach (var cookie in await cookies.GetCookiesAsync())
        {
            var domain = cookie.Domain.TrimStart('.');
            if (page.ForgetDomains.Any(d => domain.Equals(d, StringComparison.OrdinalIgnoreCase) || domain.EndsWith("." + d, StringComparison.OrdinalIgnoreCase)))
            {
                cookies.DeleteCookie(cookie);
                deleted++;
            }
        }

        AppLog.Write($"Browser: {deleted} Cookies gelöscht ({string.Join(", ", page.ForgetDomains)}).");
        page.Complete(WebPageResult.Done(string.Empty));
    }

    private async Task LoadedAsync(WebPageViewModel page, NativeWebView view, bool isSuccess)
    {
        if (page.Result.IsCompleted || page.ForgetDomains.Count > 0)
        {
            return;
        }

        if (!isSuccess)
        {
            // The first page not loading (offline) ends it: the launcher page says the launcher can't be reached and A
            // tries again. Later, the user is on the launcher's site and its own error pages apply, as in a browser.
            if (!page.IsShown || !_hasLoaded)
            {
                AppLog.Write($"Browser: {page.Page.Host} nicht geladen.");
                page.Complete(WebPageResult.Failed("Die Seite konnte nicht geladen werden."));
            }

            return;
        }

        _hasLoaded = true;

        var load = _loads;
        var text = WebPageViewModel.ScriptText(await view.InvokeScript(page.Script));
        if (page.Accepts(text))
        {
            page.Complete(WebPageResult.Done(text));
            return;
        }

        if (!page.IsShown)
        {
            // Back on the UI thread after the wait: the handlers run there.
            await Task.Delay(SettleTime);
            if (_loads == load)
            {
                page.Complete(WebPageResult.Done(text));
            }
        }
    }
}
