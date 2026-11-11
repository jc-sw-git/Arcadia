using System.ComponentModel;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.Core.Navigation;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>Where the focus is on the detail page (#499): the button row, the rail of areas, or the open area's rows.</summary>
public enum DetailZone
{
    Buttons,
    Rail,
    Rows,
}

/// <summary>
/// The focus of a detail page, carried to the next game when the carousel turns or the tab changes (#499): the open area
/// and the focus stay; only a page opened anew starts on "Spielen" and "Übersicht".
/// </summary>
public readonly record struct DetailFocus(DetailZone Zone, GameActionKind Button, string AreaKey, int Row)
{
    public static DetailFocus Start { get; } = new(DetailZone.Buttons, GameActionKind.Play, DetailAreaViewModel.OverviewKey, 0);
}

/// <summary>
/// The shown game of the detail page (#499): title and the button row "Spielen", "Zu Favoriten", "In … öffnen" with a quiet
/// note beside it, and the rail of areas ("Übersicht", "Controller", "Verwalten") with the open area's content and the info
/// box. ▼ leads from the buttons into the rail, A into the area's rows, B back; ◀ ▶ change a value at once, A starts an
/// action, as on the settings page.
/// </summary>
public sealed partial class GameDetailPageViewModel : ViewModelBase
{
    private readonly IGameActionHost _host;
    private int _button = -1;
    private int _area;
    private int _row;

    public GameDetailPageViewModel(GameTileViewModel tile, IGameActionHost host, DetailFocus focus)
    {
        Tile = tile;
        _host = host;
        (Backdrop, Ring) = Formatting.HeroBrushes(tile.Title);
        var game = tile.Game;
        Drive = Formatting.Drive(game.InstallDirectory);
        LastPlayedAt = Formatting.DateAndTime(game.LastPlayedAt);
        PlaytimeSource = game.Playtime is null ? AppTexts.DetailLauncherReportsNone(tile.LauncherName) : AppTexts.DetailPlaytimeFrom(tile.LauncherName);
        InstalledAt = Formatting.Date(game.IsInstalled ? game.InstalledAt : null);
        Areas =
        [
            new(DetailAreaViewModel.OverviewKey, AppTexts.DetailAreaOverview, DetailAreas.OverviewIcon, OpenByPointer),
            new(DetailAreaViewModel.ControllerKey, AppTexts.DetailAreaController, DetailAreas.ControllerIcon, OpenByPointer),
            new(DetailAreaViewModel.ManageKey, AppTexts.DetailAreaManage, DetailAreas.ManageIcon, OpenByPointer),
        ];
        Areas[0].ValueLine = DetailAreas.OverviewLine(tile);
        OpenArea = Areas[0];

        tile.PropertyChanged += OnTilePropertyChanged;
        RebuildActions();
        Restore(focus);
    }

    public GameTileViewModel Tile { get; }

    /// <summary>The background in the game's hue until artwork is there (#13), with its rings.</summary>
    public IBrush Backdrop { get; }

    public IBrush Ring { get; }

    public string LauncherUpper => Tile.LauncherName.ToUpperInvariant();

    public string LastPlayedAt { get; }

    public string PlaytimeSource { get; }

    public string InstalledAt { get; }

    public string? Drive { get; }

    public string DriveText => Drive ?? "–";

    /// <summary>"Spielen", "Zu Favoriten", "In … öffnen": what is done most with a game, on the artwork (#499).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<GameActionViewModel> Buttons { get; private set; } = [];

    public IReadOnlyList<DetailAreaViewModel> Areas { get; }

    /// <summary>The area whose content stands beside the rail.</summary>
    [ObservableProperty]
    public partial DetailAreaViewModel OpenArea { get; private set; }

    /// <summary>The row the info box explains: the focused one, or the area's first while the focus is in the rail.</summary>
    [ObservableProperty]
    public partial DetailRowViewModel? InfoRow { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnButtons), nameof(IsInRows), nameof(BackHint), nameof(ShowsAreasHint))]
    public partial DetailZone Zone { get; private set; }

    public bool IsOnButtons => Zone == DetailZone.Buttons;

    public bool IsInRows => Zone == DetailZone.Rows;

    /// <summary>What the footer says A does here; null where A does nothing.</summary>
    [ObservableProperty]
    public partial string? AcceptHint { get; private set; }

    /// <summary>◀ ▶ change the focused row's value.</summary>
    [ObservableProperty]
    public partial bool ShowsChangeHint { get; private set; }

    /// <summary>▼ leads from the buttons into the areas.</summary>
    public bool ShowsAreasHint => Zone == DetailZone.Buttons;

    /// <summary>B goes from the rows back to the rail, elsewhere back to where the page was opened from.</summary>
    public string BackHint => Zone == DetailZone.Rows ? AppTexts.DetailAreas : AppTexts.Back;

    /// <summary>"LÄUFT SEIT 18:04" or "WIRD GESTARTET …" for the chip beside the launcher's (#95).</summary>
    public string RunStateUpper => Tile.RunStateText.ToUpper(Formatting.Culture);

    /// <summary>The launcher may install a pending update before the game starts (owner, 2026-10-12).</summary>
    public string? UpdateNote => Tile.UpdateAvailable ? AppTexts.DetailUpdateNote(Tile.LauncherName) : null;

    public bool HasUpdateNote => UpdateNote is not null;

    /// <summary>A soundtrack is played by its launcher, not by Arcadia (#141).</summary>
    public string? SoundtrackNote => Tile.IsSoundtrack && Tile.IsInstalled ? AppTexts.DetailSoundtrackNote(Tile.LauncherName) : null;

    public bool HasSoundtrackNote => SoundtrackNote is not null;

    /// <summary>Why "Spielen" isn't available, e.g. the running installation; a disabled other button tells why in its tooltip.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayNote), nameof(HasNotes))]
    public partial string? PlayNote { get; private set; }

    public bool HasPlayNote => PlayNote is not null;

    /// <summary>The quiet note beside the buttons has something to say.</summary>
    public bool HasNotes => HasUpdateNote || HasPlayNote || HasSoundtrackNote;

    /// <summary>The focus to carry to the next game.</summary>
    public DetailFocus Focus => new(Zone, FocusedButton?.Kind ?? GameActionKind.Play, OpenArea.Key, _row);

    /// <summary>The kind of the focused button, null while the focus is in the areas.</summary>
    public GameActionKind? FocusedKind => Zone == DetailZone.Buttons ? FocusedButton?.Kind : null;

    /// <summary>The focused row, null while the focus is on the buttons or in the rail.</summary>
    public DetailRowViewModel? FocusedRow => Zone == DetailZone.Rows && _row < OpenArea.Rows.Count ? OpenArea.Rows[_row] : null;

    private GameActionViewModel? FocusedButton => _button >= 0 && _button < Buttons.Count ? Buttons[_button] : null;

    /// <summary>D-pad and A. B only while the focus is in the rows: back to the rail (false: the page closes).</summary>
    public bool Handle(UiAction action)
    {
        switch (Zone, action)
        {
            case (_, UiAction.Back):
                if (Zone != DetailZone.Rows)
                {
                    return false;
                }

                SetZone(DetailZone.Rail);
                break;
            case (DetailZone.Buttons, UiAction.Left or UiAction.Right):
                MoveButton(action == UiAction.Left ? -1 : 1);
                break;
            case (DetailZone.Buttons, UiAction.Down):
                SetZone(DetailZone.Rail);
                break;
            case (DetailZone.Buttons, UiAction.Accept) when FocusedButton is { } button:
                button.Invoke();
                UpdatePlayNote();
                break;
            case (DetailZone.Rail, UiAction.Up) when _area == 0:
                // ▲ on the first area leads back to the buttons, on "Spielen" (owner's scenario 2); a held ▲ stops there.
                if (!HeldPress.IsRepeat)
                {
                    FocusButton(GameActionKind.Play);
                    SetZone(DetailZone.Buttons);
                }

                break;
            case (DetailZone.Rail, UiAction.Up or UiAction.Down):
                ShowArea(ListFocus.Cycle(_area, action == UiAction.Up ? -1 : 1, Areas.Count, HeldPress.IsRepeat));
                break;
            case (DetailZone.Rail, UiAction.Accept) when OpenArea.HasRows:
                _row = 0;
                SetZone(DetailZone.Rows);
                break;
            case (DetailZone.Rows, UiAction.Up or UiAction.Down):
                _row = ListFocus.Cycle(_row, action == UiAction.Up ? -1 : 1, OpenArea.Rows.Count, HeldPress.IsRepeat);
                ShowFocus();
                break;
            case (DetailZone.Rows, UiAction.Left or UiAction.Right):
                FocusedRow?.Change(action == UiAction.Left ? -1 : 1);
                UpdateValueLines();
                break;
            case (DetailZone.Rows, UiAction.Accept):
                FocusedRow?.Activate();
                UpdateValueLines();
                break;
        }

        return true;
    }

    /// <summary>
    /// Builds buttons and rows again, e.g. when an installation finished or the Steam Controller connected. The rows of an
    /// area stay the same in number, so the focus stays on its row; a button that can't be pressed now hands it to the next.
    /// </summary>
    public void RebuildActions()
    {
        var focusedKind = FocusedButton?.Kind;
        var actions = GameActionViewModel.For(Tile, _host);
        Buttons = [.. new[] { GameActionKind.Play, GameActionKind.Favorite, GameActionKind.OpenInLauncher }
            .SelectMany(kind => actions.Where(a => a.Kind == kind))];
        Area(DetailAreaViewModel.ControllerKey).Rows = DetailAreas.ControllerRows(Tile, actions, _host, Point);
        Area(DetailAreaViewModel.ManageKey).Rows = DetailAreas.ManageRows(Tile, actions, Point);
        UpdateValueLines();
        _button = -1;
        FocusButton(focusedKind ?? GameActionKind.Play);
        ShowFocus();
        UpdatePlayNote();
    }

    /// <summary>Stops following the game's cover; the page was replaced by the next game's.</summary>
    public void Detach() => Tile.PropertyChanged -= OnTilePropertyChanged;

    private DetailAreaViewModel Area(string key) => Areas.First(area => area.Key == key);

    private void Restore(DetailFocus focus)
    {
        FocusButton(focus.Button);
        _area = Math.Max(0, Areas.ToList().FindIndex(area => area.Key == focus.AreaKey));
        OpenArea = Areas[_area];
        _row = Math.Clamp(focus.Row, 0, Math.Max(0, OpenArea.Rows.Count - 1));
        SetZone(focus.Zone == DetailZone.Rows && !OpenArea.HasRows ? DetailZone.Rail : focus.Zone);
    }

    /// <summary>Focuses the button of <paramref name="kind"/>, else the first one that can be pressed; none if none can.</summary>
    private void FocusButton(GameActionKind kind)
    {
        var index = Buttons.ToList().FindIndex(button => button.Kind == kind && button.IsEnabled);
        _button = index >= 0 ? index : Buttons.ToList().FindIndex(button => button.IsEnabled);
    }

    /// <summary>◀ ▶ pass over buttons that can't be pressed and go round at the ends (#476).</summary>
    private void MoveButton(int delta)
    {
        if (_button >= 0)
        {
            _button = ListFocus.Next(_button, delta, Buttons.Count, b => Buttons[b].IsEnabled, HeldPress.IsRepeat);
            ShowFocus();
        }
    }

    private void ShowArea(int index)
    {
        _area = index;
        _row = 0;
        OpenArea = Areas[index];
        ShowFocus();
    }

    private void OpenByPointer(DetailAreaViewModel area)
    {
        ShowArea(Areas.ToList().IndexOf(area));
        SetZone(DetailZone.Rail);
    }

    /// <summary>The mouse over a row or a click on it: the focus goes there.</summary>
    private void Point(DetailRowViewModel row)
    {
        var index = OpenArea.Rows.ToList().IndexOf(row);
        if (index >= 0)
        {
            _row = index;
            SetZone(DetailZone.Rows);
        }
    }

    private void SetZone(DetailZone zone)
    {
        Zone = zone;
        ShowFocus();
    }

    /// <summary>Exactly one thing has the focus ring: a button, an area in the rail, or a row.</summary>
    private void ShowFocus()
    {
        for (var b = 0; b < Buttons.Count; b++)
        {
            Buttons[b].IsFocused = Zone == DetailZone.Buttons && b == _button;
        }

        foreach (var area in Areas)
        {
            area.IsOpen = area == OpenArea;
            area.IsFocused = Zone == DetailZone.Rail && area == OpenArea;
            for (var r = 0; r < area.Rows.Count; r++)
            {
                area.Rows[r].IsFocused = Zone == DetailZone.Rows && area == OpenArea && r == _row;
            }
        }

        InfoRow = OpenArea.HasRows ? OpenArea.Rows[Zone == DetailZone.Rows ? Math.Min(_row, OpenArea.Rows.Count - 1) : 0] : null;
        var row = FocusedRow;
        AcceptHint = Zone switch
        {
            DetailZone.Buttons => FocusedButton?.ButtonLabel,
            DetailZone.Rail => OpenArea.HasRows ? AppTexts.Select : null,
            _ => row is { IsAction: true, IsGreyed: false } ? row.Title : null,
        };
        ShowsChangeHint = row is { IsChoice: true, IsGreyed: false };
        OnPropertyChanged(nameof(FocusedKind));
        OnPropertyChanged(nameof(FocusedRow));
    }

    private void UpdateValueLines()
    {
        var controller = Area(DetailAreaViewModel.ControllerKey);
        controller.ValueLine = DetailAreas.ControllerLine(controller.Rows);
        Area(DetailAreaViewModel.ManageKey).ValueLine = AppTexts.DetailManageLine;
    }

    private void OnTilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GameTileViewModel.InstallState) or nameof(GameTileViewModel.RunState) or nameof(GameTileViewModel.LauncherReportsUpdate))
        {
            OnPropertyChanged(nameof(UpdateNote));
            OnPropertyChanged(nameof(HasUpdateNote));
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(RunStateUpper));
            RebuildActions();
        }
    }

    private void UpdatePlayNote()
    {
        // "Spielen" explains itself through the update note; a disabled "Spielen" is the running installation.
        var play = Buttons.FirstOrDefault(a => a.Kind == GameActionKind.Play);
        // A blocked install whose reason the game's notice already gives (#83) needs no second line.
        PlayNote = Tile.UpdateAvailable || (Tile.IsInstallBlocked && Tile.HasNotice) ? null : play?.Note;
    }
}
