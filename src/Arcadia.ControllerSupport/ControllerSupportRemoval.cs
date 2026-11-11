using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Arcadia.Launchers.Abstractions;
using Microsoft.Win32;

namespace Arcadia.ControllerSupport;

/// <summary>What of the controller emulation is installed; <see cref="Drivers"/> need Pan's elevated uninstall.</summary>
public sealed record InstalledParts(bool Sisr, bool Viiper, bool Usbip, bool HidHide)
{
    public bool Any => Sisr || Viiper || Usbip || HidHide;

    public bool Drivers => Usbip || HidHide;
}

/// <summary>
/// Removes the controller emulation's drivers and SISR, when Arcadia is uninstalled and the user agrees to the one
/// question "Treiber für die Controller-Emulation auch entfernen?" (#243 part B2). SISR and VIIPER go here as "SISR
/// entfernen" in the settings removes them (<see cref="SisrInstallation"/>, #214). usbip-win2 and HidHide go in Pan's
/// <c>--uninstall --remove-drivers</c>, inside its one UAC prompt and after Pan undid its own HidHide entries.
/// </summary>
public static class ControllerSupportRemoval
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string SisrDirectory => Path.Combine(LocalAppData, "SISR");

    private static string ViiperExecutable => Path.Combine(LocalAppData, "VIIPER", "viiper.exe");

    /// <summary>Arcadia's own <c>viiper.exe</c> (<see cref="ViiperInstallation.InDataFolder"/>, about 13 MB), kept in the data folder.</summary>
    private static string OwnViiperDirectory => Path.Combine(Core.Diagnostics.LogLocation.DataFolder, "viiper");

    /// <summary>
    /// What is installed of the controller emulation, for the question at Arcadia's uninstall. Arcadia's own VIIPER is
    /// not asked about: it goes at every uninstall (<see cref="RemoveOwnViiper"/>).
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static InstalledParts FindInstalledParts() => new(
        Sisr: Directory.Exists(SisrDirectory),
        Viiper: File.Exists(ViiperExecutable),
        Usbip: UsbipDriver.UninstallCommand() is not null,
        HidHide: HidHideInstallation.InstalledVersion() is not null);

    /// <summary>
    /// The one question at Arcadia's uninstall (#243 part B2). A Windows message box can't offer a checklist, so it lists
    /// what is there and asks once; no answer or "Nein" keeps everything (other programs may use the drivers).
    /// </summary>
    public static string Question(InstalledParts parts, bool steamRunning, uint timeoutSeconds)
    {
        var lines = new List<string>();
        if (parts.Usbip)
        {
            lines.Add(ControllerSupportTexts.RemovalUsbip);
        }

        if (parts.HidHide)
        {
            lines.Add(ControllerSupportTexts.RemovalHidHide);
        }

        if (parts.Sisr || parts.Viiper)
        {
            lines.Add(parts.Sisr && parts.Viiper ? ControllerSupportTexts.RemovalSisrAndViiper : parts.Sisr ? "SISR" : "VIIPER");
        }

        return ControllerSupportTexts.RemovalIntro
            + ControllerSupportTexts.RemovalAsk
            + string.Concat(lines.Select(l => $"•  {l}\n"))
            + ControllerSupportTexts.RemovalOtherPrograms
            + (parts.Drivers ? ControllerSupportTexts.RemovalAdmin : string.Empty)
            + (parts.Sisr && steamRunning ? ControllerSupportTexts.RemovalSteamRestarts : string.Empty)
            + ControllerSupportTexts.RemovalTimeout(timeoutSeconds);
    }

    /// <summary>
    /// The last message of the uninstall when Windows removes a driver only at the next restart (#407). Without it, a
    /// player who installs Arcadia again before restarting can't install the drivers until then.
    /// </summary>
    public static string RestartNotice => ControllerSupportTexts.RemovalDone
        + ControllerSupportTexts.RemovalRestartNeeded
        + ControllerSupportTexts.RemovalRestartBeforeReinstall;

    /// <summary>
    /// Whether <see cref="RestartNotice"/> is due (#423): the player chose to remove a driver and Pan's elevated uninstall
    /// started. Not from the drivers' state: Pan hands off to its copy (#270) and returns before the copy removed
    /// anything, and a removed driver practically always waits for the restart, so a notice too many beats a missing one.
    /// </summary>
    public static bool RestartNoticeDue(bool removeDrivers, PanUninstallResult pan) => removeDrivers && pan == PanUninstallResult.Started;

    /// <summary>
    /// How long <see cref="RestartNotice"/> stays open: what is left of the uninstall hook's limit, less a margin so
    /// Velopack doesn't end the hook while the box is open. Null when too little is left to read it.
    /// </summary>
    public static TimeSpan? RestartNoticeTime(TimeSpan elapsed, TimeSpan hookLimit)
    {
        var left = hookLimit - elapsed - NoticeMargin;
        return left >= MinimumNoticeTime ? left : null;
    }

    private static readonly TimeSpan NoticeMargin = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan MinimumNoticeTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Removes SISR and VIIPER (<see cref="FindInstalledParts"/>) as "SISR entfernen" does (<see cref="SisrInstallation"/>);
    /// the drivers go with Pan's uninstall. Best effort: what fails is logged and skipped. Returns within a few seconds.
    /// </summary>
    /// <param name="steam">Steam the program (the launcher that owns Steam Input), closed for SISR's marker; null: no Steam.</param>
    [SupportedOSPlatform("windows")]
    public static void Remove(ILauncherProgram? steam, Action<string> log)
    {
        // SISR and its VIIPER stop in Remove, by path. Arcadia goes too, so its own viiper.exe no longer needs VIIPER's key folder.
        var sisr = new SisrInstallation(SisrInstallation.DefaultPaths(arcadiaViiper: null), steam, new WindowsSisrSystem());
        var steamClosed = steam is not null && sisr.Find().HasMarker && CloseSteamForMarker(steam, log);
        var result = sisr.Remove(steamRunning: steam?.IsRunning == true);
        log(result.Complete ? "Entfernt: SISR und VIIPER" : $"Nicht entfernt: {string.Join(", ", result.NotRemoved)} ({result.Cause})");
        if (steamClosed)
        {
            steam!.Start();
        }
    }

    /// <summary>
    /// Arcadia's own VIIPER goes at every uninstall, whatever the player answers (#422): nobody else uses it, and it needs
    /// no administrator rights. Only processes started from its folder are ended; SISR's VIIPER is a separate install.
    /// Runs first in the hook, before Velopack may end it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void RemoveOwnViiper(Action<string> log)
    {
        try
        {
            if (Directory.Exists(OwnViiperDirectory))
            {
                StopProcessesIn(OwnViiperDirectory);
                Directory.Delete(OwnViiperDirectory, recursive: true);
                log("Entfernt: Arcadias VIIPER");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"Arcadias VIIPER nicht entfernt: {ex.Message}");
        }
    }

    /// <summary>How long the uninstall waits for Steam to close; it usually takes 2 to 5 s.</summary>
    public static readonly TimeSpan SteamShutdownTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Steam rewrites <c>shortcuts.vdf</c> from memory when it exits, so a marker removed while it runs comes back:
    /// Steam is closed for the removal and started again afterwards (owner, 2026-10-24). True if Arcadia closed it.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static bool CloseSteamForMarker(ILauncherProgram steam, Action<string> log)
    {
        if (!steam.IsRunning)
        {
            return false;
        }

        // Shorter than Steam's own exit timeout, so the uninstall hook's limit holds.
        using var timeout = new CancellationTokenSource(SteamShutdownTimeout);
        bool closed;
        try
        {
            closed = steam.CloseAsync(TimeProvider.System, timeout.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            closed = false;
        }

        if (!closed)
        {
            log($"Steam-Eintrag „SISR Marker“ nicht entfernt: Steam war nach {SteamShutdownTimeout.TotalSeconds:0} s noch nicht beendet");
            return false;
        }

        return true;
    }

    private static void StopProcessesIn(string directory)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        foreach (var process in Process.GetProcessesByName("viiper"))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is not { } file || !file.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                {
                    // Already gone, or not ours to inspect or stop.
                }
            }
        }
    }
}
