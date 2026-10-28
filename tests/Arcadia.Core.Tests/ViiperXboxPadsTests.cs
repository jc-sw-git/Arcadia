using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
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
        // Both loopbacks: on Windows "localhost" is ::1 first (the Windows CI run, 2026-10-28).
        var listeners = Listen(IPAddress.Loopback, IPAddress.IPv6Loopback);
        var requests = new List<string>();
        using var stop = new CancellationTokenSource();
        var serving = Task.WhenAll(listeners.Select(listener => ServeAsync(listener, requests, stop.Token)));
        var log = new List<string>();
        var pads = new ViiperXboxPads(Path.Combine(Path.GetTempPath(), "nowhere", "viiper.exe"), Path.GetTempPath(), _ => Task.FromResult(0), line => { lock (log) { log.Add(line); } });

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => pads.AddRawDeviceAsync("hidproxy", new Dictionary<string, object?> { ["device"] = "12" }, CancellationToken.None));

        await stop.CancelAsync();
        await serving;
        listeners.ForEach(listener => listener.Dispose());
        Assert.Contains("nicht gestartet", refused.Message, StringComparison.Ordinal);
        lock (requests)
        {
            Assert.DoesNotContain(requests, request => request.StartsWith("bus/", StringComparison.Ordinal));
        }
    }

    [Theory]
    [Trait("TestCase", "T242-4")]
    [InlineData(new[] { 42 }, true)]
    [InlineData(new[] { 42, 42 }, true)]
    [InlineData(new[] { 42, 7 }, false)]
    [InlineData(new[] { 7 }, false)]
    [InlineData(new int[0], false)]
    public void The_copy_runs_only_where_Arcadia_s_server_is_the_only_one_listening(int[] listeners, bool own)
    {
        Assert.Equal(own, ViiperXboxPads.OnlyOwnServerListens(listeners, ownServer: 42));
    }

    [WindowsFact]
    [Trait("TestCase", "T242-4")]
    [SupportedOSPlatform("windows")]
    public void Windows_names_this_process_as_the_one_listening_on_its_port()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        Assert.Equal([Environment.ProcessId], TcpListeners.OwnersOnPort(port));
    }

    /// <summary>A listener on each address the machine has; a container without IPv6 has no ::1.</summary>
    private static List<TcpListener> Listen(params IPAddress[] addresses)
    {
        var listeners = new List<TcpListener>();
        foreach (var address in addresses)
        {
            try
            {
                var listener = new TcpListener(address, ViiperXboxPads.ApiPort);
                listener.Start();
                listeners.Add(listener);
            }
            catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressFamilyNotSupported && address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // No IPv6 here, so "localhost" can't be ::1 either.
            }
        }

        return listeners;
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
