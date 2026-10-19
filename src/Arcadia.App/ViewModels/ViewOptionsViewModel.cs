using Arcadia.App.Input;
using Arcadia.Core.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.ViewModels;

/// <summary>One choice in the "Ansicht" dialog. Selecting it applies immediately.</summary>
public partial class OptionViewModel(string label, bool isSelected, bool isEnabled, Action apply) : ViewModelBase
{
    public string Label => label;

    public bool IsSelected => isSelected;

    public bool IsEnabled => isEnabled;

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    public IRelayCommand ApplyCommand { get; } = new RelayCommand(apply, () => isEnabled);
}

public sealed record SortRowViewModel(string Label, IReadOnlyList<OptionViewModel> Options);

/// <summary>
/// The "Ansicht" dialog: sort order and games per page in the library; launcher filter and sort
/// order in the search (L3), which adds relevance as its own first order. Controller navigation
/// works on a grid: rows of launchers, one row per sort field (two directions each) and one row
/// of page sizes.
/// </summary>
public sealed class ViewOptionsViewModel : ViewModelBase
{
    /// <summary>Launcher entries per row in the search's filter.</summary>
    public const int LauncherColumns = 4;

    private readonly List<IReadOnlyList<OptionViewModel>> _grid;
    private readonly UiAction _closeAction;
    private int _row;
    private int _column;

    private ViewOptionsViewModel(
        string title,
        string subtitle,
        IReadOnlyList<IReadOnlyList<OptionViewModel>> launcherRows,
        IReadOnlyList<SortRowViewModel> sortRows,
        IReadOnlyList<OptionViewModel> pageSizeOptions,
        string pageSizeNote,
        UiAction closeAction)
    {
        Title = title;
        Subtitle = subtitle;
        LauncherRows = launcherRows;
        SortRows = sortRows;
        PageSizeOptions = pageSizeOptions;
        PageSizeNote = pageSizeNote;
        _closeAction = closeAction;
        _grid = [.. launcherRows, .. sortRows.Select(r => r.Options)];
        if (pageSizeOptions.Count > 0)
        {
            _grid.Add(pageSizeOptions);
        }

        if (launcherRows.Count > 0)
        {
            SetFocus(0, 0);
        }
        else
        {
            FocusSelectedSort();
        }
    }

    /// <param name="note">Replaces the note on page sizes, e.g. on the detail page, which always shows one game.</param>
    public static ViewOptionsViewModel ForLibrary(string tabName, int gameCount, SortOrder sort, int preferredPageSize, Action<SortOrder> applySort, Action<int> applyPageSize, string? note = null)
    {
        var effective = PageSizes.Effective(preferredPageSize, gameCount);
        var pageSizes = PageSizes.Options
            .Select(size => new OptionViewModel(
                size.ToString(System.Globalization.CultureInfo.InvariantCulture),
                size == effective,
                PageSizes.IsAllowed(size, gameCount),
                () => applyPageSize(size)))
            .ToList();

        var blocked = PageSizes.Options.Where(size => !PageSizes.IsAllowed(size, gameCount)).ToList();
        var largest = PageSizes.Allowed(gameCount)[^1];
        var pageSizeNote = note ?? (blocked.Count == 0
            ? $"{tabName} hat {gameCount} Spiele – alle Seitengrößen wählbar."
            : $"{tabName} hat {gameCount} Spiele – {largest} pro Seite zeigt bereits alle, {JoinGerman(blocked)} {(blocked.Count == 1 ? "ist" : "sind")} daher nicht wählbar.");

        return new ViewOptionsViewModel(
            "Ansicht",
            "Sortierung und Einträge pro Seite, gültig für alle Launcher",
            [],
            SortRowsFor(sort, order => applySort(order!.Value), withRelevance: false),
            pageSizes,
            pageSizeNote,
            UiAction.ViewOptions);
    }

    /// <summary>The search's filter (L3): the launchers it covers, several at once, and the order of its hits.</summary>
    /// <param name="launchers">"Alle Launcher" first, then one per launcher, each with whether it is covered.</param>
    public static ViewOptionsViewModel ForSearch(IReadOnlyList<(string Label, bool IsOn, Action Toggle)> launchers, SortOrder? sort, Action<SortOrder?> applySort)
    {
        var options = launchers.Select(l => new OptionViewModel(l.Label, l.IsOn, true, l.Toggle)).ToList();
        var rows = options.Chunk(LauncherColumns).Select(row => (IReadOnlyList<OptionViewModel>)row).ToList();
        return new ViewOptionsViewModel(
            "Filter und Sortierung",
            "Gilt nur für diese Suche",
            rows,
            SortRowsFor(sort, applySort, withRelevance: true),
            [],
            string.Empty,
            UiAction.Filter);
    }

    public string Title { get; }

    public string Subtitle { get; }

    /// <summary>The search's launchers, <see cref="LauncherColumns"/> per row; empty in the library.</summary>
    public IReadOnlyList<IReadOnlyList<OptionViewModel>> LauncherRows { get; }

    public bool HasLaunchers => LauncherRows.Count > 0;

    public bool HasPageSizes => PageSizeOptions.Count > 0;

    public IReadOnlyList<SortRowViewModel> SortRows { get; }

    public IReadOnlyList<OptionViewModel> PageSizeOptions { get; }

    public string PageSizeNote { get; }

    /// <summary>The focused cell, so a rebuilt dialog (after applying a choice) keeps the controller position.</summary>
    public (int Row, int Column) FocusPosition
    {
        get => (_row, _column);
        set
        {
            var row = Math.Clamp(value.Row, 0, _grid.Count - 1);
            var column = NearestEnabled(_grid[row], Math.Clamp(value.Column, 0, _grid[row].Count - 1));
            SetFocus(row, Math.Max(0, column));
        }
    }

    /// <returns>False if the action closes the dialog.</returns>
    public bool Handle(UiAction action)
    {
        switch (action)
        {
            case UiAction.Up:
                MoveRow(-1);
                return true;
            case UiAction.Down:
                MoveRow(1);
                return true;
            case UiAction.Left:
                MoveColumn(-1);
                return true;
            case UiAction.Right:
                MoveColumn(1);
                return true;
            case UiAction.Accept:
                var option = _grid[_row][_column];
                if (option.ApplyCommand.CanExecute(null))
                {
                    option.ApplyCommand.Execute(null);
                }

                return true;
            case UiAction.Back:
                return false;
            case var close when close == _closeAction:
                return false;
            default:
                return true;
        }
    }

    /// <summary>The same rows in the library and the search, so both offer the same orders; the search adds relevance.</summary>
    private static IReadOnlyList<SortRowViewModel> SortRowsFor(SortOrder? sort, Action<SortOrder?> apply, bool withRelevance)
    {
        OptionViewModel SortOption(SortField field, SortDirection direction, string label) =>
            new(label, sort == new SortOrder(field, direction), true, () => apply(new SortOrder(field, direction)));

        List<SortRowViewModel> rows =
        [
            new("Name", [SortOption(SortField.Name, SortDirection.Ascending, "A → Z"), SortOption(SortField.Name, SortDirection.Descending, "Z → A")]),
            new("Installationsdatum", [SortOption(SortField.InstalledAt, SortDirection.Descending, "Neueste zuerst"), SortOption(SortField.InstalledAt, SortDirection.Ascending, "Älteste zuerst")]),
            new("Zuletzt gespielt", [SortOption(SortField.LastPlayed, SortDirection.Descending, "Neueste zuerst"), SortOption(SortField.LastPlayed, SortDirection.Ascending, "Älteste zuerst")]),
        ];

        if (withRelevance)
        {
            rows.Insert(0, new("Relevanz", [new OptionViewModel("Beste Treffer zuerst", sort is null, true, () => apply(null))]));
        }

        return rows;
    }

    private void FocusSelectedSort()
    {
        var first = LauncherRows.Count;
        for (var r = 0; r < SortRows.Count; r++)
        {
            for (var c = 0; c < SortRows[r].Options.Count; c++)
            {
                if (SortRows[r].Options[c].IsSelected)
                {
                    SetFocus(first + r, c);
                    return;
                }
            }
        }

        SetFocus(first, 0);
    }

    private void MoveRow(int delta)
    {
        var row = Math.Clamp(_row + delta, 0, _grid.Count - 1);
        var column = Math.Min(_column, _grid[row].Count - 1);
        column = NearestEnabled(_grid[row], column);
        if (column >= 0)
        {
            SetFocus(row, column);
        }
    }

    private void MoveColumn(int delta)
    {
        var options = _grid[_row];
        for (var c = _column + delta; c >= 0 && c < options.Count; c += delta)
        {
            if (options[c].IsEnabled)
            {
                SetFocus(_row, c);
                return;
            }
        }
    }

    private static int NearestEnabled(IReadOnlyList<OptionViewModel> options, int column)
    {
        for (var c = column; c >= 0; c--)
        {
            if (options[c].IsEnabled)
            {
                return c;
            }
        }

        return -1;
    }

    private void SetFocus(int row, int column)
    {
        _grid[_row][_column].IsFocused = false;
        _row = row;
        _column = column;
        _grid[_row][_column].IsFocused = true;
    }

    private static string JoinGerman(IReadOnlyList<int> values) => values.Count switch
    {
        0 => string.Empty,
        1 => values[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => string.Join(", ", values.Take(values.Count - 1)) + " und " + values[^1],
    };
}
