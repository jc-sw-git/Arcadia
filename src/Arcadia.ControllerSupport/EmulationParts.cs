using Arcadia.Pan;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The parts the controller emulation needs and the rows that say one is missing or can't be used (#243 part B2):
/// usbip-win2, VIIPER, Pan and HidHide, in that order below "Controller-Wächter (Pan)". While a part is
/// missing the switch is greyed out; with every part missing the switch itself offers "Treiber installieren".
/// </summary>
public static class EmulationParts
{
    /// <summary>The ids of the rows that mean "this part is missing or too old"; "Installieren" sets each one up.</summary>
    private static readonly HashSet<string> MissingIds =
    [
        "usbip-missing", "usbip-outdated", "usbip-stopped", "viiper-missing", "pan-missing", "pan-failing", "hidhide-missing", "hidhide-unsupported", "hidhide-removal-pending",
    ];

    /// <summary>The parts as the user knows them, by the rows that say one is missing.</summary>
    private static readonly (string Prefix, string Name)[] Parts =
    [
        ("usbip-", ControllerSupportTexts.PartUsbip), ("viiper-", "VIIPER"), ("pan-", ControllerSupportTexts.PartPan), ("hidhide-", "HidHide"),
    ];

    public static HealthIssue ViiperMissing { get; } = new("viiper-missing", HealthSeverity.Warning, ControllerSupportTexts.ViiperMissingTitle,
        ControllerSupportTexts.ViiperMissingText,
        HealthFix.InstallDrivers, ControllerSupportTexts.Install, Summary: ControllerSupportTexts.ViiperSubtitle);

    /// <summary>
    /// Pan is installed but its last run ended with an error: it crashed, or its data folder was untrusted and couldn't be
    /// made anew (owner's B2 test, 2026-10-27). Its install makes the folder anew, so the fix is the same as for a
    /// missing Pan.
    /// </summary>
    public static HealthIssue PanFailing { get; } = new("pan-failing", HealthSeverity.Warning, ControllerSupportTexts.PanFailingTitle,
        ControllerSupportTexts.PanFailingText(PanPaths.LogPath),
        HealthFix.InstallDrivers, ControllerSupportTexts.SetUpAgain, Summary: ControllerSupportTexts.PanFailingSubtitle);

    /// <summary>
    /// HidHide was uninstalled and is still there until Windows restarts. Counts as a missing part (so "Treiber
    /// installieren" knows every part is gone) but never shows: the restart row stands for it (#382, D2).
    /// </summary>
    public static HealthIssue HidHideRemovalPending { get; } = new("hidhide-removal-pending", HealthSeverity.Info, ControllerSupportTexts.HidHideRestartPending,
        ControllerSupportTexts.HidHideRemovalPending);

    public const string WindowsRestartId = "windows-restart";
    public static string WindowsRestartTitle => ControllerSupportTexts.WindowsRestartPendingTitle;
    public static string WindowsRestartLabel => ControllerSupportTexts.RestartWindows;

    public static string PausedSummary => ControllerSupportTexts.PausedSummary;
    public static string PausedText => ControllerSupportTexts.PausedText;
    public static string PausedMore => ControllerSupportTexts.PausedMore;
    public static string PausedBoxTitle => ControllerSupportTexts.PausedBoxTitle;
    public static string PausedBoxText => ControllerSupportTexts.PausedBoxText;

    public static string RecommendedSummary => ControllerSupportTexts.RecommendedSummary;
    public static string RecommendedText => ControllerSupportTexts.RecommendedText;
    public static string RecommendedMore => ControllerSupportTexts.RecommendedMore;
    public static string RecommendedBoxTitle => ControllerSupportTexts.RecommendedBoxTitle;
    public static string RecommendedBoxText => ControllerSupportTexts.RecommendedBoxText;

    /// <summary>
    /// The one restart row (#382, D2, artboards T382F01, T382F04): a driver removal pauses Pan until the restart, a fresh
    /// usbip-win2 only recommends it. The texts name no driver: which one is often not knowable for sure.
    /// </summary>
    public static HealthIssue WindowsRestartRequired { get; } = new(WindowsRestartId, HealthSeverity.Info, WindowsRestartTitle,
        $"{PausedText} {PausedMore}", HealthFix.RestartWindows, WindowsRestartLabel, PausedSummary);

    /// <inheritdoc cref="WindowsRestartRequired"/>
    public static HealthIssue WindowsRestartRecommended { get; } = new(WindowsRestartId, HealthSeverity.Info, WindowsRestartTitle,
        $"{RecommendedText} {RecommendedMore}", HealthFix.RestartWindows, WindowsRestartLabel, RecommendedSummary);

    /// <summary>The restart row for <paramref name="restart"/>, or null when none is pending.</summary>
    public static HealthIssue? WindowsRestart(PendingRestart restart) => restart switch
    {
        PendingRestart.Required => WindowsRestartRequired,
        PendingRestart.Recommended => WindowsRestartRecommended,
        _ => null,
    };

    /// <summary>
    /// The rows as the settings show them (#382): while a driver removal waits for the restart, the restart row instead
    /// of every part row (nothing can be installed until then) and of HidHide's states (Pan isn't asked); else the rows
    /// with the restart recommendation after the parts.
    /// </summary>
    public static IReadOnlyList<HealthIssue> WithRestart(IReadOnlyList<HealthIssue> issues, PendingRestart restart) => restart switch
    {
        PendingRestart.Required => [WindowsRestartRequired, .. issues.Where(issue => !IsMissingPart(issue) && !IsHidingState(issue))],
        PendingRestart.Recommended => InRowOrder([.. issues, WindowsRestartRecommended]),
        _ => issues,
    };

    /// <summary>HidHide is installed in a version Pan can use: only then is Pan asked whether it can hide (#382, S1).</summary>
    public static bool HidHideUsable(IEnumerable<HealthIssue> issues) => !issues.Any(issue => IsMissingPart(issue) && issue.Id.StartsWith("hidhide-", StringComparison.Ordinal));

    /// <summary>"Treiber installieren" waits while a controller's firmware is flashed (#407): the install restarts the puck and controllers.</summary>
    public static HealthIssue FirmwareUpdateRuns { get; } = new("install-firmware-update", HealthSeverity.Info, ControllerSupportTexts.FirmwareUpdateRunsTitle,
        ControllerSupportTexts.FirmwareUpdateRunsText,
        Summary: ControllerSupportTexts.FirmwareUpdateRunsSubtitle);

    /// <summary>
    /// An SISR runs while Pan is on (#214 decision 5): both take the Steam Controller, so games get input twice or not at
    /// all. Shown also while the group "SISR" is hidden; Arcadia doesn't stop an SISR the user started.
    /// </summary>
    public static HealthIssue SisrRunning { get; } = new("sisr-running", HealthSeverity.Warning, ControllerSupportTexts.SisrRunningTitle,
        ControllerSupportTexts.SisrRunningText,
        Summary: ControllerSupportTexts.SisrRunningSubtitle);

    /// <summary>True if <paramref name="issue"/> says a part is missing or too old (not how a present part is set).</summary>
    public static bool IsMissingPart(HealthIssue issue) => MissingIds.Contains(issue.Id);

    /// <summary>The names of the missing parts among <paramref name="issues"/>, in row order.</summary>
    public static IReadOnlyList<string> MissingNames(IEnumerable<HealthIssue> issues)
    {
        var missing = issues.Where(IsMissingPart).ToList();
        return Parts.Where(p => missing.Any(i => i.Id.StartsWith(p.Prefix, StringComparison.Ordinal))).Select(p => p.Name).ToList();
    }

    /// <summary>Every part is missing: nothing of the emulation was ever set up.</summary>
    public static bool AllMissing(IEnumerable<HealthIssue> issues) => MissingNames(issues).Count == Parts.Length;

    /// <summary>The rows in the order of the parts (usbip, VIIPER, Pan, HidHide); anything else after them, as it came.</summary>
    public static IReadOnlyList<HealthIssue> InRowOrder(IEnumerable<HealthIssue> issues) =>
        issues.OrderBy(i => Array.FindIndex(Parts, p => i.Id.StartsWith(p.Prefix, StringComparison.Ordinal)) is var rank and >= 0 ? rank : Parts.Length).ToList();

    private static readonly string[] HidingStateIds = ["hidhide-inactive", "hidhide-interface", "hidhide-inverse", "hidhide-cloaking-off", "hidhide-busy"];

    /// <summary>A row from <see cref="FromHidingCheck"/>: asked while the settings are open, and only valid then.</summary>
    public static bool IsHidingState(HealthIssue issue) => HidingStateIds.Contains(issue.Id);

    /// <summary>Looked at only while the settings are open (Pan's pipe, the process list), so a closed SISR doesn't stay in the header.</summary>
    public static bool IsSettingsOnly(HealthIssue issue) => IsHidingState(issue) || issue.Id == SisrRunning.Id;

    /// <summary>
    /// The row for Pan's answer whether it could hide the Steam Controller now; null when it could or didn't answer.
    /// Only asked while HidHide is installed in a supported version, so "missing" here means its driver doesn't run.
    /// "Neu installieren" only over the very version it installs (<paramref name="installed"/>): over another it would
    /// be an up- or downgrade nobody asked for.
    /// </summary>
    public static HealthIssue? FromHidingCheck(PanReason? reason, Version? installed = null) => reason switch
    {
        PanReason.HidHideMissing => new("hidhide-inactive", HealthSeverity.Warning, ControllerSupportTexts.HidHideInactiveTitle,
            ControllerSupportTexts.HidHideInactiveText),
        PanReason.HidHideUnsupported => new("hidhide-interface", HealthSeverity.Warning, ControllerSupportTexts.HidHideUnexpectedAnswer,
            ControllerSupportTexts.HidHideUnknownRequestText(HidHideRelease.Version),
            installed is { } version && HidHideRelease.IsPinnedDriver(version) ? HealthFix.ReinstallHidHide : HealthFix.None,
            installed is { } pinned && HidHideRelease.IsPinnedDriver(pinned) ? ControllerSupportTexts.Reinstall : null,
            Summary: ControllerSupportTexts.HidHideReinstallHelps(HidHideRelease.Version)),
        PanReason.HidHideInverseMode => new("hidhide-inverse", HealthSeverity.Warning, ControllerSupportTexts.HidHideInverseTitle,
            ControllerSupportTexts.HidHideInverseText),
        PanReason.HidHideCloakingOffWithForeignEntries => new("hidhide-cloaking-off", HealthSeverity.Warning, ControllerSupportTexts.HidHideHidingOffTitle,
            ControllerSupportTexts.HidHideHidingOffText),
        PanReason.HidHideBusy => new("hidhide-busy", HealthSeverity.Warning, ControllerSupportTexts.HidHideConfigOpenTitle,
            ControllerSupportTexts.HidHideConfigOpenText),
        _ => null,
    };
}
