using Arcadia.Launchers.Abstractions;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

public enum GameActionKind
{
    Play,
    Details,
    Favorite,
    Artwork,
    OpenInLauncher,
    ControllerMode,
    MouseMode,
    Uninstall,
}

/// <summary>What the actions of a game do; implemented by the window's view model.</summary>
public interface IGameActionHost
{
    /// <summary>True when the Steam Controller mode matters (option on, Steam Controller connected).</summary>
    bool ShowControllerMode { get; }

    /// <summary>"Xbox 360 Controller (SISR)" or "Steam Input".</summary>
    string ControllerModeOf(Game game);

    void Play(GameTileViewModel tile);

    void ShowDetails(GameTileViewModel tile);

    void OpenInLauncher(GameTileViewModel tile);

    void ToggleControllerMode(GameTileViewModel tile);

    /// <summary>True when the mouse mode is "Von Hand" with "Beenden des Mausmodus in Spielen erzwingen" on "Individuell".</summary>
    bool ShowGameMouseMode { get; }

    /// <summary>The game's "Individueller Mausmodus": allowed, forbidden, or null for "Standard".</summary>
    bool? GameMouseModeOf(Game game);

    /// <summary>What "Standard" means now: allowed or forbidden.</summary>
    bool DefaultGameMouseModeAllowed { get; }

    void CycleGameMouseMode(GameTileViewModel tile);

    void Uninstall(GameTileViewModel tile);
}

/// <summary>
/// One action on a game. The context menu lists them, the detail page shows them as buttons;
/// both get them from <see cref="For"/>, so they always offer the same.
/// </summary>
public sealed partial class GameActionViewModel : ViewModelBase
{
    private const string NotYetAvailable = "Folgt in einer späteren Version";

    private readonly Action _invoke;

    private GameActionViewModel(GameActionKind kind, string label, string? note, bool isEnabled, Action invoke, string? buttonLabel = null)
    {
        Kind = kind;
        Label = label;
        ButtonLabel = buttonLabel ?? label;
        Note = note;
        IsEnabled = isEnabled;
        _invoke = invoke;
        InvokeCommand = new RelayCommand(Invoke, () => IsEnabled);
    }

    public GameActionKind Kind { get; }

    [ObservableProperty]
    public partial string Label { get; private set; }

    /// <summary>The shorter text of the detail page's button.</summary>
    [ObservableProperty]
    public partial string ButtonLabel { get; private set; }

    /// <summary>A small heading above the button text, e.g. "STEAM CONTROLLER" above the mode.</summary>
    public string? Caption => Kind switch
    {
        GameActionKind.ControllerMode => "STEAM CONTROLLER",
        GameActionKind.MouseMode => "INDIVIDUELLER MAUSMODUS",
        _ => null,
    };

    public bool HasCaption => Caption is not null;

    public bool IsPrimary => Kind == GameActionKind.Play;

    /// <summary>A hint below the label, or why the action isn't available.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; private set; }

    public bool HasNote => !string.IsNullOrEmpty(Note);

    public bool IsEnabled { get; }

    public bool IsDanger => Kind == GameActionKind.Uninstall;

    /// <summary>The menu draws a line above it.</summary>
    public bool StartsGroup => Kind == GameActionKind.Uninstall;

    /// <summary>Switching a mode keeps the menu open, so the new mode can be seen.</summary>
    public bool KeepsMenuOpen => Kind is GameActionKind.ControllerMode or GameActionKind.MouseMode;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand InvokeCommand { get; }

    /// <summary>Stroke icon in a 24×24 box.</summary>
    public Geometry Icon => StreamGeometry.Parse(Kind switch
    {
        GameActionKind.Play => "M7 4.5v15l13-7.5z",
        GameActionKind.Details => "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M12 11v6M12 7.5v.5",
        GameActionKind.Favorite => "M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.8l-5.2 2.8 1-5.8-4.3-4.1 5.9-.8z",
        GameActionKind.Artwork => "M5.5 4.5h13a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2h-13a2 2 0 0 1-2-2v-11a2 2 0 0 1 2-2M20.5 16l-5-5-8 8.5M9 8.2a1.8 1.8 0 1 0 0 3.6a1.8 1.8 0 1 0 0-3.6",
        GameActionKind.OpenInLauncher => "M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5",
        GameActionKind.ControllerMode => "M4 8h12M13 5l3 3-3 3M20 16H8M11 13l-3 3 3 3",
        GameActionKind.MouseMode => "M12 3.5a5.5 5.5 0 0 0-5.5 5.5v6a5.5 5.5 0 0 0 11 0V9A5.5 5.5 0 0 0 12 3.5M6.5 10h11M12 3.5V10",
        _ => "M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3",
    });

    public void Invoke()
    {
        if (IsEnabled)
        {
            _invoke();
        }
    }

    /// <summary>The actions for <paramref name="tile"/> in menu order; actions that don't apply are left out.</summary>
    public static IReadOnlyList<GameActionViewModel> For(GameTileViewModel tile, IGameActionHost host)
    {
        var game = tile.Game;
        var launcher = tile.LauncherName;
        var busy = tile.InstallState is not null;
        var actions = new List<GameActionViewModel>();

        // While an installation, update or uninstall runs, its status takes the place of the primary action.
        actions.Add(busy
            ? new(GameActionKind.Play, tile.InstallStatusText, tile.ShowLauncherControlsHint ? $"Pausieren und Abbrechen in {launcher}" : null, false, () => { })
            : game.IsInstalled
                ? new(GameActionKind.Play, "Spielen", tile.UpdateAvailable ? $"{launcher} installiert ggf. vorher das Update" : null, true, () => host.Play(tile))
                : new(GameActionKind.Play, "Installieren", $"Die Installation wird in {launcher} geöffnet", true, () => host.Play(tile)));

        actions.Add(new(GameActionKind.Details, "Details anzeigen", null, true, () => host.ShowDetails(tile)));
        actions.Add(new(GameActionKind.Favorite, "Zu Favoriten hinzufügen", NotYetAvailable, false, () => { }, "Zu Favoriten"));
        if (game.IsInstalled)
        {
            actions.Add(new(GameActionKind.Artwork, "Artwork ändern …", NotYetAvailable, false, () => { }, "Artwork ändern"));
        }

        actions.Add(game.OpenInLauncher is null
            ? new(GameActionKind.OpenInLauncher, $"In {launcher} öffnen", $"{launcher} lässt sich nicht von Arcadia aus öffnen", false, () => { })
            : new(GameActionKind.OpenInLauncher, $"In {launcher} öffnen", null, true, () => host.OpenInLauncher(tile)));

        if (!game.IsInstalled)
        {
            return actions;
        }

        if (host.ShowControllerMode)
        {
            GameActionViewModel? mode = null;
            mode = new(GameActionKind.ControllerMode, string.Empty, null, true, () =>
            {
                host.ToggleControllerMode(tile);
                mode!.ShowControllerMode(host.ControllerModeOf(game));
            });
            mode.ShowControllerMode(host.ControllerModeOf(game));
            actions.Add(mode);
        }

        if (host.ShowGameMouseMode)
        {
            GameActionViewModel? mouse = null;
            mouse = new(GameActionKind.MouseMode, string.Empty, null, true, () =>
            {
                host.CycleGameMouseMode(tile);
                mouse!.ShowGameMouseMode(host.GameMouseModeOf(game), host.DefaultGameMouseModeAllowed);
            });
            mouse.ShowGameMouseMode(host.GameMouseModeOf(game), host.DefaultGameMouseModeAllowed);
            actions.Add(mouse);
        }

        actions.Add(busy
            ? new(GameActionKind.Uninstall, "Deinstallieren …", "Erst wenn die laufende Installation fertig ist", false, () => { })
            : game.Uninstall is not null
                ? new(GameActionKind.Uninstall, "Deinstallieren …", null, true, () => host.Uninstall(tile))
                : game.OpenInLauncher is not null
                    ? new(GameActionKind.Uninstall, "Deinstallieren …", $"Nur in {launcher} möglich – öffnet {launcher}", true, () => host.Uninstall(tile))
                    : new(GameActionKind.Uninstall, "Deinstallieren …", $"Nur in {launcher} möglich", false, () => { }));

        return actions;
    }

    private void ShowGameMouseMode(bool? choice, bool defaultAllowed)
    {
        var mode = GameMouseModes.Of(choice, defaultAllowed);
        Label = $"Mausmodus: {mode}";
        ButtonLabel = mode;
        Note = $"A wechselt zu „{GameMouseModes.Of(GameMouseModes.Next(choice), defaultAllowed)}“";
    }

    private void ShowControllerMode(string mode)
    {
        Label = $"Steam Controller: {mode}";
        ButtonLabel = mode;
        Note = $"A wechselt zu „{(mode == ControllerModes.Steam ? ControllerModes.Sisr : ControllerModes.Steam)}“";
    }
}

/// <summary>The names of the two Steam Controller modes (#92).</summary>
public static class ControllerModes
{
    public const string Sisr = "Xbox 360 Controller (SISR)";
    public const string Steam = "Steam Input";
}
