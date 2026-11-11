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

    public static string RowTitle => AppTexts.SteamDesktopTitle;

    public static string IgnoredSubtitle => AppTexts.SteamDesktopIgnored;

    public static string AllowedSubtitle => AppTexts.SteamDesktopAllowed;

    private readonly SettingsChanges _changes;

    public SteamDesktopLayoutRowViewModel(SettingsChanges changes)
        : base(RowKey, RowTitle, () => !changes.Current.IgnoreSteamDesktopLayout,
            allowed => changes.Apply(s => s with { IgnoreSteamDesktopLayout = !allowed }, RowTitle))
    {
        _changes = changes;
        Group = GroupName;
        Info =
        [
            new InfoText(AppTexts.SteamDesktopInfo1),
            new InfoText(AppTexts.SteamDesktopInfo2),
            new InfoText(AppTexts.SteamDesktopInfo3),
            new InfoNote(AppTexts.SteamDesktopInfo4, HealthSeverity.Info) { IsBold = true, Symbols = ButtonSymbols.SteamController },
            new InfoText(AppTexts.SteamDesktopInfo5),
            new InfoText(AppTexts.SteamDesktopInfo6) { Symbols = ButtonSymbols.SteamController },
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
