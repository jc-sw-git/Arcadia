using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>A launcher tab in the header.</summary>
public partial class LauncherTabViewModel : ViewModelBase
{
    public LauncherTabViewModel(LauncherLibrary library, LauncherBadge badge, Action<LauncherTabViewModel> select)
    {
        Launcher = library.Launcher;
        Badge = badge;
        Games = library.Games;
        IsFound = library.IsFound;
        ScanFailed = library.ScanFailed;
        SelectCommand = new RelayCommand(() => select(this));
    }

    /// <summary>Replaced when the user renames the launcher or gives it another keyword (#137).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    public partial LauncherInfo Launcher { get; set; }

    /// <summary>Its logo (#138); replaced when the user picks another or renames it.</summary>
    [ObservableProperty]
    public partial LauncherBadge Badge { get; set; }

    /// <summary>Replaced when the library is rescanned, e.g. after an installation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count))]
    public partial IReadOnlyList<Game> Games { get; set; }

    /// <summary>False if the launcher is active but no longer installed: the tab says so instead of showing games (#136).</summary>
    [ObservableProperty]
    public partial bool IsFound { get; set; }

    /// <summary>Its last scan failed: it shows the games read before, or says so when there are none (#369).</summary>
    [ObservableProperty]
    public partial bool ScanFailed { get; set; }

    public LauncherLibrary Library => new(Launcher, Games, IsFound, ScanFailed);

    public string Name => Launcher.DisplayName;

    public int Count => Games.Count;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IRelayCommand SelectCommand { get; }
}
