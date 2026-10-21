using System.Runtime.InteropServices;
using Arcadia.Core.Library;
using Arcadia.Launchers;

namespace Arcadia.App.Services;

/// <summary>The window of a running game, for "Zurück ins Spiel" (#95).</summary>
public static partial class GameWindow
{
    /// <summary>Windows are listed while they open and close; a list that long has gone round in a circle.</summary>
    private const int MaxWindows = 10_000;

    private const uint NextWindow = 2;
    private const uint Owner = 4;
    private const int ExtendedStyle = -20;
    private const long ToolWindow = 0x80;
    private const int Restore = 9;

    /// <summary>
    /// The frontmost main window of a process of <paramref name="gameKey"/>: from the game's install folder, or, with a
    /// path that can't be read, started after Arcadia started that game (<see cref="LaunchedGames"/>). 0 if there is none.
    /// Lists processes; call it off the UI thread.
    /// </summary>
    public static nint Find(string gameKey, GameFolders games, LaunchedGames launched)
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        // A game has a few windows at most, other programs many: each process is asked for its path once.
        var isGame = new Dictionary<uint, bool>();
        var window = GetTopWindow(0);
        for (var i = 0; window != 0 && i < MaxWindows; i++, window = GetWindow(window, NextWindow))
        {
            if (!IsMainWindow(window) || GetWindowThreadProcessId(window, out var processId) == 0 || processId == Environment.ProcessId)
            {
                continue;
            }

            if (!isGame.TryGetValue(processId, out var belongs))
            {
                var id = (int)processId;
                belongs = (ProcessWatch.PathOf(id) is { } path ? games.GameOf(path) : launched.GameStartedBefore(id)) == gameKey;
                isGame[processId] = belongs;
            }

            if (belongs)
            {
                return window;
            }
        }

        return 0;
    }

    /// <summary>Restores <paramref name="window"/> if it is minimized and gives it the foreground; false if Windows refused.</summary>
    public static bool BringToFront(nint window)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (IsIconic(window))
        {
            ShowWindow(window, Restore);
        }

        return SetForegroundWindow(window);
    }

    /// <summary>A window of its own on the taskbar: visible, without an owner (dialogs have one) and no tool window.</summary>
    private static bool IsMainWindow(nint window) =>
        IsWindowVisible(window) && GetWindow(window, Owner) == 0 && (GetWindowLongPtr(window, ExtendedStyle) & ToolWindow) == 0;

    [LibraryImport("user32.dll")]
    private static partial nint GetTopWindow(nint parent);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindow(nint window, uint relation);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);
}
