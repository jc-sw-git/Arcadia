using Arcadia.Core.Launchers;
using Arcadia.Launchers.Abstractions;
using Arcadia.Launchers.Steam.Account;

namespace Arcadia.Launchers.Steam;

/// <summary>
/// Steam: the installed games of all libraries. Games start through <c>steam://rungameid</c>,
/// which starts Steam itself when needed, so there is no client of its own. Installs, updates and
/// uninstalls are followed through the app manifests, running games through Steam's registry, starts Steam aborts
/// through its console log.
/// </summary>
public sealed class SteamIntegration : ILauncherIntegration
{
    private readonly Func<SteamInstallation?> _locate;
    private readonly SteamOnlineAccount? _account;

    /// <param name="time">The clock of the install and running-game monitors.</param>
    /// <param name="locate">Finds Steam; defaults to <see cref="SteamInstallation.Find"/>. The acceptance tests point it at a Steam folder of their own.</param>
    /// <param name="account">The sign-in for the uninstalled games (#83); null offers none.</param>
    public SteamIntegration(TimeProvider time, Func<SteamInstallation?>? locate = null, SteamOnlineAccount? account = null)
    {
        _locate = locate ?? SteamInstallation.Find;
        _account = account;
        Scanner = new SteamScanner(locate, account);
        InstallMonitor = new SteamInstallMonitor(locate, time);
        RunningGames = new SteamRunningGames(time, consoleLog: new SteamConsoleLog(() => _locate()?.ConsoleLogPath));

        // A switch of the account in Steam shows at once, not only with the next rescan (owner, 2026-10-27).
        account?.FollowClient(() => _locate()?.ActiveUserId);
    }

    /// <summary>
    /// Steam's logo (#138) from simple-icons (CC0), the shape Valve publishes; the name and the logo are Valve's
    /// trademarks (THIRD-PARTY-NOTICES.md).
    /// </summary>
    private const string Logo =
        "M11.979 0C5.678 0 .511 4.86.022 11.037l6.432 2.658c.545-.371 1.203-.59 1.912-.59.063 0 .125.004.188.006l2.861-4.142V8.91c0-2.495 2.028-4.524 4.524-4.524 2.494 0 4.524 2.031 4.524 4.527s-2.03 4.525-4.524 4.525h-.105l-4.076 2.911c0 .052.004.105.004.159 0 1.875-1.515 3.396-3.39 3.396-1.635 0-3.016-1.173-3.331-2.727L.436 15.27C1.862 20.307 6.486 24 11.979 24c6.627 0 11.999-5.373 11.999-12S18.605 0 11.979 0zM7.54 18.21l-1.473-.61c.262.543.714.999 1.314 1.25 1.297.539 2.793-.076 3.332-1.375.263-.63.264-1.319.005-1.949s-.75-1.121-1.377-1.383c-.624-.26-1.29-.249-1.878-.03l1.523.63c.956.4 1.409 1.5 1.009 2.455-.397.957-1.497 1.41-2.454 1.012H7.54zm11.415-9.303c0-1.662-1.353-3.015-3.015-3.015-1.665 0-3.015 1.353-3.015 3.015 0 1.665 1.35 3.015 3.015 3.015 1.663 0 3.015-1.35 3.015-3.015zm-5.273-.005c0-1.252 1.013-2.266 2.265-2.266 1.249 0 2.266 1.014 2.266 2.266 0 1.251-1.017 2.265-2.266 2.265-1.253 0-2.265-1.014-2.265-2.265z";

    public LauncherInfo Launcher { get; } = KnownLaunchers.Steam with { Logo = Logo };

    public ILauncherScanner Scanner { get; }

    public ILauncherAccount? Account => _account;

    public ILauncherClient? Client => null;

    public IInstallMonitor? InstallMonitor { get; }

    public IRunningGamesMonitor? RunningGames { get; }

    public IReadOnlyCollection<string> ProgramNames { get; } = ["steam.exe", "steamwebhelper.exe"];

    public string? Locate() => _locate()?.RootPath;

    public ILauncherFolderChoice? FolderChoice { get; } = new SteamFolderChoice();

    /// <summary>One switch per category Steam shows beside its games, each off at first (owner, 2026-10-22).</summary>
    public IReadOnlyList<LibraryTypeOption> LibraryOptions { get; } =
    [
        new(
            "tools",
            "Werkzeuge einbeziehen",
            "Etwa SteamVR und SDKs",
            "Steam führt unter „Werkzeuge“ Programme für Spiele und ihre Entwicklung. Eingeschaltet erscheinen sie auch in Arcadia, mit Steam-Konto auch die nicht installierten; Laufzeitumgebungen und Redistributables bleiben immer draußen:",
            ["SteamVR", "Source SDK", "Dedizierte Server"],
            [GameType.Tool]),
        new(
            "software",
            "Software einbeziehen",
            "Etwa Wallpaper Engine und Blender",
            "Steam verkauft auch Programme, die keine Spiele sind. Eingeschaltet erscheinen sie auch in Arcadia, mit Steam-Konto auch die nicht installierten:",
            ["Wallpaper Engine", "Blender", "RPG Maker"],
            [GameType.Software]),
        new(
            "soundtracks",
            "Soundtracks einbeziehen",
            "Die Soundtracks deiner Spiele",
            "Steam installiert Soundtracks wie Spiele. Eingeschaltet erscheinen sie auch in Arcadia, mit Steam-Konto auch die nicht installierten; [A] öffnet einen Soundtrack in Steam, das ihn abspielt.",
            [],
            [GameType.Soundtrack]),
    ];
}
