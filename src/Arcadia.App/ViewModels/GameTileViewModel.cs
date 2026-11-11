using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>One cover in the grid, plus everything the preview panel shows for it.</summary>
public partial class GameTileViewModel : ViewModelBase
{
    private readonly DateTimeOffset _now;

    public GameTileViewModel(Game game, LauncherInfo launcher, LauncherBadge launcherBadge, DateTimeOffset now, Action<GameTileViewModel> focus, Action<GameTileViewModel> activate)
    {
        Game = game;
        _now = now;
        (CoverBrush, AccentBrush) = Formatting.CoverBrushes(game.Title);
        LauncherName = launcher.DisplayName;
        LauncherBadge = launcherBadge;
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

    /// <summary>The game's cached cover and background (#487); null until the cache has them, the placeholder shows meanwhile.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cover), nameof(Hero))]
    public partial GameArtwork? Artwork { get; set; }

    public ArtworkFile? Cover => Artwork?.Cover;

    public ArtworkFile? Hero => Artwork?.Hero;

    public bool IsInstalled => Game.IsInstalled;

    /// <summary>Software, tools and soundtracks show their kind on cover, preview and detail page (#141); games don't.</summary>
    public bool HasTypeIcon => Game.Type != GameType.Game;

    /// <summary>The resource key of the kind's icon (<see cref="GameTypeTexts.IconKey"/>).</summary>
    public string TypeIconKey => GameTypeTexts.IconKey(Game.Type);

    /// <summary>"Werkzeug" in the preview.</summary>
    public string TypeName => GameTypeTexts.Name(Game.Type);

    /// <summary>"WERKZEUG" on the detail page's chip.</summary>
    public string TypeNameUpper => TypeName.ToUpper(Formatting.Culture);

    /// <summary>A soundtrack is handed to its launcher, which plays it; Arcadia doesn't wait for it to run (#141).</summary>
    public bool IsSoundtrack => Game.Type == GameType.Soundtrack;

    /// <summary>The update icon; a running game shows that it runs instead, its update comes with the next start.</summary>
    public bool UpdateAvailable => Game.IsInstalled && (LauncherReportsUpdate ?? Game.UpdateAvailable) && InstallState is null && RunState is null;

    /// <summary>
    /// Whether the launcher says now that the game has an update, newer than the scan that made <see cref="Game"/>;
    /// null if it doesn't tell (#288). Lets the update icon appear and go without a rescan.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateAvailable))]
    public partial bool? LauncherReportsUpdate { get; set; }

    public string PrimaryActionText => InstallState?.Phase == InstallPhase.Uninstalling ? AppTexts.TileUninstalling
        : InstallState is not null ? AppTexts.TileInstallVerb(InstallVerb)
        : !Game.IsInstalled ? AppTexts.Install
        : IsStarting ? AppTexts.TileStarting
        : IsRunning ? GameTypeTexts.ReturnAction(Game.Type)
        : GameTypeTexts.PrimaryAction(Game.Type);

    /// <summary>Whether the game runs, or was handed to its launcher and doesn't run yet (#95); null if neither.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning), nameof(IsStarting), nameof(IsRunningOrStarting), nameof(NextStartOnlyReason), nameof(RunStateText),
        nameof(RunStateShortText), nameof(UpdateAvailable), nameof(PrimaryActionText), nameof(ShowRunningBadge), nameof(ShowStartingBadge))]
    public partial RunState? RunState { get; set; }

    public bool IsRunning => RunState?.Phase == RunPhase.Running;

    public bool IsStarting => RunState?.Phase == RunPhase.Starting;

    // A cover has room for one badge (#167): a launcher that updates the game before starting it shows the update's progress.
    public bool ShowRunningBadge => IsRunning && InstallState is null;

    public bool ShowStartingBadge => IsStarting && InstallState is null;

    /// <summary>"Deinstallieren …" and settings that would only apply to the next start wait until the game ended.</summary>
    public bool IsRunningOrStarting => RunState is not null;

    /// <summary>Why a setting for the game's next start can't be changed now; null while it can.</summary>
    public string? NextStartOnlyReason => IsRunningOrStarting ? GameActionViewModel.NextStartOnly : null;

    /// <summary>"Läuft seit 18:04" for the preview and the detail page; with the date if it started on another day.</summary>
    public string RunStateText => RunState switch
    {
        null => string.Empty,
        { Phase: RunPhase.Starting } => AppTexts.TileStarting,
        { Since: { } since } => AppTexts.TileRunningSince(Formatting.TimeOfDay(since, _now)),
        _ => AppTexts.TileRunning,
    };

    /// <summary>Fits the badge on a cover.</summary>
    public string RunStateShortText => RunState?.Phase switch
    {
        null => string.Empty,
        RunPhase.Starting => AppTexts.TileBadgeStarting,
        _ => AppTexts.TileRunning,
    };

    /// <summary>The running installation of this game, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstallActive), nameof(UpdateAvailable), nameof(ShowInstallBadge), nameof(CanActivate), nameof(PrimaryActionText),
        nameof(IsInstallBlocked), nameof(ShowInstallHint), nameof(ShowInstallBlockedHint),
        nameof(InstallPercent), nameof(IsInstallIndeterminate), nameof(IsInstallPaused), nameof(IsInstallQueued), nameof(IsInstallSpinning),
        nameof(InstallStatusText), nameof(InstallShortText), nameof(ShowLauncherControlsHint), nameof(ShowRunningBadge), nameof(ShowStartingBadge))]
    public partial InstallProgress? InstallState { get; set; }

    public bool IsInstallActive => InstallState is not null;

    /// <summary>Pausing and cancelling happen in the launcher; an uninstall can't be paused.</summary>
    public bool ShowLauncherControlsHint => InstallState is { Phase: not InstallPhase.Uninstalling };

    /// <summary>The "can be installed" badge; replaced by the progress while an installation runs.</summary>
    public bool ShowInstallBadge => !Game.IsInstalled && InstallState is null;

    /// <summary>False while an installation runs, so it can't be requested twice, and while it can't be installed now.</summary>
    // A waiting game opens the launcher's install dialog again: no launcher reports a cancelled one (#343).
    public bool CanActivate => InstallState is null or { Phase: InstallPhase.AwaitingConfirmation } && !IsInstallBlocked;

    /// <summary>Not installed, and the launcher can't install it right now (#83: Steam runs with another account).</summary>
    public bool IsInstallBlocked => !Game.IsInstalled && InstallState is null && Game.InstallBlockedReason is not null;

    /// <summary>"Die Installation wird in … geöffnet" below "Installieren", unless that is blocked.</summary>
    public bool ShowInstallHint => ShowInstallBadge && !IsInstallBlocked;

    /// <summary>Why it can't be installed, below "Installieren", unless <see cref="Notice"/> already says so.</summary>
    public bool ShowInstallBlockedHint => IsInstallBlocked && !HasNotice;

    /// <summary>A hint about the game on the preview and the detail page, e.g. another account in the launcher (#83).</summary>
    public string? Notice => Game.Notice;

    public bool HasNotice => Notice is not null;

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
        { Phase: InstallPhase.AwaitingConfirmation } when Game.InstallStartsInLauncher => AppTexts.TileStartInLauncher(LauncherName),
        { Phase: InstallPhase.AwaitingConfirmation } => AppTexts.TileConfirmInLauncher(LauncherName),
        { Phase: InstallPhase.Queued, QueuePosition: { } position } => AppTexts.TileQueuedAt(position),
        { Phase: InstallPhase.Queued } => AppTexts.TileQueued,
        { Phase: InstallPhase.Paused, Fraction: { } f } => AppTexts.TilePausedAt(Formatting.Percent(f)),
        { Phase: InstallPhase.Paused } => AppTexts.TileBadgePaused,
        { Phase: InstallPhase.Finishing } => AppTexts.TileFinishing,
        { Phase: InstallPhase.Uninstalling } => AppTexts.TileUninstalling,
        { Fraction: { } f } => $"{InstallVerb} · {Formatting.Percent(f)}",
        _ => AppTexts.TileInstallVerb(InstallVerb),
    };

    // An installation of a game that is already installed is an update.
    private string InstallVerb => Game.IsInstalled ? AppTexts.TileUpdatingVerb : AppTexts.TileInstallingVerb;

    /// <summary>Fits the badge on a cover.</summary>
    public string InstallShortText => InstallState switch
    {
        null => string.Empty,
        { Phase: InstallPhase.AwaitingConfirmation } when Game.InstallStartsInLauncher => AppTexts.TileBadgeWaiting,
        { Phase: InstallPhase.AwaitingConfirmation } => AppTexts.TileConfirm,
        { Phase: InstallPhase.Queued, QueuePosition: { } position } => AppTexts.TileBadgeWaitingAt(position),
        { Phase: InstallPhase.Queued } => AppTexts.TileBadgeWaiting,
        { Phase: InstallPhase.Paused } => AppTexts.TileBadgePaused,
        { Phase: InstallPhase.Finishing } => AppTexts.TileAlmostDone,
        { Phase: InstallPhase.Uninstalling } => AppTexts.TileRemoving,
        { Fraction: { } f } => Formatting.Percent(f),
        _ => AppTexts.TileLoading,
    };

    public string LauncherName { get; }

    /// <summary>The launcher's logo (#138).</summary>
    public LauncherBadge LauncherBadge { get; }

    public string LastPlayedText { get; }

    public string PlaytimeText { get; }

    public string SizeText { get; }

    public string LocationText => Game.IsInstalled ? Game.InstallDirectory ?? "–" : AppTexts.NotInstalled;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A is held on this game; the ring around the preview fills until the detail page opens.</summary>
    [ObservableProperty]
    public partial bool IsHolding { get; set; }

    public IRelayCommand FocusCommand { get; }

    public IRelayCommand ActivateCommand { get; }
}
