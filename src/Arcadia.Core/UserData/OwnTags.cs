using System.Collections.Immutable;
using System.Globalization;
using Arcadia.Core.Library;

namespace Arcadia.Core.UserData;

/// <summary>How a rename turned out (#495).</summary>
public enum TagRename
{
    /// <summary>Nothing changed: the name was the same, empty, or couldn't be kept, or the tag is gone.</summary>
    Unchanged,

    /// <summary>The tag has the new name on every game, absent ones too.</summary>
    Renamed,

    /// <summary>Another tag has that name ignoring case: the player is asked whether to merge (<see cref="OwnTags.Merge"/>).</summary>
    NeedsMerge,
}

/// <summary>
/// The rules of the player's own tags (#43, #495) over <see cref="UserDataStore"/>: names are trimmed, at most
/// <see cref="UserDataStore.MaxTagLength"/> characters and equal ignoring case; tags are global across launchers and
/// listed alphabetically; a tag without games stays until it is deleted; delete and rename reach games absent from the
/// library too. Every change holds at once and is written in the background.
/// </summary>
public sealed class OwnTags
{
    private readonly UserDataStore? _store;
    private readonly IComparer<string> _order;

    /// <param name="store">Where own tags are kept; null: none can be made.</param>
    /// <param name="culture">The alphabet the tags are listed in, ignoring case (German in the App).</param>
    public OwnTags(UserDataStore? store = null, CultureInfo? culture = null)
    {
        _store = store;
        _order = GameSorter.TitleComparer(culture);
        if (store is not null)
        {
            store.TagsChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Tags can be made and changed: there is a store and its file could be read.</summary>
    public bool CanChange => _store is { IsUnreadable: false };

    /// <summary>A tag was made, ticked, renamed, merged, deleted or put back after a failed write; raised on the thread that changed it.</summary>
    public event EventHandler? Changed;

    /// <summary>Every tag alphabetically, ignoring case; none while the file is unreadable.</summary>
    public IReadOnlyList<OwnTag> All => CanChange ? [.. _store!.Tags.OrderBy(tag => tag.Name, _order)] : [];

    /// <summary>The names of the game's tags, alphabetically.</summary>
    public IReadOnlyList<string> Of(string gameKey) => [.. All.Where(tag => tag.Games.Contains(gameKey)).Select(tag => tag.Name)];

    /// <summary>The tag of this name, ignoring case; null without one.</summary>
    public OwnTag? Find(string name) => All.FirstOrDefault(tag => UserDataStore.TagNames.Equals(tag.Name, name));

    /// <summary>What is kept of a typed name: trimmed; null when it is empty, too long or holds control characters.</summary>
    public static string? NameFrom(string typed) => UserDataStore.CheckedTagName(typed);

    /// <summary>
    /// "Neuer Tag": the tag of the typed name, made unless one of that name (ignoring case) exists, then that one. With
    /// <paramref name="forGame"/> the game has it at once. The tag's name, or null when the name can't be kept or nothing
    /// can be changed.
    /// </summary>
    public string? Create(string typed, string? forGame = null)
    {
        if (NameFrom(typed) is not { } name || !CanChange)
        {
            return null;
        }

        var existing = Find(name)?.Name;
        var changed = _store!.ChangeTags(tags =>
        {
            var all = existing is null ? tags.Add(new OwnTag(name)) : tags;
            return forGame is null ? all : WithGame(all, existing ?? name, forGame, has: true);
        });
        return changed ? existing ?? name : null;
    }

    /// <summary>Ticks or unticks the tag for the game. False when nothing can be changed or the tag is gone.</summary>
    public bool Set(string name, string gameKey, bool has) =>
        CanChange && Find(name) is not null && _store!.ChangeTags(tags => WithGame(tags, name, gameKey, has));

    /// <summary>Deletes the tag from every game, absent ones too. False when nothing can be changed.</summary>
    public bool Delete(string name) =>
        CanChange && _store!.ChangeTags(tags => tags.Where(tag => !UserDataStore.TagNames.Equals(tag.Name, name)));

    /// <summary>
    /// Renames the tag on every game, absent ones too. A name differing only in case renames without asking; the name of
    /// another tag asks first (<see cref="TagRename.NeedsMerge"/>, then <see cref="Merge"/>).
    /// </summary>
    public TagRename Rename(string name, string typed)
    {
        if (!CanChange || NameFrom(typed) is not { } newName || Find(name) is not { } tag || string.Equals(tag.Name, newName, StringComparison.Ordinal))
        {
            return TagRename.Unchanged;
        }

        if (!UserDataStore.TagNames.Equals(tag.Name, newName) && Find(newName) is not null)
        {
            return TagRename.NeedsMerge;
        }

        var renamed = _store!.ChangeTags(tags => tags.Select(t => UserDataStore.TagNames.Equals(t.Name, tag.Name) ? t with { Name = newName } : t));
        return renamed ? TagRename.Renamed : TagRename.Unchanged;
    }

    /// <summary>
    /// Merges the tag into <paramref name="into"/>, which keeps its name: every game of either has it once. False when
    /// nothing can be changed or either is gone.
    /// </summary>
    public bool Merge(string name, string into)
    {
        if (!CanChange || Find(name) is not { } from || Find(into) is not { } target || ReferenceEquals(from, target))
        {
            return false;
        }

        return _store!.ChangeTags(tags => tags
            .Where(tag => !UserDataStore.TagNames.Equals(tag.Name, from.Name))
            .Select(tag => UserDataStore.TagNames.Equals(tag.Name, target.Name) ? tag with { Games = tag.Games.Union(from.Games) } : tag));
    }

    private static IEnumerable<OwnTag> WithGame(ImmutableList<OwnTag> tags, string name, string gameKey, bool has) =>
        tags.Select(tag => !UserDataStore.TagNames.Equals(tag.Name, name) ? tag : tag with { Games = has ? tag.Games.Add(gameKey) : tag.Games.Remove(gameKey) });
}
