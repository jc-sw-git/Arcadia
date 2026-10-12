using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Core.Models;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>A launcher tab in the header.</summary>
public partial class LauncherTabViewModel : ViewModelBase
{
    public LauncherTabViewModel(LauncherLibrary library, Action<LauncherTabViewModel> select)
    {
        Launcher = library.Launcher;
        Games = library.Games;
        TileBrush = Formatting.LauncherTileBrush(Launcher.Id);
        SelectCommand = new RelayCommand(() => select(this));
    }

    public LauncherInfo Launcher { get; }

    /// <summary>Replaced when the library is rescanned, e.g. after an installation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Count))]
    public partial IReadOnlyList<Game> Games { get; set; }

    public string Name => Launcher.DisplayName;

    public string Monogram => Launcher.DisplayName[..1];

    public int Count => Games.Count;

    /// <summary>Placeholder until launcher logos (default or user-uploaded) are supported.</summary>
    public IBrush TileBrush { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public IRelayCommand SelectCommand { get; }
}
