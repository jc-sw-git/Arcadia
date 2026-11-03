using Arcadia.ControllerSupport;
using Arcadia.Pan;
using static Arcadia.ControllerSupport.DriverInstallStepKind;

namespace Arcadia.Core.Tests;

/// <summary>The progress dialog's bar (#413, T413-11): downloads by their bytes, other steps by their weight, never back.</summary>
public sealed class DriverInstallProgressTests
{
    private static DriverInstallProgress FullRun() =>
        new(new DriverInstallPlan(Prompt: true, Viiper: true, SteamCloses: true, Usbip: true, HidHide: true, HidHideRepair: false, Restarts: false));

    [Fact]
    [Trait("TestCase", "T413-11")]
    public void A_download_s_share_grows_with_its_bytes_and_other_steps_jump_when_they_end()
    {
        var progress = FullRun();
        progress.Start(Prompt);
        Assert.Equal(0, progress.Value);
        progress.Complete(Prompt);
        var afterPrompt = progress.Value;
        Assert.True(afterPrompt > 0, "the prompt's weight once it ends");

        progress.Start(UsbipDownload);
        progress.Advance(UsbipDownload, 0, 4_000_000);
        var atStart = progress.Value;
        progress.Advance(UsbipDownload, 2_000_000, 4_000_000);
        var halfWay = progress.Value;
        progress.Complete(UsbipDownload);

        Assert.Equal(afterPrompt, atStart);
        Assert.True(halfWay > atStart, "bytes move the bar");
        Assert.Equal(halfWay - atStart, progress.Value - halfWay, 6);
    }

    [Fact]
    [Trait("TestCase", "T413-11")]
    public void Over_any_report_sequence_the_bar_never_goes_back_and_is_full_at_the_last_step()
    {
        var progress = FullRun();
        var values = new List<double>();
        void Note() => values.Add(progress.Value);

        progress.Start(Prompt);
        progress.Complete(Prompt);
        Note();
        progress.Start(UsbipDownload);
        progress.Advance(UsbipDownload, 3_000_000, 4_000_000);
        Note();
        // A download that starts over, then one whose size isn't known.
        progress.Advance(UsbipDownload, 0, 4_000_000);
        Note();
        progress.Advance(UsbipDownload, 1_000_000, null);
        Note();
        progress.Complete(UsbipDownload);
        Note();
        // Pan's report repeated and out of order: done before running, running again after done.
        progress.FollowReport(new Dictionary<PanInstallStep, PanStepState> { [PanInstallStep.HidHide] = PanStepState.Done });
        Note();
        progress.FollowReport(new Dictionary<PanInstallStep, PanStepState> { [PanInstallStep.Usbip] = PanStepState.Running, [PanInstallStep.HidHide] = PanStepState.Running });
        Note();
        progress.FollowReport(new Dictionary<PanInstallStep, PanStepState> { [PanInstallStep.Usbip] = PanStepState.Running });
        Note();
        foreach (var kind in progress.Steps.Select(step => step.Kind).ToList())
        {
            progress.Complete(kind);
        }

        progress.End();
        Note();

        Assert.Equal(values.Order(), values);
        Assert.Equal(1, progress.Value, 6);
        Assert.Equal(DriverInstallStepState.Done, progress.StateOf(HidHideInstall));
    }
}
