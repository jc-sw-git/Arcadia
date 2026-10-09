using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>A page number in the pagination bar, or an ellipsis between distant pages.</summary>
public sealed class PageButtonViewModel(int pageIndex, bool isCurrent, Action<int> goTo) : ViewModelBase
{
    public bool IsEllipsis => pageIndex < 0;

    public bool IsPage => pageIndex >= 0;

    public string Label => IsEllipsis ? "…" : (pageIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool IsCurrent => isCurrent;

    public IRelayCommand GoCommand { get; } = new RelayCommand(() => goTo(pageIndex));

    /// <summary>Page numbers to show: all if few, otherwise first, last and the neighbours of the current page.</summary>
    public static IEnumerable<int> Layout(int pageIndex, int pageCount)
    {
        if (pageCount <= 9)
        {
            return Enumerable.Range(0, pageCount);
        }

        var pages = new List<int> { 0 };
        if (pageIndex > 2)
        {
            pages.Add(-1);
        }

        for (var i = Math.Max(1, pageIndex - 1); i <= Math.Min(pageCount - 2, pageIndex + 1); i++)
        {
            pages.Add(i);
        }

        if (pageIndex < pageCount - 3)
        {
            pages.Add(-1);
        }

        pages.Add(pageCount - 1);
        return pages;
    }
}
