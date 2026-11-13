using Arcadia.App.Input;

namespace Arcadia.App.Settings;

/// <summary>
/// A tab of "Datenquellen" (#538): "Artwork" (<see cref="ArtworkSourcesTab"/>) or "Metadaten" (<see cref="MetadataSourcesTab"/>),
/// each with its own rows and its own order of sources to move.
/// </summary>
public interface IDataSourcesTab
{
    string Name { get; }

    /// <summary>A line drawing in a 24 × 24 box, drawn in the tab's text colour.</summary>
    string Icon { get; }

    /// <summary>The tab's rows as they stand after the last <see cref="Refresh"/>.</summary>
    IReadOnlyList<SettingsRowViewModel> Rows { get; }

    /// <summary>A key or access the service turned down, or a file that couldn't be read: a warning beside the tab's name.</summary>
    bool HasWarning { get; }

    /// <summary>A source of the tab's order is lifted; it takes ▲ ▼, L3 and B until it is put down.</summary>
    bool IsReordering { get; }

    /// <summary>Something the rows show changed; UI thread.</summary>
    void Refresh();

    /// <summary>L3 and, while a source is lifted, ▲ ▼ and B; true if the tab took the press.</summary>
    bool Handle(UiAction action, SettingsRowViewModel? focused);

    /// <summary>The row is a source of the tab's order, which the mouse can drag.</summary>
    bool CanDrag(SettingsRowViewModel row);

    void BeginDrag(SettingsRowViewModel row);

    void DragTo(int rowsAbove);

    void Drop();

    /// <summary>Another tab or category, or the page closes: a lifted source goes back to where it was.</summary>
    void PutBack();
}
