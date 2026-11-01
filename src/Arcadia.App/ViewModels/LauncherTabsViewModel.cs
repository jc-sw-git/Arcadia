using System.Collections.ObjectModel;
using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The launcher tabs in the header (#136): one per active launcher in tab order, the selected one, LB/RB. Shared by the
/// library, the detail page and the search's launchers, and owned by no page (#155): pages read it and ask it to switch,
/// Main follows a switch through <see cref="Chosen"/>.
/// </summary>
public sealed partial class LauncherTabsViewModel(Func<LauncherInfo, LauncherBadge> badgeOf) : ViewModelBase
{
    public ObservableCollection<LauncherTabViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial LauncherTabViewModel? SelectedTab { get; private set; }

    /// <summary>The user picked a tab (click, LB/RB, number key); Arcadia opens with it next time.</summary>
    public event Action<LauncherTabViewModel>? Chosen;

    /// <summary>False if no tab has a game: then there is nothing to search either.</summary>
    public bool HasGames => Items.Any(t => t.Count > 0);

    /// <summary>What every tab shows, also the empty ones and launchers not found.</summary>
    public IReadOnlyList<LauncherLibrary> Libraries() => Items.Select(t => t.Library).ToList();

    /// <summary>The libraries the search covers: a tab without games has nothing to find (#136).</summary>
    public IReadOnlyList<LauncherLibrary> SearchedLibraries() => Items.Where(t => t.Count > 0).Select(t => t.Library).ToList();

    public LauncherTabViewModel? Find(string launcherId) => Items.FirstOrDefault(t => t.Launcher.Id == launcherId);

    /// <summary>Every shown game comes from a tab, so its launcher has one.</summary>
    public LauncherInfo LauncherOf(Game game) => Items.First(t => t.Launcher.Id == game.LauncherId).Launcher;

    public LauncherBadge BadgeOf(Game game) => Items.First(t => t.Launcher.Id == game.LauncherId).Badge;

    /// <summary>
    /// Takes over the scanned libraries: one tab per launcher in the scanned order, launchers that are gone lose their tab.
    /// A tab that stays keeps its selection; the selection of one that went is cleared.
    /// </summary>
    public void Show(IReadOnlyList<LauncherLibrary> libraries)
    {
        foreach (var gone in Items.Where(t => libraries.All(l => l.Launcher.Id != t.Launcher.Id)).ToList())
        {
            Items.Remove(gone);
        }

        for (var i = 0; i < libraries.Count; i++)
        {
            var library = libraries[i];
            var index = IndexOf(library.Launcher.Id);
            if (index < 0)
            {
                Items.Insert(i, new LauncherTabViewModel(library, badgeOf(library.Launcher), Choose));
                continue;
            }

            Items[index].Games = library.Games;
            Items[index].IsFound = library.IsFound;
            Items[index].ScanFailed = library.ScanFailed;
            if (index != i)
            {
                Items.Move(index, i);
            }
        }

        if (SelectedTab is { } selected && !Items.Contains(selected))
        {
            SelectedTab = null;
        }
    }

    /// <summary>A launcher was renamed, got another keyword or logo (#137, #138): its tab shows it at once.</summary>
    public void Restyle(Func<string, LauncherInfo> launcherOf)
    {
        foreach (var tab in Items)
        {
            var launcher = launcherOf(tab.Launcher.Id);
            tab.Launcher = launcher;
            tab.Badge = badgeOf(launcher);
        }
    }

    /// <summary>Selects <paramref name="tab"/> without it counting as the user's choice, e.g. the first one after a scan.</summary>
    public void Select(LauncherTabViewModel tab)
    {
        if (SelectedTab is { } previous)
        {
            previous.IsSelected = false;
        }

        tab.IsSelected = true;
        SelectedTab = tab;
    }

    /// <summary>The user picked <paramref name="tab"/>.</summary>
    public void Choose(LauncherTabViewModel tab)
    {
        Select(tab);
        Chosen?.Invoke(tab);
    }

    /// <summary>LB (-1) and RB (1): the previous or next tab, round from the last to the first.</summary>
    public void Cycle(int delta)
    {
        if (Items.Count == 0 || SelectedTab is null)
        {
            return;
        }

        Choose(Items[(Items.IndexOf(SelectedTab) + delta + Items.Count) % Items.Count]);
    }

    [RelayCommand]
    private void Previous() => Cycle(-1);

    [RelayCommand]
    private void Next() => Cycle(1);

    private int IndexOf(string launcherId)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Launcher.Id == launcherId)
            {
                return i;
            }
        }

        return -1;
    }
}
