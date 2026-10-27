using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.Services;

/// <summary>
/// How Arcadia names and draws the kinds of library entries (#141): the type's name, its icon in <c>Theme.axaml</c> and
/// what A says. A type Arcadia doesn't know yet (from a plugin) is shown as software.
/// </summary>
public static class GameTypeTexts
{
    /// <summary>"Werkzeug", for the preview's line; the detail page's chip writes it in capitals.</summary>
    public static string Name(GameType type) => type switch
    {
        GameType.Game => "Spiel",
        GameType.Tool => "Werkzeug",
        GameType.Soundtrack => "Soundtrack",
        _ => "Software",
    };

    /// <summary>"331 Spiele", "1 Werkzeug", for the count line's tooltip.</summary>
    public static string Count(GameType type, int count)
    {
        var name = (type, count == 1) switch
        {
            (GameType.Game, true) => "Spiel",
            (GameType.Game, false) => "Spiele",
            (GameType.Tool, true) => "Werkzeug",
            (GameType.Tool, false) => "Werkzeuge",
            (GameType.Soundtrack, true) => "Soundtrack",
            (GameType.Soundtrack, false) => "Soundtracks",
            _ => "Software",
        };
        return $"{Formatting.Count(count)} {name}";
    }

    /// <summary>The resource key of the type's icon, drawn by <c>ctl:LineIcon</c>.</summary>
    public static string IconKey(GameType type) => type switch
    {
        GameType.Game => "IconTypeGame",
        GameType.Tool => "IconTypeTool",
        GameType.Soundtrack => "IconTypeSoundtrack",
        _ => "IconTypeSoftware",
    };

    /// <summary>What A does with an installed entry (owner, 2026-10-26), the same on the button, in the menu and in the hint.</summary>
    public static string PrimaryAction(GameType type) => type switch
    {
        GameType.Game => "Spielen",
        GameType.Soundtrack => "Abrocken",
        _ => "Tüfteln",
    };

    /// <summary>
    /// What A does while the entry runs: its window comes back to the front. A tool is no game (owner, 2026-10-27); a
    /// soundtrack never runs in Arcadia, its launcher plays it.
    /// </summary>
    public static string ReturnAction(GameType type) => type == GameType.Game ? "Zurück ins Spiel" : "Weiter tüfteln";
}
