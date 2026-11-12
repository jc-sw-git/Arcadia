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

/// <summary>The settings of the group "Reihenfolge für Artwork" and "Zuschneiden erlaubt" in "Datenquellen" (#489).</summary>
public sealed record ArtworkSettings
{
    /// <summary>Launcher images first, then SteamGridDB (#13).</summary>
    public static IReadOnlyList<ArtworkSourceKind> DefaultOrder { get; } = [ArtworkSourceKind.Launcher, ArtworkSourceKind.SteamGridDb];

    public IReadOnlyList<ArtworkSourceKind> Order { get; init; } = DefaultOrder;

    public CropAllowance Crop { get; init; } = CropAllowance.Normal;

    /// <summary>The share of an image <see cref="Crop"/> lets the crop cut away.</summary>
    public double Tolerance => ToleranceOf(Crop);

    public static double ToleranceOf(CropAllowance crop) => crop switch
    {
        CropAllowance.Little => 0.05,
        CropAllowance.Much => 0.30,
        _ => ArtworkFormat.NormalTolerance,
    };

    /// <summary>
    /// What a hand-edited or newer <c>settings.json</c> may hold, made usable: every known source once, in the saved order,
    /// a missing one at the end; an unknown tolerance is "Normal".
    /// </summary>
    public ArtworkSettings WithValidValues()
    {
        var order = (Order ?? DefaultOrder).Where(Enum.IsDefined).Distinct().ToList();
        order.AddRange(DefaultOrder.Except(order));
        return this with
        {
            Order = order.SequenceEqual(Order ?? []) ? Order! : order,
            Crop = Enum.IsDefined(Crop) ? Crop : CropAllowance.Normal,
        };
    }
}
