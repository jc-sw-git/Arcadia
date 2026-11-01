using Arcadia.App.ViewModels;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Services;

/// <summary>Where a web page is shown or loaded: the main window's view model, once it exists.</summary>
public interface IWebPageHost
{
    /// <summary>Opens <paramref name="page"/>; called on the UI thread.</summary>
    void Open(WebPageViewModel page);

    /// <summary>Closes <paramref name="page"/> if it is still open; called on the UI thread.</summary>
    void Close(WebPageViewModel page);
}

/// <summary>
/// Arcadia's browser for launchers (#83, <see cref="IWebPages"/>). The launchers are made before the window, so a page
/// waits until <see cref="Attach"/> named the host. One page at a time: they share the window's one web view.
/// </summary>
public sealed class WebPages : IWebPages
{
    /// <summary>A page loaded unseen that hasn't answered by then counts as unreachable; the store page takes a few seconds.</summary>
    public static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a page waits for the window; it is there within a second of the start.</summary>
    public static readonly TimeSpan HostTimeout = TimeSpan.FromSeconds(30);

    private readonly TimeProvider _time;
    private readonly Action<Action> _post;
    private readonly TaskCompletionSource<IWebPageHost> _host = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _onePage = new(1, 1);

    /// <param name="post">Runs an action on the UI thread.</param>
    public WebPages(TimeProvider time, Action<Action> post)
    {
        _time = time;
        _post = post;
    }

    public void Attach(IWebPageHost host) => _host.TrySetResult(host);

    public Task<WebPageResult> ShowAsync(string title, Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone, CancellationToken cancellationToken) =>
        OpenAsync(WebPageViewModel.SignIn(title, page, domains, script, isDone), timeout: null, cancellationToken);

    public Task<WebPageResult> ReadAsync(Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone, CancellationToken cancellationToken) =>
        OpenAsync(WebPageViewModel.Read(page, domains, script, isDone), ReadTimeout, cancellationToken);

    public Task<WebPageResult> ForgetAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken) =>
        RunAsync(WebPageViewModel.Forget(domains), ReadTimeout, cancellationToken);

    /// <param name="timeout">How long the page may take once open; none for a page the user works on.</param>
    /// <summary>A page off its launcher's sites is a launcher's mistake: the browser would cancel its first load anyway, this says why.</summary>
    private Task<WebPageResult> OpenAsync(WebPageViewModel page, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (!page.MayRead(page.Page))
        {
            AppLog.Write($"Browser: {page.Page.Host} gehört nicht zu {string.Join(", ", page.Domains)}, nicht geöffnet.");
            return Task.FromResult(WebPageResult.Failed("Die Seite gehört nicht zum Launcher."));
        }

        return RunAsync(page, timeout, cancellationToken);
    }

    private async Task<WebPageResult> RunAsync(WebPageViewModel page, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        try
        {
            var host = await _host.Task.WaitAsync(HostTimeout, _time, cancellationToken);
            await _onePage.WaitAsync(cancellationToken);
            try
            {
                _post(() => host.Open(page));
                await using var cancel = cancellationToken.Register(page.Cancel);
                return timeout is { } limit ? await page.Result.WaitAsync(limit, _time) : await page.Result;
            }
            catch (TimeoutException)
            {
                return WebPageResult.Failed("Die Seite hat nicht rechtzeitig geantwortet.");
            }
            finally
            {
                _post(() => host.Close(page));
                _onePage.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return WebPageResult.Cancelled;
        }
        catch (TimeoutException)
        {
            return WebPageResult.Failed("Das Fenster für Webseiten ist nicht bereit.");
        }
        catch (Exception ex)
        {
            AppErrors.Report(ex, "Webseite laden");
            return WebPageResult.Failed(ex.Message);
        }
    }
}
