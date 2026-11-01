using Arcadia.Input;
using Arcadia.Pan;

namespace Arcadia.ControllerSupport.Pan;

/// <summary>
/// What the player chose for a controller model in "Controller zuordnen" (#238 D3a): the kind its pads are, or
/// <see cref="NotTakenOver"/> (Pan leaves it visible, it reaches games directly).
/// </summary>
public readonly record struct ControllerLayout(PadKind? Kind)
{
    public static readonly ControllerLayout NotTakenOver = new((PadKind?)null);

    public bool IsTakenOver => Kind is not null;

    public static ControllerLayout Of(PadKind kind) => new(kind);
}

/// <summary>A controller model connected now, for "Controller zuordnen": one row per model (D3b).</summary>
public sealed record ConnectedModel(ControllerModel Model, string Name, PadKind DefaultKind);

/// <summary>
/// The stored choices of "Controller zuordnen", by model (<c>AppSettings.ControllerLayouts</c>: "054C:0CE6" →
/// "DualSense" or "NotTakenOver"). Read from the settings file, so anything unknown counts as no choice.
/// </summary>
public static class ControllerLayouts
{
    public const string NotTakenOverValue = "NotTakenOver";

    public static string Key(ControllerModel model) => model.ToString();

    /// <summary>The model's choice, or null: its default (<see cref="PadKinds.Default"/>).</summary>
    public static ControllerLayout? Find(IReadOnlyDictionary<string, string>? stored, ControllerModel model)
    {
        if (stored is null || !stored.TryGetValue(Key(model), out var value))
        {
            return null;
        }

        if (value == NotTakenOverValue)
        {
            return ControllerLayout.NotTakenOver;
        }

        // A kind's exact name only (no numbers, no lists): an edited file can't name a kind that doesn't exist.
        return Enum.GetNames<PadKind>().Contains(value, StringComparer.Ordinal) ? ControllerLayout.Of(Enum.Parse<PadKind>(value)) : null;
    }

    public static string Value(ControllerLayout layout) => layout.Kind is { } kind ? kind.ToString() : NotTakenOverValue;

    /// <summary>
    /// What a model may be, in the order ◀ ▶ cycles them (designs T238Modal): the four kinds, the Edge only for a real
    /// Edge (its paddles need its report), then "Nicht übernehmen".
    /// </summary>
    public static IReadOnlyList<ControllerLayout> Choices(PadKind defaultKind) =>
    [
        ControllerLayout.Of(PadKind.Xbox360),
        ControllerLayout.Of(PadKind.DualShock4),
        ControllerLayout.Of(PadKind.DualSense),
        .. defaultKind == PadKind.DualSenseEdge ? [ControllerLayout.Of(PadKind.DualSenseEdge)] : Array.Empty<ControllerLayout>(),
        ControllerLayout.Of(PadKind.Switch2Pro),
        ControllerLayout.NotTakenOver,
    ];

    /// <summary>The kind a taken-over controller's pad is: the choice where it is one of the model's, else the default.</summary>
    public static PadKind KindFor(ControllerLayout? chosen, PadKind defaultKind) =>
        chosen is { Kind: { } kind } && Choices(defaultKind).Contains(ControllerLayout.Of(kind)) ? kind : defaultKind;

    /// <summary>What a model is now, as the settings show it: "Nicht übernehmen", or the kind its pads are.</summary>
    public static ControllerLayout Effective(ControllerLayout? chosen, PadKind defaultKind) =>
        chosen is { IsTakenOver: false } ? ControllerLayout.NotTakenOver : ControllerLayout.Of(KindFor(chosen, defaultKind));

    /// <summary>The choice's name as the settings show it.</summary>
    public static string DisplayName(ControllerLayout layout) => layout.Kind is { } kind ? PadKinds.DisplayName(kind) : "Nicht übernehmen";
}
