using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Launchers.Abstractions;
using Arcadia.Core.Navigation;
using Arcadia.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>A row of an area that "Übersicht anpassen" offers for the quick selection (#500), with the area it is in.</summary>
public sealed record QuickChoice(string Id, string Title, string AreaName);

/// <summary>What "Übersicht anpassen" (#500) needs from the window: storing a change at once, and closing.</summary>
public interface ICustomizeOverviewHost
{
    /// <summary>A tick or an untick: the game's own selection from now on.</summary>
    void StoreQuickSelection(Game game, IReadOnlyList<string> ids);

    /// <summary>"Ersetzen": the selection for every game, also games added later; every game's own selection goes.</summary>
    void ApplyQuickSelectionToAllGames(IReadOnlyList<string> ids);

    void Close(CustomizeOverviewViewModel dialog);
}

/// <summary>Where "Für alle Spiele übernehmen" stands: offered, asked about, or done.</summary>
public enum ApplyToAllState
{
    Offered,
    Asking,
    Applied,
}

/// <summary>
/// "Übersicht anpassen" (#500, design RD6Mischung): every row of the other areas, grouped by area, to tick for the game's
/// quick selection. A ticks or unticks and holds at once, up to five ("n von 5"; with five the others are greyed and passed
/// over). ▼ after the last entry reaches "Für alle Spiele übernehmen", which asks in place first. B closes; no save, no cancel.
/// </summary>
public sealed partial class CustomizeOverviewViewModel : ViewModelBase
{
    private readonly Game _game;
    private readonly ICustomizeOverviewHost _host;
    private int _focus;

    public CustomizeOverviewViewModel(GameTileViewModel tile, IReadOnlyList<QuickChoice> choices, IReadOnlyList<string> ticked, ICustomizeOverviewHost host)
    {
        _game = tile.Game;
        _host = host;
        Subtitle = AppTexts.QuickSelectionDialogSubtitle(tile.Title);
        Entries = [.. choices.Select(choice => new QuickChoiceViewModel(choice, ticked.Contains(choice.Id, StringComparer.Ordinal), Click))];
        Groups = [.. Entries.GroupBy(entry => entry.AreaName).Select(group => new QuickChoiceGroup(group.Key.ToUpper(Formatting.Culture), [.. group]))];
        ShowState();
    }

    public string Subtitle { get; }

    /// <summary>Every entry in the order of the areas; the focus walks them across the groups.</summary>
    public IReadOnlyList<QuickChoiceViewModel> Entries { get; }

    public IReadOnlyList<QuickChoiceGroup> Groups { get; }

    /// <summary>The design's two columns: the first area left, the others right.</summary>
    public IReadOnlyList<QuickChoiceGroup> LeftGroups => [.. Groups.Take(1)];

    public IReadOnlyList<QuickChoiceGroup> RightGroups => [.. Groups.Skip(1)];

    /// <summary>"3 von 5".</summary>
    [ObservableProperty]
    public partial string CountText { get; private set; } = string.Empty;

    /// <summary>Five are ticked: the others are greyed out and a note says to untick one first.</summary>
    [ObservableProperty]
    public partial bool IsFull { get; private set; }

    public static string FullNote => AppTexts.QuickSelectionFull(QuickSelectionSettings.MaxEntries);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffered), nameof(IsAsking), nameof(IsApplied), nameof(AcceptText), nameof(BackText), nameof(MovesSideways))]
    public partial ApplyToAllState ApplyState { get; private set; }

    /// <summary>The explanation beside the button before it was used.</summary>
    public bool IsOffered => ApplyState == ApplyToAllState.Offered;

    /// <summary>"Schnellauswahl aller Spiele ersetzen?" with "Abbrechen" and "Ersetzen" in place of the button.</summary>
    public bool IsAsking => ApplyState == ApplyToAllState.Asking;

    /// <summary>"Für alle Spiele übernommen." beside the button, until the next tick.</summary>
    public bool IsApplied => ApplyState == ApplyToAllState.Applied;

    [ObservableProperty]
    public partial bool IsApplyFocused { get; private set; }

    /// <summary>In the question: "Ersetzen" has the focus, else "Abbrechen" (the safe answer comes first).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCancelFocused))]
    public partial bool IsReplaceFocused { get; private set; }

    public bool IsCancelFocused => IsAsking && !IsReplaceFocused;

    /// <summary>◀ ▶ choose between "Abbrechen" and "Ersetzen"; elsewhere ▲ ▼ walk the list.</summary>
    public bool MovesSideways => IsAsking;

    public string AcceptText => IsAsking ? AppTexts.Select : IsApplyFocused ? AppTexts.QuickSelectionApplyToAll : AppTexts.FilterHintToggle;

    public string BackText => IsAsking ? AppTexts.Cancel : AppTexts.Close;

    private int ApplyIndex => Entries.Count;

    public void Handle(UiAction action)
    {
        if (IsAsking)
        {
            Answer(action);
            return;
        }

        switch (action)
        {
            case UiAction.Back:
                Close();
                break;
            case UiAction.Up or UiAction.Down:
                // Greyed entries (five ticked) are passed over; "Für alle Spiele übernehmen" comes after the last (#500).
                _focus = ListFocus.Next(_focus, action == UiAction.Up ? -1 : 1, Entries.Count + 1, CanFocus, HeldPress.IsRepeat);
                ShowFocus();
                break;
            case UiAction.Accept when _focus == ApplyIndex:
                Ask();
                break;
            case UiAction.Accept:
                Toggle(Entries[_focus]);
                break;
        }
    }

    private void Answer(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left or UiAction.Right:
                IsReplaceFocused = ListFocus.Cycle(IsReplaceFocused ? 1 : 0, action == UiAction.Left ? -1 : 1, 2, HeldPress.IsRepeat) == 1;
                OnPropertyChanged(nameof(IsCancelFocused));
                break;
            case UiAction.Accept when IsReplaceFocused:
                Replace();
                break;
            case UiAction.Accept or UiAction.Back:
                CancelQuestion();
                break;
        }
    }

    private bool CanFocus(int index) => index == ApplyIndex || !Entries[index].IsGreyed;

    /// <summary>
    /// A click on an entry: the focus goes there and it is ticked or unticked, as with A. A greyed one takes neither, as
    /// the stick passes over it.
    /// </summary>
    private void Click(QuickChoiceViewModel entry)
    {
        if (IsAsking || entry.IsGreyed)
        {
            return;
        }

        _focus = Entries.ToList().IndexOf(entry);
        ShowFocus();
        Toggle(entry);
    }

    private void Toggle(QuickChoiceViewModel entry)
    {
        if (!entry.IsTicked && IsFull)
        {
            return;
        }

        entry.IsTicked = !entry.IsTicked;
        _host.StoreQuickSelection(_game, TickedIds());
        ApplyState = ApplyToAllState.Offered;
        ShowState();
    }

    /// <summary>A, a click on "Für alle Spiele übernehmen": asks in place before every other game's selection goes.</summary>
    [RelayCommand]
    private void Ask()
    {
        _focus = ApplyIndex;
        IsReplaceFocused = false;
        ApplyState = ApplyToAllState.Asking;
        ShowFocus();
    }

    [RelayCommand]
    private void Replace()
    {
        _host.ApplyQuickSelectionToAllGames(TickedIds());
        ApplyState = ApplyToAllState.Applied;
        ShowFocus();
    }

    /// <summary>"Abbrechen" or B in the question: nothing changes, the focus is on the button again.</summary>
    [RelayCommand]
    private void CancelQuestion()
    {
        ApplyState = ApplyToAllState.Offered;
        ShowFocus();
    }

    /// <summary>B, ✕ or a click beside the dialog.</summary>
    [RelayCommand]
    private void Close() => _host.Close(this);

    private IReadOnlyList<string> TickedIds() => [.. Entries.Where(entry => entry.IsTicked).Select(entry => entry.Id)];

    private void ShowState()
    {
        var ticked = Entries.Count(entry => entry.IsTicked);
        CountText = AppTexts.QuickSelectionCount(count: ticked, max: QuickSelectionSettings.MaxEntries);
        IsFull = ticked >= QuickSelectionSettings.MaxEntries;
        foreach (var entry in Entries)
        {
            entry.IsGreyed = IsFull && !entry.IsTicked;
        }

        ShowFocus();
    }

    /// <summary>Exactly one ring: an entry, the button, or one of the question's answers.</summary>
    private void ShowFocus()
    {
        for (var e = 0; e < Entries.Count; e++)
        {
            Entries[e].IsFocused = !IsAsking && e == _focus;
        }

        IsApplyFocused = !IsAsking && _focus == ApplyIndex;
        OnPropertyChanged(nameof(IsCancelFocused));
        OnPropertyChanged(nameof(AcceptText));
    }
}

/// <summary>An area's entries in "Übersicht anpassen", under the area's name.</summary>
public sealed record QuickChoiceGroup(string Caption, IReadOnlyList<QuickChoiceViewModel> Entries);

/// <summary>One entry of "Übersicht anpassen": a checkbox row.</summary>
public sealed partial class QuickChoiceViewModel : ViewModelBase
{
    private readonly Action<QuickChoiceViewModel> _click;

    public QuickChoiceViewModel(QuickChoice choice, bool isTicked, Action<QuickChoiceViewModel> click)
    {
        Id = choice.Id;
        Title = choice.Title;
        AreaName = choice.AreaName;
        IsTicked = isTicked;
        _click = click;
    }

    public string Id { get; }

    public string Title { get; }

    public string AreaName { get; }

    [ObservableProperty]
    public partial bool IsTicked { get; set; }

    /// <summary>Five others are ticked: it can't be ticked now and the focus passes over it.</summary>
    [ObservableProperty]
    public partial bool IsGreyed { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [RelayCommand]
    private void Click() => _click(this);
}
