using Arcadia.ControllerSupport;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.Settings;

/// <summary>What a setting that needs something installed first offers on this PC, e.g. "Controller-Wächter (Pan)".</summary>
public enum SetupRowMode
{
    /// <summary>Everything is in place (or the setting is already on): a switch.</summary>
    Switch,

    /// <summary>Something is missing: an install action (e.g. "Treiber installieren") instead of the switch.</summary>
    Install,

    /// <summary>Not on this PC (e.g. no Windows): greyed out, saying why.</summary>
    Unavailable,
}

/// <summary>A setting that needs something installed first: a switch, an install action, or why neither is possible.</summary>
public partial class SetupSwitchRowViewModel(string key, string title, Action<bool?> activate)
    : SettingsRowViewModel(key, title)
{
    private string _subtitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSwitch), nameof(IsInstall))]
    public partial SetupRowMode Mode { get; private set; }

    public bool IsSwitch => Mode == SetupRowMode.Switch;

    public bool IsInstall => Mode == SetupRowMode.Install;

    /// <summary>What the install action says on the right, e.g. "Treiber installieren" or "Wird installiert …".</summary>
    [ObservableProperty]
    public partial string InstallText { get; set; } = "Treiber installieren";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText))]
    public partial bool IsOn { get; private set; }

    public string ValueText => IsOn ? "An" : "Aus";

    /// <summary>Something starts or stops (or a game runs): the switch keeps its position but can't be flipped.</summary>
    [ObservableProperty]
    public partial bool IsLocked { get; private set; }

    public override bool CanChange => IsSwitch && !IsLocked;

    public override bool CanActivate => Mode != SetupRowMode.Unavailable && !IsLocked;

    public override string ActivateText => IsInstall ? InstallText : "Umschalten";

    /// <summary>◀ and ▶ both switch it, like every ◀ ▶ selector (owner, 2026-10-24).</summary>
    public override void Change(int delta) => activate(null);

    public override void Activate() => activate(null);

    /// <summary>Whether what it switches on really works: null while the option is off, the switch says so.</summary>
    public void ShowLamp(RowLamp? lamp) => Lamp = lamp;

    /// <summary>"Treiber installieren" on this row failed (#243 part B2): the error in place of the subtitle, with the error icon.</summary>
    public void ShowProblem(string? problem)
    {
        Problem = problem;
        Severity = problem is null ? null : HealthSeverity.Error;
    }

    /// <param name="status">How the option is doing while it is on, shown in place of what it is for; null while off.</param>
    public void Update(SetupRowMode mode, bool isOn, bool isLocked, string unavailableText, string? status)
    {
        if (_subtitle.Length == 0)
        {
            _subtitle = Subtitle ?? string.Empty;
        }

        Mode = mode;
        IsOn = isOn;
        IsLocked = isLocked;
        IsDimmed = mode == SetupRowMode.Unavailable;
        Subtitle = mode == SetupRowMode.Unavailable ? unavailableText : status ?? _subtitle;
        NotifyActionsChanged();
    }

    partial void OnInstallTextChanged(string value) => NotifyActionsChanged();
}
