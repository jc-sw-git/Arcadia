using System.Net;
using System.Security.Cryptography;
using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

/// <summary>
/// Arcadia's own <c>viiper.exe</c> (#242 part B): only the pinned build of the fork counts, "Installieren" fetches
/// exactly it, and a download that fails its check never lands where <c>viiper.exe</c> is started from.
/// </summary>
public sealed class ViiperInstallationTests : IDisposable
{
    private static readonly byte[] Build = "fork build"u8.ToArray();

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "arcadia-viiper-" + Guid.NewGuid());

    public ViiperInstallationTests() => Directory.CreateDirectory(_directory);

    private string Executable => Path.Combine(_directory, ViiperRelease.ExecutableName);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    [Trait("TestCase", "T242-27")]
    public void Only_the_pinned_build_counts_as_the_copy_s_VIIPER()
    {
        var installation = new ViiperInstallation(Executable, Pinned(Build));
        Assert.False(installation.IsPinned());

        File.WriteAllBytes(Executable, "upstream viiper"u8.ToArray());
        Assert.True(installation.IsInstalled);
        Assert.False(installation.IsPinned());

        File.WriteAllBytes(Executable, Build);
        File.SetLastWriteTimeUtc(Executable, DateTime.UtcNow.AddMinutes(1));
        Assert.True(installation.IsPinned());
    }

    [Fact]
    [Trait("TestCase", "T242-27")]
    public void Without_a_pin_no_viiper_exe_counts()
    {
        File.WriteAllBytes(Executable, Build);

        Assert.False(new ViiperInstallation(Executable, Pinned(Build) with { Sha256 = null }).IsPinned());
    }

    [Fact]
    [Trait("TestCase", "T242-27")]
    public async Task Installieren_puts_the_checked_build_next_to_Arcadia()
    {
        using var http = new HttpClient(new Handler(Build));
        var installation = new ViiperInstallation(Executable, Pinned(Build));

        await installation.InstallAsync(http, CancellationToken.None);

        Assert.Equal(Build, File.ReadAllBytes(Executable));
        Assert.True(installation.IsPinned());
        Assert.Equal([ViiperRelease.ExecutableName], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    [Trait("TestCase", "T242-27")]
    public async Task A_download_that_fails_its_check_leaves_nothing_where_VIIPER_starts()
    {
        using var http = new HttpClient(new Handler("tampered"u8.ToArray()));
        var installation = new ViiperInstallation(Executable, Pinned(Build));

        await Assert.ThrowsAsync<InvalidOperationException>(() => installation.InstallAsync(http, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    [Trait("TestCase", "T242-27")]
    public async Task Without_a_pin_nothing_is_downloaded()
    {
        var handler = new Handler(Build);
        using var http = new HttpClient(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ViiperInstallation(Executable, Pinned(Build) with { Sha256 = null }).InstallAsync(http, CancellationToken.None));

        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public void The_build_s_pin_is_a_full_hash_or_none()
    {
        Assert.True(ViiperRelease.Sha256.Length is 0 or 64);
        Assert.Equal(ViiperRelease.IsPinned, ViiperRelease.Asset.Sha256 is not null);
    }

    private static ReleaseAsset Pinned(byte[] content) =>
        new(ViiperRelease.ExecutableName, new Uri("https://example.invalid/viiper.exe"), content.Length, Convert.ToHexStringLower(SHA256.HashData(content)));

    private sealed class Handler(byte[] content) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }
}
