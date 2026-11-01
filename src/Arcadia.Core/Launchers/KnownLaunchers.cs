using Arcadia.Launchers.Abstractions;

namespace Arcadia.Core.Launchers;

/// <summary>The launchers Arcadia knows out of the box.</summary>
public static class KnownLaunchers
{
    // Steam's games read the Steam Controller through Steam Input themselves (#156).
    public static LauncherInfo Steam { get; } = new("steam", "Steam", "steam", GamesUseSteamInput: true);
    public static LauncherInfo Epic { get; } = new("epic", "Epic Games", "epic");
    public static LauncherInfo Gog { get; } = new("gog", "GOG Galaxy", "gog");
    public static LauncherInfo Ubisoft { get; } = new("ubisoft", "Ubisoft Connect", "ubi");
    public static LauncherInfo Ea { get; } = new("ea", "EA app", "ea");
    public static LauncherInfo Xbox { get; } = new("xbox", "Xbox", "xbox");
    public static LauncherInfo BattleNet { get; } = new("battlenet", "Battle.net", "bnet");
    public static LauncherInfo Amazon { get; } = new("amazon", "Amazon Games", "amazon");

    public static IReadOnlyList<LauncherInfo> All { get; } = [Steam, Epic, Gog, Ubisoft, Ea, Xbox, BattleNet, Amazon];
}
