using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.Launchers.Demo;

/// <summary>
/// Generates a fictional library for one launcher, so the UI can be tried out without any
/// launcher installed. Enabled with <c>--demo</c>. The data is deterministic per launcher.
/// </summary>
public sealed class DemoScanner(LauncherInfo launcher, int gameCount, DateTimeOffset now) : ILauncherScanner
{
    private static readonly string[] Adjectives =
    [
        "Hollow", "Iron", "Silent", "Crimson", "Distant", "Broken", "Neon", "Frozen", "Golden", "Shattered",
        "Last", "Wild", "Echo", "Hidden", "Burning", "Sunken", "Endless", "Pale", "Stellar", "Rogue",
    ];

    private static readonly string[] Nouns =
    [
        "Frontier", "Tides", "Kingdom", "Signal", "Harbor", "Protocol", "Crown", "Horizon", "Vault", "Odyssey",
        "Requiem", "Drift", "Citadel", "Orbit", "Legacy", "Wilds", "Circuit", "Ember", "Expanse", "Outpost",
    ];

    public LauncherInfo Launcher => launcher;

    public Task<IReadOnlyList<Game>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var random = new Random(StableSeed(launcher.Id));
        var titles = new HashSet<string>(StringComparer.Ordinal);
        var games = new List<Game>(gameCount);
        var maxTitles = Adjectives.Length * Nouns.Length * 3;

        while (games.Count < Math.Min(gameCount, maxTitles))
        {
            var title = $"{Adjectives[random.Next(Adjectives.Length)]} {Nouns[random.Next(Nouns.Length)]}";
            var suffix = random.NextDouble();
            if (suffix < 0.1)
            {
                title += " II";
            }
            else if (suffix < 0.16)
            {
                title += ": Remastered";
            }

            if (!titles.Add(title))
            {
                continue;
            }

            // Roughly a third of a library is owned but not installed.
            if (random.NextDouble() < 0.3)
            {
                games.Add(new Game
                {
                    Id = $"demo-{games.Count}",
                    LauncherId = launcher.Id,
                    Title = title,
                    Launch = new LaunchTarget.None(),
                    IsInstalled = false,
                    Install = new LaunchTarget.None(),
                    OpenInLauncher = new LaunchTarget.None(),
                });
                continue;
            }

            var installedDaysAgo = random.Next(1, 1100);
            var neverPlayed = random.NextDouble() < 0.15;
            var playedDaysAgo = neverPlayed ? (int?)null : random.Next(0, Math.Min(installedDaysAgo, 420));
            var sizeGb = 2 + (random.NextDouble() * 118);

            games.Add(new Game
            {
                Id = $"demo-{games.Count}",
                LauncherId = launcher.Id,
                Title = title,
                Launch = new LaunchTarget.None(),
                OpenInLauncher = new LaunchTarget.None(),

                // As the real launchers: Epic can only uninstall in its own window.
                Uninstall = launcher.Id == KnownLaunchers.Epic.Id ? null : new LaunchTarget.None(),
                InstallDirectory = Path.Combine("D:", "Games", launcher.DisplayName, title.Replace(":", string.Empty, StringComparison.Ordinal)),
                InstalledAt = now.AddDays(-installedDaysAgo),
                LastPlayedAt = playedDaysAgo is { } days ? now.AddDays(-days).AddHours(-random.Next(0, 20)) : null,
                Playtime = neverPlayed ? null : TimeSpan.FromHours(1 + random.Next(0, 160)),
                InstallSizeBytes = (long)(sizeGb * 1024 * 1024 * 1024),
                UpdateAvailable = random.NextDouble() < 0.12,
            });
        }

        return Task.FromResult<IReadOnlyList<Game>>(games);
    }

    // string.GetHashCode is randomized per process; this keeps demo data identical across runs.
    private static int StableSeed(string text)
    {
        var hash = 17;
        foreach (var c in text)
        {
            hash = unchecked((hash * 31) + c);
        }

        return hash;
    }
}
