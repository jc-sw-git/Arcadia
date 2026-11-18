using Arcadia.App.ViewModels;
using Arcadia.Core.Library;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arcadia.App.Filter;

/// <summary>
/// A category row on the left of the filter modal (#44, design T44Bausteine "Kategorie-Zeile"): name, icon and a one-line
/// summary of its choice, in the accent colour while it filters. <see cref="IsCurrent"/> is the one whose values show.
/// </summary>
public sealed partial class FilterCategoryViewModel(FilterCategory category, string label, string iconKey, Action<FilterCategoryViewModel> pick) : ViewModelBase
{
    /// <summary>The summary when the chosen values don't fit their line (#44: "Mehrere", never cut).</summary>
    public static string SeveralText => AppTexts.FilterSeveral;

    public FilterCategory Category => category;

    public string Label => label;

    public string IconKey => iconKey;

    /// <summary>The choice in words, e.g. "Nur installierte" or "Werkzeuge, Software".</summary>
    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    /// <summary>A multiple choice falls back to "Mehrere" where it doesn't fit; a single one always fits.</summary>
    [ObservableProperty]
    public partial string SummaryFallback { get; set; } = string.Empty;

    /// <summary>Something is chosen here: icon and summary in the accent colour.</summary>
    [ObservableProperty]
    public partial bool IsChosen { get; set; }

    [ObservableProperty]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [RelayCommand]
    private void Pick() => pick(this);
}

/// <summary>
/// One value on the right of the filter modal (#44, #466): a radio ("Installiert") or a checkbox ("Typ" with its type's
/// icon, a genre, tag, developer or publisher), with the hits it would bring given the other categories' choice. A value
/// with none is greyed out and still choosable.
/// </summary>
public sealed partial class FilterValueViewModel(string label, string? iconKey, Action<FilterValueViewModel> pick) : ViewModelBase
{
    public string Label => label;

    /// <summary>What a choice holds: a genre's key, a name in its most frequent spelling; empty for a fixed option.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>An own tag (#495): it hits only the entries the player gave it.</summary>
    public bool IsOwn { get; init; }

    /// <summary>"Alle …": clears the category.</summary>
    public bool IsAll { get; init; }

    /// <summary>How many of the tab's entries have the value, for "Häufigste zuerst".</summary>
    public int Entries { get; init; }

    /// <summary>The type's icon (#141); null for none.</summary>
    public string? IconKey => iconKey;

    public bool HasIcon => iconKey is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoHits))]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    public bool HasNoHits => Count == 0;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [RelayCommand]
    private void Pick() => pick(this);
}
