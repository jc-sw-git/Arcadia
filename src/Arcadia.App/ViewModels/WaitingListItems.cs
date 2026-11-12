using Arcadia.App.Services;
using Arcadia.Core.Artwork;
using Arcadia.Core.Artwork.SteamGridDb;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>One game on the waiting list, left: its initial, name and launcher.</summary>
public sealed partial class WaitingGameViewModel(WaitingGame waiting, Action<WaitingGameViewModel> pick) : ViewModelBase
{
    /// <summary>The game as the service has it now; its hits change when some are dropped for having no image (#506).</summary>
    [ObservableProperty]
    public partial WaitingGame Waiting { get; set; } = waiting;

    public string Key => Waiting.Game.Key;

    public string Title => Waiting.Game.Title;

    public string Monogram => Formatting.Monogram(Waiting.Game.Title);

    public string LauncherName => Waiting.LauncherName;

    /// <summary>The game the focus is on in the games list, and whose hits show right.</summary>
    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    /// <summary>A click makes it the current game.</summary>
    [RelayCommand]
    private void Pick() => pick(this);
}

/// <summary>One hit of the current game, right: its image of the sought kind (a placeholder with its name until it is there), name and year.</summary>
public sealed partial class WaitingHitViewModel(SteamGridDbHit hit, Action<WaitingHitViewModel> take) : ViewModelBase
{
    public SteamGridDbHit Hit { get; } = hit;

    public string Name => Hit.Name;

    /// <summary>The year it came out; empty when SteamGridDB doesn't know it.</summary>
    public string YearText => Hit.Year?.ToString(Formatting.Culture) ?? string.Empty;

    public bool HasYear => Hit.Year is not null;

    /// <summary>Its cover, or its background when only that is sought.</summary>
    [ObservableProperty]
    public partial ArtworkFile? Image { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A click takes it, as A on it.</summary>
    [RelayCommand]
    private void Pick() => take(this);
}
