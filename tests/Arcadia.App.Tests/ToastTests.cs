using Arcadia.ControllerSupport;
using Arcadia.App.ViewModels;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.App.Tests;

/// <summary>#370: the toast hides itself through a guarded task, and a newer message is not hidden by an older timer.</summary>
public sealed class ToastTests
{
    [AvaloniaFact]
    public void A_message_hides_after_its_duration_and_a_newer_one_keeps_its_own_time()
    {
        var time = new FakeTimeProvider();
        var toast = new ToastViewModel(time, new Messages.RecentMessages(time, _ => { }));

        toast.Show(HealthSeverity.Info, "erste");
        time.Advance(ToastViewModel.Duration / 2);
        toast.Show(HealthSeverity.Info, "zweite");
        time.Advance(ToastViewModel.Duration / 2);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("zweite", toast.Text);

        time.Advance(ToastViewModel.Duration / 2);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(toast.Text);
    }

    [AvaloniaFact]
    public void A_sticky_message_stays_until_the_next_one()
    {
        var time = new FakeTimeProvider();
        var toast = new ToastViewModel(time, new Messages.RecentMessages(time, _ => { }));

        toast.Show(HealthSeverity.Info, "bleibt", sticky: true);
        time.Advance(ToastViewModel.Duration * 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("bleibt", toast.Text);
    }

    [AvaloniaFact]
    [Trait("TestCase", "T683-10")]
    public void A_message_shows_the_symbol_of_its_level_as_its_entry_in_Letzte_Meldungen()
    {
        var time = new FakeTimeProvider();
        var toast = new ToastViewModel(time, new Messages.RecentMessages(time, _ => { }));

        toast.Show(HealthSeverity.Info, "info");
        Assert.Equal((true, false), (toast.IsInfo, toast.IsError));
        toast.Show(HealthSeverity.Warning, "warnung");
        Assert.Equal((HealthSeverity.Warning, false, false), (toast.Level, toast.IsInfo, toast.IsError));
        toast.Show(HealthSeverity.Error, "fehler");
        Assert.Equal((false, true), (toast.IsInfo, toast.IsError));
    }
}
