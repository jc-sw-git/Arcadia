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
    public async Task Sign_in_on_Steams_login_page_takes_the_store_token_and_the_profile_name_and_fetches_the_games()
    {
        var account = Create();
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);

        account.BeginSignIn();
        await AdvanceUntilAsync(() => _web.IsSignInPageShown);
        Assert.Equal(AccountStep.SigningIn, account.State.Step);

        _web.IsApproved = true;
        await AdvanceUntilAsync(() => account.State.OwnedGames is not null);

        Assert.Equal(AccountStep.SignedIn, account.State.Step);
        Assert.Equal(FakeSteamWeb.ProfileName, account.State.AccountName);
        Assert.Equal(2, account.State.OwnedGames);
        Assert.Equal(1, libraryChanges);
        Assert.Equal(1, _web.SignInPagesShown);
        var owned = account.OwnedGamesFor(Account);
        Assert.NotNull(owned);
        Assert.Equal([10u, 20u], owned.Owned.Select(g => g.AppId));
    }

    [Fact]
    public async Task An_unexpected_error_in_the_sign_in_ends_it_so_B_goes_back_and_a_new_sign_in_works()
    {
        var account = Create(new ThrowingWebPages());
        var failures = new List<Exception>();
        account.Failed += (_, e) => failures.Add(e);

        account.BeginSignIn();
        await account.WhenIdleAsync();

        Assert.IsType<InvalidOperationException>(Assert.Single(failures));
        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        account.CancelSignIn();
        Assert.Equal(AccountStep.SignedOut, account.State.Step);

        account.BeginSignIn();
        await account.WhenIdleAsync();
        account.CancelSignIn();
        Assert.Equal(2, failures.Count);
        Assert.Equal(AccountStep.SignedOut, account.State.Step);
    }

    [Fact]
    public async Task An_unexpected_error_in_the_sign_in_tells_the_page_it_is_signed_out_again()
    {
        var account = Create(new ThrowingWebPages());
        var steps = new List<AccountStep>();
        account.StateChanged += (_, _) => { lock (steps) { steps.Add(account.State.Step); } };

        account.BeginSignIn();
        await account.WhenIdleAsync();

        // The launcher page only redraws on StateChanged: without it, "Anmeldung läuft" stays on screen.
        lock (steps)
        {
            Assert.Equal([AccountStep.SigningIn, AccountStep.SignedOut], steps);
        }
    }

    [Fact]
    public async Task An_older_sign_in_that_fails_after_a_newer_one_began_leaves_the_newer_one_running()
    {
        var web = new ControlledWebPages();
        var account = Create(web);
        var failures = new List<Exception>();
        account.Failed += (_, e) => failures.Add(e);

        account.BeginSignIn();
        await AdvanceUntilAsync(() => web.Shown.Count == 1);
        account.CancelSignIn();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => web.Shown.Count == 2);

        web.Shown[0].SetException(new InvalidOperationException("Browser kaputt"));
        await AdvanceUntilAsync(() => failures.Count == 1);

        Assert.Equal(AccountStep.SigningIn, account.State.Step);
        account.CancelSignIn();
        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        web.Shown[1].SetResult(WebPageResult.Cancelled);
        await account.WhenIdleAsync();
        Assert.Equal(AccountStep.SignedOut, account.State.Step);
    }

    [Fact]
    public async Task A_cancellation_nobody_asked_for_ends_the_sign_in_too()
    {
        var web = new ControlledWebPages();
        var account = Create(web);

        account.BeginSignIn();
        await AdvanceUntilAsync(() => web.Shown.Count == 1);
        // E.g. a timeout inside the browser: an OperationCanceledException although neither B nor Arcadia's end cancelled.
        web.Shown[0].SetException(new TaskCanceledException("Zeitüberschreitung"));
        await account.WhenIdleAsync();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        account.CancelSignIn();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => web.Shown.Count == 2);
        Assert.Equal(AccountStep.SigningIn, account.State.Step);
        account.CancelSignIn();
        Assert.Equal(AccountStep.SignedOut, account.State.Step);
    }

    [Fact]
    public async Task Sign_in_keeps_the_profiles_avatar_and_sign_out_deletes_it()
    {
        var account = await SignedInAsync();

        var avatar = account.State.AvatarPath;
        Assert.NotNull(avatar);
        Assert.Equal(FakeSteamWeb.AvatarPicture, File.ReadAllBytes(avatar));
        Assert.Equal(avatar, Create().State.AvatarPath);

        account.SignOut();

        Assert.Null(account.State.AvatarPath);
        Assert.False(File.Exists(avatar));
    }

    [Fact]
    public async Task An_avatar_answer_that_is_no_image_is_not_kept_and_the_sign_in_holds()
    {
        _web.AvatarAnswer = "<html>Fehler</html>"u8.ToArray();

        var account = await SignedInAsync();

        Assert.Equal(1, _web.AvatarRequests);
        Assert.Null(account.State.AvatarPath);
        Assert.Equal(FakeSteamWeb.ProfileName, account.State.AccountName);
    }

    [Fact]
    public async Task An_avatar_too_large_is_not_kept()
    {
        _web.AvatarAnswer = [0xFF, 0xD8, 0xFF, .. new byte[SteamWebApi.MaxAvatarBytes]];

        var account = await SignedInAsync();

        Assert.Null(account.State.AvatarPath);
    }

    [Fact]
    public async Task Profile_name_that_cannot_be_loaded_falls_back_to_Steam()
    {
        _web.ProfileFails = true;
        var account = await SignedInAsync();

        Assert.Equal("Steam", account.State.AccountName);
    }

    [Fact]
    public async Task Apps_only_the_store_lists_are_added_without_a_name_and_do_not_count_as_games()
    {
        _web.StoreApps.AddRange([40u, 5u]);
        var account = await SignedInAsync();

        var owned = account.OwnedGamesFor(Account)!.Owned;
        Assert.Equal([10u, 20u, 5u, 40u], owned.Select(g => g.AppId));
        Assert.Equal([null, null], owned.Skip(2).Select(g => g.Name));
        Assert.Equal(2, account.State.OwnedGames);
    }

    [Fact]
    public async Task A_failed_store_list_keeps_the_store_apps_fetched_before()
    {
        _web.StoreApps.Add(40);
        var account = await SignedInAsync();

        _web.StorePackages.Add(500);
        account.RefreshNow();
        await account.WhenIdleAsync();
        Assert.Equal([500u], account.OwnedGamesFor(Account)!.Packages);

        _web.StoreFails = true;
        account.RefreshNow();
        await account.WhenIdleAsync();

        Assert.Equal([10u, 20u, 40u], account.OwnedGamesFor(Account)!.Owned.Select(g => g.AppId));
        Assert.Equal([500u], account.OwnedGamesFor(Account)!.Packages);
        Assert.Null(account.State.Problem);
    }

    [Fact]
    public async Task Cancelled_sign_in_closes_the_page_and_stays_signed_out_even_if_signed_in_there_later()
    {
        var account = Create();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => _web.IsSignInPageShown);

        account.CancelSignIn();
        await AdvanceUntilAsync(() => !_web.IsSignInPageShown);
        _web.IsApproved = true;
        await account.WhenIdleAsync();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Null(Create().State.AccountName);
    }

    [Fact]
    public async Task Login_page_that_does_not_load_says_Steam_is_unreachable_and_A_tries_again()
    {
        _web.IsOffline = true;
        var account = Create();

        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.Unreachable);
        Assert.True(account.State.IsSigningIn);

        _web.IsOffline = false;
        _web.IsApproved = true;
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.SignedIn);
        Assert.Equal(2, _web.SignInPagesShown);
    }

    [Fact]
    public async Task Without_a_browser_the_sign_in_says_Steam_is_unreachable()
    {
        var account = Create(web: null);

        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.Unreachable);
        Assert.Equal(0, _web.SignInPagesShown);
    }

    [Fact]
    public async Task Browser_still_signed_in_after_a_restart_signs_in_at_once()
    {
        await SignedInAsync();
        File.Delete(Path.Combine(_folder, "steam-account.dat"));

        var account = Create();
        account.BeginSignIn();
        await AdvanceUntilAsync(() => account.State.Step == AccountStep.SignedIn);
    }

    [Fact]
    public void Sign_in_of_the_first_version_posing_as_the_phone_app_is_deleted_at_the_start()
    {
        var store = new SteamAccountStore(_folder, Flip, Flip);
        store.SaveSignIn(new SteamSignIn("access", FakeSteamWeb.SteamId, "Name", RefreshToken: "refresh"));

        var account = Create();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Null(account.State.AccountName);
        Assert.Null(store.LoadSignIn());
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
        Assert.Equal(2, restarted.OwnedGamesFor(Account)?.Owned.Count);
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
    public async Task Refresh_asked_for_by_the_user_fetches_within_the_pause()
    {
        var account = await SignedInAsync();
        var requests = _web.OwnedGamesRequests;

        account.RefreshNow();
        await account.WhenIdleAsync();

        Assert.Equal(requests + 1, _web.OwnedGamesRequests);
    }

    [Fact]
    public async Task An_answer_without_games_keeps_the_cached_list_with_a_status_message()
    {
        var account = await SignedInAsync();

        _web.ListsNoGames = true;
        account.RefreshNow();
        await account.WhenIdleAsync();

        Assert.Equal(2, account.OwnedGamesFor(Account)?.Owned.Count);
        Assert.Equal(SteamOnlineAccount.UnreachableWithList, account.State.Problem);
    }

    [Fact]
    public async Task An_unexpected_error_in_the_background_is_reported_not_thrown()
    {
        var account = await SignedInAsync();
        Exception? reported = null;
        account.Failed += (_, e) => reported = e;

        _web.FailsUnexpectedly = true;
        account.RefreshNow();
        await account.WhenIdleAsync();

        Assert.IsType<InvalidOperationException>(reported);
        Assert.Equal(AccountStep.SignedIn, account.State.Step);
    }

    [Fact]
    public async Task Expired_token_is_read_anew_from_the_store_page_without_showing_the_login_page()
    {
        var account = await SignedInAsync();
        var reads = _web.StorePageReads;

        _time.Advance(TimeSpan.FromDays(2));
        _web.Now = _time.GetUtcNow();
        account.FetchSoon();
        await account.WhenIdleAsync();

        Assert.Equal(reads + 1, _web.StorePageReads);
        Assert.Equal(1, _web.SignInPagesShown);
        Assert.Null(account.State.Problem);
        Assert.Equal(AccountStep.SignedIn, account.State.Step);
    }

    [Fact]
    public async Task A_store_token_revoked_before_it_expired_is_read_anew_once()
    {
        var account = await SignedInAsync();
        var reads = _web.StorePageReads;

        // Another read of the store page (e.g. in a second Arcadia) replaced the token.
        await _web.ReadAsync(SteamStorePage.StorePage, SteamStorePage.Domains, SteamStorePage.AccountScript, _ => true, CancellationToken.None);
        account.RefreshNow();
        await account.WhenIdleAsync();

        Assert.Equal(reads + 2, _web.StorePageReads);
        Assert.Null(account.State.Problem);
        Assert.Equal(AccountStep.SignedIn, account.State.Step);
    }

    [Fact]
    public async Task Store_page_signed_out_signs_out_with_a_status_message_and_drops_the_games()
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
    public async Task Store_page_signed_in_with_another_account_counts_as_signed_out()
    {
        var account = await SignedInAsync();

        _web.AccountSteamId = FakeSteamWeb.SteamId + 1;
        _time.Advance(TimeSpan.FromDays(2));
        _web.Now = _time.GetUtcNow();
        account.FetchSoon();
        await account.WhenIdleAsync();

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Equal(SteamOnlineAccount.ExpiredProblem, account.State.Problem);
    }

    [Fact]
    public async Task Another_account_in_the_Steam_client_keeps_the_games_and_says_so()
    {
        var account = await SignedInAsync();

        var other = account.OwnedGamesFor(Account + 1);
        Assert.Equal(2, other?.Owned.Count);
        Assert.True(other?.SteamHasOtherAccount);
        Assert.Equal(SteamOnlineAccount.OtherAccountNotice, account.State.Notice);

        Assert.False(account.OwnedGamesFor(Account)?.SteamHasOtherAccount);
        Assert.Null(account.State.Notice);

        // Steam isn't running: nothing to compare with.
        Assert.False(account.OwnedGamesFor(0)?.SteamHasOtherAccount);
        Assert.False(account.OwnedGamesFor(null)?.SteamHasOtherAccount);
    }

    [Fact]
    public async Task Account_switch_in_Steam_asks_for_a_rescan_without_waiting_for_one()
    {
        var account = await SignedInAsync();
        uint? client = Account;
        account.OwnedGamesFor(client);
        account.FollowClient(() => client);
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);

        _time.Advance(SteamOnlineAccount.ClientCheckInterval * 3);
        Assert.Equal(0, libraryChanges);

        client = Account + 1;
        _time.Advance(SteamOnlineAccount.ClientCheckInterval);
        Assert.Equal(1, libraryChanges);

        _time.Advance(SteamOnlineAccount.ClientCheckInterval * 3);
        Assert.Equal(1, libraryChanges);
    }

    [Fact]
    public async Task Sign_out_deletes_sign_in_games_and_Steams_cookies()
    {
        var account = await SignedInAsync();
        var libraryChanges = 0;
        account.LibraryChanged += (_, _) => Interlocked.Increment(ref libraryChanges);

        account.SignOut();
        await AdvanceUntilAsync(() => _web.CookiesForgotten == 1);

        Assert.Equal(AccountStep.SignedOut, account.State.Step);
        Assert.Null(account.OwnedGamesFor(Account));
        Assert.Equal(1, libraryChanges);
        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Equal(AccountStep.SignedOut, Create().State.Step);

        // The next sign-in shows the login page again instead of using the old cookies.
        var next = Create();
        next.BeginSignIn();
        await AdvanceUntilAsync(() => _web.IsSignInPageShown);
        next.CancelSignIn();
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
        string[] secrets = [_web.LastAccessToken!, FakeSteamWeb.SteamId.ToString(System.Globalization.CultureInfo.InvariantCulture), Account.ToString(System.Globalization.CultureInfo.InvariantCulture), FakeSteamWeb.ProfileName, FakeSteamWeb.AvatarHash];
        foreach (var secret in secrets)
        {
            Assert.DoesNotContain(lineCopy, line => line.Contains(secret, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Stored_sign_in_is_encrypted_and_an_unreadable_file_counts_as_signed_out()
    {
        var store = new SteamAccountStore(_folder, Flip, Flip);
        store.SaveSignIn(new SteamSignIn("token-secret", FakeSteamWeb.SteamId, "Name"));

        var raw = File.ReadAllBytes(Path.Combine(_folder, "steam-account.dat"));
        Assert.DoesNotContain("token-secret", System.Text.Encoding.UTF8.GetString(raw), StringComparison.Ordinal);
        Assert.Equal("token-secret", store.LoadSignIn()?.AccessToken);

        File.WriteAllBytes(Path.Combine(_folder, "steam-account.dat"), [1, 2, 3]);
        Assert.Null(store.LoadSignIn());
    }

    [Fact]
    public void Without_encryption_the_sign_in_is_kept_in_memory_only()
    {
        var store = new SteamAccountStore(_folder, null, null);
        store.SaveSignIn(new SteamSignIn("token-secret", FakeSteamWeb.SteamId, "Name"));

        Assert.Equal("token-secret", store.LoadSignIn()?.AccessToken);
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
        DeleteFolder();
    }

    /// <summary>
    /// Dispose cancels the account's background work but doesn't wait for it: a fetch that started after the sign-in
    /// may still be writing its file (temporary file, then moved) while the folder is deleted, and the delete fails with
    /// "Directory not empty". It goes again once that write is done.
    /// </summary>
    private void DeleteFolder()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(_folder, recursive: true);
                return;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && attempt < 50)
            {
                Thread.Sleep(20);
            }
        }
    }

    // A stand-in for DPAPI that still changes every byte.
    private static byte[] Flip(byte[] data) => [.. data.Select(b => (byte)~b)];

    private SteamOnlineAccount Create() => Create(_web);

    private SteamOnlineAccount Create(IWebPages? web)
    {
        var account = new SteamOnlineAccount(new SteamWebApi(_http), new SteamAccountStore(_folder, Flip, Flip), web, _time);
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

    /// <summary>A browser whose sign-in page fails with an exception nobody expects (#368).</summary>
    private sealed class ThrowingWebPages : IWebPages
    {
        public Task<WebPageResult> ShowAsync(string title, Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Browser kaputt");

        public Task<WebPageResult> ReadAsync(Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Browser kaputt");

        public Task<WebPageResult> ForgetAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Browser kaputt");
    }

    /// <summary>A browser whose sign-in pages end only when the test says so, one per call.</summary>
    private sealed class ControlledWebPages : IWebPages
    {
        private readonly List<TaskCompletionSource<WebPageResult>> _shown = [];

        public IReadOnlyList<TaskCompletionSource<WebPageResult>> Shown
        {
            get
            {
                lock (_shown)
                {
                    return [.. _shown];
                }
            }
        }

        public Task<WebPageResult> ShowAsync(string title, Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone, CancellationToken cancellationToken)
        {
            var shown = new TaskCompletionSource<WebPageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_shown)
            {
                _shown.Add(shown);
            }

            return shown.Task;
        }

        public Task<WebPageResult> ReadAsync(Uri page, IReadOnlyList<string> domains, string script, Func<string, bool> isDone, CancellationToken cancellationToken) =>
            Task.FromResult(WebPageResult.Failed("nicht im Test"));

        public Task<WebPageResult> ForgetAsync(IReadOnlyList<string> domains, CancellationToken cancellationToken) =>
            Task.FromResult(WebPageResult.Cancelled);
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
