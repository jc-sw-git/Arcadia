using SteamInputProbe;

// Spike #112: can Arcadia read the Steam Controller through Steam Input itself, instead of running SISR?
// Usage:
// Every mode also takes --stopfile <file>: it ends as soon as that file exists (spike #239's runner uses it).
//   SteamInputProbe steam --marker <appid> [--seconds 60] [--no-overlay] [--no-force] [--force-at 1]
//                         [--release-at s] [--reforce-at s] [--window] [--viiper] [--say "s=text"]... [--log file]
//   SteamInputProbe raw [--seconds 60] [--no-sensors] [--say "s=text"]... [--log file]
//   SteamInputProbe describe [--json file] [--log file]   (spike #239: USB and HID descriptors of Valve devices)
//   SteamInputProbe uac [--seconds 1800] [--poll 100] [--log file]   (spike #118: answer UAC with a VIIPER keyboard/mouse)
if (args.Length == 0 || ProbeOptions.Parse(args) is not { } options)
{
    Console.WriteLine("usage: SteamInputProbe steam --marker <appid> [options] | raw [options] | describe  (see Program.cs)");
    return 1;
}

using var log = new ProbeLog(options.LogPath);
log.Line($"SteamInputProbe {string.Join(' ', args)}");
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};
try
{
    return options.Mode switch
    {
        "steam" => await new SteamPadProbe(options, log).RunAsync(cancel.Token),
        "describe" => new DescribeProbe(log, options.JsonPath).Run(),
        "uac" => await RunUacAsync(options, log, cancel.Token),
        _ => new RawProbe(options, log).Run(cancel.Token),
    };
}
catch (Exception ex)
{
    log.Line($"CRASH: {ex}");
    return 3;
}

static async Task<int> RunUacAsync(ProbeOptions options, ProbeLog log, CancellationToken cancel)
{
    await using var probe = new UacProbe(options, log);
    return await probe.RunAsync(cancel);
}

internal sealed record ProbeOptions(string Mode, string LogPath)
{
    public uint MarkerAppId { get; private init; }
    public double Seconds { get; private init; } = 60;
    public bool LoadOverlay { get; private init; } = true;
    public bool Force { get; private init; } = true;
    public double ForceAt { get; private init; } = 1;
    public double? ReleaseAt { get; private init; }
    public double? ReforceAt { get; private init; }
    public int PollMs { get; private init; } = 100;
    public bool Window { get; private init; }
    public bool Viiper { get; private init; }
    public bool Sensors { get; private init; } = true;
    public string? JsonPath { get; private init; }
    public string? StopFile { get; private init; }
    public List<(double At, string Text)> Prompts { get; } = [];

    public static ProbeOptions? Parse(string[] args)
    {
        var mode = args[0];
        if (mode is not ("steam" or "raw" or "describe" or "uac")) return null;
        var options = new ProbeOptions(mode, Path.Combine(AppContext.BaseDirectory, $"probe-{mode}-{DateTime.Now:HHmmss}.log"));
        if (mode == "uac") options = options with { Seconds = 1800 }; // the UAC probe waits for the owner; a long default
        for (var i = 1; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value");
            options = args[i] switch
            {
                "--marker" => options with { MarkerAppId = uint.Parse(Next()) },
                "--seconds" => options with { Seconds = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture) },
                "--no-overlay" => options with { LoadOverlay = false },
                "--no-force" => options with { Force = false },
                "--force-at" => options with { ForceAt = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture) },
                "--release-at" => options with { ReleaseAt = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture) },
                "--reforce-at" => options with { ReforceAt = double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture) },
                "--poll" => options with { PollMs = int.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture) },
                "--window" => options with { Window = true },
                "--viiper" => options with { Viiper = true },
                "--no-sensors" => options with { Sensors = false },
                "--log" => options with { LogPath = Path.GetFullPath(Next()) },
                "--json" => options with { JsonPath = Path.GetFullPath(Next()) },
                "--stopfile" => options with { StopFile = Path.GetFullPath(Next()) },
                "--say" => options.WithPrompt(Next()),
                _ => throw new ArgumentException($"unknown option {args[i]}"),
            };
        }
        return mode == "steam" && options.MarkerAppId == 0 ? null : options;
    }

    public bool ShouldStop(ProbeLog log) => log.ElapsedMs >= Seconds * 1000 || (StopFile is not null && File.Exists(StopFile));

    private ProbeOptions WithPrompt(string value)
    {
        var split = value.IndexOf('=');
        Prompts.Add((double.Parse(value[..split], System.Globalization.CultureInfo.InvariantCulture), value[(split + 1)..]));
        return this;
    }
}
