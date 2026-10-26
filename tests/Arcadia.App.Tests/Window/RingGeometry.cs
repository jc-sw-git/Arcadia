using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Arcadia.App.Tests;

/// <summary>
/// Where focus rings would be cut off, worked out from the layout instead of the pixels (#180): every visible button is
/// given the focus at once, and its ring (the sharp outer outlines of its <c>BoxShadow</c> from <c>Theme.axaml</c>) must
/// lie inside every ancestor that clips. It finds a clipping parent before anybody focuses the button that runs into
/// it; only the pixel checks (<see cref="ArcadiaWindow.AssertRingAround(Control, string, string)"/>) find a ring that
/// something covers.
/// </summary>
internal static class RingGeometry
{
    /// <summary>Half a pixel for rounding: layout and transforms land on fractions.</summary>
    private const double Tolerance = 0.5;

    /// <summary>
    /// The rings cut off in <paramref name="window"/>, one line each, e.g. "Cover 'Alpha' (tile): rechts 3 px
    /// abgeschnitten von ItemsControl"; empty when every ring shows whole. Returns how many buttons were looked at too.
    /// </summary>
    public static (IReadOnlyList<string> CutOff, int Checked) CutOffRings(Window window, Action waitForTransitions)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("plain") && IsShown(window, button))
            .ToList();

        // All of them focused at once: one wait for the lifts and fades instead of one per button.
        var added = buttons.Where(button => !button.Classes.Contains("focused")).ToList();
        added.ForEach(button => button.Classes.Add("focused"));
        try
        {
            waitForTransitions();
            var cutOff = new List<string>();
            var looked = 0;
            foreach (var button in buttons)
            {
                if (Chrome(button) is not { } chrome || OuterReach(chrome.BoxShadow) is not { } reach)
                {
                    continue;
                }

                var ownBounds = InWindow(window, chrome, new Rect(chrome.Bounds.Size));
                var ring = InWindow(window, chrome, new Rect(chrome.Bounds.Size).Inflate(reach));
                var clips = ClippingAncestors(chrome).ToList();

                // A button cut off itself isn't where the focus can be: scrolled (partly) out of its list's view, or on
                // a neighbour's page beside the detail page.
                if (clips.Any(clip => !Contains(clip.Area(window), ownBounds)))
                {
                    continue;
                }

                looked++;
                foreach (var clip in clips)
                {
                    if (CutSides(clip.Area(window), ring) is { Length: > 0 } sides)
                    {
                        cutOff.Add($"{Describe(button)}: {sides} abgeschnitten von {clip.Visual.GetType().Name}{NameOf(clip.Visual)}");
                        break;
                    }
                }
            }

            return (cutOff, looked);
        }
        finally
        {
            added.ForEach(button => button.Classes.Remove("focused"));
        }
    }

    /// <summary>Fails unless every ring in <paramref name="window"/> shows whole; <paramref name="state"/> names the page state.</summary>
    public static void AssertNoRingCutOff(Window window, Action waitForTransitions, string state, int atLeast = 1)
    {
        var (cutOff, looked) = CutOffRings(window, waitForTransitions);
        Assert.True(cutOff.Count == 0, $"{state}: {cutOff.Count} Rahmen abgeschnitten, z. B. {string.Join("; ", cutOff.Take(3))}");
        Assert.True(looked >= atLeast, $"{state}: nur {looked} Schaltflächen mit Fokusrahmen geprüft");
    }

    private static bool IsShown(Window window, Button button) =>
        button.IsEffectivelyVisible
        && button.GetSelfAndVisualAncestors().OfType<Visual>().All(visual => visual.Opacity > 0)
        && button.TransformToVisual(window) is { } transform
        && new Rect(button.Bounds.Size).TransformToAABB(transform).Intersects(new Rect(window.ClientSize));

    private static Border? Chrome(Button button) =>
        button.GetVisualDescendants().OfType<Border>().FirstOrDefault(border => border.Name == "Chrome" && border.TemplatedParent == button);

    /// <summary>How far the widest sharp outer outline reaches beyond the edges, per side; null without one.</summary>
    private static Thickness? OuterReach(BoxShadows shadows)
    {
        Thickness? reach = null;
        for (var i = 0; i < shadows.Count; i++)
        {
            var shadow = shadows[i];
            if (shadow.IsInset || shadow.Blur != 0 || shadow.Spread <= 0)
            {
                continue;
            }

            var side = new Thickness(
                shadow.Spread - shadow.OffsetX, shadow.Spread - shadow.OffsetY, shadow.Spread + shadow.OffsetX, shadow.Spread + shadow.OffsetY);
            reach = reach is { } so
                ? new Thickness(Math.Max(so.Left, side.Left), Math.Max(so.Top, side.Top), Math.Max(so.Right, side.Right), Math.Max(so.Bottom, side.Bottom))
                : side;
        }

        return reach;
    }

    private static IEnumerable<(Visual Visual, Func<Window, Rect> Area)> ClippingAncestors(Visual visual) =>
        visual.GetVisualAncestors()
            .TakeWhile(ancestor => ancestor is not Window)
            .Where(ancestor => ancestor.ClipToBounds || ancestor.Clip is not null)
            .Select(ancestor => (ancestor, (Func<Window, Rect>)(window => InWindow(window, ancestor, ancestor.Clip?.Bounds ?? new Rect(ancestor.Bounds.Size)))));

    private static Rect InWindow(Window window, Visual visual, Rect local) =>
        visual.TransformToVisual(window) is { } transform ? local.TransformToAABB(transform) : throw new InvalidOperationException($"{visual} steht nicht im Fenster.");

    private static bool Contains(Rect outer, Rect inner) => CutSides(outer, inner).Length == 0;

    private static string CutSides(Rect clip, Rect ring)
    {
        var sides = new List<string>();
        Add("oben", clip.Top - ring.Top);
        Add("unten", ring.Bottom - clip.Bottom);
        Add("links", clip.Left - ring.Left);
        Add("rechts", ring.Right - clip.Right);
        return string.Join(", ", sides);

        void Add(string side, double by)
        {
            if (by > Tolerance)
            {
                sides.Add($"{side} {by:0.#} px");
            }
        }
    }

    private static string NameOf(Visual visual) => visual is Control { Name: { Length: > 0 } name } ? $" #{name}" : "";

    private static string Describe(Button button)
    {
        var texts = button.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text)).Select(text => text.Text).Take(2);
        var classes = string.Join('.', button.Classes.Where(name => name is not ("plain" or "focused") && !name.StartsWith(':')));
        return $"'{string.Join(' ', texts)}' ({classes})";
    }
}
