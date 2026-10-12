using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Demo;
using Arcadia.Launchers.Epic;
using Arcadia.Launchers.Steam;

namespace Arcadia.Launchers;

/// <summary>The launchers that ship with Arcadia. A new launcher is added here and nowhere else.</summary>
public static class BuiltInLaunchers
{
    // Deliberately different sizes, so paging and the page size rule can be tried out.
    private static readonly Dictionary<string, int> DemoGameCounts = new(StringComparer.Ordinal)
    {
        [KnownLaunchers.Gog.Id] = 31,
        [KnownLaunchers.Ubisoft.Id] = 9,
        [KnownLaunchers.Ea.Id] = 14,
        [KnownLaunchers.Xbox.Id] = 62,
        [KnownLaunchers.BattleNet.Id] = 21,
        [KnownLaunchers.Amazon.Id] = 6,
    };

    /// <param name="demo">Adds a demo library for every known launcher that has no integration yet (<c>--demo</c>).</param>
    /// <returns>In the order of <see cref="KnownLaunchers.All"/>, which is the tab order.</returns>
    public static IReadOnlyList<ILauncherIntegration> Create(TimeProvider time, bool demo)
    {
        ILauncherIntegration[] integrations =
        [
            new SteamIntegration(),
            new EpicIntegration(time),
        ];

        var ordered = new List<ILauncherIntegration>();
        foreach (var launcher in KnownLaunchers.All)
        {
            if (integrations.FirstOrDefault(i => i.Launcher.Id == launcher.Id) is { } integration)
            {
                ordered.Add(integration);
            }
            else if (demo && DemoGameCounts.TryGetValue(launcher.Id, out var count))
            {
                ordered.Add(new DemoIntegration(launcher, count, time.GetLocalNow()));
            }
        }

        ordered.AddRange(integrations.Where(i => !ordered.Contains(i)));
        return ordered;
    }
}
