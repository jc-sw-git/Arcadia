using System.Text.Json;
using System.Text.Json.Serialization;

namespace Arcadia.Core.Settings;

/// <summary>
/// The quick selection in a game's "Übersicht" (#500): which entries of the other areas it shows, by their stable ids. A
/// game shows its own selection, else the one applied to all games, else the defaults. So a default entry a later version
/// adds reaches only games whose selection was never changed, and only while nothing was applied to all games.
/// </summary>
[JsonConverter(typeof(QuickSelectionJsonConverter))]
public sealed record QuickSelectionSettings
{
    /// <summary>"n von 5": a selection holds at most this many entries.</summary>
    public const int MaxEntries = 5;

    /// <summary>The selection "Für alle Spiele übernehmen" stored; null until it was used.</summary>
    public IReadOnlyList<string>? AllGames { get; init; }

    /// <summary>Each game's own selection, by <c>Game.Key</c>; also of games no longer in the library, so it is back with them.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> PerGame { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// <c>settings.json</c> held a quick selection this version can't read (a wrong type, written by hand): the defaults
    /// apply, the rest of the settings still load, and the next change replaces it.
    /// </summary>
    [JsonIgnore]
    public bool WasUnreadable { get; init; }

    /// <summary>The ids the game shows, in the order stored; ids this version doesn't know are the caller's to drop.</summary>
    public IReadOnlyList<string> Of(string gameKey, IReadOnlyList<string> defaults) =>
        PerGame.TryGetValue(gameKey, out var own) ? own : AllGames ?? defaults;

    /// <summary>The game's own selection: only this game differs from then on.</summary>
    public QuickSelectionSettings WithGame(string gameKey, IReadOnlyList<string> entries) =>
        this with { PerGame = new Dictionary<string, IReadOnlyList<string>>(PerGame, StringComparer.Ordinal) { [gameKey] = Limited(entries) } };

    /// <summary>
    /// "Ersetzen" after "Für alle Spiele übernehmen": the selection for all games, also for games added later, and no game's
    /// own selection left. One value, so the one save that stores it never leaves a mix of old and new.
    /// </summary>
    public static QuickSelectionSettings ForAllGames(IReadOnlyList<string> entries) => new() { AllGames = Limited(entries) };

    /// <summary>No more than <see cref="MaxEntries"/>, each once, whatever a caller or the file hands in.</summary>
    internal static IReadOnlyList<string> Limited(IEnumerable<string?> entries) =>
        [.. entries.OfType<string>().Where(id => id.Length > 0).Distinct(StringComparer.Ordinal).Take(MaxEntries)];
}

/// <summary>
/// Reads a quick selection without ever failing the whole file: a value of the wrong shape gives the defaults
/// (<see cref="QuickSelectionSettings.WasUnreadable"/>), so a hand edit there doesn't cost the player their other settings.
/// </summary>
internal sealed class QuickSelectionJsonConverter : JsonConverter<QuickSelectionSettings>
{
    public override QuickSelectionSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var value = JsonDocument.ParseValue(ref reader);
        try
        {
            var stored = value.RootElement.Deserialize<Stored>(options);
            return new QuickSelectionSettings
            {
                AllGames = stored?.AllGames is { } all ? QuickSelectionSettings.Limited(all) : null,
                PerGame = (stored?.PerGame ?? [])
                    .Where(game => game.Value is not null)
                    .ToDictionary(game => game.Key, game => QuickSelectionSettings.Limited(game.Value!), StringComparer.Ordinal),
            };
        }
        catch (JsonException)
        {
            return new QuickSelectionSettings { WasUnreadable = true };
        }
    }

    public override void Write(Utf8JsonWriter writer, QuickSelectionSettings value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new Stored(value.AllGames is { } all ? [.. all] : null, value.PerGame.ToDictionary(game => game.Key, game => (List<string?>?)[.. game.Value])), options);

    /// <summary>The file's shape; nulls inside are tolerated and dropped.</summary>
    private sealed record Stored(List<string?>? AllGames, Dictionary<string, List<string?>?>? PerGame);
}
