using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// One row of the settings page, pattern "Leiste" (#15, owner 2026-10-22): the focus moves only up and down; ◀ ▶ change
/// a switch or a choice at once, A starts an action. The info panel beside the rows shows <see cref="Info"/> of the
/// focused one. The subclasses decide what stands on the right of the row and what the buttons do.
/// </summary>
public abstract partial class SettingsRowViewModel(string key, string title) : ViewModelBase
{
    /// <summary>Stays the same while the row is shown, so the focus stays on it when rows above come or go.</summary>
    public string Key => key;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InfoTitle))]
    public partial string Title { get; set; } = title;

    /// <summary>The heading of the info panel: the title in capitals, like the group headings.</summary>
    public virtual string InfoTitle => Title.ToUpper(System.Globalization.CultureInfo.GetCultureInfo("de-DE"));

    /// <summary>One short line below the title; the long explanation goes into <see cref="Info"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubtitle))]
    public partial string? Subtitle { get; set; }

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle) && !HasProblem;

    /// <summary>Why what is being entered can't be taken, e.g. a keyword in use (#137); shown in place of the subtitle.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem), nameof(HasSubtitle))]
    public partial string? Problem { get; protected set; }

    public bool HasProblem => Problem is not null;

    /// <summary>
    /// A lamp beside the title that shows at a glance whether what the row switches on really works, e.g. SISR (owner,
    /// 2026-10-22 and 2026-10-24); null shows none.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLamp), nameof(IsLampWorking), nameof(IsLampProblem), nameof(IsLampFailure))]
    public partial RowLamp? Lamp { get; protected set; }

    public bool HasLamp => Lamp is not null;

    public bool IsLampWorking => Lamp == RowLamp.Working;

    public bool IsLampProblem => Lamp == RowLamp.Problem;

    public bool IsLampFailure => Lamp == RowLamp.Failure;

    /// <summary>The heading of the group the row belongs to, e.g. "MAUSMODUS"; may change while the row is shown (#136).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupHeading))]
    public partial string? Group { get; set; }

    /// <summary>The first row of its group: the group's heading stands above it, and its box begins.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupHeading))]
    public partial bool StartsGroup { get; set; }

    public bool HasGroupHeading => StartsGroup && !string.IsNullOrEmpty(Group);

    /// <summary>The last row of its group: the group's box ends below it (owner, 2026-10-22).</summary>
    [ObservableProperty]
    public partial bool EndsGroup { get; set; }

    /// <summary>
    /// How far the row stands in: 1 below the setting it depends on, 2 below one that depends itself (owner,
    /// 2026-10-22), e.g. "Haltedauer" under "Mausmodus".
    /// </summary>
    public int Indent { get; init; }

    public bool IsIndented => Indent == 1;

    public bool IsIndentedTwice => Indent >= 2;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>Lifted to be moved, e.g. a launcher with Y or the mouse (#136): ▲ ▼ move it until it is put down.</summary>
    [ObservableProperty]
    public partial bool IsLifted { get; set; }

    /// <summary>
    /// Depends on a main setting that is off (e.g. "Haltedauer" with "Mausmodus: Aus"): greyed out, but it keeps its
    /// value and can still be changed, so it is ready when the main setting comes on again.
    /// </summary>
    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

    /// <summary>A status message the row shows with its level's symbol, e.g. a problem of Steam Controller Support.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSeverity), nameof(IsInfo), nameof(IsError))]
    public partial HealthSeverity? Severity { get; set; }

    public bool HasSeverity => Severity is not null;

    public bool IsInfo => Severity == HealthSeverity.Info;

    public bool IsError => Severity == HealthSeverity.Error;

    /// <summary>What the info panel shows while the row has the focus.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<InfoBlock> Info { get; set; } = [];

    /// <summary>
    /// A box of its own above the info panel showing what the row changes, e.g. the tab strip of the launchers (#136, owner
    /// 2026-10-22); empty shows none.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    public partial IReadOnlyList<InfoBlock> Preview { get; set; } = [];

    public bool HasPreview => Preview.Count > 0;

    /// <summary>The heading of <see cref="Preview"/>.</summary>
    public virtual string PreviewTitle { get; init; } = string.Empty;

    /// <summary>A line below <see cref="PreviewTitle"/> that says what the box shows; empty shows nothing.</summary>
    public virtual string PreviewSubtitle { get; init; } = string.Empty;

    public bool HasPreviewSubtitle => PreviewSubtitle.Length > 0;

    /// <summary>◀ ▶ change the row's value.</summary>
    public virtual bool CanChange => false;

    /// <summary>A does something on this row.</summary>
    public virtual bool CanActivate => false;

    /// <summary>What A does, for the footer.</summary>
    public virtual string ActivateText => "Auswählen";

    /// <summary>The footer names A. A choice doesn't: A only counts on there, which ◀ ▶ already say.</summary>
    public virtual bool ShowsActivateHint => CanActivate;

    /// <summary>A click on the row focuses it before it acts; set by the category the row is in.</summary>
    internal Action<SettingsRowViewModel>? Pointed { get; set; }

    public virtual void Change(int delta)
    {
    }

    /// <summary>The mouse moved over the row: it takes the focus, so the info panel explains it before a click changes it.</summary>
    public void Point() => Pointed?.Invoke(this);

    public virtual void Activate()
    {
    }

    /// <summary>A click: focus, then what A does.</summary>
    [RelayCommand]
    private void Click()
    {
        Point();
        if (CanActivate)
        {
            Activate();
        }
    }

    /// <summary>The arrow left of a value: focus, then what ◀ does.</summary>
    [RelayCommand]
    private void StepBack() => Step(-1);

    /// <summary>The arrow right of a value: focus, then what ▶ does.</summary>
    [RelayCommand]
    private void StepOn() => Step(1);

    private void Step(int delta)
    {
        Point();
        if (CanChange)
        {
            Change(delta);
        }
    }

    /// <summary>Tells the footer that <see cref="CanChange"/>, <see cref="CanActivate"/> or <see cref="ActivateText"/> changed.</summary>
    protected void NotifyActionsChanged()
    {
        OnPropertyChanged(nameof(CanChange));
        OnPropertyChanged(nameof(CanActivate));
        OnPropertyChanged(nameof(ActivateText));
        OnPropertyChanged(nameof(ShowsActivateHint));
    }
}

/// <summary>
/// A value out of a few, e.g. "Mausmodus: Aus / Von Hand / Automatisch" or a number from 1 to 9: ◀ ▶ step through them
/// and wrap at the ends, so an arrow is never greyed out just for standing at an end (owner, 2026-10-24); A counts on
/// like ▶, so A alone reaches every value.
/// </summary>
public partial class ChoiceRowViewModel : SettingsRowViewModel
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;
    private readonly Func<int, string> _format;
    private readonly int _min;
    private readonly int _max;

    public ChoiceRowViewModel(string key, string title, Func<int> get, Action<int> set, Func<int, string> format, int min, int max)
        : base(key, title)
    {
        _get = get;
        _set = set;
        _format = format;
        _min = min;
        _max = max;
    }

    public string ValueText => _format(_get());

    public bool CanDecrease => CanStep(-1);

    public bool CanIncrease => CanStep(1);

    public override bool CanChange => true;

    public override bool CanActivate => true;

    public override string ActivateText => "Ändern";

    public override bool ShowsActivateHint => false;

    public override void Change(int delta)
    {
        if (CanStep(delta))
        {
            Set(ValueCycle.Step(_get(), delta, _min, _max));
        }
    }

    public override void Activate() => Change(1);

    /// <summary>The value changed elsewhere (or a value it is shown with), so it is read again.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(CanDecrease));
        OnPropertyChanged(nameof(CanIncrease));
        OnValueShown();
    }

    /// <summary>"1 Sekunde", "3 Sekunden".</summary>
    public static string Seconds(int value) => value == 1 ? "1 Sekunde" : $"{value} Sekunden";

    protected virtual void OnValueShown()
    {
    }

    /// <summary>Whether the row may take this value; a subclass may hold one back, e.g. "Aktiv" for a launcher that isn't installed (#137).</summary>
    protected virtual bool Allows(int value) => true;

    private bool CanStep(int delta)
    {
        var target = ValueCycle.Step(_get(), delta, _min, _max);
        return target != _get() && Allows(target);
    }

    private void Set(int value)
    {
        if (value == _get())
        {
            return;
        }

        _set(value);
        Refresh();
    }
}

/// <summary>On or off, a choice of two: ◀, ▶ and A all switch it.</summary>
public partial class SwitchRowViewModel(string key, string title, Func<bool> get, Action<bool> set)
    : ChoiceRowViewModel(key, title, () => get() ? 1 : 0, value => set(value == 1), value => value == 1 ? "An" : "Aus", 0, 1)
{
    public bool IsOn => get();

    public override string ActivateText => "Umschalten";

    public override bool ShowsActivateHint => true;

    protected override void OnValueShown() => OnPropertyChanged(nameof(IsOn));
}

/// <summary>A row that does something on A, e.g. "Nach Updates suchen"; its <see cref="ActionText"/> stands on the right.</summary>
public partial class ActionRowViewModel(string key, string title, Action run) : SettingsRowViewModel(key, title)
{
    /// <summary>Shown on the right of the row; empty shows nothing there and A does nothing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction))]
    public partial string ActionText { get; set; } = string.Empty;

    public bool HasAction => ActionText.Length > 0;

    /// <summary>Shown, but not now, e.g. while a search for updates runs.</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    /// <summary>The action the row is there for, drawn in the accent colour (e.g. "SISR installieren").</summary>
    [ObservableProperty]
    public partial bool IsPrimary { get; set; }

    /// <summary>A button that does the same from anywhere in the list, shown in the pill (e.g. "X" / "F"); null shows none.</summary>
    public string? ShortcutPad { get; init; }

    public string? ShortcutKeys { get; init; }

    public bool HasShortcut => ShortcutPad is not null;

    public override bool CanActivate => HasAction && IsEnabled;

    public override string ActivateText => HasAction ? ActionText : base.ActivateText;

    public override void Activate() => run();

    partial void OnActionTextChanged(string value) => NotifyActionsChanged();

    partial void OnIsEnabledChanged(bool value) => NotifyActionsChanged();
}

/// <summary>A row that only shows something, with more about it in the info panel (e.g. "Nachteile").</summary>
public partial class InfoRowViewModel(string key, string title) : SettingsRowViewModel(key, title)
{
    /// <summary>A short value on the right, e.g. a version; empty shows nothing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValue))]
    public partial string ValueText { get; set; } = string.Empty;

    public bool HasValue => ValueText.Length > 0;
}

/// <summary>The colour of a row's lamp (owner, 2026-10-24).</summary>
public enum RowLamp
{
    /// <summary>Grey: on, but resting for now, e.g. SISR paused for a game.</summary>
    Resting,

    /// <summary>Green: on and ready.</summary>
    Working,

    /// <summary>Yellow: a warning below the row needs the user.</summary>
    Problem,

    /// <summary>Red: an error below the row, e.g. SISR keeps crashing.</summary>
    Failure,
}
