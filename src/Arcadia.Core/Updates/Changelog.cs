using System.Text.RegularExpressions;

namespace Arcadia.Core.Updates;

/// <summary>A published release as GitHub lists it.</summary>
public sealed record PublishedRelease(string Tag, string Body, DateTimeOffset? PublishedAt, bool Prerelease);

/// <summary>The changes of one version, for the changelog shown before updating.</summary>
public sealed record ChangelogEntry(Version Version, DateTimeOffset? PublishedAt, IReadOnlyList<string> Changes);

public static partial class Changelog
{
    /// <summary>
    /// The stable releases after <paramref name="current"/> up to and including
    /// <paramref name="latest"/>, newest first.
    /// </summary>
    public static IReadOnlyList<ChangelogEntry> Between(IEnumerable<PublishedRelease> releases, Version current, Version latest) =>
        releases
            .Where(r => !r.Prerelease)
            .Select(r => (Release: r, Version: ParseVersion(r.Tag)))
            .Where(r => r.Version is not null && r.Version > current && r.Version <= latest)
            .OrderByDescending(r => r.Version)
            .Select(r => new ChangelogEntry(r.Version!, r.Release.PublishedAt, ParseChanges(r.Release.Body)))
            .ToList();

    /// <summary>
    /// The bullet points of a release body, i.e. GitHub's generated "What's Changed" list without
    /// the "by @user in …" suffix. The install hint and source link above it carry no bullets.
    /// </summary>
    public static IReadOnlyList<string> ParseChanges(string body)
    {
        var changes = new List<string>();
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## New Contributors", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            {
                var change = AuthorSuffix().Replace(line[2..], string.Empty).Trim();
                if (change.Length > 0)
                {
                    changes.Add(change);
                }
            }
        }

        return changes;
    }

    /// <summary>Parses <c>v1.2.3</c>; pre-release suffixes are ignored.</summary>
    public static Version? ParseVersion(string tag)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var dash = text.IndexOf('-', StringComparison.Ordinal);
        return Version.TryParse(dash >= 0 ? text[..dash] : text, out var version) ? version : null;
    }

    [GeneratedRegex(@"\s+by @\S+ in https?://\S+$")]
    private static partial Regex AuthorSuffix();
}
