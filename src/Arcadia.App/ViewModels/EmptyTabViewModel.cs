using Arcadia.App.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The library without games to show (#136): an active launcher's tab without games or of a launcher no longer found keeps
/// its tab, and a library without any active launcher says so. Each in the style of the search's empty state, with
/// "Launcher-Einstellungen" and "Neu einlesen" (or "Erneut suchen"). ◀ ▶ move between the two, A chooses. With no launcher
/// active only "Launcher-Einstellungen": reading again can't find anything before one is switched on there (owner, 2026-10-24).
/// </summary>
/// <param name="tab">The empty tab, or null when no launcher is active.</param>
/// <param name="rescan">Reads the tab's launcher again; null when no launcher is active.</param>
public sealed partial class EmptyTabViewModel(LauncherTabViewModel? tab, Action openSettings, Action? rescan) : ViewModelBase
{
    public LauncherTabViewModel? Tab => tab;

    /// <summary>No launcher is active, so there are no tabs at all: empty tabs instead of covers.</summary>
    public bool HasNoLauncher => tab is null;

    /// <summary>There are tabs to switch to with LB/RB.</summary>
    public bool HasTab => tab is not null;

    /// <summary>The launcher isn't installed any more, rather than without games: a question mark instead of covers.</summary>
    public bool IsMissing => tab is { IsFound: false };

    public bool HasNoGames => tab is { IsFound: true };

    /// <summary>The launcher's logo (#138); null when no launcher is active.</summary>
    public Services.LauncherBadge? Badge => tab?.Badge;

    public string Title => tab switch
    {
        null => "Kein Launcher aktiv",
        { IsFound: false } => $"{tab.Name} nicht gefunden",
        _ => $"Keine Spiele in {tab.Name}",
    };

    public string Text => tab switch
    {
        null => "Arcadia zeigt die Spiele deiner Launcher in Tabs. Schalte in den Einstellungen unter „Launcher“ die ein, deren Spiele du hier sehen willst.",
        { IsFound: false } => $"Arcadia findet {tab.Name} auf diesem PC nicht mehr. Ist es deinstalliert, schalte den Launcher in den Einstellungen ab, dann verschwindet dieser Tab.",
        _ => $"{tab.Name} ist installiert, aber Arcadia hat dort keine Spiele gefunden. Sobald du in {tab.Name} ein Spiel kaufst oder installierst, erscheint es hier.",
    };

    /// <summary>Below the tab's name, where a tab with games counts them.</summary>
    public string Summary => IsMissing ? "Launcher nicht gefunden" : "Keine Spiele";

    /// <summary>A launcher no longer found is looked for again.</summary>
    public string RescanText => HasNoGames ? "Neu einlesen" : "Erneut suchen";

    public bool CanRescan => rescan is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRescanFocused))]
    public partial bool IsSettingsFocused { get; private set; } = true;

    public bool IsRescanFocused => !IsSettingsFocused;

    public void Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Left:
                IsSettingsFocused = true;
                break;
            case UiAction.Right when CanRescan:
                IsSettingsFocused = false;
                break;
            case UiAction.Accept when IsSettingsFocused:
                OpenSettings();
                break;
            case UiAction.Accept:
                Rescan();
                break;
        }
    }

    [RelayCommand]
    private void OpenSettings() => openSettings();

    [RelayCommand]
    private void Rescan() => rescan?.Invoke();
}
