using System.Diagnostics;
using System.Security.Cryptography;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport;

/// <summary>A <c>viiper.exe</c> Arcadia started; ends it with Arcadia (job object).</summary>
public interface IViiperProcess : IDisposable
{
    int Id { get; }

    bool HasExited { get; }

    /// <summary>Ends it; never throws.</summary>
    void Stop();
}

/// <summary>What starting and checking a VIIPER needs from Windows; a fake in the tests.</summary>
public interface IViiperHost
{
    /// <summary>Ends every <c>viiper.exe</c> of this user running from <paramref name="executable"/> (a crash whose job object failed).</summary>
    void EndLeftovers(string executable);

    /// <summary>Starts it in the job object that ends it with Arcadia; throws, with nothing left running, when either fails.</summary>
    IViiperProcess Start(ProcessStartInfo start);

    /// <summary>Every TCP listener on this machine; throws when Windows won't say.</summary>
    IReadOnlyCollection<TcpListening> Listeners();

    Task<ViiperAnswer> PingAsync(int port, byte[]? key, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Arcadia's running VIIPER after its start was checked: its two ports on <c>127.0.0.1</c> and the key its API takes.</summary>
public sealed class RunningViiper(IViiperProcess process, int apiPort, int usbipPort, string key, byte[] derivedKey) : IDisposable
{
    public int ProcessId => process.Id;

    public int ApiPort { get; } = apiPort;

    /// <summary>usbip-win2 attaches from here; Pan journals it (#304).</summary>
    public int UsbipPort { get; } = usbipPort;

    /// <summary>The key's text, as VIIPER's client takes it. Same-user code could read it from memory (#247's caveat).</summary>
    public string Key { get; } = key;

    /// <summary>The key derived once, for Arcadia's own connections (the copy's stream).</summary>
    public byte[] DerivedKey { get; } = derivedKey;

    public bool HasExited => process.HasExited;

    public void Dispose()
    {
        process.Stop();
        process.Dispose();
    }
}

/// <summary>
/// Starts Arcadia's own <c>viiper.exe</c> and trusts it only once checked (#304): on <c>127.0.0.1</c> with ports
/// Windows assigns, with a fresh key of its own (never VIIPER's shared <c>%APPDATA%\VIIPER</c>), and refusing a client
/// without that key. Any check that fails stops it: no pad, no copy.
/// </summary>
/// <param name="configRoot">
/// Where its VIIPER keeps its key (as <c>%AppData%</c> of the child: <c>VIIPER\viiper.key.txt</c> below it), inside
/// Arcadia's data folder. Also its working directory, so it reads no configuration file from anywhere else.
/// </param>
public sealed class ViiperServer(string executable, string configRoot, string logDirectory, IViiperHost host, Action<string> log)
{
    public static readonly TimeSpan DefaultStartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ListenPoll = TimeSpan.FromMilliseconds(100);

    public string Executable => executable;

    public TimeSpan StartTimeout { get; init; } = DefaultStartTimeout;

    /// <summary>Starts and checks it; throws with what failed, and then nothing of it runs and no key file is left.</summary>
    public async Task<RunningViiper> StartAsync(CancellationToken cancellationToken)
    {
        host.EndLeftovers(executable);
        var key = ViiperKeyFile.NewKey();
        var keyFile = ViiperKeyFile.PathIn(configRoot);
        IViiperProcess? process = null;
        try
        {
            ViiperKeyFile.Write(keyFile, key);
            process = host.Start(StartInfo());
            log($"VIIPER: viiper.exe gestartet (PID {process.Id})");
            var clock = Stopwatch.StartNew();
            var ports = await PortsAsync(process, clock, cancellationToken);
            var derived = ViiperConnection.DeriveKey(key);
            var apiPort = await ApiPortAsync(ports, derived, cancellationToken);
            // VIIPER read its key at the start; the file goes before anything is plugged in.
            ViiperKeyFile.Delete(keyFile);
            await RequireKeyAsync(apiPort, cancellationToken);
            var running = new RunningViiper(process, apiPort, ports.Single(port => port != apiPort), key, derived);
            log($"VIIPER: bereit nach {clock.ElapsedMilliseconds} ms (usbip 127.0.0.1:{running.UsbipPort}, API 127.0.0.1:{apiPort}, Schlüssel geprüft)");
            return running;
        }
        catch (Exception ex)
        {
            process?.Stop();
            process?.Dispose();
            log($"VIIPER: Start fehlgeschlagen, viiper.exe beendet: {ex.Message}");
            throw;
        }
        finally
        {
            ViiperKeyFile.DeleteQuietly(keyFile, log);
        }
    }

    /// <summary>Before a device goes onto it: still exactly its two ports, and nobody else on them. Throws when not.</summary>
    public void CheckListeners(RunningViiper server)
    {
        var (ports, problem) = ViiperListeners.OfServer(host.Listeners(), server.ProcessId);
        if (problem is not null || ports is null || !ports.Contains(server.ApiPort) || !ports.Contains(server.UsbipPort))
        {
            throw Untrusted(problem ?? "its ports changed");
        }
    }

    /// <summary>The detail goes to the log; the message also shows in the settings row.</summary>
    private InvalidOperationException Untrusted(string problem)
    {
        log($"VIIPER: Ports nicht wie erwartet: {problem}");
        return new InvalidOperationException("VIIPER lauscht nicht wie erwartet");
    }

    private ProcessStartInfo StartInfo()
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = configRoot,
            // As flags: an older build would ignore an environment variable it doesn't know and listen on every interface.
            ArgumentList =
            {
                "server", "--no-tray", $"--log.file={Path.Combine(logDirectory, "viiper.log")}",
                "--usb.addr=127.0.0.1:0", "--api.addr=127.0.0.1:0", "--api.require-local-host-auth",
            },
        };
        // Settings the user gave their own VIIPER (auto-attach off, other addresses) must not reach Arcadia's.
        foreach (var name in start.Environment.Keys.Where(name => name.StartsWith("VIIPER_", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(name);
        }

        // VIIPER finds its key below %AppData%: Arcadia's own folder, so another program's VIIPER and its key stay untouched.
        start.Environment["AppData"] = configRoot;
        // VIIPER would offer to replace itself with an upstream release every hour, which lacks the copy's device type.
        start.Environment["VIIPER_UPDATE_NOTIFY"] = "none";
        return start;
    }

    /// <summary>Its two ports once both listen, checked; throws when it ends, listens wrongly or takes too long.</summary>
    private async Task<int[]> PortsAsync(IViiperProcess process, Stopwatch clock, CancellationToken cancellationToken)
    {
        while (true)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException("viiper.exe hat sich gleich wieder beendet");
            }

            var (ports, problem) = ViiperListeners.OfServer(host.Listeners(), process.Id);
            if (problem is not null)
            {
                throw Untrusted(problem);
            }

            if (ports is not null)
            {
                return ports;
            }

            if (clock.Elapsed > StartTimeout)
            {
                throw new TimeoutException($"viiper.exe lauscht nach {clock.Elapsed.TotalSeconds:0} s nicht auf beiden Ports");
            }

            await Task.Delay(ListenPoll, cancellationToken);
        }
    }

    /// <summary>Which port is the API: the one that answers a ping with the key; usbip doesn't speak VIIPER's protocol.</summary>
    private async Task<int> ApiPortAsync(int[] ports, byte[] key, CancellationToken cancellationToken)
    {
        var answers = await Task.WhenAll(ports.Select(port => host.PingAsync(port, key, PingTimeout, cancellationToken)));
        var answering = ports.Where((_, i) => answers[i] == ViiperAnswer.Answered).ToList();
        return answering.Count == 1
            ? answering[0]
            : throw new InvalidOperationException($"VIIPER antwortet nicht mit dem Schlüssel ({string.Join(", ", answers)})");
    }

    /// <summary>
    /// The positive check: without the key, and with a wrong one, VIIPER must refuse. Only a refusal counts; no answer at
    /// all fails too, as it proves nothing.
    /// </summary>
    private async Task RequireKeyAsync(int apiPort, CancellationToken cancellationToken)
    {
        var withoutKey = await host.PingAsync(apiPort, null, PingTimeout, cancellationToken);
        var wrongKey = await host.PingAsync(apiPort, RandomNumberGenerator.GetBytes(32), PingTimeout, cancellationToken);
        if (withoutKey != ViiperAnswer.Refused || wrongKey != ViiperAnswer.Refused)
        {
            throw new InvalidOperationException($"VIIPER verlangt den Schlüssel nicht (ohne: {withoutKey}, falscher: {wrongKey})");
        }
    }
}

/// <summary>The key file VIIPER reads at its start (#304): written fresh before each start, deleted once VIIPER runs.</summary>
public static class ViiperKeyFile
{
    public const string FileName = "viiper.key.txt";

    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>VIIPER looks in <c>%AppData%\VIIPER</c>; Arcadia sets that <c>%AppData%</c> to <paramref name="configRoot"/>.</summary>
    public static string PathIn(string configRoot) => Path.Combine(configRoot, "VIIPER", FileName);

    /// <summary>32 characters from 62 (about 190 bits), unbiased; VIIPER's own are 16.</summary>
    public static string NewKey() => RandomNumberGenerator.GetString(Alphabet, 32);

    /// <summary>
    /// Written to a temporary file and renamed, so VIIPER never reads half a key. Throws when it can't be written: then
    /// VIIPER isn't started, as it would make and log a key of its own.
    /// </summary>
    public static void Write(string path, string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            file.Write(System.Text.Encoding.UTF8.GetBytes(key));
        }

        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Deletes it; throws when it stays (a key left on disk fails the start).</summary>
    public static void Delete(string path)
    {
        File.Delete(path + ".tmp");
        File.Delete(path);
    }

    /// <summary>The last word after any start, failed or not; never throws.</summary>
    public static void DeleteQuietly(string path, Action<string> log)
    {
        try
        {
            Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log($"VIIPER: Schlüsseldatei nicht gelöscht: {ex.Message}");
        }
    }
}
