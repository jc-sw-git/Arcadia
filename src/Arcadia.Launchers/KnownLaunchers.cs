using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Epic;
using Arcadia.Launchers.Steam;

namespace Arcadia.Launchers;

/// <summary>
/// The launchers that ship with Arcadia, in their tab order: Steam and Epic with an integration of their own, the others
/// only as demo libraries (<c>--demo</c>) until they get one.
/// </summary>
public static class KnownLaunchers
{
    public static LauncherInfo Steam => SteamIntegration.Info;

    public static LauncherInfo Epic => EpicIntegration.Info;

    public static LauncherInfo Gog { get; } = new("gog", "GOG Galaxy", "gog");

    public static LauncherInfo Ubisoft { get; } = new("ubisoft", "Ubisoft Connect", "ubi");

    public static LauncherInfo Ea { get; } = new("ea", "EA app", "ea");

    public static LauncherInfo Xbox { get; } = new("xbox", "Xbox", "xbox");

    public static LauncherInfo BattleNet { get; } = new("battlenet", "Battle.net", "bnet");

    public static LauncherInfo Amazon { get; } = new("amazon", "Amazon Games", "amazon");

    public static IReadOnlyList<LauncherInfo> All { get; } = [Steam, Epic, Gog, Ubisoft, Ea, Xbox, BattleNet, Amazon];
}
