using System.Globalization;
using Arcadia.App.Input;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Launchers;
using Arcadia.Core.Library;
using Arcadia.Core.Navigation;
using Arcadia.Core.Search;
using Arcadia.Core.Settings;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Settings;

/// <summary>
/// The category "Launcher" (#136): which launchers are active, i.e. read, watched and shown as tabs, and in which order.
/// Above "Aktiv · erscheinen als Tabs" in tab order, below "Nicht aktiv", the installed ones first. ◀ ▶ switch a launcher
/// on (it goes to the end of the active ones) or off (to the top of the inactive ones); Y lifts it, ▲ ▼ move it, Y puts it
/// down and B cancels; put down among the inactive ones it is off. X, or the row "Alle neu einlesen" below the launchers
/// (for the mouse), reads every launcher again. A or a click open a
/// launcher's page (#137): "Aktiv", its name, keyword and logo, its folder if Arcadia can't find it (#138), "Neu einlesen";
/// LT/RT turn to the launcher before or after it in the list, B goes back to the list. Applies at once.
/// </summary>
public sealed class LauncherSettingsViewModel : SettingsCategoryViewModel
{
    private const string ActiveGroup = "AKTIV · ERSCHEINEN ALS TABS";
    private const string InactiveGroup = "NICHT AKTIV";
    private const string DropGroup = "NICHT AKTIV · HIERHER VERSCHIEBEN SCHALTET IHN AB";
    private const string GeneralGroup = "ALLGEMEIN";
    private const string LibraryGroup = "BIBLIOTHEK";
    private const string ControllerGroup = "CONTROLLER";
    private const string AccountGroup = "KONTO";
    private const string ListYAction = "Verschieben";

    /// <summary>The box with the tabs in miniature beside the list and the page (owner, 2026-10-23).</summary>
    internal const string OrderPreviewTitle = "LAUNCHER-REIHENFOLGE";

    internal const string OrderPreviewSubtitle = "Eine Vorschau der Reihenfolge, in der die Launcher aufgeführt werden";
    private const string ListXAction = "Alle neu einlesen";
    private const string PanOffSubtitle = "Aus – erst mit dem Controller-Wächter (Pan)";

    // As Pan's own row says it, so the rows don't send the user to a switch that can't help (QE review PR #319).
    private const string PanUnsupportedSubtitle = "Auf diesem System noch nicht verfügbar";

    // The question before the reset names this many games, then how many more.
    private const int MaxTitlesInQuestion = 5;

    private readonly ILauncherSettingsHost _host;
    private readonly SettingsChanges _changes;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, LauncherRowViewModel> _rows = new(StringComparer.Ordinal);

    // Switched off while the page is open, the latest first: each goes to the top of the inactive ones (owner, 2026-10-22).
    private readonly List<string> _switchedOff = [];

    private readonly LauncherPageHeadViewModel _head;
    private readonly LauncherActiveRowViewModel _activeRow;
    private readonly TextRowViewModel _nameRow;
    private readonly TextRowViewModel _keywordRow;
    private readonly ActionRowViewModel _logoRow;
    private readonly ActionRowViewModel _defaultLogoRow;
    private readonly ActionRowViewModel _folderRow;
    private readonly ActionRowViewModel _rescanRow;
    private readonly ActionRowViewModel _rescanAllRow;
    private readonly LauncherControllerModeRowViewModel _controllerModeRow;
    private readonly ActionRowViewModel _resetModesRow;
    private readonly ActionRowViewModel _accountRow;
    private readonly SteamDesktopLayoutRowViewModel _steamDesktopLayoutRow;

    // The account picture shown last, by its file (AvatarOf).
    private string? _avatarPath;
    private Avalonia.Media.Imaging.Bitmap? _avatar;
    private readonly List<(ILauncherAccount Account, EventHandler Handler)> _accounts = [];

    // A launcher's switches for software, tools and soundtracks (#141), made when its page first shows them.
    private readonly Dictionary<string, SwitchRowViewModel> _optionRows = new(StringComparer.Ordinal);

    private LauncherRowViewModel? _lifted;

    // The library's games with a mode of their own, as the reset row lists them, for its question (#178).
    private IReadOnlyList<string> _ownModeTitles = [];

    // The launcher whose page is shown (#137); null on the list.
    private string? _page;

    // A file or folder dialog is open: a second A must not open another.
    private bool _isPicking;

    // Where the lifted launcher stands: a place among the other active ones, or one more for the top of the inactive ones.
    private int _slot;

    /// <param name="focus">The launcher to show first, e.g. from its empty tab's "Launcher-Einstellungen".</param>
    public LauncherSettingsViewModel(ILauncherSettingsHost host, SettingsChanges changes, TimeProvider time, string? focus = null)
        : base("Launcher", "M4.5 3h4A1.5 1.5 0 0 1 10 4.5v4A1.5 1.5 0 0 1 8.5 10h-4A1.5 1.5 0 0 1 3 8.5v-4A1.5 1.5 0 0 1 4.5 3z M15.5 3h4A1.5 1.5 0 0 1 21 4.5v4a1.5 1.5 0 0 1-1.5 1.5h-4A1.5 1.5 0 0 1 14 8.5v-4A1.5 1.5 0 0 1 15.5 3z M4.5 14h4a1.5 1.5 0 0 1 1.5 1.5v4A1.5 1.5 0 0 1 8.5 21h-4A1.5 1.5 0 0 1 3 19.5v-4A1.5 1.5 0 0 1 4.5 14z M15.5 14h4a1.5 1.5 0 0 1 1.5 1.5v4a1.5 1.5 0 0 1-1.5 1.5h-4a1.5 1.5 0 0 1-1.5-1.5v-4a1.5 1.5 0 0 1 1.5-1.5z")
    {
        _host = host;
        _changes = changes;
        _time = time;
        YActionText = ListYAction;
        XActionText = ListXAction;
        FocusMoved += (_, _) => ShowYAction();
        foreach (var integration in host.Launchers.All)
        {
            var launcher = host.Launchers.Info(integration.Launcher.Id);
            _rows[launcher.Id] = new LauncherRowViewModel(launcher, host.Logos.BadgeOf(launcher), SwitchTo, OpenPage);
        }

        _head = new LauncherPageHeadViewModel(Turn, () => Back());
        _activeRow = new LauncherActiveRowViewModel(
            () => _page is { } id && _host.Launchers.ActiveIds.Contains(id),
            on => SwitchTo(_rows[_page!], on),
            () => _page is { } id && _host.Launchers.IsFound(id))
        {
            Group = GeneralGroup,
            Subtitle = "Als Tab zeigen, in der Suche und beim Einlesen",
            PreviewTitle = OrderPreviewTitle,
            PreviewSubtitle = OrderPreviewSubtitle,
        };
        _nameRow = new TextRowViewModel("launcher-name", "Name im Tab", () => PageInfo.DisplayName, Edit)
        {
            Group = GeneralGroup,
            Subtitle = "So heißt der Tab in der Leiste",
            PreviewTitle = OrderPreviewTitle,
            PreviewSubtitle = OrderPreviewSubtitle,
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
            PreviewTitle = OrderPreviewTitle,
            PreviewSubtitle = OrderPreviewSubtitle,
        };
        _defaultLogoRow = new ActionRowViewModel("launcher-logo-default", "Standardlogo", RestoreLogo)
        {
            Group = GeneralGroup,
            Indent = 1,
            ActionText = "Wiederherstellen",
            PreviewTitle = OrderPreviewTitle,
            PreviewSubtitle = OrderPreviewSubtitle,
        };
        _folderRow = new ActionRowViewModel("launcher-folder", "Ordner", () => PickFolderAsync().Forget("Ordner wählen"))
        {
            Group = LibraryGroup,
            ActionText = "Ordner wählen …",
        };
        _rescanRow = new ActionRowViewModel("launcher-rescan", "Neu einlesen", () => _host.RescanLauncher(_page!))
        {
            Group = LibraryGroup,
            ActionText = "Jetzt einlesen",
        };
        // X does the same; the row is there so that the mouse alone can do it too.
        _rescanAllRow = new ActionRowViewModel("launchers-rescan-all", "Alle neu einlesen", RescanAll)
        {
            Group = LibraryGroup,
            ActionText = "Jetzt einlesen",
            ShortcutPad = "X",
            ShortcutKeys = "F",
            Info =
            [
                new InfoText("Liest die Spiele aller aktiven Launcher sofort neu ein, etwa nach einer Installation, die Arcadia nicht mitbekommen hat. Sonst liest Arcadia neu ein, wenn es wieder nach vorne kommt."),
            ],
        };

        _controllerModeRow = new LauncherControllerModeRowViewModel(
            () => _changes.Current.LauncherUsesArcadiaPad(_page!, _host.Launchers.GamesUseSteamInput),
            SetControllerMode,
            () => _host.IsPanOn)
        {
            Group = ControllerGroup,
        };
        _resetModesRow = new ActionRowViewModel("launcher-controller-reset", "Alle Spiele auf die Vorauswahl setzen", AskResetControllerModes)
        {
            Group = ControllerGroup,
            Indent = 1,
            ActionText = "Zurücksetzen",
        };

        // Pan is switched in the category "Controller": coming back here, the Steam Controller rows follow it.
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsSelected) && IsSelected)
            {
                Refresh();
            }
        };

        _accountRow = new ActionRowViewModel("launcher-account", string.Empty, ActivateAccount) { Group = AccountGroup };
        _steamDesktopLayoutRow = new SteamDesktopLayoutRowViewModel(changes);
        foreach (var integration in host.Launchers.All)
        {
            if (integration.Account is { } account)
            {
                var launcherId = integration.Launcher.Id;
                // The account changes off the UI thread (a code renewed, approved, the games fetched).
                EventHandler handler = (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_page == launcherId)
                    {
                        Refresh();
                    }
                });
                account.StateChanged += handler;
                _accounts.Add((account, handler));
            }
        }

        Refresh();
        if (focus is not null && _rows.TryGetValue(focus, out var row))
        {
            Focus(row);
        }
    }

    /// <summary>The launcher whose page is shown, or null on the list.</summary>
    public string? PageLauncherId => _page;

    /// <summary>The library was read again or its tabs changed: states, counts and order are shown anew.</summary>
    public void Refresh()
    {
        var (active, inactive) = Arrangement();
        var tabs = active.Select(id => _rows[id]).ToList();
        foreach (var id in active.Concat(inactive))
        {
            ShowState(_rows[id], tabs);
        }

        // A launcher without games is information, which the category's tab doesn't show (owner, 2026-10-24).
        Marker = _host.Launchers.ActiveIds.Any(id => _rows[id].Severity > HealthSeverity.Info) ? HealthSeverity.Warning : null;
        if (_page is { } page)
        {
            ShowPage(page, tabs);
            return;
        }

        foreach (var id in active)
        {
            _rows[id].Group = ActiveGroup;
        }

        foreach (var id in inactive)
        {
            _rows[id].Group = _lifted is null ? InactiveGroup : DropGroup;
        }

        _rescanAllRow.IsEnabled = active.Count > 0;
        _rescanAllRow.Subtitle = active.Count > 0 ? "Liest die Spiele aller aktiven Launcher neu ein" : "Kein Launcher aktiv";
        SetRows([.. active.Select(id => _rows[id]), .. inactive.Select(id => _rows[id]), _rescanAllRow]);
    }

    /// <summary>A launcher's name, keyword or logo changed elsewhere: the rows show it.</summary>
    public void RefreshLooks()
    {
        foreach (var (id, row) in _rows)
        {
            var launcher = _host.Launchers.Info(id);
            row.ShowLooks(launcher, _host.Logos.BadgeOf(launcher));
        }

        Refresh();
    }

    public override bool Handle(UiAction action)
    {
        if (_page is not null)
        {
            switch (action)
            {
                case UiAction.PreviousPage:
                    Turn(-1);
                    return true;
                case UiAction.NextPage:
                    Turn(1);
                    return true;
                default:
                    // Y and X have nothing to do on a launcher's page.
                    return action is UiAction.ViewOptions or UiAction.Search;
            }
        }

        if (_lifted is not null)
        {
            switch (action)
            {
                case UiAction.Up:
                    MoveLifted(-1);
                    break;
                case UiAction.Down:
                    MoveLifted(1);
                    break;
                case UiAction.ViewOptions:
                    Drop();
                    break;
                case UiAction.Back:
                    Leave();
                    break;
            }

            // Anything else waits until the launcher is put down.
            return true;
        }

        switch (action)
        {
            case UiAction.ViewOptions:
                if (FocusedRow is LauncherRowViewModel row)
                {
                    Lift(row);
                }

                return true;
            case UiAction.Search:
                RescanAll();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Y lifts only a launcher, so the footer names it only there, not on "Alle neu einlesen".</summary>
    private void ShowYAction()
    {
        if (_page is null)
        {
            YActionText = FocusedRow is LauncherRowViewModel ? ListYAction : null;
        }
    }

    private void RescanAll()
    {
        // A click on the row while a launcher is lifted waits until it is put down, as X does.
        if (_lifted is null)
        {
            _host.RescanLaunchers();
        }
    }

    /// <summary>B on a launcher's page: back to the list, on that launcher.</summary>
    public override bool Back()
    {
        if (_page is not { } id)
        {
            return false;
        }

        // B drops a sign-in under way and stays on the page (owner, 2026-10-26).
        if (PageAccount is { State.IsSigningIn: true } account)
        {
            account.CancelSignIn();
            return true;
        }

        ClosePage();
        Focus(_rows[id]);
        return true;
    }

    /// <summary>The tab "Launcher" is chosen again: the list, on the launcher whose page was open (owner, 2026-10-30).</summary>
    public override void ShowTopLevel()
    {
        if (_page is { } id)
        {
            ClosePage();
            Focus(_rows[id]);
        }
    }

    /// <summary>B while a launcher is lifted, or another category: it goes back to where it was; a text being written is dropped.</summary>
    public override void Leave()
    {
        Editor?.Drop();
        PageAccount?.CancelSignIn();
        if (_lifted is { } lifted)
        {
            lifted.IsLifted = false;
            _lifted = null;
            IsReordering = false;
            Refresh();
        }
    }

    /// <summary>The mouse starts dragging <paramref name="row"/>: lifted as with Y.</summary>
    public void BeginDrag(LauncherRowViewModel row)
    {
        if (_lifted is null)
        {
            Lift(row);
        }
    }

    /// <summary>The dragged launcher is over the place below <paramref name="rowsAbove"/> of the other rows.</summary>
    public void DragTo(int rowsAbove)
    {
        if (_lifted is not null)
        {
            var activeOthers = Arrangement().Active.Count(id => id != _lifted.Launcher.Id);
            PlaceLifted(Math.Min(rowsAbove, activeOthers + 1));
        }
    }

    /// <summary>Y, or the mouse let go: the lifted launcher stays where it stands; among the inactive ones it is off.</summary>
    public void Drop()
    {
        if (_lifted is not { } lifted)
        {
            return;
        }

        var id = lifted.Launcher.Id;
        var active = _host.Launchers.ActiveIds;
        var others = active.Where(other => other != id).ToList();
        var switchedOff = _slot > others.Count;
        var chosen = LauncherSelection.Placed(others, id, switchedOff ? null : _slot);
        lifted.IsLifted = false;
        _lifted = null;
        IsReordering = false;
        if (chosen.SequenceEqual(active))
        {
            Refresh();
            return;
        }

        Choose(chosen, switchedOff && active.Contains(id) ? id : null);
    }

    /// <summary>A or a click on a launcher in the list: its page, the focus on its first row.</summary>
    private void OpenPage(LauncherRowViewModel row)
    {
        if (IsReordering)
        {
            return;
        }

        Focus(row);
        _page = row.Launcher.Id;
        YActionText = null;
        XActionText = null;
        Head = _head;
        Refresh();
        Focus(_activeRow);
    }

    private void ClosePage()
    {
        Editor?.Drop();
        PageAccount?.CancelSignIn();
        _page = null;
        Head = null;
        XActionText = ListXAction;
        Refresh();
        ShowYAction();
    }

    /// <summary>LT/RT or a click on a neighbour: the page of the launcher before or after this one in the list; the focus stays on its row.</summary>
    private void Turn(int delta)
    {
        if (_page is not { } id || Editor is not null)
        {
            return;
        }

        var order = ListOrder();
        var next = order[ListFocus.Cycle(order.ToList().IndexOf(id), delta, order.Count)];
        if (next != id)
        {
            PageAccount?.CancelSignIn();
            _page = next;
            Refresh();
        }
    }

    /// <summary>The launchers as the list shows them: the active ones in tab order, then the inactive ones.</summary>
    private IReadOnlyList<string> ListOrder()
    {
        var (active, inactive) = Arrangement();
        return [.. active, .. inactive];
    }

    private Arcadia.Launchers.Abstractions.LauncherInfo PageInfo => _host.Launchers.Info(_page!);

    private LauncherPreferences PagePreferences => _changes.Current.PreferencesOf(_page!);

    private void ShowPage(string id, IReadOnlyList<LauncherRowViewModel> tabs)
    {
        var launchers = _host.Launchers;
        var row = _rows[id];
        var launcher = row.Launcher;
        var name = launcher.DisplayName;
        var original = launchers.Registered(id)!.Launcher;
        var isActive = tabs.Contains(row);
        var isFound = launchers.IsFound(id);
        var preferences = PagePreferences;
        var folderChoice = launchers.Registered(id)?.FolderChoice;

        _head.Badge = row.Badge;
        _head.Name = name;
        _head.IsActive = isActive;
        _head.StateText = isActive ? $"Aktiv · Tab {tabs.ToList().IndexOf(row) + 1}" : isFound ? "Nicht aktiv" : "Nicht installiert";
        var order = ListOrder();
        var place = order.ToList().IndexOf(id);
        LauncherNeighbour? Neighbour(int delta) => order.Count < 2 ? null
            : _rows[order[ListFocus.Cycle(place, delta, order.Count)]] is var other ? new LauncherNeighbour(other.Launcher.DisplayName, other.Badge) : null;
        _head.Previous = Neighbour(-1);
        _head.Next = Neighbour(1);

        var tabBar = new InfoTabBar([.. tabs.Select(tab => new InfoTab(tab.Launcher.DisplayName, tab.Badge, tab == row))]);
        var keyword = $"{SearchQuery.PrefixMarker}{launcher.SearchKeyword}";

        _activeRow.Refresh();
        _activeRow.Preview = [tabBar];
        _activeRow.Info =
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

        var hasOwnLogo = preferences.LogoFile is not null;
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

        // Only a fallback: shown while Arcadia can't find the launcher by itself, or a folder was chosen (owner, 2026-10-23).
        var showsFolder = folderChoice is not null && (!isFound || preferences.Folder is not null);
        if (folderChoice is not null)
        {
            _folderRow.Subtitle = preferences.Folder ?? $"Arcadia findet {name} nicht von selbst";
            _folderRow.Severity = isFound ? null : HealthSeverity.Warning;
            _folderRow.Info =
            [
                new InfoText($"Arcadia findet {name} sonst über Windows. Klappt das nicht, etwa bei einer Installation ohne Eintrag in der Registry, wähle hier den Ordner, in dem {name} liegt."),
                new InfoText($"Arcadia prüft gleich beim Wählen, ob dort {folderChoice.Expected} liegen, und nimmt den Ordner nur dann."),
                new InfoFacts("ORT",
                [
                    new InfoFact("Gewählt", preferences.Folder ?? "–", IsMuted: preferences.Folder is null),
                    new InfoFact("Gefunden unter", launchers.LocationOf(id) ?? "Nicht gefunden", IsMuted: !isFound),
                ]),
            ];
        }

        var library = isActive ? _host.LibraryOf(id) : null;
        var scannedAt = _host.ScannedAt(id);
        var counts = library is { Games.Count: > 0 } && isFound ? TypeCountLine.Of(library.Games) : null;
        _rescanRow.IsEnabled = isActive && isFound;
        _rescanRow.Counts = counts;
        _rescanRow.Subtitle = (isActive, isFound, library) switch
        {
            (false, _, _) => "Nur für einen aktiven Launcher",
            (true, false, _) => "Nicht gefunden",
            (true, true, null) => "Wird eingelesen …",
            (true, true, { Games.Count: 0 }) when scannedAt is null => GameCount(0),
            (true, true, { Games.Count: 0 }) => $"{GameCount(0)} · zuletzt {Ago(scannedAt)}",
            (true, true, _) when scannedAt is null => null,
            // After the count line (#141).
            (true, true, _) => $"· zuletzt {Ago(scannedAt)}",
        };
        _rescanRow.Info =
        [
            new InfoText($"Liest die Spiele von {name} sofort neu ein, etwa nach einer Installation, die Arcadia nicht mitbekommen hat. Sonst liest Arcadia neu ein, wenn es wieder nach vorne kommt."),
        ];

        var ownModes = ShowControllerRows(id, name, original.GamesUseSteamInput, library);
        var optionRows = launchers.LibraryOptions(id).Select(option => OptionRow(id, option)).ToList();

        // First on the page, as the setting that makes the most of the library; only usable while the launcher is active and
        // found, since without it nothing can be installed. A sign-in stays stored meanwhile (owner, 2026-10-26/27).
        var account = launchers.Registered(id)?.Account;
        if (account is not null)
        {
            ShowAccount(account, name, isActive, isFound);
        }

        SetRows([.. account is null ? Array.Empty<SettingsRowViewModel>() : [_accountRow], _activeRow, _nameRow, _keywordRow, _logoRow, .. hasOwnLogo ? [_defaultLogoRow] : Array.Empty<SettingsRowViewModel>(), .. showsFolder ? [_folderRow] : Array.Empty<SettingsRowViewModel>(), .. optionRows, _rescanRow,
            _controllerModeRow, .. ownModes ? [_resetModesRow] : Array.Empty<SettingsRowViewModel>(), .. DesktopControlRows(id)]);
    }

    /// <summary>
    /// The group "Desktop-Steuerung" on Steam's page (#315). Not the launcher integration's: it is about Steam the program,
    /// which Arcadia's controller support deals with anyway, so it is there whether Steam's launcher is active or not.
    /// </summary>
    private IReadOnlyList<SettingsRowViewModel> DesktopControlRows(string id)
    {
        if (id != KnownLaunchers.Steam.Id)
        {
            return [];
        }

        _steamDesktopLayoutRow.Refresh();
        return [_steamDesktopLayoutRow];
    }

    /// <summary>The account of the launcher whose page is shown, while its group "Konto" is there (#83).</summary>
    private ILauncherAccount? PageAccount =>
        _page is { } id && _host.Launchers.ActiveIds.Contains(id) && _host.Launchers.IsFound(id) ? _host.Launchers.Registered(id)?.Account : null;

    /// <summary>
    /// The account's picture, decoded once per file: the info panel is built anew with every change of the account. A file
    /// that can't be read or isn't an image shows no picture.
    /// </summary>
    private Avalonia.Media.Imaging.Bitmap? AvatarOf(string? path)
    {
        if (path != _avatarPath)
        {
            _avatarPath = path;
            _avatar = null;
            if (path is not null)
            {
                try
                {
                    _avatar = new Avalonia.Media.Imaging.Bitmap(path);
                }
                catch (Exception e)
                {
                    AppLog.Write($"Avatar nicht lesbar ({e.GetType().Name})");
                }
            }
        }

        return _avatar;
    }

    /// <summary>
    /// The row of the launcher's account (#83): signed out "Anmelden"; A opens the launcher's sign-in page over the window,
    /// B there goes back; signed in it shows the account's name and "Abmelden", which asks first. Disabled while the
    /// launcher is inactive or missing.
    /// </summary>
    private void ShowAccount(ILauncherAccount account, string launcherName, bool isActive, bool isFound)
    {
        var state = account.State;
        var row = _accountRow;
        row.Title = account.Title;
        if (!isActive || !isFound)
        {
            row.IsEnabled = false;
            row.ActionText = state.Step == AccountStep.SignedIn ? "Abmelden" : "Anmelden";
            row.Subtitle = (isActive, state.AccountName) switch
            {
                (false, { } accountName) => $"{accountName} · nur bei aktivem {launcherName}",
                (false, null) => $"Nur bei aktivem {launcherName}",
                (true, { } accountName) => $"{accountName} · {launcherName} nicht gefunden",
                (true, null) => $"{launcherName} nicht gefunden",
            };
            row.Severity = null;
            row.Info =
            [
                new InfoText(account.About),
                new InfoNote(isActive
                    ? $"Anmelden und Abmelden geht erst, wenn Arcadia {launcherName} findet: Ohne {launcherName} lässt sich nichts installieren."
                    : $"Anmelden und Abmelden geht erst, wenn {launcherName} aktiv ist. Eine gespeicherte Anmeldung bleibt erhalten.", HealthSeverity.Info),
            ];
            return;
        }

        row.IsEnabled = true;
        (row.ActionText, row.Subtitle) = state.Step switch
        {
            AccountStep.SigningIn => ("Abbrechen", $"Anmeldung auf der Seite von {launcherName} …"),
            AccountStep.Unreachable => ("Erneut versuchen", $"{launcherName} nicht erreichbar – [A] erneut versuchen"),
            AccountStep.SignedIn => ("Abmelden", state.Problem is not null || state.Notice is not null
                ? $"{state.AccountName} · {state.Notice ?? FirstSentence(state.Problem!)}"
                : state.AccountName),
            _ => ("Anmelden", state.Problem is not null ? FirstSentence(state.Problem) : account.Purpose),
        };
        row.Severity = state.Problem is not null ? HealthSeverity.Warning : state.Notice is not null ? HealthSeverity.Info : null;

        List<InfoBlock> info = [];
        if (state.Problem is { } problem)
        {
            info.Add(new InfoNote(problem));
        }

        if (state.Notice is not null)
        {
            info.Add(new InfoNote($"{launcherName} ist momentan mit einem anderen Konto angemeldet als in Arcadia. Arcadia wird so lange keine Installationen starten können. Installierte Spiele könnten ggf. nicht starten oder falsche Spielstände verwenden.", HealthSeverity.Info));
        }

        switch (state.Step)
        {
            case AccountStep.SigningIn:
                info.Add(new InfoText(account.SignInMethod));
                info.Add(new InfoText("[B] bricht die Anmeldung ab."));
                break;
            case AccountStep.Unreachable:
                info.Add(new InfoText($"Arcadia konnte die Anmeldeseite von {launcherName} nicht laden. Prüfe die Internetverbindung; [A] versucht es erneut."));
                break;
            case AccountStep.SignedIn:
                if (AvatarOf(state.AvatarPath) is { } avatar)
                {
                    info.Add(new InfoAvatar(state.AccountName ?? launcherName, avatar));
                }

                info.Add(new InfoText($"Angemeldet als {state.AccountName}. Arcadia zeigt neben deinen installierten Spielen auch die Spiele, die im Besitz von {state.AccountName} sind."));
                info.Add(new InfoFacts("KONTO",
                [
                    new InfoFact("Spiele im Konto", state.OwnedGames is { } count ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) : "Noch nicht geladen", IsMuted: state.OwnedGames is null),
                    new InfoFact("Stand", state.ListedAt is { } at ? Ago(at) : "–", IsMuted: state.ListedAt is null),
                ]));
                info.Add(new InfoFootnote("Meldest du dich ab, werden nur noch die installierten Spiele gelistet."));
                break;
            default:
                info.Add(new InfoText(account.About));
                info.Add(new InfoText(account.SignInMethod));
                break;
        }

        row.Info = info;
    }

    private static string FirstSentence(string text) => text.IndexOf(". ", StringComparison.Ordinal) is var end and > 0 ? text[..end] : text.TrimEnd('.');

    /// <summary>A on the account's row: sign in, drop a sign-in under way, or sign out after asking.</summary>
    private void ActivateAccount()
    {
        if (PageAccount is not { } account)
        {
            return;
        }

        switch (account.State.Step)
        {
            case AccountStep.SignedIn:
                Dialog = new ConfirmationViewModel(
                    $"Vom {account.Title} abmelden?",
                    $"Willst du {account.State.AccountName} wirklich in Arcadia abmelden? Arcadia kann dir dann nur noch die Spiele auflisten, die auf diesem PC installiert sind. "
                        + $"Ob du sie auch starten kannst, hängt von den Lizenzen des Benutzers ab, der in {PageInfo.DisplayName} angemeldet ist.",
                    "Abmelden",
                    account.SignOut,
                    () => Dialog = null);
                break;
            case AccountStep.SigningIn:
                account.CancelSignIn();
                break;
            default:
                account.BeginSignIn();
                break;
        }
    }

    /// <summary>
    /// The question before signing out: ◀ ▶ choose, A answers, B keeps the sign-in. The one before resetting the games'
    /// modes (#178): A resets, B keeps them.
    /// </summary>
    public override void HandleDialog(UiAction action)
    {
        switch (Dialog)
        {
            case ConfirmationViewModel confirmation when !confirmation.Handle(action):
                Dialog = null;
                break;
            case SettingsQuestionViewModel question:
                question.Handle(action);
                break;
        }
    }

    public override void Dispose()
    {
        foreach (var (account, handler) in _accounts)
        {
            account.StateChanged -= handler;
        }

        _accounts.Clear();
        base.Dispose();
    }

    /// <summary>
    /// A switch that adds software, tools or soundtracks to the launcher's library (#141), as the launcher offers it. Acts
    /// at once and is kept in the settings; the tab shows the entries without reading the launcher again.
    /// </summary>
    private SwitchRowViewModel OptionRow(string launcherId, Arcadia.Launchers.Abstractions.LibraryTypeOption option)
    {
        var key = $"library-option:{launcherId}:{option.Id}";
        if (!_optionRows.TryGetValue(key, out var row))
        {
            row = new SwitchRowViewModel(key, option.Title, () => _host.Launchers.IsOptionOn(launcherId, option.Id), on => SwitchOption(launcherId, option, on))
            {
                Group = LibraryGroup,
                Subtitle = option.Subtitle,
                Info =
                [
                    new InfoText(option.Info),
                    .. option.Examples.Select(example => new InfoBullet(example)),
                    new InfoFootnote("Standard: Aus. Wirkt sofort, ohne Speichern."),
                ],
            };
            _optionRows[key] = row;
        }

        row.Refresh();
        return row;
    }

    private void SwitchOption(string launcherId, Arcadia.Launchers.Abstractions.LibraryTypeOption option, bool on)
    {
        _changes.Apply(settings => settings.WithLauncherPreferences(launcherId, p => p.WithLibraryOption(option.Id, on)), option.Title);
        AppLog.Write($"{launcherId}: „{option.Title}“ {(on ? "an" : "aus")}");
        _host.LibraryTypesChanged();
        Refresh();
    }

    /// <summary>
    /// The group "Controller" (#178): the Steam Controller mode the launcher's games on "Standard" get, and below it, while
    /// games of its library have a mode of their own, the row that puts them all back on "Standard".
    /// </summary>
    /// <returns>Whether a game of the library has a mode of its own, i.e. the reset row shows.</returns>
    private bool ShowControllerRows(string id, string name, bool gamesUseSteamInput, LauncherLibrary? library)
    {
        var panOn = _host.IsPanOn;
        var settings = _changes.Current;
        var preset = ControllerModes.Of(settings.LauncherUsesArcadiaPad(id, _host.Launchers.GamesUseSteamInput));
        var panSupported = _host.IsPanSupported;
        var panOffSubtitle = panSupported ? PanOffSubtitle : PanUnsupportedSubtitle;
        IReadOnlyList<InfoBlock> panOff = panOn ? [] : [new InfoNote(panSupported
            ? "Einstellbar, sobald der Controller-Wächter (Pan) an ist: Controller → Pan."
            : "Der Controller-Wächter (Pan) läuft auf diesem System noch nicht, und ohne ihn gibt es keinen Steam-Controller-Modus.", HealthSeverity.Info)];

        _controllerModeRow.ShowPan();
        _controllerModeRow.Subtitle = panOn ? "Vorauswahl für die Spiele dieses Launchers" : panOffSubtitle;
        _controllerModeRow.Info =
        [
            new InfoText($"So startet ein Spiel aus {name} mit dem Steam Controller, solange du in seinen Details nichts anderes wählst. Spiele mit eigenem Modus behalten ihn."),
            .. panOff,
            .. PanRows.ModeCards,
            new InfoFootnote($"{(gamesUseSteamInput ? $"Ab Werk: {ControllerModes.Steam}, weil Spiele aus {name} Steams Belegungen nutzen." : $"Ab Werk: {ControllerModes.Arcadia}.")} Gilt ab dem nächsten Spielstart."),
        ];

        // Only games of the library count and are listed; the reset clears the missing ones' choices too.
        var own = (library?.Games ?? [])
            .Select(game => (game.Title, Choice: settings.ControllerModeChoiceFor(game.Key)))
            .Where(game => game.Choice is not null)
            .OrderBy(game => game.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        _resetModesRow.IsEnabled = panOn;
        _resetModesRow.IsDimmed = !panOn;
        _resetModesRow.Subtitle = !panOn ? panOffSubtitle : own.Count == 1 ? "1 Spiel hat einen eigenen Modus" : $"{own.Count} Spiele haben einen eigenen Modus";
        _resetModesRow.Info =
        [
            new InfoText($"Diese Spiele aus {name} hast du selbst umgestellt. Danach folgen sie wieder der Vorauswahl „{preset}“:"),
            new InfoFacts(string.Empty, [.. own.Select(game => new InfoFact(game.Title, ControllerModes.Of(game.Choice!.Value)))]),
            .. panOff,
            new InfoText("[A] fragt vorher nach."),
            new InfoFootnote("Gilt ab dem nächsten Spielstart."),
        ];
        _ownModeTitles = [.. own.Select(game => game.Title)];
        return own.Count > 0;
    }

    private void SetControllerMode(bool useArcadiaPad)
    {
        var id = _page!;
        _changes.Apply(settings => settings.WithLauncherControllerMode(id, useArcadiaPad, _host.Launchers.GamesUseSteamInput), "Steam Controller");
        AppLog.Write($"Steam-Controller-Vorauswahl von {id}: {ControllerModes.Of(useArcadiaPad)}");
        Refresh();
    }

    /// <summary>[A] on "Alle Spiele auf die Vorauswahl setzen": asks first, since the games' own choices are gone afterwards.</summary>
    private void AskResetControllerModes()
    {
        if (_page is not { } id || !_host.IsPanOn || _ownModeTitles.Count == 0)
        {
            return;
        }

        var name = _host.Launchers.Info(id).DisplayName;
        var preset = ControllerModes.Of(_changes.Current.LauncherUsesArcadiaPad(id, _host.Launchers.GamesUseSteamInput));
        Dialog = new SettingsQuestionViewModel(
            "Alle Spiele auf die Vorauswahl setzen?",
            $"{ResetGames(_ownModeTitles, name)} wieder „{preset}“: {TitleList(_ownModeTitles)}. Gilt ab dem nächsten Spielstart.",
            "Zurücksetzen",
            () => ResetControllerModes(id),
            () => Dialog = null);
    }

    private static string ResetGames(IReadOnlyList<string> titles, string launcher) =>
        titles.Count == 1 ? $"1 Spiel aus {launcher} bekommt" : $"{titles.Count} Spiele aus {launcher} bekommen";

    /// <summary>The first few titles; a long list ends with how many more there are.</summary>
    private static string TitleList(IReadOnlyList<string> titles) =>
        titles.Count <= MaxTitlesInQuestion ? string.Join(", ", titles) : $"{string.Join(", ", titles.Take(MaxTitlesInQuestion))} und {titles.Count - MaxTitlesInQuestion} weitere";

    /// <summary>Every game of the launcher back on "Standard", missing ones too, in one write; the focus goes to "Steam Controller".</summary>
    private void ResetControllerModes(string id)
    {
        Dialog = null;
        _changes.Apply(settings => settings.WithoutControllerModeChoices(id), "Steam Controller");
        AppLog.Write($"Steam-Controller-Modus aller Spiele von {id} auf die Vorauswahl gesetzt");
        Refresh();
        Focus(_controllerModeRow);
    }

    /// <summary>A on "Name im Tab" or "Such-Kürzel": the on-screen keyboard over the text, checked while it is typed.</summary>
    private void Edit(TextRowViewModel row)
    {
        if (_page is not { } id || Editor is not null)
        {
            return;
        }

        var isName = row == _nameRow;
        TextEditorViewModel? editor = null;
        editor = new TextEditorViewModel(
            row.ValueText,
            // An empty name is the launcher's own again (owner, 2026-10-23), so only a keyword must not be empty.
            text => isName ? (text.Trim().Length == 0 ? null : LauncherNames.NameProblem(text)) : LauncherNames.KeywordProblem(text, id, _host.Launchers.Shown),
            text =>
            {
                SaveText(id, isName, text);
                return true;
            },
            () => CloseEditor(row, editor!),
            _host.CreateKeyboard,
            // A keyword offers only the keys it may hold (owner, 2026-10-23); a name takes any character.
            isName ? null : LauncherNames.IsKeywordCharacter);
        row.Editor = editor;
        Editor = editor;
    }

    private void CloseEditor(TextRowViewModel row, TextEditorViewModel editor)
    {
        row.Editor = null;
        Editor = null;
        if (editor.Rejection is { } reason)
        {
            _host.Notify($"{row.Title} nicht übernommen: {reason}");
        }

        editor.Dispose();
    }

    /// <summary>The launcher's own name or keyword is kept as none, so it follows the integration if that renames it.</summary>
    private void SaveText(string id, bool isName, string text)
    {
        var original = _host.Launchers.Registered(id)!.Launcher;
        var value = isName ? text.Trim() : text;
        var own = value.Length == 0 || value == (isName ? original.DisplayName : original.SearchKeyword) ? null : value;
        _changes.Apply(settings => settings.WithLauncherPreferences(id, p => isName ? p with { Name = own } : p with { Keyword = own }), isName ? "Launcher-Name" : "Such-Kürzel");
        AppLog.Write($"{(isName ? "Name" : "Such-Kürzel")} von {id} geändert");
        LooksChanged();
    }

    /// <summary>"Logo" → "Hochladen …": the file dialog; a picture that passes the checks replaces the logo everywhere.</summary>
    private async Task PickLogoAsync()
    {
        if (_page is not { } id || _host.Dialogs is not { } dialogs || _isPicking)
        {
            return;
        }

        _isPicking = true;
        try
        {
            var name = _host.Launchers.Info(id).DisplayName;
            if (await dialogs.PickFileAsync($"Logo für {name} wählen", "Bilder", LogoFile.Extensions) is not { } path)
            {
                return;
            }

            var import = await _host.Logos.ImportAsync(id, path);
            if (import.FileName is not { } fileName)
            {
                _host.Notify($"Logo nicht übernommen: {import.Problem}");
                return;
            }

            var previous = _changes.Current.PreferencesOf(id).LogoFile;
            _changes.Apply(settings => settings.WithLauncherPreferences(id, p => p with { LogoFile = fileName }), "Launcher-Logo");
            if (previous != fileName)
            {
                _host.Logos.Delete(previous);
            }

            AppLog.Write($"Eigenes Logo für {id}");
            LooksChanged();
        }
        finally
        {
            _isPicking = false;
        }
    }

    /// <summary>"Standardlogo" → "Wiederherstellen": the launcher's own logo again; the copy of the picture goes.</summary>
    private void RestoreLogo()
    {
        if (_page is not { } id || _changes.Current.PreferencesOf(id).LogoFile is not { } fileName)
        {
            return;
        }

        _changes.Apply(settings => settings.WithLauncherPreferences(id, p => p with { LogoFile = null }), "Launcher-Logo");
        _host.Logos.Delete(fileName);
        AppLog.Write($"Standardlogo für {id}");
        LooksChanged();

        // The row pressed is gone; the focus goes back to the logo, not on to the next row.
        Focus(_logoRow);
    }

    /// <summary>
    /// "Ordner wählen …" (#138): the folder dialog; Arcadia takes the folder only if the launcher really lies there, then
    /// switches the launcher on if it isn't yet and reads it, so its games appear.
    /// </summary>
    private async Task PickFolderAsync()
    {
        if (_page is not { } id || _host.Launchers.Registered(id)?.FolderChoice is not { } choice || _host.Dialogs is not { } dialogs || _isPicking)
        {
            return;
        }

        _isPicking = true;
        try
        {
            var name = _host.Launchers.Info(id).DisplayName;
            if (await dialogs.PickFolderAsync($"Ordner von {name} wählen") is not { } folder)
            {
                return;
            }

            if (await Task.Run(() => choice.Find(folder)) is not { } root)
            {
                _host.Notify($"Dort liegt kein {name}: Arcadia sucht {choice.Expected}.");
                return;
            }

            _changes.Apply(settings => settings.WithLauncherPreferences(id, p => p with { Folder = root }), "Launcher-Ordner");
            AppLog.Write($"Ordner für {id} gewählt");
            var active = _host.Launchers.ActiveIds;
            if (active.Contains(id))
            {
                _host.RescanLauncher(id);
                Refresh();
            }
            else
            {
                Choose(LauncherSelection.Placed(active, id, active.Count), null);
            }
        }
        finally
        {
            _isPicking = false;
        }
    }

    private void LooksChanged()
    {
        _host.LauncherLooksChanged();
        RefreshLooks();
    }

    private void Lift(LauncherRowViewModel row)
    {
        if (!row.CanMove)
        {
            return;
        }

        Focus(row);
        var active = _host.Launchers.ActiveIds;
        var index = active.ToList().IndexOf(row.Launcher.Id);
        _slot = index >= 0 ? index : active.Count + 1;
        _lifted = row;
        row.IsLifted = true;
        IsReordering = true;
        Refresh();
    }

    private void MoveLifted(int delta) => PlaceLifted(_slot + delta);

    private void PlaceLifted(int slot)
    {
        if (_lifted is null)
        {
            return;
        }

        var activeOthers = _host.Launchers.ActiveIds.Count(id => id != _lifted.Launcher.Id);
        slot = Math.Clamp(slot, 0, activeOthers + 1);
        if (slot != _slot)
        {
            _slot = slot;
            Refresh();
        }
    }

    /// <summary>◀ ▶ on a row, or its arrows: on goes to the end of the active ones, off to the top of the inactive ones.</summary>
    private void SwitchTo(LauncherRowViewModel row, bool on)
    {
        if (IsReordering)
        {
            return;
        }

        var active = _host.Launchers.ActiveIds;
        var id = row.Launcher.Id;
        Choose(LauncherSelection.Placed(active, id, on ? active.Count : null), on ? null : id);
    }

    private void Choose(IReadOnlyList<string> active, string? switchedOff)
    {
        _switchedOff.RemoveAll(active.Contains);
        if (switchedOff is not null)
        {
            _switchedOff.Remove(switchedOff);
            _switchedOff.Insert(0, switchedOff);
        }

        _changes.Apply(settings => settings with { ActiveLaunchers = active }, "Launcher");
        AppLog.Write($"Aktive Launcher: {string.Join(", ", active)}");
        _host.LauncherChoiceChanged();
        Refresh();
    }

    /// <summary>The rows as shown: the active ones in tab order and the inactive ones, with a lifted one where it stands now.</summary>
    private (IReadOnlyList<string> Active, IReadOnlyList<string> Inactive) Arrangement()
    {
        var launchers = _host.Launchers;
        var active = launchers.ActiveIds;
        // Still those found first; within each, the ones switched off last at the top.
        IReadOnlyList<string> inactive = launchers.InactiveIds
            .OrderBy(id => launchers.IsFound(id) ? 0 : 1)
            .ThenBy(id => _switchedOff.IndexOf(id) is >= 0 and var index ? index : int.MaxValue)
            .ToList();
        if (_lifted is null)
        {
            return (active, inactive);
        }

        var lifted = _lifted.Launcher.Id;
        var others = active.Where(id => id != lifted).ToList();
        var rest = inactive.Where(id => id != lifted).ToList();
        return _slot > others.Count
            ? (others, [lifted, .. rest])
            : (LauncherSelection.Placed(others, lifted, _slot), rest);
    }

    private void ShowState(LauncherRowViewModel row, IReadOnlyList<LauncherRowViewModel> tabs)
    {
        var launchers = _host.Launchers;
        var id = row.Launcher.Id;
        var name = row.Launcher.DisplayName;
        var place = tabs.ToList().IndexOf(row);
        var isActive = place >= 0;
        var isFound = launchers.IsFound(id);
        var library = isActive ? _host.LibraryOf(id) : null;
        var keyword = $"{SearchQuery.PrefixMarker}{row.Launcher.SearchKeyword}";

        // The status message: short in the row instead of the state, the whole text atop the info panel.
        (string Text, HealthSeverity Severity)? notice = null;
        string? note = null;
        if (isActive && !isFound)
        {
            notice = ("Nicht gefunden", HealthSeverity.Warning);
            note = $"Arcadia findet {name} auf diesem PC nicht mehr. Ist es deinstalliert, schalte den Launcher hier ab, dann verschwindet sein Tab.";
        }
        else if (library is { Games.Count: 0 })
        {
            // Nothing is broken and nothing needs doing: information, not a warning (owner, 2026-10-24).
            notice = ("Keine Spiele gefunden", HealthSeverity.Info);
            note = $"{name} ist installiert, aber Arcadia hat dort keine Spiele gefunden. Sobald du dort ein Spiel kaufst oder installierst, erscheint es im Tab.";
        }

        var status = (isActive, isFound, library) switch
        {
            (true, _, { } shown) => $"{GameCount(shown.Games.Count)} · {keyword}",
            (true, _, null) => "Wird eingelesen …",
            (false, true, _) => "Installiert",
            _ => "Nicht gefunden",
        };

        var tabNumber = isActive ? place + 1 : (int?)null;
        var integration = launchers.Registered(id);
        var location = launchers.LocationOf(id);
        InfoBlock[] preview = [new InfoTabBar([.. tabs.Select(tab => new InfoTab(tab.Launcher.DisplayName, tab.Badge, tab == row))])];

        // Whether it is active, its tab, key and keyword are in the preview and the row already (owner, 2026-10-22).
        List<InfoBlock> info = [new InfoLauncher(name, row.Badge, isActive ? $"Aktiv · Tab {tabNumber}" : isFound ? "Nicht aktiv" : "Nicht installiert", isActive)];
        if (notice is { } message && note is not null)
        {
            info.Add(new InfoNote(note, message.Severity));
        }

        var scannedAt = _host.ScannedAt(id);
        info.Add(new InfoFacts("BIBLIOTHEK",
        [
            library is null
                ? new InfoFact("Spiele", isActive ? "werden eingelesen" : "nicht eingelesen", IsMuted: true)
                : library.Games.Count == 0 ? new InfoFact("Spiele", GameCount(0))
                : CountsFact(TypeCountLine.Of(library.Games)),
            new InfoFact("Zuletzt eingelesen", Ago(scannedAt), IsMuted: scannedAt is null),
        ]));
        info.Add(new InfoFacts("ORT",
        [
            new InfoFact("Gefunden unter", location ?? "Nicht gefunden", IsMuted: location is null),
            new InfoFact("Installationen", (isActive, integration?.InstallMonitor) switch
            {
                (false, _) => "nicht überwacht",
                (true, null) => "nur aus Arcadia gestartete",
                _ => "werden überwacht",
            }, IsMuted: !isActive, IsGood: isActive && integration?.InstallMonitor is not null),
            new InfoFact("Laufende Spiele", (isActive, integration?.RunningGames) switch
            {
                (false, _) => "nicht überwacht",
                (true, null) => "an ihren Prozessen erkannt",
                _ => "meldet der Launcher",
            }, IsMuted: !isActive, IsGood: isActive),
        ]));

        row.Show(isActive, isFound, tabNumber, status, notice, preview, info);
    }

    // The same count line as beside the tab's name (#141); the words are the tooltip's.
    private static InfoFact CountsFact(TypeCountLine counts) => new("Einträge", counts.Tip, Counts: counts);

    private static string GameCount(int count) => count == 1 ? "1 Spiel" : $"{count} Spiele";

    private string Ago(DateTimeOffset? at)
    {
        if (at is not { } time)
        {
            return "–";
        }

        var age = _time.GetUtcNow() - time;
        return age < TimeSpan.FromMinutes(1) ? "gerade eben"
            : age < TimeSpan.FromHours(1) ? $"vor {(int)age.TotalMinutes} Min."
            : $"um {time.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)} Uhr";
    }
}
