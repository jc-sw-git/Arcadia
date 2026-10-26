using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A place in the library's grid (<see cref="LibraryViewModel.Slots"/>) or the search's hits (<see cref="Search.SearchViewModel.Slots"/>),
/// showing one tile. The place stays while its tile changes, so the view keeps its controls and only binds them to the new
/// tile: building a cover's controls again costs far more than binding them (#229).
/// </summary>
public sealed partial class TileSlotViewModel(GameTileViewModel tile) : ViewModelBase
{
    [ObservableProperty]
    public partial GameTileViewModel Tile { get; set; } = tile;

    /// <summary>
    /// False while there are fewer hits than places. The place keeps its last tile meanwhile: without
    /// one, its controls would go and have to be built again for the next tile.
    /// </summary>
    [ObservableProperty]
    public partial bool IsShown { get; set; } = true;

    /// <summary>Shows <paramref name="tiles"/> in <paramref name="slots"/>, adding places as needed; places left over are hidden.</summary>
    public static void Fill(ObservableCollection<TileSlotViewModel> slots, IReadOnlyList<GameTileViewModel> tiles)
    {
        for (var i = 0; i < Math.Max(slots.Count, tiles.Count); i++)
        {
            if (i >= slots.Count)
            {
                slots.Add(new TileSlotViewModel(tiles[i]));
            }
            else if (i < tiles.Count)
            {
                slots[i].Tile = tiles[i];
                slots[i].IsShown = true;
            }
            else
            {
                slots[i].IsShown = false;
            }
        }
    }
}
