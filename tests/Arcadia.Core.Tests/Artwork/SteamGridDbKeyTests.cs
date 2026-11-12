using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Arcadia.Core.Artwork.SteamGridDb;
using Arcadia.Launchers.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>#489: the SteamGridDB key, its shape, its storage and what SteamGridDB says about it (T489-3 to T489-5, T489-7 to T489-9, T489-12).</summary>
public sealed class SteamGridDbKeyTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("arcadia-sgdb-key-").FullName;
    private readonly FakeSteamGridDb _server = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 11, 12, 58, 0, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private SteamGridDbKeyStore EncryptedStore() => new(_folder, data => [.. data.Select(b => (byte)(b ^ 0x5A))], data => [.. data.Select(b => (byte)(b ^ 0x5A))]);

    private SteamGridDbKey KeyOver(SteamGridDbKeyStore store) => new(store, new SteamGridDbClient(new HttpClient(_server), _time), _time);

    [Theory]
    [Trait("TestCase", "T489-4")]
    [InlineData("  0123456789abcdef0123456789abcdef  ")]
    [InlineData("0123456789abcdef0123456789abcdef\r\n")]
    [InlineData("\t0123456789ABCDEF0123456789abcdef")]
    public void A_key_with_spaces_or_a_line_break_around_it_is_trimmed(string text) =>
        Assert.Equal(text.Trim(), SteamGridDbKey.Normalize(text));

    [Theory]
    [Trait("TestCase", "T489-5")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0")]
    [InlineData("0123456789abcdef 123456789abcdef")]
    [InlineData("0123456789abcdef\r\n23456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcd\u0000f")]
    [InlineData("0123456789abcdef0123456789abcd/f")]
    [InlineData("0123456789abcdef0123456789abcdé")]
    public void Anything_but_32_letters_and_digits_is_no_key(string? text) => Assert.Null(SteamGridDbKey.Normalize(text));

    [Fact]
    [Trait("TestCase", "T489-5")]
    public async Task Setting_a_malformed_key_throws_before_any_request()
    {
        var key = KeyOver(EncryptedStore());
        await Assert.ThrowsAsync<ArgumentException>(() => key.SetAsync("Bearer x\r\nHost: evil"));
        Assert.Equal(0, _server.Count);
        Assert.Equal(SteamGridDbKeyState.None, key.State);
    }

    [Fact]
    [Trait("TestCase", "T489-12")]
    public async Task Without_a_key_nothing_is_sent()
    {
        var key = KeyOver(EncryptedStore());

        Assert.Equal(SteamGridDbKeyState.None, key.State);
        Assert.Null(await key.TestAsync());
        Assert.Equal(SteamGridDbOutcome.KeyRejected, (await key.ImagesAsync(new(ArtworkStoreId.Steam, "4000001"), ArtworkKind.Cover, default)).Outcome);
        Assert.Equal(0, _server.Count);
    }

    [Fact]
    [Trait("TestCase", "T489-7")]
    public async Task A_key_is_checked_when_it_is_set_and_the_answer_decides_its_state()
    {
        var key = KeyOver(EncryptedStore());
        Assert.Equal(SteamGridDbOutcome.Found, await key.SetAsync(FakeSteamGridDb.Key));
        Assert.Equal((SteamGridDbKeyState.Valid, "cdef"), (key.State, key.Ending));
        Assert.Equal(_time.GetUtcNow(), key.LastCheck!.Value.At);

        _server.ValidKey = "ffffffffffffffffffffffffffffffff";
        Assert.Equal(SteamGridDbOutcome.KeyRejected, await key.TestAsync());
        Assert.Equal(SteamGridDbKeyState.Rejected, key.State);
        Assert.False(key.IsUsable);

        // A rejected key sends nothing more, not one request per game.
        var before = _server.Count;
        await key.ImagesAsync(new(ArtworkStoreId.Steam, "4000001"), ArtworkKind.Cover, default);
        Assert.Equal(before, _server.Count);
    }

    [Fact]
    [Trait("TestCase", "T489-7")]
    public async Task No_answer_within_ten_seconds_keeps_the_key_without_a_warning()
    {
        var key = KeyOver(EncryptedStore());
        _server.Hangs = true;

        var set = key.SetAsync(FakeSteamGridDb.Key);
        await WaitForRequestAsync(1);
        _time.Advance(SteamGridDbClient.Timeout);

        Assert.Equal(SteamGridDbOutcome.NoConnection, await set);
        Assert.Equal(SteamGridDbKeyState.Unchecked, key.State);
        Assert.True(key.IsUsable);
        Assert.Equal(FakeSteamGridDb.Key, EncryptedStore().Load());
    }

    [Fact]
    [Trait("TestCase", "T489-3")]
    public async Task Clearing_the_key_deletes_it_and_ends_a_warning()
    {
        var store = EncryptedStore();
        var key = KeyOver(store);
        _server.ValidKey = "ffffffffffffffffffffffffffffffff";
        await key.SetAsync(FakeSteamGridDb.Key);
        Assert.Equal(SteamGridDbKeyState.Rejected, key.State);
        var changes = 0;
        key.Changed += (_, _) => changes++;
        var asked = _server.Count;

        key.Clear();

        Assert.Equal((SteamGridDbKeyState.None, (string?)null, 1), (key.State, key.Ending, changes));
        Assert.Null(store.Load());
        Assert.False(File.Exists(Path.Combine(_folder, SteamGridDbKeyStore.FileName)));
        Assert.Equal(asked, _server.Count);
    }

    [Fact]
    [Trait("TestCase", "T489-8")]
    public async Task The_key_is_kept_encrypted_and_comes_back_after_a_restart()
    {
        await KeyOver(EncryptedStore()).SetAsync(FakeSteamGridDb.Key);

        var file = File.ReadAllBytes(Path.Combine(_folder, SteamGridDbKeyStore.FileName));
        Assert.DoesNotContain(FakeSteamGridDb.Key, Encoding.UTF8.GetString(file), StringComparison.Ordinal);
        var restarted = KeyOver(EncryptedStore());
        Assert.Equal((SteamGridDbKeyState.Unchecked, "cdef"), (restarted.State, restarted.Ending));
    }

    [Fact]
    [Trait("TestCase", "T489-8")]
    public void A_key_file_that_cant_be_decrypted_counts_as_no_key_and_logs_without_the_value()
    {
        File.WriteAllBytes(Path.Combine(_folder, SteamGridDbKeyStore.FileName), [1, 2, 3]);
        var store = new SteamGridDbKeyStore(_folder, data => data, _ => throw new CryptographicException("other user"));
        var lines = new List<string>();
        using var listener = new LinesListener(lines);
        Trace.Listeners.Add(listener);
        try
        {
            var key = KeyOver(store);
            Assert.Equal(SteamGridDbKeyState.None, key.State);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }

        Assert.Contains(lines, line => line.Contains("SteamGridDB: gespeicherter Wert nicht lesbar", StringComparison.Ordinal));
        Assert.Equal(0, _server.Count);
    }

    [WindowsFact]
    [Trait("TestCase", "T489-8")]
    public void On_windows_dpapi_keeps_the_key_for_this_user()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var store = DpapiStore();
        Assert.True(store.Save(FakeSteamGridDb.Key));

        var file = File.ReadAllBytes(Path.Combine(_folder, SteamGridDbKeyStore.FileName));
        Assert.DoesNotContain(FakeSteamGridDb.Key, Encoding.UTF8.GetString(file), StringComparison.Ordinal);
        Assert.DoesNotContain(FakeSteamGridDb.Key, Encoding.Unicode.GetString(file), StringComparison.Ordinal);
        Assert.Equal(FakeSteamGridDb.Key, DpapiStore().Load());
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private SteamGridDbKeyStore DpapiStore() => new(_folder,
        data => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser),
        data => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));

    [Fact]
    [Trait("TestCase", "T489-8")]
    public async Task A_key_that_cant_be_written_still_works_and_says_it_isnt_stored()
    {
        var store = new SteamGridDbKeyStore(_folder, _ => throw new CryptographicException("no profile"), data => data);
        var key = KeyOver(store);

        Assert.Equal(SteamGridDbOutcome.Found, await key.SetAsync(FakeSteamGridDb.Key));

        Assert.False(key.IsStored);
        Assert.True(key.IsUsable);
        Assert.False(File.Exists(Path.Combine(_folder, SteamGridDbKeyStore.FileName)));
    }

    [Fact]
    [Trait("TestCase", "T489-9")]
    public async Task Without_encryption_the_key_lasts_until_arcadia_closes_and_nothing_is_written()
    {
        var store = new SteamGridDbKeyStore(_folder, null, null);
        var key = KeyOver(store);
        await key.SetAsync(FakeSteamGridDb.Key);

        Assert.Equal(SteamGridDbKeyState.Valid, key.State);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_folder));
        Assert.Equal(SteamGridDbKeyState.None, KeyOver(new SteamGridDbKeyStore(_folder, null, null)).State);
    }

    private async Task WaitForRequestAsync(int count)
    {
        for (var i = 0; i < 500 && _server.Count < count; i++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class LinesListener(List<string> lines) : TraceListener
    {
        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            lock (lines)
            {
                lines.Add(message ?? string.Empty);
            }
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message) => WriteLine(message);
    }
}
