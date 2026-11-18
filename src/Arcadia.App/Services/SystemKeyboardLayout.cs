using System.Runtime.InteropServices;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;

namespace Arcadia.App.Services;

/// <summary>The layouts the on-screen keyboards offer, from "Lokalisierung" and the system's keyboard layout (#553).</summary>
public static partial class SystemKeyboardLayout
{
    private static string? _id;
    private static bool _read;

    /// <summary>
    /// Where the system's layout comes from; a test puts a fake one here. Read once, on the first keyboard: a layout
    /// switched in Windows while Arcadia runs counts from the next start.
    /// </summary>
    internal static Func<string?> Read { get; set; } = ReadFromWindows;

    /// <summary>The id of the system's keyboard layout, null when it isn't one of the five.</summary>
    public static string? Id()
    {
        if (!_read)
        {
            _id = Read();
            _read = true;
        }

        return _id;
    }

    /// <summary>Forgets the layout read, so the next <see cref="Id"/> reads <see cref="Read"/> again (tests).</summary>
    internal static void Forget() => _read = false;

    /// <summary>What the keyboards offer with these settings: the default layout and the quick selection for R3.</summary>
    public static KeyboardLayoutChoice Choice(LocalizationSettings settings) =>
        KeyboardLayoutChoice.From(settings, Id(), UiLanguage.Culture.Name);

    // The layout Windows gives the calling thread: on a thread that never switched it, the default input method.
    private static string? ReadFromWindows() =>
        OperatingSystem.IsWindows() ? SystemKeyboardLayouts.IdOf(unchecked((uint)GetKeyboardLayout(0).ToInt64())) : null;

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetKeyboardLayout(uint thread);
}
