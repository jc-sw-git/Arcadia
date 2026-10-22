using Arcadia.App.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// An active launcher's tab without games, or of a launcher no longer found (#136): it keeps its tab, which says so in the
/// style of the search's empty state, with "Launcher-Einstellungen" and "Neu einlesen" (or "Erneut suchen"). ◀ ▶ move
/// between the two, A chooses.
/// </summary>
public sealed partial class EmptyTabViewModel(LauncherTabViewModel tab, Action<LauncherTabViewModel> openSettings, Action<LauncherTabViewModel> rescan)
    : ViewModelBase
{
    public LauncherTabViewModel Tab => tab;

    /// <summary>The launcher isn't installed any more, rather than without games: a question mark instead of covers.</summary>
    public bool IsMissing => !tab.IsFound;

    public string Title => IsMissing ? $"{tab.Name} nicht gefunden" : $"Keine Spiele in {tab.Name}";

    public string Text => IsMissing
        ? $"Arcadia findet {tab.Name} auf diesem PC nicht mehr. Ist es deinstalliert, schalte den Launcher in den Einstellungen ab, dann verschwindet dieser Tab."
        : $"{tab.Name} ist installiert, aber Arcadia hat dort keine Spiele gefunden. Sobald du in {tab.Name} ein Spiel kaufst oder installierst, erscheint es hier.";

    /// <summary>Below the tab's name, where a tab with games counts them.</summary>
    public string Summary => IsMissing ? "Launcher nicht gefunden" : "Keine Spiele";

    public string RescanText => IsMissing ? "Erneut suchen" : "Neu einlesen";

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
            case UiAction.Right:
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
    private void OpenSettings() => openSettings(tab);

    [RelayCommand]
    private void Rescan() => rescan(tab);
}
