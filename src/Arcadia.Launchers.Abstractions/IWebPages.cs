namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// Web pages Arcadia shows or loads for a launcher (#83): a browser of Arcadia's own whose profile (cookies) is kept in
/// Arcadia's data folder, so a sign-in on the launcher's own web page holds as it would in a browser. Signing in there is
/// a sign-in like any other in a browser; Arcadia doesn't pose as another app. A launcher sees nothing the user types,
/// only what its script returns from a loaded page.
/// </summary>
/// <remarks>Never throws; every outcome is a <see cref="WebPageResult"/>. May be called from any thread.</remarks>
public interface IWebPages
{
    /// <summary>
    /// Shows <paramref name="page"/> over the whole window for the user to sign in, headed <paramref name="title"/>. After
    /// every load <paramref name="script"/> runs; the first result <paramref name="isDone"/> accepts ends it. B ends it as
    /// cancelled.
    /// </summary>
    Task<WebPageResult> ShowAsync(string title, Uri page, string script, Func<string, bool> isDone, CancellationToken cancellationToken);

    /// <summary>
    /// Loads <paramref name="page"/> unseen and returns what <paramref name="script"/> gives: the first result
    /// <paramref name="isDone"/> accepts, or a refused one once the page stayed put for a few seconds (a page that passes
    /// on by script, e.g. to renew a sign-in, loads more than once).
    /// </summary>
    Task<WebPageResult> ReadAsync(Uri page, string script, Func<string, bool> isDone, CancellationToken cancellationToken);

    /// <summary>Deletes the cookies of every site under one of <paramref name="domains"/>, e.g. <c>steampowered.com</c>.</summary>
    Task<WebPageResult> ForgetAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken);
}

/// <summary>What a script returned (<see cref="Text"/>), why there is nothing (<see cref="Error"/>), or neither: cancelled.</summary>
public sealed record WebPageResult(string? Text, string? Error)
{
    public static WebPageResult Cancelled { get; } = new(null, null);

    public bool IsCancelled => Text is null && Error is null;

    public static WebPageResult Done(string text) => new(text, null);

    public static WebPageResult Failed(string error) => new(null, error);
}
