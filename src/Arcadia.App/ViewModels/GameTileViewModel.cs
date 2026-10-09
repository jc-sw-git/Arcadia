using Arcadia.App.Services;
using Arcadia.Core.Models;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>One cover in the grid, plus everything the preview panel shows for it.</summary>
public partial class GameTileViewModel : ViewModelBase
{
    public GameTileViewModel(Game game, LauncherInfo launcher, DateTimeOffset now, Action<GameTileViewModel> focus, Action<GameTileViewModel> activate)
    {
        Game = game;
        (CoverBrush, AccentBrush) = Formatting.CoverBrushes(game.Title);
        LauncherName = launcher.DisplayName;
        LauncherMonogram = launcher.DisplayName[..1];
        LauncherBrush = Formatting.LauncherTileBrush(launcher.Id);
        LastPlayedText = Formatting.LastPlayed(game.LastPlayedAt, now);
        PlaytimeText = Formatting.Playtime(game.Playtime);
        SizeText = Formatting.Size(game.InstallSizeBytes);
        FocusCommand = new RelayCommand(() => focus(this));
        ActivateCommand = new RelayCommand(() => activate(this));
    }

    public Game Game { get; }

    public string Title => Game.Title;

    public string Monogram => Game.Title[..1].ToUpperInvariant();

    public IBrush CoverBrush { get; }

    public IBrush AccentBrush { get; }

    public bool UpdateAvailable => Game.UpdateAvailable;

    public string PrimaryActionText => Game.UpdateAvailable ? "Update starten" : "Spielen";

    public string LauncherName { get; }

    public string LauncherMonogram { get; }

    public IBrush LauncherBrush { get; }

    public string LastPlayedText { get; }

    public string PlaytimeText { get; }

    public string SizeText { get; }

    public string LocationText => Game.InstallDirectory ?? "–";

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand FocusCommand { get; }

    public IRelayCommand ActivateCommand { get; }
}
