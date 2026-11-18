using Arcadia.Core.Library;
using Arcadia.Core.Metadata;
using Arcadia.Core.UserData;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Filter;

/// <summary>
/// The <see cref="GameFacts"/> the library's filter reads (#466): each game's merged metadata and own tags, kept per game
/// until its metadata or the own tags change, so a tab's facts cost a lookup per game instead of a merge. UI thread only.
/// </summary>
public sealed class FilterFactsSource(Func<string, MergedMetadata> metadataOf, OwnTags tags)
{
    private readonly Dictionary<string, GameFacts> _byKey = [];
    private Dictionary<string, List<string>>? _ownByKey;

    /// <summary>Bumped on every change: a tab's filtered list made with an older one is stale.</summary>
    public int Version { get; private set; }

    public FilterFacts Of(IReadOnlyList<Game> games)
    {
        var own = _ownByKey ??= OwnTagsByGame();
        var byKey = new Dictionary<string, GameFacts>(games.Count);
        foreach (var game in games)
        {
            if (!_byKey.TryGetValue(game.Key, out var facts))
            {
                _byKey[game.Key] = facts = GameFacts.Of(metadataOf(game.Key), own.GetValueOrDefault(game.Key));
            }

            byKey[game.Key] = facts;
        }

        return new FilterFacts(byKey);
    }

    /// <summary>The metadata of these games changed.</summary>
    public void MetadataChanged(IReadOnlyCollection<string> keys)
    {
        foreach (var key in keys)
        {
            _byKey.Remove(key);
        }

        Version++;
    }

    /// <summary>An own tag was made, ticked, renamed or deleted: any game's facts may differ.</summary>
    public void TagsChanged()
    {
        _byKey.Clear();
        _ownByKey = null;
        Version++;
    }

    private Dictionary<string, List<string>> OwnTagsByGame()
    {
        var byGame = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var tag in tags.All)
        {
            foreach (var key in tag.Games)
            {
                if (!byGame.TryGetValue(key, out var names))
                {
                    byGame[key] = names = [];
                }

                names.Add(tag.Name);
            }
        }

        return byGame;
    }
}
