using Arcadia.App.Services;
using Arcadia.Launchers.Abstractions;
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

    public bool IsInstalled => Game.IsInstalled;

    public bool UpdateAvailable => Game.IsInstalled && Game.UpdateAvailable && InstallState is null;

    public string PrimaryActionText => InstallState?.Phase == InstallPhase.Uninstalling ? "Wird deinstalliert …"
        : InstallState is not null ? $"{InstallVerb} …"
        : !Game.IsInstalled ? "Installieren"
        : "Spielen";

    /// <summary>The running installation of this game, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstallActive), nameof(UpdateAvailable), nameof(ShowInstallBadge), nameof(CanActivate), nameof(PrimaryActionText),
        nameof(InstallPercent), nameof(IsInstallIndeterminate), nameof(IsInstallPaused), nameof(IsInstallQueued), nameof(IsInstallSpinning),
        nameof(InstallStatusText), nameof(InstallShortText), nameof(ShowLauncherControlsHint))]
    public partial InstallProgress? InstallState { get; set; }

    public bool IsInstallActive => InstallState is not null;

    /// <summary>Pausing and cancelling happen in the launcher; an uninstall can't be paused.</summary>
    public bool ShowLauncherControlsHint => InstallState is { Phase: not InstallPhase.Uninstalling };

    /// <summary>The "can be installed" badge; replaced by the progress while an installation runs.</summary>
    public bool ShowInstallBadge => !Game.IsInstalled && InstallState is null;

    /// <summary>False while an installation runs, so it can't be requested twice.</summary>
    public bool CanActivate => InstallState is null;

    public double InstallPercent => (InstallState?.Fraction ?? 0) * 100;

    public bool IsInstallIndeterminate => InstallState is { } state
        && (state.Fraction is null || state.Phase is InstallPhase.AwaitingConfirmation or InstallPhase.Queued or InstallPhase.Finishing or InstallPhase.Uninstalling);

    public bool IsInstallPaused => InstallState?.Phase == InstallPhase.Paused;

    /// <summary>Waiting behind other installations; shown with an hourglass instead of the spinner.</summary>
    public bool IsInstallQueued => InstallState?.Phase == InstallPhase.Queued;

    public bool IsInstallSpinning => InstallState is not null && !IsInstallPaused && !IsInstallQueued;

    public string InstallStatusText => InstallState switch
    {
        null => string.Empty,
        { Phase: InstallPhase.AwaitingConfirmation } => $"Bitte in {LauncherName} bestätigen",
        { Phase: InstallPhase.Queued, QueuePosition: { } position } => $"In der Warteschlange · Platz {position}",
        { Phase: InstallPhase.Queued } => "In der Warteschlange",
        { Phase: InstallPhase.Paused, Fraction: { } f } => $"Pausiert · {Formatting.Percent(f)}",
        { Phase: InstallPhase.Paused } => "Pausiert",
        { Phase: InstallPhase.Finishing } => "Wird abgeschlossen …",
        { Phase: InstallPhase.Uninstalling } => "Wird deinstalliert …",
        { Fraction: { } f } => $"{InstallVerb} · {Formatting.Percent(f)}",
        _ => $"{InstallVerb} …",
    };

    // An installation of a game that is already installed is an update.
    private string InstallVerb => Game.IsInstalled ? "Wird aktualisiert" : "Wird installiert";

    /// <summary>Fits the badge on a cover.</summary>
    public string InstallShortText => InstallState switch
    {
        null => string.Empty,
        { Phase: InstallPhase.AwaitingConfirmation } => "Bestätigen",
        { Phase: InstallPhase.Queued, QueuePosition: { } position } => $"Wartet · {position}",
        { Phase: InstallPhase.Queued } => "Wartet",
        { Phase: InstallPhase.Paused } => "Pausiert",
        { Phase: InstallPhase.Finishing } => "Fast fertig",
        { Phase: InstallPhase.Uninstalling } => "Entfernen",
        { Fraction: { } f } => Formatting.Percent(f),
        _ => "Lädt",
    };

    public string LauncherName { get; }

    public string LauncherMonogram { get; }

    public IBrush LauncherBrush { get; }

    public string LastPlayedText { get; }

    public string PlaytimeText { get; }

    public string SizeText { get; }

    public string LocationText => Game.IsInstalled ? Game.InstallDirectory ?? "–" : "Nicht installiert";

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A is held on this game; the ring around the preview fills until the detail page opens.</summary>
    [ObservableProperty]
    public partial bool IsHolding { get; set; }

    public IRelayCommand FocusCommand { get; }

    public IRelayCommand ActivateCommand { get; }
}
