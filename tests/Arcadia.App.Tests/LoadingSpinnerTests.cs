using Arcadia.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Arcadia.App.Tests;

/// <summary>#636: a spinner turns only while it is seen; a hidden one kept the window drawing every frame at idle.</summary>
public sealed class LoadingSpinnerTests
{
    [AvaloniaFact]
    [Trait("TestCase", "T636-3")]
    public void A_spinner_turns_only_while_it_is_seen()
    {
        var spinner = new LoadingSpinner();
        var panel = new Panel { Children = { spinner } };
        var window = new Window { Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(spinner.Classes.Contains("turning"), "Ein sichtbarer Kreisel dreht sich nicht.");

        panel.IsVisible = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(spinner.Classes.Contains("turning"), "Ein Kreisel in einem versteckten Feld dreht sich weiter.");

        panel.IsVisible = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(spinner.Classes.Contains("turning"), "Der Kreisel dreht sich nach dem Wiederzeigen nicht.");

        window.Close();
        Assert.False(spinner.Classes.Contains("turning"), "Ein geschlossener Kreisel dreht sich weiter.");
    }

    [AvaloniaFact]
    [Trait("TestCase", "T680-3")]
    public void A_spinner_stands_still_while_its_window_is_not_in_front()
    {
        var spinner = new LoadingSpinner();
        var window = new Window { Content = spinner };
        window.Show();
        window.Activate();
        Dispatcher.UIThread.RunJobs();
        Assert.True(spinner.Classes.Contains("turning"), "Ein sichtbarer Kreisel im vorderen Fenster dreht sich nicht.");

        // Another program comes to the front; the headless platform has no other windows of its own to lose against.
        // Avalonia keeps the platform's activation callbacks internal: reached by reflection, as Windows would call them.
        var deactivated = (Action)window.PlatformImpl!.GetType().GetProperty("Deactivated")!.GetValue(window.PlatformImpl)!;
        deactivated();
        Dispatcher.UIThread.RunJobs();
        Assert.False(window.IsActive, "Arcadias Fenster blieb vorn.");
        Assert.False(spinner.Classes.Contains("turning"), "Ein Kreisel dreht sich weiter, obwohl sein Fenster nicht vorn ist.");

        window.Activate();
        Dispatcher.UIThread.RunJobs();
        Assert.True(spinner.Classes.Contains("turning"), "Der Kreisel dreht sich nicht wieder, als sein Fenster nach vorn kam.");
        window.Close();
    }
}
