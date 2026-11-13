using System.Text.Json.Serialization;

namespace Arcadia.Core.Settings;

/// <summary>The providers of metadata in the order of "Reihenfolge für Metadaten" (#494).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetadataSourceKind>))]
public enum MetadataSourceKind
{
    /// <summary>The launchers' own data: their local files and store pages (#493).</summary>
    Launcher,

    /// <summary>IGDB, with the player's access.</summary>
    Igdb,
}

/// <summary>
/// Tab "Metadaten" of "Datenquellen": "Reihenfolge für Metadaten" (#494), one order for all fields, each source on or off;
/// and its own "Bei mehreren Treffern" (#538).
/// </summary>
public sealed record MetadataSettings
{
    /// <summary>Launcher data first, then IGDB (#43).</summary>
    public static IReadOnlyList<MetadataSourceKind> DefaultOrder { get; } = [MetadataSourceKind.Launcher, MetadataSourceKind.Igdb];

    public IReadOnlyList<MetadataSourceKind> Order { get; init; } = DefaultOrder;

    /// <summary>The sources switched off: never asked, their fields not shown (the cache keeps them).</summary>
    public IReadOnlyList<MetadataSourceKind> Off { get; init; } = [];

    /// <summary>
    /// "Bei mehreren Treffern" for IGDB's search by name; null in a <c>settings.json</c> from before #538, when artwork and
    /// metadata shared the artwork's choice, which then still holds (<see cref="SeveralHitsOr"/>).
    /// </summary>
    public SeveralHits? SeveralHits { get; init; }

    /// <summary>The metadata's own choice, else the one it shared with the artwork before.</summary>
    public SeveralHits SeveralHitsOr(SeveralHits shared) => SeveralHits is { } own && Enum.IsDefined(own) ? own : shared;

    public bool IsOn(MetadataSourceKind source) => Off?.Contains(source) != true;

    /// <summary>The sources switched on, in their order.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<MetadataSourceKind> OnInOrder => [.. Order.Where(IsOn)];

    /// <summary>A hand-edited or newer <c>settings.json</c> made usable: every known source once, a missing one at the end.</summary>
    public MetadataSettings WithValidValues()
    {
        var order = (Order ?? DefaultOrder).Where(Enum.IsDefined).Distinct().ToList();
        order.AddRange(DefaultOrder.Except(order));
        var off = (Off ?? []).Where(Enum.IsDefined).Distinct().ToList();
        return this with
        {
            Order = order.SequenceEqual(Order ?? []) ? Order! : order,
            Off = off.SequenceEqual(Off ?? []) ? Off! : off,
        };
    }
}
