namespace Arcadia.Input;

/// <summary>
/// The USB ids of the PlayStation and Switch pads Arcadia has plugged in for games right now (#238 part 2). They carry
/// the ids of real controllers; normally their place on the software USB host tells them apart. Where Windows' device
/// tree can't be read, a pad with these ids counts as Arcadia's own, so a pad never gets a pad of its own and they
/// can't multiply. Thread-safe.
/// </summary>
public sealed class OwnPadModels
{
    private readonly Dictionary<(ushort Vendor, ushort Product), int> _plugged = [];

    /// <summary>VIIPER's ids for the kind, or null for the Xbox 360 pad, which XInput's slots tell apart.</summary>
    public static (ushort Vendor, ushort Product)? IdsOf(PadKind kind) => kind switch
    {
        PadKind.DualShock4 => (0x054C, 0x09CC),
        PadKind.DualSense => (0x054C, 0x0CE6),
        PadKind.DualSenseEdge => (0x054C, 0x0DF2),
        PadKind.Switch2Pro => (0x057E, 0x2069),
        _ => null,
    };

    public void Plugged(PadKind kind) => Change(kind, +1);

    public void Unplugged(PadKind kind) => Change(kind, -1);

    /// <summary>Whether a pad of Arcadia's with these ids is plugged in.</summary>
    public bool Has(ushort vendor, ushort product)
    {
        lock (_plugged) return _plugged.GetValueOrDefault((vendor, product)) > 0;
    }

    private void Change(PadKind kind, int by)
    {
        if (IdsOf(kind) is not { } ids)
        {
            return;
        }

        lock (_plugged)
        {
            var count = Math.Max(0, _plugged.GetValueOrDefault(ids) + by);
            _plugged[ids] = count;
        }
    }
}
