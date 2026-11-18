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
}
