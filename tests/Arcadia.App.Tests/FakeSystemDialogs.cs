using Arcadia.App.Services;

namespace Arcadia.App.Tests;

/// <summary>
/// The file and folder dialogs over Arcadia's window: a test sees which opened (<see cref="Opened"/>) and answers the
/// open one when it likes (<see cref="Answer"/>), so it can also press while a dialog is still open.
/// </summary>
internal sealed class FakeSystemDialogs : ISystemDialogs
{
    private TaskCompletionSource<string?>? _open;

    /// <summary>The titles of the dialogs opened, in order.</summary>
    public List<string> Opened { get; } = [];

    public Task<string?> PickFileAsync(string title, string typeName, IReadOnlyList<string> extensions) => Open(title);

    public Task<string?> PickFolderAsync(string title) => Open(title);

    /// <summary>The user picks <paramref name="path"/> in the open dialog, or cancels it with null.</summary>
    public void Answer(string? path)
    {
        var open = _open ?? throw new InvalidOperationException("No dialog is open.");
        _open = null;
        open.SetResult(path);
    }

    private Task<string?> Open(string title)
    {
        Opened.Add(title);
        _open = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _open.Task;
    }
}
