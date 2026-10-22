using Arcadia.Core.Updates;
using System.Globalization;

namespace Arcadia.App.Settings;

/// <summary>
/// A piece of the info panel beside the settings rows (#15). Each kind has its own template in MainWindow.axaml, so the
/// theme draws them all alike, whichever category they come from. Texts may name controller buttons (⧉, ≡, [L3]).
/// </summary>
public abstract record InfoBlock;

/// <summary>A paragraph.</summary>
public sealed record InfoText(string Text) : InfoBlock;

/// <summary>A small heading in capitals, e.g. "NACHTEILE".</summary>
public sealed record InfoHeading(string Text) : InfoBlock;

/// <summary>A paragraph that is a point of a list.</summary>
public sealed record InfoBullet(string Text) : InfoBlock;

/// <summary>A box that sets one choice apart from another, e.g. one of the two Steam Controller modes.</summary>
public sealed record InfoCard(string Title, string Text, string Note) : InfoBlock;

/// <summary>A button and what it does, e.g. a line of the mouse mode's buttons.</summary>
public sealed record InfoKey(string Input, string Effect) : InfoBlock;

/// <summary>Something that went wrong, in the danger colour.</summary>
public sealed record InfoError(string Text) : InfoBlock;

/// <summary>
/// A box of its own below the row's description, e.g. "Status" or "So funktioniert es" of Steam Controller Support:
/// facts about the setting that are nothing to set, so they are no rows (owner, 2026-10-22).
/// </summary>
public sealed record InfoSection(string Title, IReadOnlyList<InfoBlock> Blocks) : InfoBlock;

/// <summary>The state of something running: a dot (green while all is well) or a spinner while it starts or stops.</summary>
public sealed record InfoStatus(string Text, bool IsHealthy, bool IsInTransition) : InfoBlock
{
    public bool ShowsDot => !IsInTransition;
}

/// <summary>Small print at the end, e.g. licence and source.</summary>
public sealed record InfoFootnote(string Text) : InfoBlock;

/// <summary>One version in "Was ist neu".</summary>
public sealed record InfoRelease(string VersionText, string DateText, IReadOnlyList<string> Changes) : InfoBlock
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static InfoRelease Of(ChangelogEntry entry) => new(
        $"Version {entry.Version.ToString(3)}",
        entry.PublishedAt is { } date ? date.ToLocalTime().ToString("d. MMMM yyyy", German) : string.Empty,
        entry.Changes.Count > 0 ? entry.Changes : ["Keine Änderungen aufgeführt."]);
}
