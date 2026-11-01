using System.Text.Json;
using Arcadia.App.Input;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A web page Arcadia's browser shows or loads for a launcher (#83, <see cref="IWebPages"/>): shown over the whole window
/// for a sign-in (B goes back), or loaded unseen to read something from it, or no page at all to delete cookies. The view
/// (<c>WebPageHost</c>) loads it, runs <see cref="Script"/> after every load and reports through <see cref="Complete"/>.
/// </summary>
public sealed partial class WebPageViewModel : ObservableObject
{
    private readonly Func<string, bool>? _isDone;
    private readonly TaskCompletionSource<WebPageResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private WebPageViewModel(string title, Uri page, IReadOnlyList<string> domains, string script, Func<string, bool>? isDone, bool isShown, IReadOnlyList<string> forgetDomains)
    {
        Title = title;
        Page = page;
        Domains = domains;
        Script = script;
        _isDone = isDone;
        IsShown = isShown;
        ForgetDomains = forgetDomains;
        Address = AddressOf(page);
    }

    /// <summary>Scheme and host only: the path and query may hold what the page passes on.</summary>
    public static string AddressOf(Uri page) => page.IsAbsoluteUri ? $"{page.Scheme}://{page.Host}" : string.Empty;

    public static WebPageViewModel SignIn(string title, Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone) =>
        new(title, page, domains, script, isDone, isShown: true, []);

    public static WebPageViewModel Read(Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone) =>
        new(string.Empty, page, domains, script, isDone, isShown: false, []);

    public static WebPageViewModel Forget(IReadOnlyList<string> domains) =>
        new(string.Empty, new Uri("about:blank"), [], string.Empty, null, isShown: false, domains);

    /// <summary>Whether <paramref name="host"/> is one of <paramref name="domains"/> or a subdomain of one.</summary>
    public static bool IsUnder(string host, IReadOnlyList<string> domains)
    {
        host = host.TrimEnd('.');
        return domains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The heading above a shown page, e.g. "Bei Steam anmelden".</summary>
    public string Title { get; }

    public Uri Page { get; }

    /// <summary>
    /// Where the shown page is now, like a browser's address bar: the user sees it is Steam's own site before signing in.
    /// </summary>
    [ObservableProperty]
    public partial string Address { get; set; } = string.Empty;

    public string Script { get; }

    /// <summary>The launcher's own sites (<see cref="IWebPages.ShowAsync"/>): the only hosts the page opens and reads.</summary>
    public IReadOnlyList<string> Domains { get; }

    /// <summary>
    /// Whether the page may go to <paramref name="address"/>: an https address on the launcher's sites, or a blank page
    /// (<c>about:</c>) the browser opens itself. Anything else stays unopened.
    /// </summary>
    public bool MayOpen(Uri address) => address.IsAbsoluteUri && (address.Scheme == "about" || MayRead(address));

    /// <summary>Whether the script may run on <paramref name="address"/>: only an https page on the launcher's sites.</summary>
    public bool MayRead(Uri? address) =>
        address is { IsAbsoluteUri: true } && address.Scheme == Uri.UriSchemeHttps && IsUnder(address.IdnHost, Domains);

    /// <summary>Shown over the whole window; otherwise loaded unseen.</summary>
    public bool IsShown { get; }

    /// <summary>The cookie domains to delete instead of loading a page; empty for a page.</summary>
    public IReadOnlyList<string> ForgetDomains { get; }

    public Task<WebPageResult> Result => _result.Task;

    /// <summary>Whether a script result ends the page at once.</summary>
    public bool Accepts(string text) => _isDone?.Invoke(text) ?? true;

    public void Complete(WebPageResult result) => _result.TrySetResult(result);

    public void Cancel() => Complete(WebPageResult.Cancelled);

    /// <summary>A shown page takes every press: the page itself is used with mouse and keyboard, B goes back.</summary>
    public void Handle(UiAction action)
    {
        if (action == UiAction.Back)
        {
            Cancel();
        }
    }

    /// <summary>
    /// The text a script returned. Browsers hand the result over JSON-encoded (WebView2: a string in quotes); a script's
    /// null or anything but a string counts as empty.
    /// </summary>
    public static string ScriptText(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw is "null" or "undefined")
        {
            return string.Empty;
        }

        if (raw[0] != '"')
        {
            return raw;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
        }
        catch (JsonException)
        {
            return raw;
        }
    }
}
