using Arcadia.ControllerSupport;
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

/// <summary>An account's picture and name, e.g. the Steam avatar after the sign-in (#280).</summary>
public sealed record InfoAvatar(string Name, Avalonia.Media.Imaging.Bitmap Picture) : InfoBlock;

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

/// <summary>
/// A status message in a box above the rest, with its level's symbol and colour: e.g. "Nicht gefunden" a warning,
/// "Keine Spiele gefunden" information (owner, 2026-10-24).
/// </summary>
/// <summary>A warning that must not be missed, in bold on red (#242: no firmware update may run before a takeover).</summary>
public sealed record InfoDanger(string Title, string Text) : InfoBlock;

public sealed record InfoNote(string Text, HealthSeverity Severity = HealthSeverity.Warning) : InfoBlock
{
    public bool IsInfo => Severity == HealthSeverity.Info;
}

/// <summary>A fact in a line: what on the left, its value on the right, e.g. "Such-Kürzel  /epic".</summary>
/// <param name="IsMuted">Something missing or off, e.g. "Aus" or "–".</param>
/// <param name="IsGood">Something that works as it should, e.g. "werden überwacht".</param>
/// <param name="Counts">A count line in place of <paramref name="Value"/> (#141), which then is its tooltip's text.</param>
public sealed record InfoFact(string Label, string Value, bool IsMuted = false, bool IsGood = false, ViewModels.TypeCountLine? Counts = null)
{
    public bool HasCounts => Counts is { HasParts: true };
}

/// <summary>Facts under a small heading, line by line, e.g. "BIBLIOTHEK" of a launcher (#136).</summary>
public sealed record InfoFacts(string Title, IReadOnlyList<InfoFact> Facts) : InfoBlock;

/// <summary>A launcher as its tab shows it: logo and name.</summary>
/// <param name="IsHighlighted">The launcher the info panel is about.</param>
public sealed record InfoTab(string Name, Services.LauncherBadge Badge, bool IsHighlighted);

/// <summary>
/// The library's tab strip in miniature, in its current order (#136); which tab the library shows doesn't matter here
/// (owner, 2026-10-23).
/// </summary>
public sealed record InfoTabBar(IReadOnlyList<InfoTab> Tabs) : InfoBlock
{
    public bool IsEmpty => Tabs.Count == 0;
}

/// <summary>A launcher's logo, name and state at the head of its info, e.g. "Aktiv · Tab 2".</summary>
public sealed record InfoLauncher(string Name, Services.LauncherBadge Badge, string StateText, bool IsActive) : InfoBlock;

/// <summary>Something to type and what it does, e.g. "/steam hollow" – "sucht nur in Steam" (#137).</summary>
public sealed record InfoExample(string Sample, string Effect) : InfoBlock;
