using Arcadia.App.Settings;
using Arcadia.Core.Artwork;
using Arcadia.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// One tile of "Artwork ändern" (#490): the player's own file (empty until one is chosen) or an offered image, with its tag
/// on the image and its caption below.
/// </summary>
public sealed partial class ArtworkTileViewModel : ViewModelBase
{
    private readonly Action<ArtworkTileViewModel> _pick;

    private ArtworkTileViewModel(Action<ArtworkTileViewModel> pick) => _pick = pick;

    /// <summary>The offered image; null for the own file's tile.</summary>
    public ArtworkOffer? Offer { get; private init; }

    public bool IsOwnFile => Offer is null;

    /// <summary>The image the tile shows; null for an own file not chosen yet.</summary>
    public ArtworkFile? File { get; private init; }

    /// <summary>"Eigene Datei" with its folder icon: nothing chosen yet, A opens the file dialog.</summary>
    public bool IsEmptyOwnFile => IsOwnFile && File is null;

    public string Tag { get; private init; } = string.Empty;

    public bool HasTag => Tag.Length > 0;

    public string Caption { get; private init; } = string.Empty;

    public static string OwnFileLabel => AppTexts.ArtworkOwnFile;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>The image the game shows now: the tile gets the tick.</summary>
    [ObservableProperty]
    public partial bool IsInUse { get; set; }

    /// <summary>A click: the same as A on the focused tile.</summary>
    [RelayCommand]
    private void Pick() => _pick(this);

    public static ArtworkTileViewModel OwnFile(ArtworkFile? file, string? name, Action<ArtworkTileViewModel> pick) => new(pick)
    {
        File = file,
        Tag = file is null ? string.Empty : AppTexts.ArtworkOwnFileTag,
        Caption = file is null ? AppTexts.ArtworkOwnFileFormats : name ?? AppTexts.ArtworkOwnFile,
    };

    public static ArtworkTileViewModel Offered(ArtworkOffer offer, string launcherName, Action<ArtworkTileViewModel> pick) => new(pick)
    {
        Offer = offer,
        File = offer.File,
        // SteamGridDB's API gives every image the rating 0, whatever its likes on the site: its tiles carry no tag (#532).
        Tag = offer.Source == ArtworkSource.SteamGridDb ? string.Empty : launcherName.ToUpperInvariant(),
        Caption = offer.Source != ArtworkSource.SteamGridDb ? launcherName
            : offer.File.Kind == Launchers.Abstractions.ArtworkKind.Hero
                ? AppTexts.ArtworkSuggestionHero(AppTexts.ArtworkImageSize(width: offer.File.Size.Width, height: offer.File.Size.Height))
                : SteamGridDbName,
    };

    // A product name, not a UI text (docs/architecture/ui-texts.md).
    internal const string SteamGridDbName = "SteamGridDB";
}

/// <summary>
/// The notice in "Artwork ändern" without a working key (#490): how to get more images, and "Zu den Datenquellen",
/// reachable with the D-pad after the tiles.
/// </summary>
public sealed partial class ArtworkNoticeViewModel(Action openDataSources) : ViewModelBase
{
    /// <summary>Why the modal offers fewer images: no key, a rejected key or a source switched off (#506).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Text))]
    public partial ArtworkNoticeReason Reason { get; private set; }

    public string Title => Reason switch
    {
        ArtworkNoticeReason.BadKey => AppTexts.ArtworkBadKeyTitle,
        ArtworkNoticeReason.SteamGridDbOff => AppTexts.ArtworkSteamGridDbOffTitle,
        ArtworkNoticeReason.LauncherImagesOff => AppTexts.ArtworkLauncherImagesOffTitle,
        ArtworkNoticeReason.BothOff => AppTexts.ArtworkSourcesOffTitle,
        _ => AppTexts.ArtworkNoKeyTitle,
    };

    public string Text => Reason switch
    {
        ArtworkNoticeReason.BadKey => AppTexts.ArtworkBadKeyText,
        ArtworkNoticeReason.SteamGridDbOff => AppTexts.ArtworkSteamGridDbOffText,
        ArtworkNoticeReason.LauncherImagesOff => AppTexts.ArtworkLauncherImagesOffText,
        ArtworkNoticeReason.BothOff => AppTexts.ArtworkSourcesOffText,
        _ => AppTexts.ArtworkNoKeyText,
    };

    /// <summary>The row of "Datenquellen" the button leads to: the first source switched off, else the key.</summary>
    public string RowKey => Reason switch
    {
        ArtworkNoticeReason.SteamGridDbOff => ArtworkSourceRows.SourceKey(ArtworkSourceKind.SteamGridDb),
        ArtworkNoticeReason.LauncherImagesOff or ArtworkNoticeReason.BothOff => ArtworkSourceRows.SourceKey(ArtworkSourceKind.Launcher),
        _ => SteamGridDbKeyRows.KeyRowKey,
    };

    /// <summary>A notice shows: the key isn't usable or a source is switched off.</summary>
    public bool IsShown { get; private set; }

    public static string ButtonText => AppTexts.ArtworkToDataSources;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A source switched off says so before the key does: switching it on comes first.</summary>
    public void Show(SuggestionKeyState state, IReadOnlyList<ArtworkSourceKind> off)
    {
        var launcherOff = off.Contains(ArtworkSourceKind.Launcher);
        var steamGridDbOff = off.Contains(ArtworkSourceKind.SteamGridDb);
        Reason = (launcherOff, steamGridDbOff) switch
        {
            (true, true) => ArtworkNoticeReason.BothOff,
            (true, false) => ArtworkNoticeReason.LauncherImagesOff,
            (false, true) => ArtworkNoticeReason.SteamGridDbOff,
            _ => state == SuggestionKeyState.Invalid ? ArtworkNoticeReason.BadKey : ArtworkNoticeReason.NoKey,
        };
        IsShown = launcherOff || steamGridDbOff || state != SuggestionKeyState.Valid;
    }

    [RelayCommand]
    private void OpenDataSources() => openDataSources();
}

public enum ArtworkNoticeReason
{
    NoKey,
    BadKey,
    SteamGridDbOff,
    LauncherImagesOff,
    BothOff,
}
