using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.ControllerSupport.Copy;
using Arcadia.ControllerSupport.Pan;
using Arcadia.Pan;

namespace Arcadia.App.Settings;

/// <summary>What the settings, the header and the mode need of Pan's emulation (#242); the App's is <c>SteamControllerForGames</c>.</summary>
public interface IPanEmulationView
{
    PanEmulationStatus Status { get; }

    /// <summary><see cref="Status"/> changed; any thread.</summary>
    event Action? Changed;

    /// <summary>"Erneut versuchen": the failures so far no longer count, and the next tick tries again.</summary>
    void Retry();

    /// <summary>The attempt "Erneut versuchen" started hasn't ended yet; a toast tells how it ended (#308).</summary>
    bool Retrying { get; }

    /// <summary>Steam tried a firmware update through the copy while no game was in front (#295); any thread.</summary>
    event Action? FirmwareUpdateTried;

    /// <summary>
    /// "Controller freigeben" for a firmware update (#295): the real Steam Controller and puck are shown until Pan is
    /// switched off and on, or Arcadia restarts.
    /// </summary>
    Task ReleaseForFirmwareAsync();

    /// <summary>Connected controllers Pan couldn't hide, so games see them directly (#238 D7).</summary>
    IReadOnlyList<ControllerNotHidden> NotHidden => [];

    /// <summary>Connected controllers Pan waits with while their vendor's firmware tool runs (#238 D8).</summary>
    IReadOnlyList<ControllerWaitingForTool> WaitingForTool => [];
}

/// <summary>No emulation (tests that don't need one): always off.</summary>
public sealed class NoPanEmulation : IPanEmulationView
{
    public PanEmulationStatus Status => PanEmulationStatus.Off;

    public event Action? Changed
    {
        add { }
        remove { }
    }

    public void Retry()
    {
    }

    public bool Retrying => false;

    public event Action? FirmwareUpdateTried
    {
        add { }
        remove { }
    }

    public Task ReleaseForFirmwareAsync() => Task.CompletedTask;
}

/// <summary>"Controller-Wächter (Pan)": a switch, "Treiber installieren", or why neither is possible (#242).</summary>
public sealed class PanRowViewModel(Action<bool?> activate)
    : SetupSwitchRowViewModel(PanRows.MainKey, "Controller-Wächter (Pan)", activate)
{
    public override string InfoTitle => "PAN, DER CONTROLLER-WÄCHTER";

    public override string ActivateText => IsInstall ? InstallText : "Ändern";
}

/// <summary>
/// The group "PAN" of the controller settings (#242, artboards T242Settings, T242Hauptzeile, T242Warnzeilen): the switch
/// "Controller-Wächter (Pan)", below it one indented row per reason it doesn't run (only while it is on: off needs
/// nothing), and "Controller wiederherstellen" as soon as Pan is installed, also while it is off.
/// </summary>
public sealed class PanRows
{
    public const string MainKey = "pan";
    public const string RestoreKey = "pan-restore";
    public const string Group = "PAN";

    private const string Admin = "Windows fragt einmal nach Administratorrechten.";

    public const string UpdateDueSubtitle = "Neue Pan-Version verfügbar";
    public const string UpdatingSubtitle = "Pan wird aktualisiert …";
    public const string UpdateDueInfo = "Schaltest du den Controller-Wächter ein, wird Pan zuerst aktualisiert. Dabei fragt Windows nach deiner Erlaubnis.";
    public const string ExcludedSubtitle = "An – Steam Controller und Puck freigegeben, bis du Pan aus- und wieder einschaltest";
    public const string NotUpdatedToast = "Pan: nicht aktualisiert, Controller-Wächter bleibt aus";

    private readonly ControllerSupportService _support;
    private readonly SettingsChanges _changes;
    private readonly IPanEmulationView _pan;
    private readonly Func<string?> _gameInTheWay;
    private readonly Action<HealthFix> _applyFix;
    private readonly Action<string> _notify;
    private readonly Dictionary<string, ActionRowViewModel> _rows = [];

    /// <param name="notify">Shows a toast, e.g. how switching on with a due update ended (#326).</param>
    public PanRows(ControllerSupportService support, SettingsChanges changes, IPanEmulationView pan, Func<string?> gameInTheWay, Action<HealthFix> applyFix,
        Action<string> notify)
    {
        _notify = notify;
        _support = support;
        _changes = changes;
        _pan = pan;
        _gameInTheWay = gameInTheWay;
        _applyFix = applyFix;
        Main = new PanRowViewModel(_ => Activate()) { Group = Group };
        Restore = new ActionRowViewModel(RestoreKey, "Controller wiederherstellen", () => _support.RestoreControllersAsync().Forget("Controller wiederherstellen"))
        {
            Group = Group,
            Subtitle = "Wenn Controller nicht reagieren oder doppelt eingeben",
            ActionText = "Wiederherstellen",
            Info =
            [
                new InfoText("Stellt deine Controller wieder her, wenn sie unerwartet nicht mehr reagieren oder doppelte Eingaben verursachen. Pan gibt dann alle Controller wieder frei und räumt die emulierten Controller auf. Windows fragt dafür nach Administratorrechten."),
                new InfoText("Im Normalfall ist das nicht nötig, aber besser haben als brauchen."),
                new InfoSection("HILFT AUCH DAS NICHT",
                [
                    new InfoText("Öffne die HidHide-Konfiguration aus dem Startmenü, hake deine Controller ab, entferne Arcadia aus der Programmliste und schalte „Enable device hiding“ aus."),
                ]),
            ],
        };
    }

    public PanRowViewModel Main { get; }

    public ActionRowViewModel Restore { get; }

    /// <summary>The symbol beside the category: the worst row below the switch, or the emulation's problem; null while off.</summary>
    public HealthSeverity? Marker { get; private set; }

    /// <summary>The switch was flipped: the rows are due to be built again.</summary>
    public event Action? Switched;

    /// <summary>The first row that says why the emulation doesn't run, where [A] on a greyed mode leads; null: none.</summary>
    public string? FirstWarningKey { get; private set; }

    private bool IsOn => _changes.Current.PanEnabled;

    /// <summary>The installed Pan isn't this Arcadia's and Pan is off (#326): switching on updates it first.</summary>
    private bool IsUpdateDue => _support.IsPanUpdateDue && !IsOn;

    /// <summary>Switching on runs the update now; the Windows prompt is up.</summary>
    private bool _updating;

    private bool IsBusy => _support.BusyText is not null;

    private bool IsInstalling => _support.BusyText == ControllerSupportService.DriversBusyText;

    /// <summary>The rows of the group, in order; call on every change of the support, the emulation or the settings.</summary>
    public IReadOnlyList<SettingsRowViewModel> Build()
    {
        var status = _pan.Status;
        var notHere = !_support.IsSupportedPlatform;
        var allMissing = !notHere && EmulationParts.AllMissing(_support.Issues);
        var installFailed = allMissing && _support.FailedFix == HealthFix.InstallDrivers && _support.LastError is not null;
        var game = _gameInTheWay();
        var warnings = !notHere && !allMissing && IsOn ? WarningRows(status) : [];
        var excluded = !notHere && !allMissing && IsOn && status.Problem == PanEmulationProblem.Excluded;

        var problem = warnings.Any(row => row.Severity > HealthSeverity.Info) || status.HasProblem;
        var updateDue = !notHere && !allMissing && IsUpdateDue;

        Main.InstallText = IsInstalling ? "Wird installiert …" : installFailed ? "Erneut versuchen" : "Treiber installieren";
        Main.Update(
            notHere ? SetupRowMode.Unavailable : allMissing ? SetupRowMode.Install : SetupRowMode.Switch,
            !notHere && !allMissing && IsOn,
            isLocked: IsBusy || _updating || (game is not null && !allMissing),
            unavailableText: "Auf diesem System noch nicht verfügbar",
            status: Subtitle(status, allMissing, installFailed, problem, game, excluded, updateDue));
        // A running game would lose its controller mid-game: greyed out like "Auf Werkseinstellungen zurücksetzen".
        Main.IsDimmed = notHere || (game is not null && !allMissing && !IsBusy);
        Main.Severity = installFailed ? HealthSeverity.Error : updateDue && !_updating ? HealthSeverity.Info : null;
        Main.ShowLamp(notHere || allMissing || updateDue ? null
            : !IsOn ? RowLamp.Resting
            : status.State == PanEmulationState.Stopped ? RowLamp.Failure
            : problem ? RowLamp.Problem
            : status.State == PanEmulationState.Running ? RowLamp.Working
            : RowLamp.Resting);
        Main.Info = [.. installFailed ? [new InfoError(_support.LastError!)] : Array.Empty<InfoBlock>(), .. updateDue ? UpdateInfo() : [], .. MainInfo];

        // Not beside the update's Windows prompt: one prompt at a time.
        Restore.IsEnabled = !IsBusy && !_updating;
        // A fix that failed marks its row red, not the category: the problem itself is still the same (#243 B2).
        Marker = !IsOn || notHere ? null
            : status.State == PanEmulationState.Stopped ? HealthSeverity.Error
            : problem || allMissing ? HealthSeverity.Warning
            : null;
        FirstWarningKey = warnings.FirstOrDefault(row => row.Severity > HealthSeverity.Info)?.Key ?? warnings.FirstOrDefault()?.Key;
        return [Main, .. warnings, .. _support.PanInstalled ? [Restore] : Array.Empty<SettingsRowViewModel>()];
    }

    /// <summary>First in the panel, before everything else (artboard T326S01): why switching on asks Windows; why the last update failed.</summary>
    private InfoBlock[] UpdateInfo() =>
        _updating ? [new InfoNote(PanUpdatePageViewModel.PromptHint, HealthSeverity.Info)]
        : [new InfoNote(UpdateDueInfo, HealthSeverity.Info) { IsBold = true }, .. _support.PanUpdateError is { } error ? [new InfoError(error)] : Array.Empty<InfoBlock>()];

    private string Subtitle(PanEmulationStatus status, bool allMissing, bool installFailed, bool problem, string? game, bool excluded, bool updateDue) =>
        (allMissing, IsOn) switch
        {
            (true, _) when IsInstalling => "Windows fragt einmal nach Administratorrechten",
            (true, _) when installFailed => "Installation abgebrochen – warum, steht im Info-Panel",
            (true, _) => "Pan verwaltet, wie Spiele, Programme und Launcher deine Controller nutzen",
            _ when game is not null => $"Erst änderbar, wenn „{game}“ beendet ist",
            _ when updateDue => _updating ? UpdatingSubtitle : UpdateDueSubtitle,
            (_, false) => "Aus – Spiele und Launcher sehen deine echten Controller direkt",
            _ when status.State == PanEmulationState.Stopped => "Angehalten – der Grund steht darunter",
            _ when problem => "Es liegt ein Problem vor. Prüfe die Warnmeldungen darunter.",
            _ when excluded => ExcludedSubtitle,
            _ => status.State switch
            {
                PanEmulationState.Running => "An – deine Controller werden von Arcadia verwaltet",
                PanEmulationState.Ready => "Bereit – kein Controller verbunden",
                _ => "Wird eingerichtet …",
            },
        };

    /// <summary>A, ◀ ▶ or a click: installs everything while nothing is set up, else switches; nothing while locked.</summary>
    private void Activate()
    {
        if (Main.IsLocked || Main.Mode == SetupRowMode.Unavailable)
        {
            return;
        }

        if (Main.Mode == SetupRowMode.Install)
        {
            _applyFix(HealthFix.InstallDrivers);
            return;
        }

        var on = !IsOn;
        if (on && _support.IsPanUpdateDue)
        {
            UpdateAndSwitchOnAsync().Forget("Pan aktualisieren");
            return;
        }

        _changes.Apply(settings => settings with { PanEnabled = on }, "Controller-Wächter (Pan)");
        Services.AppLog.Write($"Controller-Wächter (Pan): {(on ? "an" : "aus")}");
        Switched?.Invoke();
    }

    /// <summary>An old Pan is never switched on (#326): it is updated first, and only a confirmed update switches Pan on.</summary>
    private async Task UpdateAndSwitchOnAsync()
    {
        _updating = true;
        Switched?.Invoke();
        bool updated;
        try
        {
            // Off the UI thread: the elevated start waits for the Windows prompt.
            updated = await Task.Run(() => _support.UpdatePanAsync(CancellationToken.None));
            if (!updated)
            {
                _support.SwitchPanOffForUpdate(null);
            }
        }
        catch (Exception ex)
        {
            // Pan's own failures come as these with their text, shown in the info panel; anything else is a bug,
            // logged with its stack, and the row still says that Pan stays off.
            if (ex is not (InvalidOperationException or TimeoutException))
            {
                AppErrors.Report(ex, "Pan aktualisieren");
            }

            _support.SwitchPanOffForUpdate(ex.Message);
            updated = false;
        }
        finally
        {
            _updating = false;
        }

        if (updated)
        {
            _changes.Apply(settings => settings with { PanEnabled = true }, "Controller-Wächter (Pan)");
            Services.AppLog.Write("Controller-Wächter (Pan): aktualisiert und an");
        }

        _notify(updated ? PanUpdatePageViewModel.UpdatedToast : NotUpdatedToast);
        Switched?.Invoke();
    }

    /// <summary>One row per reason, the parts first (as the support lists them), then the emulation's own.</summary>
    private List<ActionRowViewModel> WarningRows(PanEmulationStatus status)
    {
        var rows = new List<ActionRowViewModel>();
        var ids = new HashSet<string>();
        // Several parts share "Installieren": its error shows once, at the first of them.
        var errorShown = false;
        foreach (var issue in _support.Issues)
        {
            var failed = !errorShown && issue.Fix != HealthFix.None && _support.FailedFix == issue.Fix ? _support.LastError : null;
            errorShown |= failed is not null;
            rows.Add(IssueRow(issue, failed));
            ids.Add(issue.Id);
        }

        foreach (var (id, row) in EmulationRows(status))
        {
            if (ids.Add(id))
            {
                rows.Add(row);
            }
        }

        // Only while the emulation runs: otherwise every controller reaches games directly anyway, and the rows above say why.
        if (status.State == PanEmulationState.Running)
        {
            rows.AddRange(FirmwareToolRows());
            if (NotHiddenRow() is { } notHidden)
            {
                rows.Add(notHidden);
            }
        }

        if (status.State == PanEmulationState.Unavailable && !rows.Any(row => row.Severity > HealthSeverity.Info))
        {
            // Pan or VIIPER doesn't answer, and no part row says why.
            rows.Add(Row("pan:start-failed", "Emulation startet nicht", status.Reason ?? "Pan antwortet nicht", HealthSeverity.Warning,
                StartFailedText, RetryText, _pan.Retry));
        }

        if (_pan.Retrying && _retried is { } retried && rows.All(row => row.Key != retried) && _rows.TryGetValue(retried, out var kept))
        {
            // The attempt sets up anew for a moment, so its problem is gone meanwhile: the pressed row stays, showing
            // that it runs, until the attempt ends (owner, 2026-10-29); focus doesn't jump either.
            ShowRetrying(kept);
            rows.Add(kept);
        }

        foreach (var gone in _rows.Keys.Where(key => rows.All(row => row.Key != key)).ToList())
        {
            _rows.Remove(gone);
        }

        return rows;
    }

    private IEnumerable<(string Id, ActionRowViewModel Row)> EmulationRows(PanEmulationStatus status)
    {
        switch (status.Problem)
        {
            case PanEmulationProblem.Hiding when EmulationParts.FromHidingCheck(status.Hiding) is { } hiding:
                // HidHide's own state (its configuration open, the list inverted, ...): the same row the support shows.
                yield return (hiding.Id, IssueRow(hiding, null));
                break;
            case PanEmulationProblem.Hiding:
                yield return ("pan:hide-failed", Row("pan:hide-failed", "Steam Controller lässt sich nicht verstecken", "Pan versucht es gleich wieder", HealthSeverity.Warning,
                    "Pan konnte den Steam Controller nicht vor anderen Programmen verstecken, zum Beispiel weil Windows ihn nicht neu verbinden ließ. Solange er nicht versteckt ist, emuliert Arcadia ihn nicht, damit keine Eingabe doppelt ankommt. Pan versucht es alle 10 Sekunden wieder; warum es scheitert, steht in pan.log.",
                    RetryText, _pan.Retry));
                break;
            case PanEmulationProblem.CopyFailed:
                yield return ("pan:start-failed", Row("pan:start-failed", "Emulation startet nicht", "Arcadia versucht es gleich wieder", HealthSeverity.Warning,
                    StartFailedText, RetryText, _pan.Retry));
                break;
            case PanEmulationProblem.TooManyFailures:
                yield return ("pan:stopped", Row("pan:stopped", "Emulation angehalten", "Mehr als 3 Ausfälle in 5 Minuten, Arcadia versucht es nicht mehr von selbst", HealthSeverity.Error,
                    "Die Emulation ist mehrmals kurz hintereinander ausgefallen. Damit Spiele nicht ständig Controller verlieren und wiederbekommen, wartet Arcadia jetzt auf dich. „Erneut versuchen“ startet sie neu. Hilft das nicht, „Controller wiederherstellen“.",
                    RetryText, _pan.Retry));
                break;
            case PanEmulationProblem.CleaningUp:
                yield return ("pan:cleaning-up", Row("pan:cleaning-up", "Pan räumt noch auf", "Gleich geht es von selbst weiter", HealthSeverity.Info,
                    "Pan gibt gerade nach dem letzten Ende der Emulation die echten Controller zurück. Danach richtet Arcadia die Emulation von selbst neu ein; das dauert wenige Sekunden.",
                    null, null));
                break;
        }
    }

    /// <summary>One warning per firmware tool that runs (#238 D8, artboards T238F06–F07); it goes once the tool has ended.</summary>
    private IEnumerable<ActionRowViewModel> FirmwareToolRows()
    {
        foreach (var vendor in _pan.WaitingForTool.GroupBy(waiting => waiting.Model.Vendor))
        {
            var (tool, controller, it) = vendor.Key == ControllerList.Sony
                ? ("DualSense-Firmware-Updater", "den DualSense", "ihn")
                : ("Xbox Zubehör", "den Xbox Controller", "es");
            var row = Row($"pan:firmware-tool:{vendor.Key:X4}", $"{tool} ist geöffnet", $"Arcadia übernimmt {controller}, sobald du {it} schließt", HealthSeverity.Warning,
                $"Mit {tool} aktualisierst du die Firmware {(vendor.Key == ControllerList.Sony ? "deines DualSense" : "deines Xbox Controllers")}. Damit ein Update nicht abbricht, übernimmt Arcadia den Controller erst, wenn {tool} geschlossen ist.",
                null, null);
            row.Info = [.. row.Info, new InfoSection("WARTET AUF ÜBERNAHME", [.. vendor.Select(waiting => new InfoText(waiting.Name))])];
            yield return row;
        }
    }

    /// <summary>
    /// The controllers that reach games directly as Pan couldn't hide them (#238 D7, artboard T238F05): one row with the
    /// blue i, since there is usually nothing to do.
    /// </summary>
    private ActionRowViewModel? NotHiddenRow()
    {
        var notHidden = _pan.NotHidden;
        if (notHidden.Count == 0)
        {
            return null;
        }

        var title = notHidden.Count == 1 ? $"{notHidden[0].Name} lässt sich nicht verstecken" : $"{notHidden.Count} Controller lassen sich nicht verstecken";
        var row = Row("pan:not-hidden", title, "Spiele sehen sie direkt, Arcadias Tastenkombinationen kommen dort mit an", HealthSeverity.Info,
            "Pan konnte diese Controller nicht vor Spielen verstecken. Damit keine Eingabe doppelt ankommt, emuliert Arcadia sie nicht: Spiele sehen sie direkt, so als liefe Arcadia nicht.",
            null, null);
        row.Info =
        [
            .. row.Info,
            new InfoText("In Arcadia funktionieren sie wie gewohnt. Nur Tastenkombinationen wie „Zurück zu Arcadia“ kommen dabei auch im Spiel an."),
            new InfoSection("BETROFFEN", [.. notHidden.Select(controller => new InfoCard(controller.Name, NotHiddenReason(controller.Reason), string.Empty))]),
            new InfoText("Steckst du den Controller neu ein, versucht Pan es erneut. Wer gar keine Emulation möchte, schaltet den Controller-Wächter aus."),
        ];
        return row;
    }

    /// <summary>Why Pan didn't hide a controller, in the player's words (#335's findings A1–A3).</summary>
    private static string NotHiddenReason(PanReason reason) => reason switch
    {
        PanReason.ControllerNeedsReplug => "Windows gibt diesen Controller erst frei, wenn du ihn neu verbindest. Bis dahin sehen Programme, die ihn schon geöffnet haben, ihn direkt.",
        PanReason.ControllerNotHideable => "Pan findet an diesem Controller nichts, was es vor Spielen verstecken kann.",
        PanReason.ControllerNotConnected => "Pan findet diesen Controller nicht unter den Geräten von Windows.",
        // HidingFailed has several causes (a failed restart, an unknown image path, HidHide's errors): no guess at which.
        _ => "Pan konnte diesen Controller nicht verstecken. Was dabei schiefging, steht in pan.log.",
    };

    private const string StartFailedText = "Arcadia konnte die emulierten Controller nicht anlegen. Pan hat die echten Controller deshalb wieder freigegeben, Steam und Spiele sehen sie direkt. Arcadia versucht es beim nächsten Durchlauf von selbst; Details stehen in arcadia.log.";

    private ActionRowViewModel IssueRow(HealthIssue issue, string? failed)
    {
        var (subtitle, text, severity) = Texts(issue);
        var fix = issue.Fix;
        var row = Row($"issue:{issue.Id}", issue.Title, subtitle, failed is null ? severity : HealthSeverity.Error, text,
            fix == HealthFix.None ? null : issue.FixLabel ?? "Beheben", fix == HealthFix.None ? null : () => _applyFix(fix));
        row.ShowProblem(failed);
        if (failed is not null)
        {
            row.Info = [new InfoText(text), new InfoError(failed)];
        }

        return row;
    }

    private ActionRowViewModel Row(string key, string title, string subtitle, HealthSeverity severity, string text, string? action, Action? run)
    {
        if (!_rows.TryGetValue(key, out var row))
        {
            row = new ActionRowViewModel(key, title, () => _run.GetValueOrDefault(key)?.Invoke()) { Group = Group, Indent = 1 };
            _rows[key] = row;
        }

        _run[key] = action == RetryText ? () => RetryFrom(key) : run;
        row.Title = title;
        row.Subtitle = subtitle;
        row.Severity = severity;
        row.ActionText = action ?? string.Empty;
        row.IsEnabled = !IsBusy;
        row.IsWorking = false;
        row.ShowProblem(null);
        row.Info = [new InfoText(text)];
        if (action == RetryText && _pan.Retrying && _retried == key)
        {
            ShowRetrying(row);
        }

        return row;
    }

    private const string RetryText = "Erneut versuchen";

    /// <summary>The row whose "Erneut versuchen" was pressed last; it shows the attempt while it runs.</summary>
    private string? _retried;

    private void RetryFrom(string key)
    {
        _retried = key;
        _pan.Retry();
    }

    private static void ShowRetrying(ActionRowViewModel row)
    {
        row.ActionText = "Wird versucht …";
        row.IsWorking = true;
        row.IsEnabled = false;
    }

    private readonly Dictionary<string, Action?> _run = [];

    /// <summary>The designs' line and explanation for a part's row (T242Warnzeilen); a row they don't name keeps its own.</summary>
    private static (string Subtitle, string Text, HealthSeverity Severity) Texts(HealthIssue issue) => issue.Id switch
    {
        "usbip-missing" => ("Erzeugt die emulierten Controller",
            $"Über den usbip-Treiber (usbip-win2) erzeugt Arcadia die emulierten Controller, die Spiele und Steam sehen. „Installieren“ richtet alle fehlenden Teile in einem Schritt ein. {Admin}", issue.Severity),
        "usbip-outdated" => (issue.Detail.TrimEnd('.'),
            $"Arcadia ist mit usbip-win2 ab {UsbipRelease.MinimumVersion} geprüft. „Aktualisieren“ installiert diese Version. {Admin}", issue.Severity),
        "usbip-stopped" => ("Meist hilft ein Neustart des PCs",
            $"Der Treiber ist installiert, aber Windows hat ihn nicht geladen. Meist hilft ein Neustart des PCs, sonst eine Neuinstallation. {Admin}", issue.Severity),
        "viiper-missing" => ("Leitet die Signale der Controller an Spiele und Programme weiter", issue.Detail, issue.Severity),
        "pan-missing" => ("Gibt deine Controller zurück, falls Arcadia abstürzt",
            $"Pan läuft als Windows-Dienst und passt auf: Stürzt Arcadia ab oder hängt, beendet Pan die Emulation und gibt allen Programmen die echten Controller zurück. Ohne Pan emuliert Arcadia nichts. {Admin}", issue.Severity),
        "pan-failing" => ("„Neu einrichten“ behebt das meist",
            $"Pan beendet sich gleich nach dem Start. „Neu einrichten“ installiert Pan neu und legt seinen Ordner neu an. Warum es sich beendet hat, steht in {PanPaths.LogPath}. {Admin}", issue.Severity),
        "hidhide-missing" => ("Versteckt die echten Controller vor anderen Programmen",
            $"HidHide versteckt Geräte vor Programmen. Pan trägt dort deine echten Controller ein, solange Arcadia sie emuliert, und nimmt sie wieder heraus, wenn Arcadia endet. Bis dahin sieht nur Arcadia sie. {Admin}", issue.Severity),
        "hidhide-unsupported" => ($"Arcadia braucht HidHide {HidHideRelease.Version} oder neuer", $"{issue.Detail} {Admin}", issue.Severity),
        "hidhide-inactive" => ("Meist hilft ein Neustart des PCs",
            "HidHide ist installiert, sein Treiber läuft aber noch nicht. Meist hilft ein Neustart des PCs.", issue.Severity),
        "hidhide-interface" => ($"HidHide {HidHideRelease.Version} neu installieren hilft",
            $"Der HidHide-Treiber kennt eine Anfrage nicht, die Pan braucht. HidHide {HidHideRelease.Version} neu installieren hilft: {HidHideRelease.ManualInstallUrl}", issue.Severity),
        "hidhide-busy" => ("Bitte schließe sie. Danach geht es von selbst weiter.",
            "Die HidHide-Konfiguration ist geöffnet. Solange sie offen ist, kann Pan deine Controller nicht überwachen, und Arcadias Controller-Verwaltung ist pausiert. Schließe das Fenster der HidHide-Konfiguration, danach geht es von selbst weiter.", issue.Severity),
        "hidhide-inverse" => ("Schalte „Inverse application cloak“ in HidHide aus",
            "In HidHide ist „Inverse application cloak“ eingeschaltet. Dann versteckt HidHide die Geräte vor Arcadia statt vor allen anderen. Schalte es in der HidHide-Konfiguration aus.", issue.Severity),
        "hidhide-cloaking-off" => ("Sonst würde Pan auch deine anderen Geräte dort verstecken",
            "In HidHide sind Geräte eingetragen, aber „Enable device hiding“ ist aus. Pan müsste es einschalten und würde diese Geräte dann mit verstecken, deshalb tut es das nicht. Schalte es in der HidHide-Konfiguration ein oder trage die Geräte dort aus.", issue.Severity),
        // Nothing to do but wait for the restart: the blue i, not the triangle (designs).
        "reboot-pending" => ("Bis dahin können Controller in Spielen kurz aussetzen",
            "Der usbip-Treiber wurde aktualisiert und ist erst nach einem Neustart des PCs ganz aktiv. Bis dahin kann ein emulierter Controller kurz aussetzen; Arcadia verbindet ihn dann selbst neu.", HealthSeverity.Info),
        "hidhide-removal-pending" => ("Windows entfernt HidHide beim nächsten Neustart",
            "HidHide wurde deinstalliert, Windows entfernt es aber erst beim nächsten Neustart des PCs. Danach lässt es sich hier wieder installieren.", HealthSeverity.Info),
        _ => (issue.ShortText, issue.Detail, issue.Severity),
    };

    /// <summary>What each Steam Controller mode brings, here and on every launcher's page (#178).</summary>
    public static IReadOnlyList<InfoBlock> ModeCards { get; } =
    [
        new InfoCard(ControllerModes.Arcadia, "+ In jedem Spiel, auch ohne Steam\n+ Das Spiel sieht einen Xbox Controller\n− Keine Steam-Belegungen, kein Gyro, keine Trackpads", string.Empty),
        new InfoCard(ControllerModes.Steam, "+ Belegungen aus Steam, auch Community, Gyro und Trackpads\n− Außerhalb von Steam nur Steams Desktop-Belegung", string.Empty),
    ];

    /// <summary>The switch's info panel (T242Settings).</summary>
    private static readonly InfoBlock[] MainInfo =
    [
        new InfoText("Pan ist der Controller-Wächter von Arcadia. Er stellt sicher, dass Arcadia alle Controller im vollen Umfang nutzen kann, und bestimmt, wie andere Launcher, Spiele und Programme mit ihnen umgehen."),
        new InfoText("So funktionieren Arcadias Features wie der Mausmodus oder „Zurück zu Arcadia“ überall, ohne dass andere Launcher ebenfalls darauf reagieren, etwa Steams Desktop-Steuerung. Arcadia wird so zum Kontrollzentrum deiner Controller. Pan überwacht, dass echte und emulierte Controller fehlerfrei zusammenspielen, und gibt deine Controller sofort zurück, falls Arcadia unerwartet beendet wird."),
        new InfoText("Pan versteckt deine echten Controller vor Spielen. Spiele sehen stattdessen für jeden einen emulierten Controller derselben Art, zum Beispiel einen DualSense als DualSense. So kommen Arcadias Tastenkombinationen nie im Spiel an."),
        new InfoSection("STEAM CONTROLLER",
        [
            new InfoText("Von Haus aus funktioniert der Steam Controller nur in Steam als echter Controller. Arcadia betreibt ihn deshalb in zwei Modi, „Steam-Eingabe“ und „Arcadia“, damit er auch außerhalb von Steam funktioniert, zum Beispiel in Spielen aus dem Epic Games Launcher oder dem Xbox Game Pass. Welchen Modus die Spiele eines Launchers bekommen, wählst du auf seiner Seite unter Launcher vor; pro Spiel stellst du ihn in seinen Details um."),
            .. ModeCards,
            new InfoNote("Solange Arcadia den Steam Controller übernommen hat, hält es Firmware-Updates von Steam auf und gibt ihn dafür frei, sobald Steam aufgegeben hat. Läuft Steam beim Start der Emulation, fragt Arcadia vorher, ob gerade ein Update läuft.", HealthSeverity.Info),
        ]),
        new InfoSection("WAS PAN BRAUCHT",
        [
            new InfoText("Pan braucht einige Treiber und Programme von Drittanbietern, die vor dem ersten Start eingerichtet werden:"),
            new InfoCard("usbip-win2", "Erzeugt die emulierten Controller", "github.com/vadimgrn/usbip-win2"),
            new InfoCard("VIIPER", "Leitet die Signale der Controller an Spiele und Programme weiter", "github.com/Alia5/VIIPER"),
            new InfoCard("HidHide", "Versteckt die echten Controller, damit nichts doppelt ankommt", "github.com/nefarius/HidHide"),
            new InfoText("Die Installation fragt einmal nach Administratorrechten. Danach ist ein Neustart des PCs empfohlen."),
        ]),
    ];
}
