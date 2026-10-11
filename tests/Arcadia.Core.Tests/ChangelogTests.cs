using Arcadia.Core.Updates;

namespace Arcadia.Core.Tests;

public class ChangelogTests
{
    private const string Body = """
        **Installation:** `Arcadia-win-Setup.exe` herunterladen und starten.

        Arcadia ist freie Software (GPL-3.0). Quelltext dieser Version: https://github.com/jc-sw-git/Arcadia/tree/v0.2.0

        ## What's Changed
        * Show the changelog before updating by @jc-sw-git in https://github.com/jc-sw-git/Arcadia/pull/70
        * Fix sorting by install date by @someone in https://github.com/jc-sw-git/Arcadia/pull/71

        ## New Contributors
        * @someone made their first contribution in https://github.com/jc-sw-git/Arcadia/pull/71

        **Full Changelog**: https://github.com/jc-sw-git/Arcadia/compare/v0.1.0...v0.2.0
        """;

    [Fact]
    public void Changes_are_the_bullets_without_author_and_link()
    {
        Assert.Equal(
            ["Show the changelog before updating", "Fix sorting by install date"],
            Changelog.ParseChanges(Body));
    }

    [Fact]
    public void Only_stable_versions_after_the_current_up_to_the_latest_newest_first()
    {
        PublishedRelease Release(string tag, bool pre = false) => new(tag, "* " + tag, null, pre);

        var entries = Changelog.Between(
            [Release("v0.1.0"), Release("v0.1.1"), Release("v0.2.0-beta.1", pre: true), Release("v0.2.0"), Release("v0.3.0")],
            current: new Version(0, 1, 0),
            latest: new Version(0, 2, 0));

        Assert.Equal([new Version(0, 2, 0), new Version(0, 1, 1)], entries.Select(e => e.Version));
        Assert.Equal(["v0.2.0"], entries[0].Changes);
    }
}
