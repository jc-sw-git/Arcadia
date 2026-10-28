using Arcadia.ControllerSupport;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "SISR" in the controller settings (#214, artboard T214Settings): "SISR entfernen" and the indented "SISR
/// behalten". Shown while SISR or its Steam marker is there and the player didn't choose to keep it.
/// </summary>
public sealed class SisrRows
{
    public const string Group = "SISR";
    public const string RemoveKey = "sisr-remove";
    public const string KeepKey = "sisr-keep";

    private readonly ControllerSupportService _support;

    /// <param name="remove">[Entfernen]: the settings ask about Steam first, if they need to.</param>
    /// <param name="keep">[Ausblenden]: hides the group; SISR stays as it is.</param>
    public SisrRows(ControllerSupportService support, Action remove, Action keep)
    {
        _support = support;
        Remove = new ActionRowViewModel(RemoveKey, "SISR entfernen", remove) { Group = Group, ActionText = "Entfernen" };
        Keep = new ActionRowViewModel(KeepKey, "SISR behalten", keep)
        {
            Group = Group,
            Indent = 1,
            ActionText = "Ausblenden",
            Subtitle = "Blendet diesen Bereich aus, SISR bleibt installiert",
            Info =
            [
                new InfoText("Nutzt du SISR für andere Programme, blende diesen Bereich aus. SISR, sein Eintrag in Steam und Steams Fernsteuerung bleiben dann, wie sie sind."),
                new InfoText("Läuft SISR, während Pan an ist, warnt Arcadia trotzdem unter Pan."),
                new InfoFootnote("Der Bereich erscheint wieder nach „Auf Werkseinstellungen zurücksetzen“. Beim Deinstallieren von Arcadia fragt Arcadia weiterhin, ob SISR mit entfernt werden soll."),
            ],
        };
    }

    public ActionRowViewModel Remove { get; }

    public ActionRowViewModel Keep { get; }

    /// <summary>The rows of the group, or none while it is hidden; call on every change of the support or the settings.</summary>
    public IReadOnlyList<SettingsRowViewModel> Build()
    {
        if (!_support.IsSupportedPlatform || !_support.ShowsSisrGroup || _support.SisrFound is not { } found)
        {
            return [];
        }

        var cause = _support.SisrRemovalCause;
        var busy = _support.BusyText is not null;
        Remove.Subtitle = cause is not null ? "Nicht alles ließ sich entfernen"
            : found.Program ? "Pan ersetzt SISR. Arcadia braucht es nicht mehr."
            : "Übrig ist noch ein Eintrag in Steam";
        Remove.Severity = cause is null ? null : HealthSeverity.Warning;
        Remove.IsEnabled = !busy;
        Keep.IsEnabled = !busy;
        Remove.Info =
        [
            new InfoText("Früher hat Arcadia den Steam Controller mit SISR in Spielen außerhalb von Steam nutzbar gemacht. Das übernimmt jetzt Pan, Arcadia startet SISR nicht mehr. Brauchst du SISR für ein anderes Programm, kannst du es behalten."),
            new InfoNote("Nutze SISR und Pan nicht gleichzeitig. Beide greifen auf den Steam Controller zu, Spiele bekommen dann Eingaben doppelt oder gar nicht."),
            .. cause is not null ? [new InfoNote(cause)] : Array.Empty<InfoBlock>(),
            new InfoSection("WAS ENTFERNT WIRD", [.. Removed(found).Select(item => new InfoBullet(item))]),
            .. found.HasMarker
                ? [new InfoText("Den Eintrag in Steam kann Arcadia nur ändern, solange Steam geschlossen ist. Steam wird dafür kurz beendet und danach wieder gestartet.")]
                : Array.Empty<InfoBlock>(),
            new InfoSection("BLEIBT ERHALTEN", [new InfoText("Der Modus pro Spiel („Arcadia“ oder „Steam-Eingabe“) und der Treiber usbip-win2, den Pan braucht.")]),
            new InfoFootnote("Ist alles entfernt, verschwindet dieser Bereich."),
        ];
        return [Remove, Keep];
    }

    private static IEnumerable<string> Removed(SisrFound found)
    {
        if (found.Program)
        {
            yield return "SISR mit seinen Dateien und Einstellungen";
        }

        if (found.HasMarker)
        {
            yield return "Der Eintrag „SISR Marker“ in deiner Steam-Bibliothek";
        }

        if (found.CefFlag)
        {
            yield return "Steams Fernsteuerung (CEF-Debugging), die SISR eingeschaltet hat";
        }
    }
}
