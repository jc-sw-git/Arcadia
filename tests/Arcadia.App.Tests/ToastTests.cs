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
        var toast = new ToastViewModel(time);

        toast.Show("erste");
        time.Advance(ToastViewModel.Duration / 2);
        toast.Show("zweite");
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
        var toast = new ToastViewModel(time);

        toast.Show("bleibt", sticky: true);
        time.Advance(ToastViewModel.Duration * 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("bleibt", toast.Text);
    }
}
