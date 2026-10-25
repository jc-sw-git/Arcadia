using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// A launcher in the category "Launcher" (#136): its tab key, logo, name and state, and the switch "Aktiv". ◀ ▶ switch it,
/// Y lifts it to move it, A or a click open its page (#137) (<see cref="LauncherSettingsViewModel"/>).
/// </summary>
public sealed partial class LauncherRowViewModel : SettingsRowViewModel
{
    private readonly Action<LauncherRowViewModel, bool> _switch;
    private readonly Action<LauncherRowViewModel> _open;

    public LauncherRowViewModel(LauncherInfo launcher, LauncherBadge badge, Action<LauncherRowViewModel, bool> switchTo, Action<LauncherRowViewModel> open)
        : base(launcher.Id, launcher.DisplayName)
    {
        Launcher = launcher;
        Badge = badge;
        _switch = switchTo;
        _open = open;
    }

    /// <summary>With the name and keyword the user gave it (#137).</summary>
    [ObservableProperty]
    public partial LauncherInfo Launcher { get; private set; }

    /// <summary>Its logo (#138).</summary>
    [ObservableProperty]
    public partial LauncherBadge Badge { get; private set; }

    /// <summary>Shown as a tab; off: not read, not watched, no tab.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText), nameof(HasSwitch))]
    public partial bool IsActive { get; private set; }

    /// <summary>Installed on this PC; one that isn't can't be switched on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSwitch))]
    public partial bool IsFound { get; private set; }

    /// <summary>The key that selects its tab, "1" … "0"; empty for an inactive launcher or beyond the tenth tab.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTabKey))]
    public partial string TabKey { get; private set; } = string.Empty;

    public bool HasTabKey => TabKey.Length > 0;

    /// <summary>The line below the name, e.g. "128 Spiele · /steam" or "Nicht gefunden".</summary>
    [ObservableProperty]
    public partial string StatusText { get; private set; } = string.Empty;

    /// <summary>Everything as it should be: a green dot before <see cref="StatusText"/>.</summary>
    [ObservableProperty]
    public partial bool IsHealthy { get; private set; }

    /// <summary>A launcher that isn't installed and isn't active has nothing to switch: "Nicht installiert".</summary>
    public bool HasSwitch => IsActive || IsFound;

    public string ValueText => IsActive ? "Aktiv" : "Aus";

    /// <summary>A choice of two wraps like every ◀ ▶ selector, so both arrows switch it (owner, 2026-10-24).</summary>
    public bool CanDecrease => HasSwitch;

    public bool CanIncrease => HasSwitch;

    /// <summary>Not a launcher that's missing and off: that one can't go anywhere.</summary>
    public bool CanMove => HasSwitch;

    public override bool CanChange => HasSwitch;

    public override string PreviewTitle => LauncherSettingsViewModel.OrderPreviewTitle;

    public override string PreviewSubtitle => LauncherSettingsViewModel.OrderPreviewSubtitle;

    public override string InfoTitle => "LAUNCHER-DETAILS";

    public override bool CanActivate => true;

    public override string ActivateText => "Öffnen";

    public override void Activate() => _open(this);

    /// <summary>The user renamed it, gave it another keyword or logo.</summary>
    public void ShowLooks(LauncherInfo launcher, LauncherBadge badge)
    {
        Launcher = launcher;
        Title = launcher.DisplayName;
        Badge = badge;
    }

    public override void Change(int delta)
    {
        if (HasSwitch)
        {
            _switch(this, !IsActive);
        }
    }

    /// <summary>
    /// A click anywhere on the switch flips it, not just one on its arrows, which were too small a target (owner,
    /// 2026-10-22). A click on the rest of the row stays free for the launcher's page (#137).
    /// </summary>
    [RelayCommand]
    private void Toggle()
    {
        Point();
        Change(1);
    }

    /// <param name="tabNumber">Its place among the tabs from 1, or null while inactive.</param>
    /// <param name="notice">
    /// A status message shown with its level's symbol instead of the state, e.g. "Nicht gefunden" (a warning) or "Keine
    /// Spiele gefunden" (information).
    /// </param>
    public void Show(bool isActive, bool isFound, int? tabNumber, string statusText, (string Text, HealthSeverity Severity)? notice, IReadOnlyList<InfoBlock> preview, IReadOnlyList<InfoBlock> info)
    {
        var actionsChanged = IsActive != isActive || IsFound != isFound;
        IsActive = isActive;
        IsFound = isFound;
        TabKey = tabNumber is >= 1 and <= 10 ? (tabNumber.Value % 10).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        StatusText = notice?.Text ?? statusText;
        Severity = notice?.Severity;
        IsHealthy = isActive && notice is null;
        Preview = preview;
        Info = info;
        if (actionsChanged)
        {
            NotifyActionsChanged();
        }
    }
}
