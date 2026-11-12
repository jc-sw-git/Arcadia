using System.Diagnostics;

namespace Arcadia.App.Services;

/// <summary>The system's default browser, for a web page Arcadia names itself (#489: SteamGridDB's key page).</summary>
public interface IWebBrowser
{
    /// <summary>Opens <paramref name="page"/>; throws what the system throws when it can't.</summary>
    void Open(Uri page);
}

/// <summary>The clipboard's text (#489: a copied SteamGridDB key).</summary>
public interface IClipboardText
{
    /// <summary>The text on the clipboard; null if it holds none or can't be read.</summary>
    Task<string?> ReadAsync();
}

/// <summary>Opens https pages in the default browser through the shell; nothing else is started.</summary>
public sealed class SystemWebBrowser : IWebBrowser
{
    public void Open(Uri page)
    {
        // Only addresses written in Arcadia's code reach this; still, nothing but https goes to the shell.
        if (page.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Nur https-Seiten", nameof(page)); // not a UI text (log only)
        }

        Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true })?.Dispose();
    }
}

/// <summary>Until the window gives its own: no clipboard.</summary>
public sealed class NoClipboard : IClipboardText
{
    public Task<string?> ReadAsync() => Task.FromResult<string?>(null);
}

/// <summary>The window's clipboard (Avalonia's, Windows and Linux).</summary>
public sealed class WindowClipboard(Avalonia.Controls.TopLevel window) : IClipboardText
{
    public async Task<string?> ReadAsync() =>
        window.Clipboard is { } clipboard ? await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard) : null;
}
