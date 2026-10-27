using Arcadia.Pan;

namespace Arcadia.ControllerSupport;

/// <summary>
/// The parts the controller emulation needs and the rows that say one is missing or can't be used (#243 part B2):
/// usbip-win2, VIIPER, Pan and HidHide, in that order below "Steam Controller Kompatibilität". While a part is
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
        ("usbip-", "usbip-Treiber"), ("viiper-", "VIIPER"), ("pan-", "Wächterdienst Pan"), ("hidhide-", "HidHide"),
    ];

    public static HealthIssue ViiperMissing { get; } = new("viiper-missing", HealthSeverity.Warning, "VIIPER fehlt",
        "Arcadia erzeugt mit VIIPER den virtuellen Controller. Bis auf Weiteres kommt VIIPER mit den Dateien von SISR; „Installieren“ lädt sie herunter, ohne Administratorrechte.",
        HealthFix.InstallDrivers, "Installieren");

    /// <summary>
    /// Pan is installed but its last run ended with an error: it crashed, or its data folder was untrusted and couldn't be
    /// made anew (owner's B2 test, 2026-10-27). Its install makes the folder anew, so the fix is the same as for a
    /// missing Pan.
    /// </summary>
    public static HealthIssue PanFailing { get; } = new("pan-failing", HealthSeverity.Warning, "Wächterdienst Pan startet nicht",
        $"Pan beendet sich gleich nach dem Start. „Neu einrichten“ installiert Pan neu und legt seinen Ordner neu an; warum es sich beendet hat, steht in {PanPaths.LogPath}. Die Installation fragt einmalig nach Administratorrechten.",
        HealthFix.InstallDrivers, "Neu einrichten", Summary: "„Neu einrichten“ behebt das meist.");

    /// <summary>
    /// HidHide was uninstalled and is still there until Windows restarts (owner's B2 test, 2026-10-27). No "Installieren":
    /// its setup fails while the old service waits for deletion.
    /// </summary>
    public static HealthIssue HidHideRemovalPending { get; } = new("hidhide-removal-pending", HealthSeverity.Warning, "HidHide: Neustart ausstehend",
        "HidHide wurde deinstalliert, Windows entfernt es aber erst beim nächsten Neustart des PCs. Danach lässt es sich hier wieder installieren.",
        Summary: "Windows entfernt HidHide beim nächsten Neustart.");

    /// <summary>
    /// The usbip driver was updated and Windows wants a restart; with SISR gone (#213) there is nothing to restart in
    /// Arcadia, so the row only says so (owner's B2 test, 2026-10-27: "SISR neu starten" was left over).
    /// </summary>
    public static HealthIssue RebootPending { get; } = new("reboot-pending", HealthSeverity.Warning, "Windows-Neustart ausstehend",
        "Der usbip-Treiber wurde aktualisiert und ist erst nach einem Neustart des PCs ganz aktiv. Bis dahin kann der Steam Controller in Spielen kurz aussetzen; Arcadia verbindet ihn dann selbst neu.",
        Summary: "Bis zum Neustart kann der Steam Controller in Spielen aussetzen.");

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

    /// <summary>
    /// The row for Pan's answer whether it could hide the Steam Controller now; null when it could or didn't answer.
    /// Only asked while HidHide is installed in a supported version, so "missing" here means its driver doesn't run.
    /// </summary>
    public static HealthIssue? FromHidingCheck(PanReason? reason) => reason switch
    {
        PanReason.HidHideMissing => new("hidhide-inactive", HealthSeverity.Warning, "HidHide ist nicht aktiv",
            "HidHide ist installiert, sein Treiber läuft aber noch nicht. Meist hilft ein Neustart des PCs."),
        PanReason.HidHideUnsupported => new("hidhide-interface", HealthSeverity.Warning, "HidHide antwortet unerwartet",
            $"Der HidHide-Treiber kennt eine Anfrage nicht, die Arcadia braucht. HidHide {HidHideRelease.Version} neu installieren hilft: {HidHideRelease.ManualInstallUrl}"),
        PanReason.HidHideInverseMode => new("hidhide-inverse", HealthSeverity.Warning, "HidHide: Liste ist umgekehrt",
            "In HidHide ist „Inverse application cloak“ eingeschaltet. So kann Arcadia den Steam Controller nicht verstecken. Schalte es in der HidHide-Konfiguration aus."),
        PanReason.HidHideCloakingOffWithForeignEntries => new("hidhide-cloaking-off", HealthSeverity.Warning, "HidHide: Verstecken ist aus",
            "In HidHide sind Geräte eingetragen, aber „Enable device hiding“ ist aus. Arcadia müsste es einschalten und würde diese Geräte dann mit verstecken, deshalb tut es das nicht. Schalte es in der HidHide-Konfiguration ein oder trage die Geräte dort aus."),
        PanReason.HidHideBusy => new("hidhide-busy", HealthSeverity.Warning, "HidHide-Konfiguration ist geöffnet",
            "Solange die HidHide-Konfiguration offen ist, kann Arcadia HidHide nicht nutzen. Bitte schließe sie."),
        _ => null,
    };
}
