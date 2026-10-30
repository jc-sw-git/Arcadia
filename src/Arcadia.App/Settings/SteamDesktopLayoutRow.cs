using Arcadia.ControllerSupport;

namespace Arcadia.App.Settings;

/// <summary>
/// "Steams Desktop-Belegung ignorieren" on Steam's launcher page, group "Desktop-Steuerung" (#315): one setting for every
/// controller, Pan on or off. It concerns Steam the program, which runs also while its launcher isn't active, so the row
/// is there either way. Saved at once and applies at once (the filter and the copy read it on every tick or report).
/// </summary>
public sealed class SteamDesktopLayoutRowViewModel : SwitchRowViewModel
{
    public const string RowKey = "steam-desktop-layout";

    public const string GroupName = "DESKTOP-STEUERUNG";

    public const string IgnoredSubtitle = "Steams Desktop-Steuerung wird momentan ignoriert";

    public const string ActiveSubtitle = "Steams Desktop-Steuerung ist momentan aktiviert";

    private readonly SettingsChanges _changes;

    public SteamDesktopLayoutRowViewModel(SettingsChanges changes)
        : base(RowKey, "Steams Desktop-Belegung ignorieren", () => changes.Current.IgnoreSteamDesktopLayout,
            value => changes.Apply(s => s with { IgnoreSteamDesktopLayout = value }, "Steams Desktop-Belegung ignorieren"))
    {
        _changes = changes;
        Group = GroupName;
        Info =
        [
            new InfoText("Steam bietet allen Controllern die Möglichkeit den Desktop zu steuern; zum Beispiel wird bei gedrückter Guide-Taste ([Guide]) mit dem rechten Stick ([RS]) der Mauszeiger bewegt oder mit ⧉ + ≡ ein Alt+Tab ausgelöst."),
            new InfoText("Diese Steam-Funktion kann in manchen Fällen bestimmte Arcadia-Funktionen wie den Mausmodus oder „Zurück zu Arcadia“ beeinträchtigen."),
            new InfoNote("Beim Steam Controller können darüber hinaus (wenn er im Aktionsset „Desktop“ betrieben wird) Eingaben in Arcadia doppelt ausgeführt werden – wie zum Beispiel die Navigation in der Bibliothek oder ein Druck auf [A] löst sowohl [A] als auch Enter aus. Es wird daher empfohlen den Steam Controller im Aktionsset \"Gamepad\" zu betreiben, wenn \"Steams Desktop-Belegung ignorieren\" ausgeschaltet ist.", HealthSeverity.Info) { IsBold = true },
            new InfoText("Ist „Steams Desktop-Belegung ignorieren“ eingeschaltet, wird diese Funktion von Steam von Arcadia unterbunden."),
            new InfoText("Um den Wechsel des Aktionssets (⧉ + ≡) für den Steam Controller zu unterbinden, muss zusätzlich auch der Controller-Wächter (Pan) aktiviert sein: Einstellungen → Controller → Pan."),
        ];
        ShowState();
    }

    protected override void OnValueShown()
    {
        base.OnValueShown();
        ShowState();
    }

    private void ShowState() => Subtitle = _changes.Current.IgnoreSteamDesktopLayout ? IgnoredSubtitle : ActiveSubtitle;
}
