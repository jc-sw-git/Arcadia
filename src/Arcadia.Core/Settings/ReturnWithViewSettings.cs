namespace Arcadia.Core.Settings;

/// <summary>"Zurück zu Arcadia": View held brings Arcadia back from the background, with or without the mouse mode (#64).</summary>
public sealed record ReturnWithViewSettings
{
    /// <summary>
    /// The range of <see cref="HoldSeconds"/>. No "at once" (owner, 2026-10-22): a tap of View is free for the
    /// on-screen keyboard of the mouse mode, which it moves to the other edge of the screen (#110).
    /// </summary>
    public const int Min = 1;

    public const int Max = 9;

    public bool Enabled { get; init; } = true;

    /// <summary>"Haltedauer": how long View must be held.</summary>
    public int HoldSeconds { get; init; } = 1;

    /// <summary>
    /// Values out of range (edited by hand, from a newer Arcadia, or 0 for "Sofort" from an older one) fall back
    /// to the default, 1 s.
    /// </summary>
    public ReturnWithViewSettings WithValidValues() =>
        HoldSeconds is >= Min and <= Max ? this : this with { HoldSeconds = new ReturnWithViewSettings().HoldSeconds };
}
