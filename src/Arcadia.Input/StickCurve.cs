namespace Arcadia.Input;

/// <summary>How far a stick moves the pointer or the wheel in the mouse mode.</summary>
public static class StickCurve
{
    /// <summary>XInput's recommended dead zone for the left stick (7849 of 32767); worn sticks rest inside it.</summary>
    public const double PointerDeadZone = 0.24;

    /// <summary>Larger than the pointer's, so the wheel doesn't creep while the stick is only touched.</summary>
    public const double WheelDeadZone = 0.3;

    /// <summary>The pointer setting's range ("Zeigergeschwindigkeit" 1–9).</summary>
    public const int MinSpeed = 1;

    public const int MaxSpeed = 9;

    /// <summary>The slow pointer (L3) moves at a third of the speed.</summary>
    public const double SlowFactor = 1.0 / 3;

    /// <summary>One wheel notch, as Windows counts it.</summary>
    public const int WheelNotch = 120;

    /// <summary>Notches per second with the stick pushed all the way.</summary>
    private const double WheelNotchesPerSecond = 12;

    /// <summary>
    /// The velocity, in pixels per second (positive: right and down), for a stick position and the
    /// "Zeigergeschwindigkeit" setting. The deflection past the dead zone counts squared: a light
    /// push moves the pointer precisely, a full push fast (the acceleration).
    /// </summary>
    public static (double X, double Y) PointerVelocity(StickPosition stick, int speed, bool slow)
    {
        var top = TopSpeed(speed) * (slow ? SlowFactor : 1);
        var (x, y) = Scaled(stick, PointerDeadZone);
        return (x * top, -y * top);
    }

    /// <summary>The wheel's turn per second (120 per notch; positive: up and right) for a stick position.</summary>
    public static (double Vertical, double Horizontal) WheelVelocity(StickPosition stick)
    {
        var (x, y) = Scaled(stick, WheelDeadZone);
        var top = WheelNotchesPerSecond * WheelNotch;
        return (y * top, x * top);
    }

    /// <summary>Pixels per second at full deflection: 500 at speed 1, 1,500 at 5 (a 1080p screen's width in about 1.3 s), 2,500 at 9.</summary>
    public static double TopSpeed(int speed) => 250 + (250 * Math.Clamp(speed, MinSpeed, MaxSpeed));

    /// <summary>The stick's direction, scaled by its deflection past the dead zone (0–1), squared.</summary>
    private static (double X, double Y) Scaled(StickPosition stick, double deadZone)
    {
        var length = Math.Sqrt((stick.X * stick.X) + (stick.Y * stick.Y));
        if (length <= deadZone)
        {
            return (0, 0);
        }

        // A dead zone around the centre, not per axis, so diagonals aren't snapped to the axes.
        var deflection = Math.Min(1, (length - deadZone) / (1 - deadZone));
        var factor = deflection * deflection / length;
        return (stick.X * factor, stick.Y * factor);
    }
}
