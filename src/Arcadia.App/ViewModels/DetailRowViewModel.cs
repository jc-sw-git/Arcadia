using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A row of an area on the detail page (#499): a game's setting with its value (◀ ▶ change it, A counts on) or an action
/// (A starts it). A row that doesn't apply now stays, greyed out; it still takes the focus and its reason stands in the
/// info box, so the rows don't come and go while the carousel turns.
/// </summary>
public sealed partial class DetailRowViewModel : ViewModelBase
{
    private readonly GameActionViewModel? _action;
    private readonly string? _greyedReason;
    private readonly Action<DetailRowViewModel> _point;

    /// <param name="action">What the row does; null when it doesn't apply to the game now.</param>
    /// <param name="greyedReason">Why it doesn't apply, when <paramref name="action"/> is null.</param>
    /// <param name="value">The value shown while there is no action to name it, for a setting.</param>
    /// <param name="point">Focuses the row: the mouse over it or a click.</param>
    public DetailRowViewModel(
        GameActionKind kind, string title, string subtitle, string infoText, GameActionViewModel? action, string? greyedReason, string? value,
        Action<DetailRowViewModel> point)
    {
        Kind = kind;
        Title = title;
        Subtitle = subtitle;
        InfoText = infoText;
        _action = action;
        _greyedReason = greyedReason;
        _point = point;
        IsChoice = action?.IsChoice ?? value is not null;
        ValueText = action is { IsChoice: true } ? action.ButtonLabel : value ?? string.Empty;
    }

    public GameActionKind Kind { get; }

    /// <summary>What the quick selection stores the row by (#500).</summary>
    public string QuickId => DetailAreas.QuickIdOf(Kind);

    /// <summary>Its icon as a quick selection entry in "Übersicht" (#500).</summary>
    public Geometry QuickIcon => DetailAreas.QuickIconOf(Kind);

    public string Title { get; }

    /// <summary>One line below the title; the info box adds the rest (`docs/product/ui-texts.md`).</summary>
    public string Subtitle { get; }

    /// <summary>What the row does, for the info box below <see cref="ValueBoxes"/>; empty when the boxes say it all.</summary>
    public string InfoText { get; }

    public bool HasInfoText => InfoText.Length > 0;

    /// <summary>One box per value below the row's name in the info box (#506), the current one highlighted; empty for an action.</summary>
    public IReadOnlyList<ValueBox> ValueBoxes
    {
        get;
        init
        {
            field = value;
            ValueBox.ShowCurrent(value, ValueText);
        }
    } = [];

    public bool HasValueBoxes => ValueBoxes.Count > 0;

    /// <summary>
    /// The own tags the game has, listed in the info box below <see cref="InfoText"/> (#533); null for a row that doesn't
    /// list tags, empty for a game without any.
    /// </summary>
    public IReadOnlyList<string>? InfoTags { get; init; }

    public bool HasInfoTags => InfoTags is { Count: > 0 };

    public bool HasNoInfoTags => InfoTags is { Count: 0 };

    /// <summary>A value with ◀ ▶ beside it, not an action.</summary>
    public bool IsChoice { get; }

    public bool IsAction => !IsChoice;

    /// <summary>An action that names a value beside its title, e.g. "2 Tags" (#495); A still starts the action.</summary>
    public bool HasActionValue => IsAction && ValueText.Length > 0;

    [ObservableProperty]
    public partial string ValueText { get; private set; }

    public bool IsDanger => Kind == GameActionKind.Uninstall;

    /// <summary>
    /// Doesn't apply now: drawn greyed out, ◀ ▶ do nothing. Pan's broken emulation greys the mode too, but A on it still leads
    /// to the settings row that says why (#242).
    /// </summary>
    public bool IsGreyed => _action is not { IsEnabled: true, IsBroken: false };

    /// <summary>
    /// Why the row is greyed out, or, for an available action that does something else than its name says (uninstall in the
    /// launcher only), what it does instead; null otherwise. The info box shows it below <see cref="InfoText"/>.
    /// </summary>
    public string? Note => IsGreyed ? _action?.Note ?? _greyedReason : IsAction ? _action?.Note : null;

    public bool HasNote => !string.IsNullOrEmpty(Note);

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>◀ (-1) or ▶ (1): the value one step back or on, at once.</summary>
    public void Change(int delta)
    {
        if (IsChoice && _action is not null)
        {
            _action.Step(delta);
            ValueText = _action.ButtonLabel;
            ValueBox.ShowCurrent(ValueBoxes, ValueText);
        }
    }

    /// <summary>
    /// The same setting shown elsewhere on the page (a quick selection entry and its row, #500) changed: the value follows,
    /// both act on the same game.
    /// </summary>
    public void FollowValue()
    {
        if (IsChoice && _action is not null)
        {
            ValueText = _action.ButtonLabel;
            ValueBox.ShowCurrent(ValueBoxes, ValueText);
        }
    }

    /// <summary>A: starts the action; on a value it counts on like ▶, so A alone reaches every value (as in the settings).</summary>
    public void Activate()
    {
        if (_action is null)
        {
            return;
        }

        if (IsChoice && !_action.IsBroken)
        {
            Change(1);
            return;
        }

        _action.Invoke();
    }

    /// <summary>The mouse moved over the row: it takes the focus, so the info box explains it before a click acts.</summary>
    public void Point() => _point(this);

    /// <summary>A click: focus, then what A does on an action. A value changes only through its arrows.</summary>
    [RelayCommand]
    private void Click()
    {
        Point();
        if (IsAction)
        {
            Activate();
        }
    }

    [RelayCommand]
    private void StepBack()
    {
        Point();
        Change(-1);
    }

    [RelayCommand]
    private void StepOn()
    {
        Point();
        Change(1);
    }
}
