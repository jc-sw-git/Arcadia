using Arcadia.App.Services;
using Arcadia.ControllerSupport;
using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// A launcher in the category "Launcher" (#136): its tab key, logo, name and state, and the switch "Aktiv". ◀ ▶ switch it,
/// Y lifts it to move it (<see cref="LauncherSettingsViewModel"/>).
/// </summary>
public sealed partial class LauncherRowViewModel : SettingsRowViewModel
{
    private readonly Action<LauncherRowViewModel, bool> _switch;

    public LauncherRowViewModel(LauncherInfo launcher, Action<LauncherRowViewModel, bool> switchTo)
        : base(launcher.Id, launcher.DisplayName)
    {
        Launcher = launcher;
        Monogram = launcher.DisplayName[..1];
        TileBrush = Formatting.LauncherTileBrush(launcher.Id);
        _switch = switchTo;
    }

    public LauncherInfo Launcher { get; }

    public string Monogram { get; }

    /// <summary>Placeholder until launcher logos (#138).</summary>
    public IBrush TileBrush { get; }

    /// <summary>Shown as a tab; off: not read, not watched, no tab.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText), nameof(CanDecrease), nameof(CanIncrease), nameof(HasSwitch))]
    public partial bool IsActive { get; private set; }

    /// <summary>Installed on this PC; one that isn't can't be switched on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIncrease), nameof(HasSwitch))]
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

    public bool CanDecrease => IsActive;

    public bool CanIncrease => !IsActive && IsFound;

    /// <summary>Not a launcher that's missing and off: that one can't go anywhere.</summary>
    public bool CanMove => HasSwitch;

    public override bool CanChange => HasSwitch;

    public override string PreviewTitle => "TAB-LEISTE";

    public override string InfoTitle => "LAUNCHER-DETAILS";

    public override void Change(int delta)
    {
        if (delta < 0 ? CanDecrease : CanIncrease)
        {
            _switch(this, delta > 0);
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
        Change(IsActive ? -1 : 1);
    }

    /// <param name="tabNumber">Its place among the tabs from 1, or null while inactive.</param>
    /// <param name="warning">Why the launcher needs a look (e.g. "Keine Spiele gefunden"), shown instead of the state.</param>
    public void Show(bool isActive, bool isFound, int? tabNumber, string statusText, string? warning, IReadOnlyList<InfoBlock> preview, IReadOnlyList<InfoBlock> info)
    {
        var actionsChanged = IsActive != isActive || IsFound != isFound;
        IsActive = isActive;
        IsFound = isFound;
        TabKey = tabNumber is >= 1 and <= 10 ? (tabNumber.Value % 10).ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        StatusText = warning ?? statusText;
        Severity = warning is null ? null : HealthSeverity.Warning;
        IsHealthy = isActive && warning is null;
        Preview = preview;
        Info = info;
        if (actionsChanged)
        {
            NotifyActionsChanged();
        }
    }
}
