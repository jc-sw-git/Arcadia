using System.Net;

namespace Arcadia.Core.Tests.Artwork;

/// <summary>
/// An image server for the artwork tests (#487): answers per address with an image, an error, a hang until cancelled, or no
/// network; records every request. It models only what Arcadia asks of Steam's image server (a GET by address), not its
/// caching or redirects.
/// </summary>
internal sealed class FakeImageServer : HttpMessageHandler
{
    private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _answers = new(StringComparer.Ordinal);

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Every request fails as with no network.</summary>
    public bool Offline { get; set; }

    /// <summary>Set when a request waits for its cancellation.</summary>
    public TaskCompletionSource Hanging { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Serve(string url, byte[] content) =>
        _answers[url] = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });

    public void Fail(string url, HttpStatusCode status) => _answers[url] = _ => Task.FromResult(new HttpResponseMessage(status));

    public void Hang(string url) => _answers[url] = async cancel =>
    {
        Hanging.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancel);
        throw new InvalidOperationException("unreachable");
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (Requests)
        {
            Requests.Add(request);
        }

        if (Offline)
        {
            throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.");
        }

        return _answers.TryGetValue(request.RequestUri!.AbsoluteUri, out var answer)
            ? answer(cancellationToken)
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
