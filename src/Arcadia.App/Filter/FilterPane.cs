using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.Keyboard;
using Arcadia.App.Settings;
using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Filter;

/// <summary>What the filter modal counts and offers (#44, #466): the tab's entries and facts as they were when it opened.</summary>
/// <param name="ThisYear">"Letzte 3 Jahre" counts back from it.</param>
/// <param name="MetadataLoading">A metadata run goes on: the counts may change once the modal closes (#466 case 16).</param>
public sealed record FilterModalSetup(
    string LauncherName,
    IReadOnlyList<Game> Games,
    FilterFacts Facts,
    FilterOffer Offer,
    int ThisYear,
    bool MetadataLoading = false)
{
    /// <summary>The setup of a tab without metadata, as part A opened it.</summary>
    public static FilterModalSetup Of(string launcherName, IReadOnlyList<Game> games) =>
        new(launcherName, games, FilterFacts.None, FilterOffer.Of(games), DateTime.Now.Year);
}

/// <summary>What a category's values ask of the modal.</summary>
public interface IFilterPaneHost
{
    FilterModalSetup Setup { get; }

    /// <summary>The choice so far; it applies when the modal closes.</summary>
    GameFilter Chosen { get; }

    /// <summary>Changes the choice; the counts follow.</summary>
    void Change(Func<GameFilter, GameFilter> change);

    /// <summary>A text written in the values (the value search), with the on-screen keyboard over the modal.</summary>
    TextEditorViewModel Write(string text, Action<string> typed, Action closed);

    /// <summary>A click in the values of <paramref name="pane"/>: the focus goes into them first.</summary>
    void FocusValues(FilterPane pane);

    /// <summary>The focus moved within the values: the footer's hints follow.</summary>
    void HintsChanged();
}

/// <summary>
/// The right side of the filter modal for one category (#44, #466): its values, where the focus is among them and what
/// the footer offers. The modal hands it the presses while the focus is in the values and the counts whenever they change.
/// </summary>
public abstract class FilterPane(FilterCategory category, IFilterPaneHost host) : ViewModelBase
{
    public FilterCategory Category => category;

    protected IFilterPaneHost Host => host;

    /// <summary>The focus is in these values.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The footer's hint for ▲▼ (or the D-pad, <see cref="MovesInGrid"/>), A, X, LT/RT, L3 and ◀▶; null hides one.</summary>
    public virtual string MoveText => AppTexts.FilterValue;

    public virtual bool MovesInGrid => false;

    public virtual string AcceptText => AppTexts.Select;

    public virtual string? SearchText => null;

    public virtual string? PageText => null;

    public virtual string? OrderText => null;

    public virtual string? StepText => null;

    public virtual string? StepTenText => null;

    /// <summary>The on-screen keyboard is open over the modal: R3 is its layout menu, B puts it away.</summary>
    public virtual bool IsWriting => false;

    /// <summary>The focus goes into the values.</summary>
    public void Enter()
    {
        IsActive = true;
        OnPropertyChanged(nameof(IsActive));
        Entered();
    }

    /// <summary>The focus goes back to the categories; what the values only kept while they had it (a search) is cleared.</summary>
    public void Leave()
    {
        IsActive = false;
        OnPropertyChanged(nameof(IsActive));
        Left();
    }

    /// <summary>A press in the values; false when it leaves them (B, ◀ at the left edge).</summary>
    public abstract bool Handle(UiAction action);

    /// <summary>The choice or its counts changed: selection, counts and greyed values follow.</summary>
    public abstract void Show(GameFilter chosen, FilterCounts counts);

    /// <summary>The category's choice in words for the left column; the second is the fallback where it doesn't fit.</summary>
    public abstract (string Summary, string Fallback, bool IsChosen) Summarize(GameFilter chosen);

    protected abstract void Entered();

    protected virtual void Left()
    {
    }

    /// <summary>"… & o. Angabe" when the switch is on (#44).</summary>
    protected static string WithUnknownTail(string summary, bool withUnknown) =>
        withUnknown ? AppTexts.FilterSummaryWithUnknown(summary) : summary;

    protected static string CountOf(int count) => Formatting.Count(count);

    protected static bool IsRepeat => HeldPress.IsRepeat;
}
