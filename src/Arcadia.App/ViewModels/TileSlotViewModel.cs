using Arcadia.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.Search;

/// <summary>
/// A place in the search's row or grid of hits, showing one tile. The place stays while its tile
/// changes, so the view keeps its controls and only binds them to the new tile (see <see cref="SearchViewModel.Slots"/>).
/// </summary>
public sealed partial class HitSlotViewModel(GameTileViewModel tile) : ViewModelBase
{
    [ObservableProperty]
    public partial GameTileViewModel Tile { get; set; } = tile;

    /// <summary>
    /// False while there are fewer hits than places. The place keeps its last tile meanwhile: without
    /// one, its controls would go and have to be built again for the next hit.
    /// </summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;
}
