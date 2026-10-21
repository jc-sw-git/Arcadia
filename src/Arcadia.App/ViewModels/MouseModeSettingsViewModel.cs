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
        InGamesItem = new SettingsStepperViewModel(
            () => (int)Current.InGames,
            value => Change(s => s with { InGames = (MouseModeInGames)value }),
            value => InGamesNames[value],
            (int)MouseModeInGames.Allowed,
            (int)MouseModeInGames.PerGame);
        PerGameDefaultItem = new SettingsStepperViewModel(
            () => Current.PerGameDefaultAllowed ? 0 : 1,
            value => Change(s => s with { PerGameDefaultAllowed = value == 0 }),
            value => value == 0 ? GameMouseModes.Allowed : GameMouseModes.Blocked,
            0,
            1);
        HelpItem = new SettingsItemViewModel(openHelp);
        ToggleEndInFrontCommand = new RelayCommand(EndInFrontItem.Activate);
        HelpCommand = new RelayCommand(openHelp);
    }

    /// <summary>The values of "Mausmodus", in the order of <see cref="MouseModeActivation"/>.</summary>
    private static readonly string[] ActivationNames = ["Aus", "Von Hand", "Automatisch"];

    /// <summary>The values of "Beenden des Mausmodus in Spielen erzwingen", in the order of <see cref="MouseModeInGames"/>.</summary>
    private static readonly string[] InGamesNames = ["Nein", "Ja", "Individuell"];

    /// <summary>The mouse mode's buttons, as <see cref="Arcadia.Input.MouseMode"/> maps them.</summary>
    public static IReadOnlyList<MouseModeHelpRow> HelpRows { get; } =
    [
        new("LS", "Mauszeiger"),
        new("L3", "Antippen: langsamer Zeiger an/aus · Halten: langsam, solange gehalten"),
        new("RT oder A", "Linke Maustaste (gedrückt halten = ziehen)"),
        new("LT oder X", "Rechte Maustaste"),
        new("RS", "Mausrad, auch seitwärts"),
        new("D-Pad", "Pfeiltasten"),
        new("B", "Esc"),
        new("LB / RB", "Umschalt+Tab / Tab"),
        new("≡", "Enter"),
        new("⧉ halten", "Holt Arcadia nach vorne (Einstellung „Zurück zu Arcadia“)"),
        new("⧉ + ≡ halten", "Zurück zum normalen Controller (bei „Automatisch“, bis Arcadia wieder vorne war)"),
    ];

    /// <summary>Where the mouse mode can't help, so nobody wonders why.</summary>
    public static IReadOnlyList<string> HelpLimits { get; } =
    [
        "Windows-Abfragen nach Administratorrechten (UAC) laufen auf einem geschützten Bildschirm, den keine Eingabe von Arcadia erreicht.",
        "Fenster von Programmen, die als Administrator laufen, lassen Eingaben eines Arcadia ohne Administratorrechte nicht durch.",
        "Über Spielen im exklusiven Vollbild zeigt Windows den Rand und den Hinweis nicht an; das Umschalten spürst du dann nur am Vibrieren.",
    ];

    /// <summary>"Mausmodus": Aus, Von Hand or Automatisch. Aus: every other row counts for nothing but keeps its value.</summary>
    public SettingsStepperViewModel ActivationItem { get; }

    public SettingsStepperViewModel HoldItem { get; }

    public SettingsStepperViewModel SpeedItem { get; }

    public SettingsItemViewModel EndInFrontItem { get; }

    /// <summary>"Beenden des Mausmodus in Spielen erzwingen": Nein, Ja or Individuell (only for "Von Hand").</summary>
    public SettingsStepperViewModel InGamesItem { get; }

    /// <summary>"Standardeinstellung bei „Individuell“": Erlaubt or Deaktiviert.</summary>
    public SettingsStepperViewModel PerGameDefaultItem { get; }

    /// <summary>"So funktioniert's": the buttons and the limits of the mouse mode.</summary>
    public SettingsItemViewModel HelpItem { get; }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsItemViewModel> Items => [ActivationItem, HoldItem, SpeedItem, EndInFrontItem, InGamesItem, PerGameDefaultItem, HelpItem];

    public IRelayCommand ToggleEndInFrontCommand { get; }

    public IRelayCommand HelpCommand { get; }

    public bool Enabled => Current.Activation != MouseModeActivation.Off;

    /// <summary>The automatic mode always ends when Arcadia comes to the front.</summary>
    public bool EndInFrontApplies => Current.Activation == MouseModeActivation.Manual;

    /// <summary>"Beenden des Mausmodus in Spielen erzwingen" counts only for "Von Hand".</summary>
    public bool InGamesApplies => Current.Activation == MouseModeActivation.Manual;

    public bool PerGameDefaultApplies => InGamesApplies && Current.InGames == MouseModeInGames.PerGame;

    /// <summary>What the chosen value of "Beenden des Mausmodus in Spielen erzwingen" does (owner's wording, 2026-10-21).</summary>
    public string InGamesText => Current.InGames switch
    {
        MouseModeInGames.Blocked => "Nur „Von Hand“: Der Mausmodus wird automatisch beendet und kann auch nicht mehr von Hand aktiviert werden, sobald ein Spiel im Vordergrund läuft. Das Spiel muss dafür in Arcadias Bibliothek vorhanden sein.",
        MouseModeInGames.PerGame => "Nur „Von Hand“: Es kann pro Spiel (auf der Detailseite) entschieden werden, ob der Mausmodus beendet wird oder erlaubt ist.",
        _ => "Nur „Von Hand“: Der Mausmodus bleibt auch in Spielen aktiv und kann auch aktiviert werden, wenn das Spiel gerade im Vordergrund läuft.",
    };

    /// <summary>What the chosen way of switching does, under "Mausmodus".</summary>
    public string ActivationText => Current.Activation switch
    {
        MouseModeActivation.Off => "Der Controller bleibt immer ein Controller.",
        MouseModeActivation.Automatic => "Bei „Automatisch“ schaltet Arcadia automatisch in den Mausmodus, sobald Arcadia (oder ein Arcadia bekanntes Spiel) nicht mehr den Fokus hat, das heißt nicht mehr im Vordergrund läuft. Der Controller kann dann den Mauszeiger bedienen. Unter „So funktioniert's“ erhältst du weitere Informationen dazu. Kommt Arcadia (oder ein Spiel) zurück in den Fokus, wird der Mausmodus automatisch beendet. Dies passiert auch dann, wenn „Mausmodus beenden, wenn Arcadia im Vordergrund ist“ ausgeschaltet ist.",
        _ => "Durch Halten von ⧉ + ≡ wechselst du in den Mausmodus. Dies erlaubt dem Controller, den Mauszeiger zu steuern. Erneutes Halten schaltet den Modus aus. Unter „So funktioniert's“ erhältst du weitere Informationen dazu.",
    };

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
        OnPropertyChanged(nameof(InGamesApplies));
        OnPropertyChanged(nameof(PerGameDefaultApplies));
        OnPropertyChanged(nameof(InGamesText));
        OnPropertyChanged(nameof(ActivationText));
        OnPropertyChanged(nameof(EndWhenArcadiaInFront));
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

/// <summary>The "Steams Desktop-Belegung ignorieren" box (#64): its own box, independent of the mouse mode (owner, 2026-10-20).</summary>
public sealed partial class SteamDesktopLayoutSettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settings;

    public SteamDesktopLayoutSettingsViewModel(SettingsService settings)
    {
        _settings = settings;
        EnabledItem = new SettingsItemViewModel(Toggle);
        ToggleCommand = new RelayCommand(EnabledItem.Activate);
    }

    public SettingsItemViewModel EnabledItem { get; }

    /// <summary>In the order the controller moves through them.</summary>
    public IReadOnlyList<SettingsItemViewModel> Items => [EnabledItem];

    public IRelayCommand ToggleCommand { get; }

    public bool Enabled => _settings.Current.IgnoreSteamDesktopLayout;

    /// <summary>Set when a change couldn't be saved; it still applies until Arcadia closes.</summary>
    [ObservableProperty]
    public partial string? SaveError { get; private set; }

    private void Toggle()
    {
        try
        {
            _settings.Update(s => s with { IgnoreSteamDesktopLayout = !s.IgnoreSteamDesktopLayout });
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Einstellung „Steams Desktop-Belegung ignorieren“ nicht gespeichert: {ex.Message}");
            SaveError = "Die Änderung konnte nicht gespeichert werden und gilt nur bis zum Beenden – Details stehen in arcadia.log.";
        }

        OnPropertyChanged(nameof(Enabled));
    }
}

/// <summary>The values of "Individueller Mausmodus" on a game (owner, 2026-10-20).</summary>
public static class GameMouseModes
{
    public const string Allowed = "Erlaubt";
    public const string Blocked = "Deaktiviert";

    /// <summary>"Standard" names what it means now, e.g. "Standard (erlaubt)" (owner, 2026-10-21).</summary>
    public static string Of(bool? choice, bool defaultAllowed) => choice switch
    {
        true => Allowed,
        false => Blocked,
        null => defaultAllowed ? "Standard (erlaubt)" : "Standard (deaktiviert)",
    };

    /// <summary>Standard → Erlaubt → Deaktiviert → Standard.</summary>
    public static bool? Next(bool? choice) => choice switch
    {
        null => true,
        true => false,
        false => null,
    };
}
