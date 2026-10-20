using Arcadia.App.Services;
using Arcadia.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A setting from 1 to 9: left and right on the D-pad (or the arrows beside it) change it; A counts
/// up and starts again at 1 after 9, so it can be set with A alone.
/// </summary>
public sealed partial class SettingsStepperViewModel : SettingsItemViewModel
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;
    private readonly Func<int, string> _format;

    public SettingsStepperViewModel(Func<int> get, Action<int> set, Func<int, string> format)
        : base(static () => { })
    {
        _get = get;
        _set = set;
        _format = format;
        DecreaseCommand = new RelayCommand(() => Step(-1));
        IncreaseCommand = new RelayCommand(() => Step(1));
    }

    public string ValueText => _format(_get());

    public bool CanDecrease => _get() > MouseModeSettings.Min;

    public bool CanIncrease => _get() < MouseModeSettings.Max;

    public IRelayCommand DecreaseCommand { get; }

    public IRelayCommand IncreaseCommand { get; }

    public void Step(int delta) => Set(Math.Clamp(_get() + delta, MouseModeSettings.Min, MouseModeSettings.Max));

    public override void Activate() => Set(_get() >= MouseModeSettings.Max ? MouseModeSettings.Min : _get() + 1);

    private void Set(int value)
    {
        if (value == _get())
        {
            return;
        }

        _set(value);
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(CanDecrease));
        OnPropertyChanged(nameof(CanIncrease));
    }
}

/// <summary>One line of "So funktioniert's": a button and what it does in the mouse mode.</summary>
public sealed record MouseModeHelpRow(string Input, string Effect);

/// <summary>The "Mausmodus" box in the settings (#64); every change is saved at once and applies at once.</summary>
public sealed partial class MouseModeSettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;

    public MouseModeSettingsViewModel(SettingsService settings, Action openHelp)
    {
        _settings = settings;
        ComboItem = new SettingsItemViewModel(() => Change(s => s with { ComboEnabled = !s.ComboEnabled }));
        HoldItem = new SettingsStepperViewModel(
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            value => value == 1 ? "1 Sekunde" : $"{value} Sekunden");
        SpeedItem = new SettingsStepperViewModel(
            () => Current.PointerSpeed,
            value => Change(s => s with { PointerSpeed = value }),
            value => value.ToString(Formatting.Culture));
        EndInFrontItem = new SettingsItemViewModel(() => Change(s => s with { EndWhenArcadiaInFront = !s.EndWhenArcadiaInFront }));
        AutoStartItem = new SettingsItemViewModel(() => Change(s => s with { StartWhenArcadiaLosesFocus = !s.StartWhenArcadiaLosesFocus }));
        HelpItem = new SettingsItemViewModel(openHelp);
        ToggleComboCommand = new RelayCommand(ComboItem.Activate);
        ToggleEndInFrontCommand = new RelayCommand(EndInFrontItem.Activate);
        ToggleAutoStartCommand = new RelayCommand(AutoStartItem.Activate);
        HelpCommand = new RelayCommand(openHelp);
    }

    /// <summary>The mouse mode's buttons, as <see cref="Arcadia.Input.MouseMode"/> maps them.</summary>
    public static IReadOnlyList<MouseModeHelpRow> HelpRows { get; } =
    [
        new("Linker Stick", "Mauszeiger"),
        new("L3", "Antippen: langsamer Zeiger an/aus · Halten: langsam, solange gehalten"),
        new("RT oder A", "Linke Maustaste (gedrückt halten = ziehen)"),
        new("LT oder X", "Rechte Maustaste"),
        new("Rechter Stick", "Mausrad, auch seitwärts"),
        new("Steuerkreuz", "Pfeiltasten"),
        new("B", "Esc"),
        new("LB / RB", "Umschalt+Tab / Tab"),
        new("Menu ☰", "Enter"),
        new("View ⧉", "Holt Arcadia nach vorne"),
        new("Back + Start halten", "Zurück zum normalen Controller"),
    ];

    /// <summary>Where the mouse mode can't help, so nobody wonders why.</summary>
    public static IReadOnlyList<string> HelpLimits { get; } =
    [
        "Windows-Abfragen nach Administratorrechten (UAC) laufen auf einem geschützten Bildschirm, den keine Eingabe von Arcadia erreicht.",
        "Fenster von Programmen, die als Administrator laufen, lassen Eingaben eines Arcadia ohne Administratorrechte nicht durch.",
        "Über Spielen im exklusiven Vollbild zeigt Windows den Rand und den Hinweis nicht an; das Umschalten spürst du dann nur am Vibrieren.",
        "Im Mausmodus sieht ein Spiel im Vordergrund die Controller-Eingaben weiterhin.",
    ];

    public SettingsItemViewModel ComboItem { get; }

    public SettingsStepperViewModel HoldItem { get; }

    public SettingsStepperViewModel SpeedItem { get; }

    public SettingsItemViewModel EndInFrontItem { get; }

    public SettingsItemViewModel AutoStartItem { get; }

    /// <summary>"So funktioniert's": the buttons and the limits of the mouse mode.</summary>
    public SettingsItemViewModel HelpItem { get; }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsItemViewModel> Items => [ComboItem, HoldItem, SpeedItem, EndInFrontItem, AutoStartItem, HelpItem];

    public IRelayCommand ToggleComboCommand { get; }

    public IRelayCommand ToggleEndInFrontCommand { get; }

    public IRelayCommand ToggleAutoStartCommand { get; }

    public IRelayCommand HelpCommand { get; }

    public bool ComboEnabled => Current.ComboEnabled;

    public bool EndWhenArcadiaInFront => Current.EndWhenArcadiaInFront;

    public bool StartWhenArcadiaLosesFocus => Current.StartWhenArcadiaLosesFocus;

    /// <summary>Set when a change couldn't be saved; it still applies until Arcadia closes.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; private set; }

    private MouseModeSettings Current => _settings.Current.MouseMode;

    private void Change(Func<MouseModeSettings, MouseModeSettings> change)
    {
        try
        {
            _settings.Update(s => s with { MouseMode = change(s.MouseMode) });
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Mausmodus-Einstellung nicht gespeichert: {ex.Message}");
            SaveError = "Die Änderung konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.";
        }

        OnPropertyChanged(nameof(ComboEnabled));
        OnPropertyChanged(nameof(EndWhenArcadiaInFront));
        OnPropertyChanged(nameof(StartWhenArcadiaLosesFocus));
    }
}
