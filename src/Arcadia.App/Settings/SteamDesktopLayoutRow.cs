using Arcadia.ControllerSupport;
using Arcadia.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// "Steams Desktop-Steuerung zulassen" on Steam's launcher page, group "Desktop-Steuerung" (#315): one setting for every
/// controller, Pan on or off, off by default. It shows the stored <c>IgnoreSteamDesktopLayout</c> the other way round, so
/// settings saved before keep their meaning. It concerns Steam the program, which runs also while its launcher isn't active, so the row
/// is there either way. Saved at once and applies at once (the filter and the copy read it on every tick or report).
/// </summary>
public sealed class SteamDesktopLayoutRowViewModel : SwitchRowViewModel
{
    public const string RowKey = "steam-desktop-layout";

    public const string GroupName = "DESKTOP-STEUERUNG";

    public const string RowTitle = "Steams Desktop-Steuerung zulassen";

    public const string IgnoredSubtitle = "Steams Desktop-Steuerung wird momentan ignoriert";

    public const string AllowedSubtitle = "Steams Desktop-Steuerung ist momentan aktiviert";

    private readonly SettingsChanges _changes;

    public SteamDesktopLayoutRowViewModel(SettingsChanges changes)
        : base(RowKey, RowTitle, () => !changes.Current.IgnoreSteamDesktopLayout,
            allowed => changes.Apply(s => s with { IgnoreSteamDesktopLayout = !allowed }, RowTitle))
    {
        _changes = changes;
        Group = GroupName;
        Info =
        [
            new InfoText("Steam bietet allen Controllern die Möglichkeit den Desktop zu steuern; zum Beispiel wird bei gedrückter Guide-Taste ([Guide]) mit dem rechten Stick ([RS]) der Mauszeiger bewegt oder mit ⧉ + ≡ ein Alt+Tab ausgelöst."),
            new InfoText("Das lohnt sich vor allem, wenn du dir in Steam eine eigene Desktop-Belegung eingerichtet hast, etwa mit Tastenkürzeln, Makros oder Gyro-Steuerung, und sie auch neben Arcadia nutzen willst. Für Maus, Tastatur und Bildschirmtastatur genügt sonst Arcadias Mausmodus."),
            new InfoText("Diese Steam-Funktion kann in manchen Fällen bestimmte Arcadia-Funktionen wie den Mausmodus oder „Zurück zu Arcadia“ beeinträchtigen."),
            new InfoNote("Beim Steam Controller können darüber hinaus (wenn er im Aktionsset „Desktop“ betrieben wird) Eingaben in Arcadia doppelt ausgeführt werden – wie zum Beispiel die Navigation in der Bibliothek oder ein Druck auf [A] löst sowohl [A] als auch Enter aus. Es wird daher empfohlen den Steam Controller im Aktionsset \"Gamepad\" zu betreiben, wenn \"Steams Desktop-Steuerung zulassen\" eingeschaltet ist.", HealthSeverity.Info) { IsBold = true, Symbols = ButtonSymbols.SteamController },
            new InfoText("Ist „Steams Desktop-Steuerung zulassen“ ausgeschaltet, wird diese Funktion von Steam von Arcadia unterbunden."),
            new InfoText("Um den Wechsel des Aktionssets (⧉ + ≡) für den Steam Controller zu unterbinden, muss zusätzlich auch der Controller-Wächter (Pan) aktiviert sein: Einstellungen → Controller → Pan.") { Symbols = ButtonSymbols.SteamController },
        ];
        ShowState();
    }

    protected override void OnValueShown()
    {
        base.OnValueShown();
        ShowState();
    }

    private void ShowState() => Subtitle = _changes.Current.IgnoreSteamDesktopLayout ? IgnoredSubtitle : AllowedSubtitle;
}
