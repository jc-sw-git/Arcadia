using SteamInputProbe;

// Spike #112: can Arcadia read the Steam Controller through Steam Input itself, instead of running SISR?
// Usage:
//   SteamInputProbe steam --marker <appid> [--seconds 60] [--no-overlay] [--no-force] [--force-at 1]
//                         [--release-at s] [--reforce-at s] [--window] [--viiper] [--say "s=text"]... [--log file]
//   SteamInputProbe raw [--seconds 60] [--no-sensors] [--say "s=text"]... [--log file]
//   SteamInputProbe describe [--log file]     (spike #239: USB and HID descriptors of Valve devices)
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
        "describe" => new DescribeProbe(log).Run(),
        _ => new RawProbe(options, log).Run(cancel.Token),
    };
}
catch (Exception ex)
{
    log.Line($"CRASH: {ex}");
    return 3;
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
    public bool Window { get; private init; }
    public bool Viiper { get; private init; }
    public bool Sensors { get; private init; } = true;
    public List<(double At, string Text)> Prompts { get; } = [];

    public static ProbeOptions? Parse(string[] args)
    {
        var mode = args[0];
        if (mode is not ("steam" or "raw" or "describe")) return null;
        var options = new ProbeOptions(mode, Path.Combine(AppContext.BaseDirectory, $"probe-{mode}-{DateTime.Now:HHmmss}.log"));
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
                "--window" => options with { Window = true },
                "--viiper" => options with { Viiper = true },
                "--no-sensors" => options with { Sensors = false },
                "--log" => options with { LogPath = Path.GetFullPath(Next()) },
                "--say" => options.WithPrompt(Next()),
                _ => throw new ArgumentException($"unknown option {args[i]}"),
            };
        }
        return mode == "steam" && options.MarkerAppId == 0 ? null : options;
    }

    private ProbeOptions WithPrompt(string value)
    {
        var split = value.IndexOf('=');
        Prompts.Add((double.Parse(value[..split], System.Globalization.CultureInfo.InvariantCulture), value[(split + 1)..]));
        return this;
    }
}
