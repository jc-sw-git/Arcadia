using Arcadia.App.Services;
using Arcadia.Core.Settings;

namespace Arcadia.App.Settings;

/// <summary>One line of "So funktioniert es": a button and what it does in the mouse mode.</summary>
public sealed record MouseModeHelpRow(string Input, string Effect);

/// <summary>
/// The rows of the mouse mode (#64) in the category "Controller": how it is switched on and everything that depends on
/// it, in one group. Every change is saved at once and applies at once.
/// </summary>
public sealed class MouseModeRows
{
    private static string Group => AppTexts.MouseModeGroup;

    /// <summary>The values of "Mausmodus", in the order of <see cref="MouseModeActivation"/>.</summary>
    private static readonly string[] ActivationNames = [AppTexts.Off, AppTexts.MouseModeManual, AppTexts.MouseModeAutomatic];

    /// <summary>The values of "Beenden des Mausmodus in Spielen erzwingen", in the order of <see cref="MouseModeInGames"/>.</summary>
    private static readonly string[] InGamesNames = [AppTexts.No, AppTexts.Yes, AppTexts.MouseModeIndividual];

    private readonly SettingsChanges _changes;
    private readonly ChoiceRowViewModel _activation;
    private readonly ChoiceRowViewModel _hold;
    private readonly ChoiceRowViewModel _speed;
    private readonly SwitchRowViewModel _forDialogs;
    private readonly SwitchRowViewModel _endInFront;
    private readonly ChoiceRowViewModel _inGames;
    private readonly ChoiceRowViewModel _perGameDefault;

    public MouseModeRows(SettingsChanges changes)
    {
        _changes = changes;
        _activation = new ChoiceRowViewModel(
            "mouse-mode",
            AppTexts.MouseMode,
            () => (int)Current.Activation,
            value => Change(s => s with { Activation = (MouseModeActivation)value }),
            value => ActivationNames[value],
            (int)MouseModeActivation.Off,
            (int)MouseModeActivation.Automatic)
        {
            Group = Group,
            Subtitle = AppTexts.MouseModeSubtitle,
        };
        _hold = new ChoiceRowViewModel(
            "mouse-mode-hold",
            AppTexts.HoldTime,
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            ChoiceRowViewModel.Seconds,
            MouseModeSettings.Min,
            MouseModeSettings.Max)
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.MouseModeHoldSubtitle,
            Info = [new InfoText(AppTexts.MouseModeHoldSubtitle)],
        };
        _speed = new ChoiceRowViewModel(
            "mouse-mode-speed",
            AppTexts.PointerSpeed,
            () => Current.PointerSpeed,
            value => Change(s => s with { PointerSpeed = value }),
            value => value.ToString(Formatting.Culture),
            MouseModeSettings.Min,
            MouseModeSettings.Max)
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.MouseModeSpeedSubtitle,
            Info = [new InfoText(AppTexts.MouseModeSpeedInfo)],
        };
        _forDialogs = new SwitchRowViewModel(
            "mouse-mode-for-dialogs",
            AppTexts.MouseModeForDialogsTitle,
            () => Current.ForDialogs,
            value => Change(s => s with { ForDialogs = value }))
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.MouseModeForDialogsSubtitle,
            Info = [new InfoText(AppTexts.MouseModeForDialogsInfo)],
        };
        _endInFront = new SwitchRowViewModel(
            "mouse-mode-end-in-front",
            AppTexts.MouseModeEndInFrontTitle,
            () => Current.EndWhenArcadiaInFront,
            value => Change(s => s with { EndWhenArcadiaInFront = value }))
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.MouseModeOnlyManual,
            Info = [new InfoText(AppTexts.MouseModeEndInFrontInfo)],
        };
        _inGames = new ChoiceRowViewModel(
            "mouse-mode-in-games",
            AppTexts.MouseModeForceEndInGamesTitle,
            () => (int)Current.InGames,
            value => Change(s => s with { InGames = (MouseModeInGames)value }),
            value => InGamesNames[value],
            (int)MouseModeInGames.Allowed,
            (int)MouseModeInGames.PerGame)
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.MouseModeOnlyManual,
        };
        _perGameDefault = new ChoiceRowViewModel(
            "mouse-mode-per-game-default",
            AppTexts.MouseModeDefaultTitle,
            () => Current.PerGameDefaultAllowed ? 0 : 1,
            value => Change(s => s with { PerGameDefaultAllowed = value == 0 }),
            value => value == 0 ? GameMouseModes.Allowed : GameMouseModes.Blocked,
            0,
            1)
        {
            Group = Group,
            Indent = 2,
            Subtitle = AppTexts.MouseModeDefaultSubtitle,
            Info = [new InfoText(AppTexts.MouseModeDefaultInfo)],
        };
        Update();
    }

    /// <summary>The mouse mode's buttons, as <see cref="Arcadia.Input.MouseMode"/> maps them.</summary>
    public static IReadOnlyList<MouseModeHelpRow> HelpRows { get; } =
    [
        new("LS", AppTexts.MousePointer),
        new("L3", AppTexts.MouseModeHelpL3),
        new(AppTexts.MouseModeHelpRtOrA, AppTexts.MouseModeHelpLeftClick),
        new(AppTexts.MouseModeHelpLtOrX, AppTexts.MouseModeHelpRightClick),
        new("RS", AppTexts.MouseModeHelpWheel),
        new("D-Pad", AppTexts.MouseModeHelpArrowKeys),
        new("B", "Esc"),
        new("LB / RB", AppTexts.MouseModeHelpShiftTab),
        new("≡", "Enter"),
        new("Y", AppTexts.MouseModeHelpKeyboard),
        new(AppTexts.MouseModeHelpHoldView, AppTexts.MouseModeHelpBackToArcadia),
        new(AppTexts.MouseModeHelpHoldViewMenu, AppTexts.MouseModeHelpBackToController),
    ];

    /// <summary>Where the mouse mode can't help, so nobody wonders why.</summary>
    public static IReadOnlyList<string> HelpLimits { get; } =
    [
        AppTexts.MouseModeLimitUac,
        AppTexts.MouseModeLimitAdmin,
        AppTexts.MouseModeLimitFullscreen,
    ];

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [_activation, _hold, _speed, _forDialogs, _endInFront, _inGames, _perGameDefault];

    private MouseModeSettings Current => _changes.Current.MouseMode;

    /// <summary>Below the description of "Mausmodus", in a box of its own (owner, 2026-10-22).</summary>
    private static readonly InfoSection Help = new(AppTexts.MouseModeHowItWorks,
    [
        new InfoText(AppTexts.MouseModeHelp),
        .. HelpRows.Select(r => new InfoKey(r.Input, r.Effect)),
        new InfoHeading(AppTexts.MouseModeLimitsHeading),
        .. HelpLimits.Select(l => new InfoBullet(l)),
    ]);

    /// <summary>What the chosen way of switching does, under "Mausmodus".</summary>
    private string ActivationText => Current.Activation switch
    {
        MouseModeActivation.Off => AppTexts.MouseModeActivationOff,
        MouseModeActivation.Automatic => AppTexts.MouseModeActivationAutomatic,
        _ => AppTexts.MouseModeActivationManual,
    };

    /// <summary>What the chosen value of "Beenden des Mausmodus in Spielen erzwingen" does (owner's wording, 2026-10-21).</summary>
    private string InGamesText => Current.InGames switch
    {
        MouseModeInGames.Blocked => AppTexts.MouseModeInGamesEnd,
        MouseModeInGames.PerGame => AppTexts.MouseModeInGamesPerGame,
        _ => AppTexts.MouseModeInGamesAllow,
    };

    private void Change(Func<MouseModeSettings, MouseModeSettings> change)
    {
        _changes.Apply(s => s with { MouseMode = change(s.MouseMode) }, AppTexts.MouseMode);
        Update();
    }

    /// <summary>"Aus" leaves every other row greyed out, with its value kept; some only count for "Von Hand".</summary>
    private void Update()
    {
        var enabled = Current.Activation != MouseModeActivation.Off;
        var manual = Current.Activation == MouseModeActivation.Manual;
        _hold.IsDimmed = !enabled;
        _speed.IsDimmed = !enabled;
        _forDialogs.IsDimmed = !enabled;
        _endInFront.IsDimmed = !manual;
        _inGames.IsDimmed = !manual;
        _perGameDefault.IsDimmed = !manual || Current.InGames != MouseModeInGames.PerGame;
        _activation.Info = [new InfoText(ActivationText), Help];
        _inGames.Info = [new InfoText(InGamesText)];
    }
}

/// <summary>"Zurück zu Arcadia" (#64): View held brings Arcadia back, with or without the mouse mode; its own group.</summary>
public sealed class ReturnWithViewRows
{
    private static string Group => AppTexts.MouseModeBackToArcadiaGroup;

    private readonly SettingsChanges _changes;
    private readonly SwitchRowViewModel _enabled;
    private readonly ChoiceRowViewModel _hold;

    public ReturnWithViewRows(SettingsChanges changes)
    {
        _changes = changes;
        _enabled = new SwitchRowViewModel("return-with-view", AppTexts.MouseModeBackToArcadiaTitle, () => Current.Enabled, value => Change(s => s with { Enabled = value }))
        {
            Group = Group,
            Subtitle = AppTexts.MouseModeBackToArcadiaSubtitle,
            Info = [new InfoText(AppTexts.MouseModeBackToArcadiaInfo)],
        };
        _hold = new ChoiceRowViewModel(
            "return-with-view-hold",
            AppTexts.HoldTime,
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            ChoiceRowViewModel.Seconds,
            ReturnWithViewSettings.Min,
            ReturnWithViewSettings.Max)
        {
            Group = Group,
            Indent = 1,
            Subtitle = AppTexts.MouseModeBackHoldSubtitle,
            Info = [new InfoText(AppTexts.MouseModeBackHoldInfo)],
        };
        Update();
    }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsRowViewModel> Rows => [_enabled, _hold];

    private ReturnWithViewSettings Current => _changes.Current.ReturnWithView;

    private void Change(Func<ReturnWithViewSettings, ReturnWithViewSettings> change)
    {
        _changes.Apply(s => s with { ReturnWithView = change(s.ReturnWithView) }, AppTexts.BackToArcadia);
        Update();
    }

    private void Update() => _hold.IsDimmed = !Current.Enabled;
}

/// <summary>The values of "Individueller Mausmodus" on a game (owner, 2026-10-20).</summary>
public static class GameMouseModes
{
    public static string Allowed => AppTexts.GameMouseAllowed;
    public static string Blocked => AppTexts.GameMouseBlocked;

    /// <summary>"Standard" names what it means now, e.g. "Standard (erlaubt)" (owner, 2026-10-21).</summary>
    public static string Of(bool? choice, bool defaultAllowed) => choice switch
    {
        true => Allowed,
        false => Blocked,
        null => defaultAllowed ? AppTexts.MouseModeDefaultAllowed : AppTexts.MouseModeDefaultDisabled,
    };

    /// <summary>Standard → Erlaubt → Deaktiviert → Standard.</summary>
    public static bool? Next(bool? choice) => choice switch
    {
        null => true,
        true => false,
        false => null,
    };
}
