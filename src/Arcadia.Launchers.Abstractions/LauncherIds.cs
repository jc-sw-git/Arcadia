namespace Arcadia.Launchers.Abstractions;

/// <summary>
/// The rule for <see cref="LauncherInfo.Id"/>: lower-case letters, digits and hyphens. A launcher id is the part of a
/// <see cref="Game.Key"/> before the first colon and is kept in settings.json, so it may hold neither a colon nor
/// anything that reads differently in another spelling.
/// </summary>
public static class LauncherIds
{
    /// <summary>Why <paramref name="id"/> can't be a launcher id, or null if it can.</summary>
    public static string? Problem(string? id) =>
        string.IsNullOrEmpty(id) ? "Die Launcher-Id ist leer."
        : !id.All(IsIdCharacter) ? $"Die Launcher-Id „{id}“ darf nur a–z, 0–9 und - enthalten."
        : null;

    private static bool IsIdCharacter(char c) => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
}
