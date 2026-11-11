using Arcadia.App.Input;
using Arcadia.App.ViewModels;
using Arcadia.Core.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Status;

/// <summary>What the status modal asks of the window: closing it, the way into the settings and the update.</summary>
public interface IStatusModalHost
{
    void Close(StatusModalViewModel modal);

    /// <summary>Closes the modal and opens the entry's category on its row; with <paramref name="startFix"/> its fix starts there.</summary>
    void OpenSettingsOf(StatusEntry entry, bool startFix);

    /// <summary>"Jetzt aktualisieren": Arcadia closes, installs and restarts; a failure is an error toast.</summary>
    void InstallUpdate();

    /// <summary>L3 switched the order; it is saved for every opening.</summary>
    void OrderChanged(bool newestFirst);
}

/// <summary>
/// "Status" (#166, designs T166Modal/T166Empty): the header's messages as a list. ▲ ▼ choose an entry, ◀ ▶ its button,
/// A presses it, L3 switches between "Dringlichkeit" and "Neueste zuerst", B closes. The list follows the header live;
/// the focus stays on its entry, or moves to the next when that one goes. Without entries it says all is done.
/// </summary>
public sealed partial class StatusModalViewModel : ViewModelBase
{
    public static string Title => AppTexts.StatusTitle;

    public static string UrgencyText => AppTexts.StatusUrgency;

    public static string NewestText => AppTexts.NewestFirst;

    // The empty state's text (owner), "Alles erledigt!" set large (design T166Empty).
    public static string EmptyLead => AppTexts.StatusEmpty;

    public static string EmptyHeadline => AppTexts.StatusAllDone;

    public static string EmptyPraise => AppTexts.StatusPraise;

    private readonly IStatusModalHost _host;
    private readonly Dictionary<string, StatusEntryViewModel> _byId = [];
    private IReadOnlyList<StatusEntry> _shown = [];

    public StatusModalViewModel(IReadOnlyList<StatusEntry> entries, bool newestFirst, IStatusModalHost host)
    {
        _host = host;
        NewestFirst = newestFirst;
        Show(entries);
        if (Entries.Count > 0)
        {
            Focus(Entries[0]);
        }
    }

    /// <summary>The entries in the chosen order.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(HasEntries), nameof(HasSeveral), nameof(CountText))]
    public partial IReadOnlyList<StatusEntryViewModel> Entries { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusedHasFix), nameof(AcceptText))]
    public partial StatusEntryViewModel? Focused { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrderText))]
    public partial bool NewestFirst { get; private set; }

    /// <summary>The last entry went while the modal was open: "Alles erledigt!" (owner).</summary>
    public bool IsEmpty => Entries.Count == 0;

    public bool HasEntries => !IsEmpty;

    /// <summary>▲ ▼ have somewhere to go.</summary>
    public bool HasSeveral => Entries.Count > 1;

    public bool FocusedHasFix => Focused?.HasFix == true;

    public string AcceptText => Focused?.FocusedLabel ?? string.Empty;

    public string CountText => Entries.Count == 1 ? AppTexts.MessageCountOne : AppTexts.MessageCountMany(Entries.Count);

    public string OrderText => NewestFirst ? NewestText : UrgencyText;

    /// <summary>The focus moved to another entry, so the view brings it into sight.</summary>
    public event EventHandler? FocusMoved;

    /// <summary>The header's messages changed: entries come and go, the focus stays where it is (UI thread).</summary>
    public void Show(IReadOnlyList<StatusEntry> entries)
    {
        _shown = entries;
        foreach (var gone in _byId.Keys.Where(id => entries.All(entry => entry.Id != id)).ToList())
        {
            _byId.Remove(gone);
        }

        foreach (var entry in entries)
        {
            if (_byId.TryGetValue(entry.Id, out var shown))
            {
                shown.Entry = entry;
            }
            else
            {
                _byId[entry.Id] = new StatusEntryViewModel(entry, Press);
            }
        }

        Arrange();

        // The focused entry's fix can come or go (an update starts loading), and with it what A does.
        OnPropertyChanged(nameof(FocusedHasFix));
        OnPropertyChanged(nameof(AcceptText));
    }

    /// <summary>A press while the modal is open; it takes every one.</summary>
    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                Move(-1);
                break;
            case UiAction.Down:
                Move(1);
                break;
            case UiAction.Left or UiAction.Right when Focused is { HasFix: true } entry:
                // Two buttons side by side: a step past either goes round to the other (#476).
                entry.FixChosen = ListFocus.Cycle(entry.FixChosen ? 0 : 1, action == UiAction.Left ? -1 : 1, 2, HeldPress.IsRepeat) == 0;
                OnPropertyChanged(nameof(AcceptText));
                break;
            case UiAction.Accept when Focused is { } entry:
                Press(entry, entry.FixChosen && entry.HasFix);
                break;
            case UiAction.ViewOptions when HasEntries:
                NewestFirst = !NewestFirst;
                Arrange();
                _host.OrderChanged(NewestFirst);
                break;
            case UiAction.Back:
                _host.Close(this);
                break;
        }
    }

    /// <summary>The ✕ and a click beside the modal.</summary>
    [RelayCommand]
    private void Close() => _host.Close(this);

    private void Press(StatusEntryViewModel entry, bool fix)
    {
        if (fix && entry.Entry.Target == StatusTarget.ArcadiaUpdate)
        {
            // Runs from here: the modal stays, and so does the entry if the update fails.
            _host.InstallUpdate();
            return;
        }

        _host.OpenSettingsOf(entry.Entry, startFix: fix);
    }

    /// <summary>Puts the entries in the chosen order; the focus keeps its entry, or takes the one now where it stood.</summary>
    private void Arrange()
    {
        var focusedIndex = Focused is { } focused ? Entries.ToList().IndexOf(focused) : -1;
        var ordered = (NewestFirst
                ? _shown.OrderByDescending(entry => entry.Since)
                : _shown.OrderByDescending(entry => entry.Level).ThenByDescending(entry => entry.Since))
            .Select(entry => _byId[entry.Id])
            .ToList();
        Entries = ordered;

        if (Focused is { } kept && ordered.Contains(kept))
        {
            return;
        }

        if (ordered.Count == 0)
        {
            SetFocus(null);
            return;
        }

        Focus(ordered[Math.Clamp(focusedIndex, 0, ordered.Count - 1)]);
    }

    private void Move(int step)
    {
        if (Entries.Count == 0)
        {
            return;
        }

        var index = Focused is { } focused ? Entries.ToList().IndexOf(focused) : -1;
        var next = ListFocus.Cycle(index, step, Entries.Count, HeldPress.IsRepeat);
        if (next != index)
        {
            Focus(Entries[next]);
        }
    }

    /// <summary>A newly focused entry starts on its first button: the fix where it has one (designs).</summary>
    private void Focus(StatusEntryViewModel entry)
    {
        entry.FixChosen = entry.HasFix;
        SetFocus(entry);
    }

    private void SetFocus(StatusEntryViewModel? entry)
    {
        if (Focused is { } before)
        {
            before.IsFocused = false;
        }

        Focused = entry;
        if (entry is not null)
        {
            entry.IsFocused = true;
        }

        OnPropertyChanged(nameof(AcceptText));
        FocusMoved?.Invoke(this, EventArgs.Empty);
    }
}
