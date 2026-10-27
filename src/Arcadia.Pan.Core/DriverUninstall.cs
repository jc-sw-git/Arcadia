namespace Arcadia.Pan;

/// <summary>An entry of Windows' list of installed programs (<c>HKLM\…\Uninstall\&lt;key&gt;</c>), as far as Pan reads it.</summary>
public sealed record UninstallEntry(string KeyName, string? DisplayName, string? Publisher, string? UninstallString, bool WindowsInstaller);

/// <summary>A program to run elevated, with its arguments; <see cref="What"/> names it in the log.</summary>
public sealed record UninstallCommand(string What, string File, IReadOnlyList<string> Arguments);

/// <summary>
/// How Pan removes the drivers of the controller emulation when the user says yes at Arcadia's uninstall (#243 part
/// B2): silently, inside Pan's one UAC prompt. Only commands Pan builds itself run: msiexec with a product code, or
/// usbip-win2's own Inno Setup uninstaller from Program Files; never an uninstall string as it stands.
/// </summary>
public static class DriverUninstall
{
    private static readonly string[] InnoSilent = ["/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"];

    /// <summary>
    /// HidHide comes as an MSI inside an Advanced Installer bootstrapper; its MSI entry is keyed by the product code.
    /// Null when no such entry exists (then Windows' "Apps" removes it by hand).
    /// </summary>
    public static UninstallCommand? ForHidHide(IEnumerable<UninstallEntry> entries, string systemDirectory)
    {
        var entry = entries.FirstOrDefault(e => e.WindowsInstaller
                                                && Guid.TryParseExact(e.KeyName, "B", out _)
                                                && e.DisplayName?.StartsWith("HidHide", StringComparison.OrdinalIgnoreCase) == true);
        return entry is null
            ? null
            : new UninstallCommand("HidHide", Path.Combine(systemDirectory, "msiexec.exe"), ["/x", entry.KeyName, "/qn", "/norestart"]);
    }

    /// <summary>
    /// usbip-win2's Inno Setup uninstaller (<c>unins000.exe</c>), only from <paramref name="programFiles"/>, which only
    /// administrators can write; null when there is none.
    /// </summary>
    public static UninstallCommand? ForUsbip(IEnumerable<UninstallEntry> entries, string programFiles)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(programFiles)) + Path.DirectorySeparatorChar;
        foreach (var entry in entries.Where(e => e.DisplayName?.StartsWith("USBip", StringComparison.OrdinalIgnoreCase) == true))
        {
            if (entry.UninstallString is not { } command || ExecutableOf(command) is not { } file)
            {
                continue;
            }

            var name = Path.GetFileName(file);
            if (name.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return new UninstallCommand("usbip-win2", Path.GetFullPath(file), InnoSilent);
            }
        }

        return null;
    }

    /// <summary>The program of an uninstall string like <c>"C:\x\unins000.exe" /S</c>; null if it isn't a rooted path.</summary>
    public static string? ExecutableOf(string command)
    {
        command = command.Trim();
        var file = command.StartsWith('"') && command.IndexOf('"', 1) is var end and > 0
            ? command[1..end]
            : command.Split(' ', 2)[0];
        return file.Length > 0 && Path.IsPathFullyQualified(file) && file.IndexOfAny(Path.GetInvalidPathChars()) < 0 ? file : null;
    }
}
