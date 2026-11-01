using Arcadia.ControllerSupport.Copy;
using Microsoft.Extensions.Time.Testing;

namespace Arcadia.Core.Tests;

/// <summary>A firmware update Steam tries through the copy (#295): one notice per attempt, a later attempt gets its own.</summary>
public sealed class FirmwareAttemptWatchTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly List<string> _log = [];
    private readonly FirmwareAttemptWatch _watch;
    private int _attempts;

    public FirmwareAttemptWatchTests()
    {
        _watch = new FirmwareAttemptWatch(_time, _log.Add);
        _watch.Attempted += () => _attempts++;
    }

    [Fact]
    [Trait("TestCase", "T295-2")]
    public void Steams_repeated_messages_of_one_attempt_raise_one_notice()
    {
        for (var i = 0; i < 5; i++)
        {
            _watch.Traffic("Nachricht REBOOT_INTO_BOOTLOADER (0x90)");
            _time.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(1, _attempts);
        Assert.Contains(_log, line => line.Contains("REBOOT_INTO_BOOTLOADER (0x90)", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("TestCase", "T295-2")]
    public void An_attempt_after_a_quiet_while_raises_a_new_notice()
    {
        _watch.Traffic("Nachricht REBOOT_INTO_BOOTLOADER (0x90)");
        _time.Advance(FirmwareAttemptWatch.Quiet - TimeSpan.FromSeconds(1));
        _watch.Traffic("Nachricht REBOOT_INTO_BOOTLOADER (0x90)");
        Assert.Equal(1, _attempts);

        _time.Advance(FirmwareAttemptWatch.Quiet);
        _watch.Traffic("Nachricht REBOOT_INTO_BOOTLOADER (0x90)");

        Assert.Equal(2, _attempts);
    }
}
