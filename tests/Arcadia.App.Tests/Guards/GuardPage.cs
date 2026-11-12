using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit.Sdk;

namespace Arcadia.App.Tests.Guards;

/// <summary>
/// One page, dialog or settings category the cross-page guards walk (#518), as listed in <see cref="GuardPages"/>.
/// </summary>
/// <param name="Name">How the guards' messages name it, in the UI's words.</param>
/// <param name="Shows">The view models a player sees while it is open; <c>GuardPageListTests</c> fails for one no entry names.</param>
/// <param name="Open">From the library, as Arcadia starts, to the entry, with the gamepad where a player can.</param>
/// <param name="Walk">
/// Moves the focus over what is focusable on it, one state at a time: each yielded name is a state the guards look at,
/// the first one the state right after opening.
/// </param>
/// <param name="Start">
/// Arcadia started with what the entry needs (other launchers, settings or controller parts); null for the default
/// start, whose window the entries share: each one is opened from the library and left with B.
/// </param>
/// <param name="Focused">The focused control; null for the one focused button, the default.</param>
/// <param name="RingsAtLeast">How many buttons with a focus ring each state shows at least, so an empty state can't pass.</param>
/// <param name="NotWalked">Why the guards can't open it yet; null for every entry they walk.</param>
/// <param name="RingFinding">
/// A state whose focus ring is known to be drawn wrong, and what is wrong: the focus ring guard leaves it out until
/// the bug is fixed.
/// </param>
internal sealed record GuardPage(
    string Name,
    IReadOnlyList<Type> Shows,
    Func<ArcadiaWindow, Task>? Open = null,
    Func<ArcadiaWindow, IEnumerable<string>>? Walk = null,
    Func<Task<ArcadiaWindow>>? Start = null,
    Func<ArcadiaWindow, Control?>? Focused = null,
    int RingsAtLeast = 1,
    string? NotWalked = null,
    (string State, string Finding)? RingFinding = null)
{
    public bool IsWalked => NotWalked is null;

    /// <summary>A settings category or the launcher page: rows the player moves through with ▲ ▼.</summary>
    public bool IsSettingsPage { get; init; }

    /// <summary>An entry named for completeness that the guards can't open yet, and why.</summary>
    public static GuardPage NotWalkedYet(string name, IReadOnlyList<Type> shows, string why) => new(name, shows, NotWalked: why);

    /// <summary>The states of <see cref="Walk"/>, or only the opened one.</summary>
    public IEnumerable<string> States(ArcadiaWindow arcadia) => Walk?.Invoke(arcadia) ?? ["offen"];

    /// <summary>The focused control as a player sees it, or null where nothing is focused or it is dimmed (not available here).</summary>
    public Control? FocusedControl(ArcadiaWindow arcadia)
    {
        var focused = Focused is null ? FocusedButton(arcadia, this) : Focused(arcadia);
        return focused is not null && focused.IsEffectivelyEnabled && focused.GetSelfAndVisualAncestors().OfType<Avalonia.Visual>().All(visual => visual.Opacity >= 1)
            ? focused
            : null;
    }

    /// <summary>
    /// The one focused button besides the covers of the grid. Where a dialog lies over a page that keeps its own focus
    /// (a confirmation over the detail page), the one shown by a view model of <paramref name="page"/>.
    /// </summary>
    private static Button? FocusedButton(ArcadiaWindow arcadia, GuardPage page)
    {
        var focused = arcadia.Window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("focused") && !button.Classes.Contains("tile") && RingGeometry.IsShown(arcadia.Window, button))
            .ToList();
        if (focused.Count > 1)
        {
            focused = [.. focused.Where(button => button.GetSelfAndVisualAncestors().OfType<Control>().Any(control => page.Shows.Contains(control.DataContext?.GetType())))];
        }

        return focused switch
        {
            [var one] => one,
            [] => null,
            _ => throw new XunitException($"{focused.Count} Schaltflächen zugleich fokussiert: {string.Join(", ", focused.Select(button => string.Join('.', button.Classes)))}"),
        };
    }

    /// <summary>
    /// The part of <paramref name="control"/> that wears its focus ring (its chrome, or a frame inside it such as an
    /// artwork's) and the ring's shadows; null where the focus shows as a fill instead (a status entry's buttons).
    /// </summary>
    public static (Border Ringed, BoxShadows Ring)? RingOf(ArcadiaWindow arcadia, Control control)
    {
        arcadia.WaitForTransitions();
        // The largest one: a small part inside may wear an outline of its own (a cover's launcher badge).
        var ringed = control.GetSelfAndVisualDescendants().OfType<Border>()
            .Where(border => border.IsEffectivelyVisible && Enumerable.Range(0, border.BoxShadow.Count).Any(i => border.BoxShadow[i] is { Blur: 0, Spread: > 0 }))
            .MaxBy(border => border.Bounds.Width * border.Bounds.Height);
        return ringed is null ? null : (ringed, ringed.BoxShadow);
    }

    public override string ToString() => Name;
}
