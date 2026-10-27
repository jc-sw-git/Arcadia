using System.Diagnostics;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam.Account;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>The Steam account's sign-in, owned games and their cache (#83), against <see cref="FakeSteamWeb"/>.</summary>
public sealed class SteamOnlineAccountTests : IDisposable
{
    private const uint Account = 100_000_001;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-steam-account-").FullName;
    private readonly FakeSteamWeb _web;
    private readonly HttpClient _http;
    private readonly List<SteamOnlineAccount> _accounts = [];

    public SteamOnlineAccountTests()
    {
        _web = new FakeSteamWeb(_time.GetUtcNow());
        _http = new HttpClient(_web);
        _web.OwnedGames.Add((10, "Counter-Strike", 600, 1_700_000_000));
        _web.OwnedGames.Add((20, "Team Fortress Classic", 0, 0));
    }

    [Fact]
    public async Task Approved_code_signs_in_with_the_profile_name_and_fetches_the_games()
    {
        var account = Create();
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);

        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.ShowingCode);
        Assert.Equal(FakeSteamWeb.CodeUrl(1), account.State.Code);

        _web.IsApproved = true;
        await AdvanceUntilAsync(() => account.State.OwnedGames is not null);

        Assert.Equal(AccountStep.SignedIn, account.State.Step);
        Assert.Equal(FakeSteamWeb.ProfileName, account.State.AccountName);
        Assert.Equal(2, account.State.OwnedGames);
        Assert.Equal(1, libraryChanges);
        var owned = account.OwnedGamesFor(Account);
        Assert.NotNull(owned);
        Assert.Equal([10u, 20u], owned.Select(g => g.AppId));
    }

    [Fact]
    public async Task Renewed_code_replaces_the_one_shown()
    {
        var account = Create();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.ShowingCode);

        _web.RenewCode();
        await AdvanceUntilAsync(() => account.State.Code == FakeSteamWeb.CodeUrl(2));

        Assert.Equal(AccountStep.ShowingCode, account.State.Step);
    }

    [Fact]
    public async Task Code_nobody_approves_expires_after_two_minutes_and_A_gets_a_new_one()
    {
        var account = Create();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.ShowingCode);
        var shownAt = _time.GetUtcNow();

        await AdvanceUntilAsync(() => account.State.Step == AccountStep.CodeExpired);

        Assert.InRange(_time.GetUtcNow() - shownAt, SteamOnlineAccount.CodeLifetime, SteamOnlineAccount.CodeLifetime + TimeSpan.FromSeconds(10));
        Assert.Null(account.State.Code);
        Assert.True(account.State.IsSigningIn);

        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.ShowingCode);
        Assert.Equal(FakeSteamWeb.CodeUrl(2), account.State.Code);
    }

    [Fact]
    public async Task Code_rejected_on_the_phone_says_so()
    {
        var account = Create();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.ShowingCode);

        _web.IsRejected = true;
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.CodeRejected);

        Assert.Null(account.State.AccountName);
    }

    [Fact]
    public async Task Sign_in_without_network_says_Steam_is_unreachable()
    {
        _web.IsOffline = true;
        var account = Create();

        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.Unreachable);
    }

    [Fact]
    public async Task Cancelled_sign_in_stays_signed_out_even_if_approved_later()
    {
        var account = Create();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.ShowingCode);

        account.CancelSignIn();
        _web.IsApproved = true;
        _time.Advance(TimeSpan.FromSeconds(30));
        await account.WhenIdleAsync();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Null(Create().State.AccountName);
    }

    [Fact]
    public async Task Sign_in_and_games_survive_a_restart_and_are_shown_offline()
    {
        var account = await SignedInAsync();
        _web.IsOffline = true;

        var restarted = Create();
        Assert.Equal(AccountStep.SignedIn, restarted.State.Step);
        Assert.Equal(FakeSteamWeb.ProfileName, restarted.State.AccountName);
        restarted.FetchSoon();
        await AdvanceUntilAsync(() => restarted.State.Problem is not null);

        Assert.Equal(SteamOnlineAccount.UnreachableWithList, restarted.State.Problem);
        Assert.Equal(2, restarted.OwnedGamesFor(Account)?.Count);
    }

    [Fact]
    public async Task Fetch_is_skipped_within_the_pause_and_a_changed_list_asks_for_a_rescan()
    {
        var account = await SignedInAsync();
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);
        var requests = _web.OwnedGamesRequests;

        account.FetchSoon();
        await account.WhenIdleAsync();
        Assert.Equal(requests, _web.OwnedGamesRequests);

        _time.Advance(SteamOnlineAccount.FetchPause);
        account.FetchSoon();
        await account.WhenIdleAsync();
        Assert.Equal(requests + 1, _web.OwnedGamesRequests);
        Assert.Equal(0, libraryChanges);

        _web.OwnedGames.Add((30, "Day of Defeat", 0, 0));
        _time.Advance(SteamOnlineAccount.FetchPause);
        account.FetchSoon();
        await account.WhenIdleAsync();
        Assert.Equal(1, libraryChanges);
        Assert.Equal(3, account.State.OwnedGames);
    }

    [Fact]
    public async Task Expired_access_token_is_renewed_before_the_games_are_fetched()
    {
        var account = await SignedInAsync();
        var renewals = _web.AccessTokenRequests;

        _time.Advance(TimeSpan.FromDays(2));
        _web.Now = _time.GetUtcNow();
        account.FetchSoon();
        await account.WhenIdleAsync();

        Assert.Equal(renewals + 1, _web.AccessTokenRequests);
        Assert.Null(account.State.Problem);
    }

    [Fact]
    public async Task Revoked_sign_in_signs_out_with_a_status_message_and_drops_the_games()
    {
        var account = await SignedInAsync();
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);

        _web.IsSignInRevoked = true;
        _time.Advance(TimeSpan.FromDays(2));
        _web.Now = _time.GetUtcNow();
        account.FetchSoon();
        await account.WhenIdleAsync();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Equal(SteamOnlineAccount.ExpiredProblem, account.State.Problem);
        Assert.Equal(1, libraryChanges);
        Assert.Null(account.OwnedGamesFor(Account));
        Assert.False(File.Exists(Path.Combine(_folder, "steam-owned-games.json")));
        Assert.Equal(SteamOnlineAccount.ExpiredProblem, Create().State.Problem);
    }

    [Fact]
    public async Task Another_account_in_the_Steam_client_hides_the_games_with_a_notice()
    {
        var account = await SignedInAsync();

        Assert.Null(account.OwnedGamesFor(Account + 1));
        Assert.Equal(SteamOnlineAccount.OtherAccountNotice, account.State.Notice);

        Assert.NotNull(account.OwnedGamesFor(Account));
        Assert.Null(account.State.Notice);

        // Nobody signed in to Steam: nothing to compare with.
        Assert.NotNull(account.OwnedGamesFor(null));
    }

    [Fact]
    public async Task Sign_out_deletes_sign_in_and_games()
    {
        var account = await SignedInAsync();
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);

        account.SignOut();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Null(account.OwnedGamesFor(Account));
        Assert.Equal(1, libraryChanges);
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Equal(AccountStep.SignedOut, Create().State.Step);
    }

    [Fact]
    public async Task Log_holds_no_token_no_account_id_and_no_name()
    {
        var lines = new List<string>();
        using var listener = new RecordingListener(lines);
        Trace.Listeners.Add(listener);
        try
        {
            var account = await SignedInAsync();
            _web.IsOffline = true;
            _time.Advance(TimeSpan.FromDays(2));
            account.FetchSoon();
            await account.WhenIdleAsync();
            account.SignOut();
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        string[] lineCopy;
        lock (lines)
        {
            lineCopy = [.. lines];
        }

        Assert.Contains(lineCopy, line => line.Contains("Steam-Konto: angemeldet", StringComparison.Ordinal));
        string[] secrets = [_web.LastAccessToken!, _web.LastRefreshToken!, FakeSteamWeb.SteamId.ToString(System.Globalization.CultureInfo.InvariantCulture), Account.ToString(System.Globalization.CultureInfo.InvariantCulture), FakeSteamWeb.ProfileName, FakeSteamWeb.LoginName];
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(lineCopy, line => line.Contains(secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Stored_sign_in_is_encrypted_and_an_unreadable_file_counts_as_signed_out()
    {
        var store = new SteamAccountStore(_folder, Flip, Flip);
        store.SaveSignIn(new SteamSignIn("refresh-secret", null, FakeSteamWeb.SteamId, "Name"));

        var raw = File.ReadAllBytes(Path.Combine(_folder, "steam-account.dat"));
        Assert.DoesNotContain("refresh-secret", System.Text.Encoding.UTF8.GetString(raw), StringComparison.Ordinal);
        Assert.Equal("refresh-secret", store.LoadSignIn()?.RefreshToken);

        File.WriteAllBytes(Path.Combine(_folder, "steam-account.dat"), [1, 2, 3]);
        Assert.Null(store.LoadSignIn());
    }

    [Fact]
    public void Without_encryption_the_sign_in_is_kept_in_memory_only()
    {
        var store = new SteamAccountStore(_folder, null, null);
        store.SaveSignIn(new SteamSignIn("refresh-secret", null, FakeSteamWeb.SteamId, "Name"));

        Assert.Equal("refresh-secret", store.LoadSignIn()?.RefreshToken);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    public void Dispose()
    {
        foreach (var account in _accounts)
        {
            account.Dispose();
        }

        _http.Dispose();
        _web.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    // A stand-in for DPAPI that still changes every byte.
    private static byte[] Flip(byte[] data) => [.. data.Select(b => (byte)~b)];

    private SteamOnlineAccount Create()
    {
        var account = new SteamOnlineAccount(new SteamWebApi(_http), new SteamAccountStore(_folder, Flip, Flip), _time);
        _accounts.Add(account);
        return account;
    }

    private async Task<SteamOnlineAccount> SignedInAsync()
    {
        var account = Create();
        _web.IsApproved = true;
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.OwnedGames is not null);
        _web.IsApproved = false;
        return account;
    }

    /// <summary>Moves the clock in small steps until <paramref name="condition"/> holds; the account works on other threads.</summary>
    private async Task AdvanceUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 2000 && !condition(); i++)
        {
            await Task.Delay(1);
            if (!condition())
            {
                _time.Advance(TimeSpan.FromMilliseconds(250));
            }
        }

        Assert.True(condition());
    }

    private sealed class RecordingListener(List<string> lines) : TraceListener
    {
        public override void Write(string? message) => Add(message);

        public override void WriteLine(string? message) => Add(message);

        private void Add(string? message)
        {
            lock (lines)
            {
                lines.Add(message ?? string.Empty);
            }
        }
    }
}
