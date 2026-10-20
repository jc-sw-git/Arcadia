using Arcadia.App.Services;
using Arcadia.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A number setting, e.g. from 1 to 9: left and right on the D-pad (or the arrows beside it) change
/// it; A counts up and starts again at the lowest after the highest, so it can be set with A alone.
/// </summary>
public sealed partial class SettingsStepperViewModel : SettingsItemViewModel
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;
    private readonly Func<int, string> _format;
    private readonly int _min;
    private readonly int _max;

    public SettingsStepperViewModel(Func<int> get, Action<int> set, Func<int, string> format, int min, int max)
        : base(static () => { })
    {
        _get = get;
        _set = set;
        _format = format;
        _min = min;
        _max = max;
        DecreaseCommand = new RelayCommand(() => Step(-1));
        IncreaseCommand = new RelayCommand(() => Step(1));
    }

    public string ValueText => _format(_get());

    public bool CanDecrease => _get() > _min;

    public bool CanIncrease => _get() < _max;

    public IRelayCommand DecreaseCommand { get; }

    public IRelayCommand IncreaseCommand { get; }

    public void Step(int delta) => Set(Math.Clamp(_get() + delta, _min, _max));

    public override void Activate() => Set(_get() >= _max ? _min : _get() + 1);

    /// <summary>"1 Sekunde", "3 Sekunden".</summary>
    public static string Seconds(int value) => value == 1 ? "1 Sekunde" : $"{value} Sekunden";

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
        ActivationItem = new SettingsStepperViewModel(
            () => (int)Current.Activation,
            value => Change(s => s with { Activation = (MouseModeActivation)value }),
            value => ActivationNames[value],
            (int)MouseModeActivation.Off,
            (int)MouseModeActivation.Automatic);
        HoldItem = new SettingsStepperViewModel(
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            SettingsStepperViewModel.Seconds,
            MouseModeSettings.Min,
            MouseModeSettings.Max);
        SpeedItem = new SettingsStepperViewModel(
            () => Current.PointerSpeed,
            value => Change(s => s with { PointerSpeed = value }),
            value => value.ToString(Formatting.Culture),
            MouseModeSettings.Min,
            MouseModeSettings.Max);
        EndInFrontItem = new SettingsItemViewModel(() => Change(s => s with { EndWhenArcadiaInFront = !s.EndWhenArcadiaInFront }));
        SteamLayoutItem = new SettingsItemViewModel(() => Change(s => s with { IgnoreSteamDesktopLayout = !s.IgnoreSteamDesktopLayout }));
        HelpItem = new SettingsItemViewModel(openHelp);
        ToggleEndInFrontCommand = new RelayCommand(EndInFrontItem.Activate);
        ToggleSteamLayoutCommand = new RelayCommand(SteamLayoutItem.Activate);
        HelpCommand = new RelayCommand(openHelp);
    }

    /// <summary>The values of "Mausmodus", in the order of <see cref="MouseModeActivation"/>.</summary>
    private static readonly string[] ActivationNames = ["Aus", "Von Hand", "Automatisch"];

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
        new("≡", "Enter"),
        new("⧉ halten", "Holt Arcadia nach vorne (Einstellung „Zurück zu Arcadia“)"),
        new("⧉ + ≡ halten", "Zurück zum normalen Controller"),
    ];

    /// <summary>Where the mouse mode can't help, so nobody wonders why.</summary>
    public static IReadOnlyList<string> HelpLimits { get; } =
    [
        "Windows-Abfragen nach Administratorrechten (UAC) laufen auf einem geschützten Bildschirm, den keine Eingabe von Arcadia erreicht.",
        "Fenster von Programmen, die als Administrator laufen, lassen Eingaben eines Arcadia ohne Administratorrechte nicht durch.",
        "Über Spielen im exklusiven Vollbild zeigt Windows den Rand und den Hinweis nicht an; das Umschalten spürst du dann nur am Vibrieren.",
        "Im Mausmodus sieht ein Spiel im Vordergrund die Controller-Eingaben weiterhin.",
    ];

    /// <summary>"Mausmodus": Aus, Von Hand or Automatisch. Aus: every other row counts for nothing but keeps its value.</summary>
    public SettingsStepperViewModel ActivationItem { get; }

    public SettingsStepperViewModel HoldItem { get; }

    public SettingsStepperViewModel SpeedItem { get; }

    public SettingsItemViewModel EndInFrontItem { get; }

    public SettingsItemViewModel SteamLayoutItem { get; }

    /// <summary>"So funktioniert's": the buttons and the limits of the mouse mode.</summary>
    public SettingsItemViewModel HelpItem { get; }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsItemViewModel> Items => [ActivationItem, HoldItem, SpeedItem, EndInFrontItem, SteamLayoutItem, HelpItem];

    public IRelayCommand ToggleEndInFrontCommand { get; }

    public IRelayCommand ToggleSteamLayoutCommand { get; }

    public IRelayCommand HelpCommand { get; }

    public bool Enabled => Current.Activation != MouseModeActivation.Off;

    /// <summary>The automatic mode always ends when Arcadia comes to the front.</summary>
    public bool EndInFrontApplies => Current.Activation == MouseModeActivation.Manual;

    /// <summary>What the chosen way of switching does, under "Mausmodus".</summary>
    public string ActivationText => Current.Activation switch
    {
        MouseModeActivation.Off => "Der Controller bleibt immer ein Controller. Die Einstellungen darunter behalten ihre Werte.",
        MouseModeActivation.Automatic => "Verliert Arcadia den Fokus an ein Fenster, das kein Spiel aus der Bibliothek ist, wird der Controller sofort zur Maus; kommt Arcadia oder ein Spiel nach vorne, wieder zum Controller. Bis dahin schaltet ⧉ + ≡ gehalten von Hand um, z. B. in einem Spiel, das Arcadia nicht kennt.",
        _ => "⧉ + ≡ gedrückt halten macht den Controller zur Maus und wieder zurück, auch wenn Arcadia im Hintergrund ist. So lassen sich Launcher-Fenster und Windows-Dialoge vom Sofa aus bedienen.",
    };

    public bool IgnoreSteamDesktopLayout => Current.IgnoreSteamDesktopLayout;

    public bool EndWhenArcadiaInFront => Current.EndWhenArcadiaInFront;

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

        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(EndInFrontApplies));
        OnPropertyChanged(nameof(ActivationText));
        OnPropertyChanged(nameof(EndWhenArcadiaInFront));
        OnPropertyChanged(nameof(IgnoreSteamDesktopLayout));
    }
}

/// <summary>The "Zurück zu Arcadia" box (#64): View held brings Arcadia back, with or without the mouse mode.</summary>
public sealed partial class ReturnWithViewSettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;

    public ReturnWithViewSettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        EnabledItem = new SettingsItemViewModel(() => Change(s => s with { Enabled = !s.Enabled }));
        HoldItem = new SettingsStepperViewModel(
            () => Current.HoldSeconds,
            value => Change(s => s with { HoldSeconds = value }),
            value => value == 0 ? "Sofort" : SettingsStepperViewModel.Seconds(value),
            ReturnWithViewSettings.Min,
            ReturnWithViewSettings.Max);
        ToggleEnabledCommand = new RelayCommand(EnabledItem.Activate);
    }

    public SettingsItemViewModel EnabledItem { get; }

    public SettingsStepperViewModel HoldItem { get; }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsItemViewModel> Items => [EnabledItem, HoldItem];

    public IRelayCommand ToggleEnabledCommand { get; }

    public bool Enabled => Current.Enabled;

    /// <summary>Set when a change couldn't be saved; it still applies until Arcadia closes.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; private set; }

    private ReturnWithViewSettings Current => _settings.Current.ReturnWithView;

    private void Change(Func<ReturnWithViewSettings, ReturnWithViewSettings> change)
    {
        try
        {
            _settings.Update(s => s with { ReturnWithView = change(s.ReturnWithView) });
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Einstellung „Zurück zu Arcadia“ nicht gespeichert: {ex.Message}");
            SaveError = "Die Änderung konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.";
        }

        OnPropertyChanged(nameof(Enabled));
    }
}
