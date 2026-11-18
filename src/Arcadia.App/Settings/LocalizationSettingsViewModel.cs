using Arcadia.App.Services;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Lokalisierung" (#14, #553, #554) between "Datenquellen" and "Über Arcadia": the UI language
/// (<see cref="UiLanguageRows"/>), the on-screen keyboard's default layout with one switch per layout for R3's quick
/// selection, and the clock. Every change is saved at once; all but the language apply at once too.
/// </summary>
public sealed class LocalizationSettingsViewModel : SettingsCategoryViewModel
{
    public const string DefaultLayoutKey = "localization-default-layout";
    public const string ClockKey = "localization-clock";

    /// <summary>The key of a layout's switch for the quick selection.</summary>
    public static string QuickLayoutKey(KeyboardLayout layout) => "localization-quick-" + layout.Id;

    private static readonly ClockFormat[] Clocks = [ClockFormat.System, ClockFormat.TwentyFourHours, ClockFormat.TwelveHours];

    private readonly SettingsChanges _changes;
    private readonly Action _clockChanged;
    private readonly ChoiceRowViewModel _defaultLayout;
    private readonly List<QuickLayoutRowViewModel> _quick;
    private readonly UiLanguageRows _language;

    /// <param name="clockChanged">"Uhrzeit" changed: every time shown from now on follows it; the header clock at once.</param>
    /// <param name="language">What "Sprache anwenden" waits for, and how it restarts Arcadia.</param>
    public LocalizationSettingsViewModel(SettingsChanges changes, Action clockChanged, LanguageRestart language)
        : base(AppTexts.LocalizationTitle, "M3 12a9 9 0 1 0 18 0a9 9 0 1 0 -18 0z M3.6 9h16.8 M3.6 15h16.8 M11.5 3a17 17 0 0 0 0 18 M12.5 3a17 17 0 0 1 0 18")
    {
        _changes = changes;
        _clockChanged = clockChanged;
        _language = new UiLanguageRows(changes, language);
        var keyboard = AppTexts.LocalizationKeyboardGroup;
        _defaultLayout = new ChoiceRowViewModel(
            DefaultLayoutKey,
            AppTexts.LocalizationDefaultLayout,
            () => IndexOf(Layouts.Default),
            index => ChooseDefault(KeyboardLayout.All[index]),
            index => KeyboardLayout.All[index].Name,
            0,
            KeyboardLayout.All.Count - 1)
        {
            Group = keyboard,
            Subtitle = AppTexts.LocalizationDefaultLayoutSubtitle,
            ValueInfos = [.. KeyboardLayout.All.Select(layout => AppTexts.LocalizationDefaultLayoutValue(kind: layout.Kind))],
            Info = [new InfoText(AppTexts.LocalizationDefaultLayoutInfo) { KeepsButtons = true }],
        };
        _quick = [.. KeyboardLayout.All.Select(layout => new QuickLayoutRowViewModel(this, layout) { Group = keyboard, Indent = 1 })];
        var clock = new ChoiceRowViewModel(
            ClockKey,
            AppTexts.LocalizationClock,
            () => Array.IndexOf(Clocks, _changes.Current.Localization.Clock),
            index => ChooseClock(Clocks[index]),
            index => ClockName(Clocks[index]),
            0,
            Clocks.Length - 1)
        {
            Group = AppTexts.LocalizationClockGroup,
            Subtitle = AppTexts.LocalizationClockSubtitle,
            ValueInfos = [AppTexts.LocalizationClockSystemInfo, AppTexts.LocalizationClock24Info, AppTexts.LocalizationClock12Info],
            Info = [new InfoText(AppTexts.LocalizationClockInfo)],
        };
        RefreshQuickRows();
        SetRows([_language.LanguageRow, _language.ApplyRow, _defaultLayout, .. _quick, clock]);
    }

    /// <summary>A game, the driver installation, an update or a reload began or ended: "Sprache anwenden" follows. UI thread.</summary>
    public void RestartStateChanged() => _language.Refresh();

    /// <summary>The layouts as the keyboards offer them now, with a default taken from the system when none is saved.</summary>
    private KeyboardLayoutChoice Layouts => SystemKeyboardLayout.Choice(_changes.Current.Localization);

    /// <summary>A layout as its switch names it: "Deutsch · QWERTZ". The value pill of "Standard-Layout" has room only for the name.</summary>
    private static string NameOf(KeyboardLayout layout) => AppTexts.LocalizationLayoutName(name: layout.Name, kind: layout.Kind);

    private static int IndexOf(KeyboardLayout layout) => KeyboardLayout.All.ToList().IndexOf(layout);

    private static string ClockName(ClockFormat clock) => clock switch
    {
        ClockFormat.TwentyFourHours => AppTexts.LocalizationClock24,
        ClockFormat.TwelveHours => AppTexts.LocalizationClock12,
        _ => AppTexts.LocalizationClockSystem,
    };

    private void ChooseDefault(KeyboardLayout layout)
    {
        var before = Layouts.Default;
        _changes.Apply(s => s with { Localization = s.Localization.WithDefaultLayout(layout, before) }, AppTexts.LocalizationDefaultLayout);
        RefreshQuickRows();
    }

    private void ChooseClock(ClockFormat clock)
    {
        _changes.Apply(s => s with { Localization = s.Localization with { Clock = clock } }, AppTexts.LocalizationClock);
        _clockChanged();
    }

    private void SwitchQuick(KeyboardLayout layout, bool on) =>
        _changes.Apply(s => s with { Localization = s.Localization.WithQuickLayout(layout, on) }, AppTexts.LocalizationQuickLayout);

    private bool IsQuick(KeyboardLayout layout) => Layouts.Quick.Contains(layout);

    private bool IsDefault(KeyboardLayout layout) => Layouts.Default == layout;

    private void RefreshQuickRows()
    {
        foreach (var row in _quick)
        {
            row.ShowDefault();
        }
    }

    /// <summary>
    /// A layout's switch for the quick selection: the default layout's is on and greyed out, with the reason, and can't be
    /// switched off; it still takes the focus.
    /// </summary>
    private sealed class QuickLayoutRowViewModel : SwitchRowViewModel
    {
        private readonly LocalizationSettingsViewModel _category;
        private readonly KeyboardLayout _layout;

        public QuickLayoutRowViewModel(LocalizationSettingsViewModel category, KeyboardLayout layout)
            : base(QuickLayoutKey(layout), NameOf(layout), () => category.IsQuick(layout), on => category.SwitchQuick(layout, on))
        {
            _category = category;
            _layout = layout;
            Subtitle = AppTexts.LocalizationQuickLayoutSubtitle;
            ValueInfos = [AppTexts.LocalizationQuickLayoutOff, AppTexts.LocalizationQuickLayoutOn];
            Info = [new InfoText(AppTexts.LocalizationQuickLayoutInfo)];
            DimmedNote = AppTexts.LocalizationDefaultAlwaysIn;
        }

        // The default's switch can't change: the footer offers neither ◀ ▶ nor A on it.
        public override bool CanChange => !_category.IsDefault(_layout);

        public override bool CanActivate => CanChange;

        public override bool ShowsActivateHint => CanChange;

        /// <summary>The default may have moved to or from this layout.</summary>
        public void ShowDefault()
        {
            IsDimmed = _category.IsDefault(_layout);
            Refresh();
            NotifyActionsChanged();
        }

        protected override bool Allows(int value) => !_category.IsDefault(_layout);
    }
}
