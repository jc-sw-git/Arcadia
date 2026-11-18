using System.Net.NetworkInformation;

namespace Arcadia.Core.Net;

/// <summary>
/// Whether the hosts a search or load is about to ask can be reached (#683): checked before the player's search starts and
/// when a run's first host fails, so without internet nothing waits for one timeout per host and the player is told.
/// </summary>
public interface IConnectionCheck
{
    /// <summary>
    /// True when one of the hosts answers at all (any HTTP status), or there is none to ask; false without a network or
    /// when none answers within <see cref="ConnectionCheck.Timeout"/>. Never throws but for <paramref name="cancel"/>.
    /// </summary>
    /// <param name="addresses">The addresses the work would ask; read off the caller's thread (it may read the disk).</param>
    Task<bool> ReachesAnyAsync(Func<IReadOnlyCollection<Uri>> addresses, CancellationToken cancel);
}

/// <summary>
/// The real check: no network adapter up is offline at once; otherwise a <c>HEAD</c> to the root of each host (at most
/// <see cref="MostHosts"/>, only <c>https</c>, in parallel) with the client of the work itself, so its proxy and handler
/// apply. The request holds no path, query or account data: only the host the work would ask anyway learns of it.
/// </summary>
public sealed class ConnectionCheck(HttpClient http, TimeProvider time) : IConnectionCheck
{
    /// <summary>The longest the check takes; a host that answers at all does so well within it.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>The most hosts asked at once.</summary>
    public const int MostHosts = 4;

    /// <summary>Always online, asking nothing: for a service the App didn't give the real check (the tests).</summary>
    public static IConnectionCheck Assumed { get; } = new AssumedOnline();

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>Whether a network adapter is up; the tests put their own in.</summary>
    public Func<bool> NetworkUp { get; init; } = IsAnyAdapterUp;

    public async Task<bool> ReachesAnyAsync(Func<IReadOnlyCollection<Uri>> addresses, CancellationToken cancel)
    {
        if (!NetworkUp())
        {
            Log("Verbindung: kein Netzwerk");
            return false;
        }

        var hosts = Roots(await Task.Run(addresses, cancel));
        if (hosts.Count == 0)
        {
            return true;
        }

        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancel, timeout.Token);
        var probes = hosts.Select(host => AnswersAsync(host, linked.Token)).ToList();
        while (probes.Count > 0)
        {
            var done = await Task.WhenAny(probes);
            if (await done)
            {
                // The others aren't needed any more; each ends quietly on the cancellation.
                await linked.CancelAsync();
                return true;
            }

            probes.Remove(done);
        }

        cancel.ThrowIfCancellationRequested();
        Log($"Verbindung: keine Antwort von {string.Join(", ", hosts.Select(h => h.IdnHost))} in {Timeout.TotalSeconds:0} s");
        return false;
    }

    /// <summary>One root per host, only <c>https</c>, at most <see cref="MostHosts"/>.</summary>
    private static List<Uri> Roots(IReadOnlyCollection<Uri> addresses) =>
        [.. addresses
            .Where(a => a.IsAbsoluteUri && a.Scheme == Uri.UriSchemeHttps)
            .Select(a => new UriBuilder(Uri.UriSchemeHttps, a.IdnHost, a.Port).Uri)
            .DistinctBy(a => a.Authority, StringComparer.OrdinalIgnoreCase)
            .Take(MostHosts)];

    private async Task<bool> AnswersAsync(Uri root, CancellationToken cancel)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, root);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
            return true;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
        {
            return false;
        }
    }

    private static bool IsAnyAdapterUp()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch (NetworkInformationException)
        {
            // The system can't say: the hosts decide.
            return true;
        }
    }

    private sealed class AssumedOnline : IConnectionCheck
    {
        public Task<bool> ReachesAnyAsync(Func<IReadOnlyCollection<Uri>> addresses, CancellationToken cancel) => Task.FromResult(true);
    }
}
