namespace Arcadia.Core.Settings;

/// <summary>"Zurück zu Arcadia": View held brings Arcadia back from the background, with or without the mouse mode (#64).</summary>
public sealed record ReturnWithViewSettings
{
    /// <summary>The range of <see cref="HoldSeconds"/>; 0 is at once.</summary>
    public const int Min = 0;

    public const int Max = 9;

    public bool Enabled { get; init; } = true;

    /// <summary>"Haltedauer": how long View must be held; 0 brings Arcadia back as soon as View is clearly alone.</summary>
    public int HoldSeconds { get; init; } = 1;

    /// <summary>Values out of range (edited by hand, or from a newer Arcadia) fall back to the defaults.</summary>
    public ReturnWithViewSettings WithValidValues() =>
        HoldSeconds is >= Min and <= Max ? this : this with { HoldSeconds = new ReturnWithViewSettings().HoldSeconds };
}
