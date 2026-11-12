using System.Net;
using System.Text;
using Arcadia.Core.Metadata.Igdb;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Metadata.Igdb;

/// <summary>#494: the IGDB access with a fake Twitch and IGDB: entering, checking, storing, rejecting, the token and what goes out.</summary>
public sealed class IgdbAccessTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-igdb-access-").FullName;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeIgdb _igdb;
    private readonly List<string> _log = [];

    public IgdbAccessTests() => _igdb = new FakeIgdb(_time);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static byte[] Xor(byte[] data) => [.. data.Select(b => (byte)(b ^ 0x5A))];

    private IgdbAccessStore EncryptedStore() => new(_folder, Xor, Xor);

    private IgdbAccess AccessOver(IgdbAccessStore store) =>
        new(store, new IgdbClient(new HttpClient(_igdb), _time) { Log = line => { lock (_log) { _log.Add(line); } } }, _time);

    private IgdbAccess NewAccess() => AccessOver(EncryptedStore());

    private static IgdbQuery Query => IgdbQuery.ByName("Nordwind")!;

    [Fact]
    [Trait("TestCase", "T494-2")]
    public async Task The_access_is_checked_with_one_token_request_once_both_values_are_there()
    {
        var access = NewAccess();
        Assert.Null(await access.SetClientIdAsync(FakeIgdb.ClientId));
        Assert.Equal(0, _igdb.TokenRequests);
        Assert.Equal(IgdbAccessState.None, access.State);

        Assert.Equal(IgdbOutcome.Found, await access.SetSecretAsync(FakeIgdb.Secret));
        Assert.Equal(1, _igdb.TokenRequests);
        Assert.Equal(IgdbAccessState.Valid, access.State);
        Assert.Equal(FakeIgdb.ClientId[^4..], access.ClientIdEnding);
        Assert.Equal(FakeIgdb.Secret[^4..], access.SecretEnding);
    }

    [Fact]
    [Trait("TestCase", "T494-6")]
    public async Task A_test_without_network_keeps_the_access_and_says_no_connection()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);
        _igdb.Offline = true;

        Assert.Equal(IgdbOutcome.NoConnection, await access.TestAsync());
        Assert.Equal(IgdbAccessState.Valid, access.State);
        Assert.Equal(IgdbOutcome.NoConnection, access.LastCheck?.Outcome);
        Assert.True(access.IsUsable);
    }

    [Fact]
    [Trait("TestCase", "T494-6")]
    public async Task A_test_without_both_values_sends_nothing()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        Assert.Null(await access.TestAsync());
        Assert.Empty(_igdb.Snapshot());
    }

    [Fact]
    [Trait("TestCase", "T494-7")]
    public async Task The_access_is_stored_encrypted_and_back_after_a_restart()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);

        var stored = File.ReadAllBytes(Path.Combine(_folder, IgdbAccessStore.FileName));
        var text = Encoding.UTF8.GetString(stored);
        Assert.DoesNotContain(FakeIgdb.ClientId, text);
        Assert.DoesNotContain(FakeIgdb.Secret, text);

        var restarted = NewAccess();
        Assert.Equal(IgdbAccessState.Unchecked, restarted.State);
        Assert.Equal(FakeIgdb.Secret[^4..], restarted.SecretEnding);
    }

    [Fact]
    [Trait("TestCase", "T494-7")]
    public void A_stored_access_that_cant_be_decrypted_counts_as_none()
    {
        File.WriteAllBytes(Path.Combine(_folder, IgdbAccessStore.FileName), [1, 2, 3]);
        var access = AccessOver(new IgdbAccessStore(_folder, Xor, _ => throw new System.Security.Cryptography.CryptographicException("made-up")));
        Assert.Equal(IgdbAccessState.None, access.State);
    }

    [Fact]
    [Trait("TestCase", "T494-7")]
    public async Task Without_encryption_nothing_is_written_and_the_access_lasts_until_arcadia_closes()
    {
        var access = AccessOver(new IgdbAccessStore(_folder, null, null));
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);
        Assert.Equal(IgdbAccessState.Valid, access.State);
        Assert.False(access.Persists);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_folder));
    }

    [Fact]
    [Trait("TestCase", "T494-9")]
    public async Task A_rejected_access_sends_no_further_request_until_it_changes()
    {
        _igdb.AcceptedSecret = "anothersecret000000000000000001";
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        Assert.Equal(IgdbOutcome.Rejected, await access.SetSecretAsync(FakeIgdb.Secret));
        Assert.Equal(IgdbAccessState.Rejected, access.State);
        var before = _igdb.Snapshot().Count;

        Assert.Equal(IgdbOutcome.Rejected, (await QueryPassingTimeAsync(access)).Outcome);
        Assert.Equal(before, _igdb.Snapshot().Count);

        Assert.Equal(IgdbOutcome.Found, await access.SetSecretAsync("anothersecret000000000000000001"));
        Assert.Equal(IgdbAccessState.Valid, access.State);
    }

    [Fact]
    [Trait("TestCase", "T494-9")]
    public async Task Deleting_the_access_ends_the_warning_and_every_request()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);
        Assert.True(access.Clear());
        Assert.Equal(IgdbAccessState.None, access.State);
        Assert.False(File.Exists(Path.Combine(_folder, IgdbAccessStore.FileName)));
        var before = _igdb.Snapshot().Count;
        Assert.Equal(IgdbOutcome.Rejected, (await QueryPassingTimeAsync(access)).Outcome);
        Assert.Equal(before, _igdb.Snapshot().Count);
    }

    [Fact]
    [Trait("TestCase", "T494-11")]
    public async Task The_secret_goes_only_to_twitch_and_client_id_and_token_only_in_igdbs_headers()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);
        await QueryPassingTimeAsync(access);

        var requests = _igdb.Snapshot();
        Assert.All(requests, r => Assert.Equal(Uri.UriSchemeHttps, r.Address.Scheme));
        Assert.All(requests, r => Assert.Contains(r.Address.Host, new[] { "id.twitch.tv", "api.igdb.com" }));
        Assert.All(requests, r => Assert.DoesNotContain(FakeIgdb.Secret, r.Address.ToString()));
        Assert.All(requests, r => Assert.False(r.Headers.ContainsKey("Cookie")));
        var token = Assert.Single(requests, r => r.Address.Host == "id.twitch.tv");
        Assert.Contains("client_secret=" + FakeIgdb.Secret, token.Body);
        var api = Assert.Single(requests, r => r.Address.Host == "api.igdb.com");
        Assert.Equal(FakeIgdb.ClientId, api.Headers["Client-ID"]);
        Assert.Equal("Bearer " + FakeIgdb.Token, api.Headers["Authorization"]);
        Assert.DoesNotContain(FakeIgdb.Secret, api.Body);
        Assert.DoesNotContain(FakeIgdb.ClientId, api.Body);
        Assert.StartsWith("search \"Nordwind\";", api.Body);
    }

    [Fact]
    [Trait("TestCase", "T494-11")]
    public async Task A_rejected_token_is_renewed_once_then_the_access_is_rejected()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);
        _igdb.RejectsTokens = true;

        Assert.Equal(IgdbOutcome.Rejected, (await QueryPassingTimeAsync(access)).Outcome);
        Assert.Equal(2, _igdb.TokenRequests);
        Assert.Equal(2, _igdb.ApiRequests);
        Assert.Equal(IgdbAccessState.Rejected, access.State);
    }

    [Fact]
    [Trait("TestCase", "T494-8")]
    public async Task No_value_of_the_access_reaches_a_log_line()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);
        await access.TestAsync();
        _igdb.ThrottleNext = 2;
        await QueryPassingTimeAsync(access);
        _igdb.ApiStatus = HttpStatusCode.InternalServerError;
        await QueryPassingTimeAsync(access);
        _igdb.ApiStatus = null;
        _igdb.Hang = TimeSpan.FromSeconds(30);
        await QueryPassingTimeAsync(access);
        _igdb.Hang = null;
        _igdb.RejectsTokens = true;
        await QueryPassingTimeAsync(access);
        _igdb.AcceptedSecret = "anothersecret000000000000000001";
        await access.TestAsync();

        string[] lines;
        lock (_log)
        {
            lines = [.. _log];
        }

        Assert.NotEmpty(lines);
        Assert.All(lines, line =>
        {
            Assert.DoesNotContain(FakeIgdb.ClientId, line);
            Assert.DoesNotContain(FakeIgdb.Secret, line);
            Assert.DoesNotContain(FakeIgdb.Token, line);
        });
    }

    [Fact]
    [Trait("TestCase", "T494-12")]
    public async Task Throttling_waits_and_asks_again_and_a_timeout_is_no_connection()
    {
        var access = NewAccess();
        await access.SetClientIdAsync(FakeIgdb.ClientId);
        await access.SetSecretAsync(FakeIgdb.Secret);

        _igdb.ThrottleNext = 2;
        Assert.Equal(IgdbOutcome.Found, (await QueryPassingTimeAsync(access)).Outcome);
        Assert.Equal(3, _igdb.ApiRequests);

        _igdb.Hang = TimeSpan.FromSeconds(30);
        Assert.Equal(IgdbOutcome.NoConnection, (await QueryPassingTimeAsync(access)).Outcome);
        Assert.Equal(IgdbAccessState.Valid, access.State);
    }

    private async Task<IgdbLookup> QueryPassingTimeAsync(IgdbAccess access)
    {
        var query = access.QueryAsync(Query, default);
        for (var i = 0; i < 500 && !query.IsCompleted; i++)
        {
            await Task.Delay(1);
            _time.Advance(TimeSpan.FromMilliseconds(500));
        }

        return await query.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
