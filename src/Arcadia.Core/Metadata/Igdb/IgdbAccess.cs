namespace Arcadia.Core.Metadata.Igdb;

/// <summary>What Arcadia knows about the IGDB access (#494).</summary>
public enum IgdbAccessState
{
    /// <summary>No client id, no secret or only one of them: Arcadia sends Twitch and IGDB no request at all.</summary>
    None,

    /// <summary>Both values, not answered for yet (just loaded, or no connection at their check): they are used.</summary>
    Unchecked,

    Valid,

    /// <summary>Twitch or IGDB turned it down: a warning, and IGDB is skipped until the access changes.</summary>
    Rejected,
}

/// <summary>
/// The player's IGDB access (#494): Twitch's client id and secret, entered, pasted, tested, deleted, and the token got
/// for them, kept in memory only. Every request to IGDB goes through it, so none goes without a usable access; a token
/// IGDB turns down is renewed once, then the access counts as rejected. Thread-safe; <see cref="Changed"/> is raised on the
/// thread that changed it.
/// </summary>
public sealed class IgdbAccess(IgdbAccessStore store, IgdbClient client, TimeProvider time)
{
    public const int MinLength = 20;

    public const int MaxLength = 64;

    private readonly Lock _lock = new();
    private bool _loaded;
    private string? _clientId;
    private string? _secret;
    private IgdbToken? _token;
    private IgdbAccessState _state;
    private (DateTimeOffset At, IgdbOutcome Outcome)? _lastCheck;

    public event EventHandler? Changed;

    public IgdbAccessState State
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _state;
            }
        }
    }

    /// <summary>A request may go: both values are there and nobody turned them down.</summary>
    public bool IsUsable => State is IgdbAccessState.Unchecked or IgdbAccessState.Valid;

    /// <summary>The access lasts across a restart (Windows' DPAPI); else only until Arcadia closes.</summary>
    public bool Persists => store.Persists;

    /// <summary>Whether the last change was written as the store keeps it; false: it lasts only until Arcadia closes.</summary>
    public bool IsStored { get; private set; } = true;

    /// <summary>The client id's last four characters, for "endet auf …"; null without one.</summary>
    public string? ClientIdEnding => Locked(() => _clientId is null ? null : IgdbClient.Ending(_clientId));

    /// <summary>The secret's last four characters; null without one.</summary>
    public string? SecretEnding => Locked(() => _secret is null ? null : IgdbClient.Ending(_secret));

    /// <summary>When the access was last checked (entered or "Verbindung testen"), and its answer; null before.</summary>
    public (DateTimeOffset At, IgdbOutcome Outcome)? LastCheck => Locked(() => _lastCheck);

    /// <summary>
    /// The value in <paramref name="text"/> without spaces and line breaks around it; null unless it is 20 to 64 letters and
    /// digits (D2), so nothing else reaches a header, a request body, a file or a log.
    /// </summary>
    public static string? Normalize(string? text)
    {
        var value = text?.Trim();
        return value is { Length: >= MinLength and <= MaxLength } && value.All(char.IsAsciiLetterOrDigit) ? value : null;
    }

    /// <summary>Saves the client id; with a secret there too, checks the access at once (one token request).</summary>
    /// <returns>The check's answer; null when the secret is still missing (nothing is sent).</returns>
    /// <exception cref="ArgumentException">Not a value (<see cref="Normalize"/> first).</exception>
    public Task<IgdbOutcome?> SetClientIdAsync(string clientId, CancellationToken cancel = default) =>
        SetAsync(Normalize(clientId) ?? throw new ArgumentException("Not a client id", nameof(clientId)), null, cancel);

    /// <summary>Saves the secret; with a client id there too, checks the access at once.</summary>
    /// <exception cref="ArgumentException">Not a value.</exception>
    public Task<IgdbOutcome?> SetSecretAsync(string secret, CancellationToken cancel = default) =>
        SetAsync(null, Normalize(secret) ?? throw new ArgumentException("Not a secret", nameof(secret)), cancel);

    /// <summary>"Zugang löschen": both values are gone, no request goes from now on.</summary>
    /// <returns>False when the stored access couldn't be deleted: it is gone until Arcadia closes.</returns>
    public bool Clear()
    {
        bool deleted;
        lock (_lock)
        {
            EnsureLoaded();
            deleted = store.Save(null, null);
            (_clientId, _secret, _token, _state, _lastCheck) = (null, null, null, IgdbAccessState.None, null);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return deleted;
    }

    /// <summary>"Verbindung testen": one token request; null without both values (nothing is sent).</summary>
    public async Task<IgdbOutcome?> TestAsync(CancellationToken cancel = default) =>
        Locked(() => _clientId is { } id && _secret is { } secret ? (id, secret) : ((string, string)?)null) is { } access
            ? await CheckAsync(access.Item1, access.Item2, cancel)
            : null;

    /// <summary>
    /// The games of <paramref name="query"/>; <see cref="IgdbOutcome.Rejected"/> without a usable access, with no request.
    /// A token IGDB turns down is renewed once; turned down again, the access is rejected.
    /// </summary>
    public async Task<IgdbLookup> QueryAsync(IgdbQuery query, CancellationToken cancel)
    {
        for (var renewed = false; ; renewed = true)
        {
            if (Locked(() => _state is IgdbAccessState.Unchecked or IgdbAccessState.Valid ? (_clientId!, _secret!, _token) : ((string, string, IgdbToken?)?)null)
                is not var (clientId, secret, token))
            {
                return IgdbLookup.Of(IgdbOutcome.Rejected);
            }

            if (token is null)
            {
                var (outcome, fresh) = await client.TokenAsync(clientId, secret, cancel);
                Learn(clientId, secret, outcome, fresh, checkedNow: false);
                if (fresh is null)
                {
                    return IgdbLookup.Of(outcome);
                }

                token = fresh;
            }

            var lookup = await client.QueryAsync(clientId, token, query, cancel);
            if (lookup.Outcome == IgdbOutcome.Rejected && !renewed)
            {
                // The token ran out or was revoked: a new one, once.
                ForgetToken(token);
                continue;
            }

            Learn(clientId, secret, lookup.Outcome, token, checkedNow: false);
            return lookup;
        }
    }

    private async Task<IgdbOutcome?> SetAsync(string? clientId, string? secret, CancellationToken cancel)
    {
        (string Id, string Secret)? both;
        lock (_lock)
        {
            EnsureLoaded();
            _clientId = clientId ?? _clientId;
            _secret = secret ?? _secret;
            IsStored = store.Save(_clientId, _secret);
            _token = null;
            _lastCheck = null;
            both = _clientId is { } id && _secret is { } s ? (id, s) : null;
            _state = both is null ? IgdbAccessState.None : IgdbAccessState.Unchecked;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return both is { } access ? await CheckAsync(access.Id, access.Secret, cancel) : null;
    }

    private async Task<IgdbOutcome> CheckAsync(string clientId, string secret, CancellationToken cancel)
    {
        var (outcome, token) = await client.TokenAsync(clientId, secret, cancel);
        Learn(clientId, secret, outcome, token, checkedNow: true);
        return outcome;
    }

    /// <summary>What an answer says about the access, unless it changed meanwhile.</summary>
    private void Learn(string clientId, string secret, IgdbOutcome outcome, IgdbToken? token, bool checkedNow)
    {
        bool changed;
        lock (_lock)
        {
            if (_clientId != clientId || _secret != secret)
            {
                return;
            }

            var before = (_state, _lastCheck);
            _state = outcome switch
            {
                IgdbOutcome.Rejected => IgdbAccessState.Rejected,
                IgdbOutcome.NoConnection => _state,
                _ => IgdbAccessState.Valid,
            };
            _token = outcome == IgdbOutcome.Rejected ? null : token ?? _token;
            if (checkedNow)
            {
                _lastCheck = (time.GetUtcNow(), outcome);
            }

            changed = before != (_state, _lastCheck);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ForgetToken(IgdbToken token)
    {
        lock (_lock)
        {
            if (_token == token)
            {
                _token = null;
            }
        }
    }

    private T Locked<T>(Func<T> read)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return read();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        (_clientId, _secret) = store.Load();
        _state = _clientId is not null && _secret is not null ? IgdbAccessState.Unchecked : IgdbAccessState.None;
    }
}
