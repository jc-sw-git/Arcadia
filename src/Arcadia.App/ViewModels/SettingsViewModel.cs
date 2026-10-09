using System.Collections.ObjectModel;
using Arcadia.App.Input;
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
/// The settings dialog. For now it holds "Steam Controller Support"; the other sections from
/// the designs follow.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ControllerSupportService _support;
    private readonly SettingsItemViewModel _toggle;
    private readonly SettingsItemViewModel _update;
    private int _focus;

    public SettingsViewModel(ControllerSupportService support)
    {
        _support = support;
        _toggle = new SettingsItemViewModel(Toggle);
        _update = new SettingsItemViewModel(() => _ = _support.InstallUpdateAsync());
        ToggleCommand = new RelayCommand(Toggle);
        UpdateCommand = new RelayCommand(() => _ = _support.InstallUpdateAsync());
        _support.Changed += OnSupportChanged;
        Refresh();
        _toggle.IsFocused = true;
    }

    public SettingsItemViewModel ToggleItem => _toggle;

    public SettingsItemViewModel UpdateItem => _update;

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

    public void Dispose() => _support.Changed -= OnSupportChanged;

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
