using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Library;

/// <summary>
/// The one place that says which title a game shows (#43, #496): the player's own title above everything, else the
/// automatic title, which is a provider's correction of a doubtful launcher title (#494) or the launcher's title. The
/// library, the search and the sorting see the shown title; matching with providers keeps the launcher's.
/// </summary>
public sealed class GameTitles
{
    private readonly UserDataStore? _userData;
    private readonly Func<string, string?> _automaticTitle;

    /// <param name="userData">Where own titles are kept; null: none can be set.</param>
    /// <param name="automaticTitle">A provider's title for a game key that replaces the launcher's, or null to keep it (#494).</param>
    public GameTitles(UserDataStore? userData = null, Func<string, string?>? automaticTitle = null)
    {
        _userData = userData;
        _automaticTitle = automaticTitle ?? (_ => null);
        if (userData is not null)
        {
            userData.Changed += (_, keys) => Changed?.Invoke(this, keys);
        }
    }

    /// <summary>Where own titles are kept; null: none can be set.</summary>
    public UserDataStore? UserData => _userData;

    /// <summary>Own titles can be set and reset: there is a store and its file could be read.</summary>
    public bool CanChange => _userData is { IsUnreadable: false };

    /// <summary>
    /// The shown title of these games changed: an own title set, reset or put back after a failed write, or an automatic
    /// title that changed (<see cref="AutomaticTitlesChanged"/>). Raised on the thread that changed it.
    /// </summary>
    public event EventHandler<IReadOnlyCollection<string>>? Changed;

    /// <summary>The title the game would show without an own one; <paramref name="launcherGame"/> as its launcher delivered it.</summary>
    public string AutomaticTitleOf(Game launcherGame) =>
        _automaticTitle(launcherGame.Key) is { Length: > 0 } corrected ? corrected : launcherGame.Title;

    /// <summary>The player's title, null without one or while the file holding it is unreadable.</summary>
    public string? OwnTitleOf(string gameKey) => CanChange ? _userData!.Of(gameKey).Title : null;

    /// <summary><paramref name="launcherGame"/> with its shown title; the same instance when that is the launcher's.</summary>
    public Game Shown(Game launcherGame)
    {
        var title = OwnTitleOf(launcherGame.Key) ?? AutomaticTitleOf(launcherGame);
        return title == launcherGame.Title ? launcherGame : launcherGame with { Title = title };
    }

    /// <summary>
    /// What is kept of a typed title: without spaces around it; empty or the automatic title itself is no own title, so
    /// it clears one.
    /// </summary>
    public static string? OwnTitleFrom(string typed, string automaticTitle) =>
        typed.Trim() is { Length: > 0 } title && !string.Equals(title, automaticTitle, StringComparison.Ordinal) ? title : null;

    /// <summary>Too long to be kept (<see cref="UserDataStore.MaxTitleLength"/>).</summary>
    public static bool IsTooLong(string typed) => typed.Trim().Length > UserDataStore.MaxTitleLength;

    /// <summary>"Titel ändern": sets <paramref name="typed"/> as the game's own title (<see cref="OwnTitleFrom"/>). False when none can be set.</summary>
    public bool Change(Game launcherGame, string typed)
    {
        var own = OwnTitleFrom(typed, AutomaticTitleOf(launcherGame));
        return _userData?.Change(launcherGame.Key, data => data with { Title = own }) ?? false;
    }

    /// <summary>"Titel zurücksetzen": the automatic title again. False when none can be set.</summary>
    public bool Reset(string gameKey) => _userData?.Change(gameKey, data => data with { Title = null }) ?? false;

    /// <summary>A provider's title of these games changed (#494): their shown titles follow without a scan.</summary>
    public void AutomaticTitlesChanged(IReadOnlyCollection<string> gameKeys) => Changed?.Invoke(this, gameKeys);
}
