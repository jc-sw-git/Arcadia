using Arcadia.App.Services;
using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// The window's part of own titles (#496): "Titel ändern" over the detail page, "Titel zurücksetzen", the original title,
/// and what the file of own titles reports (unreadable at the start, a write that failed).
/// </summary>
public partial class MainViewModel
{
    /// <summary>"Titel ändern": the field and the on-screen keyboard over the detail page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChangeTitleOpen))]
    [NotifyPropertyChangedFor(nameof(IsDialogOpen))]
    [NotifyPropertyChangedFor(nameof(OpenTextEditor))]
    public partial ChangeTitleViewModel? ChangeTitle { get; private set; }

    public bool IsChangeTitleOpen => ChangeTitle is not null;

    partial void OnChangeTitleChanged(ChangeTitleViewModel? oldValue, ChangeTitleViewModel? newValue) => oldValue?.Dispose();

    void IGameActionsOwner.ChangeTitle(GameTileViewModel tile)
    {
        var key = tile.Game.Key;
        if (!Session.Titles.CanChange || Session.LauncherGameOf(key) is not { } launcherGame)
        {
            return;
        }

        ChangeTitle = new ChangeTitleViewModel(
            tile.Title,
            Session.Titles.AutomaticTitleOf(launcherGame),
            // The game may have left the library while the keyboard was open; its title is then kept for its return.
            text => FollowingTheGame(key, () => Session.Titles.Change(Session.LauncherGameOf(key) ?? launcherGame, text)),
            CloseChangeTitle,
            editor => SettingsKeyboard.Take(editor, Input.UsesController),
            SettingsKeyboard.GiveBack);
    }

    void IGameActionsOwner.ResetTitle(GameTileViewModel tile) => FollowingTheGame(tile.Game.Key, () => Session.Titles.Reset(tile.Game.Key));

    /// <summary>
    /// Changes a title from the library's game menu so that the focus stays on the game: the new title sorts it elsewhere,
    /// and the rebuilt page would otherwise focus whatever game took its place. The detail page and the search keep their
    /// own focus.
    /// </summary>
    private bool FollowingTheGame(string gameKey, Func<bool> change)
    {
        var follows = Detail is null && Search is null && Library.FocusedGame?.Game.Key == gameKey;
        var changed = change();
        if (follows && Session.Libraries.SelectMany(l => l.Games).FirstOrDefault(g => g.Key == gameKey) is { } shown)
        {
            Library.Show(shown);
        }

        return changed;
    }

    string? IGameActionsOwner.OriginalTitleOf(Game game) =>
        Session.Titles.OwnTitleOf(game.Key) is null ? null : Session.AutomaticTitleOf(game.Key);

    string? IGameActionsOwner.TitleBlockedReason => Session.Titles.CanChange ? null : AppTexts.DetailTitleUnreadable;

    /// <summary>
    /// Follows the file of own titles from the start: unreadable, it is a status message; a write that fails is logged with
    /// its stack trace and toasted, and the titles already went back to the stored ones.
    /// </summary>
    private void WatchUserData(UserDataStore? userData)
    {
        if (userData is null)
        {
            return;
        }

        Status.ShowUserData(userData.IsUnreadable);
        userData.WriteFailed += (_, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => AppErrors.Report(e, AppTexts.ErrorContextUserData));
    }

    private void CloseChangeTitle(ChangeTitleViewModel dialog)
    {
        if (ChangeTitle == dialog)
        {
            ChangeTitle = null;
        }
    }
}
