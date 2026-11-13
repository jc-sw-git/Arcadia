using Arcadia.App.ViewModels;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Settings;

/// <summary>The tabs of "Datenquellen" above its rows (#538): "Artwork" and "Metadaten", LT/RT (Q/E) or a click switch them.</summary>
public sealed class DataSourcesHeadViewModel(IReadOnlyList<DataSourcesTabViewModel> tabs) : ViewModelBase
{
    public IReadOnlyList<DataSourcesTabViewModel> Tabs { get; } = tabs;
}

/// <summary>A tab of "Datenquellen": its icon and name, chosen or not, and a warning beside the name.</summary>
public sealed partial class DataSourcesTabViewModel(IDataSourcesTab tab, Action<IDataSourcesTab> choose) : ViewModelBase
{
    public IDataSourcesTab Tab { get; } = tab;

    public string Name => Tab.Name;

    public Geometry Icon { get; } = StreamGeometry.Parse(tab.Icon);

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool HasWarning { get; set; }

    [RelayCommand]
    private void Choose() => choose(Tab);
}
