using Arcadia.App.Services;
using Arcadia.Core.Library;
using Arcadia.Launchers.Abstractions;

namespace Arcadia.App.ViewModels;

/// <summary>
/// One part of the count line (#141): how many entries of one type, with its icon, and in brackets how many of them are
/// installed, left out when all are. Parts after the first stand behind the header's divider.
/// </summary>
public sealed record TypeCountPart(GameType Type, string TotalText, string InstalledText, bool ShowsInstalled, bool HasDivider)
{
    public string IconKey => GameTypeTexts.IconKey(Type);
}

/// <summary>
/// "331 🎮 (1 ✓) | 2 🔧" beside the tab's name and on the launcher's page (#141): one part per type in the order Spiele,
/// Software, Werkzeuge, Soundtracks, types without entries left out; the tooltip says it in words for the mouse.
/// </summary>
public sealed record TypeCountLine(IReadOnlyList<TypeCountPart> Parts, string Tip)
{
    public static TypeCountLine None { get; } = new([], string.Empty);

    public bool HasParts => Parts.Count > 0;

    public static TypeCountLine Of(IEnumerable<Game> games) => Of(LibraryTypes.Count(games));

    public static TypeCountLine Of(IReadOnlyList<TypeCount> counts)
    {
        var parts = counts
            .Select((count, index) => new TypeCountPart(
                count.Type,
                Formatting.Count(count.Total),
                Formatting.Count(count.Installed),
                !count.AreAllInstalled,
                index > 0))
            .ToList();
        var tip = string.Join(" · ", counts.Select(count => count.AreAllInstalled
            ? GameTypeTexts.Count(count.Type, count.Total)
            : $"{GameTypeTexts.Count(count.Type, count.Total)}, davon {Formatting.Count(count.Installed)} installiert"));
        return new TypeCountLine(parts, tip);
    }
}
