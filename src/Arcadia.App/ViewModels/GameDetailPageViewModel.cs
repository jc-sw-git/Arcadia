using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// One game's page in the detail carousel. The page in the middle has the controller focus; its
/// neighbours show the same content and a click on them turns the carousel.
/// </summary>
public sealed partial class GameDetailPageViewModel : ViewModelBase
{
    /// <summary>Must match the page width in MainWindow.axaml.</summary>
    public const double Width = 1152;

    /// <summary>Distance between the left edges of two neighbouring pages.</summary>
    public const double Pitch = 1200;

    /// <summary>Left edge of the page in the middle of the 1920 px wide layout.</summary>
    private const double CentreLeft = (1920 - Width) / 2;

    private readonly IGameActionHost _host;
    private int _focusRow;
    private int _focusColumn = -1;

    public GameDetailPageViewModel(GameTileViewModel tile, int offset, IGameActionHost host, Action<int> rotate)
    {
        Tile = tile;
        _host = host;
        (Backdrop, Ring) = Formatting.HeroBrushes(tile.Title);
        var game = tile.Game;
        Drive = Formatting.Drive(game.InstallDirectory);
        LastPlayedAt = Formatting.DateAndTime(game.LastPlayedAt);
        PlaytimeSource = game.Playtime is null ? $"{tile.LauncherName} meldet keine" : $"Laut {tile.LauncherName}";
        InstalledAt = Formatting.Date(game.IsInstalled ? game.InstalledAt : null);
        Offset = offset;
        SelectCommand = new RelayCommand(() => rotate(Math.Sign(Offset)));

        tile.PropertyChanged += OnTilePropertyChanged;
        RebuildActions();
    }

    public GameTileViewModel Tile { get; }

    public IBrush Backdrop { get; }

    public IBrush Ring { get; }

    public string LauncherUpper => Tile.LauncherName.ToUpperInvariant();

    public string LastPlayedAt { get; }

    public string PlaytimeSource { get; }

    public string InstalledAt { get; }

    public string? Drive { get; }

    public string DriveText => Drive ?? "–";

    /// <summary>Places away from the middle: -1 is the previous game, 1 the next.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Left), nameof(IsCentre), nameof(IsSide))]
    public partial int Offset { get; set; }

    public double Left => CentreLeft + (Offset * Pitch);

    public bool IsCentre => Offset == 0;

    public bool IsSide => Offset != 0;

    /// <summary>Spielen, Zu Favoriten, In … öffnen, Artwork ändern (owner, 2026-10-21).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<GameActionViewModel> TopRow { get; private set; } = [];

    /// <summary>
    /// What can be set for this game alone: Steam Controller mode, "Individueller Mausmodus"; later e.g. launch
    /// options or the controller layout (owner, 2026-10-21). Empty when nothing applies.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGameSettings))]
    public partial IReadOnlyList<GameActionViewModel> GameSettingsRow { get; private set; } = [];

    public bool HasGameSettings => GameSettingsRow.Count > 0;

    /// <summary>"Deinstallieren …", alone in the last row.</summary>
    [ObservableProperty]
    public partial GameActionViewModel? Uninstall { get; private set; }

    /// <summary>"LÄUFT SEIT 18:04" or "WIRD GESTARTET …" for the chip beside the launcher's (#95).</summary>
    public string RunStateUpper => Tile.RunStateText.ToUpper(Formatting.Culture);

    /// <summary>The launcher may install a pending update before the game starts (owner, 2026-10-12).</summary>
    public string? UpdateNote => Tile.UpdateAvailable ? $"{Tile.LauncherName} installiert das Update eventuell, bevor das Spiel startet." : null;

    public bool HasUpdateNote => UpdateNote is not null;

    /// <summary>A soundtrack is played by its launcher, not by Arcadia (#141).</summary>
    public string? SoundtrackNote => Tile.IsSoundtrack && Tile.IsInstalled ? $"{Tile.LauncherName} öffnet den Soundtrack, dort spielst du ihn ab." : null;

    public bool HasSoundtrackNote => SoundtrackNote is not null;

    /// <summary>
    /// Why "Spielen" isn't available, e.g. the running installation. The other buttons show nothing below them: their
    /// label says what they do, and a disabled one explains itself in its tooltip (owner, 2026-10-21).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayNote), nameof(HasNotes))]
    public partial string? PlayNote { get; private set; }

    public bool HasPlayNote => PlayNote is not null;

    /// <summary>The line below the buttons has something to say; without, it takes no room.</summary>
    public bool HasNotes => HasUpdateNote || HasPlayNote || HasSoundtrackNote;

    public IRelayCommand SelectCommand { get; }

    /// <summary>The kind of the focused button, so a rebuilt page can keep the focus where it was.</summary>
    public GameActionKind? FocusedKind => Focused?.Kind;

    private GameActionViewModel? Focused => _focusColumn >= 0 && Row(_focusRow) is { } row && _focusColumn < row.Count ? row[_focusColumn] : null;

    /// <summary>D-pad and A on the page in the middle.</summary>
    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left:
                MoveInRow(-1);
                break;
            case UiAction.Right:
                MoveInRow(1);
                break;
            case UiAction.Up:
                MoveToRow(-1);
                break;
            case UiAction.Down:
                MoveToRow(1);
                break;
            case UiAction.Accept when Focused is { } focused:
                focused.Invoke();
                UpdatePlayNote();
                break;
        }
    }

    /// <summary>
    /// Builds the buttons again, e.g. when an installation starts or the Steam Controller mode
    /// became relevant. The focus stays on the same kind of button where it still can.
    /// </summary>
    public void RebuildActions()
    {
        var focusedKind = FocusedKind;
        var actions = GameActionViewModel.For(Tile, _host);

        // No context menu here: everything it offers is a button of its own, except "Details", which is this page.
        List<GameActionViewModel> InOrder(params GameActionKind[] kinds) =>
            kinds.SelectMany(kind => actions.Where(a => a.Kind == kind)).ToList();
        TopRow = InOrder(GameActionKind.Play, GameActionKind.Favorite, GameActionKind.OpenInLauncher, GameActionKind.Artwork);
        GameSettingsRow = InOrder(GameActionKind.ControllerMode, GameActionKind.MouseMode);
        Uninstall = actions.FirstOrDefault(a => a.Kind == GameActionKind.Uninstall);

        _focusColumn = -1;
        if (IsCentre)
        {
            FocusKind(focusedKind);
        }

        UpdatePlayNote();
    }

    partial void OnOffsetChanged(int value)
    {
        if (value == 0)
        {
            FocusKind(null);
        }
        else
        {
            SetFocus(-1, -1);
        }
    }

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameTileViewModel.InstallState) or nameof(GameTileViewModel.RunState))
        {
            OnPropertyChanged(nameof(UpdateNote));
            OnPropertyChanged(nameof(HasUpdateNote));
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(RunStateUpper));
            RebuildActions();
        }
    }

    private const int RowCount = 3;

    private IReadOnlyList<GameActionViewModel>? Row(int row) => row switch
    {
        0 => TopRow,
        1 => GameSettingsRow,
        2 => Uninstall is null ? [] : [Uninstall],
        _ => null,
    };

    /// <summary>Focuses the button of <paramref name="kind"/>, else "Spielen", else the first one available.</summary>
    public void FocusKind(GameActionKind? kind)
    {
        for (var row = 0; row < RowCount; row++)
        {
            var actions = Row(row)!;
            for (var column = 0; column < actions.Count; column++)
            {
                if (actions[column].IsEnabled && actions[column].Kind == (kind ?? GameActionKind.Play))
                {
                    SetFocus(row, column);
                    return;
                }
            }
        }

        for (var row = 0; row < RowCount; row++)
        {
            var actions = Row(row)!;
            for (var column = 0; column < actions.Count; column++)
            {
                if (actions[column].IsEnabled)
                {
                    SetFocus(row, column);
                    return;
                }
            }
        }

        SetFocus(-1, -1);
    }

    /// <summary>Left/right skip buttons that aren't available and stop at the ends of the row.</summary>
    private void MoveInRow(int delta)
    {
        if (Row(_focusRow) is not { } actions || _focusColumn < 0)
        {
            return;
        }

        for (var column = _focusColumn + delta; column >= 0 && column < actions.Count; column += delta)
        {
            if (actions[column].IsEnabled)
            {
                SetFocus(_focusRow, column);
                return;
            }
        }
    }

    /// <summary>
    /// Up/down go to the next row in that direction with an available button (an empty settings row is skipped) and
    /// land on the one nearest in order, preferring the left one; they stop at the first and last row.
    /// </summary>
    private void MoveToRow(int delta)
    {
        if (_focusColumn < 0)
        {
            return;
        }

        for (var row = _focusRow + delta; Row(row) is { } actions; row += delta)
        {
            for (var distance = 0; distance < actions.Count + _focusColumn + 1; distance++)
            {
                foreach (var column in new[] { _focusColumn - distance, _focusColumn + distance })
                {
                    if (column >= 0 && column < actions.Count && actions[column].IsEnabled)
                    {
                        SetFocus(row, column);
                        return;
                    }
                }
            }
        }
    }

    private void SetFocus(int row, int column)
    {
        if (Focused is { } previous)
        {
            previous.IsFocused = false;
        }

        _focusRow = Math.Max(row, 0);
        _focusColumn = column;
        if (Focused is { } focused)
        {
            focused.IsFocused = true;
        }
    }

    private void UpdatePlayNote()
    {
        // "Spielen" explains itself through the update note; a disabled "Spielen" is the running installation.
        var play = TopRow.FirstOrDefault(a => a.Kind == GameActionKind.Play);
        PlayNote = Tile.UpdateAvailable ? null : play?.Note;
    }
}
