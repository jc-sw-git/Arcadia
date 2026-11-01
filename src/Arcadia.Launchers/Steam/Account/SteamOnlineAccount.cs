using System.Diagnostics;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// The Steam account signed in to Arcadia (#83): a sign-in on Steam's own store page in Arcadia's browser
/// (<see cref="IWebPages"/>, <see cref="SteamStorePage"/>), the account kept by <see cref="SteamAccountStore"/>, and the
/// owned games, fetched in the background and cached for a start offline.
/// <see cref="SteamScanner"/> asks <see cref="OwnedGamesFor"/> for the games it adds as not installed. Tokens, the
/// account's ids and names never go into the log.
/// </summary>
public sealed class SteamOnlineAccount : ILauncherAccount, IDisposable
{
    /// <summary>How often the account Steam runs with is looked at, so a switch in Steam shows at once (#83, finding 10).</summary>
    internal static readonly TimeSpan ClientCheckInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The owned games are fetched at most this often: every rescan asks (Arcadia back in front, a game ended), but a
    /// list that changes only with a purchase needn't be fetched every few seconds.
    /// </summary>
    internal static readonly TimeSpan FetchPause = TimeSpan.FromSeconds(30);

    private const ulong SteamId64Base = 76561197960265728;

    internal const string OtherAccountNotice = "Steam ist mit einem anderen Konto angemeldet";

    internal const string UnreachableWithList = "Steam ist nicht erreichbar. Arcadia zeigt die zuletzt geladene Liste deiner Spiele.";

    internal const string UnreachableWithoutList = "Steam ist nicht erreichbar. Deine nicht installierten Spiele fehlen, bis Arcadia ihre Liste laden kann.";

    internal const string ExpiredProblem = "Die Anmeldung ist abgelaufen. Melde dich neu an, um wieder alle Spiele zu sehen.";

    /// <summary>A stored token closer to its end than this is read anew before it is used.</summary>
    private static readonly TimeSpan TokenMargin = TimeSpan.FromMinutes(5);

    private readonly SteamWebApi _api;
    private readonly IWebPages? _web;
    private readonly SteamAccountStore _store;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _shutdown = new();

    private SteamSignIn? _signIn;
    private SteamOwnedList? _owned;
    private string? _avatar;
    private string? _problem;
    private string? _notice;
    private LauncherAccountState _state;

    // The sign-in under way; a newer one or a cancel replaces it, and the old one's results are dropped.
    private CancellationTokenSource? _signInRun;
    private Task _signInTask = Task.CompletedTask;
    private Task _fetchTask = Task.CompletedTask;
    private DateTimeOffset? _fetchedAt;

    // The account the running Steam is signed in with, as last seen; 0 or null while Steam isn't running.
    private Func<uint?>? _activeClient;
    private uint? _lastClient;
    private ITimer? _clientCheck;

    /// <param name="web">Arcadia's browser; null: no sign-in possible (the page says Steam can't be reached).</param>
    internal SteamOnlineAccount(SteamWebApi api, SteamAccountStore store, IWebPages? web, TimeProvider time)
    {
        _api = api;
        _store = store;
        _web = web;
        _time = time;
        _signIn = store.LoadSignIn();
        if (_signIn is { RefreshToken: not null })
        {
            // The first #83 build posed as Steam's phone app, and Steam then restricted the owner's account as possibly
            // stolen (2026-10-27): its token is deleted, never used; the user signs in again on the store page.
            Trace.TraceWarning("Steam-Konto: Anmeldung der ersten Version (als Handy-App) gelöscht");
            store.Clear(null);
            _signIn = null;
        }

        _owned = IsSignedIn ? store.LoadOwned() : null;
        _avatar = IsSignedIn ? store.AvatarFile : null;
        _state = Compose(IsSignedIn ? AccountStep.SignedIn : AccountStep.SignedOut);
    }

    /// <param name="folder">Where the sign-in and the cached games are kept; null: Arcadia's data folder.</param>
    public static SteamOnlineAccount Create(HttpClient http, IWebPages web, TimeProvider time, string? folder = null) =>
        new(new SteamWebApi(http), SteamAccountStore.CreateDefault(folder), web, time);

    public string Title => "Steam-Konto";

    public string Purpose => "Melde dich an, um vollen Zugriff auf deine Spielebibliothek zu haben";

    public string About => "Hinterlegst du in Arcadia dein Steam-Konto, erhältst du auch in Arcadia vollen Zugriff auf deine Spielebibliothek. "
        + "Ohne Anmeldung kann Arcadia nur die Spiele auflisten, die auf diesem PC installiert sind. "
        + "Ob du sie auch erfolgreich aus Arcadia starten kannst, hängt davon ab, ob das Konto, das aktuell in Steam angemeldet ist, die Berechtigung dafür hat.";

    public string SignInMethod => "Die Anmeldung erfolgt auf der Anmeldeseite von Steam, per QR-Code mit der Steam Mobile App oder mit Name und Passwort. Arcadia sieht deine Anmelde-Informationen nie.";

    public LauncherAccountState State
    {
        get
        {
            lock (_lock)
            {
                return _state;
            }
        }
    }

    public event EventHandler? StateChanged;

    public event EventHandler? LibraryChanged;

    public event EventHandler<Exception>? Failed;

    private bool IsSignedIn => _signIn is { Expired: false, RefreshToken: null };

    public void BeginSignIn()
    {
        CancellationTokenSource run;
        lock (_lock)
        {
            if (IsSignedIn)
            {
                return;
            }

            _signInRun?.Cancel();
            run = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            _signInRun = run;
        }

        SetStep(AccountStep.SigningIn);
        lock (_lock)
        {
            _signInTask = Run(async () =>
            {
                try
                {
                    await SignInAsync(run).ConfigureAwait(false);
                }
                finally
                {
                    EndSignIn(run);
                }
            }, "Anmeldung");
        }
    }

    /// <summary>
    /// Lets go of the sign-in <paramref name="run"/> however it ended. The normal exits have already let go; after an
    /// unexpected exception the run would stay current with its source disposed, so B on the launcher page would throw
    /// and "Anmeldung läuft" would stay until Arcadia restarts (#368).
    /// </summary>
    private void EndSignIn(CancellationTokenSource run)
    {
        bool stillCurrent;
        lock (_lock)
        {
            // Cleared before the dispose below, so a cancel or a newer sign-in never touches a disposed source.
            stillCurrent = _signInRun == run;
            if (stillCurrent)
            {
                _signInRun = null;
                _state = Compose(IsSignedIn ? AccountStep.SignedIn : AccountStep.SignedOut);
            }
        }

        run.Dispose();
        if (stillCurrent)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void CancelSignIn()
    {
        lock (_lock)
        {
            _signInRun?.Cancel();
            _signInRun = null;
            if (!_state.IsSigningIn)
            {
                return;
            }
        }

        Trace.TraceInformation("Steam-Konto: Anmeldung abgebrochen");
        SetStep(IsSignedIn ? AccountStep.SignedIn : AccountStep.SignedOut);
    }

    public void SignOut()
    {
        lock (_lock)
        {
            _signInRun?.Cancel();
            _signInRun = null;
            _signIn = null;
            _owned = null;
            _avatar = null;
            _problem = null;
            _notice = null;
            _fetchedAt = null;
            _store.Clear(null);
        }

        // Steam's cookies in Arcadia's browser are the sign-in itself: without deleting them, the store page stays signed in.
        if (_web is { } web)
        {
            Run(async () =>
            {
                var forgotten = await web.ForgetAsync(SteamStorePage.Domains, _shutdown.Token).ConfigureAwait(false);
                if (forgotten.Error is { } error)
                {
                    Trace.TraceWarning($"Steam-Konto: Cookies nicht gelöscht ({error})");
                }
            }, "Abmelden");
        }

        Trace.TraceInformation("Steam-Konto: abgemeldet, gespeicherte Anmeldung und Liste gelöscht");
        SetStep(AccountStep.SignedOut);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The owned apps for Steam's library, or null while signed out. <paramref name="activeClient"/> is the account the
    /// running Steam is signed in with (null or 0: Steam isn't running). Another account there still shows the games, but
    /// <c>steam://install</c> would install into that account and a start may fail or use other saves
    /// (owner, 2026-10-27), so <see cref="SteamOwnedApps.SteamHasOtherAccount"/> says so and
    /// <see cref="LauncherAccountState.Notice"/> too.
    /// </summary>
    internal SteamOwnedApps? OwnedGamesFor(uint? activeClient)
    {
        bool noticeChanged;
        SteamOwnedApps? apps;
        lock (_lock)
        {
            var other = IsOtherAccount(activeClient);
            var notice = other ? OtherAccountNotice : null;
            noticeChanged = notice != _notice;
            _notice = notice;
            _lastClient = activeClient;
            apps = IsSignedIn ? new SteamOwnedApps(_owned?.Games ?? [], other, _owned?.Packages ?? []) : null;
        }

        if (noticeChanged)
        {
            if (_notice is not null)
            {
                Trace.TraceInformation("Steam-Konto: Steam ist mit einem anderen Konto angemeldet");
            }

            SetStep(State.Step);
        }

        return apps;
    }

    /// <summary>
    /// Looks every <see cref="ClientCheckInterval"/> at the account the running Steam is signed in with (null or 0: not
    /// running) and asks for a rescan when it changed, so a switch in Steam shows without waiting for the next rescan.
    /// </summary>
    internal void FollowClient(Func<uint?> activeClient)
    {
        lock (_lock)
        {
            _activeClient = activeClient;
            _clientCheck ??= _time.CreateTimer(_ => CheckClient(), null, ClientCheckInterval, ClientCheckInterval);
        }
    }

    private void CheckClient()
    {
        try
        {
            Func<uint?>? read;
            lock (_lock)
            {
                read = IsSignedIn && !_shutdown.IsCancellationRequested ? _activeClient : null;
            }

            if (read?.Invoke() is var client && read is not null && Normal(client) != Normal(_lastClient))
            {
                lock (_lock)
                {
                    _lastClient = client;
                }

                LibraryChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception e)
        {
            Report(e, "Steam-Konto prüfen");
        }

        static uint Normal(uint? client) => client ?? 0;
    }

    private bool IsOtherAccount(uint? activeClient) =>
        IsSignedIn && activeClient is { } client and not 0 && client != _signIn!.SteamId - SteamId64Base;

    /// <summary>
    /// Fetches the owned games in the background, unless that happened in the last <see cref="FetchPause"/> or is under
    /// way. A changed list raises <see cref="LibraryChanged"/>; a failed fetch keeps the cached list and is a status message.
    /// </summary>
    internal void FetchSoon() => Fetch(force: false);

    public void RefreshNow() => Fetch(force: true);

    private void Fetch(bool force)
    {
        lock (_lock)
        {
            var now = _time.GetUtcNow();
            if (_web is null || !IsSignedIn || !_fetchTask.IsCompleted || (!force && _fetchedAt is { } last && now - last < FetchPause))
            {
                return;
            }

            _fetchedAt = now;
            _fetchTask = Run(FetchAsync, "Spiele laden");
        }
    }

    /// <summary>The sign-in and fetch under way, for tests.</summary>
    internal Task WhenIdleAsync()
    {
        lock (_lock)
        {
            return Task.WhenAll(_signInTask, _fetchTask);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _clientCheck?.Dispose();
            _clientCheck = null;
        }

        if (!_shutdown.IsCancellationRequested)
        {
            _shutdown.Cancel();
        }
    }

    private async Task SignInAsync(CancellationTokenSource run)
    {
        var cancellationToken = run.Token;
        if (_web is null)
        {
            SetStepFor(run, AccountStep.Unreachable);
            return;
        }

        Trace.TraceInformation("Steam-Konto: Anmeldeseite geöffnet");
        var shown = await _web.ShowAsync(
            "Bei Steam anmelden",
            SteamStorePage.LoginPage, SteamStorePage.Domains, SteamStorePage.AccountScript, page => SteamStorePage.SignInOf(page) is not null, cancellationToken).ConfigureAwait(false);
        if (shown.IsCancelled)
        {
            Trace.TraceInformation("Steam-Konto: Anmeldung abgebrochen");
            SetStepFor(run, AccountStep.SignedOut);
            return;
        }

        if (shown.Text is not { } page || SteamStorePage.SignInOf(page) is not { } signIn)
        {
            Trace.TraceWarning($"Steam-Konto: Anmeldeseite nicht geladen ({shown.Error})");
            SetStepFor(run, AccountStep.Unreachable);
            return;
        }

        var profile = await _api.GetProfileAsync(signIn.WebApiToken, signIn.SteamId, cancellationToken).ConfigureAwait(false);
        if (profile.Error is { IsCancelled: false } error)
        {
            // The sign-in holds; the row shows "Steam" until a later sign-in gets the name.
            Trace.TraceWarning($"Steam-Konto: Profilname nicht geladen ({error.Message})");
        }

        var avatar = profile.Value?.AvatarHash is { } avatarHash ? await _api.GetAvatarAsync(avatarHash, cancellationToken).ConfigureAwait(false) : null;
        if (avatar?.Error is { IsCancelled: false } avatarError)
        {
            Trace.TraceWarning($"Steam-Konto: Avatar nicht geladen ({avatarError.Message})");
        }

        lock (_lock)
        {
            if (_signInRun != run)
            {
                return;
            }

            _signInRun = null;
            _signIn = new SteamSignIn(signIn.WebApiToken, signIn.SteamId, Clip(profile.Value?.Name ?? "Steam"));
            _owned = null;
            _problem = null;
            _fetchedAt = null;
            _store.Clear(_signIn);
            _avatar = _store.SaveAvatar(avatar?.Value);
        }

        Trace.TraceInformation("Steam-Konto: angemeldet");
        SetStep(AccountStep.SignedIn);
        FetchSoon();
    }

    private async Task FetchAsync()
    {
        var cancellationToken = _shutdown.Token;
        SteamSignIn signIn;
        lock (_lock)
        {
            if (!IsSignedIn)
            {
                return;
            }

            signIn = _signIn!;
        }

        var watch = Stopwatch.StartNew();
        var cached = signIn.AccessToken is { } stored && SteamToken.ExpiresAt(stored) is { } expires && expires - _time.GetUtcNow() > TokenMargin
            ? stored
            : null;
        var accessToken = cached is not null ? SteamApiResult<string>.Ok(cached) : await NewAccessTokenAsync(signIn, cancellationToken).ConfigureAwait(false);
        var listed = accessToken.Value is { } token
            ? await _api.GetOwnedGamesAsync(token, signIn.SteamId, cancellationToken).ConfigureAwait(false)
            : accessToken.Error!;
        if (listed.Error is { IsSignInGone: true } && cached is not null)
        {
            // The stored access token may have been revoked before it expired: one more try with a new one.
            accessToken = await NewAccessTokenAsync(signIn, cancellationToken).ConfigureAwait(false);
            listed = accessToken.Value is { } renewed
                ? await _api.GetOwnedGamesAsync(renewed, signIn.SteamId, cancellationToken).ConfigureAwait(false)
                : accessToken.Error!;
        }

        if (listed.Value is not { } games)
        {
            var error = listed.Error!;
            if (error.IsCancelled)
            {
                return;
            }

            if (error.IsSignInGone)
            {
                SignInGone(signIn, error);
                return;
            }

            Trace.TraceWarning($"Steam-Konto: Spiele nicht geladen ({error.Message}), es gilt die zuletzt geladene Liste");
            lock (_lock)
            {
                _problem = _owned is null ? UnreachableWithoutList : UnreachableWithList;
            }

            SetStep(State.Step);
            return;
        }

        var userData = await _web!.ReadAsync(SteamStorePage.UserDataPage, SteamStorePage.Domains, SteamStorePage.TextScript, text => SteamStorePage.OwnershipOf(text) is not null, cancellationToken).ConfigureAwait(false);
        var apps = userData.Text is { } text ? SteamStorePage.OwnershipOf(text) : null;
        IReadOnlyList<SteamOwnedGame> owned;
        IReadOnlyList<uint> packages;
        lock (_lock)
        {
            owned = WithStoreApps(games, apps?.Apps ?? StoreOnly(_owned));
            packages = apps?.Packages ?? _owned?.Packages ?? [];
        }

        if (apps is null && !userData.IsCancelled)
        {
            Trace.TraceWarning($"Steam-Konto: Store-Liste nicht geladen ({userData.Error ?? "keine Apps"}), Soundtracks und Werkzeuge wie zuletzt");
        }

        bool changed;
        lock (_lock)
        {
            if (_signIn?.SteamId != signIn.SteamId || !IsSignedIn)
            {
                return;
            }

            changed = _owned is null || !_owned.Games.SequenceEqual(owned) || !(_owned.Packages ?? []).SequenceEqual(packages);
            _owned = new SteamOwnedList(_time.GetUtcNow(), owned, packages);
            _problem = null;
            _store.SaveOwned(_owned);
        }

        Trace.TraceInformation($"Steam-Konto: {games.Count} Spiele und {owned.Count - games.Count} weitere Apps in {watch.ElapsedMilliseconds} ms geladen{(changed ? string.Empty : ", unverändert")}");
        SetStep(State.Step);
        if (changed)
        {
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// A new web API token from the store page, which Arcadia's browser keeps signed in. A page signed out (or in with
    /// another account) means the sign-in is gone; a page that doesn't load, that Steam can't be reached.
    /// </summary>
    private async Task<SteamApiResult<string>> NewAccessTokenAsync(SteamSignIn signIn, CancellationToken cancellationToken)
    {
        var read = await _web!.ReadAsync(SteamStorePage.StorePage, SteamStorePage.Domains, SteamStorePage.AccountScript, page => SteamStorePage.SignInOf(page) is not null, cancellationToken).ConfigureAwait(false);
        if (read.IsCancelled)
        {
            return new SteamApiError("Store-Seite: abgebrochen", IsCancelled: true);
        }

        if (read.Text is not { } page)
        {
            return new SteamApiError($"Store-Seite: {read.Error}", IsNetwork: true);
        }

        if (SteamStorePage.SignInOf(page) is not { } current || current.SteamId != signIn.SteamId)
        {
            return new SteamApiError("Store-Seite: nicht angemeldet", Status: System.Net.HttpStatusCode.Unauthorized);
        }

        lock (_lock)
        {
            if (_signIn?.SteamId == signIn.SteamId && IsSignedIn)
            {
                _signIn = _signIn with { AccessToken = current.WebApiToken };
                _store.SaveSignIn(_signIn);
            }
        }

        return SteamApiResult<string>.Ok(current.WebApiToken);
    }

    /// <summary>The store page is no longer signed in: signed out, with a status message saying why.</summary>
    private void SignInGone(SteamSignIn signIn, SteamApiError error)
    {
        Trace.TraceWarning($"Steam-Konto: Anmeldung abgelaufen ({error.Message})");
        lock (_lock)
        {
            if (_signIn?.SteamId != signIn.SteamId)
            {
                return;
            }

            _signIn = signIn with { AccessToken = null, Expired = true };
            _owned = null;
            _store.Clear(_signIn);
        }

        SetStep(AccountStep.SignedOut);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The games with name and playtime, then every other app the store lists, nameless: <c>appinfo.vdf</c> names and
    /// types them, and leaves out DLC and the like.
    /// </summary>
    private static IReadOnlyList<SteamOwnedGame> WithStoreApps(IReadOnlyList<SteamOwnedGame> games, IReadOnlyList<uint> storeApps)
    {
        var listed = games.Select(game => game.AppId).ToHashSet();
        return [.. games, .. storeApps.Where(listed.Add).Order().Select(appId => new SteamOwnedGame(appId, null, 0, 0))];
    }

    private static IReadOnlyList<uint> StoreOnly(SteamOwnedList? owned) =>
        owned?.Games.Where(game => game.Name is null).Select(game => game.AppId).ToList() ?? [];

    /// <summary>A step of the sign-in <paramref name="run"/>, unless a newer one or a cancel replaced it.</summary>
    private void SetStepFor(CancellationTokenSource run, AccountStep step)
    {
        // Checked and set under one lock, so a run cancelled meanwhile can't overwrite a newer one's step.
        lock (_lock)
        {
            if (_signInRun != run)
            {
                return;
            }

            if (step != AccountStep.SigningIn)
            {
                _signInRun = null;
            }

            _state = Compose(step);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetStep(AccountStep step)
    {
        lock (_lock)
        {
            _state = Compose(step);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private LauncherAccountState Compose(AccountStep step) => new(step)
    {
        AccountName = IsSignedIn ? _signIn!.Name : null,
        AvatarPath = IsSignedIn ? _avatar : null,
        Problem = IsSignedIn ? _problem : _signIn is { Expired: true } && step == AccountStep.SignedOut ? ExpiredProblem : null,
        Notice = IsSignedIn ? _notice : null,
        OwnedGames = IsSignedIn ? _owned?.Games.Count(game => game.Name is not null) : null,
        ListedAt = IsSignedIn ? _owned?.ListedAt : null,
    };

    /// <summary>Work nobody awaits: an unexpected error is logged and reported through <see cref="Failed"/>, never thrown.</summary>
    private Task Run(Func<Task> work, string what) => Task.Run(async () =>
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled or Arcadia closing.
        }
        catch (Exception e)
        {
            Report(e, what);
        }
    });

    /// <summary>Whoever listens logs it with its stack trace; else at least the log has it.</summary>
    private void Report(Exception e, string what)
    {
        if (Failed is { } failed)
        {
            failed(this, e);
        }
        else
        {
            Trace.TraceError($"Steam-Konto: {what}: {e}");
        }
    }

    private static string Clip(string name) => name.Length <= 64 ? name : name[..64];
}

/// <summary>What the account adds to Steam's library (#83).</summary>
/// <param name="Owned">The owned apps; nameless ones are named and typed by <c>appinfo.vdf</c>.</param>
/// <param name="SteamHasOtherAccount">The running Steam is signed in with another account than Arcadia.</param>
/// <param name="Packages">The account's packages; their apps come from Steam's <c>packageinfo.vdf</c>.</param>
internal sealed record SteamOwnedApps(IReadOnlyList<SteamOwnedGame> Owned, bool SteamHasOtherAccount, IReadOnlyList<uint> Packages);
