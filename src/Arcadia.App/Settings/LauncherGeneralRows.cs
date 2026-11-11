using Arcadia.App.Services;
using Arcadia.Core.Launchers;
using Arcadia.Core.Search;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "Allgemein" on a launcher's page (#137): "Aktiv", its name in the tab and search keyword, written with the
/// on-screen keyboard and checked while typed, and its logo, uploaded or restored.
/// </summary>
internal sealed class LauncherGeneralRows
{
    private const string GeneralGroup = "ALLGEMEIN";

    private readonly ILauncherCategory _category;
    private readonly TextRowViewModel _nameRow;
    private readonly TextRowViewModel _keywordRow;
    private readonly ActionRowViewModel _logoRow;
    private readonly ActionRowViewModel _defaultLogoRow;

    /// <param name="switchPage">◀ ▶ on "Aktiv": the launcher whose page is shown on or off.</param>
    public LauncherGeneralRows(ILauncherCategory category, Action<bool> switchPage)
    {
        _category = category;
        var launchers = category.Host.Launchers;
        ActiveRow = new LauncherActiveRowViewModel(
            () => category.PageLauncherId is { } id && launchers.ActiveIds.Contains(id),
            switchPage,
            () => category.PageLauncherId is { } id && launchers.IsFound(id))
        {
            Group = GeneralGroup,
            Subtitle = AppTexts.LauncherActiveSubtitle,
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
        _nameRow = new TextRowViewModel("launcher-name", AppTexts.LauncherNameTitle, () => PageInfo.DisplayName, Edit)
        {
            Group = GeneralGroup,
            Subtitle = AppTexts.LauncherNameSubtitle,
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
        _keywordRow = new TextRowViewModel("launcher-keyword", AppTexts.LauncherKeywordTitle, () => PageInfo.SearchKeyword, Edit)
        {
            Group = GeneralGroup,
            Prefix = SearchQuery.PrefixMarker.ToString(),
        };
        // Two buttons, not a choice (owner, 2026-10-23): uploading is always offered, going back only while an own logo is used.
        _logoRow = new ActionRowViewModel("launcher-logo", "Logo", () => PickLogoAsync().Forget(AppTexts.ErrorContextPickLogo))
        {
            Group = GeneralGroup,
            ActionText = AppTexts.Upload,
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
        _defaultLogoRow = new ActionRowViewModel("launcher-logo-default", "Standardlogo", RestoreLogo)
        {
            Group = GeneralGroup,
            Indent = 1,
            ActionText = AppTexts.Restore,
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
    }

    /// <summary>"Aktiv": where the focus goes when a page opens.</summary>
    public LauncherActiveRowViewModel ActiveRow { get; }

    private Arcadia.Launchers.Abstractions.LauncherInfo PageInfo => _category.Host.Launchers.Info(_category.PageLauncherId!);

    /// <summary>The rows for the page of <paramref name="launcher"/> as its row shows it, each with the tabs in miniature it changes.</summary>
    public IReadOnlyList<SettingsRowViewModel> Show(Arcadia.Launchers.Abstractions.LauncherInfo launcher, bool isActive, bool isFound, InfoTabBar tabBar)
    {
        var launchers = _category.Host.Launchers;
        var id = launcher.Id;
        var name = launcher.DisplayName;
        var original = launchers.Registered(id)!.Launcher;
        var keyword = $"{SearchQuery.PrefixMarker}{launcher.SearchKeyword}";

        ActiveRow.Refresh();
        ActiveRow.Preview = [tabBar];
        ActiveRow.Info =
        [
            new InfoText(AppTexts.LauncherActiveInfo(name)),
            .. isFound ? Array.Empty<InfoBlock>() : [new InfoNote(isActive
                ? AppTexts.LauncherNotFoundAnymore(name)
                : AppTexts.LauncherNotFoundCannotActivate(name))],
            new InfoFootnote(AppTexts.AppliesImmediately),
        ];

        _nameRow.Refresh();
        _nameRow.Preview = [tabBar];
        _nameRow.Info =
        [
            new InfoText(AppTexts.LauncherNameInfo(LauncherNames.MaxNameLength)),
            new InfoFootnote(AppTexts.DefaultAppliesImmediately(original.DisplayName)),
        ];

        _keywordRow.Refresh();
        _keywordRow.Subtitle = AppTexts.LauncherKeywordSubtitle(keyword);
        var other = launchers.Shown.FirstOrDefault(l => l.Id != id && launchers.ActiveIds.Contains(l.Id));
        _keywordRow.Info =
        [
            new InfoText(AppTexts.LauncherKeywordInfo),
            new InfoExample($"{keyword} hollow", AppTexts.LauncherKeywordSearchesOnlyIn(name)),
            .. other is null ? Array.Empty<InfoBlock>() : [new InfoExample($"{keyword}{SearchQuery.KeywordSeparator}{other.SearchKeyword} hollow", AppTexts.LauncherKeywordSearchesIn(name, other.DisplayName))],
            new InfoText(AppTexts.LauncherKeywordRules(LauncherNames.MaxKeywordLength)),
            new InfoFootnote(AppTexts.DefaultKeywordAppliesImmediately(SearchQuery.PrefixMarker, original.SearchKeyword)),
        ];

        var hasOwnLogo = _category.Changes.Current.PreferencesOf(id).LogoFile is not null;
        var defaultLogo = original.Logo is null ? AppTexts.LauncherLogoInitialTile : AppTexts.LauncherLogoOf(original.DisplayName);
        _logoRow.Subtitle = hasOwnLogo ? AppTexts.LauncherLogoOwnFitted : AppTexts.LauncherLogoDefaultFitted;
        _logoRow.Preview = [tabBar];
        _logoRow.Info =
        [
            new InfoText(AppTexts.LauncherLogoOwnInfo),
            new InfoText(AppTexts.LauncherLogoFitInfo),
            new InfoFootnote($"Standard: {defaultLogo}."),
        ];
        _defaultLogoRow.Subtitle = AppTexts.LauncherLogoRemove;
        _defaultLogoRow.Preview = [tabBar];
        _defaultLogoRow.Info =
        [
            new InfoText(AppTexts.LauncherLogoRemoveInfo(name, defaultLogo)),
        ];

        return [ActiveRow, _nameRow, _keywordRow, _logoRow, .. hasOwnLogo ? [_defaultLogoRow] : Array.Empty<SettingsRowViewModel>()];
    }

    /// <summary>A on "Name im Tab" or "Such-Kürzel": the on-screen keyboard over the text, checked while it is typed.</summary>
    private void Edit(TextRowViewModel row)
    {
        if (_category.PageLauncherId is not { } id || _category.Editor is not null)
        {
            return;
        }

        var host = _category.Host;
        var isName = row == _nameRow;
        TextEditorViewModel? editor = null;
        editor = new TextEditorViewModel(
            row.ValueText,
            // An empty name is the launcher's own again (owner, 2026-10-23), so only a keyword must not be empty.
            text => isName ? (text.Trim().Length == 0 ? null : LauncherNames.NameProblem(text)) : LauncherNames.KeywordProblem(text, id, host.Launchers.Shown),
            text =>
            {
                SaveText(id, isName, text);
                return true;
            },
            () => CloseEditor(row, editor!),
            host.TakeKeyboard,
            host.GiveBackKeyboard,
            // A keyword offers only the keys it may hold (owner, 2026-10-23); a name takes any character.
            isName ? null : LauncherNames.IsKeywordCharacter);
        row.Editor = editor;
        _category.Editor = editor;
    }

    private void CloseEditor(TextRowViewModel row, TextEditorViewModel editor)
    {
        row.Editor = null;
        _category.Editor = null;
        if (editor.Rejection is { } reason)
        {
            _category.Host.Warn(AppTexts.SettingNotApplied(row.Title, reason));
        }

        editor.Dispose();
    }

    /// <summary>The launcher's own name or keyword is kept as none, so it follows the integration if that renames it.</summary>
    private void SaveText(string id, bool isName, string text)
    {
        var original = _category.Host.Launchers.Registered(id)!.Launcher;
        var value = isName ? text.Trim() : text;
        var own = value.Length == 0 || value == (isName ? original.DisplayName : original.SearchKeyword) ? null : value;
        _category.Changes.Apply(settings => settings.WithLauncherPreferences(id, p => isName ? p with { Name = own } : p with { Keyword = own }), isName ? AppTexts.LauncherNameSetting : AppTexts.LauncherKeywordTitle);
        AppLog.Write($"{(isName ? "Name" : "Such-Kürzel")} von {id} geändert");
        LooksChanged();
    }

    /// <summary>"Logo" → "Hochladen …": the file dialog; a picture that passes the checks replaces the logo everywhere.</summary>
    private async Task PickLogoAsync()
    {
        var host = _category.Host;
        if (_category.PageLauncherId is not { } id || host.Dialogs is not { } dialogs || _category.IsPicking)
        {
            return;
        }

        _category.IsPicking = true;
        try
        {
            var name = host.Launchers.Info(id).DisplayName;
            if (await dialogs.PickFileAsync(AppTexts.LauncherPickLogo(name), "Bilder", LogoFile.Extensions) is not { } path)
            {
                return;
            }

            var import = await host.Logos.ImportAsync(id, path);
            if (import.FileName is not { } fileName)
            {
                host.Warn(AppTexts.LauncherLogoNotApplied(import.Problem));
                return;
            }

            var previous = _category.Changes.Current.PreferencesOf(id).LogoFile;
            _category.Changes.Apply(settings => settings.WithLauncherPreferences(id, p => p with { LogoFile = fileName }), "Launcher-Logo");
            if (previous != fileName)
            {
                host.Logos.Delete(previous);
            }

            AppLog.Write($"Eigenes Logo für {id}");
            LooksChanged();
        }
        finally
        {
            _category.IsPicking = false;
        }
    }

    /// <summary>"Standardlogo" → "Wiederherstellen": the launcher's own logo again; the copy of the picture goes.</summary>
    private void RestoreLogo()
    {
        if (_category.PageLauncherId is not { } id || _category.Changes.Current.PreferencesOf(id).LogoFile is not { } fileName)
        {
            return;
        }

        _category.Changes.Apply(settings => settings.WithLauncherPreferences(id, p => p with { LogoFile = null }), "Launcher-Logo");
        _category.Host.Logos.Delete(fileName);
        AppLog.Write($"Standardlogo für {id}");
        LooksChanged();

        // The row pressed is gone; the focus goes back to the logo, not on to the next row.
        _category.Focus(_logoRow);
    }

    private void LooksChanged()
    {
        _category.Host.LauncherLooksChanged();
        _category.RefreshLooks();
    }
}
