namespace Arcadia.Core.Navigation;

/// <summary>
/// The values of a setting's ◀ ▶ selector, e.g. "Zeigergeschwindigkeit 1 … 9": a step past one end comes in at the other,
/// so an arrow never stops working (owner, 2026-10-24).
/// </summary>
public static class ValueCycle
{
    /// <summary>The value <paramref name="delta"/> steps away from <paramref name="value"/> within min … max, wrapping.</summary>
    public static int Step(int value, int delta, int min, int max)
    {
        var count = max - min + 1;
        if (count <= 0)
        {
            return value;
        }

        return min + ((((value - min + delta) % count) + count) % count);
    }
}
