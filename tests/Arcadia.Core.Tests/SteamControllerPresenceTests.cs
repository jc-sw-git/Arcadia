using Arcadia.Input;

namespace Arcadia.Core.Tests;

public sealed class SteamControllerPresenceTests
{
    private static readonly GamepadReading Pad = new(1, PadButtons.None, default, default);
    private static readonly GamepadReading SteamController = new(2, PadButtons.None, default, default) { IsSteamController = true };

    [Fact]
    public void Reports_only_changes()
    {
        var presence = new SteamControllerPresence();

        Assert.False(presence.Note([Pad]));
        Assert.True(presence.Note([Pad, SteamController]));
        Assert.True(presence.IsConnected);
        Assert.False(presence.Note([SteamController]));
        Assert.True(presence.Note([Pad]));
        Assert.False(presence.IsConnected);
    }

    [Fact]
    public void Lost_controllers_count_as_gone()
    {
        var presence = new SteamControllerPresence();
        presence.Note([SteamController]);

        Assert.True(presence.Drop());
        Assert.False(presence.IsConnected);
        Assert.False(presence.Drop());
    }
}
