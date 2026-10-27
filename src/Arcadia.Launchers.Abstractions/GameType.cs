namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// What kind of entry a <see cref="Game"/> is (#141). A launcher says it; Arcadia shows the type and words the
/// primary action by it. More kinds may follow (e.g. for plugins); code that meets one it doesn't know treats it
/// like <see cref="Software"/>.
/// </summary>
public enum GameType
{
    Game,

    /// <summary>A program that isn't a game, e.g. Wallpaper Engine or Blender.</summary>
    Software,

    /// <summary>A tool for games or game making, e.g. an engine, an editor, an SDK or SteamVR.</summary>
    Tool,

    /// <summary>A game's music, played by its launcher.</summary>
    Soundtrack,
}
