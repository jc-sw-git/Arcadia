using System.Diagnostics;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Steam.Account;

/// <summary>
/// The Steam account signed in to Arcadia (#83): a QR sign-in approved in Steam's phone app, the tokens kept by
/// <see cref="SteamAccountStore"/>, and the owned games, fetched in the background and cached for a start offline.
/// <see cref="SteamScanner"/> asks <see cref="OwnedGamesFor"/> for the games it adds as not installed. Tokens, the
/// account's ids and names never go into the log.
/// </summary>
public sealed class SteamOnlineAccount : ILauncherAccount, IDisposable
{
    /// <summary>A code nobody approved disappears after this (owner, 2026-10-26).</summary>
    internal static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(2);

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

    private readonly SteamWebApi _api;
    private readonly SteamAccountStore _store;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _shutdown = new();

    private SteamSignIn? _signIn;
    private SteamOwnedList? _owned;
    private string? _problem;
    private string? _notice;
    private LauncherAccountState _state;

    // The sign-in under way; a newer one or a cancel replaces it, and the old one's results are dropped.
    private CancellationTokenSource? _signInRun;
    private Task _signInTask = Task.CompletedTask;
    private Task _fetchTask = Task.CompletedTask;
    private DateTimeOffset? _fetchedAt;

    internal SteamOnlineAccount(SteamWebApi api, SteamAccountStore store, TimeProvider time)
    {
        _api = api;
        _store = store;
        _time = time;
        _signIn = store.LoadSignIn();
        _owned = IsSignedIn ? store.LoadOwned() : null;
        _state = Compose(IsSignedIn ? AccountStep.SignedIn : AccountStep.SignedOut);
    }

    /// <param name="folder">Where the sign-in and the cached games are kept; null: Arcadia's data folder.</param>
    public static SteamOnlineAccount Create(HttpClient http, TimeProvider time, string? folder = null) =>
        new(new SteamWebApi(http), SteamAccountStore.CreateDefault(folder), time);

    public string Title => "Steam-Konto";

    public string Purpose => "Melde dich an, um auch nicht installierte Spiele zu sehen";

    public string About => "Mit deinem Steam-Konto zeigt Arcadia auch die Spiele, die du besitzt, aber nicht installiert hast; [A] installiert sie über Steam. "
        + "Angemeldet wird wie beim Steam Deck mit einem QR-Code, den du in der Steam-App auf dem Handy bestätigst. Arcadia sieht dein Passwort nie.";

    public string Requirement => "Benötigt die Steam-App auf dem Handy";

    public string CodeInstruction => "Öffne die Steam-App auf dem Handy, tippe unten auf das Schild (Steam Guard) und dann auf „QR-Code scannen“. Bestätige danach die Anmeldung.";

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

    private bool IsSignedIn => _signIn is { RefreshToken: not null };

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

        SetStep(AccountStep.Starting);
        lock (_lock)
        {
            _signInTask = Run(async () =>
            {
                using (run)
                {
                    await SignInAsync(run).ConfigureAwait(false);
                }
            }, "Anmeldung");
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
            _problem = null;
            _notice = null;
            _fetchedAt = null;
            _store.Clear(null);
        }

        Trace.TraceInformation("Steam-Konto: abgemeldet, gespeicherte Anmeldung und Liste gelöscht");
        SetStep(AccountStep.SignedOut);
        LibraryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The owned games for Steam's library, or null if none are to be added: signed out, not fetched yet, or Steam is signed
    /// in with another account (<paramref name="clientAccountId"/>, null or 0 when unknown), which <c>steam://install</c>
    /// would install into; then <see cref="LauncherAccountState.Notice"/> says so.
    /// </summary>
    internal IReadOnlyList<SteamOwnedGame>? OwnedGamesFor(uint? clientAccountId)
    {
        bool noticeChanged;
        IReadOnlyList<SteamOwnedGame>? games;
        lock (_lock)
        {
            var notice = IsSignedIn && clientAccountId is { } client and not 0 && client != _signIn!.SteamId - SteamId64Base ? OtherAccountNotice : null;
            noticeChanged = notice != _notice;
            _notice = notice;
            games = IsSignedIn && notice is null ? _owned?.Games : null;
        }

        if (noticeChanged)
        {
            if (_notice is not null)
            {
                Trace.TraceInformation("Steam-Konto: Steam ist mit einem anderen Konto angemeldet, nicht installierte Spiele ausgeblendet");
            }

            SetStep(State.Step);
        }

        return games;
    }

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
            if (!IsSignedIn || !_fetchTask.IsCompleted || (!force && _fetchedAt is { } last && now - last < FetchPause))
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
        if (!_shutdown.IsCancellationRequested)
        {
            _shutdown.Cancel();
        }
    }

    private async Task SignInAsync(CancellationTokenSource run)
    {
        var cancellationToken = run.Token;
        SteamQrSession session;
        try
        {
            session = await _api.BeginQrSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (IsTransient(e, cancellationToken))
        {
            Trace.TraceWarning($"Steam-Konto: Anmeldung nicht möglich, Steam nicht erreichbar ({Describe(e)})");
            SetStepFor(run, AccountStep.Unreachable);
            return;
        }

        Trace.TraceInformation("Steam-Konto: QR-Code angezeigt");
        var clientId = session.ClientId;
        SetStepFor(run, AccountStep.ShowingCode, session.ChallengeUrl);
        var started = _time.GetUtcNow();
        while (true)
        {
            await Task.Delay(session.Interval, _time, cancellationToken).ConfigureAwait(false);
            if (_time.GetUtcNow() - started >= CodeLifetime)
            {
                Trace.TraceInformation("Steam-Konto: QR-Code abgelaufen");
                SetStepFor(run, AccountStep.CodeExpired);
                return;
            }

            SteamPollResult poll;
            try
            {
                poll = await _api.PollAsync(clientId, session.RequestId, cancellationToken).ConfigureAwait(false);
            }
            catch (SteamApiException e) when (!e.IsTemporary)
            {
                // Steam ends the session when the phone rejects it; busy or unavailable is only a poll lost.
                Trace.TraceInformation($"Steam-Konto: Anmeldung abgelehnt (EResult {e.EResult})");
                SetStepFor(run, AccountStep.CodeRejected);
                return;
            }
            catch (Exception e) when (IsTransient(e, cancellationToken))
            {
                // A poll lost on the way: the next one may get through, until the code expires.
                Trace.TraceWarning($"Steam-Konto: Abfrage fehlgeschlagen ({Describe(e)})");
                continue;
            }

            clientId = poll.NewClientId ?? clientId;
            if (poll.NewChallengeUrl is { } renewed)
            {
                SetStepFor(run, AccountStep.ShowingCode, renewed);
            }

            if (poll.RefreshToken is { } refreshToken)
            {
                await CompleteSignInAsync(run, refreshToken, poll).ConfigureAwait(false);
                return;
            }
        }
    }

    private async Task CompleteSignInAsync(CancellationTokenSource run, string refreshToken, SteamPollResult poll)
    {
        var cancellationToken = run.Token;
        if (SteamToken.SteamIdOf(refreshToken) is not { } steamId)
        {
            Trace.TraceWarning("Steam-Konto: Token ohne Konto erhalten");
            SetStepFor(run, AccountStep.Unreachable);
            return;
        }

        var accessToken = poll.AccessToken;
        string? name = null;
        try
        {
            accessToken ??= await _api.GenerateAccessTokenAsync(refreshToken, steamId, cancellationToken).ConfigureAwait(false);
            name = await _api.GetProfileNameAsync(accessToken, steamId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (IsTransient(e, cancellationToken))
        {
            // The sign-in holds; the name falls back to the login name, the games come with the next fetch.
            Trace.TraceWarning($"Steam-Konto: Profilname nicht geladen ({Describe(e)})");
        }

        lock (_lock)
        {
            if (_signInRun != run)
            {
                return;
            }

            _signInRun = null;
            _signIn = new SteamSignIn(refreshToken, accessToken, steamId, Clip(name ?? poll.AccountName ?? "Steam"));
            _owned = null;
            _problem = null;
            _fetchedAt = null;
            _store.Clear(_signIn);
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
        IReadOnlyList<SteamOwnedGame> games;
        try
        {
            var accessToken = signIn.AccessToken is { } cached && SteamToken.ExpiresAt(cached) is { } expires && expires - _time.GetUtcNow() > TimeSpan.FromMinutes(5)
                ? cached
                : null;
            try
            {
                games = await _api.GetOwnedGamesAsync(accessToken ?? await NewAccessTokenAsync(signIn, cancellationToken).ConfigureAwait(false), signIn.SteamId, cancellationToken).ConfigureAwait(false);
            }
            catch (SteamApiException e) when (e.IsSignInGone && accessToken is not null)
            {
                // The stored access token may have been revoked before it expired: one more try with a new one.
                games = await _api.GetOwnedGamesAsync(await NewAccessTokenAsync(signIn, cancellationToken).ConfigureAwait(false), signIn.SteamId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SteamApiException e) when (e.IsSignInGone)
        {
            Trace.TraceWarning($"Steam-Konto: Anmeldung abgelaufen ({Describe(e)})");
            lock (_lock)
            {
                if (_signIn?.SteamId != signIn.SteamId)
                {
                    return;
                }

                _signIn = signIn with { RefreshToken = null, AccessToken = null, Expired = true };
                _owned = null;
                _store.Clear(_signIn);
            }

            SetStep(AccountStep.SignedOut);
            LibraryChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        catch (Exception e) when (IsTransient(e, cancellationToken))
        {
            Trace.TraceWarning($"Steam-Konto: Spiele nicht geladen ({Describe(e)}), es gilt die zuletzt geladene Liste");
            lock (_lock)
            {
                _problem = _owned is null ? UnreachableWithoutList : UnreachableWithList;
            }

            SetStep(State.Step);
            return;
        }

        bool changed;
        lock (_lock)
        {
            if (_signIn?.SteamId != signIn.SteamId || !IsSignedIn)
            {
                return;
            }

            changed = _owned is null || !_owned.Games.SequenceEqual(games);
            _owned = new SteamOwnedList(_time.GetUtcNow(), games);
            _problem = null;
            _store.SaveOwned(_owned);
        }

        Trace.TraceInformation($"Steam-Konto: {games.Count} Spiele in {watch.ElapsedMilliseconds} ms geladen{(changed ? string.Empty : ", unverändert")}");
        SetStep(State.Step);
        if (changed)
        {
            LibraryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task<string> NewAccessTokenAsync(SteamSignIn signIn, CancellationToken cancellationToken)
    {
        var accessToken = await _api.GenerateAccessTokenAsync(signIn.RefreshToken!, signIn.SteamId, cancellationToken).ConfigureAwait(false);
        lock (_lock)
        {
            if (_signIn?.SteamId == signIn.SteamId && IsSignedIn)
            {
                _signIn = _signIn with { AccessToken = accessToken };
                _store.SaveSignIn(_signIn);
            }
        }

        return accessToken;
    }

    /// <summary>A step of the sign-in <paramref name="run"/>, unless a newer one or a cancel replaced it.</summary>
    private void SetStepFor(CancellationTokenSource run, AccountStep step, string? code = null)
    {
        // Checked and set under one lock, so a run cancelled meanwhile can't overwrite a newer one's step.
        lock (_lock)
        {
            if (_signInRun != run)
            {
                return;
            }

            if (step is not (AccountStep.Starting or AccountStep.ShowingCode))
            {
                _signInRun = null;
            }

            _state = Compose(step, code);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetStep(AccountStep step, string? code = null)
    {
        lock (_lock)
        {
            _state = Compose(step, code);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private LauncherAccountState Compose(AccountStep step, string? code = null) => new(step)
    {
        AccountName = IsSignedIn ? _signIn!.Name : null,
        Code = step == AccountStep.ShowingCode ? code : null,
        Problem = IsSignedIn ? _problem : _signIn is { Expired: true } && step == AccountStep.SignedOut ? ExpiredProblem : null,
        Notice = IsSignedIn ? _notice : null,
        OwnedGames = IsSignedIn ? _owned?.Games.Count : null,
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
            // Whoever listens logs it with its stack trace; else at least the log has it.
            if (Failed is { } failed)
            {
                failed(this, e);
            }
            else
            {
                Trace.TraceError($"Steam-Konto: {what}: {e}");
            }
        }
    });

    /// <summary>Steam not reachable or its answer unusable: a timeout counts, a cancel by the user doesn't.</summary>
    private static bool IsTransient(Exception e, CancellationToken cancellationToken) =>
        e is HttpRequestException or SteamApiException or FormatException or System.Text.Json.JsonException
        || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private static string Describe(Exception e) => e is SteamApiException ? e.Message : e.GetType().Name;

    private static string Clip(string name) => name.Length <= 64 ? name : name[..64];
}
