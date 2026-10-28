using Arcadia.ControllerSupport.Copy;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>
/// A firmware update Steam tries through the copy (#242, owner's design 2026-10-28): Arcadia releases the real puck only
/// once Steam has been quiet for 5 s plus a 25 s countdown, as releasing mid-attempt could hand it the rest of the update.
/// </summary>
public sealed class FirmwareAttemptWatchTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly FirmwareAttemptWatch _watch;

    public FirmwareAttemptWatchTests() => _watch = new FirmwareAttemptWatch(_time, _log.Add);

    [Fact]
    [Trait("TestCase", "T242-8")]
    public void The_first_traffic_starts_an_attempt_once_and_quiet_turns_into_the_countdown_and_then_Steam_gave_up()
    {
        var started = 0;
        _watch.Started += () => started++;
        Assert.Equal(FirmwareAttemptState.None, _watch.State);

        _watch.Traffic("Nachricht AUDIO_UPDATE_DATA (0xB8)");
        _watch.Traffic("Nachricht AUDIO_UPDATE_DATA (0xB8)");
        Assert.Equal(1, started);
        Assert.Equal(FirmwareAttemptState.Trying, _watch.State);

        _time.Advance(FirmwareAttemptWatch.Quiet);
        Assert.Equal((FirmwareAttemptState.CountingDown, FirmwareAttemptWatch.Countdown), _watch.Now());
        _time.Advance(TimeSpan.FromSeconds(7));
        Assert.Equal((FirmwareAttemptState.CountingDown, TimeSpan.FromSeconds(18)), _watch.Now());

        _time.Advance(TimeSpan.FromSeconds(18));
        Assert.Equal(FirmwareAttemptState.GaveUp, _watch.State);
    }

    [Fact]
    [Trait("TestCase", "T242-8")]
    public void New_traffic_during_the_countdown_starts_the_wait_anew()
    {
        _watch.Traffic("Nachricht FACTORY_RESET (0x86)");
        _time.Advance(FirmwareAttemptWatch.Quiet + TimeSpan.FromSeconds(20));
        Assert.Equal(FirmwareAttemptState.CountingDown, _watch.State);

        _watch.Traffic("Nachricht FACTORY_RESET (0x86)");

        Assert.Equal(FirmwareAttemptState.Trying, _watch.State);
        _time.Advance(FirmwareAttemptWatch.Quiet + TimeSpan.FromSeconds(20));
        Assert.Equal(FirmwareAttemptState.CountingDown, _watch.State);
    }

    [Fact]
    [Trait("TestCase", "T242-23")]
    public void The_end_logs_every_message_that_counted_and_whether_the_player_released_it_and_a_new_attempt_starts_afresh()
    {
        var started = 0;
        _watch.Started += () => started++;
        _watch.Traffic("Nachricht AUDIO_UPDATE_DATA (0xB8)");
        _watch.Traffic("Nachricht AUDIO_UPDATE_DATA (0xB8)");
        _watch.Traffic("Ausgabe unbekannt (0x90)");

        _watch.End("vom Spieler: Steam hat das Update abgebrochen");

        Assert.Equal(FirmwareAttemptState.None, _watch.State);
        Assert.Contains("Pan: Firmware-Versuch beendet (vom Spieler: Steam hat das Update abgebrochen); als Update gewertet: Nachricht AUDIO_UPDATE_DATA (0xB8) ×2, Ausgabe unbekannt (0x90) ×1", _log);

        _watch.Traffic("Nachricht AUDIO_UPDATE_DATA (0xB8)");
        Assert.Equal(2, started);
    }
}
