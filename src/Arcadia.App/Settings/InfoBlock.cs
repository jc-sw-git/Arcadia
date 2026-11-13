using Arcadia.ControllerSupport;
using Arcadia.Core.Updates;
using System.Globalization;

namespace Arcadia.App.Settings;

/// <summary>
/// A piece of the info panel beside the settings rows (#15). Each kind has its own template in MainWindow.axaml, so the
/// theme draws them all alike, whichever category they come from. Texts may name controller buttons (⧉, ≡, [L3]).
/// </summary>
/// <remarks>
/// Blocks compare by content, lists included: a row given the same info again announces nothing, so a run refreshing
/// the rows doesn't send the info panel back to its top (#547).
/// </remarks>
public abstract record InfoBlock;

/// <summary>A paragraph.</summary>
public sealed record InfoText(string Text) : InfoBlock
{
    /// <summary>A fixed printing for its buttons, for a text about one controller only (#315); null follows the controller in use.</summary>
    public Arcadia.Input.ButtonSymbols? Symbols { get; init; }
}

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
public sealed record InfoSection(string Title, IReadOnlyList<InfoBlock> Blocks) : InfoBlock
{
    public bool Equals(InfoSection? other) => other is not null && Title == other.Title && Blocks.SequenceEqual(other.Blocks);

    public override int GetHashCode() => HashCode.Combine(Title, Blocks.Count);
}

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
    public bool Equals(InfoRelease? other) =>
        other is not null && VersionText == other.VersionText && DateText == other.DateText && Changes.SequenceEqual(other.Changes);

    public override int GetHashCode() => HashCode.Combine(VersionText, DateText, Changes.Count);

    public static InfoRelease Of(ChangelogEntry entry) => new(
        AppTexts.ReleaseVersion(entry.Version.ToString(3)),
        entry.PublishedAt is { } date ? Services.Formatting.LongDate(date) : string.Empty,
        entry.Changes.Count > 0 ? entry.Changes : [AppTexts.InfoNoChangesListed]);
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

    /// <summary>A fixed printing for its buttons, for a note about one controller only (#315).</summary>
    public Arcadia.Input.ButtonSymbols? Symbols { get; init; }

    /// <summary>A note that must be read before switching, e.g. doubled input with Steam's desktop layout (#315).</summary>
    public bool IsBold { get; init; }

    /// <summary>A bold line above the text that names what the note is about, e.g. "Firmware-Updates von Controllern".</summary>
    public string? Title { get; init; }

    public bool HasTitle => !string.IsNullOrEmpty(Title);
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
public sealed record InfoFacts(string Title, IReadOnlyList<InfoFact> Facts) : InfoBlock
{
    public bool Equals(InfoFacts? other) => other is not null && Title == other.Title && Facts.SequenceEqual(other.Facts);

    public override int GetHashCode() => HashCode.Combine(Title, Facts.Count);
}

/// <summary>A launcher as its tab shows it: logo and name.</summary>
/// <param name="IsHighlighted">The launcher the info panel is about.</param>
public sealed record InfoTab(string Name, Services.LauncherBadge Badge, bool IsHighlighted);

/// <summary>
/// The library's tab strip in miniature, in its current order (#136); which tab the library shows doesn't matter here
/// (owner, 2026-10-23).
/// </summary>
public sealed record InfoTabBar(IReadOnlyList<InfoTab> Tabs) : InfoBlock
{
    public bool Equals(InfoTabBar? other) => other is not null && Tabs.SequenceEqual(other.Tabs);

    public override int GetHashCode() => Tabs.Count;

    public bool IsEmpty => Tabs.Count == 0;
}

/// <summary>A launcher's logo, name and state at the head of its info, e.g. "Aktiv · Tab 2".</summary>
public sealed record InfoLauncher(string Name, Services.LauncherBadge Badge, string StateText, bool IsActive) : InfoBlock;

/// <summary>
/// A box about one launcher with its logo and name at the top, e.g. what it delivers of "Launcher-Daten" (#538).
/// </summary>
public sealed record InfoLauncherBox(string Name, Services.LauncherBadge Badge, IReadOnlyList<InfoBlock> Blocks) : InfoBlock
{
    public bool Equals(InfoLauncherBox? other) => other is not null && Name == other.Name && Badge == other.Badge && Blocks.SequenceEqual(other.Blocks);

    public override int GetHashCode() => HashCode.Combine(Name, Blocks.Count);
}

/// <summary>Something to type and what it does, e.g. "/steam hollow" – "sucht nur in Steam" (#137).</summary>
public sealed record InfoExample(string Sample, string Effect) : InfoBlock;

/// <summary>
/// An entry of "Letzte Meldungen" (#433): its level's symbol, time, a chip "Meldung" (a toast) or "Status", and its text.
/// </summary>
/// <param name="RepeatsText">"×37" for an error that came again 37 times without a toast of its own (#399); empty: none.</param>
/// <param name="EndText">How a status message ended, e.g. "Behoben 10:38" or "Jetzt Fehler"; empty: still pending.</param>
public sealed record InfoMessage(string TimeText, HealthSeverity Level, bool IsToast, string Text, string RepeatsText = "", string EndText = "") : InfoBlock
{
    public bool HasRepeats => RepeatsText.Length > 0;

    public bool HasEnded => EndText.Length > 0;

    public bool IsInfo => Level == HealthSeverity.Info;

    public bool IsError => Level == HealthSeverity.Error;

    public string KindText => IsToast ? AppTexts.Message : AppTexts.StatusTitle;

    public static InfoMessage Of(Messages.RecentMessage message) =>
        new(Services.Formatting.Clock(message.Time), message.Level, message.Kind == Messages.MessageKind.Toast, message.Text,
            message.Repeats?.Repeats is > 0 and var repeats ? $"×{repeats}" : string.Empty,
            message switch
            {
                { ResolvedAt: { } at } => AppTexts.MessageResolvedAt(Services.Formatting.Clock(at)),
                { ChangedTo: { } level } => AppTexts.LevelNow(LevelName(level)),
                _ => string.Empty,
            });

    private static string LevelName(HealthSeverity level) => level switch
    {
        HealthSeverity.Info => AppTexts.LevelInfo,
        HealthSeverity.Warning => AppTexts.LevelWarning,
        _ => AppTexts.LevelError,
    };
}

/// <summary>A framed paragraph in place of a list that shows nothing, e.g. "Noch keine Meldungen …" (#433).</summary>
public sealed record InfoEmpty(string Text) : InfoBlock;
