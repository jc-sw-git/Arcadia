using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.Core.Search;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Setup;

/// <summary>
/// A launcher in the first start's question (#139): logo, name, whether it was found and with how many games, and the
/// switch "An" / "Aus". One that wasn't found is greyed out and can't be switched on.
/// </summary>
public sealed partial class FirstRunLauncherViewModel : ViewModelBase
{
    private readonly Action<FirstRunLauncherViewModel, bool> _switch;
    private readonly Action<FirstRunLauncherViewModel> _pointed;

    // Its games as read; null before they are, or when it wasn't found.
    private IReadOnlyList<Game>? _games;
    private string? _location;

    public FirstRunLauncherViewModel(LauncherInfo launcher, LauncherBadge badge, Action<FirstRunLauncherViewModel, bool> switchTo, Action<FirstRunLauncherViewModel> pointed)
    {
        Launcher = launcher;
        Badge = badge;
        Keyword = $"{SearchQuery.PrefixMarker}{launcher.SearchKeyword}";
        _switch = switchTo;
        _pointed = pointed;
    }

    public LauncherInfo Launcher { get; }

    public string Name => Launcher.DisplayName;

    /// <summary>Its logo, as its tab shows it (#138).</summary>
    public LauncherBadge Badge { get; }

    /// <summary>The search's keyword for it, e.g. "/steam".</summary>
    public string Keyword { get; }

    /// <summary>Installed on this PC; only then it can be switched on.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDecrease), nameof(CanIncrease), nameof(StatusText), nameof(StateText))]
    public partial bool IsFound { get; private set; }

    /// <summary>Becomes a tab after "Fertig".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValueText), nameof(CanDecrease), nameof(CanIncrease), nameof(StateText))]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>The first row of its group ("Gefunden", "Nicht gefunden"): the group's box begins above it.</summary>
    [ObservableProperty]
    public partial bool StartsGroup { get; set; }

    /// <summary>The last row of its group: the group's box ends below it.</summary>
    [ObservableProperty]
    public partial bool EndsGroup { get; set; }

    /// <summary>Its place among the tabs from 1 while on; 0 while off.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText))]
    public partial int TabNumber { get; set; }

    public string ValueText => IsOn ? "An" : "Aus";

    public bool CanDecrease => IsFound && IsOn;

    public bool CanIncrease => IsFound && !IsOn;

    /// <summary>The line below its name, e.g. "Gefunden · 128 Spiele".</summary>
    public string StatusText => (IsFound, _games?.Count) switch
    {
        (false, _) => "Auf diesem PC nicht gefunden",
        (true, null) => "Gefunden",
        (true, 0) => "Gefunden · noch keine Spiele",
        (true, var count) => $"Gefunden · {GameCount(count.Value)}",
    };

    /// <summary>The line below its name in the info panel, e.g. "An · wird Tab 2".</summary>
    public string StateText => (IsFound, IsOn) switch
    {
        (false, _) => "Nicht installiert",
        (true, true) => $"An · wird Tab {TabNumber}",
        _ => "Aus · bekommt keinen Tab",
    };

    public string GamesText
    {
        get
        {
            if (_games is not { } games)
            {
                return "–";
            }

            var installed = games.Count(g => g.IsInstalled);
            return installed == games.Count ? GameCount(games.Count) : $"{games.Count} · {installed} installiert";
        }
    }

    /// <summary>Where it was found, e.g. "C:\Program Files (x86)\Steam".</summary>
    public string LocationText => _location ?? "Nicht gefunden";

    /// <param name="games">Its games as read; null if it wasn't read.</param>
    public void Show(bool isFound, string? location, IReadOnlyList<Game>? games)
    {
        IsFound = isFound;
        _location = location;
        _games = isFound ? games : null;
        if (!isFound)
        {
            IsOn = false;
        }

        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(GamesText));
        OnPropertyChanged(nameof(LocationText));
    }

    /// <summary>◀ (negative) switches it off, ▶ on.</summary>
    public void Change(int delta)
    {
        if (delta < 0 ? CanDecrease : CanIncrease)
        {
            _switch(this, delta > 0);
        }
    }

    /// <summary>A and a click on the switch flip it.</summary>
    public void Toggle()
    {
        if (IsFound)
        {
            _switch(this, !IsOn);
        }
    }

    /// <summary>The mouse moved over the row: it takes the focus, so the info panel explains it before a click changes it.</summary>
    public void Point() => _pointed(this);

    /// <summary>A click on the row: focus only; the switch is the target that changes it.</summary>
    [RelayCommand]
    private void Click() => Point();

    /// <summary>A click anywhere on the switch flips it, as in the launcher settings (owner, 2026-10-22).</summary>
    [RelayCommand]
    private void ToggleSwitch()
    {
        Point();
        Toggle();
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

    private static string GameCount(int count) => count == 1 ? "1 Spiel" : $"{count} Spiele";
}
