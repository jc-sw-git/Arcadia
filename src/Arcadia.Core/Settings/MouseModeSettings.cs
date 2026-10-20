namespace Arcadia.Core.Settings;

/// <summary>The "Mausmodus" box in the settings (#64). The mode itself isn't saved: Arcadia always starts in the normal mode.</summary>
public sealed record MouseModeSettings
{
    /// <summary>The range of <see cref="HoldSeconds"/> and <see cref="PointerSpeed"/>.</summary>
    public const int Min = 1;

    public const int Max = 9;

    /// <summary>"Mausmodus": off, the mouse mode is gone entirely; the other settings keep their values but count for nothing.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>"Mit Back + Start umschalten". Off for games that use the combination themselves.</summary>
    public bool ComboEnabled { get; init; } = true;

    /// <summary>"Haltedauer": how long Back + Start must be held.</summary>
    public int HoldSeconds { get; init; } = 3;

    /// <summary>"Zeigergeschwindigkeit".</summary>
    public int PointerSpeed { get; init; } = 5;

    /// <summary>"Beenden, wenn Arcadia nach vorne kommt".</summary>
    public bool EndWhenArcadiaInFront { get; init; } = true;

    /// <summary>"Automatisch einschalten, wenn Arcadia den Fokus verliert" (except to a game).</summary>
    public bool StartWhenArcadiaLosesFocus { get; init; }

    /// <summary>"Steams Desktop-Belegung ignorieren": the mouse and keys Steam makes of the controller outside games are dropped.</summary>
    public bool IgnoreSteamDesktopLayout { get; init; } = true;

    /// <summary>Values out of range (edited by hand, or from a newer Arcadia) fall back to the defaults.</summary>
    public MouseModeSettings WithValidValues()
    {
        var defaults = new MouseModeSettings();
        return this with
        {
            HoldSeconds = HoldSeconds is >= Min and <= Max ? HoldSeconds : defaults.HoldSeconds,
            PointerSpeed = PointerSpeed is >= Min and <= Max ? PointerSpeed : defaults.PointerSpeed,
        };
    }
}
