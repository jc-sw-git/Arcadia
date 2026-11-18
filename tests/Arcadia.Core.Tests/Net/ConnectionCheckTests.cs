using System.Net;
using Arcadia.Core.Net;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests.Net;

/// <summary>#683: the check before a search and after a failed host: no network, no answer, any answer, what is sent.</summary>
public sealed class ConnectionCheckTests
{
    private readonly Hosts _hosts = new();
    private readonly FakeTimeProvider _time = new();

    private ConnectionCheck NewCheck(bool networkUp = true) => new(new HttpClient(_hosts), _time) { NetworkUp = () => networkUp };

    private static readonly Uri Images = new("https://images.example/covers/4100001.png?size=large");
    private static readonly Uri Api = new("https://api.example/v2/grids/steam/4100001");

    [Fact]
    [Trait("TestCase", "T683-1")]
    public async Task Without_a_network_adapter_it_is_offline_at_once_and_asks_no_host()
    {
        Assert.False(await NewCheck(networkUp: false).ReachesAnyAsync(() => [Images], CancellationToken.None));
        Assert.Empty(_hosts.Requests);
    }

    [Fact]
    [Trait("TestCase", "T683-1")]
    public async Task Any_answer_of_a_host_is_online_and_only_its_root_is_asked_without_path_or_query()
    {
        _hosts.Down("images.example");
        _hosts.Answer("api.example", HttpStatusCode.NotFound);

        Assert.True(await NewCheck().ReachesAnyAsync(() => [Images, Api, new Uri("https://api.example/other")], CancellationToken.None));

        Assert.All(_hosts.Requests, r => Assert.Equal((HttpMethod.Head, "/"), (r.Method, r.RequestUri!.PathAndQuery)));
        Assert.Equal(["api.example", "images.example"], _hosts.Requests.Select(r => r.RequestUri!.Host).Order());
    }

    [Fact]
    [Trait("TestCase", "T683-1")]
    public async Task No_host_answering_is_offline_and_a_host_that_hangs_ends_after_the_timeout()
    {
        _hosts.Down("images.example");
        _hosts.Hang("api.example");

        var check = NewCheck().ReachesAnyAsync(() => [Images, Api], CancellationToken.None);
        await _hosts.Hanging.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(check.IsCompleted);
        _time.Advance(ConnectionCheck.Timeout);

        Assert.False(await check.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    [Trait("TestCase", "T683-1")]
    public async Task Nothing_to_ask_or_only_plain_http_is_online_and_sends_nothing()
    {
        Assert.True(await NewCheck().ReachesAnyAsync(() => [], CancellationToken.None));
        Assert.True(await NewCheck().ReachesAnyAsync(() => [new Uri("http://images.example/a.png")], CancellationToken.None));
        Assert.Empty(_hosts.Requests);
    }

    [Fact]
    [Trait("TestCase", "T683-1")]
    public async Task At_most_four_hosts_are_asked()
    {
        var many = Enumerable.Range(1, 6).Select(i => new Uri($"https://host{i}.example/a.png")).ToList();
        foreach (var address in many)
        {
            _hosts.Down(address.Host);
        }

        Assert.False(await NewCheck().ReachesAnyAsync(() => many, CancellationToken.None));
        Assert.Equal(ConnectionCheck.MostHosts, _hosts.Requests.Count);
    }

    /// <summary>Answers per host: a status, no connection, or a hang until cancelled; anything else is down.</summary>
    private sealed class Hosts : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task<HttpResponseMessage>>> _answers = new(StringComparer.OrdinalIgnoreCase);

        public List<HttpRequestMessage> Requests { get; } = [];

        public TaskCompletionSource Hanging { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Answer(string host, HttpStatusCode status) => _answers[host] = _ => Task.FromResult(new HttpResponseMessage(status));

        public void Down(string host) => _answers[host] = _ => throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.");

        public void Hang(string host) => _answers[host] = async cancel =>
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

            return _answers.TryGetValue(request.RequestUri!.Host, out var answer)
                ? answer(cancellationToken)
                : throw new HttpRequestException(HttpRequestError.ConnectionError, "No connection could be made.");
        }
    }
}
