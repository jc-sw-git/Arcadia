namespace Arcadia.Core.Settings;

/// <summary>How the mouse mode is switched on: the row "Mausmodus" (owner, 2026-10-20). Only one way decides, so a choice made by hand isn't undone by the next focus change.</summary>
public enum MouseModeActivation
{
    /// <summary>Never; the other settings keep their values but count for nothing.</summary>
    Off,

    /// <summary>Back + Start held switches it.</summary>
    Manual,

    /// <summary>
    /// On when Arcadia loses the focus to something that isn't a game, off when Arcadia or a game
    /// comes to the front. Back + Start overrides it until then, e.g. for a game Arcadia doesn't know.
    /// </summary>
    Automatic,
}

/// <summary>"Beenden des Mausmodus in Spielen erzwingen", only for "Von Hand" (owner, 2026-10-20 and 2026-10-21).</summary>
public enum MouseModeInGames
{
    /// <summary>"Nein": Back + Start switches the mouse mode on in games too.</summary>
    Allowed,

    /// <summary>"Ja": not in a game Arcadia knows; a game coming to the front ends it.</summary>
    Blocked,

    /// <summary>"Individuell": each game decides on its detail page, else <see cref="MouseModeSettings.PerGameDefaultAllowed"/>.</summary>
    PerGame,
}

/// <summary>The "Mausmodus" box in the settings (#64). The mode itself isn't saved: Arcadia always starts in the normal mode.</summary>
public sealed record MouseModeSettings
{
    /// <summary>The range of <see cref="HoldSeconds"/> and <see cref="PointerSpeed"/>.</summary>
    public const int Min = 1;

    public const int Max = 9;

    public MouseModeActivation Activation { get; init; } = MouseModeActivation.Manual;

    /// <summary>"Haltedauer": how long Back + Start must be held.</summary>
    public int HoldSeconds { get; init; } = 3;

    /// <summary>"Zeigergeschwindigkeit".</summary>
    public int PointerSpeed { get; init; } = 5;

    /// <summary>"Beenden, wenn Arcadia nach vorne kommt"; only for <see cref="MouseModeActivation.Manual"/>, the automatic mode always ends then.</summary>
    public bool EndWhenArcadiaInFront { get; init; } = true;

    /// <summary>"Beenden des Mausmodus in Spielen erzwingen"; only for <see cref="MouseModeActivation.Manual"/>.</summary>
    public MouseModeInGames InGames { get; init; } = MouseModeInGames.Allowed;

    /// <summary>"Standardeinstellung bei „Individuell“": "Erlaubt" (true) or "Deaktiviert".</summary>
    public bool PerGameDefaultAllowed { get; init; } = true;

    /// <summary>"Individueller Mausmodus" per game, keyed by <c>Game.Key</c>: allowed or not; a missing key is "Standard".</summary>
    public Dictionary<string, bool> GameChoices { get; init; } = [];

    /// <summary>
    /// Whether Back + Start may switch the mouse mode on while the game <paramref name="gameKey"/> is in
    /// front (null: no game). The choice applies only to "Von Hand"; the automatic mode ends in games anyway.
    /// </summary>
    public bool AllowedIn(string? gameKey) =>
        gameKey is null || Activation != MouseModeActivation.Manual || InGames switch
        {
            MouseModeInGames.Blocked => false,
            MouseModeInGames.PerGame => GameChoiceFor(gameKey) ?? PerGameDefaultAllowed,
            _ => true,
        };

    /// <summary>Null is "Standard".</summary>
    public bool? GameChoiceFor(string gameKey) => GameChoices.TryGetValue(gameKey, out var allowed) ? allowed : null;

    public MouseModeSettings WithGameChoice(string gameKey, bool? allowed)
    {
        var choices = new Dictionary<string, bool>(GameChoices);
        if (allowed is { } value)
        {
            choices[gameKey] = value;
        }
        else
        {
            choices.Remove(gameKey);
        }

        return this with { GameChoices = choices };
    }

    /// <summary>Values out of range (edited by hand, or from a newer Arcadia) fall back to the defaults.</summary>
    public MouseModeSettings WithValidValues()
    {
        var defaults = new MouseModeSettings();
        return this with
        {
            Activation = Enum.IsDefined(Activation) ? Activation : defaults.Activation,
            InGames = Enum.IsDefined(InGames) ? InGames : defaults.InGames,
            GameChoices = GameChoices ?? [],
            HoldSeconds = HoldSeconds is >= Min and <= Max ? HoldSeconds : defaults.HoldSeconds,
            PointerSpeed = PointerSpeed is >= Min and <= Max ? PointerSpeed : defaults.PointerSpeed,
        };
    }
}
