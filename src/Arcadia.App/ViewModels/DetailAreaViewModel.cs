using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>
/// An area in the detail page's rail (#499): its name, the line with its current value, and its rows. The open area's
/// content stands beside the rail; "Übersicht" has no rows, it shows the game's facts.
/// </summary>
public sealed partial class DetailAreaViewModel : ViewModelBase
{
    public const string OverviewKey = "overview";
    public const string ControllerKey = "controller";
    public const string ManageKey = "manage";

    private readonly Action<DetailAreaViewModel> _open;

    public DetailAreaViewModel(string key, string name, string iconData, Action<DetailAreaViewModel> open)
    {
        Key = key;
        Name = name;
        Icon = StreamGeometry.Parse(iconData);
        _open = open;
    }

    public string Key { get; }

    public string Name { get; }

    /// <summary>Stroke icon in a 24×24 box.</summary>
    public Geometry Icon { get; }

    public bool IsOverview => Key == OverviewKey;

    /// <summary>The line below the name, e.g. "Modus „Arcadia“"; it follows the area's values.</summary>
    [ObservableProperty]
    public partial string ValueLine { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    public partial IReadOnlyList<DetailRowViewModel> Rows { get; set; } = [];

    /// <summary>Settings or actions to explain: the info box stands beside them.</summary>
    public bool HasRows => Rows.Count > 0;

    /// <summary>Its content is shown beside the rail.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>The focus is on it in the rail.</summary>
    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>A click in the rail opens it, with the focus on it.</summary>
    [RelayCommand]
    private void Open() => _open(this);
}
