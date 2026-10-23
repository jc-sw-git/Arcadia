using System.Net;
using System.Security.Cryptography;
using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

public sealed class VerifiedDownloadTests : IDisposable
{
    private static readonly byte[] Content = "SISR"u8.ToArray();

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "arcadia-download-" + Guid.NewGuid());

    private string Target => Path.Combine(_directory, "SISR.zip");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task A_download_with_the_published_digest_is_kept()
    {
        using var http = new HttpClient(new Handler(() => new ByteArrayContent(Content)));

        await VerifiedDownload.DownloadAsync(http, Asset(Sha256Of(Content)), Target);

        Assert.Equal(Content, File.ReadAllBytes(Target));
    }

    [Fact]
    public async Task A_download_with_another_digest_is_discarded()
    {
        using var http = new HttpClient(new Handler(() => new ByteArrayContent(Content)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => VerifiedDownload.DownloadAsync(http, Asset(Sha256Of("other"u8.ToArray())), Target));

        Assert.False(File.Exists(Target));
    }

    [Fact]
    public async Task A_download_cut_off_midway_leaves_no_file()
    {
        using var http = new HttpClient(new Handler(() => new StreamContent(new BreakingStream(Content))));

        await Assert.ThrowsAsync<HttpRequestException>(() => VerifiedDownload.DownloadAsync(http, Asset(Sha256Of(Content)), Target));

        Assert.False(File.Exists(Target));
    }

    [Fact]
    public async Task A_cancelled_download_leaves_no_file()
    {
        using var cancel = new CancellationTokenSource();
        using var http = new HttpClient(new Handler(() => new StreamContent(new BreakingStream(Content, cancel))));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => VerifiedDownload.DownloadAsync(http, Asset(Sha256Of(Content)), Target, cancel.Token));

        Assert.False(File.Exists(Target));
    }

    private static ReleaseAsset Asset(string sha256) => new("SISR.zip", new Uri("https://example.invalid/SISR.zip"), Content.Length, sha256);

    private static string Sha256Of(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class Handler(Func<HttpContent> content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content() });
    }

    /// <summary>Hands out its bytes once, then fails like a dropped connection, or cancels first if given a source.</summary>
    private sealed class BreakingStream(byte[] bytes, CancellationTokenSource? cancel = null) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (Position < Length)
            {
                return await base.ReadAsync(buffer, cancellationToken);
            }

            if (cancel is not null)
            {
                await cancel.CancelAsync();
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new IOException("Verbindung abgebrochen.");
        }
    }
}
