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

/// <summary>"Reihenfolge für Metadaten" in "Datenquellen" (#494): one order for all fields, each source on or off.</summary>
public sealed record MetadataSettings
{
    /// <summary>Launcher data first, then IGDB (#43).</summary>
    public static IReadOnlyList<MetadataSourceKind> DefaultOrder { get; } = [MetadataSourceKind.Launcher, MetadataSourceKind.Igdb];

    public IReadOnlyList<MetadataSourceKind> Order { get; init; } = DefaultOrder;

    /// <summary>The sources switched off: never asked, their fields not shown (the cache keeps them).</summary>
    public IReadOnlyList<MetadataSourceKind> Off { get; init; } = [];

    public bool IsOn(MetadataSourceKind source) => Off?.Contains(source) != true;

    /// <summary>The sources switched on, in their order.</summary>
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
