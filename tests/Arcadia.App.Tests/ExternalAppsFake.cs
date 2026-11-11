using Arcadia.App.Services;

namespace Arcadia.App.Tests;

/// <summary>The default browser and the clipboard for the tests (#489): pages opened are recorded, the clipboard is set by the test.</summary>
internal sealed class ExternalAppsFake : IWebBrowser, IClipboardText
{
    private readonly List<Uri> _opened = [];

    /// <summary>The clipboard's text; null: it holds none (empty, an image).</summary>
    public string? ClipboardText { get; set; }

    /// <summary>What opening a page throws, as the shell does without a browser.</summary>
    public Exception? BrowserFailure { get; set; }

    /// <summary>The pages opened so far; a copy, as <see cref="Open"/> runs off the UI thread.</summary>
    public IReadOnlyList<Uri> Opened
    {
        get
        {
            lock (_opened)
            {
                return [.. _opened];
            }
        }
    }

    public void Open(Uri page)
    {
        if (BrowserFailure is { } failure)
        {
            throw failure;
        }

        lock (_opened)
        {
            _opened.Add(page);
        }
    }

    public Task<string?> ReadAsync() => Task.FromResult(ClipboardText);
}
