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
            Subtitle = "Als Tab zeigen, in der Suche und beim Einlesen",
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
        _nameRow = new TextRowViewModel("launcher-name", "Name im Tab", () => PageInfo.DisplayName, Edit)
        {
            Group = GeneralGroup,
            Subtitle = "So heißt der Tab in der Leiste",
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
        _keywordRow = new TextRowViewModel("launcher-keyword", "Such-Kürzel", () => PageInfo.SearchKeyword, Edit)
        {
            Group = GeneralGroup,
            Prefix = SearchQuery.PrefixMarker.ToString(),
        };
        // Two buttons, not a choice (owner, 2026-10-23): uploading is always offered, going back only while an own logo is used.
        _logoRow = new ActionRowViewModel("launcher-logo", "Logo", () => PickLogoAsync().Forget("Logo wählen"))
        {
            Group = GeneralGroup,
            ActionText = "Hochladen …",
            PreviewTitle = LauncherTexts.OrderPreviewTitle,
            PreviewSubtitle = LauncherTexts.OrderPreviewSubtitle,
        };
        _defaultLogoRow = new ActionRowViewModel("launcher-logo-default", "Standardlogo", RestoreLogo)
        {
            Group = GeneralGroup,
            Indent = 1,
            ActionText = "Wiederherstellen",
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
            new InfoText($"Ein aktiver Launcher hat einen Tab, Arcadia liest seine Spiele ein, findet sie in der Suche und sieht, wenn eins installiert wird oder läuft. Abgeschaltet bleibt {name} aus all dem heraus; seine Einstellungen bleiben."),
            .. isFound ? Array.Empty<InfoBlock>() : [new InfoNote(isActive
                ? $"Arcadia findet {name} auf diesem PC nicht mehr. Ist es deinstalliert, schalte es hier ab, dann verschwindet sein Tab."
                : $"Arcadia findet {name} auf diesem PC nicht, deshalb lässt es sich nicht einschalten.")],
            new InfoFootnote("Wirkt sofort, ohne Speichern."),
        ];

        _nameRow.Refresh();
        _nameRow.Preview = [tabBar];
        _nameRow.Info =
        [
            new InfoText($"So heißt der Launcher im Tab, in der Liste, in der Suche und auf den Spielen. Höchstens {LauncherNames.MaxNameLength} Zeichen."),
            new InfoFootnote($"Standard: {original.DisplayName}. Wirkt sofort, ohne Speichern."),
        ];

        _keywordRow.Refresh();
        _keywordRow.Subtitle = $"Sucht mit {keyword} nur in diesem Launcher";
        var other = launchers.Shown.FirstOrDefault(l => l.Id != id && launchers.ActiveIds.Contains(l.Id));
        _keywordRow.Info =
        [
            new InfoText("Steht das Kürzel vor dem Suchbegriff, sucht die Suche nur in diesem Launcher. Mehrere Kürzel verbindest du mit „-“."),
            new InfoExample($"{keyword} hollow", $"sucht nur in {name}"),
            .. other is null ? Array.Empty<InfoBlock>() : [new InfoExample($"{keyword}{SearchQuery.KeywordSeparator}{other.SearchKeyword} hollow", $"sucht in {name} und {other.DisplayName}")],
            new InfoText($"Nur Kleinbuchstaben und Ziffern, höchstens {LauncherNames.MaxKeywordLength} Zeichen. Kein „-“, denn das trennt mehrere Launcher. Jedes Kürzel gibt es nur einmal."),
            new InfoFootnote($"Standard: {SearchQuery.PrefixMarker}{original.SearchKeyword}. Wirkt sofort, ohne Speichern."),
        ];

        var hasOwnLogo = _category.Changes.Current.PreferencesOf(id).LogoFile is not null;
        var defaultLogo = original.Logo is null ? "der Anfangsbuchstabe auf einer farbigen Kachel" : $"das Logo von {original.DisplayName}";
        _logoRow.Subtitle = hasOwnLogo ? "Eigenes Bild · im Tab auf 24 × 24 px eingepasst" : "Standardlogo · im Tab auf 24 × 24 px eingepasst";
        _logoRow.Preview = [tabBar];
        _logoRow.Info =
        [
            new InfoText("Ein eigenes Bild ersetzt das Logo überall: im Tab, in der Liste, in der Suche und auf den Spielen. PNG, SVG, ICO oder WebP bis 2 MB. Arcadia legt eine Kopie ab; die Datei selbst kannst du danach verschieben oder löschen."),
            new InfoText("Im Tab wird jedes Logo in ein Feld von 24 × 24 px eingepasst. Das Seitenverhältnis bleibt, freie Fläche bleibt leer."),
            new InfoFootnote($"Standard: {defaultLogo}."),
        ];
        _defaultLogoRow.Subtitle = "Eigenes Bild entfernen";
        _defaultLogoRow.Preview = [tabBar];
        _defaultLogoRow.Info =
        [
            new InfoText($"Entfernt das eigene Bild; {name} zeigt wieder {defaultLogo}. Arcadia löscht seine Kopie des Bilds."),
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
            host.CreateKeyboard,
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
            _category.Host.Warn($"{row.Title} nicht übernommen: {reason}");
        }

        editor.Dispose();
    }

    /// <summary>The launcher's own name or keyword is kept as none, so it follows the integration if that renames it.</summary>
    private void SaveText(string id, bool isName, string text)
    {
        var original = _category.Host.Launchers.Registered(id)!.Launcher;
        var value = isName ? text.Trim() : text;
        var own = value.Length == 0 || value == (isName ? original.DisplayName : original.SearchKeyword) ? null : value;
        _category.Changes.Apply(settings => settings.WithLauncherPreferences(id, p => isName ? p with { Name = own } : p with { Keyword = own }), isName ? "Launcher-Name" : "Such-Kürzel");
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
            if (await dialogs.PickFileAsync($"Logo für {name} wählen", "Bilder", LogoFile.Extensions) is not { } path)
            {
                return;
            }

            var import = await host.Logos.ImportAsync(id, path);
            if (import.FileName is not { } fileName)
            {
                host.Warn($"Logo nicht übernommen: {import.Problem}");
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
