using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Artwork.SteamGridDb;

/// <summary>What Arcadia knows about the SteamGridDB key (#489).</summary>
public enum SteamGridDbKeyState
{
    /// <summary>No key: Arcadia sends SteamGridDB no request at all, and says nothing about it (#13).</summary>
    None,

    /// <summary>A key SteamGridDB hasn't answered for yet (just loaded, or no connection at its check): it is used.</summary>
    Unchecked,

    Valid,

    /// <summary>SteamGridDB turned it down: a warning, and SteamGridDB is skipped until a new key (#13).</summary>
    Rejected,
}

/// <summary>
/// The player's SteamGridDB key and what SteamGridDB said about it (#489): entered, pasted, tested, deleted; the artwork
/// run asks SteamGridDB through it, so no request goes without a usable key. Thread-safe; <see cref="Changed"/> is raised
/// on the thread that changed it.
/// </summary>
public sealed class SteamGridDbKey(SteamGridDbKeyStore store, SteamGridDbClient client, TimeProvider time)
{
    /// <summary>SteamGridDB's keys are 32 letters and digits.</summary>
    public const int Length = 32;

    private readonly Lock _lock = new();
    private bool _loaded;
    private string? _key;
    private SteamGridDbKeyState _state;
    private DateTimeOffset? _checkedAt;
    private SteamGridDbOutcome? _lastCheck;

    public event EventHandler? Changed;

    public SteamGridDbKeyState State
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

    /// <summary>A request may go: there is a key and SteamGridDB hasn't turned it down.</summary>
    public bool IsUsable => State is SteamGridDbKeyState.Unchecked or SteamGridDbKeyState.Valid;

    /// <summary>The key's last four characters, for "endet auf …3f9a"; null without a key.</summary>
    public string? Ending
    {
        get
        {
            lock (_lock)
            {
                EnsureLoaded();
                return _key is null ? null : SteamGridDbClient.Ending(_key);
            }
        }
    }

    /// <summary>When "Verbindung testen" (or entering a key) last got an answer, and which; null before.</summary>
    public (DateTimeOffset At, SteamGridDbOutcome Outcome)? LastCheck
    {
        get
        {
            lock (_lock)
            {
                return _checkedAt is { } at && _lastCheck is { } outcome ? (at, outcome) : null;
            }
        }
    }

    /// <summary>
    /// The key in <paramref name="text"/>, without spaces and line breaks around it; null if it isn't one (32 letters and
    /// digits, nothing else), so nothing else reaches a header, a file or a log.
    /// </summary>
    public static string? Normalize(string? text)
    {
        var key = text?.Trim();
        return key is { Length: Length } && key.All(char.IsAsciiLetterOrDigit) ? key : null;
    }

    /// <summary>Saves the key and checks it at once (entered or pasted); its answer, also kept in <see cref="State"/>.</summary>
    /// <exception cref="ArgumentException">Not a key (<see cref="Normalize"/> first).</exception>
    public async Task<SteamGridDbOutcome> SetAsync(string key, CancellationToken cancel = default)
    {
        var normalized = Normalize(key) ?? throw new ArgumentException("Not a SteamGridDB key", nameof(key));
        lock (_lock)
        {
            EnsureLoaded();
            store.Save(normalized);
            _key = normalized;
            _state = SteamGridDbKeyState.Unchecked;
            _checkedAt = null;
            _lastCheck = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return await CheckAsync(normalized, cancel);
    }

    /// <summary>Deletes the key: no request goes from now on.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            EnsureLoaded();
            store.Save(null);
            _key = null;
            _state = SteamGridDbKeyState.None;
            _checkedAt = null;
            _lastCheck = null;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>"Verbindung testen": asks SteamGridDB once; null without a key (nothing is sent).</summary>
    public async Task<SteamGridDbOutcome?> TestAsync(CancellationToken cancel = default)
    {
        string? key;
        lock (_lock)
        {
            EnsureLoaded();
            key = _key;
        }

        return key is null ? null : await CheckAsync(key, cancel);
    }

    /// <summary>The game's images; <see cref="SteamGridDbOutcome.KeyRejected"/> without a usable key, with no request.</summary>
    public async Task<SteamGridDbAnswer> ImagesAsync(ArtworkStoreId game, ArtworkKind kind, CancellationToken cancel)
    {
        string? key;
        lock (_lock)
        {
            EnsureLoaded();
            key = _state is SteamGridDbKeyState.Unchecked or SteamGridDbKeyState.Valid ? _key : null;
        }

        if (key is null)
        {
            return SteamGridDbAnswer.Of(SteamGridDbOutcome.KeyRejected);
        }

        var answer = await client.ImagesAsync(key, game, kind, cancel);
        Learn(key, answer.Outcome, checkedNow: false);
        return answer;
    }

    private async Task<SteamGridDbOutcome> CheckAsync(string key, CancellationToken cancel)
    {
        var outcome = await client.CheckAsync(key, cancel);
        Learn(key, outcome, checkedNow: true);
        return outcome;
    }

    /// <summary>What an answer says about the key, unless the key changed meanwhile.</summary>
    private void Learn(string key, SteamGridDbOutcome outcome, bool checkedNow)
    {
        bool changed;
        lock (_lock)
        {
            if (_key != key)
            {
                return;
            }

            var before = (_state, _checkedAt, _lastCheck);
            _state = outcome switch
            {
                SteamGridDbOutcome.KeyRejected => SteamGridDbKeyState.Rejected,
                SteamGridDbOutcome.NoConnection => _state,
                _ => SteamGridDbKeyState.Valid,
            };
            if (checkedNow)
            {
                _checkedAt = time.GetUtcNow();
                _lastCheck = outcome;
            }

            changed = before != (_state, _checkedAt, _lastCheck);
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _key = store.Load();
        _state = _key is null ? SteamGridDbKeyState.None : SteamGridDbKeyState.Unchecked;
    }
}
