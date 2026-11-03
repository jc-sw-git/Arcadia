namespace Arcadia.App.Settings;

/// <summary>
/// What the parts of the category "Launcher" (the list's order, a page's groups) need from the category that shows them:
/// the launcher whose page is open, the rows shown anew, the focus, a question or the on-screen keyboard over the page.
/// </summary>
internal interface ILauncherCategory
{
    ILauncherSettingsHost Host { get; }

    SettingsChanges Changes { get; }

    TimeProvider Time { get; }

    /// <summary>The launcher whose page is shown, or null on the list.</summary>
    string? PageLauncherId { get; }

    /// <summary>A file or folder dialog is open: a second A must not open another, whichever row it is on.</summary>
    bool IsPicking { get; set; }

    bool IsReordering { get; set; }

    object? Dialog { get; set; }

    TextEditorViewModel? Editor { get; set; }

    /// <summary>States, counts and order are shown anew, on the list or the page.</summary>
    void Refresh();

    /// <summary>A launcher's name, keyword or logo changed: the rows show it.</summary>
    void RefreshLooks();

    void Focus(SettingsRowViewModel row);
}
