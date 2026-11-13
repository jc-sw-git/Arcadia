using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Core.Metadata;
using Arcadia.Core.Metadata.Igdb;
using Arcadia.Launchers.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// One entry on the waiting list, left: its initial, name, launcher and what it waits for (#494, D6): "Artwork" (a
/// SteamGridDB game) or "Metadaten" (an IGDB game). A game waiting for both is there twice, each decided on its own.
/// </summary>
/// <param name="title">The title it shows, its own one included (#496); SteamGridDB and IGDB are searched by the launcher's.</param>
public sealed partial class WaitingGameViewModel : ViewModelBase
{
    private readonly Action<WaitingGameViewModel> _pick;

    public WaitingGameViewModel(WaitingGame artwork, string title, Action<WaitingGameViewModel> pick)
        : this(artwork.Game, artwork.LauncherName, title, pick)
    {
        Artwork = artwork;
    }

    public WaitingGameViewModel(WaitingMetadataGame metadata, string title, Action<WaitingGameViewModel> pick)
        : this(metadata.Game, metadata.LauncherName, title, pick)
    {
        Metadata = metadata;
    }

    private WaitingGameViewModel(Game game, string launcherName, string title, Action<WaitingGameViewModel> pick)
    {
        Game = game;
        LauncherName = launcherName;
        Title = title;
        _pick = pick;
    }

    /// <summary>The entry's key on the list: the game's key and what it waits for.</summary>
    public string Key => KeyOf(Game, IsMetadata);

    public static string KeyOf(Game game, bool metadata) => (metadata ? "metadata|" : "artwork|") + game.Key;

    public Game Game { get; }

    /// <summary>
    /// The artwork entry as the service has it now; its hits change when some are dropped for having no image (#506).
    /// Null for a metadata entry.
    /// </summary>
    public WaitingGame? Artwork { get; set; }

    /// <summary>The metadata entry as the service has it now; null for an artwork entry.</summary>
    public WaitingMetadataGame? Metadata { get; set; }

    public bool IsMetadata => Metadata is not null;

    public string Title { get; }

    public string Monogram => Formatting.Monogram(Title);

    public string LauncherName { get; }

    /// <summary>The entry the focus is on in the games list, and whose hits show right.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>A click makes it the current entry.</summary>
    [RelayCommand]
    private void Pick() => _pick(this);
}

/// <summary>One hit of the current entry, right: its image (a placeholder with its name until it is there), name and year.</summary>
/// <param name="mark">A click: marks it (#539).</param>
/// <param name="take">A double click: chooses it, as A on it.</param>
public abstract partial class WaitingHitViewModel(Action<WaitingHitViewModel> mark, Action<WaitingHitViewModel> take) : ViewModelBase
{
    public abstract string Name { get; }

    /// <summary>The year it came out, empty when the source doesn't know it.</summary>
    public string YearText => Year?.ToString(Formatting.Culture) ?? string.Empty;

    public bool HasYear => Year is not null;

    protected abstract int? Year { get; }

    /// <summary>Its cover, or its background when only that is sought.</summary>
    [ObservableProperty]
    public partial ArtworkFile? Image { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A click only marks it: a hit is chosen on purpose, with A or a double click (#539).</summary>
    [RelayCommand]
    private void Mark() => mark(this);

    [RelayCommand]
    private void Take() => take(this);
}

/// <summary>A SteamGridDB game offered for an artwork entry (#491): its cover, or its background.</summary>
public sealed class ArtworkHitViewModel(SteamGridDbHit hit, Action<WaitingHitViewModel> mark, Action<WaitingHitViewModel> take)
    : WaitingHitViewModel(mark, take)
{
    public SteamGridDbHit Hit { get; } = hit;

    /// <summary>
    /// A background offered after the cover was chosen (cover and background sought, #539): the game the cover comes from,
    /// which may be another one. Null for a cover, and for a background sought alone.
    /// </summary>
    public SteamGridDbHit? Cover { get; init; }

    public override string Name => Hit.Name;

    protected override int? Year => Hit.Year;
}

/// <summary>
/// An IGDB game offered for a metadata entry (#494): its name and year in the list, its facts in the info box beside it
/// (#539), checked as any metadata before it is shown.
/// </summary>
public sealed class MetadataHitViewModel(IgdbGame hit, Action<WaitingHitViewModel> mark, Action<WaitingHitViewModel> take)
    : WaitingHitViewModel(mark, take)
{
    private WaitingHitFacts? _facts;

    public IgdbGame Hit { get; } = hit;

    public override string Name => Hit.Name;

    /// <summary>What sets it apart from the other hits: genres and developer, under its name in the list.</summary>
    public string Line => Facts.Line;

    public bool HasLine => Line.Length > 0;

    public WaitingHitFacts Facts => _facts ??= new WaitingHitFacts(Hit.Data);

    protected override int? Year => Hit.Year;
}
