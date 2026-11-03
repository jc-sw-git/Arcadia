using System.Globalization;
using Arcadia.App.Services;
using Arcadia.App.ViewModels;
using Arcadia.ControllerSupport;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Settings;

/// <summary>
/// The group "Konto" on a launcher's page (#83), first on the page as the setting that makes the most of the library:
/// signed out "Anmelden"; A opens the launcher's sign-in page over the window, B there goes back; signed in it shows the
/// account's name and "Abmelden", which asks first. Disabled while the launcher is inactive or missing, since without it
/// nothing can be installed; a sign-in stays stored meanwhile (owner, 2026-10-26/27).
/// </summary>
internal sealed class LauncherAccountRow : IDisposable
{
    private const string AccountGroup = "KONTO";

    private readonly ILauncherCategory _category;
    private readonly List<(ILauncherAccount Account, EventHandler Handler)> _accounts = [];

    // The account picture shown last, by its file (AvatarOf).
    private string? _avatarPath;
    private Avalonia.Media.Imaging.Bitmap? _avatar;

    public LauncherAccountRow(ILauncherCategory category)
    {
        _category = category;
        Row = new ActionRowViewModel("launcher-account", string.Empty, Activate) { Group = AccountGroup };
        foreach (var integration in category.Host.Launchers.All)
        {
            if (integration.Account is { } account)
            {
                var launcherId = integration.Launcher.Id;
                // The account changes off the UI thread (a code renewed, approved, the games fetched).
                EventHandler handler = (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (category.PageLauncherId == launcherId)
                    {
                        category.Refresh();
                    }
                });
                account.StateChanged += handler;
                _accounts.Add((account, handler));
            }
        }
    }

    public ActionRowViewModel Row { get; }

    /// <summary>The account of the launcher whose page is shown, while its group "Konto" is there.</summary>
    public ILauncherAccount? PageAccount
    {
        get
        {
            var launchers = _category.Host.Launchers;
            return _category.PageLauncherId is { } id && launchers.ActiveIds.Contains(id) && launchers.IsFound(id) ? launchers.Registered(id)?.Account : null;
        }
    }

    /// <summary>The account's row as its state is now.</summary>
    public void Show(ILauncherAccount account, string launcherName, bool isActive, bool isFound)
    {
        var state = account.State;
        var row = Row;
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
                    new InfoFact("Spiele im Konto", state.OwnedGames is { } count ? count.ToString(CultureInfo.CurrentCulture) : "Noch nicht geladen", IsMuted: state.OwnedGames is null),
                    new InfoFact("Stand", state.ListedAt is { } at ? LauncherTexts.Ago(_category.Time, at) : "–", IsMuted: state.ListedAt is null),
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

    public void Dispose()
    {
        foreach (var (account, handler) in _accounts)
        {
            account.StateChanged -= handler;
        }

        _accounts.Clear();
    }

    private static string FirstSentence(string text) => text.IndexOf(". ", StringComparison.Ordinal) is var end and > 0 ? text[..end] : text.TrimEnd('.');

    /// <summary>A on the account's row: sign in, drop a sign-in under way, or sign out after asking.</summary>
    private void Activate()
    {
        if (PageAccount is not { } account)
        {
            return;
        }

        switch (account.State.Step)
        {
            case AccountStep.SignedIn:
                var launcherName = _category.Host.Launchers.Info(_category.PageLauncherId!).DisplayName;
                _category.Dialog = new ConfirmationViewModel(
                    $"Vom {account.Title} abmelden?",
                    $"Willst du {account.State.AccountName} wirklich in Arcadia abmelden? Arcadia kann dir dann nur noch die Spiele auflisten, die auf diesem PC installiert sind. "
                        + $"Ob du sie auch starten kannst, hängt von den Lizenzen des Benutzers ab, der in {launcherName} angemeldet ist.",
                    "Abmelden",
                    account.SignOut,
                    () => _category.Dialog = null);
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
}
