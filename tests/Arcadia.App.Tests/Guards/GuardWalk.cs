using Xunit.Sdk;

namespace Arcadia.App.Tests.Guards;

/// <summary>Runs a guard's check on every walked entry of <see cref="GuardPages"/> and reports all that fail at once.</summary>
internal static class GuardWalk
{
    /// <summary>B presses that bring any entry back to the library (the launcher page lies two deep in the settings).</summary>
    private const int BacksToLibrary = 6;

    /// <summary>
    /// <paramref name="check"/> on each state of each walked entry <paramref name="applies"/> to; on the entries
    /// <paramref name="walks"/> is false for, on the opened state only. Fails with every entry that failed, so one broken
    /// page hides no other. Entries on the default start share its window, which starts again after one failed.
    /// </summary>
    public static async Task EveryAsync(Func<GuardPage, bool> applies, Action<ArcadiaWindow, GuardPage, string> check, Func<GuardPage, bool>? walks = null)
    {
        var failed = new List<string>();
        var looked = 0;
        ArcadiaWindow? shared = null;
        try
        {
            foreach (var page in GuardPages.All.Where(page => page.IsWalked && applies(page)))
            {
                var own = page.Start is not null ? await page.Start() : null;
                try
                {
                    var arcadia = own ?? (shared ??= await ArcadiaWindow.ShowAsync());
                    if (page.Open is not null)
                    {
                        await page.Open(arcadia);
                    }

                    foreach (var state in page.States(arcadia))
                    {
                        check(arcadia.Settle(), page, $"{page.Name}, {state}");
                        looked++;
                        if (walks?.Invoke(page) == false)
                        {
                            break;
                        }
                    }

                    if (own is null)
                    {
                        BackToLibrary(arcadia);
                    }
                }
                catch (Exception error) when (error is XunitException or InvalidOperationException)
                {
                    failed.Add($"{page.Name}: {error.Message}");
                    if (own is null)
                    {
                        shared?.Dispose();
                        shared = null;
                    }
                }
                finally
                {
                    own?.Dispose();
                }
            }
        }
        finally
        {
            shared?.Dispose();
        }

        Assert.True(failed.Count == 0, string.Join("\n", failed));
        Assert.True(looked > 0, "Kein Eintrag der Seitenliste geprüft");
    }

    private static void BackToLibrary(ArcadiaWindow arcadia)
    {
        var main = arcadia.Arcadia.Main;
        for (var press = 0; press < BacksToLibrary && (main.IsDialogOpen || main.Search is not null || main.Detail is not null); press++)
        {
            arcadia.Press(Input.UiAction.Back);
        }

        if (main.IsDialogOpen || main.Search is not null || main.Detail is not null)
        {
            throw new InvalidOperationException($"Nach {BacksToLibrary}× B nicht zurück in der Bibliothek");
        }
    }
}
