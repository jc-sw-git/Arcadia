using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Core.Navigation;

namespace Arcadia.App.Filter;

/// <summary>
/// "Installiert" (one of three, a radio per line) and "Typ" (several at once, in a grid of <see cref="TypeColumns"/> with
/// their icons, "Alle Typen" first), #44 part A, design T44Modal.
/// </summary>
public sealed class FilterOptionsPane : FilterPane
{
    /// <summary>The type values stand in a grid of this many columns (design T44Modal).</summary>
    public const int TypeColumns = 3;

    private static readonly (InstalledChoice Choice, string Label)[] InstalledOptions =
    [
        (InstalledChoice.All, AppTexts.FilterAll),
        (InstalledChoice.Installed, AppTexts.FilterOnlyInstalled),
        (InstalledChoice.NotInstalled, AppTexts.FilterOnlyNotInstalled),
    ];

    private readonly List<(FilterValueViewModel Value, Func<GameFilter, GameFilter> Choose, Func<GameFilter, FilterCounts, (bool Selected, int Count)> Show)> _options = [];
    private int _focused;

    public FilterOptionsPane(FilterCategory category, IFilterPaneHost host)
        : base(category, host)
    {
        if (category == FilterCategory.Installed)
        {
            foreach (var (choice, label) in InstalledOptions)
            {
                Add(label, null, filter => filter.WithInstalled(choice), (filter, counts) => (filter.Installed == choice, counts.Of(choice)));
            }
        }
        else
        {
            Add(AppTexts.FilterAllTypes, null, filter => filter.WithAllTypes(), (filter, counts) => (filter.Types.Count == 0, counts.WithInstalled));
            foreach (var type in host.Setup.Offer.Types)
            {
                Add(GameTypeTexts.PluralName(type), GameTypeTexts.IconKey(type), filter => filter.WithType(type, !filter.Types.Contains(type)),
                    (filter, counts) => (filter.Types.Contains(type), counts.Of(type)));
            }
        }

        Values = [.. _options.Select(option => option.Value)];
    }

    public IReadOnlyList<FilterValueViewModel> Values { get; }

    /// <summary>"Installiert": one of three, one per line.</summary>
    public bool ShowsRadios => Category == FilterCategory.Installed;

    public bool ShowsTypes => Category == FilterCategory.Type;

    public override bool MovesInGrid => ShowsTypes;

    public override string AcceptText => ShowsTypes ? AppTexts.FilterHintToggle : AppTexts.Select;

    public override bool Handle(UiAction action)
    {
        var columns = ShowsTypes ? TypeColumns : 1;
        switch (action)
        {
            // Round at the ends like the library's grid (#476); from a full row onto a shorter last one: the last value.
            case UiAction.Up:
                Focus(GridFocus.Step(_focused, Values.Count, Values.Count, columns, 0, -1, IsRepeat));
                break;
            case UiAction.Down:
                Focus(GridFocus.Step(_focused, Values.Count, Values.Count, columns, 0, 1, IsRepeat));
                break;
            // Left out of the first column goes back to the categories; right goes on into the next row.
            case UiAction.Left when _focused % columns == 0:
            case UiAction.Back:
                return false;
            case UiAction.Left:
                Focus(_focused - 1);
                break;
            case UiAction.Right when columns > 1:
                Focus(GridFocus.Step(_focused, Values.Count, Values.Count, columns, 1, 0, IsRepeat));
                break;
            case UiAction.Accept:
                Choose(_focused);
                break;
        }

        return true;
    }

    /// <summary>A click on a value: it takes the focus and is chosen.</summary>
    public void Pick(FilterValueViewModel value)
    {
        Host.FocusValues(this);
        Focus(Values.ToList().IndexOf(value));
        Choose(_focused);
    }

    public override void Show(GameFilter chosen, FilterCounts counts)
    {
        foreach (var (value, _, show) in _options)
        {
            var (selected, count) = show(chosen, counts);
            value.IsSelected = selected;
            value.Count = count;
            value.CountText = CountOf(count);
        }
    }

    public override (string Summary, string Fallback, bool IsChosen) Summarize(GameFilter chosen)
    {
        if (ShowsRadios)
        {
            var label = InstalledOptions.First(option => option.Choice == chosen.Installed).Label;
            return (label, label, chosen.Installed != InstalledChoice.All);
        }

        var types = chosen.Types;
        var summary = types.Count == 0 ? AppTexts.FilterAllTypes : string.Join(", ", types.Select(GameTypeTexts.PluralName));
        return (summary, types.Count > 1 ? AppTexts.FilterSeveral : summary, types.Count > 0);
    }

    // A single choice starts on the chosen value, a multiple one on its first ("Alle Typen") (design T44Modal).
    protected override void Entered() => Focus(ShowsRadios ? Math.Max(0, Values.ToList().FindIndex(value => value.IsSelected)) : 0);

    protected override void Left() => Values[_focused].IsFocused = false;

    private void Focus(int index)
    {
        Values[_focused].IsFocused = false;
        _focused = index;
        Values[index].IsFocused = IsActive;
    }

    private void Choose(int index) => Host.Change(_options[index].Choose);

    private void Add(string label, string? iconKey, Func<GameFilter, GameFilter> choose, Func<GameFilter, FilterCounts, (bool Selected, int Count)> show) =>
        _options.Add((new FilterValueViewModel(label, iconKey, value => Pick(value)), choose, show));
}
