using System.Net;
using System.Net.Sockets;
using System.Text;
using Arcadia.ControllerSupport;

namespace Arcadia.Core.Tests;

/// <summary>Which VIIPER server the copy of the puck may use (#242, QE review of PR #294).</summary>
[Collection(ViiperPortCollection.Name)]
public sealed class ViiperXboxPadsTests
{
    [Fact]
    [Trait("TestCase", "T242-4")]
    public async Task The_copy_is_not_made_on_a_VIIPER_Arcadia_did_not_start()
    {
        using var foreign = new TcpListener(IPAddress.Loopback, ViiperXboxPads.ApiPort);
        foreign.Start();
        var requests = new List<string>();
        using var stop = new CancellationTokenSource();
        var serving = ServeAsync(foreign, requests, stop.Token);
        var log = new List<string>();
        var pads = new ViiperXboxPads(Path.Combine(Path.GetTempPath(), "nowhere", "viiper.exe"), Path.GetTempPath(), _ => Task.FromResult(0), line => { lock (log) { log.Add(line); } });

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => pads.AddRawDeviceAsync("hidproxy", new Dictionary<string, object?> { ["device"] = "12" }, CancellationToken.None));

        await stop.CancelAsync();
        await serving;
        Assert.Contains("nicht gestartet", refused.Message, StringComparison.Ordinal);
        lock (requests)
        {
            Assert.DoesNotContain(requests, request => request.StartsWith("bus/", StringComparison.Ordinal));
        }
    }

    /// <summary>Answers every request like a VIIPER would answer a ping, and notes what was asked.</summary>
    private static async Task ServeAsync(TcpListener listener, List<string> requests, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer, cancellationToken);
                lock (requests)
                {
                    requests.Add(Encoding.UTF8.GetString(buffer, 0, read).TrimEnd('\0', '\n'));
                }

                await stream.WriteAsync(Encoding.UTF8.GetBytes("{\"server\":\"VIIPER\",\"version\":\"0.0.0\"}\n"), cancellationToken);
                client.Client.Shutdown(SocketShutdown.Send);
            }
        }
        catch (OperationCanceledException)
        {
            // Done.
        }
    }
}

/// <summary>Tests that bind VIIPER's fixed API port run one at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ViiperPortCollection
{
    public const string Name = "VIIPER-Port";
}
