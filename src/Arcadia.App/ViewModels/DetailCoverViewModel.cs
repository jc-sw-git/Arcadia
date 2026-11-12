using CommunityToolkit.Mvvm.ComponentModel;

namespace Arcadia.App.ViewModels;

/// <summary>
/// A cover of the detail carousel (#499): the shown game's in front beside its title, the games before and after it in
/// stacks to the left and right that lead into the depth. <see cref="Offset"/> is its place: 0 in front, -1 and -2 the
/// left stack, 1 and 2 the right one; one place further out on each side it is invisible, so a cover coming in or going
/// out moves rather than pops (`Controls/DepthSlot`).
/// </summary>
public sealed partial class DetailCoverViewModel(GameTileViewModel tile, int offset) : ViewModelBase
{
    public GameTileViewModel Tile { get; } = tile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Layer))]
    public partial int Offset { get; set; } = offset;

    /// <summary>The front cover above the stacks, each stack's nearer cover above the one behind it.</summary>
    public int Layer => 10 - Math.Abs(Offset);
}
