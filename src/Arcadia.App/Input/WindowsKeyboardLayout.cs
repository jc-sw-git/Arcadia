using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Arcadia.App.Services;
using Avalonia.Controls;

namespace Arcadia.App.Input;

/// <summary>
/// Reads the letters of the Windows input layout for <see cref="KeyLabels"/> (#588) and reads them again when the player
/// switches the layout (Win + Space sends the window WM_INPUTLANGCHANGE) or comes back to Arcadia.
/// </summary>
[SupportedOSPlatform("windows")]
public static partial class WindowsKeyboardLayout
{
    private const uint InputLanguageChanged = 0x0051;
    private const uint ScanCodeToVirtualKey = 1;

    // ToUnicodeEx flag: don't touch the keyboard's state, so reading a dead key doesn't put an accent on the next typed letter.
    private const uint KeepKeyboardState = 0x4;

    // Set 1 scan codes of the letters, by their US QWERTY place.
    private static readonly Dictionary<char, uint> ScanCodes = new()
    {
        ['Q'] = 0x10, ['W'] = 0x11, ['E'] = 0x12, ['R'] = 0x13, ['T'] = 0x14, ['Y'] = 0x15, ['U'] = 0x16, ['I'] = 0x17,
        ['O'] = 0x18, ['P'] = 0x19, ['A'] = 0x1E, ['S'] = 0x1F, ['D'] = 0x20, ['F'] = 0x21, ['G'] = 0x22, ['H'] = 0x23,
        ['J'] = 0x24, ['K'] = 0x25, ['L'] = 0x26, ['Z'] = 0x2C, ['X'] = 0x2D, ['C'] = 0x2E, ['V'] = 0x2F, ['B'] = 0x30,
        ['N'] = 0x31, ['M'] = 0x32,
    };

    /// <summary>Reads the layout now and follows its changes in <paramref name="window"/>.</summary>
    public static void Follow(Window window)
    {
        Read();
        window.Activated += (_, _) => Read();
        Win32Properties.AddWndProcHookCallback(window, OnMessage);
    }

    private static IntPtr OnMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == InputLanguageChanged)
        {
            Read();
        }

        return IntPtr.Zero;
    }

    private static void Read()
    {
        try
        {
            // The UI thread's layout: the one Arcadia's keys arrive in.
            var layout = GetKeyboardLayout(0);
            KeyLabels.Use(letter => PrintedAt(ScanCodes[letter], layout));
        }
        catch (Exception ex)
        {
            // The QWERTY letters stay; the keys work by position anyway.
            AppLog.Write($"Tastaturlayout nicht lesbar: {ex}");
        }
    }

    private static unsafe char? PrintedAt(uint scanCode, IntPtr layout)
    {
        var virtualKey = MapVirtualKeyExW(scanCode, ScanCodeToVirtualKey, layout);
        if (virtualKey == 0)
        {
            return null;
        }

        var state = stackalloc byte[256];
        var text = stackalloc char[4];
        var length = ToUnicodeEx(virtualKey, scanCode, state, text, 4, KeepKeyboardState, layout);
        return length == 1 ? text[0] : null;
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetKeyboardLayout(uint threadId);

    [LibraryImport("user32.dll")]
    private static partial uint MapVirtualKeyExW(uint code, uint mapType, IntPtr layout);

    [LibraryImport("user32.dll")]
    private static unsafe partial int ToUnicodeEx(uint virtualKey, uint scanCode, byte* keyState, char* text, int size, uint flags, IntPtr layout);
}
