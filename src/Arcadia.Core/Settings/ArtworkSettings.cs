using System.Text.Json.Serialization;
using Arcadia.Core.Artwork;

namespace Arcadia.Core.Settings;

/// <summary>Where covers and backgrounds come from (#13, #489), in the order of "Reihenfolge für Artwork".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArtworkSourceKind>))]
public enum ArtworkSourceKind
{
    /// <summary>The launchers' own images (<c>ILauncherArtwork</c>); they need no key.</summary>
    Launcher,

    /// <summary>SteamGridDB, with the player's key.</summary>
    SteamGridDb,
}

/// <summary>"Zuschneiden erlaubt" (#13): how much of an image a centred crop may cut away for it to fit its frame.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CropAllowance>))]
public enum CropAllowance
{
    /// <summary>"Wenig": up to 5 %.</summary>
    Little,

    /// <summary>"Normal": up to 15 %, the default.</summary>
    Normal,

    /// <summary>"Viel": up to 30 %.</summary>
    Much,
}

/// <summary>"Bei mehreren Treffern" (#491): what happens when a game found only by its name matches several games.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SeveralHits>))]
public enum SeveralHits
{
    /// <summary>"Selbst auswählen", the default: the game waits in "Warten auf deine Auswahl".</summary>
    Ask,

    /// <summary>"Ersten nehmen": the first hit in SteamGridDB's order.</summary>
    TakeFirst,
}

/// <summary>
/// The settings of the group "Reihenfolge für Artwork", "Zuschneiden erlaubt" (#489) and "Bei mehreren Treffern" (#491) in
/// "Datenquellen".
/// </summary>
public sealed record ArtworkSettings
{
    /// <summary>Launcher images first, then SteamGridDB (#13).</summary>
    public static IReadOnlyList<ArtworkSourceKind> DefaultOrder { get; } = [ArtworkSourceKind.Launcher, ArtworkSourceKind.SteamGridDb];

    public IReadOnlyList<ArtworkSourceKind> Order { get; init; } = DefaultOrder;

    /// <summary>
    /// The sources switched off in "Reihenfolge für Artwork" (#506): never asked, their images not shown (kept on disk).
    /// </summary>
    public IReadOnlyList<ArtworkSourceKind> Off { get; init; } = [];

    /// <summary>The sources switched on, in their order.</summary>
    public IReadOnlyList<ArtworkSourceKind> OnInOrder => [.. Order.Where(IsOn)];

    public bool IsOn(ArtworkSourceKind source) => Off?.Contains(source) != true;

    public CropAllowance Crop { get; init; } = CropAllowance.Normal;

    public SeveralHits SeveralHits { get; init; } = SeveralHits.Ask;

    /// <summary>The share of a cover <see cref="Crop"/> lets the crop cut away; backgrounds always use "Normal" (#562).</summary>
    public double Tolerance => ToleranceOf(Crop);

    public static double ToleranceOf(CropAllowance crop) => crop switch
    {
        CropAllowance.Little => 0.05,
        CropAllowance.Much => 0.30,
        _ => ArtworkFormat.NormalTolerance,
    };

    /// <summary>
    /// What a hand-edited or newer <c>settings.json</c> may hold, made usable: every known source once, in the saved order,
    /// a missing one at the end, an unknown source switched off left out; an unknown tolerance is "Normal", an unknown "Bei mehreren Treffern" "Selbst auswählen".
    /// </summary>
    public ArtworkSettings WithValidValues()
    {
        var order = (Order ?? DefaultOrder).Where(Enum.IsDefined).Distinct().ToList();
        order.AddRange(DefaultOrder.Except(order));
        var off = (Off ?? []).Where(Enum.IsDefined).Distinct().ToList();
        return this with
        {
            Order = order.SequenceEqual(Order ?? []) ? Order! : order,
            Off = off.SequenceEqual(Off ?? []) ? Off! : off,
            Crop = Enum.IsDefined(Crop) ? Crop : CropAllowance.Normal,
            SeveralHits = Enum.IsDefined(SeveralHits) ? SeveralHits : SeveralHits.Ask,
        };
    }
}
