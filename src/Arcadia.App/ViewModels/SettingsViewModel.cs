using System.Collections.ObjectModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>A focusable control in the settings dialog.</summary>
public partial class SettingsItemViewModel(Action activate) : ViewModelBase
{
    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public void Activate() => activate();
}

/// <summary>A problem found by the controller-support monitoring, with an optional fix button.</summary>
public sealed class HealthIssueViewModel(HealthIssue issue, Action<HealthFix> fix) : SettingsItemViewModel(() => fix(issue.Fix))
{
    public string Title => issue.Title;

    public string Detail => issue.Detail;

    public bool IsError => issue.Severity == HealthSeverity.Error;

    public bool HasFix => issue.Fix != HealthFix.None;

    public string FixLabel => issue.FixLabel ?? "Beheben";

    public IRelayCommand FixCommand { get; } = new RelayCommand(() => fix(issue.Fix));
}

/// <summary>
/// The settings dialog. For now it holds "Steam Controller Support" and "Über Arcadia"; the
/// other sections from the designs follow.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ControllerSupportService _support;
    private readonly ArcadiaUpdateService _updates;
    private readonly SettingsItemViewModel _toggle;
    private readonly SettingsItemViewModel _update;
    private readonly SettingsItemViewModel _arcadiaUpdate;
    private int _focus;

    public SettingsViewModel(ControllerSupportService support, ArcadiaUpdateService updates, Func<Task> installArcadiaUpdate)
    {
        _support = support;
        _updates = updates;
        _toggle = new SettingsItemViewModel(Toggle);
        _update = new SettingsItemViewModel(() => _ = _support.InstallUpdateAsync());
        _arcadiaUpdate = new SettingsItemViewModel(() => _ = installArcadiaUpdate());
        ToggleCommand = new RelayCommand(Toggle);
        UpdateCommand = new RelayCommand(() => _ = _support.InstallUpdateAsync());
        ArcadiaUpdateCommand = new RelayCommand(() => _ = installArcadiaUpdate());
        _support.Changed += OnSupportChanged;
        _updates.Changed += OnSupportChanged;
        Refresh();
        _toggle.IsFocused = true;
    }

    public SettingsItemViewModel ToggleItem => _toggle;

    public SettingsItemViewModel UpdateItem => _update;

    public SettingsItemViewModel ArcadiaUpdateItem => _arcadiaUpdate;

    public IRelayCommand ArcadiaUpdateCommand { get; }

    public string ArcadiaVersionText => $"Arcadia {_updates.CurrentVersion}";

    public string SourceText => $"Freie Software unter der GPL-3.0 · Quelltext: {ArcadiaUpdateService.RepositoryUrl.Replace("https://", string.Empty, StringComparison.Ordinal)}";

    /// <summary>"Jetzt aktualisieren" is offered (an update is available and not yet downloaded).</summary>
    [ObservableProperty]
    public partial bool HasArcadiaUpdate { get; private set; }

    /// <summary>Update state below the version: available, downloading, waiting for exit, dev build.</summary>
    [ObservableProperty]
    public partial string ArcadiaUpdateText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? ArcadiaUpdateError { get; private set; }

    public ObservableCollection<HealthIssueViewModel> Issues { get; } = [];

    public IRelayCommand ToggleCommand { get; }

    public IRelayCommand UpdateCommand { get; }

    [ObservableProperty]
    public partial bool IsEnabled { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsHealthy { get; private set; }

    /// <summary>SISR runs but doesn't emulate the controller yet; shown with a spinner.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStatusDot))]
    public partial bool IsStarting { get; private set; }

    public bool ShowStatusDot => !IsStarting;

    [ObservableProperty]
    public partial bool HasIssues { get; private set; }

    [ObservableProperty]
    public partial string VersionText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasUpdate { get; private set; }

    [ObservableProperty]
    public partial string UpdateText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string? BusyText { get; private set; }

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    public bool IsBusy => BusyText is not null;

    /// <returns>False if the action closes the dialog.</returns>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                MoveFocus(-1);
                return true;
            case UiAction.Down:
                MoveFocus(1);
                return true;
            case UiAction.Accept:
                if (!IsBusy)
                {
                    FocusableItems()[_focus].Activate();
                }

                return true;
            case UiAction.Back:
            case UiAction.Settings:
                return false;
            default:
                return true;
        }
    }

    public void Dispose()
    {
        _support.Changed -= OnSupportChanged;
        _updates.Changed -= OnSupportChanged;
    }

    private void Toggle()
    {
        if (!IsBusy)
        {
            _ = _support.SetEnabledAsync(!_support.Enabled);
        }
    }

    private void OnSupportChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        var focused = FocusableItems().ElementAtOrDefault(_focus);

        IsEnabled = _support.Enabled;
        BusyText = _support.BusyText;
        OnPropertyChanged(nameof(IsBusy));
        ErrorText = _support.LastError;

        Issues.Clear();
        foreach (var issue in _support.Issues)
        {
            Issues.Add(new HealthIssueViewModel(issue, fix => _ = _support.ApplyFixAsync(fix)));
        }

        HasIssues = Issues.Count > 0;
        IsStarting = IsEnabled && _support.IsStarting;
        IsHealthy = IsEnabled && !HasIssues && BusyText is null && !IsStarting && !_support.NoControllerFound;
        StatusText = (IsEnabled, _support.IsPaused, HasIssues) switch
        {
            (false, _, _) => "Aus – Steam steuert Arcadia mit dem Steam Controller als Maus und Tastatur.",
            (true, true, _) => "Pausiert – ein Spiel läuft, das Steam Input selbst nutzt.",
            (true, false, true) => "Eingeschaltet, aber nicht voll funktionsfähig – siehe unten.",
            _ when _support.IsStarting => "SISR startet – der Controller wird gleich als Xbox-360-Controller eingerichtet …",
            _ when _support.NoControllerFound => "Bereit, aber kein Controller gefunden – schalte deinen Controller ein.",
            _ => _support.Sisr.IsRunning ? "Aktiv – der Steam Controller wird überall als Gamepad erkannt." : "Wird gestartet …",
        };

        VersionText = _support.Sisr.InstalledVersion is { } version ? $"SISR {version}" : "SISR ist noch nicht installiert";
        HasUpdate = _support.AvailableUpdate is not null;
        UpdateText = _support.AvailableUpdate is { } update ? $"Neue Version verfügbar: {update.Tag}" : string.Empty;

        HasArcadiaUpdate = _updates.AvailableVersion is not null && _updates.Progress is null && !_updates.InstallsOnExit;
        ArcadiaUpdateError = _updates.LastError;
        ArcadiaUpdateText = (_updates.IsInstalled, _updates.AvailableVersion, _updates.Progress, _updates.InstallsOnExit) switch
        {
            (false, _, _, _) => "Entwicklungs-Build – Updates gibt es nur für das installierte Arcadia.",
            (_, { } next, _, true) => $"Version {next} ist geladen und wird beim Beenden von Arcadia installiert, weil gerade ein Spiel läuft.",
            (_, { } next, { } percent, _) => $"Version {next} wird geladen … {percent} %",
            (_, { } next, _, _) => $"Neue Version verfügbar: {next}",
            _ => "Arcadia ist auf dem neuesten Stand.",
        };

        // Keep the controller focus on the same item where possible.
        foreach (var item in FocusableItems())
        {
            item.IsFocused = false;
        }

        var items = FocusableItems();
        _focus = focused is not null && items.Contains(focused) ? items.IndexOf(focused) : Math.Min(_focus, items.Count - 1);
        items[_focus].IsFocused = true;
    }

    private List<SettingsItemViewModel> FocusableItems()
    {
        var items = new List<SettingsItemViewModel> { _toggle };
        if (HasUpdate)
        {
            items.Add(_update);
        }

        items.AddRange(Issues.Where(i => i.HasFix));
        if (HasArcadiaUpdate)
        {
            items.Add(_arcadiaUpdate);
        }

        return items;
    }

    private void MoveFocus(int delta)
    {
        var items = FocusableItems();
        var next = Math.Clamp(_focus + delta, 0, items.Count - 1);
        items[_focus].IsFocused = false;
        _focus = next;
        items[_focus].IsFocused = true;
    }
}
