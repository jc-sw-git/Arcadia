using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Arcadia.App.Services;

/// <summary>The Windows dialogs Arcadia opens itself (#138): choosing a file or a folder.</summary>
public interface ISystemDialogs
{
    /// <param name="typeName">What the files are, e.g. "Bilder".</param>
    /// <param name="extensions">The file endings offered, without the dot.</param>
    /// <returns>The chosen file's local path, or null if the user cancelled.</returns>
    Task<string?> PickFileAsync(string title, string typeName, IReadOnlyList<string> extensions);

    /// <returns>The chosen folder's local path, or null if the user cancelled.</returns>
    Task<string?> PickFolderAsync(string title);
}

/// <summary>
/// The dialogs over Arcadia's window. The controller can't steer them, so one opened from the controller switches the mouse
/// mode on while it is open and off again after it, if it came on for it (owner, 2026-10-22); that holds for every dialog
/// Arcadia opens, so every one goes through here.
/// </summary>
/// <param name="controllerInUse">Whether the last input came from the controller.</param>
/// <param name="mouseMode">The mouse mode, once it is wired; null before.</param>
public sealed class SystemDialogs(TopLevel owner, Func<bool> controllerInUse, Func<MouseModeHost?> mouseMode) : ISystemDialogs
{
    public Task<string?> PickFileAsync(string title, string typeName, IReadOnlyList<string> extensions) =>
        WithMouseModeAsync(async () =>
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = [.. extensions.Select(e => $"*.{e}")] }],
            });
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        });

    public Task<string?> PickFolderAsync(string title) =>
        WithMouseModeAsync(async () =>
        {
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        });

    private async Task<string?> WithMouseModeAsync(Func<Task<string?>> show)
    {
        var host = controllerInUse() ? mouseMode() : null;
        var held = host?.HoldForDialog(true) == true;
        try
        {
            return await show();
        }
        finally
        {
            if (held)
            {
                host!.HoldForDialog(false);
            }
        }
    }
}
