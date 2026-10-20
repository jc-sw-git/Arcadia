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

    /// <summary>Values out of range (edited by hand, or from a newer Arcadia) fall back to the defaults.</summary>
    public MouseModeSettings WithValidValues()
    {
        var defaults = new MouseModeSettings();
        return this with
        {
            Activation = Enum.IsDefined(Activation) ? Activation : defaults.Activation,
            HoldSeconds = HoldSeconds is >= Min and <= Max ? HoldSeconds : defaults.HoldSeconds,
            PointerSpeed = PointerSpeed is >= Min and <= Max ? PointerSpeed : defaults.PointerSpeed,
        };
    }
}
